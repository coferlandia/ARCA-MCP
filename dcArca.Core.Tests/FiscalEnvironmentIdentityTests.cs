using dcArca.Core.Models;
using dcArca.Core.Services;
using Microsoft.Extensions.Configuration;
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

    [Fact]
    public void BindingResolver_RechazaEnvironmentPersistidoNoCanonicoAunqueExistaConfigAlias()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FiscalCredentials:cred-a:CertificatePath"] = "cred-a.pfx",
                ["FiscalEnvironments:HOMOLOGACION:WsaaUrl"] = "https://upper.example/wsaa",
                ["FiscalEnvironments:HOMOLOGACION:WsfeUrl"] = "https://upper.example/wsfe",
                ["FiscalEnvironments:HOMOLOGACION:PadronUrl"] = "https://upper.example/padron"
            })
            .Build();
        var resolver = new FiscalCredentialHostBindingResolver(
            configuration,
            new dcArcaConfig(),
            "homologacion");
        var assignment = new CredentialAssignmentRecord(
            "rev-1",
            "cred-a",
            CredentialAssignmentStatus.Active,
            "validated",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "admin");
        var context = new RepresentedFiscalContextRecord(
            "ctx-a",
            "HOMOLOGACION",
            20123456786,
            7,
            FiscalContextOperationalState.Active,
            1,
            false,
            [assignment]);

        var error = Assert.Throws<InvalidDataException>(() =>
            resolver.Resolve(context, assignment));

        Assert.Contains("FISCAL_ENVIRONMENT_NOT_CANONICAL", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BindingResolver_ResuelveSeccionCanonica()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FiscalCredentials:cred-a:CertificatePath"] = "cred-a.pfx",
                ["FiscalCredentials:cred-a:PrincipalCuit"] = "20123456786",
                ["FiscalEnvironments:homologacion:WsaaUrl"] = "https://homo.example/wsaa",
                ["FiscalEnvironments:homologacion:WsfeUrl"] = "https://homo.example/wsfe",
                ["FiscalEnvironments:homologacion:PadronUrl"] = "https://homo.example/padron"
            })
            .Build();
        var resolver = new FiscalCredentialHostBindingResolver(
            configuration,
            new dcArcaConfig(),
            "homologacion");
        var assignment = new CredentialAssignmentRecord(
            "rev-1",
            "cred-a",
            CredentialAssignmentStatus.Active,
            "validated",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "admin");
        var context = new RepresentedFiscalContextRecord(
            "ctx-a",
            "homologacion",
            20123456786,
            7,
            FiscalContextOperationalState.Active,
            1,
            false,
            [assignment]);

        var binding = resolver.Resolve(context, assignment);

        Assert.Equal("homologacion", binding.Config.Environment);
        Assert.Equal("homologacion:cred-a", binding.CacheIdentity);
        Assert.Equal("https://homo.example/wsfe", binding.Config.WsfeUrl);
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
