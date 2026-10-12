using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using dcArca.Core.Models;
using dcArca.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace dcArca.McpServer.Tests;

public sealed class McpFiscalErrorBoundaryTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string ContextId = "operator-homo";
    private const string ConsumerId = "consumer-a";
    private const long RepresentedCuit = 20123456786;
    private const int PointOfSale = 7;

    private readonly WebApplicationFactory<Program> _baseFactory;
    private readonly TestCertificatePlaceholder _certificate = new();

    public McpFiscalErrorBoundaryTests(WebApplicationFactory<Program> baseFactory)
        => _baseFactory = baseFactory;

    [Fact]
    public async Task ActivarRepresentacionInexistente_DevuelveErrorFiscalEstructurado()
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        using var factory = CreateFactory(catalog);
        using var client = factory.CreateClient();

        var body = await CallAsync(client, "arca:administrar", "activar_representacion_fiscal", new
        {
            contextId = ContextId,
            consumerId = ConsumerId,
            representedCuit = RepresentedCuit,
            pointOfSale = PointOfSale
        }, grant: $"{ContextId}|administrar");

        AssertStructuredError(body, "FISCAL_REPRESENTATION_NOT_FOUND");
        Assert.DoesNotContain("An error occurred invoking", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegistrarEnContextoInexistente_NormalizaCodigoDelStore()
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        using var factory = CreateFactory(catalog);
        using var client = factory.CreateClient();

        var body = await CallAsync(client, "arca:administrar", "registrar_representacion_fiscal", new
        {
            contextId = ContextId,
            consumerId = ConsumerId,
            representedCuit = RepresentedCuit
        }, grant: $"{ContextId}|administrar");

        AssertStructuredError(body, "FISCAL_CONTEXT_NOT_FOUND");
    }

    [Fact]
    public async Task ContextoInactivo_ConservaCodigoYMensajeSeguro()
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await catalog.AddContextAsync(new FiscalTechnicalContextRecord(
            ContextId, "homologacion", FiscalContextOperationalState.Disabled, 1, []));
        await catalog.RegisterCandidateAsync(ContextId, ConsumerId, RepresentedCuit, "admin");
        await catalog.SelectPointOfSaleAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale, "admin");

        using var factory = CreateFactory(catalog);
        using var client = factory.CreateClient();

        var body = await CallAsync(client, "arca:administrar", "activar_representacion_fiscal", new
        {
            contextId = ContextId,
            consumerId = ConsumerId,
            representedCuit = RepresentedCuit,
            pointOfSale = PointOfSale
        }, grant: $"{ContextId}|administrar");

        var safeMessage = AssertStructuredError(body, "FISCAL_CONTEXT_NOT_ACTIVE");
        Assert.Equal("No existe credencial técnica activa.", safeMessage);
    }

    [Fact]
    public async Task AdministracionSinGrant_ConservaFiscalAdminForbidden()
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        using var factory = CreateFactory(catalog);
        using var client = factory.CreateClient();

        var body = await CallAsync(client, "arca:administrar", "activar_representacion_fiscal", new
        {
            contextId = ContextId,
            consumerId = ConsumerId,
            representedCuit = RepresentedCuit,
            pointOfSale = PointOfSale
        });

        AssertStructuredError(body, "FISCAL_ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task ToolFiscalOrdinaria_UsaLaMismaFronteraDeErrores()
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        using var factory = CreateFactory(catalog);
        using var client = factory.CreateClient();

        var body = await CallAsync(client, "arca:consultar", "obtener_capacidades_fiscales", new
        {
            contextId = ContextId
        }, grant: $"{ContextId}|consultar");

        AssertStructuredError(body, "FISCAL_CONTEXT_FORBIDDEN");
    }

    [Fact]
    public async Task RegistroDuplicado_ExponeCodigoEstableMientrasSeaErrorDeDominio()
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await catalog.AddContextAsync(new FiscalTechnicalContextRecord(
            ContextId, "homologacion", FiscalContextOperationalState.Disabled, 1, []));

        using var factory = CreateFactory(catalog);
        using var client = factory.CreateClient();

        var first = await CallAsync(client, "arca:administrar", "registrar_representacion_fiscal", new
        {
            contextId = ContextId,
            consumerId = ConsumerId,
            representedCuit = RepresentedCuit
        }, grant: $"{ContextId}|administrar");
        Assert.DoesNotContain("\"isError\":true", first, StringComparison.Ordinal);

        var duplicate = await CallAsync(client, "arca:administrar", "registrar_representacion_fiscal", new
        {
            contextId = ContextId,
            consumerId = ConsumerId,
            representedCuit = RepresentedCuit
        }, grant: $"{ContextId}|administrar");

        AssertStructuredError(duplicate, "FISCAL_REPRESENTATION_ALREADY_EXISTS");
    }

    [Theory]
    [InlineData("600")]
    [InlineData("601")]
    [InlineData("602")]
    public async Task ErrorWsfeSeguro_ConservaCodigoEnFronteraMcp(string remoteCode)
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await ProvisionActiveContextAndCandidateAsync(catalog);

        var cacheKey = "arca-boundary-probe-" + Guid.NewGuid().ToString("N");
        var tokenStore = new MemoryWsaaTokenStore();
        await tokenStore.WriteAsync(cacheKey + "_wsfe",
            new WsaaTokenEntry("test-token", "test-sign", DateTime.UtcNow.AddHours(1)));

        using var factory = CreateFactory(catalog, services =>
        {
            services.RemoveAll<IFiscalCredentialMaterializer>();
            services.AddSingleton<IFiscalCredentialMaterializer>(
                new ProbeMaterializer(cacheKey, tokenStore));
            services.RemoveAll<IHttpClientFactory>();
            services.AddSingleton<IHttpClientFactory>(new ErrorWsfeClientFactory(remoteCode));
        });
        using var client = factory.CreateClient();

        var body = await CallAsync(client, "arca:administrar", "activar_representacion_fiscal", new
        {
            contextId = ContextId,
            consumerId = ConsumerId,
            representedCuit = RepresentedCuit,
            pointOfSale = PointOfSale
        }, grant: $"{ContextId}|administrar");

        AssertStructuredError(body, "WSFE_" + remoteCode);
        Assert.DoesNotContain("test-token", body, StringComparison.Ordinal);
        Assert.DoesNotContain("test-sign", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExcepcionInesperada_DevuelveCodigoGenerico_NoFiltraDetalle_YSeLoguea()
    {
        using var temp = new TempCatalog();
        var catalog = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await ProvisionActiveContextAndCandidateAsync(catalog);
        using var logs = new RecordingLoggerProvider();

        using var factory = CreateFactory(catalog, services =>
        {
            services.RemoveAll<IFiscalCredentialMaterializer>();
            services.AddSingleton<IFiscalCredentialMaterializer>(new ThrowingMaterializer());
        }, logs);
        using var client = factory.CreateClient();

        var body = await CallAsync(client, "arca:administrar", "activar_representacion_fiscal", new
        {
            contextId = ContextId,
            consumerId = ConsumerId,
            representedCuit = RepresentedCuit,
            pointOfSale = PointOfSale
        }, grant: $"{ContextId}|administrar");

        var safeMessage = AssertStructuredError(body, ArcaMcpToolErrorBoundary.InternalErrorCode);
        Assert.Equal(ArcaMcpToolErrorBoundary.InternalErrorMessage, safeMessage);
        Assert.DoesNotContain(ThrowingMaterializer.InternalDetail, body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);

        Assert.Contains(logs.Entries, entry =>
            entry.Category == "dcArca.McpServer.ArcaMcpToolErrorBoundary"
            && entry.Level == LogLevel.Error
            && entry.Exception is InvalidOperationException
            && entry.Exception.Message == ThrowingMaterializer.InternalDetail
            && entry.Message.Contains("activar_representacion_fiscal", StringComparison.Ordinal)
            && entry.Message.Contains("CorrelationId=", StringComparison.Ordinal));
    }

    private WebApplicationFactory<Program> CreateFactory(
        IFiscalTechnicalContextStore catalog,
        Action<IServiceCollection>? configureServices = null,
        ILoggerProvider? loggerProvider = null)
    {
        var contentRoot = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "dcArca.McpServer");

        return _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(contentRoot);
            builder.UseEnvironment("Testing");
            _certificate.Apply(builder);

            if (loggerProvider is not null)
                builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFiscalTechnicalContextStore>();
                services.AddSingleton(catalog);

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, FiscalBoundaryAuthHandler>(
                        FiscalBoundaryAuthHandler.SchemeName, null);
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = FiscalBoundaryAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = FiscalBoundaryAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = FiscalBoundaryAuthHandler.SchemeName;
                });

                configureServices?.Invoke(services);
            });
        });
    }

    private static async Task<string> CallAsync(
        HttpClient client,
        string scope,
        string tool,
        object arguments,
        string? grant = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/call",
                @params = new { name = tool, arguments }
            })
        };
        request.Headers.Add(FiscalBoundaryAuthHandler.ScopeHeader, scope);
        request.Headers.Add(FiscalBoundaryAuthHandler.ConsumerHeader, "admin");
        if (grant is not null)
            request.Headers.Add(FiscalBoundaryAuthHandler.GrantHeader, grant);
        request.Headers.Add("Accept", "application/json, text/event-stream");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body;
    }

    private static string AssertStructuredError(string body, string expectedCode)
    {
        using var document = ParseJsonRpcResponse(body);
        var result = document.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        var structured = result.GetProperty("structuredContent");
        Assert.Equal(expectedCode, structured.GetProperty("code").GetString());
        var safeMessage = structured.GetProperty("safeMessage").GetString();
        Assert.False(string.IsNullOrWhiteSpace(safeMessage));
        return safeMessage!;
    }

    private static JsonDocument ParseJsonRpcResponse(string body)
    {
        var trimmed = body.TrimStart();
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
            return JsonDocument.Parse(trimmed);

        var data = string.Join(
            "\n",
            body.Split('\n')
                .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
                .Select(line => line["data:".Length..].TrimStart()));

        Assert.False(string.IsNullOrWhiteSpace(data), "La respuesta MCP no contenía un frame SSE data.");
        return JsonDocument.Parse(data);
    }

    private static async Task ProvisionActiveContextAndCandidateAsync(
        FileSystemFiscalTechnicalContextStore catalog)
    {
        await catalog.AddContextAsync(new FiscalTechnicalContextRecord(
            ContextId, "homologacion", FiscalContextOperationalState.Disabled, 1, []));
        await catalog.AddCandidateAssignmentAsync(ContextId, "rev-1", "cred-1", "admin");
        await catalog.MarkAssignmentValidatedAsync(ContextId, "rev-1", "wsaa-validated", "admin");
        await catalog.ActivateAssignmentAsync(ContextId, "rev-1", "admin");
        await catalog.SetContextStateAsync(
            ContextId, FiscalContextOperationalState.Active, "admin");
        await catalog.RegisterCandidateAsync(ContextId, ConsumerId, RepresentedCuit, "admin");
        await catalog.SelectPointOfSaleAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale, "admin");
    }

    public void Dispose()
        => _certificate.Dispose();

    private sealed class TempCatalog : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "arca-mcp-error-boundary-" + Guid.NewGuid().ToString("N"));

        public TempCatalog() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed class FiscalBoundaryAuthHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "FiscalBoundaryTest";
        public const string ScopeHeader = "X-Test-Scope";
        public const string ConsumerHeader = "X-Test-Consumer";
        public const string GrantHeader = "X-Test-Grant";

        public FiscalBoundaryAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var scope = Request.Headers.TryGetValue(ScopeHeader, out var scopes)
                ? scopes.ToString()
                : null;
            if (string.IsNullOrWhiteSpace(scope))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim> { new("scope", scope) };
            if (Request.Headers.TryGetValue(ConsumerHeader, out var consumers) &&
                !string.IsNullOrWhiteSpace(consumers.ToString()))
                claims.Add(new Claim(ArcaClaimTypes.ConsumerId, consumers.ToString()));
            if (Request.Headers.TryGetValue(GrantHeader, out var grants) &&
                !string.IsNullOrWhiteSpace(grants.ToString()))
                claims.Add(new Claim(ArcaClaimTypes.ContextGrant, grants.ToString()));

            var identity = new ClaimsIdentity(claims, SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class ProbeMaterializer(string cacheKey, IWsaaTokenStore tokenStore)
        : IFiscalCredentialMaterializer
    {
        public FiscalCredentialMaterialization Materialize(
            RepresentedFiscalContextRecord context,
            CredentialAssignmentRecord assignment)
        {
            var config = new dcArcaConfig
            {
                Environment = context.Environment,
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
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<FEParamGetPtosVentaResult><Errors><Err><Code>" + code +
                    "</Code><Msg>Remote authorization failure</Msg></Err></Errors></FEParamGetPtosVentaResult>")
            });
    }

    private sealed class ThrowingMaterializer : IFiscalCredentialMaterializer
    {
        public const string InternalDetail = "internal-materializer-diagnostic-xyz";

        public FiscalCredentialMaterialization Materialize(
            RepresentedFiscalContextRecord context,
            CredentialAssignmentRecord assignment)
            => throw new InvalidOperationException(InternalDetail);
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName)
            => new RecordingLogger(categoryName, Entries);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(
            string category,
            ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel)
                => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => entries.Enqueue(new LogEntry(
                    category, logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(
        string Category,
        LogLevel Level,
        string Message,
        Exception? Exception);
}
