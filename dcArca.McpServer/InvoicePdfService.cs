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

public sealed class InvoicePdfService(IInvoiceIssuer issuer, IPdfClient pdfClient)
{
    public async Task<InvoiceWithPdfResult> EmitAsync(
        dcFacturaRequest invoice,
        PdfTemplateReference template,
        JsonElement templateData,
        CancellationToken cancellationToken = default)
    {
        ValidateRenderRequest(template, templateData);
        pdfClient.ValidateConfiguration();

        invoice.NumeroComprobante = null;
        var fiscal = await issuer.EmitAsync(invoice, cancellationToken);
        if (!fiscal.Success)
        {
            return new InvoiceWithPdfResult(
                fiscal,
                new PdfRenderResult(PdfRenderStatus.NotAttempted, null, null, null));
        }

        try
        {
            var element = BuildDocumentData(templateData, fiscal);
            var pdf = await pdfClient.RenderAsync(template, element, cancellationToken);
            return new InvoiceWithPdfResult(
                fiscal,
                new PdfRenderResult(PdfRenderStatus.Rendered, Convert.ToBase64String(pdf), null, null));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PdfFailed(fiscal, "PDF_UNAVAILABLE", "El renderer de PDF excedió el tiempo de espera.");
        }
        catch (HttpRequestException)
        {
            return PdfFailed(fiscal, "PDF_UNAVAILABLE", "No fue posible comunicarse con el renderer de PDF.");
        }
        catch (InvalidDataException)
        {
            return PdfFailed(fiscal, "PDF_INVALID_RESPONSE", "El renderer de PDF devolvió una respuesta inválida.");
        }
    }

    private static void ValidateRenderRequest(PdfTemplateReference template, JsonElement templateData)
    {
        if (template is null)
            throw new ArgumentNullException(nameof(template));
        if (string.IsNullOrWhiteSpace(template.Id))
            throw new ArgumentException("templateId es obligatorio.", nameof(template));
        if (string.IsNullOrWhiteSpace(template.Version))
            throw new ArgumentException("templateVersion es obligatorio.", nameof(template));
        if (templateData.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("templateData debe ser un objeto JSON.", nameof(templateData));

        foreach (var property in templateData.EnumerateObject())
        {
            if (property.NameEquals("fiscal"))
                throw new ArgumentException("templateData no puede definir el campo reservado fiscal.", nameof(templateData));
        }
    }

    private static JsonElement BuildDocumentData(JsonElement templateData, dcFacturaResponse fiscal)
    {
        var data = new Dictionary<string, object?>();
        foreach (var property in templateData.EnumerateObject())
        {
            if (property.NameEquals("fiscal"))
                throw new ArgumentException("templateData no puede definir el campo reservado fiscal.", nameof(templateData));
            data[property.Name] = property.Value.Clone();
        }

        data["fiscal"] = fiscal;
        return JsonSerializer.SerializeToElement(data);
    }

    private static InvoiceWithPdfResult PdfFailed(dcFacturaResponse fiscal, string code, string message)
        => new(
            fiscal,
            new PdfRenderResult(PdfRenderStatus.Failed, null, code, message));
}
