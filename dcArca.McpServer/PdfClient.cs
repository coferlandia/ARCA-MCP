using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace dcArca.McpServer;

public sealed record PdfTemplateReference(string Key, string Version)
{
    [Obsolete("Id representa una referencia lógica; usar Key.")]
    public string Id => Key;

    public bool IsLegacyProviderId
        => Key.StartsWith("tpl_", StringComparison.Ordinal);
}

public interface IPdfClient
{
    void ValidateConfiguration();
    Task<byte[]> RenderAsync(PdfTemplateReference template, JsonElement data, CancellationToken cancellationToken = default);
}

public sealed class PdfClient : IPdfClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IPdfTemplateResolver _templateResolver;

    public PdfClient(HttpClient httpClient, IConfiguration configuration)
        : this(httpClient, configuration, new CreadorPdfTemplateResolver(httpClient, configuration))
    {
    }

    [ActivatorUtilitiesConstructor]
    public PdfClient(
        HttpClient httpClient,
        IConfiguration configuration,
        IPdfTemplateResolver templateResolver)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _templateResolver = templateResolver;
    }

    public void ValidateConfiguration()
    {
        if (_httpClient.BaseAddress is null)
            throw new InvalidOperationException("Falta configurar Pdf:BaseUrl.");

        var apiKey = _configuration["Pdf:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Falta configurar Pdf:ApiKey.");
    }

    public async Task<byte[]> RenderAsync(
        PdfTemplateReference template,
        JsonElement data,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        ArgumentNullException.ThrowIfNull(template);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var physicalTemplateId = await _templateResolver.ResolveAsync(template, cancellationToken);
            try
            {
                return await RenderWithPhysicalIdAsync(
                    physicalTemplateId,
                    template.Version,
                    data,
                    cancellationToken);
            }
            catch (CreadorPdfException exception) when (
                attempt == 0
                && !template.IsLegacyProviderId
                && exception.FailureKind == PdfFailureKind.TemplateNotFound
                && string.Equals(exception.ProviderErrorCode, "unknown_template", StringComparison.OrdinalIgnoreCase))
            {
                _templateResolver.Invalidate(template);
            }
        }

        throw new InvalidOperationException("El retry acotado de resolución de template terminó sin resultado.");
    }

    private async Task<byte[]> RenderWithPhysicalIdAsync(
        string physicalTemplateId,
        string version,
        JsonElement data,
        CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Pdf:ApiKey"]!;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/documents")
        {
            Content = JsonContent.Create(new { template = new { id = physicalTemplateId, version }, data })
        };
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

            if (!string.Equals(
                    response.Content.Headers.ContentType?.MediaType,
                    "application/pdf",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new CreadorPdfException(
                    PdfFailureKind.InvalidResponse,
                    PdfFailureContract.ToSafeMessage(PdfFailureKind.InvalidResponse),
                    (int)response.StatusCode);
            }

            return await response.Content.ReadAsByteArrayAsync(effectiveToken);
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
    }
}
