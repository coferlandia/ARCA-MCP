# Fiscal smoke test

Smoke test fiscal reutilizable para validar una instalación de ARCA-MCP tanto en homologación como en producción.

El runner genera comprobantes reales, por lo que nunca emite sin `--execute`. Si el contexto diagnosticado por el MCP es producción, exige además `--allow-production`.

## Cliente local recomendado

Para probar ARCA-MCP exactamente como lo haría un consumidor externo, usar `run-local.sh` desde Git Bash, Linux o macOS. El wrapper no accede a archivos ni servicios internos del servidor: usa únicamente el endpoint HTTP público, health checks y el contrato MCP autenticado con `Authorization: Bearer <api-key>`.

Primero crear la configuración local no versionada:

```bash
cp scripts/smoke/fiscal_smoke.example.json scripts/smoke/fiscal_smoke.local.json
```

Completar el JSON y luego ejecutar:

```bash
bash scripts/smoke/run-local.sh --execute
```

Si `ARCA_MCP_TOKEN` no está exportado, el script pide la API key sin mostrarla. Por defecto usa `https://arca.cadencia.com.ar/`; se puede cambiar con `--url` o `ARCA_MCP_URL`.

Ejemplo con otro endpoint:

```bash
bash scripts/smoke/run-local.sh \
  --url https://arca.example.com \
  --config scripts/smoke/fiscal_smoke.local.json \
  --execute
```

Producción requiere además el opt-in explícito que ya impone el runner fiscal:

```bash
bash scripts/smoke/run-local.sh --execute --allow-production
```

El wrapper verifica `/health/live` y `/health/ready` y luego delega al mismo `fiscal_smoke.py`. La primera operación MCP es el diagnóstico del contexto usando la API key real; una credencial inválida, scopes/grants insuficientes o un contexto no autorizado abortan antes de iniciar emisiones.

## Qué prueba

Por cada letra configurada (`A`, `B`, `C`) intenta el circuito completo:

1. Factura por ARS 1,00.
2. Replay con la misma idempotency key para verificar que no se genera una segunda autorización.
3. Consulta de operación, reconciliación y consulta oficial del comprobante.
4. Generación y guardado del PDF.
5. Nota de débito por ARS 1,00 asociada a la factura.
6. Nota de crédito por ARS 1,00 asociada a la nota de débito.
7. Nota de crédito por ARS 1,00 asociada a la factura original.

Cuando el circuito termina correctamente, el efecto nominal del conjunto es ARS 0,00: `+1 +1 -1 -1`.

Cada documento tiene un límite duro de ARS 1,00. La factura y los datos visuales del PDF usan dos ítems de ARS 0,45 y ARS 0,55. El modelo fiscal WSFE trabaja con totales, por lo que los ítems se incorporan en `templateData` para el renderer y no como líneas fiscales transmitidas a ARCA.

Para A y B el smoke usa neto ARS 0,83 + IVA ARS 0,17 = total ARS 1,00. Para C usa neto ARS 1,00, IVA ARS 0,00 y no informa alícuota.

Si la factura de una letra no pasa el preflight determinístico, esa letra queda como `SKIPPED_UNSUPPORTED`. Si un comprobante ya fue autorizado y falla un paso posterior, incluso replay, verificación o PDF, el runner conserva ese hecho fiscal y prioriza su compensación antes de continuar.

Si la llamada de emisión sufre timeout o error de transporte, el runner consulta la operación durable por la misma idempotency key y ejecuta `reconciliar_operacion`. Si recupera estado `Authorized`, continúa usando el número/CAE durable sin reemitir una operación nueva. Si el estado sigue `Submitting`/`Uncertain` o no puede demostrarse el resultado, termina con `FAIL`, `manualReconciliationRequired=true` y deja de iniciar nuevas letras. Antes de detenerse intenta compensar cualquier autorización previa cuyo estado sí esté confirmado.

## Configuración

Completar:

- `contextId`: contexto fiscal de la instalación.
- `pointOfSale`: punto de venta del contexto.
- `letters`: letras que se quieren intentar.
- `receiver`: receptor por defecto.
- `receivers.A/B/C`: overrides por letra. Esto permite, por ejemplo, usar un CUIT Responsable Inscripto para A y Consumidor Final para B/C.
- `pdf.templateId` y `pdf.templateVersion`: plantilla publicada usada por `generar_pdf_comprobante`.
- `pdf.templateData`: datos visuales requeridos por la plantilla. El runner agrega `smokeTest`, `smokeLabel` e `items` sólo si esos campos no fueron provistos.

No guardar tokens, certificados ni passwords en el JSON.

## Uso directo del runner

El wrapper local es la forma recomendada para testing manual. Para automatizaciones también se puede invocar directamente el runner existente:

```bash
export ARCA_MCP_URL="https://arca.example.com/"
export ARCA_MCP_TOKEN="<api-key>"
bash scripts/smoke/fiscal_smoke.sh \
  --config scripts/smoke/fiscal_smoke.local.json \
  --execute
```

Producción:

```bash
bash scripts/smoke/fiscal_smoke.sh \
  --config scripts/smoke/fiscal_smoke.local.json \
  --execute \
  --allow-production
```

El flag no fuerza el entorno: primero el runner llama a `diagnosticar_contexto_fiscal` y sólo usa `--allow-production` cuando el MCP reporta producción. Cualquier nombre de entorno desconocido se rechaza de forma fail-closed.

## Evidencia y PDFs

Por defecto se crea:

```text
.smoke-evidence/
  YYYYMMDDTHHMMSSZ/
    summary.json
    summary.md
    A/
      factura-a-<nro>.pdf
      nota-debito-a-<nro>.pdf
      nota-credito-a-anula-nd-<nro>.pdf
      nota-credito-a-anula-factura-<nro>.pdf
    B/
      ...
    C/
      ...
```

Los reportes reutilizan la redacción del harness de homologación: token, CUIT, CAE, idempotency keys y otros valores sensibles no se persisten en claro.

Estados globales:

- `PASS`: todas las letras configuradas completaron el circuito y quedaron compensadas.
- `PASS_WITH_SKIPS`: al menos una letra no pasó el preflight inicial y fue omitida, sin fallos posteriores a una emisión.
- `FAIL`: hubo un fallo después de iniciar un circuito, no se pudo verificar/descargar un PDF o existe un resultado fiscal que requiere reconciliación manual.

Cada letra informa además `fiscallyBalanced`. Un `FAIL` puede tener `fiscallyBalanced=true` cuando el fallo fue de verificación/PDF pero las autorizaciones fiscales confirmadas quedaron correctamente compensadas.

## Tests sin emitir

```bash
python3 -m unittest scripts/smoke/test_fiscal_smoke.py
bash scripts/smoke/test_run_local.sh
```

Estos tests no llaman a ARCA ni al MCP desplegado.
