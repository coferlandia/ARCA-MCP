using System.Globalization;
using dcArca.Core.Models;

namespace dcArca.Core.Services;

/// <summary>
/// Resultado determinístico de la validación previa a cualquier reserva de numeración o envío fiscal.
/// </summary>
public sealed record dcFacturaPreflightValidation(
    bool IsValid,
    string? Code = null,
    string? Message = null)
{
    public static dcFacturaPreflightValidation Valid { get; } = new(true);
}

/// <summary>
/// Valida exclusivamente reglas locales/determinísticas que no requieren número de comprobante,
/// autenticación ni I/O con ARCA. Es seguro ejecutarlo antes de crear una operación idempotente.
/// </summary>
public static class dcFacturaPreflightValidator
{
    public static dcFacturaPreflightValidation Validate(dcFacturaRequest? factura)
    {
        if (factura is null)
            return Invalid("FACTURA_NULL", "No se recibieron datos de la factura a autorizar.");

        if (!factura.TieneTipoComprobanteValido())
            return Invalid("TIPOC_INVALID", "El Tipo de Comprobante es obligatorio y debe corresponder a un valor válido definido por ARCA.");

        if (!factura.TieneConceptoValido() || factura.Concepto is null)
            return Invalid("CONCEPTO_MISSING", "Concepto es obligatorio y debe ser uno de los valores soportados (1=Productos, 2=Servicios, 3=Productos y Servicios).");

        var concepto = (int)factura.Concepto.Value;
        if (concepto != 1)
        {
            if (string.IsNullOrWhiteSpace(factura.FechaServicioDesde))
                return Invalid("FCHSD_REQUIRED", "FechaServicioDesde es obligatoria para comprobantes de servicios (Concepto 2 o 3).");
            if (string.IsNullOrWhiteSpace(factura.FechaServicioHasta))
                return Invalid("FCHSH_REQUIRED", "FechaServicioHasta es obligatoria para comprobantes de servicios (Concepto 2 o 3).");
            if (string.IsNullOrWhiteSpace(factura.FechaVencimiento))
                return Invalid("FCHVTO_REQUIRED", "FechaVencimiento es obligatoria para comprobantes de servicios (Concepto 2 o 3).");
        }

        var fiscal = dcFacturaFiscalValidator.Validate(factura);
        if (!fiscal.IsValid)
            return Invalid(
                fiscal.Code ?? "FISCAL_INVALID",
                fiscal.Message ?? "El desglose fiscal del comprobante no es consistente.");

        if (string.IsNullOrWhiteSpace(factura.FechaComprobante))
            return Invalid("FCHCBTE_REQUIRED", "FechaComprobante es obligatoria y debe tener formato YYYYMMDD.");

        if (!DateTime.TryParseExact(
                factura.FechaComprobante,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            return Invalid("FCHCBTE_INVALID", "FechaComprobante no tiene el formato válido YYYYMMDD.");
        }

        var documento = dcDocumentoReceptorValidator.Validate(factura.TipoDocReceptor, factura.CuitReceptor);
        if (!documento.IsValid)
            return Invalid(
                documento.Code ?? "DOC_INVALID",
                documento.Message ?? "El documento del receptor no es válido.");

        if (factura.EsNota() && !factura.CumpleReglaNotas10197())
        {
            return Invalid(
                "NOTA_CBTEASOC_10197",
                "Notas de Débito / Crédito deben informar comprobante asociado (CbteAsoc) o periodo asociado (PeriodoAsoc) - Error 10197.");
        }

        return dcFacturaPreflightValidation.Valid;
    }

    private static dcFacturaPreflightValidation Invalid(string code, string message)
        => new(false, code, message);
}
