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

public class McpAuthTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly IApiKeyStore _store;
    private readonly string _storeFilePath;
    private readonly TestCertificatePlaceholder _certificate = new();

    public McpAuthTests(WebApplicationFactory<Program> factory)
    {
        var contentRoot = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "dcArca.McpServer");

        var directory = Path.Combine(Path.GetTempPath(), "dcarca-auth-tests", Guid.NewGuid().ToString("N"));
        _storeFilePath = Path.Combine(directory, "api_keys.json");
        _store = new FileSystemApiKeyStore(directory);
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(contentRoot);
            builder.UseEnvironment("Testing");
            _certificate.Apply(builder);
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
    [InlineData("{ json inválido")]
    [InlineData("[{\"Id\":\"key_corrupta\",\"Name\":\"test\",\"KeyHash\":\"no-es-un-hash\",\"Scopes\":[\"arca:consultar\"],\"Active\":true,\"CreatedAt\":\"2026-10-01T00:00:00Z\"}]")]
    [InlineData("[{\"Id\":\"key_corrupta\",\"Name\":\"test\",\"KeyHash\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"Scopes\":null,\"Active\":true,\"CreatedAt\":\"2026-10-01T00:00:00Z\"}]")]
    public async Task McpEndpoint_ConStoreCorrupto_Devuelve401(string contents)
    {
        await File.WriteAllTextAsync(_storeFilePath, contents);

        var response = await _factory.CreateClient().SendAsync(BuildRequest("sk-arca-cualquier-key"));

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

    public void Dispose()
    {
        _factory.Dispose();
        _certificate.Dispose();
    }
}
