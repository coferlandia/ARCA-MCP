using System.Text.Json;
using dcArca.Core.Models;
using Xunit;

namespace dcArca.Core.Tests;

public class dcFacturaResponseEvidenceSerializationTests
{
    [Fact]
    public void ConsultEvidence_IsNotSerializedInPublicResponse()
    {
        var response = new dcFacturaResponse
        {
            Success = true,
            ImporteTotal = 121m,
            ConsultEvidence = dcConsultFiscalEvidence.Unknown
        };

        var json = JsonSerializer.Serialize(response);

        Assert.DoesNotContain("ConsultEvidence", json, StringComparison.OrdinalIgnoreCase);
    }
}
