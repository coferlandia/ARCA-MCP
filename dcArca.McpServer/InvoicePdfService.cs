using System.Text.Json;
using dcArca.Core.Models;

namespace dcArca.McpServer;

public sealed record InvoiceWithPdfResult(dcFacturaResponse Fiscal, string? PdfBase64);

public sealed class InvoicePdfService(McpInvoiceSequencer sequencer, IPdfClient pdfClient)
{
    public async Task<InvoiceWithPdfResult> EmitAsync(
        dcFacturaRequest invoice,
        PdfTemplateReference template,
        JsonElement templateData,
        CancellationToken cancellationToken = default)
    {
        if (templateData.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("templateData debe ser un objeto JSON.", nameof(templateData));

        invoice.NumeroComprobante = null;
        var fiscal = await sequencer.EmitAsync(invoice, cancellationToken);
        if (!fiscal.Success) return new InvoiceWithPdfResult(fiscal, null);

        var data = new Dictionary<string, object?>();
        foreach (var property in templateData.EnumerateObject())
        {
            if (property.NameEquals("fiscal"))
                throw new ArgumentException("templateData no puede definir el campo reservado fiscal.", nameof(templateData));
            data[property.Name] = property.Value.Clone();
        }
        data["fiscal"] = fiscal;
        var element = JsonSerializer.SerializeToElement(data);
        var pdf = await pdfClient.RenderAsync(template, element, cancellationToken);
        return new InvoiceWithPdfResult(fiscal, Convert.ToBase64String(pdf));
    }
}
