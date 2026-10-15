using System.Net;
using System.Reflection;
using System.Security.Claims;
using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace dcArca.McpServer.Tests;

public sealed class FiscalPadronAdministrationTests
{
    private const string ContextId = "operator-homo";
    private const string PrincipalCuit = "20123456789";
    private const long TargetCuit = 23333444559;

    [Fact]
    public async Task AdministrativePadron_DoesNotRequireRepresentationOrPointOfSale()
    {
        using var temp = new TempCatalog();
        var catalog = await ActiveCatalogAsync(temp.Path);

        var tokenStore = new MemoryWsaaTokenStore();
        await tokenStore.WriteAsync(
            "admin-padron-cache_ws_sr_constancia_inscripcion",
            new WsaaTokenEntry("test-token", "test-sign", DateTime.UtcNow.AddHours(1)));
        var config = PadronConfig();
        var auth = new dcArcaAuthService(
            config.WsaaUrl,
            config.CertificatePath,
            config.CertificatePassword,
            "admin-padron-cache",
            serviceName: "ws_sr_constancia_inscripcion",
            tokenStore: tokenStore);
        var materializer = DispatchProxy.Create<IFiscalCredentialMaterializer, MaterializerProxy>();
        var proxy = (MaterializerProxy)(object)materializer;
        proxy.Config = config;
        proxy.PadronAuth = auth;
        var handler = new CapturingHandler(SuccessResponse());
        var factory = new SingleClientFactory(handler);

        var serviceType = typeof(FiscalRepresentationAdministration).Assembly
            .GetType("dcArca.McpServer.FiscalPadronAdministration");
        Assert.NotNull(serviceType);
        var service = Activator.CreateInstance(serviceType!, catalog, materializer, factory);
        Assert.NotNull(service);
        var method = serviceType!.GetMethod("GetPersonaAsync");
        Assert.NotNull(method);

        var task = (Task<dcPadronPersonaResult>)method!.Invoke(
            service,
            [Administrator(includeGrant: true), ContextId, TargetCuit, CancellationToken.None])!;
        var result = await task;

        Assert.True(result.Success);
        Assert.Empty(await catalog.ListRepresentationsAsync(ContextId));
        Assert.NotNull(handler.RequestBody);
        Assert.Contains($"<cuitRepresentada>{PrincipalCuit}</cuitRepresentada>", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains($"<idPersona>{TargetCuit}</idPersona>", handler.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdministrativePadron_RequiresAdminGrantForTechnicalContext()
    {
        using var temp = new TempCatalog();
        var catalog = await ActiveCatalogAsync(temp.Path);
        var materializer = DispatchProxy.Create<IFiscalCredentialMaterializer, MaterializerProxy>();
        var factory = new SingleClientFactory(new CapturingHandler(SuccessResponse()));

        var serviceType = typeof(FiscalRepresentationAdministration).Assembly
            .GetType("dcArca.McpServer.FiscalPadronAdministration");
        Assert.NotNull(serviceType);
        var service = Activator.CreateInstance(serviceType!, catalog, materializer, factory);
        var method = serviceType!.GetMethod("GetPersonaAsync");
        Assert.NotNull(method);

        var exception = await Assert.ThrowsAsync<FiscalContextAccessException>(async () =>
        {
            var task = (Task<dcPadronPersonaResult>)method!.Invoke(
                service,
                [Administrator(includeGrant: false), ContextId, TargetCuit, CancellationToken.None])!;
            await task;
        });

        Assert.Equal("FISCAL_ADMIN_FORBIDDEN", exception.Code);
    }

    [Fact]
    public void MaterializePadron_UsesTechnicalCredentialWithoutRepresentedIdentity()
    {
        var configuration = BuildConfiguration();
        var materializer = new FiscalCredentialMaterializer(
            configuration,
            new dcArcaConfig(),
            new SingleFiscalContextOptions("legacy", "legacy", "homologacion", 1, "legacy"),
            new SilentLogger());
        var assignment = Assignment();
        var technical = new FiscalTechnicalContextRecord(
            ContextId,
            "homologacion",
            FiscalContextOperationalState.Active,
            1,
            [assignment]);

        var method = typeof(FiscalCredentialMaterializer).GetMethod(
            "MaterializePadron",
            [typeof(FiscalTechnicalContextRecord), typeof(CredentialAssignmentRecord)]);
        Assert.NotNull(method);

        var materialized = method!.Invoke(materializer, [technical, assignment]);
        Assert.NotNull(materialized);
        var config = (dcArcaConfig)materialized!.GetType().GetProperty("Config")!.GetValue(materialized)!;
        var auth = (dcArcaAuthService)materialized.GetType().GetProperty("PadronAuth")!.GetValue(materialized)!;

        Assert.Equal(PrincipalCuit, config.PrincipalCuit);
        Assert.Equal(string.Empty, config.RepresentedCuit);
        Assert.Equal(0, config.PuntoVenta);

        var cacheKey = typeof(dcArcaAuthService)
            .GetField("_cacheKey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(auth);
        Assert.Equal("homologacion:cred-root_ws_sr_constancia_inscripcion", cacheKey);
    }

    private static async Task<FileSystemFiscalTechnicalContextStore> ActiveCatalogAsync(string path)
    {
        var catalog = new FileSystemFiscalTechnicalContextStore(path);
        await catalog.AddContextAsync(new FiscalTechnicalContextRecord(
            ContextId,
            "homologacion",
            FiscalContextOperationalState.Active,
            1,
            [Assignment()]));
        return catalog;
    }

    private static CredentialAssignmentRecord Assignment()
        => new(
            "rev-1",
            "cred-root",
            CredentialAssignmentStatus.Active,
            "wsaa-ok",
            DateTimeOffset.UtcNow,
            ActivatedAt: DateTimeOffset.UtcNow,
            Actor: "test");

    private static IConfiguration BuildConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FiscalCredentials:cred-root:CertificatePath"] = "/tmp/test-placeholder.pfx",
                ["FiscalCredentials:cred-root:CertificatePassword"] = "",
                ["FiscalCredentials:cred-root:PrincipalCuit"] = PrincipalCuit,
                ["FiscalEnvironments:homologacion:WsaaUrl"] = "https://example.invalid/wsaa",
                ["FiscalEnvironments:homologacion:WsfeUrl"] = "https://example.invalid/wsfe",
                ["FiscalEnvironments:homologacion:PadronUrl"] = "https://example.invalid/padron"
            })
            .Build();

    private static dcArcaConfig PadronConfig()
        => new()
        {
            Environment = "homologacion",
            PrincipalCuit = PrincipalCuit,
            CertificatePath = "/tmp/test-placeholder.pfx",
            CertificatePassword = "",
            WsaaUrl = "https://example.invalid/wsaa",
            WsfeUrl = "https://example.invalid/wsfe",
            PadronUrl = "https://example.invalid/padron"
        };

    private static ClaimsPrincipal Administrator(bool includeGrant)
    {
        var claims = new List<Claim>
        {
            new(ArcaClaimTypes.ConsumerId, "admin"),
            new("scope", "arca:administrar")
        };
        if (includeGrant)
            claims.Add(new Claim(
                ArcaClaimTypes.ContextGrant,
                ArcaClaimTypes.GrantValue(ContextId, "administrar")));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static string SuccessResponse()
        => """
           <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
             <soapenv:Body>
               <getPersona_v2Response>
                 <personaReturn>
                   <datosGenerales>
                     <razonSocial>Persona de prueba</razonSocial>
                   </datosGenerales>
                 </personaReturn>
               </getPersona_v2Response>
             </soapenv:Body>
           </soapenv:Envelope>
           """;

    private class MaterializerProxy : DispatchProxy
    {
        public dcArcaConfig Config { get; set; } = PadronConfig();
        public dcArcaAuthService? PadronAuth { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "MaterializePadron")
            {
                if (PadronAuth is null)
                    throw new InvalidOperationException("PadronAuth no configurado.");
                return Activator.CreateInstance(targetMethod.ReturnType, Config, PadronAuth);
            }

            throw new InvalidOperationException($"No se esperaba invocar {targetMethod?.Name}.");
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(handler, disposeHandler: false);
    }

    private sealed class CapturingHandler(string responseXml) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseXml)
            };
        }
    }

    private sealed class SilentLogger : IAfipLogger
    {
        public void LogTrace(string message) { }
        public void LogDebug(string message) { }
        public void LogInformation(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message, Exception? exception = null) { }
        public void LogCritical(string message, Exception? exception = null) { }
    }

    private sealed class TempCatalog : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "dcarca-padron-admin-tests-" + Guid.NewGuid().ToString("N"));

        public TempCatalog() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
