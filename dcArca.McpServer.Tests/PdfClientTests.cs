using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace dcArca.McpServer.Tests;

public class PdfClientTests
{
    [Fact]
    public async Task Render_EnviaBearerTemplateVersionYData()
    {
        HttpRequestMessage? captured = null;
        string? requestBody = null;
        var handler = new DelegateHandler(async request =>
        {
            captured = request;
            requestBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("%PDF-test"u8.ToArray())
                {
                    Headers = { ContentType = new("application/pdf") }
                }
            };
        });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Pdf:ApiKey"] = "sk-creadorpdf-test" })
            .Build();
        var client = new PdfClient(new HttpClient(handler) { BaseAddress = new Uri("https://pdf.test") }, configuration);

        var pdf = await client.RenderAsync(
            new PdfTemplateReference("tpl_123", "1.0.0"),
            JsonSerializer.SerializeToElement(new { cliente = "Ñandú" }));

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf));
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("sk-creadorpdf-test", captured.Headers.Authorization?.Parameter);
        using var json = JsonDocument.Parse(requestBody!);
        Assert.Equal("tpl_123", json.RootElement.GetProperty("template").GetProperty("id").GetString());
        Assert.Equal("1.0.0", json.RootElement.GetProperty("template").GetProperty("version").GetString());
        Assert.Equal("Ñandú", json.RootElement.GetProperty("data").GetProperty("cliente").GetString());
    }

    [Fact]
    public async Task Render_PropagaErrorSeguroSinIncluirRespuesta()
    {
        var handler = new DelegateHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("dato fiscal secreto")
        }));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Pdf:ApiKey"] = "key" })
            .Build();
        var client = new PdfClient(new HttpClient(handler) { BaseAddress = new Uri("https://pdf.test") }, configuration);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.RenderAsync(
            new PdfTemplateReference("tpl", "1"), JsonSerializer.SerializeToElement(new { })));
        Assert.DoesNotContain("dato fiscal secreto", error.Message);
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => callback(request);
    }
}
