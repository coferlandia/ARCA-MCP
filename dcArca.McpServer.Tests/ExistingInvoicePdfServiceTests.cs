using System.Text.Json;
using dcArca.Core.Models;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.McpServer.Tests;

public class ExistingInvoicePdfServiceTests
{
    [Fact]
    public async Task ComprobanteExistente_ConsultaYRendereiza_SinEmitir()
    {
        var wsfe = new FakeWsfeClient { ConsultResult = Consulted() };
        var renderer = new FakeRenderer
        {
            Result = new PdfRenderResult(PdfRenderStatus.Rendered, "JVBERg==", null, null)
        };
        var service = new ExistingInvoicePdfService(wsfe, renderer, Config());

        var result = await service.RenderAsync(
            dcTipoComprobante.FacturaB,
            123,
            new PdfTemplateReference("tpl", "1.0.0"),
            JsonSerializer.SerializeToElement(new { cliente = "Cliente" }));

        Assert.True(result.Fiscal.Success);
        Assert.Equal(PdfRenderStatus.Rendered, result.Pdf.Status);
        Assert.Equal(1, wsfe.ConsultCallCount);
        Assert.Equal(0, wsfe.EmitCallCount);
        Assert.Equal(1, renderer.RenderCallCount);
        Assert.Equal("CAE123", renderer.LastFiscal!.Cae);
        Assert.Equal("produccion", renderer.LastFiscal.Environment);
        Assert.Equal("20123456786", renderer.LastFiscal.EmisorCuit);
        Assert.Equal(7, renderer.LastFiscal.PuntoVenta);
        Assert.Equal("fe-comp-consultar-validated", renderer.LastFiscal.Provenance.FiscalData);
    }

    [Fact]
    public async Task ComprobanteInexistente_NoRenderizaNiEmite()
    {
        var wsfe = new FakeWsfeClient
        {
            ConsultResult = new dcFacturaResponse { Success = false, Codigo = "NOT_FOUND", Mensaje = "No encontrado" }
        };
        var renderer = new FakeRenderer();
        var service = new ExistingInvoicePdfService(wsfe, renderer, Config());

        var result = await service.RenderAsync(
            dcTipoComprobante.FacturaB,
            999,
            new PdfTemplateReference("tpl", "1.0.0"),
            JsonSerializer.SerializeToElement(new { }));

        Assert.False(result.Fiscal.Success);
        Assert.Equal(PdfRenderStatus.NotAttempted, result.Pdf.Status);
        Assert.Equal(1, wsfe.ConsultCallCount);
        Assert.Equal(0, wsfe.EmitCallCount);
        Assert.Equal(0, renderer.RenderCallCount);
    }

    [Fact]
    public async Task ConsultaSinCae_NoProducePdf()
    {
        var fiscal = Consulted();
        fiscal.Cae = string.Empty;
        var wsfe = new FakeWsfeClient { ConsultResult = fiscal };
        var renderer = new FakeRenderer();
        var service = new ExistingInvoicePdfService(wsfe, renderer, Config());

        var result = await service.RenderAsync(
            dcTipoComprobante.FacturaB,
            123,
            new PdfTemplateReference("tpl", "1.0.0"),
            JsonSerializer.SerializeToElement(new { }));

        Assert.Equal(PdfRenderStatus.NotAttempted, result.Pdf.Status);
        Assert.Equal("FISCAL_DOCUMENT_NOT_AUTHORIZED", result.Pdf.ErrorCode);
        Assert.Equal(0, renderer.RenderCallCount);
        Assert.Equal(0, wsfe.EmitCallCount);
    }

    [Fact]
    public async Task ConsultaAutorizadaPeroFiscalmenteIncompleta_ConservaCaeYFallaSoloPdf()
    {
        var fiscal = Consulted();
        fiscal.Iva.Clear();
        var wsfe = new FakeWsfeClient { ConsultResult = fiscal };
        var renderer = new FakeRenderer();
        var service = new ExistingInvoicePdfService(wsfe, renderer, Config());

        var result = await service.RenderAsync(
            dcTipoComprobante.FacturaB,
            123,
            new PdfTemplateReference("tpl", "1.0.0"),
            JsonSerializer.SerializeToElement(new { }));

        Assert.True(result.Fiscal.Success);
        Assert.Equal("CAE123", result.Fiscal.Cae);
        Assert.Equal(PdfRenderStatus.Failed, result.Pdf.Status);
        Assert.Equal("FISCAL_DOCUMENT_INCOMPLETE", result.Pdf.ErrorCode);
        Assert.Equal(0, renderer.RenderCallCount);
        Assert.Equal(1, wsfe.ConsultCallCount);
        Assert.Equal(0, wsfe.EmitCallCount);
    }

    [Fact]
    public async Task RegeneracionDeNotaSinAsociacionConsultable_ConservaAutorizacionYNoInventaPdf()
    {
        var fiscal = Consulted();
        fiscal.TipoComprobante = dcTipoComprobante.NotaCreditoB;
        var wsfe = new FakeWsfeClient { ConsultResult = fiscal };
        var renderer = new FakeRenderer();
        var service = new ExistingInvoicePdfService(wsfe, renderer, Config());

        var result = await service.RenderAsync(
            dcTipoComprobante.NotaCreditoB,
            123,
            new PdfTemplateReference("tpl", "1.0.0"),
            JsonSerializer.SerializeToElement(new { }));

        Assert.True(result.Fiscal.Success);
        Assert.Equal("CAE123", result.Fiscal.Cae);
        Assert.Equal(PdfRenderStatus.Failed, result.Pdf.Status);
        Assert.Equal("FISCAL_DOCUMENT_ASSOCIATION_UNAVAILABLE", result.Pdf.ErrorCode);
        Assert.Equal(0, renderer.RenderCallCount);
        Assert.Equal(0, wsfe.EmitCallCount);
    }

    [Fact]
    public async Task PrecondicionLocalInvalida_NoConsultaArca()
    {
        var wsfe = new FakeWsfeClient { ConsultResult = Consulted() };
        var renderer = new FakeRenderer { ValidationError = new ArgumentException("reserved fiscal") };
        var service = new ExistingInvoicePdfService(wsfe, renderer, Config());

        await Assert.ThrowsAsync<ArgumentException>(() => service.RenderAsync(
            dcTipoComprobante.FacturaB,
            123,
            new PdfTemplateReference("tpl", "1.0.0"),
            JsonSerializer.SerializeToElement(new { fiscal = new { } })));

        Assert.Equal(0, wsfe.ConsultCallCount);
        Assert.Equal(0, wsfe.EmitCallCount);
    }

    [Fact]
    public async Task FalloRenderer_NoEmiteNuevoComprobante()
    {
        var wsfe = new FakeWsfeClient { ConsultResult = Consulted() };
        var renderer = new FakeRenderer
        {
            Result = new PdfRenderResult(PdfRenderStatus.Failed, null, "PDF_UNAVAILABLE", "renderer unavailable")
        };
        var service = new ExistingInvoicePdfService(wsfe, renderer, Config());

        var result = await service.RenderAsync(
            dcTipoComprobante.FacturaB,
            123,
            new PdfTemplateReference("tpl", "1.0.0"),
            JsonSerializer.SerializeToElement(new { }));

        Assert.True(result.Fiscal.Success);
        Assert.Equal("CAE123", result.Fiscal.Cae);
        Assert.Equal(PdfRenderStatus.Failed, result.Pdf.Status);
        Assert.Equal(1, wsfe.ConsultCallCount);
        Assert.Equal(0, wsfe.EmitCallCount);
    }

    private static dcArcaConfig Config() => new()
    {
        Environment = "produccion",
        Cuit = "20123456786",
        PuntoVenta = 7
    };

    private static dcFacturaResponse Consulted() => new()
    {
        Success = true,
        NumeroComprobante = 123,
        PuntoVenta = 7,
        TipoComprobante = dcTipoComprobante.FacturaB,
        Concepto = dcConcepto.Productos,
        DocTipo = dcTipoDocumento.CUIT,
        DocNro = 20333444559,
        CondicionIvaReceptor = dcCondicionIvaReceptor.ResponsableInscripto,
        FechaComprobante = "20261001",
        ImporteNeto = 100m,
        ImporteIva = 21m,
        ImporteTotal = 121m,
        Iva =
        [
            new dcFacturaResponse.IvaDetalle
            {
                Alicuota = dcAlicuotaIva.Veintiuno,
                BaseImponible = 100m,
                Importe = 21m
            }
        ],
        MonedaId = "PES",
        MonedaCotizacion = 1m,
        Cae = "CAE123",
        CaeVencimiento = "20261011",
        Resultado = "A"
    };

    private sealed class FakeWsfeClient : IdcWsfeClient
    {
        public required dcFacturaResponse ConsultResult { get; init; }
        public int ConsultCallCount { get; private set; }
        public int EmitCallCount { get; private set; }

        public Task<dcFacturaResponse> FECompConsultarAsync(long numeroComprobante, dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
        {
            ConsultCallCount++;
            return Task.FromResult(ConsultResult);
        }

        public Task<dcFacturaResponse> FECAESolicitarAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
        {
            EmitCallCount++;
            return Task.FromResult(new dcFacturaResponse());
        }

        public Task<dcFacturaResponse> SolicitarCaeAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
        {
            EmitCallCount++;
            return Task.FromResult(new dcFacturaResponse());
        }

        public Task<dcFacturaResponse> FECompUltimoAutorizadoAsync(dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
            => Task.FromResult(new dcFacturaResponse());

        public Task<List<dcCondicionIvaOption>> GetCondicionesIVAReceptorAsync(int docTipo, long docNro, dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<dcCondicionIvaOption>());
    }

    private sealed class FakeRenderer : IPdfDocumentRenderer
    {
        public Exception? ValidationError { get; init; }
        public PdfRenderResult Result { get; init; } = new(PdfRenderStatus.Rendered, "JVBERg==", null, null);
        public int RenderCallCount { get; private set; }
        public FiscalDocumentSnapshot? LastFiscal { get; private set; }

        public void ValidateRequest(PdfTemplateReference template, JsonElement templateData)
        {
            if (ValidationError is not null) throw ValidationError;
        }

        public void ValidateConfiguration() { }

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
