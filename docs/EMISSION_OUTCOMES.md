# Taxonomía de resultados de emisión

ARCA-MCP distingue la fase en la que falló una operación fiscal. El objetivo es que el consumidor no confunda una validación local con una autorización incierta ni convierta un fallo ambiguo en un rechazo definitivo.

## Límite del side effect

La validación determinística (`dcFacturaPreflightValidator`) se ejecuta antes de crear la operación idempotente, consultar numeración o reservar un número. Una vez persistido `Submitting`, el servidor considera que el envío puede ocurrir: un resultado inesperado después de ese límite se trata conservadoramente como `Uncertain` salvo que exista evidencia fiscal definitiva.

## Outcomes

| Outcome | ¿pudo enviarse a ARCA? | ¿hay reserva/número? | ¿terminal para la operación? | misma key | acción segura |
|---|---|---|---|---|---|
| `InvalidRequest` | No | No en el flujo seguro | No se crea operación nueva | repetir mismo payload devuelve el mismo error local | corregir payload y usar una operación/key nueva |
| `FailedBeforeSubmission` | No, por evidencia de fase | Puede existir operación `Created`; no implica envío | No | se puede reintentar el trabajo conocido con la misma key | corregir disponibilidad/configuración y reintentar |
| `FiscalRejected` | Sí | Sí | Sí | replay del rechazo | corregir datos fiscales y crear una operación/key nueva |
| `Uncertain` | Sí o no puede demostrarse lo contrario | Sí | No | nunca reenvía a ciegas | consultar/reconciliar la misma operación |
| `Authorized` | Sí | Sí | Sí | replay del resultado autorizado | continuar con trabajo documental/delivery |
| `RecoveredSuccess` | Hubo un intento previo | Sí | se persiste como autorizado | replay del resultado recuperado | continuar; no volver a emitir |

`None` se conserva por compatibilidad para respuestas que no son emisión. Si un adapter devuelve `None`, `InvalidRequest` o `FailedBeforeSubmission` después de que el servidor ya persistió `Submitting`, el sequencer lo convierte a `Uncertain`: esa respuesta tardía no es evidencia suficiente para probar que el side effect no ocurrió.

## Validación determinística

El preflight reusable cubre, sin I/O:

- tipo de comprobante y concepto;
- fechas obligatorias de servicios y formato de fecha del comprobante;
- documento/CUIT del receptor;
- importes, IVA, tributos, moneda y cotización;
- reglas de notas y comprobantes/períodos asociados.

No requiere ni valida el número server-side. La validación completa de Core puede repetir defensas al construir/enviar el SOAP, pero el flujo MCP seguro no llega a numeración con un request que no supera el preflight.

## Fallos antes de envío

Un fallo de `FECompUltimoAutorizado` ocurre antes de asignar número y antes de invocar `FECAESolicitar`; el sequencer lo clasifica `FailedBeforeSubmission` cuando el provider no trae una semántica más específica.

No se debe extender esta clasificación a timeouts, cancelaciones, errores de parsing o fallos de transporte ocurridos después del límite de envío: permanecen `Uncertain` hasta reconciliación.

## Retry de token

Un refresh de token sólo es seguro como retry de una operación remota cuando la capa que lo ejecuta puede demostrar la fase exacta. Una falla de transporte después de construir/invocar `FECAESolicitar` no habilita un segundo envío ciego; debe conservar la misma identidad, número y evidencia para reconciliación.

## Códigos y mensajes

Las respuestas exponen `Codigo`, `Mensaje`, `Errores` y `EmissionOutcome`. Los mensajes son operativos y no deben contener token/sign, PFX, passwords, SOAP completo ni idempotency keys en claro.

La reserva durable y la equivalencia fiscal de reconciliación se endurecen en #17; esta taxonomía es el contrato de fases que ese mecanismo debe consumir.
