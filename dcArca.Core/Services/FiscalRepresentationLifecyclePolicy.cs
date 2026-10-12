namespace dcArca.Core.Services;

public enum FiscalRepresentationLifecycleDisposition
{
    Activated,
    ActionRequired,
    Inconclusive
}

public sealed record FiscalRepresentationLifecycleResult(
    FiscalRepresentationLifecycleDisposition Disposition,
    string Code,
    string SafeMessage,
    FiscalRepresentationRecord Representation,
    string? VerificationEvidence);

public sealed record FiscalRepresentationProbeIdentity(
    string ContextId,
    string Environment,
    long RepresentedCuit,
    int PointOfSale,
    string AssignmentRevision);

/// <summary>
/// Canonical Core policy that converts a non-emitting point-of-sale probe into
/// durable representation lifecycle state. It is intentionally independent of
/// ASP.NET, CLI parsing and remote transport concerns.
/// </summary>
public sealed class FiscalRepresentationLifecyclePolicy
{
    private static readonly HashSet<string> ConclusiveDenials =
        new(StringComparer.Ordinal)
        {
            "WSFE_600",
            "WSFE_601",
            "POINT_OF_SALE_NOT_AUTHORIZED",
            "POINT_OF_SALE_BLOCKED",
            "POINT_OF_SALE_DISABLED",
            "POINT_OF_SALE_NOT_CAE"
        };

    private readonly IFiscalTechnicalContextStore _store;

    public FiscalRepresentationLifecyclePolicy(IFiscalTechnicalContextStore store)
        => _store = store;

    public async Task<FiscalRepresentationLifecycleResult> ApplyAsync(
        string consumerId,
        FiscalRepresentationProbeOutcome outcome,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (string.IsNullOrWhiteSpace(consumerId))
            throw new ArgumentException("El consumerId es obligatorio.", nameof(consumerId));
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("El actor es obligatorio.", nameof(actor));

        var identity = outcome.Identity;
        var probe = outcome.Probe;
        if (identity.RepresentedCuit <= 0
            || identity.PointOfSale <= 0
            || string.IsNullOrWhiteSpace(identity.ContextId)
            || string.IsNullOrWhiteSpace(identity.Environment)
            || string.IsNullOrWhiteSpace(identity.AssignmentRevision)
            || probe.PointOfSale != identity.PointOfSale)
            throw new InvalidOperationException("POINT_OF_SALE_PROBE_IDENTITY_MISMATCH");
        if (probe.CheckedAt == default)
            throw new InvalidOperationException("POINT_OF_SALE_PROBE_INVALID");

        var candidate = await _store.GetRepresentationAsync(
            identity.ContextId,
            consumerId,
            identity.RepresentedCuit,
            identity.PointOfSale,
            cancellationToken)
            ?? throw new InvalidOperationException("FISCAL_REPRESENTATION_NOT_FOUND");

        if (!IsRevalidableStatus(candidate.Status))
            throw new InvalidOperationException("FISCAL_REPRESENTATION_STATE_INVALID");

        if (IsVerifiedActivation(probe) || IsManual602Activation(probe))
        {
            var evidence = BuildEvidence(probe, identity);
            var active = await _store.ActivateRepresentationFromProbeAsync(
                consumerId,
                identity,
                evidence,
                actor,
                cancellationToken);
            return new FiscalRepresentationLifecycleResult(
                FiscalRepresentationLifecycleDisposition.Activated,
                probe.Code,
                probe.SafeMessage,
                active,
                evidence);
        }

        if (ConclusiveDenials.Contains(probe.Code))
        {
            var actionRequired =
                await _store.MarkRepresentationActionRequiredFromProbeAsync(
                    consumerId,
                    identity,
                    probe.Code,
                    actor,
                    cancellationToken);
            return new FiscalRepresentationLifecycleResult(
                FiscalRepresentationLifecycleDisposition.ActionRequired,
                probe.Code,
                probe.SafeMessage,
                actionRequired,
                null);
        }

        // Inconclusive outcomes do not mutate durable lifecycle, but they must
        // still belong to the currently active technical assignment.
        var context = await _store.GetContextAsync(
            identity.ContextId,
            cancellationToken)
            ?? throw new InvalidOperationException("FISCAL_CONTEXT_NOT_FOUND");
        if (context.OperationalState != FiscalContextOperationalState.Active)
            throw new InvalidOperationException("FISCAL_CONTEXT_NOT_ACTIVE");
        var assignment = context.ActiveAssignment
            ?? throw new InvalidOperationException("ACTIVE_ASSIGNMENT_REQUIRED");
        if (!string.Equals(
                context.Environment,
                identity.Environment,
                StringComparison.Ordinal)
            || !string.Equals(
                assignment.AssignmentRevision,
                identity.AssignmentRevision,
                StringComparison.Ordinal))
            throw new InvalidOperationException("POINT_OF_SALE_PROBE_IDENTITY_MISMATCH");

        return new FiscalRepresentationLifecycleResult(
            FiscalRepresentationLifecycleDisposition.Inconclusive,
            probe.Code,
            probe.SafeMessage,
            candidate,
            null);
    }

    public static bool IsRevalidableStatus(FiscalRepresentationStatus status)
        => status is FiscalRepresentationStatus.Pending
            or FiscalRepresentationStatus.Verified
            or FiscalRepresentationStatus.Active
            or FiscalRepresentationStatus.ActionRequired;

    private static bool IsVerifiedActivation(dcPointOfSaleAccessProbeResult probe)
        => probe.Status == dcPointOfSaleProbeStatus.Verified
            && probe.Code == "POINT_OF_SALE_AUTHORIZATION_VERIFIED";

    private static bool IsManual602Activation(dcPointOfSaleAccessProbeResult probe)
        => probe.Status == dcPointOfSaleProbeStatus.ManualUnverified
            && probe.Code == "POINT_OF_SALE_MANUAL_UNVERIFIED";

    private static string BuildEvidence(
        dcPointOfSaleAccessProbeResult probe,
        FiscalRepresentationProbeIdentity identity)
    {
        var prefix = IsVerifiedActivation(probe)
            ? "FEParamGetPtosVenta"
            : "MANUAL_PV_UNVERIFIED_602";
        return $"{prefix}|context={identity.ContextId}|env={identity.Environment}|cuit={identity.RepresentedCuit}|pv={identity.PointOfSale}|rev={identity.AssignmentRevision}|checked={probe.CheckedAt:O}";
    }
}
