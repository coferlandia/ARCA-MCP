using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using dcArca.Core.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace dcArca.McpServer.Tests;

public sealed class AdministrativeAbandonmentTransportTests
    : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string ContextId = "operator-homo";
    private const string ConsumerA = "consumer-a";
    private const string ConsumerB = "consumer-b";
    private const string ToolName = "abandonar_operacion_pre_submission";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly TestCertificatePlaceholder _certificate = new();

    public AdministrativeAbandonmentTransportTests(WebApplicationFactory<Program> baseFactory)
    {
        var contentRoot = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "dcArca.McpServer");

        _factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(contentRoot);
            builder.UseEnvironment("Testing");
            _certificate.Apply(builder);
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, AdministrativeAbandonmentAuthHandler>(
                        AdministrativeAbandonmentAuthHandler.SchemeName, null);
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = AdministrativeAbandonmentAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = AdministrativeAbandonmentAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = AdministrativeAbandonmentAuthHandler.SchemeName;
                });
            });
        });
    }

    [Fact]
    public async Task Tool_SeExponeSoloConScopeAdministrativo()
    {
        using var client = _factory.CreateClient();

        var ordinary = await SendAsync(
            client, "arca:consultar arca:facturar", ConsumerA, grant: null,
            method: "tools/list");
        Assert.DoesNotContain($"\"name\":\"{ToolName}\"", ordinary, StringComparison.Ordinal);

        var admin = await SendAsync(
            client, "arca:administrar", ConsumerA, Grant(), method: "tools/list");
        Assert.Contains($"\"name\":\"{ToolName}\"", admin, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminAutorizado_AbandonaNumberAssigned_YReportaTransicionYLiberacion()
    {
        const int pointOfSale = 71;
        var key = await SeedAsync(
            ConsumerA, pointOfSale, EmissionIdempotencyState.NumberAssigned, 171);

        using var client = _factory.CreateClient();
        var body = await CallAbandonAsync(
            client, "arca:administrar", ConsumerA, Grant(), key, "manual_recovery");

        Assert.DoesNotContain("\"isError\":true", body, StringComparison.Ordinal);
        Assert.Contains("NumberAssigned", body, StringComparison.Ordinal);
        Assert.Contains("AbandonedBeforeSubmission", body, StringComparison.Ordinal);
        Assert.Contains("reservationReleased", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("true", body, StringComparison.OrdinalIgnoreCase);

        var store = _factory.Services.GetRequiredService<IEmissionIdempotencyStore>();
        var durable = await store.GetAsync(key);
        Assert.NotNull(durable);
        Assert.Equal(EmissionIdempotencyState.AbandonedBeforeSubmission, durable!.State);
        Assert.Equal(171, durable.NumeroComprobante);
        Assert.Null(await _factory.Services.GetRequiredService<IFiscalSeriesCoordinator>()
            .GetActiveAsync(durable.Identity));
    }

    [Fact]
    public async Task AdminSinGrant_DevuelveForbiddenEstructurado_YNoModificaOperacion()
    {
        var key = await SeedAsync(
            ConsumerA, 72, EmissionIdempotencyState.NumberAssigned, 172);

        using var client = _factory.CreateClient();
        var body = await CallAbandonAsync(
            client, "arca:administrar", ConsumerA, grant: null, key, "manual_recovery");

        AssertStructuredError(body, "FISCAL_ADMIN_FORBIDDEN");

        var durable = await _factory.Services.GetRequiredService<IEmissionIdempotencyStore>()
            .GetAsync(key);
        Assert.Equal(EmissionIdempotencyState.NumberAssigned, durable?.State);
    }

    [Fact]
    public async Task AdminDeOtroConsumer_NoPuedeAccederAlNamespaceDeLaOperacion()
    {
        var key = await SeedAsync(
            ConsumerA, 73, EmissionIdempotencyState.NumberAssigned, 173);

        using var client = _factory.CreateClient();
        var body = await CallAbandonAsync(
            client, "arca:administrar", ConsumerB, Grant(), key, "manual_recovery");

        AssertStructuredError(body, "OPERATION_NOT_FOUND");

        var durable = await _factory.Services.GetRequiredService<IEmissionIdempotencyStore>()
            .GetAsync(key);
        Assert.Equal(EmissionIdempotencyState.NumberAssigned, durable?.State);
    }

    [Theory]
    [InlineData(EmissionIdempotencyState.Submitting, 74, 174)]
    [InlineData(EmissionIdempotencyState.Uncertain, 75, 175)]
    public async Task EstadoQuePuedeHaberCruzadoSideEffect_DevuelveErrorEstable(
        EmissionIdempotencyState state,
        int pointOfSale,
        long number)
    {
        var key = await SeedAsync(ConsumerA, pointOfSale, state, number);

        using var client = _factory.CreateClient();
        var body = await CallAbandonAsync(
            client, "arca:administrar", ConsumerA, Grant(), key, "manual_recovery");

        AssertStructuredError(body, "OPERATION_ABANDON_NOT_ALLOWED");

        var durable = await _factory.Services.GetRequiredService<IEmissionIdempotencyStore>()
            .GetAsync(key);
        Assert.Equal(state, durable?.State);
    }

    [Fact]
    public async Task RepetirAbandonoAdministrativo_EsIdempotente_YConservaAuditoriaOriginal()
    {
        var key = await SeedAsync(
            ConsumerA, 76, EmissionIdempotencyState.NumberAssigned, 176);

        using var client = _factory.CreateClient();
        var first = await CallAbandonAsync(
            client, "arca:administrar", ConsumerA, Grant(), key, "manual_recovery");
        Assert.DoesNotContain("\"isError\":true", first, StringComparison.Ordinal);

        var store = _factory.Services.GetRequiredService<IEmissionIdempotencyStore>();
        var afterFirst = await store.GetAsync(key);
        Assert.NotNull(afterFirst);

        var second = await CallAbandonAsync(
            client, "arca:administrar", ConsumerA, Grant(), key, "second_reason");
        Assert.DoesNotContain("\"isError\":true", second, StringComparison.Ordinal);
        Assert.Contains("AbandonedBeforeSubmission", second, StringComparison.Ordinal);

        var afterSecond = await store.GetAsync(key);
        Assert.NotNull(afterSecond);
        Assert.Equal(afterFirst!.UpdatedAt, afterSecond!.UpdatedAt);
        Assert.Equal(afterFirst.AbandonmentAudit, afterSecond.AbandonmentAudit);
        Assert.Equal("manual_recovery", afterSecond.AbandonmentAudit?.ReasonCode);
    }

    private async Task<string> SeedAsync(
        string consumerId,
        int pointOfSale,
        EmissionIdempotencyState state,
        long number)
    {
        _ = _factory.Services;
        var store = _factory.Services.GetRequiredService<IEmissionIdempotencyStore>();
        var coordinator = _factory.Services.GetRequiredService<IFiscalSeriesCoordinator>();
        var key = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(
                        $"{consumerId}|{pointOfSale}|{state}|{Guid.NewGuid():N}")))
            .ToLowerInvariant();
        var request = Request();
        var identity = new FiscalOperationIdentity(
            consumerId,
            ContextId,
            "homologacion",
            20123456786,
            pointOfSale,
            (int)dcTipoComprobante.FacturaB,
            1,
            "cred-1");

        var created = await store.GetOrCreateAsync(
            key,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request));

        await coordinator.ReserveAsync(identity, key, number);
        await store.SaveAsync(created with
        {
            NumeroComprobante = number,
            State = state,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        return key;
    }

    private static dcFacturaRequest Request() => new()
    {
        TipoComprobante = dcTipoComprobante.FacturaB,
        Concepto = dcConcepto.Productos,
        CuitReceptor = 20123456786,
        TipoDocReceptor = (int)dcTipoDocumento.CUIT,
        CondicionIvaReceptor = dcCondicionIvaReceptor.ResponsableInscripto,
        ImporteNeto = 100m,
        ImporteIva = 21m,
        ImporteTotal = 121m,
        AlicuotaIva = dcAlicuotaIva.Veintiuno,
        MonedaId = "PES",
        MonedaCotizacion = 1m,
        FechaComprobante = "20261011"
    };

    private static string Grant()
        => ArcaClaimTypes.GrantValue(ContextId, "administrar");

    private static Task<string> CallAbandonAsync(
        HttpClient client,
        string scope,
        string consumer,
        string? grant,
        string operationId,
        string reasonCode)
        => SendAsync(
            client,
            scope,
            consumer,
            grant,
            "tools/call",
            new
            {
                name = ToolName,
                arguments = new { operationId, reasonCode }
            });

    private static async Task<string> SendAsync(
        HttpClient client,
        string scope,
        string consumer,
        string? grant,
        string method,
        object? @params = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method,
                @params
            })
        };
        request.Headers.Add(AdministrativeAbandonmentAuthHandler.ScopeHeader, scope);
        request.Headers.Add(AdministrativeAbandonmentAuthHandler.ConsumerHeader, consumer);
        request.Headers.Add(AdministrativeAbandonmentAuthHandler.KeyIdHeader, "test-admin-key");
        if (grant is not null)
            request.Headers.Add(AdministrativeAbandonmentAuthHandler.GrantHeader, grant);
        request.Headers.Add("Accept", "application/json, text/event-stream");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body;
    }

    private static void AssertStructuredError(string body, string expectedCode)
    {
        using var document = ParseJsonRpcResponse(body);
        var result = document.RootElement.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        var structured = result.GetProperty("structuredContent");
        Assert.Equal(expectedCode, structured.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            structured.GetProperty("safeMessage").GetString()));
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

    public void Dispose()
    {
        _factory.Dispose();
        _certificate.Dispose();
    }

    private sealed class AdministrativeAbandonmentAuthHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "AdministrativeAbandonmentTest";
        public const string ScopeHeader = "X-Test-Scope";
        public const string ConsumerHeader = "X-Test-Consumer";
        public const string GrantHeader = "X-Test-Grant";
        public const string KeyIdHeader = "X-Test-Key-Id";

        public AdministrativeAbandonmentAuthHandler(
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
            if (Request.Headers.TryGetValue(ConsumerHeader, out var consumers)
                && !string.IsNullOrWhiteSpace(consumers.ToString()))
                claims.Add(new Claim(ArcaClaimTypes.ConsumerId, consumers.ToString()));
            if (Request.Headers.TryGetValue(GrantHeader, out var grants)
                && !string.IsNullOrWhiteSpace(grants.ToString()))
                claims.Add(new Claim(ArcaClaimTypes.ContextGrant, grants.ToString()));
            if (Request.Headers.TryGetValue(KeyIdHeader, out var keyIds)
                && !string.IsNullOrWhiteSpace(keyIds.ToString()))
                claims.Add(new Claim(ClaimTypes.NameIdentifier, keyIds.ToString()));

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(
                    new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)),
                    SchemeName)));
        }
    }
}
