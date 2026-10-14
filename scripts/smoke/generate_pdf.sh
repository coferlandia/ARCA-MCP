#!/usr/bin/env bash
set -euo pipefail

# ============================================================
# Smoke #28 - referencia lógica de template PDF
# Uso:
#
#   ARCA_MCP_KEY='sk-arca-750d5e0ed73325034669faccd7e95113652c223218b10afa977f3c2b4a4ff56f' \
#   ./scripts/smoke/generate_pdf.sh
#
# Opcionales:
#   ARCA_MCP_URL=https://arca.cadencia.com.ar
#   CONTEXT_ID=cadencia-arca-homologacion
#   REPRESENTED_CUIT=...
#   POINT_OF_SALE=...
#   INVOICE_NUMBER=1
#   INVOICE_TYPE=FacturaC
# ============================================================

ARCA_MCP_URL="${ARCA_MCP_URL:-https://arca.cadencia.com.ar}"
ARCA_MCP_KEY="${ARCA_MCP_KEY:-}"

CONTEXT_ID="${CONTEXT_ID:-cadencia-arca-homologacion}"

# Completar estos valores si la tool V2 los requiere en tu deployment.
REPRESENTED_CUIT="${REPRESENTED_CUIT:-20236438989}"
POINT_OF_SALE="${POINT_OF_SALE:-77}"

INVOICE_NUMBER="${INVOICE_NUMBER:-1}"
INVOICE_TYPE="${INVOICE_TYPE:-FacturaC}"

TEMPLATE_KEY="factura-ar"
TEMPLATE_VERSION="3"

die() {
  echo "ERROR: $*" >&2
  exit 1
}

command -v curl >/dev/null 2>&1 || die "curl no está instalado"

if command -v jq >/dev/null 2>&1; then
  HAVE_JQ=1
else
  HAVE_JQ=0
fi

[ -n "$ARCA_MCP_KEY" ] || {
  read -rsp "ARCA MCP API key (sk-arca-...): " ARCA_MCP_KEY
  echo
}

[[ "$ARCA_MCP_KEY" == sk-arca-* ]] || {
  echo "ADVERTENCIA: la key no comienza con sk-arca-"
}

AUTH_HEADER="Authorization: Bearer $ARCA_MCP_KEY"

echo
echo "============================================================"
echo "Smoke #28"
echo "============================================================"
echo "URL              : $ARCA_MCP_URL"
echo "Context          : $CONTEXT_ID"
echo "Comprobante      : $INVOICE_TYPE #$INVOICE_NUMBER"
echo "Template lógico  : $TEMPLATE_KEY:$TEMPLATE_VERSION"
echo "API key          : [oculta]"
echo "============================================================"
echo

echo "=== 1. Health /live ==="

HTTP_CODE="$(
  curl -sS \
    -o /tmp/arca-health-live.json \
    -w '%{http_code}' \
    "$ARCA_MCP_URL/health/live"
)"

cat /tmp/arca-health-live.json || true
echo

[ "$HTTP_CODE" = "200" ] || \
  die "/health/live devolvió HTTP $HTTP_CODE"

echo "OK: proceso vivo"
echo

echo "=== 2. Health /ready ==="

HTTP_CODE="$(
  curl -sS \
    -o /tmp/arca-health-ready.json \
    -w '%{http_code}' \
    "$ARCA_MCP_URL/health/ready"
)"

cat /tmp/arca-health-ready.json || true
echo

[ "$HTTP_CODE" = "200" ] || \
  die "/health/ready devolvió HTTP $HTTP_CODE"

echo "OK: MCP ready"
echo

echo "=== 3. Verificar autenticación y acceso de consulta ==="

CAP_ARGS="\"contextId\":\"$CONTEXT_ID\""

if [ -n "$REPRESENTED_CUIT" ]; then
  CAP_ARGS="$CAP_ARGS,\"representedCuit\":\"$REPRESENTED_CUIT\""
fi

if [ -n "$POINT_OF_SALE" ]; then
  CAP_ARGS="$CAP_ARGS,\"pointOfSale\":$POINT_OF_SALE"
fi

CAP_PAYLOAD=$(cat <<JSON
{
  "jsonrpc": "2.0",
  "id": 2800,
  "method": "tools/call",
  "params": {
    "name": "obtener_capacidades_fiscales",
    "arguments": {
      $CAP_ARGS
    }
  }
}
JSON
)

CAP_RESPONSE="$(
  curl -sS -N "$ARCA_MCP_URL/" \
    -H "$AUTH_HEADER" \
    -H "Content-Type: application/json" \
    -H "Accept: application/json, text/event-stream" \
    --data "$CAP_PAYLOAD"
)"

echo "$CAP_RESPONSE"

if echo "$CAP_RESPONSE" | grep -qiE \
  '"isError"[[:space:]]*:[[:space:]]*true|401|unauthorized|forbidden'; then
  die "La API key no pudo ejecutar una operación de consulta"
fi

echo
echo "OK: API key aceptada"
echo

echo "=== 4. Ejecutar generar_pdf_comprobante ==="
echo "IMPORTANTE: se usa '$TEMPLATE_KEY', nunca un tpl_*"
echo

EXTRA_ARGS=""

if [ -n "$REPRESENTED_CUIT" ]; then
  EXTRA_ARGS="$EXTRA_ARGS,
        \"representedCuit\":\"$REPRESENTED_CUIT\""
fi

if [ -n "$POINT_OF_SALE" ]; then
  EXTRA_ARGS="$EXTRA_ARGS,
        \"pointOfSale\":$POINT_OF_SALE"
fi

PAYLOAD=$(cat <<JSON
{
  "jsonrpc": "2.0",
  "id": 2801,
  "method": "tools/call",
  "params": {
    "name": "generar_pdf_comprobante",
    "arguments": {
      "contextId": "$CONTEXT_ID"$EXTRA_ARGS,
      "numeroComprobante": $INVOICE_NUMBER,
      "tipoComprobante": "$INVOICE_TYPE",
      "templateId": "$TEMPLATE_KEY",
      "templateVersion": "$TEMPLATE_VERSION",
      "templateData": {
        "emisor": {
          "razonSocial": "Prueba Homologacion",
          "domicilio": "Domicilio de prueba"
        },
        "receptor": {
          "razonSocial": "Consumidor Final",
          "domicilio": null
        },
        "conceptos": [
          {
            "descripcion": "Producto de prueba",
            "cantidad": 1,
            "precioUnitario": 100,
            "importe": 100
          }
        ]
      }
    }
  }
}
JSON
)

RESPONSE_FILE="/tmp/arca-issue-28-response.txt"

curl -sS -N "$ARCA_MCP_URL/" \
  -H "$AUTH_HEADER" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  --data "$PAYLOAD" \
  | tee "$RESPONSE_FILE"

echo
echo
echo "=== 5. Evaluación automática ==="

if grep -q '"templateId"[[:space:]]*:[[:space:]]*"tpl_' <<< "$PAYLOAD"; then
  die "El smoke envió por error un tpl_*"
fi

echo "OK: se envió referencia lógica '$TEMPLATE_KEY:$TEMPLATE_VERSION'"

if grep -qi \
  '"isError"[[:space:]]*:[[:space:]]*true' \
  "$RESPONSE_FILE"; then
  echo
  echo "FAIL: MCP devolvió isError=true"
  exit 2
fi

if grep -qiE \
  '"status"[[:space:]]*:[[:space:]]*"Rendered"|"Status"[[:space:]]*:[[:space:]]*"Rendered"' \
  "$RESPONSE_FILE"; then
  echo "OK: PDF status = Rendered"
else
  echo "FAIL: no encontré Pdf.Status=Rendered"
  exit 3
fi

if grep -qiE \
  '"success"[[:space:]]*:[[:space:]]*true|"Success"[[:space:]]*:[[:space:]]*true' \
  "$RESPONSE_FILE"; then
  echo "OK: resultado fiscal exitoso"
else
  echo "ADVERTENCIA: no pude confirmar automáticamente Fiscal.Success=true"
  echo "Revisá la respuesta completa de arriba."
fi

if grep -qiE \
  '"base64"[[:space:]]*:[[:space:]]*"[A-Za-z0-9+/]' \
  "$RESPONSE_FILE"; then
  echo "OK: respuesta contiene PDF Base64"
else
  echo "ADVERTENCIA: no encontré Base64 del PDF automáticamente"
fi

echo
echo "============================================================"
echo "SMOKE #28 COMPLETADO"
echo "============================================================"
echo
echo "Gate esperado:"
echo "  templateId      = factura-ar"
echo "  templateVersion = 3"
echo "  Fiscal.Success  = true"
echo "  Pdf.Status      = Rendered"
echo
echo "Respuesta guardada en:"
echo "  $RESPONSE_FILE"
echo