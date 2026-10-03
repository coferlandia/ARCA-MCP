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
}
