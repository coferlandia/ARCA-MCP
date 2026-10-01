using System.Text.Json;
using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public class FiscalDocumentSnapshotTests
{
    [Fact]
    public void EmisionYConsulta_ProducenSnapshotFiscalEquivalente()
    {
        var config = new dcArcaConfig { Cuit = "20123456786", PuntoVenta = 7 };
        var request = new dcFacturaRequest
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
        var emitted = new dcFacturaResponse
        {
            Success = true,
            EmissionOutcome = dcEmissionOutcome.Authorized,
            NumeroComprobante = 123,
            PuntoVenta = 7,
            Cae = "CAE123",
            CaeVencimiento = "20261011",
            Resultado = "A"
        };
        var consulted = new dcFacturaResponse
        {
            Success = true,
            NumeroComprobante = 123,
            PuntoVenta = 7,
            TipoComprobante = dcTipoComprobante.FacturaB,
            Concepto = dcConcepto.Productos,
            DocTipo = dcTipoDocumento.CUIT,
            DocNro = 20333444559,
            CondicionIvaReceptor = dcCondicionIvaReceptor.ResponsableInscripto,
            FechaComprobante = "20261001",
            ImporteNeto = 100m,
            ImporteIva = 21m,
            ImporteTotal = 121m,
            MonedaId = "PES",
            MonedaCotizacion = 1m,
            Cae = "CAE123",
            CaeVencimiento = "20261011",
            Resultado = "A",
            Iva =
            [
                new dcFacturaResponse.IvaDetalle
                {
                    Alicuota = dcAlicuotaIva.Veintiuno,
                    BaseImponible = 100m,
                    Importe = 21m
                }
            ]
        };

        var fromEmission = FiscalDocumentSnapshotFactory.FromEmission(request, emitted, config);
        var fromConsult = FiscalDocumentSnapshotFactory.FromConsult(consulted, config);

        Assert.Equal(JsonSerializer.Serialize(fromEmission), JsonSerializer.Serialize(fromConsult));
    }

    [Fact]
    public void Snapshot_SerializaContratoJsonEstable()
    {
        var snapshot = new FiscalDocumentSnapshot
        {
            EmisorCuit = "20123456786",
            PuntoVenta = 7,
            TipoComprobante = 6,
            NumeroComprobante = 123,
            Concepto = 1,
            DocumentoReceptorTipo = 80,
            DocumentoReceptorNumero = 20333444559,
            CondicionIvaReceptor = 1,
            FechaComprobante = "20261001",
            ImporteNeto = 100m,
            ImporteIva = 21m,
            ImporteTotal = 121m,
            MonedaId = "PES",
            MonedaCotizacion = 1m,
            Cae = "CAE123",
            CaeVencimiento = "20261011",
            Resultado = "A"
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(snapshot));
        var root = json.RootElement;

        Assert.Equal("20123456786", root.GetProperty("emisorCuit").GetString());
        Assert.Equal(7, root.GetProperty("puntoVenta").GetInt32());
        Assert.Equal(6, root.GetProperty("tipoComprobante").GetInt32());
        Assert.Equal(123, root.GetProperty("numeroComprobante").GetInt64());
        Assert.Equal("CAE123", root.GetProperty("cae").GetString());
        Assert.True(root.TryGetProperty("iva", out _));
        Assert.True(root.TryGetProperty("tributos", out _));
    }

    [Fact]
    public void Snapshot_NoAutorizado_SeRechaza()
    {
        var response = new dcFacturaResponse { Success = true, NumeroComprobante = 123, Cae = "" };

        var error = Assert.Throws<InvalidOperationException>(() =>
            FiscalDocumentSnapshotFactory.FromConsult(response, new dcArcaConfig { Cuit = "20123456786" }));

        Assert.Equal("FISCAL_DOCUMENT_NOT_AUTHORIZED", error.Message);
    }
}
