# Integración con creadorpdf

ARCA-MCP encapsula la generación de comprobantes PDF y es el único consumidor que conoce la API key de creadorpdf. SecretarIA nunca llama al renderer directamente ni puede aportar el bloque fiscal.

> Este documento describe el provider externo actual. La modularización de providers/fallback interno pertenece a #13. El contrato fiscal definido aquí es deliberadamente neutral y debe ser consumido por cualquier renderer futuro.

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

## Emisión + PDF

La tool `emitir_comprobante_con_pdf` requiere una `idempotencyKey` estable y ejecuta:

1. validación de template/presentación y configuración local del renderer;
2. recuperación o creación de la operación fiscal idempotente;
3. numeración durable y solicitud de CAE sólo cuando corresponde;
4. si ARCA rechaza, `pdf.status = NotAttempted`;
5. si ARCA autoriza o la key ya estaba autorizada, snapshot fiscal V2 desde request validado + contexto histórico + autorización;
6. render del PDF;
7. si snapshot o renderer falla, conserva número/CAE y devuelve el fallo exclusivamente en `pdf`.

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
renderer PDF
```

Esta operación nunca llama a `FECAESolicitar` ni al sequencer de emisión. Si ARCA no está accesible o la consulta no contiene evidencia suficiente para un snapshot seguro, no se genera un PDF fiscal inventado.

SecretarIA debe conservar `templateId`, `templateVersion` y `templateData` si desea reproducir la misma representación visual. La verdad fiscal se obtiene del contexto y de ARCA, no de esos datos.

## Resultado documental independiente

Estados posibles:

```text
NotAttempted
Rendered
Failed
```

Ejemplo de fallo exclusivamente documental:

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

`Authorized + Failed` sigue significando comprobante fiscal autorizado. El retry documental no puede asignar otro número ni solicitar otro CAE.

## Configuración del provider actual

```text
Pdf__BaseUrl=http://creadorpdf:8080
Pdf__ApiKey=sk-creadorpdf-...
EmissionIdempotency__Directory=/data/emission-idempotency
```

La clave de creadorpdf pertenece exclusivamente a ARCA-MCP y necesita `templates:read` y `documents:render`.

Ver también `docs/MCP_IDEMPOTENCY.md`, `docs/MCP_FISCAL_CONTEXTS.md` y `docs/MCP_OPERATION_CONTRACT.md`.
