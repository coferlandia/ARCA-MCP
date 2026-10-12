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
    private readonly FiscalRepresentationLifecyclePolicy _lifecycle;
    private readonly IFiscalCredentialMaterializer _materializer;
    private readonly IHttpClientFactory _clients;
    private readonly dcWsfePointOfSaleProbe _probe = new();

    public FiscalRepresentationAdministration(
        IFiscalTechnicalContextStore catalog,
        FiscalRepresentationLifecyclePolicy lifecycle,
        IFiscalCredentialMaterializer materializer,
        IHttpClientFactory clients)
    {
        _catalog = catalog;
        _lifecycle = lifecycle;
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
        var candidate = await _catalog.GetRepresentationAsync(
            contextId, consumerId, representedCuit, pointOfSale, cancellationToken)
            ?? throw new FiscalContextAccessException(
                "FISCAL_REPRESENTATION_NOT_FOUND",
                "La representación no está registrada.");
        if (!FiscalRepresentationLifecyclePolicy.IsRevalidableStatus(candidate.Status))
            throw new FiscalContextAccessException(
                "FISCAL_REPRESENTATION_STATE_INVALID",
                "La representación no está pendiente de verificación.");

        var (context, assignment) = await ActiveContextAsync(contextId, cancellationToken);
        var material = _materializer.Materialize(
            Project(context, representedCuit, pointOfSale),
            assignment);
        FiscalRepresentationLifecycleResult lifecycle;
        try
        {
            var outcome = await _probe.ProbeBoundAsync(
                context,
                assignment,
                candidate,
                representedCuit,
                pointOfSale,
                material.HostBinding,
                _clients.CreateClient(nameof(FiscalAssignmentAuthorizationValidator)),
                cancellationToken);
            lifecycle = await _lifecycle.ApplyAsync(
                consumerId,
                outcome,
                Actor(principal),
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            var mapped = MapLifecycleFailure(exception);
            if (mapped is null) throw;
            throw mapped;
        }

        if (lifecycle.Disposition == FiscalRepresentationLifecycleDisposition.Activated)
            return lifecycle.Representation;

        throw new FiscalContextAccessException(
            lifecycle.Code,
            lifecycle.SafeMessage);
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

    private static FiscalContextAccessException? MapLifecycleFailure(
        InvalidOperationException exception)
        => exception.Message switch
        {
            "FISCAL_REPRESENTATION_NOT_FOUND" => new FiscalContextAccessException(
                "FISCAL_REPRESENTATION_NOT_FOUND",
                "La representación no está registrada."),
            "FISCAL_REPRESENTATION_STATE_INVALID" => new FiscalContextAccessException(
                "FISCAL_REPRESENTATION_STATE_INVALID",
                "La representación no está pendiente de verificación."),
            "FISCAL_CONTEXT_NOT_FOUND" => new FiscalContextAccessException(
                "FISCAL_CONTEXT_NOT_FOUND",
                "No existe el contexto técnico."),
            "FISCAL_CONTEXT_NOT_ACTIVE" or "ACTIVE_ASSIGNMENT_REQUIRED" =>
                new FiscalContextAccessException(
                    "FISCAL_CONTEXT_NOT_ACTIVE",
                    "No existe credencial técnica activa."),
            "POINT_OF_SALE_PROBE_IDENTITY_MISMATCH" => new FiscalContextAccessException(
                "POINT_OF_SALE_PROBE_IDENTITY_MISMATCH",
                "El contexto fiscal cambió durante la verificación; repita la operación."),
            "POINT_OF_SALE_PROBE_INVALID" => new FiscalContextAccessException(
                "POINT_OF_SALE_PROBE_INVALID",
                "La evidencia de verificación remota no es válida."),
            _ => null
        };

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
