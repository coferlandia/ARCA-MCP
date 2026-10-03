#!/usr/bin/env python3
"""Public entry point for the project-orchestrator skill."""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from project_orchestrator_cli.claims_cli import json_or_human, main
from project_orchestrator_cli.contract_initialization import prepare_cli_args
from project_orchestrator_cli.contracts import OrchestratorError, failure

if __name__ == "__main__":
    try:
        prepared = prepare_cli_args(sys.argv[1:], cwd=Path.cwd())
    except OrchestratorError as exc:
        json_or_human(failure("run", exc), True)
        raise SystemExit(exc.code)
    except Exception as exc:
        json_or_human(failure("run", exc), True)
        raise SystemExit(1)
    raise SystemExit(main(prepared))
