using dcArca.Core.Models;

namespace dcArca.McpServer.Tests;

internal static class ConsultEvidenceTestData
{
    public static dcConsultFiscalEvidence ValidFor(dcFacturaResponse response)
        => new(
            dcFiscalEvidenceStatus.Valid,
            dcFiscalEvidenceStatus.Valid,
            dcFiscalEvidenceStatus.Valid,
            dcFiscalEvidenceStatus.Valid,
            dcFiscalEvidenceStatus.Valid,
            dcFiscalEvidenceStatus.Valid,
            dcFiscalEvidenceStatus.Valid,
            response.Iva.Select(_ => new dcConsultIvaEvidence(
                dcFiscalEvidenceStatus.Valid,
                dcFiscalEvidenceStatus.Valid,
                dcFiscalEvidenceStatus.Valid)).ToArray(),
            response.Tributos.Select(_ => new dcConsultTributoEvidence(
                dcFiscalEvidenceStatus.Valid,
                dcFiscalEvidenceStatus.Valid,
                dcFiscalEvidenceStatus.Valid,
                dcFiscalEvidenceStatus.Valid)).ToArray());

    public static dcFacturaResponse MarkValid(dcFacturaResponse response)
    {
        response.ConsultEvidence = ValidFor(response);
        return response;
    }
}
