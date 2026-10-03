# Runbook operativo de PDF y templates

Este documento concentra el conocimiento operativo necesario para administrar la integración de ARCA-MCP con el renderer externo de PDF, publicar templates, diagnosticar fallos y ejecutar smoke tests sin comprometer la seguridad fiscal ni exponer secretos.

Última validación práctica de este runbook: 2026-10-03.

## 1. Alcance

Este runbook cubre:

- arquitectura del flujo documental;
- separación entre datos fiscales y datos de presentación;
- ubicación de código, configuración y templates en este repositorio;
- integración con CreadorPDF;
- ownership y scopes de API keys;
- creación/publicación idempotente de templates;
- smoke tests de generación y regeneración;
- diagnóstico de `401`, `403`, `404 unknown_template`, `422`, fallos de red y respuestas inválidas;
- creación y revocación segura de credenciales temporales de deploy;
- reglas de versionado de templates;
- estado actual y evolución planificada del contrato de template.

No cubre emisión fiscal general, numeración, migración de contextos fiscales ni operación de WSAA/WSFE salvo lo necesario para entender el flujo PDF.

## 2. Invariantes de seguridad

1. El PDF es un efecto documental posterior. Nunca debe determinar si un comprobante fiscal existe o no.
2. Un fallo del renderer no invalida un CAE ya obtenido.
3. `generar_pdf_comprobante` consulta un comprobante existente y nunca solicita un CAE nuevo.
4. El caller nunca puede aportar o sobrescribir el bloque reservado `fiscal`.
5. La verdad fiscal enviada al renderer proviene de `FiscalDocumentSnapshot` construido por ARCA-MCP.
6. Los secretos de CreadorPDF nunca se commitean, imprimen deliberadamente ni se incluyen en ejemplos con valores reales.
7. La credencial de runtime y la credencial de administración de templates tienen responsabilidades distintas.
8. Un template sólo es utilizable por una API key que pueda verlo bajo el mismo owner.
9. El identificador físico `tpl_*` es específico de CreadorPDF y del entorno. No debe tratarse como identidad de negocio durable.

## 3. Arquitectura del flujo

### 3.1 Emisión con PDF

```text
caller
  |
  | factura + idempotencyKey + template + templateData
  v
ARCA-MCP
  |
  | valida template/templateData
  | emite o recupera operación fiscal
  v
FiscalDocumentSnapshot
  |
  | agrega bloque reservado fiscal
  v
IPdfDocumentRenderer
  |
  v
PdfClient / adapter externo
  |
  | POST /v1/documents
  v
CreadorPDF
  |
  v
application/pdf
```

Si la etapa PDF falla después de una autorización fiscal:

```text
Fiscal.Success = true
Pdf.Status = Failed
```

El comprobante ya existe y no debe reemitirse.

### 3.2 Regeneración de un comprobante existente

```text
generar_pdf_comprobante
        |
        v
FECompConsultar
        |
        v
FiscalDocumentSnapshot
        |
        v
renderer
        |
        v
PDF
```

Este camino no utiliza `McpInvoiceSequencer.EmitAsync` ni `FECAESolicitar`.

## 4. Responsabilidad de los datos

### ARCA-MCP

Construye y controla el bloque:

```json
{
  "fiscal": {
    "emisorCuit": "...",
    "puntoVenta": 0,
    "tipoComprobante": "...",
    "numeroComprobante": 0,
    "fechaComprobante": "...",
    "importeNeto": 0,
    "importeIva": 0,
    "importeTributos": 0,
    "importeTotal": 0,
    "monedaId": "...",
    "cae": "...",
    "caeVencimiento": "..."
  }
}
```

El snapshot real contiene más campos fiscales cuando están disponibles.

### Caller / SecretarIA

Aporta únicamente datos de presentación, por ejemplo:

```json
{
  "emisor": {
    "razonSocial": "Razón social a mostrar",
    "domicilio": "Domicilio a mostrar"
  },
  "receptor": {
    "razonSocial": "Consumidor Final",
    "domicilio": null
  },
  "conceptos": [
    {
      "descripcion": "Producto o servicio",
      "cantidad": 1,
      "precioUnitario": 100,
      "importe": 100
    }
  ]
}
```

`templateData.fiscal` está reservado y debe rechazarse.

## 5. Ubicación de componentes en ARCA-MCP

### Código de runtime

```text
dcArca.McpServer/PdfClient.cs
```

Cliente HTTP del renderer externo. Actualmente realiza `POST /v1/documents` y espera `application/pdf`.

```text
dcArca.McpServer/PdfDocumentRenderer.cs
```

Compone `templateData + fiscal`, valida que el caller no inyecte `fiscal` y convierte el PDF a Base64 para el contrato MCP.

```text
dcArca.McpServer/FiscalDocumentSnapshot.cs
```

Contrato fiscal documental normalizado.

```text
dcArca.McpServer/InvoicePdfService.cs
```

Flujo de emisión fiscal + render.

```text
dcArca.McpServer/ExistingInvoicePdfService.cs
```

Flujo de consulta de comprobante existente + render.

```text
dcArca.McpServer/ArcaTools.cs
```

Expone `emitir_comprobante_con_pdf` y `generar_pdf_comprobante`.

### Deploy Cadencia

```text
deploy/cadencia/docker-compose.yml
```

Configura actualmente:

```text
Pdf__BaseUrl=http://creadorpdf:8080
Pdf__ApiKey=${PDF_API_KEY}
```

ARCA-MCP y CreadorPDF deben compartir una red Docker que permita resolver el alias `creadorpdf`.

### Templates versionados

```text
deploy/cadencia/pdf-templates/
```

Template actualmente incorporado:

```text
factura-ar-v3.html
factura-ar-v3.schema.json
```

### Bootstrap

```text
deploy/cadencia/bootstrap-pdf-template.sh
```

Publica/verifica el template usando separación entre credencial runtime y credencial de gestión.

### Documentación relacionada

```text
docs/CREADORPDF_INTEGRATION.md
docs/MCP_SERVER.md
docs/PLAN_INTEGRACION_SECRETARIA_ARCA_PDF.md
```

Issues relacionados:

```text
#13  provider externo reemplazable + fallback interno
#20  consistencia fiscal del PDF
#27  diagnóstico estructurado de errores del renderer
#28  referencias lógicas estables de templates
```

## 6. CreadorPDF: superficies operativas observadas

La instalación validada expone las siguientes superficies:

```text
GET  /health
POST /v1/documents
GET  /v1/templates/managed
POST /v1/templates
POST /v1/templates/{template_id}/publish
POST /v1/previews
GET  /v1/admin/api-keys
POST /v1/admin/api-keys
DELETE /v1/admin/api-keys/{key_id}
```

El contrato exacto debe verificarse contra `/openapi.json` cuando se actualice CreadorPDF.

### Scopes conocidos

```text
templates:read
templates:write
templates:publish
documents:render
documents:preview
```

## 7. Modelo de credenciales y ownership

### 7.1 Runtime de ARCA-MCP

La API key de runtime debe pertenecer al owner que posee los templates que utilizará ARCA-MCP.

Scopes mínimos esperados para el flujo actual:

```text
templates:read
documents:render
```

En Cadencia el owner validado para ARCA-MCP es:

```text
dcarca
```

No almacenar el secret en Git. El runtime lo recibe mediante:

```text
Pdf__ApiKey
```

### 7.2 Gestión de templates

Crear/publicar templates requiere una credencial diferente, del mismo owner, con:

```text
templates:read
templates:write
templates:publish
```

El bootstrap la recibe temporalmente en:

```text
CREADORPDF_TEMPLATE_API_KEY
```

Esta key no debe quedar configurada permanentemente en ARCA-MCP.

### 7.3 Administración de API keys

Los endpoints `/v1/admin/api-keys` están protegidos por:

```text
CREADORPDF_ADMIN_TOKEN
```

El admin token pertenece a CreadorPDF y no es equivalente a una API key normal del renderer.

### 7.4 Credenciales legacy

La instalación observada también admite valores configurados mediante:

```text
CREADORPDF_API_KEYS
```

Esas credenciales legacy se materializan internamente con owner `legacy`. Esto explica por qué un template creado/publicado bajo `legacy` puede existir y, aun así, ser invisible para la key runtime de owner `dcarca`.

No usar una credencial legacy para resolver problemas de ownership en una integración nueva.

## 8. Regla crítica: los templates están aislados por owner

`GET /v1/templates/managed` devuelve templates visibles para la credencial efectiva.

Por lo tanto:

```text
owner=legacy   template factura-ar:2
owner=dcarca   runtime key
```

produce desde la perspectiva de ARCA-MCP:

```text
template no visible
```

Aunque el template exista físicamente en la base de CreadorPDF.

Síntoma típico al intentar renderizar:

```http
HTTP 404
```

con respuesta equivalente a:

```json
{
  "error": {
    "code": "unknown_template",
    "message": "Plantilla desconocida o no publicada.",
    "details": []
  }
}
```

Antes de investigar red, HTML o schema, verificar ownership y visibilidad.

## 9. Inspeccionar templates visibles para ARCA-MCP

Ejecutar en el host Docker, sin imprimir la key:

```bash
ARCA_CONTAINER="${ARCA_CONTAINER:-dcarca-mcpserver}"
PDF_BASE_URL="${PDF_BASE_URL:-http://creadorpdf:8080}"

docker exec "$ARCA_CONTAINER" sh -lc '
  set -eu
  key="$(printenv Pdf__ApiKey)"
  [ -n "$key" ] || exit 1
  curl -fsS "$1/v1/templates/managed" \
    -H "Authorization: Bearer $key"
' sh "$PDF_BASE_URL"
```

La respuesta debe incluir el template esperado con:

```text
owner_id=dcarca
status=published
name=factura-ar
version=3
```

El ID físico tendrá forma similar a:

```text
tpl_<valor-opaco>
```

y puede variar entre entornos o recreaciones.

## 10. Publicar el template versionado del repositorio

Desde la raíz del repositorio desplegado:

```bash
CREADORPDF_TEMPLATE_API_KEY="$DEPLOY_KEY" \
  ./deploy/cadencia/bootstrap-pdf-template.sh
```

No usar `echo "$DEPLOY_KEY"`.

El script:

1. valida que existan HTML y schema;
2. lista templates visibles usando la key runtime de ARCA-MCP;
3. busca `factura-ar:3`;
4. si falta, exige `CREADORPDF_TEMPLATE_API_KEY`;
5. crea el template con la credencial de gestión;
6. publica si todavía está en draft;
7. vuelve a listar con la key runtime;
8. falla si el template no resulta visible para ARCA-MCP;
9. verifica `status=published`;
10. informa template, versión y owner.

La ejecución es idempotente respecto de `name + version` visible para el owner runtime.

## 11. Crear una key temporal de gestión

Crear esta credencial sólo cuando haga falta publicar una versión nueva.

Payload conceptual:

```json
{
  "name": "dcarca-template-deploy",
  "owner_id": "dcarca",
  "scopes": [
    "templates:read",
    "templates:write",
    "templates:publish"
  ]
}
```

El endpoint administrativo requiere `CREADORPDF_ADMIN_TOKEN`.

Recomendaciones:

- obtener el admin token dentro del host/container, no copiarlo a shell history;
- capturar el `secret` devuelto en una variable;
- pasarlo directamente al bootstrap;
- conservar solamente el `key_id` para poder revocarlo;
- revocar la key una vez finalizado el deploy.

## 12. Revocar una key temporal de gestión

Usar `docker exec -i` cuando Python recibe el script por stdin. Sin `-i`, `python -` no recibe el heredoc.

Ejemplo seguro:

```bash
CONTAINER="${CREADORPDF_CONTAINER:-creadorpdf-creadorpdf-1}"
KEY_ID="key_..."

ADMIN_TOKEN="$(docker exec "$CONTAINER" printenv CREADORPDF_ADMIN_TOKEN)"

[ -n "$ADMIN_TOKEN" ] || {
  echo "CREADORPDF_ADMIN_TOKEN no configurado" >&2
  exit 1
}

docker exec -i \
  -e ADMIN_TOKEN="$ADMIN_TOKEN" \
  -e KEY_ID="$KEY_ID" \
  "$CONTAINER" \
  python - <<'PY'
import json
import os
import urllib.request

base = "http://127.0.0.1:8080/v1/admin/api-keys"
headers = {"Authorization": f"Bearer {os.environ['ADMIN_TOKEN']}"}
key_id = os.environ["KEY_ID"]

request = urllib.request.Request(
    f"{base}/{key_id}",
    headers=headers,
    method="DELETE",
)

with urllib.request.urlopen(request) as response:
    print(json.dumps(json.load(response)))

request = urllib.request.Request(
    f"{base}?owner_id=dcarca",
    headers=headers,
)
with urllib.request.urlopen(request) as response:
    keys = json.load(response)

match = next((item for item in keys if item.get("id") == key_id), None)
if match is None:
    print("VERIFY: key no encontrada en el listado")
elif match.get("active"):
    raise SystemExit("ERROR: la key continúa activa")
else:
    print(
        "VERIFY: key revocada; "
        f"active={match.get('active')}, revoked_at={match.get('revoked_at')}"
    )
PY
```

No considerar una revocación confirmada si no se obtiene una respuesta explícita o una verificación posterior.

## 13. Contrato actual del template `factura-ar:3`

El schema versionado requiere:

```text
emisor
receptor
conceptos
fiscal
```

El caller sólo envía los tres primeros. ARCA-MCP agrega `fiscal`.

### `emisor`

```text
razonSocial : string obligatorio
domicilio   : string obligatorio
```

### `receptor`

```text
razonSocial : string obligatorio
domicilio   : string | null
```

### `conceptos[]`

```text
descripcion    : string
cantidad       : number
precioUnitario : number
importe        : number
```

### `fiscal`

El schema exige como mínimo:

```text
emisorCuit
puntoVenta
tipoComprobante
numeroComprobante
fechaComprobante
importeNeto
importeIva
importeTributos
importeTotal
monedaId
cae
caeVencimiento
```

Este bloque lo genera ARCA-MCP.

## 14. Estado actual de la referencia de template

A fecha de este runbook, `PdfClient` envía a CreadorPDF:

```json
{
  "template": {
    "id": "tpl_...",
    "version": "3"
  },
  "data": {}
}
```

Por lo tanto, el contrato runtime todavía necesita el ID físico de CreadorPDF.

Esto es una limitación conocida y está planificada en el issue #28.

### Objetivo de #28

El caller debería trabajar con una referencia lógica estable:

```text
factura-ar + 3
```

ARCA-MCP resolverá internamente:

```text
factura-ar + 3
    -> GET /v1/templates/managed
    -> tpl_<opaque-id>
    -> POST /v1/documents
```

Hasta integrar #28:

- obtener el `TEMPLATE_ID` luego del bootstrap;
- usar ese ID sólo como configuración técnica del entorno;
- no tratarlo como identificador comercial estable;
- no copiarlo a otros entornos esperando que coincida.

## 15. Smoke test de regeneración PDF

Usar un comprobante ya autorizado en homologación.

Variables esperadas en la estación operativa:

```bash
URL="https://<mcp-host>/"
TOKEN="<bearer-token-mcp>"
TEMPLATE_ID="tpl_..."
```

Ejemplo:

```bash
curl -sS -N "$URL" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  --data "{
    \"jsonrpc\":\"2.0\",
    \"id\":1301,
    \"method\":\"tools/call\",
    \"params\":{
      \"name\":\"generar_pdf_comprobante\",
      \"arguments\":{
        \"numeroComprobante\":13,
        \"tipoComprobante\":\"FacturaC\",
        \"templateId\":\"$TEMPLATE_ID\",
        \"templateVersion\":\"3\",
        \"templateData\":{
          \"emisor\":{
            \"razonSocial\":\"Prueba Homologación\",
            \"domicilio\":\"Domicilio de prueba\"
          },
          \"receptor\":{
            \"razonSocial\":\"Consumidor Final\",
            \"domicilio\":null
          },
          \"conceptos\":[{
            \"descripcion\":\"Producto de prueba\",
            \"cantidad\":1,
            \"precioUnitario\":100,
            \"importe\":100
          }]
        }
      }
    }
  }"
```

Sustituir número/tipo/datos por un comprobante de homologación apropiado.

Resultado correcto:

```text
fiscal.success = true
pdf.status = Rendered
pdf.base64 comienza con contenido PDF codificado
```

No confundir:

```text
emissionOutcome = None
```

en una consulta/regeneración con un error. En este flujo no hubo una nueva emisión fiscal.

## 16. Diagnóstico rápido

### 16.1 `403 Forbidden` al crear/publicar templates

Causa más probable:

```text
la key runtime no tiene templates:write/templates:publish
```

Acción:

- no ampliar innecesariamente la key runtime;
- crear una key temporal de gestión del mismo owner;
- ejecutar bootstrap;
- revocar la key temporal.

### 16.2 `404 unknown_template`

Revisar en este orden:

1. ¿El template es visible con la misma key runtime que usa ARCA-MCP?
2. ¿`owner_id` coincide?
3. ¿El template está `published`?
4. ¿El `templateId` físico corresponde a este entorno?
5. ¿La versión enviada coincide?
6. ¿El template fue recreado y cambió su `tpl_*`?

El hallazgo más frecuente durante la homologación fue un template existente bajo owner `legacy` mientras ARCA-MCP operaba con owner `dcarca`.

### 16.3 `422`

Revisar:

- schema del template;
- `templateData` requerido;
- tipos de `cantidad`, `precioUnitario`, `importe`;
- campos obligatorios de emisor/receptor;
- contrato `fiscal` producido por ARCA-MCP.

No resolver un `422` agregando datos fiscales arbitrarios al caller.

### 16.4 `PDF_UNAVAILABLE`

El contrato actual puede colapsar múltiples respuestas HTTP en `PDF_UNAVAILABLE`.

Hasta integrar #27, inspeccionar directamente la respuesta de CreadorPDF para diferenciar:

```text
401
403
404 unknown_template
422
429
5xx
network/timeout
```

No asumir que `PDF_UNAVAILABLE` significa caída de red.

### 16.5 CreadorPDF no resuelve desde ARCA-MCP

Verificar:

```bash
docker exec dcarca-mcpserver getent hosts creadorpdf
```

Luego:

```bash
docker exec dcarca-mcpserver \
  curl -fsS http://creadorpdf:8080/health
```

Si falla DNS, revisar redes Docker compartidas y aliases antes de investigar templates.

### 16.6 Respuesta 2xx pero no PDF

ARCA-MCP espera:

```text
Content-Type: application/pdf
```

Otro MIME se considera respuesta inválida del renderer.

## 17. Render managed vs templates internos de CreadorPDF

En la instalación inspeccionada se observaron dos caminos distintos:

- templates internos/built-in del servicio;
- templates managed creados mediante `/v1/templates`.

Los templates managed se renderizan con el contrato del template y schema publicados. La instalación observada utiliza Jinja con variables estrictas para ese camino.

También se observó que el template built-in histórico `factura-ar-v1` posee comportamiento específico del servicio, incluido preprocesamiento que no debe asumirse automáticamente disponible para templates managed.

Consecuencia operativa:

- no copiar un template built-in esperando que todos sus helpers implícitos funcionen como template managed;
- mantener el template managed autocontenido;
- versionar explícitamente cualquier helper o dato requerido;
- probar el PDF real después de publicar una nueva versión.

Esta sección describe el despliegue inspeccionado. Revalidar contra el código/OpenAPI del CreadorPDF desplegado si cambia su versión.

## 18. Versionado de templates

Regla operativa:

```text
misma identidad lógica + misma versión = contenido inmutable
```

Si cambia HTML o schema:

```text
factura-ar:3 -> factura-ar:4
```

No editar silenciosamente una versión publicada.

Razones:

- reproducibilidad de PDFs históricos;
- diagnóstico consistente;
- cache futura segura;
- evitar que una factura histórica cambie de representación sin cambiar la referencia.

El issue #28 formaliza esta política mediante referencia lógica y manifest de deploy.

## 19. Checklist para publicar una versión nueva

1. Crear nuevos archivos versionados en `deploy/cadencia/pdf-templates/`.
2. Incrementar la versión; no reutilizar una publicada.
3. Validar HTML y schema localmente.
4. Actualizar el bootstrap/manifest según el estado del issue #28.
5. Confirmar que la key runtime sólo tenga permisos necesarios.
6. Crear key temporal de gestión para owner correcto.
7. Ejecutar bootstrap.
8. Confirmar `owner`, `version` y `published` usando la key runtime.
9. Ejecutar `generar_pdf_comprobante` sobre homologación.
10. Verificar `fiscal.success=true` y `pdf.status=Rendered`.
11. Revisar visualmente el PDF cuando cambie layout.
12. Revocar la key temporal de gestión.
13. No almacenar el secret en logs, tickets ni commits.
14. Registrar el cambio/versionado en documentación/release correspondiente.

## 20. Checklist de incidente PDF

Antes de tocar la emisión fiscal:

1. Confirmar si el comprobante ya tiene CAE.
2. Si ya está autorizado, no reemitir.
3. Consultar/regenerar mediante `generar_pdf_comprobante`.
4. Verificar conectividad `ARCA-MCP -> creadorpdf`.
5. Listar templates con la key runtime.
6. Verificar owner/status/version.
7. Si existe respuesta remota, conservar status y error code para diagnóstico.
8. Revisar schema/templateData sólo después de descartar ownership.
9. No usar la key admin como runtime.
10. No ampliar scopes del runtime por comodidad.

## 21. Estado de mejoras pendientes

### #27 — errores estructurados del renderer

Objetivo: evitar que `404`, `403`, `422`, etc. se conviertan todos en `PDF_UNAVAILABLE` y preservar metadata diagnóstica segura.

Cuando #27 esté integrado, actualizar este runbook para usar su taxonomía pública como primera herramienta de diagnóstico.

### #28 — referencia lógica de templates

Objetivo: reemplazar la dependencia externa de `tpl_*` por:

```text
templateKey + templateVersion
```

con resolución interna, cache e invalidación segura.

Cuando #28 esté integrado, eliminar de los ejemplos operativos la necesidad de pasar manualmente `TEMPLATE_ID`.

### #13 — provider externo reemplazable + fallback interno

Objetivo: encapsular CreadorPDF como adapter y disponer de renderer interno .NET para continuidad operativa.

Cuando #13 esté integrado, esta guía debe separar claramente:

```text
operación de provider externo
operación de renderer interno
política de fallback
```

## 22. Qué no hacer

No:

```text
- reemitir un comprobante porque falló el PDF;
- insertar manualmente un bloque fiscal en templateData;
- commitear API keys/admin tokens;
- usar una key de owner distinto para publicar templates;
- convertir la key runtime en admin por conveniencia;
- asumir que un tpl_* funciona en otro ambiente;
- reutilizar una versión publicada para contenido nuevo;
- interpretar PDF_UNAVAILABLE como diagnóstico definitivo;
- borrar/recrear templates durante un incidente sin comprobar referencias existentes;
- considerar revocada una key sin verificar la respuesta/estado;
```

## 23. Referencia rápida

```text
Runtime PDF URL
  Pdf__BaseUrl=http://creadorpdf:8080

Runtime secret
  Pdf__ApiKey

Runtime owner validado
  dcarca

Runtime scopes
  templates:read
  documents:render

Deploy secret temporal
  CREADORPDF_TEMPLATE_API_KEY

Deploy scopes
  templates:read
  templates:write
  templates:publish

Admin secret de CreadorPDF
  CREADORPDF_ADMIN_TOKEN

Templates en repo
  deploy/cadencia/pdf-templates/

Bootstrap
  deploy/cadencia/bootstrap-pdf-template.sh

Template validado
  factura-ar:3

Diagnóstico principal de ownership
  GET /v1/templates/managed

Render
  POST /v1/documents

Regeneración segura
  generar_pdf_comprobante
```

## 24. Mantenimiento del runbook

Actualizar este documento cuando cambie cualquiera de estos contratos:

- versión/API de CreadorPDF;
- scopes;
- owner model;
- endpoints;
- formato de template;
- `PdfTemplateReference`;
- bootstrap/manifest;
- taxonomía de errores;
- provider/fallback de #13;
- flujo de regeneración.

Toda modificación debe preservar las invariantes de la sección 2.