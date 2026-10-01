# Integración con creadorpdf

La tool `emitir_comprobante_con_pdf` ejecuta un flujo compuesto con dos etapas independientes:

1. valida todas las precondiciones locales de render antes de tocar ARCA;
2. asigna la numeración y solicita el CAE;
3. si ARCA rechaza la emisión, devuelve el resultado fiscal con `pdf.status = NotAttempted`;
4. si ARCA autoriza, agrega el resultado bajo el campo reservado `fiscal` y solicita el render;
5. si creadorpdf responde correctamente, devuelve `pdf.status = Rendered` y el PDF en Base64;
6. si creadorpdf falla por transporte, timeout o contenido inválido, conserva el resultado fiscal autorizado y devuelve `pdf.status = Failed`.

Una factura con `fiscal.success = true` y `pdf.status = Failed` **ya existe fiscalmente**. El caller debe persistir número/CAE y reintentar únicamente la generación documental; nunca debe solicitar otro CAE por el fallo de PDF.

`templateData` debe ser un objeto JSON y no puede definir `fiscal`. Esa condición, junto con `templateId`, `templateVersion` y la configuración local del renderer, se valida antes de emitir para evitar side effects fiscales seguidos de errores determinísticos locales.

Contrato conceptual de resultado:

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
