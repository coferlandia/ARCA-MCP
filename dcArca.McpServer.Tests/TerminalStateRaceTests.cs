using dcArca.Core.Models;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.McpServer.Tests;

public class TerminalStateRaceTests
{
    [Fact]
    public async Task EmissionOutcomeAtrasado_AdoptaAuthorizedConcurrente()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new InMemoryFiscalSeriesCoordinator();
        var fake = new BlockingWsfeClient();
        var config = Config();
        var provider = new SingleFiscalOperationIdentityProvider(
            config,
            new SingleFiscalContextOptions("consumer", "context", "homologacion", 1, "cred-1"));
        var sequencer = new McpInvoiceSequencer(fake, config, store, provider, coordinator);
        var request = Request();
        var identity = provider.For(dcTipoComprobante.FacturaB);
        var keyHash = EmissionRequestFingerprint.OperationKeyHash("consumer", "context", "op-race");

        var emission = sequencer.EmitAsync(request, "op-race");
        await fake.EmitEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var submitting = await store.GetAsync(keyHash);
        Assert.NotNull(submitting);
        Assert.Equal(EmissionIdempotencyState.Submitting, submitting!.State);
        Assert.Equal(1, submitting.NumeroComprobante);

        var winnerResponse = new dcFacturaResponse
        {
            Success = true,
            NumeroComprobante = 1,
            Cae = "CAE-WINNER",
            CaeVencimiento = "20261020",
            Resultado = "A",
            EmissionOutcome = dcEmissionOutcome.Authorized
        };
        await store.SaveAsync(submitting with
        {
            State = EmissionIdempotencyState.Authorized,
            FiscalResult = StoredFiscalResult.FromResponse(winnerResponse),
            UpdatedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        });

        fake.ReleaseEmit.TrySetResult();
        var result = await emission;

        Assert.True(result.Success);
        Assert.Equal("CAE-WINNER", result.Cae);
        Assert.Equal(dcEmissionOutcome.Authorized, result.EmissionOutcome);
        Assert.Equal(1, fake.EmitCalls);

        var persisted = await store.GetAsync(keyHash);
        Assert.NotNull(persisted);
        Assert.Equal(EmissionIdempotencyState.Authorized, persisted!.State);
        Assert.Equal("CAE-WINNER", persisted.FiscalResult?.Cae);
        Assert.Null(await coordinator.GetActiveAsync(identity));
    }

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

    private sealed class BlockingWsfeClient : IdcWsfeClient
    {
        public TaskCompletionSource EmitEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseEmit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int EmitCalls { get; private set; }

        public Task<dcFacturaResponse> FECompUltimoAutorizadoAsync(
            dcTipoComprobante tipoComprobante,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new dcFacturaResponse { Success = true, NumeroComprobante = 0 });

        public async Task<dcFacturaResponse> FECAESolicitarAsync(
            dcFacturaRequest factura,
            CancellationToken cancellationToken = default)
        {
            EmitCalls++;
            EmitEntered.TrySetResult();
            await ReleaseEmit.Task.WaitAsync(cancellationToken);
            return new dcFacturaResponse
            {
                Success = false,
                NumeroComprobante = factura.NumeroComprobante ?? 0,
                Codigo = "EMISSION_UNCERTAIN",
                Mensaje = "Respuesta tardía incierta.",
                EmissionOutcome = dcEmissionOutcome.Uncertain
            };
        }

        public Task<dcFacturaResponse> FECompConsultarAsync(
            long numeroComprobante,
            dcTipoComprobante tipoComprobante,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new dcFacturaResponse { Success = false, NumeroComprobante = numeroComprobante });

        public Task<dcFacturaResponse> SolicitarCaeAsync(
            dcFacturaRequest factura,
            CancellationToken cancellationToken = default)
            => FECAESolicitarAsync(factura, cancellationToken);

        public Task<List<dcCondicionIvaOption>> GetCondicionesIVAReceptorAsync(
            int docTipo,
            long docNro,
            dcTipoComprobante tipoComprobante,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<dcCondicionIvaOption>());
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "arca-terminal-race-tests-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
