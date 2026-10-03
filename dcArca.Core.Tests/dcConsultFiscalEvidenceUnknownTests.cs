using dcArca.Core.Models;
using Xunit;

namespace dcArca.Core.Tests;

public class dcConsultFiscalEvidenceUnknownTests
{
    [Fact]
    public void UnknownAggregateEvidence_IsNotValid()
        => Assert.NotEqual(dcFiscalEvidenceStatus.Valid, dcConsultFiscalEvidence.Unknown.ImporteTotal);
}
