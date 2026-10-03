using System.Collections.Concurrent;
using dcArca.Core.Models;
using dcArca.Core.Services;

namespace dcArca.McpServer;

public sealed class McpInvoiceSequencer : IInvoiceIssuer
{
    private readonly IdcWsfeClient _wsfe;
    private readonly dcArcaConfig _config;
    private readonly IEmissionIdempotencyStore _store;
    private readonly IFiscalOperationIdentityProvider _identityProvider;
    private readonly IFiscalSeriesCoordinator _seriesCoordinator;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public McpInvoiceSequencer(
        IdcWsfeClient wsfe,
        dcArcaConfig config,
        IEmissionIdempotencyStore store,
        IFiscalOperationIdentityProvider identityProvider,
        IFiscalSeriesCoordinator seriesCoordinator)
    {
        _wsfe = wsfe;
        _config = config;
        _store = store;
        _identityProvider = identityProvider;
        _seriesCoordinator = seriesCoordinator;
    }

    // Compatibility constructors remain in-process only. The hosted server always injects
    // the durable FileSystemFiscalSeriesCoordinator through the five-argument constructor.
    public McpInvoiceSequencer(
        IdcWsfeClient wsfe,
        dcArcaConfig config,
        IEmissionIdempotencyStore store,
        IFiscalOperationIdentityProvider identityProvider)
        : this(wsfe, config, store, identityProvider, new InMemoryFiscalSeriesCoordinator())
    {
    }

    public McpInvoiceSequencer(
        IdcWsfeClient wsfe,
        dcArcaConfig config,
        IEmissionIdempotencyStore store)
        : this(wsfe, config, store, SingleFiscalOperationIdentityProvider.ForTests(config))
    {
    }

    public async Task<dcFacturaResponse> EmitAsync(
        dcFacturaRequest factura,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var preflight = dcFacturaPreflightValidator.Validate(factura);
        if (!preflight.IsValid)
        {
            return Error(
                preflight.Code ?? "INVALID_REQUEST",
                preflight.Message ?? "La solicitud fiscal no supera la validación local.",
                dcEmissionOutcome.InvalidRequest);
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Error(
                "IDEMPOTENCY_KEY_REQUIRED",
                "idempotencyKey es obligatoria para emitir.",
                dcEmissionOutcome.InvalidRequest);
        }

        var tipo = factura.TipoComprobante!.Value;
        var identity = _identityProvider.For(tipo);
        var requestHash = EmissionRequestFingerprint.RequestHash(factura);
        var keyHash = EmissionRequestFingerprint.OperationKeyHash(
            identity.ConsumerId,
            identity.ContextId,
            idempotencyKey);
        var evidence = StoredFiscalEvidence.FromRequest(factura);

        EmissionIdempotencyRecord record;
        try
        {
            record = await _store.GetOrCreateAsync(
                keyHash,
                requestHash,
                EmissionRequestFingerprint.CanonicalizationVersion,
                identity,
                evidence,
                cancellationToken);
        }
        catch (EmissionIdempotencyConflictException)
        {
            return IdempotencyConflict();
        }

        if (!MatchesFiscalIdentity(record, identity))
            return CredentialAssignmentMismatch();

        var replay = ReplayTerminal(record);
        if (replay is not null)
        {
            await ReleaseReplayReservationIfOwnedAsync(identity, keyHash, cancellationToken);
            return replay;
        }

        var gate = Locks.GetOrAdd(identity.SeriesKey, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);
        try
        {
            record = await _store.GetAsync(keyHash, cancellationToken)
                ?? throw new InvalidDataException("El registro de idempotencia desapareció durante la emisión.");

            if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal)
                || record.RequestCanonicalizationVersion != EmissionRequestFingerprint.CanonicalizationVersion)
            {
                return IdempotencyConflict();
            }

            if (!MatchesFiscalIdentity(record, identity))
                return CredentialAssignmentMismatch();

            replay = ReplayTerminal(record);
            if (replay is not null)
            {
                await ReleaseReplayReservationIfOwnedAsync(identity, keyHash, cancellationToken);
                return replay;
            }

            var activeReservation = await _seriesCoordinator.GetActiveAsync(identity, cancellationToken);
            if (activeReservation is not null
                && !string.Equals(activeReservation.OwnerKeyHash, keyHash, StringComparison.Ordinal))
            {
                return Error(
                    "SERIES_RESERVATION_BLOCKED",
                    "La serie fiscal tiene otra operación pendiente de resolución; no se asignará ni enviará un segundo comprobante.",
                    dcEmissionOutcome.FailedBeforeSubmission);
            }

            // Recover the narrow crash window where the durable reservation was written but
            // the operation record had not yet persisted NumberAssigned.
            if (activeReservation is not null && !record.NumeroComprobante.HasValue)
            {
                record = record with
                {
                    NumeroComprobante = activeReservation.NumeroComprobante,
                    State = EmissionIdempotencyState.NumberAssigned,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                await _store.SaveAsync(record, cancellationToken);
            }

            if (record.State is EmissionIdempotencyState.Submitting or EmissionIdempotencyState.Uncertain)
            {
                if (!record.NumeroComprobante.HasValue)
                    throw new InvalidDataException("Una operación enviada o incierta no tiene número reservado.");

                if (activeReservation is null)
                    await _seriesCoordinator.ReserveAsync(identity, keyHash, record.NumeroComprobante.Value, cancellationToken);

                if (record.FiscalEvidence.State == FiscalEvidenceState.LegacyUnavailable)
                {
                    return Error(
                        "LEGACY_RECONCILIATION_REQUIRED",
                        "La operación legacy quedó pendiente sin evidencia fiscal comparable y requiere resolución manual antes de continuar.",
                        dcEmissionOutcome.Uncertain,
                        record.NumeroComprobante.Value);
                }

                return await ReconcileRecordedAttemptAsync(record, tipo, cancellationToken);
            }

            if (!record.NumeroComprobante.HasValue)
            {
                var ultimo = await _wsfe.FECompUltimoAutorizadoAsync(tipo, cancellationToken);
                if (!ultimo.Success)
                    return BeforeSubmissionFailure(ultimo);

                var numero = ultimo.NumeroComprobante + 1;
                await _seriesCoordinator.ReserveAsync(identity, keyHash, numero, cancellationToken);

                factura.NumeroComprobante = numero;
                record = record with
                {
                    NumeroComprobante = numero,
                    State = EmissionIdempotencyState.NumberAssigned,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                await _store.SaveAsync(record, cancellationToken);
            }
            else
            {
                factura.NumeroComprobante = record.NumeroComprobante.Value;
                if (activeReservation is null)
                    await _seriesCoordinator.ReserveAsync(identity, keyHash, record.NumeroComprobante.Value, cancellationToken);
            }

            if (record.State != EmissionIdempotencyState.NumberAssigned)
                throw new InvalidDataException($"Estado de idempotencia inesperado antes de emitir: {record.State}.");

            // Revalidate immediately before crossing the side-effect boundary. The reserved
            // number is safe only while ARCA still reports its predecessor as the last authorized.
            var revalidation = await _wsfe.FECompUltimoAutorizadoAsync(tipo, cancellationToken);
            if (!revalidation.Success)
                return BeforeSubmissionFailure(revalidation, record.NumeroComprobante.Value);

            if (revalidation.NumeroComprobante != record.NumeroComprobante.Value - 1)
            {
                return Error(
                    "SERIES_NUMBER_DRIFT",
                    $"La serie cambió después de reservar el número {record.NumeroComprobante.Value}; se mantiene bloqueada para resolución manual.",
                    dcEmissionOutcome.FailedBeforeSubmission,
                    record.NumeroComprobante.Value);
            }

            record = record with
            {
                State = EmissionIdempotencyState.Submitting,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await _store.SaveAsync(record, cancellationToken);

            dcFacturaResponse result;
            try
            {
                result = await _wsfe.FECAESolicitarAsync(factura, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Submitting + durable reservation survive the caller cancellation.
                throw;
            }
            catch (Exception)
            {
                var uncertain = Error(
                    "EMISSION_UNCERTAIN",
                    "La emisión quedó en estado incierto y debe reconciliarse antes de reintentar.",
                    dcEmissionOutcome.Uncertain,
                    factura.NumeroComprobante ?? 0);
                await SaveOutcomeAsync(record, EmissionIdempotencyState.Uncertain, uncertain, CancellationToken.None);
                throw;
            }

            // dcWsfeClient can recover a transport ambiguity internally through FECompConsultar.
            // That path must pass the same fiscal-equivalence gate as a later MCP retry.
            if (result.Success && result.EmissionOutcome == dcEmissionOutcome.RecoveredSuccess)
            {
                var comparison = FiscalReconciliationComparer.Compare(record, result);
                if (comparison.Match != FiscalReconciliationMatch.Equivalent)
                {
                    var blocked = Error(
                        comparison.Code,
                        comparison.Message,
                        dcEmissionOutcome.Uncertain,
                        record.NumeroComprobante.Value);
                    await SaveOutcomeAsync(record, EmissionIdempotencyState.Uncertain, blocked, cancellationToken);
                    return blocked;
                }
            }

            var state = result.Success
                ? EmissionIdempotencyState.Authorized
                : result.EmissionOutcome == dcEmissionOutcome.FiscalRejected
                    ? EmissionIdempotencyState.FiscalRejected
                    : EmissionIdempotencyState.Uncertain;

            if (!result.Success
                && result.EmissionOutcome is dcEmissionOutcome.None
                    or dcEmissionOutcome.InvalidRequest
                    or dcEmissionOutcome.FailedBeforeSubmission)
            {
                result.EmissionOutcome = dcEmissionOutcome.Uncertain;
                if (string.IsNullOrWhiteSpace(result.Codigo)) result.Codigo = "EMISSION_UNCERTAIN";
                state = EmissionIdempotencyState.Uncertain;
            }

            await SaveOutcomeAsync(record, state, result, cancellationToken);
            if (state is EmissionIdempotencyState.Authorized or EmissionIdempotencyState.FiscalRejected)
                await _seriesCoordinator.ReleaseAsync(identity, keyHash, cancellationToken);
            return result;
        }
        catch (EmissionTerminalStateConflictException conflict)
        {
            var terminal = ReplayTerminal(conflict.Existing)
                ?? throw new InvalidDataException("El store informó conflicto terminal sin un resultado fiscal terminal válido.");
            await ReleaseReplayReservationIfOwnedAsync(
                conflict.Existing.Identity,
                conflict.Existing.KeyHash,
                CancellationToken.None);
            return terminal;
        }
        catch (FiscalSeriesBlockedException)
        {
            return Error(
                "SERIES_RESERVATION_BLOCKED",
                "La serie fiscal está reservada por otra operación pendiente; no se realizará un segundo side effect.",
                dcEmissionOutcome.FailedBeforeSubmission);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<dcFacturaResponse> ReconcileRecordedAttemptAsync(
        EmissionIdempotencyRecord record,
        dcTipoComprobante tipo,
        CancellationToken cancellationToken)
    {
        if (!record.NumeroComprobante.HasValue)
            throw new InvalidDataException("Una emisión iniciada no tiene número persistido.");

        var numero = record.NumeroComprobante.Value;
        var consulted = await _wsfe.FECompConsultarAsync(numero, tipo, cancellationToken);
        if (consulted.Success && !string.IsNullOrWhiteSpace(consulted.Cae))
        {
            consulted.NumeroComprobante = numero;
            var comparison = FiscalReconciliationComparer.Compare(record, consulted);
            if (comparison.Match == FiscalReconciliationMatch.Equivalent)
            {
                consulted.EmissionOutcome = dcEmissionOutcome.RecoveredSuccess;
                await SaveOutcomeAsync(record, EmissionIdempotencyState.Authorized, consulted, cancellationToken);
                await _seriesCoordinator.ReleaseAsync(record.Identity, record.KeyHash, cancellationToken);
                return consulted;
            }

            var blocked = Error(
                comparison.Code,
                comparison.Message,
                dcEmissionOutcome.Uncertain,
                numero);
            await SaveOutcomeAsync(record, EmissionIdempotencyState.Uncertain, blocked, cancellationToken);
            return blocked;
        }

        var uncertain = record.FiscalResult?.ToResponse() ?? Error(
            "EMISSION_UNCERTAIN",
            "La emisión sigue en estado incierto; ARCA todavía no permitió confirmar el comprobante.",
            dcEmissionOutcome.Uncertain,
            numero);
        uncertain.Success = false;
        uncertain.NumeroComprobante = numero;
        uncertain.EmissionOutcome = dcEmissionOutcome.Uncertain;
        if (string.IsNullOrWhiteSpace(uncertain.Codigo)) uncertain.Codigo = "EMISSION_UNCERTAIN";

        await SaveOutcomeAsync(record, EmissionIdempotencyState.Uncertain, uncertain, cancellationToken);
        return uncertain;
    }

    private async Task SaveOutcomeAsync(
        EmissionIdempotencyRecord record,
        EmissionIdempotencyState state,
        dcFacturaResponse response,
        CancellationToken cancellationToken)
    {
        var updated = record with
        {
            State = state,
            FiscalResult = StoredFiscalResult.FromResponse(response),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _store.SaveAsync(updated, cancellationToken);
    }

    private async Task ReleaseReplayReservationIfOwnedAsync(
        FiscalOperationIdentity identity,
        string keyHash,
        CancellationToken cancellationToken)
    {
        var active = await _seriesCoordinator.GetActiveAsync(identity, cancellationToken);
        if (active is not null && string.Equals(active.OwnerKeyHash, keyHash, StringComparison.Ordinal))
            await _seriesCoordinator.ReleaseAsync(identity, keyHash, cancellationToken);
    }

    private static dcFacturaResponse BeforeSubmissionFailure(dcFacturaResponse response, long numero = 0)
    {
        response.Success = false;
        response.NumeroComprobante = numero;
        response.EmissionOutcome = dcEmissionOutcome.FailedBeforeSubmission;
        return response;
    }

    private static bool MatchesFiscalIdentity(
        EmissionIdempotencyRecord record,
        FiscalOperationIdentity currentIdentity)
        => record.Identity.MatchesImmutableIdentity(currentIdentity)
            && string.Equals(
                record.Identity.CredentialAssignmentRevision,
                currentIdentity.CredentialAssignmentRevision,
                StringComparison.Ordinal);

    private static dcFacturaResponse? ReplayTerminal(EmissionIdempotencyRecord record)
        => record.State is EmissionIdempotencyState.Authorized or EmissionIdempotencyState.FiscalRejected
            ? record.FiscalResult?.ToResponse()
            : null;

    private static dcFacturaResponse IdempotencyConflict()
        => Error(
            "IDEMPOTENCY_KEY_REUSED",
            "La idempotencyKey ya fue utilizada con una solicitud o identidad fiscal diferente.",
            dcEmissionOutcome.InvalidRequest);

    private static dcFacturaResponse CredentialAssignmentMismatch()
        => Error(
            "CREDENTIAL_ASSIGNMENT_MISMATCH",
            "La operación durable quedó fijada a otra revisión de credencial; se requiere resolver esa revisión antes de continuar.",
            dcEmissionOutcome.FailedBeforeSubmission);

    private static dcFacturaResponse Error(
        string code,
        string message,
        dcEmissionOutcome outcome = dcEmissionOutcome.None,
        long numero = 0)
        => new()
        {
            Success = false,
            Codigo = code,
            Mensaje = message,
            EmissionOutcome = outcome,
            NumeroComprobante = numero,
            Errores = [message]
        };
}
