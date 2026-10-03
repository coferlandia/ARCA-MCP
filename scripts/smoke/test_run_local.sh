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

# Simulate Git Bash on Windows where python3 exists as a broken Microsoft Store alias.
cat > "$tmp/bin/python3" <<'EOF'
#!/usr/bin/env bash
exit 127
EOF
chmod +x "$tmp/bin/python3"

# Usable `python` must be selected after the broken python3 probe.
cat > "$tmp/bin/python" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
if [[ "${1:-}" == "-c" ]]; then
  exit 0
fi
printf '%s\n' "${ARCA_MCP_URL:-}" > "$SMOKE_CAPTURE/url"
printf '%s\n' "${ARCA_MCP_TOKEN:-}" > "$SMOKE_CAPTURE/token"
printf '%s\n' "$@" > "$SMOKE_CAPTURE/python.args"
printf '%s\n' "python" > "$SMOKE_CAPTURE/python.launcher"
EOF
chmod +x "$tmp/bin/python"

export SMOKE_CAPTURE="$tmp/capture"
export PATH="$tmp/bin:$PATH"
export ARCA_MCP_URL="https://arca.example.test/base/"
export ARCA_MCP_TOKEN="dummy-secret"

bash "$RUNNER" --config "$config" --execute --allow-production

grep -q '^https://arca.example.test/base/$' "$tmp/capture/url"
grep -q '^dummy-secret$' "$tmp/capture/token"
grep -q '^python$' "$tmp/capture/python.launcher"
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

export ARCA_MCP_TOKEN="dummy-secret"
if bash "$RUNNER" --url "http://remote.example.test" --config "$config" --execute >"$tmp/insecure.out" 2>&1; then
  echo "Expected insecure remote URL to be rejected" >&2
  exit 1
fi
grep -q 'HTTPS is required' "$tmp/insecure.out"

bash "$RUNNER" --url "http://127.0.0.1:8080" --config "$config" --execute
grep -q '^http://127.0.0.1:8080/$' "$tmp/capture/url"

printf 'OK: local external smoke client contract\n'
