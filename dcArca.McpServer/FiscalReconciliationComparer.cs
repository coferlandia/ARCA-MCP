using dcArca.Core.Models;

namespace dcArca.McpServer;

public enum FiscalReconciliationMatch
{
    Equivalent,
    Mismatch,
    InsufficientEvidence
}

public sealed record FiscalReconciliationResult(
    FiscalReconciliationMatch Match,
    string Code,
    string Message)
{
    public static FiscalReconciliationResult Equivalent()
        => new(FiscalReconciliationMatch.Equivalent, "RECONCILIATION_EQUIVALENT", "El comprobante consultado coincide con la intención fiscal persistida.");

    public static FiscalReconciliationResult Mismatch(string field)
        => new(FiscalReconciliationMatch.Mismatch, "RECONCILIATION_MISMATCH", $"El comprobante consultado no coincide con la operación original ({field}).");

    public static FiscalReconciliationResult Insufficient(string field)
        => new(FiscalReconciliationMatch.InsufficientEvidence, "RECONCILIATION_EVIDENCE_INSUFFICIENT", $"ARCA no devolvió evidencia suficiente para verificar la operación original ({field}).");
}

public static class FiscalReconciliationComparer
{
    public static FiscalReconciliationResult Compare(
        EmissionIdempotencyRecord operation,
        dcFacturaResponse consulted)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(consulted);

        if (operation.FiscalEvidence.State != FiscalEvidenceState.Complete
            || operation.FiscalEvidence.Projection is null)
            return FiscalReconciliationResult.Insufficient("legacy fiscal evidence");

        if (!operation.NumeroComprobante.HasValue)
            return FiscalReconciliationResult.Insufficient("stored invoice number");

        var expected = operation.FiscalEvidence.Projection;

        if (consulted.NumeroComprobante != operation.NumeroComprobante.Value)
            return FiscalReconciliationResult.Mismatch("invoice number");

        if (!consulted.PuntoVenta.HasValue) return FiscalReconciliationResult.Insufficient("point of sale");
        if (consulted.PuntoVenta.Value != operation.Identity.PuntoVenta) return FiscalReconciliationResult.Mismatch("point of sale");

        if (!consulted.TipoComprobante.HasValue) return FiscalReconciliationResult.Insufficient("invoice type");
        if ((int)consulted.TipoComprobante.Value != operation.Identity.TipoComprobante) return FiscalReconciliationResult.Mismatch("invoice type");

        if (!consulted.Concepto.HasValue) return FiscalReconciliationResult.Insufficient("concept");
        if ((int)consulted.Concepto.Value != expected.Concepto) return FiscalReconciliationResult.Mismatch("concept");

        if (!consulted.DocTipo.HasValue) return FiscalReconciliationResult.Insufficient("receiver document type");
        if ((int)consulted.DocTipo.Value != expected.TipoDocReceptor) return FiscalReconciliationResult.Mismatch("receiver document type");

        if (!consulted.DocNro.HasValue) return FiscalReconciliationResult.Insufficient("receiver document");
        if (consulted.DocNro.Value != expected.CuitReceptor) return FiscalReconciliationResult.Mismatch("receiver document");

        if (string.IsNullOrWhiteSpace(consulted.FechaComprobante)) return FiscalReconciliationResult.Insufficient("invoice date");
        if (!string.Equals(consulted.FechaComprobante, expected.FechaComprobante, StringComparison.Ordinal)) return FiscalReconciliationResult.Mismatch("invoice date");

        if (!string.Equals(consulted.MonedaId?.Trim(), expected.MonedaId, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(consulted.MonedaId)) return FiscalReconciliationResult.Insufficient("currency");
            return FiscalReconciliationResult.Mismatch("currency");
        }

        if (!DecimalEqual(consulted.MonedaCotizacion, expected.MonedaCotizacion)) return FiscalReconciliationResult.Mismatch("currency rate");
        if (!DecimalEqual(consulted.ImporteTotal, expected.ImporteTotal)) return FiscalReconciliationResult.Mismatch("total");
        if (!DecimalEqual(consulted.ImporteNeto, expected.ImporteNeto)) return FiscalReconciliationResult.Mismatch("net amount");
        if (!DecimalEqual(consulted.ImporteIva, expected.ImporteIva)) return FiscalReconciliationResult.Mismatch("VAT amount");
        if (!DecimalEqual(consulted.ImporteNoGravado, expected.ImporteNoGravado)) return FiscalReconciliationResult.Mismatch("non-taxed amount");
        if (!DecimalEqual(consulted.ImporteExento, expected.ImporteExento)) return FiscalReconciliationResult.Mismatch("exempt amount");
        if (!DecimalEqual(consulted.ImporteTributos, expected.ImporteTributos)) return FiscalReconciliationResult.Mismatch("tax amount");

        if (expected.CondicionIvaReceptor.HasValue)
        {
            if (!consulted.CondicionIvaReceptor.HasValue) return FiscalReconciliationResult.Insufficient("receiver VAT condition");
            if ((int)consulted.CondicionIvaReceptor.Value != expected.CondicionIvaReceptor.Value) return FiscalReconciliationResult.Mismatch("receiver VAT condition");
        }

        var serviceDates = CompareOptionalDate(expected.FechaServicioDesde, consulted.FechaServicioDesde, "service start date");
        if (serviceDates is not null) return serviceDates;
        serviceDates = CompareOptionalDate(expected.FechaServicioHasta, consulted.FechaServicioHasta, "service end date");
        if (serviceDates is not null) return serviceDates;
        serviceDates = CompareOptionalDate(expected.FechaVencimiento, consulted.FechaVencimientoPago, "payment due date");
        if (serviceDates is not null) return serviceDates;

        var ivaResult = CompareIva(expected.Iva, consulted.Iva);
        if (ivaResult is not null) return ivaResult;
        var tributosResult = CompareTributos(expected.Tributos, consulted.Tributos);
        if (tributosResult is not null) return tributosResult;

        // FECompConsultar does not currently expose associated-document or associated-period
        // information through dcFacturaResponse. Never infer equality for those fiscal fields.
        if (expected.ComprobanteAsociado.Tipo.HasValue
            || expected.ComprobanteAsociado.PuntoVenta.HasValue
            || expected.ComprobanteAsociado.Numero.HasValue
            || !string.IsNullOrWhiteSpace(expected.ComprobanteAsociado.Cuit)
            || !string.IsNullOrWhiteSpace(expected.ComprobanteAsociado.Fecha)
            || !string.IsNullOrWhiteSpace(expected.PeriodoAsocDesde)
            || !string.IsNullOrWhiteSpace(expected.PeriodoAsocHasta))
        {
            return FiscalReconciliationResult.Insufficient("associated document/period");
        }

        return FiscalReconciliationResult.Equivalent();
    }

    private static FiscalReconciliationResult? CompareOptionalDate(string? expected, string? actual, string field)
    {
        if (string.IsNullOrWhiteSpace(expected)) return null;
        if (string.IsNullOrWhiteSpace(actual)) return FiscalReconciliationResult.Insufficient(field);
        return string.Equals(expected, actual, StringComparison.Ordinal)
            ? null
            : FiscalReconciliationResult.Mismatch(field);
    }

    private static FiscalReconciliationResult? CompareIva(
        IReadOnlyList<FiscalIvaSnapshot> expected,
        IReadOnlyList<dcFacturaResponse.IvaDetalle> actual)
    {
        if (expected.Count == 0)
            return actual.Count == 0 ? null : FiscalReconciliationResult.Mismatch("VAT detail");
        if (actual.Count == 0) return FiscalReconciliationResult.Insufficient("VAT detail");
        if (expected.Count != actual.Count) return FiscalReconciliationResult.Mismatch("VAT detail count");

        var left = expected
            .OrderBy(x => x.Alicuota)
            .ThenBy(x => x.BaseImponible)
            .ThenBy(x => x.Importe)
            .ToArray();
        var right = actual
            .OrderBy(x => x.Alicuota.HasValue ? (int)x.Alicuota.Value : int.MinValue)
            .ThenBy(x => x.BaseImponible)
            .ThenBy(x => x.Importe)
            .ToArray();

        for (var i = 0; i < left.Length; i++)
        {
            if (!right[i].Alicuota.HasValue) return FiscalReconciliationResult.Insufficient("VAT rate");
            if (left[i].Alicuota != (int)right[i].Alicuota!.Value
                || !DecimalEqual(left[i].BaseImponible, right[i].BaseImponible)
                || !DecimalEqual(left[i].Importe, right[i].Importe))
                return FiscalReconciliationResult.Mismatch("VAT detail");
        }

        return null;
    }

    private static FiscalReconciliationResult? CompareTributos(
        IReadOnlyList<FiscalTributoSnapshot> expected,
        IReadOnlyList<dcFacturaResponse.TributoDetalle> actual)
    {
        if (expected.Count == 0)
            return actual.Count == 0 ? null : FiscalReconciliationResult.Mismatch("tax detail");
        if (actual.Count == 0) return FiscalReconciliationResult.Insufficient("tax detail");
        if (expected.Count != actual.Count) return FiscalReconciliationResult.Mismatch("tax detail count");

        var left = expected
            .OrderBy(x => x.Id)
            .ThenBy(x => x.Descripcion, StringComparer.Ordinal)
            .ThenBy(x => x.BaseImponible)
            .ThenBy(x => x.Alicuota)
            .ThenBy(x => x.Importe)
            .ToArray();
        var right = actual
            .OrderBy(x => x.Id ?? int.MinValue)
            .ThenBy(x => x.Descripcion, StringComparer.Ordinal)
            .ThenBy(x => x.BaseImponible)
            .ThenBy(x => x.Alicuota)
            .ThenBy(x => x.Importe)
            .ToArray();

        for (var i = 0; i < left.Length; i++)
        {
            if (!right[i].Id.HasValue) return FiscalReconciliationResult.Insufficient("tax id");
            if (left[i].Id != right[i].Id!.Value
                || !string.Equals(left[i].Descripcion, right[i].Descripcion?.Trim() ?? string.Empty, StringComparison.Ordinal)
                || !DecimalEqual(left[i].BaseImponible, right[i].BaseImponible)
                || !DecimalEqual(left[i].Alicuota, right[i].Alicuota)
                || !DecimalEqual(left[i].Importe, right[i].Importe))
                return FiscalReconciliationResult.Mismatch("tax detail");
        }

        return null;
    }

    private static bool DecimalEqual(decimal left, decimal right)
        => left == right;
}
