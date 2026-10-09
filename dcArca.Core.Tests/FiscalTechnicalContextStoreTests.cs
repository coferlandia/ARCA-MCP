using dcArca.Core.Services;
using Xunit;

namespace dcArca.Core.Tests;

public sealed class FiscalTechnicalContextStoreTests
{
    [Fact]
    public async Task UnaCredencialTecnica_PermiteDosCuitDistintos_EnUnSoloContextId()
    {
        using var temp = new TempStore();
        var store = new FileSystemFiscalTechnicalContextStore(temp.Root);
        await ProvisionContextAsync(store);
        await store.RegisterCandidateAsync("operator-homo", "secretaria", 20123456786, "admin");
        await store.RegisterCandidateAsync("operator-homo", "secretaria", 30712345678, "admin");
        await ActivateAsync(store, 20123456786, 7);
        await ActivateAsync(store, 30712345678, 14);

        var context = Assert.Single(await store.ListContextsAsync());
        Assert.Equal("operator-homo", context.ContextId);
        Assert.Equal("cred-operador", context.ActiveAssignment!.CredentialId);

        var reps = await store.ListRepresentationsAsync("operator-homo");
        Assert.Equal(2, reps.Count);
        Assert.All(reps, rep => Assert.True(rep.CanReadOrEmit));
        Assert.Contains(reps, rep => rep.RepresentedCuit == 20123456786 && rep.PointOfSale == 7);
        Assert.Contains(reps, rep => rep.RepresentedCuit == 30712345678 && rep.PointOfSale == 14);
    }

    [Fact]
    public async Task RepresentacionSinProbeValidado_NoPuedeActivarse()
    {
        using var temp = new TempStore();
        var store = new FileSystemFiscalTechnicalContextStore(temp.Root);
        await ProvisionContextAsync(store);
        await store.RegisterCandidateAsync("operator-homo", "secretaria", 20123456786, "admin");
        await store.SelectPointOfSaleAsync("operator-homo", "secretaria", 20123456786, 7, "admin");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ActivateRepresentationAsync("operator-homo", "secretaria", 20123456786, 7, "admin"));

        Assert.Equal("FISCAL_REPRESENTATION_NOT_VERIFIED", exception.Message);
        Assert.False((await store.GetRepresentationAsync("operator-homo", "secretaria", 20123456786, 7))!.CanReadOrEmit);
    }

    [Fact]
    public async Task Revoke_DejaConsumidorAislado_YConservaEvidencia()
    {
        using var temp = new TempStore();
        var store = new FileSystemFiscalTechnicalContextStore(temp.Root);
        await ProvisionContextAsync(store);
        await store.RegisterCandidateAsync("operator-homo", "consumer-a", 20123456786, "admin");
        await store.RegisterCandidateAsync("operator-homo", "consumer-b", 30712345678, "admin");
        await ActivateAsync(store, 20123456786, 7, "consumer-a");
        await ActivateAsync(store, 30712345678, 14, "consumer-b");
        await store.RevokeRepresentationAsync("operator-homo", "consumer-a", 20123456786, 7, "security");

        var restarted = new FileSystemFiscalTechnicalContextStore(temp.Root);
        var a = await restarted.GetRepresentationAsync("operator-homo", "consumer-a", 20123456786, 7);
        var b = await restarted.GetRepresentationAsync("operator-homo", "consumer-b", 30712345678, 14);
        Assert.False(a!.CanReadOrEmit);
        Assert.NotNull(a.VerificationEvidence);
        Assert.Equal("security", a.RevokedBy);
        Assert.True(b!.CanReadOrEmit);
        Assert.Null(await restarted.GetRepresentationAsync("operator-homo", "consumer-b", 20123456786, 7));
    }

    [Fact]
    public async Task UnMismoCuitPuedeTenerVariosPvYReautorizarUnoRevocado()
    {
        using var temp = new TempStore();
        var store = new FileSystemFiscalTechnicalContextStore(temp.Root);
        await ProvisionContextAsync(store);
        await store.RegisterCandidateAsync("operator-homo", "secretaria", 20123456786, "admin");
        await ActivateAsync(store, 20123456786, 7);
        await store.RegisterCandidateAsync("operator-homo", "secretaria", 20123456786, "admin");
        await ActivateAsync(store, 20123456786, 14);
        Assert.Equal(2, (await store.ListRepresentationsAsync("operator-homo")).Count(x => x.CanReadOrEmit));

        await store.RevokeRepresentationAsync("operator-homo", "secretaria", 20123456786, 7, "admin");
        await store.RegisterCandidateAsync("operator-homo", "secretaria", 20123456786, "admin");
        await ActivateAsync(store, 20123456786, 7);
        Assert.True((await store.GetRepresentationAsync("operator-homo", "secretaria", 20123456786, 7))!.CanReadOrEmit);
        Assert.Contains(await store.ListRepresentationsAsync("operator-homo"), x =>
            x.RepresentedCuit == 20123456786 && x.PointOfSale == 7 && x.Status == FiscalRepresentationStatus.Revoked);
    }

    [Fact]
    public async Task WSFERevocado_BloqueaEmisionesHastaRevalidarLaRepresentacion()
    {
        using var temp = new TempStore();
        var store = new FileSystemFiscalTechnicalContextStore(temp.Root);
        await ProvisionContextAsync(store);
        await store.RegisterCandidateAsync("operator-homo", "secretaria", 20123456786, "admin");
        await ActivateAsync(store, 20123456786, 7);

        await store.MarkRepresentationActionRequiredAsync(
            "operator-homo", "secretaria", 20123456786, 7, "WSFE_601", "ARCA-WSFE");
        var suspended = (await store.GetRepresentationAsync("operator-homo", "secretaria", 20123456786, 7))!;
        Assert.False(suspended.CanReadOrEmit);
        Assert.Equal(FiscalRepresentationStatus.ActionRequired, suspended.Status);
        Assert.Equal("WSFE_601", suspended.ActionRequiredReasonCode);

        await store.MarkRepresentationVerifiedAsync(
            "operator-homo", "secretaria", 20123456786, 7, "new-remote-evidence", "admin");
        await store.ActivateRepresentationAsync("operator-homo", "secretaria", 20123456786, 7, "admin");
        Assert.True((await store.GetRepresentationAsync("operator-homo", "secretaria", 20123456786, 7))!.CanReadOrEmit);
    }

    [Fact]
    public async Task PersistenciaV1_ExigeResetExplicito_YNuncaBootstrapImplicito()
    {
        using var temp = new TempStore();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "contexts.json"), "[]");
        var store = new FileSystemFiscalTechnicalContextStore(temp.Root);
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => store.ListContextsAsync());
        Assert.Equal("FISCAL_CATALOG_V1_RESET_REQUIRED", exception.Message);
        Assert.False(File.Exists(Path.Combine(temp.Root, "fiscal-catalog-v2.json")));
    }

    [Fact]
    public async Task CatalogoV2_PersisteConPermisosRestrictivosEnUnix()
    {
        using var temp = new TempStore();
        var store = new FileSystemFiscalTechnicalContextStore(temp.Root);
        await store.AddContextAsync(new FiscalTechnicalContextRecord("ctx-safe", "homologacion",
            FiscalContextOperationalState.Disabled, 1, []));

        if (OperatingSystem.IsWindows()) return;
        var expectedDir = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        var expectedFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.Equal(expectedDir, File.GetUnixFileMode(temp.Root));
        Assert.Equal(expectedFile, File.GetUnixFileMode(Path.Combine(temp.Root, "fiscal-catalog-v2.json")));
    }

    [Fact]
    public async Task RotacionCredencial_PreservaRevisionHistorica()
    {
        using var temp = new TempStore();
        var store = new FileSystemFiscalTechnicalContextStore(temp.Root);
        await ProvisionContextAsync(store);
        await store.AddCandidateAssignmentAsync("operator-homo", "rev-2", "cred-nueva", "admin");
        await store.MarkAssignmentValidatedAsync("operator-homo", "rev-2", "cert-and-wsaa-ok", "admin");
        await store.ActivateAssignmentAsync("operator-homo", "rev-2", "admin");
        var result = (await store.GetContextAsync("operator-homo"))!;
        Assert.Equal("rev-2", result.ActiveAssignment!.AssignmentRevision);
        Assert.Equal(CredentialAssignmentStatus.Historical,
            result.Assignments.Single(x => x.AssignmentRevision == "rev-1").Status);
    }

    private static async Task ProvisionContextAsync(FileSystemFiscalTechnicalContextStore store)
    {
        await store.AddContextAsync(new FiscalTechnicalContextRecord("operator-homo", "homologacion",
            FiscalContextOperationalState.Disabled, 1, []));
        await store.AddCandidateAssignmentAsync("operator-homo", "rev-1", "cred-operador", "admin");
        await store.MarkAssignmentValidatedAsync("operator-homo", "rev-1", "cert-and-wsaa-ok", "admin");
        await store.ActivateAssignmentAsync("operator-homo", "rev-1", "admin");
        await store.SetContextStateAsync("operator-homo", FiscalContextOperationalState.Active, "admin");
    }

    private static async Task ActivateAsync(FileSystemFiscalTechnicalContextStore store, long cuit, int pv, string consumer = "secretaria")
    {
        await store.SelectPointOfSaleAsync("operator-homo", consumer, cuit, pv, "admin");
        await store.MarkRepresentationVerifiedAsync("operator-homo", consumer, cuit, pv,
            "FEParamGetPtosVenta|eligible=true", "validator");
        await store.ActivateRepresentationAsync("operator-homo", consumer, cuit, pv, "admin");
    }

    private sealed class TempStore : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "dcarca-fiscal-v2-" + Guid.NewGuid().ToString("N"));

        public TempStore() => Directory.CreateDirectory(Root);

        public void Dispose()
        {
            try { Directory.Delete(Root, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
