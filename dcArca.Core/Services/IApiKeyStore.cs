namespace dcArca.Core.Services;

public sealed record ApiKeyContextGrant(
    string ContextId,
    string[] Operations);

public sealed record ApiKeyRecord(
    string Id,
    string Name,
    string KeyHash,
    string[] Scopes,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt = null,
    DateTimeOffset? LastUsedAt = null,
    string? ConsumerId = null,
    ApiKeyContextGrant[]? ContextGrants = null)
{
    public IReadOnlyList<ApiKeyContextGrant> Grants => ContextGrants ?? Array.Empty<ApiKeyContextGrant>();
}

public interface IApiKeyStore
{
    Task<(ApiKeyRecord Record, string RawKey)> CreateAsync(
        string name, IReadOnlyCollection<string> scopes, CancellationToken cancellationToken = default);

    Task<(ApiKeyRecord Record, string RawKey)> CreateForConsumerAsync(
        string name,
        string consumerId,
        IReadOnlyCollection<string> scopes,
        IReadOnlyCollection<ApiKeyContextGrant> contextGrants,
        CancellationToken cancellationToken = default);

    Task<ApiKeyRecord?> ValidateAsync(string rawKey, CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> SetConsumerAndGrantsAsync(
        string id,
        string consumerId,
        IReadOnlyCollection<ApiKeyContextGrant> contextGrants,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApiKeyRecord>> ListAsync(CancellationToken cancellationToken = default);
}
