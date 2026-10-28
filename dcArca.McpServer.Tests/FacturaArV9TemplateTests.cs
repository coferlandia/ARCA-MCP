using Xunit;

namespace dcArca.McpServer.Tests;

public class FacturaArV9TemplateTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string LoadTemplate()
    {
        var path = Path.Combine(Root, "deploy", "cadencia", "pdf-templates", "factura-ar-v9.html");
        Assert.True(File.Exists(path), $"No existe {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void EncabezadoUsaNombreFantasiaNoBlancoYFallbackRazonSocial()
    {
        var template = LoadTemplate();

        Assert.Contains(
            "{% if emisor.nombreFantasia is defined and emisor.nombreFantasia is not none and emisor.nombreFantasia|trim %}",
            template);
        Assert.Contains("{{ emisor.nombreFantasia|trim }}", template);
        Assert.Contains("{% else %}{{ emisor.razonSocial }}{% endif %}", template);
    }

    [Fact]
    public void RazonSocialPermaneceVisibleAunqueExistaNombreFantasia()
    {
        var template = LoadTemplate();

        Assert.Contains(
            "<span class=\"info-label\">Razón Social:</span><span>{{ emisor.razonSocial }}</span>",
            template);
    }

    [Fact]
    public void NombreLargoSoloAjustaElBloqueIssuerName()
    {
        var template = LoadTemplate();
        var start = template.IndexOf(".issuer-name {", StringComparison.Ordinal);
        var end = template.IndexOf('}', start);
        var block = template[start..(end + 1)];

        Assert.Contains("overflow-wrap: break-word", block);
        Assert.DoesNotContain("overflow-wrap: break-word", template[(end + 1)..]);
    }

    [Fact]
    public void ConservaTresCopiasYTiposDeComprobanteDeV8()
    {
        var template = LoadTemplate();

        Assert.Contains("for copia in [\"ORIGINAL\", \"DUPLICADO\", \"TRIPLICADO\"]", template);
        foreach (var tipo in new[] { 1, 3, 6, 8, 11, 13 })
            Assert.Contains($"fiscal.tipoComprobante == {tipo}", template);
    }
}
