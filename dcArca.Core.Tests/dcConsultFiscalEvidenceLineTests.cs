using dcArca.Core.Models;
using Xunit;

namespace dcArca.Core.Tests;

public class dcConsultFiscalEvidenceLineTests
{
    [Fact]
    public void IvaLineEvidence_CanRepresentPartialFailure()
    {
        var evidence = new dcConsultIvaEvidence(
            dcFiscalEvidenceStatus.Valid,
            dcFiscalEvidenceStatus.Missing,
            dcFiscalEvidenceStatus.Valid);
        Assert.Equal(dcFiscalEvidenceStatus.Missing, evidence.BaseImponible);
    }
}
