using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using dcArca.Core.Models;

namespace dcArca.McpServer;

public sealed record FiscalAssociatedDocumentProjection(
    int? Tipo,
    int? PuntoVenta,
    long? Numero,
    string? Cuit,
    string? Fecha);

public sealed record FiscalRequestProjection(
    int TipoComprobante,
    int? Concepto,
    long CuitReceptor,
    int TipoDocReceptor,
    int? CondicionIvaReceptor,
    decimal ImporteNeto,
    decimal ImporteIva,
    decimal ImporteTotal,
    decimal ImporteNoGravado,
    decimal ImporteExento,
    decimal ImporteTributos,
    string MonedaId,
    decimal MonedaCotizacion,
    string FechaComprobante,
    string? FechaServicioDesde,
    string? FechaServicioHasta,
    string? FechaVencimiento,
    FiscalAssociatedDocumentProjection ComprobanteAsociado,
    string? PeriodoAsocDesde,
    string? PeriodoAsocHasta,
    IReadOnlyList<FiscalIvaSnapshot> Iva,
    IReadOnlyList<FiscalTributoSnapshot> Tributos);

public static class EmissionRequestFingerprint
{
    // Version 1 is the canonicalization already used by the legacy store. Keep its byte
    // representation stable so migrated records remain replayable.
    public const int CanonicalizationVersion = 1;
    public const int FiscalProjectionVersion = 1;

    public static string KeyHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("idempotencyKey es obligatoria.", nameof(value));
        return HexSha256(value.Trim());
    }

    public static string OperationKeyHash(string consumerId, string contextId, string idempotencyKey)
        => OperationKeyHashFromLegacyKeyHash(consumerId, contextId, KeyHash(idempotencyKey));

    public static string OperationKeyHashFromLegacyKeyHash(string consumerId, string contextId, string legacyKeyHash)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
            throw new ArgumentException("consumerId es obligatorio.", nameof(consumerId));
        if (string.IsNullOrWhiteSpace(contextId))
            throw new ArgumentException("contextId es obligatorio.", nameof(contextId));
        ValidateHash(legacyKeyHash, nameof(legacyKeyHash));

        var canonical = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version = 1,
            ConsumerId = consumerId.Trim(),
            ContextId = contextId.Trim(),
            LegacyKeyHash = legacyKeyHash.ToLowerInvariant()
        });
        return Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant();
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

    public static FiscalRequestProjection Project(dcFacturaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.TipoComprobante.HasValue)
            throw new ArgumentException("TipoComprobante es obligatorio para proyectar evidencia fiscal.", nameof(request));

        var iva = request.Iva.Count > 0
            ? request.Iva
                .Select(x => new FiscalIvaSnapshot((int)x.Alicuota, x.BaseImponible, x.Importe))
                .OrderBy(x => x.Alicuota)
                .ThenBy(x => x.BaseImponible)
                .ThenBy(x => x.Importe)
                .ToArray()
            : request.AlicuotaIva.HasValue && request.ImporteIva != 0m
                ? [new FiscalIvaSnapshot((int)request.AlicuotaIva.Value, request.ImporteNeto, request.ImporteIva)]
                : Array.Empty<FiscalIvaSnapshot>();

        var tributos = request.Tributos
            .Select(x => new FiscalTributoSnapshot(
                x.Id,
                x.Descripcion?.Trim() ?? string.Empty,
                x.BaseImponible,
                x.Alicuota,
                x.Importe))
            .OrderBy(x => x.Id)
            .ThenBy(x => x.Descripcion, StringComparer.Ordinal)
            .ThenBy(x => x.BaseImponible)
            .ThenBy(x => x.Alicuota)
            .ThenBy(x => x.Importe)
            .ToArray();

        return new FiscalRequestProjection(
            (int)request.TipoComprobante.Value,
            request.Concepto.HasValue ? (int)request.Concepto.Value : null,
            request.CuitReceptor,
            request.TipoDocReceptor,
            request.CondicionIvaReceptor.HasValue ? (int)request.CondicionIvaReceptor.Value : null,
            request.ImporteNeto,
            request.ImporteIva,
            request.ImporteTotal,
            request.ImporteNoGravado,
            request.ImporteExento,
            request.ImporteTributos,
            request.MonedaId?.Trim() ?? string.Empty,
            request.MonedaCotizacion,
            request.FechaComprobante?.Trim() ?? string.Empty,
            request.FechaServicioDesde?.Trim(),
            request.FechaServicioHasta?.Trim(),
            request.FechaVencimiento?.Trim(),
            new FiscalAssociatedDocumentProjection(
                request.CbteAsociadoTipo,
                request.CbteAsociadoPtoVta,
                request.CbteAsociadoNro,
                request.CbteAsociadoCuit?.Trim(),
                request.CbteAsociadoFecha?.Trim()),
            request.PeriodoAsocDesde?.Trim(),
            request.PeriodoAsocHasta?.Trim(),
            iva,
            tributos);
    }

    private static string HexSha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void ValidateHash(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 || !value.All(Uri.IsHexDigit))
            throw new ArgumentException("Se esperaba un hash SHA-256 hexadecimal.", parameterName);
    }
}
