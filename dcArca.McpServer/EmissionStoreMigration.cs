using System.Text.Json;
using dcArca.Core.Models;

namespace dcArca.McpServer;

public sealed record LegacyEmissionStoreMapping(
    string ConsumerId,
    string ContextId,
    string Environment,
    long Cuit,
    int PuntoVenta,
    int ContextRevision,
    string CredentialAssignmentRevision)
{
    public FiscalContextDescriptor Context => new(ContextId, Environment, Cuit, PuntoVenta);

    public FiscalOperationIdentity IdentityFor(int tipoComprobante)
        => new(
            ConsumerId,
            ContextId,
            Environment,
            Cuit,
            PuntoVenta,
            tipoComprobante,
            ContextRevision,
            CredentialAssignmentRevision);
}

public sealed record EmissionStoreMigrationResult(
    bool DryRun,
    int TotalRecords,
    int LegacyRecords,
    int CurrentRecords,
    int PendingRecords,
    int TerminalRecords,
    string? BackupDirectory,
    IReadOnlyList<string> Warnings);

public static class EmissionStoreMigration
{
    private sealed record LegacyEmissionIdempotencyRecord(
        string KeyHash,
        string RequestHash,
        int TipoComprobante,
        int PuntoVenta,
        long? NumeroComprobante,
        EmissionIdempotencyState State,
        StoredFiscalResult? FiscalResult,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record InventoryEntry(
        string Path,
        EmissionIdempotencyRecord? Current,
        LegacyEmissionIdempotencyRecord? Legacy);

    public static async Task<EmissionStoreMigrationResult> MigrateAsync(
        string directory,
        LegacyEmissionStoreMapping mapping,
        bool dryRun,
        string? backupDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var source = Path.GetFullPath(directory);
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException($"No existe el store de idempotencia: {source}");

        ValidateMapping(mapping);
        var inventory = await InventoryAsync(source, mapping, cancellationToken);
        var legacyCount = inventory.Count(x => x.Legacy is not null);
        var currentCount = inventory.Count - legacyCount;
        var pendingCount = inventory.Count(x => IsPending(x.Current?.State ?? x.Legacy!.State));
        var terminalCount = inventory.Count - pendingCount;
        var warnings = new List<string>();

        if (inventory.Any(x => x.Legacy?.State is EmissionIdempotencyState.Submitting or EmissionIdempotencyState.Uncertain))
        {
            warnings.Add("Hay operaciones legacy inciertas: se migran bloqueadas y sin evidencia comparable; no deben declararse reconciliadas por número.");
        }

        if (dryRun)
        {
            return new EmissionStoreMigrationResult(
                true,
                inventory.Count,
                legacyCount,
                currentCount,
                pendingCount,
                terminalCount,
                null,
                warnings);
        }

        if (legacyCount == 0)
        {
            var store = new FileSystemEmissionIdempotencyStore(source);
            await store.EnsureContextAsync(mapping.Context, cancellationToken);
            return new EmissionStoreMigrationResult(
                false,
                inventory.Count,
                0,
                currentCount,
                pendingCount,
                terminalCount,
                null,
                warnings);
        }

        var parent = Path.GetDirectoryName(source)
            ?? throw new InvalidOperationException("No se pudo resolver el directorio padre del store.");
        var sourceName = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var backup = Path.GetFullPath(backupDirectory
            ?? Path.Combine(parent, sourceName + ".backup-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss")));
        if (!string.Equals(Path.GetDirectoryName(backup), parent, StringComparison.Ordinal))
            throw new InvalidOperationException("El backup debe ser un directorio hermano del store para permitir rollback por rename.");
        if (Directory.Exists(backup) || File.Exists(backup))
            throw new IOException($"El backup ya existe: {backup}");

        var staging = Path.Combine(parent, sourceName + ".migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            var outputHashes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in inventory)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EmissionIdempotencyRecord output;
                if (entry.Current is not null)
                {
                    output = entry.Current;
                }
                else
                {
                    var legacy = entry.Legacy!;
                    var identity = mapping.IdentityFor(legacy.TipoComprobante);
                    var namespacedKeyHash = EmissionRequestFingerprint.OperationKeyHashFromLegacyKeyHash(
                        mapping.ConsumerId,
                        mapping.ContextId,
                        legacy.KeyHash);
                    output = new EmissionIdempotencyRecord(
                        FileSystemEmissionIdempotencyStore.CurrentSchemaVersion,
                        namespacedKeyHash,
                        legacy.RequestHash,
                        EmissionRequestFingerprint.CanonicalizationVersion,
                        identity,
                        StoredFiscalEvidence.LegacyUnavailable(),
                        legacy.NumeroComprobante,
                        legacy.State,
                        legacy.FiscalResult,
                        legacy.CreatedAt,
                        legacy.UpdatedAt);
                }

                FileSystemEmissionIdempotencyStore.ValidateRecord(output);
                if (!outputHashes.Add(output.KeyHash))
                    throw new InvalidDataException("La migración produciría dos operaciones con el mismo namespace idempotente.");
                var outputPath = Path.Combine(staging, output.KeyHash + ".json");
                await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(output), cancellationToken);
            }

            var stagedStore = new FileSystemEmissionIdempotencyStore(staging);
            await stagedStore.EnsureContextAsync(mapping.Context, cancellationToken);
            foreach (var keyHash in outputHashes)
            {
                if (await stagedStore.GetAsync(keyHash, cancellationToken) is null)
                    throw new InvalidDataException("La verificación de la migración no pudo releer un registro convertido.");
            }

            Directory.Move(source, backup);
            try
            {
                Directory.Move(staging, source);
            }
            catch
            {
                if (!Directory.Exists(source) && Directory.Exists(backup))
                    Directory.Move(backup, source);
                throw;
            }
        }
        catch
        {
            if (Directory.Exists(staging))
            {
                try { Directory.Delete(staging, recursive: true); }
                catch { }
            }
            throw;
        }

        return new EmissionStoreMigrationResult(
            false,
            inventory.Count,
            legacyCount,
            currentCount,
            pendingCount,
            terminalCount,
            backup,
            warnings);
    }

    private static async Task<List<InventoryEntry>> InventoryAsync(
        string source,
        LegacyEmissionStoreMapping mapping,
        CancellationToken cancellationToken)
    {
        var entries = new List<InventoryEntry>();
        foreach (var path in Directory.EnumerateFiles(source, "*.json", SearchOption.TopDirectoryOnly)
                     .Where(IsRecordFile)
                     .Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Registro corrupto: {Path.GetFileName(path)}", exception);
            }

            using (document)
            {
                if (document.RootElement.TryGetProperty("SchemaVersion", out var schemaElement))
                {
                    var schemaVersion = schemaElement.GetInt32();
                    if (schemaVersion != FileSystemEmissionIdempotencyStore.CurrentSchemaVersion)
                        throw new InvalidDataException($"STORE_SCHEMA_UNSUPPORTED:{schemaVersion}");

                    var current = JsonSerializer.Deserialize<EmissionIdempotencyRecord>(json)
                        ?? throw new InvalidDataException($"Registro vacío: {Path.GetFileName(path)}");
                    FileSystemEmissionIdempotencyStore.ValidateRecord(current);
                    EnsureCurrentMappingMatches(current, mapping);
                    entries.Add(new InventoryEntry(path, current, null));
                    continue;
                }
            }

            LegacyEmissionIdempotencyRecord legacy;
            try
            {
                legacy = JsonSerializer.Deserialize<LegacyEmissionIdempotencyRecord>(json)
                    ?? throw new InvalidDataException($"Registro legacy vacío: {Path.GetFileName(path)}");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"Registro legacy inválido: {Path.GetFileName(path)}", exception);
            }

            ValidateLegacy(legacy, path, mapping);
            entries.Add(new InventoryEntry(path, null, legacy));
        }

        return entries;
    }

    private static void ValidateLegacy(
        LegacyEmissionIdempotencyRecord record,
        string path,
        LegacyEmissionStoreMapping mapping)
    {
        var fileHash = Path.GetFileNameWithoutExtension(path);
        if (!string.Equals(fileHash, record.KeyHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("El nombre de archivo legacy no coincide con KeyHash.");
        ValidateHash(record.KeyHash, "KeyHash");
        ValidateHash(record.RequestHash, "RequestHash");
        if (record.TipoComprobante <= 0 || record.PuntoVenta <= 0)
            throw new InvalidDataException("Registro legacy con identidad fiscal inválida.");
        if (record.PuntoVenta != mapping.PuntoVenta)
            throw new InvalidDataException("El mapping no coincide con el punto de venta persistido en el store legacy.");
        if (record.NumeroComprobante is <= 0)
            throw new InvalidDataException("Registro legacy con número inválido.");
        if ((record.State is EmissionIdempotencyState.Authorized or EmissionIdempotencyState.FiscalRejected)
            && record.FiscalResult is null)
            throw new InvalidDataException("Registro legacy terminal sin resultado fiscal.");
    }

    private static void EnsureCurrentMappingMatches(
        EmissionIdempotencyRecord record,
        LegacyEmissionStoreMapping mapping)
    {
        var expected = mapping.IdentityFor(record.TipoComprobante);
        if (!record.Identity.MatchesImmutableIdentity(expected))
            throw new InvalidDataException("El store actual no corresponde al mapping fiscal indicado.");
    }

    private static void ValidateMapping(LegacyEmissionStoreMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.ConsumerId)
            || string.IsNullOrWhiteSpace(mapping.ContextId)
            || string.IsNullOrWhiteSpace(mapping.Environment)
            || string.IsNullOrWhiteSpace(mapping.CredentialAssignmentRevision)
            || mapping.Cuit <= 0
            || mapping.PuntoVenta <= 0
            || mapping.ContextRevision <= 0)
        {
            throw new ArgumentException("El mapping legacy debe declarar consumer/context/ambiente/CUIT/PV y revisiones explícitas.");
        }
        FileSystemEmissionIdempotencyStore.ValidateContext(mapping.Context);
    }

    private static bool IsRecordFile(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.Length == 64 && name.All(Uri.IsHexDigit);
    }

    private static bool IsPending(EmissionIdempotencyState state)
        => state is not (EmissionIdempotencyState.Authorized or EmissionIdempotencyState.FiscalRejected);

    private static void ValidateHash(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || !value.All(Uri.IsHexDigit))
            throw new InvalidDataException($"Registro legacy con {field} inválido.");
    }
}
