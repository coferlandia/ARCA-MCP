using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public class ConsultEvidenceFailClosedTests
{
    [Fact]
    public void Reconciliation_MissingAggregateEvidence_IsInsufficientEvenWhenBalanceCloses()
    {
        var request = Request();
        var operation = Operation(request);
        var consulted = Consulted(request);
        consulted.ConsultEvidence = ConsultEvidenceTestData.ValidFor(consulted) with
        {
            ImporteTributos = dcFiscalEvidenceStatus.Missing
        };

        var result = FiscalReconciliationComparer.Compare(operation, consulted);

        Assert.Equal(FiscalReconciliationMatch.InsufficientEvidence, result.Match);
        Assert.Equal("RECONCILIATION_EVIDENCE_INSUFFICIENT", result.Code);
    }

    [Fact]
    public void Reconciliation_InvalidAggregateEvidence_IsInsufficient()
    {
        var request = Request();
        var operation = Operation(request);
        var consulted = Consulted(request);
        consulted.ConsultEvidence = ConsultEvidenceTestData.ValidFor(consulted) with
        {
            ImporteTributos = dcFiscalEvidenceStatus.Invalid
        };

        var result = FiscalReconciliationComparer.Compare(operation, consulted);

        Assert.Equal(FiscalReconciliationMatch.InsufficientEvidence, result.Match);
    }

    [Fact]
    public void Snapshot_MissingAggregateEvidence_FailsClosedEvenWhenBalanceCloses()
    {
        var consulted = Consulted(Request());
        consulted.ConsultEvidence = ConsultEvidenceTestData.ValidFor(consulted) with
        {
            ImporteNoGravado = dcFiscalEvidenceStatus.Missing,
            ImporteExento = dcFiscalEvidenceStatus.Missing,
            ImporteTributos = dcFiscalEvidenceStatus.Missing
        };

        var error = Assert.Throws<FiscalDocumentSnapshotException>(() =>
            FiscalDocumentSnapshotFactory.FromConsult(
                consulted,
                new FiscalDocumentContext("homologacion", "20123456786", 7)));

        Assert.Equal("FISCAL_DOCUMENT_INCOMPLETE", error.Code);
    }

    [Fact]
    public void Reconciliation_MissingIvaLineBase_IsInsufficient()
    {
        var request = Request();
        var operation = Operation(request);
        var consulted = Consulted(request);
        var valid = ConsultEvidenceTestData.ValidFor(consulted);
        consulted.ConsultEvidence = valid with
        {
            Iva = [valid.Iva[0] with { BaseImponible = dcFiscalEvidenceStatus.Missing }]
        };

        var result = FiscalReconciliationComparer.Compare(operation, consulted);

        Assert.Equal(FiscalReconciliationMatch.InsufficientEvidence, result.Match);
    }

    [Fact]
    public void Snapshot_InvalidTaxLineAmount_FailsClosed()
    {
        var request = Request();
        request.ImporteTributos = 1m;
        request.ImporteTotal = 122m;
        request.Tributos =
        [
            new dcFacturaRequest.TributoDetalle
            {
                Id = 1,
                Descripcion = "Tasa",
                BaseImponible = 100m,
                Alicuota = 1m,
                Importe = 1m
            }
        ];
        var consulted = Consulted(request);
        var valid = ConsultEvidenceTestData.ValidFor(consulted);
        consulted.ConsultEvidence = valid with
        {
            Tributos = [valid.Tributos[0] with { Importe = dcFiscalEvidenceStatus.Invalid }]
        };

        var error = Assert.Throws<FiscalDocumentSnapshotException>(() =>
            FiscalDocumentSnapshotFactory.FromConsult(
                consulted,
                new FiscalDocumentContext("homologacion", "20123456786", 7)));

        Assert.Equal("FISCAL_DOCUMENT_INCOMPLETE", error.Code);
    }

    private static EmissionIdempotencyRecord Operation(dcFacturaRequest request)
    {
        var identity = new FiscalOperationIdentity(
            "consumer", "context", "homologacion", 20123456786, 7,
            (int)request.TipoComprobante!.Value, 1, "cred-1");
        return new EmissionIdempotencyRecord(
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
        ImporteNoGravado = 0m,
        ImporteExento = 0m,
        ImporteTributos = 0m,
        AlicuotaIva = dcAlicuotaIva.Veintiuno,
        MonedaId = "PES",
        MonedaCotizacion = 1m,
        FechaComprobante = "20261003"
    };

    private static dcFacturaResponse Consulted(dcFacturaRequest request)
    {
        var response = new dcFacturaResponse
        {
            Success = true,
            NumeroComprobante = 42,
            PuntoVenta = 7,
            TipoComprobante = request.TipoComprobante,
            Concepto = request.Concepto,
            DocTipo = (dcTipoDocumento)request.TipoDocReceptor,
            DocNro = request.CuitReceptor,
            CondicionIvaReceptor = request.CondicionIvaReceptor,
            FechaComprobante = request.FechaComprobante ?? string.Empty,
            ImporteNeto = request.ImporteNeto,
            ImporteNoGravado = request.ImporteNoGravado,
            ImporteExento = request.ImporteExento,
            ImporteIva = request.ImporteIva,
            ImporteTributos = request.ImporteTributos,
            ImporteTotal = request.ImporteTotal,
            MonedaId = request.MonedaId,
            MonedaCotizacion = request.MonedaCotizacion,
            Cae = "12345678901234",
            CaeVencimiento = "20261020",
            Resultado = "A"
        };
        response.Iva.Add(new dcFacturaResponse.IvaDetalle
        {
            Alicuota = dcAlicuotaIva.Veintiuno,
            BaseImponible = 100m,
            Importe = 21m
        });
        foreach (var item in request.Tributos)
        {
            response.Tributos.Add(new dcFacturaResponse.TributoDetalle
            {
                Id = item.Id,
                Descripcion = item.Descripcion,
                BaseImponible = item.BaseImponible,
                Alicuota = item.Alicuota,
                Importe = item.Importe
            });
        }
        response.ConsultEvidence = ConsultEvidenceTestData.ValidFor(response);
        return response;
    }
}
