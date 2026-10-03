#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
RUNNER="$SCRIPT_DIR/manage-key.sh"

if [[ ! -f "$RUNNER" ]]; then
  echo "Expected $RUNNER to exist" >&2
  exit 1
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/bin" "$tmp/capture" "$tmp/store"
export API_KEY_CAPTURE="$tmp/capture"

cat > "$tmp/bin/dotnet" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
if [[ "${1:-}" == "--version" ]]; then
  printf '10.0.100\n'
  exit 0
fi
printf '%s\n' "${ApiKeys__Directory:-}" > "$API_KEY_CAPTURE/directory"
printf '%s\n' "$@" > "$API_KEY_CAPTURE/dotnet.args"
printf '{"Id":"key_test","Secret":"sk-arca-test"}\n'
EOF
chmod +x "$tmp/bin/dotnet"

cat > "$tmp/bin/docker" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$@" > "$API_KEY_CAPTURE/docker.args"
printf '{"Id":"key_docker","Secret":"sk-arca-docker"}\n'
EOF
chmod +x "$tmp/bin/docker"

original_path="$PATH"
export PATH="$tmp/bin:$PATH"

create_output="$(bash "$RUNNER" create \
  --directory "$tmp/store" \
  --name diego-smoke \
  --consumer cadencia \
  --context cadencia-arca-homologacion \
  --mode smoke)"

grep -q '^sk-arca-test' <<<"$(printf '%s' "$create_output" | grep -o 'sk-arca-test')"
grep -q "^$tmp/store$" "$tmp/capture/directory"
grep -q -- '^run$' "$tmp/capture/dotnet.args"
grep -q -- '--project' "$tmp/capture/dotnet.args"
grep -q -- 'dcArca.Cli' "$tmp/capture/dotnet.args"
grep -q -- 'create-key' "$tmp/capture/dotnet.args"
grep -q -- 'diego-smoke' "$tmp/capture/dotnet.args"
grep -q -- 'arca:consultar,arca:facturar' "$tmp/capture/dotnet.args"
grep -q -- 'cadencia' "$tmp/capture/dotnet.args"
grep -q -- 'cadencia-arca-homologacion:consultar,cadencia-arca-homologacion:facturar' "$tmp/capture/dotnet.args"

bash "$RUNNER" list --directory "$tmp/store" >/dev/null
grep -q -- 'list-keys' "$tmp/capture/dotnet.args"

bash "$RUNNER" revoke --directory "$tmp/store" --id key_test >/dev/null
grep -q -- 'revoke-key' "$tmp/capture/dotnet.args"
grep -q -- 'key_test' "$tmp/capture/dotnet.args"

# Explicit Docker backend must use the SDK image, mount the selected store at /data,
# and point the CLI to /data rather than the host path.
bash "$RUNNER" create \
  --backend docker \
  --directory "$tmp/store" \
  --name docker-smoke \
  --consumer cadencia \
  --context cadencia-arca-homologacion \
  --mode smoke >/dev/null

grep -q -- 'mcr.microsoft.com/dotnet/sdk:10.0' "$tmp/capture/docker.args"
grep -q -- "$tmp/store:/data" "$tmp/capture/docker.args"
grep -q -- 'ApiKeys__Directory=/data' "$tmp/capture/docker.args"
grep -q -- 'create-key' "$tmp/capture/docker.args"

# A consumer-bound key must fail closed when no context/grant is supplied.
if bash "$RUNNER" create \
  --directory "$tmp/store" \
  --name invalid \
  --consumer cadencia \
  --mode smoke >"$tmp/invalid.out" 2>&1; then
  echo "Expected missing --context to fail" >&2
  exit 1
fi
grep -q -- '--context is required' "$tmp/invalid.out"

# Unknown modes must not silently broaden permissions.
if bash "$RUNNER" create \
  --directory "$tmp/store" \
  --name invalid-mode \
  --consumer cadencia \
  --context cadencia-arca-homologacion \
  --mode everything >"$tmp/mode.out" 2>&1; then
  echo "Expected unknown --mode to fail" >&2
  exit 1
fi
grep -q 'Unsupported mode' "$tmp/mode.out"

export PATH="$original_path"
printf 'OK: API key management wrapper contract\n'
