using dcArca.Core.Services;
using Xunit;

namespace dcArca.Core.Tests;

public sealed class FiscalRepresentationLifecyclePolicyTests
{
    private const string ContextId = "operator-homo";
    private const string ConsumerId = "secretaria";
    private const long RepresentedCuit = 20123456786;
    private const int PointOfSale = 7;
    private static readonly DateTimeOffset CheckedAt =
        new(2026, 10, 11, 12, 34, 56, TimeSpan.Zero);

    [Fact]
    public async Task Verified_PersisteEvidenciaExactaYActiva()
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        var policy = new FiscalRepresentationLifecyclePolicy(store);
        var probe = Probe(
            dcPointOfSaleProbeStatus.Verified,
            "POINT_OF_SALE_AUTHORIZATION_VERIFIED",
            "mensaje que no debe formar parte de la evidencia");

        var result = await policy.ApplyAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale,
            ProbeIdentity(),
            probe, "admin");

        Assert.Equal(FiscalRepresentationLifecycleDisposition.Activated, result.Disposition);
        Assert.Equal(FiscalRepresentationStatus.Active, result.Representation.Status);
        Assert.Equal(
            "FEParamGetPtosVenta|context=operator-homo|env=homologacion|cuit=20123456786|pv=7|rev=rev-1|checked=2026-10-11T12:34:56.0000000+00:00",
            result.VerificationEvidence);
        Assert.Equal(result.VerificationEvidence, result.Representation.VerificationEvidence);
        Assert.DoesNotContain("mensaje", result.VerificationEvidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Manual602_PreservaEvidenciaExplicitaYActivaSinFingirVerificacionRemota()
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        var policy = new FiscalRepresentationLifecyclePolicy(store);
        var probe = Probe(
            dcPointOfSaleProbeStatus.ManualUnverified,
            "POINT_OF_SALE_MANUAL_UNVERIFIED",
            "ARCA no informó puntos de venta; manual 602");

        var result = await policy.ApplyAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale,
            ProbeIdentity(),
            probe, "admin");

        Assert.Equal(FiscalRepresentationLifecycleDisposition.Activated, result.Disposition);
        Assert.Equal(
            "MANUAL_PV_UNVERIFIED_602|context=operator-homo|env=homologacion|cuit=20123456786|pv=7|rev=rev-1|checked=2026-10-11T12:34:56.0000000+00:00",
            result.VerificationEvidence);
        Assert.Equal(FiscalRepresentationStatus.Active, result.Representation.Status);
        Assert.Null(result.Representation.VerifiedAt);
        Assert.Null(result.Representation.VerifiedBy);
    }

    [Theory]
    [InlineData("WSFE_600")]
    [InlineData("WSFE_601")]
    [InlineData("POINT_OF_SALE_NOT_AUTHORIZED")]
    [InlineData("POINT_OF_SALE_BLOCKED")]
    [InlineData("POINT_OF_SALE_DISABLED")]
    [InlineData("POINT_OF_SALE_NOT_CAE")]
    public async Task RechazoConcluyente_PersisteActionRequired(string code)
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        var policy = new FiscalRepresentationLifecyclePolicy(store);
        var probe = Probe(dcPointOfSaleProbeStatus.NotVerified, code, "rechazo seguro");

        var result = await policy.ApplyAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale,
            ProbeIdentity(),
            probe, "admin");

        Assert.Equal(FiscalRepresentationLifecycleDisposition.ActionRequired, result.Disposition);
        Assert.Equal(code, result.Code);
        Assert.Equal(FiscalRepresentationStatus.ActionRequired, result.Representation.Status);
        Assert.Equal(code, result.Representation.ActionRequiredReasonCode);
        Assert.False(result.Representation.CanReadOrEmit);
        Assert.Null(result.VerificationEvidence);
    }

    [Theory]
    [InlineData("WSFE_RESPONSE_INVALID")]
    [InlineData("WSFE_ACCESS_NOT_VERIFIED")]
    [InlineData("WSAA_AUTHORIZATION_NOT_VERIFIED")]
    [InlineData("WSFE_999")]
    public async Task FalloNoConcluyente_NoPersisteRevocacionNiActionRequired(string code)
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        var policy = new FiscalRepresentationLifecyclePolicy(store);
        var probe = Probe(dcPointOfSaleProbeStatus.NotVerified, code, "diagnostico transitorio");

        var result = await policy.ApplyAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale,
            ProbeIdentity(),
            probe, "admin");

        Assert.Equal(FiscalRepresentationLifecycleDisposition.Inconclusive, result.Disposition);
        Assert.Equal(code, result.Code);
        Assert.Equal(FiscalRepresentationStatus.Pending, result.Representation.Status);
        Assert.Null(result.Representation.ActionRequiredReasonCode);
        Assert.Null(result.VerificationEvidence);
    }

    [Fact]
    public async Task RepresentacionActiva_FalloNoConcluyente_ConservaActive()
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        await store.MarkRepresentationVerifiedAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale,
            "FEParamGetPtosVenta|existing", "admin");
        await store.ActivateRepresentationAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale, "admin");

        var policy = new FiscalRepresentationLifecyclePolicy(store);
        var result = await policy.ApplyAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale,
            ProbeIdentity(),
            Probe(dcPointOfSaleProbeStatus.NotVerified,
                "WSFE_ACCESS_NOT_VERIFIED", "network"),
            "admin");

        Assert.Equal(FiscalRepresentationLifecycleDisposition.Inconclusive, result.Disposition);
        Assert.Equal(FiscalRepresentationStatus.Active, result.Representation.Status);
        Assert.True(result.Representation.CanReadOrEmit);
    }

    [Fact]
    public async Task RepresentacionRevocada_NoEsRevalidable()
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        await store.RevokeRepresentationAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale, "security");

        var policy = new FiscalRepresentationLifecyclePolicy(store);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            policy.ApplyAsync(
                ContextId, ConsumerId, RepresentedCuit, PointOfSale,
                ProbeIdentity(),
                Probe(dcPointOfSaleProbeStatus.Verified,
                    "POINT_OF_SALE_AUTHORIZATION_VERIFIED", "ok"),
                "admin"));

        Assert.Equal("FISCAL_REPRESENTATION_STATE_INVALID", error.Message);
    }

    [Fact]
    public async Task RotacionDespuesDeLeerContextoPeroAntesDePersistir_FallaCerrado()
    {
        using var temp = new TempStore();
        var durable = await PendingCandidateAsync(temp.Root);
        var rotating = new RotateAfterContextReadStore(durable);
        var policy = new FiscalRepresentationLifecyclePolicy(rotating);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            policy.ApplyAsync(
                ContextId,
                ConsumerId,
                RepresentedCuit,
                PointOfSale,
                ProbeIdentity(),
                Probe(
                    dcPointOfSaleProbeStatus.Verified,
                    "POINT_OF_SALE_AUTHORIZATION_VERIFIED",
                    "probe rev-1"),
                "admin"));

        Assert.Equal("POINT_OF_SALE_PROBE_IDENTITY_MISMATCH", error.Message);

        var representation = await durable.GetRepresentationAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale);
        Assert.NotNull(representation);
        Assert.Equal(FiscalRepresentationStatus.Pending, representation!.Status);
        Assert.Null(representation.VerificationEvidence);

        var context = await durable.GetContextAsync(ContextId);
        Assert.Equal("rev-2", context?.ActiveAssignment?.AssignmentRevision);
    }

    [Fact]
    public async Task ProbeDeAssignmentAnterior_SeRechazaTrasRotacionSinMutarRepresentacion()
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        var policy = new FiscalRepresentationLifecyclePolicy(store);
        var probeIdentity = new FiscalRepresentationProbeIdentity(
            ContextId,
            "homologacion",
            RepresentedCuit,
            PointOfSale,
            "rev-1");

        await store.AddCandidateAssignmentAsync(
            ContextId, "rev-2", "cred-operador-2", "rotation");
        await store.MarkAssignmentValidatedAsync(
            ContextId, "rev-2", "cert-and-wsaa-ok-2", "rotation");
        await store.ActivateAssignmentAsync(ContextId, "rev-2", "rotation");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            policy.ApplyAsync(
                ContextId,
                ConsumerId,
                RepresentedCuit,
                PointOfSale,
                probeIdentity,
                Probe(
                    dcPointOfSaleProbeStatus.Verified,
                    "POINT_OF_SALE_AUTHORIZATION_VERIFIED",
                    "probe ejecutado con rev-1"),
                "admin"));

        Assert.Equal("POINT_OF_SALE_PROBE_IDENTITY_MISMATCH", error.Message);
        var durable = await store.GetRepresentationAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale);
        Assert.NotNull(durable);
        Assert.Equal(FiscalRepresentationStatus.Pending, durable!.Status);
        Assert.Null(durable.VerificationEvidence);
    }

    [Fact]
    public async Task ProbeDeMismoPvPeroOtroCuit_SeRechazaSinMutarRepresentacion()
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        var policy = new FiscalRepresentationLifecyclePolicy(store);
        var probeIdentity = new FiscalRepresentationProbeIdentity(
            ContextId,
            "homologacion",
            RepresentedCuit + 1,
            PointOfSale,
            "rev-1");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            policy.ApplyAsync(
                ContextId,
                ConsumerId,
                RepresentedCuit,
                PointOfSale,
                probeIdentity,
                Probe(
                    dcPointOfSaleProbeStatus.Verified,
                    "POINT_OF_SALE_AUTHORIZATION_VERIFIED",
                    "mismo PV, otro CUIT"),
                "admin"));

        Assert.Equal("POINT_OF_SALE_PROBE_IDENTITY_MISMATCH", error.Message);
        var durable = await store.GetRepresentationAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale);
        Assert.NotNull(durable);
        Assert.Equal(FiscalRepresentationStatus.Pending, durable!.Status);
        Assert.Null(durable.VerificationEvidence);
    }

    [Fact]
    public async Task ProbeDeOtroPv_EsRechazadoSinMutarRepresentacion()
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        var policy = new FiscalRepresentationLifecyclePolicy(store);
        var mismatched = new dcPointOfSaleAccessProbeResult(
            dcPointOfSaleProbeStatus.Verified,
            "POINT_OF_SALE_AUTHORIZATION_VERIFIED",
            "ok",
            PointOfSale + 1,
            CheckedAt,
            "CAE");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            policy.ApplyAsync(
                ContextId, ConsumerId, RepresentedCuit, PointOfSale,
                ProbeIdentity(),
                mismatched, "admin"));

        Assert.Equal("POINT_OF_SALE_PROBE_IDENTITY_MISMATCH", error.Message);
        var durable = await store.GetRepresentationAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale);
        Assert.Equal(FiscalRepresentationStatus.Pending, durable?.Status);
    }

    [Fact]
    public async Task EvidenciaNuncaIncluyeSafeMessageTokenSignNiXml()
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        var policy = new FiscalRepresentationLifecyclePolicy(store);
        var probe = Probe(
            dcPointOfSaleProbeStatus.Verified,
            "POINT_OF_SALE_AUTHORIZATION_VERIFIED",
            "token=secret sign=secret <xml>private</xml>");

        var result = await policy.ApplyAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale,
            ProbeIdentity(),
            probe, "admin");

        Assert.NotNull(result.VerificationEvidence);
        Assert.DoesNotContain("token", result.VerificationEvidence, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sign", result.VerificationEvidence, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("xml", result.VerificationEvidence, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result.VerificationEvidence, StringComparison.OrdinalIgnoreCase);
    }

    private static FiscalRepresentationProbeIdentity ProbeIdentity(
        string assignmentRevision = "rev-1",
        long representedCuit = RepresentedCuit,
        int pointOfSale = PointOfSale,
        string environment = "homologacion")
        => new(
            ContextId,
            environment,
            representedCuit,
            pointOfSale,
            assignmentRevision);

    private static dcPointOfSaleAccessProbeResult Probe(
        dcPointOfSaleProbeStatus status,
        string code,
        string safeMessage)
        => new(
            status,
            code,
            safeMessage,
            PointOfSale,
            CheckedAt,
            "CAE");

    private static async Task<FileSystemFiscalTechnicalContextStore> PendingCandidateAsync(
        string root)
    {
        var store = new FileSystemFiscalTechnicalContextStore(root);
        await store.AddContextAsync(new FiscalTechnicalContextRecord(
            ContextId,
            "homologacion",
            FiscalContextOperationalState.Disabled,
            1,
            []));
        await store.AddCandidateAssignmentAsync(
            ContextId, "rev-1", "cred-operador", "admin");
        await store.MarkAssignmentValidatedAsync(
            ContextId, "rev-1", "cert-and-wsaa-ok", "admin");
        await store.ActivateAssignmentAsync(ContextId, "rev-1", "admin");
        await store.SetContextStateAsync(
            ContextId, FiscalContextOperationalState.Active, "admin");
        await store.RegisterCandidateAsync(
            ContextId, ConsumerId, RepresentedCuit, "admin");
        await store.SelectPointOfSaleAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale, "admin");
        return store;
    }

    private sealed class RotateAfterContextReadStore(
        FileSystemFiscalTechnicalContextStore inner) : IFiscalTechnicalContextStore
    {
        private int _rotated;

        public Task<IReadOnlyList<FiscalTechnicalContextRecord>> ListContextsAsync(
            CancellationToken cancellationToken = default)
            => inner.ListContextsAsync(cancellationToken);

        public async Task<FiscalTechnicalContextRecord?> GetContextAsync(
            string contextId,
            CancellationToken cancellationToken = default)
        {
            var snapshot = await inner.GetContextAsync(contextId, cancellationToken);
            if (snapshot is not null
                && Interlocked.Exchange(ref _rotated, 1) == 0)
            {
                await inner.AddCandidateAssignmentAsync(
                    contextId, "rev-2", "cred-operador-2", "rotation", cancellationToken);
                await inner.MarkAssignmentValidatedAsync(
                    contextId, "rev-2", "cert-and-wsaa-ok-2", "rotation", cancellationToken);
                await inner.ActivateAssignmentAsync(
                    contextId, "rev-2", "rotation", cancellationToken);
            }
            return snapshot;
        }

        public Task AddContextAsync(
            FiscalTechnicalContextRecord context,
            CancellationToken cancellationToken = default)
            => inner.AddContextAsync(context, cancellationToken);

        public Task AddCandidateAssignmentAsync(
            string contextId, string revision, string credentialId, string actor,
            CancellationToken cancellationToken = default)
            => inner.AddCandidateAssignmentAsync(
                contextId, revision, credentialId, actor, cancellationToken);

        public Task MarkAssignmentValidatedAsync(
            string contextId, string revision, string evidence, string actor,
            CancellationToken cancellationToken = default)
            => inner.MarkAssignmentValidatedAsync(
                contextId, revision, evidence, actor, cancellationToken);

        public Task ActivateAssignmentAsync(
            string contextId, string revision, string actor,
            CancellationToken cancellationToken = default)
            => inner.ActivateAssignmentAsync(contextId, revision, actor, cancellationToken);

        public Task DisableAssignmentAsync(
            string contextId, string revision, string actor,
            CancellationToken cancellationToken = default)
            => inner.DisableAssignmentAsync(contextId, revision, actor, cancellationToken);

        public Task SetContextStateAsync(
            string contextId, FiscalContextOperationalState state, string actor,
            CancellationToken cancellationToken = default)
            => inner.SetContextStateAsync(contextId, state, actor, cancellationToken);

        public Task RegisterCandidateAsync(
            string contextId, string consumerId, long representedCuit, string actor,
            CancellationToken cancellationToken = default)
            => inner.RegisterCandidateAsync(
                contextId, consumerId, representedCuit, actor, cancellationToken);

        public Task<IReadOnlyList<FiscalRepresentationRecord>> ListRepresentationsAsync(
            string contextId,
            CancellationToken cancellationToken = default)
            => inner.ListRepresentationsAsync(contextId, cancellationToken);

        public Task<FiscalRepresentationRecord?> GetRepresentationAsync(
            string contextId, string consumerId, long representedCuit, int pointOfSale,
            CancellationToken cancellationToken = default)
            => inner.GetRepresentationAsync(
                contextId, consumerId, representedCuit, pointOfSale, cancellationToken);

        public Task SelectPointOfSaleAsync(
            string contextId, string consumerId, long representedCuit, int pointOfSale,
            string actor, CancellationToken cancellationToken = default)
            => inner.SelectPointOfSaleAsync(
                contextId, consumerId, representedCuit, pointOfSale, actor, cancellationToken);

        public Task MarkRepresentationVerifiedAsync(
            string contextId, string consumerId, long representedCuit, int pointOfSale,
            string evidence, string actor, CancellationToken cancellationToken = default)
            => inner.MarkRepresentationVerifiedAsync(
                contextId, consumerId, representedCuit, pointOfSale,
                evidence, actor, cancellationToken);

        public Task ActivateRepresentationAsync(
            string contextId, string consumerId, long representedCuit, int pointOfSale,
            string actor, CancellationToken cancellationToken = default)
            => inner.ActivateRepresentationAsync(
                contextId, consumerId, representedCuit, pointOfSale,
                actor, cancellationToken);

        public Task RevokeRepresentationAsync(
            string contextId, string consumerId, long representedCuit, int pointOfSale,
            string actor, CancellationToken cancellationToken = default)
            => inner.RevokeRepresentationAsync(
                contextId, consumerId, representedCuit, pointOfSale,
                actor, cancellationToken);

        public Task MarkRepresentationActionRequiredAsync(
            string contextId, string consumerId, long representedCuit, int pointOfSale,
            string reasonCode, string actor,
            CancellationToken cancellationToken = default)
            => inner.MarkRepresentationActionRequiredAsync(
                contextId, consumerId, representedCuit, pointOfSale,
                reasonCode, actor, cancellationToken);
    }

    private sealed class TempStore : IDisposable
    {
        public string Root { get; } = Path.Combine(
            Path.GetTempPath(),
            "dcarca-lifecycle-policy-" + Guid.NewGuid().ToString("N"));

        public TempStore()
            => Directory.CreateDirectory(Root);

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
