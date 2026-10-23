# PDF Local Render Harness

Local rendering and measurement harness for ARCA invoice templates (factura-ar).

This harness enables reproducible local PDF rendering against the same WeasyPrint stack used in production, allowing measurement of actual page geometry to confirm or refute reported visual defects.

## Purpose

The harness automates the following workflow:

1. **Clone and start creadorpdf locally** — Spins up a local instance of `enerone/creadorpdf` (main branch) using Docker Compose, the same renderer used in production.
2. **Publish template** — Uploads the template HTML and JSON Schema from this repository (without modification) to the local creadorpdf instance.
3. **Render with synthetic fixture** — Generates a PDF using 100% synthetic invoice data (no real customer information).
4. **Save to scratchpad** — Writes the PDF to a temporary location outside this repository for measurement.
5. **Measure geometry** — Extracts page count, dimensions, and bounding boxes of key layout elements (letter/code box, header, totals, QR, etc.) to produce measured evidence of rendering behavior.

## Prerequisites

- **Docker and Docker Compose** — Required to run a local creadorpdf instance.
  - Test: `docker ps` and `docker-compose --version`
  - If not installed: [Install Docker Desktop](https://www.docker.com/products/docker-desktop) or `apt-get install docker.io docker-compose`

- **Git** — Required to clone enerone/creadorpdf.
  - Test: `git --version`

- **jq** — Required for JSON parsing in the shell script.
  - Test: `jq --version`
  - If not installed: `brew install jq` (macOS) or `apt install jq` (Linux)

- **Python 3** — Required for PDF measurement.
  - Test: `python3 --version`

- **pdfplumber** — Python library for PDF text/geometry extraction.
  - Install: `pip install pdfplumber`

## Quick Start

### 1. Render a PDF

```bash
cd /path/to/ARCA-MCP

# Render using the Factura C fixture (default)
./scripts/dev/pdf_local_render.sh

# Or render using the Nota de Crédito C fixture
./scripts/dev/pdf_local_render.sh nota-credito-c-1item
```

The script will:
- Clone `enerone/creadorpdf` (if not already cloned) to a temporary directory.
- Start creadorpdf with Docker Compose.
- Create a temporary admin API key for template management.
- Publish `factura-ar-v6.html` and `factura-ar-v6.schema.json` as-is.
- Render a PDF using the chosen fixture.
- Print the output path to stdout at the end.

Example output:
```
[2026-10-23 15:45:22] ✓ Render harness completed successfully
[2026-10-23 15:45:22] PDF location: /tmp/rendered_1729694722.pdf
[2026-10-23 15:45:22] Run measurement: python3 scripts/dev/pdf_measure.py /tmp/rendered_1729694722.pdf
```

### 2. Measure the PDF

```bash
# Measure the rendered PDF
python3 ./scripts/dev/pdf_measure.py /tmp/rendered_1729694722.pdf
```

Output is a JSON structure with:
- `total_pages` — Number of pages in the PDF.
- `page_size` — Dimensions of each page (width × height in points).
- `elements` per page — Bounding boxes of key layout elements:
  - `voucher_letter` — The large letter (A/B/C) box dimensions.
  - `document_title` — "FACTURA" or "NOTA DE CRÉDITO" header.
  - `totals_box` — Bounding box of the totals table container.
  - `cae_label` and `cae_value` — CAE text and numeric value coordinates.
  - `detail_table` — Position and size of the items table.
  - `qr_code` — QR code image bounding box.
  - And others...

### 3. Analyze Results

Compare measured values against expected behavior:

```bash
python3 ./scripts/dev/pdf_measure.py /tmp/rendered_1729694722.pdf | jq '.pages[0].elements.voucher_letter'
```

Example result:
```json
{
  "x0": 247,
  "top": 24,
  "x1": 348,
  "bottom": 92,
  "width": 101,
  "height": 68,
  "text": "C"
}
```

## Fixtures

Fixtures are 100% synthetic JSON payloads that comply with the template schema. They contain no real customer data, CUIT, or CAE information.

### factura-c-1item.json

Invoice (Factura C) with one line item.

```json
{
  "emisor": {
    "razonSocial": "Empresa Ejemplo SRL",
    "domicilio": "Avenida Siempre Viva 742",
    "condicionIva": "Responsable Inscripto",
    "ingresosBrutos": "223654123",
    "fechaInicioActividades": "01/06/2010"
  },
  "receptor": { ... },
  "conceptos": [ { ... } ],
  "fiscal": { ... },
  "arcaQr": { ... }
}
```

### nota-credito-c-1item.json

Credit note (Nota de Crédito C) with one line item and associated document reference.

```json
{
  "emisor": { ... },
  "receptor": { ... },
  "conceptos": [ { ... } ],
  "comprobantesAsociados": [
    {
      "tipoComprobante": 11,
      "numero": 41,
      "fecha": "20261010"
    }
  ],
  "fiscal": { ... },
  "arcaQr": { ... }
}
```

## Measurement Tool (pdf_measure.py)

### Purpose

Extracts measurable evidence from rendered PDFs to diagnose layout defects.

### Installation

```bash
pip install pdfplumber
```

### Usage

```bash
python3 ./scripts/dev/pdf_measure.py <path-to-pdf>
```

Output: Structured JSON with page counts, dimensions, and element bounding boxes.

### Key Measurements

All coordinates and sizes are in **typographic points (pt)**, where 1 pt = 1/72 inch.

- **Page size**: A4 is nominally 595 × 842 pt (210 × 297 mm).
- **Bounding box**: Specified as `x0, top, x1, bottom` with origin at top-left (standard PDF coordinates).
- **Element widths/heights**: Computed as `x1 - x0` and `bottom - top`.

### Reading the Output

Example: Find the voucher letter box width on page 1.

```bash
python3 ./scripts/dev/pdf_measure.py /tmp/rendered.pdf \
  | jq '.pages[0].elements.voucher_letter.width'
```

If the output is `101`, the box is 101 points wide (approximately 35 mm).

## Direct Rendering for Debugging (Bypass HTTP API)

When the HTTP API returns generic "render_failed" errors, you can call WeasyPrint directly to see the actual exception:

### Technique: Use `render_dynamic()` directly

Instead of going through the HTTP API, copy your template files into the container and call the rendering function directly:

```bash
# 1. Find the running creadorpdf container
CONTAINER_ID=$(docker ps --filter "status=running" --format "{{.ID}} {{.Image}}" | grep creadorpdf | awk '{print $1}')

# 2. Copy files to container
docker cp /path/to/template.html "$CONTAINER_ID":/tmp/template.html
docker cp /path/to/schema.json "$CONTAINER_ID":/tmp/schema.json
docker cp /path/to/data.json "$CONTAINER_ID":/tmp/data.json

# 3. Execute directly in the container (uses scripts/dev/pdf_render_direct.py)
docker exec "$CONTAINER_ID" python3 /tmp/pdf_render_direct.py \
  /tmp/template.html \
  /tmp/schema.json \
  /tmp/data.json \
  /tmp/output.pdf

# 4. Copy result back to host
docker cp "$CONTAINER_ID":/tmp/output.pdf /tmp/output.pdf
```

**Advantage**: This bypasses the HTTP layer and shows actual WeasyPrint exceptions instead of the generic `{"error": {"code": "render_failed"}}` message.

**Speed**: Direct rendering is much faster than HTTP round-trips, useful for iterative debugging of template CSS/layout issues.

## Troubleshooting

### "Docker is not running"

```bash
# macOS with Docker Desktop
open -a Docker

# Linux with systemd
sudo systemctl start docker
```

### "Failed to clone creadorpdf"

The script requires network access to GitHub and permission to clone via HTTPS.

```bash
# Test GitHub access
git clone --depth 1 https://github.com/enerone/creadorpdf.git /tmp/test-clone
```

### "creadorpdf did not become healthy"

The Docker container is running but the health check failed. Check logs:

```bash
# Find the creadorpdf container
docker ps | grep creadorpdf

# View logs
docker logs <container-name>
```

Common causes:
- Port 8080 already in use (change `CREATORPDF_BASE_URL` env var).
- Insufficient disk space for Docker.
- Network issues between host and container.

### "pdfplumber not installed"

```bash
pip install pdfplumber
```

Or in a virtual environment:

```bash
python3 -m venv .venv
source .venv/bin/activate  # or .venv\Scripts\activate on Windows
pip install pdfplumber
```

## Environment Variables

Optional configuration via environment variables:

```bash
# Override temporary clone directory
export CREATORPDF_CLONE_DIR=/custom/path/creadorpdf

# Override API base URL (default: http://localhost:8080)
export CREATORPDF_BASE_URL=http://creatorpdf:8080

# Enable verbose output
export VERBOSE=1

# Run the harness
./scripts/dev/pdf_local_render.sh
```

## Measured Defects (Issue #86)

This harness is designed to measure the following reported defects in factura-ar-v6:

### P0-1: Pagination

**Issue**: Factura C produces 6 pages instead of expected 3.

**Measurement**: Run harness with `factura-c-1item` fixture and check `total_pages`.

```bash
python3 ./scripts/dev/pdf_measure.py /tmp/rendered.pdf | jq '.summary.total_pages'
```

Expected: `3` pages  
Actual (if defect exists): `6` pages

### P0-2 & P0-3: Header and Totals Layout

**Issue**: Voucher letter/code box width measures ~285 pt instead of ~47 pt (17 mm).

**Measurement**:

```bash
python3 ./scripts/dev/pdf_measure.py /tmp/rendered.pdf | jq '.pages[0].elements.voucher_letter.width'
```

Expected: ~47 pt  
Actual (if defect exists): ~285 pt (spanning nearly full page width)

**Root Cause Hypothesis**: The CSS rule `.voucher-class` uses `position: absolute` inside a `display: grid` parent (`.invoice-header`). This positioning causes the box to expand to fill the grid column instead of respecting its `width: 17mm` constraint.

Potential CSS fix location: Line 51-56 of `factura-ar-v6.html`:
```css
.voucher-class {
  position: absolute;
  z-index: 2;
  left: 50%;
  top: -0.3mm;
  transform: translateX(-50%);
  width: 17mm;  /* <-- This width is ignored due to absolute positioning in grid */
  ...
}
```

### P0-3: Totals Box Containment

**Issue**: Importes (amounts) in totals table float outside the totals box border.

**Measurement**:

```bash
python3 ./scripts/dev/pdf_measure.py /tmp/rendered.pdf | jq '.pages[0].elements.totals_box'
```

Compare the bounding box of `.totals-value` elements against the bbox of `.totals-box` — if the x1 coordinate of the values exceeds the x1 of the box, they overflow.

**Root Cause Hypothesis**: The `.totals-table` uses `position: absolute` to escape the grid layout, causing values to position relative to the page rather than the container.

## Output Location

The rendered PDF is saved to:

- **Default**: `${TMPDIR}` (or `/tmp` if `TMPDIR` is unset)
- **Custom**: Pass as second argument: `./scripts/dev/pdf_local_render.sh factura-c-1item /custom/path`

The PDF filename is timestamped to avoid collisions: `rendered_<unix-timestamp>.pdf`

**Important**: This directory is outside the repository and not tracked by Git. PDFs are **not committed**.

## Running Visual Geometry Tests

The `FacturaArV7VisualGeometryTests` suite validates the real-world layout of rendered factura-ar-v7 PDFs by measuring page geometry, element dimensions, and positioning.

### Prerequisites

- Docker running with creadorpdf container available
- Python 3 installed with pdfplumber library: `pip install pdfplumber`

### Test Execution

Tests are **gated by the `ARCA_PDF_VISUAL_TESTS=1` environment variable**.

Without the variable, tests skip immediately (no Docker required, no network access):

```bash
# Default: skip visual tests, fast green on any machine
dotnet test dcArca.McpServer.Tests/dcArca.McpServer.Tests.csproj --configuration Release
```

With the environment variable (requires Docker):

```bash
# Run visual geometry tests
ARCA_PDF_VISUAL_TESTS=1 dotnet test dcArca.McpServer.Tests/dcArca.McpServer.Tests.csproj \
  --configuration Release \
  --filter "FullyQualifiedName~FacturaArV7VisualGeometry"
```

Example output:
```
FacturaArV7VisualGeometryTests.FacturaC1Item_Renders3Pages_WithCorrectGeometry PASSED
FacturaArV7VisualGeometryTests.NotaCreditoC1Item_Renders3Pages_WithCorrectGeometry PASSED
```

### What the Tests Verify

For each test fixture (Factura C 1-item, Nota de Crédito C 1-item):

1. **Page Count**: Exactly 3 A4 pages (595×842 pt)
2. **Voucher Letter/Code Box**: Width measured 44-50 pt (objective ~47 pt = 17 mm)
3. **Totals Containment**: All total amount values remain within the totals box boundary (tolerance ±2 pt)
4. **QR Code**: Present and measurable on each page

### Pre-Publication Requirement

This test **must pass** before publishing any new version of the factura-ar template to a production creadorpdf instance:

```bash
# Standard pre-publication check
ARCA_PDF_VISUAL_TESTS=1 dotnet test dcArca.McpServer.Tests/dcArca.McpServer.Tests.csproj \
  --configuration Release \
  --filter "FacturaArV7VisualGeometry"
```

## Running in CI/CD

This harness is designed for **local development and troubleshooting only**. It requires Docker and network access (GitHub clone).

For automated geometry verification in CI, enable the `ARCA_PDF_VISUAL_TESTS=1` environment variable and Docker to run the visual tests (see above).

To include in CI:

```bash
# Enable visual tests and run them (requires Docker in CI environment)
export ARCA_PDF_VISUAL_TESTS=1
dotnet test dcArca.McpServer.Tests/dcArca.McpServer.Tests.csproj --configuration Release --filter "FacturaArV7VisualGeometry"
```

## References

- **Issue #86**: Fidelidad visual de factura-ar-v7 (paginación, cabecera, totales)
- **WeasyPrint**: https://weasyprint.org/ (version pinned to 62.3 in creadorpdf)
- **pdfplumber**: https://github.com/jsvine/pdfplumber
- **creadorpdf**: https://github.com/enerone/creadorpdf

## Security Notes

- **API keys**: Temporary keys created during render are not persisted in the repository.
- **Fixtures**: 100% synthetic data; no real customer or fiscal information.
- **PDFs**: Saved to system temporary directory; not committed to Git.
- **Secrets**: The script logs key IDs but never logs secret values.
