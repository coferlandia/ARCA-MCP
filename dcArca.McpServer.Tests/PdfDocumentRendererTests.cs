using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace dcArca.McpServer.Tests;

public class PdfDocumentRendererTests
{
    [Fact]
    public void FiscalReservado_SeRechaza()
    {
        var client = new FakePdfClient();
        var renderer = new PdfDocumentRenderer(client);
        var data = JsonSerializer.SerializeToElement(new { fiscal = new { cae = "fake" } });

        Assert.Throws<ArgumentException>(() =>
            renderer.ValidateRequest(new PdfTemplateReference("tpl", "1.0.0"), data));
        Assert.Equal(0, client.RenderCallCount);
    }

    [Theory]
    [InlineData("Fiscal")]
    [InlineData("importeTotal")]
    [InlineData("CAE")]
    [InlineData("numeroComprobante")]
    public void AliasFiscalRaiz_SeRechazaSinRender(string propertyName)
    {
        var client = new FakePdfClient();
        var renderer = new PdfDocumentRenderer(client);
        using var document = JsonDocument.Parse($"{{\"{propertyName}\":123}}");

        Assert.Throws<ArgumentException>(() =>
            renderer.ValidateRequest(new PdfTemplateReference("tpl", "1.0.0"), document.RootElement));
        Assert.Equal(0, client.RenderCallCount);
    }

    [Fact]
    public void TemplateDataDemasiadoGrande_SeRechaza()
    {
        var client = new FakePdfClient();
        var renderer = new PdfDocumentRenderer(client);
        var data = JsonSerializer.SerializeToElement(new { descripcion = new string('x', PdfDocumentRenderer.MaxTemplateDataBytes) });

        Assert.Throws<ArgumentException>(() =>
            renderer.ValidateRequest(new PdfTemplateReference("tpl", "1.0.0"), data));
        Assert.Equal(0, client.RenderCallCount);
    }

    [Fact]
    public void TemplateDataDemasiadoProfundo_SeRechaza()
    {
        var client = new FakePdfClient();
        var renderer = new PdfDocumentRenderer(client);
        object nested = "value";
        for (var i = 0; i < PdfDocumentRenderer.MaxTemplateDataDepth + 1; i++)
            nested = new Dictionary<string, object?> { ["nivel"] = nested };
        var data = JsonSerializer.SerializeToElement(nested);

        Assert.Throws<ArgumentException>(() =>
            renderer.ValidateRequest(new PdfTemplateReference("tpl", "1.0.0"), data));
        Assert.Equal(0, client.RenderCallCount);
    }

    [Fact]
    public async Task Render_InyectaSnapshotFiscalVersionado()
    {
        var client = new FakePdfClient();
        var renderer = new PdfDocumentRenderer(client);
        var fiscal = Snapshot();

        var result = await renderer.RenderAsync(
            fiscal,
            new PdfTemplateReference("tpl", "1.0.0"),
            JsonSerializer.SerializeToElement(new
            {
                cliente = "Ñandú",
                items = new[] { new { descripcion = "Servicio", importe = 999999m } }
            }));

        Assert.Equal(PdfRenderStatus.Rendered, result.Status);
        Assert.Equal(1, client.RenderCallCount);
        Assert.NotNull(client.LastData);
        var root = client.LastData!.Value;
        Assert.Equal("Ñandú", root.GetProperty("cliente").GetString());
        Assert.True(root.TryGetProperty("items", out _));
        var fiscalJson = root.GetProperty("fiscal");
        Assert.Equal("arca-fiscal-snapshot/2.0", fiscalJson.GetProperty("contractVersion").GetString());
        Assert.Equal("produccion", fiscalJson.GetProperty("environment").GetString());
        Assert.Equal("CAE123", fiscalJson.GetProperty("cae").GetString());
        Assert.Equal(123, fiscalJson.GetProperty("numeroComprobante").GetInt64());
        Assert.Equal(121m, fiscalJson.GetProperty("importeTotal").GetDecimal());
        Assert.NotEqual(
            root.GetProperty("items")[0].GetProperty("importe").GetDecimal(),
            fiscalJson.GetProperty("importeTotal").GetDecimal());
    }

    [Fact]
    public async Task HttpError_DevuelveFailedSinFiltrarRespuesta()
    {
        var client = new FakePdfClient
        {
            RenderError = new HttpRequestException("dato fiscal secreto", null, HttpStatusCode.BadGateway)
        };
        var renderer = new PdfDocumentRenderer(client);

        var result = await renderer.RenderAsync(
            Snapshot(),
            new PdfTemplateReference("tpl", "1.0.0"),
            JsonSerializer.SerializeToElement(new { }));

        Assert.Equal(PdfRenderStatus.Failed, result.Status);
        Assert.Equal("PDF_UNAVAILABLE", result.ErrorCode);
        Assert.DoesNotContain("dato fiscal secreto", result.Message ?? string.Empty);
    }

    [Fact]
    public async Task MimeInvalido_DevuelveFailed()
    {
        var client = new FakePdfClient { RenderError = new InvalidDataException("bad mime") };
        var renderer = new PdfDocumentRenderer(client);

        var result = await renderer.RenderAsync(
            Snapshot(),
            new PdfTemplateReference("tpl", "1.0.0"),
            JsonSerializer.SerializeToElement(new { }));

        Assert.Equal(PdfRenderStatus.Failed, result.Status);
        Assert.Equal("PDF_INVALID_RESPONSE", result.ErrorCode);
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
        Cae = "CAE123",
        CaeVencimiento = "20261011",
        Resultado = "A"
    };

    private sealed class FakePdfClient : IPdfClient
    {
        public int RenderCallCount { get; private set; }
        public JsonElement? LastData { get; private set; }
        public Exception? RenderError { get; init; }

        public void ValidateConfiguration() { }

        public Task<byte[]> RenderAsync(PdfTemplateReference template, JsonElement data, CancellationToken cancellationToken = default)
        {
            RenderCallCount++;
            LastData = data.Clone();
            if (RenderError is not null) throw RenderError;
            return Task.FromResult(Encoding.ASCII.GetBytes("%PDF-test"));
        }
    }
}
