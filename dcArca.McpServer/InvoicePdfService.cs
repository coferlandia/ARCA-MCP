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

public sealed class InvoicePdfService
{
    private readonly IInvoiceIssuer _issuer;
    private readonly IPdfDocumentRenderer _renderer;
    private readonly FiscalDocumentContext _fiscalContext;

    public InvoicePdfService(
        IInvoiceIssuer issuer,
        IPdfDocumentRenderer renderer,
        dcArcaConfig config)
        : this(
            issuer,
            renderer,
            new FiscalDocumentContext(
                string.IsNullOrWhiteSpace(config.Environment) ? "unspecified" : config.Environment,
                config.Cuit,
                config.PuntoVenta))
    {
    }

    public InvoicePdfService(
        IInvoiceIssuer issuer,
        IPdfDocumentRenderer renderer,
        FiscalDocumentContext fiscalContext)
    {
        _issuer = issuer;
        _renderer = renderer;
        _fiscalContext = fiscalContext;
    }

    public async Task<InvoiceWithPdfResult> EmitAsync(
        dcFacturaRequest invoice,
        string idempotencyKey,
        PdfTemplateReference template,
        JsonElement templateData,
        CancellationToken cancellationToken = default)
    {
        _renderer.ValidateRequest(template, templateData);
        _renderer.ValidateConfiguration();

        invoice.NumeroComprobante = null;
        var fiscal = await _issuer.EmitAsync(invoice, idempotencyKey, cancellationToken);
        if (!fiscal.Success)
        {
            return new InvoiceWithPdfResult(
                fiscal,
                new PdfRenderResult(PdfRenderStatus.NotAttempted, null, null, null));
        }

        FiscalDocumentSnapshot snapshot;
        try
        {
            snapshot = FiscalDocumentSnapshotFactory.FromEmission(invoice, fiscal, _fiscalContext);
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
