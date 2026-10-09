# Idempotencia durable de emisiones MCP

> **V2 (#62) — identidad del replay:** `OperationKeyHash` de los nuevos requests MCP incluye `consumerId+contextId+representedCuit+pointOfSale+idempotencyKey`; CUIT/PV distintos nunca comparten resultado. `ContextId` es la credencial técnica compartida; los manifests del store de emisiones distinguen la identidad fiscal `(contextId,environment,CUIT,PV)`. La reserva de serie continúa global por `(ambiente,CUIT,PV,tipo)`, con snapshot histórico de `assignmentRevision`. No migrar estados V1: reset operacional posterior únicamente con el control descrito en `RUNBOOK.md`.


Las tools recomendadas de emisión de ARCA-MCP requieren una `idempotencyKey` estable creada por el sistema consumidor.

La idempotencia protege exclusivamente el side effect fiscal. No es un ledger comercial, no almacena PDFs y no reemplaza el estado de negocio que mantiene SecretarIA o un futuro FacturaMCP.

## Tools protegidas

- `emitir_comprobante`
- `emitir_comprobante_avanzado`
- `emitir_comprobante_con_pdf`

La tool histórica `solicitar_cae` está deshabilitada en el servidor MCP con `LOW_LEVEL_EMISSION_DISABLED`: elegir un número desde el caller permitiría saltarse la reserva durable de la serie.

## Identidad de operación

Una `idempotencyKey` identifica una sola emisión fiscal dentro de un consumidor y contexto fiscal estables. Ejemplo de key del consumidor:

```text
tenant-123:mp-payment-456:full-payment
```

ARCA-MCP no persiste la key en texto claro. Calcula primero SHA-256 de la key y luego un hash de namespace versionado que incorpora:

- `consumerId` estable;
- `contextId` estable;
- hash SHA-256 de la key.

La operación guarda además una identidad fiscal congelada con ambiente, CUIT representado, punto de venta y tipo de comprobante. Rotar una API key o la credencial/certificado que representa al mismo emisor no cambia el `consumerId`, el `contextId` ni el namespace de operaciones anteriores.

Un mismo `contextId` no puede reasignarse a otro ambiente/CUIT/PV. Cambiar el CUIT emisor requiere otro contexto.

## Fingerprint fiscal

Se guarda un fingerprint SHA-256 determinístico del request fiscal. La canonicalización está versionada; la versión 1 conserva exactamente la representación usada por el store legacy para que la migración no invalide retries históricos.

El fingerprint excluye deliberadamente:

- el número de comprobante asignado server-side;
- `templateId`;
- `templateVersion`;
- `templateData`;
- el PDF.

Por lo tanto, repetir la misma emisión con otro template puede regenerar el documento sin producir otra factura.

Si una operación existente se reutiliza con datos fiscales o identidad fiscal incompatibles, falla con `IDEMPOTENCY_KEY_REUSED` y no llama a ARCA.

## Evidencia fiscal comparable

Las operaciones nuevas persisten una proyección fiscal normalizada y versionada separada del hash opaco del request. Esa evidencia se usa para comparar un comprobante consultado con la intención original antes de declarar `RecoveredSuccess`.

Los registros migrados desde schema legacy se marcan `LegacyUnavailable`. Esa ausencia nunca autoriza una reconciliación exitosa sólo por número o CAE.

## Persistencia

Configuración:

```text
EmissionIdempotency__Directory=/data/emission-idempotency
```

El store contiene:

- un JSON por operación namespaced;
- manifiestos de contexto;
- reservas durables por serie bajo `.series/reservations`;
- un lease exclusivo de escritor bajo `.series/writer.lock`.

No se persisten certificados, passwords, token/sign WSAA, API keys o idempotency keys en claro, SOAP completo, `templateData`, PDFs ni estado comercial externo.

Corrupción, schema desconocido, contexto incompatible, múltiples owners pendientes o reserva incoherente fallan cerrado.

La migración legacy está documentada en `EMISSION_STORE_MIGRATION.md`.

## Estados

```text
Created
NumberAssigned
Submitting
Authorized
FiscalRejected
Uncertain
```

Antes de iniciar el side effect fiscal debe existir una reserva durable de serie y un número persistido. Inmediatamente antes de `FECAESolicitar` el servidor revalida `FECompUltimoAutorizado`; luego persiste `Submitting`.

`Authorized` y `FiscalRejected` son terminales y liberan la reserva. `NumberAssigned`, `Submitting` y `Uncertain` conservan la autoridad sobre la serie.

## Replay

### Authorized

La misma key + request + identidad devuelve el resultado fiscal almacenado. No solicita otro CAE.

### FiscalRejected

La misma operación devuelve el rechazo almacenado. Para corregir datos fiscales debe utilizarse una nueva operación/key.

### NumberAssigned

La operación conserva el mismo número. Antes del primer envío vuelve a validar que ARCA siga informando el número anterior. Si hay drift devuelve `SERIES_NUMBER_DRIFT` y mantiene la serie bloqueada.

### Submitting / Uncertain

Consulta exclusivamente el mismo tipo/número mediante `FECompConsultar`. Sólo equivalencia fiscal completa produce `RecoveredSuccess`; mismatch o evidencia insuficiente mantienen `Uncertain` y la reserva. Nunca se hace un segundo `FECAESolicitar` automático para esa ambigüedad.

Otra key sobre la misma serie recibe `SERIES_RESERVATION_BLOCKED` mientras exista una operación pendiente.

## PDF y retries

`emitir_comprobante_con_pdf` usa la misma idempotencia fiscal.

Caso:

```text
key K
  -> CAE #123
  -> PDF falla
```

Un retry con `K` recupera #123 y vuelve a intentar únicamente el render. El template y los datos visuales no forman parte del fingerprint fiscal.

`generar_pdf_comprobante` consulta un comprobante existente y nunca emite.

## Concurrencia soportada

La serie canónica es:

```text
(ambiente, CUIT representado, punto de venta, tipo de comprobante)
```

ARCA-MCP V1 soporta un único proceso escritor por store filesystem. El lease se adquiere antes de readiness; un segundo proceso falla con `SERIES_WRITER_BUSY`.

Dentro de ese proceso, series distintas pueden avanzar de forma independiente. Sistemas externos que usen el mismo punto de venta no participan del lease: la revalidación previa detecta drift, pero operativamente esos puntos de venta deben considerarse exclusivos del servicio.
