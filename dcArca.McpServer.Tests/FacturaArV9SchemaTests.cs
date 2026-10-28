using System.Text.Json;
using Xunit;

namespace dcArca.McpServer.Tests;

public class FacturaArV9SchemaTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void SchemaMantieneEmisorBaseYNombreFantasiaOpcional()
    {
        using var schema = LoadSchema();
        var emisor = schema.RootElement.GetProperty("properties").GetProperty("emisor");
        var required = RequiredNames(emisor);

        Assert.Equal(new[] { "razonSocial", "domicilio", "condicionIva" }, required);
        Assert.DoesNotContain("nombreFantasia", required);

        var types = emisor.GetProperty("properties").GetProperty("nombreFantasia")
            .GetProperty("anyOf")
            .EnumerateArray()
            .Select(x => x.GetProperty("type").GetString())
            .ToArray();

        Assert.Equal(new[] { "string", "null" }, types);
    }

    [Theory]
    [InlineData("")]
    [InlineData(", \"nombreFantasia\": \"Consultorio Muriel\"")]
    [InlineData(", \"nombreFantasia\": null")]
    [InlineData(", \"nombreFantasia\": \"   \"")]
    public void SchemaAceptaNombreFantasiaAusenteStringNullOVacio(string nombreFantasia)
    {
        using var schema = LoadSchema();
        using var payload = JsonDocument.Parse(Payload(nombreFantasia));

        Assert.True(MatchesSchemaContract(schema.RootElement, payload.RootElement));
    }

    [Fact]
    public void SchemaRechazaTipoNoStringParaNombreFantasia()
    {
        using var schema = LoadSchema();
        using var payload = JsonDocument.Parse(Payload(", \"nombreFantasia\": 123"));

        Assert.False(MatchesSchemaContract(schema.RootElement, payload.RootElement));
    }

    [Fact]
    public void RazonSocialSigueSiendoObligatoria()
    {
        using var schema = LoadSchema();
        using var payload = JsonDocument.Parse(Payload(", \"nombreFantasia\": \"Consultorio Muriel\"", includeRazonSocial: false));

        Assert.False(MatchesSchemaContract(schema.RootElement, payload.RootElement));
    }

    private static JsonDocument LoadSchema()
    {
        var path = Path.Combine(Root, "deploy", "cadencia", "pdf-templates", "factura-ar-v9.schema.json");
        Assert.True(File.Exists(path), $"No existe {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string Payload(string nombreFantasia, bool includeRazonSocial = true)
    {
        var razonSocial = includeRazonSocial ? "\"razonSocial\": \"MURIEL COFRE\"," : "";
        return $$"""
        {
          "emisor": {
            {{razonSocial}}
            "domicilio": "Calle Falsa 123",
            "condicionIva": "Responsable Monotributo"{{nombreFantasia}}
          },
          "receptor": { "razonSocial": "Consumidor Final" },
          "conceptos": [
            {
              "descripcion": "Servicio",
              "cantidad": 1,
              "precioUnitario": 100,
              "importe": 100
            }
          ],
          "fiscal": {
            "emisorCuit": 20236438989,
            "puntoVenta": 1,
            "tipoComprobante": 11,
            "numeroComprobante": 1,
            "fechaComprobante": "20261015",
            "importeNeto": 100,
            "importeIva": 0,
            "importeTributos": 0,
            "importeTotal": 100,
            "monedaId": "PES",
            "cae": "12345678901234",
            "caeVencimiento": "20261025"
          }
        }
        """;
    }

    private static IReadOnlyCollection<string> RequiredNames(JsonElement schema)
    {
        if (!schema.TryGetProperty("required", out var required))
            return Array.Empty<string>();

        return required.EnumerateArray().Select(item => item.GetString()!).ToArray();
    }

    private static bool MatchesSchemaContract(JsonElement schema, JsonElement value)
    {
        if (schema.TryGetProperty("anyOf", out var anyOf))
            return anyOf.EnumerateArray().Any(candidate => MatchesSchemaContract(candidate, value));

        if (schema.TryGetProperty("type", out var typeProperty))
        {
            var matches = typeProperty.GetString() switch
            {
                "object" => value.ValueKind == JsonValueKind.Object,
                "array" => value.ValueKind == JsonValueKind.Array,
                "string" => value.ValueKind == JsonValueKind.String,
                "number" => value.ValueKind == JsonValueKind.Number,
                "null" => value.ValueKind == JsonValueKind.Null,
                _ => true
            };
            if (!matches)
                return false;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in RequiredNames(schema))
            {
                if (!value.TryGetProperty(name, out _))
                    return false;
            }

            if (schema.TryGetProperty("properties", out var properties))
            {
                foreach (var property in properties.EnumerateObject())
                {
                    if (value.TryGetProperty(property.Name, out var child)
                        && !MatchesSchemaContract(property.Value, child))
                        return false;
                }
            }
        }

        if (value.ValueKind == JsonValueKind.Array
            && schema.TryGetProperty("items", out var items))
        {
            foreach (var child in value.EnumerateArray())
            {
                if (!MatchesSchemaContract(items, child))
                    return false;
            }
        }

        return true;
    }
}
