using System.Security.Claims;
using dcArca.Core.Models;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.McpServer.Tests;

public sealed class HistoricalOperationAuthorizationRegressionTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "arca-historical-authorization-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task V2_ActionRequired_PermiteResolverOperacionHistoricaConAssignmentOriginal()
    {
        var contexts = Path.Combine(_root, "contexts");
        var operationsDirectory = Path.Combine(_root, "operations");
        Directory.CreateDirectory(contexts);
        Directory.CreateDirectory(operationsDirectory);

        var catalog = new FileSystemFiscalTechnicalContextStore(contexts);
        await catalog.AddContextAsync(new FiscalTechnicalContextRecord(
            "operator-homo",
            "homologacion",
            FiscalContextOperationalState.Disabled,
            1,
            []));
        await catalog.AddCandidateAssignmentAsync("operator-homo", "rev-1", "cred-original", "admin");
        await catalog.MarkAssignmentValidatedAsync("operator-homo", "rev-1", "wsaa-ok", "admin");
        await catalog.ActivateAssignmentAsync("operator-homo", "rev-1", "admin");
        await catalog.SetContextStateAsync("operator-homo", FiscalContextOperationalState.Active, "admin");

        await catalog.RegisterCandidateAsync("operator-homo", "consumer-a", 20123456786, "admin");
        await catalog.SelectPointOfSaleAsync("operator-homo", "consumer-a", 20123456786, 7, "admin");
        await catalog.MarkRepresentationVerifiedAsync(
            "operator-homo",
            "consumer-a",
            20123456786,
            7,
            "FEParamGetPtosVenta|context=operator-homo|env=homologacion|cuit=20123456786|pv=7|rev=rev-1|checked=2026-10-11T00:00:00Z",
            "admin");
        await catalog.ActivateRepresentationAsync(
            "operator-homo",
            "consumer-a",
            20123456786,
            7,
            "admin");

        var operations = new FileSystemEmissionIdempotencyStore(operationsDirectory);
        var request = Request();
        var operation = await operations.GetOrCreateAsync(
            EmissionRequestFingerprint.OperationKeyHash(
                "consumer-a", "operator-homo", 20123456786, 7, "payment-1"),
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            new FiscalOperationIdentity(
                "consumer-a",
                "operator-homo",
                "homologacion",
                20123456786,
                7,
                (int)dcTipoComprobante.FacturaB,
                1,
                "rev-1"),
            StoredFiscalEvidence.FromRequest(request));

        await catalog.AddCandidateAssignmentAsync("operator-homo", "rev-2", "cred-new", "admin");
        await catalog.MarkAssignmentValidatedAsync("operator-homo", "rev-2", "wsaa-new", "admin");
        await catalog.ActivateAssignmentAsync("operator-homo", "rev-2", "admin");
        await catalog.MarkRepresentationActionRequiredAsync(
            "operator-homo",
            "consumer-a",
            20123456786,
            7,
            "WSFE_601",
            "admin");

        var materializer = new FakeMaterializer();
        var resolver = new FiscalContextRuntimeResolver(
            catalog,
            operations,
            new SingleFiscalContextOptions("legacy", "legacy", "homologacion", 1, "legacy"),
            materializer,
            OpenEmissionRecoveryGate.Instance);

        using var runtime = await resolver.ResolveForHistoricalOperationAsync(
            Principal("consumer-a", ("operator-homo", "consultar")),
            operation);

        Assert.Equal("rev-1", runtime.Assignment.AssignmentRevision);
        Assert.Equal("cred-original", runtime.Assignment.CredentialId);
        Assert.Equal(1, materializer.Calls);

        var currentRead = await Assert.ThrowsAsync<FiscalContextAccessException>(() =>
            resolver.ResolveForReadAsync(
                Principal("consumer-a", ("operator-homo", "consultar")),
                "operator-homo",
                20123456786,
                7));
        Assert.Equal("FISCAL_REPRESENTATION_FORBIDDEN", currentRead.Code);
    }

    [Fact]
    public async Task V2_ActionRequiredNuncaActivado_NoReabreOperacionDeRepresentacionRevocada()
    {
        var contexts = Path.Combine(_root, "revoked-contexts");
        var operationsDirectory = Path.Combine(_root, "revoked-operations");
        Directory.CreateDirectory(contexts);
        Directory.CreateDirectory(operationsDirectory);

        var catalog = new FileSystemFiscalTechnicalContextStore(contexts);
        await catalog.AddContextAsync(new FiscalTechnicalContextRecord(
            "operator-homo",
            "homologacion",
            FiscalContextOperationalState.Disabled,
            1,
            []));
        await catalog.AddCandidateAssignmentAsync("operator-homo", "rev-1", "cred-original", "admin");
        await catalog.MarkAssignmentValidatedAsync("operator-homo", "rev-1", "wsaa-ok", "admin");
        await catalog.ActivateAssignmentAsync("operator-homo", "rev-1", "admin");
        await catalog.SetContextStateAsync("operator-homo", FiscalContextOperationalState.Active, "admin");

        await catalog.RegisterCandidateAsync("operator-homo", "consumer-a", 20123456786, "admin");
        await catalog.SelectPointOfSaleAsync("operator-homo", "consumer-a", 20123456786, 7, "admin");
        await catalog.MarkRepresentationVerifiedAsync(
            "operator-homo",
            "consumer-a",
            20123456786,
            7,
            "FEParamGetPtosVenta|context=operator-homo|env=homologacion|cuit=20123456786|pv=7|rev=rev-1|checked=2026-10-11T00:00:00Z",
            "admin");
        await catalog.ActivateRepresentationAsync(
            "operator-homo",
            "consumer-a",
            20123456786,
            7,
            "admin");

        var operations = new FileSystemEmissionIdempotencyStore(operationsDirectory);
        var request = Request();
        var operation = await operations.GetOrCreateAsync(
            EmissionRequestFingerprint.OperationKeyHash(
                "consumer-a", "operator-homo", 20123456786, 7, "revoked-payment"),
            EmissionRequestFingerprint.RequestHash(request),
            EmissionRequestFingerprint.CanonicalizationVersion,
            new FiscalOperationIdentity(
                "consumer-a",
                "operator-homo",
                "homologacion",
                20123456786,
                7,
                (int)dcTipoComprobante.FacturaB,
                1,
                "rev-1"),
            StoredFiscalEvidence.FromRequest(request));

        await catalog.RevokeRepresentationAsync(
            "operator-homo",
            "consumer-a",
            20123456786,
            7,
            "admin");

        await catalog.RegisterCandidateAsync(
            "operator-homo",
            "consumer-a",
            20123456786,
            "admin");
        await catalog.SelectPointOfSaleAsync(
            "operator-homo",
            "consumer-a",
            20123456786,
            7,
            "admin");
        await catalog.MarkRepresentationActionRequiredAsync(
            "operator-homo",
            "consumer-a",
            20123456786,
            7,
            "WSFE_601",
            "admin");

        var materializer = new FakeMaterializer();
        var resolver = new FiscalContextRuntimeResolver(
            catalog,
            operations,
            new SingleFiscalContextOptions("legacy", "legacy", "homologacion", 1, "legacy"),
            materializer,
            OpenEmissionRecoveryGate.Instance);

        var exception = await Assert.ThrowsAsync<FiscalContextAccessException>(() =>
            resolver.ResolveForHistoricalOperationAsync(
                Principal("consumer-a", ("operator-homo", "consultar")),
                operation));

        Assert.Equal("FISCAL_REPRESENTATION_FORBIDDEN", exception.Code);
        Assert.Equal(0, materializer.Calls);

        await catalog.MarkRepresentationVerifiedAsync(
            "operator-homo",
            "consumer-a",
            20123456786,
            7,
            "FEParamGetPtosVenta|context=operator-homo|env=homologacion|cuit=20123456786|pv=7|rev=rev-1|checked=2026-10-11T00:10:00Z",
            "admin");
        await catalog.ActivateRepresentationAsync(
            "operator-homo",
            "consumer-a",
            20123456786,
            7,
            "admin");

        var activeReplacement = await Assert.ThrowsAsync<FiscalContextAccessException>(() =>
            resolver.ResolveForHistoricalOperationAsync(
                Principal("consumer-a", ("operator-homo", "consultar")),
                operation));

        Assert.Equal("FISCAL_REPRESENTATION_FORBIDDEN", activeReplacement.Code);
        Assert.Equal(0, materializer.Calls);
    }

    private static ClaimsPrincipal Principal(
        string consumerId,
        params (string ContextId, string Operation)[] grants)
    {
        var claims = new List<Claim> { new(ArcaClaimTypes.ConsumerId, consumerId) };
        claims.AddRange(grants.Select(grant =>
            new Claim(
                ArcaClaimTypes.ContextGrant,
                ArcaClaimTypes.GrantValue(grant.ContextId, grant.Operation))));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
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

    private sealed class FakeMaterializer : IFiscalCredentialMaterializer
    {
        public int Calls { get; private set; }

        public FiscalCredentialMaterialization Materialize(
            RepresentedFiscalContextRecord context,
            CredentialAssignmentRecord assignment)
        {
            Calls++;
            var config = new dcArcaConfig
            {
                Cuit = context.RepresentedCuit.ToString(),
                PuntoVenta = context.PointOfSale,
                CertificatePath = "unused.pfx",
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
                new dcArcaAuthService(
                    config.WsaaUrl,
                    config.CertificatePath,
                    config.CertificatePassword,
                    binding.CacheIdentity),
                new dcArcaAuthService(
                    config.WsaaUrl,
                    config.CertificatePath,
                    config.CertificatePassword,
                    binding.CacheIdentity,
                    "ws_sr_constancia_inscripcion"),
                binding);
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
