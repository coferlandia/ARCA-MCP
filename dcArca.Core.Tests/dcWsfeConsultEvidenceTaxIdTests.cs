using dcArca.Core.Models;
using Xunit;

namespace dcArca.Core.Tests;

public class dcWsfeConsultEvidenceTaxIdTests
{
    [Fact]
    public void UnknownEvidence_DefaultIsNotValid()
    {
        Assert.Equal(dcFiscalEvidenceStatus.Unknown, dcConsultFiscalEvidence.Unknown.ImporteTotal);
        Assert.NotEqual(dcFiscalEvidenceStatus.Valid, dcConsultFiscalEvidence.Unknown.ImporteTotal);
    }
}
