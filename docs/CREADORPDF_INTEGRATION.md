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
factura-ar + 4
```

Por compatibilidad del schema MCP actual, el parámetro público continúa llamándose temporalmente `templateId`, pero su semántica es **clave lógica estable**. Por ejemplo:

```json
{
  "templateId": "factura-ar",
  "templateVersion": "4"
}
```

`PdfTemplateReference.Key` y `Version` representan esa identidad lógica.

### Default runtime y override del caller

Las tools `emitir_comprobante_con_pdf` y `generar_pdf_comprobante` comparten una única resolución de referencia efectiva:

- `templateId=null` y `templateVersion=null` (o ambos vacíos) usan `Pdf:TemplateKey` + `Pdf:TemplateVersion`;
- ambos valores informados constituyen un override explícito y no dependen del default runtime;
- un par parcial falla con `PDF_TEMPLATE_REFERENCE_INVALID`;
- si el caller necesita el default y la configuración está incompleta, falla con `PDF_TEMPLATE_DEFAULT_NOT_CONFIGURED`.

La configuración operativa recomendada para el template actual es:

```text
Pdf__TemplateKey=factura-ar
Pdf__TemplateVersion=6
```

`templateData` sigue siendo input del caller y no participa de esta selección.

ARCA-MCP resuelve internamente:

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

Nuevos consumidores —incluida SecretarIA— pueden enviar `templateId=null` y `templateVersion=null` para seguir siempre el default runtime, conservando `templateData` normalmente. Sólo necesitan persistir/enviar un par lógico explícito cuando quieran fijar una versión concreta como override o reproducir deliberadamente una representación histórica.

No deben usar como dependencia de negocio:

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

La versión `factura-ar:4` mantiene Factura A/B/C y agrega identificación visual explícita para Nota de Crédito A/B/C (tipos 3/8/13). La versión `factura-ar:5` reproduce el layout fiel de ARCA y queda inmutable. La versión `factura-ar:6` conserva ese layout y corrige el contrato semántico de presentación definido en #97, quedando publicada e inmutable. La versión `factura-ar:7` es sucesora de v6, mantiene el contrato de datos de #97 sin cambios y corrige solo problemas de layout/CSS (paginación, caja de letra/COD, alineación de totales). Todas las versiones permanecen disponibles e inmutables para snapshots históricos que las referencian.

Una versión publicada es inmutable:

```text
misma key + misma version = mismo contenido lógico
cambio de HTML/schema = nueva version
```

El bootstrap nunca sobrescribe silenciosamente una versión publicada. Si creadorpdf no expone contenido/hash suficiente para comparar drift, se confía en esta regla de versionado inmutable y la limitación queda explícita.

### Limitación conocida de WeasyPrint 62.3 con CSS Grid

Durante el desarrollo de `factura-ar:7` se identificó una limitación en WeasyPrint 62.3: cuando un elemento `position:absolute` tiene como contenedor inmediato un elemento con `display:grid`, WeasyPrint ignora propiedades `width` y `transform:translateX()` del elemento posicionado y le aplica en su lugar el ancho de la celda de grid que le corresponde. Esto causó que la caja de letra/COD (objetivo ~47pt) se expandiera a 285pt. La solución fue reemplazar `position:absolute` con un layout basado en grid de columnas explícitas (1fr 17mm 1fr) y posicionar el elemento por flujo normal. Esta limitación debe tenerse en cuenta al diseñar templates futuros: evitar `position:absolute` dentro de contenedores `display:grid` cuando se requiera dimensión exacta.

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
Pdf__TemplateKey=factura-ar
Pdf__TemplateVersion=6
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

La regeneración por consulta también puede reconstruir una Nota de Crédito/Débito cuando `FECompConsultar` devuelve evidencia suficiente en `CbtesAsoc` o `PeriodoAsoc`. El adapter SOAP conserva esos campos en `dcFacturaResponse` y `FiscalDocumentSnapshotFactory.FromConsult()` los materializa sin consultar datos comerciales ni `templateData`.

El comportamiento permanece fail-closed: si no existe asociación suficiente, si un `CbteAsoc` presente carece de tipo/punto de venta/número utilizables, o si un `PeriodoAsoc` presente está incompleto, la regeneración falla con:

```text
FISCAL_DOCUMENT_ASSOCIATION_UNAVAILABLE
```

No se inventan campos faltantes ni se descartan silenciosamente asociaciones malformadas. La operación conserva la autorización existente y nunca llama a `FECAESolicitar`.

## QR oficial server-owned (`arcaQr`)

A partir de `factura-ar:5`, `PdfDocumentRenderer` deriva un bloque adicional reservado `arcaQr` directamente desde el `FiscalDocumentSnapshot`, antes de invocar al provider:

```json
{
  "arcaQr": {
    "verificationUrl": "https://www.arca.gob.ar/fe/qr/?p=<json-v1-base64>",
    "imageDataUri": "data:image/png;base64,..."
  }
}
```

- `ArcaQrPayloadBuilder` construye el JSON v1 oficial (`ver`, `fecha`, `cuit`, `ptoVta`, `tipoCmp`, `nroCmp`, `importe`, `moneda`, `ctz`, `tipoDocRec`/`nroDocRec` opcionales, `tipoCodAut="E"`, `codAut`=CAE) y la URL de verificación oficial. Falla cerrado (`ArcaQrPayloadException`) ante CUIT/CAE/fecha/PV/moneda/cotización inválidos o un documento de receptor parcialmente informado; nunca redondea ni inventa un valor fiscal ausente.
- `ArcaQrImageGenerator` renderiza el PNG localmente con QRCoder (sin `System.Drawing`, compatible Linux/Docker) y lo codifica como `data:image/png;base64,...`. No se usan recursos remotos en tiempo de render.
- `arcaQr` es un nombre raíz reservado: un intento del caller de aportarlo en `templateData` (case-insensitive) se rechaza localmente sin llamar al provider.
- Si el QR no puede construirse, el render documental falla con `PDF_QR_UNAVAILABLE` sin invocar a `creadorpdf`. El CAE ya autorizado no se ve afectado y nunca se solicita una nueva emisión por este motivo.

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

### Contrato de presentación `factura-ar:5`

`factura-ar-v5.schema.json` amplía exclusivamente presentación respecto de `factura-ar:4`:

- `emisor`: `razonSocial`, `domicilio`, `condicionIva`, `ingresosBrutos`, `fechaInicioActividades` (todos obligatorios como texto a mostrar).
- `receptor`: `razonSocial` obligatorio, `domicilio` opcional.
- `condicionVenta`: opcional en la raíz; si falta, el template oculta la línea sin dejar texto vacío.
- `conceptos[]`: `descripcion`, `cantidad`, `unidadMedida`, `precioUnitario`, `importe` obligatorios; `codigo` opcional; `porcentajeBonificacion`/`importeBonificacion` opcionales con default visual `0,00` cuando están ausentes.

### `factura-ar:6` — contrato de presentación corregido (#97)

`factura-ar:5` permanece inmutable porque ya fue publicada. El incidente de #97 se corrige en una nueva versión `factura-ar:6`, que conserva el layout de v5 y modifica únicamente el contrato semántico de presentación:

- `emisor.razonSocial`, `emisor.domicilio` y `emisor.condicionIva` siguen siendo obligatorios.
- `emisor.ingresosBrutos`: opcional (`string|null`). Si viene con valor se presenta exactamente ese valor; si falta o viene vacío, el template deriva sólo para presentación el CUIT fiscal `fiscal.emisorCuit` con formato `XX-XXXXXXXX-X` cuando tiene 11 dígitos. Si no tiene 11 dígitos, se muestra el valor fiscal sin reformatear.
- `emisor.fechaInicioActividades`: opcional (`string|null`). Sólo se muestra cuando existe un valor no vacío; ARCA-MCP no inventa esa fecha.
- `receptor.razonSocial` sigue siendo obligatorio y `receptor.domicilio` opcional.
- `condicionVenta`: opcional (`string|null`); el valor explícito gana y, si falta o viene vacío, el fallback visual es `Contado`.
- `conceptos[].unidadMedida`: opcional (`string|null`); el valor explícito gana y, si falta o viene vacío, el fallback visual es `unidades`.
- `codigo`, `porcentajeBonificacion` e `importeBonificacion` conservan el comportamiento de v5; los dos últimos mantienen fallback visual cero.

Estos fallbacks son exclusivamente de presentación: no modifican `templateData`, el `FiscalDocumentSnapshot` ni la evidencia persistida. `fiscal` y `arcaQr` continúan siendo server-owned.

La condición frente al IVA del receptor, el tipo/número de documento, las fechas fiscales, los comprobantes/período asociados de una Nota de Crédito y el CAE/vencimiento se renderizan siempre desde `fiscal`, nunca desde `templateData`. El template numera punto de venta (5 dígitos) y comprobante (8 dígitos) con ceros a la izquierda como en ARCA.

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
templateId      # nullable; semántica actual: templateKey lógico
templateVersion # nullable; null/null usa el default runtime
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

SecretarIA puede enviar `templateId=null` y `templateVersion=null` para regenerar con el default runtime vigente y seguir enviando `templateData`. Si alguna integración necesita fijar una representación visual concreta, debe conservar y enviar ambos valores como override explícito. La verdad fiscal se obtiene del contexto y de ARCA, no de esos datos.

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
