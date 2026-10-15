using Xunit;

namespace dcArca.McpServer.Tests;

public class FacturaArV6TemplateTests
{
    private static string LoadTemplate()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var templatePath = Path.Combine(
            repositoryRoot, "deploy", "cadencia", "pdf-templates", "factura-ar-v6.html");

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
    public void ReplicaLaGeometriaBaseDelComprobanteArca()
    {
        var template = LoadTemplate();

        Assert.Contains("class=\"invoice-header\"", template);
        Assert.Contains("class=\"voucher-class\"", template);
        Assert.Contains("grid-template-columns: 1fr 1fr", template);
        Assert.Contains("left: 50%", template);
        Assert.Contains("transform: translateX(-50%)", template);
        Assert.Contains("class=\"receiver-box\"", template);
        Assert.Contains("class=\"totals-box\"", template);
        Assert.Contains("class=\"footer-grid\"", template);
        Assert.Contains("table.detalle tbody td { border: none;", template);
    }

    [Fact]
    public void IncluyeFooterConQrMarcaArcaCaeYLeyendas()
    {
        var template = LoadTemplate();

        Assert.Contains("arcaQr.imageDataUri", template);
        Assert.Contains("class=\"arca-brand\"", template);
        Assert.Contains("class=\"arca-word\">ARCA</div>", template);
        Assert.Contains("AGENCIA DE RECAUDACIÓN", template);
        Assert.Contains("class=\"pagina\">Pág. 1/1</div>", template);
        Assert.DoesNotContain("counter(pages)", template);
        Assert.Contains("fiscal.cae", template);
        Assert.Contains("fiscal.caeVencimiento", template);
        Assert.Contains("Comprobante Autorizado", template);
        Assert.Contains("Esta Agencia no se responsabiliza por los datos ingresados en el detalle de la operación", template);
    }

    [Fact]
    public void FormateaImportesComoElModeloConDosDecimalesYComa()
    {
        var template = LoadTemplate();

        Assert.Contains("{% macro dec2(value) -%}", template);
        Assert.Contains("(\"%.2f\"|format(value))|replace(\".\", \",")", template);
        Assert.Contains("{{ dec2(item.cantidad) }}", template);
        Assert.Contains("{{ dec2(item.precioUnitario) }}", template);
        Assert.Contains("{{ dec2(item.importe) }}", template);
        Assert.Contains("Importe Total: $", template);
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
