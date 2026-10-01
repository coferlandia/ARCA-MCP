# Integración con creadorpdf

La tool `emitir_comprobante_con_pdf` ejecuta un flujo compuesto con dos etapas independientes:

1. valida todas las precondiciones locales de render antes de tocar ARCA;
2. asigna la numeración y solicita el CAE;
3. si ARCA rechaza la emisión, devuelve el resultado fiscal con `pdf.status = NotAttempted`;
4. si ARCA autoriza, construye un `FiscalDocumentSnapshot` canónico y lo agrega bajo el campo reservado `fiscal`;
5. envía el snapshot más los datos visuales a creadorpdf;
6. si creadorpdf responde correctamente, devuelve `pdf.status = Rendered` y el PDF en Base64;
7. si creadorpdf falla por transporte, timeout o contenido inválido, conserva el resultado fiscal autorizado y devuelve `pdf.status = Failed`.

Una factura con `fiscal.success = true` y `pdf.status = Failed` **ya existe fiscalmente**. El caller debe persistir número/CAE y reintentar únicamente la generación documental; nunca debe solicitar otro CAE por el fallo de PDF.

`templateData` debe ser un objeto JSON y no puede definir `fiscal`. Esa condición, junto con `templateId`, `templateVersion` y la configuración local del renderer, se valida antes de emitir.

## Contrato fiscal canónico

creadorpdf no recibe directamente `dcFacturaResponse`. ARCA-MCP normaliza la información autorizada a `FiscalDocumentSnapshot`, independientemente de si los datos provinieron de una emisión nueva o de una consulta `FECompConsultar`.

El bloque `fiscal` usa nombres JSON estables e incluye, entre otros:

```json
{
  "emisorCuit": "20123456786",
  "puntoVenta": 7,
  "tipoComprobante": 6,
  "numeroComprobante": 123,
  "concepto": 1,
  "documentoReceptorTipo": 80,
  "documentoReceptorNumero": 20333444559,
  "condicionIvaReceptor": 1,
  "fechaComprobante": "20261001",
  "importeNeto": 100.00,
  "importeIva": 21.00,
  "importeTotal": 121.00,
  "iva": [
    { "alicuota": 5, "baseImponible": 100.00, "importe": 21.00 }
  ],
  "tributos": [],
  "monedaId": "PES",
  "monedaCotizacion": 1,
  "cae": "...",
  "caeVencimiento": "20261011",
  "resultado": "A"
}
```

Los datos comerciales o de presentación —por ejemplo razón social visual, domicilio mostrado, logo, items/descripciones o branding— siguen perteneciendo a `templateData`; no se inventan ni se mezclan con el snapshot fiscal.

## Resultado compuesto

```json
{
  "fiscal": {
    "success": true,
    "emissionOutcome": "Authorized",
    "numeroComprobante": 123,
    "cae": "..."
  },
  "pdf": {
    "status": "Rendered",
    "base64": "JVBERi0x...",
    "errorCode": null,
    "message": null
  }
}
```

Ante fallo exclusivo del renderer:

```json
{
  "fiscal": {
    "success": true,
    "emissionOutcome": "Authorized",
    "numeroComprobante": 123,
    "cae": "..."
  },
  "pdf": {
    "status": "Failed",
    "base64": null,
    "errorCode": "PDF_UNAVAILABLE",
    "message": "No fue posible comunicarse con el renderer de PDF."
  }
}
```

Configuración:

```text
Pdf__BaseUrl=http://creadorpdf:8080
Pdf__ApiKey=sk-creadorpdf-...
```

La clave de creadorpdf pertenece exclusivamente a ARCA-MCP y necesita `templates:read` y `documents:render`. SecretarIA nunca recibe esa credencial.
