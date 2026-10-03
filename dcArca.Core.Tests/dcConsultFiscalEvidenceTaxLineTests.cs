using dcArca.Core.Models;
using Xunit;

namespace dcArca.Core.Tests;

public class dcConsultFiscalEvidenceTaxLineTests
{
    [Fact]
    public void TaxLineEvidence_CanRepresentInvalidAmount()
    {
        var evidence = new dcConsultTributoEvidence(
            dcFiscalEvidenceStatus.Valid,
            dcFiscalEvidenceStatus.Valid,
            dcFiscalEvidenceStatus.Valid,
            dcFiscalEvidenceStatus.Invalid);
        Assert.Equal(dcFiscalEvidenceStatus.Invalid, evidence.Importe);
    }
}
