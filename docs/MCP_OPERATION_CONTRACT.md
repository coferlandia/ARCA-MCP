# Contrato MCP de capacidades, validación y recuperación

Versión actual del contrato: `arca-mcp/1.0`.

Este contrato agrega superficies de lectura y recuperación sobre la emisión durable existente. No crea un segundo motor fiscal ni un repositorio paralelo de operaciones.

## Capacidades

`obtener_capacidades_fiscales` informa únicamente capacidades implementadas por ARCA-MCP y autorizadas para el consumidor/contexto actual. La respuesta diferencia explícitamente la capacidad local de la habilitación fiscal remota.

La respuesta incluye ambiente, CUIT representado, punto de venta, estado operacional, tipos de comprobante y conceptos implementados, soporte de IVA/tributos/asociaciones y las operaciones disponibles según scopes y grants.

`remoteFiscalEnablement = not-verified-use-diagnostic` significa que la capacidad está implementada pero no implica que ARCA haya confirmado en ese instante la autorización de representación o el punto de venta.

## Prevalidación

`validar_comprobante` ejecuta validaciones determinísticas de Core antes de reservar numeración o crear una operación durable. Devuelve problemas estructurados mediante `validationIssues`.

La prevalidación:

- no reserva número;
- no crea una operación idempotente;
- no solicita CAE;
- no garantiza que una autorización remota futura vaya a ser aceptada;
- no reemplaza las validaciones remotas de ARCA.

## Diagnóstico fiscal

`diagnosticar_contexto_fiscal` ejecuta diagnóstico explícito sin emisión. Reutiliza el probe no emisor de assignments basado en `FEParamGetPtosVenta`.

La respuesta separa:

- configuración local utilizable;
- estado operacional del contexto;
- assignment activa/candidata/histórica;
- resultado de la última comprobación realizada durante ese diagnóstico;
- blockers para retirar una credencial anterior, incluidas operaciones pendientes que conservan su `assignmentRevision` histórica.

Una falla de red, WSAA, WSFE o autorización se reporta como no verificada; nunca se transforma en READY por inferencia. El diagnóstico no activa assignments y no emite una factura de prueba.

## OperationId

Cada emisión durable tiene un `operationId` opaco. V1 reutiliza el hash namespaced que ya identifica internamente la operación por `consumerId + contextId + idempotencyKey`.

La `idempotencyKey` original no se persiste ni se devuelve en claro. Las tools de emisión existentes agregan, cuando existe una operación durable:

- `operationId`;
- `operationContractVersion`;
- `operationContextId`;
- `operationState`;
- `allowedNextActions`.

Estos campos son opcionales en `dcFacturaResponse` para preservar compatibilidad con Core y consumidores no MCP.

## Consulta de operación

`consultar_operacion` acepta exactamente uno de:

- `operationId`; o
- `idempotencyKey` dentro del consumer/contexto autorizado.

La consulta sólo lee el store durable. Una operación inexistente devuelve `OPERATION_NOT_FOUND` y no crea ningún registro.

La respuesta persiste sólo el resultado mínimo que realmente existe. No inventa moneda, importes u otros campos como si hubieran sido recuperados de ARCA. `unavailableFields` identifica información que no está disponible en ese snapshot.

## Reconciliación

`reconciliar_operacion` sólo opera sobre una operación existente.

Estados terminales (`Authorized`, `FiscalRejected`) se devuelven sin nuevos efectos. Estados previos al envío confirmado (`Created`, `NumberAssigned`) no se adelantan ni se emiten desde reconcile.

Para `Submitting` o `Uncertain`, si existe número durable y evidencia fiscal comparable, la tool puede ejecutar `FECompConsultar` usando exactamente la `assignmentRevision` histórica de la operación. Luego compara la lectura oficial con la intención persistida mediante el comparador fiscal existente.

Si son equivalentes:

- la operación pasa a `Authorized`;
- el outcome pasa a `RecoveredSuccess`;
- se libera la reserva de serie;
- no se solicita un segundo CAE.

Si no hay evidencia suficiente o hay mismatch, la operación permanece protegida y exige consulta/revisión según `allowedNextActions`.

Reconcile nunca llama `FECAESolicitar` ni crea una operación ausente.

## Semántica estable

Las respuestas de operación incluyen campos equivalentes a:

```text
contractVersion
found
operationId
contextId
state
emissionOutcome
fiscalReference
credentialAssignmentRevision
errorCode
safeMessage
validationIssues
allowedNextActions
persistedFiscalResult
officialRead
unavailableFields
createdAt
updatedAt
```

No se usa un `retryable=true` genérico. Las acciones distinguen, según estado, entre:

- `retry-known-work`;
- `consult`;
- `reconcile`;
- `render-pdf`;
- `correct-with-new-operation`;
- `manual-review`.

## Seguridad

Todas las superficies validan scopes generales y grants de contexto. `operationId` no permite saltar el namespace: después del lookup se verifica `consumerId` y `contextId` antes de devolver cualquier dato.

Nunca se exponen:

- idempotency keys persistidas;
- PFX;
- passwords;
- token/sign de WSAA;
- rutas secretas de certificados;
- endpoints seleccionables por caller.

Consultar o reconciliar una operación previa a una migración de credencial conserva su revisión histórica. Ante una credencial histórica deshabilitada no existe fallback automático a la nueva.

## Health

`/health/live` y `/health/ready` continúan siendo livianos y no llaman a ARCA. El diagnóstico remoto es una operación explícita y separada.
