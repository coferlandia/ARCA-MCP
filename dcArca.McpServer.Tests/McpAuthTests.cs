using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using dcArca.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dcArca.McpServer.Tests;

public class McpAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly IApiKeyStore _store;

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

        var directory = Path.Combine(Path.GetTempPath(), "dcarca-auth-tests", Guid.NewGuid().ToString("N"));
        _store = new FileSystemApiKeyStore(directory);
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(contentRoot);
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
                services.AddSingleton(_store));
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

    [Fact]
    public async Task McpEndpoint_ConKeyValida_AutenticaYExponeToolsDelScope()
    {
        var (_, rawKey) = await _store.CreateAsync("test", ["arca:consultar"]);
        var request = BuildRequest(rawKey);
        var response = await _factory.CreateClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("consultar_padron", body);
        Assert.DoesNotContain("solicitar_cae", body);
    }

    [Fact]
    public async Task McpEndpoint_ConKeyRevocada_Devuelve401()
    {
        var (record, rawKey) = await _store.CreateAsync("test", ["arca:consultar"]);
        Assert.True(await _store.RevokeAsync(record.Id));
        var response = await _factory.CreateClient().SendAsync(BuildRequest(rawKey));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-key")]
    public async Task McpEndpoint_ConBearerInvalido_Devuelve401(string token)
    {
        var response = await _factory.CreateClient().SendAsync(BuildRequest(token));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage BuildRequest(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Accept", "application/json, text/event-stream");
        return request;
    }
}
