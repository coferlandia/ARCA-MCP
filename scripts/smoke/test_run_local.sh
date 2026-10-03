#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
RUNNER="$SCRIPT_DIR/run-local.sh"

if [[ ! -f "$RUNNER" ]]; then
  echo "Expected $RUNNER to exist" >&2
  exit 1
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/bin" "$tmp/capture"
config="$tmp/smoke.json"
printf '{}\n' > "$config"

cat > "$tmp/bin/curl" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "$SMOKE_CAPTURE/curl.log"
printf '{"status":"Healthy"}\n'
EOF
chmod +x "$tmp/bin/curl"

cat > "$tmp/bin/python3" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "${ARCA_MCP_URL:-}" > "$SMOKE_CAPTURE/url"
printf '%s\n' "${ARCA_MCP_TOKEN:-}" > "$SMOKE_CAPTURE/token"
printf '%s\n' "$@" > "$SMOKE_CAPTURE/python.args"
EOF
chmod +x "$tmp/bin/python3"

export SMOKE_CAPTURE="$tmp/capture"
export PATH="$tmp/bin:$PATH"
export ARCA_MCP_URL="https://arca.example.test/base/"
export ARCA_MCP_TOKEN="dummy-secret"

bash "$RUNNER" --config "$config" --execute --allow-production

grep -q '^https://arca.example.test/base/$' "$tmp/capture/url"
grep -q '^dummy-secret$' "$tmp/capture/token"
grep -q 'health/live' "$tmp/capture/curl.log"
grep -q 'health/ready' "$tmp/capture/curl.log"
grep -q -- '--config' "$tmp/capture/python.args"
grep -q -- '--execute' "$tmp/capture/python.args"
grep -q -- '--allow-production' "$tmp/capture/python.args"

rm -f "$tmp/capture/token"
unset ARCA_MCP_TOKEN
printf 'prompt-secret\n' | bash "$RUNNER" --url "https://arca.prompt.test" --config "$config" --execute

grep -q '^https://arca.prompt.test/$' "$tmp/capture/url"
grep -q '^prompt-secret$' "$tmp/capture/token"

printf 'OK: local external smoke client contract\n'
