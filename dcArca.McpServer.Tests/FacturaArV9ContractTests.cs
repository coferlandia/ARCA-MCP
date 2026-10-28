using System.Text.Json;
using Xunit;

namespace dcArca.McpServer.Tests;

public class FacturaArV9ContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Load(string file) =>
        File.ReadAllText(Path.Combine(Root, "deploy", "cadencia", "pdf-templates", file));

    [Fact]
    public void ConservaContratoFiscalYQrDeV8()
    {
        using var v8 = JsonDocument.Parse(Load("factura-ar-v8.schema.json"));
        using var v9 = JsonDocument.Parse(Load("factura-ar-v9.schema.json"));

        Assert.Equal(
            v8.RootElement.GetProperty("properties").GetProperty("fiscal").GetRawText(),
            v9.RootElement.GetProperty("properties").GetProperty("fiscal").GetRawText());
        Assert.Equal(
            v8.RootElement.GetProperty("properties").GetProperty("arcaQr").GetRawText(),
            v9.RootElement.GetProperty("properties").GetProperty("arcaQr").GetRawText());

        Assert.False(v9.RootElement.GetProperty("properties").TryGetProperty("nombreFantasia", out _));
        Assert.False(
            v9.RootElement.GetProperty("properties").GetProperty("fiscal")
                .TryGetProperty("nombreFantasia", out _));
    }

    [Fact]
    public void V9SoloCambiaEncabezadoYWrappingRespectoDeV8()
    {
        var original = Load("factura-ar-v8.html");
        var updated = Load("factura-ar-v9.html");

        var expected = original.Replace(
            "<div class=\"issuer-name\">{{ emisor.razonSocial }}</div>",
            "<div class=\"issuer-name\">{% if emisor.nombreFantasia is defined and emisor.nombreFantasia is not none and emisor.nombreFantasia|trim %}{{ emisor.nombreFantasia|trim }}{% else %}{{ emisor.razonSocial }}{% endif %}</div>");
        expected = expected.Replace(
            ".issuer-name {\n  text-align: center;",
            ".issuer-name {\n  overflow-wrap: break-word;\n  text-align: center;");

        Assert.Equal(expected, updated);
    }

    [Fact]
    public void ManifestConservaV8YRegistraV9ParaRollback()
    {
        using var manifest = JsonDocument.Parse(Load("manifest.json"));
        var entries = manifest.RootElement.GetProperty("templates").EnumerateArray().ToArray();

        Assert.Contains(entries, entry => entry.GetProperty("version").GetString() == "8");
        Assert.Contains(entries, entry =>
            entry.GetProperty("key").GetString() == "factura-ar"
            && entry.GetProperty("version").GetString() == "9"
            && entry.GetProperty("template").GetString() == "factura-ar-v9.html"
            && entry.GetProperty("schema").GetString() == "factura-ar-v9.schema.json");
    }
}
