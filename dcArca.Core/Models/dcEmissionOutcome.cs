namespace dcArca.Core.Models;

/// <summary>
/// Estado semántico de una solicitud de emisión WSFE.
/// Los valores históricos se conservan para compatibilidad de serialización.
/// </summary>
public enum dcEmissionOutcome
{
    None = 0,
    Authorized = 1,
    FiscalRejected = 2,
    RecoveredSuccess = 3,
    Uncertain = 4,
    InvalidRequest = 5,
    FailedBeforeSubmission = 6
}
