using System.Text.Json;
using Xunit;

namespace dcArca.McpServer.Tests;

public class FacturaArV6ContractTests
{
    private static string RepositoryRoot => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static JsonDocument LoadSchema()
    {
        var path = Path.Combine(
            RepositoryRoot, "deploy", "cadencia", "pdf-templates", "factura-ar-v6.schema.json");

        Assert.True(File.Exists(path), $"No existe {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string LoadTemplate()
    {
        var path = Path.Combine(
            RepositoryRoot, "deploy", "cadencia", "pdf-templates", "factura-ar-v6.html");

        Assert.True(File.Exists(path), $"No existe {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void SchemaAceptaPayloadMinimoSinCamposDePresentacionOpcionales()
    {
        using var schema = LoadSchema();
        using var fixture = JsonDocument.Parse("""
        {
          "emisor": {
            "razonSocial": "Prestador Sintetico",
            "domicilio": "Calle Falsa 123",
            "condicionIva": "Responsable Monotributo"
          },
          "receptor": {
            "razonSocial": "Consumidor Final"
          },
          "conceptos": [
            {
              "descripcion": "Servicio de prueba",
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
            "importeNoGravado": 0,
            "importeExento": 0,
            "importeIva": 0,
            "importeTributos": 0,
            "importeTotal": 100,
            "monedaId": "PES",
            "monedaCotizacion": 1,
            "cae": "12345678901234",
            "caeVencimiento": "20261025"
          }
        }
        """);

        Assert.True(
            MatchesSchemaContract(schema.RootElement, fixture.RootElement),
            "El fixture minimo de factura-ar:6 debe satisfacer el schema sin IIBB, fecha inicio, unidad ni condicionVenta.");

        var emisor = schema.RootElement.GetProperty("properties").GetProperty("emisor");
        var conceptosItems = schema.RootElement
            .GetProperty("properties")
            .GetProperty("conceptos")
            .GetProperty("items");

        Assert.DoesNotContain("ingresosBrutos", RequiredNames(emisor));
        Assert.DoesNotContain("fechaInicioActividades", RequiredNames(emisor));
        Assert.DoesNotContain("unidadMedida", RequiredNames(conceptosItems));

        AssertAllowsStringOrNull(
            emisor.GetProperty("properties").GetProperty("ingresosBrutos"));
        AssertAllowsStringOrNull(
            emisor.GetProperty("properties").GetProperty("fechaInicioActividades"));
        AssertAllowsStringOrNull(
            conceptosItems.GetProperty("properties").GetProperty("unidadMedida"));
    }

    [Fact]
    public void SchemaConservaOverridesExplicitosDePresentacion()
    {
        using var schema = LoadSchema();
        using var fixture = JsonDocument.Parse("""
        {
          "emisor": {
            "razonSocial": "Prestador Sintetico",
            "domicilio": "Calle Falsa 123",
            "condicionIva": "Responsable Monotributo",
            "ingresosBrutos": "IIBB-EXPLICITO",
            "fechaInicioActividades": "01/02/2020"
          },
          "receptor": {
            "razonSocial": "Cliente Sintetico",
            "domicilio": "Otra Calle 456"
          },
          "condicionVenta": "Transferencia Bancaria",
          "conceptos": [
            {
              "codigo": "SERV-01",
              "descripcion": "Hora profesional",
              "cantidad": 2,
              "unidadMedida": "hora",
              "precioUnitario": 50,
              "porcentajeBonificacion": 0,
              "importeBonificacion": 0,
              "importe": 100
            }
          ],
          "fiscal": {
            "emisorCuit": 20236438989,
            "puntoVenta": 1,
            "tipoComprobante": 1,
            "numeroComprobante": 2,
            "fechaComprobante": "20261015",
            "importeNeto": 82.64,
            "importeNoGravado": 0,
            "importeExento": 0,
            "importeIva": 17.36,
            "importeTributos": 0,
            "importeTotal": 100,
            "iva": [
              {
                "alicuota": 21,
                "baseImponible": 82.64,
                "importe": 17.36
              }
            ],
            "monedaId": "PES",
            "monedaCotizacion": 1,
            "cae": "12345678901235",
            "caeVencimiento": "20261025"
          }
        }
        """);

        Assert.True(
            MatchesSchemaContract(schema.RootElement, fixture.RootElement),
            "El schema debe seguir aceptando overrides explicitos de presentacion.");
    }

    [Fact]
    public void SchemaAceptaFacturaCDeServiciosConPeriodoYConsumidorFinal()
    {
        using var schema = LoadSchema();
        using var fixture = JsonDocument.Parse("""
        {
          "emisor": {
            "razonSocial": "Prestador Sintetico",
            "domicilio": "Calle Falsa 123",
            "condicionIva": "Responsable Monotributo",
            "ingresosBrutos": null,
            "fechaInicioActividades": null
          },
          "receptor": {
            "razonSocial": "Consumidor Final",
            "domicilio": null
          },
          "condicionVenta": null,
          "conceptos": [
            {
              "descripcion": "Servicio profesional",
              "cantidad": 1,
              "unidadMedida": null,
              "precioUnitario": 150,
              "importe": 150
            }
          ],
          "fiscal": {
            "emisorCuit": 20236438989,
            "puntoVenta": 1,
            "tipoComprobante": 11,
            "numeroComprobante": 3,
            "fechaComprobante": "20261015",
            "concepto": 2,
            "fechaServicioDesde": "20261001",
            "fechaServicioHasta": "20261031",
            "fechaVencimientoPago": "20261110",
            "documentoReceptorTipo": 99,
            "documentoReceptorNumero": 0,
            "condicionIvaReceptor": 5,
            "importeNeto": 150,
            "importeNoGravado": 0,
            "importeExento": 0,
            "importeIva": 0,
            "importeTributos": 0,
            "importeTotal": 150,
            "monedaId": "PES",
            "monedaCotizacion": 1,
            "cae": "12345678901236",
            "caeVencimiento": "20261025"
          }
        }
        """);

        Assert.True(
            MatchesSchemaContract(schema.RootElement, fixture.RootElement),
            "Una Factura C de servicios debe aceptar período fiscal y campos opcionales null.");

        AssertAllowsStringOrNull(
            schema.RootElement.GetProperty("properties").GetProperty("condicionVenta"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void SchemaAceptaFacturaABConIvaServerOwned(int tipoComprobante)
    {
        using var schema = LoadSchema();
        var fixtureJson = """
        {
          "emisor": {
            "razonSocial": "Empresa Sintetica",
            "domicilio": "Calle Fiscal 456",
            "condicionIva": "Responsable Inscripto"
          },
          "receptor": {
            "razonSocial": "Cliente Sintetico"
          },
          "conceptos": [
            {
              "descripcion": "Producto de prueba",
              "cantidad": 2,
              "precioUnitario": 50,
              "importe": 100
            }
          ],
          "fiscal": {
            "emisorCuit": 20236438989,
            "puntoVenta": 1,
            "tipoComprobante": __TIPO_COMPROBANTE__,
            "numeroComprobante": 4,
            "fechaComprobante": "20261015",
            "importeNeto": 82.64,
            "importeNoGravado": 0,
            "importeExento": 0,
            "importeIva": 17.36,
            "importeTributos": 0,
            "importeTotal": 100,
            "iva": [
              {
                "alicuota": 21,
                "baseImponible": 82.64,
                "importe": 17.36
              }
            ],
            "monedaId": "PES",
            "monedaCotizacion": 1,
            "cae": "12345678901237",
            "caeVencimiento": "20261025"
          }
        }
        """;
        using var fixture = JsonDocument.Parse(
            fixtureJson.Replace("__TIPO_COMPROBANTE__", tipoComprobante.ToString(), StringComparison.Ordinal));

        Assert.True(
            MatchesSchemaContract(schema.RootElement, fixture.RootElement),
            $"Factura tipo {tipoComprobante} debe conservar el detalle de IVA dentro de fiscal.");
    }

    [Fact]
    public void SchemaAceptaNotaCreditoConAsociacionExclusivamenteFiscal()
    {
        using var schema = LoadSchema();
        using var fixture = JsonDocument.Parse("""
        {
          "emisor": {
            "razonSocial": "Prestador Sintetico",
            "domicilio": "Calle Falsa 123",
            "condicionIva": "Responsable Monotributo"
          },
          "receptor": {
            "razonSocial": "Cliente Sintetico"
          },
          "conceptos": [
            {
              "descripcion": "Ajuste de servicio",
              "cantidad": 1,
              "precioUnitario": 25,
              "importe": 25
            }
          ],
          "fiscal": {
            "emisorCuit": 20236438989,
            "puntoVenta": 1,
            "tipoComprobante": 13,
            "numeroComprobante": 5,
            "fechaComprobante": "20261015",
            "importeNeto": 25,
            "importeNoGravado": 0,
            "importeExento": 0,
            "importeIva": 0,
            "importeTributos": 0,
            "importeTotal": 25,
            "comprobantesAsociados": [
              {
                "tipo": 11,
                "puntoVenta": 1,
                "numero": 3
              }
            ],
            "monedaId": "PES",
            "monedaCotizacion": 1,
            "cae": "12345678901238",
            "caeVencimiento": "20261025"
          }
        }
        """);

        Assert.True(
            MatchesSchemaContract(schema.RootElement, fixture.RootElement),
            "La Nota de Crédito debe aceptar la asociación dentro del bloque fiscal server-owned.");

        Assert.False(fixture.RootElement.TryGetProperty("comprobantesAsociados", out _));
        Assert.True(
            fixture.RootElement.GetProperty("fiscal").TryGetProperty("comprobantesAsociados", out _));
    }

    [Fact]
    public void TemplateDefineFallbacksDeterministicosSinMoverVerdadFiscal()
    {
        var template = LoadTemplate();

        Assert.Contains(
            "emisor.ingresosBrutos is defined and emisor.ingresosBrutos",
            template);
        Assert.Contains(
            "{% set emisor_cuit = \"%s\"|format(fiscal.emisorCuit) %}",
            template);
        Assert.Contains("emisor_cuit|length == 11", template);
        Assert.Contains(
            "{{ emisor_cuit[0:2] }}-{{ emisor_cuit[2:10] }}-{{ emisor_cuit[10:11] }}",
            template);

        Assert.Contains(
            "emisor.fechaInicioActividades is defined and emisor.fechaInicioActividades",
            template);

        Assert.Contains(
            "item.unidadMedida is defined and item.unidadMedida",
            template);
        Assert.Contains(
            "{% else %}unidades{% endif %}",
            template);

        Assert.Contains("Condición de venta:", template);
        Assert.Contains(
            "condicionVenta is defined and condicionVenta",
            template);
        Assert.Contains(
            "{% else %}Contado{% endif %}",
            template);

        Assert.Contains("fiscal.iva", template);
        Assert.Contains("fiscal.comprobantesAsociados", template);
        Assert.Contains("arcaQr.imageDataUri", template);
    }

    private static IReadOnlyCollection<string> RequiredNames(JsonElement schema)
    {
        if (!schema.TryGetProperty("required", out var required))
            return Array.Empty<string>();

        return required.EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
    }

    private static void AssertAllowsStringOrNull(JsonElement schema)
    {
        Assert.True(schema.TryGetProperty("anyOf", out var anyOf));

        var types = anyOf.EnumerateArray()
            .Where(item => item.TryGetProperty("type", out _))
            .Select(item => item.GetProperty("type").GetString())
            .ToArray();

        Assert.Contains("string", types);
        Assert.Contains("null", types);
    }

    private static bool MatchesSchemaContract(JsonElement schema, JsonElement value)
    {
        if (schema.TryGetProperty("anyOf", out var anyOf))
            return anyOf.EnumerateArray().Any(candidate => MatchesSchemaContract(candidate, value));

        if (schema.TryGetProperty("type", out var typeProperty))
        {
            var type = typeProperty.GetString();
            var kindMatches = type switch
            {
                "object" => value.ValueKind == JsonValueKind.Object,
                "array" => value.ValueKind == JsonValueKind.Array,
                "string" => value.ValueKind == JsonValueKind.String,
                "number" => value.ValueKind == JsonValueKind.Number,
                "null" => value.ValueKind == JsonValueKind.Null,
                _ => true
            };

            if (!kindMatches)
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
                    {
                        return false;
                    }
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
