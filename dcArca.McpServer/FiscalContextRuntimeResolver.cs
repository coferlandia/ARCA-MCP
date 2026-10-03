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
    public dcWsfeClient Wsfe { get; }
    public dcPadronClient Padron { get; }
    public IFiscalOperationIdentityProvider IdentityProvider { get; }

    public FiscalContextRuntime(
        string consumerId,
        RepresentedFiscalContextRecord context,
        CredentialAssignmentRecord assignment,
        dcArcaConfig config,
        dcWsfeClient wsfe,
        dcPadronClient padron,
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
        Wsfe.Dispose();
        Padron.Dispose();
    }
}

public interface IFiscalContextRuntimeResolver
{
    Task<AuthorizedFiscalContext> AuthorizeAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        string operation,
        CancellationToken cancellationToken = default);

    Task<FiscalContextRuntime> ResolveForReadAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        CancellationToken cancellationToken = default);

    Task<FiscalContextRuntime> ResolveForEmissionAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        string idempotencyKey,
        dcTipoComprobante tipoComprobante,
        CancellationToken cancellationToken = default);

    Task<FiscalContextRuntime> ResolveForHistoricalOperationAsync(
        ClaimsPrincipal principal,
        EmissionIdempotencyRecord operation,
        CancellationToken cancellationToken = default);
}

public sealed class FiscalContextRuntimeResolver : IFiscalContextRuntimeResolver
{
    private readonly IRepresentedFiscalContextStore _contexts;
    private readonly IEmissionIdempotencyStore _operations;
    private readonly SingleFiscalContextOptions _legacyOptions;
    private readonly IFiscalCredentialMaterializer _materializer;

    public FiscalContextRuntimeResolver(
        IRepresentedFiscalContextStore contexts,
        IEmissionIdempotencyStore operations,
        SingleFiscalContextOptions legacyOptions,
        IFiscalCredentialMaterializer materializer)
    {
        _contexts = contexts;
        _operations = operations;
        _legacyOptions = legacyOptions;
        _materializer = materializer;
    }

    public async Task<AuthorizedFiscalContext> AuthorizeAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        string operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is not ("consultar" or "facturar"))
            throw new ArgumentException("operation debe ser consultar o facturar.", nameof(operation));
        var (consumerId, context) = await ResolveAuthorizedContextAsync(
            principal,
            requestedContextId,
            operation,
            cancellationToken);
        return new AuthorizedFiscalContext(consumerId, context);
    }

    public async Task<FiscalContextRuntime> ResolveForReadAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        CancellationToken cancellationToken = default)
    {
        var authorized = await AuthorizeAsync(
            principal,
            requestedContextId,
            "consultar",
            cancellationToken);
        var context = authorized.Context;
        if (context.OperationalState == FiscalContextOperationalState.Disabled)
            throw new FiscalContextAccessException("FISCAL_CONTEXT_DISABLED", "El contexto fiscal está deshabilitado.");

        var assignment = context.ActiveAssignment
            ?? throw new FiscalContextAccessException(
                "ACTIVE_ASSIGNMENT_REQUIRED",
                "El contexto fiscal no tiene una asignación activa para consultas nuevas.");
        return CreateRuntime(authorized.ConsumerId, context, assignment);
    }

    public async Task<FiscalContextRuntime> ResolveForEmissionAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        string idempotencyKey,
        dcTipoComprobante tipoComprobante,
        CancellationToken cancellationToken = default)
    {
        var authorized = await AuthorizeAsync(
            principal,
            requestedContextId,
            "facturar",
            cancellationToken);
        var consumerId = authorized.ConsumerId;
        var context = authorized.Context;
        if (context.OperationalState != FiscalContextOperationalState.Active)
            throw new FiscalContextAccessException("FISCAL_CONTEXT_NOT_ACTIVE", "El contexto fiscal no admite nuevas emisiones.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new FiscalContextAccessException("IDEMPOTENCY_KEY_REQUIRED", "idempotencyKey es obligatoria para emitir.");

        var operationKeyHash = EmissionRequestFingerprint.OperationKeyHash(
            consumerId,
            context.ContextId,
            idempotencyKey);
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
        var authorized = await AuthorizeAsync(
            principal,
            operation.Identity.ContextId,
            "consultar",
            cancellationToken);
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

        var contexts = await _contexts.ListAsync(cancellationToken);
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
        var wsfe = new dcWsfeClient(
            materialized.Config,
            materialized.WsfeAuth,
            logger: null);
        var padron = new dcPadronClient(
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
