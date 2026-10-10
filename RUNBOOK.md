# ARCA-MCP — Runbook operativo

> Manual de operación cotidiana de la instancia Cadencia. Actualizado con la topología y pruebas realizadas el 10/10/2026. El repositorio es público: no registrar contraseñas, API keys, PFX, token/sign, SOAP ni datos personales. Las rutas y el dominio de despliegue aquí citados ya están definidos en el Compose versionado; cualquier cambio privado del host debe verificarse en el propio servidor.

## 1. Repositorios y ejecutables

- Principal: https://github.com/coferlandia/ARCA-MCP
- Upstream: https://github.com/diegocofre/ARCA-MCP
- Rama de despliegue: `main` (integrar PR y esperar CI antes de actualizar).
- Proyectos: `dcArca.Core`, `dcArca.McpServer`, `dcArca.Cli` y respectivos tests.
- Dockerfile: `dcArca.McpServer/Dockerfile`, .NET 10.
- Servicio de PDF: `creadorpdf` separado del MCP fiscal; un error PDF posterior a CAE no anula la emisión fiscal.

## 2. Topología verificada en Cadencia

Conexión SSH desde Git Bash: `ssh cadencia`. El hostname Linux observado es `enerone`; usuario operativo `ubuntu`.

| Elemento | Valor |
| --- | --- |
| Checkout | `/home/ubuntu/apps/dcarca-mcpserver` |
| Compose real | `deploy/cadencia/docker-compose.yml` |
| Variables Compose | `/home/ubuntu/apps/dcarca-mcpserver/.env` (privado) |
| Proyecto Compose | `cadencia` |
| Servicio Compose | `mcpserver` |
| Contenedor | `dcarca-mcpserver` |
| Imagen | `dcarca-mcpserver:latest` |
| Redes | `red-consultora` (Traefik), `red-cadencia` |
| Proxy/TLS | Traefik, `https://arca.cadencia.com.ar`, HTTPS |
| Puerto del contenedor | 8080, HTTP interno |

Montajes confirmados con `docker inspect`:

| Archivo/directorio host | Contenedor | Propósito |
| --- | --- | --- |
| `/home/ubuntu/apps/dcarca-mcpserver-config/appsettings.json` | `/app/appsettings.json:ro` | JSON privado |
| `/home/ubuntu/apps/dcarca-mcpserver-config/certificado.pfx` | `/certs/certificado.pfx:ro` | Certificado |
| `/home/ubuntu/apps/dcarca-mcpserver-data/` | `/data` | Estado fiscal durable |

No confundir el `docker-compose.yml` genérico en la raíz con el Compose efectivo del servidor.

Inspección habitual:

```bash
ssh cadencia
cd /home/ubuntu/apps/dcarca-mcpserver
docker ps -a --filter name=dcarca-mcpserver
docker inspect dcarca-mcpserver --format '{{range .Mounts}}{{println .Source "->" .Destination}}{{end}}'
docker logs --tail 100 dcarca-mcpserver
```

## 3. Configuración privada, certificados y sincronización

En el checkout local:

- `dcArca.McpServer/appsettings.prod.json`: copia local del `appsettings.json` del servidor.
- `secrets/`: contenido privado del directorio remoto de configuración, excepto el JSON localmente duplicado.
- `scripts/download-config.sh`: baja JSON y secretos usando SCP.
- `scripts/upload-config.sh`: sube JSON y todo el contenido de `secrets/` usando SCP.
- `backups/`: backups locales fechados del estado; **ignorar en Git**.

```bash
# Git Bash desde la raíz local:
bash scripts/download-config.sh
# Editar y validar JSON:
python -m json.tool dcArca.McpServer/appsettings.prod.json > /dev/null
# Subir configuración privada:
bash scripts/upload-config.sh

git check-ignore -v dcArca.McpServer/appsettings.prod.json secrets/certificado.pfx
```

Los scripts SCP **no** descargan/suben `/data`, **no** eliminan archivos sobrantes, **no** hacen rollback y **no** reinician Docker. En configuración productiva, el contenedor lee `/app/appsettings.json`. El certificado se referencia por su ruta **interna** `/certs/certificado.pfx`; no utilizar la ruta Windows/Ubuntu dentro de `FiscalCredentials`.

La configuración fiscal vigente es V2:

```json
{
  "FiscalCredentials": {
    "cadencia-homo": {
      "CertificatePath": "/certs/certificado.pfx",
      "CertificatePassword": "<valor-privado>"
    }
  },
  "FiscalEnvironments": {
    "homologacion": {
      "WsaaUrl": "https://wsaahomo.afip.gov.ar/ws/services/LoginCms",
      "WsfeUrl": "https://wswhomo.afip.gov.ar/wsfev1/service.asmx",
      "PadronUrl": "https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5"
    }
  },
  "ApiKeys": {"Directory": "/data"},
  "FiscalContexts": {"Directory": "/data/fiscal-contexts"},
  "EmissionIdempotency": {"Directory": "/data/emission-idempotency"},
  "Recovery": {"Directory": "/data/recovery"}
}
```

Este fragmento ilustra las claves; no sustituir el archivo completo. Las variables del Compose prevalecen sobre el JSON para sus claves correspondientes, incluida la configuración PDF. JWT/Auth0 ya no es el mecanismo de autenticación del MCP: se usan API keys Bearer con consumer, scopes y grants. `dcArcaConfig` es legacy y no es la fuente autoritativa del emisor V2.

**No publicar** el archivo `.env`, valores de `PDF_API_KEY`, passwords de PFX ni el resultado completo de `docker inspect` cuando incluya variables sensibles.

## 4. Backups de datos operativos

El host almacena:
- `/data/api_keys.json`: API keys y grants.
- `/data/fiscal-contexts/fiscal-catalog-v2.json`: contextos técnicos, assignments y representaciones; `contexts.json` correspondía al catálogo V1.
- `/data/emission-idempotency/`: operaciones, reservas de serie, replay y conciliación.
- `/data/recovery/`: maintenance/recovery gate e historial.

La aplicación crea varios subdirectorios y archivos como `root`, con permisos `0700`/`0600`. Un `scp -r` directo como `ubuntu` puede devolver `Permission denied` y dejar una copia aparentemente vacía. **No cambiar propietarios/permisos del store para resolverlo**.

El script local `scripts/backup-data.sh` usa una copia en frío: verifica el estado del contenedor, lo detiene si estaba en ejecución, crea un `tar.gz` temporal con `sudo`, lo transfiere por `scp`, extrae en `backups/<fecha>/data` y vuelve a iniciarlo si lo detuvo. Requiere `sudo` operativo en Cadencia. Coordinar ventana de mantenimiento y comprobar que no haya emisiones en curso ni otro writer. No confundir el backup de archivos con un restore fiscal reconciliado.

```bash
# Git Bash local:
bash scripts/backup-data.sh
# Confirmar que contienen api_keys.json y fiscal-contexts/:
find backups -name fiscal-catalog-v2.json -o -name contexts.json
```

`/backups/` y `/secrets/` deben figurar en `.gitignore`. El backup contiene datos sensibles: conservarlo fuera de Git y con acceso restringido. Para restaurar un store luego de emitir, seguir `docs/OPERATIONS_RECOVERY.md`: no recuperar archivos viejos a ciegas, hay que reconciliar cualquier ventana perdida.

## 5. Deploy estándar

Precondiciones: CI verde, PR integrado en `main`, revisión de cambios de schema/config, backup apropiado y ausencia de operaciones pendientes `Submitting/Uncertain`. Mantener un solo writer fiscal. El deploy de código **no limpia** `/data`.

```bash
ssh cadencia
cd /home/ubuntu/apps/dcarca-mcpserver
git status --short
git fetch origin
git switch main
git pull --ff-only origin main
git log -1 --oneline

# La opción --env-file es NECESARIA: de lo contrario APPSETTINGS_PATH,
# CERTIFICATE_PATH, PDF_API_KEY y DOMAIN pueden quedar vacíos.
docker compose --env-file .env -f deploy/cadencia/docker-compose.yml config --services

docker compose --env-file .env -f deploy/cadencia/docker-compose.yml \
  up -d --build --no-deps mcpserver

docker ps --filter name=dcarca-mcpserver
docker logs --tail 100 dcarca-mcpserver
docker exec dcarca-mcpserver curl -fsS http://127.0.0.1:8080/health/live
docker exec dcarca-mcpserver curl -fsS http://127.0.0.1:8080/health/ready
```

Esperado: contenedor `healthy`, `{"status":"alive"}`, `{"status":"ready"}`. La advertencia sobre Docker Bake/buildx no fue bloqueante. Los avisos ASP.NET DataProtection observados no bloquearon el arranque y deberán evaluarse si el proceso necesita persistir esas claves.

No usar `docker compose down -v`. No recrear con `docker run` manual: se perderían los montajes y la definición real de redes/Traefik. Para rollback binario revisar compatibilidad de schema; no restaurar `/data` antiguo automáticamente.

## 6. Provisionamiento V2 (homologación actual)

Contexto técnico usado en las pruebas:
- `contextId=cadencia-arca-homologacion`
- `environment=homologacion`
- `credentialId=cadencia-homo`, assignment revision `v1`
- `consumerId=cadencia`, CUIT de prueba `20236438989`, PV `77`.

Los comandos se ejecutan en Cadencia, dentro del contenedor, usando `dotnet /tools/dcArca.Cli.dll`:

```bash
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll list-contexts
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll add-context --id cadencia-arca-homologacion --environment homologacion
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll add-assignment --context cadencia-arca-homologacion --revision v1 --credential cadencia-homo --actor admin
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll validate-assignment --context cadencia-arca-homologacion --revision v1 --actor admin
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll activate-assignment --context cadencia-arca-homologacion --revision v1 --actor admin
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll set-context-state --context cadencia-arca-homologacion --state Active --actor admin
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll register-representation --context cadencia-arca-homologacion --consumer cadencia --cuit 20236438989 --actor admin
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll list-points-of-sale --context cadencia-arca-homologacion --consumer cadencia --cuit 20236438989
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll select-representation-pv --context cadencia-arca-homologacion --consumer cadencia --cuit 20236438989 --point-of-sale 77 --actor admin
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll activate-representation --context cadencia-arca-homologacion --consumer cadencia --cuit 20236438989 --point-of-sale 77 --actor admin
```

Comandos de alta son **de una sola vez**: no repetir si el registro ya existe. Verificar primero con `list-contexts` y `list-representations`. La API key se genera con `create-key --name ... --scope ... --consumer ... --grant ...`; el secreto `sk-arca-...` se devuelve una sola vez y se entrega de manera privada. La rotación o reset del store puede invalidar todas las keys anteriores.

## 7. Diagnóstico fiscal no emisor

```bash
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll diagnose-v2 \
  --context cadencia-arca-homologacion --cuit 20236438989 \
  --point-of-sale 77 --tipo-comprobante 11
```

Hallazgos **efectivamente observados** el 10/10/2026:
- WSAA autenticó exitosamente con el certificado homologación; `validate-assignment` devolvió `Verified=true`.
- WSFE `FECompUltimoAutorizado` para factura C (11), PV 77 devolvió `Success=true` y número **13**.
- `FEParamGetPtosVenta` devolvió `WSFE_602`: `Sin Resultados: - Metodo FEParamGetPtosVenta`. Un 602 con ese texto **no demuestra** por sí solo revocación ni PV bloqueado.
- Padrón A5 respondió SOAP fault `soap:Server`, `No existe persona con ese Id`. No implica que la autenticación WSFE falló.
- Los tres WSDL/servicios WSAA, WSFE y Padrón devolvieron HTTP 200 desde Docker. Es una verificación de conectividad, no autorización fiscal.

Con el contrato actual, una representación sin PV seleccionado permanece `Pending`, `PointOfSale=0`, `CanReadOrEmit=false`. El flujo de PV manual en homologación se modifica en un PR aparte: no asumir que está desplegado hasta integrarlo y verificarlo.

## 8. Pruebas de integración previstas

Secuencia aprobada para homologación, una vez activada la representación y creada la API key con grants:
1. Factura C (WSFE tipo 11) con `idempotencyKey` estable, a través del MCP V2 y su store durable.
2. Nota de crédito C (tipo 13), con referencia al tipo/PV/número de la factura realmente autorizada.
3. Consultar ambas operaciones con `FECompConsultar` y contrastar CAE y numeración.
4. Repetir exactamente las requests con las mismas keys para comprobar que no se solicitan CAE nuevos.
5. Validar flujo desde SecretarIA, manejo de errores y render PDF.

No utilizar `dcArca.Cli facturar` legacy (lee `dcArcaConfig` y elude el flujo MCP V2). Aún **no** se ejecutaron las emisiones de esta secuencia; no confundir CI ni consultas no emisoras con pruebas de facturación real. Cambiar a certificado de producción exige entorno/credencial distintos y autorización específica antes de emitir comprobantes reales.

## 9. Cutover V1 → V2 (histórico y excepcional)

En octubre de 2026 se decidió descartar sólo el estado histórico de pruebas tras backup en frío. Se detectaron `api_keys.json`, `fiscal-contexts/contexts.json` V1 y operaciones de prueba antiguas; V2 rechaza `contexts.json` con `FISCAL_CATALOG_V1_RESET_REQUIRED`. **No aplicar esa limpieza en futuras instalaciones con datos fiscales reales**. Mantener backup y seguir `docs/EMISSION_STORE_MIGRATION.md` y `docs/OPERATIONS_RECOVERY.md` cuando haya evidencia fiscal a preservar. No automatizar borrados durante el despliegue.

## 10. Troubleshooting y mantenimiento

- `docker compose config` devuelve `invalid spec :/app/appsettings.json`: falta `--env-file .env`.
- `scp` ve carpetas vacías o `Permission denied`: el store es propiedad de root; usar backup en frío con `sudo tar`, sin cambiar propietarios.
- `401/403`: revisar API key, scopes, `consumerId` y grants; no compartir secretos en tickets.
- `SERIES_WRITER_BUSY`: comprobar writer concurrente; nunca borrar locks a ciegas.
- `SERIES_RESERVATION_BLOCKED`, `Submitting`, `Uncertain`: reconciliar operación previa; nunca emitir con otra key ni saltar número.
- `RESTORE_RECONCILIATION_REQUIRED`: el recovery gate impide nuevas emisiones hasta documentar conciliación.
- CAE autorizado pero PDF fallido: reintentar solo el render, no la emisión fiscal.
- Comprobar fechas de expiración de PFX, backups, disco y CI.

Referencias: `docs/MCP_CONFIGURATION.md`, `docs/MCP_FISCAL_CONTEXTS.md`, `docs/MCP_AUTHORIZATION.md`, `docs/MCP_IDEMPOTENCY.md`, `docs/OPERATIONS_RECOVERY.md`, `docs/EMISSION_STORE_MIGRATION.md`, `docs/WSFE_RECONCILIATION.md`.
