using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace dcArca.Core.Services;

public sealed class FileSystemApiKeyStore : IApiKeyStore
{
    private readonly string _filePath;

    public FileSystemApiKeyStore(string? directory = null)
    {
        var resolvedDirectory = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dcArca")
            : Path.GetFullPath(directory);
        Directory.CreateDirectory(resolvedDirectory);
        TryHardenDirectory(resolvedDirectory);
        _filePath = Path.Combine(resolvedDirectory, "api_keys.json");
    }

    public Task<(ApiKeyRecord Record, string RawKey)> CreateAsync(
        string name, IReadOnlyCollection<string> scopes, CancellationToken cancellationToken = default)
        => CreateInternalAsync(name, scopes, consumerId: null, contextGrants: null, cancellationToken);

    public Task<(ApiKeyRecord Record, string RawKey)> CreateForConsumerAsync(
        string name,
        string consumerId,
        IReadOnlyCollection<string> scopes,
        IReadOnlyCollection<ApiKeyContextGrant> contextGrants,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
            throw new ArgumentException("consumerId es obligatorio.", nameof(consumerId));
        return CreateInternalAsync(name, scopes, consumerId.Trim(), NormalizeGrants(contextGrants), cancellationToken);
    }

    private async Task<(ApiKeyRecord Record, string RawKey)> CreateInternalAsync(
        string name,
        IReadOnlyCollection<string> scopes,
        string? consumerId,
        ApiKeyContextGrant[]? contextGrants,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la API key no puede estar vacío.", nameof(name));

        var normalizedScopes = NormalizeScopes(scopes);
        var rawKey = "sk-arca-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var record = new ApiKeyRecord(
            "key_" + Guid.NewGuid().ToString("N"),
            name.Trim(),
            Hash(rawKey),
            normalizedScopes,
            true,
            DateTimeOffset.UtcNow,
            ConsumerId: consumerId,
            ContextGrants: contextGrants);

        var coordinator = new dcWsaaFileCacheCoordinator(_filePath);
        await using (await coordinator.AcquireAsync(cancellationToken))
        {
            var records = await ReadAllAsync(cancellationToken);
            records.Add(record);
            WriteAll(coordinator, records);
        }
        return (record, rawKey);
    }

    public async Task<ApiKeyRecord?> ValidateAsync(string rawKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawKey)) return null;

        var coordinator = new dcWsaaFileCacheCoordinator(_filePath);
        await using (await coordinator.AcquireAsync(cancellationToken))
        {
            var records = await ReadAllAsync(cancellationToken);
            var candidateHash = Hash(rawKey);
            var index = records.FindIndex(record => record.Active &&
                CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(record.KeyHash), Convert.FromHexString(candidateHash)));
            if (index < 0) return null;

            var used = records[index] with { LastUsedAt = DateTimeOffset.UtcNow };
            records[index] = used;
            WriteAll(coordinator, records);
            return used;
        }
    }

    public async Task<bool> RevokeAsync(string id, CancellationToken cancellationToken = default)
    {
        var coordinator = new dcWsaaFileCacheCoordinator(_filePath);
        await using (await coordinator.AcquireAsync(cancellationToken))
        {
            var records = await ReadAllAsync(cancellationToken);
            var index = records.FindIndex(record => record.Id == id && record.Active);
            if (index < 0) return false;
            records[index] = records[index] with { Active = false, RevokedAt = DateTimeOffset.UtcNow };
            WriteAll(coordinator, records);
            return true;
        }
    }

    public async Task<bool> SetConsumerAndGrantsAsync(
        string id,
        string consumerId,
        IReadOnlyCollection<ApiKeyContextGrant> contextGrants,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
            throw new ArgumentException("consumerId es obligatorio.", nameof(consumerId));
        var normalized = NormalizeGrants(contextGrants);

        var coordinator = new dcWsaaFileCacheCoordinator(_filePath);
        await using (await coordinator.AcquireAsync(cancellationToken))
        {
            var records = await ReadAllAsync(cancellationToken);
            var index = records.FindIndex(record => record.Id == id);
            if (index < 0) return false;
            records[index] = records[index] with
            {
                ConsumerId = consumerId.Trim(),
                ContextGrants = normalized
            };
            WriteAll(coordinator, records);
            return true;
        }
    }

    public async Task<IReadOnlyList<ApiKeyRecord>> ListAsync(CancellationToken cancellationToken = default)
        => await ReadAllAsync(cancellationToken);

    private async Task<List<ApiKeyRecord>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath)) return [];
        await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        List<ApiKeyRecord> records;
        try
        {
            records = await JsonSerializer.DeserializeAsync<List<ApiKeyRecord>>(
                stream, cancellationToken: cancellationToken) ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("El store de API keys contiene JSON inválido.", exception);
        }

        if (records.Any(record => !IsValidRecord(record)))
            throw new InvalidDataException("El store de API keys contiene registros inválidos.");

        return records;
    }

    private static string[] NormalizeScopes(IReadOnlyCollection<string> scopes)
    {
        var normalized = scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0)
            throw new ArgumentException("La API key debe tener al menos un scope.", nameof(scopes));
        return normalized;
    }

    private static ApiKeyContextGrant[] NormalizeGrants(IReadOnlyCollection<ApiKeyContextGrant> grants)
    {
        if (grants is null) throw new ArgumentNullException(nameof(grants));
        return grants
            .Where(grant => grant is not null && !string.IsNullOrWhiteSpace(grant.ContextId))
            .GroupBy(grant => grant.ContextId.Trim(), StringComparer.Ordinal)
            .Select(group => new ApiKeyContextGrant(
                group.Key,
                group.SelectMany(x => x.Operations ?? Array.Empty<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()))
            .Where(grant => grant.Operations.Length > 0)
            .OrderBy(grant => grant.ContextId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsValidRecord(ApiKeyRecord? record)
        => record is not null
            && !string.IsNullOrWhiteSpace(record.Id)
            && !string.IsNullOrWhiteSpace(record.Name)
            && record.KeyHash is { Length: 64 }
            && record.KeyHash.All(Uri.IsHexDigit)
            && record.Scopes is { Length: > 0 }
            && record.Scopes.All(scope => !string.IsNullOrWhiteSpace(scope))
            && (record.ConsumerId is null || !string.IsNullOrWhiteSpace(record.ConsumerId))
            && (record.ContextGrants is null || record.ContextGrants.All(grant =>
                grant is not null
                && !string.IsNullOrWhiteSpace(grant.ContextId)
                && grant.Operations is { Length: > 0 }
                && grant.Operations.All(operation => !string.IsNullOrWhiteSpace(operation))));

    private void WriteAll(dcWsaaFileCacheCoordinator coordinator, List<ApiKeyRecord> records)
    {
        coordinator.WriteAllTextAtomic(JsonSerializer.Serialize(records));
        TryHardenFile(_filePath);
    }

    private static string Hash(string rawKey)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();

    private static void TryHardenDirectory(string directory)
    {
        if (OperatingSystem.IsWindows()) return;
        try { File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        catch (Exception) when (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) { }
    }

    private static void TryHardenFile(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch (Exception) when (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) { }
    }
}
