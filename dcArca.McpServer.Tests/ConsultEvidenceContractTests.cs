using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public class ConsultEvidenceContractTests
{
    [Theory]
    [InlineData(dcFiscalEvidenceStatus.Unknown)]
    [InlineData(dcFiscalEvidenceStatus.Missing)]
    [InlineData(dcFiscalEvidenceStatus.Invalid)]
    public void OnlyValidStatusRepresentsUsableFiscalEvidence(dcFiscalEvidenceStatus status)
        => Assert.NotEqual(dcFiscalEvidenceStatus.Valid, status);
}
