# Idempotencia durable de emisiones MCP

Las tools recomendadas de emisión de ARCA-MCP requieren una `idempotencyKey` estable creada por el sistema consumidor.

La idempotencia protege exclusivamente el side effect fiscal. No es un ledger comercial, no almacena PDFs y no reemplaza el estado de negocio que mantiene SecretarIA o un futuro FacturaMCP.

## Tools protegidas

- `emitir_comprobante`
- `emitir_comprobante_avanzado`
- `emitir_comprobante_con_pdf`

`solicitar_cae` sigue siendo una operación de bajo nivel: el caller elige el número y debe coordinar por su cuenta numeración e idempotencia. El control de esa superficie respecto de series administradas se endurece en #17.

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

Si una operación existente se reutiliza con datos fiscales o identidad fiscal incompatibles, falla con:

```text
IDEMPOTENCY_KEY_REUSED
```

y no llama a ARCA.

## Evidencia fiscal comparable

Las operaciones nuevas persisten una proyección fiscal normalizada y versionada separada del hash opaco del request. Incluye los campos necesarios para que #17 pueda comparar posteriormente un comprobante consultado con la intención original sin almacenar SOAP, templateData ni un ledger comercial completo.

Los registros migrados desde schema legacy se marcan explícitamente `LegacyUnavailable` porque no contienen esa evidencia estructurada. Esa ausencia nunca autoriza a declarar una reconciliación exitosa sólo por número o CAE.

## Persistencia

Configuración:

```text
EmissionIdempotency__Directory=/data/emission-idempotency
```

El store usa un archivo JSON por operación namespaced, manifiestos de contexto y escritura atómica mediante archivo temporal + replace. Un lock de filesystem protege cada registro/manifiesto durante su actualización.

No se persisten:

- certificados o passwords;
- token/sign WSAA;
- API keys o idempotency keys en claro;
- SOAP requests/responses completos;
- `templateData`;
- PDFs;
- estado de entrega, Mercado Pago o eventos comerciales.

Un archivo corrupto, un schema desconocido o un contexto incompatible fallan cerrado. Nunca se interpretan como “key inexistente”.

La migración desde el schema legacy es explícita y está documentada en `EMISSION_STORE_MIGRATION.md`; no se infiere CUIT/ambiente/consumidor a partir del proceso que arranca.

## Estados

```text
Created
NumberAssigned
Submitting
Authorized
FiscalRejected
Uncertain
```

El número de comprobante se persiste en `NumberAssigned` antes de iniciar el side effect fiscal. Antes de llamar a `FECAESolicitar` se persiste `Submitting`.

`Authorized` y `FiscalRejected` son terminales para esa operación y request fiscal.

## Replay

### Authorized

La misma key + el mismo request + la misma identidad estable devuelve el resultado fiscal almacenado. No solicita otro CAE.

### FiscalRejected

La misma operación devuelve el rechazo almacenado. Para corregir los datos fiscales debe utilizarse una nueva operación/key.

### Submitting / Uncertain

El comportamiento actual consulta exclusivamente el mismo tipo/número mediante `FECompConsultar` y evita un segundo `FECAESolicitar` con la misma operación. #17 endurece esa recuperación para exigir equivalencia fiscal completa y reserva durable de la serie antes de declarar `RecoveredSuccess`.

## PDF y retries

`emitir_comprobante_con_pdf` usa la misma idempotencia fiscal.

Caso:

```text
key K
  -> CAE #123
  -> PDF falla
```

Un retry con `K` y el mismo request fiscal recupera #123 y vuelve a intentar únicamente el render. El template y los datos visuales no forman parte del fingerprint fiscal.

Para regenerar explícitamente un comprobante existente también puede usarse `generar_pdf_comprobante`, que sólo consulta ARCA y nunca emite.

## Alcance de concurrencia

La identidad canónica de serie es:

```text
(ambiente, CUIT representado, punto de venta, tipo de comprobante)
```

La implementación de #15 todavía conserva la serialización de numeración in-process. La reserva durable y la exclusión verificable de un segundo escritor son responsabilidad de #17 y son gate previo a habilitar multiemisor.

No se debe afirmar soporte de coordinación multi-host o múltiples réplicas escritoras sobre un mismo punto de venta.
