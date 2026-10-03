# Release Policy Contract

The policy file is optional. `--policy <path>` has highest priority; otherwise the CLI reads `.coferlandia/release/policy.json` when present. Absence means safe generic defaults and never creates a file.

## Schema v1

```json
{
  "schema_version": 1,
  "versioning": {
    "scheme": "semver",
    "tag_prefix": "v"
  },
  "release_refs": [],
  "tag": {
    "type": "annotated",
    "signing": "optional"
  },
  "validation": {
    "required_github_checks": []
  },
  "github_release": {
    "enabled": true,
    "immutability": "observe"
  },
  "provenance": {
    "manifest": "optional"
  }
}
```

Repositories may add local controller fields that the generic publisher does not own. One optional generic transport extension is recognized under `publication.github`:

```json
{
  "publication": {
    "github": {
      "mode": "issue-comment",
      "workflow": ".github/workflows/coferlandia-release-publish.yml"
    }
  }
}
```

`mode` may be `none`, `issue-comment`, or `workflow-dispatch`. A workflow mode requires exactly one repository-relative YAML `workflow` below `.github/workflows/`. The publisher validates this transport declaration when present but does not itself trigger the workflow; orchestration belongs to `chat-release` or another explicit controller. Sibling repository fields under `publication` are preserved/ignored by the generic publisher rather than rejected.

## Semantics

- `versioning.scheme`: generic v1 supports only `semver`.
- `tag_prefix`: commonly `v`; it is separate from the semantic version itself.
- `release_refs`: allowed integration/maintenance refs. Empty means the GitHub repository default branch. A simple branch name is normalized to `refs/heads/<name>`.
- `tag.type`: v1 requires `annotated`.
- `tag.signing`: `disabled`, `optional`, or `required`. `required` uses local Git signing and publication fails if the signing environment cannot create the tag.
- `validation.required_github_checks`: exact check-run names required to be `completed/success` for the target commit. Empty means no generic remote-check requirement is invented.
- `github_release.enabled`: must be true in generic v1.
- `github_release.immutability`: `disabled`, `observe`, or `required`. `required` uses GitHub's repository immutable-releases status endpoint as a publication preflight. `observe` records the setting when permission permits but does not block merely because it is unobservable.
- `provenance.manifest`: `disabled`, `optional`, or `required`. In v1 both `optional` and `required` generate/verify the manifest during publication; `required` expresses repository policy rather than making the manifest the primary authority.
- `publication.github`: optional orchestration transport only. It never changes the publisher's Commit -> Release authority or publication order.

## GitHub-native transport boundary

The transport is deliberately separate from the deterministic publisher engine:

- `issue-comment` is suitable for Chat GitHub surfaces that can create comments but cannot dispatch workflows directly. The repository workflow validates a versioned control marker/request, actor authority and exact target identity before invoking the publisher.
- `workflow-dispatch` is suitable for clients that expose an explicit Actions dispatch primitive.
- `none` or absence means there is no declared generic GitHub-native publication transport.

A transport MUST carry already-resolved publication facts into the publisher. It must not infer target SHA, SemVer, semantic impact, title, notes, deployment target, or release authority. Missing/unsupported transport is a controller-level `RELEASE_PUBLICATION_BLOCKED` condition, not permission to run ad-hoc tag/release shell commands.

## Local publication skill precedence

This JSON policy configures the generic deterministic engine. It does not replace semantic repository-local skill precedence. The agent first checks whether a repository-local contract explicitly owns final Commit -> published Release. If so, that stronger publication contract owns the operation instead of this generic workflow.

A repository-local release-preparation/versioning gate that stops before tags/GitHub Releases may compose before this publisher; it does not automatically replace it.

## Unsupported or ambiguous schemes

If published GitHub Release history does not conform to the configured SemVer/tag-prefix model, generic v1 fails rather than mixing version schemes. Use a stronger repository-local publication contract for a deliberate legacy scheme/migration.
