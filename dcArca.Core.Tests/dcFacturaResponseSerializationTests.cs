using System.Text.Json;
using dcArca.Core.Models;
using Xunit;

namespace dcArca.Core.Tests;

public class dcFacturaResponseSerializationTests
{
    [Fact]
    public void MetadatosMcp_NoAparecenFueraDelMcpCuandoNoFueronCompletados()
    {
        var json = JsonSerializer.Serialize(new dcFacturaResponse
        {
            Success = true,
            NumeroComprobante = 1,
            Cae = "123"
        });

        Assert.DoesNotContain("OperationId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("OperationContractVersion", json, StringComparison.Ordinal);
        Assert.DoesNotContain("OperationContextId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("OperationState", json, StringComparison.Ordinal);
        Assert.DoesNotContain("AllowedNextActions", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MetadatosMcp_AparecenCuandoLaOrquestacionLosCompleta()
    {
        var json = JsonSerializer.Serialize(new dcFacturaResponse
        {
            OperationId = "abc",
            OperationContractVersion = "arca-mcp/1.0",
            OperationContextId = "ctx-a",
            OperationState = "Authorized",
            AllowedNextActions = ["consult"]
        });

        Assert.Contains("\"OperationId\":\"abc\"", json, StringComparison.Ordinal);
        Assert.Contains("\"AllowedNextActions\":[\"consult\"]", json, StringComparison.Ordinal);
    }

    [Fact]
    public void CbtesAsoc_AparecenEnJsonCuandoTienenDatos()
    {
        var json = JsonSerializer.Serialize(new dcFacturaResponse
        {
            Success = true,
            NumeroComprobante = 1,
            Cae = "123",
            CbtesAsoc = new()
            {
                new dcFacturaResponse.CbteAsocDetalle
                {
                    Tipo = 80,
                    PtoVta = 5,
                    Nro = 123,
                    Cuit = "20123456789",
                    CbteFch = "20260101"
                }
            }
        });

        Assert.Contains("\"CbtesAsoc\":", json, StringComparison.Ordinal);
        Assert.Contains("\"Tipo\":80", json, StringComparison.Ordinal);
        Assert.Contains("\"PtoVta\":5", json, StringComparison.Ordinal);
        Assert.Contains("\"Nro\":123", json, StringComparison.Ordinal);
        Assert.Contains("\"Cuit\":\"20123456789\"", json, StringComparison.Ordinal);
        Assert.Contains("\"CbteFch\":\"20260101\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PeriodoAsoc_ApareceEnJsonCuandoTieneDatos()
    {
        var json = JsonSerializer.Serialize(new dcFacturaResponse
        {
            Success = true,
            NumeroComprobante = 1,
            Cae = "123",
            PeriodoAsoc = new dcFacturaResponse.PeriodoAsocDetalle
            {
                FchDesde = "20260101",
                FchHasta = "20260131"
            }
        });

        Assert.Contains("\"PeriodoAsoc\":", json, StringComparison.Ordinal);
        Assert.Contains("\"FchDesde\":\"20260101\"", json, StringComparison.Ordinal);
        Assert.Contains("\"FchHasta\":\"20260131\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void CbtesAsocVacioYPeriodoAsocNull_SerializanComoListaVaciaYNullExplicito()
    {
        var json = JsonSerializer.Serialize(new dcFacturaResponse
        {
            Success = true,
            NumeroComprobante = 1,
            Cae = "123",
            CbtesAsoc = new(),
            PeriodoAsoc = null
        });

        // Sin [JsonIgnore(Condition = WhenWritingNull)], igual que Concepto/DocTipo: el campo
        // sigue presente para distinguir "sin asociación" de "campo no modelado".
        Assert.Contains("\"CbtesAsoc\":[]", json, StringComparison.Ordinal);
        Assert.Contains("\"PeriodoAsoc\":null", json, StringComparison.Ordinal);
    }
}
