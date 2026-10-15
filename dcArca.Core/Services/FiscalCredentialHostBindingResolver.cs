using dcArca.Core.Models;
using Microsoft.Extensions.Configuration;

namespace dcArca.Core.Services;

public sealed class FiscalCredentialHostBinding
{
    private readonly dcArcaConfig _config;

    internal FiscalCredentialHostBinding(
        string credentialId,
        string cacheIdentity,
        dcArcaConfig config)
    {
        CredentialId = credentialId;
        CacheIdentity = cacheIdentity;
        _config = Clone(config ?? throw new ArgumentNullException(nameof(config)));
    }

    public string CredentialId { get; }
    public string CacheIdentity { get; }
    public string PrincipalCuit => _config.PrincipalCuit;
    public string RepresentedCuit => _config.RepresentedCuit;

    // Never expose the resolver-owned mutable configuration instance.
    public dcArcaConfig Config => Clone(_config);

    private static dcArcaConfig Clone(dcArcaConfig source)
        => new()
        {
            Environment = source.Environment,
            RepresentedCuit = source.RepresentedCuit,
            PrincipalCuit = source.PrincipalCuit,
            CertificatePath = source.CertificatePath,
            CertificatePassword = source.CertificatePassword,
            WsaaUrl = source.WsaaUrl,
            WsfeUrl = source.WsfeUrl,
            PadronUrl = source.PadronUrl,
            PuntoVenta = source.PuntoVenta
        };
}

/// <summary>
/// Resolves an opaque credential reference and a server-owned fiscal environment into an
/// immutable dcArcaConfig. Callers never provide certificate paths, passwords, principal CUITs
/// or endpoints.
/// </summary>
public sealed class FiscalCredentialHostBindingResolver
{
    public const string NoLegacyFallback = "__v2_no_fallback__";

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
        _legacyEnvironment = string.Equals(
            legacyEnvironment,
            NoLegacyFallback,
            StringComparison.Ordinal)
            ? NoLegacyFallback
            : FiscalEnvironmentId.Normalize(legacyEnvironment);
    }

    public FiscalCredentialHostBinding Resolve(
        RepresentedFiscalContextRecord context,
        CredentialAssignmentRecord assignment)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(assignment);

        return ResolveCore(
            context.Environment,
            context.RepresentedCuit.ToString(),
            context.PointOfSale,
            assignment);
    }

    /// <summary>
    /// Resolves a technical credential for operations that are not bound to a represented
    /// taxpayer or point of sale, such as administrative Padrón queries.
    /// </summary>
    public FiscalCredentialHostBinding Resolve(
        FiscalTechnicalContextRecord context,
        CredentialAssignmentRecord assignment)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(assignment);

        return ResolveCore(
            context.Environment,
            representedCuit: string.Empty,
            pointOfSale: 0,
            assignment);
    }

    private FiscalCredentialHostBinding ResolveCore(
        string environmentId,
        string representedCuit,
        int pointOfSale,
        CredentialAssignmentRecord assignment)
    {
        var canonicalEnvironment = FiscalEnvironmentId.RequireCanonical(environmentId);
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

        var principalCuit = credential["PrincipalCuit"];
        if (principalCuit is null
            && !string.Equals(_legacyEnvironment, NoLegacyFallback, StringComparison.Ordinal)
            && string.Equals(assignment.CredentialId, "legacy-default", StringComparison.Ordinal))
        {
            principalCuit = string.IsNullOrEmpty(_legacyConfig.PrincipalCuit)
                ? _legacyConfig.Cuit
                : _legacyConfig.PrincipalCuit;
        }

        if (principalCuit is null)
            throw new InvalidOperationException("CREDENTIAL_PRINCIPAL_CUIT_REQUIRED");
        if (principalCuit.Length != 11 || !principalCuit.All(char.IsDigit))
            throw new InvalidOperationException("CREDENTIAL_PRINCIPAL_CUIT_INVALID");

        var environment = _configuration.GetSection($"FiscalEnvironments:{canonicalEnvironment}");
        var wsaa = environment["WsaaUrl"]?.Trim();
        var wsfe = environment["WsfeUrl"]?.Trim();
        var padron = environment["PadronUrl"]?.Trim();
        if (string.Equals(canonicalEnvironment, _legacyEnvironment, StringComparison.Ordinal))
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
            $"{canonicalEnvironment}:{assignment.CredentialId}",
            new dcArcaConfig
            {
                Environment = canonicalEnvironment,
                RepresentedCuit = representedCuit,
                PrincipalCuit = principalCuit,
                PuntoVenta = pointOfSale,
                CertificatePath = certificatePath,
                CertificatePassword = certificatePassword,
                WsaaUrl = wsaa,
                WsfeUrl = wsfe,
                PadronUrl = padron
            });
    }
}
