using System.Text;
using System.Text.Json;
using dcArca.Core.Models;

namespace dcArca.McpServer;

public enum EmissionIdempotencyState
{
    Created,
    NumberAssigned,
    Submitting,
    Authorized,
    FiscalRejected,
    Uncertain
}

public sealed record StoredFiscalResult(
    bool Success,
    string Cae,
    string CaeVencimiento,
    long NumeroComprobante,
    string Resultado,
    string Mensaje,
    string? Codigo,
    dcEmissionOutcome EmissionOutcome,
    string[] Observaciones,
    string[] Errores)
{
    public static StoredFiscalResult FromResponse(dcFacturaResponse response) => new(
        response.Success,
        response.Cae,
        response.CaeVencimiento,
        response.NumeroComprobante,
        response.Resultado,
        response.Mensaje,
        response.Codigo,
        response.EmissionOutcome,
        response.Observaciones.ToArray(),
        response.Errores.ToArray());

    public dcFacturaResponse ToResponse() => new()
    {
        Success = Success,
        Cae = Cae,
        CaeVencimiento = CaeVencimiento,
        NumeroComprobante = NumeroComprobante,
        Resultado = Resultado,
        Mensaje = Mensaje,
        Codigo = Codigo,
        EmissionOutcome = EmissionOutcome,
        Observaciones = Observaciones.ToList(),
        Errores = Errores.ToList()
    };
}

public sealed record EmissionIdempotencyRecord(
    string KeyHash,
    string RequestHash,
    int TipoComprobante,
    int PuntoVenta,
    long? NumeroComprobante,
    EmissionIdempotencyState State,
    StoredFiscalResult? FiscalResult,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class EmissionIdempotencyConflictException : Exception
{
    public EmissionIdempotencyConflictException()
        : base("IDEMPOTENCY_KEY_REUSED") { }
}

public interface IEmissionIdempotencyStore
{
    Task<EmissionIdempotencyRecord> GetOrCreateAsync(
        string keyHash,
        string requestHash,
        dcTipoComprobante tipoComprobante,
        int puntoVenta,
        CancellationToken cancellationToken = default);

    Task<EmissionIdempotencyRecord?> GetAsync(
        string keyHash,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        EmissionIdempotencyRecord record,
        CancellationToken cancellationToken = default);
}

public sealed class FileSystemEmissionIdempotencyStore : IEmissionIdempotencyStore
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(25);
    private readonly string _directory;

    public FileSystemEmissionIdempotencyStore(string? directory = null)
    {
        _directory = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dcArca", "emission-idempotency")
            : Path.GetFullPath(directory);
        Directory.CreateDirectory(_directory);
        TryHardenDirectory(_directory);
    }

    public async Task<EmissionIdempotencyRecord> GetOrCreateAsync(
        string keyHash,
        string requestHash,
        dcTipoComprobante tipoComprobante,
        int puntoVenta,
        CancellationToken cancellationToken = default)
    {
        ValidateHash(keyHash, nameof(keyHash));
        ValidateHash(requestHash, nameof(requestHash));
        var path = RecordPath(keyHash);

        await using var handle = await AcquireAsync(path, cancellationToken);
        if (File.Exists(path))
        {
            var existing = await ReadUnlockedAsync(path, cancellationToken);
            EnsureRequestMatches(existing, requestHash);
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var created = new EmissionIdempotencyRecord(
            keyHash,
            requestHash,
            (int)tipoComprobante,
            puntoVenta,
            null,
            EmissionIdempotencyState.Created,
            null,
            now,
            now);
        WriteAtomic(path, created);
        return created;
    }

    public async Task<EmissionIdempotencyRecord?> GetAsync(
        string keyHash,
        CancellationToken cancellationToken = default)
    {
        ValidateHash(keyHash, nameof(keyHash));
        var path = RecordPath(keyHash);
        await using var handle = await AcquireAsync(path, cancellationToken);
        if (!File.Exists(path)) return null;
        return await ReadUnlockedAsync(path, cancellationToken);
    }

    public async Task SaveAsync(
        EmissionIdempotencyRecord record,
        CancellationToken cancellationToken = default)
    {
        ValidateRecord(record);
        var path = RecordPath(record.KeyHash);
        await using var handle = await AcquireAsync(path, cancellationToken);

        if (!File.Exists(path))
            throw new InvalidDataException("El registro de idempotencia desapareció durante una operación fiscal.");

        var existing = await ReadUnlockedAsync(path, cancellationToken);
        EnsureRequestMatches(existing, record.RequestHash);
        if (!string.Equals(existing.KeyHash, record.KeyHash, StringComparison.Ordinal))
            throw new InvalidDataException("El registro de idempotencia no corresponde a la clave esperada.");

        WriteAtomic(path, record with { CreatedAt = existing.CreatedAt });
    }

    private string RecordPath(string keyHash) => Path.Combine(_directory, keyHash + ".json");

    private static async ValueTask<FileStream> AcquireAsync(string path, CancellationToken cancellationToken)
    {
        var lockPath = path + ".lock";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.None);
            }
            catch (IOException)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    private static async Task<EmissionIdempotencyRecord> ReadUnlockedAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var record = await JsonSerializer.DeserializeAsync<EmissionIdempotencyRecord>(stream, cancellationToken: cancellationToken);
            if (record is null)
                throw new InvalidDataException("El store de idempotencia contiene un registro vacío.");
            ValidateRecord(record);
            return record;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("El store de idempotencia contiene JSON inválido.", exception);
        }
    }

    private static void WriteAtomic(string path, EmissionIdempotencyRecord record)
    {
        ValidateRecord(record);
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(record), new UTF8Encoding(false));
            File.Move(tempPath, path, overwrite: true);
            TryHardenFile(path);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static void EnsureRequestMatches(EmissionIdempotencyRecord existing, string requestHash)
    {
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            throw new EmissionIdempotencyConflictException();
    }

    private static void ValidateRecord(EmissionIdempotencyRecord record)
    {
        if (record is null) throw new InvalidDataException("Registro de idempotencia nulo.");
        ValidateHash(record.KeyHash, nameof(record.KeyHash));
        ValidateHash(record.RequestHash, nameof(record.RequestHash));
        if (record.TipoComprobante <= 0 || record.PuntoVenta <= 0)
            throw new InvalidDataException("Registro de idempotencia con identidad fiscal inválida.");
        if (record.NumeroComprobante is <= 0)
            throw new InvalidDataException("Registro de idempotencia con número inválido.");
        if ((record.State is EmissionIdempotencyState.Authorized or EmissionIdempotencyState.FiscalRejected)
            && record.FiscalResult is null)
            throw new InvalidDataException("Registro terminal de idempotencia sin resultado fiscal.");
    }

    private static void ValidateHash(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || !value.All(Uri.IsHexDigit))
            throw new ArgumentException("Se esperaba un hash SHA-256 hexadecimal.", parameterName);
    }

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
