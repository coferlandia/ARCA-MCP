using dcArca.Core.Models;
using Xunit;

namespace dcArca.Core.Tests;

public class dcConsultFiscalEvidenceCompatibilityTests
{
    [Fact]
    public void Metadata_IsSeparateFromExistingMonetaryProperties()
    {
        var response = new dcFacturaResponse
        {
            ImporteTributos = 0m,
            ConsultEvidence = dcConsultFiscalEvidence.Unknown
        };
        Assert.Equal(0m, response.ImporteTributos);
        Assert.Equal(dcFiscalEvidenceStatus.Unknown, response.ConsultEvidence.ImporteTributos);
    }
}
