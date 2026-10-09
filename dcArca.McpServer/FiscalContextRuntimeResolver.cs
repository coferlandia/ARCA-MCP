using System.Security.Claims;
using dcArca.Core.Models;
using dcArca.Core.Services;

namespace dcArca.McpServer;

public static class ArcaClaimTypes
{
    public const string ConsumerId = "arca:consumer_id";
    public const string ContextGrant = "arca:context_grant";
    public const string LegacyKey = "arca:legacy_key";

    public static string GrantValue(string contextId, string operation)
        => $"{contextId}|{operation}";
}

public sealed class FiscalContextAccessException : Exception
{
    public string Code { get; }

    public FiscalContextAccessException(string code, string message) : base(message)
        => Code = code;
}

public sealed record AuthorizedFiscalContext(
    string ConsumerId,
    RepresentedFiscalContextRecord Context);

public sealed class FiscalContextRuntime : IDisposable
{
    public string ConsumerId { get; }
    public RepresentedFiscalContextRecord Context { get; }
    public CredentialAssignmentRecord Assignment { get; }
    public dcArcaConfig Config { get; }
    public IdcWsfeClient Wsfe { get; }
    public IdcPadronClient Padron { get; }
    public IFiscalOperationIdentityProvider IdentityProvider { get; }

    public FiscalContextRuntime(
        string consumerId,
        RepresentedFiscalContextRecord context,
        CredentialAssignmentRecord assignment,
        dcArcaConfig config,
        IdcWsfeClient wsfe,
        IdcPadronClient padron,
        IFiscalOperationIdentityProvider identityProvider)
    {
        ConsumerId = consumerId;
        Context = context;
        Assignment = assignment;
        Config = config;
        Wsfe = wsfe;
        Padron = padron;
        IdentityProvider = identityProvider;
    }

    public void Dispose()
    {
        if (Wsfe is IDisposable wsfeDisposable) wsfeDisposable.Dispose();
        if (Padron is IDisposable padronDisposable) padronDisposable.Dispose();
    }
}

public interface IFiscalContextRuntimeResolver
{
    Task<AuthorizedFiscalContext> AuthorizeAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        string operation,
        CancellationToken cancellationToken = default);

    Task<AuthorizedFiscalContext> AuthorizeAsync(
        ClaimsPrincipal principal, string? requestedContextId, string operation,
        long? representedCuit, int? pointOfSale, CancellationToken cancellationToken = default)
        => AuthorizeAsync(principal, requestedContextId, operation, cancellationToken);

    Task<FiscalContextRuntime> ResolveForReadAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        CancellationToken cancellationToken = default);

    Task<FiscalContextRuntime> ResolveForReadAsync(
        ClaimsPrincipal principal, string? requestedContextId, long? representedCuit,
        int? pointOfSale, CancellationToken cancellationToken = default)
        => ResolveForReadAsync(principal, requestedContextId, cancellationToken);

    Task<FiscalContextRuntime> ResolveForEmissionAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        string idempotencyKey,
        dcTipoComprobante tipoComprobante,
        CancellationToken cancellationToken = default);

    Task<FiscalContextRuntime> ResolveForEmissionAsync(
        ClaimsPrincipal principal, string? requestedContextId, string idempotencyKey,
        dcTipoComprobante tipoComprobante, long? representedCuit, int? pointOfSale,
        CancellationToken cancellationToken = default)
        => ResolveForEmissionAsync(principal, requestedContextId, idempotencyKey,
            tipoComprobante, cancellationToken);

    Task<FiscalContextRuntime> ResolveForHistoricalOperationAsync(
        ClaimsPrincipal principal,
        EmissionIdempotencyRecord operation,
        CancellationToken cancellationToken = default);
}

public sealed class FiscalContextRuntimeResolver : IFiscalContextRuntimeResolver
{
    private readonly IRepresentedFiscalContextStore? _contexts;
    private readonly IFiscalTechnicalContextStore? _catalog;
    private readonly IEmissionIdempotencyStore _operations;
    private readonly SingleFiscalContextOptions _legacyOptions;
    private readonly IFiscalCredentialMaterializer _materializer;
    private readonly IEmissionRecoveryGate _recoveryGate;

    public FiscalContextRuntimeResolver(
        IRepresentedFiscalContextStore contexts,
        IEmissionIdempotencyStore operations,
        SingleFiscalContextOptions legacyOptions,
        IFiscalCredentialMaterializer materializer)
        : this(contexts, operations, legacyOptions, materializer, OpenEmissionRecoveryGate.Instance)
    {
    }

    public FiscalContextRuntimeResolver(
        IRepresentedFiscalContextStore contexts,
        IEmissionIdempotencyStore operations,
        SingleFiscalContextOptions legacyOptions,
        IFiscalCredentialMaterializer materializer,
        IEmissionRecoveryGate recoveryGate)
    {
        _contexts = contexts;
        _operations = operations;
        _legacyOptions = legacyOptions;
        _materializer = materializer;
        _recoveryGate = recoveryGate;
    }

    public FiscalContextRuntimeResolver(
        IFiscalTechnicalContextStore catalog,
        IEmissionIdempotencyStore operations,
        SingleFiscalContextOptions legacyOptions,
        IFiscalCredentialMaterializer materializer,
        IEmissionRecoveryGate recoveryGate)
    {
        _catalog = catalog;
        _operations = operations;
        _legacyOptions = legacyOptions;
        _materializer = materializer;
        _recoveryGate = recoveryGate;
    }

    public Task<AuthorizedFiscalContext> AuthorizeAsync(
        ClaimsPrincipal principal, string? requestedContextId, string operation,
        CancellationToken cancellationToken = default)
        => AuthorizeAsync(principal, requestedContextId, operation, null, null, cancellationToken);

    public async Task<AuthorizedFiscalContext> AuthorizeAsync(
        ClaimsPrincipal principal, string? requestedContextId, string operation,
        long? representedCuit, int? pointOfSale, CancellationToken cancellationToken = default)
    {
        if (operation is not ("consultar" or "facturar"))
            throw new ArgumentException("operation debe ser consultar o facturar.", nameof(operation));
        var (consumerId, context) = _catalog is null
            ? await ResolveAuthorizedContextAsync(principal, requestedContextId, operation, cancellationToken)
            : await ResolveTechnicalRepresentationAsync(principal, requestedContextId, operation,
                representedCuit, pointOfSale, cancellationToken);
        return new AuthorizedFiscalContext(consumerId, context);
    }

    public Task<FiscalContextRuntime> ResolveForReadAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        CancellationToken cancellationToken = default)
        => ResolveForReadAsync(principal, requestedContextId, null, null, cancellationToken);

    public async Task<FiscalContextRuntime> ResolveForReadAsync(
        ClaimsPrincipal principal, string? requestedContextId, long? representedCuit,
        int? pointOfSale, CancellationToken cancellationToken = default)
    {
        var authorized = await AuthorizeAsync(
            principal, requestedContextId, "consultar",
            representedCuit, pointOfSale, cancellationToken);
        var context = authorized.Context;
        if (context.OperationalState == FiscalContextOperationalState.Disabled)
            throw new FiscalContextAccessException("FISCAL_CONTEXT_DISABLED", "El contexto fiscal está deshabilitado.");

        var assignment = context.ActiveAssignment
            ?? throw new FiscalContextAccessException(
                "ACTIVE_ASSIGNMENT_REQUIRED",
                "El contexto fiscal no tiene una asignación activa para consultas nuevas.");
        return CreateRuntime(authorized.ConsumerId, context, assignment);
    }

    public Task<FiscalContextRuntime> ResolveForEmissionAsync(
        ClaimsPrincipal principal, string? requestedContextId, string idempotencyKey,
        dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
        => ResolveForEmissionAsync(principal, requestedContextId, idempotencyKey,
            tipoComprobante, null, null, cancellationToken);

    public async Task<FiscalContextRuntime> ResolveForEmissionAsync(
        ClaimsPrincipal principal, string? requestedContextId, string idempotencyKey,
        dcTipoComprobante tipoComprobante, long? representedCuit, int? pointOfSale,
        CancellationToken cancellationToken = default)
    {
        var authorized = await AuthorizeAsync(
            principal, requestedContextId, "facturar",
            representedCuit, pointOfSale, cancellationToken);
        var consumerId = authorized.ConsumerId;
        var context = authorized.Context;
        if (context.OperationalState != FiscalContextOperationalState.Active)
            throw new FiscalContextAccessException("FISCAL_CONTEXT_NOT_ACTIVE", "El contexto fiscal no admite nuevas emisiones.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new FiscalContextAccessException("IDEMPOTENCY_KEY_REQUIRED", "idempotencyKey es obligatoria para emitir.");

        var recoveryBlock = await _recoveryGate.GetBlockAsync(cancellationToken);
        if (recoveryBlock is not null)
        {
            throw new FiscalContextAccessException(
                "RESTORE_RECONCILIATION_REQUIRED",
                $"Las emisiones están bloqueadas por recuperación del restore {recoveryBlock.RestoreId}; reconciliar la ventana no cubierta y completar el recovery antes de autorizar nuevos comprobantes.");
        }

        var operationKeyHash = _catalog is null
            ? EmissionRequestFingerprint.OperationKeyHash(consumerId, context.ContextId, idempotencyKey)
            : EmissionRequestFingerprint.OperationKeyHash(consumerId, context.ContextId,
                context.RepresentedCuit, context.PointOfSale, idempotencyKey);
        var existing = await _operations.GetAsync(operationKeyHash, cancellationToken);

        CredentialAssignmentRecord assignment;
        if (existing is not null)
        {
            EnsureOperationIdentity(existing, consumerId, context, (int)tipoComprobante);
            assignment = ResolveHistoricalAssignment(context, existing);
        }
        else
        {
            assignment = context.ActiveAssignment
                ?? throw new FiscalContextAccessException(
                    "ACTIVE_ASSIGNMENT_REQUIRED",
                    "El contexto fiscal no tiene una asignación activa para nuevas emisiones.");
        }

        return CreateRuntime(consumerId, context, assignment);
    }

    public async Task<FiscalContextRuntime> ResolveForHistoricalOperationAsync(
        ClaimsPrincipal principal,
        EmissionIdempotencyRecord operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var authorized = _catalog is null
            ? await AuthorizeAsync(principal, operation.Identity.ContextId, "consultar", cancellationToken)
            : await AuthorizeHistoricalAsync(principal, operation, cancellationToken);
        var context = authorized.Context;
        if (context.OperationalState == FiscalContextOperationalState.Disabled)
            throw new FiscalContextAccessException(
                "FISCAL_CONTEXT_DISABLED",
                "El contexto fiscal está deshabilitado incluso para recuperación histórica.");

        EnsureOperationIdentity(
            operation,
            authorized.ConsumerId,
            context,
            operation.Identity.TipoComprobante);
        var assignment = ResolveHistoricalAssignment(context, operation);
        return CreateRuntime(authorized.ConsumerId, context, assignment);
    }

    private async Task<AuthorizedFiscalContext> AuthorizeHistoricalAsync(
        ClaimsPrincipal principal, EmissionIdempotencyRecord operation, CancellationToken cancellationToken)
    {
        var (consumerId, context) = await ResolveTechnicalRepresentationAsync(
            principal, operation.Identity.ContextId, "consultar", operation.Identity.Cuit,
            operation.Identity.PuntoVenta, cancellationToken,
            operation.Identity.CredentialAssignmentRevision);
        return new AuthorizedFiscalContext(consumerId, context);
    }

    private static void EnsureOperationIdentity(
        EmissionIdempotencyRecord existing,
        string consumerId,
        RepresentedFiscalContextRecord context,
        int tipoComprobante)
    {
        if (!string.Equals(existing.Identity.ConsumerId, consumerId, StringComparison.Ordinal)
            || !string.Equals(existing.Identity.ContextId, context.ContextId, StringComparison.Ordinal)
            || !string.Equals(existing.Identity.Environment, context.Environment, StringComparison.Ordinal)
            || existing.Identity.Cuit != context.RepresentedCuit
            || existing.Identity.PuntoVenta != context.PointOfSale
            || existing.Identity.TipoComprobante != tipoComprobante)
        {
            throw new FiscalContextAccessException(
                "IDEMPOTENCY_CONTEXT_MISMATCH",
                "La operación persistida no pertenece al contexto fiscal autorizado.");
        }
    }

    private static CredentialAssignmentRecord ResolveHistoricalAssignment(
        RepresentedFiscalContextRecord context,
        EmissionIdempotencyRecord operation)
    {
        var assignment = context.Assignments.SingleOrDefault(x =>
                string.Equals(
                    x.AssignmentRevision,
                    operation.Identity.CredentialAssignmentRevision,
                    StringComparison.Ordinal))
            ?? throw new FiscalContextAccessException(
                "HISTORICAL_ASSIGNMENT_MISSING",
                "La asignación de credencial histórica de la operación ya no está disponible.");

        if (assignment.Status == CredentialAssignmentStatus.Disabled)
        {
            throw new FiscalContextAccessException(
                "HISTORICAL_ASSIGNMENT_INTERVENTION_REQUIRED",
                "La credencial original de la operación está deshabilitada; se requiere intervención explícita y no se hará fallback automático.");
        }
        return assignment;
    }

    private async Task<(string ConsumerId, RepresentedFiscalContextRecord Context)> ResolveTechnicalRepresentationAsync(
        ClaimsPrincipal principal, string? requestedContextId, string operation,
        long? requestedCuit, int? requestedPointOfSale, CancellationToken cancellationToken,
        string? evidenceAssignmentRevision = null)
    {
        var consumerId = principal.FindFirstValue(ArcaClaimTypes.ConsumerId);
        if (string.IsNullOrWhiteSpace(consumerId))
            throw new FiscalContextAccessException("CONSUMER_ID_REQUIRED", "No existe consumerId autenticado.");
        if (principal.HasClaim(ArcaClaimTypes.LegacyKey, "true"))
            throw new FiscalContextAccessException("LEGACY_KEY_UNSUPPORTED_V2", "Es necesario reprovisionar la API key del consumidor.");
        if (requestedCuit.HasValue != requestedPointOfSale.HasValue ||
            requestedCuit is <= 0 || requestedPointOfSale is <= 0)
            throw new FiscalContextAccessException("FISCAL_REPRESENTATION_INVALID", "CUIT y PV deben indicarse juntos y ser positivos.");

        var catalog = _catalog ?? throw new InvalidOperationException("TECHNICAL_CONTEXT_STORE_UNCONFIGURED");
        var allowed = principal.FindAll(ArcaClaimTypes.ContextGrant)
            .Select(x => ParseGrant(x.Value))
            .Where(x => x.Operation == operation)
            .Select(x => x.ContextId)
            .ToHashSet(StringComparer.Ordinal);
        var contexts = await catalog.ListContextsAsync(cancellationToken);
        var authorizedContexts = contexts.Where(x => allowed.Contains(x.ContextId)).ToArray();
        if (authorizedContexts.Length == 0)
            throw new FiscalContextAccessException("FISCAL_CONTEXT_FORBIDDEN", "El consumidor no tiene grant del contexto técnico.");
        FiscalTechnicalContextRecord technical;
        if (string.IsNullOrWhiteSpace(requestedContextId))
        {
            if (authorizedContexts.Length != 1)
                throw new FiscalContextAccessException("FISCAL_CONTEXT_REQUIRED", "Debe indicar el contexto técnico.");
            technical = authorizedContexts[0];
        }
        else
        {
            technical = authorizedContexts.SingleOrDefault(x => x.ContextId == requestedContextId)
                ?? throw new FiscalContextAccessException("FISCAL_CONTEXT_FORBIDDEN", "El consumidor no tiene grant del contexto técnico.");
        }
        var reps = (await catalog.ListRepresentationsAsync(technical.ContextId, cancellationToken))
            .Where(x => x.ConsumerId == consumerId && x.Status == FiscalRepresentationStatus.Active)
            .Where(x => (!requestedCuit.HasValue || x.RepresentedCuit == requestedCuit.Value) &&
                (!requestedPointOfSale.HasValue || x.PointOfSale == requestedPointOfSale.Value))
            .ToArray();
        if (reps.Length == 0)
            throw new FiscalContextAccessException("FISCAL_REPRESENTATION_FORBIDDEN", "El CUIT y PV no están activos para este consumidor/contexto.");
        if (reps.Length != 1)
            throw new FiscalContextAccessException("FISCAL_REPRESENTATION_REQUIRED", "Debe especificar CUIT representado y punto de venta.");
        var rep = reps[0];
        // A representation proved against one certificate revision cannot authorize a
        // replacement certificate by implication. A new authenticated WSFE probe is required.
        var activeRevision = evidenceAssignmentRevision ?? technical.ActiveAssignment?.AssignmentRevision;
        if (activeRevision is null || rep.VerificationEvidence is null ||
            !rep.VerificationEvidence.Contains(
                $"|rev={activeRevision}|", StringComparison.Ordinal) ||
            !rep.VerificationEvidence.Contains(
                $"|env={technical.Environment}|", StringComparison.Ordinal))
            throw new FiscalContextAccessException("FISCAL_REPRESENTATION_REVERIFY_REQUIRED",
                "La representación debe revalidarse con la credencial y ambiente técnicos activos.");

        // Compatibility projection for the existing WSFE services. The persisted ContextId is technical:
        // CUIT/PV are resolved on every operation and NEVER stored as technical context identity.
        var projected = new RepresentedFiscalContextRecord(technical.ContextId,
            technical.Environment, rep.RepresentedCuit, rep.PointOfSale,
            technical.OperationalState, technical.ContextRevision, false, technical.Assignments);
        return (consumerId, projected);
    }

    private async Task<(string ConsumerId, RepresentedFiscalContextRecord Context)> ResolveAuthorizedContextAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        string operation,
        CancellationToken cancellationToken)
    {
        var consumerId = principal.FindFirstValue(ArcaClaimTypes.ConsumerId);
        if (string.IsNullOrWhiteSpace(consumerId))
            throw new FiscalContextAccessException(
                "CONSUMER_ID_REQUIRED",
                "La identidad autenticada no contiene un consumerId estable.");

        var contexts = await (_contexts ?? throw new InvalidOperationException("LEGACY_CONTEXT_STORE_NOT_CONFIGURED")).ListAsync(cancellationToken);
        var explicitGrants = principal.FindAll(ArcaClaimTypes.ContextGrant)
            .Select(claim => ParseGrant(claim.Value))
            .Where(grant => string.Equals(grant.Operation, operation, StringComparison.Ordinal))
            .Select(grant => grant.ContextId)
            .Where(contextId => !string.IsNullOrWhiteSpace(contextId))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        HashSet<string> authorized;
        if (explicitGrants.Count > 0)
        {
            authorized = explicitGrants;
        }
        else if (principal.HasClaim(ArcaClaimTypes.LegacyKey, "true"))
        {
            var legacyCandidates = contexts
                .Where(x => x.LegacyDefault
                    && string.Equals(x.ContextId, _legacyOptions.ContextId, StringComparison.Ordinal))
                .Select(x => x.ContextId)
                .ToArray();
            authorized = legacyCandidates.Length == 1
                ? legacyCandidates.ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
        }
        else
        {
            authorized = new HashSet<string>(StringComparer.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(requestedContextId))
        {
            var contextId = requestedContextId.Trim();
            if (!authorized.Contains(contextId))
                throw new FiscalContextAccessException(
                    "FISCAL_CONTEXT_FORBIDDEN",
                    "El consumidor no tiene grant para el contexto fiscal solicitado.");
            var selected = contexts.SingleOrDefault(x =>
                    string.Equals(x.ContextId, contextId, StringComparison.Ordinal))
                ?? throw new FiscalContextAccessException(
                    "FISCAL_CONTEXT_NOT_FOUND",
                    "El contexto fiscal solicitado no existe.");
            return (consumerId, selected);
        }

        var candidates = contexts.Where(x => authorized.Contains(x.ContextId)).ToArray();
        if (candidates.Length == 0)
            throw new FiscalContextAccessException(
                "FISCAL_CONTEXT_FORBIDDEN",
                "El consumidor no tiene un contexto fiscal autorizado para esta operación.");
        if (candidates.Length > 1)
            throw new FiscalContextAccessException(
                "FISCAL_CONTEXT_REQUIRED",
                "contextId es obligatorio porque el consumidor tiene más de un contexto autorizado.");
        return (consumerId, candidates[0]);
    }

    private FiscalContextRuntime CreateRuntime(
        string consumerId,
        RepresentedFiscalContextRecord context,
        CredentialAssignmentRecord assignment)
    {
        if (assignment.Status is CredentialAssignmentStatus.Candidate or CredentialAssignmentStatus.Validated)
            throw new FiscalContextAccessException(
                "ASSIGNMENT_NOT_ACTIVE",
                "La asignación de credencial todavía no está activa para trabajo fiscal.");

        var materialized = _materializer.Materialize(context, assignment);
        IdcWsfeClient wsfe = new dcWsfeClient(
            materialized.Config,
            materialized.WsfeAuth,
            logger: null);
        IdcPadronClient padron = new dcPadronClient(
            materialized.Config,
            materialized.PadronAuth,
            logger: null);
        var identityProvider = new SingleFiscalOperationIdentityProvider(
            materialized.Config,
            new SingleFiscalContextOptions(
                consumerId,
                context.ContextId,
                context.Environment,
                context.ContextRevision,
                assignment.AssignmentRevision));

        return new FiscalContextRuntime(
            consumerId,
            context,
            assignment,
            materialized.Config,
            wsfe,
            padron,
            identityProvider);
    }

    private static (string ContextId, string Operation) ParseGrant(string value)
    {
        var separator = value.LastIndexOf('|');
        return separator <= 0 || separator == value.Length - 1
            ? (string.Empty, string.Empty)
            : (value[..separator], value[(separator + 1)..]);
    }
}
