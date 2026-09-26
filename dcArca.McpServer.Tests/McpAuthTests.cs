using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace dcArca.McpServer.Tests;

public class McpAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public McpAuthTests(WebApplicationFactory<Program> factory)
    {
        var contentRoot = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "dcArca.McpServer");

        // The Testing config's CertificatePath ("test-cert-placeholder.pfx") only needs to
        // exist (auth rejects the request with 401 before it's ever read) -- but
        // dcConfigurationHelper.ValidateConfig checks it with a relative File.Exists,
        // which resolves against the process's current directory, not the content
        // root. It's gitignored, so create it here instead of committing a binary.
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

    [Fact]
    public async Task McpEndpoint_SinToken_Devuelve401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/", new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
