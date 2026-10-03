using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace dcArca.McpServer.Tests;

public class McpContractScopeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public McpContractScopeTests(WebApplicationFactory<Program> factory)
    {
        var contentRoot = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "dcArca.McpServer");
        var certPath = Path.Combine(Directory.GetCurrentDirectory(), "test-cert-placeholder.pfx");
        if (!File.Exists(certPath)) File.WriteAllBytes(certPath, Array.Empty<byte>());

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(contentRoot);
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, ScopeTestAuthHandler>(ScopeTestAuthHandler.SchemeName, null);
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = ScopeTestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = ScopeTestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = ScopeTestAuthHandler.SchemeName;
                });
            });
        });
    }

    [Fact]
    public async Task ToolsList_Consultar_ExponeLecturaYRecuperacionPeroNoPrevalidacionDeEmision()
    {
        using var client = _factory.CreateClient();
        var response = await client.SendAsync(Request("arca:consultar"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"name\":\"obtener_capacidades_fiscales\"", body);
        Assert.Contains("\"name\":\"diagnosticar_contexto_fiscal\"", body);
        Assert.Contains("\"name\":\"consultar_operacion\"", body);
        Assert.Contains("\"name\":\"reconciliar_operacion\"", body);
        Assert.DoesNotContain("\"name\":\"validar_comprobante\"", body);
    }

    [Fact]
    public async Task ToolsList_Facturar_ExponePrevalidacionPeroNoLecturaDeOperaciones()
    {
        using var client = _factory.CreateClient();
        var response = await client.SendAsync(Request("arca:facturar"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"name\":\"validar_comprobante\"", body);
        Assert.DoesNotContain("\"name\":\"diagnosticar_contexto_fiscal\"", body);
        Assert.DoesNotContain("\"name\":\"consultar_operacion\"", body);
        Assert.DoesNotContain("\"name\":\"reconciliar_operacion\"", body);
    }

    private static HttpRequestMessage Request(string scope)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list" })
        };
        request.Headers.Add(ScopeTestAuthHandler.ScopeHeader, scope);
        request.Headers.Add("Accept", "application/json, text/event-stream");
        return request;
    }
}
