# Integración SecretarIA + ARCA-MCP + creadorpdf

**Contrato:** `arca-mcp/1.0`  
**Tracking SecretarIA:** https://github.com/coferlandia/secretarIA/issues/1736  
**Estado:** contrato, idempotencia, multiemisor, recuperación y PDF implementados; homologación fiscal real pendiente de evidencia autorizada.

## 1. Objetivo

SecretarIA solicita una intención fiscal; ARCA-MCP es la autoridad del side effect fiscal y creadorpdf sólo renderiza un derivado documental.

```text
SecretarIA
  | API key + scopes/grants
  | contextId + idempotencyKey + request fiscal
  v
ARCA-MCP
  |-- preflight / operación durable / numeración / reconcile --> ARCA
  |
  |-- snapshot fiscal autorizado + presentación -----------> creadorpdf
  v
resultado fiscal + operationId + estado/acciones + PDF opcional
```

## 2. Responsabilidades

| Sistema | Autoridad |
| --- | --- |
| SecretarIA | intención comercial, tenant/pago, `idempotencyKey`, `contextId`, estado comercial, presentación y delivery |
| ARCA-MCP | identidad fiscal, grants, credenciales server-owned, operación idempotente, serie, emisión, reconciliación, snapshot fiscal |
| creadorpdf | render del JSON validado; no decide datos fiscales ni emite |
| SIM/entrega | email/WhatsApp/reintentos posteriores; nunca reautoriza por fallo de entrega |

El PDF no es la fuente de verdad fiscal. SecretarIA puede cachearlo/almacenarlo por producto, pero una falla o pérdida del PDF no habilita otra emisión.

## 3. Identidad y multiemisor

SecretarIA autentica con una API key asociada a un `consumerId` estable. La key tiene scopes y grants de contexto.

El `contextId` referencia un contexto fiscal server-owned que fija ambiente, CUIT representado, PV y assignments versionadas. El caller nunca envía ruta de PFX, password, token/sign ni `credentialId` arbitrario.

Si un consumer tiene más de un contexto autorizado, `contextId` es obligatorio. Crear un contexto/tenant nuevo no amplía grants existentes.

Rotaciones:

- API key nueva + mismo `consumerId`: las operaciones siguen perteneciendo al mismo consumer;
- certificado nuevo + mismo emisor: nueva `assignmentRevision`; las operaciones históricas conservan la anterior;
- CUIT/titular fiscal distinto: nuevo contexto/identidad fiscal.

## 4. Idempotencia y operationId

Toda emisión recomendada requiere `idempotencyKey`. La key representa una única intención fiscal y se persiste sólo como hash namespaced por `consumerId + contextId`.

La respuesta incluye `operationId` opaco. SecretarIA debe persistir ambos:

```text
business intent
  -> contextId
  -> idempotencyKey
  -> operationId
  -> outcome/state
```

La misma key + mismo request recupera la misma operación. La misma key + payload fiscal distinto devuelve `IDEMPOTENCY_KEY_REUSED` sin otro side effect.

## 5. Tools contractuales

### Lectura `arca:consultar`

- `obtener_capacidades_fiscales`
- `diagnosticar_contexto_fiscal`
- `consultar_operacion`
- `reconciliar_operacion`
- `consultar_ultimo_comprobante`
- `consultar_comprobante`
- `generar_pdf_comprobante`
- `consultar_condiciones_iva`
- `consultar_padron`

### Validación/emisión `arca:facturar`

- `validar_comprobante`
- `emitir_comprobante`
- `emitir_comprobante_avanzado`
- `emitir_comprobante_con_pdf`

`solicitar_cae` no forma parte del flujo integrable: devuelve `LOW_LEVEL_EMISSION_DISABLED` en MCP.

## 6. Flujo recomendado en SecretarIA

Antes de emitir:

1. `obtener_capacidades_fiscales(contextId)`;
2. opcionalmente `validar_comprobante` para UX inmediata;
3. cuando corresponda, `diagnosticar_contexto_fiscal` durante onboarding/soporte;
4. persistir la intención y su `idempotencyKey` antes de llamar a emisión.

Emisión:

```text
emitir_comprobante[_avanzado|_con_pdf]
  -> persistir operationId/outcome
  -> si Authorized/RecoveredSuccess: continuar
  -> si FiscalRejected: registrar rechazo
  -> si Uncertain/respuesta perdida: consultar_operacion
       -> reconciliar_operacion cuando corresponda
       -> nunca crear otra key para reintentar a ciegas
```

SecretarIA debe consumir `allowedNextActions` de la respuesta de operación, no inferir que un timeout significa “no emitido”.

## 7. Outcomes

- `InvalidRequest`: no cruzó el side effect; corregir input.
- `FailedBeforeSubmission`: no hubo envío comprobado; consultar operación si existe reserva/operationId.
- `FiscalRejected`: rechazo terminal de la operación.
- `Uncertain`: envío posible/evidencia insuficiente; sólo query/reconcile/manual review.
- `Authorized`: autorización terminal.
- `RecoveredSuccess`: autorización confirmada por lectura equivalente después de incertidumbre.

`SERIES_RESERVATION_BLOCKED`, `SERIES_NUMBER_DRIFT`, `RECONCILIATION_MISMATCH` y `RECONCILIATION_EVIDENCE_INSUFFICIENT` nunca se resuelven creando otra key automáticamente.

## 8. PDF

`emitir_comprobante_con_pdf` compone dos fases:

```text
fiscal -> documental
```

Puede ocurrir:

```text
Fiscal = Authorized
PDF = Failed
```

La factura ya existe. Con la misma key/request se recupera esa autorización y se reintenta sólo el render, o se usa `generar_pdf_comprobante`.

El snapshot fiscal V2 incorpora procedencia/completitud y es la única fuente de datos fiscales del render. `templateData` puede aportar branding, receptor descriptivo/items visuales y otros datos no fiscales, pero no puede reemplazar `fiscal`, CAE, totales, moneda, impuestos o numeración.

## 9. Numeración y concurrencia

ARCA-MCP mantiene una reserva durable por serie:

```text
(ambiente, CUIT representado, PV, tipo)
```

V1 soporta un único proceso escritor por store filesystem y rechaza un segundo writer con `SERIES_WRITER_BUSY`.

SecretarIA no debe llamar directamente a ARCA ni usar el mismo PV desde otro numerador. La revalidación previa al envío detecta drift, pero el PV administrado debe considerarse dedicado.

## 10. Recovery/restore

Un restore de backup anterior a emisiones posteriores deja:

```text
/health/live  -> vivo
/health/ready -> 503 RESTORE_RECONCILIATION_REQUIRED
```

Durante recovery:

- nuevas emisiones están bloqueadas;
- lectura/diagnóstico/reconcile siguen disponibles;
- se reconcilia la ventana entre backup y restore contra ledger del consumidor + ARCA;
- sólo una completion operativa con referencia de evidencia vuelve a habilitar nuevas autorizaciones.

SecretarIA debe mostrar/propagar esta indisponibilidad controlada y no cambiar keys/contextos para evitarla.

Ver `OPERATIONS_RECOVERY.md`.

## 11. Estado de implementación del plan original

Ya implementado:

- API keys, scopes, revocación e identidad estable;
- idempotencia durable y `operationId`;
- preflight determinístico;
- reserva/numeración durable y single-writer;
- reconciliación fiscal por equivalencia;
- multiemisor por contextos/grants;
- assignments versionadas y migración gradual de credenciales;
- diagnóstico no emisor;
- query/reconcile explícitos por operación;
- snapshot fiscal V2 y PDF regenerable;
- recovery gate para restore antiguo;
- Docker Linux + health + CI de smoke/restart.

No resuelto por ARCA-MCP porque pertenece al consumidor/producto:

- tratamiento comercial/contable de seña, saldo, total o momento de emisión;
- persistencia comercial de factura/pago/delivery;
- UX/historial/descarga;
- política de almacenamiento de PDFs;
- eventos SIM/email/WhatsApp.

Pendiente como evidencia externa:

- homologación real de cada contexto/titular que vaya a habilitarse;
- verificación operativa de la migración personal → empresa con credenciales autorizadas reales;
- plantilla fiscal/productiva y reglas de QR según el caso de negocio de SecretarIA.

## 12. Gate para integrar SecretarIA

Antes de activar emisión automática en el issue #1736:

1. fijar SHA de ARCA-MCP + contrato `arca-mcp/1.0`;
2. provisionar key/scopes/grants en secret manager;
3. confirmar `contextId` y assignment activa;
4. capabilities + diagnóstico sin blockers;
5. ejecutar homologación real controlada;
6. comprobar retry con misma key = una sola autorización;
7. comprobar recuperación por `operationId`;
8. comprobar PDF/re-render sin segundo CAE;
9. registrar resultado y evidencia no sensible en #1736;
10. sólo entonces habilitar el flujo automático.

La matriz automatizada del Epic #14 está en `EPIC14_ACCEPTANCE.md`.
