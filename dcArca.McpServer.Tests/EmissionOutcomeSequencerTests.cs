using dcArca.Core.Models;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.McpServer.Tests;

public class EmissionOutcomeSequencerTests
{
    [Fact]
    public async Task RequestLocalInvalido_NoCreaOperacionNiConsultaNumeracion()
    {
        using var temp = new TempDirectory();
        var fake = new PhaseFakeWsfeClient();
        var sequencer = new McpInvoiceSequencer(fake, Config(), new FileSystemEmissionIdempotencyStore(temp.Path));
        var request = Request();
        request.ImporteTotal = 999m;

        var result = await sequencer.EmitAsync(request, "invalid-local");

        Assert.False(result.Success);
        Assert.Equal(dcEmissionOutcome.InvalidRequest, result.EmissionOutcome);
        Assert.Equal("IMP_MISMATCH", result.Codigo);
        Assert.Equal(0, fake.LastNumberCalls);
        Assert.Equal(0, fake.EmitCalls);
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.json", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task FalloAlConsultarNumeracion_SeClasificaAntesDelEnvio()
    {
        using var temp = new TempDirectory();
        var fake = new PhaseFakeWsfeClient { FailLastNumber = true };
        var sequencer = new McpInvoiceSequencer(fake, Config(), new FileSystemEmissionIdempotencyStore(temp.Path));

        var result = await sequencer.EmitAsync(Request(), "pre-submit-failure");

        Assert.False(result.Success);
        Assert.Equal(dcEmissionOutcome.FailedBeforeSubmission, result.EmissionOutcome);
        Assert.Equal(1, fake.LastNumberCalls);
        Assert.Equal(0, fake.EmitCalls);
    }

    [Fact]
    public async Task AdapterDevuelveInvalidRequestDespuesDeSubmitting_FallaCerradoComoUncertain()
    {
        using var temp = new TempDirectory();
        var fake = new PhaseFakeWsfeClient { ReturnInvalidAfterSubmit = true };
        var sequencer = new McpInvoiceSequencer(fake, Config(), new FileSystemEmissionIdempotencyStore(temp.Path));

        var result = await sequencer.EmitAsync(Request(), "unexpected-adapter-result");

        Assert.False(result.Success);
        Assert.Equal(dcEmissionOutcome.Uncertain, result.EmissionOutcome);
        Assert.Equal(1, fake.EmitCalls);

        var keyHash = EmissionRequestFingerprint.OperationKeyHash(
            "test-consumer",
            "test-context",
            "unexpected-adapter-result");
        var record = await new FileSystemEmissionIdempotencyStore(temp.Path).GetAsync(keyHash);
        Assert.NotNull(record);
        Assert.Equal(EmissionIdempotencyState.Uncertain, record!.State);
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
        AlicuotaIva = dcAlicuotaIva.Veintiuno,
        MonedaId = "PES",
        MonedaCotizacion = 1m,
        FechaComprobante = "20261003"
    };

    private sealed class PhaseFakeWsfeClient : IdcWsfeClient
    {
        public bool FailLastNumber { get; init; }
        public bool ReturnInvalidAfterSubmit { get; init; }
        public int LastNumberCalls { get; private set; }
        public int EmitCalls { get; private set; }

        public Task<dcFacturaResponse> FECompUltimoAutorizadoAsync(
            dcTipoComprobante tipoComprobante,
            CancellationToken cancellationToken = default)
        {
            LastNumberCalls++;
            return Task.FromResult(FailLastNumber
                ? new dcFacturaResponse { Success = false, Codigo = "WSAA_UNAVAILABLE", Mensaje = "auth unavailable" }
                : new dcFacturaResponse { Success = true, NumeroComprobante = 10 });
        }

        public Task<dcFacturaResponse> FECAESolicitarAsync(
            dcFacturaRequest factura,
            CancellationToken cancellationToken = default)
        {
            EmitCalls++;
            if (ReturnInvalidAfterSubmit)
            {
                return Task.FromResult(new dcFacturaResponse
                {
                    Success = false,
                    NumeroComprobante = factura.NumeroComprobante ?? 0,
                    Codigo = "ADAPTER_INVALID",
                    Mensaje = "unexpected local adapter validation",
                    EmissionOutcome = dcEmissionOutcome.InvalidRequest
                });
            }

            return Task.FromResult(new dcFacturaResponse
            {
                Success = true,
                NumeroComprobante = factura.NumeroComprobante ?? 0,
                Cae = "CAE11",
                Resultado = "A",
                EmissionOutcome = dcEmissionOutcome.Authorized
            });
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
            "arca-outcome-tests-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
