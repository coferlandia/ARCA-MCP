using System.Text;
using System.Text.Json;
using Xunit;

namespace dcArca.McpServer.Tests;

public class ArcaQrPayloadBuilderTests
{
    [Fact]
    public void Build_ProduceJsonV1ConFechaISO()
    {
        var fiscal = Snapshot();

        var payload = ArcaQrPayloadBuilder.Build(fiscal);

        Assert.Equal(1, payload.Ver);
        Assert.Equal("2026-10-01", payload.Fecha);
        Assert.Equal(20123456786L, payload.Cuit);
        Assert.Equal(7, payload.PtoVta);
        Assert.Equal(6, payload.TipoCmp);
        Assert.Equal(123L, payload.NroCmp);
        Assert.Equal(121m, payload.Importe);
        Assert.Equal("PES", payload.Moneda);
        Assert.Equal(1m, payload.Ctz);
        Assert.Equal(80, payload.TipoDocRec);
        Assert.Equal(20333444559L, payload.NroDocRec);
        Assert.Equal("E", payload.TipoCodAut);
        Assert.Equal(12345678901234L, payload.CodAut);
    }

    [Fact]
    public void Build_ReceptorAmbosAusentes_EsValido()
    {
        var fiscal = Snapshot() with { DocumentoReceptorTipo = null, DocumentoReceptorNumero = null };

        var payload = ArcaQrPayloadBuilder.Build(fiscal);

        Assert.Null(payload.TipoDocRec);
        Assert.Null(payload.NroDocRec);
    }

    [Fact]
    public void Build_ReceptorEstadoParcial_FallaCerrado()
    {
        var fiscalSoloTipo = Snapshot() with { DocumentoReceptorNumero = null };
        var fiscalSoloNumero = Snapshot() with { DocumentoReceptorTipo = null };

        Assert.Throws<ArcaQrPayloadException>(() => ArcaQrPayloadBuilder.Build(fiscalSoloTipo));
        Assert.Throws<ArcaQrPayloadException>(() => ArcaQrPayloadBuilder.Build(fiscalSoloNumero));
    }

    [Theory]
    [InlineData("202610010")]
    [InlineData("2026-10-01")]
    [InlineData("")]
    [InlineData("20261301")]
    public void Build_FechaComprobanteInvalida_FallaCerrado(string fecha)
    {
        var fiscal = Snapshot() with { FechaComprobante = fecha };

        Assert.Throws<ArcaQrPayloadException>(() => ArcaQrPayloadBuilder.Build(fiscal));
    }

    [Theory]
    [InlineData("2012345678")]
    [InlineData("201234567890")]
    [InlineData("2012345678a")]
    public void Build_CuitEmisorInvalido_FallaCerrado(string cuit)
    {
        var fiscal = Snapshot() with { EmisorCuit = cuit };

        Assert.Throws<ArcaQrPayloadException>(() => ArcaQrPayloadBuilder.Build(fiscal));
    }

    [Theory]
    [InlineData("1234567890123")]
    [InlineData("123456789012345")]
    [InlineData("1234567890123a")]
    [InlineData("")]
    public void Build_CaeInvalido_FallaCerrado(string cae)
    {
        var fiscal = Snapshot() with { Cae = cae };

        Assert.Throws<ArcaQrPayloadException>(() => ArcaQrPayloadBuilder.Build(fiscal));
    }

    [Fact]
    public void Build_PuntoVentaOCotizacionInvalidos_FallaCerrado()
    {
        Assert.Throws<ArcaQrPayloadException>(() => ArcaQrPayloadBuilder.Build(Snapshot() with { PuntoVenta = 0 }));
        Assert.Throws<ArcaQrPayloadException>(() => ArcaQrPayloadBuilder.Build(Snapshot() with { MonedaCotizacion = 0m }));
        Assert.Throws<ArcaQrPayloadException>(() => ArcaQrPayloadBuilder.Build(Snapshot() with { MonedaId = "" }));
    }

    [Fact]
    public void Build_NoVuelcaPayloadFiscalCompletoEnElMensajeDeError()
    {
        var fiscal = Snapshot() with { Cae = "bad" };

        var exception = Assert.Throws<ArcaQrPayloadException>(() => ArcaQrPayloadBuilder.Build(fiscal));

        Assert.DoesNotContain(fiscal.EmisorCuit, exception.Message);
        Assert.DoesNotContain(fiscal.NumeroComprobante.ToString(), exception.Message);
    }

    [Fact]
    public void BuildVerificationUrl_CodificaJsonBase64EnUrlOficial()
    {
        var fiscal = Snapshot();
        var payload = ArcaQrPayloadBuilder.Build(fiscal);

        var url = ArcaQrPayloadBuilder.BuildVerificationUrl(fiscal);

        Assert.StartsWith("https://www.arca.gob.ar/fe/qr/?p=", url);
        var encoded = url["https://www.arca.gob.ar/fe/qr/?p=".Length..];
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(payload.Ver, root.GetProperty("ver").GetInt32());
        Assert.Equal(payload.Fecha, root.GetProperty("fecha").GetString());
        Assert.Equal(payload.Cuit, root.GetProperty("cuit").GetInt64());
        Assert.Equal(payload.CodAut, root.GetProperty("codAut").GetInt64());
        Assert.Equal("E", root.GetProperty("tipoCodAut").GetString());
    }

    [Fact]
    public void BuildVerificationUrl_ReceptorAusente_OmiteCamposEnJson()
    {
        var fiscal = Snapshot() with { DocumentoReceptorTipo = null, DocumentoReceptorNumero = null };

        var url = ArcaQrPayloadBuilder.BuildVerificationUrl(fiscal);
        var encoded = url["https://www.arca.gob.ar/fe/qr/?p=".Length..];
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        using var document = JsonDocument.Parse(json);

        Assert.False(document.RootElement.TryGetProperty("tipoDocRec", out _));
        Assert.False(document.RootElement.TryGetProperty("nroDocRec", out _));
    }

    private static FiscalDocumentSnapshot Snapshot() => new()
    {
        Environment = "produccion",
        Provenance = new FiscalSnapshotProvenance("authorized-context", "test", "test"),
        AvailableFields = ["importeTotal", "cae"],
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
        Cae = "12345678901234",
        CaeVencimiento = "20261011",
        Resultado = "A"
    };
}
