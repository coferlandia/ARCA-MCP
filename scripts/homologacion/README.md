# Harness de homologación — Epic #14 / issue #21

Este directorio contiene un harness reproducible para ejecutar la evidencia real de homologación contra un `dcArca.McpServer` ya desplegado. No copia certificados, PFX, claves privadas ni tokens al repositorio.

## Requisitos

- Python 3.10+.
- Un MCP desplegado con el build que se quiere homologar.
- Un bearer/API key con scopes y grants necesarios para el `contextId` de homologación.
- Credenciales ARCA de homologación ya instaladas/configuradas del lado servidor.
- Un archivo local de configuración derivado de `homologacion.example.json`.

El token nunca se escribe en el JSON. Se toma únicamente de `ARCA_MCP_TOKEN`.

## Preparación

Desde Git Bash:

```bash
cp scripts/homologacion/homologacion.example.json scripts/homologacion/homologacion.local.json
```

Editar `homologacion.local.json` con el `contextId`, receptor, fecha y escenarios válidos para el ambiente de homologación. El archivo local está ignorado por Git.

Configurar el endpoint y el token solamente en el entorno del proceso:

```bash
export ARCA_MCP_URL="https://<host-mcp>/"
export ARCA_MCP_TOKEN="<api-key>"
export ARCA_MCP_BUILD_SHA="<sha-o-digest-exacto-del-build-desplegado>"
```

## Ejecución segura inicial

Sin flags de emisión, el harness sólo ejecuta health, capacidades, diagnóstico y preflight. No crea comprobantes:

```bash
python scripts/homologacion/epic14_homologacion.py \
  --config scripts/homologacion/homologacion.local.json
```

El resultado debe indicar `INCOMPLETE`, porque deliberadamente todavía falta la evidencia fiscal real.

## Homologación fiscal

Una vez verificado que el diagnóstico corresponde al ambiente `homologacion`:

```bash
python scripts/homologacion/epic14_homologacion.py \
  --config scripts/homologacion/homologacion.local.json \
  --execute-emission
```

La secuencia base es:

1. `/health/live` y `/health/ready`.
2. `obtener_capacidades_fiscales`.
3. `diagnosticar_contexto_fiscal`.
4. `validar_comprobante`.
5. `emitir_comprobante_avanzado`.
6. replay con la misma idempotency key.
7. `consultar_operacion`.
8. `reconciliar_operacion`.
9. `consultar_comprobante` sobre el número autorizado.
10. PDF/renderer, si está habilitado en el JSON.
11. nota asociada, sólo si está habilitada y se pasa `--execute-associated-note`.
12. rechazo fiscal controlado, sólo si está habilitado y se pasa `--execute-controlled-rejection`.

La idempotency key se genera por corrida y nunca se escribe en claro en el reporte.

## Rechazo controlado

El objeto `controlledRejection.invoice` debe ser un caso previamente elegido para llegar hasta ARCA y ser rechazado fiscalmente. No sirve un request que falle en preflight local. El harness sólo considera cumplido este escenario cuando observa:

```text
EmissionOutcome = FiscalRejected
```

Para ejecutarlo:

```bash
python scripts/homologacion/epic14_homologacion.py \
  --config scripts/homologacion/homologacion.local.json \
  --execute-emission \
  --execute-controlled-rejection
```

## Nota asociada

Si las capacidades V1 y el contexto a homologar permiten nota de crédito/débito, configurar `associatedNote.enabled=true` y su factura. El harness toma de la factura base el tipo y número asociados; el punto de venta se toma de `pointOfSale` salvo que la nota lo indique explícitamente.

La nota sólo se emite con el opt-in adicional:

```text
--execute-associated-note
```

## PDF y error de renderer

El escenario de PDF usa `generar_pdf_comprobante` sobre un comprobante ya autorizado, por lo que un error de renderer no genera otro CAE. Puede usarse una plantilla válida para probar generación y, en una corrida separada/controlada, una referencia de renderer que produzca el error esperado. El reporte conserva el resultado fiscal y omite/redacta el contenido PDF.

## Evidencia generada

Por defecto se crean archivos locales en:

```text
.homologation-evidence/
```

Ese directorio está ignorado por Git. Se generan JSON y Markdown con:

- SHA/digest declarado del build desplegado;
- contrato/capacidades y ambiente detectado;
- estado de cada paso;
- invariantes de replay (mismo número, mismo CAE y mismo `operationId` cuando está disponible);
- outcome del rechazo controlado;
- faltantes de evidencia.

CUIT, CAE, token, idempotency key, referencias de credenciales y blobs PDF se redactan antes de persistir el reporte.

El estado `CANDIDATE_COMPLETE_REQUIRES_HUMAN_REVIEW` significa únicamente que el harness reunió todos los escenarios configurados sin inconsistencias automáticas. No cierra #21 ni #14 por sí solo: el reporte debe revisarse y adjuntarse/resumirse en el issue con el SHA/digest real del despliegue.

## Gate de cierre de #21

No cerrar #21 si falta cualquiera de estos elementos:

- build exacto desplegado identificado;
- ambiente de homologación confirmado;
- emisión soportada autorizada;
- replay de la misma key sin segunda autorización;
- query/reconcile sin nueva emisión;
- consulta oficial del comprobante;
- rechazo fiscal real controlado (`FiscalRejected`);
- PDF/renderer con preservación del resultado fiscal;
- nota asociada si forma parte de las capacidades V1 que se quieren declarar homologadas;
- evidencia de coexistencia/migración personal→empresa cuando aplique al contexto real.

Los escenarios de migración de identidad/credencial que no puedan probarse responsablemente en el mismo contexto deben documentarse por separado; los tests automáticos cubren invariantes de software, pero no sustituyen autorización/representación real de ARCA.
