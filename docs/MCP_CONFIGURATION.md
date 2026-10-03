# Configuración y operabilidad de dcArca.McpServer

## Health

El servidor expone dos señales livianas que no llaman a ARCA:

- `GET /health/live`: confirma que el proceso HTTP está levantado.
- `GET /health/ready`: confirma que el proceso completó el arranque con la configuración esencial válida.

Las respuestas sólo contienen `status`; no incluyen CUIT, certificado, rutas, token/sign ni passwords.

## Configuración requerida

Se requiere:

- `ApiKeys:Directory` / `ApiKeys__Directory`;
- `EmissionIdempotency:Directory` / `EmissionIdempotency__Directory`;
- `FiscalContext:ConsumerId` / `FiscalContext__ConsumerId`: identidad estable del consumidor en modo single-context; no debe ser el id de una API key;
- `FiscalContext:ContextId` / `FiscalContext__ContextId`: identidad estable del contexto fiscal;
- `FiscalContext:Environment` / `FiscalContext__Environment`: ambiente fiscal explícito, por ejemplo `homologacion` o `produccion`;
- `FiscalContext:ContextRevision` / `FiscalContext__ContextRevision`: revisión positiva del contexto; por defecto `1`;
- `FiscalContext:CredentialAssignmentRevision` / `FiscalContext__CredentialAssignmentRevision`: revisión de la credencial del operador usada para operaciones nuevas;
- `dcArcaConfig:Cuit`;
- `dcArcaConfig:CertificatePath`;
- `dcArcaConfig:CertificatePassword` cuando el PFX lo requiera;
- `dcArcaConfig:WsaaUrl`;
- `dcArcaConfig:WsfeUrl`;
- `dcArcaConfig:PadronUrl`;
- `dcArcaConfig:PuntoVenta`;
- `Pdf:BaseUrl` / `Pdf__BaseUrl` y `Pdf:ApiKey` / `Pdf__ApiKey` cuando se use creadorpdf.

`ConsumerId` y `ContextId` son identidades operativas persistentes: rotar una API key o cambiar la credencial que representa al mismo CUIT no debe cambiarlos. Un `ContextId` ya registrado no puede reutilizarse para otro ambiente, CUIT o punto de venta. Cambiar el CUIT emisor requiere un contexto fiscal distinto.

El certificado y sus secretos deben provenir del host/secret manager y no del repositorio. `CredentialAssignmentRevision` es sólo una referencia de auditoría; nunca contiene la ruta del PFX, password ni material del certificado.

El directorio de API keys, el store de idempotencia y el certificado necesitan almacenamiento persistente. `Pdf:ApiKey` debe provenir del secret manager; nunca debe escribirse en el repositorio ni exponerse a SecretarIA.

Antes de arrancar una versión que usa el schema versionado del store sobre datos legacy, ejecutar el procedimiento de `docs/EMISSION_STORE_MIGRATION.md`. El servidor falla cerrado ante registros legacy no migrados, versiones desconocidas o contextos incompatibles.
