# Epic #14 — matriz de aceptación integrada

Esta matriz consolida evidencia automatizada de los issues #15–#20 y del hardening operativo agregado en #21. No sustituye la homologación contra ARCA con credenciales autorizadas.

Contrato MCP objetivo: `arca-mcp/1.0`.

## Estado de gates

| Gate | Alcance | Evidencia automatizada | Estado |
| --- | --- | --- | --- |
| #15 | identidad fiscal estable, schema/migración legacy | `EmissionIdempotencyStoreTests`, `EmissionStoreMigrationTests`, `LegacyReconciliationBlockTests` | cubierto |
| #16 | preflight y taxonomía de outcomes | `dcFacturaPreflightValidatorTests`, `EmissionOutcomeSequencerTests` | cubierto |
| #17 | reserva durable, single-writer, crash/reconcile | `FiscalSeriesCoordinatorTests`, `DurableSequencerSafetyTests`, `FiscalReconciliationComparerTests` | cubierto |
| #18 | multiemisor, grants, assignments y rotación | `FiscalContextRuntimeResolverTests`, tests de autorización/contextos/CLI | cubierto |
| #19 | contrato de capacidades/query/reconcile/diagnóstico | `McpOperationContractServiceTests`, `McpOperationRecoveryTransportTests`, `McpContractScopeTests` | cubierto |
| #20 | snapshot fiscal/PDF consistente y replay | `FiscalDocumentSnapshotTests`, `IdempotentInvoicePdfTests`, `ExistingInvoicePdfServiceTests`, `InvoicePdfServiceTests`, `PdfDocumentRendererTests` | cubierto |
| #21 | backup/restore fail-closed, Docker Linux, handoff y harness de homologación | `EmissionStoreBackupRecoveryTests` + CI Docker/smoke/restart + `scripts/homologacion/` | candidato; homologación real pendiente |

La evidencia final debe identificar el SHA o digest exacto del artefacto realmente desplegado. No asumir que el último merge de `main` coincide con el binario/contenedor en ejecución: declararlo mediante `ARCA_MCP_BUILD_SHA` al correr el harness y cotejarlo con el despliegue.

## Escenarios obligatorios

| # | Escenario | Evidencia principal | Resultado esperado |
| ---: | --- | --- | --- |
| 1 | store legacy válido + migración | `EmissionStoreMigrationTests` | inventario/dry-run, backup y conversión determinística; estados preservados |
| 2 | store corrupto o mapping inválido | `EmissionStoreMigrationTests`, `EmissionIdempotencyStoreTests` | fail-closed; nunca tratar corrupción como store vacío |
| 3 | aislamiento consumer/contexto | `FiscalContextRuntimeResolverTests`, `McpScopeAuthorizationTests` | grants obligatorios; sin acceso ni credenciales ajenas |
| 4 | misma idempotency key concurrente | `McpInvoiceSequencerTests` | un único side effect fiscal y mismo resultado |
| 5 | una operación incierta y segunda key | `DurableSequencerSafetyTests` | la reserva permanece y la segunda operación recibe bloqueo |
| 6 | crash entre reserva/envío/restart | `FiscalSeriesCoordinatorTests`, `DurableSequencerSafetyTests` | reserva/NumberAssigned se reparan y no se duplica envío |
| 7 | consulta con CAE pero evidencia distinta/insuficiente | `FiscalReconciliationComparerTests`, `DurableSequencerSafetyTests` | nunca `RecoveredSuccess`; queda `Uncertain` y serie bloqueada |
| 8 | request determinísticamente inválido | `dcFacturaPreflightValidatorTests`, `EmissionOutcomeSequencerTests` | `InvalidRequest`; cero numeración y cero envío |
| 9 | rotación de API key/certificado | `FiscalContextRuntimeResolverTests`, tests de auth/contextos | `consumerId/contextId` estables; retry conserva assignment histórica |
| 10 | segundo writer | `FiscalSeriesCoordinatorTests`, `EmissionStoreBackupRecoveryTests` | `SERIES_WRITER_BUSY`; no compite por numeración/backup |
| 11 | respuesta perdida + query/reconcile | `McpOperationRecoveryTransportTests` | operación durable consultable; reconcile sólo lee; envío total = 1 |
| 12 | PDF falla/retry/regeneración | `IdempotentInvoicePdfTests`, `ExistingInvoicePdfServiceTests` | el CAE no se repite; PDF es derivado regenerable |
| 13 | cambio de configuración/assignment | `FiscalContextRuntimeResolverTests`, `FiscalDocumentSnapshotTests` | operación histórica no se redirige a config/credencial nueva |
| 14 | scopes y context grants de lectura | `McpContractScopeTests`, `McpScopeAuthorizationTests` | tools visibles/ejecutables sólo con permisos correctos |
| 15 | incertidumbre prolongada | `LegacyReconciliationBlockTests`, `DurableSequencerSafetyTests`, `OPERATIONS_RECOVERY.md` | no reemisión automática; intervención/reconcile explícitos |
| 16 | restore de backup anterior a emisiones posteriores | `EmissionStoreBackupRecoveryTests` | readiness 503 + nuevas emisiones bloqueadas hasta evidencia explícita |

## Migración de certificado personal → empresarial

La suite automatizada demuestra las invariantes de software, no la autorización real de ARCA:

- una operación existente conserva `assignmentRevision` histórica;
- una operación nueva usa sólo la assignment activa;
- una assignment histórica deshabilitada no hace fallback silencioso a la nueva;
- el mismo CUIT/PV/tipo comparte serie aunque existan aliases/contextos;
- cambiar de CUIT requiere identidad/contexto fiscal distinto;
- una credencial candidata debe superar probe no emisor antes de activarse.

La prueba real con dos titulares/certificados y autorización efectiva en homologación queda **pendiente** hasta contar con credenciales autorizadas y un caso controlado. Registrar evidencia sin PII ni secretos.

## Docker/operación

El CI del candidato debe completar:

```bash
docker compose config -q
docker build -t dcarca-mcpserver:ci -f dcArca.McpServer/Dockerfile .
```

y un smoke Linux real del contenedor:

1. arranque con configuración de testing montada read-only;
2. `/health/live` y `/health/ready` exitosos;
3. restart del mismo contenedor;
4. health exitoso después del restart.

No se considera evidencia de homologación: sólo valida artefacto y lifecycle Linux.

## Harness de homologación real

El harness versionado está en `scripts/homologacion/epic14_homologacion.py` y su procedimiento en `scripts/homologacion/README.md`.

Principios del harness:

- endpoint, token y SHA/digest del build llegan por variables de entorno;
- configuración fiscal local y reportes quedan ignorados por Git;
- sin `--execute-emission` sólo hace health/capabilities/diagnóstico/preflight;
- emisión, nota asociada y rechazo fiscal tienen opt-ins explícitos;
- el replay reutiliza la misma idempotency key y comprueba mismo número/CAE/operationId cuando está disponible;
- el rechazo sólo cuenta como evidencia si `EmissionOutcome=FiscalRejected`;
- el escenario PDF conserva `status/errorCode`, redacta el blob y comprueba que el CAE fiscal permanezca igual;
- CUIT, CAE, token, idempotency key, paths/referencias sensibles y contenido PDF se redactan antes de persistir evidencia;
- el máximo estado automático es `CANDIDATE_COMPLETE_REQUIRES_HUMAN_REVIEW`.

El reporte del harness no cierra issues automáticamente. Debe revisarse antes de resumirlo/adjuntarlo a #21.

## Criterios de operación

Antes de habilitar un ambiente fiscal:

- store persistente montado y protegido;
- `FiscalContexts__Directory` y `Recovery__Directory` persistentes;
- un solo writer por store y PV administrado;
- TLS terminado por reverse proxy/host;
- API key con scopes + grants mínimos;
- certificado fuera del repositorio y assignment validada;
- backup/restore ensayado con `OPERATIONS_RECOVERY.md`;
- recovery gate sin bloqueo;
- CI del SHA desplegado verde;
- homologación real documentada para el contexto que se habilita.

## Homologación pendiente

No hay en esta ejecución acceso autorizado a credenciales ARCA de homologación que permita producir evidencia fiscal real. Por lo tanto:

- no se afirma que el gate de homologación esté aprobado;
- #21 no debe cerrarse automáticamente sólo por mergear el harness;
- la ejecución real debe registrar fecha, ambiente, SHA/imagen, contexto anonimizado, tipo de comprobante, resultado y referencia de evidencia;
- nunca commitear CUIT reales, CAE, certificados, passwords, API keys, token/sign, XML/SOAP ni payloads con PII.
