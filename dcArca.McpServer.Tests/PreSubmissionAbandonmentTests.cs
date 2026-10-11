using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public sealed class PreSubmissionAbandonmentTests
{
    [Fact]
    public async Task NumberAssigned_Abandon_PersisteAuditoriaConservaNumeroYLiberLaSerie()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await coordinator.InitializeAsync(store);
        var identity = Identity();
        var key = new string('a', 64);
        var request = Request();
        var created = await store.GetOrCreateAsync(
            key,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request));
        await coordinator.ReserveAsync(identity, key, 41);
        await store.SaveAsync(created with
        {
            NumeroComprobante = 41,
            State = EmissionIdempotencyState.NumberAssigned,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var service = new PreSubmissionAbandonmentService(store, coordinator);
        var result = await service.AbandonAsync(key, "admin-test", "manual_recovery");

        Assert.True(result.ReservationReleased);
        Assert.Equal(EmissionIdempotencyState.AbandonedBeforeSubmission, result.Operation.State);
        Assert.Equal(41, result.Operation.NumeroComprobante);
        Assert.Equal("admin-test", result.Operation.AbandonmentAudit?.ActorReference);
        Assert.Equal("manual_recovery", result.Operation.AbandonmentAudit?.ReasonCode);
        Assert.Null(result.Operation.FiscalResult);
        Assert.Null(await coordinator.GetActiveAsync(identity));

        var next = await coordinator.ReserveAsync(identity, new string('b', 64), 42);
        Assert.Equal(42, next.NumeroComprobante);
    }

    [Fact]
    public async Task CreatedConReservaDeCrashWindow_AbandonConservaNumeroYLimpiaReserva()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await coordinator.InitializeAsync(store);
        var identity = Identity();
        var key = new string('a', 64);
        var request = Request();
        await store.GetOrCreateAsync(
            key,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request));
        await coordinator.ReserveAsync(identity, key, 17);

        var result = await new PreSubmissionAbandonmentService(store, coordinator)
            .AbandonAsync(key, "admin-test", "crash_window_recovery");

        Assert.Equal(EmissionIdempotencyState.AbandonedBeforeSubmission, result.Operation.State);
        Assert.Equal(17, result.Operation.NumeroComprobante);
        Assert.Null(await coordinator.GetActiveAsync(identity));
    }

    [Fact]
    public async Task ReservaCreadaEntreObservacionYPersistencia_AbandonoConservaNumero()
    {
        using var temp = new TempDirectory();
        var durableStore = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await coordinator.InitializeAsync(durableStore);
        var identity = Identity();
        var key = new string('a', 64);
        var request = Request();
        await durableStore.GetOrCreateAsync(
            key,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request));

        var pausingStore = new PausingAbandonStore(durableStore);
        var service = new PreSubmissionAbandonmentService(pausingStore, coordinator);
        var abandon = service.AbandonAsync(key, "admin-test", "race_recovery");

        await pausingStore.AbandonEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.ReserveAsync(identity, key, 31);
        pausingStore.ResumeAbandon.TrySetResult();

        var result = await abandon;

        Assert.Equal(EmissionIdempotencyState.AbandonedBeforeSubmission, result.Operation.State);
        Assert.Equal(31, result.Operation.NumeroComprobante);
        Assert.Null(await coordinator.GetActiveAsync(identity));
    }

    [Fact]
    public async Task AbandonoGanaCarrera_ReservaPosteriorDelMismoOwnerEsRechazada()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await coordinator.InitializeAsync(store);
        var identity = Identity();
        var key = new string('a', 64);
        var request = Request();
        await store.GetOrCreateAsync(
            key,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request));

        await new PreSubmissionAbandonmentService(store, coordinator)
            .AbandonAsync(key, "admin-test", "race_recovery");

        var exception = await Assert.ThrowsAsync<FiscalSeriesBlockedException>(() =>
            coordinator.ReserveAsync(identity, key, 31));

        Assert.Equal("SERIES_OWNER_CLOSED", exception.Message);
        Assert.Null(await coordinator.GetActiveAsync(identity));
    }

    [Fact]
    public async Task RepetirAbandono_EsIdempotenteYConservaAuditoriaOriginal()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await coordinator.InitializeAsync(store);
        var key = new string('a', 64);
        var request = Request();
        await store.GetOrCreateAsync(
            key,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            Identity(),
            StoredFiscalEvidence.FromRequest(request));
        var service = new PreSubmissionAbandonmentService(store, coordinator);

        var first = await service.AbandonAsync(key, "admin-one", "manual_recovery");
        var second = await service.AbandonAsync(key, "admin-two", "different_reason");

        Assert.Equal(first.Operation.UpdatedAt, second.Operation.UpdatedAt);
        Assert.Equal("admin-one", second.Operation.AbandonmentAudit?.ActorReference);
        Assert.Equal("manual_recovery", second.Operation.AbandonmentAudit?.ReasonCode);
    }

    [Theory]
    [InlineData(EmissionIdempotencyState.Submitting)]
    [InlineData(EmissionIdempotencyState.Uncertain)]
    [InlineData(EmissionIdempotencyState.Authorized)]
    [InlineData(EmissionIdempotencyState.FiscalRejected)]
    public async Task EstadoQuePuedeHaberCruzadoSideEffect_NoPuedeAbandonarse(
        EmissionIdempotencyState state)
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await coordinator.InitializeAsync(store);
        var identity = Identity();
        var key = new string('a', 64);
        var request = Request();
        var created = await store.GetOrCreateAsync(
            key,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request));
        await coordinator.ReserveAsync(identity, key, 23);

        StoredFiscalResult? fiscalResult = state switch
        {
            EmissionIdempotencyState.Authorized => StoredFiscalResult.FromResponse(new dcFacturaResponse
            {
                Success = true,
                NumeroComprobante = 23,
                Cae = "CAE23",
                Resultado = "A",
                EmissionOutcome = dcEmissionOutcome.Authorized
            }),
            EmissionIdempotencyState.FiscalRejected => StoredFiscalResult.FromResponse(new dcFacturaResponse
            {
                Success = false,
                NumeroComprobante = 23,
                Resultado = "R",
                Codigo = "ARCA_REJECTED",
                EmissionOutcome = dcEmissionOutcome.FiscalRejected
            }),
            EmissionIdempotencyState.Uncertain => StoredFiscalResult.FromResponse(new dcFacturaResponse
            {
                Success = false,
                NumeroComprobante = 23,
                Codigo = "EMISSION_UNCERTAIN",
                EmissionOutcome = dcEmissionOutcome.Uncertain
            }),
            _ => null
        };

        await store.SaveAsync(created with
        {
            NumeroComprobante = 23,
            State = state,
            FiscalResult = fiscalResult,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var service = new PreSubmissionAbandonmentService(store, coordinator);
        var exception = await Assert.ThrowsAsync<PreSubmissionAbandonmentException>(() =>
            service.AbandonAsync(key, "admin-test", "manual_recovery"));

        Assert.Equal("OPERATION_ABANDON_NOT_ALLOWED", exception.Code);
        var active = await coordinator.GetActiveAsync(identity);
        Assert.NotNull(active);
        Assert.Equal(key, active!.OwnerKeyHash);
    }

    [Fact]
    public async Task Store_NoPermiteReabrirOperacionAbandonada()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        var key = new string('a', 64);
        var request = Request();
        await store.GetOrCreateAsync(
            key,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            Identity(),
            StoredFiscalEvidence.FromRequest(request));
        var abandoned = await store.AbandonBeforeSubmissionAsync(
            key,
            null,
            new PreSubmissionAbandonmentAudit("admin-test", "manual_recovery", DateTimeOffset.UtcNow));

        var conflict = await Assert.ThrowsAsync<EmissionTerminalStateConflictException>(() =>
            store.SaveAsync(abandoned with
            {
                State = EmissionIdempotencyState.NumberAssigned,
                NumeroComprobante = 99,
                AbandonmentAudit = null,
                UpdatedAt = DateTimeOffset.UtcNow.AddSeconds(1)
            }));

        Assert.Equal(EmissionIdempotencyState.AbandonedBeforeSubmission, conflict.Existing.State);
    }

    [Fact]
    public async Task AuditoriaConTextoLibre_FallaCerrado()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        var key = new string('a', 64);
        var request = Request();
        await store.GetOrCreateAsync(
            key,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            Identity(),
            StoredFiscalEvidence.FromRequest(request));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.AbandonBeforeSubmissionAsync(
                key,
                null,
                new PreSubmissionAbandonmentAudit(
                    "admin test@example.com",
                    "copied secret value",
                    DateTimeOffset.UtcNow)));
    }

    private static FiscalOperationIdentity Identity() => new(
        "consumer",
        "context",
        "homologacion",
        20123456786,
        7,
        (int)dcTipoComprobante.FacturaB,
        1,
        "cred-1");

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
        FechaComprobante = "20261011"
    };

    private sealed class PausingAbandonStore : IEmissionIdempotencyStore
    {
        private readonly IEmissionIdempotencyStore _inner;
        public TaskCompletionSource AbandonEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ResumeAbandon { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PausingAbandonStore(IEmissionIdempotencyStore inner)
            => _inner = inner;

        public Task EnsureContextAsync(
            FiscalContextDescriptor context,
            CancellationToken cancellationToken = default)
            => _inner.EnsureContextAsync(context, cancellationToken);

        public Task<EmissionIdempotencyRecord> GetOrCreateAsync(
            string keyHash,
            string requestHash,
            int requestCanonicalizationVersion,
            FiscalOperationIdentity identity,
            StoredFiscalEvidence fiscalEvidence,
            CancellationToken cancellationToken = default)
            => _inner.GetOrCreateAsync(
                keyHash,
                requestHash,
                requestCanonicalizationVersion,
                identity,
                fiscalEvidence,
                cancellationToken);

        public Task<EmissionIdempotencyRecord?> GetAsync(
            string keyHash,
            CancellationToken cancellationToken = default)
            => _inner.GetAsync(keyHash, cancellationToken);

        public Task SaveAsync(
            EmissionIdempotencyRecord record,
            CancellationToken cancellationToken = default)
            => _inner.SaveAsync(record, cancellationToken);

        public async Task<EmissionIdempotencyRecord> AbandonBeforeSubmissionAsync(
            string keyHash,
            long? reservedNumber,
            PreSubmissionAbandonmentAudit audit,
            CancellationToken cancellationToken = default)
        {
            AbandonEntered.TrySetResult();
            await ResumeAbandon.Task.WaitAsync(cancellationToken);
            return await _inner.AbandonBeforeSubmissionAsync(
                keyHash,
                reservedNumber,
                audit,
                cancellationToken);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "arca-abandonment-tests-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
