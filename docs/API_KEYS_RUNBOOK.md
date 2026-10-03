# Runbook de API keys de ARCA-MCP

Este runbook describe cómo crear, listar, rotar, revocar y usar API keys de `dcArca.McpServer` sin editar manualmente `api_keys.json`.

La única fuente de verdad para crear y modificar keys es `dcArca.Cli`, que usa `FileSystemApiKeyStore`. El wrapper `scripts/api-keys/manage-key.sh` sólo normaliza la operación para uso humano y elige cómo ejecutar ese CLI.

## Modelo de autorización

ARCA-MCP usa la API key directamente como Bearer token:

```http
Authorization: Bearer sk-arca-...
```

No existe actualmente un endpoint OAuth/login que intercambie credenciales por un access token de corta duración.

Cada API key tiene dos niveles independientes de autorización:

- `scope`: habilita una clase de herramientas MCP. `arca:consultar` permite lecturas y `arca:facturar` permite emisiones.
- `grant`: limita un consumidor a un contexto fiscal y una operación concreta (`consultar` o `facturar`).

Una key destinada a un cliente real debe usar identidad estable (`consumerId`) y grants explícitos por contexto. Evitar nuevas keys legacy sin `consumerId` salvo compatibilidad deliberada.

El secreto se genera con entropía criptográfica, se devuelve una sola vez y el store persiste únicamente su SHA-256. `list` nunca puede recuperar el secreto original.

## Wrapper operativo

Ayuda:

```bash
bash scripts/api-keys/manage-key.sh --help
```

El wrapper soporta tres comandos:

```text
create
list
revoke
```

Y tres backends:

```text
auto     usa dotnet si hay un SDK utilizable; si no, usa Docker
dotnet   fuerza dcArca.Cli desde el checkout local
docker   ejecuta /tools/dcArca.Cli.dll incluido en la imagen del MCP
```

El backend `docker` no modifica el store por una implementación alternativa: ejecuta el mismo `dcArca.Cli` empaquetado en la imagen del servidor.

## Crear una key para un MCP local

Si el servidor local usa `ApiKeys__Directory=./data`, desde la raíz del repositorio:

```bash
bash scripts/api-keys/manage-key.sh create \
  --directory ./data \
  --name local-smoke \
  --consumer local-dev \
  --context local-arca-homologacion \
  --mode smoke
```

`--mode smoke` es un preset deliberadamente acotado que crea:

```text
scopes:
  arca:consultar
  arca:facturar

grants:
  <context>:consultar
  <context>:facturar
```

El JSON de salida contiene `Secret`. Copiarlo en ese momento. No se puede recuperar después.

Para una key personalizada, no usar `--mode` y declarar explícitamente scopes/grants:

```bash
bash scripts/api-keys/manage-key.sh create \
  --directory ./data \
  --name reader \
  --consumer reporting \
  --scope arca:consultar \
  --grant local-arca-homologacion:consultar
```

## Cadencia

El compose de Cadencia monta:

```text
host:      /home/ubuntu/apps/dcarca-mcpserver-data
container: /data
```

El MCP usa `ApiKeys__Directory=/data`, por lo que el wrapper debe apuntar al directorio del host, no a `/data`:

```bash
cd /home/ubuntu/apps/dcarca-mcpserver

bash scripts/api-keys/manage-key.sh create \
  --directory /home/ubuntu/apps/dcarca-mcpserver-data \
  --name diego-smoke \
  --consumer cadencia \
  --context cadencia-arca-homologacion \
  --mode smoke
```

Si el host no tiene un SDK .NET, `auto` usa Docker. La imagen `dcarca-mcpserver:latest` incluye `/tools/dcArca.Cli.dll`; el wrapper monta temporalmente el store host en `/data` y ejecuta ese CLI. No hace falta detener el MCP: el store usa coordinación de archivo y cada request valida la key contra el store persistido.

Para forzar el mismo artefacto Docker:

```bash
bash scripts/api-keys/manage-key.sh create \
  --backend docker \
  --image dcarca-mcpserver:latest \
  --directory /home/ubuntu/apps/dcarca-mcpserver-data \
  --name diego-smoke \
  --consumer cadencia \
  --context cadencia-arca-homologacion \
  --mode smoke
```

Antes de usar este procedimiento en un host actualizado desde una versión anterior, confirmar que la imagen desplegada ya contiene `/tools/dcArca.Cli.dll`.

## Listar keys

```bash
bash scripts/api-keys/manage-key.sh list \
  --directory /home/ubuntu/apps/dcarca-mcpserver-data
```

La salida muestra metadatos como `Id`, nombre, consumidor, grants, scopes, estado, creación, revocación y último uso. No muestra ni puede reconstruir el secreto.

## Probar una key desde un cliente externo

No guardar el secreto en el repositorio ni en archivos versionados. Para el smoke local contra Cadencia se puede exportar sólo en la sesión actual:

```bash
export ARCA_MCP_TOKEN='sk-arca-...'

bash scripts/smoke/run-local.sh \
  --url https://arca.cadencia.com.ar \
  --config scripts/smoke/fiscal_smoke.local.json
```

Sin `--execute`, el smoke no emite comprobantes. Para homologación con emisiones:

```bash
bash scripts/smoke/run-local.sh \
  --url https://arca.cadencia.com.ar \
  --config scripts/smoke/fiscal_smoke.local.json \
  --execute
```

`run-local.sh` es deliberadamente un cliente. No crea, rota ni revoca credenciales del servidor.

## Rotación

La rotación no modifica el secreto de una key existente. Se hace con create-before-revoke:

1. crear una nueva key con el mismo consumidor/scopes/grants;
2. entregar el nuevo `Secret` al cliente por un canal seguro;
3. probar el cliente con la nueva key;
4. obtener el `Id` de la key anterior con `list`;
5. revocar la anterior.

Esto permite solapamiento controlado sin una ventana de indisponibilidad.

## Revocar

```bash
bash scripts/api-keys/manage-key.sh revoke \
  --directory /home/ubuntu/apps/dcarca-mcpserver-data \
  --id key_0123456789abcdef
```

La revocación se aplica al siguiente request: `FileSystemApiKeyStore` valida únicamente registros activos.

## Recuperación y seguridad

- No editar `api_keys.json` a mano.
- No guardar `Secret` en `appsettings.json`, Git, issues, logs o evidencia de smoke.
- Si se pierde el secreto, crear una nueva key y revocar la anterior; no existe recuperación del secreto.
- Usar una key distinta por consumidor/uso operativo para que la revocación tenga blast radius acotado.
- Mantener grants mínimos por contexto.
- En producción, preferir rotación create-before-revoke y verificar el cliente antes de revocar.
- El wrapper crea directorios con `umask 077`; el store además intenta fijar permisos Unix restrictivos sobre directorio y archivo.

## Diagnóstico rápido

Si `auto` informa que no existe backend utilizable:

```bash
dotnet --version
docker --version
```

Si se fuerza Docker y falla porque `/tools/dcArca.Cli.dll` no existe, reconstruir/desplegar una imagen ARCA-MCP que incluya este runbook/cambio.

Si una key existe pero el MCP responde 401, verificar que se está enviando exactamente:

```text
Authorization: Bearer <Secret>
```

Si responde 403 o una operación fiscal informa contexto no autorizado, revisar `consumerId`, scopes y grants con `list`; no ampliar permisos a ciegas para hacer pasar la prueba.
