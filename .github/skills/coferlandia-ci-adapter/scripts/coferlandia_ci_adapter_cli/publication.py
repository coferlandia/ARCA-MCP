from __future__ import annotations

import json
from pathlib import Path, PurePosixPath
from typing import Any

DEFAULT_WORKFLOW_PATH = Path(".github/workflows/coferlandia-release-publish.yml")
DEFAULT_RUNS_ON: str = "ubuntu-latest"
RELEASE_POLICY_PATH = Path(".coferlandia/release/policy.json")
PUBLICATION_REQUEST_MARKER = "<!-- coferlandia-release-publication-request:v1 -->"
PUBLICATION_RETRY_COMMAND = "Retry the existing Coferlandia publication request."


def _safe_repo_path(value: str, *, suffixes: tuple[str, ...] | None = None) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError("repository path must be a non-empty string")
    path = PurePosixPath(value)
    if path.is_absolute() or ".." in path.parts or value.startswith("./"):
        raise ValueError("repository path must be relative and must not escape the repository")
    normalized = path.as_posix()
    if suffixes and not normalized.endswith(suffixes):
        raise ValueError(f"repository path must end with one of: {', '.join(suffixes)}")
    return normalized


def _normalize_runs_on(value: Any) -> str | list[str]:
    if value is None:
        return DEFAULT_RUNS_ON
    if isinstance(value, str):
        normalized = value.strip()
        if not normalized or "\n" in normalized or "\r" in normalized:
            raise ValueError("publication.github.runs_on string must be non-empty and single-line")
        return normalized
    if isinstance(value, list):
        if not value:
            raise ValueError("publication.github.runs_on list must not be empty")
        normalized: list[str] = []
        for item in value:
            if not isinstance(item, str):
                raise ValueError("publication.github.runs_on labels must be strings")
            label = item.strip()
            if not label or "\n" in label or "\r" in label:
                raise ValueError("publication.github.runs_on labels must be non-empty and single-line")
            normalized.append(label)
        if len(normalized) != len(set(normalized)):
            raise ValueError("publication.github.runs_on contains duplicate labels")
        return normalized
    raise ValueError("publication.github.runs_on must be a string or non-empty list of strings")


def publication_github(policy: dict[str, Any]) -> dict[str, Any] | None:
    publication = policy.get("publication")
    if publication is None:
        return None
    if not isinstance(publication, dict):
        raise ValueError("publication must be an object")
    github = publication.get("github")
    if github is None:
        return None
    if not isinstance(github, dict):
        raise ValueError("publication.github must be an object")
    mode = github.get("mode")
    if mode == "none":
        if set(github) != {"mode"}:
            raise ValueError("publication.github mode=none accepts no additional fields")
        return github
    if mode not in {"issue-comment", "workflow-dispatch"}:
        raise ValueError("publication.github.mode must be none, issue-comment, or workflow-dispatch")
    allowed = {"mode", "workflow", "runs_on"}
    if not {"mode", "workflow"}.issubset(github) or not set(github).issubset(allowed):
        raise ValueError(f"{mode} publication requires mode/workflow and optionally runs_on")
    workflow = _safe_repo_path(str(github.get("workflow", "")), suffixes=(".yml", ".yaml"))
    if not workflow.startswith(".github/workflows/"):
        raise ValueError("publication.github.workflow must be under .github/workflows/")
    rendered = {"mode": mode, "workflow": workflow}
    if "runs_on" in github:
        rendered["runs_on"] = _normalize_runs_on(github["runs_on"])
    return rendered


def load_release_policy(path: Path) -> dict[str, Any]:
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ValueError(f"invalid release policy JSON: {exc}") from exc
    if not isinstance(raw, dict):
        raise ValueError("release policy must be a JSON object")
    publication_github(raw)
    return raw


def render_workflow(*, publisher_path: str, runs_on: Any = None) -> str:
    publisher = _safe_repo_path(publisher_path, suffixes=(".py",))
    rendered_runs_on = json.dumps(_normalize_runs_on(runs_on), separators=(",", ":"))
    return f"""name: Coferlandia Release Publish

on:
  issue_comment:
    types: [created]

permissions:
  contents: write
  issues: read
  pull-requests: read

concurrency:
  group: coferlandia-release-${{{{ github.event.issue.number }}}}
  cancel-in-progress: false

jobs:
  publish:
    name: Publish exact release
    if: >-
      github.event.issue.pull_request &&
      (contains(github.event.comment.body, '{PUBLICATION_REQUEST_MARKER}') ||
       github.event.comment.body == '{PUBLICATION_RETRY_COMMAND}')
    runs-on: {rendered_runs_on}
    env:
      GITHUB_TOKEN: ${{{{ github.token }}}}
    steps:
      - name: Set up Python
        uses: actions/setup-python@v5
        with:
          python-version: '3.12'

      - name: Require release authority
        id: authority
        shell: bash
        env:
          RELEASE_PR_NUMBER: ${{{{ github.event.issue.number }}}}
        run: |
          python - <<'PY'
          import json
          import os
          from urllib.parse import quote
          from urllib.request import Request, urlopen

          repo = os.environ['GITHUB_REPOSITORY']
          actor = os.environ['GITHUB_ACTOR']
          token = os.environ['GITHUB_TOKEN']
          pr_number = os.environ['RELEASE_PR_NUMBER']

          def api(path):
              request = Request(
                  f'https://api.github.com/repos/{{repo}}/{{path}}',
                  headers={{
                      'Accept': 'application/vnd.github+json',
                      'Authorization': f'Bearer {{token}}',
                      'User-Agent': 'coferlandia-release-publication',
                      'X-GitHub-Api-Version': '2022-11-28',
                  }},
              )
              with urlopen(request, timeout=30) as response:
                  return json.load(response)

          permission = api(f'collaborators/{{quote(actor, safe="")}}/permission').get('permission')
          if permission not in {{'admin', 'maintain'}}:
              raise SystemExit(f'actor {{actor}} has insufficient release permission: {{permission}}')
          pr = api(f'pulls/{{pr_number}}')
          if not pr.get('merged_at'):
              raise SystemExit('release PR is not merged')
          merge_sha = pr.get('merge_commit_sha')
          if not merge_sha:
              raise SystemExit('release PR has no merge_commit_sha')
          with open(os.environ['GITHUB_OUTPUT'], 'a', encoding='utf-8') as output:
              output.write(f'merge_sha={{merge_sha}}\\n')
          PY

      - name: Parse immutable publication request
        id: request
        shell: bash
        run: |
          python - <<'PY'
          import json
          import os
          import re
          from pathlib import Path
          from urllib.error import HTTPError
          from urllib.parse import quote
          from urllib.request import Request, urlopen

          marker = {PUBLICATION_REQUEST_MARKER!r}
          retry_command = {PUBLICATION_RETRY_COMMAND!r}
          def parse_request(candidate_body):
              if not isinstance(candidate_body, str) or candidate_body.count(marker) != 1:
                  raise ValueError('publication request must contain exactly one marker')
              tail = candidate_body.split(marker, 1)[1]
              matches = re.findall(r'```json\\s*(.+?)\\s*```', tail, re.S)
              if len(matches) != 1:
                  raise ValueError('publication request must contain exactly one JSON block')
              try:
                  request = json.loads(matches[0])
              except json.JSONDecodeError as exc:
                  raise ValueError('publication request JSON is invalid') from exc
              expected = {{'schema', 'target_sha', 'version', 'impact', 'title', 'notes'}}
              if not isinstance(request, dict) or set(request) != expected or request.get('schema') != 1:
                  raise ValueError('invalid publication request schema')
              if not re.fullmatch(r'[0-9a-f]{{40}}', request['target_sha']):
                  raise ValueError('target_sha must be an exact 40-character lowercase SHA')
              if not re.fullmatch(r'[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\\+[0-9A-Za-z.-]+)?', request['version']):
                  raise ValueError('version must be explicit SemVer without tag prefix')
              if request['impact'] not in {{'patch', 'minor', 'major'}}:
                  raise ValueError('impact must be patch, minor, or major')
              if not isinstance(request['title'], str) or not request['title'].strip():
                  raise ValueError('title must be non-empty')
              if not isinstance(request['notes'], str) or not request['notes'].strip():
                  raise ValueError('notes must be non-empty')
              return request

          event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text(encoding='utf-8'))
          body = event['comment']['body']
          request_comment_id = event['comment']['id']
          if body == retry_command:
              repo = os.environ['GITHUB_REPOSITORY']
              token = os.environ['GITHUB_TOKEN']
              issue_number = event['issue']['number']
              current_id = event['comment']['id']

              def api_json(url):
                  api_request = Request(url, headers={{
                      'Accept': 'application/vnd.github+json',
                      'Authorization': f'Bearer {{token}}',
                      'User-Agent': 'coferlandia-release-publication',
                      'X-GitHub-Api-Version': '2022-11-28',
                  }})
                  with urlopen(api_request, timeout=30) as response:
                      return json.load(response)

              def permission_for(login):
                  url = f'https://api.github.com/repos/{{repo}}/collaborators/{{quote(login, safe="")}}/permission'
                  try:
                      value = api_json(url)
                  except HTTPError as exc:
                      if exc.code in {{403, 404}}:
                          return None
                      raise
                  return value.get('permission')

              comments = []
              page = 1
              while True:
                  page_items = api_json(f'https://api.github.com/repos/{{repo}}/issues/{{issue_number}}/comments?per_page=100&page={{page}}')
                  if not isinstance(page_items, list):
                      raise SystemExit('GitHub issue comments response must be a list')
                  comments.extend(page_items)
                  if len(page_items) < 100:
                      break
                  page += 1

              request = None
              for comment in sorted(comments, key=lambda item: item.get('id', 0), reverse=True):
                  comment_id = comment.get('id', 0)
                  candidate_body = comment.get('body', '')
                  if comment_id >= current_id or marker not in candidate_body:
                      continue
                  author = (comment.get('user') or {{}}).get('login')
                  if not author or permission_for(author) not in {{'admin', 'maintain'}}:
                      continue
                  try:
                      candidate = parse_request(candidate_body)
                  except ValueError:
                      continue
                  request = candidate
                  request_comment_id = comment_id
                  break
              if request is None:
                  raise SystemExit('retry requested but no prior authorized valid publication request exists')
          else:
              try:
                  request = parse_request(body)
              except ValueError as exc:
                  raise SystemExit(str(exc)) from exc
          request_path = Path(os.environ['RUNNER_TEMP']) / 'coferlandia-publication-request.json'
          request_path.write_text(json.dumps(request, sort_keys=True), encoding='utf-8')
          with open(os.environ['GITHUB_OUTPUT'], 'a', encoding='utf-8') as output:
              output.write(f"target_sha={{request['target_sha']}}\\n")
              output.write(f"version={{request['version']}}\\n")
              output.write(f"request_comment_id={{request_comment_id}}\\n")
          PY

      - name: Bind request to release PR integration
        env:
          TARGET_SHA: ${{{{ steps.request.outputs.target_sha }}}}
          MERGE_SHA: ${{{{ steps.authority.outputs.merge_sha }}}}
        shell: bash
        run: |
          set -euo pipefail
          test -n "$MERGE_SHA"
          test "$MERGE_SHA" = "$TARGET_SHA"

      - name: Check out exact publication control plane
        uses: actions/checkout@v4
        with:
          ref: ${{{{ github.sha }}}}
          path: control-plane
          persist-credentials: false

      - name: Check out exact publication target
        uses: actions/checkout@v4
        with:
          ref: ${{{{ steps.request.outputs.target_sha }}}}
          path: release-target
          fetch-depth: 0
          persist-credentials: true

      - name: Revalidate publication control plane
        working-directory: control-plane
        env:
          CONTROL_PLANE_SHA: ${{{{ github.sha }}}}
        shell: bash
        run: |
          set -euo pipefail
          test "$(git rev-parse HEAD)" = "$CONTROL_PLANE_SHA"

      - name: Configure release identity
        working-directory: release-target
        shell: bash
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "41898282+github-actions[bot]@users.noreply.github.com"

      - name: Revalidate exact checkout
        working-directory: release-target
        env:
          TARGET_SHA: ${{{{ steps.request.outputs.target_sha }}}}
        shell: bash
        run: |
          set -euo pipefail
          test "$(git rev-parse HEAD)" = "$TARGET_SHA"
          git fetch --tags --force origin
          test "$(git rev-parse HEAD)" = "$TARGET_SHA"

      - name: Materialize request and deterministic release plan
        working-directory: release-target
        shell: bash
        run: |
          python - <<'PY'
          import json
          import os
          import re
          import subprocess
          from pathlib import Path

          request_path = Path(os.environ['RUNNER_TEMP']) / 'coferlandia-publication-request.json'
          request = json.loads(request_path.read_text(encoding='utf-8'))
          root = Path('.agent/release-publisher')
          root.mkdir(parents=True, exist_ok=True)
          notes = root / 'release-notes.md'
          notes.write_text(request['notes'], encoding='utf-8')
          plan = root / 'release-plan.json'
          publisher_script = Path(os.environ['GITHUB_WORKSPACE']) / 'control-plane' / {publisher!r}
          subprocess.run([
              'python', str(publisher_script),
              '--repository', os.environ['GITHUB_REPOSITORY'],
              '--policy', '.coferlandia/release/policy.json',
              'plan',
              '--target', request['target_sha'],
              '--impact', request['impact'],
              '--version', request['version'],
              '--title', request['title'],
              '--notes-file', str(notes),
              '--output', str(plan),
          ], check=True)
          PY

      - name: Publish through Coferlandia release publisher
        working-directory: release-target
        shell: bash
        run: |
          python "$GITHUB_WORKSPACE/control-plane/{publisher}" \
            --repository "$GITHUB_REPOSITORY" \
            --policy .coferlandia/release/policy.json \
            publish \
            --input .agent/release-publisher/release-plan.json
"""


def render_publication_policy(
    policy: dict[str, Any], *, workflow_path: str, runs_on: Any = None
) -> dict[str, Any]:
    workflow = _safe_repo_path(workflow_path, suffixes=(".yml", ".yaml"))
    if not workflow.startswith(".github/workflows/"):
        raise ValueError("publication workflow must be under .github/workflows/")
    rendered = json.loads(json.dumps(policy))
    publication = rendered.setdefault("publication", {})
    if not isinstance(publication, dict):
        raise ValueError("publication must be an object")
    existing_github = publication.get("github")
    inherited_runs_on = existing_github.get("runs_on") if isinstance(existing_github, dict) else None
    resolved_runs_on = _normalize_runs_on(runs_on if runs_on is not None else inherited_runs_on)
    publication["github"] = {
        "mode": "issue-comment",
        "workflow": workflow,
        "runs_on": resolved_runs_on,
    }
    publication_github(rendered)
    return rendered


def atomic_write_text(path: Path, value: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_name(f".{path.name}.tmp")
    temp.write_text(value, encoding="utf-8")
    temp.replace(path)


def atomic_write_json(path: Path, value: dict[str, Any]) -> None:
    atomic_write_text(path, json.dumps(value, indent=2, sort_keys=True) + "\n")
