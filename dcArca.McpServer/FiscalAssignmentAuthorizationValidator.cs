using dcArca.Core.Services;

namespace dcArca.McpServer;

public enum FiscalAssignmentValidationStatus
{
    Verified,
    NotVerified,
    InvalidConfiguration
}

public sealed record FiscalAssignmentValidationResult(
    FiscalAssignmentValidationStatus Status,
    string Code,
    string SafeMessage,
    string ContextId,
    string AssignmentRevision,
    string CredentialId,
    int PointOfSale,
    DateTimeOffset CheckedAt,
    string? Evidence = null)
{
    public bool Verified => Status == FiscalAssignmentValidationStatus.Verified;
}

public interface IFiscalAssignmentAuthorizationValidator
{
    Task<FiscalAssignmentValidationResult> ProbeAsync(
        string contextId,
        string assignmentRevision,
        CancellationToken cancellationToken = default);

    Task<FiscalAssignmentValidationResult> ValidateCandidateAsync(
        string contextId,
        string assignmentRevision,
        string actor,
        CancellationToken cancellationToken = default);
}

public sealed class FiscalAssignmentAuthorizationValidator : IFiscalAssignmentAuthorizationValidator
{
    private readonly IRepresentedFiscalContextStore _contexts;
    private readonly IFiscalCredentialMaterializer _materializer;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly dcWsfePointOfSaleProbe _probe = new();

    public FiscalAssignmentAuthorizationValidator(
        IRepresentedFiscalContextStore contexts,
        IFiscalCredentialMaterializer materializer,
        IHttpClientFactory httpClientFactory)
    {
        _contexts = contexts;
        _materializer = materializer;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<FiscalAssignmentValidationResult> ProbeAsync(
        string contextId,
        string assignmentRevision,
        CancellationToken cancellationToken = default)
    {
        var context = await _contexts.GetAsync(contextId, cancellationToken)
            ?? throw new FiscalContextAccessException("FISCAL_CONTEXT_NOT_FOUND", "El contexto fiscal no existe.");
        var assignment = context.Assignments.SingleOrDefault(x =>
                string.Equals(x.AssignmentRevision, assignmentRevision, StringComparison.Ordinal))
            ?? throw new FiscalContextAccessException("ASSIGNMENT_NOT_FOUND", "La revisión de asignación no existe en el contexto fiscal.");

        FiscalCredentialMaterialization materialized;
        try
        {
            materialized = _materializer.Materialize(context, assignment);
        }
        catch (FiscalContextAccessException exception)
        {
            return new FiscalAssignmentValidationResult(
                FiscalAssignmentValidationStatus.InvalidConfiguration,
                exception.Code,
                exception.Message,
                context.ContextId,
                assignment.AssignmentRevision,
                assignment.CredentialId,
                context.PointOfSale,
                DateTimeOffset.UtcNow);
        }

        var client = _httpClientFactory.CreateClient(nameof(FiscalAssignmentAuthorizationValidator));
        var probe = await _probe.ProbeAsync(
            materialized.Config,
            materialized.WsfeAuth,
            client,
            cancellationToken);
        var evidence = probe.Verified
            ? $"FEParamGetPtosVenta|context={context.ContextId}|assignment={assignment.AssignmentRevision}|pv={probe.PointOfSale}|emission={probe.EmissionType}|checked={probe.CheckedAt:O}"
            : null;
        return new FiscalAssignmentValidationResult(
            probe.Verified ? FiscalAssignmentValidationStatus.Verified : FiscalAssignmentValidationStatus.NotVerified,
            probe.Verified ? "ASSIGNMENT_AUTHORIZATION_VERIFIED" : probe.Code,
            probe.SafeMessage,
            context.ContextId,
            assignment.AssignmentRevision,
            assignment.CredentialId,
            context.PointOfSale,
            probe.CheckedAt,
            evidence);
    }

    public async Task<FiscalAssignmentValidationResult> ValidateCandidateAsync(
        string contextId,
        string assignmentRevision,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("actor es obligatorio.", nameof(actor));

        var context = await _contexts.GetAsync(contextId, cancellationToken)
            ?? throw new FiscalContextAccessException("FISCAL_CONTEXT_NOT_FOUND", "El contexto fiscal no existe.");
        var assignment = context.Assignments.SingleOrDefault(x =>
                string.Equals(x.AssignmentRevision, assignmentRevision, StringComparison.Ordinal))
            ?? throw new FiscalContextAccessException("ASSIGNMENT_NOT_FOUND", "La revisión de asignación no existe en el contexto fiscal.");
        if (assignment.Status is not (CredentialAssignmentStatus.Candidate or CredentialAssignmentStatus.Validated))
            throw new FiscalContextAccessException("ASSIGNMENT_VALIDATION_STATE_INVALID", "La asignación no está en un estado validable.");

        var result = await ProbeAsync(contextId, assignmentRevision, cancellationToken);
        if (result.Verified && !string.IsNullOrWhiteSpace(result.Evidence))
        {
            await _contexts.MarkAssignmentValidatedAsync(
                contextId,
                assignmentRevision,
                result.Evidence,
                actor,
                cancellationToken);
        }
        return result;
    }
}
