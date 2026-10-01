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

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyStore store) : base(options, logger, encoder) => _store = store;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var values))
            return AuthenticateResult.NoResult();

        var parts = values.ToString().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !parts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.Fail("Credenciales inválidas.");

        var record = await _store.ValidateAsync(parts[1].Trim(), Context.RequestAborted);
        if (record is null) return AuthenticateResult.Fail("Credenciales inválidas.");

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, record.Id), new(ClaimTypes.Name, record.Name) };
        claims.AddRange(record.Scopes.Select(scope => new Claim("scope", scope)));
        var identity = new ClaimsIdentity(claims, SchemeName);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
