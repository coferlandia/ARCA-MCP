# Release Model

## Authority

The generic release identity is:

```text
version -> annotated Git tag -> exact Git commit SHA
                         \
                          -> GitHub Release
```

The annotated tag is the Git-native version identity. The GitHub Release publishes that existing tag with human notes and optional assets. A `release-manifest.json` asset may add provenance, but it never replaces tag + GitHub Release as the minimum authority.

A published version is not automatically repaired by moving/deleting/recreating its identity. Any disagreement among tag, Release, target SHA, or provenance is an inconsistency.

## Target commit

The target can be `HEAD` or an older commit. Eligibility is based on the exact commit object and allowed release refs, not on the current checkout. The generic publisher does not amend the target or create a declaration commit.

The target must be reachable from at least one allowed release ref. The default allowed ref is the repository default branch returned by GitHub; local policy may add maintenance/hotfix refs.

## Previous release

For each published GitHub Release whose tag matches the configured SemVer scheme:

1. dereference the tag;
2. require an annotated tag;
3. require its commit to be an ancestor of the target;
4. select the nearest ancestor by Git history;
5. fail when equally-near candidates on distinct commits are genuinely ambiguous.

Do not select the previous release only by publication time or largest version across divergent histories.

## Versioning

Generic v1 uses Semantic Versioning. Semantic impact is agent input based on compatibility evidence; the deterministic CLI validates syntax, precedence, and that the chosen version does not understate PATCH/MINOR/MAJOR impact.

A first release requires an explicit version. Existing non-SemVer GitHub Release history is not silently migrated by this generic contract.

## Release notes

Release notes are semantic human output supported by repository evidence. Prefer consumer/operator-significant changes. Commit messages are evidence, not release notes by themselves.

## Artifacts and provenance

Declared artifacts must already exist. The publisher records SHA-256 before publication and verifies the file again before upload. Existing same-name assets must have a matching GitHub-reported digest; conflicting published assets are not overwritten.

Optional provenance is generated outside the target commit under `.agent/release-publisher/` and uploaded as `release-manifest.json`. It records the repository, version/tag/SHA, previous release, impact, policy schema, plan hash, and artifact hashes.

## Downstream contract

`resolve` returns a normalized, read-only record with:

```text
repository
version
tag
commit
created_at
published_at
title
release_notes
prerelease
immutable
previous_version
previous_tag
artifacts
provenance
consistency
```

Consumers such as a deployment controller resolve releases through this contract and then perform their own deployment logic. The release publisher never answers which release is deployed on a host.
