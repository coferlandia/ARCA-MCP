using System.Text.Json;
using Xunit;

namespace dcArca.McpServer.Tests;

public class FacturaArV5SchemaTests
{
    [Fact]
    public void Schema_RequiereCamposDePresentacionDelEmisor()
    {
        var schema = LoadSchema();
        var emisorRequired = schema
            .GetProperty("properties").GetProperty("emisor").GetProperty("required")
            .EnumerateArray().Select(x => x.GetString()).ToArray();

        Assert.Equal(
            new[] { "razonSocial", "domicilio", "condicionIva", "ingresosBrutos", "fechaInicioActividades" },
            emisorRequired);
    }

    [Fact]
    public void Schema_ReceptorSoloExigeRazonSocial()
    {
        var schema = LoadSchema();
        var receptorRequired = schema
            .GetProperty("properties").GetProperty("receptor").GetProperty("required")
            .EnumerateArray().Select(x => x.GetString()).ToArray();

        Assert.Equal(new[] { "razonSocial" }, receptorRequired);
    }

    [Fact]
    public void Schema_ConceptosExigenUnidadMedidaYPermitenCodigoOpcional()
    {
        var schema = LoadSchema();
        var itemSchema = schema.GetProperty("properties").GetProperty("conceptos").GetProperty("items");
        var required = itemSchema.GetProperty("required").EnumerateArray().Select(x => x.GetString()).ToArray();

        Assert.Contains("unidadMedida", required);
        Assert.Contains("descripcion", required);
        Assert.Contains("cantidad", required);
        Assert.Contains("precioUnitario", required);
        Assert.Contains("importe", required);
        Assert.DoesNotContain("codigo", required);
        Assert.True(itemSchema.GetProperty("properties").TryGetProperty("porcentajeBonificacion", out _));
        Assert.True(itemSchema.GetProperty("properties").TryGetProperty("importeBonificacion", out _));
    }

    [Fact]
    public void Schema_FiscalExigeContratoMinimoYArcaQrExigeAmbosCampos()
    {
        var schema = LoadSchema();
        var fiscalRequired = schema
            .GetProperty("properties").GetProperty("fiscal").GetProperty("required")
            .EnumerateArray().Select(x => x.GetString()).ToArray();

        Assert.Contains("cae", fiscalRequired);
        Assert.Contains("caeVencimiento", fiscalRequired);
        Assert.Contains("importeTotal", fiscalRequired);

        var arcaQrRequired = schema
            .GetProperty("properties").GetProperty("arcaQr").GetProperty("required")
            .EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Equal(new[] { "verificationUrl", "imageDataUri" }, arcaQrRequired);
    }

    private static JsonElement LoadSchema()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var schemaPath = Path.Combine(
            repositoryRoot, "deploy", "cadencia", "pdf-templates", "factura-ar-v5.schema.json");

        Assert.True(File.Exists(schemaPath), $"No existe {schemaPath}");
        return JsonDocument.Parse(File.ReadAllText(schemaPath)).RootElement;
    }
}
