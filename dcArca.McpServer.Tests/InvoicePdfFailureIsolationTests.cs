using System.Text.Json;
using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public class InvoicePdfFailureIsolationTests
{
    [Theory]
    [InlineData("PDF_TEMPLATE_NOT_FOUND", 404, "unknown_template")]
    [InlineData("PDF_PROVIDER_FORBIDDEN", 403, "forbidden")]
    [InlineData("PDF_UNAVAILABLE", 503, "service_unavailable")]
    public async Task AuthorizedFiscalResult_RemainsIntact_WhenPdfFails(
        string errorCode,
        int providerStatusCode,
        string providerErrorCode)
    {
        var fiscal = Authorized();
        var renderer = new ResultRenderer(new PdfRenderResult(
            PdfRenderStatus.Failed,
            null,
            errorCode,
            "safe message",
            "creadorpdf",
            providerStatusCode,
            providerErrorCode));
        var issuer = new CountingIssuer(fiscal);
        var service = new InvoicePdfService(issuer, renderer, Config());

        var result = await service.EmitAsync(
            Invoice(),
            "idem-structured-pdf-failure",
            new PdfTemplateReference("factura-ar", "3"),
            JsonSerializer.SerializeToElement(new { cliente = "Cliente" }));

        Assert.Same(fiscal, result.Fiscal);
        Assert.True(result.Fiscal.Success);
        Assert.Equal(dcEmissionOutcome.Authorized, result.Fiscal.EmissionOutcome);
        Assert.Equal("CAE123", result.Fiscal.Cae);
        Assert.Equal(123, result.Fiscal.NumeroComprobante);
        Assert.Equal(1, issuer.CallCount);
        Assert.Equal(1, renderer.RenderCallCount);
        Assert.Equal(PdfRenderStatus.Failed, result.Pdf.Status);
        Assert.Equal(errorCode, result.Pdf.ErrorCode);
        Assert.Equal(providerStatusCode, result.Pdf.ProviderStatusCode);
        Assert.Equal(providerErrorCode, result.Pdf.ProviderErrorCode);
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

    private sealed class ResultRenderer(PdfRenderResult result) : IPdfDocumentRenderer
    {
        public int RenderCallCount { get; private set; }

        public void ValidateRequest(PdfTemplateReference template, JsonElement templateData) { }
        public void ValidateConfiguration() { }

        public Task<PdfRenderResult> RenderAsync(
            FiscalDocumentSnapshot fiscal,
            PdfTemplateReference template,
            JsonElement templateData,
            CancellationToken cancellationToken = default)
        {
            RenderCallCount++;
            return Task.FromResult(result);
        }
    }
}
