using System.Text.Json;
using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public class EmissionStoreMigrationTests
{
    [Fact]
    public async Task DryRun_InventariaSinMutar()
    {
        using var temp = new TempStore();
        var legacyKeyHash = new string('a', 64);
        WriteLegacy(temp.StorePath, legacyKeyHash, EmissionIdempotencyState.Uncertain, numero: 42);

        var result = await EmissionStoreMigration.MigrateAsync(
            temp.StorePath,
            Mapping(),
            dryRun: true);

        Assert.True(result.DryRun);
        Assert.Equal(1, result.TotalRecords);
        Assert.Equal(1, result.LegacyRecords);
        Assert.Equal(1, result.PendingRecords);
        Assert.True(File.Exists(Path.Combine(temp.StorePath, legacyKeyHash + ".json")));
        Assert.False(Directory.Exists(Path.Combine(temp.StorePath, ".contexts")));
    }

    [Fact]
    public async Task Apply_MigraNamespaceYPreservaPendiente_SegundaEjecucionEsIdempotente()
    {
        using var temp = new TempStore();
        var legacyKeyHash = new string('a', 64);
        WriteLegacy(temp.StorePath, legacyKeyHash, EmissionIdempotencyState.Uncertain, numero: 42);
        var backup = Path.Combine(temp.RootPath, "backup");
        var mapping = Mapping();

        var first = await EmissionStoreMigration.MigrateAsync(
            temp.StorePath,
            mapping,
            dryRun: false,
            backupDirectory: backup);

        var migratedKeyHash = EmissionRequestFingerprint.OperationKeyHashFromLegacyKeyHash(
            mapping.ConsumerId,
            mapping.ContextId,
            legacyKeyHash);
        var store = new FileSystemEmissionIdempotencyStore(temp.StorePath);
        var migrated = await store.GetAsync(migratedKeyHash);

        Assert.False(first.DryRun);
        Assert.Equal(1, first.LegacyRecords);
        Assert.Equal(backup, first.BackupDirectory);
        Assert.True(Directory.Exists(backup));
        Assert.False(File.Exists(Path.Combine(temp.StorePath, legacyKeyHash + ".json")));
        Assert.NotNull(migrated);
        Assert.Equal(EmissionIdempotencyState.Uncertain, migrated!.State);
        Assert.Equal(42, migrated.NumeroComprobante);
        Assert.Equal("consumer-a", migrated.Identity.ConsumerId);
        Assert.Equal("ctx-a", migrated.Identity.ContextId);
        Assert.Equal(FiscalEvidenceState.LegacyUnavailable, migrated.FiscalEvidence.State);
        Assert.Null(migrated.FiscalEvidence.Projection);

        var second = await EmissionStoreMigration.MigrateAsync(
            temp.StorePath,
            mapping,
            dryRun: false);

        Assert.Equal(0, second.LegacyRecords);
        Assert.Equal(1, second.CurrentRecords);
        Assert.Null(second.BackupDirectory);
        Assert.NotNull(await new FileSystemEmissionIdempotencyStore(temp.StorePath).GetAsync(migratedKeyHash));
    }

    [Fact]
    public async Task MappingConOtroPuntoVenta_FallaSinMutar()
    {
        using var temp = new TempStore();
        var legacyKeyHash = new string('a', 64);
        WriteLegacy(temp.StorePath, legacyKeyHash, EmissionIdempotencyState.Created, numero: null);
        var wrong = Mapping() with { PuntoVenta = 8 };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            EmissionStoreMigration.MigrateAsync(temp.StorePath, wrong, dryRun: true));

        Assert.True(File.Exists(Path.Combine(temp.StorePath, legacyKeyHash + ".json")));
    }

    [Fact]
    public async Task RegistroTerminal_PreservaCaeYNumero()
    {
        using var temp = new TempStore();
        var legacyKeyHash = new string('a', 64);
        WriteLegacy(
            temp.StorePath,
            legacyKeyHash,
            EmissionIdempotencyState.Authorized,
            numero: 99,
            fiscalResult: new StoredFiscalResult(
                true,
                "CAE99",
                "20261020",
                99,
                "A",
                "OK",
                null,
                dcEmissionOutcome.Authorized,
                [],
                []));
        var mapping = Mapping();

        await EmissionStoreMigration.MigrateAsync(
            temp.StorePath,
            mapping,
            dryRun: false,
            backupDirectory: Path.Combine(temp.RootPath, "backup"));

        var migratedKeyHash = EmissionRequestFingerprint.OperationKeyHashFromLegacyKeyHash(
            mapping.ConsumerId,
            mapping.ContextId,
            legacyKeyHash);
        var migrated = await new FileSystemEmissionIdempotencyStore(temp.StorePath).GetAsync(migratedKeyHash);

        Assert.NotNull(migrated);
        Assert.Equal(99, migrated!.NumeroComprobante);
        Assert.Equal("CAE99", migrated.FiscalResult!.Cae);
    }

    private static LegacyEmissionStoreMapping Mapping() => new(
        "consumer-a",
        "ctx-a",
        "homologacion",
        20123456786,
        7,
        1,
        "cred-v1");

    private static void WriteLegacy(
        string directory,
        string keyHash,
        EmissionIdempotencyState state,
        long? numero,
        StoredFiscalResult? fiscalResult = null)
    {
        var record = new
        {
            KeyHash = keyHash,
            RequestHash = new string('b', 64),
            TipoComprobante = (int)dcTipoComprobante.FacturaB,
            PuntoVenta = 7,
            NumeroComprobante = numero,
            State = state,
            FiscalResult = fiscalResult,
            CreatedAt = DateTimeOffset.Parse("2026-10-01T00:00:00+00:00"),
            UpdatedAt = DateTimeOffset.Parse("2026-10-01T00:00:00+00:00")
        };
        File.WriteAllText(Path.Combine(directory, keyHash + ".json"), JsonSerializer.Serialize(record));
    }

    private sealed class TempStore : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "arca-migration-tests-" + Guid.NewGuid().ToString("N"));
        public string StorePath => Path.Combine(RootPath, "store");

        public TempStore()
        {
            Directory.CreateDirectory(StorePath);
        }

        public void Dispose()
        {
            try { Directory.Delete(RootPath, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
