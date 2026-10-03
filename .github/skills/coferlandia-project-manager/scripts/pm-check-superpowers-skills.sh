#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./lib/config.sh
source "${script_dir}/lib/config.sh"

print_help() {
  cat <<'EOF'
Usage: pm-check-superpowers-skills.sh [--json]
Description: Check which required and optional Superpowers skills are available.
EOF
}

json_output=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --json)
      json_output=true
      ;;
    --help|-h)
      print_help
      exit 0
      ;;
    *)
      die "Unknown argument: $1"
      ;;
  esac
  shift
done

python_cmd="$(pm_python_cmd)"
skill_root="$(pm_skill_root)"
"${python_cmd}" - "${json_output}" "${skill_root}" <<'PY'
import json
import os
import sys
from pathlib import Path

json_output = sys.argv[1].lower() == "true"
repo_root = Path(sys.argv[2])
template = json.loads(
    (repo_root / "templates" / "config.template.json").read_text(
        encoding="utf-8"
    )
)
required = template["superpowers"]["required_skills"]
optional = template["superpowers"]["optional_skills"]

roots = [Path(os.path.expanduser("~/.codex/skills")), Path(os.path.expanduser("~/.agents/skills"))]
available_names = set()
for root in roots:
    if not root.is_dir():
        continue
    for child in root.iterdir():
        if (child / "SKILL.md").is_file():
          available_names.add(child.name)

required_available = sorted([name for name in required if name in available_names])
required_missing = sorted([name for name in required if name not in available_names])
optional_available = sorted([name for name in optional if name in available_names])
optional_missing = sorted([name for name in optional if name not in available_names])
status = "ok" if not required_missing else "partial"
payload = {
    "status": status,
    "required_available": required_available,
    "required_missing": required_missing,
    "optional_available": optional_available,
    "optional_missing": optional_missing,
}

if json_output:
    print(json.dumps(payload, indent=2))
else:
    print(f"status: {status}")
    print(f"required_available: {', '.join(required_available) if required_available else 'none'}")
    print(f"required_missing: {', '.join(required_missing) if required_missing else 'none'}")
    print(f"optional_available: {', '.join(optional_available) if optional_available else 'none'}")
    print(f"optional_missing: {', '.join(optional_missing) if optional_missing else 'none'}")
PY
