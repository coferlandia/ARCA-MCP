# Autorización MCP — contrato V2

ARCA-MCP autentica mediante `Authorization: Bearer sk-arca-...` y un hash persistido en el host. No ofrece OAuth de usuario. El consumidor es un `consumerId` estable, independiente del secreto y de la rotación de la API key.

## Capas de autorización

1. Scope general de la key: `arca:consultar` (lectura, PDF, diagnóstico, reconciliación), `arca:facturar` (validar, emitir), o `arca:administrar` (provisionamiento y revocación).
2. Grant explícito para el contexto y la operación: `operador-homo:consultar`, `operador-homo:facturar` o `operador-homo:administrar`.
3. Representación `Active` registrada para ese `consumerId + contextId + representedCuit + pointOfSale`. Un grant de contexto no permite por implicancia todos los representados del mismo certificado.

`arca:facturar` no implica `arca:consultar` ni `arca:administrar`. Las tools administrativas requieren scope administrativo distinto y verifican en el servidor su grant. Una key legacy sin `consumerId` es incompatible con V2 (`LEGACY_KEY_UNSUPPORTED_V2`) y debe reaprovisionarse.

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

Los errores devuelven códigos seguros y nunca rutas secretas, passwords o Token/Sign. `WSFE_600`, `WSFE_601` y `WSFE_602` permanecen distintos; 602 no demuestra por sí solo una revocación de delegación.
