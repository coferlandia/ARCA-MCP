using System.ComponentModel;
using dcArca.Core.Models;
using dcArca.Core.Services;
using ModelContextProtocol.Server;

namespace dcArca.McpServer;

/// <summary>
/// Operaciones de facturación electrónica ARCA (WSFEv1 + padrón) expuestas como MCP tools.
/// Delegan directamente en dcArca.Core; esta clase no tiene lógica de negocio propia.
/// </summary>
[McpServerToolType]
public sealed class ArcaTools
{
    private readonly IdcWsfeClient _wsfe;
    private readonly IdcPadronClient _padron;

    public ArcaTools(IdcWsfeClient wsfe, IdcPadronClient padron)
    {
        _wsfe = wsfe;
        _padron = padron;
    }

    [McpServerTool, Description("Consulta el último número de comprobante autorizado por AFIP para un tipo de comprobante dado, en el punto de venta configurado.")]
    public Task<dcFacturaResponse> ConsultarUltimoComprobante(
        [Description("Tipo de comprobante AFIP (ej: 1=Factura A, 6=Factura B, 11=Factura C).")] dcTipoComprobante tipoComprobante,
        CancellationToken cancellationToken)
        => _wsfe.FECompUltimoAutorizadoAsync(tipoComprobante, cancellationToken);

    [McpServerTool, Description("Consulta los datos de un comprobante ya emitido/autorizado por AFIP.")]
    public Task<dcFacturaResponse> ConsultarComprobante(
        [Description("Número del comprobante a consultar.")] long numeroComprobante,
        [Description("Tipo de comprobante AFIP (ej: 1=Factura A, 6=Factura B, 11=Factura C).")] dcTipoComprobante tipoComprobante,
        CancellationToken cancellationToken)
        => _wsfe.FECompConsultarAsync(numeroComprobante, tipoComprobante, cancellationToken);

    [McpServerTool, Description("Solicita a AFIP la autorización (CAE) de una factura. Los importes deben cumplir ImporteTotal = ImporteNeto + ImporteIva.")]
    public Task<dcFacturaResponse> SolicitarCae(
        [Description("Tipo de comprobante AFIP a autorizar.")] dcTipoComprobante tipoComprobante,
        [Description("Número de comprobante a autorizar (CbteDesde/CbteHasta).")] long numeroComprobante,
        [Description("Concepto: 1=Productos, 2=Servicios, 3=Productos y Servicios.")] dcConcepto concepto,
        [Description("CUIT del receptor (sin guiones).")] long cuitReceptor,
        [Description("Tipo de documento del receptor (80=CUIT, 96=DNI, 99=Consumidor Final).")] int tipoDocReceptor,
        [Description("Condición frente al IVA del receptor (obligatoria por RG 5616).")] dcCondicionIvaReceptor condicionIvaReceptor,
        [Description("Importe neto gravado (sin IVA).")] decimal importeNeto,
        [Description("Importe de IVA.")] decimal importeIva,
        [Description("Importe total (debe ser ImporteNeto + ImporteIva).")] decimal importeTotal,
        [Description("Fecha del comprobante en formato YYYYMMDD.")] string fechaComprobante,
        CancellationToken cancellationToken)
    {
        var factura = new dcFacturaRequest
        {
            TipoComprobante = tipoComprobante,
            NumeroComprobante = numeroComprobante,
            Concepto = concepto,
            CuitReceptor = cuitReceptor,
            TipoDocReceptor = tipoDocReceptor,
            CondicionIvaReceptor = condicionIvaReceptor,
            ImporteNeto = importeNeto,
            ImporteIva = importeIva,
            ImporteTotal = importeTotal,
            FechaComprobante = fechaComprobante,
        };

        return _wsfe.FECAESolicitarAsync(factura, cancellationToken);
    }

    [McpServerTool, Description("Consulta las condiciones de IVA válidas para un receptor dado, según el tipo de comprobante a emitir.")]
    public Task<List<dcCondicionIvaOption>> ConsultarCondicionesIva(
        [Description("Tipo de documento del receptor (80=CUIT, 96=DNI).")] int docTipo,
        [Description("Número de documento del receptor.")] long docNro,
        [Description("Tipo de comprobante AFIP a emitir.")] dcTipoComprobante tipoComprobante,
        CancellationToken cancellationToken)
        => _wsfe.GetCondicionesIVAReceptorAsync(docTipo, docNro, tipoComprobante, cancellationToken);

    [McpServerTool, Description("Consulta los datos registrales de un CUIT en el padrón de AFIP (razón social, estado, actividades).")]
    public Task<dcPadronPersonaResult> ConsultarPadron(
        [Description("CUIT a consultar (sin guiones).")] long cuit,
        CancellationToken cancellationToken)
        => _padron.GetPersonaAsync(cuit, cancellationToken);
}
