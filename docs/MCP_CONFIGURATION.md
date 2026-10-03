# Configuración y operabilidad de dcArca.McpServer

## Health

El servidor expone dos señales livianas que no llaman a ARCA:

- `GET /health/live`: confirma que el proceso HTTP está levantado.
- `GET /health/ready`: confirma que el proceso completó el arranque con la configuración esencial válida.

Las respuestas sólo contienen `status`; no incluyen CUIT, certificado, rutas, token/sign ni passwords.

## Persistencia requerida

Se requiere almacenamiento persistente para:

- `ApiKeys:Directory` / `ApiKeys__Directory`;
- `EmissionIdempotency:Directory` / `EmissionIdempotency__Directory`;
- `FiscalContexts:Directory` / `FiscalContexts__Directory`.

El store de contextos contiene sólo identidades fiscales, referencias opacas de credencial, grants/estados administrativos y evidencia no secreta de validación. Los certificados y passwords no se copian al store.

## Compatibilidad single-context

La configuración existente continúa siendo la fuente de bootstrap cuando todavía no existe un catálogo de contextos:

- `FiscalContext:ConsumerId` / `FiscalContext__ConsumerId`: identidad estable del consumidor legacy; no debe ser el id de una API key;
- `FiscalContext:ContextId` / `FiscalContext__ContextId`: identidad estable del contexto legacy;
- `FiscalContext:Environment` / `FiscalContext__Environment`: ambiente fiscal explícito;
- `FiscalContext:ContextRevision` / `FiscalContext__ContextRevision`: revisión positiva del contexto;
- `FiscalContext:CredentialAssignmentRevision` / `FiscalContext__CredentialAssignmentRevision`: revisión histórica inicial de la credencial;
- `dcArcaConfig:Cuit`;
- `dcArcaConfig:CertificatePath`;
- `dcArcaConfig:CertificatePassword` cuando el PFX lo requiera;
- `dcArcaConfig:WsaaUrl`;
- `dcArcaConfig:WsfeUrl`;
- `dcArcaConfig:PadronUrl`;
- `dcArcaConfig:PuntoVenta`.

En el primer arranque sin catálogo, el servidor crea un contexto `LegacyDefault=true` y una asignación activa opaca `legacy-default`. Las API keys antiguas sin `consumerId` ni grants quedan limitadas a ese único contexto.

Una vez que existe `contexts.json`, el servidor no agrega contextos implícitos aunque cambie `dcArcaConfig`. La administración posterior debe ser explícita.

## Credenciales adicionales

Cada credencial nueva se identifica por un id opaco server-owned:

```text
FiscalCredentials__empresa-2026__CertificatePath=/run/secrets/empresa-2026.pfx
FiscalCredentials__empresa-2026__CertificatePassword=...
```

El `credentialId` almacenado en el contexto sería `empresa-2026`. El caller MCP no puede enviar ni modificar esos valores.

El certificado y sus secretos deben provenir del host/secret manager y no del repositorio.

## Ambientes adicionales

Para contextos cuyo ambiente no sea el ambiente legacy se declaran endpoints server-owned:

```text
FiscalEnvironments__produccion__WsaaUrl=https://...
FiscalEnvironments__produccion__WsfeUrl=https://...
FiscalEnvironments__produccion__PadronUrl=https://...
```

Si el nombre de ambiente coincide con `FiscalContext:Environment`, se mantienen como fallback los endpoints de `dcArcaConfig` para compatibilidad.

## Identidades estables

`ConsumerId` y `ContextId` son identidades operativas persistentes. Rotar una API key o cambiar la credencial que representa al mismo CUIT no debe cambiarlos.

Un contexto registrado no puede mutar silenciosamente su ambiente, CUIT representado, punto de venta ni revisión. Cambiar esa identidad requiere crear otro contexto.

La revisión de credencial (`assignmentRevision`) se persiste en cada operación idempotente. Un retry posterior a una rotación de certificado vuelve a resolver esa revisión histórica; nunca usa la credencial activa nueva como fallback.

## Validación de assignments

Una credencial candidata debe validarse antes de activarse. La validación no emite comprobantes: usa el método autenticado `FEParamGetPtosVenta` y exige que ARCA devuelva el PV del contexto sin bloqueo ni fecha de baja.

La activación falla si no existe evidencia validada. Una falla de red, WSAA, WSFE, autorización o PV deja la asignación sin verificar.

El cache WSAA se namespacia por ambiente + `credentialId` + servicio para impedir que dos certificados distintos que representen el mismo CUIT compartan token/sign.

## PDF

`Pdf:BaseUrl` / `Pdf__BaseUrl` y `Pdf:ApiKey` / `Pdf__ApiKey` se requieren cuando se use creadorpdf. `Pdf:ApiKey` debe provenir del secret manager y nunca debe exponerse a SecretarIA.

## Migración del store de emisiones

Antes de arrancar una versión que usa el schema versionado del store sobre datos legacy, ejecutar `docs/EMISSION_STORE_MIGRATION.md`. El servidor falla cerrado ante registros legacy no migrados, versiones desconocidas, reservas incompatibles o contextos incoherentes.

La administración de contextos, grants y migración de certificado está documentada en `docs/MCP_FISCAL_CONTEXTS.md`.
