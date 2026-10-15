# Autorización MCP — contrato V2

ARCA-MCP autentica mediante `Authorization: Bearer sk-arca-...` y un hash persistido en el host. No ofrece OAuth de usuario. El consumidor es un `consumerId` estable, independiente del secreto y de la rotación de la API key.

## Capas de autorización

1. Scope general de la key: `arca:consultar` (lectura, PDF, diagnóstico, reconciliación), `arca:facturar` (validar, emitir), o `arca:administrar` (provisionamiento, revocación y recuperación administrativa pre-submission).
2. Grant explícito para el contexto y la operación: `operador-homo:consultar`, `operador-homo:facturar` o `operador-homo:administrar`.
3. Para operaciones fiscales ligadas a tenant, representación `Active` registrada para ese `consumerId + contextId + representedCuit + pointOfSale`. Un grant de contexto no permite por implicancia todos los representados del mismo certificado. La excepción intencional es `consultar_padron_administrativo`: opera sobre la credencial técnica del contexto, exige scope+grant `administrar` y no requiere representación/PV.

`arca:facturar` no implica `arca:consultar` ni `arca:administrar`. Las tools administrativas requieren scope administrativo distinto y verifican en el servidor su grant. `consultar_padron_administrativo(contextId, cuit)` sólo acepta el contexto y el CUIT objetivo; el `PrincipalCuit` que viaja como `cuitRepresentada` a Padrón se resuelve server-side desde la credencial y no puede ser sobrescrito por el caller. Una key legacy sin `consumerId` es incompatible con V2 (`LEGACY_KEY_UNSUPPORTED_V2`) y debe reaprovisionarse.

Las keys se guardan con hash SHA-256 (no el secreto en claro) en `ApiKeys:Directory/api_keys.json`. Se crean/revocan con `dcArca.Cli` o `scripts/api-keys/manage-key.sh` sin editar el JSON a mano. Los operadores deben proteger el acceso local al CLI y a los stores. Nunca incluir secrets en Issues o PR.

## Ejemplos

```bash
dcArca.Cli create-key --name secretaria --consumer secretaria --scope arca:consultar,arca:facturar --grant operador-homo:consultar,operador-homo:facturar
dcArca.Cli create-key --name fiscal-admin --consumer fiscal-admin --scope arca:administrar --grant operador-homo:administrar
```

El backend de SecretarIA controla tenant→CUIT/PV, aceptación humana y estado READY; no existe un tenant comercial dentro de ARCA-MCP. Los CUIT/PV autorizados por representación se almacenan por `consumerId`. La selección del CUIT en requests ordinarios no crea permisos nuevos.

## Herramientas administrativas

- `registrar_representacion_fiscal(contextId,consumerId,representedCuit)`: crea candidato, no habilita.
- `listar_puntos_venta(contextId,consumerId,representedCuit)`: consulta remota NO emisora después de registrar candidato.
- `seleccionar_punto_venta(contextId,consumerId,representedCuit,pointOfSale)`: conserva estado pendiente.
- `activar_representacion_fiscal(contextId,consumerId,representedCuit,pointOfSale)`: revalida PV ante ARCA y activa con evidencia.
- `revocar_representacion_fiscal(contextId,consumerId,representedCuit,pointOfSale)`: revocación local inmediata.
- `abandonar_operacion_pre_submission(operationId,reasonCode)`: cierra de forma auditada una operación durable que todavía está en `Created` o `NumberAssigned`; no acepta `idempotencyKey`.

El abandono administrativo de operaciones es deliberadamente más restrictivo que el provisionamiento de representaciones: además de `arca:administrar` y el grant `contextId:administrar` del contexto durable, la key administrativa debe tener el mismo `consumerId` que la operación. Un `operationId` de otro namespace se trata como inexistente. `Submitting` y `Uncertain` nunca pueden abandonarse por esta vía: deben reconciliarse. La razón no es texto libre: sólo se aceptan los códigos server-owned `manual_recovery`, `operator_cancelled`, `failed_before_submission` o `stale_pre_submission`. Cualquier otro valor —aunque tenga formato machine-safe— se rechaza antes de persistir o loguear. El actor se deriva de la API key autenticada y no puede ser inyectado por el caller.

Los errores devuelven códigos seguros y nunca rutas secretas, passwords o Token/Sign. `WSFE_600`, `WSFE_601` y `WSFE_602` permanecen distintos; 602 no demuestra por sí solo una revocación de delegación.
