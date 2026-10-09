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

public sealed record dcWsfePointOfSale(
    int Number,
    string? EmissionType,
    bool Blocked,
    string? DisabledDate,
    bool EligibleForCae);

public sealed record dcPointOfSaleListingResult(
    dcPointOfSaleProbeStatus Status,
    string Code,
    string SafeMessage,
    DateTimeOffset CheckedAt,
    IReadOnlyList<dcWsfePointOfSale> Points)
{
    public bool Verified => Status == dcPointOfSaleProbeStatus.Verified;
}

/// <summary>
/// Authenticated, non-emitting WSFE FEParamGetPtosVenta discovery and validation.
/// Caller must have independent permission to discover this represented CUIT.
/// </summary>
public sealed class dcWsfePointOfSaleProbe
{
    public async Task<dcPointOfSaleListingResult> ListAsync(
        dcArcaConfig config,
        dcArcaAuthService authService,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(httpClient);
        var checkedAt = DateTimeOffset.UtcNow;
        if (!long.TryParse(config.Cuit, out var representedCuit) || representedCuit <= 0)
            return Failed("FISCAL_CONTEXT_INVALID", "El CUIT representado no es válido.", checkedAt);

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
            return Failed("WSAA_AUTHORIZATION_NOT_VERIFIED", "No se pudo validar la credencial contra WSAA.", checkedAt);
        }

        var soap = BuildRequest(token, sign, representedCuit);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, config.WsfeUrl)
            {
                Content = new StringContent(soap, Encoding.UTF8, "text/xml")
            };
            request.Headers.TryAddWithoutValidation("SOAPAction", "http://ar.gov.afip.dif.FEV1/FEParamGetPtosVenta");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Failed("WSFE_ACCESS_NOT_VERIFIED", "WSFE no permitió verificar los puntos de venta de este CUIT.", checkedAt);

            return ParseListingResponse(body, checkedAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Failed("WSFE_ACCESS_NOT_VERIFIED", "No se pudo completar el diagnóstico remoto de WSFE.", checkedAt);
        }
    }

    public async Task<dcPointOfSaleAccessProbeResult> ProbeAsync(
        dcArcaConfig config,
        dcArcaAuthService authService,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        var checkedAt = DateTimeOffset.UtcNow;
        if (config.PuntoVenta <= 0)
            return new dcPointOfSaleAccessProbeResult(
                dcPointOfSaleProbeStatus.NotVerified, "FISCAL_CONTEXT_INVALID",
                "El punto de venta no es válido.", config.PuntoVenta, checkedAt);

        var listing = await ListAsync(config, authService, httpClient, cancellationToken);
        if (!listing.Verified)
            return new dcPointOfSaleAccessProbeResult(listing.Status, listing.Code,
                listing.SafeMessage, config.PuntoVenta, listing.CheckedAt);

        var point = listing.Points.SingleOrDefault(x => x.Number == config.PuntoVenta);
        if (point is null)
            return new dcPointOfSaleAccessProbeResult(
                dcPointOfSaleProbeStatus.NotVerified, "POINT_OF_SALE_NOT_AUTHORIZED",
                "ARCA no informó el punto de venta para el CUIT representado.",
                config.PuntoVenta, listing.CheckedAt);
        if (point.Blocked)
            return new dcPointOfSaleAccessProbeResult(
                dcPointOfSaleProbeStatus.NotVerified, "POINT_OF_SALE_BLOCKED",
                "ARCA informó que el punto de venta está bloqueado.",
                config.PuntoVenta, listing.CheckedAt, point.EmissionType);
        if (!string.IsNullOrWhiteSpace(point.DisabledDate))
            return new dcPointOfSaleAccessProbeResult(
                dcPointOfSaleProbeStatus.NotVerified, "POINT_OF_SALE_DISABLED",
                "ARCA informó una fecha de baja para el punto de venta.",
                config.PuntoVenta, listing.CheckedAt, point.EmissionType);
        if (!point.EligibleForCae)
            return new dcPointOfSaleAccessProbeResult(
                dcPointOfSaleProbeStatus.NotVerified, "POINT_OF_SALE_NOT_CAE",
                "El punto de venta no tiene modalidad CAE para WSFE.",
                config.PuntoVenta, listing.CheckedAt, point.EmissionType);

        return new dcPointOfSaleAccessProbeResult(
            dcPointOfSaleProbeStatus.Verified, "POINT_OF_SALE_AUTHORIZATION_VERIFIED",
            "ARCA confirmó acceso al CUIT representado y al punto de venta WSFE/CAE.",
            config.PuntoVenta, listing.CheckedAt, point.EmissionType);
    }

    public static dcPointOfSaleListingResult ParseListingResponse(string xml, DateTimeOffset checkedAt)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.None);
        }
        catch
        {
            return Failed("WSFE_RESPONSE_INVALID", "WSFE devolvió una respuesta que no pudo validarse.", checkedAt);
        }

        var error = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "Err");
        if (error is not null)
        {
            var raw = Value(error, "Code");
            var code = string.IsNullOrWhiteSpace(raw) ? "WSFE_ACCESS_NOT_VERIFIED" : "WSFE_" + raw;
            var safe = raw switch
            {
                "600" => "ARCA rechazó el token/firma o la autorización del usuario técnico.",
                "601" => "El CUIT representado no está incluido en la autorización del token.",
                "602" => "ARCA no informó datos o puntos de venta para este CUIT en el ambiente seleccionado.",
                _ => "WSFE devolvió un error remoto y no se verificó el acceso al CUIT."
            };
            return Failed(code, safe, checkedAt);
        }

        if (!document.Descendants().Any(x => x.Name.LocalName == "FEParamGetPtosVentaResult"))
            return Failed("WSFE_RESPONSE_INVALID", "WSFE no devolvió el resultado esperado de puntos de venta.", checkedAt);

        var points = new List<dcWsfePointOfSale>();
        foreach (var node in document.Descendants().Where(x => x.Name.LocalName == "PtoVenta"))
        {
            if (!int.TryParse(Value(node, "Nro"), out var number) || number <= 0)
                return Failed("WSFE_RESPONSE_INVALID", "WSFE devolvió un punto de venta sin número válido.", checkedAt);
            var emission = Value(node, "EmisionTipo");
            var blockedText = Value(node, "Bloqueado");
            if (blockedText is not ("S" or "N" or "s" or "n"))
                return Failed("WSFE_RESPONSE_INVALID", "WSFE no informó un estado válido de bloqueo.", checkedAt);
            var blocked = string.Equals(blockedText, "S", StringComparison.OrdinalIgnoreCase);
            var disabledDate = Value(node, "FchBaja");
            var eligible = !blocked && string.IsNullOrWhiteSpace(disabledDate)
                && string.Equals(emission, "CAE", StringComparison.OrdinalIgnoreCase);
            points.Add(new dcWsfePointOfSale(number, emission, blocked, disabledDate, eligible));
        }

        if (points.Select(x => x.Number).Distinct().Count() != points.Count)
            return Failed("WSFE_RESPONSE_INVALID", "WSFE devolvió números de punto de venta duplicados.", checkedAt);

        return new dcPointOfSaleListingResult(
            dcPointOfSaleProbeStatus.Verified, "POINT_OF_SALE_LIST_VERIFIED",
            "ARCA devolvió el listado de puntos de venta del CUIT representado.",
            checkedAt, points);
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

    private static string? Value(XElement parent, string localName)
        => parent.Elements().FirstOrDefault(x => x.Name.LocalName == localName)?.Value?.Trim();

    private static dcPointOfSaleListingResult Failed(string code, string message, DateTimeOffset checkedAt)
        => new(dcPointOfSaleProbeStatus.NotVerified, code, message, checkedAt, []);
}
