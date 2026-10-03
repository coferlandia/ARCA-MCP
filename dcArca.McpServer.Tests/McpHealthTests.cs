using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace dcArca.McpServer.Tests;

public class McpHealthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public McpHealthTests(WebApplicationFactory<Program> factory)
    {
        var contentRoot = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "dcArca.McpServer");

        var certPath = Path.Combine(Directory.GetCurrentDirectory(), "test-cert-placeholder.pfx");
        if (!File.Exists(certPath))
        {
            File.WriteAllBytes(certPath, Array.Empty<byte>());
        }

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(contentRoot);
            builder.UseEnvironment("Testing");
        });
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
            // Program reads Recovery:Directory during top-level startup, before late
            // ConfigureAppConfiguration callbacks can influence the captured value.
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
