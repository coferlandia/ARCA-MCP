using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;

namespace dcArca.McpServer;

public interface IPdfTemplateResolver
{
    Task<string> ResolveAsync(PdfTemplateReference template, CancellationToken cancellationToken = default);
    void Invalidate(PdfTemplateReference template);
}

public sealed class CreadorPdfTemplateResolver : IPdfTemplateResolver
{
    private const int MaxCacheEntries = 128;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _resolutionLock = new(1, 1);

    public CreadorPdfTemplateResolver(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> ResolveAsync(PdfTemplateReference template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.IsLegacyProviderId)
            return template.Key;

        ValidateConfiguration();
        var cacheKey = CacheKey(template);
        if (TryGetCached(cacheKey, out var cached))
            return cached;

        await _resolutionLock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetCached(cacheKey, out cached))
                return cached;

            var resolved = await ResolveRemoteAsync(template, cancellationToken);
            if (_cache.Count >= MaxCacheEntries)
                _cache.Clear();
            _cache[cacheKey] = new CacheEntry(resolved, DateTimeOffset.UtcNow.Add(CacheTtl()));
            return resolved;
        }
        finally
        {
            _resolutionLock.Release();
        }
    }

    public void Invalidate(PdfTemplateReference template)
    {
        if (template is null || template.IsLegacyProviderId)
            return;
        _cache.TryRemove(CacheKey(template), out _);
    }

    private async Task<string> ResolveRemoteAsync(PdfTemplateReference template, CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Pdf:ApiKey"]!;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/templates/managed");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var deadline = CreadorPdfHttpDeadline.Start(_httpClient.Timeout, cancellationToken);
        var effectiveToken = deadline.Token;

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                effectiveToken);

            if (!response.IsSuccessStatusCode)
                throw await CreadorPdfHttpErrors.FromResponseAsync(response, effectiveToken);

            await using var stream = await response.Content.ReadAsStreamAsync(effectiveToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: effectiveToken);
            var items = EnumerateItems(document.RootElement);
            var candidates = items
                .Where(item => string.Equals(GetString(item, "name"), template.Key, StringComparison.Ordinal)
                               && string.Equals(GetString(item, "version"), template.Version, StringComparison.Ordinal))
                .ToArray();

            var published = candidates
                .Where(item => string.Equals(GetString(item, "status"), "published", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (published.Length == 0)
            {
                var kind = candidates.Length > 0
                    ? PdfFailureKind.TemplateNotPublished
                    : PdfFailureKind.TemplateNotFound;
                throw new CreadorPdfException(kind, PdfFailureContract.ToSafeMessage(kind));
            }

            if (published.Length > 1)
                throw new CreadorPdfException(
                    PdfFailureKind.TemplateAmbiguous,
                    PdfFailureContract.ToSafeMessage(PdfFailureKind.TemplateAmbiguous));

            var id = GetString(published[0], "id");
            if (string.IsNullOrWhiteSpace(id))
                throw new CreadorPdfException(
                    PdfFailureKind.InvalidResponse,
                    PdfFailureContract.ToSafeMessage(PdfFailureKind.InvalidResponse));

            return id;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CreadorPdfException(
                PdfFailureKind.Timeout,
                PdfFailureContract.ToSafeMessage(PdfFailureKind.Timeout),
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new CreadorPdfException(
                PdfFailureKind.Unavailable,
                PdfFailureContract.ToSafeMessage(PdfFailureKind.Unavailable),
                exception.StatusCode.HasValue ? (int)exception.StatusCode.Value : null,
                innerException: exception);
        }
        catch (JsonException exception)
        {
            throw new CreadorPdfException(
                PdfFailureKind.InvalidResponse,
                PdfFailureContract.ToSafeMessage(PdfFailureKind.InvalidResponse),
                innerException: exception);
        }
    }

    private static IEnumerable<JsonElement> EnumerateItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().Select(item => item.Clone()).ToArray();

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array)
        {
            return items.EnumerateArray().Select(item => item.Clone()).ToArray();
        }

        throw new JsonException("Respuesta de templates inesperada.");
    }

    private static string? GetString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(propertyName, out var property)
           && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private bool TryGetCached(string key, out string id)
    {
        if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            id = entry.Id;
            return true;
        }

        _cache.TryRemove(key, out _);
        id = string.Empty;
        return false;
    }

    private TimeSpan CacheTtl()
    {
        if (int.TryParse(_configuration["Pdf:TemplateResolutionCacheSeconds"], out var seconds))
            return TimeSpan.FromSeconds(Math.Clamp(seconds, 30, 3600));
        return DefaultTtl;
    }

    private void ValidateConfiguration()
    {
        if (_httpClient.BaseAddress is null)
            throw new InvalidOperationException("Falta configurar Pdf:BaseUrl.");
        if (string.IsNullOrWhiteSpace(_configuration["Pdf:ApiKey"]))
            throw new InvalidOperationException("Falta configurar Pdf:ApiKey.");
    }

    private static string CacheKey(PdfTemplateReference template)
        => $"{CreadorPdfException.ProviderId}\n{template.Key}\n{template.Version}";

    private sealed record CacheEntry(string Id, DateTimeOffset ExpiresAt);
}
