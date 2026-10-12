using dcArca.Core.Services;

namespace dcArca.McpServer;

public sealed record FiscalLocalReadinessResult(
    bool Ready,
    string? Code = null,
    EmissionRecoveryBlock? RecoveryBlock = null);

/// <summary>
/// Local-only readiness. It validates durable/catalog/config structure and never
/// authenticates against ARCA, loads a certificate, or performs HTTP I/O.
/// </summary>
public sealed class FiscalLocalReadinessValidator
{
    private readonly IEmissionRecoveryGate _recoveryGate;
    private readonly IFiscalTechnicalContextStore _catalog;
    private readonly IConfiguration _configuration;

    public FiscalLocalReadinessValidator(
        IEmissionRecoveryGate recoveryGate,
        IFiscalTechnicalContextStore catalog,
        IConfiguration configuration)
    {
        _recoveryGate = recoveryGate;
        _catalog = catalog;
        _configuration = configuration;
    }

    public async Task<FiscalLocalReadinessResult> ValidateAsync(
        CancellationToken cancellationToken = default)
    {
        EmissionRecoveryBlock? block;
        try
        {
            block = await _recoveryGate.GetBlockAsync(cancellationToken);
        }
        catch
        {
            return NotReady("RECOVERY_GATE_UNREADABLE");
        }

        if (block is not null)
            return new FiscalLocalReadinessResult(
                false,
                "RESTORE_RECONCILIATION_REQUIRED",
                block);

        IReadOnlyList<FiscalTechnicalContextRecord> contexts;
        try
        {
            contexts = await _catalog.ListContextsAsync(cancellationToken);
        }
        catch
        {
            return NotReady("FISCAL_CATALOG_UNREADABLE");
        }

        foreach (var context in contexts)
        {
            if (!FiscalEnvironmentId.IsCanonical(context.Environment))
                return NotReady("FISCAL_CATALOG_UNREADABLE");

            var environment = _configuration.GetSection(
                $"FiscalEnvironments:{context.Environment}");
            if (Missing(environment["WsaaUrl"])
                || Missing(environment["WsfeUrl"])
                || Missing(environment["PadronUrl"]))
                return NotReady("FISCAL_ENVIRONMENT_UNRESOLVED");

            foreach (var assignment in context.Assignments.Where(
                x => x.Status == CredentialAssignmentStatus.Active))
            {
                var credential = _configuration.GetSection(
                    $"FiscalCredentials:{assignment.CredentialId}");
                if (Missing(credential["CertificatePath"]))
                    return NotReady("CREDENTIAL_REFERENCE_UNRESOLVED");
            }

            if (context.OperationalState == FiscalContextOperationalState.Active
                && context.ActiveAssignment is null)
                return NotReady("FISCAL_CONTEXT_INVALID");
        }

        return new FiscalLocalReadinessResult(true);
    }

    private static FiscalLocalReadinessResult NotReady(string code)
        => new(false, code);

    private static bool Missing(string? value)
        => string.IsNullOrWhiteSpace(value);
}
