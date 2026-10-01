using System.Text.Json;
using dcArca.Core.Models;

namespace dcArca.McpServer;

public enum PdfRenderStatus
{
    NotAttempted,
    Rendered,
    Failed
}

public sealed record PdfRenderResult(
    PdfRenderStatus Status,
    string? Base64,
    string? ErrorCode,
    string? Message);

public sealed record InvoiceWithPdfResult(dcFacturaResponse Fiscal, PdfRenderResult Pdf);

public sealed class InvoicePdfService(
    IInvoiceIssuer issuer,
    IPdfDocumentRenderer renderer,
    dcArcaConfig config)
{
    public async Task<InvoiceWithPdfResult> EmitAsync(
        dcFacturaRequest invoice,
        string idempotencyKey,
        PdfTemplateReference template,
        JsonElement templateData,
        CancellationToken cancellationToken = default)
    {
        renderer.ValidateRequest(template, templateData);
        renderer.ValidateConfiguration();

        invoice.NumeroComprobante = null;
        var fiscal = await issuer.EmitAsync(invoice, idempotencyKey, cancellationToken);
        if (!fiscal.Success)
        {
            return new InvoiceWithPdfResult(
                fiscal,
                new PdfRenderResult(PdfRenderStatus.NotAttempted, null, null, null));
        }

        var snapshot = FiscalDocumentSnapshotFactory.FromEmission(invoice, fiscal, config);
        var pdf = await renderer.RenderAsync(snapshot, template, templateData, cancellationToken);
        return new InvoiceWithPdfResult(fiscal, pdf);
    }
}
