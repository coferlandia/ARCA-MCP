using System.ComponentModel;
using System.Text.Json;
using dcArca.Core.Models;
using dcArca.Core.Services;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace dcArca.McpServer;

/// <summary>
/// Operaciones de facturación electrónica ARCA (WSFEv1 + padrón) expuestas como MCP tools.
/// Delegan en dcArca.Core y en los servicios de orquestación del MCP.
/// </summary>
[McpServerToolType]
public sealed class ArcaTools
{
    private readonly IdcWsfeClient _wsfe;
    private readonly IdcPadronClient _padron;
    private readonly McpInvoiceSequencer _sequencer;
    private readonly InvoicePdfService _invoicePdf;
    private readonly ExistingInvoicePdfService _existingInvoicePdf;

    public ArcaTools(
        IdcWsfeClient wsfe,
        IdcPadronClient padron,
        McpInvoiceSequencer sequencer,
        InvoicePdfService invoicePdf,
        ExistingInvoicePdfService existingInvoicePdf)
    {
        _wsfe = wsfe;
        _padron = padron;
        _sequencer = sequencer;
        _invoicePdf = invoicePdf;
        _existingInvoicePdf = existingInvoicePdf;
    }

    [McpServerTool, Description("Emite un comprobante de forma idempotente, incorpora el resultado fiscal a una plantilla publicada y devuelve el PDF.")]
    [Authorize(Policy = "ArcaFacturar")]
    public Task<InvoiceWithPdfResult> EmitirComprobanteConPdf(
        [Description("Modelo fiscal completo. La numeración es asignada por el servidor.")] dcFacturaRequest factura,
        [Description("Clave idempotente estable para esta emisión fiscal. Repetirla con el mismo request recupera la misma operación.")] string idempotencyKey,
        [Description("Id de la plantilla publicada en creadorpdf.")] string templateId,
        [Description("Versión inmutable de la plantilla.")] string templateVersion,
        [Description("Datos visuales de la plantilla. No puede contener el campo reservado fiscal.")] JsonElement templateData,
        CancellationToken cancellationToken)
        => _invoicePdf.EmitAsync(
            factura,
            idempotencyKey,
            new PdfTemplateReference(templateId, templateVersion),
            templateData,
            cancellationToken);

    [McpServerTool, Description("Genera o regenera el PDF de un comprobante ya autorizado consultándolo primero en ARCA. Esta operación nunca solicita un nuevo CAE.")]
    [Authorize(Policy = "ArcaConsultar")]
    public Task<InvoiceWithPdfResult> GenerarPdfComprobante(
        [Description("Número del comprobante ya emitido/autorizado.")] long numeroComprobante,
        [Description("Tipo de comprobante ARCA (ej: 1=Factura A, 6=Factura B, 11=Factura C).")] dcTipoComprobante tipoComprobante,
        [Description("Id de la plantilla publicada en creadorpdf.")] string templateId,
        [Description("Versión inmutable de la plantilla.")] string templateVersion,
        [Description("Datos visuales originales de la plantilla. No puede contener el campo reservado fiscal.")] JsonElement templateData,
        CancellationToken cancellationToken)
        => _existingInvoicePdf.RenderAsync(
            tipoComprobante,
            numeroComprobante,
            new PdfTemplateReference(templateId, templateVersion),
            templateData,
            cancellationToken);

    [McpServerTool, Description("Consulta el último número de comprobante autorizado por AFIP para un tipo de comprobante dado, en el punto de venta configurado.")]
    [Authorize(Policy = "ArcaConsultar")]
    public Task<dcFacturaResponse> ConsultarUltimoComprobante(
        [Description("Tipo de comprobante AFIP (ej: 1=Factura A, 6=Factura B, 11=Factura C).")] dcTipoComprobante tipoComprobante,
        CancellationToken cancellationToken)
        => _wsfe.FECompUltimoAutorizadoAsync(tipoComprobante, cancellationToken);

    [McpServerTool, Description("Consulta los datos de un comprobante ya emitido/autorizado por AFIP.")]
    [Authorize(Policy = "ArcaConsultar")]
    public Task<dcFacturaResponse> ConsultarComprobante(
        [Description("Número del comprobante a consultar.")] long numeroComprobante,
        [Description("Tipo de comprobante AFIP (ej: 1=Factura A, 6=Factura B, 11=Factura C).")] dcTipoComprobante tipoComprobante,
        CancellationToken cancellationToken)
        => _wsfe.FECompConsultarAsync(numeroComprobante, tipoComprobante, cancellationToken);

    [McpServerTool, Description("Emite un comprobante idempotente calculando el próximo número autorizado dentro del servidor. Es el flujo recomendado para emisión MCP.")]
    [Authorize(Policy = "ArcaFacturar")]
    public Task<dcFacturaResponse> EmitirComprobante(
        [Description("Clave idempotente estable para esta emisión fiscal.")] string idempotencyKey,
        [Description("Tipo de comprobante AFIP a autorizar.")] dcTipoComprobante tipoComprobante,
        [Description("Concepto: 1=Productos, 2=Servicios, 3=Productos y Servicios.")] dcConcepto concepto,
        [Description("Número de documento del receptor.")] long cuitReceptor,
        [Description("Tipo de documento del receptor.")] dcTipoDocumento tipoDocReceptor,
        [Description("Condición frente al IVA del receptor.")] dcCondicionIvaReceptor condicionIvaReceptor,
        [Description("Importe neto gravado.")] decimal importeNeto,
        [Description("Importe de IVA.")] decimal importeIva,
        [Description("Importe total.")] decimal importeTotal,
        [Description("Fecha del comprobante YYYYMMDD.")] string fechaComprobante,
        [Description("Alícuota IVA para el caso simple de una sola alícuota. Obligatoria si importeIva > 0 y no se usa un detalle fiscal más avanzado.")] dcAlicuotaIva? alicuotaIva,
        [Description("Fecha de servicio desde YYYYMMDD.")] string? fechaServicioDesde,
        [Description("Fecha de servicio hasta YYYYMMDD.")] string? fechaServicioHasta,
        [Description("Fecha de vencimiento YYYYMMDD.")] string? fechaVencimiento,
        [Description("Tipo del comprobante asociado para notas.")] int? tipoComprobanteAsociado,
        [Description("Punto de venta del comprobante asociado.")] int? puntoVentaAsociado,
        [Description("Número del comprobante asociado.")] long? numeroAsociado,
        [Description("Período asociado desde YYYYMMDD.")] string? periodoAsociadoDesde,
        [Description("Período asociado hasta YYYYMMDD.")] string? periodoAsociadoHasta,
        CancellationToken cancellationToken)
    {
        var factura = new dcFacturaRequest
        {
            TipoComprobante = tipoComprobante,
            Concepto = concepto,
            CuitReceptor = cuitReceptor,
            TipoDocReceptor = (int)tipoDocReceptor,
            CondicionIvaReceptor = condicionIvaReceptor,
            ImporteNeto = importeNeto,
            ImporteIva = importeIva,
            ImporteTotal = importeTotal,
            AlicuotaIva = alicuotaIva,
            FechaComprobante = fechaComprobante,
            FechaServicioDesde = fechaServicioDesde,
            FechaServicioHasta = fechaServicioHasta,
            FechaVencimiento = fechaVencimiento,
            CbteAsociadoTipo = tipoComprobanteAsociado,
            CbteAsociadoPtoVta = puntoVentaAsociado,
            CbteAsociadoNro = numeroAsociado,
            PeriodoAsocDesde = periodoAsociadoDesde,
            PeriodoAsocHasta = periodoAsociadoHasta,
        };

        return _sequencer.EmitAsync(factura, idempotencyKey, cancellationToken);
    }

    [McpServerTool, Description("Emite un comprobante idempotente usando el modelo fiscal completo de dcARCA y asigna la numeración server-side.")]
    [Authorize(Policy = "ArcaFacturar")]
    public Task<dcFacturaResponse> EmitirComprobanteAvanzado(
        [Description("Modelo completo del comprobante. NumeroComprobante se ignora y es asignado por el servidor.")] dcFacturaRequest factura,
        [Description("Clave idempotente estable para esta emisión fiscal.")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        factura.NumeroComprobante = null;
        return _sequencer.EmitAsync(factura, idempotencyKey, cancellationToken);
    }

    [McpServerTool, Description("Compatibilidad histórica: la emisión con número elegido por el caller está deshabilitada en el MCP porque puede violar la reserva durable de la serie. Use emitir_comprobante o emitir_comprobante_avanzado.")]
    [Authorize(Policy = "ArcaFacturar")]
    public Task<dcFacturaResponse> SolicitarCae(
        [Description("Tipo de comprobante AFIP a autorizar.")] dcTipoComprobante tipoComprobante,
        [Description("Número de comprobante a autorizar (CbteDesde/CbteHasta).")] long numeroComprobante,
        [Description("Concepto: 1=Productos, 2=Servicios, 3=Productos y Servicios.")] dcConcepto concepto,
        [Description("CUIT del receptor (sin guiones).")] long cuitReceptor,
        [Description("Tipo de documento del receptor.")] dcTipoDocumento tipoDocReceptor,
        [Description("Condición frente al IVA del receptor (obligatoria por RG 5616).")] dcCondicionIvaReceptor condicionIvaReceptor,
        [Description("Importe neto gravado (sin IVA).")] decimal importeNeto,
        [Description("Importe de IVA.")] decimal importeIva,
        [Description("Importe total fiscal del comprobante.")] decimal importeTotal,
        [Description("Alícuota IVA para el caso simple de una sola alícuota. Obligatoria si importeIva > 0.")] dcAlicuotaIva? alicuotaIva,
        [Description("Fecha del comprobante en formato YYYYMMDD.")] string fechaComprobante,
        [Description("Fecha de servicio desde, formato YYYYMMDD. Obligatorio si concepto es Servicios o ProductosYServicios.")] string? fechaServicioDesde,
        [Description("Fecha de servicio hasta, formato YYYYMMDD. Obligatorio si concepto es Servicios o ProductosYServicios.")] string? fechaServicioHasta,
        [Description("Fecha de vencimiento de pago, formato YYYYMMDD. Obligatorio si concepto es Servicios o ProductosYServicios.")] string? fechaVencimiento,
        [Description("Tipo de comprobante asociado.")] int? tipoComprobanteAsociado,
        [Description("Punto de venta del comprobante asociado.")] int? puntoVentaAsociado,
        [Description("Número del comprobante asociado.")] long? numeroAsociado,
        [Description("Fecha desde del período asociado, formato YYYYMMDD.")] string? periodoAsociadoDesde,
        [Description("Fecha hasta del período asociado, formato YYYYMMDD.")] string? periodoAsociadoHasta,
        CancellationToken cancellationToken)
        => Task.FromResult(new dcFacturaResponse
        {
            Success = false,
            Codigo = "LOW_LEVEL_EMISSION_DISABLED",
            Mensaje = "La emisión MCP con número elegido por el caller está deshabilitada para proteger la serie fiscal. Use emitir_comprobante o emitir_comprobante_avanzado.",
            EmissionOutcome = dcEmissionOutcome.InvalidRequest,
            NumeroComprobante = numeroComprobante,
            Errores = ["La numeración de una serie administrada sólo puede asignarla el sequencer durable."]
        });

    [McpServerTool, Description("Consulta las condiciones de IVA válidas para un receptor dado, según el tipo de comprobante a emitir.")]
    [Authorize(Policy = "ArcaConsultar")]
    public Task<List<dcCondicionIvaOption>> ConsultarCondicionesIva(
        [Description("Tipo de documento del receptor.")] dcTipoDocumento docTipo,
        [Description("Número de documento del receptor.")] long docNro,
        [Description("Tipo de comprobante AFIP a emitir.")] dcTipoComprobante tipoComprobante,
        CancellationToken cancellationToken)
        => _wsfe.GetCondicionesIVAReceptorAsync((int)docTipo, docNro, tipoComprobante, cancellationToken);

    [McpServerTool, Description("Consulta los datos registrales de un CUIT en el padrón de AFIP (razón social, estado, actividades).")]
    [Authorize(Policy = "ArcaConsultar")]
    public Task<dcPadronPersonaResult> ConsultarPadron(
        [Description("CUIT a consultar (sin guiones).")] long cuit,
        CancellationToken cancellationToken)
        => _padron.GetPersonaAsync(cuit, cancellationToken);
}
