using dcArca.Core.Models;
using dcArca.Core.Services;
using Microsoft.Extensions.Configuration;

namespace dcArca.Cli;

public interface IFiscalRepresentationProbeExecutor
{
    Task<FiscalRepresentationProbeOutcome> ProbeAsync(
        FiscalTechnicalContextRecord context,
        CredentialAssignmentRecord assignment,
        FiscalRepresentationRecord representation,
        long representedCuit,
        int pointOfSale,
        CancellationToken cancellationToken = default);
}

public sealed record FiscalRepresentationActivationCommandResult(
    int ExitCode,
    FiscalRepresentationLifecycleDisposition Disposition,
    string Code,
    string SafeMessage,
    FiscalRepresentationRecord Representation);

public sealed class FiscalRepresentationActivationCommand
{
    private readonly IFiscalTechnicalContextStore _store;
    private readonly FiscalRepresentationLifecyclePolicy _lifecycle;
    private readonly IFiscalRepresentationProbeExecutor _probe;

    public FiscalRepresentationActivationCommand(
        IFiscalTechnicalContextStore store,
        FiscalRepresentationLifecyclePolicy lifecycle,
        IFiscalRepresentationProbeExecutor probe)
    {
        _store = store;
        _lifecycle = lifecycle;
        _probe = probe;
    }

    public async Task<FiscalRepresentationActivationCommandResult> ExecuteAsync(
        string contextId,
        string consumerId,
        long representedCuit,
        int pointOfSale,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contextId))
            throw new ArgumentException("El contextId es obligatorio.", nameof(contextId));
        if (string.IsNullOrWhiteSpace(consumerId))
            throw new ArgumentException("El consumerId es obligatorio.", nameof(consumerId));
        if (representedCuit <= 0)
            throw new ArgumentOutOfRangeException(nameof(representedCuit));
        if (pointOfSale <= 0)
            throw new ArgumentOutOfRangeException(nameof(pointOfSale));
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("El actor es obligatorio.", nameof(actor));

        var context = await _store.GetContextAsync(contextId, cancellationToken)
            ?? throw new InvalidOperationException("FISCAL_CONTEXT_NOT_FOUND");
        if (context.OperationalState != FiscalContextOperationalState.Active)
            throw new InvalidOperationException("FISCAL_CONTEXT_NOT_ACTIVE");
        var assignment = context.ActiveAssignment
            ?? throw new InvalidOperationException("ACTIVE_ASSIGNMENT_REQUIRED");

        var candidate = await _store.GetRepresentationAsync(
            contextId,
            consumerId,
            representedCuit,
            pointOfSale,
            cancellationToken)
            ?? throw new InvalidOperationException("FISCAL_REPRESENTATION_NOT_FOUND");
        if (!FiscalRepresentationLifecyclePolicy.IsRevalidableStatus(candidate.Status))
            throw new InvalidOperationException("FISCAL_REPRESENTATION_STATE_INVALID");

        var outcome = await _probe.ProbeAsync(
            context,
            assignment,
            candidate,
            representedCuit,
            pointOfSale,
            cancellationToken);
        var lifecycle = await _lifecycle.ApplyAsync(
            consumerId,
            outcome,
            actor,
            cancellationToken);

        return new FiscalRepresentationActivationCommandResult(
            lifecycle.Disposition == FiscalRepresentationLifecycleDisposition.Activated ? 0 : 2,
            lifecycle.Disposition,
            lifecycle.Code,
            lifecycle.SafeMessage,
            lifecycle.Representation);
    }
}

public sealed class FiscalRepresentationProbeExecutor : IFiscalRepresentationProbeExecutor
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly dcWsfePointOfSaleProbe _probe = new();

    public FiscalRepresentationProbeExecutor(
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public Task<FiscalRepresentationProbeOutcome> ProbeAsync(
        FiscalTechnicalContextRecord context,
        CredentialAssignmentRecord assignment,
        FiscalRepresentationRecord representation,
        long representedCuit,
        int pointOfSale,
        CancellationToken cancellationToken = default)
    {
        var legacy = new dcArcaConfig();
        _configuration.GetSection("dcArcaConfig").Bind(legacy);
        var legacyEnvironment =
            _configuration["FiscalContext:Environment"]
            ?? context.Environment;
        var resolver = new FiscalCredentialHostBindingResolver(
            _configuration,
            legacy,
            legacyEnvironment);
        var projected = new RepresentedFiscalContextRecord(
            context.ContextId,
            context.Environment,
            representedCuit,
            pointOfSale,
            context.OperationalState,
            context.ContextRevision,
            false,
            context.Assignments);
        var binding = resolver.Resolve(projected, assignment);

        return _probe.ProbeBoundAsync(
            context,
            assignment,
            representation,
            representedCuit,
            pointOfSale,
            binding,
            _httpClient,
            cancellationToken);
    }
}
