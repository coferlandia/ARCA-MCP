# GitHub-native release publication contract

Use this reference only when repository adaptation explicitly includes complete Coferlandia GITHUB_NATIVE release delivery.

## Boundary

Qualification, Integration and Publication are separate authorities:

- `.coferlandia/ci/profile.json` describes Qualification only.
- repository release policy describes Integration/Publication topology and local decisions.
- `chat-release` orchestrates the GITHUB_NATIVE release lifecycle.
- `coferlandia-release-publisher` exclusively owns exact Commit -> annotated tag -> GitHub Release mechanics.
- the repository publication workflow is only a GitHub-native transport for the publisher.

Never add publication fields to `.coferlandia/ci/profile.json`.

## Policy extension

The adapter owns only the optional `publication.github` transport block. Other `publication` members belong to repository policy or another controller and MUST be preserved.

Supported forms:

```json
{
  "publication": {
    "github": {
      "mode": "none"
    }
  }
}
```

or the standard Chat-compatible form:

```json
{
  "publication": {
    "github": {
      "mode": "issue-comment",
      "workflow": ".github/workflows/coferlandia-release-publish.yml",
      "runs_on": "ubuntu-latest"
    }
  }
}
```

Repositories using self-hosted publication runners declare the exact GitHub Actions labels explicitly, for example:

```json
{
  "publication": {
    "github": {
      "mode": "issue-comment",
      "workflow": ".github/workflows/coferlandia-release-publish.yml",
      "runs_on": ["self-hosted", "Linux", "ARM64", "coferlandia-ci", "docker"]
    }
  }
}
```

A client that exposes Actions dispatch may instead use `mode: workflow-dispatch` with the same workflow-path and runner contracts. `workflow` must always be a repository-relative YAML path below `.github/workflows/`.

`runs_on` is optional for backward compatibility. When absent during adaptation, the generated workflow defaults explicitly to `ubuntu-latest`. When repository study shows that GitHub-hosted runners are not the intended publication surface, the adapter MUST materialize the real repository labels instead of assuming a hosted runner. `runs_on` may be one non-empty single-line string or one non-empty duplicate-free list of non-empty single-line strings.

Do not infer publication runner labels from `.coferlandia/ci/profile.json`: Qualification and Publication are separate contracts even when the repository happens to use the same runner pool for both.

Absence of `publication.github` is backward-compatible and means the generic GitHub-native release controller has no declared publication transport. It must stop with `RELEASE_PUBLICATION_BLOCKED`; it must not infer LOCAL fallback or perform ad-hoc tag/release mutation.

## Canonical publication identity

Every transport carries exactly the release identity facts that must already be resolved before publication:

- `target_sha` — exact 40-character integrated commit SHA.
- `version` — explicit SemVer without repository tag prefix.
- `impact` — approved semantic impact: `patch`, `minor`, or `major`.
- `title` — explicit GitHub Release title.
- `notes` — final release notes.

These are transport values, not decisions delegated to GitHub Actions. The workflow MUST NOT infer a target, version, impact, title, notes, deployment, or environment promotion.

## Standard Chat-compatible request

The adapter generates `issue-comment` because the Chat GitHub surface can create a top-level release-PR comment even when it cannot call Actions `workflow_dispatch` directly.

The canonical marker is:

```html
<!-- coferlandia-release-publication-request:v1 -->
```

The marker is followed by one fenced `json` object containing exactly:

```json
{
  "schema": 1,
  "target_sha": "<40-char-sha>",
  "version": "<semver-without-tag-prefix>",
  "impact": "patch|minor|major",
  "title": "<release-title>",
  "notes": "<final-release-notes>"
}
```

The request is durable control-plane evidence, not a secret-bearing channel. Never place credentials or deployment instructions in it.

## Generated workflow invariants

The standard generated workflow must:

1. run only for a newly created `issue_comment` containing the canonical marker on a PR;
2. use the repository-declared `publication.github.runs_on` runner contract, defaulting only when adaptation intentionally accepts `ubuntu-latest`;
3. require the commenting actor to have repository `admin` or `maintain` permission;
4. require the referenced PR to already be merged/integrated;
5. require request `target_sha` to equal that same PR's exact `merge_commit_sha`, binding the publication identity to the durable release work surface;
6. request only `contents: write`, `issues: read`, and `pull-requests: read`;
7. serialize publication attempts for the release work surface without cancelling an in-progress publication;
8. parse the marker/JSON request from `$GITHUB_EVENT_PATH` as data rather than shell-evaluating comment text;
9. validate exact SHA, SemVer, impact, title and notes before checkout;
10. checkout exactly `target_sha` with full history/tags and push credentials available;
11. configure a non-interactive Git identity for annotated tag creation;
12. revalidate `HEAD == target_sha` before publisher invocation;
13. materialize notes without shell-evaluating their content;
14. build a deterministic publisher plan from exact request data;
15. execute `coferlandia-release-publisher publish` from that plan;
16. never deploy or invoke repository deployment workflows.

The target repository supplies the repository-relative publisher entrypoint and publication runner contract during adaptation. The adapter rejects absolute publisher paths, parent traversal, empty runner labels, duplicate labels, and multiline labels.

## Controller behavior

`chat-release` must re-read repository policy after integration. For `issue-comment`, it revalidates the integrated SHA and release identity, creates one canonical request comment on the durable release PR/work surface, binds to the workflow run causally triggered by that exact comment, waits for a successful terminal conclusion, then independently verifies the resulting annotated tag and GitHub Release resolve to the exact integrated commit.

For `workflow-dispatch`, the same identity and verification rules apply, but it is usable only when the active client actually exposes a dispatch primitive.

A missing workflow, unsupported mode, unavailable publication runner, insufficient actor authority, trigger capability gap, ambiguous run binding, RED workflow, stale SHA, release-PR integration mismatch, or release/tag mismatch is a publication blocker. None authorizes direct shell mutation or a LOCAL fallback.

## Adaptation review checklist

Before materializing publication support, verify:

- repository policy file and ownership are current;
- existing sibling `publication` fields will be preserved;
- the publisher skill is vendored/available at the proposed repository-relative path;
- the workflow path does not conflict with repository conventions;
- the publication runner actually available to this repository is known; if self-hosted, capture its exact required labels explicitly;
- the repository uses a release PR/work surface compatible with issue comments when `issue-comment` is selected;
- the release integration mechanism yields a stable PR `merge_commit_sha` equal to the exact commit that should become the formal release identity;
- GitHub Actions is allowed to create tags/releases with `GITHUB_TOKEN` and `contents: write` under repository rules;
- expected release authorities have `admin` or `maintain` repository permission;
- no environment/deployment side effect is attached to the publication workflow;
- the publication workflow itself is covered by repository review/CI policy before use.

After materialization, validate both policy and generated workflow and confirm the CI profile fingerprint did not change unless Qualification facts also changed.
