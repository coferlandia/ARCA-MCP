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
    public async Task RegistroYSeleccionDelMismoTargetSonIdempotentes()
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await catalog.AddContextAsync(new FiscalTechnicalContextRecord(
            "operator-homo", "homologacion", FiscalContextOperationalState.Disabled, 1, []));
        var administration = new FiscalRepresentationAdministration(
            catalog, new NoRemoteMaterializer(), new NoRemoteClientFactory());
        var principal = Principal("admin", "arca:administrar", true);

        var first = await administration.RegisterCandidateAsync(
            principal, "operator-homo", "consumer-a", 20123456786, default);
        var repeated = await administration.RegisterCandidateAsync(
            principal, "operator-homo", "consumer-a", 20123456786, default);

        Assert.Equal(first.CreatedAt, repeated.CreatedAt);
        Assert.Single((await catalog.ListRepresentationsAsync("operator-homo"))
            .Where(x => x.ConsumerId == "consumer-a" && x.RepresentedCuit == 20123456786));

        var selected = await administration.SelectPointOfSaleAsync(
            principal, "operator-homo", "consumer-a", 20123456786, 7, default);
        var selectedAgain = await administration.SelectPointOfSaleAsync(
            principal, "operator-homo", "consumer-a", 20123456786, 7, default);
        var registerAfterSelection = await administration.RegisterCandidateAsync(
            principal, "operator-homo", "consumer-a", 20123456786, default);

        Assert.Equal(7, selected.PointOfSale);
        Assert.Equal(selected.CreatedAt, selectedAgain.CreatedAt);
        Assert.Equal(7, registerAfterSelection.PointOfSale);
        Assert.Single((await catalog.ListRepresentationsAsync("operator-homo"))
            .Where(x => x.ConsumerId == "consumer-a" && x.RepresentedCuit == 20123456786));
    }

    [Fact]
    public async Task RegistroYSeleccionConcurrentesConvergenEnUnaRepresentacion()
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await catalog.AddContextAsync(new FiscalTechnicalContextRecord(
            "operator-homo", "homologacion", FiscalContextOperationalState.Disabled, 1, []));
        var administration = new FiscalRepresentationAdministration(
            catalog, new NoRemoteMaterializer(), new NoRemoteClientFactory());
        var principal = Principal("admin", "arca:administrar", true);

        await Task.WhenAll(
            administration.RegisterCandidateAsync(
                principal, "operator-homo", "consumer-a", 20123456786, default),
            administration.RegisterCandidateAsync(
                principal, "operator-homo", "consumer-a", 20123456786, default));

        await Task.WhenAll(
            administration.SelectPointOfSaleAsync(
                principal, "operator-homo", "consumer-a", 20123456786, 7, default),
            administration.SelectPointOfSaleAsync(
                principal, "operator-homo", "consumer-a", 20123456786, 7, default));

        var records = (await catalog.ListRepresentationsAsync("operator-homo"))
            .Where(x => x.ConsumerId == "consumer-a" && x.RepresentedCuit == 20123456786)
            .ToArray();
        Assert.Single(records);
        Assert.Equal(7, records[0].PointOfSale);
        Assert.Equal(FiscalRepresentationStatus.Pending, records[0].Status);
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


    [Theory]
    [InlineData("600", false)]
    [InlineData("601", false)]
    [InlineData("600", true)]
    [InlineData("601", true)]
    public async Task RechazoWsfeEnVerificacion_PersisteActionRequiredAntesDePropagarError(
        string wsfeCode, bool alreadyActive)
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        const string contextId = "operator-homo";
        const string consumerId = "consumer-a";
        const long cuit = 20123456786;
        const int pointOfSale = 7;

        await catalog.AddContextAsync(new FiscalTechnicalContextRecord(
            contextId, "homologacion", FiscalContextOperationalState.Disabled, 1, []));
        await catalog.AddCandidateAssignmentAsync(contextId, "rev-1", "cred-1", "admin");
        await catalog.MarkAssignmentValidatedAsync(contextId, "rev-1", "wsaa-validated", "admin");
        await catalog.ActivateAssignmentAsync(contextId, "rev-1", "admin");
        await catalog.SetContextStateAsync(contextId, FiscalContextOperationalState.Active, "admin");
        await catalog.RegisterCandidateAsync(contextId, consumerId, cuit, "admin");
        await catalog.SelectPointOfSaleAsync(contextId, consumerId, cuit, pointOfSale, "admin");

        if (alreadyActive)
        {
            await catalog.MarkRepresentationVerifiedAsync(
                contextId, consumerId, cuit, pointOfSale,
                "FEParamGetPtosVenta|context=operator-homo|env=homologacion|cuit=20123456786|pv=7|rev=rev-1|checked=test",
                "admin");
            await catalog.ActivateRepresentationAsync(contextId, consumerId, cuit, pointOfSale, "admin");
        }

        var cacheKey = "arca-probe-test-" + Guid.NewGuid().ToString("N");
        var tokenStore = new MemoryWsaaTokenStore();
        await tokenStore.WriteAsync(cacheKey + "_wsfe",
            new WsaaTokenEntry("test-token", "test-sign", DateTime.UtcNow.AddHours(1)));
        var administration = new FiscalRepresentationAdministration(
            catalog, new ProbeMaterializer(cacheKey, tokenStore), new ErrorWsfeClientFactory(wsfeCode));

        var exception = await Assert.ThrowsAsync<FiscalContextAccessException>(() =>
            administration.VerifyAndActivateAsync(
                Principal("admin", "arca:administrar", true),
                contextId, consumerId, cuit, pointOfSale, default));

        Assert.Equal("WSFE_" + wsfeCode, exception.Code);
        var record = await catalog.GetRepresentationAsync(contextId, consumerId, cuit, pointOfSale);
        Assert.NotNull(record);
        Assert.Equal(FiscalRepresentationStatus.ActionRequired, record!.Status);
        Assert.Equal("WSFE_" + wsfeCode, record.ActionRequiredReasonCode);
        Assert.Equal("admin", record.ActionRequiredBy);
        Assert.NotNull(record.ActionRequiredAt);
    }

    private sealed class ProbeMaterializer(string cacheKey, IWsaaTokenStore tokenStore)
        : IFiscalCredentialMaterializer
    {
        public FiscalCredentialMaterialization Materialize(
            RepresentedFiscalContextRecord context, CredentialAssignmentRecord assignment)
        {
            var config = new dcArca.Core.Models.dcArcaConfig
            {
                Cuit = context.RepresentedCuit.ToString(),
                PuntoVenta = context.PointOfSale,
                CertificatePath = "unused.pfx",
                CertificatePassword = "",
                WsaaUrl = "https://example.invalid/wsaa",
                WsfeUrl = "https://example.invalid/wsfe",
                PadronUrl = "https://example.invalid/padron"
            };
            var binding = new FiscalCredentialHostBinding(
                assignment.CredentialId, cacheKey, config);
            return new FiscalCredentialMaterialization(
                config,
                new dcArcaAuthService(
                    config.WsaaUrl, config.CertificatePath, config.CertificatePassword,
                    cacheKey, tokenStore: tokenStore),
                new dcArcaAuthService(
                    config.WsaaUrl, config.CertificatePath, config.CertificatePassword,
                    cacheKey, "ws_sr_constancia_inscripcion", tokenStore: tokenStore),
                binding);
        }
    }

    private sealed class ErrorWsfeClientFactory(string code) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(new ErrorWsfeHandler(code));
    }

    private sealed class ErrorWsfeHandler(string code) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<FEParamGetPtosVentaResult><Errors><Err><Code>" + code +
                    "</Code><Msg>Remote authorization failure</Msg></Err></Errors></FEParamGetPtosVentaResult>")
            });
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
