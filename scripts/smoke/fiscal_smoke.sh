#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

python_cmd=()

if command -v python3 >/dev/null 2>&1 && python3 -c 'import sys' >/dev/null 2>&1; then
  python_cmd=(python3)
elif command -v python >/dev/null 2>&1 && python -c 'import sys' >/dev/null 2>&1; then
  python_cmd=(python)
elif command -v py >/dev/null 2>&1 && py -3 -c 'import sys' >/dev/null 2>&1; then
  python_cmd=(py -3)
else
  echo "ERROR: a usable Python 3 launcher is required (python3, python, or py -3)." >&2
  exit 127
fi

exec "${python_cmd[@]}" "$SCRIPT_DIR/fiscal_smoke.py" "$@"
