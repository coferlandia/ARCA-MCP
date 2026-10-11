using System.Security.Claims;
using dcArca.Core.Models;
using dcArca.Core.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace dcArca.McpServer.Tests;

public sealed class FiscalAuthorizationRaceTests
{
    private const long RepresentedCuit = 20123456786;

    [Theory]
    [InlineData(FiscalContextOperationalState.Disabled)]
    [InlineData(FiscalContextOperationalState.ReadOnly)]
    public async Task CambioDeEstadoDuranteRevalidacionDeNumero_NoSolicitaCAE(
        FiscalContextOperationalState nextState)
    {
        using var temp = new TempDirectory();
        var operations = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new InMemoryFiscalSeriesCoordinator();
        var wsfe = new PausedNumberingWsfeClient();
        var config = new dcArcaConfig
        {
            Cuit = RepresentedCuit.ToString(),
            PuntoVenta = 7,
            WsfeUrl = "https://example.invalid/wsfe"
        };
        var assignment = new CredentialAssignmentRecord(
            "rev-1", "cred-1", CredentialAssignmentStatus.Active,
            "validated", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow, "admin");
        var context = new RepresentedFiscalContextRecord(
            "operator-homo", "homologacion", RepresentedCuit, 7,
            FiscalContextOperationalState.Active, 1, false, [assignment]);
        var identity = new SingleFiscalOperationIdentityProvider(
            config, new SingleFiscalContextOptions(
                "consumer-a", "operator-homo", "homologacion", 1, "rev-1"));
        var runtime = new FiscalContextRuntime(
            "consumer-a", context, assignment, config, wsfe, null!, identity);
        var resolver = new MutableContextResolver(runtime);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ArcaClaimTypes.ConsumerId, "consumer-a"),
            new Claim("scope", "arca:facturar"),
            new Claim(ArcaClaimTypes.ContextGrant,
                ArcaClaimTypes.GrantValue("operator-homo", "facturar"))
        ], "test"));
        var tools = new ArcaTools(
            resolver, operations, coordinator, null!,
            new McpOperationContractService(operations, null!, resolver, null!, coordinator),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });

        // Pause the SECOND FECompUltimoAutorizado call: numbering is reserved, but
        // the final pre-CAE authorization has not run yet.
        var emission = tools.EmitirComprobanteAvanzado(
            Request(), "race-state-change", "operator-homo", RepresentedCuit, 7);
        await wsfe.RevalidationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        resolver.State = nextState;
        wsfe.ResumeRevalidation.TrySetResult();

        var error = await Assert.ThrowsAsync<FiscalContextAccessException>(() => emission);
        Assert.Equal("FISCAL_CONTEXT_NOT_ACTIVE", error.Code);
        Assert.Equal(2, wsfe.LastNumberCalls);
        Assert.Equal(0, wsfe.EmitCalls);

        var keyHash = EmissionRequestFingerprint.OperationKeyHash(
            "consumer-a", "operator-homo", RepresentedCuit, 7, "race-state-change");
        var stored = await operations.GetAsync(keyHash);
        Assert.NotNull(stored);
        Assert.Equal(EmissionIdempotencyState.NumberAssigned, stored!.State);
    }

    [Fact]
    public async Task ReemplazoDeRepresentacionDuranteRevalidacion_NoCruzaLimiteDeCAE()
    {
        using var temp = new TempDirectory();
        var operations = new FileSystemEmissionIdempotencyStore(temp.Path);
        using var coordinator = new InMemoryFiscalSeriesCoordinator();
        var wsfe = new PausedNumberingWsfeClient();
        var config = new dcArcaConfig
        {
            Cuit = RepresentedCuit.ToString(),
            PuntoVenta = 7,
            WsfeUrl = "https://example.invalid/wsfe"
        };
        var activatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var assignment = new CredentialAssignmentRecord(
            "rev-1", "cred-1", CredentialAssignmentStatus.Active,
            "validated", activatedAt, activatedAt,
            activatedAt, "admin");
        var context = new RepresentedFiscalContextRecord(
            "operator-homo", "homologacion", RepresentedCuit, 7,
            FiscalContextOperationalState.Active, 1, false, [assignment]);
        var identity = new SingleFiscalOperationIdentityProvider(
            config, new SingleFiscalContextOptions(
                "consumer-a", "operator-homo", "homologacion", 1, "rev-1"));
        var runtime = new FiscalContextRuntime(
            "consumer-a", context, assignment, config, wsfe, null!, identity);
        var resolver = new MutableContextResolver(runtime);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ArcaClaimTypes.ConsumerId, "consumer-a"),
            new Claim("scope", "arca:facturar"),
            new Claim(ArcaClaimTypes.ContextGrant,
                ArcaClaimTypes.GrantValue("operator-homo", "facturar"))
        ], "test"));
        var tools = new ArcaTools(
            resolver, operations, coordinator, null!,
            new McpOperationContractService(operations, null!, resolver, null!, coordinator),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });

        var emission = tools.EmitirComprobanteAvanzado(
            Request(), "race-representation-replacement", "operator-homo", RepresentedCuit, 7);
        await wsfe.RevalidationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Current authorization still looks Active with the same assignment revision/environment,
        // but the operation-era representation has been revoked/replaced while numbering was in flight.
        resolver.HistoricalEmissionAllowed = false;
        wsfe.ResumeRevalidation.TrySetResult();

        var error = await Assert.ThrowsAsync<FiscalContextAccessException>(() => emission);
        Assert.Equal("FISCAL_REPRESENTATION_FORBIDDEN", error.Code);
        Assert.Equal(2, wsfe.LastNumberCalls);
        Assert.Equal(0, wsfe.EmitCalls);
        Assert.Equal(1, resolver.HistoricalEmissionAuthorizeCalls);

        var keyHash = EmissionRequestFingerprint.OperationKeyHash(
            "consumer-a", "operator-homo", RepresentedCuit, 7,
            "race-representation-replacement");
        var stored = await operations.GetAsync(keyHash);
        Assert.NotNull(stored);
        Assert.Equal(EmissionIdempotencyState.NumberAssigned, stored!.State);
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
        FechaComprobante = "20261003"
    };

    private sealed class MutableContextResolver(FiscalContextRuntime runtime) : IFiscalContextRuntimeResolver
    {
        public FiscalContextOperationalState State { get; set; } = FiscalContextOperationalState.Active;
        public bool HistoricalEmissionAllowed { get; set; } = true;
        public int HistoricalEmissionAuthorizeCalls { get; private set; }

        public Task<AuthorizedFiscalContext> AuthorizeAsync(
            ClaimsPrincipal principal, string? contextId, string operation,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AuthorizedFiscalContext(
                runtime.ConsumerId, runtime.Context with { OperationalState = State }));

        public Task<FiscalContextRuntime> ResolveForEmissionAsync(
            ClaimsPrincipal principal, string? contextId, string idempotencyKey,
            dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
            => Task.FromResult(runtime);

        public Task<FiscalContextRuntime> ResolveForReadAsync(
            ClaimsPrincipal principal, string? contextId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AuthorizedFiscalContext> AuthorizeHistoricalEmissionAsync(
            ClaimsPrincipal principal,
            EmissionIdempotencyRecord operation,
            CancellationToken cancellationToken = default)
        {
            HistoricalEmissionAuthorizeCalls++;
            if (!HistoricalEmissionAllowed)
                throw new FiscalContextAccessException(
                    "FISCAL_REPRESENTATION_FORBIDDEN",
                    "La representación histórica de la operación fue reemplazada.");
            return Task.FromResult(new AuthorizedFiscalContext(
                runtime.ConsumerId, runtime.Context with { OperationalState = State }));
        }

        public Task<FiscalContextRuntime> ResolveForHistoricalOperationAsync(
            ClaimsPrincipal principal, EmissionIdempotencyRecord operation,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class PausedNumberingWsfeClient : IdcWsfeClient
    {
        private int _lastNumberCalls;
        private int _emitCalls;

        public TaskCompletionSource RevalidationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ResumeRevalidation { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int LastNumberCalls => Volatile.Read(ref _lastNumberCalls);
        public int EmitCalls => Volatile.Read(ref _emitCalls);

        public async Task<dcFacturaResponse> FECompUltimoAutorizadoAsync(
            dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _lastNumberCalls) == 2)
            {
                RevalidationEntered.TrySetResult();
                await ResumeRevalidation.Task.WaitAsync(cancellationToken);
            }
            return new dcFacturaResponse { Success = true, NumeroComprobante = 0 };
        }

        public Task<dcFacturaResponse> FECAESolicitarAsync(
            dcFacturaRequest factura, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _emitCalls);
            return Task.FromResult(new dcFacturaResponse
            {
                Success = true,
                NumeroComprobante = factura.NumeroComprobante ?? 0,
                Cae = "UNEXPECTED",
                EmissionOutcome = dcEmissionOutcome.Authorized
            });
        }

        public Task<dcFacturaResponse> FECompConsultarAsync(
            long numeroComprobante, dcTipoComprobante tipoComprobante,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new dcFacturaResponse
            {
                Success = false,
                NumeroComprobante = numeroComprobante
            });

        public Task<dcFacturaResponse> SolicitarCaeAsync(
            dcFacturaRequest factura, CancellationToken cancellationToken = default)
            => FECAESolicitarAsync(factura, cancellationToken);

        public Task<List<dcCondicionIvaOption>> GetCondicionesIVAReceptorAsync(
            int docTipo, long docNro, dcTipoComprobante tipoComprobante,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new List<dcCondicionIvaOption>());
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "arca-authorization-race-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
