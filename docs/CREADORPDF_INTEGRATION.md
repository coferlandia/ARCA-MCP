# Integración con creadorpdf

ARCA-MCP encapsula la generación de comprobantes PDF y es el único consumidor que conoce la API key de creadorpdf. SecretarIA nunca llama al renderer directamente ni puede aportar el bloque fiscal.

## Contrato fiscal canónico

creadorpdf no recibe directamente `dcFacturaResponse`. ARCA-MCP normaliza la información autorizada a `FiscalDocumentSnapshot`, independientemente de si los datos provinieron de una emisión nueva o de `FECompConsultar`.

El bloque `fiscal` usa nombres JSON estables e incluye CUIT emisor, punto de venta, tipo/número, receptor, fechas, importes, IVA, tributos, moneda, CAE y vencimiento.

Los datos comerciales o de presentación —razón social visual, domicilio mostrado, logo, items/descripciones y branding— siguen perteneciendo a `templateData`.

`templateData` debe ser un objeto JSON y no puede definir `fiscal`. ARCA-MCP valida esa condición antes de cualquier side effect fiscal o consulta remota.

## Emisión + PDF

La tool `emitir_comprobante_con_pdf` requiere una `idempotencyKey` estable y ejecuta un flujo compuesto:

1. valida template y configuración local del renderer;
2. recupera o crea la operación fiscal idempotente;
3. asigna/persiste la numeración y solicita CAE sólo cuando corresponde;
4. si ARCA rechaza, devuelve `pdf.status = NotAttempted`;
5. si ARCA autoriza o la key ya estaba autorizada, construye `FiscalDocumentSnapshot`;
6. renderiza el PDF;
7. si creadorpdf falla, conserva el resultado fiscal y devuelve `pdf.status = Failed`.

Una respuesta con fiscal autorizado y PDF fallido significa que **el comprobante ya existe**. Repetir la misma key con el mismo request fiscal recupera esa autorización y vuelve a intentar solamente la etapa documental.

El template, su versión y `templateData` no forman parte del fingerprint fiscal; cambiar exclusivamente la presentación no genera un nuevo CAE.

## Generación o regeneración de un comprobante existente

La tool `generar_pdf_comprobante` recibe:

```text
numeroComprobante
tipoComprobante
templateId
templateVersion
templateData
```

y ejecuta exclusivamente:

```text
FECompConsultar
    ↓
FiscalDocumentSnapshot
    ↓
creadorpdf
```

Esta operación nunca llama a `FECAESolicitar` ni al sequencer de emisión. Sirve tanto inmediatamente después de una emisión separada como para regenerar/re-enviar un comprobante histórico.

SecretarIA debe conservar `templateId`, `templateVersion` y el `templateData` original si desea reproducir la misma representación documental. Los datos fiscales se vuelven a obtener desde ARCA.

## Resultado documental

Estados posibles:

```text
NotAttempted
Rendered
Failed
```

Ejemplo de fallo exclusivo del renderer:

```json
{
  "fiscal": {
    "success": true,
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
EmissionIdempotency__Directory=/data/emission-idempotency
```

La clave de creadorpdf pertenece exclusivamente a ARCA-MCP y necesita `templates:read` y `documents:render`.

Ver también `docs/MCP_IDEMPOTENCY.md`.
