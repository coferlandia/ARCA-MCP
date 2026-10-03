using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

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

public sealed class PdfDocumentRenderer(
    IPdfClient pdfClient,
    ILogger<PdfDocumentRenderer>? logger = null) : IPdfDocumentRenderer
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
        if (string.IsNullOrWhiteSpace(template.Key))
            throw new ArgumentException("templateId/templateKey es obligatorio.", nameof(template));
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
        catch (CreadorPdfException exception)
        {
            logger?.LogWarning(
                "PDF provider failure: provider={PdfProvider} failure_kind={PdfFailureKind} provider_status_code={PdfProviderStatusCode} provider_error_code={PdfProviderErrorCode} template_reference={PdfTemplateReference} template_version={PdfTemplateVersion}",
                exception.Provider,
                exception.FailureKind,
                exception.ProviderStatusCode,
                exception.ProviderErrorCode,
                template.Key,
                template.Version);

            return new PdfRenderResult(
                PdfRenderStatus.Failed,
                null,
                PdfFailureContract.ToPublicCode(exception.FailureKind),
                PdfFailureContract.ToSafeMessage(exception.FailureKind),
                exception.Provider,
                exception.ProviderStatusCode,
                exception.ProviderErrorCode,
                exception.FailureKind);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new PdfRenderResult(
                PdfRenderStatus.Failed,
                null,
                PdfFailureContract.ToPublicCode(PdfFailureKind.Timeout),
                PdfFailureContract.ToSafeMessage(PdfFailureKind.Timeout),
                FailureKind: PdfFailureKind.Timeout);
        }
        catch (HttpRequestException)
        {
            return new PdfRenderResult(
                PdfRenderStatus.Failed,
                null,
                PdfFailureContract.ToPublicCode(PdfFailureKind.Unavailable),
                PdfFailureContract.ToSafeMessage(PdfFailureKind.Unavailable),
                FailureKind: PdfFailureKind.Unavailable);
        }
        catch (InvalidDataException)
        {
            return new PdfRenderResult(
                PdfRenderStatus.Failed,
                null,
                PdfFailureContract.ToPublicCode(PdfFailureKind.InvalidResponse),
                PdfFailureContract.ToSafeMessage(PdfFailureKind.InvalidResponse),
                FailureKind: PdfFailureKind.InvalidResponse);
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
