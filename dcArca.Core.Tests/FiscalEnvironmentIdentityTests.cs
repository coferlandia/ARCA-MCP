using dcArca.Core.Services;
using Xunit;

namespace dcArca.Core.Tests;

public sealed class FiscalEnvironmentIdentityTests
{
    [Theory]
    [InlineData("homologacion", "homologacion")]
    [InlineData(" Homologacion ", "homologacion")]
    [InlineData("HOMOLOGACION", "homologacion")]
    [InlineData("produccion", "produccion")]
    [InlineData(" Produccion ", "produccion")]
    [InlineData("PRODUCCION", "produccion")]
    public void EntradaNueva_SeCanoniza(string raw, string expected)
        => Assert.Equal(expected, FiscalEnvironmentId.Normalize(raw));

    [Theory]
    [InlineData("")]
    [InlineData("homo")]
    [InlineData("prod")]
    [InlineData("testing")]
    [InlineData("homologación")]
    public void AliasDesconocido_FallaCerrado(string raw)
        => Assert.Throws<InvalidOperationException>(() => FiscalEnvironmentId.Normalize(raw));

    [Fact]
    public async Task AddContext_PersisteEnvironmentCanonico()
    {
        using var temp = new TempStore();
        var store = new FileSystemFiscalTechnicalContextStore(temp.Root);

        await store.AddContextAsync(new FiscalTechnicalContextRecord(
            "ctx-homo",
            " HOMOLOGACION ",
            FiscalContextOperationalState.Disabled,
            1,
            []));

        var persisted = await store.GetContextAsync("ctx-homo");
        Assert.NotNull(persisted);
        Assert.Equal("homologacion", persisted!.Environment);
    }

    [Fact]
    public async Task CatalogoPersistidoNoCanonico_FallaCerrado()
    {
        using var temp = new TempStore();
        await File.WriteAllTextAsync(
            Path.Combine(temp.Root, "fiscal-catalog-v2.json"),
            """
            {
              "SchemaVersion":2,
              "Contexts":[
                {
                  "ContextId":"ctx-homo",
                  "Environment":"HOMOLOGACION",
                  "OperationalState":2,
                  "ContextRevision":1,
                  "Assignments":[]
                }
              ],
              "Representations":[]
            }
            """);

        var store = new FileSystemFiscalTechnicalContextStore(temp.Root);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => store.ListContextsAsync());

        Assert.Contains("FISCAL_ENVIRONMENT_NOT_CANONICAL", error.Message, StringComparison.Ordinal);
    }

    private sealed class TempStore : IDisposable
    {
        public string Root { get; } = Path.Combine(
            Path.GetTempPath(),
            "dcarca-environment-tests-" + Guid.NewGuid().ToString("N"));

        public TempStore() => Directory.CreateDirectory(Root);

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
