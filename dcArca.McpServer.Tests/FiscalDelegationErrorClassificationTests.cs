using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public sealed class FiscalDelegationErrorClassificationTests
{
    [Theory]
    [InlineData("600", "WSFE_600")]
    [InlineData("WSFE_601", "WSFE_601")]
    public void RechazoExplicitoDeDelegacion_RequiereIntervencion(string code, string expected)
    {
        var response = new dcFacturaResponse { Success = false, Codigo = code };
        Assert.Equal(expected, ArcaTools.DelegationFailureCode(response));
    }

    [Theory]
    [InlineData("[600] token/firma no autorizada", "WSFE_600")]
    [InlineData("[601] representada no incluida", "WSFE_601")]
    public void WSFEErrorsEnColeccion_PreservaCodigo(string message, string expected)
    {
        var response = new dcFacturaResponse { Success = false, Errores = [message] };
        Assert.Equal(expected, ArcaTools.DelegationFailureCode(response));
    }

    [Fact]
    public void Codigo602_NoSeInterpretaComoRevocacion()
    {
        Assert.Null(ArcaTools.DelegationFailureCode(new dcFacturaResponse
        {
            Success = false, Errores = ["[602] No existen datos en nuestros registros"]
        }));
    }

    [Fact]
    public void RespuestaAutorizada_NoSeSuspendeInclusoConObservaciones()
    {
        Assert.Null(ArcaTools.DelegationFailureCode(new dcFacturaResponse
        {
            Success = true, Codigo = "WSFE_600", Errores = ["[600] observación histórica"]
        }));
    }
}
