using System.Collections.Concurrent;
using dcArca.Core.Models;
using dcArca.Core.Services;

namespace dcArca.McpServer;

public sealed class McpInvoiceSequencer : IInvoiceIssuer
{
    private readonly IdcWsfeClient _wsfe;
    private readonly dcArcaConfig _config;
    private readonly IEmissionIdempotencyStore _store;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public McpInvoiceSequencer(
        IdcWsfeClient wsfe,
        dcArcaConfig config,
        IEmissionIdempotencyStore store)
    {
        _wsfe = wsfe;
        _config = config;
        _store = store;
    }

    public async Task<dcFacturaResponse> EmitAsync(
        dcFacturaRequest factura,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!factura.TipoComprobante.HasValue)
            return Error("TIPOC_INVALID", "TipoComprobante es obligatorio para emitir con numeración server-side.");

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Error("IDEMPOTENCY_KEY_REQUIRED", "idempotencyKey es obligatoria para emitir.");

        var tipo = factura.TipoComprobante.Value;
        var requestHash = EmissionRequestFingerprint.RequestHash(factura);
        var keyHash = EmissionRequestFingerprint.KeyHash(idempotencyKey);

        EmissionIdempotencyRecord record;
        try
        {
            record = await _store.GetOrCreateAsync(
                keyHash, requestHash, tipo, _config.PuntoVenta, cancellationToken);
        }
        catch (EmissionIdempotencyConflictException)
        {
            return IdempotencyConflict();
        }

        if (!MatchesFiscalIdentity(record, tipo))
            return IdempotencyConflict();

        var replay = ReplayTerminal(record);
        if (replay is not null) return replay;

        var seriesKey = $"{_config.Cuit}:{_config.PuntoVenta}:{(int)tipo}";
        var gate = Locks.GetOrAdd(seriesKey, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);
        try
        {
            record = await _store.GetAsync(keyHash, cancellationToken)
                ?? throw new InvalidDataException("El registro de idempotencia desapareció durante la emisión.");

            if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal)
                || !MatchesFiscalIdentity(record, tipo))
            {
                return IdempotencyConflict();
            }

            replay = ReplayTerminal(record);
            if (replay is not null) return replay;

            if (record.State is EmissionIdempotencyState.Submitting or EmissionIdempotencyState.Uncertain)
                return await ReconcileRecordedAttemptAsync(record, tipo, cancellationToken);

            if (!record.NumeroComprobante.HasValue)
            {
                var ultimo = await _wsfe.FECompUltimoAutorizadoAsync(tipo, cancellationToken);
                if (!ultimo.Success) return ultimo;

                var numero = ultimo.NumeroComprobante + 1;
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
            }

            if (record.State != EmissionIdempotencyState.NumberAssigned)
                throw new InvalidDataException($"Estado de idempotencia inesperado antes de emitir: {record.State}.");

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
                // El intento ya quedó durablemente marcado como Submitting.
                // Un retry posterior reconciliará este mismo número.
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

            var state = result.Success
                ? EmissionIdempotencyState.Authorized
                : result.EmissionOutcome == dcEmissionOutcome.FiscalRejected
                    ? EmissionIdempotencyState.FiscalRejected
                    : EmissionIdempotencyState.Uncertain;

            await SaveOutcomeAsync(record, state, result, cancellationToken);
            return result;
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
            consulted.EmissionOutcome = dcEmissionOutcome.RecoveredSuccess;
            await SaveOutcomeAsync(record, EmissionIdempotencyState.Authorized, consulted, cancellationToken);
            return consulted;
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

    private bool MatchesFiscalIdentity(EmissionIdempotencyRecord record, dcTipoComprobante tipo)
        => record.TipoComprobante == (int)tipo && record.PuntoVenta == _config.PuntoVenta;

    private static dcFacturaResponse? ReplayTerminal(EmissionIdempotencyRecord record)
        => record.State is EmissionIdempotencyState.Authorized or EmissionIdempotencyState.FiscalRejected
            ? record.FiscalResult?.ToResponse()
            : null;

    private static dcFacturaResponse IdempotencyConflict()
        => Error(
            "IDEMPOTENCY_KEY_REUSED",
            "La idempotencyKey ya fue utilizada con una solicitud o identidad fiscal diferente.");

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
