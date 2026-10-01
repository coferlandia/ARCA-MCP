namespace dcArca.Core.Services;

public sealed record ApiKeyRecord(
    string Id,
    string Name,
    string KeyHash,
    string[] Scopes,
    bool Active,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt = null,
    DateTimeOffset? LastUsedAt = null);

public interface IApiKeyStore
{
    Task<(ApiKeyRecord Record, string RawKey)> CreateAsync(
        string name, IReadOnlyCollection<string> scopes, CancellationToken cancellationToken = default);

    Task<ApiKeyRecord?> ValidateAsync(string rawKey, CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApiKeyRecord>> ListAsync(CancellationToken cancellationToken = default);
}
