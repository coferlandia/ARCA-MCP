using System.Text.Json.Serialization;
using dcArca.Core.Models;

namespace dcArca.McpServer;

public sealed record FiscalIvaSnapshot(
    [property: JsonPropertyName("alicuota")] int? Alicuota,
    [property: JsonPropertyName("baseImponible")] decimal BaseImponible,
    [property: JsonPropertyName("importe")] decimal Importe);

public sealed record FiscalTributoSnapshot(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("descripcion")] string Descripcion,
    [property: JsonPropertyName("baseImponible")] decimal BaseImponible,
    [property: JsonPropertyName("alicuota")] decimal Alicuota,
    [property: JsonPropertyName("importe")] decimal Importe);

public sealed record FiscalDocumentSnapshot
{
    [JsonPropertyName("emisorCuit")]
    public required string EmisorCuit { get; init; }

    [JsonPropertyName("puntoVenta")]
    public required int PuntoVenta { get; init; }

    [JsonPropertyName("tipoComprobante")]
    public required int TipoComprobante { get; init; }

    [JsonPropertyName("numeroComprobante")]
    public required long NumeroComprobante { get; init; }

    [JsonPropertyName("concepto")]
    public int? Concepto { get; init; }

    [JsonPropertyName("documentoReceptorTipo")]
    public int? DocumentoReceptorTipo { get; init; }

    [JsonPropertyName("documentoReceptorNumero")]
    public long? DocumentoReceptorNumero { get; init; }

    [JsonPropertyName("condicionIvaReceptor")]
    public int? CondicionIvaReceptor { get; init; }

    [JsonPropertyName("fechaComprobante")]
    public string FechaComprobante { get; init; } = string.Empty;

    [JsonPropertyName("fechaServicioDesde")]
    public string FechaServicioDesde { get; init; } = string.Empty;

    [JsonPropertyName("fechaServicioHasta")]
    public string FechaServicioHasta { get; init; } = string.Empty;

    [JsonPropertyName("fechaVencimientoPago")]
    public string FechaVencimientoPago { get; init; } = string.Empty;

    [JsonPropertyName("importeNeto")]
    public decimal ImporteNeto { get; init; }

    [JsonPropertyName("importeNoGravado")]
    public decimal ImporteNoGravado { get; init; }

    [JsonPropertyName("importeExento")]
    public decimal ImporteExento { get; init; }

    [JsonPropertyName("importeIva")]
    public decimal ImporteIva { get; init; }

    [JsonPropertyName("importeTributos")]
    public decimal ImporteTributos { get; init; }

    [JsonPropertyName("importeTotal")]
    public decimal ImporteTotal { get; init; }

    [JsonPropertyName("iva")]
    public IReadOnlyList<FiscalIvaSnapshot> Iva { get; init; } = Array.Empty<FiscalIvaSnapshot>();

    [JsonPropertyName("tributos")]
    public IReadOnlyList<FiscalTributoSnapshot> Tributos { get; init; } = Array.Empty<FiscalTributoSnapshot>();

    [JsonPropertyName("monedaId")]
    public string MonedaId { get; init; } = string.Empty;

    [JsonPropertyName("monedaCotizacion")]
    public decimal MonedaCotizacion { get; init; }

    [JsonPropertyName("cae")]
    public required string Cae { get; init; }

    [JsonPropertyName("caeVencimiento")]
    public string CaeVencimiento { get; init; } = string.Empty;

    [JsonPropertyName("resultado")]
    public string Resultado { get; init; } = string.Empty;
}

public static class FiscalDocumentSnapshotFactory
{
    public static FiscalDocumentSnapshot FromEmission(
        dcFacturaRequest request,
        dcFacturaResponse response,
        dcArcaConfig config)
    {
        EnsureAuthorized(response);
        if (!request.TipoComprobante.HasValue)
            throw new InvalidOperationException("El request emitido no contiene TipoComprobante.");

        var iva = request.Iva.Count > 0
            ? request.Iva.Select(item => new FiscalIvaSnapshot((int)item.Alicuota, item.BaseImponible, item.Importe)).ToArray()
            : request.AlicuotaIva.HasValue && request.ImporteIva != 0m
                ? [new FiscalIvaSnapshot((int)request.AlicuotaIva.Value, request.ImporteNeto, request.ImporteIva)]
                : Array.Empty<FiscalIvaSnapshot>();

        return new FiscalDocumentSnapshot
        {
            EmisorCuit = config.Cuit,
            PuntoVenta = response.PuntoVenta ?? config.PuntoVenta,
            TipoComprobante = (int)request.TipoComprobante.Value,
            NumeroComprobante = response.NumeroComprobante,
            Concepto = request.Concepto.HasValue ? (int)request.Concepto.Value : null,
            DocumentoReceptorTipo = request.TipoDocReceptor,
            DocumentoReceptorNumero = request.CuitReceptor,
            CondicionIvaReceptor = request.CondicionIvaReceptor.HasValue ? (int)request.CondicionIvaReceptor.Value : null,
            FechaComprobante = request.FechaComprobante,
            FechaServicioDesde = request.FechaServicioDesde ?? string.Empty,
            FechaServicioHasta = request.FechaServicioHasta ?? string.Empty,
            FechaVencimientoPago = request.FechaVencimiento ?? string.Empty,
            ImporteNeto = request.ImporteNeto,
            ImporteNoGravado = request.ImporteNoGravado,
            ImporteExento = request.ImporteExento,
            ImporteIva = request.ImporteIva,
            ImporteTributos = request.ImporteTributos,
            ImporteTotal = request.ImporteTotal,
            Iva = iva,
            Tributos = request.Tributos.Select(item => new FiscalTributoSnapshot(
                item.Id, item.Descripcion, item.BaseImponible, item.Alicuota, item.Importe)).ToArray(),
            MonedaId = request.MonedaId,
            MonedaCotizacion = request.MonedaCotizacion,
            Cae = response.Cae,
            CaeVencimiento = response.CaeVencimiento,
            Resultado = response.Resultado
        };
    }

    public static FiscalDocumentSnapshot FromConsult(dcFacturaResponse response, dcArcaConfig config)
    {
        EnsureAuthorized(response);
        var tipo = response.TipoComprobante
            ?? throw new InvalidOperationException("La consulta no devolvió TipoComprobante.");
        var puntoVenta = response.PuntoVenta
            ?? throw new InvalidOperationException("La consulta no devolvió PuntoVenta.");

        return new FiscalDocumentSnapshot
        {
            EmisorCuit = config.Cuit,
            PuntoVenta = puntoVenta,
            TipoComprobante = (int)tipo,
            NumeroComprobante = response.NumeroComprobante,
            Concepto = response.Concepto.HasValue ? (int)response.Concepto.Value : null,
            DocumentoReceptorTipo = response.DocTipo.HasValue ? (int)response.DocTipo.Value : null,
            DocumentoReceptorNumero = response.DocNro,
            CondicionIvaReceptor = response.CondicionIvaReceptor.HasValue ? (int)response.CondicionIvaReceptor.Value : null,
            FechaComprobante = response.FechaComprobante,
            FechaServicioDesde = response.FechaServicioDesde,
            FechaServicioHasta = response.FechaServicioHasta,
            FechaVencimientoPago = response.FechaVencimientoPago,
            ImporteNeto = response.ImporteNeto,
            ImporteNoGravado = response.ImporteNoGravado,
            ImporteExento = response.ImporteExento,
            ImporteIva = response.ImporteIva,
            ImporteTributos = response.ImporteTributos,
            ImporteTotal = response.ImporteTotal,
            Iva = response.Iva.Select(item => new FiscalIvaSnapshot(
                item.Alicuota.HasValue ? (int)item.Alicuota.Value : null,
                item.BaseImponible,
                item.Importe)).ToArray(),
            Tributos = response.Tributos.Select(item => new FiscalTributoSnapshot(
                item.Id, item.Descripcion, item.BaseImponible, item.Alicuota, item.Importe)).ToArray(),
            MonedaId = response.MonedaId,
            MonedaCotizacion = response.MonedaCotizacion,
            Cae = response.Cae,
            CaeVencimiento = response.CaeVencimiento,
            Resultado = response.Resultado
        };
    }

    private static void EnsureAuthorized(dcFacturaResponse response)
    {
        if (!response.Success || response.NumeroComprobante <= 0 || string.IsNullOrWhiteSpace(response.Cae))
            throw new InvalidOperationException("FISCAL_DOCUMENT_NOT_AUTHORIZED");
    }
}
