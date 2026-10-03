using System.Security.Claims;
using dcArca.Core.Models;
using dcArca.Core.Services;

namespace dcArca.McpServer;

public sealed class McpOperationContractService
{
    private readonly IEmissionIdempotencyStore _operations;
    private readonly IEmissionOperationInspector _inspector;
    private readonly IFiscalContextRuntimeResolver _runtimeResolver;
    private readonly IFiscalAssignmentAuthorizationValidator _assignmentValidator;
    private readonly IFiscalSeriesCoordinator _seriesCoordinator;

    public McpOperationContractService(
        IEmissionIdempotencyStore operations,
        IEmissionOperationInspector inspector,
        IFiscalContextRuntimeResolver runtimeResolver,
        IFiscalAssignmentAuthorizationValidator assignmentValidator,
        IFiscalSeriesCoordinator seriesCoordinator)
    {
        _operations = operations;
        _inspector = inspector;
        _runtimeResolver = runtimeResolver;
        _assignmentValidator = assignmentValidator;
        _seriesCoordinator = seriesCoordinator;
    }

    public async Task<McpCapabilitiesResult> GetCapabilitiesAsync(
        ClaimsPrincipal principal,
        string? contextId,
        CancellationToken cancellationToken = default)
    {
        var authorized = await _runtimeResolver.AuthorizeAsync(
            principal,
            contextId,
            "consultar",
            cancellationToken);
        var context = authorized.Context;
        var operations = new List<string>();
        if (HasScope(principal, "arca:consultar") && HasGrant(principal, context.ContextId, "consultar", allowLegacy: true))
            operations.AddRange(["capabilities", "validate", "diagnose", "consult", "reconcile", "render-pdf"]);
        if (HasScope(principal, "arca:facturar") && HasGrant(principal, context.ContextId, "facturar", allowLegacy: true))
            operations.Add("emit");

        return new McpCapabilitiesResult(
            ArcaMcpContract.Version,
            context.ContextId,
            context.Environment,
            context.RepresentedCuit,
            context.PointOfSale,
            context.OperationalState.ToString(),
            Enum.GetValues<dcTipoComprobante>().Select(x => (int)x).Order().ToArray(),
            Enum.GetValues<dcConcepto>().Select(x => (int)x).Order().ToArray(),
            ArcaMcpContract.ImplementedCurrenciesContract,
            SupportsAssociatedDocuments: true,
            SupportsAssociatedPeriods: true,
            SupportsDetailedVat: true,
            SupportsTributes: true,
            operations.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            RemoteFiscalEnablement: "not-verified-use-diagnostic");
    }

    public async Task<McpValidationResult> ValidateRequestAsync(
        ClaimsPrincipal principal,
        string? contextId,
        dcFacturaRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorized = await _runtimeResolver.AuthorizeAsync(
            principal,
            contextId,
            "facturar",
            cancellationToken);
        var issues = new List<McpValidationIssue>();
        if (authorized.Context.OperationalState != FiscalContextOperationalState.Active)
        {
            issues.Add(new McpValidationIssue(
                "FISCAL_CONTEXT_NOT_ACTIVE",
                "El contexto fiscal no admite nuevas emisiones.",
                "contextId",
                "local-context"));
        }

        var validation = dcFacturaPreflightValidator.Validate(request);
        if (!validation.IsValid)
        {
            issues.Add(new McpValidationIssue(
                validation.Code ?? "INVALID_REQUEST",
                validation.Message ?? "La solicitud fiscal no supera la validación determinística.",
                FieldFor(validation.Code),
                "deterministic"));
        }

        return new McpValidationResult(
            ArcaMcpContract.Version,
            authorized.Context.ContextId,
            issues.Count == 0,
            issues,
            NumberReserved: false,
            AuthorizationGuaranteed: false);
    }

    public async Task<McpFiscalDiagnosticResult> DiagnoseAsync(
        ClaimsPrincipal principal,
        string? contextId,
        CancellationToken cancellationToken = default)
    {
        var authorized = await _runtimeResolver.AuthorizeAsync(
            principal,
            contextId,
            "consultar",
            cancellationToken);
        var context = authorized.Context;
        var operationSummaries = await _inspector.ListByContextAsync(context.ContextId, cancellationToken);
        var diagnostics = new List<McpCredentialAssignmentDiagnostic>();
        FiscalAssignmentValidationResult? activeProbe = null;
        var localReady = context.ActiveAssignment is not null
            && context.OperationalState != FiscalContextOperationalState.Disabled;

        foreach (var assignment in context.Assignments.OrderBy(x => x.CreatedAt))
        {
            FiscalAssignmentValidationResult? probe = null;
            if (assignment.Status is CredentialAssignmentStatus.Candidate
                or CredentialAssignmentStatus.Validated
                or CredentialAssignmentStatus.Active)
            {
                probe = await _assignmentValidator.ProbeAsync(
                    context.ContextId,
                    assignment.AssignmentRevision,
                    cancellationToken);
                if (assignment.Status == CredentialAssignmentStatus.Active)
                    activeProbe = probe;
                if (probe.Status == FiscalAssignmentValidationStatus.InvalidConfiguration
                    && assignment.Status == CredentialAssignmentStatus.Active)
                    localReady = false;
            }

            var pending = operationSummaries.Count(x =>
                string.Equals(x.AssignmentRevision, assignment.AssignmentRevision, StringComparison.Ordinal)
                && x.State is not (EmissionIdempotencyState.Authorized or EmissionIdempotencyState.FiscalRejected));
            var blockers = new List<string>();
            if (assignment.Status == CredentialAssignmentStatus.Active)
                blockers.Add("active-assignment");
            if (pending > 0)
                blockers.Add($"pending-operations:{pending}");
            if (assignment.Status == CredentialAssignmentStatus.Candidate
                && string.IsNullOrWhiteSpace(assignment.ValidatedAuthorizationEvidence))
                blockers.Add("authorization-not-validated");

            diagnostics.Add(new McpCredentialAssignmentDiagnostic(
                assignment.AssignmentRevision,
                assignment.CredentialId,
                assignment.Status.ToString(),
                assignment.Status == CredentialAssignmentStatus.Active,
                assignment.CreatedAt,
                assignment.ValidatedAt,
                assignment.ActivatedAt,
                probe?.Code,
                probe?.SafeMessage,
                probe?.CheckedAt,
                blockers));
        }

        var remoteState = activeProbe?.Verified == true
            ? "verified"
            : activeProbe is null ? "not-verified" : "not-verified";
        var safeMessage = activeProbe?.Verified == true
            ? "La asignación activa fue verificada remotamente sin emitir comprobantes."
            : "La autorización fiscal remota de la asignación activa no está verificada en este diagnóstico.";

        return new McpFiscalDiagnosticResult(
            ArcaMcpContract.Version,
            context.ContextId,
            context.Environment,
            context.RepresentedCuit,
            context.PointOfSale,
            context.OperationalState.ToString(),
            localReady,
            remoteState,
            diagnostics,
            DateTimeOffset.UtcNow,
            safeMessage);
    }

    public async Task<McpOperationResult> GetOperationAsync(
        ClaimsPrincipal principal,
        string? contextId,
        string? operationId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var located = await LocateAsync(principal, contextId, operationId, idempotencyKey, cancellationToken);
        return located.Record is null
            ? NotFound(located.ContextId)
            : Build(located.Record, officialRead: null);
    }

    public async Task<McpOperationResult> ReconcileAsync(
        ClaimsPrincipal principal,
        string? contextId,
        string? operationId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var located = await LocateAsync(principal, contextId, operationId, idempotencyKey, cancellationToken);
        var record = located.Record;
        if (record is null) return NotFound(located.ContextId);

        if (record.State is EmissionIdempotencyState.Authorized or EmissionIdempotencyState.FiscalRejected)
            return Build(record, officialRead: null);

        if (record.State is EmissionIdempotencyState.Created or EmissionIdempotencyState.NumberAssigned)
        {
            return Build(record, officialRead: null) with
            {
                SafeMessage = "La operación todavía no cruzó un límite de envío confirmado; reconciliar no emitirá ni adelantará su estado."
            };
        }

        if (!record.NumeroComprobante.HasValue)
        {
            return Build(record, officialRead: null) with
            {
                ErrorCode = "RECONCILIATION_NUMBER_MISSING",
                SafeMessage = "La operación incierta no tiene un número durable para consultar y requiere revisión manual."
            };
        }

        if (record.FiscalEvidence.State != FiscalEvidenceState.Complete)
        {
            return Build(record, officialRead: null) with
            {
                ErrorCode = "RECONCILIATION_EVIDENCE_INSUFFICIENT",
                SafeMessage = "La operación no conserva evidencia fiscal estructurada suficiente para una reconciliación automática."
            };
        }

        using var runtime = await _runtimeResolver.ResolveForHistoricalOperationAsync(
            principal,
            record,
            cancellationToken);
        var consulted = await runtime.Wsfe.FECompConsultarAsync(
            record.NumeroComprobante.Value,
            (dcTipoComprobante)record.Identity.TipoComprobante,
            cancellationToken);

        if (!consulted.Success || string.IsNullOrWhiteSpace(consulted.Cae))
        {
            return Build(record, consulted) with
            {
                ErrorCode = "RECONCILIATION_NOT_CONFIRMED",
                SafeMessage = "La lectura fiscal no confirmó un comprobante autorizado equivalente. No se realizó ninguna emisión."
            };
        }

        consulted.NumeroComprobante = record.NumeroComprobante.Value;
        var comparison = FiscalReconciliationComparer.Compare(record, consulted);
        if (comparison.Match == FiscalReconciliationMatch.Equivalent)
        {
            consulted.EmissionOutcome = dcEmissionOutcome.RecoveredSuccess;
            var authorized = record with
            {
                State = EmissionIdempotencyState.Authorized,
                FiscalResult = StoredFiscalResult.FromResponse(consulted),
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await _operations.SaveAsync(authorized, cancellationToken);
            await _seriesCoordinator.ReleaseAsync(record.Identity, record.KeyHash, cancellationToken);
            return Build(authorized, consulted) with
            {
                SafeMessage = "La lectura fiscal confirmó equivalencia completa con la intención persistida; no se emitió un nuevo comprobante."
            };
        }

        var synthetic = new dcFacturaResponse
        {
            Success = false,
            NumeroComprobante = record.NumeroComprobante.Value,
            Codigo = comparison.Code,
            Mensaje = comparison.Message,
            EmissionOutcome = dcEmissionOutcome.Uncertain,
            Errores = [comparison.Message]
        };
        var uncertain = record with
        {
            State = EmissionIdempotencyState.Uncertain,
            FiscalResult = StoredFiscalResult.FromResponse(synthetic),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _operations.SaveAsync(uncertain, cancellationToken);
        return Build(uncertain, consulted) with
        {
            ErrorCode = comparison.Code,
            SafeMessage = comparison.Message
        };
    }

    public async Task DecorateEmissionResponseAsync(
        string consumerId,
        string contextId,
        string idempotencyKey,
        dcFacturaResponse response,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return;
        var operationId = EmissionRequestFingerprint.OperationKeyHash(consumerId, contextId, idempotencyKey);
        var record = await _operations.GetAsync(operationId, cancellationToken);
        if (record is null) return;
        response.OperationId = record.KeyHash;
        response.OperationContractVersion = ArcaMcpContract.Version;
        response.OperationContextId = record.Identity.ContextId;
        response.OperationState = record.State.ToString();
        response.AllowedNextActions = AllowedActions(record);
    }

    private async Task<(string ContextId, EmissionIdempotencyRecord? Record)> LocateAsync(
        ClaimsPrincipal principal,
        string? requestedContextId,
        string? operationId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var authorized = await _runtimeResolver.AuthorizeAsync(
            principal,
            requestedContextId,
            "consultar",
            cancellationToken);
        if (string.IsNullOrWhiteSpace(operationId) == string.IsNullOrWhiteSpace(idempotencyKey))
            throw new FiscalContextAccessException(
                "OPERATION_LOOKUP_INVALID",
                "Informe exactamente operationId o idempotencyKey.");

        string keyHash;
        if (!string.IsNullOrWhiteSpace(operationId))
        {
            keyHash = operationId.Trim().ToLowerInvariant();
            if (keyHash.Length != 64 || !keyHash.All(Uri.IsHexDigit))
                return (authorized.Context.ContextId, null);
        }
        else
        {
            keyHash = EmissionRequestFingerprint.OperationKeyHash(
                authorized.ConsumerId,
                authorized.Context.ContextId,
                idempotencyKey!);
        }

        var record = await _operations.GetAsync(keyHash, cancellationToken);
        if (record is null
            || !string.Equals(record.Identity.ConsumerId, authorized.ConsumerId, StringComparison.Ordinal)
            || !string.Equals(record.Identity.ContextId, authorized.Context.ContextId, StringComparison.Ordinal))
            return (authorized.Context.ContextId, null);
        return (authorized.Context.ContextId, record);
    }

    private static McpOperationResult Build(
        EmissionIdempotencyRecord record,
        dcFacturaResponse? officialRead)
    {
        var fiscalResult = record.FiscalResult is null
            ? null
            : new McpPersistedFiscalResult(
                record.FiscalResult.Success,
                EmptyToNull(record.FiscalResult.Cae),
                EmptyToNull(record.FiscalResult.CaeVencimiento),
                record.FiscalResult.NumeroComprobante > 0 ? record.FiscalResult.NumeroComprobante : null,
                EmptyToNull(record.FiscalResult.Resultado),
                record.FiscalResult.Codigo,
                record.FiscalResult.EmissionOutcome,
                record.FiscalResult.Observaciones,
                record.FiscalResult.Errores);
        var outcome = record.FiscalResult?.EmissionOutcome ?? dcEmissionOutcome.None;
        var reference = new McpFiscalReference(
            record.Identity.Environment,
            record.Identity.Cuit,
            record.Identity.PuntoVenta,
            record.Identity.TipoComprobante,
            record.NumeroComprobante);
        var unavailable = new List<string>();
        if (officialRead is null)
            unavailable.Add("officialRead");
        if (record.FiscalEvidence.State == FiscalEvidenceState.LegacyUnavailable)
            unavailable.Add("comparableFiscalEvidence");
        if (fiscalResult is null)
            unavailable.Add("persistedFiscalResult");

        return new McpOperationResult(
            ArcaMcpContract.Version,
            true,
            record.KeyHash,
            record.Identity.ContextId,
            record.State.ToString(),
            outcome,
            reference,
            record.Identity.CredentialAssignmentRevision,
            record.FiscalResult?.Codigo,
            SafeMessage(record),
            Array.Empty<McpValidationIssue>(),
            AllowedActions(record),
            fiscalResult,
            officialRead,
            unavailable,
            record.CreatedAt,
            record.UpdatedAt);
    }

    private static McpOperationResult NotFound(string contextId)
        => new(
            ArcaMcpContract.Version,
            false,
            null,
            contextId,
            null,
            dcEmissionOutcome.None,
            null,
            null,
            "OPERATION_NOT_FOUND",
            "No existe una operación durable visible en este namespace de consumidor/contexto.",
            Array.Empty<McpValidationIssue>(),
            Array.Empty<string>(),
            null,
            null,
            ["operation"],
            null,
            null);

    private static string[] AllowedActions(EmissionIdempotencyRecord record)
        => record.State switch
        {
            EmissionIdempotencyState.Created => ["retry-known-work"],
            EmissionIdempotencyState.NumberAssigned => ["retry-known-work", "consult"],
            EmissionIdempotencyState.Submitting => ["consult", "reconcile"],
            EmissionIdempotencyState.Uncertain => ["consult", "reconcile", "manual-review"],
            EmissionIdempotencyState.Authorized => ["consult", "render-pdf"],
            EmissionIdempotencyState.FiscalRejected => ["consult", "correct-with-new-operation"],
            _ => Array.Empty<string>()
        };

    private static string SafeMessage(EmissionIdempotencyRecord record)
        => record.State switch
        {
            EmissionIdempotencyState.Created => "La operación existe y todavía no tiene numeración reservada.",
            EmissionIdempotencyState.NumberAssigned => "La operación tiene número reservado pero no existe evidencia durable de envío.",
            EmissionIdempotencyState.Submitting => "La operación cruzó el límite de envío y debe consultarse/reconciliarse antes de cualquier nueva emisión.",
            EmissionIdempotencyState.Uncertain => "El resultado fiscal no está demostrado; la serie permanece protegida hasta reconciliación o revisión manual.",
            EmissionIdempotencyState.Authorized => "La operación está autorizada y puede consultarse o regenerar su documento.",
            EmissionIdempotencyState.FiscalRejected => "La operación fue rechazada fiscalmente; corregir requiere una nueva operación.",
            _ => "Estado durable disponible."
        };

    private static string? FieldFor(string? code)
        => code switch
        {
            "TIPOC_INVALID" => "tipoComprobante",
            "CONCEPTO_MISSING" => "concepto",
            "FCHSD_REQUIRED" => "fechaServicioDesde",
            "FCHSH_REQUIRED" => "fechaServicioHasta",
            "FCHVTO_REQUIRED" => "fechaVencimiento",
            "FCHCBTE_REQUIRED" or "FCHCBTE_INVALID" => "fechaComprobante",
            "DOC_INVALID" or "DOC_CUIT_INVALID" => "cuitReceptor",
            "MONEDA_REQUIRED" => "monedaId",
            "MONEDA_COT_INVALID" => "monedaCotizacion",
            "IMP_MISMATCH" or "IMP_NEGATIVE" => "importeTotal",
            "IVA_MISMATCH" or "IVA_BASE_MISMATCH" or "IVA_DETAIL_REQUIRED" or "IVA_AMBIGUOUS" => "iva",
            "NOTA_CBTEASOC_10197" => "comprobanteAsociado",
            _ => null
        };

    private static bool HasScope(ClaimsPrincipal principal, string scope)
        => principal.FindAll("scope").Any(claim =>
            claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(scope, StringComparer.Ordinal));

    private static bool HasGrant(ClaimsPrincipal principal, string contextId, string operation, bool allowLegacy)
    {
        if (principal.FindAll(ArcaClaimTypes.ContextGrant).Any(claim =>
                string.Equals(claim.Value, ArcaClaimTypes.GrantValue(contextId, operation), StringComparison.Ordinal)))
            return true;
        return allowLegacy && principal.HasClaim(ArcaClaimTypes.LegacyKey, "true");
    }

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
