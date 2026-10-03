using System.Text.Json;
using dcArca.Core.Models;
using dcArca.Core.Services;

namespace dcArca.McpServer;

public sealed class ExistingInvoicePdfService
{
    private readonly IdcWsfeClient _wsfe;
    private readonly IPdfDocumentRenderer _renderer;
    private readonly FiscalDocumentContext _fiscalContext;

    public ExistingInvoicePdfService(
        IdcWsfeClient wsfe,
        IPdfDocumentRenderer renderer,
        dcArcaConfig config)
        : this(wsfe, renderer, FiscalDocumentContext.FromLegacyConfig(config))
    {
    }

    public ExistingInvoicePdfService(
        IdcWsfeClient wsfe,
        IPdfDocumentRenderer renderer,
        FiscalDocumentContext fiscalContext)
    {
        _wsfe = wsfe;
        _renderer = renderer;
        _fiscalContext = fiscalContext;
    }

    public async Task<InvoiceWithPdfResult> RenderAsync(
        dcTipoComprobante tipoComprobante,
        long numeroComprobante,
        PdfTemplateReference template,
        JsonElement templateData,
        CancellationToken cancellationToken = default)
    {
        _renderer.ValidateRequest(template, templateData);
        _renderer.ValidateConfiguration();

        var fiscal = await _wsfe.FECompConsultarAsync(numeroComprobante, tipoComprobante, cancellationToken);
        if (!fiscal.Success)
        {
            return new InvoiceWithPdfResult(
                fiscal,
                new PdfRenderResult(PdfRenderStatus.NotAttempted, null, null, null));
        }

        FiscalDocumentSnapshot snapshot;
        try
        {
            snapshot = FiscalDocumentSnapshotFactory.FromConsult(fiscal, _fiscalContext);
        }
        catch (FiscalDocumentSnapshotException exception)
        {
            return new InvoiceWithPdfResult(
                fiscal,
                new PdfRenderResult(
                    exception.Code == "FISCAL_DOCUMENT_NOT_AUTHORIZED"
                        ? PdfRenderStatus.NotAttempted
                        : PdfRenderStatus.Failed,
                    null,
                    exception.Code,
                    exception.SafeMessage));
        }

        var pdf = await _renderer.RenderAsync(snapshot, template, templateData, cancellationToken);
        return new InvoiceWithPdfResult(fiscal, pdf);
    }
}
