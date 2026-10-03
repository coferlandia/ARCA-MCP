using dcArca.Core.Models;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.McpServer.Tests;

public class LegacyReconciliationBlockTests
{
    [Fact]
    public async Task LegacyUncertainSinEvidencia_NoConsultaNiReemite()
    {
        using var temp = new TempDirectory();
        var config = new dcArcaConfig
        {
            Cuit = "20123456786",
            PuntoVenta = 7,
            WsfeUrl = "https://wswhomo.afip.gov.ar/wsfev1/service.asmx"
        };
        var provider = SingleFiscalOperationIdentityProvider.ForTests(config);
        var identity = provider.For(dcTipoComprobante.FacturaB);
        var request = Request();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        var keyHash = EmissionRequestFingerprint.OperationKeyHash(
            identity.ConsumerId,
            identity.ContextId,
            "legacy-key");

        var created = await store.GetOrCreateAsync(
            keyHash,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.LegacyUnavailable());
        await store.SaveAsync(created with
        {
            NumeroComprobante = 42,
            State = EmissionIdempotencyState.Uncertain,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var fake = new CountingWsfeClient();
        var sequencer = new McpInvoiceSequencer(fake, config, store, provider);

        var result = await sequencer.EmitAsync(Request(), "legacy-key");

        Assert.False(result.Success);
        Assert.Equal("LEGACY_RECONCILIATION_REQUIRED", result.Codigo);
        Assert.Equal(dcEmissionOutcome.Uncertain, result.EmissionOutcome);
        Assert.Equal(42, result.NumeroComprobante);
        Assert.Equal(0, fake.LastNumberCalls);
        Assert.Equal(0, fake.ConsultCalls);
        Assert.Equal(0, fake.EmitCalls);
    }

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
        FechaComprobante = "20261001"
    };

    private sealed class CountingWsfeClient : IdcWsfeClient
    {
        public int LastNumberCalls { get; private set; }
        public int ConsultCalls { get; private set; }
        public int EmitCalls { get; private set; }

        public Task<dcFacturaResponse> FECompUltimoAutorizadoAsync(
            dcTipoComprobante tipoComprobante,
            CancellationToken cancellationToken = default)
        {
            LastNumberCalls++;
            return Task.FromResult(new dcFacturaResponse { Success = true, NumeroComprobante = 41 });
        }

        public Task<dcFacturaResponse> FECompConsultarAsync(
            long numeroComprobante,
            dcTipoComprobante tipoComprobante,
            CancellationToken cancellationToken = default)
        {
            ConsultCalls++;
            return Task.FromResult(new dcFacturaResponse
            {
                Success = true,
                NumeroComprobante = numeroComprobante,
                Cae = "CAE42",
                Resultado = "A"
            });
        }

        public Task<dcFacturaResponse> FECAESolicitarAsync(
            dcFacturaRequest factura,
            CancellationToken cancellationToken = default)
        {
            EmitCalls++;
            return Task.FromResult(new dcFacturaResponse
            {
                Success = true,
                NumeroComprobante = factura.NumeroComprobante ?? 0,
                Cae = "CAE42",
                Resultado = "A"
            });
        }

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
            "arca-legacy-block-tests-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
