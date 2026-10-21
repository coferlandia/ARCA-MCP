using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;
using Xunit;

namespace dcArca.Core.Tests;

public class dcWsfeConsultAssociationTests
{
    private readonly dcWsfeSoapParser _parser = new(new NullLogger());

    [Fact]
    public void SingleCompleteCbteAsoc_IsParsedCorrectly()
    {
        var response = _parser.ParseFECompConsultarResponse(
            Soap(
                cbtesAsoc: new[] {
                    new { Tipo = "80", PtoVta = "5", Nro = "123", Cuit = "20123456789", CbteFch = "20260101" }
                }),
            1);

        Assert.Single(response.CbtesAsoc);
        var cbteAsoc = response.CbtesAsoc[0];
        Assert.Equal(80, cbteAsoc.Tipo);
        Assert.Equal(5, cbteAsoc.PtoVta);
        Assert.Equal(123L, cbteAsoc.Nro);
        Assert.Equal("20123456789", cbteAsoc.Cuit);
        Assert.Equal("20260101", cbteAsoc.CbteFch);
    }

    [Fact]
    public void MultipleCbtesAsoc_PreserveOrderAndQuantity()
    {
        var response = _parser.ParseFECompConsultarResponse(
            Soap(
                cbtesAsoc: new[] {
                    new { Tipo = "80", PtoVta = "5", Nro = "100", Cuit = "20111111111", CbteFch = "20260101" },
                    new { Tipo = "81", PtoVta = "6", Nro = "200", Cuit = "20222222222", CbteFch = "20260102" },
                    new { Tipo = "82", PtoVta = "7", Nro = "300", Cuit = "20333333333", CbteFch = "20260103" }
                }),
            1);

        Assert.Equal(3, response.CbtesAsoc.Count);

        // First
        Assert.Equal(80, response.CbtesAsoc[0].Tipo);
        Assert.Equal(5, response.CbtesAsoc[0].PtoVta);
        Assert.Equal(100L, response.CbtesAsoc[0].Nro);
        Assert.Equal("20111111111", response.CbtesAsoc[0].Cuit);
        Assert.Equal("20260101", response.CbtesAsoc[0].CbteFch);

        // Second
        Assert.Equal(81, response.CbtesAsoc[1].Tipo);
        Assert.Equal(6, response.CbtesAsoc[1].PtoVta);
        Assert.Equal(200L, response.CbtesAsoc[1].Nro);
        Assert.Equal("20222222222", response.CbtesAsoc[1].Cuit);
        Assert.Equal("20260102", response.CbtesAsoc[1].CbteFch);

        // Third
        Assert.Equal(82, response.CbtesAsoc[2].Tipo);
        Assert.Equal(7, response.CbtesAsoc[2].PtoVta);
        Assert.Equal(300L, response.CbtesAsoc[2].Nro);
        Assert.Equal("20333333333", response.CbtesAsoc[2].Cuit);
        Assert.Equal("20260103", response.CbtesAsoc[2].CbteFch);
    }

    [Fact]
    public void CbteAsocWithoutOptionalFields_HasNullCuitAndCbteFch()
    {
        var response = _parser.ParseFECompConsultarResponse(
            Soap(
                cbtesAsoc: new[] {
                    new { Tipo = "80", PtoVta = "5", Nro = "123", Cuit = (string?)null, CbteFch = (string?)null }
                }),
            1);

        Assert.Single(response.CbtesAsoc);
        var cbteAsoc = response.CbtesAsoc[0];
        Assert.Equal(80, cbteAsoc.Tipo);
        Assert.Equal(5, cbteAsoc.PtoVta);
        Assert.Equal(123L, cbteAsoc.Nro);
        Assert.Null(cbteAsoc.Cuit);
        Assert.Null(cbteAsoc.CbteFch);
    }

    [Fact]
    public void OnlyPeriodoAsoc_IsParsedCorrectly()
    {
        var response = _parser.ParseFECompConsultarResponse(
            Soap(
                periodoAsoc: new { FchDesde = "20260101", FchHasta = "20260131" }),
            1);

        Assert.Empty(response.CbtesAsoc);
        Assert.NotNull(response.PeriodoAsoc);
        Assert.Equal("20260101", response.PeriodoAsoc!.FchDesde);
        Assert.Equal("20260131", response.PeriodoAsoc.FchHasta);
    }

    [Fact]
    public void NoAssociations_ReturnsEmptyListsAndNull()
    {
        var response = _parser.ParseFECompConsultarResponse(Soap(), 1);

        Assert.Empty(response.CbtesAsoc);
        Assert.Null(response.PeriodoAsoc);
    }

    private static string Soap(
        object[]? cbtesAsoc = null,
        object? periodoAsoc = null)
    {
        var cbtesAsocXml = string.Empty;
        if (cbtesAsoc != null && cbtesAsoc.Length > 0)
        {
            var cbtesElements = string.Join("", cbtesAsoc.Select(cbte =>
            {
                var tipoVal = cbte.GetType().GetProperty("Tipo")?.GetValue(cbte)?.ToString() ?? "";
                var ptoVal = cbte.GetType().GetProperty("PtoVta")?.GetValue(cbte)?.ToString() ?? "";
                var nroVal = cbte.GetType().GetProperty("Nro")?.GetValue(cbte)?.ToString() ?? "";
                var cuitVal = cbte.GetType().GetProperty("Cuit")?.GetValue(cbte)?.ToString();
                var fchVal = cbte.GetType().GetProperty("CbteFch")?.GetValue(cbte)?.ToString();

                var cuitElement = cuitVal != null ? $"<ar:Cuit>{cuitVal}</ar:Cuit>" : "";
                var fchElement = fchVal != null ? $"<ar:CbteFch>{fchVal}</ar:CbteFch>" : "";

                return $"""
                    <ar:CbteAsoc>
                      <ar:Tipo>{tipoVal}</ar:Tipo>
                      <ar:PtoVta>{ptoVal}</ar:PtoVta>
                      <ar:Nro>{nroVal}</ar:Nro>
                      {cuitElement}
                      {fchElement}
                    </ar:CbteAsoc>
                    """;
            }));

            cbtesAsocXml = $"""
                <ar:CbtesAsoc>
                  {cbtesElements}
                </ar:CbtesAsoc>
                """;
        }

        var periodoAsocXml = string.Empty;
        if (periodoAsoc != null)
        {
            var fchDesde = periodoAsoc.GetType().GetProperty("FchDesde")?.GetValue(periodoAsoc)?.ToString() ?? "";
            var fchHasta = periodoAsoc.GetType().GetProperty("FchHasta")?.GetValue(periodoAsoc)?.ToString() ?? "";
            periodoAsocXml = $"""
                <ar:PeriodoAsoc>
                  <ar:FchDesde>{fchDesde}</ar:FchDesde>
                  <ar:FchHasta>{fchHasta}</ar:FchHasta>
                </ar:PeriodoAsoc>
                """;
        }

        return $"""
        <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
          <soap:Body>
            <ar:FECompConsultarResponse>
              <ar:FECompConsultarResult>
                <ar:ResultGet>
                  <ar:Concepto>1</ar:Concepto>
                  <ar:DocTipo>80</ar:DocTipo>
                  <ar:DocNro>20123456786</ar:DocNro>
                  <ar:CbteDesde>1</ar:CbteDesde>
                  <ar:CbteHasta>1</ar:CbteHasta>
                  <ar:CbteTipo>6</ar:CbteTipo>
                  <ar:PtoVta>7</ar:PtoVta>
                  <ar:CbteFch>20261003</ar:CbteFch>
                  <ar:ImpTotal>121</ar:ImpTotal>
                  <ar:ImpNeto>100</ar:ImpNeto>
                  <ar:ImpTrib>0</ar:ImpTrib>
                  <ar:ImpIVA>21</ar:ImpIVA>
                  <ar:MonId>PES</ar:MonId>
                  <ar:MonCotiz>1</ar:MonCotiz>
                  <ar:CondicionIVAReceptorId>1</ar:CondicionIVAReceptorId>
                  <ar:CodAutorizacion>12345678901234</ar:CodAutorizacion>
                  <ar:FchVto>20261020</ar:FchVto>
                  <ar:Resultado>A</ar:Resultado>
                  <ar:Iva>
                    <ar:AlicIva>
                      <ar:Id>5</ar:Id>
                      <ar:BaseImp>100</ar:BaseImp>
                      <ar:Importe>21</ar:Importe>
                    </ar:AlicIva>
                  </ar:Iva>
                  {cbtesAsocXml}
                  {periodoAsocXml}
                </ar:ResultGet>
              </ar:FECompConsultarResult>
            </ar:FECompConsultarResponse>
          </soap:Body>
        </soap:Envelope>
        """;
    }

    private sealed class NullLogger : IAfipLogger
    {
        public void LogTrace(string message) { }
        public void LogDebug(string message) { }
        public void LogInformation(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message, Exception? exception = null) { }
        public void LogCritical(string message, Exception? exception = null) { }
    }
}
