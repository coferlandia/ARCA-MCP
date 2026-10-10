# Contextos técnicos y representaciones fiscales — contrato V2

**Contrato de ARCA-MCP 2.0.** El ContextId ya no identifica un CUIT ni un punto de venta. Un contexto técnico identifica una credencial de operador (con revisión) y el ambiente fiscal. Su identidad es `contextId + environment`. La identidad fiscal y los permisos se registran aparte.

## Modelo y estados

- `FiscalTechnicalContextRecord`: `contextId`, `environment`, `contextRevision`, `operationalState`, assignments de `credentialId` con `assignmentRevision` histórica.
- `FiscalRepresentationRecord`: `contextId`, `consumerId`, `representedCuit`, `pointOfSale`, `status`, evidencia de `FEParamGetPtosVenta`, actores y fechas de alta, verificación, activación y revocación.
- Los estados son `Pending`, `Verified`, `Active`, `Revoked` y `ActionRequired`. La selección inicial tiene `pointOfSale=0`; **nunca** autoriza emitir o consultar.
- Las credenciales son server-owned mediante `FiscalCredentials:{credentialId}` y `FiscalEnvironments:{environment}`. No se devuelven PFX, passwords, Token/Sign, rutas secretas ni endpoints configurables.
- WSAA autentica al operador técnico; cada request WSFE configura `Auth.Cuit` y `PtoVta` para **la representación concreta**. El material de autenticación y configuración se crea por operación; nunca se muta globalmente.

## Persistencia

`FiscalContexts:Directory/fiscal-catalog-v2.json` almacena el catálogo técnico y las representaciones con `schemaVersion=2` y escrituras atómicas bajo lock de archivo. No se migra ni reutiliza `contexts.json` V1: su presencia genera `FISCAL_CATALOG_V1_RESET_REQUIRED` y evita el arranque del host. El reset se hace sólo en el procedimiento de cutover controlado, **nunca** durante un upgrade automático.

Una representación activa conserva evidencia remota vinculada al `assignmentRevision` y `environment`. Si rota la credencial técnica, las representaciones previamente activas quedan **bloqueadas para nuevas operaciones** con `FISCAL_REPRESENTATION_REVERIFY_REQUIRED` hasta volver a verificar remotamente el CUIT/PV contra la nueva revisión; no se heredan delegaciones por implicancia. La administración permite revalidar una representación activa mediante el mismo flujo de probe y activación sin duplicar identidad.

Una misma revisión activa de credencial puede autenticar CUIT/PV diferentes, pero los grants de contexto y la representación activa asociada al `consumerId` son controles independientes. Un grant de contexto sin representación activa no habilita ninguna consulta/emisión. Las revocaciones afectan nuevas operaciones; el snapshot de intentos fiscales conserva CUIT/PV/ambiente/revisión para tratamiento explícito de incertidumbre. Cuando un rechazo WSFE muestra explícitamente `600` o `601`, la representación se marca `ActionRequired` y se bloquea su uso hasta revalidación administrativa remota. `602` conserva su semántica de ausencia de datos y no implica automáticamente revocación de la delegación.

## Provisionamiento

Con el host detenido o en mantenimiento, crear primero contexto y assignment técnico:

```bash
dcArca.Cli add-context --id operador-homo --environment homologacion
dcArca.Cli add-assignment --context operador-homo --revision v1 --credential certificado-operador --actor admin
dcArca.Cli validate-assignment --context operador-homo --revision v1 --actor admin
dcArca.Cli activate-assignment --context operador-homo --revision v1 --actor admin
dcArca.Cli set-context-state --context operador-homo --state Active --actor admin
```

Registrar el CUIT candidato, descubrir los PV sin emitir, seleccionar uno y verificarlo/activarlo:

```bash
dcArca.Cli register-representation --context operador-homo --consumer secretaria --cuit 20XXXXXXXXX --actor admin
dcArca.Cli list-points-of-sale --context operador-homo --consumer secretaria --cuit 20XXXXXXXXX
dcArca.Cli select-representation-pv --context operador-homo --consumer secretaria --cuit 20XXXXXXXXX --point-of-sale 4 --actor admin
dcArca.Cli activate-representation --context operador-homo --consumer secretaria --cuit 20XXXXXXXXX --point-of-sale 4 --actor admin
```

Los endpoints administrativos MCP cumplen las mismas etapas y exigen `arca:administrar` más grant `{contextId}:administrar`. El listado no autoriza ni activa. La selección manual de un PV está permitida. La activación repite el probe del CUIT/PV elegido: si ARCA devuelve un listado, exige PV CAE sin bloqueo ni baja; ante `WSFE_602` con mensaje explícito `Sin Resultados` permite activar el PV manual con evidencia `MANUAL_PV_UNVERIFIED_602`, que **no constituye validación remota del PV**. Otros errores 602, 600/601, fallos de conexión, bloqueo/baja o modalidad distinta de CAE siguen bloqueando. La autorización fiscal definitiva sigue siendo decisión de WSFE al operar.

## Emisión, aislamiento y recuperación

Los contratos de emisión y consulta aceptan `contextId`, `representedCuit` y `pointOfSale`. Si el consumidor tiene una única representación activa, CUIT/PV pueden inferirse; si tiene varias, especificarlos es obligatorio. Se revalida la representación antes del side effect de CAE, con aislamiento por consumidor.

La identidad durable es `consumerId + contextId + environment + representedCuit + pointOfSale + tipoComprobante` y snapshot de `assignmentRevision`. La clave idempotente incorpora consumidor, contexto, CUIT y PV. La serie fiscal continúa siendo `environment + representedCuit + pointOfSale + tipoComprobante`, incluso si varios contextos comparten identidad fiscal.

Consultar/reconciliar una operación usa siempre la representación de su snapshot histórico. Una revocación local o credencial histórica deshabilitada falla cerrada, requiriendo intervención autorizada si la operación quedó incierta. Nunca se pide un nuevo CAE por una respuesta incierta.

**Cutover:** ver `RUNBOOK.md`; no ejecutar limpieza automática ni conservar JSON V1.
