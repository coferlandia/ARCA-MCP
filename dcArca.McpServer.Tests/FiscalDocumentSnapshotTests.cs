using System.Text.Json;
using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public class FiscalDocumentSnapshotTests
{
    [Fact]
    public void EmisionYConsulta_Complejas_RepresentanLaMismaVerdadFiscalConProcedenciaDistinta()
    {
        var context = new FiscalDocumentContext("homologacion", "20123456786", 7);
        var request = ComplexRequest();
        var emitted = AuthorizedResponse(123, 7);
        var consulted = ComplexConsulted(123, 7);

        var fromEmission = FiscalDocumentSnapshotFactory.FromEmission(request, emitted, context);
        var fromConsult = FiscalDocumentSnapshotFactory.FromConsult(consulted, context);

        Assert.Equal(FiscalDocumentSnapshotContract.Version, fromEmission.ContractVersion);
        Assert.Equal(FiscalDocumentSnapshotContract.Version, fromConsult.ContractVersion);
        Assert.Equal("emission-request", fromEmission.Provenance.FiscalData);
        Assert.Equal("fe-comp-consultar-validated", fromConsult.Provenance.FiscalData);

        Assert.Equal(fromEmission.Environment, fromConsult.Environment);
        Assert.Equal(fromEmission.EmisorCuit, fromConsult.EmisorCuit);
        Assert.Equal(fromEmission.PuntoVenta, fromConsult.PuntoVenta);
        Assert.Equal(fromEmission.TipoComprobante, fromConsult.TipoComprobante);
        Assert.Equal(fromEmission.NumeroComprobante, fromConsult.NumeroComprobante);
        Assert.Equal(fromEmission.Concepto, fromConsult.Concepto);
        Assert.Equal(fromEmission.DocumentoReceptorTipo, fromConsult.DocumentoReceptorTipo);
        Assert.Equal(fromEmission.DocumentoReceptorNumero, fromConsult.DocumentoReceptorNumero);
        Assert.Equal(fromEmission.CondicionIvaReceptor, fromConsult.CondicionIvaReceptor);
        Assert.Equal(fromEmission.FechaComprobante, fromConsult.FechaComprobante);
        Assert.Equal(fromEmission.FechaServicioDesde, fromConsult.FechaServicioDesde);
        Assert.Equal(fromEmission.FechaServicioHasta, fromConsult.FechaServicioHasta);
        Assert.Equal(fromEmission.FechaVencimientoPago, fromConsult.FechaVencimientoPago);
        Assert.Equal(fromEmission.ImporteNeto, fromConsult.ImporteNeto);
        Assert.Equal(fromEmission.ImporteNoGravado, fromConsult.ImporteNoGravado);
        Assert.Equal(fromEmission.ImporteExento, fromConsult.ImporteExento);
        Assert.Equal(fromEmission.ImporteIva, fromConsult.ImporteIva);
        Assert.Equal(fromEmission.ImporteTributos, fromConsult.ImporteTributos);
        Assert.Equal(fromEmission.ImporteTotal, fromConsult.ImporteTotal);
        Assert.Equal(fromEmission.Iva, fromConsult.Iva);
        Assert.Equal(fromEmission.Tributos, fromConsult.Tributos);
        Assert.Equal(fromEmission.MonedaId, fromConsult.MonedaId);
        Assert.Equal(fromEmission.MonedaCotizacion, fromConsult.MonedaCotizacion);
        Assert.Equal(fromEmission.Cae, fromConsult.Cae);
        Assert.Equal(fromEmission.CaeVencimiento, fromConsult.CaeVencimiento);
        Assert.Equal(fromEmission.Resultado, fromConsult.Resultado);
    }

    [Fact]
    public void Snapshot_SerializaContratoVersionadoYProcedencia()
    {
        var snapshot = FiscalDocumentSnapshotFactory.FromEmission(
            SimpleRequest(),
            AuthorizedResponse(123, 7),
            new FiscalDocumentContext("produccion", "20123456786", 7));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot));
        var root = json.RootElement;

        Assert.Equal("arca-fiscal-snapshot/2.0", root.GetProperty("contractVersion").GetString());
        Assert.Equal("produccion", root.GetProperty("environment").GetString());
        Assert.Equal("20123456786", root.GetProperty("emisorCuit").GetString());
        Assert.Equal(7, root.GetProperty("puntoVenta").GetInt32());
        Assert.Equal(6, root.GetProperty("tipoComprobante").GetInt32());
        Assert.Equal(123, root.GetProperty("numeroComprobante").GetInt64());
        Assert.Equal("CAE123", root.GetProperty("cae").GetString());
        Assert.Equal("emission-request", root.GetProperty("provenance").GetProperty("fiscalData").GetString());
        Assert.Contains("importeTotal", root.GetProperty("availableFields").EnumerateArray().Select(x => x.GetString()));
        Assert.True(root.TryGetProperty("iva", out _));
        Assert.True(root.TryGetProperty("tributos", out _));
    }

    [Fact]
    public void ConsultaConImporteAusenteConvertidoACero_NoProduceSnapshotFicticio()
    {
        var consulted = ComplexConsulted(123, 7);
        consulted.ImporteIva = 0m;
        consulted.Iva.Clear();

        var error = Assert.Throws<FiscalDocumentSnapshotException>(() =>
            FiscalDocumentSnapshotFactory.FromConsult(
                consulted,
                new FiscalDocumentContext("homologacion", "20123456786", 7)));

        Assert.Equal("FISCAL_DOCUMENT_INCOMPLETE", error.Code);
    }

    [Fact]
    public void ConsultaConIvaAgregadoPeroSinDetalle_NoProduceSnapshot()
    {
        var consulted = ComplexConsulted(123, 7);
        consulted.Iva.Clear();

        var error = Assert.Throws<FiscalDocumentSnapshotException>(() =>
            FiscalDocumentSnapshotFactory.FromConsult(
                consulted,
                new FiscalDocumentContext("homologacion", "20123456786", 7)));

        Assert.Equal("FISCAL_DOCUMENT_INCOMPLETE", error.Code);
    }

    [Fact]
    public void ConsultaDeServiciosSinFechas_NoProduceSnapshot()
    {
        var consulted = ComplexConsulted(123, 7);
        consulted.FechaServicioHasta = string.Empty;

        var error = Assert.Throws<FiscalDocumentSnapshotException>(() =>
            FiscalDocumentSnapshotFactory.FromConsult(
                consulted,
                new FiscalDocumentContext("homologacion", "20123456786", 7)));

        Assert.Equal("FISCAL_DOCUMENT_INCOMPLETE", error.Code);
    }

    [Fact]
    public void ContextoHistoricoNoPuedeSerSustituidoPorOtroPuntoDeVenta()
    {
        var consulted = ComplexConsulted(123, 7);

        var error = Assert.Throws<FiscalDocumentSnapshotException>(() =>
            FiscalDocumentSnapshotFactory.FromConsult(
                consulted,
                new FiscalDocumentContext("produccion", "30999999991", 9)));

        Assert.Equal("FISCAL_CONTEXT_MISMATCH", error.Code);
    }

    [Fact]
    public void EmisionDeNota_PreservaComprobanteAsociado()
    {
        var request = SimpleRequest();
        request.TipoComprobante = dcTipoComprobante.NotaCreditoB;
        request.CbteAsociadoTipo = (int)dcTipoComprobante.FacturaB;
        request.CbteAsociadoPtoVta = 7;
        request.CbteAsociadoNro = 99;
        request.CbteAsociadoCuit = "20123456786";
        request.CbteAsociadoFecha = "20260930";

        var snapshot = FiscalDocumentSnapshotFactory.FromEmission(
            request,
            AuthorizedResponse(124, 7),
            new FiscalDocumentContext("produccion", "20123456786", 7));

        var associated = Assert.Single(snapshot.ComprobantesAsociados);
        Assert.Equal((int)dcTipoComprobante.FacturaB, associated.Tipo);
        Assert.Equal(7, associated.PuntoVenta);
        Assert.Equal(99, associated.Numero);
        Assert.Equal("20123456786", associated.Cuit);
        Assert.Equal("20260930", associated.Fecha);
        Assert.Contains("comprobantesAsociados", snapshot.AvailableFields);
    }

    [Fact]
    public void EmisionDeNota_PreservaPeriodoAsociado()
    {
        var request = SimpleRequest();
        request.TipoComprobante = dcTipoComprobante.NotaCreditoB;
        request.PeriodoAsocDesde = "20260901";
        request.PeriodoAsocHasta = "20260930";

        var snapshot = FiscalDocumentSnapshotFactory.FromEmission(
            request,
            AuthorizedResponse(124, 7),
            new FiscalDocumentContext("produccion", "20123456786", 7));

        Assert.NotNull(snapshot.PeriodoAsociado);
        Assert.Equal("20260901", snapshot.PeriodoAsociado!.Desde);
        Assert.Equal("20260930", snapshot.PeriodoAsociado.Hasta);
        Assert.Contains("periodoAsociado", snapshot.AvailableFields);
    }

    [Fact]
    public void RegeneracionDeNotaSinAsociacionDevueltaPorConsulta_FallaCerrado()
    {
        var consulted = ComplexConsulted(124, 7);
        consulted.TipoComprobante = dcTipoComprobante.NotaCreditoB;

        var error = Assert.Throws<FiscalDocumentSnapshotException>(() =>
            FiscalDocumentSnapshotFactory.FromConsult(
                consulted,
                new FiscalDocumentContext("produccion", "20123456786", 7)));

        Assert.Equal("FISCAL_DOCUMENT_ASSOCIATION_UNAVAILABLE", error.Code);
    }

    [Fact]
    public void Snapshot_NoAutorizado_SeRechaza()
    {
        var response = new dcFacturaResponse { Success = true, NumeroComprobante = 123, Cae = "" };

        var error = Assert.Throws<FiscalDocumentSnapshotException>(() =>
            FiscalDocumentSnapshotFactory.FromConsult(
                response,
                new FiscalDocumentContext("homologacion", "20123456786", 7)));

        Assert.Equal("FISCAL_DOCUMENT_NOT_AUTHORIZED", error.Code);
    }

    private static dcFacturaRequest SimpleRequest() => new()
    {
        TipoComprobante = dcTipoComprobante.FacturaB,
        Concepto = dcConcepto.Productos,
        CuitReceptor = 20333444559,
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

    private static dcFacturaRequest ComplexRequest() => new()
    {
        TipoComprobante = dcTipoComprobante.FacturaB,
        Concepto = dcConcepto.Servicios,
        CuitReceptor = 20333444559,
        TipoDocReceptor = (int)dcTipoDocumento.CUIT,
        CondicionIvaReceptor = dcCondicionIvaReceptor.ResponsableInscripto,
        ImporteNeto = 150m,
        ImporteNoGravado = 10m,
        ImporteExento = 20m,
        ImporteIva = 26.25m,
        ImporteTotal = 209.75m,
        Iva =
        [
            new dcFacturaRequest.IvaDetalle { Alicuota = dcAlicuotaIva.Veintiuno, BaseImponible = 100m, Importe = 21m },
            new dcFacturaRequest.IvaDetalle { Alicuota = dcAlicuotaIva.Diez_Cinco, BaseImponible = 50m, Importe = 5.25m }
        ],
        Tributos =
        [
            new dcFacturaRequest.TributoDetalle { Id = 99, Descripcion = "Tasa", BaseImponible = 100m, Alicuota = 3.5m, Importe = 3.5m }
        ],
        MonedaId = "DOL",
        MonedaCotizacion = 1450.25m,
        FechaComprobante = "20261001",
        FechaServicioDesde = "20260901",
        FechaServicioHasta = "20260930",
        FechaVencimiento = "20261015"
    };

    private static dcFacturaResponse AuthorizedResponse(long number, int pointOfSale) => new()
    {
        Success = true,
        EmissionOutcome = dcEmissionOutcome.Authorized,
        NumeroComprobante = number,
        PuntoVenta = pointOfSale,
        Cae = "CAE123",
        CaeVencimiento = "20261011",
        Resultado = "A"
    };

    private static dcFacturaResponse ComplexConsulted(long number, int pointOfSale)
    {
        var response = new dcFacturaResponse
        {
            Success = true,
            NumeroComprobante = number,
            PuntoVenta = pointOfSale,
            TipoComprobante = dcTipoComprobante.FacturaB,
            Concepto = dcConcepto.Servicios,
            DocTipo = dcTipoDocumento.CUIT,
            DocNro = 20333444559,
            CondicionIvaReceptor = dcCondicionIvaReceptor.ResponsableInscripto,
            FechaComprobante = "20261001",
            FechaServicioDesde = "20260901",
            FechaServicioHasta = "20260930",
            FechaVencimientoPago = "20261015",
            ImporteNeto = 150m,
            ImporteNoGravado = 10m,
            ImporteExento = 20m,
            ImporteIva = 26.25m,
            ImporteTributos = 3.5m,
            ImporteTotal = 209.75m,
            Iva =
            [
                new dcFacturaResponse.IvaDetalle { Alicuota = dcAlicuotaIva.Veintiuno, BaseImponible = 100m, Importe = 21m },
                new dcFacturaResponse.IvaDetalle { Alicuota = dcAlicuotaIva.Diez_Cinco, BaseImponible = 50m, Importe = 5.25m }
            ],
            Tributos =
            [
                new dcFacturaResponse.TributoDetalle { Id = 99, Descripcion = "Tasa", BaseImponible = 100m, Alicuota = 3.5m, Importe = 3.5m }
            ],
            MonedaId = "DOL",
            MonedaCotizacion = 1450.25m,
            Cae = "CAE123",
            CaeVencimiento = "20261011",
            Resultado = "A"
        };
        response.ConsultEvidence = ConsultEvidenceTestData.ValidFor(response);
        return response;
    }
}
