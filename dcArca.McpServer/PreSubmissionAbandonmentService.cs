namespace dcArca.McpServer;

public sealed record PreSubmissionAbandonmentResult(
    EmissionIdempotencyRecord Operation,
    bool ReservationReleased);

public sealed class PreSubmissionAbandonmentService
{
    private readonly IEmissionIdempotencyStore _store;
    private readonly IFiscalSeriesCoordinator _seriesCoordinator;

    public PreSubmissionAbandonmentService(
        IEmissionIdempotencyStore store,
        IFiscalSeriesCoordinator seriesCoordinator)
    {
        _store = store;
        _seriesCoordinator = seriesCoordinator;
    }

    public async Task<PreSubmissionAbandonmentResult> AbandonAsync(
        string operationId,
        string actorReference,
        string reasonCode,
        CancellationToken cancellationToken = default)
    {
        var keyHash = operationId?.Trim().ToLowerInvariant() ?? string.Empty;
        var existing = await _store.GetAsync(keyHash, cancellationToken)
            ?? throw new PreSubmissionAbandonmentException("OPERATION_NOT_FOUND");

        if (existing.State is not (
                EmissionIdempotencyState.Created
                or EmissionIdempotencyState.NumberAssigned
                or EmissionIdempotencyState.AbandonedBeforeSubmission))
            throw new PreSubmissionAbandonmentException("OPERATION_ABANDON_NOT_ALLOWED");

        var active = await _seriesCoordinator.GetActiveAsync(existing.Identity, cancellationToken);
        if (existing.State == EmissionIdempotencyState.NumberAssigned
            && active is not null
            && !string.Equals(active.OwnerKeyHash, existing.KeyHash, StringComparison.Ordinal))
            throw new PreSubmissionAbandonmentException("OPERATION_RESERVATION_MISMATCH");

        var exactReservation = active is not null
            && string.Equals(active.OwnerKeyHash, existing.KeyHash, StringComparison.Ordinal)
                ? active
                : null;

        var audit = existing.AbandonmentAudit
            ?? new PreSubmissionAbandonmentAudit(
                actorReference,
                reasonCode,
                DateTimeOffset.UtcNow);

        var abandoned = await _store.AbandonBeforeSubmissionAsync(
            existing.KeyHash,
            exactReservation?.NumeroComprobante,
            audit,
            cancellationToken);

        var released = exactReservation is not null;
        try
        {
            await _seriesCoordinator.ReleaseAsync(
                abandoned.Identity,
                abandoned.KeyHash,
                cancellationToken);
        }
        catch (FiscalSeriesBlockedException exception)
            when (string.Equals(
                exception.Message,
                "SERIES_RESERVATION_OWNER_MISMATCH",
                StringComparison.Ordinal))
        {
            released = false;
        }

        return new PreSubmissionAbandonmentResult(abandoned, released);
    }
}
