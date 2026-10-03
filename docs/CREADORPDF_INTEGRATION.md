# Integración con creadorpdf

Para operación, publicación de templates, ownership/scopes, smoke tests y troubleshooting, ver [RUNBOOK_PDF_TEMPLATES.md](RUNBOOK_PDF_TEMPLATES.md).

ARCA-MCP encapsula la generación de comprobantes PDF y es el único consumidor que conoce la API key de creadorpdf. SecretarIA nunca llama al renderer directamente ni puede aportar el bloque fiscal.

> Este documento describe el provider externo actual. La modularización de providers/fallback interno pertenece a #13. El contrato fiscal y la taxonomía pública definidos aquí son deliberadamente neutrales para poder reutilizarse con otro backend documental.

## Referencia lógica de template

Los callers ya no deben persistir ni configurar el identificador físico `tpl_*` generado por creadorpdf.

La identidad durable es:

```text
templateKey + templateVersion
```

Ejemplo:

```text
factura-ar + 3
```

Por compatibilidad del schema MCP actual, el parámetro público continúa llamándose temporalmente `templateId`, pero su semántica es **clave lógica estable**. Por ejemplo:

```json
{
  "templateId": "factura-ar",
  "templateVersion": "3"
}
```

`PdfTemplateReference.Key` y `Version` representan esa identidad lógica. ARCA-MCP resuelve internamente:

```text
factura-ar:3
    ↓
GET /v1/templates/managed
    ↓
exactamente una versión published visible para la credencial runtime
    ↓
tpl_<id-opaco>
    ↓
POST /v1/documents
```

El owner nunca es input del caller. La visibilidad queda determinada por la API key runtime de creadorpdf.

### Compatibilidad temporal `tpl_*`

Durante la migración se acepta `templateId="tpl_..."` como pass-through explícito. Ese modo existe sólo para consumidores legacy y está deprecated.

Nuevos consumidores —incluida SecretarIA— deben persistir:

```text
templateKey
templateVersion
templateData snapshot
```

y no deben usar como dependencia de negocio:

```text
tpl_*
owner_id
Pdf:BaseUrl
Pdf:ApiKey
```

### Cache e invalidación

La resolución lógica se cachea en memoria por:

```text
provider + templateKey + templateVersion
```

con expiración acotada y sin secretos.

Si creadorpdf responde `404 unknown_template` para un ID físico obtenido de cache:

1. ARCA-MCP invalida esa referencia una vez;
2. vuelve a consultar `/v1/templates/managed`;
3. reintenta **una sola vez** el render documental con el nuevo `tpl_*`;
4. nunca repite emisión, numeración ni solicitud de CAE.

Esto permite recrear/reseedear un template físico sin cambiar `factura-ar:3` en SecretarIA.

## Manifest y bootstrap de templates

La fuente declarativa de deploy es:

```text
deploy/cadencia/pdf-templates/manifest.json
```

Formato actual:

```json
{
  "version": 1,
  "templates": [
    {
      "key": "factura-ar",
      "version": "3",
      "template": "factura-ar-v3.html",
      "schema": "factura-ar-v3.schema.json"
    }
  ]
}
```

`bootstrap-pdf-template.sh` itera el manifest y es idempotente:

- valida manifest y archivos;
- busca `key+version` visible con la credencial runtime;
- no crea duplicados cuando ya existe;
- publica un draft cuando corresponde;
- crea y publica sólo cuando la versión no existe;
- vuelve a verificar visibilidad con la credencial runtime;
- prioriza en su salida `TEMPLATE_KEY`, `TEMPLATE_VERSION`, `TEMPLATE_OWNER` y `TEMPLATE_STATUS`;
- el ID físico se imprime únicamente como diagnóstico y no debe copiarse a SecretarIA.

Una versión publicada es inmutable:

```text
misma key + misma version = mismo contenido lógico
cambio de HTML/schema = nueva version
```

El bootstrap nunca sobrescribe silenciosamente una versión publicada. Si creadorpdf no expone contenido/hash suficiente para comparar drift, se confía en esta regla de versionado inmutable y la limitación queda explícita.

## Credenciales y scopes

### Runtime ARCA-MCP

La credencial permanente del contenedor necesita únicamente el mínimo operativo:

```text
templates:read
documents:render
```

Se configura como:

```text
Pdf__BaseUrl=http://creadorpdf:8080
Pdf__ApiKey=...
```

### Deploy/bootstrap

Crear o publicar templates requiere una credencial de gestión temporal del mismo owner, con scopes equivalentes a:

```text
templates:read
templates:write
templates:publish
```

Se pasa al bootstrap como `CREADORPDF_TEMPLATE_API_KEY`. Esa credencial:

- no se commitea;
- no entra como secret permanente al contenedor ARCA;
- se crea/revoca fuera del runtime;
- debe pertenecer al mismo owner que la key runtime;
- se valida indirectamente comprobando que la versión resultante sea visible con la key runtime.

## Contrato fiscal autorizado

creadorpdf no recibe directamente `dcFacturaResponse`. ARCA-MCP normaliza la información autorizada a `FiscalDocumentSnapshot`.

La versión actual es:

```text
arca-fiscal-snapshot/2.0
```

El snapshot incluye:

- `contractVersion`;
- `provenance`;
- `availableFields`;
- ambiente, CUIT emisor y punto de venta del contexto fiscal autorizado;
- tipo y número de comprobante;
- receptor y condición IVA cuando está disponible;
- concepto y fechas fiscales;
- importes, IVA y tributos;
- moneda y cotización;
- comprobantes/período asociados cuando el camino de origen los conoce de forma autorizada;
- CAE, vencimiento y resultado.

### Procedencia

`provenance` permite distinguir de dónde proviene la verdad fiscal sin cambiar su representación:

```text
emisión/replay
identity      = authorized-context
fiscalData    = emission-request
authorization = arca-cae-response

regeneración por consulta
identity      = authorized-context
fiscalData    = fe-comp-consultar-validated
authorization = fe-comp-consultar
```

El ambiente, CUIT emisor y punto de venta provienen del contexto fiscal server-owned materializado por ARCA-MCP. No se toman de `templateData` ni de parámetros arbitrarios del caller.

`availableFields` enumera los campos que el snapshot considera seguros para presentar. Su objetivo es evitar que un valor ausente se confunda con un cero o string vacío autorizado.

## Validación de consultas fiscales

`FECompConsultar` es la fuente fiscal para regenerar un comprobante existente. Antes de construir el snapshot, ARCA-MCP verifica:

- tipo, PV, concepto y receptor;
- coincidencia del PV con el contexto histórico seleccionado;
- fecha de comprobante;
- moneda y cotización;
- importes no negativos y balance:

```text
neto + noGravado + exento + IVA + tributos = total
```

con tolerancia de `0.01`;

- detalle de IVA cuando `ImporteIva > 0`;
- detalle de tributos cuando `ImporteTributos > 0`;
- fechas desde/hasta/vencimiento para servicios.

El parser SOAP histórico representa algunos nodos ausentes como `0` o `""`. El snapshot V2 no acepta esos defaults automáticamente como verdad fiscal: la reconstrucción debe ser internamente consistente. Si no alcanza la evidencia disponible, el PDF falla con `FISCAL_DOCUMENT_INCOMPLETE` y la autorización fiscal permanece intacta.

### Notas históricas

La emisión/replay conoce el request fiscal validado y por eso el snapshot preserva `CbteAsoc` o `PeriodoAsoc`.

La implementación actual de `FECompConsultar` todavía no expone esas asociaciones en `dcFacturaResponse`. Por lo tanto, la regeneración histórica de una nota falla cerrado con:

```text
FISCAL_DOCUMENT_ASSOCIATION_UNAVAILABLE
```

hasta que el adapter de consulta incorpore ese dato. No se inventa ni se reutiliza una asociación aportada por presentación.

## Datos de presentación (`templateData`)

Los datos comerciales o visuales —por ejemplo razón social mostrada, domicilio, logo, items/descripciones y branding— pertenecen a `templateData`.

`templateData`:

- debe ser un objeto JSON;
- tiene un máximo de 128 KiB UTF-8;
- tiene profundidad máxima de 16 niveles;
- puede contener `items` y sus importes descriptivos;
- no es fuente de verdad para totales, impuestos, identidad fiscal ni autorización.

En la raíz se rechazan, sin distinguir mayúsculas/minúsculas, `fiscal` y los aliases del contrato fiscal como `cae`, `importeTotal`, `puntoVenta`, `numeroComprobante`, `monedaId`, etc.

Un template puede mostrar líneas monetarias provenientes de `items`, pero los totales, IVA, tributos, moneda, número y CAE deben renderizarse desde el bloque reservado `fiscal`. Una discrepancia visual en `items` nunca modifica el comprobante autorizado.

## Resultado documental independiente

Estados posibles:

```text
NotAttempted
Rendered
Failed
```

`PdfRenderResult` conserva los campos históricos:

```text
Status
Base64
ErrorCode
Message
```

y agrega metadata diagnóstica opcional:

```text
Provider
ProviderStatusCode
ProviderErrorCode
FailureKind
```

Los campos nuevos son informativos. El contrato estable para decisiones del caller es `ErrorCode`.

### Taxonomía pública

| Condición | `ErrorCode` estable |
| --- | --- |
| request / schema rechazado (400/422) | `PDF_INVALID_REQUEST` |
| credencial inválida (401) | `PDF_PROVIDER_UNAUTHORIZED` |
| permiso insuficiente (403) | `PDF_PROVIDER_FORBIDDEN` |
| `404 + unknown_template` | `PDF_TEMPLATE_NOT_FOUND` |
| referencia lógica existente pero no publicada | `PDF_TEMPLATE_NOT_PUBLISHED` |
| más de un published para la misma referencia lógica | `PDF_TEMPLATE_AMBIGUOUS` |
| rate limit (429) | `PDF_RATE_LIMITED` |
| timeout remoto/local no causado por caller | `PDF_TIMEOUT` |
| 5xx, DNS, refused/network | `PDF_UNAVAILABLE` |
| 2xx con MIME no PDF | `PDF_INVALID_RESPONSE` |
| otro fallo documental clasificado | `PDF_RENDER_FAILED` |

No se asume que todo 404 sea un template inexistente: `PDF_TEMPLATE_NOT_FOUND` se usa cuando el código remoto confirma `unknown_template` o cuando falla la resolución lógica.

La cancelación explícita del caller conserva semántica de cancelación y no se convierte en fallo documental.

### Ejemplo: template inexistente

```json
{
  "status": "Failed",
  "base64": null,
  "errorCode": "PDF_TEMPLATE_NOT_FOUND",
  "message": "La plantilla solicitada no existe o no está publicada para la credencial configurada.",
  "provider": "creadorpdf",
  "providerStatusCode": 404,
  "providerErrorCode": "unknown_template",
  "failureKind": "TemplateNotFound"
}
```

### Ejemplo: forbidden

```json
{
  "status": "Failed",
  "errorCode": "PDF_PROVIDER_FORBIDDEN",
  "providerStatusCode": 403
}
```

### Ejemplo: validación 422

```json
{
  "status": "Failed",
  "errorCode": "PDF_INVALID_REQUEST",
  "providerStatusCode": 422
}
```

### Ejemplo: provider 5xx

```json
{
  "status": "Failed",
  "errorCode": "PDF_UNAVAILABLE",
  "providerStatusCode": 503
}
```

ARCA-MCP nunca devuelve el body remoto crudo, `details`, headers, API keys, PDF Base64 en logs ni `templateData` completo. El body de error sólo se lee hasta un límite explícito para intentar extraer el código remoto conocido.

## Emisión + PDF

La tool `emitir_comprobante_con_pdf` requiere una `idempotencyKey` estable y ejecuta:

1. validación de template/presentación y configuración local del renderer;
2. recuperación o creación de la operación fiscal idempotente;
3. numeración durable y solicitud de CAE sólo cuando corresponde;
4. si ARCA rechaza, `pdf.status = NotAttempted`;
5. si ARCA autoriza o la key ya estaba autorizada, snapshot fiscal V2 desde request validado + contexto histórico + autorización;
6. resolución lógica del template y render del PDF;
7. si snapshot o renderer falla, conserva número/CAE y devuelve el fallo exclusivamente en `pdf`.

Una respuesta con fiscal autorizado y PDF fallido significa que **el comprobante ya existe**. Repetir la misma key con el mismo request fiscal recupera esa autorización y vuelve a intentar solamente la etapa documental.

El template, su versión y `templateData` no forman parte del fingerprint fiscal; cambiar exclusivamente la presentación no genera un nuevo CAE.

## Generación o regeneración de un comprobante existente

La tool `generar_pdf_comprobante` recibe:

```text
numeroComprobante
tipoComprobante
templateId      # semántica actual: templateKey lógico
templateVersion
templateData
contextId (cuando sea necesario desambiguar)
```

y ejecuta exclusivamente:

```text
contexto autorizado
    ↓
FECompConsultar
    ↓
validación de evidencia fiscal
    ↓
FiscalDocumentSnapshot V2
    ↓
resolver templateKey + version
    ↓
renderer PDF
```

Esta operación nunca llama a `FECAESolicitar` ni al sequencer de emisión. Si ARCA no está accesible o la consulta no contiene evidencia suficiente para un snapshot seguro, no se genera un PDF fiscal inventado.

SecretarIA debe conservar `templateKey`, `templateVersion` y `templateData` si desea reproducir la misma representación visual. La verdad fiscal se obtiene del contexto y de ARCA, no de esos datos.

## Qué debe hacer el caller ante un fallo PDF

- `PDF_TEMPLATE_NOT_FOUND`, `PDF_TEMPLATE_NOT_PUBLISHED`, `PDF_TEMPLATE_AMBIGUOUS`: corregir/publicar/configurar la referencia; no reemitir fiscalmente.
- `PDF_INVALID_REQUEST`: corregir `templateData` o el contrato de presentación.
- `PDF_PROVIDER_UNAUTHORIZED` / `PDF_PROVIDER_FORBIDDEN`: corregir credencial/scopes del renderer.
- `PDF_RATE_LIMITED`, `PDF_TIMEOUT`, `PDF_UNAVAILABLE`: puede reintentar sólo la etapa documental según su política.
- `PDF_INVALID_RESPONSE`: tratar como fallo del backend documental; nunca reemitir el comprobante.

En todos los casos:

```text
Fiscal.Success=true
```

prevalece: el comprobante sigue autorizado aunque `Pdf.Status=Failed`.

## Logging seguro

El borde documental puede registrar metadata estructurada equivalente a:

```text
pdf.provider
pdf.failure_kind
pdf.provider_status_code
pdf.provider_error_code
pdf.template_reference
pdf.template_version
```

Nunca debe registrar:

```text
Authorization
API keys
body remoto completo
PDF Base64
templateData
payload fiscal completo
SOAP
certificados
```

Ver también `docs/MCP_IDEMPOTENCY.md`, `docs/MCP_FISCAL_CONTEXTS.md` y `docs/MCP_OPERATION_CONTRACT.md`.
