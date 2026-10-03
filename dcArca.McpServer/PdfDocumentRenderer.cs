using System.Text;
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
    public const int MaxTemplateDataBytes = 128 * 1024;
    public const int MaxTemplateDataDepth = 16;

    private static readonly HashSet<string> ReservedFiscalRootNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "fiscal",
        "contractVersion",
        "provenance",
        "availableFields",
        "environment",
        "emisorCuit",
        "puntoVenta",
        "tipoComprobante",
        "numeroComprobante",
        "concepto",
        "documentoReceptorTipo",
        "documentoReceptorNumero",
        "condicionIvaReceptor",
        "fechaComprobante",
        "fechaServicioDesde",
        "fechaServicioHasta",
        "fechaVencimientoPago",
        "importeNeto",
        "importeNoGravado",
        "importeExento",
        "importeIva",
        "importeTributos",
        "importeTotal",
        "iva",
        "tributos",
        "monedaId",
        "monedaCotizacion",
        "cae",
        "caeVencimiento",
        "resultado",
        "comprobantesAsociados",
        "periodoAsociado"
    };

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

        var rawSize = Encoding.UTF8.GetByteCount(templateData.GetRawText());
        if (rawSize > MaxTemplateDataBytes)
            throw new ArgumentException($"templateData excede el límite de {MaxTemplateDataBytes} bytes.", nameof(templateData));

        ValidateDepth(templateData, 1);
        foreach (var property in templateData.EnumerateObject())
        {
            if (ReservedFiscalRootNames.Contains(property.Name))
            {
                throw new ArgumentException(
                    $"templateData no puede definir el campo fiscal reservado '{property.Name}'.",
                    nameof(templateData));
            }
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
            if (ReservedFiscalRootNames.Contains(property.Name))
                throw new ArgumentException($"templateData no puede definir el campo fiscal reservado '{property.Name}'.", nameof(templateData));
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

    private static void ValidateDepth(JsonElement element, int depth)
    {
        if (depth > MaxTemplateDataDepth)
            throw new ArgumentException($"templateData excede la profundidad máxima de {MaxTemplateDataDepth} niveles.", nameof(element));

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    ValidateDepth(property.Value, depth + 1);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    ValidateDepth(item, depth + 1);
                break;
        }
    }
}
