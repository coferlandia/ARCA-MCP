using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using dcArca.Core.Models;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace dcArca.McpServer.Tests;

public class LogicalTemplateRetryFiscalIsolationTests
{
    [Fact]
    public async Task StalePhysicalTemplate_RetriesOnlyDocumentRendering()
    {
        var issuer = new CountingIssuer(Authorized());
        var resolver = new SequenceResolver("tpl_old", "tpl_new");
        var postCount = 0;
        var handler = new DelegateHandler((_, _) =>
        {
            postCount++;
            if (postCount == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new
                        {
                            error = new
                            {
                                code = "unknown_template",
                                message = "remote detail",
                                details = Array.Empty<object>()
                            }
                        }),
                        Encoding.UTF8,
                        "application/json")
                });
            }

            var content = new ByteArrayContent("%PDF-ok"u8.ToArray());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        var httpClient = new HttpClient(handler)
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
        var pdfClient = new PdfClient(httpClient, configuration, resolver);
        var service = new InvoicePdfService(
            issuer,
            new PdfDocumentRenderer(pdfClient),
            Config());

        var result = await service.EmitAsync(
            Invoice(),
            "idem-stale-template",
            new PdfTemplateReference("factura-ar", "3"),
            JsonSerializer.SerializeToElement(new { cliente = "Cliente" }));

        Assert.Equal(1, issuer.CallCount);
        Assert.Equal(2, postCount);
        Assert.Equal(2, resolver.ResolveCount);
        Assert.Equal(1, resolver.InvalidateCount);
        Assert.True(result.Fiscal.Success);
        Assert.Equal("CAE123", result.Fiscal.Cae);
        Assert.Equal(123, result.Fiscal.NumeroComprobante);
        Assert.Equal(PdfRenderStatus.Rendered, result.Pdf.Status);
        Assert.NotNull(result.Pdf.Base64);
    }

    private static dcArcaConfig Config() => new()
    {
        Environment = "produccion",
        Cuit = "20123456786",
        PuntoVenta = 7
    };

    private static dcFacturaRequest Invoice() => new()
    {
        TipoComprobante = dcTipoComprobante.FacturaB,
        Concepto = dcConcepto.Productos,
        CuitReceptor = 20333444559,
        TipoDocReceptor = 80,
        CondicionIvaReceptor = dcCondicionIvaReceptor.ResponsableInscripto,
        ImporteNeto = 100m,
        ImporteIva = 21m,
        ImporteTotal = 121m,
        AlicuotaIva = dcAlicuotaIva.Veintiuno,
        FechaComprobante = "20261001"
    };

    private static dcFacturaResponse Authorized() => new()
    {
        Success = true,
        EmissionOutcome = dcEmissionOutcome.Authorized,
        NumeroComprobante = 123,
        PuntoVenta = 7,
        Cae = "CAE123",
        CaeVencimiento = "20261011",
        Resultado = "A"
    };

    private sealed class CountingIssuer(dcFacturaResponse result) : IInvoiceIssuer
    {
        public int CallCount { get; private set; }

        public Task<dcFacturaResponse> EmitAsync(
            dcFacturaRequest factura,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class SequenceResolver(params string[] ids) : IPdfTemplateResolver
    {
        private readonly Queue<string> _ids = new(ids);
        public int ResolveCount { get; private set; }
        public int InvalidateCount { get; private set; }

        public Task<string> ResolveAsync(
            PdfTemplateReference template,
            CancellationToken cancellationToken = default)
        {
            ResolveCount++;
            return Task.FromResult(_ids.Count > 1 ? _ids.Dequeue() : _ids.Peek());
        }

        public void Invalidate(PdfTemplateReference template) => InvalidateCount++;
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => callback(request, cancellationToken);
    }
}
