# Configuración y operabilidad de dcArca.McpServer

## Health

El servidor expone dos señales livianas que no llaman a ARCA:

- `GET /health/live`: confirma que el proceso HTTP está levantado.
- `GET /health/ready`: confirma que el proceso completó el arranque con la configuración esencial válida.

Las respuestas sólo contienen `status`; no incluyen CUIT, certificado, rutas, token/sign ni passwords.

## Configuración requerida

Se requiere:

- `ApiKeys:Directory` / `ApiKeys__Directory`;
- `dcArcaConfig:Cuit`;
- `dcArcaConfig:CertificatePath`;
- `dcArcaConfig:CertificatePassword` cuando el PFX lo requiera;
- `dcArcaConfig:WsaaUrl`;
- `dcArcaConfig:WsfeUrl`;
- `dcArcaConfig:PadronUrl`;
- `dcArcaConfig:PuntoVenta`.
- `Pdf:BaseUrl` / `Pdf__BaseUrl` y `Pdf:ApiKey` / `Pdf__ApiKey` cuando se use creadorpdf.

El certificado y sus secretos deben provenir del host/secret manager y no del repositorio.

El directorio de API keys y el certificado necesitan almacenamiento persistente. `Pdf:ApiKey` debe provenir del secret manager; nunca debe escribirse en el repositorio ni exponerse a SecretarIA.
