# Contrato MCP de operaciones y recuperación — V2

Versión: `arca-mcp/2.0`. Es un cambio deliberado frente a 1.0 y **no** existe migración automática de datos persistidos del modelo anterior.

## Selección fiscal

Las tools ordinarias de consulta, prevalidación, emisión, PDF, diagnóstico y recuperación admiten `contextId`, `representedCuit` (CUIT del **emisor**, no del receptor) y `pointOfSale`. Ambas propiedades de representación deben indicarse juntas cuando el consumidor tiene más de una representación activa. Si no, se devuelve `FISCAL_REPRESENTATION_REQUIRED`; una selección CUIT/PV no registrada para el `consumerId` falla con `FISCAL_REPRESENTATION_FORBIDDEN`.

`obtener_capacidades_fiscales` devuelve sólo capacidades implementadas por el servidor y autorizadas para esa representación. `remoteFiscalEnablement=not-verified-use-diagnostic` sigue sin afirmar autorización remota actual.

`validar_comprobante` aplica preflight determinístico sin reservar número, crear operación o solicitar CAE. La validación local no garantiza que ARCA autorice la emisión posterior.

`diagnosticar_contexto_fiscal` comprueba certificado y credencial, y efectúa un probe no emisor del CUIT/PV seleccionado. Diferencia `WSFE_600` (token/firma/autorización técnica), `WSFE_601` (representado no incluido en token) y `WSFE_602` (sin datos en registros de ese ambiente).

## Idempotencia e identidad

El `operationId` es un hash opaco del namespace `consumerId+contextId+representedCuit+pointOfSale+idempotencyKey`. Cambiar cualquiera de esos elementos nunca recupera el resultado de otra representación. La clave en claro no se persiste. El request fiscal se compara por fingerprint; mismo namespace/key con datos distintos falla.

La reserva de numeración queda por `(ambiente, CUIT representado, PV, tipo)` y no se fragmenta por consumer. El registro durable conserva ambiente, CUIT, PV, tipo, `contextId`, `consumerId`, `assignmentRevision` y, para operaciones V2 nuevas, la provenance exacta del registro de representación que admitió el request (alta + primera activación). Esa provenance queda congelada antes de persistir la operación y se revalida en retries/recuperación y en el último boundary previo a CAE; una representación sustituta no puede heredar trabajo durable del registro anterior.

`consultar_operacion` acepta un `operationId` o la `idempotencyKey` junto con la representación autorizada. `reconciliar_operacion` no emite ni reserva números: en `Submitting`/`Uncertain` consulta el número histórico con `FECompConsultar`, compara evidencia y recupera sólo ante equivalencia estricta. No se usa `FECAESolicitar` durante reconciliación.

Los estados `Authorized`, `FiscalRejected`, `Created`, `NumberAssigned`, `Submitting`, `Uncertain` y `AbandonedBeforeSubmission` retienen las reglas documentadas en `docs/MCP_IDEMPOTENCY.md` y `docs/WSFE_RECONCILIATION.md`. `AbandonedBeforeSubmission` es terminal y demuestra únicamente que la operación fue cerrada antes de persistir `Submitting`; conserva número si ya había sido reservado, consume definitivamente la idempotency key y no equivale a un rechazo fiscal. El PDF es posterior al CAE; un fallo documental no causa reemisión.

## Administración y descubrimiento

`listar_puntos_venta` opera sobre una representación candidata y ejecuta WSFE `FEParamGetPtosVenta` con `Auth.Cuit=representedCuit`; devuelve el listado y elegibilidad `EligibleForCae`. No crea ni modifica PV ni emite factura. Los comandos MCP de alta, selección, activación y revocación son administrativos y requieren `arca:administrar` más grant administrativo de contexto.

No se exponen PFX, passwords, rutas privadas, WSAA Token/Sign ni endpoints configurables. El cache WSAA es por ambiente, credencial y servicio, independiente del representado. Las instancias de cliente por operación no comparten mutable CUIT/PV.

`/health/live` y `/health/ready` son locales y no llaman ARCA. Smoke real de homologación requiere ejecución controlada separada; una CI verde no prueba autorización fiscal real.
