using System.Security.Claims;
using dcArca.Core.Services;

namespace dcArca.McpServer;

/// <summary>
/// Administrative V2 provisioning. Neither discovery nor validation emits an invoice.
/// Administration is scoped separately from the consumer's regular fiscal permissions.
/// </summary>
public sealed class FiscalRepresentationAdministration
{
    private readonly IFiscalTechnicalContextStore _catalog;
    private readonly IFiscalCredentialMaterializer _materializer;
    private readonly IHttpClientFactory _clients;
    private readonly dcWsfePointOfSaleProbe _probe = new();

    public FiscalRepresentationAdministration(
        IFiscalTechnicalContextStore catalog, IFiscalCredentialMaterializer materializer,
        IHttpClientFactory clients)
    {
        _catalog = catalog;
        _materializer = materializer;
        _clients = clients;
    }

    public async Task<FiscalRepresentationRecord> RegisterCandidateAsync(
        ClaimsPrincipal principal, string contextId, string consumerId, long representedCuit,
        CancellationToken cancellationToken)
    {
        DemandAdministrator(principal, contextId);
        await _catalog.RegisterCandidateAsync(contextId, consumerId, representedCuit,
            Actor(principal), cancellationToken);
        return (await _catalog.GetRepresentationAsync(contextId, consumerId, representedCuit, 0, cancellationToken))!;
    }

    public async Task<dcPointOfSaleListingResult> ListPointsOfSaleAsync(
        ClaimsPrincipal principal, string contextId, string consumerId, long representedCuit,
        CancellationToken cancellationToken)
    {
        DemandAdministrator(principal, contextId);
        var registrations = await _catalog.ListRepresentationsAsync(contextId, cancellationToken);
        if (!registrations.Any(x => x.ConsumerId == consumerId && x.RepresentedCuit == representedCuit &&
            x.Status is FiscalRepresentationStatus.Pending or FiscalRepresentationStatus.Verified or FiscalRepresentationStatus.Active))
            throw new FiscalContextAccessException("FISCAL_REPRESENTATION_CANDIDATE_REQUIRED",
                "No existe representación candidata autorizada para este consumidor/CUIT.");

        var (context, assignment) = await ActiveContextAsync(contextId, cancellationToken);
        var projected = Project(context, representedCuit, pointOfSale: 1);
        var material = _materializer.Materialize(projected, assignment);
        return await _probe.ListAsync(material.Config, material.WsfeAuth,
            _clients.CreateClient(nameof(FiscalAssignmentAuthorizationValidator)), cancellationToken);
    }

    public async Task<FiscalRepresentationRecord> SelectPointOfSaleAsync(
        ClaimsPrincipal principal, string contextId, string consumerId, long representedCuit,
        int pointOfSale, CancellationToken cancellationToken)
    {
        DemandAdministrator(principal, contextId);
        // Selection itself never implies remote authorization.
        await _catalog.SelectPointOfSaleAsync(contextId, consumerId, representedCuit,
            pointOfSale, Actor(principal), cancellationToken);
        return (await _catalog.GetRepresentationAsync(contextId, consumerId,
            representedCuit, pointOfSale, cancellationToken))!;
    }

    public async Task<FiscalRepresentationRecord> VerifyAndActivateAsync(
        ClaimsPrincipal principal, string contextId, string consumerId, long representedCuit,
        int pointOfSale, CancellationToken cancellationToken)
    {
        DemandAdministrator(principal, contextId);
        var candidate = await _catalog.GetRepresentationAsync(contextId, consumerId,
            representedCuit, pointOfSale, cancellationToken)
            ?? throw new FiscalContextAccessException("FISCAL_REPRESENTATION_NOT_FOUND",
                "La representación no está registrada.");
        if (candidate.Status is not (FiscalRepresentationStatus.Pending or FiscalRepresentationStatus.Verified or FiscalRepresentationStatus.Active or FiscalRepresentationStatus.ActionRequired))
            throw new FiscalContextAccessException("FISCAL_REPRESENTATION_STATE_INVALID",
                "La representación no está pendiente de verificación.");
        var (context, assignment) = await ActiveContextAsync(contextId, cancellationToken);
        var material = _materializer.Materialize(Project(context, representedCuit, pointOfSale), assignment);
        var probe = await _probe.ProbeAsync(material.Config, material.WsfeAuth,
            _clients.CreateClient(nameof(FiscalAssignmentAuthorizationValidator)), cancellationToken);
        if (!probe.Verified)
            throw new FiscalContextAccessException(probe.Code, probe.SafeMessage);

        // Evidence binds the concrete CUIT/PV/context/environment/credential revision.
        var evidence = $"FEParamGetPtosVenta|context={contextId}|env={context.Environment}|cuit={representedCuit}|pv={pointOfSale}|rev={assignment.AssignmentRevision}|checked={probe.CheckedAt:O}";
        await _catalog.MarkRepresentationVerifiedAsync(contextId, consumerId,
            representedCuit, pointOfSale, evidence, Actor(principal), cancellationToken);
        await _catalog.ActivateRepresentationAsync(contextId, consumerId,
            representedCuit, pointOfSale, Actor(principal), cancellationToken);
        return (await _catalog.GetRepresentationAsync(contextId, consumerId,
            representedCuit, pointOfSale, cancellationToken))!;
    }

    public async Task<FiscalRepresentationRecord> RevokeAsync(
        ClaimsPrincipal principal, string contextId, string consumerId, long representedCuit,
        int pointOfSale, CancellationToken cancellationToken)
    {
        DemandAdministrator(principal, contextId);
        await _catalog.RevokeRepresentationAsync(contextId, consumerId,
            representedCuit, pointOfSale, Actor(principal), cancellationToken);
        return (await _catalog.GetRepresentationAsync(contextId, consumerId,
            representedCuit, pointOfSale, cancellationToken))!;
    }

    private async Task<(FiscalTechnicalContextRecord, CredentialAssignmentRecord)> ActiveContextAsync(
        string contextId, CancellationToken cancellationToken)
    {
        var context = await _catalog.GetContextAsync(contextId, cancellationToken)
            ?? throw new FiscalContextAccessException("FISCAL_CONTEXT_NOT_FOUND", "No existe el contexto técnico.");
        if (context.OperationalState != FiscalContextOperationalState.Active || context.ActiveAssignment is null)
            throw new FiscalContextAccessException("FISCAL_CONTEXT_NOT_ACTIVE", "No existe credencial técnica activa.");
        return (context, context.ActiveAssignment);
    }

    private static RepresentedFiscalContextRecord Project(FiscalTechnicalContextRecord technical, long cuit, int pointOfSale)
        => new(technical.ContextId, technical.Environment, cuit, pointOfSale,
            technical.OperationalState, technical.ContextRevision, false, technical.Assignments);

    private static string Actor(ClaimsPrincipal principal)
        => principal.FindFirstValue(ArcaClaimTypes.ConsumerId)
            ?? throw new FiscalContextAccessException("CONSUMER_ID_REQUIRED", "Falta identidad administrativa.");

    private static void DemandAdministrator(ClaimsPrincipal principal, string contextId)
    {
        if (!ArcaScopeAuthorization.HasScope(principal, "arca:administrar") ||
            !principal.FindAll(ArcaClaimTypes.ContextGrant).Any(x =>
                x.Value == ArcaClaimTypes.GrantValue(contextId, "administrar")) ||
            principal.HasClaim(ArcaClaimTypes.LegacyKey, "true"))
            throw new FiscalContextAccessException("FISCAL_ADMIN_FORBIDDEN",
                "La administración requiere scope y grant administrativo explícito.");
    }
}
