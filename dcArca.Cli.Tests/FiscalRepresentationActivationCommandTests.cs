using dcArca.Cli;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.Cli.Tests;

public sealed class FiscalRepresentationActivationCommandTests
{
    private const string ContextId = "operator-homo";
    private const string ConsumerId = "secretaria";
    private const long RepresentedCuit = 20123456786;
    private const int PointOfSale = 7;

    [Fact]
    public async Task Verified_ActivaYDevuelveExitCodeCero()
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        var command = Command(
            store,
            Probe(
                dcPointOfSaleProbeStatus.Verified,
                "POINT_OF_SALE_AUTHORIZATION_VERIFIED"));

        var result = await command.ExecuteAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale, "cli-admin");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(FiscalRepresentationLifecycleDisposition.Activated, result.Disposition);
        Assert.Equal(FiscalRepresentationStatus.Active, result.Representation.Status);
        Assert.StartsWith("FEParamGetPtosVenta|", result.Representation.VerificationEvidence);
    }

    [Fact]
    public async Task Manual602_ActivaSinFingirVerificacionRemota()
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        var command = Command(
            store,
            Probe(
                dcPointOfSaleProbeStatus.ManualUnverified,
                "POINT_OF_SALE_MANUAL_UNVERIFIED"));

        var result = await command.ExecuteAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale, "cli-admin");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(FiscalRepresentationStatus.Active, result.Representation.Status);
        Assert.StartsWith("MANUAL_PV_UNVERIFIED_602|", result.Representation.VerificationEvidence);
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
    public async Task DenegacionConcluyente_PersisteActionRequiredYDevuelveExitDos(
        string code)
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        var command = Command(
            store,
            Probe(dcPointOfSaleProbeStatus.NotVerified, code));

        var result = await command.ExecuteAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale, "cli-admin");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(FiscalRepresentationLifecycleDisposition.ActionRequired, result.Disposition);
        Assert.Equal(code, result.Code);
        Assert.Equal(FiscalRepresentationStatus.ActionRequired, result.Representation.Status);
        Assert.Equal(code, result.Representation.ActionRequiredReasonCode);
        Assert.False(result.Representation.CanReadOrEmit);
    }

    [Fact]
    public async Task FalloInconcluyenteSobreActive_NoDegradaLifecycleYDevuelveExitDos()
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        await store.MarkRepresentationVerifiedAsync(
            ContextId,
            ConsumerId,
            RepresentedCuit,
            PointOfSale,
            "FEParamGetPtosVenta|existing",
            "setup");
        await store.ActivateRepresentationAsync(
            ContextId,
            ConsumerId,
            RepresentedCuit,
            PointOfSale,
            "setup");

        var command = Command(
            store,
            Probe(
                dcPointOfSaleProbeStatus.NotVerified,
                "WSFE_ACCESS_NOT_VERIFIED"));

        var result = await command.ExecuteAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale, "cli-admin");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(FiscalRepresentationLifecycleDisposition.Inconclusive, result.Disposition);
        Assert.Equal(FiscalRepresentationStatus.Active, result.Representation.Status);
        Assert.True(result.Representation.CanReadOrEmit);
        Assert.Null(result.Representation.ActionRequiredReasonCode);
    }

    [Fact]
    public async Task RotacionLuegoDelProbe_FallaCerradoSinPersistirEvidenciaVieja()
    {
        using var temp = new TempStore();
        var store = await PendingCandidateAsync(temp.Root);
        var executor = new RotatingProbeExecutor(
            store,
            Probe(
                dcPointOfSaleProbeStatus.Verified,
                "POINT_OF_SALE_AUTHORIZATION_VERIFIED"));
        var command = new FiscalRepresentationActivationCommand(
            store,
            new FiscalRepresentationLifecyclePolicy(store),
            executor);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            command.ExecuteAsync(
                ContextId, ConsumerId, RepresentedCuit, PointOfSale, "cli-admin"));

        Assert.Equal("POINT_OF_SALE_PROBE_IDENTITY_MISMATCH", error.Message);
        var durable = await store.GetRepresentationAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale);
        Assert.NotNull(durable);
        Assert.Equal(FiscalRepresentationStatus.Pending, durable!.Status);
        Assert.Null(durable.VerificationEvidence);
    }

    private static FiscalRepresentationActivationCommand Command(
        IFiscalTechnicalContextStore store,
        dcPointOfSaleAccessProbeResult probe)
        => new(
            store,
            new FiscalRepresentationLifecyclePolicy(store),
            new StaticProbeExecutor(probe));

    private static dcPointOfSaleAccessProbeResult Probe(
        dcPointOfSaleProbeStatus status,
        string code)
        => new(
            status,
            code,
            code,
            PointOfSale,
            new DateTimeOffset(2026, 10, 11, 12, 34, 56, TimeSpan.Zero),
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
            ContextId, "rev-1", "cred-1", "setup");
        await store.MarkAssignmentValidatedAsync(
            ContextId, "rev-1", "validated", "setup");
        await store.ActivateAssignmentAsync(
            ContextId, "rev-1", "setup");
        await store.SetContextStateAsync(
            ContextId, FiscalContextOperationalState.Active, "setup");
        await store.RegisterCandidateAsync(
            ContextId, ConsumerId, RepresentedCuit, "setup");
        await store.SelectPointOfSaleAsync(
            ContextId, ConsumerId, RepresentedCuit, PointOfSale, "setup");
        return store;
    }

    private sealed class StaticProbeExecutor(
        dcPointOfSaleAccessProbeResult probe)
        : IFiscalRepresentationProbeExecutor
    {
        public Task<FiscalRepresentationProbeOutcome> ProbeAsync(
            FiscalTechnicalContextRecord context,
            CredentialAssignmentRecord assignment,
            long representedCuit,
            int pointOfSale,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                new FiscalRepresentationProbeOutcome(
                    new FiscalRepresentationProbeIdentity(
                        context.ContextId,
                        context.Environment,
                        representedCuit,
                        pointOfSale,
                        assignment.AssignmentRevision),
                    probe));
    }

    private sealed class RotatingProbeExecutor(
        FileSystemFiscalTechnicalContextStore store,
        dcPointOfSaleAccessProbeResult probe)
        : IFiscalRepresentationProbeExecutor
    {
        public async Task<FiscalRepresentationProbeOutcome> ProbeAsync(
            FiscalTechnicalContextRecord context,
            CredentialAssignmentRecord assignment,
            long representedCuit,
            int pointOfSale,
            CancellationToken cancellationToken = default)
        {
            var outcome = new FiscalRepresentationProbeOutcome(
                new FiscalRepresentationProbeIdentity(
                    context.ContextId,
                    context.Environment,
                    representedCuit,
                    pointOfSale,
                    assignment.AssignmentRevision),
                probe);

            await store.AddCandidateAssignmentAsync(
                context.ContextId, "rev-2", "cred-2", "rotation", cancellationToken);
            await store.MarkAssignmentValidatedAsync(
                context.ContextId, "rev-2", "validated-2", "rotation", cancellationToken);
            await store.ActivateAssignmentAsync(
                context.ContextId, "rev-2", "rotation", cancellationToken);
            return outcome;
        }
    }

    private sealed class TempStore : IDisposable
    {
        public string Root { get; } = Path.Combine(
            Path.GetTempPath(),
            "dcarca-cli-lifecycle-" + Guid.NewGuid().ToString("N"));

        public TempStore() => Directory.CreateDirectory(Root);

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
