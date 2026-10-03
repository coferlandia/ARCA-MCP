# dcArca.McpServer

`dcArca.McpServer` expone facturación electrónica ARCA como tools MCP HTTP stateless, protegidas por API keys Bearer, scopes y grants de contexto.

Contrato actual: `arca-mcp/1.0`.

## Modelo de autoridad

La identidad de una operación fiscal combina:

```text
consumerId + contextId + ambiente + CUIT representado + PV + tipo
```

La serie fiscal se coordina por:

```text
ambiente + CUIT representado + PV + tipo
```

Una API key no identifica una credencial fiscal. La key autentica un `consumerId` estable y sus grants; el servidor resuelve el `contextId` y una `assignmentRevision` server-owned hacia una referencia opaca de certificado.

Las operaciones idempotentes persisten esa revisión histórica: rotar una API key o certificado no redirige un retry ya existente.

## Tools contractuales

### `arca:consultar`

- `obtener_capacidades_fiscales` — capacidades locales autorizadas; no prueba habilitación remota.
- `diagnosticar_contexto_fiscal` — probe/diagnóstico no emisor.
- `consultar_operacion` — lectura durable por `operationId` o `idempotencyKey`.
- `reconciliar_operacion` — consulta oficial + comparación; nunca emite.
- `consultar_ultimo_comprobante`.
- `consultar_comprobante`.
- `generar_pdf_comprobante`.
- `consultar_condiciones_iva`.
- `consultar_padron`.

### `arca:facturar`

- `validar_comprobante` — preflight determinístico, sin numeración ni side effect.
- `emitir_comprobante`.
- `emitir_comprobante_avanzado`.
- `emitir_comprobante_con_pdf`.

`solicitar_cae` se conserva sólo como superficie histórica de compatibilidad pero el MCP devuelve `LOW_LEVEL_EMISSION_DISABLED`; elegir el número desde el caller rompería la reserva durable.

`arca:facturar` no implica `arca:consultar`, ni al revés.

## Emisión segura

Flujo simplificado:

```text
preflight
 -> identidad/contexto/grant
 -> operation namespace
 -> reserva durable de serie
 -> NumberAssigned
 -> revalidación FECompUltimoAutorizado
 -> Submitting
 -> FECAESolicitar
 -> Authorized | FiscalRejected | Uncertain
```

Antes de cruzar el side effect, la reserva ya está persistida. `Submitting`/`Uncertain` mantienen la serie bloqueada.

Una segunda key sobre la misma serie no avanza mientras exista una operación pendiente.

## Single writer V1

La topología filesystem soportada admite un único proceso escritor por store. `FileSystemFiscalSeriesCoordinator` mantiene un lease exclusivo `writer.lock`; un segundo proceso sobre el mismo store falla con `SERIES_WRITER_BUSY` antes de aceptar trabajo fiscal.

Esto no coordina sistemas externos que llamen a ARCA usando el mismo PV. El PV administrado debe considerarse dedicado; una revalidación inmediatamente antes del envío detecta drift y bloquea fail-closed.

No se declara soporte multi-host mediante NFS/directorio compartido.

## Idempotencia y outcomes

La `idempotencyKey` se hashea y namespacia por `consumerId + contextId`; nunca se persiste en claro.

Estados internos:

```text
Created
NumberAssigned
Submitting
Authorized
FiscalRejected
Uncertain
```

Outcomes de contrato:

- `InvalidRequest` — fallo determinístico antes del side effect.
- `FailedBeforeSubmission` — no hubo envío comprobado, aunque puede existir una reserva según fase.
- `FiscalRejected` — rechazo fiscal terminal.
- `Uncertain` — el envío pudo ocurrir o falta evidencia.
- `Authorized` — autorización terminal.
- `RecoveredSuccess` — operación previamente ambigua confirmada por lectura fiscal equivalente.

Una consulta por número/CAE no basta para `RecoveredSuccess`: debe coincidir con la evidencia fiscal persistida. Mismatch o evidencia insuficiente mantienen `Uncertain` y la reserva.

## Recuperación de operación

Ante timeout/respuesta perdida:

```text
emitir
 -> consultar_operacion
 -> reconciliar_operacion si sigue incierta
 -> nunca un segundo FECAESolicitar ciego
```

`operationId` es una referencia opaca durable devuelta por el MCP y puede persistirse en el consumidor.

## Multiemisor y credenciales

Los contextos fiscales se administran server-side. Una assignment candidata sólo puede activarse después de un probe no emisor que autentica contra WSFE y confirma el PV.

Dos contextos pueden compartir la misma serie fiscal si representan el mismo ambiente/CUIT/PV/tipo; eso no les da los mismos grants ni el mismo namespace idempotente.

Cambiar CUIT/titular fiscal requiere otro contexto. Cambiar sólo la credencial que representa al mismo emisor crea otra `assignmentRevision`.

## PDF

El PDF es un side effect documental posterior al fiscal. El snapshot fiscal V2 diferencia procedencia/completitud y nunca rellena silenciosamente evidencia ausente con valores inventados.

Caso válido:

```text
Fiscal = Authorized
PDF = Failed
```

La factura ya existe. Un retry con la misma operación recupera la misma autorización y reintenta sólo el render. `generar_pdf_comprobante` consulta ARCA y nunca emite.

`templateData` es presentación; el bloque `fiscal` y aliases fiscales están reservados por el servidor.

## Persistencia

Configurar almacenamiento durable para:

```text
ApiKeys__Directory
EmissionIdempotency__Directory
FiscalContexts__Directory
Recovery__Directory
```

Ver `MCP_CONFIGURATION.md`, `API_KEYS_RUNBOOK.md` y `OPERATIONS_RECOVERY.md`.

## Restore fail-closed

El restore soportado de un backup del store fiscal crea un recovery gate separado. Si el backup es anterior a autorizaciones posteriores, el proceso puede estar vivo pero:

- `/health/ready` devuelve 503 `RESTORE_RECONCILIATION_REQUIRED`;
- nuevas emisiones se bloquean antes de resolver credenciales/crear operaciones;
- consulta, diagnóstico y reconciliación siguen disponibles;
- sólo una completion explícita con referencia de evidencia levanta el gate.

Copiar archivos antiguos directamente sobre el store no es un restore soportado.

## Health

- `GET /health/live`: proceso vivo.
- `GET /health/ready`: listo para nuevas emisiones; puede ser 503 durante recovery.

No llaman a ARCA ni revelan secretos/CUIT.

## Docker

```bash
docker compose config -q
docker build -f dcArca.McpServer/Dockerfile -t dcarca-mcpserver .
```

La imagen también contiene `/tools/dcArca.Cli.dll` para operaciones administrativas explícitas como gestión de API keys. El proceso normal sigue arrancando únicamente `dcArca.McpServer.dll`.

El compose genérico no configura TLS/proxy/red privada. En Cadencia, TLS se termina mediante Traefik y el contenedor queda en redes privadas.

## Seguridad

- PFX/password/API keys/token-sign fuera del repositorio y del payload MCP.
- mínimo privilegio en scopes + grants.
- secretos visibles una sola vez y hashes en persistencia de keys.
- no registrar idempotency key en claro ni payload fiscal/SOAP completo.
- no usar una assignment nueva como fallback de una operación histórica.
- no liberar reservas inciertas para “destrabar” numeración.
- no interpretar corrupción/schema desconocido como store vacío.

## Referencias

- `SECRETARIA_HANDOFF.md`
- `EPIC14_ACCEPTANCE.md`
- `OPERATIONS_RECOVERY.md`
- `API_KEYS_RUNBOOK.md`
- `MCP_AUTHORIZATION.md`
- `MCP_FISCAL_CONTEXTS.md`
- `MCP_NUMBERING.md`
- `MCP_CONFIGURATION.md`
- `MCP_IDEMPOTENCY.md`
- `CREADORPDF_INTEGRATION.md`
- `WSFE_RECONCILIATION.md`
- `EMISSION_STORE_MIGRATION.md`
