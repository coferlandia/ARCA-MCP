using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace dcArca.McpServer.Tests;

public class CreadorPdfTemplateResolverTests
{
    [Fact]
    public async Task PublishedExacto_ResuelveIdFisico()
    {
        var (resolver, handler) = CreateResolver(_ => TemplatesResponse(
            new { id = "tpl_123", name = "factura-ar", version = "3", status = "published" }));

        var id = await resolver.ResolveAsync(new PdfTemplateReference("factura-ar", "3"));

        Assert.Equal("tpl_123", id);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task VersionDistinta_NoHaceMatch()
    {
        var (resolver, _) = CreateResolver(_ => TemplatesResponse(
            new { id = "tpl_123", name = "factura-ar", version = "4", status = "published" }));

        var exception = await Assert.ThrowsAsync<CreadorPdfException>(() =>
            resolver.ResolveAsync(new PdfTemplateReference("factura-ar", "3")));

        Assert.Equal(PdfFailureKind.TemplateNotFound, exception.FailureKind);
    }

    [Fact]
    public async Task SoloDraft_DevuelveTemplateNotPublished()
    {
        var (resolver, _) = CreateResolver(_ => TemplatesResponse(
            new { id = "tpl_123", name = "factura-ar", version = "3", status = "draft" }));

        var exception = await Assert.ThrowsAsync<CreadorPdfException>(() =>
            resolver.ResolveAsync(new PdfTemplateReference("factura-ar", "3")));

        Assert.Equal(PdfFailureKind.TemplateNotPublished, exception.FailureKind);
    }

    [Fact]
    public async Task DuplicadoPublicado_FallaCerrado()
    {
        var (resolver, _) = CreateResolver(_ => TemplatesResponse(
            new { id = "tpl_1", name = "factura-ar", version = "3", status = "published" },
            new { id = "tpl_2", name = "factura-ar", version = "3", status = "published" }));

        var exception = await Assert.ThrowsAsync<CreadorPdfException>(() =>
            resolver.ResolveAsync(new PdfTemplateReference("factura-ar", "3")));

        Assert.Equal(PdfFailureKind.TemplateAmbiguous, exception.FailureKind);
    }

    [Theory]
    [InlineData(401, PdfFailureKind.Unauthorized)]
    [InlineData(403, PdfFailureKind.Forbidden)]
    [InlineData(500, PdfFailureKind.Unavailable)]
    public async Task ErrorAlListar_ConservaClasificacion(int status, PdfFailureKind expected)
    {
        var (resolver, _) = CreateResolver(_ => new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });

        var exception = await Assert.ThrowsAsync<CreadorPdfException>(() =>
            resolver.ResolveAsync(new PdfTemplateReference("factura-ar", "3")));

        Assert.Equal(expected, exception.FailureKind);
        Assert.Equal(status, exception.ProviderStatusCode);
    }

    [Fact]
    public async Task NetworkAlListar_DevuelveUnavailable()
    {
        var handler = new DelegateHandler((_, _) => throw new HttpRequestException("refused"));
        var resolver = CreateResolver(handler);

        var exception = await Assert.ThrowsAsync<CreadorPdfException>(() =>
            resolver.ResolveAsync(new PdfTemplateReference("factura-ar", "3")));

        Assert.Equal(PdfFailureKind.Unavailable, exception.FailureKind);
        Assert.Null(exception.ProviderStatusCode);
    }

    [Fact]
    public async Task CacheHit_EvitaSegundoListing()
    {
        var (resolver, handler) = CreateResolver(_ => TemplatesResponse(
            new { id = "tpl_123", name = "factura-ar", version = "3", status = "published" }));

        var first = await resolver.ResolveAsync(new PdfTemplateReference("factura-ar", "3"));
        var second = await resolver.ResolveAsync(new PdfTemplateReference("factura-ar", "3"));

        Assert.Equal(first, second);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ConcurrenciaMismaReferencia_HaceUnaResolucionRemota()
    {
        var (resolver, handler) = CreateResolver(_ => TemplatesResponse(
            new { id = "tpl_123", name = "factura-ar", version = "3", status = "published" }));
        var template = new PdfTemplateReference("factura-ar", "3");

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => resolver.ResolveAsync(template)));

        Assert.All(results, id => Assert.Equal("tpl_123", id));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task LegacyTpl_PasaDirectoSinListing()
    {
        var (resolver, handler) = CreateResolver(_ => throw new InvalidOperationException("no debe llamar"));

        var id = await resolver.ResolveAsync(new PdfTemplateReference("tpl_legacy", "3"));

        Assert.Equal("tpl_legacy", id);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task StaleCachedId_UnknownTemplate_InvalidaYReintentaUnaVez()
    {
        var resolver = new SequenceResolver("tpl_old", "tpl_new");
        var postCount = 0;
        var client = CreatePdfClient(async (_, _) =>
        {
            postCount++;
            if (postCount == 1)
                return ErrorResponse(HttpStatusCode.NotFound, "unknown_template");
            return PdfResponse("%PDF-ok"u8.ToArray());
        }, resolver);

        var bytes = await client.RenderAsync(
            new PdfTemplateReference("factura-ar", "3"),
            JsonSerializer.SerializeToElement(new { }));

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes));
        Assert.Equal(2, resolver.ResolveCount);
        Assert.Equal(1, resolver.InvalidateCount);
        Assert.Equal(2, postCount);
    }

    [Fact]
    public async Task SegundoUnknownTemplate_NoProduceLoop()
    {
        var resolver = new SequenceResolver("tpl_old", "tpl_new", "tpl_never");
        var postCount = 0;
        var client = CreatePdfClient((_, _) =>
        {
            postCount++;
            return Task.FromResult(ErrorResponse(HttpStatusCode.NotFound, "unknown_template"));
        }, resolver);

        var exception = await Assert.ThrowsAsync<CreadorPdfException>(() => client.RenderAsync(
            new PdfTemplateReference("factura-ar", "3"),
            JsonSerializer.SerializeToElement(new { })));

        Assert.Equal(PdfFailureKind.TemplateNotFound, exception.FailureKind);
        Assert.Equal(2, postCount);
        Assert.Equal(1, resolver.InvalidateCount);
    }

    private static (CreadorPdfTemplateResolver Resolver, DelegateHandler Handler) CreateResolver(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new DelegateHandler((request, _) => Task.FromResult(responder(request)));
        return (CreateResolver(handler), handler);
    }

    private static CreadorPdfTemplateResolver CreateResolver(DelegateHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://pdf.test"),
            Timeout = TimeSpan.FromSeconds(5)
        };
        return new CreadorPdfTemplateResolver(httpClient, Configuration());
    }

    private static PdfClient CreatePdfClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
        IPdfTemplateResolver resolver)
    {
        var httpClient = new HttpClient(new DelegateHandler(responder))
        {
            BaseAddress = new Uri("https://pdf.test"),
            Timeout = TimeSpan.FromSeconds(5)
        };
        return new PdfClient(httpClient, Configuration(), resolver);
    }

    private static IConfiguration Configuration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pdf:BaseUrl"] = "https://pdf.test",
                ["Pdf:ApiKey"] = "test-key",
                ["Pdf:TemplateResolutionCacheSeconds"] = "300"
            })
            .Build();

    private static HttpResponseMessage TemplatesResponse(params object[] templates)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(templates), Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage ErrorResponse(HttpStatusCode status, string code)
        => new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { error = new { code, message = "remote", details = Array.Empty<object>() } }),
                Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage PdfResponse(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return callback(request, cancellationToken);
        }
    }

    private sealed class SequenceResolver(params string[] ids) : IPdfTemplateResolver
    {
        private readonly Queue<string> _ids = new(ids);
        public int ResolveCount { get; private set; }
        public int InvalidateCount { get; private set; }

        public Task<string> ResolveAsync(PdfTemplateReference template, CancellationToken cancellationToken = default)
        {
            ResolveCount++;
            return Task.FromResult(_ids.Count > 1 ? _ids.Dequeue() : _ids.Peek());
        }

        public void Invalidate(PdfTemplateReference template) => InvalidateCount++;
    }
}
