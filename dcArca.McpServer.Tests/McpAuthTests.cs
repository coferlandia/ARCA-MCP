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
