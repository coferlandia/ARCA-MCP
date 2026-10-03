using dcArca.Core.Models;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.McpServer.Tests;

public class DurableSequencerSafetyTests
{
    [Fact]
    public async Task UncertainOperation_BlocksDifferentKeyWithoutSecondSideEffect()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await coordinator.InitializeAsync(store);
        var fake = new DurableFakeWsfeClient { ReturnUncertainOnNextIssue = true };
        var sequencer = Sequencer(fake, store, coordinator);

        var first = await sequencer.EmitAsync(Request(), "op-a");
        var second = await sequencer.EmitAsync(Request(), "op-b");

        Assert.Equal(dcEmissionOutcome.Uncertain, first.EmissionOutcome);
        Assert.False(second.Success);
        Assert.Equal("SERIES_RESERVATION_BLOCKED", second.Codigo);
        Assert.Equal(1, fake.EmitCalls);
    }

    [Fact]
    public async Task Restart_RebuildsPendingReservationAndStillBlocksDifferentKey()
    {
        using var temp = new TempDirectory();
        var fake = new DurableFakeWsfeClient { ReturnUncertainOnNextIssue = true };
        var firstStore = new FileSystemEmissionIdempotencyStore(temp.Path);

        using (var firstCoordinator = new FileSystemFiscalSeriesCoordinator(temp.Path))
        {
            await firstCoordinator.InitializeAsync(firstStore);
            var firstSequencer = Sequencer(fake, firstStore, firstCoordinator);
            var first = await firstSequencer.EmitAsync(Request(), "op-a");
            Assert.Equal(dcEmissionOutcome.Uncertain, first.EmissionOutcome);
        }

        var restartedStore = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var restartedCoordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await restartedCoordinator.InitializeAsync(restartedStore);
        var restartedSequencer = Sequencer(fake, restartedStore, restartedCoordinator);

        var second = await restartedSequencer.EmitAsync(Request(), "op-b");

        Assert.False(second.Success);
        Assert.Equal("SERIES_RESERVATION_BLOCKED", second.Codigo);
        Assert.Equal(1, fake.EmitCalls);
    }

    [Fact]
    public async Task EquivalentReconciliation_RecoversAndReleasesSeries()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await coordinator.InitializeAsync(store);
        var fake = new DurableFakeWsfeClient { ReturnUncertainOnNextIssue = true };
        var sequencer = Sequencer(fake, store, coordinator);

        var first = await sequencer.EmitAsync(Request(), "op-a");
        fake.ConsultAuthorized = true;
        var recovered = await sequencer.EmitAsync(Request(), "op-a");
        var next = await sequencer.EmitAsync(Request(), "op-b");

        Assert.Equal(dcEmissionOutcome.Uncertain, first.EmissionOutcome);
        Assert.True(recovered.Success);
        Assert.Equal(dcEmissionOutcome.RecoveredSuccess, recovered.EmissionOutcome);
        Assert.True(next.Success);
        Assert.Equal(2, next.NumeroComprobante);
        Assert.Equal(2, fake.EmitCalls);
    }

    [Fact]
    public async Task TerminalReplay_DoesNotReleaseNewOwnersReservation()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await coordinator.InitializeAsync(store);
        var fake = new DurableFakeWsfeClient();
        var sequencer = Sequencer(fake, store, coordinator);

        var oldAuthorized = await sequencer.EmitAsync(Request(), "op-old");
        fake.ReturnUncertainOnNextIssue = true;
        var pending = await sequencer.EmitAsync(Request(), "op-new");
        var oldReplay = await sequencer.EmitAsync(Request(), "op-old");
        var third = await sequencer.EmitAsync(Request(), "op-third");

        Assert.True(oldAuthorized.Success);
        Assert.Equal(dcEmissionOutcome.Uncertain, pending.EmissionOutcome);
        Assert.True(oldReplay.Success);
        Assert.Equal(oldAuthorized.NumeroComprobante, oldReplay.NumeroComprobante);
        Assert.Equal("SERIES_RESERVATION_BLOCKED", third.Codigo);
        Assert.Equal(2, fake.EmitCalls);
    }

    [Fact]
    public async Task ReconciliationMismatch_KeepsSeriesBlocked()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await coordinator.InitializeAsync(store);
        var fake = new DurableFakeWsfeClient { ReturnUncertainOnNextIssue = true };
        var sequencer = Sequencer(fake, store, coordinator);

        await sequencer.EmitAsync(Request(), "op-a");
        fake.ConsultAuthorized = true;
        fake.ReturnMismatchedConsult = true;
        var reconciliation = await sequencer.EmitAsync(Request(), "op-a");
        var second = await sequencer.EmitAsync(Request(), "op-b");

        Assert.False(reconciliation.Success);
        Assert.Equal("RECONCILIATION_MISMATCH", reconciliation.Codigo);
        Assert.Equal(dcEmissionOutcome.Uncertain, reconciliation.EmissionOutcome);
        Assert.Equal("SERIES_RESERVATION_BLOCKED", second.Codigo);
        Assert.Equal(1, fake.EmitCalls);
    }

    [Fact]
    public async Task TransportRecoveredSuccess_MustPassFiscalComparer()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await coordinator.InitializeAsync(store);
        var fake = new DurableFakeWsfeClient
        {
            ReturnRecoveredSuccessOnNextIssue = true,
            ReturnMismatchedConsult = true
        };
        var sequencer = Sequencer(fake, store, coordinator);

        var result = await sequencer.EmitAsync(Request(), "op-a");
        var second = await sequencer.EmitAsync(Request(), "op-b");

        Assert.False(result.Success);
        Assert.Equal("RECONCILIATION_MISMATCH", result.Codigo);
        Assert.Equal(dcEmissionOutcome.Uncertain, result.EmissionOutcome);
        Assert.Equal("SERIES_RESERVATION_BLOCKED", second.Codigo);
        Assert.Equal(1, fake.EmitCalls);
    }

    [Fact]
    public async Task NumberDriftBeforeSubmitting_BlocksWithoutEmission()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        await coordinator.InitializeAsync(store);
        var fake = new DurableFakeWsfeClient();
        fake.LastNumberResponses.Enqueue(0);
        fake.LastNumberResponses.Enqueue(1);
        var sequencer = Sequencer(fake, store, coordinator);

        var result = await sequencer.EmitAsync(Request(), "op-a");
        var second = await sequencer.EmitAsync(Request(), "op-b");

        Assert.False(result.Success);
        Assert.Equal("SERIES_NUMBER_DRIFT", result.Codigo);
        Assert.Equal(dcEmissionOutcome.FailedBeforeSubmission, result.EmissionOutcome);
        Assert.Equal(0, fake.EmitCalls);
        Assert.Equal("SERIES_RESERVATION_BLOCKED", second.Codigo);
    }

    [Fact]
    public async Task PersistedAssignmentMismatch_BlocksBeforeWsfeAndRetryUsesWinner()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new FileSystemFiscalSeriesCoordinator(temp.Path);
        var request = Request();
        var config = Config();
        var winningProvider = IdentityProvider(config, "cred-1");
        var winningIdentity = winningProvider.For(dcTipoComprobante.FacturaB);
        var keyHash = EmissionRequestFingerprint.OperationKeyHash(
            winningIdentity.ConsumerId,
            winningIdentity.ContextId,
            "op-a");

        await store.GetOrCreateAsync(
            keyHash,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            winningIdentity,
            StoredFiscalEvidence.FromRequest(request));
        await coordinator.InitializeAsync(store);

        var fake = new DurableFakeWsfeClient();
        var losingSequencer = Sequencer(fake, store, coordinator, "cred-2");
        var blocked = await losingSequencer.EmitAsync(Request(), "op-a");

        Assert.False(blocked.Success);
        Assert.Equal("CREDENTIAL_ASSIGNMENT_MISMATCH", blocked.Codigo);
        Assert.Equal(dcEmissionOutcome.FailedBeforeSubmission, blocked.EmissionOutcome);
        Assert.Equal(0, fake.LastNumberCalls);
        Assert.Equal(0, fake.ConsultCalls);
        Assert.Equal(0, fake.EmitCalls);

        var retryWithWinner = await Sequencer(fake, store, coordinator, "cred-1")
            .EmitAsync(Request(), "op-a");

        Assert.True(retryWithWinner.Success);
        Assert.Equal(1, fake.EmitCalls);
        var persisted = await store.GetAsync(keyHash);
        Assert.NotNull(persisted);
        Assert.Equal("cred-1", persisted!.Identity.CredentialAssignmentRevision);
    }

    private static McpInvoiceSequencer Sequencer(
        IdcWsfeClient fake,
        IEmissionIdempotencyStore store,
        IFiscalSeriesCoordinator coordinator,
        string credentialRevision = "cred-1")
    {
        var config = Config();
        var provider = IdentityProvider(config, credentialRevision);
        return new McpInvoiceSequencer(fake, config, store, provider, coordinator);
    }

    private static SingleFiscalOperationIdentityProvider IdentityProvider(
        dcArcaConfig config,
        string credentialRevision)
        => new(
            config,
            new SingleFiscalContextOptions("consumer", "context", "homologacion", 1, credentialRevision));

    private static dcArcaConfig Config() => new()
    {
        Cuit = "20123456786",
        PuntoVenta = 7,
        WsfeUrl = "https://wswhomo.afip.gov.ar/wsfev1/service.asmx"
    };

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
        ImporteNoGravado = 0m,
        ImporteExento = 0m,
        AlicuotaIva = dcAlicuotaIva.Veintiuno,
        MonedaId = "PES",
        MonedaCotizacion = 1m,
        FechaComprobante = "20261003"
    };

    private sealed class DurableFakeWsfeClient : IdcWsfeClient
    {
        private dcFacturaRequest? _lastRequest;
        private long _lastNumber;

        public Queue<long> LastNumberResponses { get; } = new();
        public bool ReturnUncertainOnNextIssue { get; set; }
        public bool ReturnRecoveredSuccessOnNextIssue { get; set; }
        public bool ConsultAuthorized { get; set; }
        public bool ReturnMismatchedConsult { get; set; }
        public int LastNumberCalls { get; private set; }
        public int ConsultCalls { get; private set; }
        public int EmitCalls { get; private set; }

        public Task<dcFacturaResponse> FECompUltimoAutorizadoAsync(dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
        {
            LastNumberCalls++;
            var number = LastNumberResponses.Count > 0 ? LastNumberResponses.Dequeue() : _lastNumber;
            return Task.FromResult(new dcFacturaResponse { Success = true, NumeroComprobante = number });
        }

        public Task<dcFacturaResponse> FECAESolicitarAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
        {
            EmitCalls++;
            _lastRequest = factura;
            _lastNumber = factura.NumeroComprobante!.Value;
            if (ReturnUncertainOnNextIssue)
            {
                ReturnUncertainOnNextIssue = false;
                return Task.FromResult(new dcFacturaResponse
                {
                    Success = false,
                    NumeroComprobante = _lastNumber,
                    Codigo = "EMISSION_UNCERTAIN",
                    EmissionOutcome = dcEmissionOutcome.Uncertain
                });
            }

            if (ReturnRecoveredSuccessOnNextIssue)
            {
                ReturnRecoveredSuccessOnNextIssue = false;
                var recovered = BuildConsultResponse(factura, _lastNumber);
                recovered.EmissionOutcome = dcEmissionOutcome.RecoveredSuccess;
                return Task.FromResult(recovered);
            }

            return Task.FromResult(new dcFacturaResponse
            {
                Success = true,
                NumeroComprobante = _lastNumber,
                Cae = "CAE" + _lastNumber,
                CaeVencimiento = "20261020",
                Resultado = "A",
                EmissionOutcome = dcEmissionOutcome.Authorized
            });
        }

        public Task<dcFacturaResponse> FECompConsultarAsync(long numeroComprobante, dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
        {
            ConsultCalls++;
            if (!ConsultAuthorized || _lastRequest is null)
                return Task.FromResult(new dcFacturaResponse { Success = false, NumeroComprobante = numeroComprobante });

            return Task.FromResult(BuildConsultResponse(_lastRequest, numeroComprobante));
        }

        private dcFacturaResponse BuildConsultResponse(dcFacturaRequest request, long numeroComprobante)
        {
            var response = new dcFacturaResponse
            {
                Success = true,
                NumeroComprobante = numeroComprobante,
                PuntoVenta = 7,
                TipoComprobante = request.TipoComprobante,
                Concepto = request.Concepto,
                DocTipo = (dcTipoDocumento)request.TipoDocReceptor,
                DocNro = ReturnMismatchedConsult ? 30999999991 : request.CuitReceptor,
                CondicionIvaReceptor = request.CondicionIvaReceptor,
                FechaComprobante = request.FechaComprobante ?? string.Empty,
                FechaServicioDesde = request.FechaServicioDesde ?? string.Empty,
                FechaServicioHasta = request.FechaServicioHasta ?? string.Empty,
                FechaVencimientoPago = request.FechaVencimiento ?? string.Empty,
                ImporteNeto = request.ImporteNeto,
                ImporteIva = request.ImporteIva,
                ImporteTotal = request.ImporteTotal,
                ImporteNoGravado = request.ImporteNoGravado,
                ImporteExento = request.ImporteExento,
                ImporteTributos = request.ImporteTributos,
                MonedaId = request.MonedaId,
                MonedaCotizacion = request.MonedaCotizacion,
                Cae = "CAE" + numeroComprobante,
                CaeVencimiento = "20261020",
                Resultado = "A"
            };
            if (request.AlicuotaIva.HasValue && request.ImporteIva != 0m)
            {
                response.Iva.Add(new dcFacturaResponse.IvaDetalle
                {
                    Alicuota = request.AlicuotaIva,
                    BaseImponible = request.ImporteNeto,
                    Importe = request.ImporteIva
                });
            }
            return response;
        }

        public Task<dcFacturaResponse> SolicitarCaeAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
            => FECAESolicitarAsync(factura, cancellationToken);

        public Task<List<dcCondicionIvaOption>> GetCondicionesIVAReceptorAsync(int docTipo, long docNro, dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<dcCondicionIvaOption>());
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "arca-durable-safety-tests-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
