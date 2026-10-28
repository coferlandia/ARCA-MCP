# ARCA-MCP — Runbook operativo

> Manual de operación cotidiana de la instancia Cadencia. Actualizado con la topología y procedimientos verificados hasta el 16/10/2026. El repositorio es público: no registrar contraseñas, API keys, PFX, token/sign, SOAP ni datos personales. Las rutas y el dominio de despliegue aquí citados ya están definidos en el Compose versionado; cualquier cambio privado del host debe verificarse en el propio servidor.

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
| `${CERTIFICATE_PATH}` (Cadencia: `diegocofre-homo.pfx`) | `/certs/certificado.pfx:ro` | Certificado de homologación; se conserva la ruta interna legacy para no rotar la assignment activa |
| `${PRODUCTION_CERTIFICATE_PATH}` (Cadencia: `diegocofre-prod.pfx`) | `/certs/diegocofre-prod.pfx:ro` | Certificado de producción |
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

git check-ignore -v dcArca.McpServer/appsettings.prod.json secrets/diegocofre-homo.pfx secrets/diegocofre-prod.pfx
```

Los scripts SCP **no** descargan/suben `/data`, **no** eliminan archivos sobrantes, **no** hacen rollback y **no** reinician Docker. En configuración productiva, el contenedor lee `/app/appsettings.json`. Las credenciales fiscales siempre referencian rutas **internas** del contenedor: homologación conserva `/certs/certificado.pfx` por compatibilidad con la assignment activa y producción usa `/certs/diegocofre-prod.pfx`. No utilizar rutas Windows/Ubuntu dentro de `FiscalCredentials`.

La configuración fiscal vigente es V2. Los IDs de ambiente fiscal son estrictamente `homologacion` y `produccion`; no crear secciones alias (`homo`, `prod`, mayúsculas, espacios, etc.). La CLI puede normalizar casing/whitespace al crear un contexto nuevo, pero un catálogo persistido no canónico debe corregirse mediante procedimiento explícito y nunca se reinterpreta silenciosamente.

La configuración fiscal vigente es V2:

```json
{
  "FiscalCredentials": {
    "cadencia-homo": {
      "CertificatePath": "/certs/certificado.pfx",
      "CertificatePassword": "<valor-privado>",
      "PrincipalCuit": "<CUIT_PRINCIPAL_11_DIGITOS>"
    },
    "cadencia-prod": {
      "CertificatePath": "/certs/diegocofre-prod.pfx",
      "CertificatePassword": "<valor-privado>",
      "PrincipalCuit": "<CUIT_PRINCIPAL_11_DIGITOS>"
    }
  },
  "FiscalEnvironments": {
    "homologacion": {
      "WsaaUrl": "https://wsaahomo.afip.gov.ar/ws/services/LoginCms",
      "WsfeUrl": "https://wswhomo.afip.gov.ar/wsfev1/service.asmx",
      "PadronUrl": "https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5"
    },
    "produccion": {
      "WsaaUrl": "https://wsaa.afip.gov.ar/ws/services/LoginCms",
      "WsfeUrl": "https://servicios1.afip.gov.ar/wsfev1/service.asmx",
      "PadronUrl": "https://aws.arca.gob.ar/sr-padron/webservices/personaServiceA5"
    }
  },
  "ApiKeys": {"Directory": "/data"},
  "FiscalContexts": {"Directory": "/data/fiscal-contexts"},
  "EmissionIdempotency": {"Directory": "/data/emission-idempotency"},
  "Recovery": {"Directory": "/data/recovery"}
}
```

Este fragmento ilustra las claves; no sustituir el archivo completo. Las variables del Compose prevalecen sobre el JSON para sus claves correspondientes, incluida la configuración PDF. En el `.env` de Cadencia se pueden definir `Pdf__TemplateKey=factura-ar` y `Pdf__TemplateVersion=8`; si esas variables existen, prevalecen sobre los fallbacks del Compose. Los callers que envían `templateId=null` y `templateVersion=null` usan esa referencia runtime, mientras un par explícito sigue actuando como override. Cambiar esas variables modifica la plantilla por defecto para nuevas generaciones/regeneraciones sin alterar la verdad fiscal ni el CAE. JWT/Auth0 ya no es el mecanismo de autenticación del MCP: se usan API keys Bearer con consumer, scopes y grants. `dcArcaConfig` es legacy y no es la fuente autoritativa del emisor V2.

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

# Antes de cambiar el runtime a v6, publicar/verificar la nueva versión con
# una credencial temporal de gestión del mismo owner que Pdf__ApiKey.
# Preparar CREADORPDF_TEMPLATE_API_KEY según docs/RUNBOOK_PDF_TEMPLATES.md.
CREADORPDF_TEMPLATE_API_KEY="${CREADORPDF_TEMPLATE_API_KEY:?falta credencial temporal de gestión}" \
  ./deploy/cadencia/bootstrap-pdf-template.sh

# La opción --env-file es NECESARIA: de lo contrario APPSETTINGS_PATH,
# CERTIFICATE_PATH, PRODUCTION_CERTIFICATE_PATH, PDF_API_KEY y DOMAIN pueden quedar vacíos.
# Pdf__TemplateKey/Pdf__TemplateVersion también se leen desde .env.
# El fallback operativo versionado es factura-ar/8.
# No activar factura-ar/9 hasta publicarla, verificarla con la key runtime y completar
# el gate visual específico documentado en docs/RUNBOOK_PDF_TEMPLATES.md.
grep '^Pdf__TemplateVersion=' .env || echo 'Pdf__TemplateVersion no fijado en .env; Compose usará el fallback factura-ar/8'

docker compose --env-file .env -f deploy/cadencia/docker-compose.yml config --services
docker compose --env-file .env -f deploy/cadencia/docker-compose.yml config | grep -E 'Pdf__Template(Key|Version)'

docker compose --env-file .env -f deploy/cadencia/docker-compose.yml \
  up -d --build --no-deps mcpserver

docker ps --filter name=dcarca-mcpserver
docker logs --tail 100 dcarca-mcpserver
docker exec dcarca-mcpserver curl -fsS http://127.0.0.1:8080/health/live
docker exec dcarca-mcpserver curl -fsS http://127.0.0.1:8080/health/ready
```

Esperado: contenedor `healthy`, `{"status":"alive"}`, `{"status":"ready"}`. `/health/ready` valida exclusivamente estructura local: recovery gate, catálogo V2, environments canónicos/configurados, referencias de credenciales activas y un `PrincipalCuit` de exactamente 11 dígitos por credencial activa. Un valor ausente o inválido devuelve respectivamente `CREDENTIAL_PRINCIPAL_CUIT_REQUIRED` o `CREDENTIAL_PRINCIPAL_CUIT_INVALID`. No llama ARCA ni abre el PFX; por lo tanto un 200 no demuestra autorización WSAA/WSFE ni habilitación del PV. Un catálogo vacío pero provisionable puede estar ready. Si devuelve 503, corregir el código seguro informado antes de habilitar nuevas emisiones.

La advertencia sobre Docker Bake/buildx no fue bloqueante. Los avisos ASP.NET DataProtection observados no bloquearon el arranque y deberán evaluarse si el proceso necesita persistir esas claves.

No usar `docker compose down -v`. No recrear con `docker run` manual: se perderían los montajes y la definición real de redes/Traefik. Para rollback binario revisar compatibilidad de schema; no restaurar `/data` antiguo automáticamente. El lector actual soporta operaciones v2 legacy sin provenance y v3 con provenance; las operaciones nuevas o reescritas se escriben en schema v4 para preservar también el cierre `AbandonedBeforeSubmission`. Un binario pre-v4 debe fallar cerrado al encontrarlas; no bajar manualmente `SchemaVersion`, quitar provenance ni borrar auditoría/estado de abandono para forzar compatibilidad.

## 5.1. Rollout de PrincipalCuit y smoke de Padrón administrativo

Antes de desplegar una versión que requiera identidad principal separada, agregar `PrincipalCuit` a cada credencial activa de la configuración privada del host. En Cadencia la fuente es `/home/ubuntu/apps/dcarca-mcpserver-config/appsettings.json`, sincronizada con `scripts/download-config.sh` y `scripts/upload-config.sh`; no hace falta convertir estas claves a variables de entorno si ya viven en ese JSON.

Ejemplo conceptual, sin valores reales:

```json
{
  "FiscalCredentials": {
    "cadencia-homo": {
      "CertificatePath": "/certs/certificado.pfx",
      "CertificatePassword": "<valor-privado>",
      "PrincipalCuit": "<CUIT_PRINCIPAL_11_DIGITOS>"
    },
    "cadencia-prod": {
      "CertificatePath": "/certs/diegocofre-prod.pfx",
      "CertificatePassword": "<valor-privado>",
      "PrincipalCuit": "<CUIT_PRINCIPAL_11_DIGITOS>"
    }
  }
}
```

`PrincipalCuit` identifica al titular técnico de la credencial/certificado. No es el CUIT representado por un tenant: Padrón lo usa como `cuitRepresentada`, mientras WSFE sigue usando el `RepresentedCuit` de la representación autorizada como `Auth.Cuit`.

Después de subir la configuración privada, recrear únicamente el servicio MCP para que vuelva a montar `/app/appsettings.json`:

```bash
cd /home/ubuntu/apps/dcarca-mcpserver
docker compose --env-file .env -f deploy/cadencia/docker-compose.yml \
  up -d --no-deps mcpserver
```

El readiness debe responder 200:

```bash
docker exec dcarca-mcpserver \
  curl -fsS http://127.0.0.1:8080/health/ready
```

Si devuelve 503, leer el cuerpo sin `-f`:

```bash
docker exec dcarca-mcpserver \
  curl -sS -i http://127.0.0.1:8080/health/ready
```

`CREDENTIAL_PRINCIPAL_CUIT_REQUIRED` significa que alguna credencial referenciada por una assignment activa no tiene `PrincipalCuit`; `CREDENTIAL_PRINCIPAL_CUIT_INVALID` significa que no son exactamente 11 dígitos. El readiness es local: no abre el PFX ni autentica contra ARCA.

Después del deploy:

1. confirmar `/health/ready = 200`;
2. con una key que tenga `arca:administrar` y grant `<contextId>:administrar`, ejecutar `consultar_padron_administrativo` contra un CUIT objetivo conocido;
3. comprobar que la consulta funciona incluso si ese CUIT objetivo no tiene representación/PV registrados;
4. verificar no-regresión WSFE sobre una representación conocida: en homologación puede usarse consulta o emisión controlada; en producción usar por defecto una consulta de comprobante/PV existente y solicitar un CAE nuevo sólo con aprobación explícita. En ambos casos confirmar que conserva su `representedCuit`/PV habitual.

No exponer `PrincipalCuit`, rutas de PFX, passwords, token/sign ni payloads completos de configuración en Issues o PR. Si cualquiera de estos smokes falla, mantener el issue abierto y revertir/configurar antes de habilitar nuevas operaciones.

## 5.2. Publicación de una nueva plantilla CreadorPDF

Antes de apuntar el runtime a una versión nueva de `factura-ar`, publicar esa versión con una credencial temporal de gestión cuyo `owner_id` sea `dcarca` y cuyos scopes sean `templates:read`, `templates:write` y `templates:publish`.

CreadorPDF expone la administración de keys en `/v1/admin/api-keys` protegida por `CREADORPDF_ADMIN_TOKEN`. El contenedor productivo puede no incluir `curl`; en ese caso usar `python3` del host para llamar a la API y obtener el token con `docker exec ... printenv`. No imprimir el admin token ni el secreto de la key temporal.

Flujo operativo:

1. crear una key temporal, por ejemplo `dcarca-template-deploy`, owner `dcarca`. En Cadencia se verificó este procedimiento desde el host, sin depender de `curl` dentro del contenedor:

   ```bash
   CONTAINER="${CREADORPDF_CONTAINER:-creadorpdf-creadorpdf-1}"
   ADMIN_TOKEN="$(docker exec "$CONTAINER" printenv CREADORPDF_ADMIN_TOKEN)"
   [ -n "$ADMIN_TOKEN" ]

   CREATE_RESPONSE="$(
     ADMIN_TOKEN="$ADMIN_TOKEN" python3 - <<'PY'
   import json
   import os
   import urllib.request

   payload = json.dumps({
       "name": "dcarca-template-deploy",
       "owner_id": "dcarca",
       "scopes": ["templates:read", "templates:write", "templates:publish"],
   }).encode()

   req = urllib.request.Request(
       "http://127.0.0.1:8080/v1/admin/api-keys",
       data=payload,
       method="POST",
       headers={
           "Authorization": f"Bearer {os.environ['ADMIN_TOKEN']}",
           "Content-Type": "application/json",
       },
   )

   with urllib.request.urlopen(req, timeout=10) as response:
       print(response.read().decode())
   PY
   )"

   KEY_ID="$(
     printf '%s' "$CREATE_RESPONSE" |
       python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])'
   )"
   CREADORPDF_TEMPLATE_API_KEY="$(
     printf '%s' "$CREATE_RESPONSE" |
       python3 -c 'import json,sys; print(json.load(sys.stdin)["secret"])'
   )"
   unset CREATE_RESPONSE ADMIN_TOKEN
   echo "Key temporal creada: $KEY_ID"
   ```

2. conservar en variables de shell sólo su `id` y `secret`; no imprimir nunca el secret;
3. ejecutar desde la raíz del repo:

   ```bash
   CREADORPDF_TEMPLATE_API_KEY="$CREADORPDF_TEMPLATE_API_KEY" \
     ./deploy/cadencia/bootstrap-pdf-template.sh
   ```

4. confirmar que el bootstrap informa `TEMPLATE_KEY=factura-ar`, la versión esperada, `TEMPLATE_OWNER=dcarca` y `TEMPLATE_STATUS=published`;
5. verificar con la key runtime de ARCA-MCP que la versión publicada es visible:

   ```bash
   ARCA_CONTAINER="${ARCA_CONTAINER:-dcarca-mcpserver}"
   PDF_BASE_URL="${PDF_BASE_URL:-http://creadorpdf:8080}"

   docker exec "$ARCA_CONTAINER" sh -lc '
     set -eu
     key="$(printenv Pdf__ApiKey)"
     [ -n "$key" ]
     curl -fsS "$1/v1/templates/managed" \
       -H "Authorization: Bearer $key"
   ' sh "$PDF_BASE_URL"
   ```

6. revocar la key temporal con `DELETE /v1/admin/api-keys/{key_id}`. Procedimiento verificado desde el host:

   ```bash
   ADMIN_TOKEN="$(docker exec "$CONTAINER" printenv CREADORPDF_ADMIN_TOKEN)"
   [ -n "$ADMIN_TOKEN" ]

   ADMIN_TOKEN="$ADMIN_TOKEN" KEY_ID="$KEY_ID" python3 - <<'PY'
   import os
   import urllib.request

   req = urllib.request.Request(
       f"http://127.0.0.1:8080/v1/admin/api-keys/{os.environ['KEY_ID']}",
       method="DELETE",
       headers={"Authorization": f"Bearer {os.environ['ADMIN_TOKEN']}"},
   )

   with urllib.request.urlopen(req, timeout=10) as response:
       print(response.read().decode())
   PY
   ```

7. limpiar de la shell las variables sensibles:

   ```bash
   unset ADMIN_TOKEN CREADORPDF_TEMPLATE_API_KEY KEY_ID
   ```

Si el bootstrap falla antes de la revocación, no crear otra key a ciegas: conservar el `KEY_ID`, diagnosticar el error y revocar explícitamente esa credencial temporal.

Las versiones publicadas son inmutables. Si el mismo `name + version` ya existe publicado con contenido distinto al repositorio, no sobrescribirlo: incrementar la versión y actualizar el runtime deliberadamente.

La configuración efectiva del template se comprueba antes del deploy con:

```bash
docker compose --env-file .env -f deploy/cadencia/docker-compose.yml \
  config | grep -E 'Pdf__Template(Key|Version)'
```

En Cadencia el fallback versionado actual es `factura-ar:8`. Si `.env` no fija `Pdf__TemplateVersion`, Compose debe mostrar igualmente `Pdf__TemplateVersion: "8"`. `factura-ar:9` se activa sólo después de publicación, visibilidad runtime y gate visual.

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

`register-representation` y `select-representation-pv` son idempotentes para la misma representación viva y pueden reintentarse de forma segura ante retries/carreras locales; igual conviene inspeccionar `list-contexts` y `list-representations` antes de una intervención manual. Activación/revocación siguen siendo decisiones administrativas explícitas. La API key se genera con `create-key --name ... --scope ... --consumer ... --grant ...`; el secreto `sk-arca-...` se devuelve una sola vez y se entrega de manera privada. La rotación o reset del store puede invalidar todas las keys anteriores.

### Alta de producción

Producción se provisiona como un contexto técnico separado; **no** se cambia el ambiente del contexto de homologación ni se reutiliza su certificado. Precondiciones:

- el certificado de producción debe estar montado como `/certs/diegocofre-prod.pfx` y declarado como `FiscalCredentials:cadencia-prod`;
- `FiscalEnvironments:produccion` debe contener los endpoints oficiales de WSAA, WSFEv1 y Padrón;
- en ARCA, el certificado debe estar asociado al WSN de facturación electrónica (`wsfe`) mediante Administrador de Relaciones de Clave Fiscal;
- el emisor debe disponer de un punto de venta habilitado para Web Services. No adivinar ni reutilizar automáticamente el PV de homologación.

Antes del deploy, el `.env` privado de Cadencia debe apuntar a ambos PFX del host:

```bash
CERTIFICATE_PATH=/home/ubuntu/apps/dcarca-mcpserver-config/diegocofre-homo.pfx
PRODUCTION_CERTIFICATE_PATH=/home/ubuntu/apps/dcarca-mcpserver-config/diegocofre-prod.pfx
```

Validar el Compose **antes** de recrear el contenedor:

```bash
docker compose --env-file .env -f deploy/cadencia/docker-compose.yml config >/dev/null
```

Crear y validar la credencial técnica de producción. `validate-assignment` autentica contra WSAA pero no emite comprobantes:

```bash
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll add-context --id cadencia-arca-produccion --environment produccion
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll add-assignment --context cadencia-arca-produccion --revision v1 --credential cadencia-prod --actor admin
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll validate-assignment --context cadencia-arca-produccion --revision v1 --actor admin
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll activate-assignment --context cadencia-arca-produccion --revision v1 --actor admin
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll set-context-state --context cadencia-arca-produccion --state Active --actor admin
```

Registrar la representación productiva y descubrir los puntos de venta disponibles sin emitir:

```bash
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll register-representation --context cadencia-arca-produccion --consumer cadencia --cuit 20236438989 --actor admin
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll list-points-of-sale --context cadencia-arca-produccion --consumer cadencia --cuit 20236438989
```

Con el PV real devuelto por ARCA, seleccionarlo y activarlo:

```bash
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll select-representation-pv --context cadencia-arca-produccion --consumer cadencia --cuit 20236438989 --point-of-sale <PV> --actor admin
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll activate-representation --context cadencia-arca-produccion --consumer cadencia --cuit 20236438989 --point-of-sale <PV> --actor admin
```

Antes de cualquier CAE real, ejecutar diagnóstico no emisor:

```bash
docker exec dcarca-mcpserver dotnet /tools/dcArca.Cli.dll diagnose-v2 \
  --context cadencia-arca-produccion --cuit 20236438989 \
  --point-of-sale <PV> --tipo-comprobante 11
```

Las API keys existentes **no heredan** acceso al contexto nuevo. Inspeccionar `list-keys` y agregar explícitamente grants de `cadencia-arca-produccion` conservando los grants vigentes. `set-key-grants` reemplaza el conjunto completo de grants de la key: no ejecutarlo con sólo el grant nuevo. Para SecretarIA se requieren, según la función de cada key, scopes/grants `consultar`, `facturar` y/o `administrar`. Nunca copiar el secreto de una API key al repositorio, logs o tickets.

No ejecutar una factura de prueba hasta que WSAA, WSFE/PV, representación y grants estén validados. La primera emisión en este contexto solicita un CAE **real** de producción y debe tratarse como comprobante fiscal definitivo.

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

### Abandono administrativo pre-submission

Usar `abandonar_operacion_pre_submission(operationId, reasonCode)` únicamente para cerrar trabajo que el store durable todavía marque como `Created` o `NumberAssigned` y cuya intención ya no deba continuar. La key debe tener `arca:administrar`, grant `<contextId>:administrar` para el contexto durable y el mismo `consumerId` que la operación. El `operationId` es obligatorio; no sustituirlo por una `idempotencyKey`.

La operación persiste actor, timestamp y `reasonCode`, conserva cualquier número ya reservado y libera sólo la reserva cuyo owner coincide exactamente. La respuesta informa estado previo/final y si se liberó la reserva. Una repetición sobre la misma operación ya abandonada es idempotente y conserva la auditoría original.

**Prohibido** usar este procedimiento sobre `Submitting` o `Uncertain`, borrar archivos del store, editar JSON durable o liberar una serie manualmente para destrabarla. Esos estados pueden haber cruzado el side effect fiscal y deben reconciliarse. El `reasonCode` no admite texto libre: debe ser exactamente uno de `manual_recovery`, `operator_cancelled`, `failed_before_submission` o `stale_pre_submission`. El servidor rechaza cualquier otro valor antes de persistirlo o loguearlo; nunca colocar secretos ni credenciales en este campo.

- `docker compose config` devuelve `invalid spec :/app/appsettings.json`: falta `--env-file .env`.
- `scp` ve carpetas vacías o `Permission denied`: el store es propiedad de root; usar backup en frío con `sudo tar`, sin cambiar propietarios.
- `401/403`: revisar API key, scopes, `consumerId` y grants; no compartir secretos en tickets.
- `SERIES_WRITER_BUSY`: comprobar writer concurrente; nunca borrar locks a ciegas.
- `SERIES_RESERVATION_BLOCKED`, `Submitting`, `Uncertain`: reconciliar operación previa; nunca emitir con otra key ni saltar número.
- `RESTORE_RECONCILIATION_REQUIRED`: el recovery gate impide nuevas emisiones hasta documentar conciliación.
- `FISCAL_CATALOG_UNREADABLE`: catálogo V2 corrupto/schema inválido/V1 pendiente; mantener live para diagnóstico y reparar mediante procedimiento controlado, no editar JSON a ciegas.
- `FISCAL_ENVIRONMENT_UNRESOLVED`: un contexto referencia un environment canónico que no tiene los tres endpoints locales configurados.
- `CREDENTIAL_REFERENCE_UNRESOLVED`: una assignment activa referencia un `credentialId` sin `CertificatePath` server-owned. El health no intenta abrir ese archivo ni autenticar contra WSAA.
- CAE autorizado pero PDF fallido: reintentar solo el render, no la emisión fiscal.
- Comprobar fechas de expiración de PFX, backups, disco y CI.

Referencias: `docs/MCP_CONFIGURATION.md`, `docs/MCP_FISCAL_CONTEXTS.md`, `docs/MCP_AUTHORIZATION.md`, `docs/MCP_IDEMPOTENCY.md`, `docs/OPERATIONS_RECOVERY.md`, `docs/EMISSION_STORE_MIGRATION.md`, `docs/WSFE_RECONCILIATION.md`.
