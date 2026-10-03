using System.Text.Json.Serialization;
using dcArca.Core.Models;

namespace dcArca.McpServer;

public static class FiscalDocumentSnapshotContract
{
    public const string Version = "arca-fiscal-snapshot/2.0";
    public const decimal MonetaryTolerance = 0.01m;
}

public sealed record FiscalDocumentContext(
    string Environment,
    string EmisorCuit,
    int PuntoVenta)
{
    public static FiscalDocumentContext FromLegacyConfig(dcArcaConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new FiscalDocumentContext(
            string.IsNullOrWhiteSpace(config.Environment) ? "unspecified" : config.Environment,
            config.Cuit,
            config.PuntoVenta);
    }
}

public sealed record FiscalSnapshotProvenance(
    [property: JsonPropertyName("identity")] string Identity,
    [property: JsonPropertyName("fiscalData")] string FiscalData,
    [property: JsonPropertyName("authorization")] string Authorization);

public sealed record FiscalAssociatedDocumentSnapshot(
    [property: JsonPropertyName("tipo")] int Tipo,
    [property: JsonPropertyName("puntoVenta")] int PuntoVenta,
    [property: JsonPropertyName("numero")] long Numero,
    [property: JsonPropertyName("cuit")] string? Cuit,
    [property: JsonPropertyName("fecha")] string? Fecha);

public sealed record FiscalAssociatedPeriodSnapshot(
    [property: JsonPropertyName("desde")] string Desde,
    [property: JsonPropertyName("hasta")] string Hasta);

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

public sealed class FiscalDocumentSnapshotException : InvalidOperationException
{
    public string Code { get; }
    public string SafeMessage { get; }

    public FiscalDocumentSnapshotException(string code, string safeMessage)
        : base(code)
    {
        Code = code;
        SafeMessage = safeMessage;
    }
}

public sealed record FiscalDocumentSnapshot
{
    [JsonPropertyName("contractVersion")]
    public string ContractVersion { get; init; } = FiscalDocumentSnapshotContract.Version;

    [JsonPropertyName("provenance")]
    public FiscalSnapshotProvenance Provenance { get; init; } =
        new("unspecified", "unspecified", "unspecified");

    [JsonPropertyName("availableFields")]
    public IReadOnlyList<string> AvailableFields { get; init; } = Array.Empty<string>();

    [JsonPropertyName("environment")]
    public string Environment { get; init; } = "unspecified";

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

    [JsonPropertyName("comprobantesAsociados")]
    public IReadOnlyList<FiscalAssociatedDocumentSnapshot> ComprobantesAsociados { get; init; } = Array.Empty<FiscalAssociatedDocumentSnapshot>();

    [JsonPropertyName("periodoAsociado")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FiscalAssociatedPeriodSnapshot? PeriodoAsociado { get; init; }

    [JsonPropertyName("cae")]
    public required string Cae { get; init; }

    [JsonPropertyName("caeVencimiento")]
    public string CaeVencimiento { get; init; } = string.Empty;

    [JsonPropertyName("resultado")]
    public string Resultado { get; init; } = string.Empty;
}

public static class FiscalDocumentSnapshotFactory
{
    private static readonly string[] BaseAvailableFields =
    [
        "environment", "emisorCuit", "puntoVenta", "tipoComprobante", "numeroComprobante",
        "concepto", "documentoReceptorTipo", "documentoReceptorNumero", "fechaComprobante",
        "importeNeto", "importeNoGravado", "importeExento", "importeIva", "importeTributos",
        "importeTotal", "iva", "tributos", "monedaId", "monedaCotizacion", "cae"
    ];

    public static FiscalDocumentSnapshot FromEmission(
        dcFacturaRequest request,
        dcFacturaResponse response,
        dcArcaConfig config)
        => FromEmission(request, response, FiscalDocumentContext.FromLegacyConfig(config));

    public static FiscalDocumentSnapshot FromEmission(
        dcFacturaRequest request,
        dcFacturaResponse response,
        FiscalDocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        ValidateContext(context);
        EnsureAuthorized(response);
        if (!request.TipoComprobante.HasValue)
            throw Error("FISCAL_DOCUMENT_INCOMPLETE", "El request emitido no contiene TipoComprobante.");
        if (response.PuntoVenta.HasValue && response.PuntoVenta.Value != context.PuntoVenta)
            throw Error("FISCAL_CONTEXT_MISMATCH", "El punto de venta autorizado no coincide con el contexto fiscal usado para renderizar.");

        var iva = request.Iva.Count > 0
            ? request.Iva.Select(item => new FiscalIvaSnapshot((int)item.Alicuota, item.BaseImponible, item.Importe)).ToArray()
            : request.AlicuotaIva.HasValue && request.ImporteIva != 0m
                ? [new FiscalIvaSnapshot((int)request.AlicuotaIva.Value, request.ImporteNeto, request.ImporteIva)]
                : Array.Empty<FiscalIvaSnapshot>();
        var associations = BuildAssociations(request);
        var period = BuildAssociatedPeriod(request);
        var available = BuildAvailableFields(
            request.CondicionIvaReceptor.HasValue,
            !string.IsNullOrWhiteSpace(request.FechaServicioDesde),
            !string.IsNullOrWhiteSpace(request.FechaServicioHasta),
            !string.IsNullOrWhiteSpace(request.FechaVencimiento),
            associations.Length > 0,
            period is not null,
            !string.IsNullOrWhiteSpace(response.CaeVencimiento),
            !string.IsNullOrWhiteSpace(response.Resultado));

        return new FiscalDocumentSnapshot
        {
            Provenance = new FiscalSnapshotProvenance("authorized-context", "emission-request", "arca-cae-response"),
            AvailableFields = available,
            Environment = context.Environment,
            EmisorCuit = context.EmisorCuit,
            PuntoVenta = context.PuntoVenta,
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
            ComprobantesAsociados = associations,
            PeriodoAsociado = period,
            Cae = response.Cae,
            CaeVencimiento = response.CaeVencimiento,
            Resultado = response.Resultado
        };
    }

    public static FiscalDocumentSnapshot FromConsult(dcFacturaResponse response, dcArcaConfig config)
        => FromConsult(response, FiscalDocumentContext.FromLegacyConfig(config));

    public static FiscalDocumentSnapshot FromConsult(
        dcFacturaResponse response,
        FiscalDocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(response);
        ValidateContext(context);
        EnsureAuthorized(response);
        var tipo = response.TipoComprobante
            ?? throw Error("FISCAL_DOCUMENT_INCOMPLETE", "La consulta no devolvió TipoComprobante.");
        var puntoVenta = response.PuntoVenta
            ?? throw Error("FISCAL_DOCUMENT_INCOMPLETE", "La consulta no devolvió PuntoVenta.");
        if (puntoVenta != context.PuntoVenta)
            throw Error("FISCAL_CONTEXT_MISMATCH", "El punto de venta consultado no coincide con el contexto fiscal usado para renderizar.");
        if (!response.Concepto.HasValue || !response.DocTipo.HasValue || !response.DocNro.HasValue)
            throw Error("FISCAL_DOCUMENT_INCOMPLETE", "La consulta no devolvió concepto o identificación suficiente del receptor.");
        if (string.IsNullOrWhiteSpace(response.FechaComprobante))
            throw Error("FISCAL_DOCUMENT_INCOMPLETE", "La consulta no devolvió la fecha del comprobante.");
        if (string.IsNullOrWhiteSpace(response.MonedaId) || response.MonedaCotizacion <= 0m)
            throw Error("FISCAL_DOCUMENT_INCOMPLETE", "La consulta no devolvió moneda y cotización fiscal utilizables.");
        if (string.IsNullOrWhiteSpace(response.CaeVencimiento))
            throw Error("FISCAL_DOCUMENT_INCOMPLETE", "La consulta no devolvió el vencimiento de la autorización fiscal.");
        ValidateConsultAmounts(response);
        ValidateServiceDates(response);
        if (IsNote(tipo))
        {
            throw Error(
                "FISCAL_DOCUMENT_ASSOCIATION_UNAVAILABLE",
                "La consulta fiscal actual no expone la asociación necesaria para regenerar una nota sin inventar datos.");
        }

        var available = BuildAvailableFields(
            response.CondicionIvaReceptor.HasValue,
            !string.IsNullOrWhiteSpace(response.FechaServicioDesde),
            !string.IsNullOrWhiteSpace(response.FechaServicioHasta),
            !string.IsNullOrWhiteSpace(response.FechaVencimientoPago),
            hasAssociatedDocuments: false,
            hasAssociatedPeriod: false,
            hasCaeExpiry: true,
            hasResult: !string.IsNullOrWhiteSpace(response.Resultado));

        return new FiscalDocumentSnapshot
        {
            Provenance = new FiscalSnapshotProvenance("authorized-context", "fe-comp-consultar-validated", "fe-comp-consultar"),
            AvailableFields = available,
            Environment = context.Environment,
            EmisorCuit = context.EmisorCuit,
            PuntoVenta = puntoVenta,
            TipoComprobante = (int)tipo,
            NumeroComprobante = response.NumeroComprobante,
            Concepto = (int)response.Concepto.Value,
            DocumentoReceptorTipo = (int)response.DocTipo.Value,
            DocumentoReceptorNumero = response.DocNro.Value,
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

    private static void ValidateContext(FiscalDocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(context.Environment)
            || string.IsNullOrWhiteSpace(context.EmisorCuit)
            || context.PuntoVenta <= 0)
        {
            throw Error("FISCAL_CONTEXT_INVALID", "El contexto fiscal histórico no tiene ambiente, CUIT emisor y punto de venta válidos.");
        }
    }

    private static void ValidateConsultAmounts(dcFacturaResponse response)
    {
        if (response.ImporteTotal <= 0m
            || response.ImporteNeto < 0m
            || response.ImporteNoGravado < 0m
            || response.ImporteExento < 0m
            || response.ImporteIva < 0m
            || response.ImporteTributos < 0m)
        {
            throw Error("FISCAL_DOCUMENT_INCOMPLETE", "La consulta fiscal devolvió importes incompletos o inválidos.");
        }

        var expected = response.ImporteNeto
            + response.ImporteNoGravado
            + response.ImporteExento
            + response.ImporteIva
            + response.ImporteTributos;
        if (Math.Abs(expected - response.ImporteTotal) > FiscalDocumentSnapshotContract.MonetaryTolerance)
        {
            throw Error(
                "FISCAL_DOCUMENT_INCOMPLETE",
                "Los importes devueltos por la consulta no permiten reconstruir el total fiscal sin asumir valores ausentes.");
        }

        if (response.Iva.Any(x => !x.Alicuota.HasValue || x.BaseImponible < 0m || x.Importe < 0m))
            throw Error("FISCAL_DOCUMENT_INCOMPLETE", "La consulta devolvió líneas de IVA incompletas o inválidas.");
        if (response.ImporteIva > 0m && response.Iva.Count == 0)
            throw Error("FISCAL_DOCUMENT_INCOMPLETE", "La consulta no devolvió el detalle de IVA requerido por el importe agregado.");
        if (Math.Abs(response.Iva.Sum(x => x.Importe) - response.ImporteIva) > FiscalDocumentSnapshotContract.MonetaryTolerance)
            throw Error("FISCAL_DOCUMENT_INCOMPLETE", "La consulta no devolvió un detalle de IVA consistente con el total informado.");
        if (response.Iva.Count > 0
            && Math.Abs(response.Iva.Sum(x => x.BaseImponible) - response.ImporteNeto) > FiscalDocumentSnapshotContract.MonetaryTolerance)
        {
            throw Error("FISCAL_DOCUMENT_INCOMPLETE", "Las bases de IVA consultadas no reconstruyen el neto fiscal informado.");
        }

        if (response.Tributos.Any(x =>
                !x.Id.HasValue
                || string.IsNullOrWhiteSpace(x.Descripcion)
                || x.BaseImponible < 0m
                || x.Alicuota < 0m
                || x.Importe < 0m))
        {
            throw Error("FISCAL_DOCUMENT_INCOMPLETE", "La consulta devolvió líneas de tributos incompletas o inválidas.");
        }
        if (response.ImporteTributos > 0m && response.Tributos.Count == 0)
            throw Error("FISCAL_DOCUMENT_INCOMPLETE", "La consulta no devolvió el detalle de tributos requerido por el importe agregado.");
        if (Math.Abs(response.Tributos.Sum(x => x.Importe) - response.ImporteTributos) > FiscalDocumentSnapshotContract.MonetaryTolerance)
            throw Error("FISCAL_DOCUMENT_INCOMPLETE", "La consulta no devolvió tributos consistentes con el total informado.");
    }

    private static void ValidateServiceDates(dcFacturaResponse response)
    {
        if (response.Concepto is not (dcConcepto.Servicios or dcConcepto.ProductosYServicios)) return;
        if (string.IsNullOrWhiteSpace(response.FechaServicioDesde)
            || string.IsNullOrWhiteSpace(response.FechaServicioHasta)
            || string.IsNullOrWhiteSpace(response.FechaVencimientoPago))
        {
            throw Error("FISCAL_DOCUMENT_INCOMPLETE", "La consulta de un comprobante de servicios no devolvió todas las fechas fiscales requeridas.");
        }
    }

    private static FiscalAssociatedDocumentSnapshot[] BuildAssociations(dcFacturaRequest request)
    {
        if (!request.CbteAsociadoTipo.HasValue
            || !request.CbteAsociadoPtoVta.HasValue
            || !request.CbteAsociadoNro.HasValue)
            return [];

        return
        [
            new FiscalAssociatedDocumentSnapshot(
                request.CbteAsociadoTipo.Value,
                request.CbteAsociadoPtoVta.Value,
                request.CbteAsociadoNro.Value,
                string.IsNullOrWhiteSpace(request.CbteAsociadoCuit) ? null : request.CbteAsociadoCuit,
                string.IsNullOrWhiteSpace(request.CbteAsociadoFecha) ? null : request.CbteAsociadoFecha)
        ];
    }

    private static FiscalAssociatedPeriodSnapshot? BuildAssociatedPeriod(dcFacturaRequest request)
        => !string.IsNullOrWhiteSpace(request.PeriodoAsocDesde) && !string.IsNullOrWhiteSpace(request.PeriodoAsocHasta)
            ? new FiscalAssociatedPeriodSnapshot(request.PeriodoAsocDesde, request.PeriodoAsocHasta)
            : null;

    private static IReadOnlyList<string> BuildAvailableFields(
        bool hasIvaCondition,
        bool hasServiceFrom,
        bool hasServiceTo,
        bool hasPaymentDue,
        bool hasAssociatedDocuments,
        bool hasAssociatedPeriod,
        bool hasCaeExpiry,
        bool hasResult)
    {
        var fields = BaseAvailableFields.ToList();
        if (hasIvaCondition) fields.Add("condicionIvaReceptor");
        if (hasServiceFrom) fields.Add("fechaServicioDesde");
        if (hasServiceTo) fields.Add("fechaServicioHasta");
        if (hasPaymentDue) fields.Add("fechaVencimientoPago");
        if (hasAssociatedDocuments) fields.Add("comprobantesAsociados");
        if (hasAssociatedPeriod) fields.Add("periodoAsociado");
        if (hasCaeExpiry) fields.Add("caeVencimiento");
        if (hasResult) fields.Add("resultado");
        return fields.Order(StringComparer.Ordinal).ToArray();
    }

    private static bool IsNote(dcTipoComprobante tipo)
        => tipo is dcTipoComprobante.NotaDebitoA or dcTipoComprobante.NotaCreditoA
            or dcTipoComprobante.NotaDebitoB or dcTipoComprobante.NotaCreditoB
            or dcTipoComprobante.NotaDebitoC or dcTipoComprobante.NotaCreditoC
            or dcTipoComprobante.NotaDebitoM or dcTipoComprobante.NotaCreditoM;

    private static void EnsureAuthorized(dcFacturaResponse response)
    {
        if (!response.Success || response.NumeroComprobante <= 0 || string.IsNullOrWhiteSpace(response.Cae))
            throw Error("FISCAL_DOCUMENT_NOT_AUTHORIZED", "El comprobante no contiene una autorización fiscal renderizable.");
    }

    private static FiscalDocumentSnapshotException Error(string code, string message)
        => new(code, message);
}
