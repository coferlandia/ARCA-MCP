using dcArca.Core.Models;
using Xunit;

namespace dcArca.Core.Tests;

public class dcConsultFiscalEvidenceValidTests
{
    [Fact]
    public void ValidStatus_IsDistinctFromFailureStatuses()
    {
        Assert.NotEqual(dcFiscalEvidenceStatus.Valid, dcFiscalEvidenceStatus.Missing);
        Assert.NotEqual(dcFiscalEvidenceStatus.Valid, dcFiscalEvidenceStatus.Invalid);
    }
}
