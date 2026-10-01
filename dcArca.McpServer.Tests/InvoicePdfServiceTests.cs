using System.Text.Json;
using dcArca.Core.Models;
using Xunit;

namespace dcArca.McpServer.Tests;

public class InvoicePdfServiceTests
{
    [Fact]
    public async Task PrecondicionInvalida_SeDetectaAntesDeEmitir()
    {
        var issuer = new FakeIssuer(Authorized());
        var renderer = new FakeRenderer { ValidationError = new ArgumentException("invalid template") };
        var service = new InvoicePdfService(issuer, renderer, Config());

        await Assert.ThrowsAsync<ArgumentException>(() => service.EmitAsync(
            Invoice(), new PdfTemplateReference("tpl", "1.0.0"), JsonSerializer.SerializeToElement(new { })));

        Assert.Equal(0, issuer.CallCount);
        Assert.Equal(0, renderer.RenderCallCount);
    }

    [Fact]
    public async Task ConfiguracionPdfInvalida_SeDetectaAntesDeEmitir()
    {
        var issuer = new FakeIssuer(Authorized());
        var renderer = new FakeRenderer { ConfigurationError = new InvalidOperationException("missing config") };
        var service = new InvoicePdfService(issuer, renderer, Config());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EmitAsync(
            Invoice(), new PdfTemplateReference("tpl", "1.0.0"), JsonSerializer.SerializeToElement(new { })));

        Assert.Equal(0, issuer.CallCount);
        Assert.Equal(0, renderer.RenderCallCount);
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
        var renderer = new FakeRenderer();
        var service = new InvoicePdfService(issuer, renderer, Config());

        var result = await service.EmitAsync(
            Invoice(), new PdfTemplateReference("tpl", "1.0.0"), JsonSerializer.SerializeToElement(new { }));

        Assert.Same(fiscal, result.Fiscal);
        Assert.Equal(PdfRenderStatus.NotAttempted, result.Pdf.Status);
        Assert.Equal(0, renderer.RenderCallCount);
    }

    [Fact]
    public async Task Autorizado_ConstruyeSnapshotYRendereiza()
    {
        var fiscal = Authorized();
        var issuer = new FakeIssuer(fiscal);
        var renderer = new FakeRenderer
        {
            Result = new PdfRenderResult(PdfRenderStatus.Rendered, "JVBERg==", null, null)
        };
        var service = new InvoicePdfService(issuer, renderer, Config());

        var result = await service.EmitAsync(
            Invoice(), new PdfTemplateReference("tpl", "1.0.0"), JsonSerializer.SerializeToElement(new { cliente = "Ñandú" }));

        Assert.True(result.Fiscal.Success);
        Assert.Equal(PdfRenderStatus.Rendered, result.Pdf.Status);
        Assert.Equal(1, renderer.RenderCallCount);
        Assert.NotNull(renderer.LastFiscal);
        Assert.Equal(123, renderer.LastFiscal!.NumeroComprobante);
        Assert.Equal("CAE123", renderer.LastFiscal.Cae);
        Assert.Equal("20123456786", renderer.LastFiscal.EmisorCuit);
    }

    [Fact]
    public async Task FalloPdf_PreservaExitoFiscal()
    {
        var fiscal = Authorized();
        var renderer = new FakeRenderer
        {
            Result = new PdfRenderResult(PdfRenderStatus.Failed, null, "PDF_UNAVAILABLE", "renderer unavailable")
        };
        var service = new InvoicePdfService(new FakeIssuer(fiscal), renderer, Config());

        var result = await service.EmitAsync(
            Invoice(), new PdfTemplateReference("tpl", "1.0.0"), JsonSerializer.SerializeToElement(new { }));

        Assert.True(result.Fiscal.Success);
        Assert.Equal("CAE123", result.Fiscal.Cae);
        Assert.Equal(123, result.Fiscal.NumeroComprobante);
        Assert.Equal(PdfRenderStatus.Failed, result.Pdf.Status);
        Assert.Equal("PDF_UNAVAILABLE", result.Pdf.ErrorCode);
    }

    private static dcArcaConfig Config() => new() { Cuit = "20123456786", PuntoVenta = 7 };

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
        Cae = "CAE123",
        CaeVencimiento = "20261011",
        Resultado = "A"
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

    private sealed class FakeRenderer : IPdfDocumentRenderer
    {
        public Exception? ValidationError { get; init; }
        public Exception? ConfigurationError { get; init; }
        public PdfRenderResult Result { get; init; } = new(PdfRenderStatus.Rendered, "JVBERg==", null, null);
        public int RenderCallCount { get; private set; }
        public FiscalDocumentSnapshot? LastFiscal { get; private set; }

        public void ValidateRequest(PdfTemplateReference template, JsonElement templateData)
        {
            if (ValidationError is not null) throw ValidationError;
        }

        public void ValidateConfiguration()
        {
            if (ConfigurationError is not null) throw ConfigurationError;
        }

        public Task<PdfRenderResult> RenderAsync(
            FiscalDocumentSnapshot fiscal,
            PdfTemplateReference template,
            JsonElement templateData,
            CancellationToken cancellationToken = default)
        {
            RenderCallCount++;
            LastFiscal = fiscal;
            return Task.FromResult(Result);
        }
    }
}
