using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using dcArca.Core.Models;

namespace dcArca.McpServer;

public static class EmissionRequestFingerprint
{
    public static string KeyHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("idempotencyKey es obligatoria.", nameof(value));
        return HexSha256(value.Trim());
    }

    public static string RequestHash(dcFacturaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var canonical = new
        {
            TipoComprobante = request.TipoComprobante.HasValue ? (int)request.TipoComprobante.Value : (int?)null,
            Concepto = request.Concepto.HasValue ? (int)request.Concepto.Value : (int?)null,
            request.CuitReceptor,
            request.TipoDocReceptor,
            CondicionIvaReceptor = request.CondicionIvaReceptor.HasValue ? (int)request.CondicionIvaReceptor.Value : (int?)null,
            request.ImporteNeto,
            request.ImporteIva,
            request.ImporteTotal,
            request.ImporteNoGravado,
            request.ImporteExento,
            AlicuotaIva = request.AlicuotaIva.HasValue ? (int)request.AlicuotaIva.Value : (int?)null,
            MonedaId = request.MonedaId?.Trim() ?? string.Empty,
            request.MonedaCotizacion,
            FechaComprobante = request.FechaComprobante?.Trim() ?? string.Empty,
            FechaServicioDesde = request.FechaServicioDesde?.Trim(),
            FechaServicioHasta = request.FechaServicioHasta?.Trim(),
            FechaVencimiento = request.FechaVencimiento?.Trim(),
            request.CbteAsociadoTipo,
            request.CbteAsociadoPtoVta,
            request.CbteAsociadoNro,
            CbteAsociadoCuit = request.CbteAsociadoCuit?.Trim(),
            CbteAsociadoFecha = request.CbteAsociadoFecha?.Trim(),
            PeriodoAsocDesde = request.PeriodoAsocDesde?.Trim(),
            PeriodoAsocHasta = request.PeriodoAsocHasta?.Trim(),
            Iva = request.Iva
                .Select(x => new { Alicuota = (int)x.Alicuota, x.BaseImponible, x.Importe })
                .OrderBy(x => x.Alicuota)
                .ThenBy(x => x.BaseImponible)
                .ThenBy(x => x.Importe)
                .ToArray(),
            Tributos = request.Tributos
                .Select(x => new { x.Id, Descripcion = x.Descripcion?.Trim() ?? string.Empty, x.BaseImponible, x.Alicuota, x.Importe })
                .OrderBy(x => x.Id)
                .ThenBy(x => x.Descripcion, StringComparer.Ordinal)
                .ThenBy(x => x.BaseImponible)
                .ThenBy(x => x.Alicuota)
                .ThenBy(x => x.Importe)
                .ToArray()
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(canonical);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string HexSha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
