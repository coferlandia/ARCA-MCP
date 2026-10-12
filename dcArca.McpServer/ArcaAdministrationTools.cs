using System.ComponentModel;
using System.Security.Claims;
using dcArca.Core.Services;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace dcArca.McpServer;

[McpServerToolType]
public sealed class ArcaAdministrationTools
{
    private readonly FiscalRepresentationAdministration _admin;
    private readonly FiscalOperationAdministration _operations;
    private readonly IHttpContextAccessor _accessor;

    public ArcaAdministrationTools(
        FiscalRepresentationAdministration admin,
        FiscalOperationAdministration operations,
        IHttpContextAccessor accessor)
    {
        _admin = admin;
        _operations = operations;
        _accessor = accessor;
    }

    private ClaimsPrincipal Principal => _accessor.HttpContext?.User
        ?? throw new FiscalContextAccessException("AUTHENTICATED_PRINCIPAL_REQUIRED", "No existe identidad administrativa.");

    [McpServerTool, Description("Registra una representación candidata en el catálogo local. No la activa.")]
    [Authorize(Policy = "ArcaAdministrar")]
    public Task<FiscalRepresentationRecord> RegistrarRepresentacionFiscal(
        string contextId, string consumerId, long representedCuit, CancellationToken cancellationToken = default)
        => _admin.RegisterCandidateAsync(Principal, contextId, consumerId, representedCuit, cancellationToken);

    [McpServerTool, Description("Devuelve puntos de venta por WSFE sin solicitar CAE.")]
    [Authorize(Policy = "ArcaAdministrar")]
    public Task<dcPointOfSaleListingResult> ListarPuntosVenta(
        string contextId, string consumerId, long representedCuit, CancellationToken cancellationToken = default)
        => _admin.ListPointsOfSaleAsync(Principal, contextId, consumerId, representedCuit, cancellationToken);

    [McpServerTool, Description("Selecciona el PV de una representación pendiente, sin activarla.")]
    [Authorize(Policy = "ArcaAdministrar")]
    public Task<FiscalRepresentationRecord> SeleccionarPuntoVenta(
        string contextId, string consumerId, long representedCuit, int pointOfSale,
        CancellationToken cancellationToken = default)
        => _admin.SelectPointOfSaleAsync(Principal, contextId, consumerId, representedCuit, pointOfSale, cancellationToken);

    [McpServerTool, Description("Activa una representación con PV seleccionado: usa verificación remota cuando hay listado o permite PV manual no verificado ante WSFE 602 Sin Resultados; rechazos explícitos siguen bloqueando.")]
    [Authorize(Policy = "ArcaAdministrar")]
    public Task<FiscalRepresentationRecord> ActivarRepresentacionFiscal(
        string contextId, string consumerId, long representedCuit, int pointOfSale,
        CancellationToken cancellationToken = default)
        => _admin.VerifyAndActivateAsync(Principal, contextId, consumerId, representedCuit, pointOfSale, cancellationToken);

    [McpServerTool, Description("Marca una representación local como revocada.")]
    [Authorize(Policy = "ArcaAdministrar")]
    public Task<FiscalRepresentationRecord> RevocarRepresentacionFiscal(
        string contextId, string consumerId, long representedCuit, int pointOfSale,
        CancellationToken cancellationToken = default)
        => _admin.RevokeAsync(Principal, contextId, consumerId, representedCuit, pointOfSale, cancellationToken);

    [McpServerTool, Description("Abandona de forma auditada una operación Created/NumberAssigned antes del envío fiscal. Requiere operationId durable; nunca acepta idempotencyKey. reasonCode: manual_recovery, operator_cancelled, failed_before_submission o stale_pre_submission.")]
    [Authorize(Policy = "ArcaAdministrar")]
    public Task<AdministrativePreSubmissionAbandonmentResult> AbandonarOperacionPreSubmission(
        string operationId,
        string reasonCode,
        CancellationToken cancellationToken = default)
        => _operations.AbandonBeforeSubmissionAsync(
            Principal,
            operationId,
            reasonCode,
            cancellationToken);
}
