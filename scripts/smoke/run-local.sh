#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
DEFAULT_CONFIG="$SCRIPT_DIR/fiscal_smoke.local.json"
DEFAULT_URL="https://arca.cadencia.com.ar/"

usage() {
  cat <<'EOF'
Usage:
  bash scripts/smoke/run-local.sh [--url URL] [fiscal_smoke options]

Examples:
  # Homologation
  bash scripts/smoke/run-local.sh --execute

  # Production
  bash scripts/smoke/run-local.sh --execute --allow-production

  # Explicit endpoint/config
  bash scripts/smoke/run-local.sh \
    --url https://arca.example.com \
    --config scripts/smoke/fiscal_smoke.local.json \
    --execute

Authentication:
  Uses ARCA_MCP_TOKEN if already exported. Otherwise prompts for the current
  ARCA-MCP Bearer API key without echoing it.

Endpoint:
  --url takes precedence over ARCA_MCP_URL. If neither is set, defaults to
  https://arca.cadencia.com.ar/.
EOF
}

for command in curl python3; do
  if ! command -v "$command" >/dev/null 2>&1; then
    echo "ERROR: $command is required." >&2
    exit 127
  fi
done

url="${ARCA_MCP_URL:-$DEFAULT_URL}"
args=()
has_config=false

while (($#)); do
  case "$1" in
    --url)
      if (($# < 2)); then
        echo "ERROR: --url requires a value." >&2
        exit 2
      fi
      url="$2"
      shift 2
      ;;
    --url=*)
      url="${1#--url=}"
      shift
      ;;
    --config)
      if (($# < 2)); then
        echo "ERROR: --config requires a value." >&2
        exit 2
      fi
      args+=("$1" "$2")
      has_config=true
      shift 2
      ;;
    --config=*)
      args+=("$1")
      has_config=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      args+=("$1")
      shift
      ;;
  esac
done

url="${url%/}/"
export ARCA_MCP_URL="$url"

if [[ -z "${ARCA_MCP_TOKEN:-}" ]]; then
  if [[ -t 0 ]]; then
    read -r -s -p "ARCA-MCP API key: " ARCA_MCP_TOKEN
    echo
  else
    IFS= read -r ARCA_MCP_TOKEN
  fi
  if [[ -z "$ARCA_MCP_TOKEN" ]]; then
    echo "ERROR: ARCA-MCP API key is required." >&2
    exit 2
  fi
  export ARCA_MCP_TOKEN
fi

if [[ "$has_config" == false ]]; then
  args=(--config "$DEFAULT_CONFIG" "${args[@]}")
fi

config_path=""
for ((i = 0; i < ${#args[@]}; i++)); do
  case "${args[$i]}" in
    --config)
      if ((i + 1 < ${#args[@]})); then
        config_path="${args[$((i + 1))]}"
      fi
      ;;
    --config=*)
      config_path="${args[$i]#--config=}"
      ;;
  esac
done

if [[ -z "$config_path" || ! -f "$config_path" ]]; then
  echo "ERROR: smoke config not found: ${config_path:-<missing>}" >&2
  echo "Create it with:" >&2
  echo "  cp scripts/smoke/fiscal_smoke.example.json scripts/smoke/fiscal_smoke.local.json" >&2
  exit 2
fi

echo "ARCA-MCP endpoint: $ARCA_MCP_URL"
echo "Checking remote health..."
curl -fsS "${ARCA_MCP_URL}health/live" >/dev/null
curl -fsS "${ARCA_MCP_URL}health/ready" >/dev/null
echo "Remote health: OK"
echo "Starting authenticated MCP smoke client..."

# Authentication is intentionally exercised through the real MCP request path.
# fiscal_smoke.py creates an MCP client with Authorization: Bearer $ARCA_MCP_TOKEN,
# diagnoses each configured fiscal context before any emission, and aborts before
# issuing documents if authentication/authorization/context checks fail.
exec bash "$SCRIPT_DIR/fiscal_smoke.sh" "${args[@]}"
