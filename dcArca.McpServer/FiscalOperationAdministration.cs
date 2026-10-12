using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace dcArca.McpServer;

public sealed record AdministrativePreSubmissionAbandonmentResult(
    string OperationId,
    string PreviousState,
    string FinalState,
    bool ReservationReleased,
    long? NumeroComprobante,
    DateTimeOffset? AbandonedAt);

/// <summary>
/// Administrative adapter for explicitly closing work that is still provably pre-submission.
/// The fiscal state machine remains owned by <see cref="PreSubmissionAbandonmentService"/>.
/// </summary>
public sealed class FiscalOperationAdministration
{
    private readonly IEmissionIdempotencyStore _store;
    private readonly PreSubmissionAbandonmentService _abandonment;

    public FiscalOperationAdministration(
        IEmissionIdempotencyStore store,
        PreSubmissionAbandonmentService abandonment)
    {
        _store = store;
        _abandonment = abandonment;
    }

    public async Task<AdministrativePreSubmissionAbandonmentResult> AbandonBeforeSubmissionAsync(
        ClaimsPrincipal principal,
        string operationId,
        string reasonCode,
        CancellationToken cancellationToken = default)
    {
        var administrativeConsumer = DemandAdministrativePrincipal(principal);
        var keyHash = NormalizeOperationId(operationId);
        var normalizedReason = NormalizeReasonCode(reasonCode);

        var existing = await _store.GetAsync(keyHash, cancellationToken);
        if (existing is null)
            throw OperationNotFound();

        // Operation IDs are opaque, but namespace isolation still fails closed:
        // never disclose that another consumer owns a matching durable operation.
        if (!string.Equals(
                existing.Identity.ConsumerId,
                administrativeConsumer,
                StringComparison.Ordinal))
            throw OperationNotFound();

        if (!principal.FindAll(ArcaClaimTypes.ContextGrant).Any(claim =>
                string.Equals(
                    claim.Value,
                    ArcaClaimTypes.GrantValue(existing.Identity.ContextId, "administrar"),
                    StringComparison.Ordinal)))
            throw AdminForbidden();

        var previousState = existing.State;
        var result = await _abandonment.AbandonAsync(
            keyHash,
            ActorReference(principal),
            normalizedReason,
            cancellationToken);

        return new AdministrativePreSubmissionAbandonmentResult(
            keyHash,
            previousState.ToString(),
            result.Operation.State.ToString(),
            result.ReservationReleased,
            result.Operation.NumeroComprobante,
            result.Operation.AbandonmentAudit?.AbandonedAt);
    }

    private static string DemandAdministrativePrincipal(ClaimsPrincipal principal)
    {
        if (!ArcaScopeAuthorization.HasScope(principal, "arca:administrar")
            || principal.HasClaim(ArcaClaimTypes.LegacyKey, "true"))
            throw AdminForbidden();

        var consumerId = principal.FindFirstValue(ArcaClaimTypes.ConsumerId)?.Trim();
        if (string.IsNullOrWhiteSpace(consumerId))
            throw AdminForbidden();

        return consumerId;
    }

    private static string NormalizeOperationId(string operationId)
    {
        var normalized = operationId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length != 64 || !normalized.All(Uri.IsHexDigit))
            throw OperationNotFound();
        return normalized;
    }

    private static string NormalizeReasonCode(string reasonCode)
    {
        var normalized = reasonCode?.Trim() ?? string.Empty;
        if (!IsAuditToken(normalized, 96))
            throw new FiscalContextAccessException(
                "OPERATION_ABANDON_REASON_INVALID",
                "La razón administrativa debe ser un código seguro de hasta 96 caracteres.");
        return normalized;
    }

    private static string ActorReference(ClaimsPrincipal principal)
    {
        var keyId = principal.FindFirstValue(ClaimTypes.NameIdentifier)?.Trim();
        if (string.IsNullOrWhiteSpace(keyId))
            throw AdminForbidden();

        var safeKeyId = IsAuditToken(keyId, 120)
            ? keyId
            : Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(keyId)))
                .ToLowerInvariant();

        // Prefix keeps the audit field meaningful while remaining a machine-safe token.
        return "api-key:" + safeKeyId;
    }

    private static bool IsAuditToken(string value, int maxLength)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= maxLength
            && value.All(ch =>
                char.IsLetterOrDigit(ch)
                || ch is '-' or '_' or '.' or ':' or '/');

    private static FiscalContextAccessException AdminForbidden()
        => new(
            "FISCAL_ADMIN_FORBIDDEN",
            "La administración requiere scope, grant y namespace administrativo explícitos.");

    private static FiscalContextAccessException OperationNotFound()
        => new(
            "OPERATION_NOT_FOUND",
            "No existe una operación administrable con ese identificador.");
}
