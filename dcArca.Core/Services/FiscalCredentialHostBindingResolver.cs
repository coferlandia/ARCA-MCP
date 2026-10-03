using dcArca.Core.Models;
using Microsoft.Extensions.Configuration;

namespace dcArca.Core.Services;

public sealed record FiscalCredentialHostBinding(
    string CredentialId,
    string CacheIdentity,
    dcArcaConfig Config);

/// <summary>
/// Resolves an opaque credential reference and a server-owned fiscal environment into an
/// immutable dcArcaConfig. Callers never provide certificate paths, passwords or endpoints.
/// </summary>
public sealed class FiscalCredentialHostBindingResolver
{
    private readonly IConfiguration _configuration;
    private readonly dcArcaConfig _legacyConfig;
    private readonly string _legacyEnvironment;

    public FiscalCredentialHostBindingResolver(
        IConfiguration configuration,
        dcArcaConfig legacyConfig,
        string legacyEnvironment)
    {
        _configuration = configuration;
        _legacyConfig = legacyConfig;
        _legacyEnvironment = !string.IsNullOrWhiteSpace(legacyEnvironment)
            ? legacyEnvironment.Trim()
            : throw new ArgumentException("legacyEnvironment es obligatorio.", nameof(legacyEnvironment));
    }

    public FiscalCredentialHostBinding Resolve(
        RepresentedFiscalContextRecord context,
        CredentialAssignmentRecord assignment)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(assignment);

        var credential = _configuration.GetSection($"FiscalCredentials:{assignment.CredentialId}");
        var certificatePath = credential["CertificatePath"]?.Trim();
        var certificatePassword = credential["CertificatePassword"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(certificatePath)
            && string.Equals(assignment.CredentialId, "legacy-default", StringComparison.Ordinal))
        {
            certificatePath = _legacyConfig.CertificatePath;
            certificatePassword = _legacyConfig.CertificatePassword;
        }
        if (string.IsNullOrWhiteSpace(certificatePath))
            throw new InvalidOperationException("CREDENTIAL_REFERENCE_UNRESOLVED");

        var environment = _configuration.GetSection($"FiscalEnvironments:{context.Environment}");
        var wsaa = environment["WsaaUrl"]?.Trim();
        var wsfe = environment["WsfeUrl"]?.Trim();
        var padron = environment["PadronUrl"]?.Trim();
        if (string.Equals(context.Environment, _legacyEnvironment, StringComparison.OrdinalIgnoreCase))
        {
            wsaa ??= _legacyConfig.WsaaUrl;
            wsfe ??= _legacyConfig.WsfeUrl;
            padron ??= _legacyConfig.PadronUrl;
        }
        if (string.IsNullOrWhiteSpace(wsaa)
            || string.IsNullOrWhiteSpace(wsfe)
            || string.IsNullOrWhiteSpace(padron))
            throw new InvalidOperationException("FISCAL_ENVIRONMENT_UNRESOLVED");

        return new FiscalCredentialHostBinding(
            assignment.CredentialId,
            $"{context.Environment}:{assignment.CredentialId}",
            new dcArcaConfig
            {
                Cuit = context.RepresentedCuit.ToString(),
                PuntoVenta = context.PointOfSale,
                CertificatePath = certificatePath,
                CertificatePassword = certificatePassword,
                WsaaUrl = wsaa,
                WsfeUrl = wsfe,
                PadronUrl = padron
            });
    }
}
