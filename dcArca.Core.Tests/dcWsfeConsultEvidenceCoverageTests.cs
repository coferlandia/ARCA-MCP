using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;
using Xunit;

namespace dcArca.Core.Tests;

public class dcWsfeConsultEvidenceCoverageTests
{
    [Fact]
    public void MissingBalancedOptionalAmounts_AreStillMissingEvidence()
    {
        const string xml = """
        <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
          <soap:Body><ar:FECompConsultarResponse><ar:FECompConsultarResult><ar:ResultGet>
            <ar:Concepto>1</ar:Concepto><ar:DocTipo>80</ar:DocTipo><ar:DocNro>1</ar:DocNro>
            <ar:CbteDesde>1</ar:CbteDesde><ar:CbteHasta>1</ar:CbteHasta><ar:CbteTipo>6</ar:CbteTipo><ar:PtoVta>7</ar:PtoVta>
            <ar:CbteFch>20261003</ar:CbteFch><ar:ImpTotal>121</ar:ImpTotal><ar:ImpNeto>100</ar:ImpNeto><ar:ImpIVA>21</ar:ImpIVA>
            <ar:MonId>PES</ar:MonId><ar:MonCotiz>1</ar:MonCotiz><ar:CodAutorizacion>CAE</ar:CodAutorizacion><ar:FchVto>20261020</ar:FchVto>
            <ar:Iva><ar:AlicIva><ar:Id>5</ar:Id><ar:BaseImp>100</ar:BaseImp><ar:Importe>21</ar:Importe></ar:AlicIva></ar:Iva>
          </ar:ResultGet></ar:FECompConsultarResult></ar:FECompConsultarResponse></soap:Body>
        </soap:Envelope>
        """;
        var parser = new dcWsfeSoapParser(new NullLogger());

        var response = parser.ParseFECompConsultarResponse(xml, 1);

        Assert.Equal(0m, response.ImporteNoGravado);
        Assert.Equal(0m, response.ImporteExento);
        Assert.Equal(0m, response.ImporteTributos);
        Assert.Equal(dcFiscalEvidenceStatus.Missing, response.ConsultEvidence!.ImporteNoGravado);
        Assert.Equal(dcFiscalEvidenceStatus.Missing, response.ConsultEvidence.ImporteExento);
        Assert.Equal(dcFiscalEvidenceStatus.Missing, response.ConsultEvidence.ImporteTributos);
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
