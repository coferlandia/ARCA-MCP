using dcArca.Core.Services;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dcArca.McpServer.Tests;

public class McpHealthTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly TestCertificatePlaceholder _certificate = new();

    public McpHealthTests(WebApplicationFactory<Program> factory)
    {
        var contentRoot = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "dcArca.McpServer");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(contentRoot);
            builder.UseEnvironment("Testing");
            _certificate.Apply(builder);
        });
    }

    [Fact]
    public void ReadinessValidator_RecibePathDeCatalogoRuntimeExplicito()
    {
        Assert.Contains(
            typeof(FiscalLocalReadinessValidator).GetConstructors(),
            constructor => constructor.GetParameters().Any(
                parameter => parameter.ParameterType == typeof(string)
                    && parameter.Name == "catalogDirectory"));
    }

    [Theory]
    [InlineData("/health/live", "alive")]
    [InlineData("/health/ready", "ready")]
    public async Task HealthEndpoints_SonAnonimosYNoExponenSecretos(string path, string expectedStatus)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedStatus, body);
        Assert.DoesNotContain("20123456786", body);
        Assert.DoesNotContain("pfx", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Host_ResuelvePdfClientSinAmbiguedad()
    {
        var client = _factory.Services.GetRequiredService<IPdfClient>();

        Assert.IsType<PdfClient>(client);
    }

    [Fact]
    public async Task RestoreRecovery_KeepsLiveButReadyReturns503()
    {
        using var temp = new TempDirectory();
        var gate = new FileSystemEmissionRecoveryGate(temp.Path);
        var block = await gate.MarkRestoreRequiredAsync(
            "restore-health-test",
            "backup-health-test",
            DateTimeOffset.Parse("2026-10-03T10:00:00Z"),
            "integration test");

        using var blockedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Recovery:Directory", temp.Path);
        });
        var client = blockedFactory.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");
        var readyBody = await ready.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Contains("RESTORE_RECONCILIATION_REQUIRED", readyBody);
        Assert.Contains(block.RestoreId, readyBody);
        Assert.DoesNotContain("20123456786", readyBody);
        Assert.DoesNotContain("pfx", readyBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", readyBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", readyBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CatalogoCorrupto_KeepsLiveButReadyReturnsSafe503()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(temp.Path, "fiscal-catalog-v2.json"),
            "{invalid-json");

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FiscalContexts:Directory", temp.Path);
        });
        var client = factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");
        var body = await ready.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Contains("FISCAL_CATALOG_UNREADABLE", body);
        Assert.DoesNotContain(temp.Path, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DirectorioDeCatalogoInvalido_KeepsLiveButReadyReturnsSafe503()
    {
        using var temp = new TempDirectory();
        var notDirectory = System.IO.Path.Combine(temp.Path, "catalog-is-a-file");
        await File.WriteAllTextAsync(notDirectory, "not a directory");

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FiscalContexts:Directory", notDirectory);
        });
        var client = factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");
        var body = await ready.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Contains("FISCAL_CATALOG_UNREADABLE", body);
        Assert.DoesNotContain(notDirectory, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CatalogoVacioNoEsReadySiDirectorioNoEsProvisionable()
    {
        if (!OperatingSystem.IsLinux())
            return;

        const string nonProvisionableDirectory = "/proc";
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FiscalContexts:Directory", nonProvisionableDirectory);
        });
        var client = factory.CreateClient();

        var ready = await client.GetAsync("/health/ready");
        var body = await ready.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Contains("FISCAL_CATALOG_UNREADABLE", body);
        Assert.DoesNotContain(nonProvisionableDirectory, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnvironmentReferenciadoSinConfig_Returns503()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await store.AddContextAsync(new FiscalTechnicalContextRecord(
            "ctx-prod",
            "produccion",
            FiscalContextOperationalState.Disabled,
            1,
            []));

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FiscalContexts:Directory", temp.Path);
        });
        var client = factory.CreateClient();

        var ready = await client.GetAsync("/health/ready");
        var body = await ready.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Contains("FISCAL_ENVIRONMENT_UNRESOLVED", body);
        Assert.DoesNotContain("produccion", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssignmentActivoSinCredentialRef_Returns503()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await store.AddContextAsync(new FiscalTechnicalContextRecord(
            "ctx-homo",
            "homologacion",
            FiscalContextOperationalState.Disabled,
            1,
            []));
        await store.AddCandidateAssignmentAsync("ctx-homo", "rev-1", "missing-cred", "setup");
        await store.MarkAssignmentValidatedAsync("ctx-homo", "rev-1", "validated", "setup");
        await store.ActivateAssignmentAsync("ctx-homo", "rev-1", "setup");
        await store.SetContextStateAsync("ctx-homo", FiscalContextOperationalState.Active, "setup");

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FiscalContexts:Directory", temp.Path);
            builder.UseSetting("FiscalEnvironments:homologacion:WsaaUrl", "https://wsaa.example.invalid");
            builder.UseSetting("FiscalEnvironments:homologacion:WsfeUrl", "https://wsfe.example.invalid");
            builder.UseSetting("FiscalEnvironments:homologacion:PadronUrl", "https://padron.example.invalid");
        });
        var client = factory.CreateClient();

        var ready = await client.GetAsync("/health/ready");
        var body = await ready.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Contains("CREDENTIAL_REFERENCE_UNRESOLVED", body);
        Assert.DoesNotContain("missing-cred", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfigLocalValida_ReadySinConectividadArca()
    {
        using var temp = new TempDirectory();
        var store = new FileSystemFiscalTechnicalContextStore(temp.Path);
        await store.AddContextAsync(new FiscalTechnicalContextRecord(
            "ctx-homo",
            "homologacion",
            FiscalContextOperationalState.Disabled,
            1,
            []));
        await store.AddCandidateAssignmentAsync("ctx-homo", "rev-1", "cred-a", "setup");
        await store.MarkAssignmentValidatedAsync("ctx-homo", "rev-1", "validated", "setup");
        await store.ActivateAssignmentAsync("ctx-homo", "rev-1", "setup");
        await store.SetContextStateAsync("ctx-homo", FiscalContextOperationalState.Active, "setup");

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FiscalContexts:Directory", temp.Path);
            builder.UseSetting("FiscalEnvironments:homologacion:WsaaUrl", "https://wsaa.example.invalid");
            builder.UseSetting("FiscalEnvironments:homologacion:WsfeUrl", "https://wsfe.example.invalid");
            builder.UseSetting("FiscalEnvironments:homologacion:PadronUrl", "https://padron.example.invalid");
            builder.UseSetting("FiscalCredentials:cred-a:CertificatePath", "never-opened-placeholder.pfx");
        });
        var client = factory.CreateClient();

        var ready = await client.GetAsync("/health/ready");
        var body = await ready.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Contains("\"ready\"", body);
        Assert.DoesNotContain("never-opened-placeholder", body, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _certificate.Dispose();
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "arca-health-recovery-tests-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
