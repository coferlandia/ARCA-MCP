using System.Text.Json;

namespace dcArca.Core.Services;

/// <summary>
/// V2: one technical operator context may authorize many independent represented fiscal identities.
/// No represented CUIT or point of sale is part of the technical context identity.
/// </summary>
public sealed record FiscalTechnicalContextRecord(
    string ContextId,
    string Environment,
    FiscalContextOperationalState OperationalState,
    int ContextRevision,
    IReadOnlyList<CredentialAssignmentRecord> Assignments)
{
    public CredentialAssignmentRecord? ActiveAssignment =>
        Assignments.SingleOrDefault(x => x.Status == CredentialAssignmentStatus.Active);
}

public enum FiscalRepresentationStatus
{
    Pending,
    Verified,
    Active,
    Revoked,
    ActionRequired
}

/// <summary>
/// Consumer ownership is explicit. An authorization for consumer A never grants access to B.
/// PointOfSale=0 is allowed only during candidate registration, before selecting a PV.
/// </summary>
public sealed record FiscalRepresentationRecord(
    string ContextId,
    string ConsumerId,
    long RepresentedCuit,
    int PointOfSale,
    FiscalRepresentationStatus Status,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    string? VerificationEvidence = null,
    DateTimeOffset? VerifiedAt = null,
    string? VerifiedBy = null,
    DateTimeOffset? ActivatedAt = null,
    string? ActivatedBy = null,
    DateTimeOffset? RevokedAt = null,
    string? RevokedBy = null,
    DateTimeOffset? SelectedAt = null,
    string? SelectedBy = null)
{
    public bool CanReadOrEmit => Status == FiscalRepresentationStatus.Active;
}

public interface IFiscalTechnicalContextStore
{
    Task<IReadOnlyList<FiscalTechnicalContextRecord>> ListContextsAsync(CancellationToken cancellationToken = default);
    Task<FiscalTechnicalContextRecord?> GetContextAsync(string contextId, CancellationToken cancellationToken = default);
    Task AddContextAsync(FiscalTechnicalContextRecord context, CancellationToken cancellationToken = default);
    Task AddCandidateAssignmentAsync(string contextId, string revision, string credentialId, string actor, CancellationToken cancellationToken = default);
    Task MarkAssignmentValidatedAsync(string contextId, string revision, string evidence, string actor, CancellationToken cancellationToken = default);
    Task ActivateAssignmentAsync(string contextId, string revision, string actor, CancellationToken cancellationToken = default);
    Task DisableAssignmentAsync(string contextId, string revision, string actor, CancellationToken cancellationToken = default);
    Task SetContextStateAsync(string contextId, FiscalContextOperationalState state, string actor, CancellationToken cancellationToken = default);
    Task RegisterCandidateAsync(string contextId, string consumerId, long representedCuit, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FiscalRepresentationRecord>> ListRepresentationsAsync(string contextId, CancellationToken cancellationToken = default);
    Task<FiscalRepresentationRecord?> GetRepresentationAsync(string contextId, string consumerId, long representedCuit, int pointOfSale, CancellationToken cancellationToken = default);
    Task SelectPointOfSaleAsync(string contextId, string consumerId, long representedCuit, int pointOfSale, string actor, CancellationToken cancellationToken = default);
    Task MarkRepresentationVerifiedAsync(string contextId, string consumerId, long representedCuit, int pointOfSale, string evidence, string actor, CancellationToken cancellationToken = default);
    Task ActivateRepresentationAsync(string contextId, string consumerId, long representedCuit, int pointOfSale, string actor, CancellationToken cancellationToken = default);
    Task RevokeRepresentationAsync(string contextId, string consumerId, long representedCuit, int pointOfSale, string actor, CancellationToken cancellationToken = default);
}

public sealed record FiscalTechnicalCatalog(
    int SchemaVersion,
    IReadOnlyList<FiscalTechnicalContextRecord> Contexts,
    IReadOnlyList<FiscalRepresentationRecord> Representations);

/// <summary>
/// Atomic, single-file catalog for technical contexts and per-consumer fiscal representations.
/// The V1 contexts.json file is never silently interpreted as V2.
/// </summary>
public sealed class FileSystemFiscalTechnicalContextStore : IFiscalTechnicalContextStore
{
    public const int SchemaVersion = 2;
    private readonly string _path;
    private readonly string _legacyPath;

    public FileSystemFiscalTechnicalContextStore(string? directory = null)
    {
        var dir = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dcArca", "fiscal-contexts")
            : Path.GetFullPath(directory);
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "fiscal-catalog-v2.json");
        _legacyPath = Path.Combine(dir, "contexts.json");
    }

    public async Task<IReadOnlyList<FiscalTechnicalContextRecord>> ListContextsAsync(CancellationToken cancellationToken = default)
        => (await ReadAsync(cancellationToken)).Contexts;

    public async Task<FiscalTechnicalContextRecord?> GetContextAsync(string contextId, CancellationToken cancellationToken = default)
        => (await ReadAsync(cancellationToken)).Contexts.SingleOrDefault(x => x.ContextId == contextId);

    public Task AddContextAsync(FiscalTechnicalContextRecord context, CancellationToken cancellationToken = default)
    {
        ValidateContext(context);
        return MutateAsync(cat =>
        {
            if (cat.Contexts.Any(x => x.ContextId == context.ContextId))
                throw new InvalidOperationException("FISCAL_CONTEXT_ALREADY_EXISTS");
            return cat with { Contexts = [.. cat.Contexts, context] };
        }, cancellationToken);
    }

    public Task AddCandidateAssignmentAsync(string contextId, string revision, string credentialId, string actor, CancellationToken cancellationToken = default)
    {
        Require(revision, nameof(revision));
        Require(credentialId, nameof(credentialId));
        Require(actor, nameof(actor));
        return UpdateContextAsync(contextId, ctx =>
        {
            if (ctx.Assignments.Any(x => x.AssignmentRevision == revision))
                throw new InvalidOperationException("ASSIGNMENT_REVISION_ALREADY_EXISTS");
            return ctx with
            {
                Assignments = [.. ctx.Assignments, new CredentialAssignmentRecord(
                    revision, credentialId, CredentialAssignmentStatus.Candidate, null, DateTimeOffset.UtcNow, Actor: actor)]
            };
        }, cancellationToken);
    }

    public Task MarkAssignmentValidatedAsync(string contextId, string revision, string evidence, string actor, CancellationToken cancellationToken = default)
    {
        Require(evidence, nameof(evidence));
        Require(actor, nameof(actor));
        return UpdateContextAsync(contextId, ctx => ChangeAssignment(ctx, revision, existing =>
        {
            if (existing.Status is not (CredentialAssignmentStatus.Candidate or CredentialAssignmentStatus.Validated))
                throw new InvalidOperationException("ASSIGNMENT_VALIDATION_STATE_INVALID");
            return existing with { Status = CredentialAssignmentStatus.Validated, ValidatedAuthorizationEvidence = evidence, ValidatedAt = DateTimeOffset.UtcNow, Actor = actor };
        }), cancellationToken);
    }

    public Task ActivateAssignmentAsync(string contextId, string revision, string actor, CancellationToken cancellationToken = default)
    {
        Require(actor, nameof(actor));
        return UpdateContextAsync(contextId, ctx =>
        {
            var selected = ctx.Assignments.SingleOrDefault(x => x.AssignmentRevision == revision)
                ?? throw new InvalidOperationException("ASSIGNMENT_NOT_FOUND");
            if (selected.Status != CredentialAssignmentStatus.Validated || string.IsNullOrWhiteSpace(selected.ValidatedAuthorizationEvidence))
                throw new InvalidOperationException("ASSIGNMENT_NOT_VALIDATED");
            return ctx with
            {
                Assignments = ctx.Assignments.Select(x =>
                    x.AssignmentRevision == revision
                        ? x with { Status = CredentialAssignmentStatus.Active, ActivatedAt = DateTimeOffset.UtcNow, Actor = actor }
                        : x.Status == CredentialAssignmentStatus.Active
                            ? x with { Status = CredentialAssignmentStatus.Historical }
                            : x).ToArray()
            };
        }, cancellationToken);
    }

    public Task DisableAssignmentAsync(string contextId, string revision, string actor, CancellationToken cancellationToken = default)
    {
        Require(actor, nameof(actor));
        return UpdateContextAsync(contextId, ctx => ChangeAssignment(ctx, revision,
            existing => existing with { Status = CredentialAssignmentStatus.Disabled, Actor = actor }), cancellationToken);
    }

    public Task SetContextStateAsync(string contextId, FiscalContextOperationalState state, string actor, CancellationToken cancellationToken = default)
    {
        Require(actor, nameof(actor));
        return UpdateContextAsync(contextId, ctx =>
        {
            if (state == FiscalContextOperationalState.Active && ctx.ActiveAssignment is null)
                throw new InvalidOperationException("ACTIVE_ASSIGNMENT_REQUIRED");
            return ctx with { OperationalState = state };
        }, cancellationToken);
    }

    public Task RegisterCandidateAsync(string contextId, string consumerId, long representedCuit, string actor, CancellationToken cancellationToken = default)
    {
        Require(consumerId, nameof(consumerId));
        Require(actor, nameof(actor));
        if (representedCuit <= 0) throw new ArgumentOutOfRangeException(nameof(representedCuit));
        return MutateAsync(cat =>
        {
            RequireContext(cat, contextId);
            if (cat.Representations.Any(x => x.ContextId == contextId && x.ConsumerId == consumerId &&
                x.RepresentedCuit == representedCuit && x.PointOfSale == 0 &&
                x.Status != FiscalRepresentationStatus.Revoked))
                throw new InvalidOperationException("FISCAL_REPRESENTATION_ALREADY_EXISTS");
            var rep = new FiscalRepresentationRecord(contextId, consumerId, representedCuit, 0,
                FiscalRepresentationStatus.Pending, DateTimeOffset.UtcNow, actor);
            return cat with { Representations = [.. cat.Representations, rep] };
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<FiscalRepresentationRecord>> ListRepresentationsAsync(string contextId, CancellationToken cancellationToken = default)
        => (await ReadAsync(cancellationToken)).Representations.Where(x => x.ContextId == contextId).ToArray();

    public async Task<FiscalRepresentationRecord?> GetRepresentationAsync(string contextId, string consumerId, long representedCuit, int pointOfSale, CancellationToken cancellationToken = default)
        => (await ReadAsync(cancellationToken)).Representations
            .Where(x => x.ContextId == contextId && x.ConsumerId == consumerId &&
                x.RepresentedCuit == representedCuit && x.PointOfSale == pointOfSale)
            .OrderBy(x => x.Status == FiscalRepresentationStatus.Revoked ? 1 : 0)
            .ThenByDescending(x => x.CreatedAt).FirstOrDefault();

    public Task SelectPointOfSaleAsync(string contextId, string consumerId, long representedCuit, int pointOfSale, string actor, CancellationToken cancellationToken = default)
    {
        if (pointOfSale <= 0) throw new ArgumentOutOfRangeException(nameof(pointOfSale));
        Require(actor, nameof(actor));
        return UpdateRepresentationAsync(contextId, consumerId, representedCuit, 0, rep =>
        {
            if (rep.Status != FiscalRepresentationStatus.Pending)
                throw new InvalidOperationException("FISCAL_REPRESENTATION_STATE_INVALID");
            return rep with { PointOfSale = pointOfSale, SelectedBy = actor, SelectedAt = DateTimeOffset.UtcNow };
        }, cancellationToken);
    }

    public Task MarkRepresentationVerifiedAsync(string contextId, string consumerId, long representedCuit, int pointOfSale, string evidence, string actor, CancellationToken cancellationToken = default)
    {
        Require(evidence, nameof(evidence));
        Require(actor, nameof(actor));
        return UpdateRepresentationAsync(contextId, consumerId, representedCuit, pointOfSale, rep =>
        {
            if (rep.Status is not (FiscalRepresentationStatus.Pending or FiscalRepresentationStatus.Verified or FiscalRepresentationStatus.Active))
                throw new InvalidOperationException("FISCAL_REPRESENTATION_STATE_INVALID");
            return rep with { Status = FiscalRepresentationStatus.Verified, VerificationEvidence = evidence, VerifiedAt = DateTimeOffset.UtcNow, VerifiedBy = actor };
        }, cancellationToken);
    }

    public Task ActivateRepresentationAsync(string contextId, string consumerId, long representedCuit, int pointOfSale, string actor, CancellationToken cancellationToken = default)
    {
        Require(actor, nameof(actor));
        return MutateAsync(cat =>
        {
            var context = RequireContext(cat, contextId);
            if (context.OperationalState != FiscalContextOperationalState.Active || context.ActiveAssignment is null)
                throw new InvalidOperationException("FISCAL_CONTEXT_NOT_ACTIVE");
            return ChangeRepresentation(cat, contextId, consumerId, representedCuit, pointOfSale, rep =>
            {
                if (rep.Status != FiscalRepresentationStatus.Verified || string.IsNullOrWhiteSpace(rep.VerificationEvidence))
                    throw new InvalidOperationException("FISCAL_REPRESENTATION_NOT_VERIFIED");
                return rep with { Status = FiscalRepresentationStatus.Active, ActivatedAt = DateTimeOffset.UtcNow, ActivatedBy = actor };
            });
        }, cancellationToken);
    }

    public Task RevokeRepresentationAsync(string contextId, string consumerId, long representedCuit, int pointOfSale, string actor, CancellationToken cancellationToken = default)
    {
        Require(actor, nameof(actor));
        return UpdateRepresentationAsync(contextId, consumerId, representedCuit, pointOfSale,
            rep => rep.Status == FiscalRepresentationStatus.Revoked
                ? throw new InvalidOperationException("FISCAL_REPRESENTATION_ALREADY_REVOKED")
                : rep with { Status = FiscalRepresentationStatus.Revoked, RevokedAt = DateTimeOffset.UtcNow, RevokedBy = actor }, cancellationToken);
    }

    private Task UpdateContextAsync(string id, Func<FiscalTechnicalContextRecord, FiscalTechnicalContextRecord> action, CancellationToken ct)
        => MutateAsync(cat =>
        {
            var original = RequireContext(cat, id);
            var updated = action(original);
            if (updated.ContextId != original.ContextId || updated.Environment != original.Environment || updated.ContextRevision != original.ContextRevision)
                throw new InvalidOperationException("FISCAL_CONTEXT_IMMUTABLE_IDENTITY_CHANGED");
            ValidateContext(updated);
            return cat with { Contexts = cat.Contexts.Select(x => x.ContextId == id ? updated : x).ToArray() };
        }, ct);

    private Task UpdateRepresentationAsync(string contextId, string consumerId, long cuit, int pv,
        Func<FiscalRepresentationRecord, FiscalRepresentationRecord> action, CancellationToken ct)
        => MutateAsync(cat => ChangeRepresentation(cat, contextId, consumerId, cuit, pv, action), ct);

    private static FiscalTechnicalCatalog ChangeRepresentation(FiscalTechnicalCatalog cat, string contextId, string consumerId,
        long cuit, int pv, Func<FiscalRepresentationRecord, FiscalRepresentationRecord> action)
    {
        var original = cat.Representations
            .Where(x => x.ContextId == contextId && x.ConsumerId == consumerId &&
                x.RepresentedCuit == cuit && x.PointOfSale == pv)
            .OrderBy(x => x.Status == FiscalRepresentationStatus.Revoked ? 1 : 0)
            .ThenByDescending(x => x.CreatedAt).FirstOrDefault()
            ?? throw new InvalidOperationException("FISCAL_REPRESENTATION_NOT_FOUND");
        var updated = action(original);
        if (updated.ContextId != original.ContextId || updated.ConsumerId != original.ConsumerId ||
            updated.RepresentedCuit != original.RepresentedCuit)
            throw new InvalidOperationException("FISCAL_REPRESENTATION_IMMUTABLE_IDENTITY_CHANGED");
        if (cat.Representations.Any(x => !ReferenceEquals(x, original) &&
            x.ContextId == updated.ContextId && x.ConsumerId == updated.ConsumerId &&
            x.RepresentedCuit == updated.RepresentedCuit && x.PointOfSale == updated.PointOfSale &&
            x.Status != FiscalRepresentationStatus.Revoked && updated.Status != FiscalRepresentationStatus.Revoked))
            throw new InvalidOperationException("FISCAL_REPRESENTATION_ALREADY_EXISTS");
        return cat with { Representations = cat.Representations.Select(x => ReferenceEquals(x, original) ? updated : x).ToArray() };
    }

    private static FiscalTechnicalContextRecord RequireContext(FiscalTechnicalCatalog cat, string id)
        => cat.Contexts.SingleOrDefault(x => x.ContextId == id)
            ?? throw new InvalidOperationException("FISCAL_CONTEXT_NOT_FOUND");

    private static FiscalTechnicalContextRecord ChangeAssignment(FiscalTechnicalContextRecord ctx, string revision,
        Func<CredentialAssignmentRecord, CredentialAssignmentRecord> action)
    {
        var assignment = ctx.Assignments.SingleOrDefault(x => x.AssignmentRevision == revision)
            ?? throw new InvalidOperationException("ASSIGNMENT_NOT_FOUND");
        return ctx with { Assignments = ctx.Assignments.Select(x => x.AssignmentRevision == revision ? action(x) : x).ToArray() };
    }

    private async Task MutateAsync(Func<FiscalTechnicalCatalog, FiscalTechnicalCatalog> action, CancellationToken cancellationToken)
    {
        var lockCoordinator = new dcWsaaFileCacheCoordinator(_path);
        await using (await lockCoordinator.AcquireAsync(cancellationToken))
        {
            var current = await ReadAsync(cancellationToken);
            var updated = action(current);
            ValidateCatalog(updated);
            lockCoordinator.WriteAllTextAtomic(JsonSerializer.Serialize(updated));
        }
    }

    private async Task<FiscalTechnicalCatalog> ReadAsync(CancellationToken cancellationToken)
    {
        // An unreset V1 installation may never silently open an empty V2 authorization catalog.
        if (File.Exists(_legacyPath))
            throw new InvalidDataException("FISCAL_CATALOG_V1_RESET_REQUIRED");
        if (!File.Exists(_path))
            return new FiscalTechnicalCatalog(SchemaVersion, [], []);
        try
        {
            await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var catalog = await JsonSerializer.DeserializeAsync<FiscalTechnicalCatalog>(stream, cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("FISCAL_CATALOG_INVALID");
            ValidateCatalog(catalog);
            return catalog;
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("FISCAL_CATALOG_INVALID", e);
        }
    }

    private static void ValidateCatalog(FiscalTechnicalCatalog catalog)
    {
        if (catalog.SchemaVersion != SchemaVersion || catalog.Contexts is null || catalog.Representations is null)
            throw new InvalidDataException("FISCAL_CATALOG_SCHEMA_UNSUPPORTED");
        foreach (var context in catalog.Contexts) ValidateContext(context);
        if (catalog.Contexts.Select(x => x.ContextId).Distinct(StringComparer.Ordinal).Count() != catalog.Contexts.Count)
            throw new InvalidDataException("FISCAL_CONTEXT_DUPLICATE_ID");
        foreach (var rep in catalog.Representations)
        {
            if (rep is null || string.IsNullOrWhiteSpace(rep.ContextId) || string.IsNullOrWhiteSpace(rep.ConsumerId) ||
                rep.RepresentedCuit <= 0 || rep.PointOfSale < 0 || string.IsNullOrWhiteSpace(rep.CreatedBy) ||
                (rep.Status is FiscalRepresentationStatus.Verified or FiscalRepresentationStatus.Active && (rep.PointOfSale == 0 ||
                    string.IsNullOrWhiteSpace(rep.VerificationEvidence))) ||
                !catalog.Contexts.Any(x => x.ContextId == rep.ContextId))
                throw new InvalidDataException("FISCAL_REPRESENTATION_INVALID");
        }
        if (catalog.Representations.Where(x => x.Status != FiscalRepresentationStatus.Revoked)
            .GroupBy(x => (x.ContextId, x.ConsumerId, x.RepresentedCuit, x.PointOfSale))
            .Any(g => g.Count() > 1))
            throw new InvalidDataException("FISCAL_REPRESENTATION_DUPLICATE");
    }

    private static void ValidateContext(FiscalTechnicalContextRecord context)
    {
        if (context is null || string.IsNullOrWhiteSpace(context.ContextId) || string.IsNullOrWhiteSpace(context.Environment) ||
            context.ContextRevision <= 0 || context.Assignments is null ||
            context.Assignments.Count(x => x.Status == CredentialAssignmentStatus.Active) > 1 ||
            context.Assignments.Select(x => x.AssignmentRevision).Distinct(StringComparer.Ordinal).Count() != context.Assignments.Count ||
            context.Assignments.Any(x => string.IsNullOrWhiteSpace(x.AssignmentRevision) || string.IsNullOrWhiteSpace(x.CredentialId)) ||
            context.OperationalState == FiscalContextOperationalState.Active && context.ActiveAssignment is null)
            throw new InvalidDataException("FISCAL_CONTEXT_INVALID");
    }

    private static void Require(string text, string name)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException(name + " es obligatorio.", name);
    }
}
