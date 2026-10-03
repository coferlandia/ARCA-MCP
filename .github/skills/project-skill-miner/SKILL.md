---
name: project-skill-miner
description: >
  Use when the user asks to convert project documentation into local agent skills,
  mine a repository's docs for reusable agent workflows, extract operational recipes
  from README/RUNBOOK/AGENTS/DECISIONS plus current GitHub Issues/PRs, discover what project-specific
  skills should exist, or build project-local `.agents/skills/` entries from current
  documented procedures. This skill proposes candidates first, waits for explicit
  approval, and only then authors approved project-local skills under
  `.agents/skills/<skill-name>/`.
license: Apache-2.0
compatibility: >
  Requires read access to the target repository's documentation and write access to
  the target repository only if the controlling authority explicitly approves skill
  generation. When available in the environment, requires superpowers:writing-skills
  before authoring each approved generated skill, and follows its methodology.
metadata:
  author: community
  version: "1.1.0"
  category: meta
  status: active
  tested: "2026-07-07 - validated with _protocol/scripts/validate_skill.py; exercised
    the discovery and approval workflow against tests/fixtures/sample-project and the
    activation prompts in tests/cases.json."
---

## Context

This is a **meta-skill for skill discovery and skill authoring**, not for running the
project's operations itself. Its job is to inspect a project's documentation deeply,
separate live operational knowledge from stale or speculative material, propose which
project-local skills should exist, and only after explicit approval generate one local
skill per approved recipe.

The generated skills are always **project-local** and always live under the analyzed
repository's `.agents/skills/<skill-name>/` path. That destination is mandatory.

This skill is conservative on authority and safety:

- It never treats all docs as equally authoritative.
- It never generates skills silently.
- It never writes anything under `.agents/skills/` before explicit approval.
- It never upgrades stale, contradictory, or speculative notes into a skill without
  warning and explicit supervisor approval.

If the target project already uses documentation-archivist-style documents,
use them. If documentation is fragmented or contradictory, recommend archivist as a
preparatory step, but do not do archivist's full job here.

## Prerequisites

- Work from the target repository root.
- Read the repository's high-authority docs before proposing anything.
- Treat the controlling authority as external: human user, supervisor agent, or
  explicit approval workflow.
- If available, invoke superpowers:writing-skills before authoring each approved
  generated skill.
- If superpowers:writing-skills is unavailable, still follow its RED -> GREEN ->
  REFACTOR logic: test the candidate skill idea against documented evidence, write the
  minimum viable skill, then tighten loopholes before finishing.

## Steps

1. Identify the target repository root and confirm you are mining project
   documentation, not being asked to execute the documented operations directly.
2. Detect whether archivist-style docs exist. Check first for `README.md`,
   `AGENTS.md`, `RUNBOOK.md`, and `DECISIONS.md`. When GitHub is available, also inspect
   relevant current Issues/PRs as operational evidence. Treat `TODO.md`, `HISTORY.md`, and
   legacy `OPEN_QUESTIONS.md` only as migration evidence when they still exist. If sources
   are fragmented, missing, or contradictory, note that a documentation archivist would
   improve future mining, but continue with the evidence that exists.
3. Read sources in authority order. Start with `AGENTS.md` and `RUNBOOK.md`, then
   `DECISIONS.md`, `README.md`, relevant GitHub Issues/PRs and current repository state,
   and finally project-specific docs or docs folders. Legacy TODO/HISTORY/open-question
   files and scattered notes are supporting migration evidence only, never a newer
   authority than GitHub/current implementation state.
4. Search for operational language and recurring task patterns: `how to`, `run`,
   `deploy`, `debug`, `validate`, `ingest`, `release`, `migrate`, `reconcile`,
   `process`, `generate`, `test`, `rollback`, `configure`, `rotate`, `diagnose`,
   `maintenance`, `production`, and `local setup`.
5. Build a candidate recipe inventory. A valid candidate is current, operational,
   repeatable, project-specific, useful to an agent, safe to encode, and verifiable.
   Reject purely descriptive docs, historical one-offs, unresolved plans, generic
   tasks that need no project-specific behavior, and workflows that cannot be checked.
6. Evaluate each candidate against this matrix:
   - Is it current?
   - Is it operational and repeatable?
   - Is it project-specific?
   - Is it useful for automatic agents?
   - Are inputs and outputs clear?
   - Can success be verified?
   - Does it involve production, secrets, destructive actions, data sensitivity, or
     irreversible changes?
   - Is a skill the right artifact, or would a checklist/runbook section be better?
   - Which docs support it, and which files act as source of truth?
   - Does newer documentation contradict it?
7. Classify each candidate as one of:
   - `recommended` - current, well-supported, useful, and safe enough to encode
   - `optional` - useful but narrower, lower-value, or somewhat heavier-weight
   - `needs clarification` - likely useful, but the docs are ambiguous or conflicting
   - `rejected/stale` - obsolete, speculative, contradicted, or unsafe to encode by
     default
8. Detect staleness aggressively. Mark recipes stale when you find old dates
   contradicted by newer docs, references to removed scripts/directories, deprecated
   commands, superseded decisions, work already resolved by closed Issues/merged PRs,
   legacy TODO/HISTORY entries contradicted by GitHub or current repository state, or
   language such as `planned`, `future`, `proposal`, `draft`, `legacy`, `deprecated`,
   or `maybe`.
9. Produce a proposal and stop. For each candidate include proposed skill name, short
   description, operational task covered, why it should or should not be a skill,
   trigger/use cases, required inputs, expected outputs, safety or approval gates,
   verification method, confidence level, status, and traceable source documentation.
   Then explicitly request approval or adjustments before generating anything.
10. Wait for explicit approval. Allow the controlling authority to approve all,
    approve some, reject, rename, merge, split, defer, ask for more evidence, request
    another discovery pass, or insist that archivist runs first.
11. Before writing an approved generated skill, check whether
    `<repo-root>/.agents/skills/<skill-name>/` already exists. If it does, inspect the
    existing skill, report the conflict, explain whether it seems related/sufficient/
    stale/incomplete, and ask whether to update it, rename the new one, merge, or
    skip. Never overwrite blindly.
12. For each approved skill that is clear to generate:
    - Invoke superpowers:writing-skills when available.
    - Write exactly one project-local skill directory under
      `<repo-root>/.agents/skills/<skill-name>/`.
    - Keep the name stable, lowercase, kebab-case, and descriptive.
    - Scope the skill explicitly to the current repository.
    - Distill the procedure; do not copy long documentation blocks verbatim.
13. Ensure every generated skill includes at minimum:
    - skill name
    - short description
    - activation criteria
    - when to use it
    - when not to use it
    - required inputs
    - operational procedure
    - safety constraints
    - verification steps
    - expected outputs
    - failure handling
    - escalation or approval points
    - source documentation references
    - maintenance notes
    - example prompts that should activate it
    - example prompts that should not activate it
14. Harden dangerous procedures instead of copying them naively. Any generated skill
    involving deployment, production access, migrations, payments, credentials,
    certificates, destructive file changes, external APIs, customer/private data,
    financial data, infrastructure changes, or irreversible actions must include
    explicit approval gates and clearly distinguish inspection-only, local-only,
    staging/test, and production/destructive steps.
15. Validate each generated skill before reporting completion:
    - stored under `.agents/skills/<skill-name>/`
    - name is lowercase kebab-case
    - scoped to the current repository
    - includes activation criteria, required inputs, procedure, verification, safety,
      failure handling, approval gates, and source documentation
    - avoids unsupported invention, excessive verbatim copying, secrets, and hidden
      assumptions
16. Finish with a factual summary: skills created, paths written, source docs used,
    candidates rejected or deferred, open questions, risks or assumptions, conflicts
    encountered, whether existing skill directories required special handling, and
    whether archivist would improve future mining.

## Gotchas

- **Don't generate before approval:** a good proposal is still not approval. Stop
  after discovery and wait for an explicit yes, selected candidates, or requested
  adjustments.
- **Don't confuse documentation authority with file age alone:** an older `RUNBOOK.md`
  can still outrank a newer scattered note. Use authority plus recency, not recency
  alone.
- **Don't turn speculative or historical material into a live skill:** words like
  `draft`, `legacy`, `future`, `proposal`, or references to removed commands are a
  stale-signal, not a green light.
- **Don't write generated skills anywhere except `.agents/skills/<skill-name>/`:**
  never substitute `agents/skills`, `.claude`, `.cursor`, `.github`, `docs`,
  `superpowers`, or the global `skills/` tree.
- **Don't overwrite an existing project-local skill directory blindly:** inspect,
  report, and escalate the conflict for a decision.
- **Don't copy unsafe instructions as-is:** harden dangerous procedures with explicit
  approval gates, environment boundaries, and verification checkpoints.
- **Don't use this skill as a substitute for archivist:** if the docs are too messy to
  interpret confidently, recommend archivist rather than pretending certainty.

## Output Location

This skill writes no repository artifact during the discovery/proposal phase.

When the controlling authority approves generation, each generated project-local skill
must be written only to `<repo-root>/.agents/skills/<skill-name>/`.
No alternative destination is allowed.

## Expected Output

During discovery, use this proposal structure:

```md
# Project Skill Mining Proposal

## Repository

- Root: <repo-root>
- Archivist-style docs detected: yes | no | partial
- Notes: <fragmentation, conflicts, or documentation quality summary>

## Recommended Skills

1. `<skill-name>`
   - Description: <one-line purpose>
   - Task covered: <operational recipe>
   - Why this should be a skill: <why reusable and agent-valuable>
   - Trigger/use cases: <natural prompts or situations>
   - Required inputs: <inputs>
   - Expected outputs: <outputs>
   - Safety / approval gates: <none or explicit gate>
   - Verification: <how success is checked>
   - Confidence: high | medium | low
   - Source docs: <file paths and sections>

## Optional Skills

1. ...

## Candidates Needing Clarification

1. ...
   - Ambiguity: <what conflicts or is missing>

## Rejected / Stale Recipes

1. ...
   - Reason rejected: <obsolete / contradicted / speculative / generic / unsafe>

## Approval Request

Choose one or more:
- approve all recommended
- approve selected skills
- rename / merge / split candidates
- defer unclear candidates
- request more evidence
- request another discovery pass
- run archivist first
```

After approval and generation, use this summary:

```text
Project-local skills created: <count>
Paths written:
- <repo-root>/.agents/skills/<skill-name>/
Source docs used:
- <path>
Rejected or deferred candidates:
- <name>: <reason>
Conflicts handled:
- <existing dir>: <decision>
Open questions:
- <item>
Risks / assumptions:
- <item>
Archivist recommended next: yes | no | maybe
```

## References

- Read `tests/cases.json` when you need quick positive/negative activation examples.
- Read `tests/fixtures/sample-project/` when you need a concrete repository fixture
  containing recommended, dangerous, ambiguous, and stale recipe candidates.
