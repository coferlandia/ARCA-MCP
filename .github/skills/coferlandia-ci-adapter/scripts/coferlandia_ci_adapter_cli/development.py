from __future__ import annotations

import hashlib
import json
import os
import re
import tempfile
from pathlib import Path
from typing import Any

DEVELOPMENT_CONTRACT_PATH = Path(".coferlandia/development/validation.json")
DEFAULT_DEVELOPMENT_WORKFLOW_PATH = Path(".github/workflows/coferlandia-development-validation.yml")
ENV_NAME_RE = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")
ACTION_USES_RE = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[A-Za-z0-9_./-]+$")
WITH_KEY_RE = re.compile(r"^[A-Za-z0-9_.-]+$")
CONTROL_REF_RE = re.compile(r"^(?!.*\.\.)(?!.*//)[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)*$")
SHA256_RE = re.compile(r"^[a-f0-9]{64}$")
FORBIDDEN_KEY_PARTS = {"secret", "password", "passwd", "token", "credential", "private_key"}
ALLOWED_TOP = {
    "schema_version",
    "repository",
    "documentation",
    "working_directory",
    "commands",
    "required_services",
    "environment",
    "github",
    "fingerprint",
}


def _require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def _scan_forbidden(value: Any, path: str = "$") -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            normalized = str(key).lower().replace("-", "_")
            if any(part in normalized for part in FORBIDDEN_KEY_PARTS):
                raise ValueError(f"forbidden secret-bearing key at {path}.{key}")
            _scan_forbidden(child, f"{path}.{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            _scan_forbidden(child, f"{path}[{index}]")


def _validate_relative_path(value: str, label: str) -> None:
    _require(isinstance(value, str) and value, f"{label} is required")
    path = Path(value)
    _require(not path.is_absolute(), f"{label} must be repository-relative")
    _require(".." not in path.parts, f"{label} must not escape the repository")


def _validate_commands(commands: object) -> None:
    _require(isinstance(commands, list) and commands, "commands must be a non-empty list")
    ids: list[str] = []
    for item in commands:
        _require(isinstance(item, dict), "command entries must be objects")
        _require(set(item) == {"id", "command", "purpose"}, "command fields are invalid")
        for key in ("id", "command", "purpose"):
            _require(isinstance(item[key], str) and item[key], f"command {key} must be a non-empty string")
        ids.append(item["id"])
    _require(len(ids) == len(set(ids)), "command ids must be unique")


def _validate_setup(setup: object) -> None:
    _require(isinstance(setup, list), "github.setup must be a list")
    names: list[str] = []
    for index, item in enumerate(setup):
        label = f"github.setup[{index}]"
        _require(isinstance(item, dict), f"{label} must be an object")
        fields = set(item)
        _require({"name", "uses"} <= fields <= {"name", "uses", "with"}, f"{label} fields are invalid")
        name = item["name"]
        uses = item["uses"]
        _require(isinstance(name, str) and name.strip(), f"{label}.name must be a non-empty string")
        _require(
            isinstance(uses, str) and ACTION_USES_RE.fullmatch(uses) is not None,
            f"{label}.uses must be a static owner/repo@ref action",
        )
        names.append(name)

        inputs = item.get("with", {})
        _require(isinstance(inputs, dict), f"{label}.with must be an object")
        for key, value in inputs.items():
            _require(
                isinstance(key, str) and WITH_KEY_RE.fullmatch(key) is not None,
                f"{label}.with keys must be simple action input names",
            )
            _require(
                isinstance(value, (str, int, bool)) and not isinstance(value, float),
                f"{label}.with.{key} must be a string, integer, or boolean",
            )
            if isinstance(value, str):
                _require(value != "", f"{label}.with.{key} must not be empty")
                _require("${{" not in value, f"{label}.with.{key} must not contain GitHub expressions")
                _require("\n" not in value and "\r" not in value, f"{label}.with.{key} must be a single-line value")
    _require(len(names) == len(set(names)), "github.setup names must be unique")


def validate_development_contract(contract: dict, *, verify_fingerprint: bool = False) -> None:
    _require(isinstance(contract, dict), "development contract must be an object")
    unknown = set(contract) - ALLOWED_TOP
    _require(not unknown, f"unknown top-level fields: {sorted(unknown)}")
    _scan_forbidden(contract)
    _require(contract.get("schema_version") == 1, "schema_version must be 1")

    repository = contract.get("repository")
    _require(
        isinstance(repository, str) and repository.count("/") == 1 and all(repository.split("/")),
        "repository must be owner/name",
    )
    docs = contract.get("documentation")
    _require(
        isinstance(docs, list) and all(isinstance(item, str) and item for item in docs),
        "documentation must be a string list",
    )
    _require(len(docs) == len(set(docs)), "documentation entries must be unique")

    working_directory = contract.get("working_directory")
    _validate_relative_path(working_directory, "working_directory")
    _validate_commands(contract.get("commands"))

    services = contract.get("required_services")
    _require(
        isinstance(services, list) and all(isinstance(item, str) and item for item in services),
        "required_services must be a string list",
    )
    _require(len(services) == len(set(services)), "required_services entries must be unique")

    environment = contract.get("environment")
    _require(
        isinstance(environment, list)
        and all(isinstance(item, str) and ENV_NAME_RE.fullmatch(item) for item in environment),
        "environment must contain variable names only, never NAME=value or secret values",
    )
    _require(len(environment) == len(set(environment)), "environment entries must be unique")

    github = contract.get("github")
    required_github_fields = {"submission", "candidate_binding", "runner_labels", "shell", "gate"}
    allowed_github_fields = required_github_fields | {"setup"}
    _require(
        isinstance(github, dict)
        and required_github_fields <= set(github) <= allowed_github_fields,
        "github fields are invalid",
    )
    submission = github["submission"]
    _require(
        isinstance(submission, dict)
        and {"mode", "workflow"} <= set(submission) <= {"mode", "workflow", "fallback"},
        "github.submission fields are invalid",
    )
    _require(submission["mode"] == "existing-pr-events", "development submission mode must be existing-pr-events")
    _validate_relative_path(submission["workflow"], "github.submission.workflow")
    fallback = submission.get("fallback")
    if fallback is not None:
        _require(
            isinstance(fallback, dict) and set(fallback) == {"mode", "control_ref"},
            "github.submission.fallback fields are invalid",
        )
        _require(
            fallback["mode"] == "workflow-dispatch-exact-head",
            "development fallback mode must be workflow-dispatch-exact-head",
        )
        control_ref = fallback["control_ref"]
        _require(
            isinstance(control_ref, str) and CONTROL_REF_RE.fullmatch(control_ref) is not None,
            "github.submission.fallback.control_ref must be a repository ref name",
        )
    workflow = Path(submission["workflow"])
    _require(
        workflow.parts[:2] == (".github", "workflows") and workflow.suffix in {".yml", ".yaml"},
        "development workflow must live under .github/workflows and be YAML",
    )
    _require(github["candidate_binding"] == "pull-request-head", "github.candidate_binding must be pull-request-head")
    labels = github["runner_labels"]
    _require(
        isinstance(labels, list) and labels and all(isinstance(item, str) and item.strip() for item in labels),
        "github.runner_labels must be a non-empty string list",
    )
    _require(len(labels) == len(set(labels)), "github.runner_labels entries must be unique")
    _require(github["shell"] in {"bash", "pwsh"}, "github.shell must be bash or pwsh")
    _validate_setup(github.get("setup", []))

    gate = github["gate"]
    _require(isinstance(gate, dict) and set(gate) == {"name", "allowed_conclusions"}, "github.gate fields are invalid")
    _require(isinstance(gate["name"], str) and gate["name"], "github.gate.name is required")
    allowed = gate["allowed_conclusions"]
    _require(
        isinstance(allowed, list) and allowed and all(isinstance(item, str) and item for item in allowed),
        "github.gate.allowed_conclusions must be non-empty",
    )
    _require(allowed == ["success"], "development gate currently allows only success")

    if "fingerprint" in contract:
        _require(
            isinstance(contract["fingerprint"], str) and SHA256_RE.fullmatch(contract["fingerprint"]) is not None,
            "fingerprint must be sha256 hex",
        )
        if verify_fingerprint:
            _require(contract["fingerprint"] == development_fingerprint(contract), "development contract fingerprint mismatch")


def canonical_development_contract(contract: dict) -> dict:
    clean = json.loads(json.dumps(contract))
    clean.pop("fingerprint", None)
    return clean


def development_fingerprint(contract: dict) -> str:
    clean = canonical_development_contract(contract)
    validate_development_contract(clean)
    payload = json.dumps(clean, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def load_development_contract(path: Path, *, verify_fingerprint: bool = False) -> dict:
    data = json.loads(path.read_text(encoding="utf-8"))
    validate_development_contract(data, verify_fingerprint=verify_fingerprint)
    return data


def render_development_contract(source: dict) -> dict:
    clean = canonical_development_contract(source)
    validate_development_contract(clean)
    clean["fingerprint"] = development_fingerprint(clean)
    return clean


def _yaml_string(value: str) -> str:
    return json.dumps(value, ensure_ascii=False)


def _yaml_scalar(value: str | int | bool) -> str:
    return json.dumps(value, ensure_ascii=False)


def _indent_command(command: str, spaces: int = 10) -> str:
    prefix = " " * spaces
    return "\n".join(prefix + line for line in command.splitlines())


def render_development_workflow(contract: dict) -> str:
    rendered = render_development_contract(contract)
    github = rendered["github"]
    fingerprint = rendered["fingerprint"]
    runs_on = json.dumps(github["runner_labels"], ensure_ascii=False)
    shell = github["shell"]
    gate_name = _yaml_string(github["gate"]["name"])
    working_directory = _yaml_string(rendered["working_directory"])
    fallback = github["submission"].get("fallback")
    exact_head_dispatch = fallback is not None

    if shell == "bash":
        identity = 'actual="$(git rev-parse HEAD)"\ntest "$actual" = "$EXPECTED_SHA"'
    else:
        identity = '$actual = (git rev-parse HEAD).Trim()\nif ($actual -ne $env:EXPECTED_SHA) { throw "candidate SHA mismatch: expected $env:EXPECTED_SHA observed $actual" }'

    lines = [
        "name: Coferlandia Development Validation",
        "",
        "on:",
        "  pull_request:",
        "    types: [opened, synchronize, reopened, converted_to_draft]",
    ]
    if exact_head_dispatch:
        lines.extend([
            "  workflow_dispatch:",
            "    inputs:",
            "      pr_number:",
            '        description: "Draft pull request number"',
            "        required: true",
            "        type: number",
            "      candidate_sha:",
            '        description: "Exact current Draft PR head SHA"',
            "        required: true",
            "        type: string",
        ])

    lines.extend(["", "permissions:", "  contents: read"])
    if exact_head_dispatch:
        lines.append("  pull-requests: read")

    concurrency_pr = "${{ github.event.pull_request.number || inputs.pr_number }}" if exact_head_dispatch else "${{ github.event.pull_request.number }}"
    lines.extend([
        "",
        "concurrency:",
        f"  group: coferlandia-development-{concurrency_pr}",
        "  cancel-in-progress: true",
        "",
        "jobs:",
        "  validation:",
        f"    name: {gate_name}",
    ])
    if exact_head_dispatch:
        lines.extend([
            "    if: >-",
            "      (github.event_name == 'pull_request' && github.event.pull_request.draft == true) ||",
            "      github.event_name == 'workflow_dispatch'",
        ])
    else:
        lines.append("    if: github.event.pull_request.draft == true")

    lines.extend([f"    runs-on: {runs_on}", "    steps:"])

    if exact_head_dispatch:
        control_ref = json.dumps(fallback["control_ref"], ensure_ascii=False)
        script = "\n".join([
            "const eventName = context.eventName;",
            "if (eventName === 'pull_request') {",
            "  const pr = context.payload.pull_request;",
            "  if (!pr || !pr.draft || pr.state !== 'open') throw new Error('Development validation requires an open Draft PR');",
            "  core.setOutput('pr_number', String(pr.number));",
            "  core.setOutput('candidate_sha', pr.head.sha);",
            "  return;",
            "}",
            f"const expectedControlRef = {control_ref};",
            "const observedControlRef = context.ref.replace(/^refs\\/(heads|tags)\\//, '');",
            "if (observedControlRef !== expectedControlRef) throw new Error('trusted control ref mismatch: expected ' + expectedControlRef + ' observed ' + observedControlRef);",
            "const prNumber = Number('${{ inputs.pr_number }}');",
            "const requestedSha = '${{ inputs.candidate_sha }}';",
            "if (!Number.isInteger(prNumber) || prNumber <= 0) throw new Error('invalid PR number');",
            "if (!/^[0-9a-f]{40}$/.test(requestedSha)) throw new Error('candidate_sha must be a full 40-character lowercase Git SHA');",
            "const { data: pr } = await github.rest.pulls.get({ owner: context.repo.owner, repo: context.repo.repo, pull_number: prNumber });",
            "if (pr.state !== 'open' || !pr.draft) throw new Error('Development validation requires an open Draft PR');",
            "if (pr.head.sha !== requestedSha) throw new Error('candidate SHA mismatch: requested ' + requestedSha + ' current ' + pr.head.sha);",
            "core.setOutput('pr_number', String(pr.number));",
            "core.setOutput('candidate_sha', pr.head.sha);",
        ])
        lines.extend([
            "      - name: Resolve exact candidate identity",
            "        id: identity",
            "        uses: actions/github-script@v7",
            "        with:",
            "          github-token: ${{ github.token }}",
            "          script: |",
            _indent_command(script, 12),
        ])
        candidate_expr = "${{ steps.identity.outputs.candidate_sha }}"
    else:
        candidate_expr = "${{ github.event.pull_request.head.sha }}"

    lines.extend([
        "      - name: Check out exact PR head",
        "        uses: actions/checkout@v4",
        "        with:",
        f"          ref: {candidate_expr}",
        "          fetch-depth: 0",
        "          persist-credentials: false",
        "      - name: Verify exact candidate identity",
        f"        shell: {shell}",
        "        env:",
        f"          EXPECTED_SHA: {candidate_expr}",
        "        run: |",
        _indent_command(identity),
        "      - name: Record Development contract",
        f"        shell: {shell}",
        "        run: |",
    ])
    if shell == "bash":
        summary = (
            f'echo "Development contract fingerprint: {fingerprint}" | tee -a "$GITHUB_STEP_SUMMARY"\n'
            f'echo "Candidate SHA: {candidate_expr}" | tee -a "$GITHUB_STEP_SUMMARY"'
        )
    else:
        summary = (
            f'$line = "Development contract fingerprint: {fingerprint}"; Write-Output $line; Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $line\n'
            f'$line = "Candidate SHA: {candidate_expr}"; Write-Output $line; Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $line'
        )
    lines.append(_indent_command(summary))

    for setup in github.get("setup", []):
        lines.extend([
            f"      - name: {_yaml_string(setup['name'])}",
            f"        uses: {_yaml_string(setup['uses'])}",
        ])
        if setup.get("with"):
            lines.append("        with:")
            for key in sorted(setup["with"]):
                lines.append(f"          {key}: {_yaml_scalar(setup['with'][key])}")

    for command in rendered["commands"]:
        lines.extend([
            f"      - name: {_yaml_string(command['purpose'])}",
            f"        shell: {shell}",
            f"        working-directory: {working_directory}",
            "        run: |",
            _indent_command(command["command"]),
        ])
    lines.append("")
    return "\n".join(lines)


def atomic_write_json(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp_name = tempfile.mkstemp(prefix=f".{path.name}.", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as handle:
            json.dump(value, handle, indent=2, ensure_ascii=False)
            handle.write("\n")
        os.replace(tmp_name, path)
    except Exception:
        try:
            os.unlink(tmp_name)
        except OSError:
            pass
        raise


def atomic_write_text(path: Path, value: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp_name = tempfile.mkstemp(prefix=f".{path.name}.", suffix=".tmp", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(value)
        os.replace(tmp_name, path)
    except Exception:
        try:
            os.unlink(tmp_name)
        except OSError:
            pass
        raise
