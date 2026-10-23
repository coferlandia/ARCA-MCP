#!/bin/bash
set -euo pipefail

# pdf_local_render.sh - Local PDF rendering harness for ARCA invoice templates
#
# Purpose:
#   - Clone enerone/creadorpdf (main branch) to a temporary directory
#   - Start creadorpdf using Docker Compose
#   - Create temporary admin credentials
#   - Publish invoice template from this repo (v6 or v7)
#   - Render a test PDF using synthetic fixtures
#   - Save PDF to scratchpad for measurement
#
# Usage:
#   ./scripts/dev/pdf_local_render.sh [FIXTURE] [OUTPUT_DIR] [TEMPLATE]
#   Examples:
#     ./scripts/dev/pdf_local_render.sh factura-c-1item "" factura-ar-v7
#     ./scripts/dev/pdf_local_render.sh nota-credito-c-1item /tmp factura-ar-v6
#
# Environment variables:
#   - CREATORPDF_CLONE_DIR: override temp directory for cloned repo (default: scratchpad)
#   - CREATORPDF_BASE_URL: API endpoint (default: http://localhost:8080)
#   - VERBOSE: set to 1 for detailed logging

set -o pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
FIXTURE="${1:-factura-c-1item}"
OUTPUT_DIR="${2:-}"
TEMPLATE="${3:-factura-ar-v6}"

# Derived paths
TEMPLATE_DIR="${REPO_ROOT}/deploy/cadencia/pdf-templates"
FIXTURES_DIR="${SCRIPT_DIR}/fixtures"
CREATORPDF_REPO="${CREATORPDF_CLONE_DIR:-${TMPDIR:-/tmp}/creadorpdf-dev}"
CREATORPDF_BASE_URL="${CREATORPDF_BASE_URL:-http://localhost:8080}"
VERBOSE="${VERBOSE:-0}"

# Output directory: use scratchpad or provided path
if [ -z "${OUTPUT_DIR}" ]; then
    # Try to detect scratchpad from Claude's environment
    if [ -n "${TMPDIR:-}" ]; then
        OUTPUT_DIR="${TMPDIR}"
    else
        OUTPUT_DIR="/tmp"
    fi
fi

# Ensure output directory exists
mkdir -p "${OUTPUT_DIR}"
PDF_OUTPUT="${OUTPUT_DIR}/rendered_$(date +%s).pdf"

log() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $*" >&2
}

error() {
    echo "ERROR: $*" >&2
    exit 1
}

debug() {
    if [ "${VERBOSE}" = "1" ]; then
        log "DEBUG: $*"
    fi
}

check_docker() {
    if ! command -v docker &> /dev/null; then
        error "Docker is not installed or not in PATH"
    fi

    if ! docker ps &> /dev/null; then
        error "Docker daemon is not running or user lacks permissions (try: sudo usermod -aG docker \$USER)"
    fi

    log "✓ Docker is available"
}

check_jq() {
    if ! command -v jq &> /dev/null; then
        error "jq is required for JSON parsing (install with: brew install jq or apt install jq)"
    fi
}

check_git() {
    if ! command -v git &> /dev/null; then
        error "Git is not installed or not in PATH"
    fi
}

clone_creadorpdf() {
    if [ -d "${CREATORPDF_REPO}/.git" ]; then
        log "✓ creadorpdf already cloned at ${CREATORPDF_REPO}"
        cd "${CREATORPDF_REPO}"
        git fetch origin main 2>/dev/null || true
        return 0
    fi

    log "Cloning enerone/creadorpdf from GitHub..."
    if ! git clone --depth 1 --branch main \
        "https://github.com/enerone/creadorpdf.git" \
        "${CREATORPDF_REPO}" 2>&1; then
        error "Failed to clone creadorpdf. Check: git access, branch 'main' exists, network connectivity"
    fi

    log "✓ creadorpdf cloned to ${CREATORPDF_REPO}"
}

start_creadorpdf() {
    # First, check if creadorpdf is already running
    if curl -sf "${CREATORPDF_BASE_URL}/health" > /dev/null 2>&1; then
        log "✓ creadorpdf is already running at ${CREATORPDF_BASE_URL}"
        return 0
    fi

    cd "${CREATORPDF_REPO}"

    # Check for docker-compose or docker compose
    local docker_compose_cmd
    if command -v docker-compose &> /dev/null; then
        docker_compose_cmd="docker-compose"
    elif docker compose version &> /dev/null; then
        docker_compose_cmd="docker compose"
    else
        error "Neither 'docker-compose' nor 'docker compose' found"
    fi

    log "Starting creadorpdf with ${docker_compose_cmd}..."

    # Start services (will be background)
    if ! $docker_compose_cmd up -d 2>&1 | grep -E "(Creating|Created|Starting|Started|already)"; then
        error "Failed to start creadorpdf with docker-compose"
    fi

    log "✓ creadorpdf services started"

    # Get the container name - try common naming patterns
    local container_name
    container_name=$(${docker_compose_cmd} ps --services | head -1)
    if [ -z "${container_name}" ]; then
        container_name="creadorpdf-creadorpdf-1"  # fallback
    fi

    log "Waiting for creadorpdf to be healthy..."
    local max_attempts=30
    local attempt=0

    while [ ${attempt} -lt ${max_attempts} ]; do
        if curl -sf "${CREATORPDF_BASE_URL}/health" > /dev/null 2>&1; then
            log "✓ creadorpdf is healthy"
            return 0
        fi
        attempt=$((attempt + 1))
        sleep 2
    done

    error "creadorpdf did not become healthy after ${max_attempts} attempts (${max_attempts} seconds)"
}

get_admin_token() {
    # Retrieve CREADORPDF_ADMIN_TOKEN from the running container
    # This is the bootstrap/setup token that allows key creation

    log "Retrieving admin token from creadorpdf container..."

    # Find the container - match both "creadorpdf" image name patterns
    local container_id
    container_id=$(docker ps --filter "status=running" -q | while read cid; do
        docker ps --no-trunc --filter "id=$cid" | grep -i creadorpdf | awk '{print $1}' && break
    done)

    if [ -z "${container_id}" ]; then
        # Try a more general approach
        container_id=$(docker ps --filter "status=running" --format "{{.ID}} {{.Image}}" | grep -i creadorpdf | head -1 | awk '{print $1}')
    fi

    if [ -z "${container_id}" ]; then
        error "Could not find a running creadorpdf container"
    fi

    # Try to get admin token from container environment
    local admin_token
    admin_token=$(docker exec "${container_id}" sh -c 'printenv CREADORPDF_ADMIN_TOKEN || echo ""' 2>/dev/null || echo "")

    if [ -z "${admin_token}" ]; then
        # If env var not set, try to get it from the running process or logs
        debug "CREADORPDF_ADMIN_TOKEN not found in environment, trying alternate methods..."
        error "Could not retrieve CREADORPDF_ADMIN_TOKEN from container ${container_id}. Check that the token is configured in the running container."
    fi

    echo "${admin_token}"
}

create_api_key() {
    local admin_token="$1"

    log "Creating temporary API key for template management..."

    local payload=$(cat <<'EOF'
{
  "name": "dcarca-template-dev-$(date +%s)",
  "owner_id": "dcarca",
  "scopes": ["templates:read", "templates:write", "templates:publish", "documents:render"]
}
EOF
    )

    local response
    response=$(curl -sf -X POST \
        "${CREATORPDF_BASE_URL}/v1/admin/api-keys" \
        -H "Authorization: Bearer ${admin_token}" \
        -H "Content-Type: application/json" \
        -d "$(jq -n \
            --arg name "dcarca-template-dev-$(date +%s)" \
            --arg owner "dcarca" \
            '{name: $name, owner_id: $owner, scopes: ["templates:read", "templates:write", "templates:publish", "documents:render"]}')" \
        2>&1 || error "Failed to create API key")

    local api_key_secret
    api_key_secret=$(echo "${response}" | jq -r '.secret // empty')
    local api_key_id
    api_key_id=$(echo "${response}" | jq -r '.id // empty')

    if [ -z "${api_key_secret}" ] || [ -z "${api_key_id}" ]; then
        error "API key creation failed or returned empty secret. Response: ${response}"
    fi

    log "✓ API key created: ${api_key_id} (secret will not be logged)"
    echo "${api_key_secret}|${api_key_id}"
}

publish_template() {
    local api_key="$1"

    log "Publishing ${TEMPLATE} template..."

    # Read template files
    local template_html
    local template_schema

    if [ ! -f "${TEMPLATE_DIR}/${TEMPLATE}.html" ]; then
        error "Template HTML not found at ${TEMPLATE_DIR}/${TEMPLATE}.html"
    fi

    if [ ! -f "${TEMPLATE_DIR}/${TEMPLATE}.schema.json" ]; then
        error "Template schema not found at ${TEMPLATE_DIR}/${TEMPLATE}.schema.json"
    fi

    template_html=$(cat "${TEMPLATE_DIR}/${TEMPLATE}.html")
    template_schema=$(cat "${TEMPLATE_DIR}/${TEMPLATE}.schema.json")

    # Extract version number from template name (e.g., factura-ar-v6 -> v6)
    local version_num
    version_num=$(echo "${TEMPLATE}" | grep -oE 'v[0-9]+' || echo "dev")

    # Create template (POST /v1/templates)
    local create_response
    create_response=$(curl -sf -X POST \
        "${CREATORPDF_BASE_URL}/v1/templates" \
        -H "Authorization: Bearer ${api_key}" \
        -H "Content-Type: application/json" \
        -d "$(jq -n \
            --arg name "factura-ar" \
            --arg version "${version_num}-dev-$(date +%s)" \
            --argjson schema "$(echo "${template_schema}")" \
            --arg template "${template_html}" \
            '{
                name: $name,
                version: $version,
                schema: $schema,
                template: $template
            }')" \
        2>&1 || error "Failed to create template")

    local template_id
    template_id=$(echo "${create_response}" | jq -r '.id // empty')

    if [ -z "${template_id}" ]; then
        error "Template creation failed or returned empty ID. Response: ${create_response}"
    fi

    log "✓ Template created: ${template_id}"

    # Publish template (POST /v1/templates/{id}/publish)
    local publish_response
    publish_response=$(curl -sf -X POST \
        "${CREATORPDF_BASE_URL}/v1/templates/${template_id}/publish" \
        -H "Authorization: Bearer ${api_key}" \
        -H "Content-Type: application/json" \
        -d '{}' \
        2>&1 || error "Failed to publish template")

    log "✓ Template published: ${template_id}"
    echo "${template_id}"
}

render_document() {
    local template_id="$1"
    local api_key="$2"
    local fixture_name="$3"

    log "Rendering PDF with fixture: ${fixture_name}..."

    if [ ! -f "${FIXTURES_DIR}/${fixture_name}.json" ]; then
        error "Fixture not found: ${FIXTURES_DIR}/${fixture_name}.json"
    fi

    local template_data
    template_data=$(cat "${FIXTURES_DIR}/${fixture_name}.json")

    # POST /v1/documents
    local pdf_response
    pdf_response=$(curl -sf -X POST \
        "${CREATORPDF_BASE_URL}/v1/documents" \
        -H "Authorization: Bearer ${api_key}" \
        -H "Content-Type: application/json" \
        -d "$(jq -n \
            --arg template_id "${template_id}" \
            --argjson data "$(echo "${template_data}")" \
            '{
                template: {
                    id: $template_id
                },
                data: $data
            }')" \
        --output "${PDF_OUTPUT}" \
        2>&1)

    if [ ! -f "${PDF_OUTPUT}" ] || [ ! -s "${PDF_OUTPUT}" ]; then
        error "PDF rendering failed. Response: ${pdf_response}"
    fi

    log "✓ PDF rendered: ${PDF_OUTPUT} ($(stat -f%z "${PDF_OUTPUT}" 2>/dev/null || stat -c%s "${PDF_OUTPUT}" 2>/dev/null) bytes)"
}

cleanup() {
    log "Cleanup: temporary API key would be revoked in production"
    log "Scratchpad location: ${OUTPUT_DIR}"
}

# Main execution
main() {
    log "=========================================="
    log "ARCA PDF Local Render Harness (v6 Template)"
    log "=========================================="
    log "Repo root: ${REPO_ROOT}"
    log "Template: ${TEMPLATE_DIR}/${TEMPLATE}.html"
    log "Fixture: ${FIXTURE}"
    log "Output: ${PDF_OUTPUT}"
    log "CreadorPDF URL: ${CREATORPDF_BASE_URL}"
    log ""

    check_docker
    check_git
    check_jq

    clone_creadorpdf
    start_creadorpdf

    local admin_token
    admin_token=$(get_admin_token)
    debug "Admin token obtained (length: ${#admin_token})"

    local api_key_data
    api_key_data=$(create_api_key "${admin_token}")
    local api_key="${api_key_data%|*}"
    local api_key_id="${api_key_data#*|}"
    debug "API key: ${api_key_id}"

    local template_id
    template_id=$(publish_template "${api_key}")

    render_document "${template_id}" "${api_key}" "${FIXTURE}"

    log ""
    log "=========================================="
    log "✓ Render harness completed successfully"
    log "=========================================="
    log "PDF location: ${PDF_OUTPUT}"
    log "Run measurement: python3 scripts/dev/pdf_measure.py ${PDF_OUTPUT}"
    log ""

    cleanup
}

# Run main
main "$@"
