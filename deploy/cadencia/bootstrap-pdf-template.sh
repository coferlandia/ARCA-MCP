#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARCA_CONTAINER="${ARCA_CONTAINER:-dcarca-mcpserver}"
PDF_BASE_URL="${PDF_BASE_URL:-http://creadorpdf:8080}"
TEMPLATE_NAME="${TEMPLATE_NAME:-factura-ar}"
TEMPLATE_VERSION="${TEMPLATE_VERSION:-3}"
TEMPLATE_FILE="${TEMPLATE_FILE:-$SCRIPT_DIR/pdf-templates/factura-ar-v3.html}"
SCHEMA_FILE="${SCHEMA_FILE:-$SCRIPT_DIR/pdf-templates/factura-ar-v3.schema.json}"
TEMPLATE_API_KEY="${CREADORPDF_TEMPLATE_API_KEY:-}"

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

runtime_request() {
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

template_request() {
  local method="$1"
  local path="$2"

  [[ -n "$TEMPLATE_API_KEY" ]] || {
    echo "Falta CREADORPDF_TEMPLATE_API_KEY." >&2
    echo "La key runtime de ARCA sólo debe renderizar/listar; el bootstrap requiere una key de gestión de templates del mismo owner." >&2
    exit 1
  }

  docker exec -i \
    -e CREADORPDF_TEMPLATE_API_KEY="$TEMPLATE_API_KEY" \
    "$ARCA_CONTAINER" sh -lc '
      set -eu
      key="$(printenv CREADORPDF_TEMPLATE_API_KEY)"
      method="$1"
      url="$2"
      exec curl -fsS -X "$method" "$url" \
        -H "Authorization: Bearer $key" \
        -H "Content-Type: application/json" \
        --data-binary @-
    ' sh "$method" "$PDF_BASE_URL$path"
}

list_runtime_templates() {
  docker exec "$ARCA_CONTAINER" sh -lc '
    set -eu
    key="$(printenv Pdf__ApiKey)"
    [ -n "$key" ] || { echo "Falta Pdf__ApiKey en el contenedor ARCA" >&2; exit 1; }
    exec curl -fsS "$1/v1/templates/managed" -H "Authorization: Bearer $key"
  ' sh "$PDF_BASE_URL"
}

find_template() {
  python3 -c '
import json, sys
name, version = sys.argv[1:3]
items = json.load(sys.stdin)
for item in items:
    if item.get("name") == name and item.get("version") == version:
        print(item.get("id", ""))
        print(item.get("status", ""))
        print(item.get("owner_id", ""))
        break
' "$TEMPLATE_NAME" "$TEMPLATE_VERSION"
}

managed="$(list_runtime_templates)"
match="$(printf '%s' "$managed" | find_template)"
template_id="$(printf '%s\n' "$match" | sed -n '1p')"
status="$(printf '%s\n' "$match" | sed -n '2p')"
owner_id="$(printf '%s\n' "$match" | sed -n '3p')"

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

  created="$(printf '%s' "$payload" | template_request POST /v1/templates)"
  template_id="$(printf '%s' "$created" | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')"
  owner_id="$(printf '%s' "$created" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("owner_id", ""))')"
  status="draft"
  echo "Creada plantilla $TEMPLATE_NAME:$TEMPLATE_VERSION ($template_id), owner=$owner_id."
else
  echo "Plantilla existente $TEMPLATE_NAME:$TEMPLATE_VERSION ($template_id), estado=$status, owner=$owner_id."
fi

if [[ "$status" != "published" ]]; then
  printf '{}' | template_request POST "/v1/templates/$template_id/publish" >/dev/null
  echo "Plantilla publicada."
fi

# Verificación final con la key runtime de ARCA. Si la key de gestión pertenece a
# otro owner, el template puede haberse creado correctamente pero ARCA no podrá usarlo.
managed="$(list_runtime_templates)"
match="$(printf '%s' "$managed" | find_template)"
visible_id="$(printf '%s\n' "$match" | sed -n '1p')"
visible_status="$(printf '%s\n' "$match" | sed -n '2p')"
visible_owner="$(printf '%s\n' "$match" | sed -n '3p')"

if [[ -z "$visible_id" ]]; then
  echo "La plantilla fue gestionada, pero no es visible con la key runtime de ARCA." >&2
  echo "CREADORPDF_TEMPLATE_API_KEY debe pertenecer al mismo owner que Pdf__ApiKey (actualmente dcarca)." >&2
  exit 1
fi

if [[ "$visible_status" != "published" ]]; then
  echo "La plantilla visible para ARCA no quedó publicada (estado=$visible_status)." >&2
  exit 1
fi

echo "TEMPLATE_ID=$visible_id"
echo "TEMPLATE_VERSION=$TEMPLATE_VERSION"
echo "TEMPLATE_OWNER=$visible_owner"
