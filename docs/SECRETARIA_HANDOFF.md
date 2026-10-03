# Handoff ARCA-MCP → SecretarIA

Contrato: `arca-mcp/1.0`  
Tracking consumidor: https://github.com/coferlandia/secretarIA/issues/1736

Este documento define qué puede asumir SecretarIA del servidor fiscal. El SHA exacto desplegado debe pinnearse en el issue consumidor; no integrar contra “latest”.

## División de responsabilidades

### SecretarIA

Es dueña de:

- intención comercial de facturar;
- tenant/cliente/pago/origen;
- generación y persistencia de `idempotencyKey` estable;
- selección del `contextId` permitido por su grant;
- estado comercial y de entrega;
- datos visuales/no fiscales del template;
- caché/almacenamiento del PDF si el producto lo necesita;
- reintentos de entrega.

### ARCA-MCP

Es dueño de:

- identidad fiscal representada;
- resolución server-owned de credenciales;
- grants por contexto;
- prevalidación fiscal determinística;
- idempotencia del side effect fiscal;
- numeración/reserva de serie;
- emisión/reconciliación con ARCA;
- historial mínimo de operación fiscal;
- snapshot fiscal usado para PDF;
- bloqueo fail-closed ante incertidumbre/restore.

SecretarIA no debe conocer rutas de certificados, passwords, token/sign WSAA ni la API key de creadorpdf.

## Autenticación y autorización

Cada key tiene:

- `consumerId` estable;
- scopes (`arca:consultar`, `arca:facturar`);
- grants explícitos `contextId|operación`.

`arca:facturar` no implica `arca:consultar` y viceversa. Si hay más de un contexto permitido para la operación, SecretarIA debe enviar `contextId` explícitamente.

Rotar una API key no cambia el `consumerId`. Rotar un certificado que representa la misma identidad fiscal no cambia operaciones históricas: cada operación conserva su `assignmentRevision`.

## Tools contractuales

### Consulta — `arca:consultar`

- `obtener_capacidades_fiscales`: contrato/capacidades locales del contexto; no garantiza autorización remota.
- `diagnosticar_contexto_fiscal`: diagnóstico no emisor de configuración/assignment/ARCA.
- `consultar_operacion`: lee una operación durable por `operationId` o `idempotencyKey`; no emite.
- `reconciliar_operacion`: consulta evidencia oficial de una operación existente; nunca crea operación ni solicita CAE.
- `consultar_ultimo_comprobante` / `consultar_comprobante`: lecturas WSFE.
- `generar_pdf_comprobante`: regenera desde consulta oficial; nunca emite.
- `consultar_condiciones_iva`, `consultar_padron`.

### Validación/emisión — `arca:facturar`

- `validar_comprobante`: preflight local; no reserva número ni garantiza futura autorización.
- `emitir_comprobante`.
- `emitir_comprobante_avanzado`.
- `emitir_comprobante_con_pdf`.

`solicitar_cae` con número elegido por caller está deshabilitada en el MCP (`LOW_LEVEL_EMISSION_DISABLED`) para impedir bypass de la serie administrada.

## Identificador de operación

Una emisión durable devuelve un `operationId` opaco. SecretarIA debe persistirlo junto con su `idempotencyKey` y el contexto.

El `operationId` sirve para:

- recuperar estado después de una respuesta perdida;
- consultar sin reconstruir el payload original;
- reconciliar un intento incierto;
- correlacionar soporte sin registrar la idempotency key en logs.

No debe interpretarse internamente ni usarse entre consumers/contextos.

## Idempotency key

La key debe representar una intención fiscal única y estable. Ejemplos conceptuales:

```text
tenant:<tenantRef>:payment:<paymentRef>:full
tenant:<tenantRef>:payment:<paymentRef>:deposit
tenant:<tenantRef>:manual:<requestRef>
```

Reglas:

- mismo contexto + misma key + mismo request fiscal => replay de la misma operación;
- misma key + request diferente => `IDEMPOTENCY_KEY_REUSED`;
- nunca cambiar la key sólo para reintentar un timeout;
- una nueva intención fiscal corregida debe tener una nueva key después de un rechazo determinístico/terminal cuando corresponda.

## Outcomes y acción del consumidor

| Outcome/estado | Significado operativo | Acción SecretarIA |
| --- | --- | --- |
| `InvalidRequest` | no cruzó side effect | corregir datos; no asumir número/CAE |
| `FailedBeforeSubmission` | se sabe que no hubo envío, pero puede existir reserva según fase | consultar operación y seguir `allowedNextActions` |
| `FiscalRejected` | ARCA rechazó definitivamente esa operación | mostrar/registrar rechazo; nueva intención corregida cuando corresponda |
| `Uncertain` | el envío pudo ocurrir o falta evidencia | no reemitir; `consultar_operacion` / `reconciliar_operacion` |
| `Authorized` | autorización persistida | continuar flujo comercial/documental |
| `RecoveredSuccess` | autorización recuperada por lectura equivalente | tratar como autorizada; no volver a emitir |

SecretarIA debe preferir `allowedNextActions` del contrato sobre heurísticas propias.

## Respuesta perdida

Flujo recomendado:

```text
emitir -> timeout/respuesta perdida
       -> consultar_operacion(operationId o idempotencyKey)
       -> si Uncertain: reconciliar_operacion
       -> si Authorized/RecoveredSuccess: continuar
       -> si sigue Uncertain: manual_review / retry de reconcile, nunca otra emisión ciega
```

El test MCP de transporte demuestra un único envío fiscal con recuperación por lectura.

## PDF

El PDF es un derivado, no la fuente de verdad fiscal.

`emitir_comprobante_con_pdf` puede devolver:

```text
Fiscal = Authorized
PDF = Failed
```

En ese caso la factura ya existe. Repetir la misma operación no debe crear otra autorización. SecretarIA puede:

- reintentar la misma tool con misma key/request;
- o regenerar el PDF del comprobante existente.

El campo `fiscal` del payload de render es reservado por ARCA-MCP. `templateData` no puede sustituir importes, CAE, moneda, numeración ni otros campos fiscales canónicos.

## Contextos y multiemisor

`contextId` identifica un contexto fiscal server-owned. No es el CUIT ni una referencia de certificado.

SecretarIA sólo puede usar contextos incluidos en sus grants. Crear un tenant nuevo no amplía automáticamente los grants de keys existentes.

Cambiar titular/CUIT requiere otro contexto fiscal. Cambiar sólo la credencial que representa al mismo emisor se realiza mediante assignment versionada y probe no emisor antes de activarla.

## Restore/recovery

Si ARCA-MCP restaura un backup anterior a emisiones posteriores:

- `/health/ready` queda en 503 con `RESTORE_RECONCILIATION_REQUIRED`;
- nuevas emisiones se bloquean;
- lectura/diagnóstico/reconcile permanecen disponibles;
- operación normal sólo se reanuda después de reconciliar la ventana y registrar evidencia explícita.

SecretarIA debe tratar ese error como indisponibilidad fiscal controlada, no como señal para cambiar idempotency keys o usar otro endpoint de emisión.

## Errores accionables relevantes

- `FISCAL_CONTEXT_REQUIRED`: enviar `contextId` explícito.
- `FISCAL_CONTEXT_FORBIDDEN`: configuración/grant; no reintentar con otro contexto arbitrario.
- `ACTIVE_ASSIGNMENT_REQUIRED` / `ASSIGNMENT_NOT_ACTIVE`: intervención de provisioning.
- `HISTORICAL_ASSIGNMENT_INTERVENTION_REQUIRED`: no hacer fallback de credencial.
- `IDEMPOTENCY_KEY_REUSED`: conflicto de intención/payload.
- `SERIES_RESERVATION_BLOCKED`: existe otra operación pendiente de esa serie.
- `SERIES_NUMBER_DRIFT`: escritor externo/cambio de serie; intervención.
- `RECONCILIATION_MISMATCH`: no reemitir.
- `RECONCILIATION_EVIDENCE_INSUFFICIENT`: no reemitir.
- `RESTORE_RECONCILIATION_REQUIRED`: servicio recuperado desde backup; esperar reconciliación operativa.

## Provisioning mínimo para SecretarIA

Para integrar un ambiente:

1. crear/confirmar contexto fiscal server-owned;
2. validar y activar assignment de credencial;
3. crear API key para SecretarIA con `consumerId` estable;
4. otorgar scopes y grants mínimos;
5. guardar el secreto en secret manager de SecretarIA;
6. llamar `obtener_capacidades_fiscales` y `diagnosticar_contexto_fiscal`;
7. ejecutar caso de homologación controlado;
8. registrar en el issue #1736 el SHA/contract version/contextId no sensible que se aprobó.

## Limitaciones V1

- un único proceso escritor por store filesystem;
- punto de venta administrado/dedicado recomendado;
- no hay coordinación distribuida multi-host;
- un `Uncertain` puede requerir intervención manual indefinida;
- la regeneración histórica de ciertas notas puede fallar cerrado si ARCA no devuelve evidencia asociada suficiente;
- el PDF Base64 es transporte, no persistencia fiscal;
- la homologación real debe hacerse con credenciales autorizadas y no se reemplaza por fakes/CI.

## Evidencia de entrega

La matriz de pruebas está en `EPIC14_ACCEPTANCE.md` y la operación/restore en `OPERATIONS_RECOVERY.md`. El issue consumidor debe registrar el SHA mergeado exacto y la evidencia real de homologación antes de habilitar facturación automática.
