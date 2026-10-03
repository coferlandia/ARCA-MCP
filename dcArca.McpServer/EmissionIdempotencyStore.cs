using System.Security.Cryptography;
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

public enum FiscalEvidenceState
{
    Complete,
    LegacyUnavailable
}

public sealed record StoredFiscalEvidence(
    int ProjectionVersion,
    FiscalEvidenceState State,
    FiscalRequestProjection? Projection)
{
    public static StoredFiscalEvidence FromRequest(dcFacturaRequest request)
        => new(
            EmissionRequestFingerprint.FiscalProjectionVersion,
            FiscalEvidenceState.Complete,
            EmissionRequestFingerprint.Project(request));

    public static StoredFiscalEvidence LegacyUnavailable()
        => new(EmissionRequestFingerprint.FiscalProjectionVersion, FiscalEvidenceState.LegacyUnavailable, null);
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
    int SchemaVersion,
    string KeyHash,
    string RequestHash,
    int RequestCanonicalizationVersion,
    FiscalOperationIdentity Identity,
    StoredFiscalEvidence FiscalEvidence,
    long? NumeroComprobante,
    EmissionIdempotencyState State,
    StoredFiscalResult? FiscalResult,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public int TipoComprobante => Identity.TipoComprobante;
    public int PuntoVenta => Identity.PuntoVenta;
}

public sealed record FiscalContextManifest(
    int SchemaVersion,
    string ContextId,
    string Environment,
    long Cuit,
    int PuntoVenta,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class EmissionIdempotencyConflictException : Exception
{
    public EmissionIdempotencyConflictException()
        : base("IDEMPOTENCY_KEY_REUSED") { }
}

public sealed class EmissionTerminalStateConflictException : Exception
{
    public EmissionIdempotencyRecord Existing { get; }

    public EmissionTerminalStateConflictException(EmissionIdempotencyRecord existing)
        : base("EMISSION_TERMINAL_STATE_ALREADY_PERSISTED")
        => Existing = existing;
}

public interface IEmissionIdempotencyStore
{
    Task EnsureContextAsync(
        FiscalContextDescriptor context,
        CancellationToken cancellationToken = default);

    Task<EmissionIdempotencyRecord> GetOrCreateAsync(
        string keyHash,
        string requestHash,
        int requestCanonicalizationVersion,
        FiscalOperationIdentity identity,
        StoredFiscalEvidence fiscalEvidence,
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
    public const int CurrentSchemaVersion = 2;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(25);
    private readonly string _directory;

    public FileSystemEmissionIdempotencyStore(string? directory = null)
    {
        _directory = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dcArca", "emission-idempotency")
            : Path.GetFullPath(directory);
        Directory.CreateDirectory(_directory);
        TryHardenDirectory(_directory);
        Directory.CreateDirectory(ContextsDirectory);
        TryHardenDirectory(ContextsDirectory);
    }

    public string DirectoryPath => _directory;

    private string ContextsDirectory => Path.Combine(_directory, ".contexts");

    public async Task EnsureContextAsync(
        FiscalContextDescriptor context,
        CancellationToken cancellationToken = default)
    {
        ValidateContext(context);
        var path = ContextPath(context.ContextId);
        await using var handle = await AcquireAsync(path, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (File.Exists(path))
        {
            FiscalContextManifest existing;
            try
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                existing = await JsonSerializer.DeserializeAsync<FiscalContextManifest>(stream, cancellationToken: cancellationToken)
                    ?? throw new InvalidDataException("El manifiesto de contexto fiscal está vacío.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("El manifiesto de contexto fiscal contiene JSON inválido.", exception);
            }

            ValidateContextManifest(existing);
            if (!string.Equals(existing.ContextId, context.ContextId, StringComparison.Ordinal)
                || !string.Equals(existing.Environment, context.Environment, StringComparison.Ordinal)
                || existing.Cuit != context.Cuit
                || existing.PuntoVenta != context.PuntoVenta)
            {
                throw new InvalidDataException("FISCAL_CONTEXT_IDENTITY_MISMATCH");
            }
            return;
        }

        WriteAtomic(path, new FiscalContextManifest(
            CurrentSchemaVersion,
            context.ContextId,
            context.Environment,
            context.Cuit,
            context.PuntoVenta,
            now,
            now));
    }

    public async Task<EmissionIdempotencyRecord> GetOrCreateAsync(
        string keyHash,
        string requestHash,
        int requestCanonicalizationVersion,
        FiscalOperationIdentity identity,
        StoredFiscalEvidence fiscalEvidence,
        CancellationToken cancellationToken = default)
    {
        ValidateHash(keyHash, nameof(keyHash));
        ValidateHash(requestHash, nameof(requestHash));
        ValidateIdentity(identity);
        ValidateFiscalEvidence(fiscalEvidence);
        if (requestCanonicalizationVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestCanonicalizationVersion));

        await EnsureContextAsync(identity.Context, cancellationToken);
        var path = RecordPath(keyHash);

        await using var handle = await AcquireAsync(path, cancellationToken);
        if (File.Exists(path))
        {
            var existing = await ReadUnlockedAsync(path, cancellationToken);
            EnsureOperationMatches(existing, requestHash, requestCanonicalizationVersion, identity);
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var created = new EmissionIdempotencyRecord(
            CurrentSchemaVersion,
            keyHash,
            requestHash,
            requestCanonicalizationVersion,
            identity,
            fiscalEvidence,
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
        EnsureOperationMatches(
            existing,
            record.RequestHash,
            record.RequestCanonicalizationVersion,
            record.Identity);
        if (existing.Identity != record.Identity)
            throw new InvalidDataException("La identidad congelada de la operación no puede modificarse.");
        if (!FiscalEvidenceEquals(existing.FiscalEvidence, record.FiscalEvidence))
            throw new InvalidDataException("La evidencia fiscal congelada de la operación no puede modificarse.");

        if (IsTerminal(existing.State))
        {
            // The first durable terminal result is authoritative, including its fiscal result
            // metadata. Every later writer must observe that exact winner instead of silently
            // replacing it or assuming its own same-state result was persisted.
            throw new EmissionTerminalStateConflictException(existing);
        }

        WriteAtomic(path, record with
        {
            SchemaVersion = CurrentSchemaVersion,
            CreatedAt = existing.CreatedAt
        });
    }

    private string RecordPath(string keyHash) => Path.Combine(_directory, keyHash + ".json");

    private string ContextPath(string contextId)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contextId))).ToLowerInvariant();
        return Path.Combine(ContextsDirectory, hash + ".json");
    }

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

    private static void WriteAtomic<T>(string path, T value)
    {
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(value), new UTF8Encoding(false));
            File.Move(tempPath, path, overwrite: true);
            TryHardenFile(path);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static void EnsureOperationMatches(
        EmissionIdempotencyRecord existing,
        string requestHash,
        int requestCanonicalizationVersion,
        FiscalOperationIdentity identity)
    {
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal)
            || existing.RequestCanonicalizationVersion != requestCanonicalizationVersion
            || !existing.Identity.MatchesImmutableIdentity(identity))
        {
            throw new EmissionIdempotencyConflictException();
        }
    }

    private static bool IsTerminal(EmissionIdempotencyState state)
        => state is EmissionIdempotencyState.Authorized or EmissionIdempotencyState.FiscalRejected;

    private static bool FiscalEvidenceEquals(StoredFiscalEvidence left, StoredFiscalEvidence right)
        => string.Equals(
            JsonSerializer.Serialize(left),
            JsonSerializer.Serialize(right),
            StringComparison.Ordinal);

    public static void ValidateRecord(EmissionIdempotencyRecord record)
    {
        if (record is null) throw new InvalidDataException("Registro de idempotencia nulo.");
        if (record.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"STORE_SCHEMA_UNSUPPORTED:{record.SchemaVersion}");
        ValidateHash(record.KeyHash, nameof(record.KeyHash));
        ValidateHash(record.RequestHash, nameof(record.RequestHash));
        if (record.RequestCanonicalizationVersion <= 0)
            throw new InvalidDataException("Registro con versión de canonicalización inválida.");
        ValidateIdentity(record.Identity);
        ValidateFiscalEvidence(record.FiscalEvidence);
        if (record.NumeroComprobante is <= 0)
            throw new InvalidDataException("Registro de idempotencia con número inválido.");
        if ((record.State is EmissionIdempotencyState.Authorized or EmissionIdempotencyState.FiscalRejected)
            && record.FiscalResult is null)
            throw new InvalidDataException("Registro terminal de idempotencia sin resultado fiscal.");
    }

    public static void ValidateContext(FiscalContextDescriptor context)
    {
        if (context is null
            || string.IsNullOrWhiteSpace(context.ContextId)
            || string.IsNullOrWhiteSpace(context.Environment)
            || context.Cuit <= 0
            || context.PuntoVenta <= 0)
        {
            throw new InvalidDataException("Identidad de contexto fiscal inválida.");
        }
    }

    private static void ValidateContextManifest(FiscalContextManifest manifest)
    {
        if (manifest.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"CONTEXT_SCHEMA_UNSUPPORTED:{manifest.SchemaVersion}");
        ValidateContext(new FiscalContextDescriptor(
            manifest.ContextId,
            manifest.Environment,
            manifest.Cuit,
            manifest.PuntoVenta));
    }

    private static void ValidateIdentity(FiscalOperationIdentity identity)
    {
        if (identity is null
            || string.IsNullOrWhiteSpace(identity.ConsumerId)
            || string.IsNullOrWhiteSpace(identity.ContextId)
            || string.IsNullOrWhiteSpace(identity.Environment)
            || string.IsNullOrWhiteSpace(identity.CredentialAssignmentRevision)
            || identity.Cuit <= 0
            || identity.PuntoVenta <= 0
            || identity.TipoComprobante <= 0
            || identity.ContextRevision <= 0)
        {
            throw new InvalidDataException("Registro de idempotencia con identidad fiscal inválida.");
        }
    }

    private static void ValidateFiscalEvidence(StoredFiscalEvidence evidence)
    {
        if (evidence is null || evidence.ProjectionVersion <= 0)
            throw new InvalidDataException("Registro con evidencia fiscal inválida.");
        if (evidence.State == FiscalEvidenceState.Complete && evidence.Projection is null)
            throw new InvalidDataException("Registro con evidencia fiscal completa pero sin proyección.");
        if (evidence.State == FiscalEvidenceState.LegacyUnavailable && evidence.Projection is not null)
            throw new InvalidDataException("Registro legacy no puede declarar una proyección fiscal inexistente.");
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
