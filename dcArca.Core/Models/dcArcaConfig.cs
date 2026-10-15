/*
 * Copyright (c) 2025 Diego Cofré, DC Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 */

using System;

namespace dcArca.Core.Models;

/// <summary>
/// Configuración para conectar con los servicios de ARCA.
/// </summary>
public class dcArcaConfig
{
    private string _representedCuit = string.Empty;

    /// <summary>
    /// Identificador operativo del ambiente fiscal, por ejemplo homologacion o produccion.
    /// Cuando la configuración se materializa desde un contexto representado, este valor
    /// proviene del contexto inmutable y no del caller.
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// CUIT del contribuyente representado por WSFE. Es el valor que debe viajar en Auth.Cuit.
    /// </summary>
    public string RepresentedCuit
    {
        get => _representedCuit;
        set => _representedCuit = value ?? string.Empty;
    }

    /// <summary>
    /// Alias histórico de <see cref="RepresentedCuit"/> conservado por compatibilidad.
    /// Código nuevo debe usar el nombre explícito.
    /// </summary>
    public string Cuit
    {
        get => RepresentedCuit;
        set => RepresentedCuit = value;
    }

    /// <summary>
    /// CUIT principal dueño de la credencial técnica. En Padrón se usa como cuitRepresentada.
    /// Para materialización V2 proviene exclusivamente de configuración server-owned.
    /// </summary>
    public string PrincipalCuit { get; set; } = string.Empty;

    /// <summary>
    /// Ruta del archivo de certificado .pfx.
    /// </summary>
    public string CertificatePath { get; set; } = string.Empty;

    /// <summary>
    /// Password del certificado .pfx.
    /// </summary>
    public string CertificatePassword { get; set; } = string.Empty;

    /// <summary>
    /// URL del servicio WSAA para autenticación.
    /// </summary>
    public string WsaaUrl { get; set; } = "https://wsaahomo.afip.gov.ar/ws/services/LoginCms";

    /// <summary>
    /// URL del servicio WSFEv1 para facturación electrónica.
    /// </summary>
    public string WsfeUrl { get; set; } = "https://wswhomo.afip.gov.ar/wsfev1/service.asmx";

    /// <summary>
    /// URL del servicio de padrón (ws_sr_padron_a5) para validar CUITs.
    /// </summary>
    public string PadronUrl { get; set; } = "https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5";

    /// <summary>
    /// Punto de venta.
    /// </summary>
    public int PuntoVenta { get; set; } = 1;
}
