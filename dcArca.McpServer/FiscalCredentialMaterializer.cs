using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;

namespace dcArca.McpServer;

public sealed record FiscalEnvironmentEndpoints(
    string WsaaUrl,
    string WsfeUrl,
    string PadronUrl);

public sealed record FiscalCredentialMaterialization(
    dcArcaConfig Config,
    dcArcaAuthService WsfeAuth,
    dcArcaAuthService PadronAuth,
    FiscalCredentialHostBinding HostBinding)
{
    public FiscalEnvironmentEndpoints Endpoints => new(Config.WsaaUrl, Config.WsfeUrl, Config.PadronUrl);
}

public sealed record FiscalPadronMaterialization(
    dcArcaConfig Config,
    dcArcaAuthService PadronAuth);

public interface IFiscalCredentialMaterializer
{
    FiscalCredentialMaterialization Materialize(
        RepresentedFiscalContextRecord context,
        CredentialAssignmentRecord assignment);

    FiscalPadronMaterialization MaterializePadron(
        FiscalTechnicalContextRecord context,
        CredentialAssignmentRecord assignment)
        => throw new NotSupportedException("TECHNICAL_PADRON_MATERIALIZATION_NOT_IMPLEMENTED");
}

public sealed class FiscalCredentialMaterializer : IFiscalCredentialMaterializer
{
    private readonly FiscalCredentialHostBindingResolver _bindingResolver;
    private readonly IAfipLogger _logger;

    public FiscalCredentialMaterializer(
        IConfiguration configuration,
        dcArcaConfig legacyConfig,
        SingleFiscalContextOptions legacyOptions,
        IAfipLogger logger)
    {
        _bindingResolver = new FiscalCredentialHostBindingResolver(
            configuration,
            new dcArcaConfig(),
            FiscalCredentialHostBindingResolver.NoLegacyFallback);
        _logger = logger;
    }

    public FiscalCredentialMaterialization Materialize(
        RepresentedFiscalContextRecord context,
        CredentialAssignmentRecord assignment)
    {
        var binding = ResolveBinding(() => _bindingResolver.Resolve(context, assignment));

        // dcArcaAuthService's cuit constructor value is its token-cache namespace.
        // WSFE obtains the represented CUIT exclusively from binding.Config.
        var wsfeAuth = new dcArcaAuthService(
            binding.Config.WsaaUrl,
            binding.Config.CertificatePath,
            binding.Config.CertificatePassword,
            binding.CacheIdentity,
            serviceName: "wsfe",
            logger: _logger);
        var padronAuth = CreatePadronAuth(binding);

        return new FiscalCredentialMaterialization(
            binding.Config,
            wsfeAuth,
            padronAuth,
            binding);
    }

    public FiscalPadronMaterialization MaterializePadron(
        FiscalTechnicalContextRecord context,
        CredentialAssignmentRecord assignment)
    {
        var binding = ResolveBinding(() => _bindingResolver.Resolve(context, assignment));
        return new FiscalPadronMaterialization(
            binding.Config,
            CreatePadronAuth(binding));
    }

    private dcArcaAuthService CreatePadronAuth(FiscalCredentialHostBinding binding)
        => new(
            binding.Config.WsaaUrl,
            binding.Config.CertificatePath,
            binding.Config.CertificatePassword,
            binding.CacheIdentity,
            serviceName: "ws_sr_constancia_inscripcion",
            logger: _logger);

    private static FiscalCredentialHostBinding ResolveBinding(
        Func<FiscalCredentialHostBinding> resolve)
    {
        try
        {
            return resolve();
        }
        catch (InvalidOperationException exception) when (
            exception.Message is
                "CREDENTIAL_REFERENCE_UNRESOLVED"
                or "CREDENTIAL_PRINCIPAL_CUIT_REQUIRED"
                or "CREDENTIAL_PRINCIPAL_CUIT_INVALID"
                or "FISCAL_ENVIRONMENT_UNRESOLVED")
        {
            var message = exception.Message switch
            {
                "CREDENTIAL_REFERENCE_UNRESOLVED" =>
                    "La referencia de credencial del contexto no está disponible en el host.",
                "CREDENTIAL_PRINCIPAL_CUIT_REQUIRED" =>
                    "La credencial técnica no tiene PrincipalCuit configurado.",
                "CREDENTIAL_PRINCIPAL_CUIT_INVALID" =>
                    "El PrincipalCuit de la credencial técnica no tiene un formato válido.",
                _ =>
                    "El ambiente fiscal del contexto no tiene endpoints server-owned completos."
            };
            throw new FiscalContextAccessException(exception.Message, message);
        }
    }
}
