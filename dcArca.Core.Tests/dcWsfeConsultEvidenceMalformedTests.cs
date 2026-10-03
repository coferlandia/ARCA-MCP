using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;
using Xunit;

namespace dcArca.Core.Tests;

public class dcWsfeConsultEvidenceMalformedTests
{
    [Fact]
    public void MalformedCurrencyRate_IsInvalidEvidence()
    {
        const string xml = """
        <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
          <soap:Body><ar:FECompConsultarResponse><ar:FECompConsultarResult><ar:ResultGet>
            <ar:Concepto>1</ar:Concepto><ar:DocTipo>80</ar:DocTipo><ar:DocNro>1</ar:DocNro>
            <ar:CbteDesde>1</ar:CbteDesde><ar:CbteHasta>1</ar:CbteHasta><ar:CbteTipo>6</ar:CbteTipo><ar:PtoVta>7</ar:PtoVta>
            <ar:CbteFch>20261003</ar:CbteFch><ar:ImpTotal>121</ar:ImpTotal><ar:ImpNeto>100</ar:ImpNeto>
            <ar:ImpTotConc>0</ar:ImpTotConc><ar:ImpOpEx>0</ar:ImpOpEx><ar:ImpTrib>0</ar:ImpTrib><ar:ImpIVA>21</ar:ImpIVA>
            <ar:MonId>PES</ar:MonId><ar:MonCotiz>abc</ar:MonCotiz><ar:CodAutorizacion>CAE</ar:CodAutorizacion>
            <ar:Iva><ar:AlicIva><ar:Id>5</ar:Id><ar:BaseImp>100</ar:BaseImp><ar:Importe>21</ar:Importe></ar:AlicIva></ar:Iva>
          </ar:ResultGet></ar:FECompConsultarResult></ar:FECompConsultarResponse></soap:Body>
        </soap:Envelope>
        """;
        var response = new dcWsfeSoapParser(new NullLogger()).ParseFECompConsultarResponse(xml, 1);

        Assert.Equal(0m, response.MonedaCotizacion);
        Assert.Equal(dcFiscalEvidenceStatus.Invalid, response.ConsultEvidence!.MonedaCotizacion);
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
