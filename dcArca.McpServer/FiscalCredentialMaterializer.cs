using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;

namespace dcArca.McpServer;

public sealed record FiscalCredentialSecret(
    string CredentialId,
    string CacheIdentity,
    string CertificatePath,
    string CertificatePassword);

public sealed record FiscalEnvironmentEndpoints(
    string WsaaUrl,
    string WsfeUrl,
    string PadronUrl);

public sealed record FiscalCredentialMaterialization(
    dcArcaConfig Config,
    dcArcaAuthService WsfeAuth,
    dcArcaAuthService PadronAuth,
    FiscalCredentialSecret Secret,
    FiscalEnvironmentEndpoints Endpoints);

public interface IFiscalCredentialMaterializer
{
    FiscalCredentialMaterialization Materialize(
        RepresentedFiscalContextRecord context,
        CredentialAssignmentRecord assignment);
}

public sealed class FiscalCredentialMaterializer : IFiscalCredentialMaterializer
{
    private readonly IConfiguration _configuration;
    private readonly dcArcaConfig _legacyConfig;
    private readonly SingleFiscalContextOptions _legacyOptions;
    private readonly IAfipLogger _logger;

    public FiscalCredentialMaterializer(
        IConfiguration configuration,
        dcArcaConfig legacyConfig,
        SingleFiscalContextOptions legacyOptions,
        IAfipLogger logger)
    {
        _configuration = configuration;
        _legacyConfig = legacyConfig;
        _legacyOptions = legacyOptions;
        _logger = logger;
    }

    public FiscalCredentialMaterialization Materialize(
        RepresentedFiscalContextRecord context,
        CredentialAssignmentRecord assignment)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(assignment);

        var secret = ResolveCredentialSecret(assignment.CredentialId, context.Environment);
        var endpoints = ResolveEnvironment(context.Environment);
        var config = new dcArcaConfig
        {
            Cuit = context.RepresentedCuit.ToString(),
            PuntoVenta = context.PointOfSale,
            CertificatePath = secret.CertificatePath,
            CertificatePassword = secret.CertificatePassword,
            WsaaUrl = endpoints.WsaaUrl,
            WsfeUrl = endpoints.WsfeUrl,
            PadronUrl = endpoints.PadronUrl
        };

        // dcArcaAuthService's cuit constructor value is currently used as its cache namespace;
        // the represented CUIT is sent by dcWsfeSoapBuilder from Config.Cuit. Use an opaque
        // credential/environment identity here so two certificates never share a WSAA token.
        var wsfeAuth = new dcArcaAuthService(
            endpoints.WsaaUrl,
            secret.CertificatePath,
            secret.CertificatePassword,
            secret.CacheIdentity,
            serviceName: "wsfe",
            logger: _logger);
        var padronAuth = new dcArcaAuthService(
            endpoints.WsaaUrl,
            secret.CertificatePath,
            secret.CertificatePassword,
            secret.CacheIdentity,
            serviceName: "ws_sr_constancia_inscripcion",
            logger: _logger);

        return new FiscalCredentialMaterialization(config, wsfeAuth, padronAuth, secret, endpoints);
    }

    private FiscalCredentialSecret ResolveCredentialSecret(string credentialId, string environment)
    {
        var section = _configuration.GetSection($"FiscalCredentials:{credentialId}");
        var certificatePath = section["CertificatePath"]?.Trim();
        var certificatePassword = section["CertificatePassword"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(certificatePath)
            && string.Equals(credentialId, "legacy-default", StringComparison.Ordinal))
        {
            certificatePath = _legacyConfig.CertificatePath;
            certificatePassword = _legacyConfig.CertificatePassword;
        }

        if (string.IsNullOrWhiteSpace(certificatePath))
            throw new FiscalContextAccessException(
                "CREDENTIAL_REFERENCE_UNRESOLVED",
                "La referencia de credencial del contexto no está disponible en el host.");

        return new FiscalCredentialSecret(
            credentialId,
            $"{environment}:{credentialId}",
            certificatePath,
            certificatePassword);
    }

    private FiscalEnvironmentEndpoints ResolveEnvironment(string environment)
    {
        var section = _configuration.GetSection($"FiscalEnvironments:{environment}");
        var wsaa = section["WsaaUrl"]?.Trim();
        var wsfe = section["WsfeUrl"]?.Trim();
        var padron = section["PadronUrl"]?.Trim();

        if (string.Equals(environment, _legacyOptions.Environment, StringComparison.OrdinalIgnoreCase))
        {
            wsaa ??= _legacyConfig.WsaaUrl;
            wsfe ??= _legacyConfig.WsfeUrl;
            padron ??= _legacyConfig.PadronUrl;
        }

        if (string.IsNullOrWhiteSpace(wsaa)
            || string.IsNullOrWhiteSpace(wsfe)
            || string.IsNullOrWhiteSpace(padron))
        {
            throw new FiscalContextAccessException(
                "FISCAL_ENVIRONMENT_UNRESOLVED",
                "El ambiente fiscal del contexto no tiene endpoints server-owned completos.");
        }

        return new FiscalEnvironmentEndpoints(wsaa, wsfe, padron);
    }
}
