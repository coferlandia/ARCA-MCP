#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
DEFAULT_IMAGE="${ARCA_MCP_IMAGE:-dcarca-mcpserver:latest}"

usage() {
  cat <<'EOF'
Usage:
  bash scripts/api-keys/manage-key.sh create --directory DIR --name NAME [options]
  bash scripts/api-keys/manage-key.sh list --directory DIR [options]
  bash scripts/api-keys/manage-key.sh revoke --directory DIR --id KEY_ID [options]

Execution options:
  --backend auto|dotnet|docker   Default: auto (prefer local dotnet SDK).
  --image IMAGE                 Docker image containing /tools/dcArca.Cli.dll.

Create options:
  --name NAME                   Human-readable key name.
  --mode smoke                  Safe preset for smoke clients. Requires --consumer
                                and --context; grants consultar+facturar only there.
  --consumer CONSUMER_ID        Stable client/consumer identity.
  --context CONTEXT_ID          Fiscal context used by --mode smoke.
  --scope CSV                   Explicit scopes when not using --mode.
  --grant CSV                   Explicit grants context:operation,... when not using --mode.

Examples:
  # Local store, local .NET SDK
  bash scripts/api-keys/manage-key.sh create \
    --directory ./data \
    --name local-smoke \
    --consumer local-dev \
    --context local-arca-homologacion \
    --mode smoke

  # Cadencia host: uses dotnet if installed, otherwise the deployed Docker image
  bash scripts/api-keys/manage-key.sh create \
    --directory /home/ubuntu/apps/dcarca-mcpserver-data \
    --name diego-smoke \
    --consumer cadencia \
    --context cadencia-arca-homologacion \
    --mode smoke

  bash scripts/api-keys/manage-key.sh list \
    --directory /home/ubuntu/apps/dcarca-mcpserver-data

  bash scripts/api-keys/manage-key.sh revoke \
    --directory /home/ubuntu/apps/dcarca-mcpserver-data \
    --id key_...
EOF
}

fail() {
  echo "ERROR: $*" >&2
  exit 2
}

if (($# == 0)); then
  usage >&2
  exit 2
fi

if [[ "$1" == "-h" || "$1" == "--help" ]]; then
  usage
  exit 0
fi

command_name="$1"
shift

backend="auto"
image="$DEFAULT_IMAGE"
directory=""
name=""
mode=""
consumer=""
context=""
scopes=""
grants=""
key_id=""

while (($#)); do
  case "$1" in
    --backend)
      (($# >= 2)) || fail "--backend requires a value."
      backend="$2"
      shift 2
      ;;
    --backend=*) backend="${1#--backend=}"; shift ;;
    --image)
      (($# >= 2)) || fail "--image requires a value."
      image="$2"
      shift 2
      ;;
    --image=*) image="${1#--image=}"; shift ;;
    --directory)
      (($# >= 2)) || fail "--directory requires a value."
      directory="$2"
      shift 2
      ;;
    --directory=*) directory="${1#--directory=}"; shift ;;
    --name)
      (($# >= 2)) || fail "--name requires a value."
      name="$2"
      shift 2
      ;;
    --name=*) name="${1#--name=}"; shift ;;
    --mode)
      (($# >= 2)) || fail "--mode requires a value."
      mode="$2"
      shift 2
      ;;
    --mode=*) mode="${1#--mode=}"; shift ;;
    --consumer)
      (($# >= 2)) || fail "--consumer requires a value."
      consumer="$2"
      shift 2
      ;;
    --consumer=*) consumer="${1#--consumer=}"; shift ;;
    --context)
      (($# >= 2)) || fail "--context requires a value."
      context="$2"
      shift 2
      ;;
    --context=*) context="${1#--context=}"; shift ;;
    --scope)
      (($# >= 2)) || fail "--scope requires a value."
      scopes="$2"
      shift 2
      ;;
    --scope=*) scopes="${1#--scope=}"; shift ;;
    --grant)
      (($# >= 2)) || fail "--grant requires a value."
      grants="$2"
      shift 2
      ;;
    --grant=*) grants="${1#--grant=}"; shift ;;
    --id)
      (($# >= 2)) || fail "--id requires a value."
      key_id="$2"
      shift 2
      ;;
    --id=*) key_id="${1#--id=}"; shift ;;
    -h|--help)
      usage
      exit 0
      ;;
    *) fail "Unknown option: $1" ;;
  esac
done

[[ -n "$directory" ]] || fail "--directory is required."
umask 077
mkdir -p "$directory"
directory="$(cd -- "$directory" && pwd -P)"

case "$backend" in
  auto|dotnet|docker) ;;
  *) fail "Unsupported backend '$backend'. Use auto, dotnet, or docker." ;;
esac

cli_args=()
case "$command_name" in
  create)
    [[ -n "$name" ]] || fail "--name is required for create."
    if [[ -n "$mode" ]]; then
      case "$mode" in
        smoke)
          [[ -n "$consumer" ]] || fail "--consumer is required for --mode smoke."
          [[ -n "$context" ]] || fail "--context is required for --mode smoke."
          [[ -z "$scopes" && -z "$grants" ]] || fail "--mode smoke cannot be combined with --scope/--grant."
          scopes="arca:consultar,arca:facturar"
          grants="$context:consultar,$context:facturar"
          ;;
        *) fail "Unsupported mode '$mode'." ;;
      esac
    else
      [[ -n "$scopes" ]] || fail "--scope is required when --mode is not used."
      if [[ -n "$consumer" && -z "$grants" ]]; then
        fail "--grant is required when --consumer is supplied."
      fi
      if [[ -z "$consumer" && -n "$grants" ]]; then
        fail "--grant requires --consumer."
      fi
    fi

    cli_args=(create-key --name "$name" --scope "$scopes")
    if [[ -n "$consumer" ]]; then
      cli_args+=(--consumer "$consumer" --grant "$grants")
    fi
    ;;
  list)
    cli_args=(list-keys)
    ;;
  revoke)
    [[ -n "$key_id" ]] || fail "--id is required for revoke."
    cli_args=(revoke-key "$key_id")
    ;;
  *) fail "Unknown command '$command_name'. Use create, list, or revoke." ;;
esac

use_backend="$backend"
if [[ "$use_backend" == "auto" ]]; then
  if command -v dotnet >/dev/null 2>&1 && dotnet --version >/dev/null 2>&1; then
    use_backend="dotnet"
  elif command -v docker >/dev/null 2>&1; then
    use_backend="docker"
  else
    echo "ERROR: neither a usable dotnet SDK nor Docker is available." >&2
    exit 127
  fi
fi

if [[ "$command_name" == "create" ]]; then
  echo "Creating API key. The Secret printed by dcArca.Cli is shown once; store it securely." >&2
fi

case "$use_backend" in
  dotnet)
    command -v dotnet >/dev/null 2>&1 || { echo "ERROR: dotnet is required for --backend dotnet." >&2; exit 127; }
    dotnet --version >/dev/null 2>&1 || { echo "ERROR: dotnet SDK is not usable." >&2; exit 127; }
    ApiKeys__Directory="$directory" \
      dotnet run --project "$REPO_ROOT/dcArca.Cli/dcArca.Cli.csproj" -- "${cli_args[@]}"
    ;;
  docker)
    command -v docker >/dev/null 2>&1 || { echo "ERROR: docker is required for --backend docker." >&2; exit 127; }
    docker_args=(run --rm \
      -e ApiKeys__Directory=/data \
      -v "$directory:/data" \
      --entrypoint dotnet)
    if command -v id >/dev/null 2>&1 && [[ "$(uname -s 2>/dev/null || true)" != MINGW* ]]; then
      docker_args+=(--user "$(id -u):$(id -g)")
    fi
    docker_args+=("$image" /tools/dcArca.Cli.dll "${cli_args[@]}")
    docker "${docker_args[@]}"
    ;;
esac
