using Xunit;

namespace dcArca.McpServer.Tests;

public class FacturaArV5TemplateTests
{
    private static string LoadTemplate()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var templatePath = Path.Combine(
            repositoryRoot, "deploy", "cadencia", "pdf-templates", "factura-ar-v5.html");

        Assert.True(File.Exists(templatePath), $"No existe {templatePath}");
        return File.ReadAllText(templatePath);
    }

    [Fact]
    public void ProduceLasTresCopiasComoPartesDelTemplate()
    {
        var template = LoadTemplate();

        Assert.Contains("\"ORIGINAL\"", template);
        Assert.Contains("\"DUPLICADO\"", template);
        Assert.Contains("\"TRIPLICADO\"", template);
        Assert.Contains("page-break-after", template);
    }

    [Fact]
    public void DefineLasOchoColumnasExactasDeLaTabla()
    {
        var template = LoadTemplate();

        Assert.Contains("<th>Código</th>", template);
        Assert.Contains("<th>Producto / Servicio</th>", template);
        Assert.Contains("<th>Cantidad</th>", template);
        Assert.Contains("<th>U. Medida</th>", template);
        Assert.Contains("<th>Precio Unit.</th>", template);
        Assert.Contains("<th>% Bonif</th>", template);
        Assert.Contains("<th>Imp. Bonif.</th>", template);
        Assert.Contains("<th>Subtotal</th>", template);
    }

    [Fact]
    public void IdentificaLetraYCodigoParaCadaTipoSoportado()
    {
        var template = LoadTemplate();

        Assert.Contains("fiscal.tipoComprobante == 1 or fiscal.tipoComprobante == 3", template);
        Assert.Contains("fiscal.tipoComprobante == 6 or fiscal.tipoComprobante == 8", template);
        Assert.Contains("fiscal.tipoComprobante == 11 or fiscal.tipoComprobante == 13", template);
        Assert.Contains("001", template);
        Assert.Contains("003", template);
        Assert.Contains("006", template);
        Assert.Contains("008", template);
        Assert.Contains("011", template);
        Assert.Contains("013", template);
        Assert.Contains("NOTA DE CRÉDITO", template);
        Assert.Contains("FACTURA", template);
    }

    [Fact]
    public void RenderizaPeriodoDeServiciosDesdeFiscalNoDesdeTemplateData()
    {
        var template = LoadTemplate();

        Assert.Contains("fiscal.concepto == 2 or fiscal.concepto == 3", template);
        Assert.Contains("fiscal.fechaServicioDesde", template);
        Assert.Contains("fiscal.fechaServicioHasta", template);
        Assert.Contains("fiscal.fechaVencimientoPago", template);
        Assert.Contains("Período Facturado Desde", template);
        Assert.Contains("Fecha de Vto. para el pago", template);
    }

    [Fact]
    public void RenderizaAsociacionDeNotaDeCreditoDesdeFiscal()
    {
        var template = LoadTemplate();

        Assert.Contains("fiscal.comprobantesAsociados", template);
        Assert.Contains("fiscal.periodoAsociado", template);
    }

    [Fact]
    public void IncluyeFooterConQrCaeYLeyendas()
    {
        var template = LoadTemplate();

        Assert.Contains("arcaQr.imageDataUri", template);
        Assert.Contains("ARCA", template);
        Assert.Contains("class=\"pagina\"", template);
        Assert.Contains("counter(page)", template);
        Assert.Contains("counter(pages)", template);
        Assert.Contains("fiscal.cae", template);
        Assert.Contains("fiscal.caeVencimiento", template);
        Assert.Contains("Comprobante Autorizado", template);
        Assert.Contains("Esta Agencia no se responsabiliza por los datos ingresados en el detalle de la operación", template);
    }

    [Fact]
    public void NoDependeDeJavascriptNiDeRecursosRemotos()
    {
        var template = LoadTemplate();

        Assert.DoesNotContain("<script", template, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", template);
        Assert.DoesNotContain("https://", template);
    }

    [Fact]
    public void UsaFormatoDeImpresionA4()
    {
        var template = LoadTemplate();

        Assert.Contains("@page", template);
        Assert.Contains("size: A4", template);
    }
}
