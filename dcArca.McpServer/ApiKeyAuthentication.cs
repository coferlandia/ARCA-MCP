using System.Security.Claims;
using System.Text.Encodings.Web;
using dcArca.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace dcArca.McpServer;

public sealed class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    private readonly IApiKeyStore _store;
    private readonly SingleFiscalContextOptions _legacyContext;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyStore store,
        SingleFiscalContextOptions legacyContext) : base(options, logger, encoder)
    {
        _store = store;
        _legacyContext = legacyContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var values))
            return AuthenticateResult.NoResult();

        var parts = values.ToString().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !parts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.Fail("Credenciales inválidas.");

        ApiKeyRecord? record;
        try
        {
            record = await _store.ValidateAsync(parts[1].Trim(), Context.RequestAborted);
        }
        catch (InvalidDataException exception)
        {
            Logger.LogError(exception, "El store de API keys está corrupto.");
            return AuthenticateResult.Fail("Credenciales inválidas.");
        }

        if (record is null) return AuthenticateResult.Fail("Credenciales inválidas.");

        var isLegacy = string.IsNullOrWhiteSpace(record.ConsumerId);
        var consumerId = isLegacy ? _legacyContext.ConsumerId : record.ConsumerId!.Trim();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, record.Id),
            new(ClaimTypes.Name, record.Name),
            new(ArcaClaimTypes.ConsumerId, consumerId)
        };
        claims.AddRange(record.Scopes.Select(scope => new Claim("scope", scope)));
        foreach (var grant in record.Grants)
        {
            foreach (var operation in grant.Operations)
                claims.Add(new Claim(ArcaClaimTypes.ContextGrant, ArcaClaimTypes.GrantValue(grant.ContextId, operation)));
        }
        if (isLegacy)
            claims.Add(new Claim(ArcaClaimTypes.LegacyKey, "true"));

        var identity = new ClaimsIdentity(claims, SchemeName);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
