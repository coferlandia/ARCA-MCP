/*
 * Copyright (c) 2025 Diego Cofré, DC Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 */

using System.Net;
using System.Text;
using System.Xml;
using dcArca.Core.Models;
using dcArca.Core.Services.Logging;

namespace dcArca.Core.Services;

/// <summary>
/// Cliente SOAP para el padrón ws_sr_constancia_inscripcion (consulta de CUITs)
/// </summary>
public class dcPadronClient : IdcPadronClient, IDisposable
{
    private const string PadronNamespace = "http://a5.soap.ws.server.puc.sr/";
    private const string SoapNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

    private readonly dcArcaConfig _config;
    private readonly dcArcaAuthService _authService;
    private readonly string _padronUrl;
    private readonly HttpClient _httpClient;
    private readonly IAfipLogger _logger;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    public dcPadronClient(dcArcaConfig config, dcArcaAuthService? authService = null, HttpClient? httpClient = null, IAfipLogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? NoOpAfipLogger.Instance;
        _padronUrl = string.IsNullOrWhiteSpace(config.PadronUrl)
            ? "https://awshomo.arca.gov.ar/sr-padron/webservices/personaServiceA5"
            : config.PadronUrl;

        var principalCuit = string.IsNullOrWhiteSpace(config.PrincipalCuit)
            ? config.Cuit // Compatibilidad explícita para consumidores legacy directos de Core.
            : config.PrincipalCuit;

        _authService = authService ?? new dcArcaAuthService(
            config.WsaaUrl,
            config.CertificatePath,
            config.CertificatePassword,
            principalCuit,
            serviceName: "ws_sr_constancia_inscripcion",
            logger: _logger);
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _ownsHttpClient = true;
        }
    }

    /// <summary>
    /// Obtiene la información registral de un CUIT en el padrón de AFIP
    /// </summary>
    public async Task<dcPadronPersonaResult> GetPersonaAsync(long cuit, CancellationToken cancellationToken = default)
    {
        var result = new dcPadronPersonaResult { CuitConsultado = cuit };
        var rejectedToken = string.Empty;

        try
        {
            var (token, sign) = await _authService.GetTokenAsync(cancellationToken);
            rejectedToken = token;
            var soapRequest = BuildGetPersonaRequest(token, sign, cuit);
            var (responseBody, statusCode) = await SendSoapRequestAsync(soapRequest, "http://a5.soap.ws.server.puc.sr/getPersona_v2", cancellationToken);
            return ParsePersonaResponse(responseBody, cuit, statusCode);
        }
        catch (dcTokenInvalidException)
        {
            _logger.LogWarning("[dcPadronClient] Token inválido detectado, refrescando...");
            try
            {
                await _authService.InvalidateCacheAsync(rejectedToken, cancellationToken);
                var (token, sign) = await _authService.GetTokenAsync(cancellationToken);
                var soapRequest = BuildGetPersonaRequest(token, sign, cuit);
                var (responseBody, statusCode) = await SendSoapRequestAsync(soapRequest, "http://a5.soap.ws.server.puc.sr/getPersona_v2", cancellationToken);
                return ParsePersonaResponse(responseBody, cuit, statusCode);
            }
            catch (Exception retryEx)
            {
                result.Success = false;
                result.Mensaje = $"Error tras reintento por token inválido: {retryEx.Message}";
                return result;
            }
        }
        catch (dcWsaaFaultException wsaaEx)
        {
            result.Success = false;
            result.ErrorCodigo = wsaaEx.FaultCode;
            var status = wsaaEx.HttpStatusCode;
            result.ErrorDescripcion = wsaaEx.FaultString;
            result.Mensaje = $"Error WSAA al obtener token (HTTP {status}). {wsaaEx.FaultCode}: {wsaaEx.FaultString}";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Mensaje = ex.Message;
            return result;
        }
    }

    private string BuildGetPersonaRequest(string token, string sign, long cuit)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
            <soapenv:Envelope xmlns:soapenv=""{SoapNamespace}"" xmlns:a5=""{PadronNamespace}"">
                <soapenv:Header/>
                <soapenv:Body>
                    <a5:getPersona_v2>
                        <token>{token}</token>
                        <sign>{sign}</sign>
                        <cuitRepresentada>{(string.IsNullOrWhiteSpace(_config.PrincipalCuit) ? _config.Cuit : _config.PrincipalCuit)}</cuitRepresentada>
                        <idPersona>{cuit}</idPersona>
                    </a5:getPersona_v2>
                </soapenv:Body>
            </soapenv:Envelope>";
    }

    private async Task<(string Body, HttpStatusCode StatusCode)> SendSoapRequestAsync(string soapRequest, string soapAction, CancellationToken cancellationToken)
    {
        var content = new StringContent(soapRequest, Encoding.UTF8, "text/xml");
        content.Headers.Remove("Content-Type");
        content.Headers.TryAddWithoutValidation("Content-Type", "text/xml; charset=utf-8");
        content.Headers.Add("SOAPAction", soapAction);

        var response = await _httpClient.PostAsync(_padronUrl, content, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new Exception($"Padrón respondió HTTP {(int)response.StatusCode} sin contenido");
        }

        return (responseText, response.StatusCode);
    }

    private dcPadronPersonaResult ParsePersonaResponse(string xmlResponse, long cuit, HttpStatusCode statusCode)
    {
        var result = new dcPadronPersonaResult { CuitConsultado = cuit };

        var xmlDoc = new XmlDocument();
        try
        {
            xmlDoc.LoadXml(xmlResponse);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Mensaje = $"No se pudo interpretar la respuesta del padrón (HTTP {(int)statusCode}): {ex.Message}";
            return result;
        }

        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsmgr.AddNamespace("soap", SoapNamespace);
        nsmgr.AddNamespace("a5", PadronNamespace);

        var faultNode = xmlDoc.SelectSingleNode("//*[local-name()='Fault']");
        if (faultNode != null)
        {
            var faultCode = faultNode.SelectSingleNode("./faultcode")?.InnerText?.Trim();
            var faultString = faultNode.SelectSingleNode("./faultstring")?.InnerText?.Trim();

            if (dcTokenFaultDetector.IsInvalidSignature(faultString))
            {
                throw new dcTokenInvalidException($"Token inválido detectado: {faultString}");
            }

            var detailNode = faultNode.SelectSingleNode("./detail");
            var detailInfo = detailNode?.InnerXml?.Trim();
            var detailText = detailNode?.InnerText?.Trim();

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(faultString))
            {
                sb.Append(faultString);
            }
            if (!string.IsNullOrWhiteSpace(detailInfo))
            {
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append($"Detalle: {detailInfo}");
            }
            else if (!string.IsNullOrWhiteSpace(detailText))
            {
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append($"Detalle: {detailText}");
            }

            result.Success = false;
            result.ErrorCodigo = !string.IsNullOrWhiteSpace(faultCode)
                ? faultCode
                : $"HTTP {(int)statusCode}";
            result.ErrorDescripcion = sb.Length > 0
                ? sb.ToString()
                : $"HTTP {(int)statusCode} {statusCode}";
            result.Mensaje = $"Fault del padrón (HTTP {(int)statusCode}): {result.ErrorDescripcion}";
            return result;
        }

        var personaNode = xmlDoc.SelectSingleNode("//*[local-name()='personaReturn']");

        if (personaNode == null)
        {
            var faultMessage = xmlDoc.SelectSingleNode("//*[local-name()='faultstring']")?.InnerText
                ?? xmlDoc.SelectSingleNode("//soap:Fault/soap:faultstring", nsmgr)?.InnerText
                ?? "El servicio de padrón no devolvió información";

            result.Success = false;
            result.ErrorCodigo = $"HTTP {(int)statusCode}";
            result.ErrorDescripcion = faultMessage.Trim();
            result.Mensaje = $"Respuesta inesperada del padrón (HTTP {(int)statusCode}): {result.ErrorDescripcion}";
            return result;
        }

        var padronErrors = ExtractPadronErrors(personaNode);
        if (padronErrors.Count > 0)
        {
            result.Success = false;
            result.ErrorCodigo = "PADRON_ERROR";
            result.ErrorDescripcion = string.Join(" | ", padronErrors);
            result.Mensaje = result.ErrorDescripcion;
            return result;
        }

        var datosGeneralesNode = ChildNode(personaNode, "datosGenerales");
        if (datosGeneralesNode == null)
        {
            result.Success = false;
            result.Mensaje = "El padrón no informó datos generales para el CUIT consultado";
            return result;
        }

        result.Success = true;
        result.TipoPersona = ChildValue(datosGeneralesNode, "tipoPersona");
        result.TipoClave = ChildValue(datosGeneralesNode, "tipoClave");
        result.EstadoClave = ChildValue(datosGeneralesNode, "estadoClave");
        result.RazonSocial = ChildValue(datosGeneralesNode, "razonSocial");
        result.Nombre = ChildValue(datosGeneralesNode, "nombre");
        result.Apellido = ChildValue(datosGeneralesNode, "apellido");
        result.NumeroDocumento = ChildValue(datosGeneralesNode, "numeroDocumento");

        PopulateStructuredFacts(personaNode, datosGeneralesNode, result);
        PopulateLegacyCollections(personaNode, result);

        result.Mensaje = result.EstaActivo
            ? "CUIT registrado y activo en el padrón de AFIP"
            : string.IsNullOrWhiteSpace(result.EstadoClave)
                ? "CUIT registrado en padrón"
                : $"CUIT con estado {result.EstadoClave}";

        return result;
    }

    private static void PopulateStructuredFacts(
        XmlNode personaNode,
        XmlNode datosGeneralesNode,
        dcPadronPersonaResult result)
    {
        var explicitRegistrationDate = ChildValue(datosGeneralesNode, "fechaInscripcion");
        if (!string.IsNullOrWhiteSpace(explicitRegistrationDate))
        {
            result.FechaInscripcion = explicitRegistrationDate;
            result.FechaInscripcionFuente = "datosGenerales.fechaInscripcion";
        }

        var domicilioNode = ChildNode(datosGeneralesNode, "domicilioFiscal");
        if (domicilioNode != null)
        {
            result.DomicilioFiscal = new dcPadronDomicilioFiscal
            {
                TipoDomicilio = ChildValue(domicilioNode, "tipoDomicilio"),
                Direccion = ChildValue(domicilioNode, "direccion"),
                Localidad = ChildValue(domicilioNode, "localidad"),
                CodigoPostal = ChildValue(domicilioNode, "codPostal"),
                IdProvincia = ChildValue(domicilioNode, "idProvincia"),
                DescripcionProvincia = ChildValue(domicilioNode, "descripcionProvincia"),
                TipoDatoAdicional = ChildValue(domicilioNode, "tipoDatoAdicional"),
                DatoAdicional = ChildValue(domicilioNode, "datoAdicional")
            };
        }

        var regimenGeneralNode = ChildNode(personaNode, "datosRegimenGeneral");
        AddStructuredTaxes(regimenGeneralNode, "datosRegimenGeneral", result.Impuestos);

        var monotributoNode = ChildNode(personaNode, "datosMonotributo");
        if (monotributoNode == null)
        {
            return;
        }

        var monotributo = new dcPadronMonotributo();

        var categoryNode = ChildNode(monotributoNode, "categoriaMonotributo");
        if (categoryNode != null)
        {
            monotributo.Categoria = new dcPadronCategoriaMonotributo
            {
                IdCategoria = ChildValue(categoryNode, "idCategoria"),
                Descripcion = ChildValue(categoryNode, "descripcionCategoria"),
                IdImpuesto = ChildValue(categoryNode, "idImpuesto"),
                Periodo = ChildValue(categoryNode, "periodo")
            };
        }

        var activityNodes = monotributoNode.SelectNodes("./*[local-name()='actividadMonotributista']");
        if (activityNodes != null)
        {
            foreach (XmlNode activityNode in activityNodes)
            {
                monotributo.Actividades.Add(new dcPadronActividad
                {
                    IdActividad = ChildValue(activityNode, "idActividad"),
                    Descripcion = ChildValue(activityNode, "descripcionActividad"),
                    Nomenclador = ChildValue(activityNode, "nomenclador"),
                    Orden = ChildValue(activityNode, "orden"),
                    Periodo = ChildValue(activityNode, "periodo")
                });
            }
        }

        AddStructuredTaxes(monotributoNode, "datosMonotributo", monotributo.Impuestos);
        foreach (var tax in monotributo.Impuestos)
        {
            result.Impuestos.Add(tax);
        }

        result.Monotributo = monotributo;
    }

    private static void AddStructuredTaxes(
        XmlNode? container,
        string source,
        List<dcPadronImpuesto> target)
    {
        if (container == null)
        {
            return;
        }

        var nodes = container.SelectNodes("./*[local-name()='impuesto']");
        if (nodes == null)
        {
            return;
        }

        foreach (XmlNode node in nodes)
        {
            target.Add(new dcPadronImpuesto
            {
                IdImpuesto = ChildValue(node, "idImpuesto"),
                Descripcion = ChildValue(node, "descripcionImpuesto"),
                Estado = ChildValue(node, "estadoImpuesto"),
                Motivo = ChildValue(node, "motivo"),
                Periodo = ChildValue(node, "periodo"),
                Origen = source
            });
        }
    }

    private static void PopulateLegacyCollections(XmlNode personaNode, dcPadronPersonaResult result)
    {
        var caracterNodes = personaNode.SelectNodes(".//*[local-name()='caracterizacion']");
        if (caracterNodes != null)
        {
            foreach (XmlNode node in caracterNodes)
            {
                // Compatibilidad estricta: #100 es aditivo y no redefine el valor legacy.
                var descripcion = node.SelectSingleNode("descripcion")?.InnerText
                    ?? node.InnerText;
                if (!string.IsNullOrWhiteSpace(descripcion))
                {
                    result.Caracterizaciones.Add(descripcion.Trim());
                }
            }
        }

        AddActividadDescriptions(personaNode.SelectNodes(".//*[local-name()='actividad']"), result.Actividades);
        AddActividadDescriptions(personaNode.SelectNodes(".//*[local-name()='actividadMonotributista']"), result.Actividades);
        AddRegimenDescriptions(personaNode.SelectNodes(".//*[local-name()='regimen']"), result.Regimenes);
    }

    private static List<string> ExtractPadronErrors(XmlNode personaNode)
    {
        var errors = new List<string>();

        void CollectErrors(string containerName)
        {
            var node = ChildNode(personaNode, containerName);
            if (node == null) return;

            var message = ChildValue(node, "mensaje");
            if (!string.IsNullOrWhiteSpace(message))
            {
                errors.Add($"{containerName}: {message}");
            }

            var errorNodes = node.SelectNodes(".//*[local-name()='error']");
            if (errorNodes == null) return;

            foreach (XmlNode errorNode in errorNodes)
            {
                var text = errorNode.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    errors.Add($"{containerName}: {text.Trim()}");
                }
            }
        }

        CollectErrors("errorConstancia");
        CollectErrors("errorRegimenGeneral");
        CollectErrors("errorMonotributo");

        return errors;
    }

    private static void AddActividadDescriptions(XmlNodeList? nodes, List<string> target)
    {
        if (nodes == null) return;

        foreach (XmlNode node in nodes)
        {
            var descripcion = ChildValue(node, "descripcionActividad")
                ?? ChildValue(node, "descripcion")
                ?? node.InnerText?.Trim();

            if (!string.IsNullOrWhiteSpace(descripcion))
            {
                target.Add(descripcion);
            }
        }
    }

    private static void AddRegimenDescriptions(XmlNodeList? nodes, List<string> target)
    {
        if (nodes == null) return;

        foreach (XmlNode node in nodes)
        {
            var descripcion = ChildValue(node, "descripcionRegimen")
                ?? ChildValue(node, "descripcion")
                ?? node.InnerText?.Trim();

            if (!string.IsNullOrWhiteSpace(descripcion))
            {
                target.Add(descripcion);
            }
        }
    }

    private static XmlNode? ChildNode(XmlNode parent, string localName)
        => parent.SelectSingleNode($"./*[local-name()='{localName}']");

    private static string? ChildValue(XmlNode parent, string localName)
    {
        var value = ChildNode(parent, localName)?.InnerText?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
