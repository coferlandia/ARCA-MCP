using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public class FiscalReconciliationComparerTests
{
    [Fact]
    public void Equivalent_AllowsDifferentDecimalScaleAndDetailOrder()
    {
        var request = Request();
        request.Iva =
        [
            new dcFacturaRequest.IvaDetalle { Alicuota = dcAlicuotaIva.Veintiuno, BaseImponible = 100m, Importe = 21m },
            new dcFacturaRequest.IvaDetalle { Alicuota = dcAlicuotaIva.Diez_Cinco, BaseImponible = 50m, Importe = 5.25m }
        ];
        request.AlicuotaIva = null;
        request.ImporteNeto = 150m;
        request.ImporteIva = 26.25m;
        request.ImporteTotal = 176.25m;
        var operation = Operation(request);
        var response = ResponseFrom(request, 42);
        response.Iva.Reverse();
        response.ImporteTotal = 176.250m;

        var result = FiscalReconciliationComparer.Compare(operation, response);

        Assert.Equal(FiscalReconciliationMatch.Equivalent, result.Match);
    }

    [Theory]
    [InlineData("receiver")]
    [InlineData("amount")]
    [InlineData("currency")]
    public void FiscalMismatch_IsNeverRecovered(string kind)
    {
        var request = Request();
        var operation = Operation(request);
        var response = ResponseFrom(request, 42);
        switch (kind)
        {
            case "receiver": response.DocNro = 30999999991; break;
            case "amount": response.ImporteTotal += 10m; break;
            case "currency": response.MonedaId = "USD"; break;
        }

        var result = FiscalReconciliationComparer.Compare(operation, response);

        Assert.Equal(FiscalReconciliationMatch.Mismatch, result.Match);
        Assert.Equal("RECONCILIATION_MISMATCH", result.Code);
    }

    [Fact]
    public void OneCentDifference_IsMismatch()
    {
        var request = Request();
        var operation = Operation(request);
        var response = ResponseFrom(request, 42);
        response.ImporteTotal += 0.01m;

        var result = FiscalReconciliationComparer.Compare(operation, response);

        Assert.Equal(FiscalReconciliationMatch.Mismatch, result.Match);
    }

    [Fact]
    public void LegacyEvidence_IsInsufficient()
    {
        var request = Request();
        var operation = Operation(request) with { FiscalEvidence = StoredFiscalEvidence.LegacyUnavailable() };

        var result = FiscalReconciliationComparer.Compare(operation, ResponseFrom(request, 42));

        Assert.Equal(FiscalReconciliationMatch.InsufficientEvidence, result.Match);
    }

    [Fact]
    public void AssociatedDocumentWithoutQueryableEvidence_IsInsufficient()
    {
        var request = Request();
        request.TipoComprobante = dcTipoComprobante.NotaCreditoB;
        request.CbteAsociadoTipo = (int)dcTipoComprobante.FacturaB;
        request.CbteAsociadoPtoVta = 7;
        request.CbteAsociadoNro = 10;
        var operation = Operation(request);
        var response = ResponseFrom(request, 42);

        var result = FiscalReconciliationComparer.Compare(operation, response);

        Assert.Equal(FiscalReconciliationMatch.InsufficientEvidence, result.Match);
        Assert.Equal("RECONCILIATION_EVIDENCE_INSUFFICIENT", result.Code);
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
        AlicuotaIva = dcAlicuotaIva.Veintiuno,
        MonedaId = "PES",
        MonedaCotizacion = 1m,
        FechaComprobante = "20261003"
    };

    private static dcFacturaResponse ResponseFrom(dcFacturaRequest request, long number)
    {
        var response = new dcFacturaResponse
        {
            Success = true,
            NumeroComprobante = number,
            PuntoVenta = 7,
            TipoComprobante = request.TipoComprobante,
            Concepto = request.Concepto,
            DocTipo = (dcTipoDocumento)request.TipoDocReceptor,
            DocNro = request.CuitReceptor,
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
            Cae = "CAE42",
            Resultado = "A"
        };

        var iva = request.Iva.Count > 0
            ? request.Iva
            : request.AlicuotaIva.HasValue && request.ImporteIva != 0m
                ? [new dcFacturaRequest.IvaDetalle { Alicuota = request.AlicuotaIva.Value, BaseImponible = request.ImporteNeto, Importe = request.ImporteIva }]
                : [];
        foreach (var item in iva)
        {
            response.Iva.Add(new dcFacturaResponse.IvaDetalle
            {
                Alicuota = item.Alicuota,
                BaseImponible = item.BaseImponible,
                Importe = item.Importe
            });
        }

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
        return response;
    }
}
