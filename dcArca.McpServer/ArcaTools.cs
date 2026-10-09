using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using dcArca.Core.Models;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace dcArca.McpServer;

/// <summary>
/// Operaciones de facturación electrónica ARCA (WSFEv1 + padrón) expuestas como MCP tools.
/// Cada llamada resuelve un contexto fiscal server-owned autorizado para el consumer autenticado.
/// </summary>
[McpServerToolType]
public sealed class ArcaTools
{
    private readonly IFiscalContextRuntimeResolver _runtimeResolver;
    private readonly IEmissionIdempotencyStore _operationStore;
    private readonly IFiscalSeriesCoordinator _seriesCoordinator;
    private readonly IPdfDocumentRenderer _pdfRenderer;
    private readonly McpOperationContractService _contract;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ArcaTools(
        IFiscalContextRuntimeResolver runtimeResolver,
        IEmissionIdempotencyStore operationStore,
        IFiscalSeriesCoordinator seriesCoordinator,
        IPdfDocumentRenderer pdfRenderer,
        McpOperationContractService contract,
        IHttpContextAccessor httpContextAccessor)
    {
        _runtimeResolver = runtimeResolver;
        _operationStore = operationStore;
        _seriesCoordinator = seriesCoordinator;
        _pdfRenderer = pdfRenderer;
        _contract = contract;
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal Principal
        => _httpContextAccessor.HttpContext?.User
            ?? throw new FiscalContextAccessException("AUTHENTICATED_PRINCIPAL_REQUIRED", "No hay una identidad autenticada disponible para resolver el contexto fiscal.");
    private async Task CheckFiscalAuthorizationBeforeEmissionAsync(
        FiscalContextRuntime runtime, CancellationToken cancellationToken)
    {
        var authorized = await _runtimeResolver.AuthorizeAsync(Principal, runtime.Context.ContextId,
            "facturar", runtime.Context.RepresentedCuit, runtime.Context.PointOfSale, cancellationToken);
        if (authorized.Context.ActiveAssignment?.AssignmentRevision != runtime.Assignment.AssignmentRevision ||
            !string.Equals(authorized.Context.Environment, runtime.Context.Environment, StringComparison.Ordinal))
            throw new FiscalContextAccessException("FISCAL_ASSIGNMENT_CHANGED",
                "La credencial técnica o ambiente cambió antes de la emisión. La operación se mantiene protegida.");
    }


    [McpServerTool, Description("Devuelve el contrato de capacidades realmente implementadas y autorizadas para un contexto fiscal. No verifica habilitación remota en ARCA.")]
    [Authorize(Policy = "ArcaConsultar")]
    public Task<McpCapabilitiesResult> ObtenerCapacidadesFiscales(
        [Description("Contexto fiscal autorizado. Puede omitirse si existe exactamente uno permitido para consulta.")] string? contextId = null,
        [Description("CUIT del emisor representado. Obligatorio si hay más de una representación activa.")] long? representedCuit = null,
        [Description("Punto de venta habilitado para el CUIT representado.")] int? pointOfSale = null,
        CancellationToken cancellationToken = default)
        => _contract.GetCapabilitiesAsync(Principal, contextId, cancellationToken, representedCuit, pointOfSale);

    [McpServerTool, Description("Valida determinísticamente un request fiscal completo sin reservar número ni solicitar CAE. No garantiza autorización remota futura.")]
    [Authorize(Policy = "ArcaFacturar")]
    public Task<McpValidationResult> ValidarComprobante(
        [Description("Modelo fiscal completo a validar. NumeroComprobante no se utiliza para la validación server-side.")] dcFacturaRequest factura,
        [Description("Contexto fiscal autorizado. Puede omitirse si existe exactamente uno permitido para facturar.")] string? contextId = null,
        [Description("CUIT del emisor representado. Obligatorio si hay más de una representación activa.")] long? representedCuit = null,
        [Description("Punto de venta habilitado para el CUIT representado.")] int? pointOfSale = null,
        CancellationToken cancellationToken = default)
        => _contract.ValidateRequestAsync(Principal, contextId, factura, cancellationToken, representedCuit, pointOfSale);

    [McpServerTool, Description("Ejecuta diagnóstico fiscal explícito y no emisor: configuración local, assignments y verificación remota de autorización/PV. No modifica la assignment activa.")]
    [Authorize(Policy = "ArcaConsultar")]
    public Task<McpFiscalDiagnosticResult> DiagnosticarContextoFiscal(
        [Description("Contexto fiscal autorizado. Puede omitirse si existe exactamente uno permitido para consulta.")] string? contextId = null,
        [Description("CUIT del emisor representado. Obligatorio si hay más de una representación activa.")] long? representedCuit = null,
        [Description("Punto de venta habilitado para el CUIT representado.")] int? pointOfSale = null,
        CancellationToken cancellationToken = default)
        => _contract.DiagnoseAsync(Principal, contextId, cancellationToken, representedCuit, pointOfSale);

    [McpServerTool, Description("Consulta una operación durable por operationId o idempotencyKey dentro del consumer/contexto autorizado. No llama a emisión.")]
    [Authorize(Policy = "ArcaConsultar")]
    public Task<McpOperationResult> ConsultarOperacion(
        [Description("operationId opaco devuelto por una emisión previa. Informar exactamente éste o idempotencyKey.")] string? operationId = null,
        [Description("idempotencyKey original. Informar exactamente ésta u operationId; nunca se persiste ni devuelve en claro.")] string? idempotencyKey = null,
        [Description("Contexto fiscal autorizado. Puede omitirse si existe exactamente uno permitido para consulta.")] string? contextId = null,
        [Description("CUIT del emisor representado. Obligatorio si hay más de una representación activa.")] long? representedCuit = null,
        [Description("Punto de venta habilitado para el CUIT representado.")] int? pointOfSale = null,
        CancellationToken cancellationToken = default)
        => _contract.GetOperationAsync(Principal, contextId, operationId, idempotencyKey, cancellationToken, representedCuit, pointOfSale);

    [McpServerTool, Description("Reconcilia explícitamente una operación ya existente mediante lectura fiscal y comparación de evidencia. Nunca crea operación, asigna número ni solicita CAE.")]
    [Authorize(Policy = "ArcaConsultar")]
    public Task<McpOperationResult> ReconciliarOperacion(
        [Description("operationId opaco devuelto por una emisión previa. Informar exactamente éste o idempotencyKey.")] string? operationId = null,
        [Description("idempotencyKey original. Informar exactamente ésta u operationId.")] string? idempotencyKey = null,
        [Description("Contexto fiscal autorizado. Puede omitirse si existe exactamente uno permitido para consulta.")] string? contextId = null,
        [Description("CUIT del emisor representado. Obligatorio si hay más de una representación activa.")] long? representedCuit = null,
        [Description("Punto de venta habilitado para el CUIT representado.")] int? pointOfSale = null,
        CancellationToken cancellationToken = default)
        => _contract.ReconcileAsync(Principal, contextId, operationId, idempotencyKey, cancellationToken, representedCuit, pointOfSale);

    [McpServerTool, Description("Emite un comprobante de forma idempotente, incorpora el resultado fiscal a una plantilla publicada y devuelve el PDF.")]
    [Authorize(Policy = "ArcaFacturar")]
    public async Task<InvoiceWithPdfResult> EmitirComprobanteConPdf(
        [Description("Modelo fiscal completo. La numeración es asignada por el servidor.")] dcFacturaRequest factura,
        [Description("Clave idempotente estable para esta emisión fiscal. Repetirla con el mismo request recupera la misma operación.")] string idempotencyKey,
        [Description("Clave lógica estable de la plantilla (ej. factura-ar). Por compatibilidad también acepta temporalmente tpl_* legacy.")] string templateId,
        [Description("Versión inmutable de la plantilla lógica.")] string templateVersion,
        [Description("Datos visuales de la plantilla. No puede contener el campo reservado fiscal.")] JsonElement templateData,
        [Description("Contexto fiscal autorizado. Puede omitirse sólo si existe exactamente uno permitido para la operación.")] string? contextId = null,
        [Description("CUIT del emisor representado. Obligatorio si hay más de una representación activa.")] long? representedCuit = null,
        [Description("Punto de venta habilitado para el CUIT representado.")] int? pointOfSale = null,
        CancellationToken cancellationToken = default)
    {
        if (!factura.TipoComprobante.HasValue)
            return new InvoiceWithPdfResult(
                new dcFacturaResponse { Success = false, Codigo = "TIPOC_INVALID", EmissionOutcome = dcEmissionOutcome.InvalidRequest, Mensaje = "TipoComprobante es obligatorio." },
                new PdfRenderResult(PdfRenderStatus.NotAttempted, null, null, null));

        using var runtime = await _runtimeResolver.ResolveForEmissionAsync(
            Principal, contextId, idempotencyKey, factura.TipoComprobante.Value, representedCuit, pointOfSale, cancellationToken);
        var sequencer = new McpInvoiceSequencer(
            runtime.Wsfe,
            runtime.Config,
            _operationStore,
            runtime.IdentityProvider,
            _seriesCoordinator,
            ct => CheckFiscalAuthorizationBeforeEmissionAsync(runtime, ct));
        var service = new InvoicePdfService(sequencer, _pdfRenderer, runtime.Config);
        var result = await service.EmitAsync(
            factura,
            idempotencyKey,
            new PdfTemplateReference(templateId, templateVersion),
            templateData,
            cancellationToken);
        await _contract.DecorateEmissionResponseAsync(
            runtime.ConsumerId,
            runtime.Context.ContextId,
            idempotencyKey,
            result.Fiscal,
            cancellationToken,
            runtime.Context.RepresentedCuit,
            runtime.Context.PointOfSale);
        return result;
    }

    [McpServerTool, Description("Genera o regenera el PDF de un comprobante ya autorizado consultándolo primero en ARCA. Esta operación nunca solicita un nuevo CAE.")]
    [Authorize(Policy = "ArcaConsultar")]
    public async Task<InvoiceWithPdfResult> GenerarPdfComprobante(
        [Description("Número del comprobante ya emitido/autorizado.")] long numeroComprobante,
        [Description("Tipo de comprobante ARCA (ej: 1=Factura A, 6=Factura B, 11=Factura C).")] dcTipoComprobante tipoComprobante,
        [Description("Clave lógica estable de la plantilla (ej. factura-ar). Por compatibilidad también acepta temporalmente tpl_* legacy.")] string templateId,
        [Description("Versión inmutable de la plantilla lógica.")] string templateVersion,
        [Description("Datos visuales originales de la plantilla. No puede contener el campo reservado fiscal.")] JsonElement templateData,
        [Description("Contexto fiscal autorizado. Puede omitirse sólo si existe exactamente uno permitido para la operación.")] string? contextId = null,
        [Description("CUIT del emisor representado. Obligatorio si hay más de una representación activa.")] long? representedCuit = null,
        [Description("Punto de venta habilitado para el CUIT representado.")] int? pointOfSale = null,
        CancellationToken cancellationToken = default)
    {
        using var runtime = await _runtimeResolver.ResolveForReadAsync(Principal, contextId, representedCuit, pointOfSale, cancellationToken);
        var service = new ExistingInvoicePdfService(runtime.Wsfe, _pdfRenderer, runtime.Config);
        return await service.RenderAsync(
            tipoComprobante,
            numeroComprobante,
            new PdfTemplateReference(templateId, templateVersion),
            templateData,
            cancellationToken);
    }

    [McpServerTool, Description("Consulta el último número de comprobante autorizado por ARCA para un tipo de comprobante en un contexto fiscal autorizado.")]
    [Authorize(Policy = "ArcaConsultar")]
    public async Task<dcFacturaResponse> ConsultarUltimoComprobante(
        [Description("Tipo de comprobante ARCA (ej: 1=Factura A, 6=Factura B, 11=Factura C).")] dcTipoComprobante tipoComprobante,
        [Description("Contexto fiscal autorizado. Puede omitirse sólo si existe exactamente uno permitido para la operación.")] string? contextId = null,
        [Description("CUIT del emisor representado. Obligatorio si hay más de una representación activa.")] long? representedCuit = null,
        [Description("Punto de venta habilitado para el CUIT representado.")] int? pointOfSale = null,
        CancellationToken cancellationToken = default)
    {
        using var runtime = await _runtimeResolver.ResolveForReadAsync(Principal, contextId, representedCuit, pointOfSale, cancellationToken);
        return await runtime.Wsfe.FECompUltimoAutorizadoAsync(tipoComprobante, cancellationToken);
    }

    [McpServerTool, Description("Consulta los datos de un comprobante ya emitido/autorizado por ARCA dentro de un contexto fiscal autorizado.")]
    [Authorize(Policy = "ArcaConsultar")]
    public async Task<dcFacturaResponse> ConsultarComprobante(
        [Description("Número del comprobante a consultar.")] long numeroComprobante,
        [Description("Tipo de comprobante ARCA (ej: 1=Factura A, 6=Factura B, 11=Factura C).")] dcTipoComprobante tipoComprobante,
        [Description("Contexto fiscal autorizado. Puede omitirse sólo si existe exactamente uno permitido para la operación.")] string? contextId = null,
        [Description("CUIT del emisor representado. Obligatorio si hay más de una representación activa.")] long? representedCuit = null,
        [Description("Punto de venta habilitado para el CUIT representado.")] int? pointOfSale = null,
        CancellationToken cancellationToken = default)
    {
        using var runtime = await _runtimeResolver.ResolveForReadAsync(Principal, contextId, representedCuit, pointOfSale, cancellationToken);
        return await runtime.Wsfe.FECompConsultarAsync(numeroComprobante, tipoComprobante, cancellationToken);
    }

    [McpServerTool, Description("Emite un comprobante idempotente calculando el próximo número autorizado dentro del servidor. Es el flujo recomendado para emisión MCP.")]
    [Authorize(Policy = "ArcaFacturar")]
    public async Task<dcFacturaResponse> EmitirComprobante(
        [Description("Clave idempotente estable para esta emisión fiscal.")] string idempotencyKey,
        [Description("Tipo de comprobante ARCA a autorizar.")] dcTipoComprobante tipoComprobante,
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
        [Description("Contexto fiscal autorizado. Puede omitirse sólo si existe exactamente uno permitido para la operación.")] string? contextId = null,
        [Description("CUIT del emisor representado. Obligatorio si hay más de una representación activa.")] long? representedCuit = null,
        [Description("Punto de venta habilitado para el CUIT representado.")] int? pointOfSale = null,
        CancellationToken cancellationToken = default)
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

        using var runtime = await _runtimeResolver.ResolveForEmissionAsync(
            Principal, contextId, idempotencyKey, tipoComprobante, representedCuit, pointOfSale, cancellationToken);
        var sequencer = new McpInvoiceSequencer(
            runtime.Wsfe,
            runtime.Config,
            _operationStore,
            runtime.IdentityProvider,
            _seriesCoordinator,
            ct => CheckFiscalAuthorizationBeforeEmissionAsync(runtime, ct));
        var response = await sequencer.EmitAsync(factura, idempotencyKey, cancellationToken);
        await _contract.DecorateEmissionResponseAsync(
            runtime.ConsumerId,
            runtime.Context.ContextId,
            idempotencyKey,
            response,
            cancellationToken);
        return response;
    }

    [McpServerTool, Description("Emite un comprobante idempotente usando el modelo fiscal completo de dcARCA y asigna la numeración server-side.")]
    [Authorize(Policy = "ArcaFacturar")]
    public async Task<dcFacturaResponse> EmitirComprobanteAvanzado(
        [Description("Modelo completo del comprobante. NumeroComprobante se ignora y es asignado por el servidor.")] dcFacturaRequest factura,
        [Description("Clave idempotente estable para esta emisión fiscal.")] string idempotencyKey,
        [Description("Contexto fiscal autorizado. Puede omitirse sólo si existe exactamente uno permitido para la operación.")] string? contextId = null,
        [Description("CUIT del emisor representado. Obligatorio si hay más de una representación activa.")] long? representedCuit = null,
        [Description("Punto de venta habilitado para el CUIT representado.")] int? pointOfSale = null,
        CancellationToken cancellationToken = default)
    {
        if (!factura.TipoComprobante.HasValue)
            return new dcFacturaResponse
            {
                Success = false,
                Codigo = "TIPOC_INVALID",
                Mensaje = "TipoComprobante es obligatorio.",
                EmissionOutcome = dcEmissionOutcome.InvalidRequest,
                Errores = ["TipoComprobante es obligatorio."]
            };

        factura.NumeroComprobante = null;
        using var runtime = await _runtimeResolver.ResolveForEmissionAsync(
            Principal, contextId, idempotencyKey, factura.TipoComprobante.Value, representedCuit, pointOfSale, cancellationToken);
        var sequencer = new McpInvoiceSequencer(
            runtime.Wsfe,
            runtime.Config,
            _operationStore,
            runtime.IdentityProvider,
            _seriesCoordinator,
            ct => CheckFiscalAuthorizationBeforeEmissionAsync(runtime, ct));
        var response = await sequencer.EmitAsync(factura, idempotencyKey, cancellationToken);
        await _contract.DecorateEmissionResponseAsync(
            runtime.ConsumerId,
            runtime.Context.ContextId,
            idempotencyKey,
            response,
            cancellationToken);
        return response;
    }

    [McpServerTool, Description("Compatibilidad histórica: la emisión con número elegido por el caller está deshabilitada en el MCP porque puede violar la reserva durable de la serie. Use emitir_comprobante o emitir_comprobante_avanzado.")]
    [Authorize(Policy = "ArcaFacturar")]
    public async Task<dcFacturaResponse> SolicitarCae(
        [Description("Tipo de comprobante ARCA a autorizar.")] dcTipoComprobante tipoComprobante,
        [Description("Número de comprobante a autorizar (CbteDesde/CbteHasta).")] long numeroComprobante,
        [Description("Concepto: 1=Productos, 2=Servicios, 3=Productos y Servicios.")] dcConcepto concepto,
        [Description("CUIT del receptor (sin guiones).")] long cuitReceptor,
        [Description("Tipo de documento del receptor.")] dcTipoDocumento tipoDocReceptor,
        [Description("Condición frente al IVA del receptor.")] dcCondicionIvaReceptor condicionIvaReceptor,
        [Description("Importe neto gravado (sin IVA).")] decimal importeNeto,
        [Description("Importe de IVA.")] decimal importeIva,
        [Description("Importe total fiscal del comprobante.")] decimal importeTotal,
        [Description("Alícuota IVA para el caso simple de una sola alícuota.")] dcAlicuotaIva? alicuotaIva,
        [Description("Fecha del comprobante YYYYMMDD.")] string fechaComprobante,
        [Description("Fecha de servicio desde YYYYMMDD.")] string? fechaServicioDesde,
        [Description("Fecha de servicio hasta YYYYMMDD.")] string? fechaServicioHasta,
        [Description("Fecha de vencimiento YYYYMMDD.")] string? fechaVencimiento,
        [Description("Tipo de comprobante asociado.")] int? tipoComprobanteAsociado,
        [Description("Punto de venta del comprobante asociado.")] int? puntoVentaAsociado,
        [Description("Número del comprobante asociado.")] long? numeroAsociado,
        [Description("Fecha desde del período asociado YYYYMMDD.")] string? periodoAsociadoDesde,
        [Description("Fecha hasta del período asociado YYYYMMDD.")] string? periodoAsociadoHasta,
        [Description("Contexto fiscal autorizado.")] string? contextId = null,
        [Description("CUIT del emisor representado. Obligatorio si hay más de una representación activa.")] long? representedCuit = null,
        [Description("Punto de venta habilitado para el CUIT representado.")] int? pointOfSale = null,
        CancellationToken cancellationToken = default)
    {
        await _runtimeResolver.AuthorizeAsync(Principal, contextId, "facturar", representedCuit, pointOfSale, cancellationToken);
        return new dcFacturaResponse
        {
            Success = false,
            Codigo = "LOW_LEVEL_EMISSION_DISABLED",
            Mensaje = "La emisión MCP con número elegido por el caller está deshabilitada para proteger la serie fiscal. Use emitir_comprobante o emitir_comprobante_avanzado.",
            EmissionOutcome = dcEmissionOutcome.InvalidRequest,
            NumeroComprobante = numeroComprobante,
            Errores = ["La numeración de una serie administrada sólo puede asignarla el sequencer durable."]
        };
    }

    [McpServerTool, Description("Consulta las condiciones de IVA válidas para un receptor dado, según el tipo de comprobante a emitir.")]
    [Authorize(Policy = "ArcaConsultar")]
    public async Task<List<dcCondicionIvaOption>> ConsultarCondicionesIva(
        [Description("Tipo de documento del receptor.")] dcTipoDocumento docTipo,
        [Description("Número de documento del receptor.")] long docNro,
        [Description("Tipo de comprobante ARCA a emitir.")] dcTipoComprobante tipoComprobante,
        [Description("Contexto fiscal autorizado. Puede omitirse sólo si existe exactamente uno permitido para la operación.")] string? contextId = null,
        [Description("CUIT del emisor representado. Obligatorio si hay más de una representación activa.")] long? representedCuit = null,
        [Description("Punto de venta habilitado para el CUIT representado.")] int? pointOfSale = null,
        CancellationToken cancellationToken = default)
    {
        using var runtime = await _runtimeResolver.ResolveForReadAsync(Principal, contextId, representedCuit, pointOfSale, cancellationToken);
        return await runtime.Wsfe.GetCondicionesIVAReceptorAsync((int)docTipo, docNro, tipoComprobante, cancellationToken);
    }

    [McpServerTool, Description("Consulta los datos registrales de un CUIT en el padrón de ARCA usando un contexto autorizado.")]
    [Authorize(Policy = "ArcaConsultar")]
    public async Task<dcPadronPersonaResult> ConsultarPadron(
        [Description("CUIT a consultar (sin guiones).")] long cuit,
        [Description("Contexto fiscal autorizado. Puede omitirse sólo si existe exactamente uno permitido para la operación.")] string? contextId = null,
        [Description("CUIT del emisor representado. Obligatorio si hay más de una representación activa.")] long? representedCuit = null,
        [Description("Punto de venta habilitado para el CUIT representado.")] int? pointOfSale = null,
        CancellationToken cancellationToken = default)
    {
        using var runtime = await _runtimeResolver.ResolveForReadAsync(Principal, contextId, representedCuit, pointOfSale, cancellationToken);
        return await runtime.Padron.GetPersonaAsync(cuit, cancellationToken);
    }
}
