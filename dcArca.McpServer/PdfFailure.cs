using System.Net;
using System.Text;
using System.Text.Json;

namespace dcArca.McpServer;

public enum PdfFailureKind
{
    InvalidRequest,
    Unauthorized,
    Forbidden,
    TemplateNotFound,
    TemplateNotPublished,
    TemplateAmbiguous,
    RateLimited,
    Timeout,
    Unavailable,
    InvalidResponse,
    RenderFailed
}

public sealed class CreadorPdfException : Exception
{
    public const string ProviderId = "creadorpdf";

    public PdfFailureKind FailureKind { get; }
    public int? ProviderStatusCode { get; }
    public string? ProviderErrorCode { get; }
    public string Provider => ProviderId;

    public CreadorPdfException(
        PdfFailureKind failureKind,
        string safeMessage,
        int? providerStatusCode = null,
        string? providerErrorCode = null,
        Exception? innerException = null)
        : base(safeMessage, innerException)
    {
        FailureKind = failureKind;
        ProviderStatusCode = providerStatusCode;
        ProviderErrorCode = string.IsNullOrWhiteSpace(providerErrorCode) ? null : providerErrorCode;
    }
}

public static class PdfFailureContract
{
    public static string ToPublicCode(PdfFailureKind kind) => kind switch
    {
        PdfFailureKind.InvalidRequest => "PDF_INVALID_REQUEST",
        PdfFailureKind.Unauthorized => "PDF_PROVIDER_UNAUTHORIZED",
        PdfFailureKind.Forbidden => "PDF_PROVIDER_FORBIDDEN",
        PdfFailureKind.TemplateNotFound => "PDF_TEMPLATE_NOT_FOUND",
        PdfFailureKind.TemplateNotPublished => "PDF_TEMPLATE_NOT_PUBLISHED",
        PdfFailureKind.TemplateAmbiguous => "PDF_TEMPLATE_AMBIGUOUS",
        PdfFailureKind.RateLimited => "PDF_RATE_LIMITED",
        PdfFailureKind.Timeout => "PDF_TIMEOUT",
        PdfFailureKind.Unavailable => "PDF_UNAVAILABLE",
        PdfFailureKind.InvalidResponse => "PDF_INVALID_RESPONSE",
        _ => "PDF_RENDER_FAILED"
    };

    public static string ToSafeMessage(PdfFailureKind kind) => kind switch
    {
        PdfFailureKind.InvalidRequest => "El renderer rechazó los datos de la solicitud.",
        PdfFailureKind.Unauthorized => "La credencial configurada para el renderer no es válida.",
        PdfFailureKind.Forbidden => "La credencial del renderer no posee permiso para esta operación o recurso.",
        PdfFailureKind.TemplateNotFound => "La plantilla solicitada no existe o no está publicada para la credencial configurada.",
        PdfFailureKind.TemplateNotPublished => "La plantilla solicitada existe pero no está publicada.",
        PdfFailureKind.TemplateAmbiguous => "La referencia lógica de plantilla coincide con más de una plantilla publicada.",
        PdfFailureKind.RateLimited => "El renderer limitó temporalmente la cantidad de solicitudes.",
        PdfFailureKind.Timeout => "El renderer de PDF excedió el tiempo de espera.",
        PdfFailureKind.Unavailable => "El renderer de PDF no está disponible temporalmente.",
        PdfFailureKind.InvalidResponse => "El renderer de PDF devolvió una respuesta inválida.",
        _ => "No fue posible generar el PDF solicitado."
    };
}

internal static class CreadorPdfHttpErrors
{
    public const int MaxErrorBodyBytes = 64 * 1024;

    public static async Task<CreadorPdfException> FromResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var providerErrorCode = await TryReadProviderErrorCodeAsync(response.Content, cancellationToken);
        var failureKind = Classify(response.StatusCode, providerErrorCode);
        return new CreadorPdfException(
            failureKind,
            PdfFailureContract.ToSafeMessage(failureKind),
            (int)response.StatusCode,
            providerErrorCode);
    }

    public static PdfFailureKind Classify(HttpStatusCode statusCode, string? providerErrorCode)
    {
        if (statusCode == HttpStatusCode.NotFound
            && string.Equals(providerErrorCode, "unknown_template", StringComparison.OrdinalIgnoreCase))
        {
            return PdfFailureKind.TemplateNotFound;
        }

        return (int)statusCode switch
        {
            400 or 422 => PdfFailureKind.InvalidRequest,
            401 => PdfFailureKind.Unauthorized,
            403 => PdfFailureKind.Forbidden,
            408 => PdfFailureKind.Timeout,
            429 => PdfFailureKind.RateLimited,
            >= 500 => PdfFailureKind.Unavailable,
            _ => PdfFailureKind.RenderFailed
        };
    }

    private static async Task<string?> TryReadProviderErrorCodeAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[MaxErrorBodyBytes + 1];
            var total = 0;
            while (total < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
                if (read == 0) break;
                total += read;
            }

            if (total == 0 || total > MaxErrorBodyBytes)
                return null;

            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, total));
            if (!document.RootElement.TryGetProperty("error", out var error)
                || error.ValueKind != JsonValueKind.Object
                || !error.TryGetProperty("code", out var code)
                || code.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var value = code.GetString();
            return string.IsNullOrWhiteSpace(value) || value.Length > 128 ? null : value;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
