/*
 * Copyright (c) 2025 Diego Cofré, DC Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 */

namespace dcArca.Core.Models;

/// <summary>
/// Resultado de la consulta al padrón ws_sr_constancia_inscripcion.
/// Conserva las colecciones legacy y agrega facts registrales estructurados de forma aditiva.
/// </summary>
public class dcPadronPersonaResult
{
    public long CuitConsultado { get; set; }
    public bool Success { get; set; }
    public string Mensaje { get; set; } = string.Empty;

    public string? EstadoClave { get; set; }
    public string? TipoPersona { get; set; }
    public string? TipoClave { get; set; }
    public string? RazonSocial { get; set; }
    public string? Nombre { get; set; }
    public string? Apellido { get; set; }
    public string? NumeroDocumento { get; set; }
    public string? ErrorCodigo { get; set; }
    public string? ErrorDescripcion { get; set; }

    /// <summary>
    /// Fecha registral sólo cuando el XML fuente publica explícitamente fechaInscripcion.
    /// Nunca se infiere desde períodos de impuestos, actividades o categorías.
    /// </summary>
    public string? FechaInscripcion { get; set; }

    /// <summary>
    /// Ruta del campo fuente usado para FechaInscripcion. Null significa que ARCA no informó
    /// una fecha explícita; nunca identifica una inferencia.
    /// </summary>
    public string? FechaInscripcionFuente { get; set; }

    public dcPadronDomicilioFiscal? DomicilioFiscal { get; set; }
    public List<dcPadronImpuesto> Impuestos { get; } = new();
    public dcPadronMonotributo? Monotributo { get; set; }

    // Contrato legacy: se conserva para consumidores existentes.
    public List<string> Caracterizaciones { get; } = new();
    public List<string> Actividades { get; } = new();
    public List<string> Regimenes { get; } = new();

    public bool Existe => Success && string.IsNullOrWhiteSpace(ErrorCodigo);

    public bool EstaActivo =>
        string.Equals(EstadoClave, "ACTIVO", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(EstadoClave, "AC", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(EstadoClave, "A", StringComparison.OrdinalIgnoreCase);
}

public sealed class dcPadronDomicilioFiscal
{
    public string? TipoDomicilio { get; set; }
    public string? Direccion { get; set; }
    public string? Localidad { get; set; }
    public string? CodigoPostal { get; set; }
    public string? IdProvincia { get; set; }
    public string? DescripcionProvincia { get; set; }
    public string? TipoDatoAdicional { get; set; }
    public string? DatoAdicional { get; set; }
}

public sealed class dcPadronImpuesto
{
    public string? IdImpuesto { get; set; }
    public string? Descripcion { get; set; }
    public string? Estado { get; set; }
    public string? Motivo { get; set; }
    public string? Periodo { get; set; }

    /// <summary>
    /// Contenedor del XML que originó el fact: datosRegimenGeneral o datosMonotributo.
    /// </summary>
    public string Origen { get; set; } = string.Empty;
}

public sealed class dcPadronMonotributo
{
    public dcPadronCategoriaMonotributo? Categoria { get; set; }
    public List<dcPadronActividad> Actividades { get; } = new();
    public List<dcPadronImpuesto> Impuestos { get; } = new();
}

public sealed class dcPadronCategoriaMonotributo
{
    public string? IdCategoria { get; set; }
    public string? Descripcion { get; set; }
    public string? IdImpuesto { get; set; }
    public string? Periodo { get; set; }
}

public sealed class dcPadronActividad
{
    public string? IdActividad { get; set; }
    public string? Descripcion { get; set; }
    public string? Nomenclador { get; set; }
    public string? Orden { get; set; }
    public string? Periodo { get; set; }
}
