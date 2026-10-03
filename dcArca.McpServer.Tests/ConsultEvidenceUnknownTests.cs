using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public class ConsultEvidenceUnknownTests
{
    [Fact]
    public void Reconciliation_WithoutConsultEvidence_FailsClosed()
    {
        var request = new dcFacturaRequest
        {
            TipoComprobante = dcTipoComprobante.FacturaB,
            Concepto = dcConcepto.Productos,
            CuitReceptor = 1,
            TipoDocReceptor = (int)dcTipoDocumento.CUIT,
            ImporteNeto = 100m,
            ImporteIva = 21m,
            ImporteTotal = 121m,
            AlicuotaIva = dcAlicuotaIva.Veintiuno,
            MonedaId = "PES",
            MonedaCotizacion = 1m,
            FechaComprobante = "20261003"
        };
        var identity = new FiscalOperationIdentity("consumer", "context", "homologacion", 1, 7, (int)dcTipoComprobante.FacturaB, 1, "cred-1");
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
            DocNro = 1,
            FechaComprobante = "20261003",
            ImporteNeto = 100m,
            ImporteIva = 21m,
            ImporteTotal = 121m,
            MonedaId = "PES",
            MonedaCotizacion = 1m,
            Cae = "CAE"
        };

        var result = FiscalReconciliationComparer.Compare(operation, consulted);

        Assert.Equal(FiscalReconciliationMatch.InsufficientEvidence, result.Match);
        Assert.Equal("RECONCILIATION_EVIDENCE_INSUFFICIENT", result.Code);
    }
}
