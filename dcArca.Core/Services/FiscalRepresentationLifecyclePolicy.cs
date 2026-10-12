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
        string contextId,
        string consumerId,
        long representedCuit,
        int pointOfSale,
        dcPointOfSaleAccessProbeResult probe,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(probe);
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("El actor es obligatorio.", nameof(actor));
        if (pointOfSale <= 0)
            throw new ArgumentOutOfRangeException(nameof(pointOfSale));
        if (probe.PointOfSale != pointOfSale)
            throw new InvalidOperationException("POINT_OF_SALE_PROBE_IDENTITY_MISMATCH");
        if (probe.CheckedAt == default)
            throw new InvalidOperationException("POINT_OF_SALE_PROBE_INVALID");

        var candidate = await _store.GetRepresentationAsync(
            contextId,
            consumerId,
            representedCuit,
            pointOfSale,
            cancellationToken)
            ?? throw new InvalidOperationException("FISCAL_REPRESENTATION_NOT_FOUND");

        if (candidate.Status is not (
            FiscalRepresentationStatus.Pending
            or FiscalRepresentationStatus.Verified
            or FiscalRepresentationStatus.Active
            or FiscalRepresentationStatus.ActionRequired))
            throw new InvalidOperationException("FISCAL_REPRESENTATION_STATE_INVALID");

        var context = await _store.GetContextAsync(contextId, cancellationToken)
            ?? throw new InvalidOperationException("FISCAL_CONTEXT_NOT_FOUND");
        if (context.OperationalState != FiscalContextOperationalState.Active)
            throw new InvalidOperationException("FISCAL_CONTEXT_NOT_ACTIVE");
        var assignment = context.ActiveAssignment
            ?? throw new InvalidOperationException("ACTIVE_ASSIGNMENT_REQUIRED");

        if (IsVerifiedActivation(probe) || IsManual602Activation(probe))
        {
            var evidence = BuildEvidence(
                probe,
                context,
                representedCuit,
                pointOfSale,
                assignment.AssignmentRevision);

            await _store.MarkRepresentationVerifiedAsync(
                contextId,
                consumerId,
                representedCuit,
                pointOfSale,
                evidence,
                actor,
                cancellationToken);
            await _store.ActivateRepresentationAsync(
                contextId,
                consumerId,
                representedCuit,
                pointOfSale,
                actor,
                cancellationToken);

            var active = await RequireRepresentationAsync(
                contextId,
                consumerId,
                representedCuit,
                pointOfSale,
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
            await _store.MarkRepresentationActionRequiredAsync(
                contextId,
                consumerId,
                representedCuit,
                pointOfSale,
                probe.Code,
                actor,
                cancellationToken);

            var actionRequired = await RequireRepresentationAsync(
                contextId,
                consumerId,
                representedCuit,
                pointOfSale,
                cancellationToken);
            return new FiscalRepresentationLifecycleResult(
                FiscalRepresentationLifecycleDisposition.ActionRequired,
                probe.Code,
                probe.SafeMessage,
                actionRequired,
                null);
        }

        // Absence of conclusive authorization evidence is not proof of revocation.
        // Keep the durable lifecycle unchanged and let the caller decide how to
        // surface/retry the inconclusive remote diagnosis.
        return new FiscalRepresentationLifecycleResult(
            FiscalRepresentationLifecycleDisposition.Inconclusive,
            probe.Code,
            probe.SafeMessage,
            candidate,
            null);
    }

    private async Task<FiscalRepresentationRecord> RequireRepresentationAsync(
        string contextId,
        string consumerId,
        long representedCuit,
        int pointOfSale,
        CancellationToken cancellationToken)
        => await _store.GetRepresentationAsync(
            contextId,
            consumerId,
            representedCuit,
            pointOfSale,
            cancellationToken)
            ?? throw new InvalidOperationException("FISCAL_REPRESENTATION_NOT_FOUND");

    private static bool IsVerifiedActivation(dcPointOfSaleAccessProbeResult probe)
        => probe.Status == dcPointOfSaleProbeStatus.Verified
            && probe.Code == "POINT_OF_SALE_AUTHORIZATION_VERIFIED";

    private static bool IsManual602Activation(dcPointOfSaleAccessProbeResult probe)
        => probe.Status == dcPointOfSaleProbeStatus.ManualUnverified
            && probe.Code == "POINT_OF_SALE_MANUAL_UNVERIFIED";

    private static string BuildEvidence(
        dcPointOfSaleAccessProbeResult probe,
        FiscalTechnicalContextRecord context,
        long representedCuit,
        int pointOfSale,
        string assignmentRevision)
    {
        var prefix = IsVerifiedActivation(probe)
            ? "FEParamGetPtosVenta"
            : "MANUAL_PV_UNVERIFIED_602";
        return $"{prefix}|context={context.ContextId}|env={context.Environment}|cuit={representedCuit}|pv={pointOfSale}|rev={assignmentRevision}|checked={probe.CheckedAt:O}";
    }
}
