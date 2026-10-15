using dcArca.Core.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace dcArca.McpServer.Tests;

public sealed class FiscalPrincipalCuitReadinessTests
{
    [Fact]
    public async Task ActiveCredential_WithoutPrincipalCuit_IsNotReady()
    {
        using var temp = new TempCatalog();
        await SeedActiveContextAsync(temp.Path);

        var readiness = new FiscalLocalReadinessValidator(
            OpenEmissionRecoveryGate.Instance,
            temp.Path,
            Configuration(principalCuit: null));

        var result = await readiness.ValidateAsync();

        Assert.False(result.Ready);
        Assert.Equal("CREDENTIAL_PRINCIPAL_CUIT_REQUIRED", result.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2012345678")]
    [InlineData("201234567890")]
    [InlineData("20A23456789")]
    [InlineData(" 20123456789 ")]
    public async Task ActiveCredential_WithInvalidPrincipalCuit_IsNotReady(string principalCuit)
    {
        using var temp = new TempCatalog();
        await SeedActiveContextAsync(temp.Path);

        var readiness = new FiscalLocalReadinessValidator(
            OpenEmissionRecoveryGate.Instance,
            temp.Path,
            Configuration(principalCuit));

        var result = await readiness.ValidateAsync();

        Assert.False(result.Ready);
        Assert.Equal("CREDENTIAL_PRINCIPAL_CUIT_INVALID", result.Code);
    }

    [Fact]
    public async Task ActiveCredential_WithValidPrincipalCuit_IsReady()
    {
        using var temp = new TempCatalog();
        await SeedActiveContextAsync(temp.Path);

        var readiness = new FiscalLocalReadinessValidator(
            OpenEmissionRecoveryGate.Instance,
            temp.Path,
            Configuration("20123456789"));

        var result = await readiness.ValidateAsync();

        Assert.True(result.Ready);
        Assert.Null(result.Code);
    }

    private static IConfiguration Configuration(string? principalCuit)
    {
        var values = new Dictionary<string, string?>
        {
            ["FiscalCredentials:cred-root:CertificatePath"] = "/tmp/test-placeholder.pfx",
            ["FiscalEnvironments:homologacion:WsaaUrl"] = "https://example.invalid/wsaa",
            ["FiscalEnvironments:homologacion:WsfeUrl"] = "https://example.invalid/wsfe",
            ["FiscalEnvironments:homologacion:PadronUrl"] = "https://example.invalid/padron"
        };
        if (principalCuit is not null)
            values["FiscalCredentials:cred-root:PrincipalCuit"] = principalCuit;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static async Task SeedActiveContextAsync(string path)
    {
        var store = new FileSystemFiscalTechnicalContextStore(path);
        var now = DateTimeOffset.UtcNow;
        await store.AddContextAsync(new FiscalTechnicalContextRecord(
            "operator-homo",
            "homologacion",
            FiscalContextOperationalState.Active,
            1,
            [new CredentialAssignmentRecord(
                "rev-1",
                "cred-root",
                CredentialAssignmentStatus.Active,
                "wsaa-ok",
                now,
                ActivatedAt: now,
                Actor: "test")]));
    }

    private sealed class TempCatalog : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "dcarca-readiness-principal-tests-" + Guid.NewGuid().ToString("N"));

        public TempCatalog() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
