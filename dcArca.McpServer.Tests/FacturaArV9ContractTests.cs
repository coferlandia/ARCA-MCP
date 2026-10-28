using System.Text.Json;
using Xunit;

namespace dcArca.McpServer.Tests;

public class FacturaArV9ContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    private static string Load(string file) => File.ReadAllText(Path.Combine(Root, "deploy", "cadencia", "pdf-templates", file));

    [Fact]
    public void SchemaConservaCamposFiscalesYRequiereRazonSocial()
    {
        using var v8 = JsonDocument.Parse(Load("factura-ar-v8.schema.json"));
        using var v9 = JsonDocument.Parse(Load("factura-ar-v9.schema.json"));
        var oldEmisor = v8.RootElement.GetProperty("properties").GetProperty("emisor");
        var newEmisor = v9.RootElement.GetProperty("properties").GetProperty("emisor");
        Assert.Equal(oldEmisor.GetProperty("required").GetRawText(), newEmisor.GetProperty("required").GetRawText());
        Assert.Contains("razonSocial", newEmisor.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(
            v8.RootElement.GetProperty("properties").GetProperty("fiscal").GetRawText(),
            v9.RootElement.GetProperty("properties").GetProperty("fiscal").GetRawText());
        Assert.Equal(
            v8.RootElement.GetProperty("properties").GetProperty("arcaQr").GetRawText(),
            v9.RootElement.GetProperty("properties").GetProperty("arcaQr").GetRawText());
        Assert.False(v9.RootElement.GetProperty("properties").TryGetProperty("nombreFantasia", out _));
    }

    [Fact]
    public void SchemaPermiteNombreFantasiaAusenteStringONull()
    {
        using var doc = JsonDocument.Parse(Load("factura-ar-v9.schema.json"));
        var emisor = doc.RootElement.GetProperty("properties").GetProperty("emisor");
        Assert.DoesNotContain("nombreFantasia", emisor.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        var alternatives = emisor.GetProperty("properties").GetProperty("nombreFantasia").GetProperty("anyOf")
            .EnumerateArray().Select(x => x.GetProperty("type").GetString()).ToArray();
        Assert.Equal(new[] { "string", "null" }, alternatives);
    }

    [Fact]
    public void EncabezadoUsaNombreFantasiaSoloSiTieneContenidoYConservaRazonSocial()
    {
        var template = Load("factura-ar-v9.html");
        Assert.Contains("emisor.nombreFantasia is defined", template);
        Assert.Contains("emisor.nombreFantasia is not none", template);
        Assert.Contains("emisor.nombreFantasia|trim", template);
        Assert.Contains("{% else %}{{ emisor.razonSocial }}{% endif %}", template);
        Assert.Contains("Razón Social:</span><span>{{ emisor.razonSocial }}", template);
        Assert.Contains("overflow-wrap: anywhere", template);
        Assert.Contains("word-break: break-word", template);
        Assert.Contains("for copia in [\"ORIGINAL\", \"DUPLICADO\", \"TRIPLICADO\"]", template);
        foreach (var code in new[] { "== 1", "== 3", "== 6", "== 8", "== 11", "== 13" })
            Assert.Contains(code, template);
    }

    [Fact]
    public void V9NoModificaRestoDeLaPlantillaV8()
    {
        var original = Load("factura-ar-v8.html");
        var updated = Load("factura-ar-v9.html");
        var expected = original.Replace(
            "<div class=\"issuer-name\">{{ emisor.razonSocial }}</div>",
            "<div class=\"issuer-name\">{% if emisor.nombreFantasia is defined and emisor.nombreFantasia is not none and emisor.nombreFantasia|trim %}{{ emisor.nombreFantasia|trim }}{% else %}{{ emisor.razonSocial }}{% endif %}</div>");
        expected = expected.Replace(".issuer-name {\n  text-align: center;", ".issuer-name {\n  overflow-wrap: anywhere;\n  word-break: break-word;\n  text-align: center;");
        Assert.Equal(expected, updated);
    }

    [Fact]
    public void ManifestIncluyeV8YV9ParaRollback()
    {
        using var manifest = JsonDocument.Parse(Load("manifest.json"));
        var entries = manifest.RootElement.GetProperty("templates").EnumerateArray().ToArray();
        Assert.Contains(entries, entry => entry.GetProperty("version").GetString() == "8");
        Assert.Contains(entries, entry => entry.GetProperty("version").GetString() == "9"
            && entry.GetProperty("template").GetString() == "factura-ar-v9.html"
            && entry.GetProperty("schema").GetString() == "factura-ar-v9.schema.json");
    }
}
