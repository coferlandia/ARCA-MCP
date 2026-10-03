using System.Security;
using System.Text;
using System.Xml.Linq;
using dcArca.Core.Models;

namespace dcArca.Core.Services;

public enum dcPointOfSaleProbeStatus
{
    Verified,
    NotVerified
}

public sealed record dcPointOfSaleAccessProbeResult(
    dcPointOfSaleProbeStatus Status,
    string Code,
    string SafeMessage,
    int PointOfSale,
    DateTimeOffset CheckedAt,
    string? EmissionType = null)
{
    public bool Verified => Status == dcPointOfSaleProbeStatus.Verified;
}

/// <summary>
/// Executes the authenticated, non-emitting WSFE FEParamGetPtosVenta operation and verifies
/// that the configured electronic point of sale exists, is not blocked and has no disabled date.
/// </summary>
public sealed class dcWsfePointOfSaleProbe
{
    public async Task<dcPointOfSaleAccessProbeResult> ProbeAsync(
        dcArcaConfig config,
        dcArcaAuthService authService,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(httpClient);
        var checkedAt = DateTimeOffset.UtcNow;

        if (!long.TryParse(config.Cuit, out var representedCuit) || representedCuit <= 0 || config.PuntoVenta <= 0)
        {
            return Result(
                dcPointOfSaleProbeStatus.NotVerified,
                "FISCAL_CONTEXT_INVALID",
                "El CUIT representado o el punto de venta configurado no son válidos.",
                config.PuntoVenta,
                checkedAt);
        }

        string token;
        string sign;
        try
        {
            (token, sign) = await authService.GetTokenAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(
                dcPointOfSaleProbeStatus.NotVerified,
                "WSAA_AUTHORIZATION_NOT_VERIFIED",
                "No se pudo validar la credencial contra WSAA.",
                config.PuntoVenta,
                checkedAt);
        }

        var soap = BuildRequest(token, sign, representedCuit);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, config.WsfeUrl)
            {
                Content = new StringContent(soap, Encoding.UTF8, "text/xml")
            };
            request.Headers.TryAddWithoutValidation(
                "SOAPAction",
                "http://ar.gov.afip.dif.FEV1/FEParamGetPtosVenta");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result(
                    dcPointOfSaleProbeStatus.NotVerified,
                    "WSFE_ACCESS_NOT_VERIFIED",
                    "WSFE no permitió verificar el acceso del contexto.",
                    config.PuntoVenta,
                    checkedAt);
            }

            return ParseResponse(body, config.PuntoVenta, checkedAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Result(
                dcPointOfSaleProbeStatus.NotVerified,
                "WSFE_ACCESS_NOT_VERIFIED",
                "No se pudo completar el diagnóstico remoto de WSFE.",
                config.PuntoVenta,
                checkedAt);
        }
    }

    private static string BuildRequest(string token, string sign, long representedCuit)
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

    private static dcPointOfSaleAccessProbeResult ParseResponse(
        string xml,
        int pointOfSale,
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
                dcPointOfSaleProbeStatus.NotVerified,
                "WSFE_RESPONSE_INVALID",
                "WSFE devolvió una respuesta que no pudo validarse.",
                pointOfSale,
                checkedAt);
        }

        var firstError = document.Descendants()
            .Where(x => x.Name.LocalName == "Err")
            .Select(err => new
            {
                Code = Value(err, "Code"),
                Message = Value(err, "Msg")
            })
            .FirstOrDefault();
        if (firstError is not null)
        {
            var code = string.IsNullOrWhiteSpace(firstError.Code)
                ? "WSFE_AUTHORIZATION_NOT_VERIFIED"
                : $"WSFE_{firstError.Code}";
            return Result(
                dcPointOfSaleProbeStatus.NotVerified,
                code,
                "ARCA no confirmó la autorización del CUIT representado con la credencial indicada.",
                pointOfSale,
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
            .SingleOrDefault(x => x.Number == pointOfSale);

        if (point is null)
        {
            return Result(
                dcPointOfSaleProbeStatus.NotVerified,
                "POINT_OF_SALE_NOT_AUTHORIZED",
                "ARCA no informó el punto de venta entre los puntos electrónicos habilitados para el CUIT representado.",
                pointOfSale,
                checkedAt);
        }
        if (string.Equals(point.Blocked, "S", StringComparison.OrdinalIgnoreCase))
        {
            return Result(
                dcPointOfSaleProbeStatus.NotVerified,
                "POINT_OF_SALE_BLOCKED",
                "ARCA informó que el punto de venta está bloqueado.",
                pointOfSale,
                checkedAt,
                point.EmissionType);
        }
        if (!string.IsNullOrWhiteSpace(point.DisabledDate))
        {
            return Result(
                dcPointOfSaleProbeStatus.NotVerified,
                "POINT_OF_SALE_DISABLED",
                "ARCA informó una fecha de baja para el punto de venta.",
                pointOfSale,
                checkedAt,
                point.EmissionType);
        }

        return Result(
            dcPointOfSaleProbeStatus.Verified,
            "POINT_OF_SALE_AUTHORIZATION_VERIFIED",
            "ARCA confirmó acceso autenticado al CUIT representado y al punto de venta configurado.",
            pointOfSale,
            checkedAt,
            point.EmissionType);
    }

    private static int? ParseInt(XElement parent, string localName)
        => int.TryParse(Value(parent, localName), out var value) ? value : null;

    private static string? Value(XElement parent, string localName)
        => parent.Elements().FirstOrDefault(x => x.Name.LocalName == localName)?.Value?.Trim();

    private static dcPointOfSaleAccessProbeResult Result(
        dcPointOfSaleProbeStatus status,
        string code,
        string message,
        int pointOfSale,
        DateTimeOffset checkedAt,
        string? emissionType = null)
        => new(status, code, message, pointOfSale, checkedAt, emissionType);
}
