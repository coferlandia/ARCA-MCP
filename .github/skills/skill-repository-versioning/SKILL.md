---
name: skill-repository-versioning
description: >
  Use before a commit that changes what a skill repository ships — a skill added,
  removed, renamed, or deprecated, or a plugin manifest, skill index, or protocol doc
  changed — in a repository that tracks a repo-wide release version separately from
  each skill's own metadata.version. This is what tells clients a new version is worth
  reimporting. Not for a routine content edit to a single skill's own instructions —
  that's covered by the repo's general dev-process skill, not this one.
license: Apache-2.0
compatibility: >
  Requires read/write access to the repository and Python 3.11+ to run its skill
  validator and version-bump scripts. Assumes the repo follows the layout described
  in Context below; adapt paths if a given repo names things differently.
metadata:
  author: community
  version: "1.2.0"
  category: meta
  status: active
  tested: "2026-08-01 - repository-local release-maintainer deferral covered by contract review; prior validator and activation evidence remains applicable."
---

## Context

Skill repositories that track versions well tend to use two independent axes: **each
skill's own version** (that skill's instructions changed) and a **repo-wide release
version** (the installable surface changed — a skill was added or removed, packaging
changed). Committing without checking both is how they drift apart, and how a skill
index quietly stops matching what's actually in the repo.

This skill governs only the second axis: the repo-wide release version, and the
version-tracking artifacts that go with it. A single skill's own `metadata.version`
is the concern of the repo's general dev-process skill, not this one — this skill
exists specifically for the moments a client needs to know "there's a new version
worth reimporting," not for every ordinary content edit.

### Repository-local override

Before applying this generic workflow, check whether the target repository provides a
more specific local release skill, especially:

```text
.agents/skills/*release*/SKILL.md
```

When a repository-local skill explicitly owns final-delivery versioning, manifests,
documentation, packaging, or release readiness, load and follow that skill instead and
stop this generic workflow. Do not combine two competing release protocols or let this
weaker reusable default override repository-specific policy.

This skill assumes a layout like coferlandia-skills uses; adapt names if a given repo
differs:

- A skill index file listing every skill, its description, and status.
- A per-skill `metadata.version` field, bumped on that skill's own iteration.
- A mechanical skill validator script.
- A `.version-bump.json`-style config declaring which manifest files carry the
  repo-wide release version, plus a script with `--check` (report drift),
  `--audit` (find undeclared files still containing the version string), and a bump
  command.
- A `RELEASE-NOTES.md`-style changelog for the repo-wide releases.

If a repository doesn't have one of these pieces yet, treat that step as not
applicable rather than blocking the commit on it.

## Prerequisites

- Read/write access to the repository.
- Python 3.11+ (or whatever runtime the repo's validator/bump scripts use).

## Steps

1. **Identify what changed.** Classify the pending change as one or more of: a skill
   added, a skill removed or deprecated, a skill's status or category changed, a
   skill's instructions edited in place, or a repo-level file changed (index, README,
   protocol docs, plugin manifests, packaging scripts).

2. **Bump the skill's own version, if its instructions changed.** If a skill's body or
   triggering description changed behavior (not just a typo fix), bump that skill's
   own version field following the repo's naming convention, and update its "tested"
   evidence honestly — don't claim a test that didn't happen.

3. **Update the skill index if the inventory changed.** Required whenever a skill was
   added, removed, renamed, or its status changed. Use the row format the index itself
   defines — don't invent a different one.

4. **Run the mechanical skill validator** across all skills. Fix any failure before
   continuing.

5. **Decide whether this commit needs a repo-wide release version bump.** Bump when
   the installable surface changed:
   - a skill was added, removed, or deprecated
   - a plugin manifest, license file, or packaging script changed
   - a protocol/shared doc changed in a way that affects every skill, not just one

   Don't bump for a content-only iteration on a single existing skill (a Gotcha added,
   wording tightened, a bug fixed) — that's covered by step 2 alone unless the target
   repository's local policy defines a stronger release boundary.

6. **If a release bump is needed:** run the repo's version-check command first to
   confirm there's no pre-existing drift, then bump to a new version chosen by semver
   (patch for fixes, minor for additive changes like a skill added, major for removals
   or breaking changes). Add an entry to the repo's release-notes file describing what
   changed and why. Then run the audit command to confirm no manifest file was missed.

7. **Re-verify before committing:** re-run the skill validator and the version-check
   command. Both must pass clean.

## Gotchas

- **Ignoring a repository-local release skill:** wrong. Local policy owns when it exists;
  stop this generic workflow after handing off.
- **Bumping the release version for every small skill tweak:** this inflates releases
  with no real signal unless the repository's local policy explicitly makes shipped
  skill changes part of every plugin release.
- **Forgetting the skill index after adding or removing a skill:** the mechanical
  validator checks each skill's own frontmatter, not whether the index matches
  reality — that check is manual, and it's step 3, not optional.
- **Editing a skill's instructions without touching its version or tested evidence:**
  leaves a false "tested" claim attached to changed behavior.
- **Running the bump command without checking for drift first:** papers over drift
  that already existed instead of surfacing it.
- **Skipping the audit step after a bump:** a manifest file that isn't yet declared in
  the version-bump config won't get bumped and won't be flagged unless the audit runs.
- **Trusting a file was written correctly just because the write call succeeded:**
  re-parse JSON/Markdown inputs after writing before trusting the result.
- **Treating this skill as the place to write the commit message:** it isn't —
  proposing a commit message from the actual diff is the repo's general dev-process
  skill's job.

## Output Location

This skill modifies files in place at their standard repository locations
(RELEASE-NOTES.md, skills/INDEX.md, per-skill SKILL.md frontmatter, plugin
manifests). These are standard repo management artifacts, not generated outputs, so
`.agent/` does not apply.

## Expected Output

```text
## Pre-commit versioning check: {commit topic}

Repository-local release workflow: {delegated to path | none}
Skills changed: {list, or "none"}
Index updated: yes | no | not needed
Skill-level version bumps: {skill: old -> new, ...} | none
Skill validator: PASS | FAIL ({errors})
Release bump needed: yes | no
  If yes: {old version} -> {new version}; release-notes entry added
Version drift check: PASS | FAIL
Version audit: PASS | FAIL
Ready to commit: yes | no
```

## References

- In coferlandia-skills specifically: `_protocol/NAMING_CONVENTIONS.md`,
  `_protocol/SKILL_LIFECYCLE.md`, `_protocol/QUALITY_STANDARDS.md`,
  `_protocol/scripts/validate_skill.py`, `_protocol/scripts/bump_version.py`,
  `.version-bump.json`, and `RELEASE-NOTES.md`.
