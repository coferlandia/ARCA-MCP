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

    public async Task<(ApiKeyRecord Record, string RawKey)> CreateAsync(
        string name, IReadOnlyCollection<string> scopes, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la API key no puede estar vacío.", nameof(name));

        var normalizedScopes = scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (normalizedScopes.Length == 0)
            throw new ArgumentException("La API key debe tener al menos un scope.", nameof(scopes));

        var rawKey = "sk-arca-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var record = new ApiKeyRecord(
            "key_" + Guid.NewGuid().ToString("N"), name.Trim(), Hash(rawKey), normalizedScopes,
            true, DateTimeOffset.UtcNow);

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
            var index = records.FindIndex(record => record.Active &&
                CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(record.KeyHash), Convert.FromHexString(Hash(rawKey))));
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

    public async Task<IReadOnlyList<ApiKeyRecord>> ListAsync(CancellationToken cancellationToken = default)
        => await ReadAllAsync(cancellationToken);

    private async Task<List<ApiKeyRecord>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath)) return [];
        await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<List<ApiKeyRecord>>(stream, cancellationToken: cancellationToken) ?? [];
    }

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
