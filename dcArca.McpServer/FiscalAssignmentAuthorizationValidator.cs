using System.Security;
using System.Text;
using System.Xml.Linq;
using dcArca.Core.Services;

namespace dcArca.McpServer;

public enum FiscalAssignmentValidationStatus
{
    Verified,
    NotVerified,
    InvalidConfiguration
}

public sealed record FiscalAssignmentValidationResult(
    FiscalAssignmentValidationStatus Status,
    string Code,
    string SafeMessage,
    string ContextId,
    string AssignmentRevision,
    string CredentialId,
    int PointOfSale,
    DateTimeOffset CheckedAt,
    string? Evidence = null)
{
    public bool Verified => Status == FiscalAssignmentValidationStatus.Verified;
}

public interface IFiscalAssignmentAuthorizationValidator
{
    Task<FiscalAssignmentValidationResult> ProbeAsync(
        string contextId,
        string assignmentRevision,
        CancellationToken cancellationToken = default);

    Task<FiscalAssignmentValidationResult> ValidateCandidateAsync(
        string contextId,
        string assignmentRevision,
        string actor,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validates an operator credential assignment without issuing a comprobante. The probe uses
/// the authenticated WSFE FEParamGetPtosVenta method, which ARCA documents as returning the
/// electronic points of sale managed for the represented CUIT.
/// </summary>
public sealed class FiscalAssignmentAuthorizationValidator : IFiscalAssignmentAuthorizationValidator
{
    private readonly IRepresentedFiscalContextStore _contexts;
    private readonly IFiscalCredentialMaterializer _materializer;
    private readonly IHttpClientFactory _httpClientFactory;

    public FiscalAssignmentAuthorizationValidator(
        IRepresentedFiscalContextStore contexts,
        IFiscalCredentialMaterializer materializer,
        IHttpClientFactory httpClientFactory)
    {
        _contexts = contexts;
        _materializer = materializer;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<FiscalAssignmentValidationResult> ProbeAsync(
        string contextId,
        string assignmentRevision,
        CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var context = await _contexts.GetAsync(contextId, cancellationToken)
            ?? throw new FiscalContextAccessException("FISCAL_CONTEXT_NOT_FOUND", "El contexto fiscal no existe.");
        var assignment = context.Assignments.SingleOrDefault(x =>
                string.Equals(x.AssignmentRevision, assignmentRevision, StringComparison.Ordinal))
            ?? throw new FiscalContextAccessException("ASSIGNMENT_NOT_FOUND", "La revisión de asignación no existe en el contexto fiscal.");

        FiscalCredentialMaterialization materialized;
        try
        {
            materialized = _materializer.Materialize(context, assignment);
        }
        catch (FiscalContextAccessException exception)
        {
            return Result(
                FiscalAssignmentValidationStatus.InvalidConfiguration,
                exception.Code,
                exception.Message,
                context,
                assignment,
                checkedAt);
        }

        string token;
        string sign;
        try
        {
            (token, sign) = await materialized.WsfeAuth.GetTokenAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(
                FiscalAssignmentValidationStatus.NotVerified,
                "WSAA_AUTHORIZATION_NOT_VERIFIED",
                "No se pudo validar la credencial contra WSAA. La asignación no puede activarse.",
                context,
                assignment,
                checkedAt);
        }

        var soap = BuildPuntosVentaRequest(token, sign, context.RepresentedCuit);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, materialized.Endpoints.WsfeUrl)
            {
                Content = new StringContent(soap, Encoding.UTF8, "text/xml")
            };
            request.Headers.TryAddWithoutValidation(
                "SOAPAction",
                "http://ar.gov.afip.dif.FEV1/FEParamGetPtosVenta");

            var client = _httpClientFactory.CreateClient(nameof(FiscalAssignmentAuthorizationValidator));
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result(
                    FiscalAssignmentValidationStatus.NotVerified,
                    "WSFE_ACCESS_NOT_VERIFIED",
                    "WSFE no permitió verificar el acceso del contexto. La asignación no puede activarse.",
                    context,
                    assignment,
                    checkedAt);
            }

            return ParsePuntosVentaResponse(body, context, assignment, checkedAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(
                FiscalAssignmentValidationStatus.NotVerified,
                "WSFE_ACCESS_NOT_VERIFIED",
                "No se pudo completar el diagnóstico remoto de WSFE. La asignación no puede activarse.",
                context,
                assignment,
                checkedAt);
        }
    }

    public async Task<FiscalAssignmentValidationResult> ValidateCandidateAsync(
        string contextId,
        string assignmentRevision,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("actor es obligatorio.", nameof(actor));

        var context = await _contexts.GetAsync(contextId, cancellationToken)
            ?? throw new FiscalContextAccessException("FISCAL_CONTEXT_NOT_FOUND", "El contexto fiscal no existe.");
        var assignment = context.Assignments.SingleOrDefault(x =>
                string.Equals(x.AssignmentRevision, assignmentRevision, StringComparison.Ordinal))
            ?? throw new FiscalContextAccessException("ASSIGNMENT_NOT_FOUND", "La revisión de asignación no existe en el contexto fiscal.");
        if (assignment.Status is not (CredentialAssignmentStatus.Candidate or CredentialAssignmentStatus.Validated))
            throw new FiscalContextAccessException("ASSIGNMENT_VALIDATION_STATE_INVALID", "La asignación no está en un estado validable.");

        var result = await ProbeAsync(contextId, assignmentRevision, cancellationToken);
        if (result.Verified && !string.IsNullOrWhiteSpace(result.Evidence))
        {
            await _contexts.MarkAssignmentValidatedAsync(
                contextId,
                assignmentRevision,
                result.Evidence,
                actor,
                cancellationToken);
        }
        return result;
    }

    private static string BuildPuntosVentaRequest(string token, string sign, long representedCuit)
    {
        var safeToken = SecurityElement.Escape(token) ?? string.Empty;
        var safeSign = SecurityElement.Escape(sign) ?? string.Empty;
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
              <soap:Header/>
              <soap:Body>
                <ar:FEParamGetPtosVenta>
                  <ar:Auth>
                    <ar:Token>{safeToken}</ar:Token>
                    <ar:Sign>{safeSign}</ar:Sign>
                    <ar:Cuit>{representedCuit}</ar:Cuit>
                  </ar:Auth>
                </ar:FEParamGetPtosVenta>
              </soap:Body>
            </soap:Envelope>
            """;
    }

    private static FiscalAssignmentValidationResult ParsePuntosVentaResponse(
        string xml,
        RepresentedFiscalContextRecord context,
        CredentialAssignmentRecord assignment,
        DateTimeOffset checkedAt)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.None);
        }
        catch
        {
            return Result(
                FiscalAssignmentValidationStatus.NotVerified,
                "WSFE_RESPONSE_INVALID",
                "WSFE devolvió una respuesta que no pudo validarse. La asignación no puede activarse.",
                context,
                assignment,
                checkedAt);
        }

        var errors = document.Descendants()
            .Where(x => x.Name.LocalName == "Err")
            .Select(err => new
            {
                Code = err.Elements().FirstOrDefault(x => x.Name.LocalName == "Code")?.Value,
                Message = err.Elements().FirstOrDefault(x => x.Name.LocalName == "Msg")?.Value
            })
            .ToArray();
        if (errors.Length > 0)
        {
            var code = string.IsNullOrWhiteSpace(errors[0].Code)
                ? "WSFE_AUTHORIZATION_NOT_VERIFIED"
                : $"WSFE_{errors[0].Code}";
            return Result(
                FiscalAssignmentValidationStatus.NotVerified,
                code,
                "ARCA no confirmó la autorización del contexto con la credencial candidata.",
                context,
                assignment,
                checkedAt);
        }

        var point = document.Descendants()
            .Where(x => x.Name.LocalName == "PtoVenta")
            .Select(node => new
            {
                Number = ParseInt(node, "Nro"),
                EmissionType = Value(node, "EmisionTipo"),
                Blocked = Value(node, "Bloqueado"),
                DisabledDate = Value(node, "FchBaja")
            })
            .SingleOrDefault(x => x.Number == context.PointOfSale);

        if (point is null)
        {
            return Result(
                FiscalAssignmentValidationStatus.NotVerified,
                "POINT_OF_SALE_NOT_AUTHORIZED",
                "ARCA no informó el punto de venta del contexto entre los puntos electrónicos habilitados para el CUIT representado.",
                context,
                assignment,
                checkedAt);
        }

        if (string.Equals(point.Blocked, "S", StringComparison.OrdinalIgnoreCase))
        {
            return Result(
                FiscalAssignmentValidationStatus.NotVerified,
                "POINT_OF_SALE_BLOCKED",
                "ARCA informó que el punto de venta del contexto está bloqueado.",
                context,
                assignment,
                checkedAt);
        }

        if (!string.IsNullOrWhiteSpace(point.DisabledDate))
        {
            return Result(
                FiscalAssignmentValidationStatus.NotVerified,
                "POINT_OF_SALE_DISABLED",
                "ARCA informó una fecha de baja para el punto de venta del contexto.",
                context,
                assignment,
                checkedAt);
        }

        var evidence = $"FEParamGetPtosVenta|context={context.ContextId}|assignment={assignment.AssignmentRevision}|pv={context.PointOfSale}|emission={point.EmissionType}|checked={checkedAt:O}";
        return Result(
            FiscalAssignmentValidationStatus.Verified,
            "ASSIGNMENT_AUTHORIZATION_VERIFIED",
            "ARCA confirmó acceso autenticado al CUIT representado y al punto de venta configurado.",
            context,
            assignment,
            checkedAt,
            evidence);
    }

    private static int? ParseInt(XElement parent, string localName)
        => int.TryParse(Value(parent, localName), out var value) ? value : null;

    private static string? Value(XElement parent, string localName)
        => parent.Elements().FirstOrDefault(x => x.Name.LocalName == localName)?.Value?.Trim();

    private static FiscalAssignmentValidationResult Result(
        FiscalAssignmentValidationStatus status,
        string code,
        string message,
        RepresentedFiscalContextRecord context,
        CredentialAssignmentRecord assignment,
        DateTimeOffset checkedAt,
        string? evidence = null)
        => new(
            status,
            code,
            message,
            context.ContextId,
            assignment.AssignmentRevision,
            assignment.CredentialId,
            context.PointOfSale,
            checkedAt,
            evidence);
}
