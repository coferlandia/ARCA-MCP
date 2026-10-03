using dcArca.Core.Services;
using Xunit;

namespace dcArca.Core.Tests;

public class RepresentedFiscalContextStoreTests
{
    [Fact]
    public async Task Candidate_NoPuedeActivarseSinValidacion()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemRepresentedFiscalContextStore(temp.Path);
        await store.AddContextAsync(Context("ctx-a"));
        await store.AddCandidateAssignmentAsync("ctx-a", "personal-v1", "cred-personal", "admin");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ActivateAssignmentAsync("ctx-a", "personal-v1", "admin"));

        Assert.Equal("ASSIGNMENT_NOT_VALIDATED", exception.Message);
        Assert.Null((await store.GetAsync("ctx-a"))!.ActiveAssignment);
    }

    [Fact]
    public async Task ValidarYActivar_NuevaRevision_DejaLaAnteriorHistorica()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemRepresentedFiscalContextStore(temp.Path);
        await store.AddContextAsync(Context("ctx-a"));

        await store.AddCandidateAssignmentAsync("ctx-a", "personal-v1", "cred-personal", "admin");
        await store.MarkAssignmentValidatedAsync("ctx-a", "personal-v1", "evidence-personal", "validator");
        await store.ActivateAssignmentAsync("ctx-a", "personal-v1", "admin");

        await store.AddCandidateAssignmentAsync("ctx-a", "empresa-v2", "cred-empresa", "admin");
        await store.MarkAssignmentValidatedAsync("ctx-a", "empresa-v2", "evidence-empresa", "validator");
        await store.ActivateAssignmentAsync("ctx-a", "empresa-v2", "admin");

        var context = await store.GetAsync("ctx-a");
        Assert.NotNull(context);
        Assert.Equal("empresa-v2", context!.ActiveAssignment!.AssignmentRevision);
        Assert.Equal(CredentialAssignmentStatus.Historical,
            context.Assignments.Single(x => x.AssignmentRevision == "personal-v1").Status);
        Assert.Equal(CredentialAssignmentStatus.Active,
            context.Assignments.Single(x => x.AssignmentRevision == "empresa-v2").Status);
    }

    [Fact]
    public async Task DisableAssignment_NoHaceFallbackAutomaticoARevisionHistorica()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemRepresentedFiscalContextStore(temp.Path);
        await store.AddContextAsync(Context("ctx-a"));
        await store.AddCandidateAssignmentAsync("ctx-a", "v1", "cred-1", "admin");
        await store.MarkAssignmentValidatedAsync("ctx-a", "v1", "evidence", "validator");
        await store.ActivateAssignmentAsync("ctx-a", "v1", "admin");
        await store.DisableAssignmentAsync("ctx-a", "v1", "incident-response");

        var context = await store.GetAsync("ctx-a");
        Assert.NotNull(context);
        Assert.Null(context!.ActiveAssignment);
        Assert.Equal(CredentialAssignmentStatus.Disabled, Assert.Single(context.Assignments).Status);
    }

    [Fact]
    public async Task InitializeLegacy_SoloBootstrappeaStoreAusente()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemRepresentedFiscalContextStore(temp.Path);
        var first = Context("legacy") with
        {
            LegacyDefault = true,
            OperationalState = FiscalContextOperationalState.Active,
            Assignments =
            [
                new CredentialAssignmentRecord(
                    "legacy-v1", "legacy-default", CredentialAssignmentStatus.Active,
                    "bootstrap", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "bootstrap")
            ]
        };
        await store.InitializeLegacyAsync(first);
        await store.InitializeLegacyAsync(Context("should-not-appear"));

        var contexts = await store.ListAsync();
        var context = Assert.Single(contexts);
        Assert.Equal("legacy", context.ContextId);
    }

    [Fact]
    public async Task DosContextos_PuedenAliasarLaMismaSerieSinFusionarSuIdentidadAdministrativa()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemRepresentedFiscalContextStore(temp.Path);
        await store.AddContextAsync(Context("tenant-a"));
        await store.AddContextAsync(Context("tenant-b"));

        var contexts = await store.ListAsync();
        Assert.Equal(2, contexts.Count);
        Assert.Equal(contexts[0].Environment, contexts[1].Environment);
        Assert.Equal(contexts[0].RepresentedCuit, contexts[1].RepresentedCuit);
        Assert.Equal(contexts[0].PointOfSale, contexts[1].PointOfSale);
        Assert.NotEqual(contexts[0].ContextId, contexts[1].ContextId);
    }

    private static RepresentedFiscalContextRecord Context(string id)
        => new(
            id,
            "homologacion",
            20123456786,
            7,
            FiscalContextOperationalState.Disabled,
            1,
            false,
            []);

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "arca-context-store-tests-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
