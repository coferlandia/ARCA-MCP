using System.Security.Claims;
using dcArca.Core.Models;
using dcArca.Core.Services;

namespace dcArca.McpServer;

/// <summary>
/// Administrative Padrón queries run against the technical credential itself.
/// They are intentionally independent from represented taxpayers and points of sale.
/// </summary>
public sealed class FiscalPadronAdministration
{
    private readonly IFiscalTechnicalContextStore _catalog;
    private readonly IFiscalCredentialMaterializer _materializer;
    private readonly IHttpClientFactory _clients;

    public FiscalPadronAdministration(
        IFiscalTechnicalContextStore catalog,
        IFiscalCredentialMaterializer materializer,
        IHttpClientFactory clients)
    {
        _catalog = catalog;
        _materializer = materializer;
        _clients = clients;
    }

    public async Task<dcPadronPersonaResult> GetPersonaAsync(
        ClaimsPrincipal principal,
        string contextId,
        long targetCuit,
        CancellationToken cancellationToken = default)
    {
        DemandAdministrator(principal, contextId);

        var context = await _catalog.GetContextAsync(contextId, cancellationToken)
            ?? throw new FiscalContextAccessException(
                "FISCAL_CONTEXT_NOT_FOUND",
                "No existe el contexto técnico.");

        if (context.OperationalState == FiscalContextOperationalState.Disabled)
        {
            throw new FiscalContextAccessException(
                "FISCAL_CONTEXT_DISABLED",
                "El contexto fiscal está deshabilitado.");
        }

        var assignment = context.ActiveAssignment
            ?? throw new FiscalContextAccessException(
                "ACTIVE_ASSIGNMENT_REQUIRED",
                "El contexto fiscal no tiene una asignación activa para consultas administrativas.");

        var materialized = _materializer.MaterializePadron(context, assignment);
        using var padron = new dcPadronClient(
            materialized.Config,
            materialized.PadronAuth,
            _clients.CreateClient(nameof(FiscalPadronAdministration)));

        return await padron.GetPersonaAsync(targetCuit, cancellationToken);
    }

    private static void DemandAdministrator(ClaimsPrincipal principal, string contextId)
    {
        if (!ArcaScopeAuthorization.HasScope(principal, "arca:administrar")
            || !principal.FindAll(ArcaClaimTypes.ContextGrant).Any(x =>
                x.Value == ArcaClaimTypes.GrantValue(contextId, "administrar"))
            || principal.HasClaim(ArcaClaimTypes.LegacyKey, "true"))
        {
            throw new FiscalContextAccessException(
                "FISCAL_ADMIN_FORBIDDEN",
                "La consulta administrativa de Padrón requiere scope y grant administrativo explícito.");
        }
    }
}
