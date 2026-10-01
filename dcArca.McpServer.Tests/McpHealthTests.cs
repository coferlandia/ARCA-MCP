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
}
