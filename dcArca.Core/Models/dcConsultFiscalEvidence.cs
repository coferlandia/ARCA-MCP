namespace dcArca.Core.Models;

/// <summary>
/// Estado de presencia/parseo de un dato fiscal devuelto por FECompConsultar.
/// </summary>
public enum dcFiscalEvidenceStatus
{
    Unknown = 0,
    Valid = 1,
    Missing = 2,
    Invalid = 3
}

/// <summary>
/// Metadata no fiscalizable en sí misma que conserva si los importes de FECompConsultar
/// estaban presentes y pudieron parsearse. Permite distinguir cero explícito de ausencia/error.
/// </summary>
public sealed record dcConsultFiscalEvidence(
    dcFiscalEvidenceStatus ImporteTotal,
    dcFiscalEvidenceStatus ImporteNeto,
    dcFiscalEvidenceStatus ImporteIva,
    dcFiscalEvidenceStatus ImporteNoGravado,
    dcFiscalEvidenceStatus ImporteExento,
    dcFiscalEvidenceStatus ImporteTributos,
    dcFiscalEvidenceStatus MonedaCotizacion,
    IReadOnlyList<dcConsultIvaEvidence> Iva,
    IReadOnlyList<dcConsultTributoEvidence> Tributos)
{
    public static dcConsultFiscalEvidence Unknown { get; } = new(
        dcFiscalEvidenceStatus.Unknown,
        dcFiscalEvidenceStatus.Unknown,
        dcFiscalEvidenceStatus.Unknown,
        dcFiscalEvidenceStatus.Unknown,
        dcFiscalEvidenceStatus.Unknown,
        dcFiscalEvidenceStatus.Unknown,
        dcFiscalEvidenceStatus.Unknown,
        Array.Empty<dcConsultIvaEvidence>(),
        Array.Empty<dcConsultTributoEvidence>());
}

public sealed record dcConsultIvaEvidence(
    dcFiscalEvidenceStatus Alicuota,
    dcFiscalEvidenceStatus BaseImponible,
    dcFiscalEvidenceStatus Importe);

public sealed record dcConsultTributoEvidence(
    dcFiscalEvidenceStatus Id,
    dcFiscalEvidenceStatus BaseImponible,
    dcFiscalEvidenceStatus Alicuota,
    dcFiscalEvidenceStatus Importe);
