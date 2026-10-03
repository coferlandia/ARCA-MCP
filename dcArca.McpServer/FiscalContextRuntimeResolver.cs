using System.Security.Claims;
using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;

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

public sealed record FiscalCredentialSecret(
    string CredentialId,
    string CacheIdentity,
    string CertificatePath,
    string CertificatePassword);

public sealed record FiscalEnvironmentEndpoints(
    string WsaaUrl,
    string WsfeUrl,
    string PadronUrl);

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
}

public sealed class FiscalContextRuntimeResolver : IFiscalContextRuntimeResolver
{
    private readonly IRepresentedFiscalContextStore _contexts;
    private readonly IEmissionIdempotencyStore _operations;
    private readonly IConfiguration _configuration;
    private readonly dcArcaConfig _legacyConfig;
    private readonly SingleFiscalContextOptions _legacyOptions;
    private readonly IAfipLogger _logger;

    public FiscalContextRuntimeResolver(
        IRepresentedFiscalContextStore contexts,
        IEmissionIdempotencyStore operations,
        IConfiguration configuration,
        dcArcaConfig legacyConfig,
        SingleFiscalContextOptions legacyOptions,
        IAfipLogger logger)
    {
        _contexts = contexts;
        _operations = operations;
        _configuration = configuration;
        _legacyConfig = legacyConfig;
        _legacyOptions = legacyOptions;
        _logger = logger;
    }

    public async Task<FiscalContextRuntime> ResolveForReadAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        CancellationToken cancellationToken = default)
    {
        var (consumerId, context) = await ResolveAuthorizedContextAsync(
            principal, requestedContextId, "consultar", cancellationToken);
        if (context.OperationalState == FiscalContextOperationalState.Disabled)
            throw new FiscalContextAccessException("FISCAL_CONTEXT_DISABLED", "El contexto fiscal está deshabilitado.");

        var assignment = context.ActiveAssignment
            ?? throw new FiscalContextAccessException("ACTIVE_ASSIGNMENT_REQUIRED", "El contexto fiscal no tiene una asignación activa para consultas nuevas.");
        return CreateRuntime(consumerId, context, assignment);
    }

    public async Task<FiscalContextRuntime> ResolveForEmissionAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        string idempotencyKey,
        dcTipoComprobante tipoComprobante,
        CancellationToken cancellationToken = default)
    {
        var (consumerId, context) = await ResolveAuthorizedContextAsync(
            principal, requestedContextId, "facturar", cancellationToken);
        if (context.OperationalState != FiscalContextOperationalState.Active)
            throw new FiscalContextAccessException("FISCAL_CONTEXT_NOT_ACTIVE", "El contexto fiscal no admite nuevas emisiones.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new FiscalContextAccessException("IDEMPOTENCY_KEY_REQUIRED", "idempotencyKey es obligatoria para emitir.");

        var operationKeyHash = EmissionRequestFingerprint.OperationKeyHash(consumerId, context.ContextId, idempotencyKey);
        var existing = await _operations.GetAsync(operationKeyHash, cancellationToken);

        CredentialAssignmentRecord assignment;
        if (existing is not null)
        {
            if (!string.Equals(existing.Identity.ConsumerId, consumerId, StringComparison.Ordinal)
                || !string.Equals(existing.Identity.ContextId, context.ContextId, StringComparison.Ordinal)
                || !string.Equals(existing.Identity.Environment, context.Environment, StringComparison.Ordinal)
                || existing.Identity.Cuit != context.RepresentedCuit
                || existing.Identity.PuntoVenta != context.PointOfSale
                || existing.Identity.TipoComprobante != (int)tipoComprobante)
            {
                throw new FiscalContextAccessException("IDEMPOTENCY_CONTEXT_MISMATCH", "La operación persistida no pertenece al contexto fiscal solicitado.");
            }

            assignment = context.Assignments.SingleOrDefault(x =>
                    string.Equals(x.AssignmentRevision, existing.Identity.CredentialAssignmentRevision, StringComparison.Ordinal))
                ?? throw new FiscalContextAccessException("HISTORICAL_ASSIGNMENT_MISSING", "La asignación de credencial histórica de la operación ya no está disponible.");

            if (assignment.Status == CredentialAssignmentStatus.Disabled)
                throw new FiscalContextAccessException("HISTORICAL_ASSIGNMENT_INTERVENTION_REQUIRED", "La credencial original de la operación está deshabilitada; se requiere intervención explícita y no se hará fallback automático.");
        }
        else
        {
            assignment = context.ActiveAssignment
                ?? throw new FiscalContextAccessException("ACTIVE_ASSIGNMENT_REQUIRED", "El contexto fiscal no tiene una asignación activa para nuevas emisiones.");
        }

        return CreateRuntime(consumerId, context, assignment);
    }

    private async Task<(string ConsumerId, RepresentedFiscalContextRecord Context)> ResolveAuthorizedContextAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        string operation,
        CancellationToken cancellationToken)
    {
        var consumerId = principal.FindFirstValue(ArcaClaimTypes.ConsumerId);
        if (string.IsNullOrWhiteSpace(consumerId))
            throw new FiscalContextAccessException("CONSUMER_ID_REQUIRED", "La identidad autenticada no contiene un consumerId estable.");

        var contexts = await _contexts.ListAsync(cancellationToken);
        var explicitGrants = principal.FindAll(ArcaClaimTypes.ContextGrant)
            .Select(claim => ParseGrant(claim.Value))
            .Where(grant => string.Equals(grant.Operation, operation, StringComparison.Ordinal))
            .Select(grant => grant.ContextId)
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
                throw new FiscalContextAccessException("FISCAL_CONTEXT_FORBIDDEN", "El consumidor no tiene grant para el contexto fiscal solicitado.");
            var selected = contexts.SingleOrDefault(x => string.Equals(x.ContextId, contextId, StringComparison.Ordinal))
                ?? throw new FiscalContextAccessException("FISCAL_CONTEXT_NOT_FOUND", "El contexto fiscal solicitado no existe.");
            return (consumerId, selected);
        }

        var candidates = contexts.Where(x => authorized.Contains(x.ContextId)).ToArray();
        if (candidates.Length == 0)
            throw new FiscalContextAccessException("FISCAL_CONTEXT_FORBIDDEN", "El consumidor no tiene un contexto fiscal autorizado para esta operación.");
        if (candidates.Length > 1)
            throw new FiscalContextAccessException("FISCAL_CONTEXT_REQUIRED", "contextId es obligatorio porque el consumidor tiene más de un contexto autorizado.");
        return (consumerId, candidates[0]);
    }

    private FiscalContextRuntime CreateRuntime(
        string consumerId,
        RepresentedFiscalContextRecord context,
        CredentialAssignmentRecord assignment)
    {
        if (assignment.Status is CredentialAssignmentStatus.Candidate or CredentialAssignmentStatus.Validated)
            throw new FiscalContextAccessException("ASSIGNMENT_NOT_ACTIVE", "La asignación de credencial todavía no está activa para trabajo fiscal.");

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

        // dcArcaAuthService currently uses its cuit constructor argument only as the WSAA
        // token-cache namespace. Passing an opaque credential/environment identity prevents
        // two operator certificates representing the same CUIT from sharing a token.
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

        var wsfe = new dcWsfeClient(config, wsfeAuth, logger: _logger);
        var padron = new dcPadronClient(config, padronAuth, logger: _logger);
        var identityProvider = new SingleFiscalOperationIdentityProvider(
            config,
            new SingleFiscalContextOptions(
                consumerId,
                context.ContextId,
                context.Environment,
                context.ContextRevision,
                assignment.AssignmentRevision));

        return new FiscalContextRuntime(consumerId, context, assignment, config, wsfe, padron, identityProvider);
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
            throw new FiscalContextAccessException("CREDENTIAL_REFERENCE_UNRESOLVED", "La referencia de credencial del contexto no está disponible en el host.");

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

        if (string.IsNullOrWhiteSpace(wsaa) || string.IsNullOrWhiteSpace(wsfe) || string.IsNullOrWhiteSpace(padron))
            throw new FiscalContextAccessException("FISCAL_ENVIRONMENT_UNRESOLVED", "El ambiente fiscal del contexto no tiene endpoints server-owned completos.");
        return new FiscalEnvironmentEndpoints(wsaa, wsfe, padron);
    }

    private static (string ContextId, string Operation) ParseGrant(string value)
    {
        var separator = value.LastIndexOf('|');
        return separator <= 0 || separator == value.Length - 1
            ? (string.Empty, string.Empty)
            : (value[..separator], value[(separator + 1)..]);
    }
}
