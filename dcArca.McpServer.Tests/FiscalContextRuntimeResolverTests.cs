using System.Security.Claims;
using dcArca.Core.Models;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.McpServer.Tests;

public class FiscalContextRuntimeResolverTests
{
    [Fact]
    public async Task ContextoSinGrant_FallaAntesDeResolverCredenciales()
    {
        using var temp = new TempDirectory();
        var contexts = ContextStore(temp, Context("ctx-a"), Context("ctx-b"));
        var materializer = new FakeMaterializer();
        var resolver = Resolver(contexts, temp, materializer);
        var principal = Principal("consumer-a", ("ctx-a", "facturar"));

        var exception = await Assert.ThrowsAsync<FiscalContextAccessException>(() =>
            resolver.ResolveForEmissionAsync(
                principal,
                "ctx-b",
                "payment-1",
                dcTipoComprobante.FacturaB));

        Assert.Equal("FISCAL_CONTEXT_FORBIDDEN", exception.Code);
        Assert.Equal(0, materializer.Calls);
    }

    [Fact]
    public async Task OmitirContextoConDosGrants_EsAmbiguo()
    {
        using var temp = new TempDirectory();
        var contexts = ContextStore(temp, Context("ctx-a"), Context("ctx-b"));
        var materializer = new FakeMaterializer();
        var resolver = Resolver(contexts, temp, materializer);
        var principal = Principal(
            "consumer-a",
            ("ctx-a", "facturar"),
            ("ctx-b", "facturar"));

        var exception = await Assert.ThrowsAsync<FiscalContextAccessException>(() =>
            resolver.ResolveForEmissionAsync(
                principal,
                null,
                "payment-1",
                dcTipoComprobante.FacturaB));

        Assert.Equal("FISCAL_CONTEXT_REQUIRED", exception.Code);
        Assert.Equal(0, materializer.Calls);
    }

    [Fact]
    public async Task RetryPosteriorARotacion_UsaRevisionHistoricaOriginal()
    {
        using var temp = new TempDirectory();
        var context = Context("ctx-a") with
        {
            Assignments =
            [
                Assignment("personal-v1", "cred-personal", CredentialAssignmentStatus.Historical),
                Assignment("empresa-v2", "cred-empresa", CredentialAssignmentStatus.Active)
            ]
        };
        var contexts = ContextStore(temp, context);
        var operations = new FileSystemEmissionIdempotencyStore(temp.Operations);
        var materializer = new FakeMaterializer();
        var resolver = Resolver(contexts, operations, materializer);
        var principal = Principal("consumer-a", ("ctx-a", "facturar"));
        var request = Request();
        var keyHash = EmissionRequestFingerprint.OperationKeyHash("consumer-a", "ctx-a", "payment-1");
        await operations.GetOrCreateAsync(
            keyHash,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            new FiscalOperationIdentity(
                "consumer-a", "ctx-a", "homologacion", 20123456786, 7,
                (int)dcTipoComprobante.FacturaB, 1, "personal-v1"),
            StoredFiscalEvidence.FromRequest(request));

        using var runtime = await resolver.ResolveForEmissionAsync(
            principal,
            "ctx-a",
            "payment-1",
            dcTipoComprobante.FacturaB);

        Assert.Equal("personal-v1", runtime.Assignment.AssignmentRevision);
        Assert.Equal("cred-personal", runtime.Assignment.CredentialId);
        Assert.Equal("personal-v1", materializer.LastAssignmentRevision);
    }

    [Fact]
    public async Task NuevaOperacionPosteriorARotacion_UsaSoloAssignmentActivo()
    {
        using var temp = new TempDirectory();
        var context = Context("ctx-a") with
        {
            Assignments =
            [
                Assignment("personal-v1", "cred-personal", CredentialAssignmentStatus.Historical),
                Assignment("empresa-v2", "cred-empresa", CredentialAssignmentStatus.Active)
            ]
        };
        var contexts = ContextStore(temp, context);
        var materializer = new FakeMaterializer();
        var resolver = Resolver(contexts, temp, materializer);
        var principal = Principal("consumer-a", ("ctx-a", "facturar"));

        using var runtime = await resolver.ResolveForEmissionAsync(
            principal,
            "ctx-a",
            "new-payment",
            dcTipoComprobante.FacturaB);

        Assert.Equal("empresa-v2", runtime.Assignment.AssignmentRevision);
        Assert.Equal("cred-empresa", runtime.Assignment.CredentialId);
    }

    [Fact]
    public async Task RetryConAssignmentHistoricoDeshabilitado_NoHaceFallbackAlActivo()
    {
        using var temp = new TempDirectory();
        var context = Context("ctx-a") with
        {
            Assignments =
            [
                Assignment("personal-v1", "cred-personal", CredentialAssignmentStatus.Disabled),
                Assignment("empresa-v2", "cred-empresa", CredentialAssignmentStatus.Active)
            ]
        };
        var contexts = ContextStore(temp, context);
        var operations = new FileSystemEmissionIdempotencyStore(temp.Operations);
        var materializer = new FakeMaterializer();
        var resolver = Resolver(contexts, operations, materializer);
        var principal = Principal("consumer-a", ("ctx-a", "facturar"));
        var request = Request();
        var keyHash = EmissionRequestFingerprint.OperationKeyHash("consumer-a", "ctx-a", "payment-1");
        await operations.GetOrCreateAsync(
            keyHash,
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            new FiscalOperationIdentity(
                "consumer-a", "ctx-a", "homologacion", 20123456786, 7,
                (int)dcTipoComprobante.FacturaB, 1, "personal-v1"),
            StoredFiscalEvidence.FromRequest(request));

        var exception = await Assert.ThrowsAsync<FiscalContextAccessException>(() =>
            resolver.ResolveForEmissionAsync(
                principal,
                "ctx-a",
                "payment-1",
                dcTipoComprobante.FacturaB));

        Assert.Equal("HISTORICAL_ASSIGNMENT_INTERVENTION_REQUIRED", exception.Code);
        Assert.Equal(0, materializer.Calls);
    }

    [Fact]
    public async Task ContextosAlias_MantienenContextIdDistintoPeroCompartenSerieFiscal()
    {
        using var temp = new TempDirectory();
        var contexts = ContextStore(temp, Context("tenant-a"), Context("tenant-b"));
        var materializer = new FakeMaterializer();
        var resolver = Resolver(contexts, temp, materializer);
        var principal = Principal(
            "consumer-a",
            ("tenant-a", "facturar"),
            ("tenant-b", "facturar"));

        using var a = await resolver.ResolveForEmissionAsync(principal, "tenant-a", "a", dcTipoComprobante.FacturaB);
        using var b = await resolver.ResolveForEmissionAsync(principal, "tenant-b", "b", dcTipoComprobante.FacturaB);
        var identityA = a.IdentityProvider.For(dcTipoComprobante.FacturaB);
        var identityB = b.IdentityProvider.For(dcTipoComprobante.FacturaB);

        Assert.NotEqual(identityA.ContextId, identityB.ContextId);
        Assert.Equal(identityA.SeriesKey, identityB.SeriesKey);
    }

    [Fact]
    public async Task LegacyKey_SoloResuelveLegacyDefaultAunqueExistanOtrosContextos()
    {
        using var temp = new TempDirectory();
        var legacy = Context("legacy") with { LegacyDefault = true };
        var contexts = ContextStore(temp, legacy, Context("other"));
        var materializer = new FakeMaterializer();
        var resolver = Resolver(contexts, temp, materializer, legacyContextId: "legacy");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ArcaClaimTypes.ConsumerId, "legacy-consumer"),
            new Claim(ArcaClaimTypes.LegacyKey, "true")
        ], "test"));

        using var runtime = await resolver.ResolveForEmissionAsync(
            principal,
            null,
            "legacy-key",
            dcTipoComprobante.FacturaB);

        Assert.Equal("legacy", runtime.Context.ContextId);
    }

    private static FileSystemRepresentedFiscalContextStore ContextStore(
        TempDirectory temp,
        params RepresentedFiscalContextRecord[] contexts)
    {
        var store = new FileSystemRepresentedFiscalContextStore(temp.Contexts);
        foreach (var context in contexts)
            store.AddContextAsync(context).GetAwaiter().GetResult();
        return store;
    }

    private static FiscalContextRuntimeResolver Resolver(
        IRepresentedFiscalContextStore contexts,
        TempDirectory temp,
        FakeMaterializer materializer,
        string legacyContextId = "legacy")
        => Resolver(
            contexts,
            new FileSystemEmissionIdempotencyStore(temp.Operations),
            materializer,
            legacyContextId);

    private static FiscalContextRuntimeResolver Resolver(
        IRepresentedFiscalContextStore contexts,
        IEmissionIdempotencyStore operations,
        FakeMaterializer materializer,
        string legacyContextId = "legacy")
        => new(
            contexts,
            operations,
            new SingleFiscalContextOptions("legacy-consumer", legacyContextId, "homologacion", 1, "legacy-v1"),
            materializer);

    private static ClaimsPrincipal Principal(string consumerId, params (string ContextId, string Operation)[] grants)
    {
        var claims = new List<Claim> { new(ArcaClaimTypes.ConsumerId, consumerId) };
        claims.AddRange(grants.Select(grant =>
            new Claim(ArcaClaimTypes.ContextGrant, ArcaClaimTypes.GrantValue(grant.ContextId, grant.Operation))));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static RepresentedFiscalContextRecord Context(string contextId)
        => new(
            contextId,
            "homologacion",
            20123456786,
            7,
            FiscalContextOperationalState.Active,
            1,
            false,
            [Assignment("active-v1", "cred-active", CredentialAssignmentStatus.Active)]);

    private static CredentialAssignmentRecord Assignment(
        string revision,
        string credentialId,
        CredentialAssignmentStatus status)
        => new(
            revision,
            credentialId,
            status,
            status == CredentialAssignmentStatus.Candidate ? null : "validated",
            DateTimeOffset.UtcNow,
            status == CredentialAssignmentStatus.Candidate ? null : DateTimeOffset.UtcNow,
            status == CredentialAssignmentStatus.Active ? DateTimeOffset.UtcNow : null,
            "test");

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
        FechaComprobante = "20261003"
    };

    private sealed class FakeMaterializer : IFiscalCredentialMaterializer
    {
        public int Calls { get; private set; }
        public string? LastAssignmentRevision { get; private set; }

        public FiscalCredentialMaterialization Materialize(
            RepresentedFiscalContextRecord context,
            CredentialAssignmentRecord assignment)
        {
            Calls++;
            LastAssignmentRevision = assignment.AssignmentRevision;
            var config = new dcArcaConfig
            {
                Cuit = context.RepresentedCuit.ToString(),
                PuntoVenta = context.PointOfSale,
                CertificatePath = "not-used.pfx",
                CertificatePassword = string.Empty,
                WsaaUrl = "https://example.invalid/wsaa",
                WsfeUrl = "https://example.invalid/wsfe",
                PadronUrl = "https://example.invalid/padron"
            };
            var binding = new FiscalCredentialHostBinding(
                assignment.CredentialId,
                $"{context.Environment}:{assignment.CredentialId}",
                config);
            return new FiscalCredentialMaterialization(
                config,
                new dcArcaAuthService(config.WsaaUrl, config.CertificatePath, config.CertificatePassword, binding.CacheIdentity),
                new dcArcaAuthService(config.WsaaUrl, config.CertificatePath, config.CertificatePassword, binding.CacheIdentity, "ws_sr_constancia_inscripcion"),
                binding);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "arca-runtime-resolver-tests-" + Guid.NewGuid().ToString("N"));
        public string Contexts => Path.Combine(Root, "contexts");
        public string Operations => Path.Combine(Root, "operations");

        public TempDirectory()
        {
            Directory.CreateDirectory(Contexts);
            Directory.CreateDirectory(Operations);
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
