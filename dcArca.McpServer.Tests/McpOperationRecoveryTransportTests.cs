using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
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

public sealed class OperationContractTransportAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "OperationContractTransport";

    public OperationContractTransportAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim("scope", "arca:consultar arca:facturar"),
            new Claim(ArcaClaimTypes.ConsumerId, "test-consumer"),
            new Claim(ArcaClaimTypes.ContextGrant, ArcaClaimTypes.GrantValue("test-context", "consultar")),
            new Claim(ArcaClaimTypes.ContextGrant, ArcaClaimTypes.GrantValue("test-context", "facturar"))
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public class McpOperationRecoveryTransportTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly LostResponseWsfeClient _wsfe = new();

    public McpOperationRecoveryTransportTests(WebApplicationFactory<Program> factory)
    {
        var contentRoot = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "dcArca.McpServer");
        var certPath = Path.Combine(Directory.GetCurrentDirectory(), "test-cert-placeholder.pfx");
        if (!File.Exists(certPath)) File.WriteAllBytes(certPath, Array.Empty<byte>());

        var resolver = new TransportRuntimeResolver(_wsfe);
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(contentRoot);
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFiscalContextRuntimeResolver>();
                services.AddSingleton<IFiscalContextRuntimeResolver>(resolver);

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
    public async Task EmitirPerderRespuestaConsultarYReconciliar_EnviaCAEUnaSolaVez()
    {
        using var client = _factory.CreateClient();

        var emission = await client.SendAsync(JsonRpc("tools/call", new
        {
            name = "emitir_comprobante_avanzado",
            arguments = new
            {
                factura = new
                {
                    tipoComprobante = "FacturaB",
                    concepto = "Productos",
                    cuitReceptor = 20123456786L,
                    tipoDocReceptor = 80,
                    condicionIvaReceptor = "ResponsableInscripto",
                    importeNeto = 100m,
                    importeIva = 21m,
                    importeTotal = 121m,
                    alicuotaIva = "Veintiuno",
                    monedaId = "PES",
                    monedaCotizacion = 1m,
                    fechaComprobante = "20261003"
                },
                idempotencyKey = "transport-lost-response-1",
                contextId = "test-context"
            }
        }));
        var emissionBody = await emission.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, emission.StatusCode);
        Assert.Equal(1, _wsfe.EmitCalls);
        Assert.Contains("error", emissionBody, StringComparison.OrdinalIgnoreCase);

        var lookup = await client.SendAsync(JsonRpc("tools/call", new
        {
            name = "consultar_operacion",
            arguments = new
            {
                idempotencyKey = "transport-lost-response-1",
                contextId = "test-context"
            }
        }));
        var lookupBody = await lookup.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, lookup.StatusCode);
        Assert.Contains("Uncertain", lookupBody, StringComparison.Ordinal);
        Assert.Equal(1, _wsfe.EmitCalls);
        Assert.Equal(0, _wsfe.ConsultCalls);

        var reconcile = await client.SendAsync(JsonRpc("tools/call", new
        {
            name = "reconciliar_operacion",
            arguments = new
            {
                idempotencyKey = "transport-lost-response-1",
                contextId = "test-context"
            }
        }));
        var reconcileBody = await reconcile.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, reconcile.StatusCode);
        Assert.Contains("Authorized", reconcileBody, StringComparison.Ordinal);
        Assert.Contains("RecoveredSuccess", reconcileBody, StringComparison.Ordinal);
        Assert.Equal(1, _wsfe.EmitCalls);
        Assert.Equal(1, _wsfe.ConsultCalls);
    }

    private static HttpRequestMessage JsonRpc(string method, object? @params = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params })
        };
        request.Headers.Add("Accept", "application/json, text/event-stream");
        return request;
    }

    private sealed class TransportRuntimeResolver : IFiscalContextRuntimeResolver
    {
        private readonly LostResponseWsfeClient _wsfe;
        private readonly RepresentedFiscalContextRecord _context;

        public TransportRuntimeResolver(LostResponseWsfeClient wsfe)
        {
            _wsfe = wsfe;
            var now = DateTimeOffset.UtcNow;
            _context = new RepresentedFiscalContextRecord(
                "test-context",
                "homologacion",
                20123456786,
                1,
                FiscalContextOperationalState.Active,
                1,
                true,
                [new CredentialAssignmentRecord(
                    "test-credential-1",
                    "legacy-default",
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
        {
            if (!string.IsNullOrWhiteSpace(requestedContextId)
                && !string.Equals(requestedContextId, _context.ContextId, StringComparison.Ordinal))
                throw new FiscalContextAccessException("FISCAL_CONTEXT_FORBIDDEN", "Contexto no autorizado.");
            return Task.FromResult(new AuthorizedFiscalContext("test-consumer", _context));
        }

        public Task<FiscalContextRuntime> ResolveForReadAsync(
            ClaimsPrincipal principal,
            string? requestedContextId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Runtime());

        public Task<FiscalContextRuntime> ResolveForEmissionAsync(
            ClaimsPrincipal principal,
            string? requestedContextId,
            string idempotencyKey,
            dcTipoComprobante tipoComprobante,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Runtime());

        public Task<FiscalContextRuntime> ResolveForHistoricalOperationAsync(
            ClaimsPrincipal principal,
            EmissionIdempotencyRecord operation,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Runtime());

        private FiscalContextRuntime Runtime()
        {
            var config = new dcArcaConfig
            {
                Cuit = "20123456786",
                PuntoVenta = 1,
                CertificatePath = "unused.pfx",
                WsaaUrl = "https://example.invalid/wsaa",
                WsfeUrl = "https://example.invalid/wsfe",
                PadronUrl = "https://example.invalid/padron"
            };
            var identity = new SingleFiscalOperationIdentityProvider(
                config,
                new SingleFiscalContextOptions(
                    "test-consumer",
                    "test-context",
                    "homologacion",
                    1,
                    "test-credential-1"));
            return new FiscalContextRuntime(
                "test-consumer",
                _context,
                _context.ActiveAssignment!,
                config,
                _wsfe,
                new TransportPadronClient(),
                identity);
        }
    }

    private sealed class LostResponseWsfeClient : IdcWsfeClient
    {
        private dcFacturaRequest? _submitted;
        public int EmitCalls { get; private set; }
        public int ConsultCalls { get; private set; }

        public Task<dcFacturaResponse> FECompUltimoAutorizadoAsync(
            dcTipoComprobante tipoComprobante,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new dcFacturaResponse
            {
                Success = true,
                NumeroComprobante = 0,
                TipoComprobante = tipoComprobante
            });

        public Task<dcFacturaResponse> FECAESolicitarAsync(
            dcFacturaRequest factura,
            CancellationToken cancellationToken = default)
        {
            EmitCalls++;
            _submitted = factura;
            throw new HttpRequestException("Simulated lost response after submission.");
        }

        public Task<dcFacturaResponse> SolicitarCaeAsync(
            dcFacturaRequest factura,
            CancellationToken cancellationToken = default)
            => FECAESolicitarAsync(factura, cancellationToken);

        public Task<dcFacturaResponse> FECompConsultarAsync(
            long numeroComprobante,
            dcTipoComprobante tipoComprobante,
            CancellationToken cancellationToken = default)
        {
            ConsultCalls++;
            var request = _submitted ?? throw new InvalidOperationException("No existe envío previo.");
            return Task.FromResult(new dcFacturaResponse
            {
                Success = true,
                Cae = "12345678901234",
                CaeVencimiento = "20261013",
                NumeroComprobante = numeroComprobante,
                Resultado = "A",
                PuntoVenta = 1,
                TipoComprobante = tipoComprobante,
                Concepto = request.Concepto,
                DocTipo = (dcTipoDocumento)request.TipoDocReceptor,
                DocNro = request.CuitReceptor,
                CondicionIvaReceptor = request.CondicionIvaReceptor,
                FechaComprobante = request.FechaComprobante,
                FechaServicioDesde = request.FechaServicioDesde ?? string.Empty,
                FechaServicioHasta = request.FechaServicioHasta ?? string.Empty,
                FechaVencimientoPago = request.FechaVencimiento ?? string.Empty,
                MonedaId = request.MonedaId,
                MonedaCotizacion = request.MonedaCotizacion,
                ImporteTotal = request.ImporteTotal,
                ImporteNeto = request.ImporteNeto,
                ImporteIva = request.ImporteIva,
                ImporteNoGravado = request.ImporteNoGravado,
                ImporteExento = request.ImporteExento,
                ImporteTributos = request.ImporteTributos,
                Iva = request.AlicuotaIva.HasValue
                    ? [new dcFacturaResponse.IvaDetalle
                    {
                        Alicuota = request.AlicuotaIva,
                        BaseImponible = request.ImporteNeto,
                        Importe = request.ImporteIva
                    }]
                    : request.Iva.Select(x => new dcFacturaResponse.IvaDetalle
                    {
                        Alicuota = x.Alicuota,
                        BaseImponible = x.BaseImponible,
                        Importe = x.Importe
                    }).ToList(),
                Tributos = request.Tributos.Select(x => new dcFacturaResponse.TributoDetalle
                {
                    Id = x.Id,
                    Descripcion = x.Descripcion,
                    BaseImponible = x.BaseImponible,
                    Alicuota = x.Alicuota,
                    Importe = x.Importe
                }).ToList()
            });
        }

        public Task<List<dcCondicionIvaOption>> GetCondicionesIVAReceptorAsync(
            int docTipo,
            long docNro,
            dcTipoComprobante tipoComprobante,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No esperado en este flujo.");
    }

    private sealed class TransportPadronClient : IdcPadronClient
    {
        public Task<dcPadronPersonaResult> GetPersonaAsync(long cuit, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No esperado en este flujo.");
    }
}
