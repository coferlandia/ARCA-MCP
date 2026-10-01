using System.Net;
using System.Text;
using System.Text.Json;
using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public class InvoicePdfServiceTests
{
    [Fact]
    public async Task FiscalReservado_SeRechazaAntesDeEmitir()
    {
        var issuer = new FakeIssuer(Authorized());
        var pdf = new FakePdfClient();
        var service = new InvoicePdfService(issuer, pdf);
        var data = JsonSerializer.SerializeToElement(new { fiscal = new { cae = "fake" } });

        await Assert.ThrowsAsync<ArgumentException>(() => service.EmitAsync(
            Invoice(), new PdfTemplateReference("tpl", "1.0.0"), data));

        Assert.Equal(0, issuer.CallCount);
        Assert.Equal(0, pdf.RenderCallCount);
    }

    [Fact]
    public async Task TemplateDataNoObjeto_SeRechazaAntesDeEmitir()
    {
        var issuer = new FakeIssuer(Authorized());
        var pdf = new FakePdfClient();
        var service = new InvoicePdfService(issuer, pdf);
        var data = JsonSerializer.SerializeToElement(new[] { "invalid" });

        await Assert.ThrowsAsync<ArgumentException>(() => service.EmitAsync(
            Invoice(), new PdfTemplateReference("tpl", "1.0.0"), data));

        Assert.Equal(0, issuer.CallCount);
        Assert.Equal(0, pdf.RenderCallCount);
    }

    [Theory]
    [InlineData("", "1.0.0")]
    [InlineData("tpl", "")]
    public async Task TemplateInvalido_SeRechazaAntesDeEmitir(string templateId, string version)
    {
        var issuer = new FakeIssuer(Authorized());
        var pdf = new FakePdfClient();
        var service = new InvoicePdfService(issuer, pdf);

        await Assert.ThrowsAsync<ArgumentException>(() => service.EmitAsync(
            Invoice(), new PdfTemplateReference(templateId, version), JsonSerializer.SerializeToElement(new { cliente = "x" })));

        Assert.Equal(0, issuer.CallCount);
        Assert.Equal(0, pdf.RenderCallCount);
    }

    [Fact]
    public async Task ConfiguracionPdfInvalida_SeDetectaAntesDeEmitir()
    {
        var issuer = new FakeIssuer(Authorized());
        var pdf = new FakePdfClient { ConfigurationError = new InvalidOperationException("missing config") };
        var service = new InvoicePdfService(issuer, pdf);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EmitAsync(
            Invoice(), new PdfTemplateReference("tpl", "1.0.0"), JsonSerializer.SerializeToElement(new { })));

        Assert.Equal(0, issuer.CallCount);
        Assert.Equal(0, pdf.RenderCallCount);
    }

    [Fact]
    public async Task RechazoFiscal_NoIntentaPdf()
    {
        var fiscal = new dcFacturaResponse
        {
            Success = false,
            EmissionOutcome = dcEmissionOutcome.FiscalRejected,
            Codigo = "REJECTED"
        };
        var issuer = new FakeIssuer(fiscal);
        var pdf = new FakePdfClient();
        var service = new InvoicePdfService(issuer, pdf);

        var result = await service.EmitAsync(
            Invoice(), new PdfTemplateReference("tpl", "1.0.0"), JsonSerializer.SerializeToElement(new { }));

        Assert.Same(fiscal, result.Fiscal);
        Assert.Equal(PdfRenderStatus.NotAttempted, result.Pdf.Status);
        Assert.Null(result.Pdf.Base64);
        Assert.Equal(0, pdf.RenderCallCount);
    }

    [Fact]
    public async Task PdfCorrecto_DevuelveFiscalYPdf()
    {
        var fiscal = Authorized();
        var issuer = new FakeIssuer(fiscal);
        var pdf = new FakePdfClient { Bytes = Encoding.ASCII.GetBytes("%PDF-test") };
        var service = new InvoicePdfService(issuer, pdf);

        var result = await service.EmitAsync(
            Invoice(), new PdfTemplateReference("tpl", "1.0.0"), JsonSerializer.SerializeToElement(new { cliente = "Ñandú" }));

        Assert.Same(fiscal, result.Fiscal);
        Assert.Equal(PdfRenderStatus.Rendered, result.Pdf.Status);
        Assert.NotNull(result.Pdf.Base64);
        Assert.Equal(1, pdf.RenderCallCount);
    }

    [Fact]
    public async Task PdfHttpError_PreservaExitoFiscal()
    {
        var fiscal = Authorized();
        var issuer = new FakeIssuer(fiscal);
        var pdf = new FakePdfClient { RenderError = new HttpRequestException("secret body", null, HttpStatusCode.BadGateway) };
        var service = new InvoicePdfService(issuer, pdf);

        var result = await service.EmitAsync(
            Invoice(), new PdfTemplateReference("tpl", "1.0.0"), JsonSerializer.SerializeToElement(new { }));

        Assert.True(result.Fiscal.Success);
        Assert.Equal("CAE123", result.Fiscal.Cae);
        Assert.Equal(123, result.Fiscal.NumeroComprobante);
        Assert.Equal(PdfRenderStatus.Failed, result.Pdf.Status);
        Assert.Equal("PDF_UNAVAILABLE", result.Pdf.ErrorCode);
        Assert.Null(result.Pdf.Base64);
    }

    [Fact]
    public async Task PdfContenidoInvalido_PreservaExitoFiscal()
    {
        var issuer = new FakeIssuer(Authorized());
        var pdf = new FakePdfClient { RenderError = new InvalidDataException("bad mime") };
        var service = new InvoicePdfService(issuer, pdf);

        var result = await service.EmitAsync(
            Invoice(), new PdfTemplateReference("tpl", "1.0.0"), JsonSerializer.SerializeToElement(new { }));

        Assert.True(result.Fiscal.Success);
        Assert.Equal(PdfRenderStatus.Failed, result.Pdf.Status);
        Assert.Equal("PDF_INVALID_RESPONSE", result.Pdf.ErrorCode);
    }

    private static dcFacturaRequest Invoice() => new()
    {
        TipoComprobante = dcTipoComprobante.FacturaB,
        Concepto = dcConcepto.Productos,
        CuitReceptor = 20123456786,
        ImporteNeto = 100m,
        ImporteIva = 21m,
        ImporteTotal = 121m,
        FechaComprobante = "20261001"
    };

    private static dcFacturaResponse Authorized() => new()
    {
        Success = true,
        EmissionOutcome = dcEmissionOutcome.Authorized,
        NumeroComprobante = 123,
        Cae = "CAE123",
        CaeVencimiento = "20261011"
    };

    private sealed class FakeIssuer(dcFacturaResponse result) : IInvoiceIssuer
    {
        public int CallCount { get; private set; }

        public Task<dcFacturaResponse> EmitAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class FakePdfClient : IPdfClient
    {
        public int RenderCallCount { get; private set; }
        public byte[] Bytes { get; init; } = Encoding.ASCII.GetBytes("%PDF-test");
        public Exception? ConfigurationError { get; init; }
        public Exception? RenderError { get; init; }

        public void ValidateConfiguration()
        {
            if (ConfigurationError is not null) throw ConfigurationError;
        }

        public Task<byte[]> RenderAsync(PdfTemplateReference template, JsonElement data, CancellationToken cancellationToken = default)
        {
            RenderCallCount++;
            if (RenderError is not null) throw RenderError;
            return Task.FromResult(Bytes);
        }
    }
}
