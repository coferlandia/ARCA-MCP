#!/bin/bash
set -euo pipefail

# Simplified PDF render harness using /v1/previews endpoint
# (no need to create API keys or publish templates)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
FIXTURE="${1:-factura-c-1item}"
OUTPUT_DIR="${2:-${TMPDIR:-/tmp}}"

TEMPLATE_DIR="${REPO_ROOT}/deploy/cadencia/pdf-templates"
FIXTURES_DIR="${SCRIPT_DIR}/fixtures"
CREATORPDF_BASE_URL="${CREATORPDF_BASE_URL:-http://localhost:8080}"

mkdir -p "${OUTPUT_DIR}"
PDF_OUTPUT="${OUTPUT_DIR}/rendered_$(date +%s).pdf"

log() {
    echo "[$(date +'%H:%M:%S')] $*" >&2
}

log "=========================================="
log "ARCA PDF Local Render (Preview Endpoint)"
log "=========================================="
log "Template: factura-ar-v6.html"
log "Fixture: ${FIXTURE}"
log "Output: ${PDF_OUTPUT}"
log "CreadorPDF: ${CREATORPDF_BASE_URL}"
log ""

# Check creadorpdf health
if ! curl -sf "${CREATORPDF_BASE_URL}/health" > /dev/null; then
    echo "ERROR: CreadorPDF not healthy at ${CREATORPDF_BASE_URL}" >&2
    exit 1
fi
log "✓ CreadorPDF is healthy"

# Load files
template_html=$(cat "${TEMPLATE_DIR}/factura-ar-v6.html")
template_schema=$(cat "${TEMPLATE_DIR}/factura-ar-v6.schema.json")
template_data=$(cat "${FIXTURES_DIR}/${FIXTURE}.json")

log "✓ Files loaded"

# Render using /v1/previews (no authentication needed)
log "Rendering PDF..."

if ! curl -sf -X POST \
    "${CREATORPDF_BASE_URL}/v1/previews" \
    -H "Content-Type: application/json" \
    -d "$(jq -n \
        --argjson schema "$(echo "${template_schema}")" \
        --arg template "${template_html}" \
        --argjson data "$(echo "${template_data}")" \
        '{
            schema: $schema,
            template: $template,
            data: $data
        }')" \
    --output "${PDF_OUTPUT}" 2>&1; then
    echo "ERROR: Render failed" >&2
    exit 1
fi

if [ ! -s "${PDF_OUTPUT}" ]; then
    echo "ERROR: Output file is empty" >&2
    exit 1
fi

log "✓ PDF rendered successfully"
log ""
log "=========================================="
log "Output: ${PDF_OUTPUT}"
log "Size: $(stat -f%z "${PDF_OUTPUT}" 2>/dev/null || stat -c%s "${PDF_OUTPUT}" 2>/dev/null) bytes"
log ""
log "Measure it with:"
log "  python3 scripts/dev/pdf_measure.py \"${PDF_OUTPUT}\""
log "=========================================="
