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
            catalog,
            new FiscalRepresentationLifecyclePolicy(catalog),
            new NoRemoteMaterializer(),
            new NoRemoteClientFactory());

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
            catalog,
            new FiscalRepresentationLifecyclePolicy(catalog),
            new NoRemoteMaterializer(),
            new NoRemoteClientFactory());

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
            catalog,
            new FiscalRepresentationLifecyclePolicy(catalog),
            new ProbeMaterializer(cacheKey, tokenStore),
            new ErrorWsfeClientFactory(wsfeCode));

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

    [Theory]
    [InlineData("not-authorized", "POINT_OF_SALE_NOT_AUTHORIZED")]
    [InlineData("blocked", "POINT_OF_SALE_BLOCKED")]
    [InlineData("disabled", "POINT_OF_SALE_DISABLED")]
    [InlineData("not-cae", "POINT_OF_SALE_NOT_CAE")]
    public async Task DenegacionDeterministicaSobreActive_PersisteActionRequired(
        string scenario,
        string expectedCode)
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await ProvisionRepresentationAsync(catalog, activate: true);
        var administration = await ProbeAdministrationAsync(
            catalog,
            new ListingWsfeClientFactory(scenario));

        var exception = await Assert.ThrowsAsync<FiscalContextAccessException>(() =>
            administration.VerifyAndActivateAsync(
                Principal("admin", "arca:administrar", true),
                "operator-homo", "consumer-a", 20123456786, 7, default));

        Assert.Equal(expectedCode, exception.Code);
        var record = await catalog.GetRepresentationAsync(
            "operator-homo", "consumer-a", 20123456786, 7);
        Assert.NotNull(record);
        Assert.Equal(FiscalRepresentationStatus.ActionRequired, record!.Status);
        Assert.Equal(expectedCode, record.ActionRequiredReasonCode);
        Assert.False(record.CanReadOrEmit);
    }

    [Fact]
    public async Task FalloNoConcluyenteSobreActive_NoDegradaLifecycle()
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await ProvisionRepresentationAsync(catalog, activate: true);
        var administration = await ProbeAdministrationAsync(
            catalog,
            new RawWsfeClientFactory("<not-valid-xml"));

        var exception = await Assert.ThrowsAsync<FiscalContextAccessException>(() =>
            administration.VerifyAndActivateAsync(
                Principal("admin", "arca:administrar", true),
                "operator-homo", "consumer-a", 20123456786, 7, default));

        Assert.Equal("WSFE_RESPONSE_INVALID", exception.Code);
        var record = await catalog.GetRepresentationAsync(
            "operator-homo", "consumer-a", 20123456786, 7);
        Assert.NotNull(record);
        Assert.Equal(FiscalRepresentationStatus.Active, record!.Status);
        Assert.True(record.CanReadOrEmit);
        Assert.Null(record.ActionRequiredReasonCode);
    }

    [Fact]
    public async Task Manual602SinResultados_SigueActivandoConEvidenciaExplicita()
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await ProvisionRepresentationAsync(catalog, activate: false);
        var administration = await ProbeAdministrationAsync(
            catalog,
            new RawWsfeClientFactory(
                "<FEParamGetPtosVentaResult><Errors><Err><Code>602</Code><Msg>Sin Resultados: - Metodo FEParamGetPtosVenta</Msg></Err></Errors></FEParamGetPtosVentaResult>"));

        var record = await administration.VerifyAndActivateAsync(
            Principal("admin", "arca:administrar", true),
            "operator-homo", "consumer-a", 20123456786, 7, default);

        Assert.Equal(FiscalRepresentationStatus.Active, record.Status);
        Assert.StartsWith(
            "MANUAL_PV_UNVERIFIED_602|context=operator-homo|env=homologacion|cuit=20123456786|pv=7|rev=rev-1|checked=",
            record.VerificationEvidence,
            StringComparison.Ordinal);
        Assert.Null(record.VerifiedAt);
        Assert.Null(record.VerifiedBy);
    }

    [Fact]
    public async Task RotacionDuranteProbe_NoPuedeActivarConEvidenciaDeAssignmentAnterior()
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await ProvisionRepresentationAsync(catalog, activate: false);
        var administration = await ProbeAdministrationAsync(
            catalog,
            new RotatingWsfeClientFactory(catalog));

        var exception = await Assert.ThrowsAsync<FiscalContextAccessException>(() =>
            administration.VerifyAndActivateAsync(
                Principal("admin", "arca:administrar", true),
                "operator-homo", "consumer-a", 20123456786, 7, default));

        Assert.Equal("POINT_OF_SALE_PROBE_IDENTITY_MISMATCH", exception.Code);
        var record = await catalog.GetRepresentationAsync(
            "operator-homo", "consumer-a", 20123456786, 7);
        Assert.NotNull(record);
        Assert.Equal(FiscalRepresentationStatus.Pending, record!.Status);
        Assert.Null(record.VerificationEvidence);

        var context = await catalog.GetContextAsync("operator-homo");
        Assert.Equal("rev-2", context?.ActiveAssignment?.AssignmentRevision);
    }

    private static async Task<FiscalRepresentationAdministration> ProbeAdministrationAsync(
        FileSystemFiscalTechnicalContextStore catalog,
        IHttpClientFactory clients)
    {
        var cacheKey = "arca-probe-test-" + Guid.NewGuid().ToString("N");
        var tokenStore = new MemoryWsaaTokenStore();
        await tokenStore.WriteAsync(
            cacheKey + "_wsfe",
            new WsaaTokenEntry(
                "test-token",
                "test-sign",
                DateTime.UtcNow.AddHours(1)));

        return new FiscalRepresentationAdministration(
            catalog,
            new FiscalRepresentationLifecyclePolicy(catalog),
            new ProbeMaterializer(cacheKey, tokenStore),
            clients);
    }

    private static async Task ProvisionRepresentationAsync(
        FileSystemFiscalTechnicalContextStore catalog,
        bool activate)
    {
        const string contextId = "operator-homo";
        const string consumerId = "consumer-a";
        const long cuit = 20123456786;
        const int pointOfSale = 7;

        await catalog.AddContextAsync(new FiscalTechnicalContextRecord(
            contextId,
            "homologacion",
            FiscalContextOperationalState.Disabled,
            1,
            []));
        await catalog.AddCandidateAssignmentAsync(
            contextId, "rev-1", "cred-1", "admin");
        await catalog.MarkAssignmentValidatedAsync(
            contextId, "rev-1", "wsaa-validated", "admin");
        await catalog.ActivateAssignmentAsync(
            contextId, "rev-1", "admin");
        await catalog.SetContextStateAsync(
            contextId, FiscalContextOperationalState.Active, "admin");
        await catalog.RegisterCandidateAsync(
            contextId, consumerId, cuit, "admin");
        await catalog.SelectPointOfSaleAsync(
            contextId, consumerId, cuit, pointOfSale, "admin");

        if (!activate)
            return;

        await catalog.MarkRepresentationVerifiedAsync(
            contextId,
            consumerId,
            cuit,
            pointOfSale,
            "FEParamGetPtosVenta|context=operator-homo|env=homologacion|cuit=20123456786|pv=7|rev=rev-1|checked=2026-10-11T12:00:00.0000000+00:00",
            "admin");
        await catalog.ActivateRepresentationAsync(
            contextId, consumerId, cuit, pointOfSale, "admin");
    }

    private sealed class ListingWsfeClientFactory(string scenario)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(new RawWsfeHandler(ListingXml(scenario)));

        private static string ListingXml(string scenario)
        {
            var point = scenario switch
            {
                "not-authorized" =>
                    "<PtoVenta><Nro>8</Nro><EmisionTipo>CAE</EmisionTipo><Bloqueado>N</Bloqueado><FchBaja></FchBaja></PtoVenta>",
                "blocked" =>
                    "<PtoVenta><Nro>7</Nro><EmisionTipo>CAE</EmisionTipo><Bloqueado>S</Bloqueado><FchBaja></FchBaja></PtoVenta>",
                "disabled" =>
                    "<PtoVenta><Nro>7</Nro><EmisionTipo>CAE</EmisionTipo><Bloqueado>N</Bloqueado><FchBaja>20261010</FchBaja></PtoVenta>",
                "not-cae" =>
                    "<PtoVenta><Nro>7</Nro><EmisionTipo>CAI</EmisionTipo><Bloqueado>N</Bloqueado><FchBaja></FchBaja></PtoVenta>",
                _ => throw new ArgumentOutOfRangeException(nameof(scenario))
            };

            return "<FEParamGetPtosVentaResult><ResultGet>" + point +
                "</ResultGet></FEParamGetPtosVentaResult>";
        }
    }

    private sealed class RawWsfeClientFactory(string xml) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(new RawWsfeHandler(xml));
    }

    private sealed class RawWsfeHandler(string xml) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(xml)
            });
    }

    private sealed class RotatingWsfeClientFactory(
        FileSystemFiscalTechnicalContextStore catalog) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(new RotatingWsfeHandler(catalog));
    }

    private sealed class RotatingWsfeHandler(
        FileSystemFiscalTechnicalContextStore catalog) : HttpMessageHandler
    {
        private int _rotated;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _rotated, 1) == 0)
            {
                await catalog.AddCandidateAssignmentAsync(
                    "operator-homo",
                    "rev-2",
                    "cred-2",
                    "rotation",
                    cancellationToken);
                await catalog.MarkAssignmentValidatedAsync(
                    "operator-homo",
                    "rev-2",
                    "wsaa-validated-2",
                    "rotation",
                    cancellationToken);
                await catalog.ActivateAssignmentAsync(
                    "operator-homo",
                    "rev-2",
                    "rotation",
                    cancellationToken);
            }

            const string xml =
                "<FEParamGetPtosVentaResult><ResultGet>" +
                "<PtoVenta><Nro>7</Nro><EmisionTipo>CAE</EmisionTipo><Bloqueado>N</Bloqueado><FchBaja></FchBaja></PtoVenta>" +
                "</ResultGet></FEParamGetPtosVentaResult>";
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(xml)
            };
        }
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
