#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BOOTSTRAP="$ROOT_DIR/deploy/cadencia/bootstrap-pdf-template.sh"
SOURCE_TEMPLATES="$ROOT_DIR/deploy/cadencia/pdf-templates"
TMP_ROOT="$(mktemp -d)"
trap 'rm -rf "$TMP_ROOT"' EXIT

FAKE_BIN="$TMP_ROOT/bin"
mkdir -p "$FAKE_BIN"

cat > "$FAKE_BIN/docker" <<'FAKE_DOCKER'
#!/usr/bin/env bash
set -euo pipefail

if [[ "${1:-}" == "inspect" ]]; then
  exit 0
fi

if [[ "${1:-}" != "exec" ]]; then
  echo "fake docker: unsupported command: $*" >&2
  exit 90
fi

joined="$*"

if [[ "$joined" == *"printenv Pdf__ApiKey"* && "$joined" != *"curl -fsS"* ]]; then
  [[ "${FAKE_RUNTIME_KEY_PRESENT:-1}" == "1" ]]
  exit
fi

if [[ "$joined" == *"/v1/templates/managed"* ]]; then
  cat "$FAKE_STATE_FILE"
  exit
fi

method=""
url=""
args=("$@")
for ((i = 0; i < ${#args[@]}; i++)); do
  if [[ "${args[$i]}" == "POST" && $((i + 1)) -lt ${#args[@]} ]]; then
    method="POST"
    url="${args[$((i + 1))]}"
    break
  fi
done

if [[ "$method" != "POST" || -z "$url" ]]; then
  echo "fake docker: unsupported exec: $*" >&2
  exit 91
fi

if [[ "$url" == */v1/templates ]]; then
  payload_file="$(mktemp)"
  cat > "$payload_file"
  printf 'CREATE\n' >> "$FAKE_DOCKER_LOG"
  python3 - "$payload_file" "$FAKE_STATE_FILE" <<'PY'
import json, os, pathlib, sys
payload_path, state_path = map(pathlib.Path, sys.argv[1:3])
payload = json.loads(payload_path.read_text(encoding="utf-8"))
created = {
    "id": os.environ.get("FAKE_CREATED_ID", "tpl_created"),
    "name": payload["name"],
    "version": payload["version"],
    "status": "draft",
    "owner_id": os.environ.get("FAKE_CREATED_OWNER", "dcarca"),
}
if os.environ.get("FAKE_HIDE_CREATED_FROM_RUNTIME") != "1":
    state_path.write_text(json.dumps([created]), encoding="utf-8")
print(json.dumps(created))
PY
  rm -f "$payload_file"
  exit
fi

if [[ "$url" == */publish ]]; then
  template_id="${url%/publish}"
  template_id="${template_id##*/}"
  cat >/dev/null
  printf 'PUBLISH %s\n' "$template_id" >> "$FAKE_DOCKER_LOG"
  python3 - "$FAKE_STATE_FILE" "$template_id" <<'PY'
import json, os, pathlib, sys
state_path = pathlib.Path(sys.argv[1])
template_id = sys.argv[2]
if os.environ.get("FAKE_HIDE_CREATED_FROM_RUNTIME") == "1":
    print("{}")
    raise SystemExit(0)
items = json.loads(state_path.read_text(encoding="utf-8"))
for item in items:
    if item.get("id") == template_id:
        item["status"] = "published"
state_path.write_text(json.dumps(items), encoding="utf-8")
print("{}")
PY
  exit
fi

echo "fake docker: unsupported URL: $url" >&2
exit 92
FAKE_DOCKER
chmod +x "$FAKE_BIN/docker"

export PATH="$FAKE_BIN:$PATH"
export ARCA_CONTAINER="dcarca-mcpserver-test"
export PDF_BASE_URL="http://creadorpdf:8080"

scenario_dir=""
prepare_scenario() {
  local name="$1"
  scenario_dir="$TMP_ROOT/$name"
  mkdir -p "$scenario_dir"
  cp "$SOURCE_TEMPLATES/factura-ar-v3.html" "$scenario_dir/"
  cp "$SOURCE_TEMPLATES/factura-ar-v3.schema.json" "$scenario_dir/"
  cat > "$scenario_dir/manifest.json" <<'JSON'
{
  "version": 1,
  "templates": [
    {
      "key": "factura-ar",
      "version": "3",
      "template": "factura-ar-v3.html",
      "schema": "factura-ar-v3.schema.json"
    }
  ]
}
JSON
  export PDF_TEMPLATE_MANIFEST="$scenario_dir/manifest.json"
  export FAKE_STATE_FILE="$scenario_dir/state.json"
  export FAKE_DOCKER_LOG="$scenario_dir/docker.log"
  printf '[]' > "$FAKE_STATE_FILE"
  : > "$FAKE_DOCKER_LOG"
  unset CREADORPDF_TEMPLATE_API_KEY FAKE_HIDE_CREATED_FROM_RUNTIME FAKE_CREATED_OWNER FAKE_CREATED_ID
}

run_success() {
  local output_file="$1"
  shift
  "$@" >"$output_file" 2>&1
}

run_failure() {
  local output_file="$1"
  shift
  if "$@" >"$output_file" 2>&1; then
    echo "Se esperaba fallo pero el comando terminó exitosamente: $*" >&2
    cat "$output_file" >&2
    exit 1
  fi
}

# Existing published => no-op and no management key required.
prepare_scenario published
cat > "$FAKE_STATE_FILE" <<'JSON'
[{"id":"tpl_existing","name":"factura-ar","version":"3","status":"published","owner_id":"dcarca"}]
JSON
published_output="$scenario_dir/output.txt"
run_success "$published_output" bash "$BOOTSTRAP"
grep -q '^TEMPLATE_KEY=factura-ar$' "$published_output"
grep -q '^TEMPLATE_VERSION=3$' "$published_output"
grep -q '^TEMPLATE_OWNER=dcarca$' "$published_output"
grep -q '^TEMPLATE_STATUS=published$' "$published_output"
[[ ! -s "$FAKE_DOCKER_LOG" ]]

# Existing draft => explicit management credential required.
prepare_scenario draft-no-key
cat > "$FAKE_STATE_FILE" <<'JSON'
[{"id":"tpl_draft","name":"factura-ar","version":"3","status":"draft","owner_id":"dcarca"}]
JSON
draft_no_key_output="$scenario_dir/output.txt"
run_failure "$draft_no_key_output" bash "$BOOTSTRAP"
grep -q 'Falta CREADORPDF_TEMPLATE_API_KEY' "$draft_no_key_output"

# Existing draft + management key => publish, then visible/published.
prepare_scenario draft-publish
cat > "$FAKE_STATE_FILE" <<'JSON'
[{"id":"tpl_draft","name":"factura-ar","version":"3","status":"draft","owner_id":"dcarca"}]
JSON
export CREADORPDF_TEMPLATE_API_KEY="test-management-secret"
draft_output="$scenario_dir/output.txt"
run_success "$draft_output" bash "$BOOTSTRAP"
grep -q '^PUBLISH tpl_draft$' "$FAKE_DOCKER_LOG"
grep -q '^TEMPLATE_STATUS=published$' "$draft_output"
! grep -q 'test-management-secret' "$draft_output"

# Missing version => create + publish exactly once; second run is a no-op without management key.
prepare_scenario create-idempotent
export CREADORPDF_TEMPLATE_API_KEY="test-management-secret"
first_output="$scenario_dir/first.txt"
run_success "$first_output" bash "$BOOTSTRAP"
[[ "$(grep -c '^CREATE$' "$FAKE_DOCKER_LOG")" -eq 1 ]]
[[ "$(grep -c '^PUBLISH tpl_created$' "$FAKE_DOCKER_LOG")" -eq 1 ]]
unset CREADORPDF_TEMPLATE_API_KEY
second_output="$scenario_dir/second.txt"
run_success "$second_output" bash "$BOOTSTRAP"
[[ "$(grep -c '^CREATE$' "$FAKE_DOCKER_LOG")" -eq 1 ]]
[[ "$(grep -c '^PUBLISH tpl_created$' "$FAKE_DOCKER_LOG")" -eq 1 ]]
grep -q '^TEMPLATE_PROVIDER_ID=tpl_created  # diagnóstico solamente; no persistir en SecretarIA$' "$second_output"

# Management credential from another owner => created resource is invisible to runtime key and bootstrap fails closed.
prepare_scenario wrong-owner
export CREADORPDF_TEMPLATE_API_KEY="other-owner-secret"
export FAKE_CREATED_OWNER="other-owner"
export FAKE_HIDE_CREATED_FROM_RUNTIME=1
wrong_owner_output="$scenario_dir/output.txt"
run_failure "$wrong_owner_output" bash "$BOOTSTRAP"
grep -q 'no es visible de forma unívoca con la key runtime' "$wrong_owner_output"
! grep -q 'other-owner-secret' "$wrong_owner_output"

# Missing artifact => fail fast before create/publish.
prepare_scenario missing-file
python3 - "$PDF_TEMPLATE_MANIFEST" <<'PY'
import json, pathlib, sys
path = pathlib.Path(sys.argv[1])
manifest = json.loads(path.read_text(encoding="utf-8"))
manifest["templates"][0]["template"] = "missing.html"
path.write_text(json.dumps(manifest), encoding="utf-8")
PY
missing_output="$scenario_dir/output.txt"
run_failure "$missing_output" bash "$BOOTSTRAP"
grep -q 'No existe .*missing.html' "$missing_output"
[[ ! -s "$FAKE_DOCKER_LOG" ]]

echo "bootstrap-pdf-template tests: OK"
