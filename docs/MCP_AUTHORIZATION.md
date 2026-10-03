# Autorización MCP por API key, scopes y grants

`dcArca.McpServer` usa la API key directamente como Bearer token:

```http
Authorization: Bearer sk-arca-...
```

Actualmente no existe un endpoint OAuth/login que intercambie credenciales por un access token de corta duración.

## Scopes

Los scopes de lectura y escritura son capacidades independientes.

- `arca:consultar`: habilita las tools de consulta, diagnóstico, reconciliación y generación de PDF de comprobantes existentes.
- `arca:facturar`: habilita validación y emisión.

`arca:facturar` no implica `arca:consultar`. Un cliente que necesite ambas capacidades debe tener ambos scopes:

```text
arca:consultar,arca:facturar
```

## Consumer y grants de contexto

Para clientes nuevos se recomienda crear keys ligadas a un `consumerId` estable y grants explícitos por contexto fiscal.

Ejemplo conceptual:

```text
consumerId: cadencia
scopes:
  arca:consultar
  arca:facturar
grants:
  cadencia-arca-homologacion:consultar
  cadencia-arca-homologacion:facturar
```

El scope habilita la clase de operación MCP; el grant limita sobre qué contexto fiscal puede ejercerla. Ambos controles deben autorizar la operación.

Las keys legacy sin `consumerId` se conservan por compatibilidad, pero no deben ser el patrón para nuevos consumidores multi-contexto.

## Persistencia y secreto

Cada API key se guarda en `ApiKeys__Directory/api_keys.json`. El secreto:

- se genera aleatoriamente con prefijo `sk-arca-`;
- se muestra una sola vez al crear la key;
- nunca se persiste en claro;
- se almacena únicamente como hash SHA-256;
- no puede recuperarse mediante `list-keys`;
- una revocación se aplica al request siguiente.

No editar `api_keys.json` manualmente.

## Administración recomendada

Usar el wrapper operativo:

```bash
bash scripts/api-keys/manage-key.sh --help
```

Para una key de smoke ligada a un contexto:

```bash
bash scripts/api-keys/manage-key.sh create \
  --directory ./data \
  --name local-smoke \
  --consumer local-dev \
  --context local-arca-homologacion \
  --mode smoke
```

Listar:

```bash
bash scripts/api-keys/manage-key.sh list --directory ./data
```

Revocar:

```bash
bash scripts/api-keys/manage-key.sh revoke --directory ./data --id key_ID
```

El wrapper delega en `dcArca.Cli`; no implementa otro formato de store ni otra generación de secretos.

## CLI directo

También puede usarse `dcArca.Cli` directamente:

```bash
ApiKeys__Directory=/data dotnet run --project dcArca.Cli -- \
  create-key \
  --name secretaria \
  --scope arca:consultar,arca:facturar \
  --consumer cadencia \
  --grant cadencia-arca-homologacion:consultar,cadencia-arca-homologacion:facturar

ApiKeys__Directory=/data dotnet run --project dcArca.Cli -- list-keys
ApiKeys__Directory=/data dotnet run --project dcArca.Cli -- revoke-key key_ID
```

Para el procedimiento completo de local, Docker/Cadencia, rotación, revocación y uso desde `run-local.sh`, ver `API_KEYS_RUNBOOK.md`.
