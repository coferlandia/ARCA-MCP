using System.Security.Claims;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.McpServer.Tests;

public sealed class FiscalRepresentationAdministrationTests
{
    [Fact]
    public async Task ScopeYGrantAdministrativosSonIndependientesDelScopeDeEmision()
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await catalog.AddContextAsync(new FiscalTechnicalContextRecord(
            "operator-homo", "homologacion", FiscalContextOperationalState.Disabled, 1, []));
        var administration = new FiscalRepresentationAdministration(
            catalog, new NoRemoteMaterializer(), new NoRemoteClientFactory());

        var keyWithOnlyFiscalScope = Principal("admin", "arca:facturar", true);
        var missingAdminScope = await Assert.ThrowsAsync<FiscalContextAccessException>(() =>
            administration.RegisterCandidateAsync(keyWithOnlyFiscalScope, "operator-homo",
                "consumer-a", 20123456786, default));
        Assert.Equal("FISCAL_ADMIN_FORBIDDEN", missingAdminScope.Code);

        var missingAdminGrant = await Assert.ThrowsAsync<FiscalContextAccessException>(() =>
            administration.RegisterCandidateAsync(Principal("admin", "arca:administrar", false), "operator-homo",
                "consumer-a", 20123456786, default));
        Assert.Equal("FISCAL_ADMIN_FORBIDDEN", missingAdminGrant.Code);

        var registered = await administration.RegisterCandidateAsync(
            Principal("admin", "arca:administrar", true), "operator-homo",
            "consumer-a", 20123456786, default);
        Assert.Equal(FiscalRepresentationStatus.Pending, registered.Status);
        Assert.Equal("consumer-a", registered.ConsumerId);
        Assert.Null(await catalog.GetRepresentationAsync("operator-homo", "consumer-b", 20123456786, 0));
    }

    [Fact]
    public async Task DescubrimientoRequiereCandidatoDelConsumidorAntesDeUsarLaCredencial()
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await catalog.AddContextAsync(new FiscalTechnicalContextRecord(
            "operator-homo", "homologacion", FiscalContextOperationalState.Disabled, 1, []));
        await catalog.RegisterCandidateAsync("operator-homo", "consumer-a", 20123456786, "admin");
        var administration = new FiscalRepresentationAdministration(
            catalog, new NoRemoteMaterializer(), new NoRemoteClientFactory());

        var forbidden = await Assert.ThrowsAsync<FiscalContextAccessException>(() =>
            administration.ListPointsOfSaleAsync(Principal("admin", "arca:administrar", true),
                "operator-homo", "consumer-b", 20123456786, default));
        Assert.Equal("FISCAL_REPRESENTATION_CANDIDATE_REQUIRED", forbidden.Code);
    }

    private static ClaimsPrincipal Principal(string consumer, string scope, bool grant)
    {
        var claims = new List<Claim>
        {
            new(ArcaClaimTypes.ConsumerId, consumer),
            new("scope", scope)
        };
        if (grant)
            claims.Add(new Claim(ArcaClaimTypes.ContextGrant,
                ArcaClaimTypes.GrantValue("operator-homo", "administrar")));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private sealed class NoRemoteMaterializer : IFiscalCredentialMaterializer
    {
        public FiscalCredentialMaterialization Materialize(
            RepresentedFiscalContextRecord context, CredentialAssignmentRecord assignment)
            => throw new InvalidOperationException("No debe acceder a credenciales en este test.");
    }

    private sealed class NoRemoteClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => throw new InvalidOperationException("No debe acceder a red en este test.");
    }

    private sealed class TempCatalog : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "arca-admin-tests-" + Guid.NewGuid().ToString("N"));

        public TempCatalog() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
