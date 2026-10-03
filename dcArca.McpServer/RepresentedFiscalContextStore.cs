using System.Text.Json;
using dcArca.Core.Services;

namespace dcArca.McpServer;

public enum FiscalContextOperationalState
{
    Active,
    ReadOnly,
    Disabled
}

public enum CredentialAssignmentStatus
{
    Candidate,
    Validated,
    Active,
    Historical,
    Disabled
}

public sealed record CredentialAssignmentRecord(
    string AssignmentRevision,
    string CredentialId,
    CredentialAssignmentStatus Status,
    string? ValidatedAuthorizationEvidence,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ValidatedAt = null,
    DateTimeOffset? ActivatedAt = null,
    string? Actor = null);

public sealed record RepresentedFiscalContextRecord(
    string ContextId,
    string Environment,
    long RepresentedCuit,
    int PointOfSale,
    FiscalContextOperationalState OperationalState,
    int ContextRevision,
    bool LegacyDefault,
    IReadOnlyList<CredentialAssignmentRecord> Assignments)
{
    public CredentialAssignmentRecord? ActiveAssignment
        => Assignments.SingleOrDefault(x => x.Status == CredentialAssignmentStatus.Active);
}

public interface IRepresentedFiscalContextStore
{
    Task InitializeLegacyAsync(RepresentedFiscalContextRecord context, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RepresentedFiscalContextRecord>> ListAsync(CancellationToken cancellationToken = default);
    Task<RepresentedFiscalContextRecord?> GetAsync(string contextId, CancellationToken cancellationToken = default);
    Task AddContextAsync(RepresentedFiscalContextRecord context, CancellationToken cancellationToken = default);
    Task AddCandidateAssignmentAsync(string contextId, string revision, string credentialId, string actor, CancellationToken cancellationToken = default);
    Task MarkAssignmentValidatedAsync(string contextId, string revision, string evidence, string actor, CancellationToken cancellationToken = default);
    Task ActivateAssignmentAsync(string contextId, string revision, string actor, CancellationToken cancellationToken = default);
    Task SetOperationalStateAsync(string contextId, FiscalContextOperationalState state, string actor, CancellationToken cancellationToken = default);
}

public sealed class FileSystemRepresentedFiscalContextStore : IRepresentedFiscalContextStore
{
    private readonly string _filePath;

    public FileSystemRepresentedFiscalContextStore(string? directory = null)
    {
        var resolvedDirectory = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dcArca", "fiscal-contexts")
            : Path.GetFullPath(directory);
        Directory.CreateDirectory(resolvedDirectory);
        TryHardenDirectory(resolvedDirectory);
        _filePath = Path.Combine(resolvedDirectory, "contexts.json");
    }

    public async Task InitializeLegacyAsync(RepresentedFiscalContextRecord context, CancellationToken cancellationToken = default)
    {
        ValidateContext(context);
        var coordinator = new dcWsaaFileCacheCoordinator(_filePath);
        await using (await coordinator.AcquireAsync(cancellationToken))
        {
            if (File.Exists(_filePath)) return;
            WriteAll(coordinator, [context]);
        }
    }

    public async Task<IReadOnlyList<RepresentedFiscalContextRecord>> ListAsync(CancellationToken cancellationToken = default)
        => await ReadAllAsync(cancellationToken);

    public async Task<RepresentedFiscalContextRecord?> GetAsync(string contextId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contextId)) return null;
        var all = await ReadAllAsync(cancellationToken);
        return all.SingleOrDefault(x => string.Equals(x.ContextId, contextId.Trim(), StringComparison.Ordinal));
    }

    public async Task AddContextAsync(RepresentedFiscalContextRecord context, CancellationToken cancellationToken = default)
    {
        ValidateContext(context);
        var coordinator = new dcWsaaFileCacheCoordinator(_filePath);
        await using (await coordinator.AcquireAsync(cancellationToken))
        {
            var all = await ReadAllAsync(cancellationToken);
            if (all.Any(x => string.Equals(x.ContextId, context.ContextId, StringComparison.Ordinal)))
                throw new InvalidOperationException("FISCAL_CONTEXT_ALREADY_EXISTS");
            all.Add(context);
            WriteAll(coordinator, all);
        }
    }

    public async Task AddCandidateAssignmentAsync(
        string contextId,
        string revision,
        string credentialId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(revision) || string.IsNullOrWhiteSpace(credentialId) || string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("revision, credentialId y actor son obligatorios.");

        await UpdateContextAsync(contextId, context =>
        {
            if (context.Assignments.Any(x => string.Equals(x.AssignmentRevision, revision.Trim(), StringComparison.Ordinal)))
                throw new InvalidOperationException("ASSIGNMENT_REVISION_ALREADY_EXISTS");
            var assignments = context.Assignments.ToList();
            assignments.Add(new CredentialAssignmentRecord(
                revision.Trim(),
                credentialId.Trim(),
                CredentialAssignmentStatus.Candidate,
                null,
                DateTimeOffset.UtcNow,
                Actor: actor.Trim()));
            return context with { Assignments = assignments };
        }, cancellationToken);
    }

    public Task MarkAssignmentValidatedAsync(
        string contextId,
        string revision,
        string evidence,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(evidence))
            throw new ArgumentException("La evidencia validada no puede estar vacía.", nameof(evidence));
        return UpdateContextAsync(contextId, context =>
        {
            var assignments = context.Assignments.ToList();
            var index = assignments.FindIndex(x => string.Equals(x.AssignmentRevision, revision, StringComparison.Ordinal));
            if (index < 0) throw new InvalidOperationException("ASSIGNMENT_NOT_FOUND");
            var existing = assignments[index];
            if (existing.Status is CredentialAssignmentStatus.Active or CredentialAssignmentStatus.Historical or CredentialAssignmentStatus.Disabled)
                throw new InvalidOperationException("ASSIGNMENT_VALIDATION_STATE_INVALID");
            assignments[index] = existing with
            {
                Status = CredentialAssignmentStatus.Validated,
                ValidatedAuthorizationEvidence = evidence.Trim(),
                ValidatedAt = DateTimeOffset.UtcNow,
                Actor = actor.Trim()
            };
            return context with { Assignments = assignments };
        }, cancellationToken);
    }

    public Task ActivateAssignmentAsync(
        string contextId,
        string revision,
        string actor,
        CancellationToken cancellationToken = default)
        => UpdateContextAsync(contextId, context =>
        {
            var assignments = context.Assignments.ToList();
            var index = assignments.FindIndex(x => string.Equals(x.AssignmentRevision, revision, StringComparison.Ordinal));
            if (index < 0) throw new InvalidOperationException("ASSIGNMENT_NOT_FOUND");
            var candidate = assignments[index];
            if (candidate.Status != CredentialAssignmentStatus.Validated
                || string.IsNullOrWhiteSpace(candidate.ValidatedAuthorizationEvidence))
                throw new InvalidOperationException("ASSIGNMENT_NOT_VALIDATED");

            for (var i = 0; i < assignments.Count; i++)
            {
                if (assignments[i].Status == CredentialAssignmentStatus.Active)
                    assignments[i] = assignments[i] with { Status = CredentialAssignmentStatus.Historical };
            }
            assignments[index] = candidate with
            {
                Status = CredentialAssignmentStatus.Active,
                ActivatedAt = DateTimeOffset.UtcNow,
                Actor = actor.Trim()
            };
            return context with { Assignments = assignments };
        }, cancellationToken);

    public Task SetOperationalStateAsync(
        string contextId,
        FiscalContextOperationalState state,
        string actor,
        CancellationToken cancellationToken = default)
        => UpdateContextAsync(contextId, context => context with { OperationalState = state }, cancellationToken);

    private async Task UpdateContextAsync(
        string contextId,
        Func<RepresentedFiscalContextRecord, RepresentedFiscalContextRecord> update,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contextId)) throw new ArgumentException("contextId es obligatorio.");
        var coordinator = new dcWsaaFileCacheCoordinator(_filePath);
        await using (await coordinator.AcquireAsync(cancellationToken))
        {
            var all = await ReadAllAsync(cancellationToken);
            var index = all.FindIndex(x => string.Equals(x.ContextId, contextId.Trim(), StringComparison.Ordinal));
            if (index < 0) throw new InvalidOperationException("FISCAL_CONTEXT_NOT_FOUND");
            var original = all[index];
            var updated = update(original);
            if (!string.Equals(original.ContextId, updated.ContextId, StringComparison.Ordinal)
                || !string.Equals(original.Environment, updated.Environment, StringComparison.Ordinal)
                || original.RepresentedCuit != updated.RepresentedCuit
                || original.PointOfSale != updated.PointOfSale
                || original.ContextRevision != updated.ContextRevision)
                throw new InvalidOperationException("FISCAL_CONTEXT_IMMUTABLE_IDENTITY_CHANGED");
            ValidateContext(updated);
            all[index] = updated;
            WriteAll(coordinator, all);
        }
    }

    private async Task<List<RepresentedFiscalContextRecord>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath)) return [];
        try
        {
            await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var records = await JsonSerializer.DeserializeAsync<List<RepresentedFiscalContextRecord>>(stream, cancellationToken: cancellationToken) ?? [];
            foreach (var record in records) ValidateContext(record);
            if (records.Select(x => x.ContextId).Distinct(StringComparer.Ordinal).Count() != records.Count)
                throw new InvalidDataException("FISCAL_CONTEXT_DUPLICATE_ID");
            return records;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("El store de contextos fiscales contiene JSON inválido.", exception);
        }
    }

    private void WriteAll(dcWsaaFileCacheCoordinator coordinator, List<RepresentedFiscalContextRecord> records)
    {
        coordinator.WriteAllTextAtomic(JsonSerializer.Serialize(records));
        TryHardenFile(_filePath);
    }

    private static void ValidateContext(RepresentedFiscalContextRecord context)
    {
        if (context is null
            || string.IsNullOrWhiteSpace(context.ContextId)
            || string.IsNullOrWhiteSpace(context.Environment)
            || context.RepresentedCuit <= 0
            || context.PointOfSale <= 0
            || context.ContextRevision <= 0
            || context.Assignments is null)
            throw new InvalidDataException("FISCAL_CONTEXT_INVALID");

        if (context.Assignments.Count(x => x.Status == CredentialAssignmentStatus.Active) > 1)
            throw new InvalidDataException("FISCAL_CONTEXT_MULTIPLE_ACTIVE_ASSIGNMENTS");
        if (context.Assignments.Select(x => x.AssignmentRevision).Distinct(StringComparer.Ordinal).Count() != context.Assignments.Count)
            throw new InvalidDataException("FISCAL_CONTEXT_DUPLICATE_ASSIGNMENT_REVISION");
        if (context.Assignments.Any(x => string.IsNullOrWhiteSpace(x.AssignmentRevision) || string.IsNullOrWhiteSpace(x.CredentialId)))
            throw new InvalidDataException("FISCAL_CONTEXT_ASSIGNMENT_INVALID");
    }

    private static void TryHardenDirectory(string directory)
    {
        if (OperatingSystem.IsWindows()) return;
        try { File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        catch (Exception) when (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) { }
    }

    private static void TryHardenFile(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch (Exception) when (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) { }
    }
}
