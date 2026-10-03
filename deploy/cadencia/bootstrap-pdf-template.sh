#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARCA_CONTAINER="${ARCA_CONTAINER:-dcarca-mcpserver}"
PDF_BASE_URL="${PDF_BASE_URL:-http://creadorpdf:8080}"
TEMPLATE_NAME="${TEMPLATE_NAME:-factura-ar}"
TEMPLATE_VERSION="${TEMPLATE_VERSION:-3}"
TEMPLATE_FILE="${TEMPLATE_FILE:-$SCRIPT_DIR/pdf-templates/factura-ar-v3.html}"
SCHEMA_FILE="${SCHEMA_FILE:-$SCRIPT_DIR/pdf-templates/factura-ar-v3.schema.json}"

for cmd in docker python3; do
  command -v "$cmd" >/dev/null 2>&1 || {
    echo "Falta dependencia requerida: $cmd" >&2
    exit 1
  }
done

[[ -f "$TEMPLATE_FILE" ]] || { echo "No existe $TEMPLATE_FILE" >&2; exit 1; }
[[ -f "$SCHEMA_FILE" ]] || { echo "No existe $SCHEMA_FILE" >&2; exit 1; }

docker inspect "$ARCA_CONTAINER" >/dev/null 2>&1 || {
  echo "No existe el contenedor $ARCA_CONTAINER" >&2
  exit 1
}

pdf_request() {
  local method="$1"
  local path="$2"
  docker exec -i "$ARCA_CONTAINER" sh -lc '
    set -eu
    key="$(printenv Pdf__ApiKey)"
    [ -n "$key" ] || { echo "Falta Pdf__ApiKey en el contenedor ARCA" >&2; exit 1; }
    method="$1"
    url="$2"
    exec curl -fsS -X "$method" "$url" \
      -H "Authorization: Bearer $key" \
      -H "Content-Type: application/json" \
      --data-binary @-
  ' sh "$method" "$PDF_BASE_URL$path"
}

list_templates() {
  docker exec "$ARCA_CONTAINER" sh -lc '
    set -eu
    key="$(printenv Pdf__ApiKey)"
    [ -n "$key" ] || { echo "Falta Pdf__ApiKey en el contenedor ARCA" >&2; exit 1; }
    exec curl -fsS "$1/v1/templates/managed" -H "Authorization: Bearer $key"
  ' sh "$PDF_BASE_URL"
}

managed="$(list_templates)"
match="$(printf '%s' "$managed" | python3 -c '
import json, sys
name, version = sys.argv[1:3]
items = json.load(sys.stdin)
for item in items:
    if item.get("name") == name and item.get("version") == version:
        print(item.get("id", ""))
        print(item.get("status", ""))
        break
' "$TEMPLATE_NAME" "$TEMPLATE_VERSION")"

template_id="$(printf '%s\n' "$match" | sed -n '1p')"
status="$(printf '%s\n' "$match" | sed -n '2p')"

if [[ -z "$template_id" ]]; then
  payload="$(python3 - "$TEMPLATE_FILE" "$SCHEMA_FILE" "$TEMPLATE_NAME" "$TEMPLATE_VERSION" <<'PY'
import json, pathlib, sys
html_path, schema_path, name, version = sys.argv[1:5]
payload = {
    "name": name,
    "version": version,
    "format": "html",
    "template": pathlib.Path(html_path).read_text(encoding="utf-8"),
    "schema": json.loads(pathlib.Path(schema_path).read_text(encoding="utf-8")),
}
print(json.dumps(payload, ensure_ascii=False))
PY
)"

  created="$(printf '%s' "$payload" | pdf_request POST /v1/templates)"
  template_id="$(printf '%s' "$created" | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')"
  status="draft"
  echo "Creada plantilla $TEMPLATE_NAME:$TEMPLATE_VERSION ($template_id)."
else
  echo "Plantilla existente $TEMPLATE_NAME:$TEMPLATE_VERSION ($template_id), estado=$status."
fi

if [[ "$status" != "published" ]]; then
  printf '{}' | pdf_request POST "/v1/templates/$template_id/publish" >/dev/null
  echo "Plantilla publicada."
fi

echo "TEMPLATE_ID=$template_id"
echo "TEMPLATE_VERSION=$TEMPLATE_VERSION"
