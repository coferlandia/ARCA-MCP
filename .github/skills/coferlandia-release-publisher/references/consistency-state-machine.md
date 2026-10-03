# Consistency State Machine

The publisher classifies the requested version/tag against the exact planned target SHA before any publication mutation.

| State | Observed condition | Publication behavior |
|---|---|---|
| `NEW` | no tag, no GitHub Release | eligible to create after preflight |
| `TAG_ONLY_CORRECT` | annotated tag exists and peels to target; no Release | resume at draft Release creation |
| `DRAFT_CORRECT` | correct annotated tag and matching draft Release | resume asset/provenance verification and publish |
| `PUBLISHED_CONSISTENT` | correct annotated tag and matching published Release | return `already_consistent` after verification |
| `INCONSISTENT` | identity disagreement, lightweight tag, Release without tag, wrong SHA, conflicting provenance/asset | fail closed; no destructive repair |

## Publication transition

```text
NEW
  -> create annotated tag at exact target
  -> push exact tag ref without force
  -> re-read remote tag
  -> TAG_ONLY_CORRECT
  -> create GitHub Release draft using the existing tag
  -> DRAFT_CORRECT
  -> upload/verify declared assets
  -> upload/verify optional provenance
  -> publish draft
  -> re-read tag + Release
  -> PUBLISHED_CONSISTENT
```

Each invocation re-reads authoritative state. A failure after tag or draft creation leaves a resumable state rather than rolling back by deleting public identity.

## Stale-plan guard

A dry-run records a fingerprint of target SHA, allowed refs, published release lineage, previous release, required-check evidence, and immutable-release observation. `publish` recalculates this immediately before the first remote mutation. A mismatch means the plan is stale and must be regenerated.

If the planned release has independently become fully consistent, `publish` may return `already_consistent` without recreating it.

## Race at tag push

If a non-force tag push loses a race because another publisher created the tag, re-read the remote tag. Continue only when it is annotated and peels to the exact planned target SHA. Otherwise preserve the conflict and fail.

## Assets/provenance

A same-name draft/published asset is reusable only when its observable SHA-256 matches the planned digest. Missing/unobservable digest for an existing conflicting name is not treated as equality. Never overwrite a conflicting published asset.

When `release-manifest.json` exists, its repository/tag/commit identity must agree with the tag and GitHub Release.

## Prohibited automatic repair

Never use:

```text
git tag -f
forced tag push
release deletion/recreation to change identity
published asset replacement to hide drift
silent tag movement
```

A human/operator may investigate and choose a repository-specific recovery outside this generic automatic workflow, but the publisher never disguises inconsistent published state as success.
