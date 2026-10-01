# Idempotencia durable de emisiones MCP

Las tools recomendadas de emisión de ARCA-MCP requieren una `idempotencyKey` estable creada por el sistema consumidor.

La idempotencia protege exclusivamente el side effect fiscal. No es un ledger comercial, no almacena PDFs y no reemplaza el estado de negocio que mantiene SecretarIA o un futuro FacturaMCP.

## Tools protegidas

- `emitir_comprobante`
- `emitir_comprobante_avanzado`
- `emitir_comprobante_con_pdf`

`solicitar_cae` sigue siendo una operación de bajo nivel: el caller elige el número y debe coordinar por su cuenta numeración e idempotencia.

## Semántica de la key

Una `idempotencyKey` identifica una sola emisión fiscal. Ejemplo:

```text
tenant-123:mp-payment-456:full-payment
```

ARCA-MCP no persiste la key en texto claro. Guarda SHA-256 de la key y un fingerprint SHA-256 determinístico del request fiscal.

El fingerprint excluye deliberadamente:

- el número de comprobante asignado server-side;
- `templateId`;
- `templateVersion`;
- `templateData`;
- el PDF.

Por lo tanto, repetir la misma emisión con otro template puede regenerar el documento sin producir otra factura.

Si una key existente se reutiliza con datos fiscales distintos, la operación falla con:

```text
IDEMPOTENCY_KEY_REUSED
```

y no llama a ARCA.

## Persistencia

Configuración:

```text
EmissionIdempotency__Directory=/data/emission-idempotency
```

El MVP usa un archivo JSON por key hasheada, con lock de filesystem y escritura atómica mediante archivo temporal + replace.

No se persisten:

- certificados o passwords;
- token/sign WSAA;
- API keys;
- SOAP requests/responses completos;
- `templateData`;
- PDFs;
- estado de entrega, Mercado Pago o eventos comerciales.

Un archivo corrupto falla cerrado. Nunca se interpreta corrupción como “key inexistente”.

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

`Authorized` y `FiscalRejected` son terminales para esa key y request fiscal.

## Replay

### Authorized

La misma key + el mismo request devuelve el resultado fiscal almacenado. No solicita otro CAE.

### FiscalRejected

La misma key devuelve el rechazo almacenado. Para corregir los datos fiscales debe utilizarse una nueva operación/key.

### Submitting / Uncertain

ARCA-MCP consulta exclusivamente el mismo tipo/número mediante `FECompConsultar`.

Si ARCA confirma el comprobante:

```text
EmissionOutcome = RecoveredSuccess
state = Authorized
```

Si todavía no puede confirmarse:

```text
state = Uncertain
```

y no se asigna otro número ni se ejecuta un segundo `FECAESolicitar`.

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

La implementación actual está diseñada para una instancia de ARCA-MCP con volumen durable local/compartido en el mismo host.

La serialización de numeración continúa siendo in-process por:

```text
(CUIT, punto de venta, tipo de comprobante)
```

No se debe afirmar soporte de coordinación multi-host o múltiples réplicas escritoras sobre un mismo punto de venta sin agregar un mecanismo distribuido específico.
