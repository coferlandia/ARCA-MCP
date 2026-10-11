using System.Net.Http.Json;
using System.Security.Claims;
using dcArca.Core.Models;
using dcArca.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace dcArca.McpServer.Tests;

public sealed class TerminalEmissionReplayTransportTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string ContextId = "test-context";
    private const string ConsumerId = "test-consumer";
    private const long RepresentedCuit = 20123456786;
    private const int PointOfSale = 1;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "arca-terminal-replay-" + Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly FileSystemEmissionIdempotencyStore _store;
    private readonly ActionRequiredReplayResolver _resolver = new();
    private readonly TestCertificatePlaceholder _certificate = new();

    public TerminalEmissionReplayTransportTests(WebApplicationFactory<Program> baseFactory)
    {
        Directory.CreateDirectory(_root);
        _store = new FileSystemEmissionIdempotencyStore(
            Path.Combine(_root, "operations"));

        var contentRoot = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "dcArca.McpServer");
        _factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(contentRoot);
            builder.UseEnvironment("Testing");
            _certificate.Apply(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmissionIdempotencyStore>();
                services.AddSingleton<IEmissionIdempotencyStore>(_store);

                services.RemoveAll<IEmissionOperationInspector>();
                services.AddSingleton<IEmissionOperationInspector>(
                    new FileSystemEmissionOperationInspector(_store));

                services.RemoveAll<IFiscalSeriesCoordinator>();
                services.AddSingleton<IFiscalSeriesCoordinator>(
                    new InMemoryFiscalSeriesCoordinator());

                services.RemoveAll<IFiscalContextRuntimeResolver>();
                services.AddSingleton<IFiscalContextRuntimeResolver>(_resolver);

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, OperationContractTransportAuthHandler>(
                        OperationContractTransportAuthHandler.SchemeName,
                        null);
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = OperationContractTransportAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = OperationContractTransportAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = OperationContractTransportAuthHandler.SchemeName;
                });
            });
        });
    }

    [Fact]
    public async Task TerminalAuthorized_SameKey_ActionRequired_ReplayStoreFirstSinRuntimeNiWsfe()
    {
        var request = Request();
        const string idempotencyKey = "terminal-action-required";
        var identity = new FiscalOperationIdentity(
            ConsumerId,
            ContextId,
            "homologacion",
            RepresentedCuit,
            PointOfSale,
            (int)dcTipoComprobante.FacturaB,
            1,
            "test-credential-1");
        var keyHash = EmissionRequestFingerprint.OperationKeyHash(
            ConsumerId,
            ContextId,
            RepresentedCuit,
            PointOfSale,
            idempotencyKey);

        var created = await _store.GetOrCreateAsync(
            keyHash,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request));
        await _store.SaveAsync(created with
        {
            NumeroComprobante = 123,
            State = EmissionIdempotencyState.Authorized,
            FiscalResult = StoredFiscalResult.FromResponse(AuthorizedResponse(request)),
            UpdatedAt = DateTimeOffset.UtcNow
        });

        using var client = _factory.CreateClient();
        var response = await client.SendAsync(JsonRpc("tools/call", new
        {
            name = "emitir_comprobante_avanzado",
            arguments = new
            {
                factura = new
                {
                    tipoComprobante = "FacturaB",
                    concepto = "Productos",
                    cuitReceptor = RepresentedCuit,
                    tipoDocReceptor = 80,
                    condicionIvaReceptor = "ResponsableInscripto",
                    importeNeto = 100m,
                    importeIva = 21m,
                    importeTotal = 121m,
                    alicuotaIva = "Veintiuno",
                    monedaId = "PES",
                    monedaCotizacion = 1m,
                    fechaComprobante = "20261011"
                },
                idempotencyKey,
                contextId = ContextId,
                representedCuit = RepresentedCuit,
                pointOfSale = PointOfSale
            }
        }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("12345678901234", body, StringComparison.Ordinal);
        Assert.Contains("Authorized", body, StringComparison.Ordinal);
        Assert.DoesNotContain("FISCAL_REPRESENTATION_FORBIDDEN", body, StringComparison.Ordinal);
        Assert.Equal(0, _resolver.CurrentEmissionResolveCalls);
        Assert.Equal(1, _resolver.HistoricalEmissionAuthorizeCalls);
        Assert.Equal(0, _resolver.WsfeCalls);
    }

    [Fact]
    public async Task TerminalSameKey_DifferentRequest_DevuelveConflictoAntesDeAutorizacionActual()
    {
        var request = Request();
        const string idempotencyKey = "terminal-conflict";
        await SeedAsync(
            idempotencyKey,
            request,
            EmissionIdempotencyState.Authorized,
            AuthorizedResponse(request));

        using var client = _factory.CreateClient();
        var response = await client.SendAsync(JsonRpc("tools/call", new
        {
            name = "emitir_comprobante_avanzado",
            arguments = new
            {
                factura = new
                {
                    tipoComprobante = "FacturaB",
                    concepto = "Productos",
                    cuitReceptor = RepresentedCuit,
                    tipoDocReceptor = 80,
                    condicionIvaReceptor = "ResponsableInscripto",
                    importeNeto = 200m,
                    importeIva = 42m,
                    importeTotal = 242m,
                    alicuotaIva = "Veintiuno",
                    monedaId = "PES",
                    monedaCotizacion = 1m,
                    fechaComprobante = "20261011"
                },
                idempotencyKey,
                contextId = ContextId,
                representedCuit = RepresentedCuit,
                pointOfSale = PointOfSale
            }
        }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("IDEMPOTENCY_KEY_REUSED", body, StringComparison.Ordinal);
        Assert.Equal(0, _resolver.CurrentEmissionResolveCalls);
        Assert.Equal(1, _resolver.HistoricalEmissionAuthorizeCalls);
        Assert.Equal(0, _resolver.WsfeCalls);
    }

    [Fact]
    public async Task UnknownKey_ActionRequired_SigueBloqueadoComoTrabajoNuevo()
    {
        using var client = _factory.CreateClient();
        var response = await client.SendAsync(JsonRpc("tools/call", new
        {
            name = "emitir_comprobante_avanzado",
            arguments = new
            {
                factura = new
                {
                    tipoComprobante = "FacturaB",
                    concepto = "Productos",
                    cuitReceptor = RepresentedCuit,
                    tipoDocReceptor = 80,
                    condicionIvaReceptor = "ResponsableInscripto",
                    importeNeto = 100m,
                    importeIva = 21m,
                    importeTotal = 121m,
                    alicuotaIva = "Veintiuno",
                    monedaId = "PES",
                    monedaCotizacion = 1m,
                    fechaComprobante = "20261011"
                },
                idempotencyKey = "unknown-action-required",
                contextId = ContextId,
                representedCuit = RepresentedCuit,
                pointOfSale = PointOfSale
            }
        }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("FISCAL_REPRESENTATION_FORBIDDEN", body, StringComparison.Ordinal);
        Assert.Equal(1, _resolver.CurrentEmissionResolveCalls);
        Assert.Equal(0, _resolver.HistoricalEmissionAuthorizeCalls);
        Assert.Equal(0, _resolver.WsfeCalls);
    }

    [Fact]
    public async Task UncertainSameKey_ActionRequired_DevuelveEstadoDurableSinReemitir()
    {
        var request = Request();
        const string idempotencyKey = "uncertain-action-required";
        var uncertain = new dcFacturaResponse
        {
            Success = false,
            NumeroComprobante = 123,
            Codigo = "FECAE_OUTCOME_UNCERTAIN",
            Mensaje = "Resultado incierto.",
            EmissionOutcome = dcEmissionOutcome.Uncertain,
            Errores = ["Resultado incierto."]
        };
        await SeedAsync(
            idempotencyKey,
            request,
            EmissionIdempotencyState.Uncertain,
            uncertain);

        using var client = _factory.CreateClient();
        var response = await client.SendAsync(JsonRpc("tools/call", new
        {
            name = "emitir_comprobante_avanzado",
            arguments = new
            {
                factura = new
                {
                    tipoComprobante = "FacturaB",
                    concepto = "Productos",
                    cuitReceptor = RepresentedCuit,
                    tipoDocReceptor = 80,
                    condicionIvaReceptor = "ResponsableInscripto",
                    importeNeto = 100m,
                    importeIva = 21m,
                    importeTotal = 121m,
                    alicuotaIva = "Veintiuno",
                    monedaId = "PES",
                    monedaCotizacion = 1m,
                    fechaComprobante = "20261011"
                },
                idempotencyKey,
                contextId = ContextId,
                representedCuit = RepresentedCuit,
                pointOfSale = PointOfSale
            }
        }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("FECAE_OUTCOME_UNCERTAIN", body, StringComparison.Ordinal);
        Assert.Contains("reconcile", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _resolver.CurrentEmissionResolveCalls);
        Assert.Equal(1, _resolver.HistoricalEmissionAuthorizeCalls);
        Assert.Equal(0, _resolver.WsfeCalls);
    }

    private async Task SeedAsync(
        string idempotencyKey,
        dcFacturaRequest request,
        EmissionIdempotencyState state,
        dcFacturaResponse response)
    {
        var identity = new FiscalOperationIdentity(
            ConsumerId,
            ContextId,
            "homologacion",
            RepresentedCuit,
            PointOfSale,
            (int)dcTipoComprobante.FacturaB,
            1,
            "test-credential-1");
        var keyHash = EmissionRequestFingerprint.OperationKeyHash(
            ConsumerId,
            ContextId,
            RepresentedCuit,
            PointOfSale,
            idempotencyKey);
        var created = await _store.GetOrCreateAsync(
            keyHash,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request));
        await _store.SaveAsync(created with
        {
            NumeroComprobante = 123,
            State = state,
            FiscalResult = StoredFiscalResult.FromResponse(response),
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    private static dcFacturaRequest Request() => new()
    {
        TipoComprobante = dcTipoComprobante.FacturaB,
        Concepto = dcConcepto.Productos,
        CuitReceptor = RepresentedCuit,
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

    private static dcFacturaResponse AuthorizedResponse(dcFacturaRequest request)
        => new()
        {
            Success = true,
            Cae = "12345678901234",
            CaeVencimiento = "20261021",
            NumeroComprobante = 123,
            Resultado = "A",
            EmissionOutcome = dcEmissionOutcome.Authorized,
            PuntoVenta = PointOfSale,
            TipoComprobante = request.TipoComprobante,
            Concepto = request.Concepto,
            DocTipo = (dcTipoDocumento)request.TipoDocReceptor,
            DocNro = request.CuitReceptor,
            FechaComprobante = request.FechaComprobante,
            MonedaId = request.MonedaId,
            MonedaCotizacion = request.MonedaCotizacion,
            ImporteTotal = request.ImporteTotal,
            ImporteNeto = request.ImporteNeto,
            ImporteIva = request.ImporteIva,
            CondicionIvaReceptor = request.CondicionIvaReceptor
        };

    private static HttpRequestMessage JsonRpc(string method, object? @params = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params })
        };
        request.Headers.Add("Accept", "application/json, text/event-stream");
        return request;
    }

    private sealed class ActionRequiredReplayResolver : IFiscalContextRuntimeResolver
    {
        private readonly RepresentedFiscalContextRecord _context;
        public int CurrentEmissionResolveCalls { get; private set; }
        public int HistoricalEmissionAuthorizeCalls { get; private set; }
        public int WsfeCalls { get; private set; }

        public ActionRequiredReplayResolver()
        {
            var now = DateTimeOffset.UtcNow.AddMinutes(-10);
            _context = new RepresentedFiscalContextRecord(
                ContextId,
                "homologacion",
                RepresentedCuit,
                PointOfSale,
                FiscalContextOperationalState.Active,
                1,
                false,
                [new CredentialAssignmentRecord(
                    "test-credential-1",
                    "cred-v1",
                    CredentialAssignmentStatus.Active,
                    "test",
                    now,
                    now,
                    now,
                    "test")]);
        }

        public Task<AuthorizedFiscalContext> AuthorizeAsync(
            ClaimsPrincipal principal,
            string? requestedContextId,
            string operation,
            CancellationToken cancellationToken = default)
            => throw new FiscalContextAccessException(
                "FISCAL_REPRESENTATION_FORBIDDEN",
                "La representación actual está ActionRequired.");

        public Task<AuthorizedFiscalContext> AuthorizeHistoricalEmissionAsync(
            ClaimsPrincipal principal,
            EmissionIdempotencyRecord operation,
            CancellationToken cancellationToken = default)
        {
            HistoricalEmissionAuthorizeCalls++;
            return Task.FromResult(new AuthorizedFiscalContext(
                ConsumerId,
                _context,
                operation.Identity.RepresentationProvenance));
        }

        public Task<FiscalContextRuntime> ResolveForEmissionAsync(
            ClaimsPrincipal principal,
            string? requestedContextId,
            string idempotencyKey,
            dcTipoComprobante tipoComprobante,
            CancellationToken cancellationToken = default)
        {
            CurrentEmissionResolveCalls++;
            throw new FiscalContextAccessException(
                "FISCAL_REPRESENTATION_FORBIDDEN",
                "La representación actual está ActionRequired.");
        }

        public Task<FiscalContextRuntime> ResolveForReadAsync(
            ClaimsPrincipal principal,
            string? requestedContextId,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No esperado.");

        public Task<FiscalContextRuntime> ResolveForHistoricalOperationAsync(
            ClaimsPrincipal principal,
            EmissionIdempotencyRecord operation,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "El replay terminal no debe materializar runtime histórico.");
    }

    public void Dispose()
    {
        _factory.Dispose();
        _certificate.Dispose();
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
