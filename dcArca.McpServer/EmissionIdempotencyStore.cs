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
    Uncertain,
    AbandonedBeforeSubmission
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

public sealed record PreSubmissionAbandonmentAudit(
    string ActorReference,
    string ReasonCode,
    DateTimeOffset AbandonedAt);

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
    public PreSubmissionAbandonmentAudit? AbandonmentAudit { get; init; }
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

public sealed class PreSubmissionAbandonmentException : Exception
{
    public string Code { get; }

    public PreSubmissionAbandonmentException(string code)
        : base(code) => Code = code;
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

    Task<EmissionIdempotencyRecord> AbandonBeforeSubmissionAsync(
        string keyHash,
        long? reservedNumber,
        PreSubmissionAbandonmentAudit audit,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("PRE_SUBMISSION_ABANDONMENT_UNSUPPORTED");

    Task<EmissionIdempotencyRecord> CaptureAbandonedReservationAsync(
        string keyHash,
        long reservedNumber,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("ABANDONED_RESERVATION_CAPTURE_UNSUPPORTED");
}

public sealed class FileSystemEmissionIdempotencyStore : IEmissionIdempotencyStore
{
    public const int LegacyOperationSchemaVersion = 2;
    public const int ProvenanceOperationSchemaVersion = 3;
    public const int CurrentSchemaVersion = 4;
    public const int CurrentContextSchemaVersion = 2;
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
        // V2 technical contexts may represent multiple CUIT/PV. Protect each immutable
        // fiscal identity separately rather than permanently binding a ContextId to one CUIT/PV.
        var path = ContextPath($"{context.ContextId}:{context.Environment}:{context.Cuit}:{context.PuntoVenta}");
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
            CurrentContextSchemaVersion,
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

    public async Task<EmissionIdempotencyRecord> AbandonBeforeSubmissionAsync(
        string keyHash,
        long? reservedNumber,
        PreSubmissionAbandonmentAudit audit,
        CancellationToken cancellationToken = default)
    {
        ValidateHash(keyHash, nameof(keyHash));
        ValidateAbandonmentAudit(audit);
        if (reservedNumber is <= 0)
            throw new ArgumentOutOfRangeException(nameof(reservedNumber));

        var path = RecordPath(keyHash);
        await using var handle = await AcquireAsync(path, cancellationToken);
        if (!File.Exists(path))
            throw new PreSubmissionAbandonmentException("OPERATION_NOT_FOUND");

        var existing = await ReadUnlockedAsync(path, cancellationToken);
        if (existing.State == EmissionIdempotencyState.AbandonedBeforeSubmission)
            return existing;

        if (existing.State is not (EmissionIdempotencyState.Created or EmissionIdempotencyState.NumberAssigned))
            throw new PreSubmissionAbandonmentException("OPERATION_ABANDON_NOT_ALLOWED");

        if (existing.NumeroComprobante.HasValue
            && reservedNumber.HasValue
            && existing.NumeroComprobante.Value != reservedNumber.Value)
            throw new PreSubmissionAbandonmentException("OPERATION_RESERVATION_MISMATCH");

        if (existing.State == EmissionIdempotencyState.NumberAssigned
            && !existing.NumeroComprobante.HasValue)
            throw new InvalidDataException("NumberAssigned sin número durable.");

        var abandoned = existing with
        {
            SchemaVersion = CurrentSchemaVersion,
            NumeroComprobante = existing.NumeroComprobante ?? reservedNumber,
            State = EmissionIdempotencyState.AbandonedBeforeSubmission,
            FiscalResult = null,
            UpdatedAt = audit.AbandonedAt,
            AbandonmentAudit = audit
        };
        ValidateRecord(abandoned);
        WriteAtomic(path, abandoned);
        return abandoned;
    }

    public async Task<EmissionIdempotencyRecord> CaptureAbandonedReservationAsync(
        string keyHash,
        long reservedNumber,
        CancellationToken cancellationToken = default)
    {
        ValidateHash(keyHash, nameof(keyHash));
        if (reservedNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(reservedNumber));

        var path = RecordPath(keyHash);
        await using var handle = await AcquireAsync(path, cancellationToken);
        if (!File.Exists(path))
            throw new PreSubmissionAbandonmentException("OPERATION_NOT_FOUND");

        var existing = await ReadUnlockedAsync(path, cancellationToken);
        if (existing.State != EmissionIdempotencyState.AbandonedBeforeSubmission)
            throw new PreSubmissionAbandonmentException("OPERATION_ABANDON_NOT_ALLOWED");
        if (existing.NumeroComprobante.HasValue)
        {
            if (existing.NumeroComprobante.Value != reservedNumber)
                throw new PreSubmissionAbandonmentException("OPERATION_RESERVATION_MISMATCH");
            return existing;
        }

        var repaired = existing with
        {
            SchemaVersion = CurrentSchemaVersion,
            NumeroComprobante = reservedNumber,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        ValidateRecord(repaired);
        WriteAtomic(path, repaired);
        return repaired;
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
        => state is EmissionIdempotencyState.Authorized
            or EmissionIdempotencyState.FiscalRejected
            or EmissionIdempotencyState.AbandonedBeforeSubmission;

    private static bool FiscalEvidenceEquals(StoredFiscalEvidence left, StoredFiscalEvidence right)
        => string.Equals(
            JsonSerializer.Serialize(left),
            JsonSerializer.Serialize(right),
            StringComparison.Ordinal);

    public static void ValidateRecord(EmissionIdempotencyRecord record)
    {
        if (record is null) throw new InvalidDataException("Registro de idempotencia nulo.");
        if (record.SchemaVersion is not (LegacyOperationSchemaVersion
            or ProvenanceOperationSchemaVersion
            or CurrentSchemaVersion))
            throw new InvalidDataException($"STORE_SCHEMA_UNSUPPORTED:{record.SchemaVersion}");
        ValidateIdentity(record.Identity);
        if (record.SchemaVersion == LegacyOperationSchemaVersion
            && record.Identity.RepresentationProvenance is not null)
            throw new InvalidDataException("STORE_SCHEMA_PROVENANCE_REQUIRES_V3");
        if (record.SchemaVersion == ProvenanceOperationSchemaVersion
            && record.Identity.RepresentationProvenance is null)
            throw new InvalidDataException("STORE_SCHEMA_V3_REQUIRES_PROVENANCE");
        if (record.SchemaVersion < CurrentSchemaVersion
            && (record.State == EmissionIdempotencyState.AbandonedBeforeSubmission
                || record.AbandonmentAudit is not null))
            throw new InvalidDataException("STORE_SCHEMA_ABANDONMENT_REQUIRES_V4");
        if (!Enum.IsDefined(record.State))
            throw new InvalidDataException("Registro de idempotencia con estado desconocido.");
        ValidateHash(record.KeyHash, nameof(record.KeyHash));
        ValidateHash(record.RequestHash, nameof(record.RequestHash));
        if (record.RequestCanonicalizationVersion <= 0)
            throw new InvalidDataException("Registro con versión de canonicalización inválida.");
        ValidateFiscalEvidence(record.FiscalEvidence);
        if (record.NumeroComprobante is <= 0)
            throw new InvalidDataException("Registro de idempotencia con número inválido.");
        if ((record.State is EmissionIdempotencyState.Authorized or EmissionIdempotencyState.FiscalRejected)
            && record.FiscalResult is null)
            throw new InvalidDataException("Registro terminal de idempotencia sin resultado fiscal.");
        if (record.State == EmissionIdempotencyState.AbandonedBeforeSubmission)
        {
            if (record.AbandonmentAudit is null)
                throw new InvalidDataException("Operación abandonada sin auditoría durable.");
            if (record.FiscalResult is not null)
                throw new InvalidDataException("Operación abandonada no puede contener resultado fiscal.");
            ValidateAbandonmentAudit(record.AbandonmentAudit);
        }
        else if (record.AbandonmentAudit is not null)
        {
            throw new InvalidDataException("Auditoría de abandono presente en un estado incompatible.");
        }
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
        if (manifest.SchemaVersion != CurrentContextSchemaVersion)
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
            || identity.ContextRevision <= 0
            || (identity.RepresentationProvenance is { } provenance
                && (provenance.CreatedAt == default
                    || provenance.ActivatedAt == default
                    || provenance.ActivatedAt < provenance.CreatedAt)))
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

    private static void ValidateAbandonmentAudit(PreSubmissionAbandonmentAudit audit)
    {
        if (audit is null || audit.AbandonedAt == default)
            throw new InvalidDataException("Auditoría de abandono inválida.");
        ValidateAuditToken(audit.ActorReference, "actorReference", 128);
        ValidateAuditToken(audit.ReasonCode, "reasonCode", 96);
    }

    private static void ValidateAuditToken(string value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > maxLength
            || value.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or ':' or '/')))
            throw new InvalidDataException($"Auditoría de abandono con {field} inválido.");
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
