using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;

namespace dcArca.McpServer;

public sealed record FiscalCredentialMaterialization(
    dcArcaConfig Config,
    dcArcaAuthService WsfeAuth,
    dcArcaAuthService PadronAuth,
    FiscalCredentialHostBinding HostBinding);

public interface IFiscalCredentialMaterializer
{
    FiscalCredentialMaterialization Materialize(
        RepresentedFiscalContextRecord context,
        CredentialAssignmentRecord assignment);
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
            legacyConfig,
            legacyOptions.Environment);
        _logger = logger;
    }

    public FiscalCredentialMaterialization Materialize(
        RepresentedFiscalContextRecord context,
        CredentialAssignmentRecord assignment)
    {
        FiscalCredentialHostBinding binding;
        try
        {
            binding = _bindingResolver.Resolve(context, assignment);
        }
        catch (InvalidOperationException exception) when (
            exception.Message is "CREDENTIAL_REFERENCE_UNRESOLVED" or "FISCAL_ENVIRONMENT_UNRESOLVED")
        {
            var message = exception.Message == "CREDENTIAL_REFERENCE_UNRESOLVED"
                ? "La referencia de credencial del contexto no está disponible en el host."
                : "El ambiente fiscal del contexto no tiene endpoints server-owned completos.";
            throw new FiscalContextAccessException(exception.Message, message);
        }

        // dcArcaAuthService's cuit constructor value is currently its token-cache namespace;
        // the represented CUIT is taken from binding.Config by the WSFE SOAP builder.
        var wsfeAuth = new dcArcaAuthService(
            binding.Config.WsaaUrl,
            binding.Config.CertificatePath,
            binding.Config.CertificatePassword,
            binding.CacheIdentity,
            serviceName: "wsfe",
            logger: _logger);
        var padronAuth = new dcArcaAuthService(
            binding.Config.WsaaUrl,
            binding.Config.CertificatePath,
            binding.Config.CertificatePassword,
            binding.CacheIdentity,
            serviceName: "ws_sr_constancia_inscripcion",
            logger: _logger);

        return new FiscalCredentialMaterialization(
            binding.Config,
            wsfeAuth,
            padronAuth,
            binding);
    }
}
