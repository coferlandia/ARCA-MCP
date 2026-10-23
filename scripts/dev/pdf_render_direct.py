#!/usr/bin/env python3
"""
Direct PDF render for debugging via creadorpdf's render_dynamic function.

This bypasses the HTTP API and calls WeasyPrint directly, allowing inspection
of render errors without the generic "render_failed" wrapping.

Usage:
  docker exec <container-id> python3 /tmp/pdf_render_direct.py \
    <template.html> <schema.json> <data.json> <output.pdf>

Example:
  docker exec creadorpdf-creadorpdf-1 python3 /tmp/pdf_render_direct.py \
    /tmp/v6/factura-ar-v6.html \
    /tmp/v6/factura-ar-v6.schema.json \
    /tmp/v6/factura-c-1item.json \
    /tmp/v6/v6_direct_render.pdf
"""

import sys
import json
import traceback
import os

def main():
    if len(sys.argv) != 5:
        print(f"Usage: {sys.argv[0]} <template.html> <schema.json> <data.json> <output.pdf>")
        sys.exit(1)

    template_path = sys.argv[1]
    schema_path = sys.argv[2]
    data_path = sys.argv[3]
    output_path = sys.argv[4]

    # Inject creadorpdf app module into path
    sys.path.insert(0, "/app")

    try:
        from app.dynamic_render import render_dynamic

        print(f"[*] Loading template from {template_path}...", file=sys.stderr)
        with open(template_path, 'r') as f:
            template = f.read()

        print(f"[*] Loading schema from {schema_path}...", file=sys.stderr)
        with open(schema_path, 'r') as f:
            schema = json.load(f)

        print(f"[*] Loading data from {data_path}...", file=sys.stderr)
        with open(data_path, 'r') as f:
            data = json.load(f)

        print(f"[*] Rendering PDF...", file=sys.stderr)
        pdf = render_dynamic(template, schema, data)

        print(f"[*] Writing {len(pdf)} bytes to {output_path}...", file=sys.stderr)
        with open(output_path, 'wb') as f:
            f.write(pdf)

        print(f"SUCCESS: {len(pdf)} bytes written to {output_path}", file=sys.stderr)
        sys.exit(0)

    except Exception as e:
        print(f"ERROR during render:", file=sys.stderr)
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main()
