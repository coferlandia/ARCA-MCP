using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;
using Xunit;

namespace dcArca.Core.Tests;

public class dcWsfeConsultEvidenceTests
{
    private readonly dcWsfeSoapParser _parser = new(new NullLogger());

    [Fact]
    public void ExplicitOptionalZeros_AreValidEvidence()
    {
        var response = _parser.ParseFECompConsultarResponse(Soap(), 1);

        Assert.NotNull(response.ConsultEvidence);
        Assert.Equal(dcFiscalEvidenceStatus.Valid, response.ConsultEvidence!.ImporteNoGravado);
        Assert.Equal(dcFiscalEvidenceStatus.Valid, response.ConsultEvidence.ImporteExento);
        Assert.Equal(dcFiscalEvidenceStatus.Valid, response.ConsultEvidence.ImporteTributos);
        Assert.Equal(0m, response.ImporteNoGravado);
        Assert.Equal(0m, response.ImporteExento);
        Assert.Equal(0m, response.ImporteTributos);
    }

    [Fact]
    public void MissingAggregate_IsDistinguishedFromExplicitZero()
    {
        var response = _parser.ParseFECompConsultarResponse(
            Soap(impTrib: null),
            1);

        Assert.NotNull(response.ConsultEvidence);
        Assert.Equal(dcFiscalEvidenceStatus.Missing, response.ConsultEvidence!.ImporteTributos);
        Assert.Equal(0m, response.ImporteTributos);
    }

    [Fact]
    public void InvalidAggregate_IsDistinguishedFromExplicitZero()
    {
        var response = _parser.ParseFECompConsultarResponse(
            Soap(impTrib: "abc"),
            1);

        Assert.NotNull(response.ConsultEvidence);
        Assert.Equal(dcFiscalEvidenceStatus.Invalid, response.ConsultEvidence!.ImporteTributos);
        Assert.Equal(0m, response.ImporteTributos);
    }

    [Theory]
    [InlineData(null, dcFiscalEvidenceStatus.Missing)]
    [InlineData("abc", dcFiscalEvidenceStatus.Invalid)]
    public void IvaBase_PreservesMissingOrInvalidStatus(string? baseImp, dcFiscalEvidenceStatus expected)
    {
        var response = _parser.ParseFECompConsultarResponse(
            Soap(ivaBase: baseImp),
            1);

        Assert.Single(response.Iva);
        Assert.Single(response.ConsultEvidence!.Iva);
        Assert.Equal(expected, response.ConsultEvidence.Iva[0].BaseImponible);
    }

    [Theory]
    [InlineData("base", null, dcFiscalEvidenceStatus.Missing)]
    [InlineData("base", "abc", dcFiscalEvidenceStatus.Invalid)]
    [InlineData("rate", null, dcFiscalEvidenceStatus.Missing)]
    [InlineData("rate", "abc", dcFiscalEvidenceStatus.Invalid)]
    [InlineData("amount", null, dcFiscalEvidenceStatus.Missing)]
    [InlineData("amount", "abc", dcFiscalEvidenceStatus.Invalid)]
    public void TaxLine_PreservesMissingOrInvalidStatus(string field, string? value, dcFiscalEvidenceStatus expected)
    {
        var response = _parser.ParseFECompConsultarResponse(
            Soap(
                includeTaxLine: true,
                taxBase: field == "base" ? value : "100",
                taxRate: field == "rate" ? value : "1",
                taxAmount: field == "amount" ? value : "1"),
            1);

        Assert.Single(response.Tributos);
        Assert.Single(response.ConsultEvidence!.Tributos);
        var evidence = response.ConsultEvidence.Tributos[0];
        var actual = field switch
        {
            "base" => evidence.BaseImponible,
            "rate" => evidence.Alicuota,
            _ => evidence.Importe
        };
        Assert.Equal(expected, actual);
    }

    private static string Soap(
        string? impTrib = "0",
        string? impTotConc = "0",
        string? impOpEx = "0",
        string? ivaBase = "100",
        bool includeTaxLine = false,
        string? taxBase = "100",
        string? taxRate = "1",
        string? taxAmount = "1")
    {
        static string Node(string name, string? value)
            => value is null ? string.Empty : $"<ar:{name}>{value}</ar:{name}>";

        var taxLine = includeTaxLine
            ? $"""
              <ar:Tributos>
                <ar:Tributo>
                  <ar:Id>1</ar:Id>
                  <ar:Desc>Impuesto test</ar:Desc>
                  {Node("BaseImp", taxBase)}
                  {Node("Alic", taxRate)}
                  {Node("Importe", taxAmount)}
                </ar:Tributo>
              </ar:Tributos>
              """
            : string.Empty;

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
                  {Node("ImpTotConc", impTotConc)}
                  {Node("ImpOpEx", impOpEx)}
                  {Node("ImpTrib", impTrib)}
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
                      {Node("BaseImp", ivaBase)}
                      <ar:Importe>21</ar:Importe>
                    </ar:AlicIva>
                  </ar:Iva>
                  {taxLine}
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
