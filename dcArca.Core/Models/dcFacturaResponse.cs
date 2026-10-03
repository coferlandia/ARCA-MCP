/*
 * Copyright (c) 2025 Diego Cofré Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 */

namespace dcArca.Core.Models;

/// <summary>
/// Respuesta de AFIP al consultar o autorizar comprobantes.
/// Los metadatos Operation* son opcionales y sólo los completa una capa de orquestación durable.
/// </summary>
public class dcFacturaResponse
{
    public bool Success { get; set; }
    public string Cae { get; set; } = string.Empty;
    public string CaeVencimiento { get; set; } = string.Empty;
    public long NumeroComprobante { get; set; }
    public string Resultado { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string? Codigo { get; set; }
    public dcEmissionOutcome EmissionOutcome { get; set; } = dcEmissionOutcome.None;

    /// <summary>
    /// Identificador opaco y estable de la operación durable cuando la respuesta proviene del MCP.
    /// Nunca contiene la idempotency key original.
    /// </summary>
    public string? OperationId { get; set; }

    /// <summary>
    /// Versión del contrato de operación expuesto por la capa MCP.
    /// </summary>
    public string? OperationContractVersion { get; set; }

    /// <summary>
    /// Contexto fiscal administrativo asociado a la operación durable.
    /// </summary>
    public string? OperationContextId { get; set; }

    /// <summary>
    /// Estado durable de la operación (Created, NumberAssigned, Submitting, Authorized, FiscalRejected, Uncertain).
    /// </summary>
    public string? OperationState { get; set; }

    /// <summary>
    /// Acciones explícitas permitidas/recomendadas por el contrato. Evita un retryable ambiguo.
    /// </summary>
    public string[] AllowedNextActions { get; set; } = Array.Empty<string>();

    public dcConcepto? Concepto { get; set; }
    public dcTipoDocumento? DocTipo { get; set; }
    public long? DocNro { get; set; }
    public int? PuntoVenta { get; set; }
    public dcTipoComprobante? TipoComprobante { get; set; }
    public long? CbteDesde { get; set; }
    public long? CbteHasta { get; set; }
    public string FechaComprobante { get; set; } = string.Empty;
    public string FechaServicioDesde { get; set; } = string.Empty;
    public string FechaServicioHasta { get; set; } = string.Empty;
    public string FechaVencimientoPago { get; set; } = string.Empty;
    public List<string> Observaciones { get; set; } = new();
    public List<string> Errores { get; set; } = new();
    public string FechaVencimientoCae => CaeVencimiento;
    public string FechaProceso { get; set; } = string.Empty;
    public string EmisionTipo { get; set; } = string.Empty;
    public decimal ImporteTotal { get; set; }
    public decimal ImporteNeto { get; set; }
    public decimal ImporteNoGravado { get; set; }
    public decimal ImporteExento { get; set; }
    public decimal ImporteTributos { get; set; }
    public decimal ImporteIva { get; set; }
    public string MonedaId { get; set; } = string.Empty;
    public decimal MonedaCotizacion { get; set; }
    public dcCondicionIvaReceptor? CondicionIvaReceptor { get; set; }
    public List<IvaDetalle> Iva { get; set; } = new();
    public List<TributoDetalle> Tributos { get; set; } = new();

    public sealed class IvaDetalle
    {
        public dcAlicuotaIva? Alicuota { get; set; }
        public decimal BaseImponible { get; set; }
        public decimal Importe { get; set; }
    }

    public sealed class TributoDetalle
    {
        public int? Id { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public decimal BaseImponible { get; set; }
        public decimal Alicuota { get; set; }
        public decimal Importe { get; set; }
    }
}
