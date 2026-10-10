using dcArca.Core.Services;
using Xunit;

namespace dcArca.Core.Tests;

public class WsfePointOfSaleDiscoveryTests
{
    [Theory]
    [InlineData("600", "token")]
    [InlineData("601", "CUIT")]
    [InlineData("602", "datos")]
    [InlineData("999", "error")]
    public void WsfeError_ConservaCodigoRemotoYFallaCerrado(string code, string messageFragment)
    {
        var soap = Envelope($"<Errors><Err><Code>{code}</Code><Msg>remote</Msg></Err></Errors>");
        var result = dcWsfePointOfSaleProbe.ParseListingResponse(soap, DateTimeOffset.UtcNow);
        Assert.False(result.Verified);
        Assert.Equal("WSFE_" + code, result.Code);
        Assert.Contains(messageFragment, result.SafeMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Points);
    }

    [Fact]
    public void Listar_PvCaeYBloqueadoYBaja_SinEmitir()
    {
        var soap = Envelope("""
            <ResultGet>
              <PtoVenta><Nro>7</Nro><EmisionTipo>CAE</EmisionTipo><Bloqueado>N</Bloqueado><FchBaja></FchBaja></PtoVenta>
              <PtoVenta><Nro>8</Nro><EmisionTipo>CAE</EmisionTipo><Bloqueado>S</Bloqueado></PtoVenta>
              <PtoVenta><Nro>9</Nro><EmisionTipo>CAE</EmisionTipo><Bloqueado>N</Bloqueado><FchBaja>20261001</FchBaja></PtoVenta>
              <PtoVenta><Nro>10</Nro><EmisionTipo>CAEA</EmisionTipo><Bloqueado>N</Bloqueado></PtoVenta>
            </ResultGet>
            """);
        var result = dcWsfePointOfSaleProbe.ParseListingResponse(soap, DateTimeOffset.UtcNow);
        Assert.True(result.Verified);
        Assert.Equal(4, result.Points.Count);
        Assert.True(result.Points.Single(x => x.Number == 7).EligibleForCae);
        Assert.True(result.Points.Single(x => x.Number == 8).Blocked);
        Assert.False(result.Points.Single(x => x.Number == 9).EligibleForCae);
        Assert.False(result.Points.Single(x => x.Number == 10).EligibleForCae);
    }

    [Theory]
    [InlineData("<ResultGet><PtoVenta><Nro>7</Nro></PtoVenta></ResultGet>")]
    [InlineData("<ResultGet><PtoVenta><Nro>seven</Nro><Bloqueado>N</Bloqueado></PtoVenta></ResultGet>")]
    [InlineData("<ResultGet><PtoVenta><Nro>7</Nro><Bloqueado>N</Bloqueado></PtoVenta><PtoVenta><Nro>7</Nro><Bloqueado>N</Bloqueado></PtoVenta></ResultGet>")]
    public void MalformedPv_FallaCerrado(string resultXml)
    {
        var result = dcWsfePointOfSaleProbe.ParseListingResponse(Envelope(resultXml), DateTimeOffset.UtcNow);
        Assert.False(result.Verified);
        Assert.Equal("WSFE_RESPONSE_INVALID", result.Code);
    }

    [Fact]
    public void PvManual_EsPermitidoSoloPara602SinResultados()
    {
        var listing = dcWsfePointOfSaleProbe.ParseListingResponse(
            Envelope("<Errors><Err><Code>602</Code><Msg>Sin Resultados: - Metodo FEParamGetPtosVenta</Msg></Err></Errors>"),
            DateTimeOffset.UtcNow);
        Assert.False(listing.Verified);
        var result = dcWsfePointOfSaleProbe.EvaluatePointOfSale(77, listing);
        Assert.False(result.Verified);
        Assert.True(result.CanActivate);
        Assert.Equal("POINT_OF_SALE_MANUAL_UNVERIFIED", result.Code);
        Assert.Equal(77, result.PointOfSale);
    }

    [Theory]
    [InlineData("600", "Acceso denegado")]
    [InlineData("601", "CUIT sin autorización")]
    [InlineData("602", "Otro error")]
    [InlineData("999", "Sin Resultados")]
    public void PvManual_NoPermiteRechazosNi602Distinto(string code, string message)
    {
        var listing = dcWsfePointOfSaleProbe.ParseListingResponse(
            Envelope($"<Errors><Err><Code>{code}</Code><Msg>{message}</Msg></Err></Errors>"),
            DateTimeOffset.UtcNow);
        var result = dcWsfePointOfSaleProbe.EvaluatePointOfSale(77, listing);
        Assert.False(result.CanActivate);
    }

    [Fact]
    public void PvRemotoBloqueado_SigueRechazado()
    {
        var listing = dcWsfePointOfSaleProbe.ParseListingResponse(
            Envelope("<ResultGet><PtoVenta><Nro>77</Nro><EmisionTipo>CAE</EmisionTipo><Bloqueado>S</Bloqueado></PtoVenta></ResultGet>"),
            DateTimeOffset.UtcNow);
        var result = dcWsfePointOfSaleProbe.EvaluatePointOfSale(77, listing);
        Assert.False(result.CanActivate);
        Assert.Equal("POINT_OF_SALE_BLOCKED", result.Code);
    }

    private static string Envelope(string body) =>
        "<FEParamGetPtosVentaResult>" + body + "</FEParamGetPtosVentaResult>";
}
