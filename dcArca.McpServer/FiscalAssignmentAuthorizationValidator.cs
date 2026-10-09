using System.Security.Cryptography.X509Certificates;
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
    string? Evidence = null,
    DateTimeOffset? CertificateNotBefore = null,
    DateTimeOffset? CertificateNotAfter = null)
{
    public bool Verified => Status == FiscalAssignmentValidationStatus.Verified;
}

public interface IFiscalAssignmentAuthorizationValidator
{
    Task<FiscalAssignmentValidationResult> ProbeAsync(
        string contextId,
        string assignmentRevision,
        CancellationToken cancellationToken = default);

    Task<FiscalAssignmentValidationResult> ProbeRepresentationAsync(
        string contextId, string assignmentRevision, long representedCuit,
        int pointOfSale, CancellationToken cancellationToken = default)
        => ProbeAsync(contextId, assignmentRevision, cancellationToken);

    Task<FiscalAssignmentValidationResult> ValidateCandidateAsync(
        string contextId,
        string assignmentRevision,
        string actor,
        CancellationToken cancellationToken = default);
}

public sealed class FiscalAssignmentAuthorizationValidator : IFiscalAssignmentAuthorizationValidator
{
    private readonly IRepresentedFiscalContextStore? _contexts;
    private readonly IFiscalTechnicalContextStore? _catalog;
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

    public FiscalAssignmentAuthorizationValidator(
        IFiscalTechnicalContextStore catalog,
        IFiscalCredentialMaterializer materializer,
        IHttpClientFactory httpClientFactory)
    {
        _catalog = catalog;
        _materializer = materializer;
        _httpClientFactory = httpClientFactory;
    }

    public Task<FiscalAssignmentValidationResult> ProbeAsync(
        string contextId, string assignmentRevision, CancellationToken cancellationToken = default)
        => ProbeRepresentationAsync(contextId, assignmentRevision, 0, 0, cancellationToken);

    public async Task<FiscalAssignmentValidationResult> ProbeRepresentationAsync(
        string contextId, string assignmentRevision, long representedCuit, int pointOfSale,
        CancellationToken cancellationToken = default)
    {
        RepresentedFiscalContextRecord context;
        if (_catalog is null)
        {
            context = await (_contexts ?? throw new InvalidOperationException("CONTEXT_STORE_MISSING"))
                .GetAsync(contextId, cancellationToken)
                ?? throw new FiscalContextAccessException("FISCAL_CONTEXT_NOT_FOUND", "El contexto fiscal no existe.");
        }
        else
        {
            var technical = await _catalog.GetContextAsync(contextId, cancellationToken)
                ?? throw new FiscalContextAccessException("FISCAL_CONTEXT_NOT_FOUND", "El contexto técnico no existe.");
            if (representedCuit <= 0 || pointOfSale <= 0)
                throw new FiscalContextAccessException("FISCAL_REPRESENTATION_REQUIRED", "El diagnóstico remoto requiere CUIT y PV.");
            context = new RepresentedFiscalContextRecord(technical.ContextId, technical.Environment,
                representedCuit, pointOfSale, technical.OperationalState, technical.ContextRevision, false, technical.Assignments);
        }

        return await ProbeCoreAsync(context, assignmentRevision, cancellationToken);
    }

    private async Task<FiscalAssignmentValidationResult> ProbeCoreAsync(
        RepresentedFiscalContextRecord context, string assignmentRevision, CancellationToken cancellationToken)
    {
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

        DateTimeOffset? certificateNotBefore;
        DateTimeOffset? certificateNotAfter;
        try
        {
            using var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                materialized.Config.CertificatePath,
                materialized.Config.CertificatePassword);
            certificateNotBefore = new DateTimeOffset(certificate.NotBefore.ToUniversalTime());
            certificateNotAfter = new DateTimeOffset(certificate.NotAfter.ToUniversalTime());
        }
        catch
        {
            return new FiscalAssignmentValidationResult(
                FiscalAssignmentValidationStatus.InvalidConfiguration,
                "CERTIFICATE_UNREADABLE",
                "La credencial configurada no pudo leerse como certificado PKCS#12.",
                context.ContextId,
                assignment.AssignmentRevision,
                assignment.CredentialId,
                context.PointOfSale,
                DateTimeOffset.UtcNow);
        }

        var validity = $"Vigencia conocida: {certificateNotBefore:O} a {certificateNotAfter:O}.";
        var now = DateTimeOffset.UtcNow;
        if (certificateNotAfter <= now)
        {
            return new FiscalAssignmentValidationResult(
                FiscalAssignmentValidationStatus.InvalidConfiguration,
                "CERTIFICATE_EXPIRED",
                $"El certificado configurado está vencido y no puede considerarse autorizado. {validity}",
                context.ContextId,
                assignment.AssignmentRevision,
                assignment.CredentialId,
                context.PointOfSale,
                now,
                CertificateNotBefore: certificateNotBefore,
                CertificateNotAfter: certificateNotAfter);
        }
        if (certificateNotBefore > now)
        {
            return new FiscalAssignmentValidationResult(
                FiscalAssignmentValidationStatus.InvalidConfiguration,
                "CERTIFICATE_NOT_YET_VALID",
                $"El certificado configurado todavía no se encuentra dentro de su período de vigencia. {validity}",
                context.ContextId,
                assignment.AssignmentRevision,
                assignment.CredentialId,
                context.PointOfSale,
                now,
                CertificateNotBefore: certificateNotBefore,
                CertificateNotAfter: certificateNotAfter);
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
            $"{probe.SafeMessage} {validity}",
            context.ContextId,
            assignment.AssignmentRevision,
            assignment.CredentialId,
            context.PointOfSale,
            probe.CheckedAt,
            evidence,
            certificateNotBefore,
            certificateNotAfter);
    }

    public async Task<FiscalAssignmentValidationResult> ValidateCandidateAsync(
        string contextId,
        string assignmentRevision,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("actor es obligatorio.", nameof(actor));

        var context = _catalog is null
            ? await (_contexts ?? throw new InvalidOperationException("CONTEXT_STORE_MISSING")).GetAsync(contextId, cancellationToken)
            : (await _catalog.GetContextAsync(contextId, cancellationToken)) is { } technical
                ? new RepresentedFiscalContextRecord(technical.ContextId, technical.Environment, 1, 1,
                    technical.OperationalState, technical.ContextRevision, false, technical.Assignments)
                : null;
        if (context is null)
            throw new FiscalContextAccessException("FISCAL_CONTEXT_NOT_FOUND", "El contexto fiscal no existe.");
        var assignment = context.Assignments.SingleOrDefault(x =>
                string.Equals(x.AssignmentRevision, assignmentRevision, StringComparison.Ordinal))
            ?? throw new FiscalContextAccessException("ASSIGNMENT_NOT_FOUND", "La revisión de asignación no existe en el contexto fiscal.");
        if (assignment.Status is not (CredentialAssignmentStatus.Candidate or CredentialAssignmentStatus.Validated))
            throw new FiscalContextAccessException("ASSIGNMENT_VALIDATION_STATE_INVALID", "La asignación no está en un estado validable.");

        if (_catalog is not null)
        {
            // A technical certificate has no represented CUIT/PV. Validate PKCS#12 and
            // authenticate WSAA without invoking any fiscal PV probe.
            try
            {
                var material = _materializer.Materialize(context, assignment);
                using var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                    material.Config.CertificatePath, material.Config.CertificatePassword);
                var now = DateTimeOffset.UtcNow;
                if (certificate.NotAfter.ToUniversalTime() <= now.UtcDateTime ||
                    certificate.NotBefore.ToUniversalTime() > now.UtcDateTime)
                    return new FiscalAssignmentValidationResult(FiscalAssignmentValidationStatus.InvalidConfiguration,
                        "CERTIFICATE_NOT_VALID", "El certificado no está vigente.", contextId,
                        assignmentRevision, assignment.CredentialId, 0, now);
                await material.WsfeAuth.GetTokenAsync(cancellationToken);
                var evidence = $"WSAA|context={contextId}|revision={assignmentRevision}|checked={now:O}";
                await _catalog.MarkAssignmentValidatedAsync(contextId, assignmentRevision, evidence, actor, cancellationToken);
                return new FiscalAssignmentValidationResult(FiscalAssignmentValidationStatus.Verified,
                    "TECHNICAL_CREDENTIAL_VERIFIED", "Credencial técnica validada ante WSAA.",
                    contextId, assignmentRevision, assignment.CredentialId, 0, now, evidence);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch
            {
                return new FiscalAssignmentValidationResult(FiscalAssignmentValidationStatus.NotVerified,
                    "WSAA_AUTHORIZATION_NOT_VERIFIED", "La credencial técnica no pudo validarse ante WSAA.",
                    contextId, assignmentRevision, assignment.CredentialId, 0, DateTimeOffset.UtcNow);
            }
        }

        var result = await ProbeAsync(contextId, assignmentRevision, cancellationToken);
        if (result.Verified && !string.IsNullOrWhiteSpace(result.Evidence))
        {
            await (_contexts ?? throw new InvalidOperationException("CONTEXT_STORE_MISSING")).MarkAssignmentValidatedAsync(
                contextId, assignmentRevision, result.Evidence, actor, cancellationToken);
        }
        return result;
    }
}
