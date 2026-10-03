# Operación, backup, restore y recuperación

Este runbook describe la topología soportada por ARCA-MCP V1. No habilita producción ni sustituye la homologación fiscal.

## Topología soportada

- un único proceso escritor por store de emisiones;
- almacenamiento local durable para operaciones/reservas, contextos fiscales, API keys y recovery state;
- punto de venta dedicado al servicio; escritores externos sobre la misma serie no participan del lock local;
- `FileSystemFiscalSeriesCoordinator` mantiene un lease exclusivo `writer.lock` y reservas por serie;
- el host mantiene además un runtime lease en `Recovery__Directory`; restore toma ese lease en modo exclusivo para impedir carrera con startup/live host;
- no se declara soporte multi-host mediante un directorio compartido/NFS;
- TLS se termina en el reverse proxy/host; el contenedor escucha HTTP interno;
- certificados, passwords y API keys permanecen fuera del repositorio.

Rutas Docker recomendadas:

```text
/data/emission-idempotency   operaciones + reservas
/data/fiscal-contexts        catálogo server-owned de contextos/assignments
/data/recovery               gate, maintenance lease e historial de restores
/data                        store de API keys en la topología legacy actual
```

`Recovery__Directory` debe estar fuera de `EmissionIdempotency__Directory`. Un restore del store de emisiones no debe sobrescribir el recovery state.

## Health

- `/health/live`: proceso HTTP vivo; no llama a ARCA.
- `/health/ready`: capacidad local para aceptar nuevas emisiones. Devuelve `503 RESTORE_RECONCILIATION_REQUIRED` mientras exista una ventana de restore pendiente.

Durante ese bloqueo siguen permitidas las herramientas de consulta, diagnóstico y reconciliación autorizadas. Las rutas de emisión fallan antes de materializar credenciales o crear una operación nueva.

## Observabilidad segura

El host emite eventos estructurados mínimos para operación:

- transición/estado/outcome de la operación fiscal;
- reserva activa, liberada o bloqueada;
- errores de store/coordinación por tipo de excepción;
- estado/código del render PDF;
- ambiente y tipo de comprobante cuando son necesarios para diagnóstico.

Para correlación se usan `OperationRef` y `ContextRef` opacos/hash truncados. Los eventos **no** incluyen:

- CUIT del emisor o receptor;
- CAE;
- número de documento/receptor;
- `idempotencyKey` en claro;
- rutas/passwords de PFX;
- token/sign WSAA;
- request fiscal, templateData, XML/SOAP o PDF.

Los logs no sustituyen el store durable ni son fuente de verdad para liberar una reserva o completar un restore.

## Backup consistente del store de emisiones

Detener primero el escritor. El comando verifica que puede obtener el lease exclusivo y falla con `SERIES_WRITER_BUSY` si otro proceso continúa activo.

```bash
dotnet dcArca.McpServer.dll backup-emission-store \
  --directory /data/emission-idempotency \
  --backup /backups/dcarca/emission-20261003T120000
```

El backup contiene un `backup-manifest.json` con id, timestamp, conteos y SHA-256 de cada archivo durable. Locks y temporales no se copian.

El manifest provee integridad contra corrupción/modificación accidental del contenido respecto del manifest; no es una firma criptográfica de autenticidad. La protección del repositorio de backups, acceso y retención pertenece al host.

El catálogo `/data/fiscal-contexts`, las API keys y la configuración/secret references se respaldan por separado con el servicio detenido o mediante un snapshot consistente del host. No copiar PFX/passwords a GitHub ni a los manifests del store fiscal.

## Restore soportado

Restaurar siempre con el escritor detenido:

```bash
dotnet dcArca.McpServer.dll restore-emission-store \
  --backup /backups/dcarca/emission-20261003T120000 \
  --directory /data/emission-idempotency \
  --recovery-directory /data/recovery
```

El comando:

1. toma el maintenance lease exclusivo de `/data/recovery`; si el host está vivo falla con `RECOVERY_RUNTIME_ACTIVE`;
2. valida manifest, tamaño y SHA-256;
3. demuestra que no hay writer activo y conserva ese lock durante la preparación del staging;
4. crea un recovery gate fuera del store restaurado;
5. copia a staging y valida operaciones/reservas;
6. libera el writer lock sólo en la ventana mínima necesaria para el rename; el maintenance lease sigue impidiendo arrancar un host actual;
7. intercambia el directorio por rename;
8. conserva el store anterior como rollback local `*.pre-restore-<restoreId>` cuando existía.

Si falla luego de crear el gate, el gate permanece bloqueado deliberadamente hasta que un operador determine qué directorio quedó activo.

Copiar un backup antiguo directamente sobre `/data/emission-idempotency` y arrancar el servicio no es un restore soportado: el sistema no puede inferir sólo desde archivos antiguos qué autorizaciones ocurrieron después de ese backup.

## Ventana no cubierta después del restore

Un backup tomado en T0 puede restaurarse después de que hubo autorizaciones T1..Tn. Esas operaciones pueden existir en ARCA y en el ledger comercial del consumidor pero faltar localmente.

Mientras el recovery gate esté activo:

- no emitir con keys nuevas;
- no cambiar una idempotency key para “destrabar” una operación;
- no borrar registros/reservas;
- no usar una credencial nueva como fallback para un pendiente histórico;
- no intentar saltar numeración con la tool low-level deshabilitada.

Reconciliar la ventana `[backupCreatedAt, restore]` usando evidencia independiente:

1. ledger/intención durable del consumidor (por ejemplo SecretarIA);
2. operaciones que sí quedaron en el backup;
3. consultas oficiales ARCA por contexto, tipo/PV/número cuando corresponda;
4. `consultar_operacion` / `reconciliar_operacion` para operaciones conocidas;
5. último autorizado por cada serie para detectar avance posterior al backup.

Si falta evidencia suficiente, el gate debe permanecer activo y el caso escalarse a revisión manual.

Consultar estado:

```bash
dotnet dcArca.McpServer.dll emission-recovery-status \
  --recovery-directory /data/recovery
```

Una vez documentada la ventana reconciliada:

```bash
dotnet dcArca.McpServer.dll complete-emission-restore \
  --recovery-directory /data/recovery \
  --evidence-reference OPS-1234 \
  --confirmed-by diego \
  --note "Ventana contrastada contra ledger del consumidor y consultas ARCA"
```

`--evidence-reference` es obligatorio. El completion se guarda bajo `recovery/history`; no debe contener CUIT, CAE, payload fiscal, secretos ni PII.

La completion es crash-idempotent: si el historial quedó persistido y el proceso murió antes de borrar el gate, repetir el comando conserva la evidencia original y termina de levantar el bloqueo. Un historial corrupto mantiene el gate fail-closed.

## Operación `Uncertain` / mismatch

Ante `Uncertain`, `RECONCILIATION_MISMATCH` o `RECONCILIATION_EVIDENCE_INSUFFICIENT`:

1. conservar operación, key, número y assignment histórica;
2. consultar/reconciliar la misma operación;
3. registrar evidencia y resultado de ARCA;
4. mantener bloqueada la serie si no se demuestra equivalencia;
5. escalar manualmente cuando la credencial histórica esté revocada/expirada o la evidencia sea insuficiente.

Acciones prohibidas como recuperación automática:

- borrar el JSON idempotente;
- cambiar la key para obtener otro número;
- reemitir “porque la consulta no apareció todavía”;
- cambiar de certificado/assignment en una operación existente;
- liberar una reserva incierta para permitir la siguiente factura.

## Migración de certificado personal a empresarial

Seguir `MCP_FISCAL_CONTEXTS.md`:

1. crear credencial candidata opaca;
2. validar autorización/PV con probe no emisor;
3. activar una revisión explícita sólo para los tenants autorizados;
4. las operaciones nuevas capturan esa revisión;
5. retries/pending usan la revisión histórica original;
6. pendientes del certificado anterior se resuelven antes de retirarlo;
7. migrar a otro titular/CUIT crea otra identidad fiscal; no hereda autorizaciones ni namespace.

## Rollback de deployment

El rollback binario sólo es seguro si la versión anterior comprende los schemas persistidos actuales. No arrancar una versión vieja sobre un schema desconocido ni interpretar el store como vacío.

Antes de rollback:

- detener writer;
- confirmar compatibilidad de schema;
- conservar recovery state y stores;
- no restaurar un backup antiguo para “volver de versión” sin ejecutar el procedimiento de restore/reconciliación anterior.

## Evidencia operativa

Para cada ensayo registrar, sin secretos/PII:

- SHA exacto de ARCA-MCP e imagen;
- ambiente;
- timestamp de backup/restore;
- backupId/restoreId;
- resultado de CI/smoke;
- referencia externa de reconciliación/homologación;
- limitaciones o escenarios no ejecutados.
