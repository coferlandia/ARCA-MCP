using dcArca.Core.Models;
using Xunit;

namespace dcArca.Core.Tests;

public class dcConsultFiscalEvidenceContractTests
{
    [Fact]
    public void Statuses_DistinguishUnknownValidMissingInvalid()
    {
        Assert.NotEqual(dcFiscalEvidenceStatus.Unknown, dcFiscalEvidenceStatus.Valid);
        Assert.NotEqual(dcFiscalEvidenceStatus.Valid, dcFiscalEvidenceStatus.Missing);
        Assert.NotEqual(dcFiscalEvidenceStatus.Missing, dcFiscalEvidenceStatus.Invalid);
    }
}
