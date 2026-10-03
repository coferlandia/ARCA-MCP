using System.Net;
using System.Net.Http.Headers;
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
        var client = CreateClient(async (request, _) =>
        {
            captured = request;
            requestBody = await request.Content!.ReadAsStringAsync();
            return PdfResponse("%PDF-test"u8.ToArray());
        });

        var pdf = await client.RenderAsync(
            new PdfTemplateReference("tpl_123", "1.0.0"),
            JsonSerializer.SerializeToElement(new { cliente = "Ñandú" }));

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf));
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("test-key", captured.Headers.Authorization?.Parameter);
        using var json = JsonDocument.Parse(requestBody!);
        Assert.Equal("tpl_123", json.RootElement.GetProperty("template").GetProperty("id").GetString());
        Assert.Equal("1.0.0", json.RootElement.GetProperty("template").GetProperty("version").GetString());
        Assert.Equal("Ñandú", json.RootElement.GetProperty("data").GetProperty("cliente").GetString());
    }

    [Theory]
    [InlineData(400, PdfFailureKind.InvalidRequest)]
    [InlineData(401, PdfFailureKind.Unauthorized)]
    [InlineData(403, PdfFailureKind.Forbidden)]
    [InlineData(422, PdfFailureKind.InvalidRequest)]
    [InlineData(429, PdfFailureKind.RateLimited)]
    [InlineData(500, PdfFailureKind.Unavailable)]
    [InlineData(502, PdfFailureKind.Unavailable)]
    [InlineData(503, PdfFailureKind.Unavailable)]
    [InlineData(504, PdfFailureKind.Unavailable)]
    public async Task ErrorHttp_ConservaClasificacionYMetadata(int status, PdfFailureKind expected)
    {
        var client = CreateClient((_, _) => Task.FromResult(ErrorResponse(
            (HttpStatusCode)status,
            "provider_code",
            "mensaje remoto no confiable")));

        var exception = await Assert.ThrowsAsync<CreadorPdfException>(() =>
            client.RenderAsync(LegacyTemplate(), JsonSerializer.SerializeToElement(new { })));

        Assert.Equal(expected, exception.FailureKind);
        Assert.Equal(status, exception.ProviderStatusCode);
        Assert.Equal("provider_code", exception.ProviderErrorCode);
        Assert.DoesNotContain("mensaje remoto", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownTemplate404_SeClasificaComoTemplateNotFound()
    {
        var client = CreateClient((_, _) => Task.FromResult(ErrorResponse(
            HttpStatusCode.NotFound,
            "unknown_template",
            "Plantilla desconocida o no publicada.")));

        var exception = await Assert.ThrowsAsync<CreadorPdfException>(() =>
            client.RenderAsync(LegacyTemplate(), JsonSerializer.SerializeToElement(new { })));

        Assert.Equal(PdfFailureKind.TemplateNotFound, exception.FailureKind);
        Assert.Equal(404, exception.ProviderStatusCode);
        Assert.Equal("unknown_template", exception.ProviderErrorCode);
    }

    [Fact]
    public async Task Error404ConBodyInvalido_NoFallaElParserNiAsumeTemplate()
    {
        var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("not-json")
        }));

        var exception = await Assert.ThrowsAsync<CreadorPdfException>(() =>
            client.RenderAsync(LegacyTemplate(), JsonSerializer.SerializeToElement(new { })));

        Assert.Equal(PdfFailureKind.RenderFailed, exception.FailureKind);
        Assert.Equal(404, exception.ProviderStatusCode);
        Assert.Null(exception.ProviderErrorCode);
    }

    [Fact]
    public async Task RemoteErrorCodeConControles_NoSeExponeComoMetadata()
    {
        const string secret = "Bearer-super-secret";
        var unsafeCode = $"unknown_template\nAuthorization:{secret}";
        var client = CreateClient((_, _) => Task.FromResult(ErrorResponse(
            HttpStatusCode.NotFound,
            unsafeCode,
            "remote message")));

        var exception = await Assert.ThrowsAsync<CreadorPdfException>(() =>
            client.RenderAsync(LegacyTemplate(), JsonSerializer.SerializeToElement(new { })));

        Assert.Equal(PdfFailureKind.RenderFailed, exception.FailureKind);
        Assert.Equal(404, exception.ProviderStatusCode);
        Assert.Null(exception.ProviderErrorCode);
        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NetworkFailure_SeClasificaUnavailableSinInventarStatus()
    {
        var client = CreateClient((_, _) => throw new HttpRequestException("connection refused"));

        var exception = await Assert.ThrowsAsync<CreadorPdfException>(() =>
            client.RenderAsync(LegacyTemplate(), JsonSerializer.SerializeToElement(new { })));

        Assert.Equal(PdfFailureKind.Unavailable, exception.FailureKind);
        Assert.Null(exception.ProviderStatusCode);
    }

    [Fact]
    public async Task TimeoutLocal_SeClasificaTimeout()
    {
        var client = CreateClient((_, _) => throw new TaskCanceledException("timeout"));

        var exception = await Assert.ThrowsAsync<CreadorPdfException>(() =>
            client.RenderAsync(LegacyTemplate(), JsonSerializer.SerializeToElement(new { })));

        Assert.Equal(PdfFailureKind.Timeout, exception.FailureKind);
        Assert.Null(exception.ProviderStatusCode);
    }

    [Fact]
    public async Task CancelacionExplicita_SePropaga()
    {
        var client = CreateClient((_, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(PdfResponse([1]));
        });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.RenderAsync(LegacyTemplate(), JsonSerializer.SerializeToElement(new { }), cts.Token));
    }

    [Fact]
    public async Task MimeInvalido_SeClasificaInvalidResponse()
    {
        var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not a pdf", Encoding.UTF8, "text/plain")
        }));

        var exception = await Assert.ThrowsAsync<CreadorPdfException>(() =>
            client.RenderAsync(LegacyTemplate(), JsonSerializer.SerializeToElement(new { })));

        Assert.Equal(PdfFailureKind.InvalidResponse, exception.FailureKind);
        Assert.Equal(200, exception.ProviderStatusCode);
    }

    [Fact]
    public async Task ErrorBodyEnorme_SeLimitaYNoExponeDatosSensibles()
    {
        var secret = "super-secret-value";
        var huge = "{\"error\":{\"code\":\"unknown_template\",\"message\":\"" + secret + "\",\"details\":[\""
            + new string('x', (64 * 1024) + 1024)
            + "\"]}}";
        var client = CreateClient((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(huge, Encoding.UTF8, "application/json")
        }));

        var exception = await Assert.ThrowsAsync<CreadorPdfException>(() =>
            client.RenderAsync(LegacyTemplate(), JsonSerializer.SerializeToElement(new { })));

        Assert.Null(exception.ProviderErrorCode);
        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
    }

    private static PdfClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        var httpClient = new HttpClient(new DelegateHandler(responder))
        {
            BaseAddress = new Uri("https://pdf.test"),
            Timeout = TimeSpan.FromSeconds(5)
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pdf:BaseUrl"] = "https://pdf.test",
                ["Pdf:ApiKey"] = "test-key"
            })
            .Build();
        return new PdfClient(httpClient, configuration);
    }

    private static PdfTemplateReference LegacyTemplate() => new("tpl_test", "3");

    private static HttpResponseMessage PdfResponse(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static HttpResponseMessage ErrorResponse(HttpStatusCode status, string code, string message)
        => new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { error = new { code, message, details = Array.Empty<object>() } }),
                Encoding.UTF8,
                "application/json")
        };

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => callback(request, cancellationToken);
    }
}
