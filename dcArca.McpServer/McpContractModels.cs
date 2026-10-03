using dcArca.Core.Models;
using dcArca.Core.Services;

namespace dcArca.McpServer;

public static class ArcaMcpContract
{
    public const string Version = "arca-mcp/1.0";

    public static readonly string[] ImplementedCurrenciesContract = ["caller-supplied-arca-code"];
    public static readonly string[] ReadActions = ["consult", "reconcile", "render-pdf"];
}

public sealed record McpValidationIssue(
    string Code,
    string SafeMessage,
    string? Field = null,
    string Source = "deterministic");

public sealed record McpValidationResult(
    string ContractVersion,
    string ContextId,
    bool Valid,
    IReadOnlyList<McpValidationIssue> ValidationIssues,
    bool NumberReserved,
    bool AuthorizationGuaranteed);

public sealed record McpCapabilitiesResult(
    string ContractVersion,
    string ContextId,
    string Environment,
    long RepresentedCuit,
    int PointOfSale,
    string OperationalState,
    IReadOnlyList<int> ImplementedInvoiceTypes,
    IReadOnlyList<int> ImplementedConcepts,
    IReadOnlyList<string> CurrencyContract,
    bool SupportsAssociatedDocuments,
    bool SupportsAssociatedPeriods,
    bool SupportsDetailedVat,
    bool SupportsTributes,
    IReadOnlyList<string> AuthorizedOperations,
    string RemoteFiscalEnablement);

public sealed record McpFiscalReference(
    string Environment,
    long IssuerCuit,
    int PointOfSale,
    int InvoiceType,
    long? InvoiceNumber);

public sealed record McpOperationResult(
    string ContractVersion,
    bool Found,
    string? OperationId,
    string ContextId,
    string? State,
    dcEmissionOutcome EmissionOutcome,
    McpFiscalReference? FiscalReference,
    string? CredentialAssignmentRevision,
    string? ErrorCode,
    string SafeMessage,
    IReadOnlyList<McpValidationIssue> ValidationIssues,
    IReadOnlyList<string> AllowedNextActions,
    dcFacturaResponse? FiscalResult,
    IReadOnlyList<string> UnavailableFields,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record McpCredentialAssignmentDiagnostic(
    string AssignmentRevision,
    string CredentialId,
    string Status,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ValidatedAt,
    DateTimeOffset? ActivatedAt,
    string? LastValidationCode,
    string? LastValidationMessage,
    DateTimeOffset? LastValidationCheckedAt,
    IReadOnlyList<string> RetirementBlockers);

public sealed record McpFiscalDiagnosticResult(
    string ContractVersion,
    string ContextId,
    string Environment,
    long RepresentedCuit,
    int PointOfSale,
    string OperationalState,
    bool LocalConfigurationReady,
    string RemoteAuthorizationState,
    IReadOnlyList<McpCredentialAssignmentDiagnostic> Assignments,
    DateTimeOffset CheckedAt,
    string SafeMessage);

public sealed record EmissionOperationSummary(
    string OperationId,
    string ContextId,
    string AssignmentRevision,
    EmissionIdempotencyState State,
    DateTimeOffset UpdatedAt);

public interface IEmissionOperationInspector
{
    Task<IReadOnlyList<EmissionOperationSummary>> ListByContextAsync(
        string contextId,
        CancellationToken cancellationToken = default);
}

public sealed class FileSystemEmissionOperationInspector : IEmissionOperationInspector
{
    private readonly FileSystemEmissionIdempotencyStore _store;

    public FileSystemEmissionOperationInspector(FileSystemEmissionIdempotencyStore store)
        => _store = store;

    public async Task<IReadOnlyList<EmissionOperationSummary>> ListByContextAsync(
        string contextId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contextId)) return Array.Empty<EmissionOperationSummary>();
        var results = new List<EmissionOperationSummary>();
        foreach (var path in Directory.EnumerateFiles(_store.DirectoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.Length != 64 || !name.All(Uri.IsHexDigit)) continue;
            var record = await _store.GetAsync(name.ToLowerInvariant(), cancellationToken);
            if (record is null || !string.Equals(record.Identity.ContextId, contextId, StringComparison.Ordinal)) continue;
            results.Add(new EmissionOperationSummary(
                record.KeyHash,
                record.Identity.ContextId,
                record.Identity.CredentialAssignmentRevision,
                record.State,
                record.UpdatedAt));
        }
        return results.OrderByDescending(x => x.UpdatedAt).ToArray();
    }
}
