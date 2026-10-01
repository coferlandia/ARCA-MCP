using System.Text.Json;
using dcArca.Core.Models;
using dcArca.Core.Services;

namespace dcArca.McpServer;

public sealed class ExistingInvoicePdfService(
    IdcWsfeClient wsfe,
    IPdfDocumentRenderer renderer,
    dcArcaConfig config)
{
    public async Task<InvoiceWithPdfResult> RenderAsync(
        dcTipoComprobante tipoComprobante,
        long numeroComprobante,
        PdfTemplateReference template,
        JsonElement templateData,
        CancellationToken cancellationToken = default)
    {
        renderer.ValidateRequest(template, templateData);
        renderer.ValidateConfiguration();

        var fiscal = await wsfe.FECompConsultarAsync(numeroComprobante, tipoComprobante, cancellationToken);
        if (!fiscal.Success)
        {
            return new InvoiceWithPdfResult(
                fiscal,
                new PdfRenderResult(PdfRenderStatus.NotAttempted, null, null, null));
        }

        FiscalDocumentSnapshot snapshot;
        try
        {
            snapshot = FiscalDocumentSnapshotFactory.FromConsult(fiscal, config);
        }
        catch (InvalidOperationException ex) when (ex.Message == "FISCAL_DOCUMENT_NOT_AUTHORIZED")
        {
            return new InvoiceWithPdfResult(
                fiscal,
                new PdfRenderResult(
                    PdfRenderStatus.NotAttempted,
                    null,
                    "FISCAL_DOCUMENT_NOT_AUTHORIZED",
                    "El comprobante consultado no tiene una autorización fiscal renderizable."));
        }

        var pdf = await renderer.RenderAsync(snapshot, template, templateData, cancellationToken);
        return new InvoiceWithPdfResult(fiscal, pdf);
    }
}
