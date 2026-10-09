# Runbook

## Cutover controlado de contextos fiscales V1 → V2 (#62)

**No ejecutado por este PR.** El titular autorizó descartar el **estado actual** únicamente, condicionado a verificar que no existan emisiones reales a preservar. Se requiere aprobación operacional independiente inmediatamente antes del cambio.

1. Coordinar versión de contrato V2 con cada consumidor y detener tráfico, HTTP MCP y **todo writer** que comparta el filesystem. Confirmar sin operaciones fiscales reales a preservar ni pendientes inciertos (`Submitting`/`Uncertain`). Nunca resetear un store que contenga actividad fiscal real.
2. Inventariar las rutas efectivas del host, sin publicar nombres internos ni secretos: `FiscalContexts:Directory` (`contexts.json`, `fiscal-catalog-v2.json`), `ApiKeys:Directory` (`api_keys.json`), `EmissionIdempotency:Directory` (operaciones `*.json`, `.contexts/`, reservas de serie), `Recovery:Directory` (evidencia/recovery), caché WSAA y respaldos locales. Confirmar que no haya otros procesos usando los directorios.
3. Realizar backup coherente **fuera** del repositorio público; almacenar en destino privado con accesos restringidos. No incluir material del PFX/password, secrets del host ni tokens/sign en logs o Issues. Mantener intactos certificados, PFX y credenciales de host.
4. Con servicios detenidos y verificación humana de alcance, eliminar/reinicializar **sólo** los stores de datos operativos autorizados (contextos V1, API keys/grants, operaciones idempotentes/series/recovery antiguos). No ejecutar una limpieza automática al iniciar ni tratar los archivos JSON uno por uno con writers vivos. No mover el archivo `contexts.json` como migración implícita: V2 lo rechaza con `FISCAL_CATALOG_V1_RESET_REQUIRED`.
5. Configurar `FiscalCredentials:{credentialId}:CertificatePath/CertificatePassword` y `FiscalEnvironments:{environment}:WsaaUrl/WsfeUrl/PadronUrl` desde secretos del host. Crear contexto técnico, asignación validada con WSAA, estado Active, API keys/grants por consumidor y representaciones candidatas → PV descubierto → revalidación remota → Active; ver `docs/MCP_FISCAL_CONTEXTS.md` y `docs/MCP_AUTHORIZATION.md`.
6. Arrancar un solo writer; validar `/health/live`, `/health/ready`, scopes y denegaciones cruzadas antes de habilitar tráfico. Realizar smoke de homologación con aprobación operativa, comprobar `Auth.Cuit`, PV, errores 600/601/602, idempotencia, reconciliación y PDF. **No afirmar smoke real si sólo se corrió CI.**
7. Si no se completa el cutover, detener nuevamente el tráfico y hacer rollback por redeploy + reaprovisionamiento consistente sobre stores V2 vacíos, sin reintroducir registros V1 parcialmente transformados ni automatizar repetición de comprobantes. Documentar el incidente y comprobar cualquier operación incierta contra ARCA antes de volver a emitir.


## Proposito

Procedimientos repetibles para compilar, validar y operar dcARCA/ARCA-MCP sin incorporar secretos ni topología privada al repositorio público. Los contratos técnicos detallados siguen viviendo bajo `docs/`; este runbook concentra el camino operativo seguro.

## Entornos

- Homologación y producción de ARCA usan certificados y endpoints distintos. No mezclar credenciales/endpoints entre ambientes.
- El repositorio público contiene configuración genérica y ejemplos; la configuración productiva real pertenece al host.
- `dcArca.McpServer` expone transporte MCP HTTP stateless, pero conserva estado local mínimo para API keys, contextos fiscales e idempotencia/numeración.

## Requisitos

- .NET 10 SDK para compilar/ejecutar los proyectos principales.
- .NET 8 SDK disponible para reproducir exactamente el entorno CI actual.
- Certificado ARCA/PFX válido y servicios autorizados en el ambiente objetivo.
- Punto de venta habilitado para WSFE.
- Para PDF: endpoint y API key de `creadorpdf` provistos externamente.

## Configuracion

Partir de `dcArca.McpServer/appsettings.example.json` y preferir variables de entorno/secret manager para secretos.

Variables operativas principales:

```text
ApiKeys__Directory=/data
EmissionIdempotency__Directory=/data/emission-idempotency
Pdf__BaseUrl=http://creadorpdf:8080
Pdf__ApiKey=...
dcArcaConfig__Cuit=...
dcArcaConfig__CertificatePath=/certs/certificado.pfx
dcArcaConfig__CertificatePassword=...
dcArcaConfig__WsaaUrl=...
dcArcaConfig__WsfeUrl=...
dcArcaConfig__PadronUrl=...
dcArcaConfig__PuntoVenta=1
```

Nunca commitear PFX, private keys, passwords, API keys ni `token/sign` de WSAA.

## Ejecucion local

Aplicación de prueba:

```bash
dotnet run --project dcArca.TestApp/dcArca.TestApp.csproj
```

MCP Server:

```bash
dotnet restore dcArca.McpServer/dcArca.McpServer.csproj
dotnet build dcArca.McpServer/dcArca.McpServer.csproj -c Release
dotnet run --project dcArca.McpServer/dcArca.McpServer.csproj
```

Docker del servidor:

```bash
docker build -f dcArca.McpServer/Dockerfile -t dcarca-mcpserver .
```

El `docker-compose.yml` raíz es genérico y no debe convertirse en un registro de infraestructura privada.

## Validacion antes de integrar

Reproducir la secuencia CI:

```bash
dotnet restore dcArca.Core.Tests/dcArca.Core.Tests.csproj
dotnet restore dcArca.McpServer.Tests/dcArca.McpServer.Tests.csproj
dotnet build dcArca.Core.Tests/dcArca.Core.Tests.csproj --configuration Release --no-restore
dotnet build dcArca.McpServer.Tests/dcArca.McpServer.Tests.csproj --configuration Release --no-restore
dotnet build dcArca.Cli/dcArca.Cli.csproj --configuration Release
dotnet list dcArca.Core/dcArca.Core.csproj package --vulnerable --include-transitive
dotnet list dcArca.McpServer/dcArca.McpServer.csproj package --vulnerable --include-transitive
dotnet list dcArca.Cli/dcArca.Cli.csproj package --vulnerable --include-transitive
dotnet test dcArca.Core.Tests/dcArca.Core.Tests.csproj --configuration Release --no-build
dotnet test dcArca.McpServer.Tests/dcArca.McpServer.Tests.csproj --configuration Release --no-build
```

Comprobar además que no haya material sensible trackeado:

```bash
git ls-files | grep -Ei '\.(pfx|p12|p8|key|pem)$'
```

Resultado esperado: sin coincidencias.

## Deploy

- No desplegar producción desde este repositorio público salvo que una política futura documentada cambie explícitamente esa frontera.
- Mantener TLS, reverse proxy, secretos, certificados, dominios y redes privadas en la capa del host.
- Montar/injectar certificados y secretos en runtime.
- Para cambios del store de emisiones, seguir `docs/EMISSION_STORE_MIGRATION.md` antes de reemplazar una instancia que ya tenga operaciones persistidas.

## Health checks

El MCP Server expone:

```text
GET /health/live
GET /health/ready
```

- `/health/live`: verifica proceso vivo.
- `/health/ready`: verifica configuración esencial y readiness del servidor.
- Ninguno debe llamar a ARCA ni revelar secretos/CUIT.
- Si el lease del store fiscal no puede adquirirse, la instancia escritora no debe considerarse ready.

## Logs

- Correlacionar emisiones mediante identificadores/hash seguros; no registrar `idempotencyKey` en claro.
- No registrar API keys, certificados/passwords, WSAA token/sign ni payloads secretos.
- Ante fallo PDF posterior a autorización fiscal, registrar ambos estados de forma separada: fiscal y documental.

## Troubleshooting

### `401` en endpoint MCP

1. Verificar Bearer API key configurada para el consumidor.
2. Confirmar que la key no esté revocada.
3. No copiar el secreto a logs, Issues o documentación.

### Tool no aparece o llamada rechazada por scope

1. Verificar scopes de la key.
2. `arca:consultar` y `arca:facturar` son independientes.
3. Aplicar mínimo privilegio; no ampliar scopes sólo para ocultar un problema de configuración.

### `SERIES_WRITER_BUSY`

1. Identificar qué proceso posee el mismo `EmissionIdempotency__Directory`.
2. No ejecutar dos writers concurrentes sobre el mismo store filesystem.
3. Verificar que el proceso previo haya terminado realmente antes de reintentar.
4. No eliminar locks/estado fiscal a ciegas.

### `SERIES_RESERVATION_BLOCKED`

1. Localizar la operación pendiente de la misma serie.
2. Resolver/reconciliar esa operación antes de iniciar otra sobre la serie.
3. No asignar manualmente un número alternativo para eludir la reserva.

### Operación `Submitting` o `Uncertain`

1. Reutilizar la misma `idempotencyKey` y request fiscal.
2. El servidor debe consultar exactamente el comprobante ya reservado mediante `FECompConsultar`.
3. Si no existe evidencia fiscal suficiente/equivalente, mantener el estado incierto.
4. Nunca solicitar automáticamente otro CAE para “salir” de la incertidumbre.

### Fiscal autorizado pero PDF falló

1. Considerar la factura ya emitida.
2. Reintentar con la misma operación para recuperar autorización y repetir sólo render documental.
3. Alternativamente usar `generar_pdf_comprobante` sobre el comprobante existente.
4. Nunca repetir emisión fiscal por un fallo de render.

### Drift de numeración

1. Tratar `SERIES_NUMBER_DRIFT` como conflicto de autoridad sobre la serie.
2. Verificar uso externo del mismo ambiente/CUIT/PV/tipo.
3. Mantener el punto de venta operativo como exclusivo del servicio mientras no exista coordinación distribuida con otros writers.

## Backups

- El directorio configurado por `EmissionIdempotency__Directory` contiene evidencia mínima y reservas necesarias para replay/recovery; incluirlo en la estrategia de backup del host cuando el servicio opere con estado real.
- Tratar cualquier backup que contenga configuración runtime como material sensible aunque el store fiscal no deba contener secretos en claro.
- La política concreta de retención/backup pertenece al operador y no se codifica con valores privados en este repositorio.

## Restauracion

1. Detener writers que usen el store objetivo.
2. Restaurar el store completo y coherente, no archivos individuales elegidos manualmente.
3. Validar permisos del directorio y configuración de contexto/ambiente.
4. Arrancar una única instancia writer.
5. Verificar `/health/ready` antes de aceptar emisiones.
6. Ante operaciones pendientes/uncertain, dejar que el flujo de replay/reconciliación determine el estado; no editar JSON fiscal manualmente.

## Mantenimiento periodico

- Ejecutar CI y auditoría de paquetes ante cambios de dependencias.
- Revisar que no se hayan agregado archivos de claves/certificados al tracking.
- Revisar permisos de colaboradores y protección/ruleset de `main` según `docs/REPOSITORY_SECURITY.md`.
- Mantener documentación de contratos sincronizada cuando cambien scopes, contextos, idempotencia, errores, numeración o PDF.
- Actualizar `.agent/catalog/SOURCE_INDEX.md` y `PROCESSING_RUNS.md` al ejecutar Project Archivist.

## Procedimientos de emergencia

### Posible exposición de API key/certificado

1. Revocar/rotar el secreto en el sistema que lo administra.
2. No publicar el valor comprometido en un Issue.
3. Si hubo una clave SSH/deploy histórica reutilizada, rotarla preventivamente antes de volver a utilizarla.
4. Investigar el alcance mediante metadatos/logs seguros.

### Store fiscal corrupto o schema incompatible

1. Detener emisiones sobre el store afectado.
2. No “arreglar” registros manualmente para forzar un estado autorizado.
3. Seguir la migración documentada y preservar una copia del estado previo.
4. Fallar cerrado hasta recuperar coherencia.

## Riesgos operativos conocidos

- El store filesystem tiene autoridad de un solo writer; no es coordinación distribuida.
- Sistemas externos que facturen sobre el mismo punto de venta pueden producir drift.
- Una factura puede quedar fiscalmente autorizada aunque falle PDF/delivery posterior.
- Homologación y producción tienen certificados/endpoints distintos y no son intercambiables.
- `creadorpdf` es una dependencia externa del flujo documental, no de la validez de una autorización fiscal ya obtenida.

## Referencias

- `README.md`
- `docs/MCP_SERVER.md`
- `docs/MCP_CONFIGURATION.md`
- `docs/MCP_AUTHORIZATION.md`
- `docs/MCP_IDEMPOTENCY.md`
- `docs/MCP_NUMBERING.md`
- `docs/WSFE_RECONCILIATION.md`
- `docs/EMISSION_STORE_MIGRATION.md`
- `docs/CREADORPDF_INTEGRATION.md`
- `docs/REPOSITORY_SECURITY.md`
