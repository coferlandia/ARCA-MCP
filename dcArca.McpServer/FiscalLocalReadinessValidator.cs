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
    private readonly string? _catalogDirectory;
    private readonly IConfiguration _configuration;

    public FiscalLocalReadinessValidator(
        IEmissionRecoveryGate recoveryGate,
        string? catalogDirectory,
        IConfiguration configuration)
    {
        _recoveryGate = recoveryGate;
        _catalogDirectory = catalogDirectory;
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
        FileSystemFiscalTechnicalContextStore catalog;
        try
        {
            catalog = new FileSystemFiscalTechnicalContextStore(_catalogDirectory);
            contexts = await catalog.ListContextsAsync(cancellationToken);
            if (contexts.Count == 0)
                VerifyProvisionableCatalog(catalog.DirectoryPath);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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

                var principalCuit = credential["PrincipalCuit"];
                if (principalCuit is null)
                    return NotReady("CREDENTIAL_PRINCIPAL_CUIT_REQUIRED");
                if (principalCuit.Length != 11 || !principalCuit.All(char.IsDigit))
                    return NotReady("CREDENTIAL_PRINCIPAL_CUIT_INVALID");
            }

            if (context.OperationalState == FiscalContextOperationalState.Active
                && context.ActiveAssignment is null)
                return NotReady("FISCAL_CONTEXT_INVALID");
        }

        return new FiscalLocalReadinessResult(true);
    }

    private static void VerifyProvisionableCatalog(string directory)
    {
        var probe = Path.Combine(directory, ".readiness-write-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (new FileStream(
                probe,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose))
            {
            }
        }
        finally
        {
            try
            {
                if (File.Exists(probe))
                    File.Delete(probe);
            }
            catch
            {
                // The readiness result is determined by the original write attempt.
            }
        }
    }

    private static FiscalLocalReadinessResult NotReady(string code)
        => new(false, code);

    private static bool Missing(string? value)
        => string.IsNullOrWhiteSpace(value);
}
