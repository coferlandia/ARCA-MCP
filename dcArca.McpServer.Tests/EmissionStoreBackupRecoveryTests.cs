using System.Security.Claims;
using dcArca.Core.Models;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.McpServer.Tests;

public class EmissionStoreBackupRecoveryTests
{
    [Fact]
    public async Task Backup_RejectsLiveWriter()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Store);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Store);
        await coordinator.InitializeAsync(store);

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            EmissionStoreBackupRecovery.CreateBackupAsync(temp.Store, temp.Backup));

        Assert.Equal("SERIES_WRITER_BUSY", exception.Message);
        Assert.False(Directory.Exists(temp.Backup));
    }

    [Fact]
    public async Task Restore_RejectsRunningHostRecoveryLease()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Store);
        await SaveAuthorizedAsync(store, "consumer-a", "ctx-a", "terminal", 9);
        await EmissionStoreBackupRecovery.CreateBackupAsync(temp.Store, temp.Backup);
        var gate = new FileSystemEmissionRecoveryGate(temp.Recovery);
        await using var runtimeLease = gate.AcquireRuntimeLease();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            EmissionStoreBackupRecovery.RestoreAsync(temp.Backup, temp.Store, gate));

        Assert.Equal("RECOVERY_RUNTIME_ACTIVE", exception.Message);
        Assert.Null(await gate.GetBlockAsync());
        Assert.NotNull(await new FileSystemEmissionIdempotencyStore(temp.Store).GetAsync(
            EmissionRequestFingerprint.OperationKeyHash("consumer-a", "ctx-a", "terminal")));
    }

    [Fact]
    public async Task Restore_OlderBackupBlocksNewEmissionUntilEvidenceCompletion()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Store);
        await SaveAuthorizedAsync(store, "consumer-a", "ctx-a", "before-backup", 10);
        var manifest = await EmissionStoreBackupRecovery.CreateBackupAsync(temp.Store, temp.Backup);

        // Simulate an authorization performed after the backup. A later restore will lose it.
        await SaveAuthorizedAsync(store, "consumer-a", "ctx-a", "after-backup", 11);
        var afterKey = EmissionRequestFingerprint.OperationKeyHash("consumer-a", "ctx-a", "after-backup");
        Assert.NotNull(await store.GetAsync(afterKey));

        var gate = new FileSystemEmissionRecoveryGate(temp.Recovery);
        var restore = await EmissionStoreBackupRecovery.RestoreAsync(temp.Backup, temp.Store, gate);

        Assert.Equal(manifest.BackupId, restore.BackupId);
        Assert.NotNull(await gate.GetBlockAsync());
        var restoredStore = new FileSystemEmissionIdempotencyStore(temp.Store);
        Assert.Null(await restoredStore.GetAsync(afterKey));

        var contexts = new FileSystemRepresentedFiscalContextStore(temp.Contexts);
        await contexts.AddContextAsync(Context());
        var materializer = new FakeMaterializer();
        var resolver = new FiscalContextRuntimeResolver(
            contexts,
            restoredStore,
            new SingleFiscalContextOptions("consumer-a", "ctx-a", "homologacion", 1, "cred-v1"),
            materializer,
            gate);

        var exception = await Assert.ThrowsAsync<FiscalContextAccessException>(() =>
            resolver.ResolveForEmissionAsync(
                Principal(),
                "ctx-a",
                "new-operation",
                dcTipoComprobante.FacturaB));

        Assert.Equal("RESTORE_RECONCILIATION_REQUIRED", exception.Code);
        Assert.Equal(0, materializer.Calls);

        var completion = await gate.CompleteAsync(
            "ops-ticket-2026-10-03-restore-window-reconciled",
            "integration-test",
            "Consumer/ARCA evidence reconciled through the restore window.");
        Assert.Equal(restore.RestoreId, completion.RestoreId);
        Assert.Null(await gate.GetBlockAsync());

        using var runtime = await resolver.ResolveForEmissionAsync(
            Principal(),
            "ctx-a",
            "new-operation",
            dcTipoComprobante.FacturaB);
        Assert.Equal(1, materializer.Calls);
        Assert.Equal("ctx-a", runtime.Context.ContextId);
    }

    [Fact]
    public async Task Restore_PreservesTerminalAndPendingRecordsAndRebuildsReservation()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Store);
        await SaveAuthorizedAsync(store, "consumer-a", "ctx-a", "terminal", 20);
        var pendingKey = await SavePendingAsync(store, "consumer-a", "ctx-a", "pending", 21);
        await EmissionStoreBackupRecovery.CreateBackupAsync(temp.Store, temp.Backup);

        Directory.Delete(temp.Store, recursive: true);
        var gate = new FileSystemEmissionRecoveryGate(temp.Recovery);
        await EmissionStoreBackupRecovery.RestoreAsync(temp.Backup, temp.Store, gate);

        var restored = new FileSystemEmissionIdempotencyStore(temp.Store);
        var terminal = await restored.GetAsync(
            EmissionRequestFingerprint.OperationKeyHash("consumer-a", "ctx-a", "terminal"));
        var pending = await restored.GetAsync(pendingKey);
        Assert.NotNull(terminal);
        Assert.Equal(EmissionIdempotencyState.Authorized, terminal!.State);
        Assert.NotNull(pending);
        Assert.Equal(EmissionIdempotencyState.NumberAssigned, pending!.State);

        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Store);
        await coordinator.InitializeAsync(restored);
        var reservation = await coordinator.GetActiveAsync(pending.Identity);
        Assert.NotNull(reservation);
        Assert.Equal(pendingKey, reservation!.OwnerKeyHash);
        Assert.Equal(21, reservation.NumeroComprobante);
    }

    [Fact]
    public async Task TamperedBackup_IsRejectedBeforeRestoreSwap()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Store);
        await SaveAuthorizedAsync(store, "consumer-a", "ctx-a", "terminal", 30);
        await EmissionStoreBackupRecovery.CreateBackupAsync(temp.Store, temp.Backup);

        var operationPath = Directory.EnumerateFiles(temp.Backup, "*.json", SearchOption.TopDirectoryOnly)
            .Single(path => Path.GetFileName(path) != EmissionStoreBackupRecovery.ManifestFileName);
        await File.AppendAllTextAsync(operationPath, " ");

        var gate = new FileSystemEmissionRecoveryGate(temp.Recovery);
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            EmissionStoreBackupRecovery.RestoreAsync(temp.Backup, temp.Store, gate));

        Assert.Contains("BACKUP_FILE_", exception.Message, StringComparison.Ordinal);
        Assert.Null(await gate.GetBlockAsync());
    }

    private static async Task SaveAuthorizedAsync(
        FileSystemEmissionIdempotencyStore store,
        string consumerId,
        string contextId,
        string key,
        long number)
    {
        var request = Request();
        var identity = Identity(consumerId, contextId);
        var keyHash = EmissionRequestFingerprint.OperationKeyHash(consumerId, contextId, key);
        var created = await store.GetOrCreateAsync(
            keyHash,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request));
        await store.SaveAsync(created with
        {
            NumeroComprobante = number,
            State = EmissionIdempotencyState.Authorized,
            FiscalResult = StoredFiscalResult.FromResponse(new dcFacturaResponse
            {
                Success = true,
                NumeroComprobante = number,
                Cae = "CAE" + number,
                CaeVencimiento = "20261020",
                Resultado = "A",
                EmissionOutcome = dcEmissionOutcome.Authorized
            }),
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    private static async Task<string> SavePendingAsync(
        FileSystemEmissionIdempotencyStore store,
        string consumerId,
        string contextId,
        string key,
        long number)
    {
        var request = Request();
        var identity = Identity(consumerId, contextId);
        var keyHash = EmissionRequestFingerprint.OperationKeyHash(consumerId, contextId, key);
        var created = await store.GetOrCreateAsync(
            keyHash,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request));
        await store.SaveAsync(created with
        {
            NumeroComprobante = number,
            State = EmissionIdempotencyState.NumberAssigned,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        return keyHash;
    }

    private static FiscalOperationIdentity Identity(string consumerId, string contextId)
        => new(
            consumerId,
            contextId,
            "homologacion",
            20123456786,
            7,
            (int)dcTipoComprobante.FacturaB,
            1,
            "cred-v1");

    private static dcFacturaRequest Request() => new()
    {
        TipoComprobante = dcTipoComprobante.FacturaB,
        Concepto = dcConcepto.Productos,
        CuitReceptor = 20123456786,
        TipoDocReceptor = (int)dcTipoDocumento.CUIT,
        CondicionIvaReceptor = dcCondicionIvaReceptor.ResponsableInscripto,
        ImporteNeto = 100m,
        ImporteIva = 21m,
        ImporteTotal = 121m,
        AlicuotaIva = dcAlicuotaIva.Veintiuno,
        MonedaId = "PES",
        MonedaCotizacion = 1m,
        FechaComprobante = "20261003"
    };

    private static RepresentedFiscalContextRecord Context()
        => new(
            "ctx-a",
            "homologacion",
            20123456786,
            7,
            FiscalContextOperationalState.Active,
            1,
            false,
            [new CredentialAssignmentRecord(
                "cred-v1",
                "credential-a",
                CredentialAssignmentStatus.Active,
                "validated",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                "test")]);

    private static ClaimsPrincipal Principal()
        => new(new ClaimsIdentity(
        [
            new Claim(ArcaClaimTypes.ConsumerId, "consumer-a"),
            new Claim(ArcaClaimTypes.ContextGrant, ArcaClaimTypes.GrantValue("ctx-a", "facturar"))
        ], "test"));

    private sealed class FakeMaterializer : IFiscalCredentialMaterializer
    {
        public int Calls { get; private set; }

        public FiscalCredentialMaterialization Materialize(
            RepresentedFiscalContextRecord context,
            CredentialAssignmentRecord assignment)
        {
            Calls++;
            var config = new dcArcaConfig
            {
                Environment = context.Environment,
                Cuit = context.RepresentedCuit.ToString(),
                PuntoVenta = context.PointOfSale,
                CertificatePath = "not-used.pfx",
                CertificatePassword = string.Empty,
                WsaaUrl = "https://example.invalid/wsaa",
                WsfeUrl = "https://example.invalid/wsfe",
                PadronUrl = "https://example.invalid/padron"
            };
            var binding = new FiscalCredentialHostBinding(
                assignment.CredentialId,
                $"{context.Environment}:{assignment.CredentialId}",
                config);
            return new FiscalCredentialMaterialization(
                config,
                new dcArcaAuthService(config.WsaaUrl, config.CertificatePath, config.CertificatePassword, binding.CacheIdentity),
                new dcArcaAuthService(config.WsaaUrl, config.CertificatePath, config.CertificatePassword, binding.CacheIdentity, "ws_sr_constancia_inscripcion"),
                binding);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "arca-backup-restore-tests-" + Guid.NewGuid().ToString("N"));
        public string Store => Path.Combine(Root, "store");
        public string Backup => Path.Combine(Root, "backup");
        public string Recovery => Path.Combine(Root, "recovery");
        public string Contexts => Path.Combine(Root, "contexts");

        public TempDirectory()
        {
            Directory.CreateDirectory(Store);
            Directory.CreateDirectory(Contexts);
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
