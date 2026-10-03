#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARCA_CONTAINER="${ARCA_CONTAINER:-dcarca-mcpserver}"
PDF_BASE_URL="${PDF_BASE_URL:-http://creadorpdf:8080}"
MANIFEST_FILE="${PDF_TEMPLATE_MANIFEST:-$SCRIPT_DIR/pdf-templates/manifest.json}"
TEMPLATE_API_KEY="${CREADORPDF_TEMPLATE_API_KEY:-}"

command -v python3 >/dev/null 2>&1 || {
  echo "Falta dependencia requerida: python3" >&2
  exit 1
}

[[ -f "$MANIFEST_FILE" ]] || { echo "No existe $MANIFEST_FILE" >&2; exit 1; }

runtime_api_key_available() {
  docker exec "$ARCA_CONTAINER" sh -lc '
    key="$(printenv Pdf__ApiKey 2>/dev/null || true)"
    [ -n "$key" ]
  '
}

template_request() {
  local method="$1"
  local path="$2"

  [[ -n "$TEMPLATE_API_KEY" ]] || {
    echo "Falta CREADORPDF_TEMPLATE_API_KEY." >&2
    echo "La key runtime de ARCA sólo debe listar/renderizar; crear o publicar requiere una key de gestión temporal del mismo owner." >&2
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
    exec curl -fsS "$1/v1/templates/managed" -H "Authorization: Bearer $key"
  ' sh "$PDF_BASE_URL"
}

manifest_entries() {
  python3 - "$MANIFEST_FILE" <<'PY'
import json, pathlib, sys
path = pathlib.Path(sys.argv[1])
try:
    manifest = json.loads(path.read_text(encoding="utf-8"))
except Exception as exc:
    raise SystemExit(f"Manifest inválido: {exc}")
if not isinstance(manifest, dict):
    raise SystemExit("Manifest inválido: la raíz debe ser un objeto")
if manifest.get("version") != 1:
    raise SystemExit("Manifest inválido: version debe ser 1")
items = manifest.get("templates")
if not isinstance(items, list) or not items:
    raise SystemExit("Manifest inválido: templates debe ser una lista no vacía")
seen = set()
rows = []
for index, item in enumerate(items):
    if not isinstance(item, dict):
        raise SystemExit(f"Manifest inválido: templates[{index}] debe ser objeto")
    values = [item.get("key"), item.get("version"), item.get("template"), item.get("schema")]
    if any(not isinstance(value, str) or not value.strip() for value in values):
        raise SystemExit(f"Manifest inválido: templates[{index}] requiere key/version/template/schema")
    key, version, template, schema = (value.strip() for value in values)
    logical = (key, version)
    if logical in seen:
        raise SystemExit(f"Manifest inválido: referencia duplicada {key}:{version}")
    seen.add(logical)
    if any("\t" in value or "\n" in value for value in (key, version, template, schema)):
        raise SystemExit("Manifest inválido: no se permiten tabs/newlines en los campos")
    rows.append("\t".join((key, version, template, schema)))
print("\n".join(rows))
PY
}

find_template() {
  local key="$1"
  local version="$2"
  python3 -c '
import json, sys
key, version = sys.argv[1:3]
items = json.load(sys.stdin)
if isinstance(items, dict):
    items = items.get("items", [])
matches = [item for item in items if item.get("name") == key and item.get("version") == version]
print(len(matches))
for item in matches:
    print(item.get("id", ""))
    print(item.get("status", ""))
    print(item.get("owner_id", ""))
' "$key" "$version"
}

if ! manifest_data="$(manifest_entries)"; then
  exit 1
fi

manifest_dir="$(cd "$(dirname "$MANIFEST_FILE")" && pwd)"

# Validar todos los assets antes de cualquier acceso a Docker/provider para evitar
# aplicar parcialmente un manifest cuya inconsistencia era detectable localmente.
while IFS=$'\t' read -r template_key template_version template_rel schema_rel; do
  [[ -n "$template_key" ]] || continue
  template_file="$manifest_dir/$template_rel"
  schema_file="$manifest_dir/$schema_rel"
  [[ -f "$template_file" ]] || { echo "No existe $template_file" >&2; exit 1; }
  [[ -f "$schema_file" ]] || { echo "No existe $schema_file" >&2; exit 1; }
done <<< "$manifest_data"

command -v docker >/dev/null 2>&1 || {
  echo "Falta dependencia requerida: docker" >&2
  exit 1
}

docker inspect "$ARCA_CONTAINER" >/dev/null 2>&1 || {
  echo "No existe el contenedor $ARCA_CONTAINER" >&2
  exit 1
}

runtime_api_key_available || {
  echo "Falta Pdf__ApiKey en el contenedor ARCA." >&2
  exit 1
}

while IFS=$'\t' read -r template_key template_version template_rel schema_rel; do
  [[ -n "$template_key" ]] || continue

  template_file="$manifest_dir/$template_rel"
  schema_file="$manifest_dir/$schema_rel"

  managed="$(list_runtime_templates)"
  match="$(printf '%s' "$managed" | find_template "$template_key" "$template_version")"
  match_count="$(printf '%s\n' "$match" | sed -n '1p')"

  if [[ "$match_count" -gt 1 ]]; then
    echo "Hay más de una plantilla visible para $template_key:$template_version; se aborta para evitar ambigüedad." >&2
    exit 1
  fi

  template_id=""
  status=""
  owner_id=""
  if [[ "$match_count" -eq 1 ]]; then
    template_id="$(printf '%s\n' "$match" | sed -n '2p')"
    status="$(printf '%s\n' "$match" | sed -n '3p')"
    owner_id="$(printf '%s\n' "$match" | sed -n '4p')"
    echo "Plantilla existente $template_key:$template_version, estado=$status, owner=$owner_id."
  else
    payload="$(python3 - "$template_file" "$schema_file" "$template_key" "$template_version" <<'PY'
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
    echo "Creada plantilla $template_key:$template_version, owner=$owner_id."
  fi

  if [[ "$status" != "published" ]]; then
    printf '{}' | template_request POST "/v1/templates/$template_id/publish" >/dev/null
    echo "Plantilla $template_key:$template_version publicada."
  fi

  # La versión publicada es inmutable. El bootstrap nunca sobrescribe HTML/schema
  # de una referencia key+version existente. Un cambio local requiere bump de version.
  managed="$(list_runtime_templates)"
  match="$(printf '%s' "$managed" | find_template "$template_key" "$template_version")"
  visible_count="$(printf '%s\n' "$match" | sed -n '1p')"
  visible_id="$(printf '%s\n' "$match" | sed -n '2p')"
  visible_status="$(printf '%s\n' "$match" | sed -n '3p')"
  visible_owner="$(printf '%s\n' "$match" | sed -n '4p')"

  if [[ "$visible_count" -ne 1 || -z "$visible_id" ]]; then
    echo "La plantilla $template_key:$template_version no es visible de forma unívoca con la key runtime de ARCA." >&2
    echo "La key de gestión debe pertenecer al mismo owner que Pdf__ApiKey." >&2
    exit 1
  fi

  if [[ "$visible_status" != "published" ]]; then
    echo "La plantilla visible para ARCA no quedó publicada (estado=$visible_status)." >&2
    exit 1
  fi

  echo "TEMPLATE_KEY=$template_key"
  echo "TEMPLATE_VERSION=$template_version"
  echo "TEMPLATE_OWNER=$visible_owner"
  echo "TEMPLATE_STATUS=$visible_status"
  echo "TEMPLATE_PROVIDER_ID=$visible_id  # diagnóstico solamente; no persistir en SecretarIA"
done <<< "$manifest_data"
