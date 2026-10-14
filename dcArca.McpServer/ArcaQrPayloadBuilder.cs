using System.Globalization;
using System.Text;
using System.Text.Json;

namespace dcArca.McpServer;

public static class ArcaQrPayloadBuilder
{
    private const string VerificationBaseUrl = "https://www.arca.gob.ar/fe/qr/?p=";
    private const string CaeAuthorizationType = "E";

    public static ArcaQrPayload Build(FiscalDocumentSnapshot fiscal)
    {
        ArgumentNullException.ThrowIfNull(fiscal);

        var cuit = ParseFixedLengthDigits(fiscal.EmisorCuit, 11, "emisorCuit");
        var cae = ParseFixedLengthDigits(fiscal.Cae, 14, "cae");
        var fecha = ParseFiscalDate(fiscal.FechaComprobante);

        if (fiscal.PuntoVenta <= 0)
            throw Error("El punto de venta fiscal no es válido para el QR.");
        if (string.IsNullOrWhiteSpace(fiscal.MonedaId))
            throw Error("La moneda fiscal no es válida para el QR.");
        if (fiscal.MonedaCotizacion <= 0m)
            throw Error("La cotización fiscal no es válida para el QR.");

        var (tipoDocRec, nroDocRec) = ResolveReceptor(fiscal);

        return new ArcaQrPayload(
            Ver: 1,
            Fecha: fecha,
            Cuit: cuit,
            PtoVta: fiscal.PuntoVenta,
            TipoCmp: fiscal.TipoComprobante,
            NroCmp: fiscal.NumeroComprobante,
            Importe: fiscal.ImporteTotal,
            Moneda: fiscal.MonedaId,
            Ctz: fiscal.MonedaCotizacion,
            TipoDocRec: tipoDocRec,
            NroDocRec: nroDocRec,
            TipoCodAut: CaeAuthorizationType,
            CodAut: cae);
    }

    public static string BuildVerificationUrl(FiscalDocumentSnapshot fiscal)
    {
        var payload = Build(fiscal);
        var json = JsonSerializer.Serialize(payload);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        return VerificationBaseUrl + encoded;
    }

    private static (int? TipoDocRec, long? NroDocRec) ResolveReceptor(FiscalDocumentSnapshot fiscal)
    {
        var hasTipo = fiscal.DocumentoReceptorTipo.HasValue;
        var hasNumero = fiscal.DocumentoReceptorNumero.HasValue;

        if (hasTipo != hasNumero)
            throw Error("El documento del receptor fiscal está incompleto para el QR.");

        return hasTipo
            ? (fiscal.DocumentoReceptorTipo!.Value, fiscal.DocumentoReceptorNumero!.Value)
            : (null, null);
    }

    private static string ParseFiscalDate(string fechaComprobante)
    {
        if (string.IsNullOrWhiteSpace(fechaComprobante)
            || fechaComprobante.Length != 8
            || !fechaComprobante.All(char.IsAsciiDigit)
            || !DateTime.TryParseExact(
                fechaComprobante,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            throw Error("La fecha fiscal del comprobante no es válida para el QR.");
        }

        return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static long ParseFixedLengthDigits(string value, int length, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != length
            || !value.All(char.IsAsciiDigit)
            || !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            throw Error($"El campo fiscal '{fieldName}' no es válido para el QR.");
        }

        return parsed;
    }

    private static ArcaQrPayloadException Error(string message) => new(message);
}
