using System.Text.Json;
using Xunit;

namespace dcArca.McpServer.Tests;

public class FacturaArV5IntegrationTests
{
    [Fact]
    public async Task FacturaC_RenderizaConLetraYCodigoCorrectosYArcaQr()
    {
        var client = new FakePdfClient();
        var renderer = new PdfDocumentRenderer(client);
        var fiscal = FacturaC();
        var templateData = JsonSerializer.SerializeToElement(new
        {
            emisor = new
            {
                razonSocial = "Sintetico SRL",
                domicilio = "Calle Falsa 123",
                condicionIva = "Responsable Monotributo",
                ingresosBrutos = "20123456786",
                fechaInicioActividades = "01/01/2020"
            },
            receptor = new { razonSocial = "Consumidor Final" },
            conceptos = new[]
            {
                new { descripcion = "Servicio sintético", cantidad = 1, unidadMedida = "unidad", precioUnitario = 1000m, importe = 1000m }
            }
        });

        var result = await renderer.RenderAsync(
            fiscal,
            new PdfTemplateReference("factura-ar", "5"),
            templateData);

        Assert.Equal(PdfRenderStatus.Rendered, result.Status);
        var root = client.LastData!.Value;
        Assert.Equal(11, root.GetProperty("fiscal").GetProperty("tipoComprobante").GetInt32());
        Assert.True(root.TryGetProperty("arcaQr", out var arcaQr));
        Assert.True(arcaQr.TryGetProperty("verificationUrl", out _));
        Assert.True(arcaQr.TryGetProperty("imageDataUri", out _));
    }

    [Fact]
    public async Task NotaDeCreditoC_PreservaAsociacionFiscalYMismoShell()
    {
        var client = new FakePdfClient();
        var renderer = new PdfDocumentRenderer(client);
        var fiscal = NotaDeCreditoC();
        var templateData = JsonSerializer.SerializeToElement(new
        {
            emisor = new
            {
                razonSocial = "Sintetico SRL",
                domicilio = "Calle Falsa 123",
                condicionIva = "Responsable Monotributo",
                ingresosBrutos = "20123456786",
                fechaInicioActividades = "01/01/2020"
            },
            receptor = new { razonSocial = "Consumidor Final" },
            conceptos = new[]
            {
                new { descripcion = "Ajuste de servicio", cantidad = 1, unidadMedida = "unidad", precioUnitario = 100m, importe = 100m }
            }
        });

        var result = await renderer.RenderAsync(
            fiscal,
            new PdfTemplateReference("factura-ar", "5"),
            templateData);

        Assert.Equal(PdfRenderStatus.Rendered, result.Status);
        var fiscalJson = client.LastData!.Value.GetProperty("fiscal");
        Assert.Equal(13, fiscalJson.GetProperty("tipoComprobante").GetInt32());
        var asociados = fiscalJson.GetProperty("comprobantesAsociados");
        Assert.Equal(1, asociados.GetArrayLength());
        Assert.Equal(11, asociados[0].GetProperty("tipo").GetInt32());
    }

    [Fact]
    public void TemplateData_NoPuedeAportarAsociacionNiNumeroNiCae()
    {
        var client = new FakePdfClient();
        var renderer = new PdfDocumentRenderer(client);
        var intento = JsonSerializer.SerializeToElement(new
        {
            comprobantesAsociados = new[] { new { tipo = 11, puntoVenta = 7, numero = 1 } }
        });

        Assert.Throws<ArgumentException>(() =>
            renderer.ValidateRequest(new PdfTemplateReference("factura-ar", "5"), intento));
        Assert.Equal(0, client.RenderCallCount);
    }

    private static FiscalDocumentSnapshot FacturaC() => new()
    {
        Environment = "homologacion",
        Provenance = new FiscalSnapshotProvenance("authorized-context", "test", "test"),
        AvailableFields = ["importeTotal", "cae"],
        EmisorCuit = "20123456786",
        PuntoVenta = 7,
        TipoComprobante = 11,
        NumeroComprobante = 42,
        Concepto = 1,
        DocumentoReceptorTipo = 99,
        DocumentoReceptorNumero = 0,
        CondicionIvaReceptor = 5,
        FechaComprobante = "20261001",
        ImporteNeto = 1000m,
        ImporteTotal = 1000m,
        MonedaId = "PES",
        MonedaCotizacion = 1m,
        Cae = "12345678901234",
        CaeVencimiento = "20261011",
        Resultado = "A"
    };

    private static FiscalDocumentSnapshot NotaDeCreditoC() => FacturaC() with
    {
        TipoComprobante = 13,
        NumeroComprobante = 5,
        ImporteNeto = 100m,
        ImporteTotal = 100m,
        ComprobantesAsociados = [new FiscalAssociatedDocumentSnapshot(11, 7, 42, "20123456786", "20261001")]
    };

    private sealed class FakePdfClient : IPdfClient
    {
        public int RenderCallCount { get; private set; }
        public JsonElement? LastData { get; private set; }

        public void ValidateConfiguration() { }

        public Task<byte[]> RenderAsync(PdfTemplateReference template, JsonElement data, CancellationToken cancellationToken = default)
        {
            RenderCallCount++;
            LastData = data.Clone();
            return Task.FromResult("%PDF-test"u8.ToArray());
        }
    }
}
