using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public class ConsultEvidencePositiveTests
{
    [Fact]
    public void ExplicitZeroEvidence_RemainsReconcilable()
    {
        var request = new dcFacturaRequest
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
        var identity = new FiscalOperationIdentity(
            "consumer", "context", "homologacion", 20123456786, 7,
            (int)dcTipoComprobante.FacturaB, 1, "cred-1");
        var operation = new EmissionIdempotencyRecord(
            FileSystemEmissionIdempotencyStore.CurrentSchemaVersion,
            new string('a', 64),
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request),
            42,
            EmissionIdempotencyState.Uncertain,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var consulted = new dcFacturaResponse
        {
            Success = true,
            NumeroComprobante = 42,
            PuntoVenta = 7,
            TipoComprobante = dcTipoComprobante.FacturaB,
            Concepto = dcConcepto.Productos,
            DocTipo = dcTipoDocumento.CUIT,
            DocNro = 20123456786,
            CondicionIvaReceptor = dcCondicionIvaReceptor.ResponsableInscripto,
            FechaComprobante = "20261003",
            ImporteNeto = 100m,
            ImporteIva = 21m,
            ImporteTotal = 121m,
            ImporteNoGravado = 0m,
            ImporteExento = 0m,
            ImporteTributos = 0m,
            MonedaId = "PES",
            MonedaCotizacion = 1m,
            Cae = "12345678901234",
            CaeVencimiento = "20261020",
            Resultado = "A"
        };
        consulted.Iva.Add(new dcFacturaResponse.IvaDetalle
        {
            Alicuota = dcAlicuotaIva.Veintiuno,
            BaseImponible = 100m,
            Importe = 21m
        });
        consulted.ConsultEvidence = ConsultEvidenceTestData.ValidFor(consulted);

        var result = FiscalReconciliationComparer.Compare(operation, consulted);

        Assert.Equal(FiscalReconciliationMatch.Equivalent, result.Match);
    }
}
