using System.Security.Claims;
using dcArca.Core.Models;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.McpServer.Tests;

public class McpOperationContractServiceTests
{
    [Fact]
    public async Task ConsultarOperacionInexistente_NoCreaRegistro()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        var wsfe = new FakeWsfeClient();
        var resolver = new FakeRuntimeResolver(Context(), wsfe);
        var service = Service(store, resolver);

        var result = await service.GetOperationAsync(
            Principal(),
            "ctx-a",
            operationId: null,
            idempotencyKey: "missing-key");

        Assert.False(result.Found);
        Assert.Equal("OPERATION_NOT_FOUND", result.ErrorCode);
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.json", SearchOption.TopDirectoryOnly));
        Assert.Equal(0, wsfe.ConsultCalls);
        Assert.Equal(0, wsfe.EmitCalls);
    }

    [Fact]
    public async Task ValidacionInvalida_NoCreaOperacionNiInvocaWsfe()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        var wsfe = new FakeWsfeClient();
        var resolver = new FakeRuntimeResolver(Context(), wsfe);
        var service = Service(store, resolver);

        var invalid = new dcFacturaRequest
        {
            ImporteTotal = 0,
            FechaComprobante = ""
        };

        var result = await service.ValidateRequestAsync(Principal(includeEmit: true), "ctx-a", invalid);

        Assert.False(result.Valid);
        Assert.NotEmpty(result.ValidationIssues);
        Assert.False(result.NumberReserved);
        Assert.False(result.AuthorizationGuaranteed);
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.json", SearchOption.TopDirectoryOnly));
        Assert.Equal(0, wsfe.ConsultCalls);
        Assert.Equal(0, wsfe.EmitCalls);
    }

    [Fact]
    public async Task ReconciliarOperacionIncierta_SoloConsultaYRecuperaSinReemitir()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        var request = ValidRequest();
        var identity = Identity();
        var operationId = EmissionRequestFingerprint.OperationKeyHash("consumer-a", "ctx-a", "payment-1");
        var uncertain = await CreateUncertainAsync(store, request, identity, operationId, 123);

        var wsfe = new FakeWsfeClient { ConsultResponse = AuthorizedResponse(request, 123) };
        var resolver = new FakeRuntimeResolver(Context(), wsfe);
        var service = Service(store, resolver);

        var result = await service.ReconcileAsync(
            Principal(),
            "ctx-a",
            operationId,
            idempotencyKey: null);

        Assert.True(result.Found);
        Assert.Equal(EmissionIdempotencyState.Authorized.ToString(), result.State);
        Assert.Equal(dcEmissionOutcome.RecoveredSuccess, result.EmissionOutcome);
        Assert.NotNull(result.OfficialRead);
        Assert.Equal(1, wsfe.ConsultCalls);
        Assert.Equal(0, wsfe.EmitCalls);

        var persisted = await store.GetAsync(operationId);
        Assert.NotNull(persisted);
        Assert.Equal(EmissionIdempotencyState.Authorized, persisted!.State);
        Assert.Equal(dcEmissionOutcome.RecoveredSuccess, persisted.FiscalResult?.EmissionOutcome);
        Assert.Equal(EmissionIdempotencyState.Uncertain, uncertain.State);
    }

    [Fact]
    public async Task ReconciliacionAtrasada_NoDegradaAuthorizedConcurrente()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemEmissionIdempotencyStore(temp.Path);
        var request = ValidRequest();
        var identity = Identity();
        var operationId = EmissionRequestFingerprint.OperationKeyHash("consumer-a", "ctx-a", "payment-race");
        var uncertain = await CreateUncertainAsync(store, request, identity, operationId, 123);
        using var coordinator = new InMemoryFiscalSeriesCoordinator();
        await coordinator.ReserveAsync(identity, operationId, 123);

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mismatched = AuthorizedResponse(request, 123);
        mismatched.DocNro = 30999999991;
        var wsfe = new FakeWsfeClient
        {
            ConsultResponse = mismatched,
            ConsultEntered = entered,
            ReleaseConsult = release
        };
        var resolver = new FakeRuntimeResolver(Context(), wsfe);
        var service = Service(store, resolver, coordinator);

        var staleReconciliation = service.ReconcileAsync(
            Principal(),
            "ctx-a",
            operationId,
            idempotencyKey: null);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var authorizedResponse = AuthorizedResponse(request, 123);
        var authorized = uncertain with
        {
            State = EmissionIdempotencyState.Authorized,
            FiscalResult = StoredFiscalResult.FromResponse(authorizedResponse),
            UpdatedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        };
        await store.SaveAsync(authorized);
        await coordinator.ReleaseAsync(identity, operationId);
        release.TrySetResult();

        var result = await staleReconciliation;

        Assert.Equal(EmissionIdempotencyState.Authorized.ToString(), result.State);
        Assert.Equal(dcEmissionOutcome.Authorized, result.EmissionOutcome);
        Assert.Equal("12345678901234", result.PersistedFiscalResult?.Cae);
        Assert.Equal(1, wsfe.ConsultCalls);
        Assert.Equal(0, wsfe.EmitCalls);

        var persisted = await store.GetAsync(operationId);
        Assert.NotNull(persisted);
        Assert.Equal(EmissionIdempotencyState.Authorized, persisted!.State);
        Assert.Equal("12345678901234", persisted.FiscalResult?.Cae);
        Assert.Null(await coordinator.GetActiveAsync(identity));
    }

    private static async Task<EmissionIdempotencyRecord> CreateUncertainAsync(
        FileSystemEmissionIdempotencyStore store,
        dcFacturaRequest request,
        FiscalOperationIdentity identity,
        string operationId,
        long number)
    {
        var created = await store.GetOrCreateAsync(
            operationId,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            identity,
            StoredFiscalEvidence.FromRequest(request));
        var uncertain = created with
        {
            NumeroComprobante = number,
            State = EmissionIdempotencyState.Uncertain,
            FiscalResult = StoredFiscalResult.FromResponse(new dcFacturaResponse
            {
                Success = false,
                NumeroComprobante = number,
                Codigo = "TRANSPORT_UNCERTAIN",
                Mensaje = "Resultado desconocido.",
                EmissionOutcome = dcEmissionOutcome.Uncertain
            }),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await store.SaveAsync(uncertain);
        return uncertain;
    }

    private static McpOperationContractService Service(
        FileSystemEmissionIdempotencyStore store,
        IFiscalContextRuntimeResolver resolver,
        IFiscalSeriesCoordinator? coordinator = null)
        => new(
            store,
            new FileSystemEmissionOperationInspector(store),
            resolver,
            new FakeAssignmentValidator(),
            coordinator ?? new InMemoryFiscalSeriesCoordinator());

    private static ClaimsPrincipal Principal(bool includeEmit = false)
    {
        var claims = new List<Claim>
        {
            new(ArcaClaimTypes.ConsumerId, "consumer-a"),
            new("scope", "arca:consultar"),
            new(ArcaClaimTypes.ContextGrant, ArcaClaimTypes.GrantValue("ctx-a", "consultar"))
        };
        if (includeEmit)
        {
            claims.Add(new Claim("scope", "arca:facturar"));
            claims.Add(new Claim(ArcaClaimTypes.ContextGrant, ArcaClaimTypes.GrantValue("ctx-a", "facturar")));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static RepresentedFiscalContextRecord Context()
    {
        var now = DateTimeOffset.UtcNow;
        return new RepresentedFiscalContextRecord(
            "ctx-a",
            "homologacion",
            20123456786,
            7,
            FiscalContextOperationalState.Active,
            1,
            false,
            [new CredentialAssignmentRecord(
                "v1",
                "cred-v1",
                CredentialAssignmentStatus.Active,
                "validated",
                now,
                now,
                now,
                "test")]);
    }

    private static FiscalOperationIdentity Identity()
        => new(
            "consumer-a",
            "ctx-a",
            "homologacion",
            20123456786,
            7,
            (int)dcTipoComprobante.FacturaB,
            1,
            "v1");

    private static dcFacturaRequest ValidRequest() => new()
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
        FechaComprobante = "20261003"
    };

    private static dcFacturaResponse AuthorizedResponse(dcFacturaRequest request, long number)
        => new()
        {
            Success = true,
            Cae = "12345678901234",
            CaeVencimiento = "20261013",
            NumeroComprobante = number,
            Resultado = "A",
            EmissionOutcome = dcEmissionOutcome.Authorized,
            PuntoVenta = 7,
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
            ImporteNoGravado = request.ImporteNoGravado,
            ImporteExento = request.ImporteExento,
            ImporteTributos = request.ImporteTributos,
            CondicionIvaReceptor = request.CondicionIvaReceptor,
            Iva =
            [
                new dcFacturaResponse.IvaDetalle
                {
                    Alicuota = request.AlicuotaIva,
                    BaseImponible = request.ImporteNeto,
                    Importe = request.ImporteIva
                }
            ]
        };

    private sealed class FakeRuntimeResolver : IFiscalContextRuntimeResolver
    {
        private readonly RepresentedFiscalContextRecord _context;
        private readonly FakeWsfeClient _wsfe;

        public FakeRuntimeResolver(RepresentedFiscalContextRecord context, FakeWsfeClient wsfe)
        {
            _context = context;
            _wsfe = wsfe;
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
            return Task.FromResult(new AuthorizedFiscalContext("consumer-a", _context));
        }

        public Task<FiscalContextRuntime> ResolveForHistoricalOperationAsync(
            ClaimsPrincipal principal,
            EmissionIdempotencyRecord operation,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Runtime(operation.Identity.CredentialAssignmentRevision));

        public Task<FiscalContextRuntime> ResolveForReadAsync(
            ClaimsPrincipal principal,
            string? requestedContextId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Runtime("v1"));

        public Task<FiscalContextRuntime> ResolveForEmissionAsync(
            ClaimsPrincipal principal,
            string? requestedContextId,
            string idempotencyKey,
            dcTipoComprobante tipoComprobante,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("El contrato read-only no debe resolver runtime de emisión en estos tests.");

        private FiscalContextRuntime Runtime(string revision)
        {
            var config = new dcArcaConfig
            {
                Cuit = _context.RepresentedCuit.ToString(),
                PuntoVenta = _context.PointOfSale,
                WsaaUrl = "https://example.invalid/wsaa",
                WsfeUrl = "https://example.invalid/wsfe",
                PadronUrl = "https://example.invalid/padron"
            };
            var assignment = _context.Assignments.Single(x => x.AssignmentRevision == revision);
            var identityProvider = new SingleFiscalOperationIdentityProvider(
                config,
                new SingleFiscalContextOptions(
                    "consumer-a",
                    _context.ContextId,
                    _context.Environment,
                    _context.ContextRevision,
                    revision));
            return new FiscalContextRuntime(
                "consumer-a",
                _context,
                assignment,
                config,
                _wsfe,
                new FakePadronClient(),
                identityProvider);
        }
    }

    private sealed class FakeWsfeClient : IdcWsfeClient
    {
        public int ConsultCalls { get; private set; }
        public int EmitCalls { get; private set; }
        public dcFacturaResponse ConsultResponse { get; init; } = new() { Success = false };
        public TaskCompletionSource? ConsultEntered { get; init; }
        public TaskCompletionSource? ReleaseConsult { get; init; }

        public async Task<dcFacturaResponse> FECompConsultarAsync(long numeroComprobante, dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
        {
            ConsultCalls++;
            ConsultEntered?.TrySetResult();
            if (ReleaseConsult is not null)
                await ReleaseConsult.Task.WaitAsync(cancellationToken);
            return ConsultResponse.Success ? ConsultEvidenceTestData.MarkValid(ConsultResponse) : ConsultResponse;
        }

        public Task<dcFacturaResponse> FECAESolicitarAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
        {
            EmitCalls++;
            throw new InvalidOperationException("Reconcile no debe emitir.");
        }

        public Task<dcFacturaResponse> SolicitarCaeAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
        {
            EmitCalls++;
            throw new InvalidOperationException("Reconcile no debe emitir.");
        }

        public Task<dcFacturaResponse> FECompUltimoAutorizadoAsync(dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No esperado.");

        public Task<List<dcCondicionIvaOption>> GetCondicionesIVAReceptorAsync(int docTipo, long docNro, dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No esperado.");
    }

    private sealed class FakePadronClient : IdcPadronClient
    {
        public Task<dcPadronPersonaResult> GetPersonaAsync(long cuit, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No esperado.");
    }

    private sealed class FakeAssignmentValidator : IFiscalAssignmentAuthorizationValidator
    {
        public Task<FiscalAssignmentValidationResult> ProbeAsync(string contextId, string assignmentRevision, CancellationToken cancellationToken = default)
            => Task.FromResult(Result(contextId, assignmentRevision));

        public Task<FiscalAssignmentValidationResult> ValidateCandidateAsync(string contextId, string assignmentRevision, string actor, CancellationToken cancellationToken = default)
            => Task.FromResult(Result(contextId, assignmentRevision));

        private static FiscalAssignmentValidationResult Result(string contextId, string revision)
            => new(
                FiscalAssignmentValidationStatus.NotVerified,
                "NOT_VERIFIED",
                "No verificado en test.",
                contextId,
                revision,
                "cred-v1",
                7,
                DateTimeOffset.UtcNow);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "arca-mcp-contract-tests-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
