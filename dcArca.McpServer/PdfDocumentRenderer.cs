using System.Text.Json;

namespace dcArca.McpServer;

public interface IPdfDocumentRenderer
{
    void ValidateRequest(PdfTemplateReference template, JsonElement templateData);
    void ValidateConfiguration();
    Task<PdfRenderResult> RenderAsync(
        FiscalDocumentSnapshot fiscal,
        PdfTemplateReference template,
        JsonElement templateData,
        CancellationToken cancellationToken = default);
}

public sealed class PdfDocumentRenderer(IPdfClient pdfClient) : IPdfDocumentRenderer
{
    public void ValidateRequest(PdfTemplateReference template, JsonElement templateData)
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

    public void ValidateConfiguration() => pdfClient.ValidateConfiguration();

    public async Task<PdfRenderResult> RenderAsync(
        FiscalDocumentSnapshot fiscal,
        PdfTemplateReference template,
        JsonElement templateData,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(template, templateData);
        ValidateConfiguration();

        var data = new Dictionary<string, object?>();
        foreach (var property in templateData.EnumerateObject())
        {
            if (property.NameEquals("fiscal"))
                throw new ArgumentException("templateData no puede definir el campo reservado fiscal.", nameof(templateData));
            data[property.Name] = property.Value.Clone();
        }
        data["fiscal"] = fiscal;
        var element = JsonSerializer.SerializeToElement(data);

        try
        {
            var pdf = await pdfClient.RenderAsync(template, element, cancellationToken);
            return new PdfRenderResult(PdfRenderStatus.Rendered, Convert.ToBase64String(pdf), null, null);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new PdfRenderResult(PdfRenderStatus.Failed, null, "PDF_UNAVAILABLE", "El renderer de PDF excedió el tiempo de espera.");
        }
        catch (HttpRequestException)
        {
            return new PdfRenderResult(PdfRenderStatus.Failed, null, "PDF_UNAVAILABLE", "No fue posible comunicarse con el renderer de PDF.");
        }
        catch (InvalidDataException)
        {
            return new PdfRenderResult(PdfRenderStatus.Failed, null, "PDF_INVALID_RESPONSE", "El renderer de PDF devolvió una respuesta inválida.");
        }
    }
}
