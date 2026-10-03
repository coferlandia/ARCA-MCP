---
name: local-ci
description: >
  Use when a Coferlandia development candidate has durable READY_FOR_CI evidence and Qualification
  is being performed through an Agent Skills/local execution surface, or when local CI is named
  explicitly. Invoking this skill selects LOCAL qualification; it never runs GitHub-native CI or merge.
license: Apache-2.0
compatibility: >
  Requires read/write access to the target repository, git, Python 3.11+ for profile validation,
  and every runtime/service required by the target repository's .coferlandia/ci/profile.json.
metadata:
  author: coferlandia
  version: "1.2.0"
  category: engineering
  status: active
  tested: "2026-09-25 - invocation ownership, no-fallback, base-sensitive pre-Qualification currentization barrier, and READY_FOR_MERGE semantics covered by contract tests."
---

## Context

`local-ci` is the local execution surface for the shared Coferlandia Qualification stage:

```text
READY_FOR_CI -> LOCAL qualification -> READY_FOR_MERGE
```

It is an Agent Skill, not the Chat `ci.md` prompt. **Invoking this skill selects `LOCAL`; no separate strategy router or strategy-choice step is required after activation.** GitHub-native Qualification belongs to the explicitly invoked Chat `ci` controller and is outside this skill.

This skill **must not run GitHub-native CI** as fallback and **must not merge**.

The target repository's real scripts, services and tools remain authoritative. This skill consumes the single repository definition at `.coferlandia/ci/profile.json`; it does not contain project test commands itself.

## Activation boundary

Activate this skill when the current task is to qualify or resume Qualification of a development candidate through an Agent Skills/local execution surface and current durable `READY_FOR_CI` evidence exists, or when the controlling instruction explicitly names local CI / `local-ci`.

Do not activate merely because a bounded controller such as `chat-coder` reached `READY_FOR_CI`; that controller's terminal boundary still applies. Do not activate for a generic test command, a request to diagnose GitHub Actions, an explicit Chat `ci` / `gh ci` / `github ci` invocation, merge/integration, or release publication.

Once this skill is active, the Qualification strategy is `LOCAL`. Do not ask a second strategy-selection question and do not consult the Chat prompt registry to choose between LOCAL and GITHUB_NATIVE.

## Preconditions

1. Read current repository instructions and the work contract.
2. Resolve the current PR/work candidate and durable `READY_FOR_CI` comment using the shared `_protocol/delivery/READY_FOR_CI.md` contract.
3. Prove handoff Candidate SHA equals current PR head/current assigned candidate.
4. Load `.coferlandia/ci/profile.json` and validate/fingerprint it with `coferlandia-ci-adapter` tooling when available.
5. Confirm the profile has a non-empty `local.qualification_commands` list and required environment/services are available.
6. Require `Base SHA synchronized` from the handoff. For `identity.base_sensitive = true`, re-read the current authoritative base and prove it still equals that synchronized SHA and is reconciled into the candidate before starting any expensive local command.

A stale/missing handoff or invalid profile blocks qualification. Do not substitute older local results or remote CI evidence. Base-sensitive drift is `DEVELOPMENT_CURRENTIZATION_REQUIRED`: Local CI must not currentize or mutate the candidate inside Qualification. Return to the already-resolved upstream Development flow when one exists; otherwise report the state and stop.

## Workflow

1. Keep work on the exact candidate branch/worktree assigned by the repository's active development workflow.
2. Re-read current authoritative base according to the profile identity rules and verify it matches the Development-currentized handoff. Do not change the candidate here.
3. Start/verify only services explicitly required by the profile; never invent infrastructure requirements.
4. Execute `local.qualification_commands` in declared order from `local.working_directory`. Record exact command, candidate SHA, start/result and relevant summarized output.
5. If a command fails, use focused diagnosis/systematic debugging. Make only bounded in-scope corrections permitted by the work contract and active development role.
6. A material correction creates a new candidate: refresh development review/validation and `READY_FOR_CI`, then restart Local CI from the beginning for the new SHA.
7. Do not switch to Chat/GitHub-native CI because Local CI fails or a local dependency is unavailable.
8. After all profile-defined local qualification is GREEN, re-read current candidate/base/profile fingerprint and required review state.
9. Create/update the shared durable `READY_FOR_MERGE` comment with strategy `LOCAL`.

## READY_FOR_MERGE evidence

Use marker:

```html
<!-- coferlandia-ready-for-merge:v1 -->
```

and fields from `_protocol/delivery/READY_FOR_MERGE.md`, including:

```text
Qualification strategy: LOCAL
CI profile fingerprint: <sha256>
Qualification evidence: <ordered command/result evidence for exact candidate>
```

The envelope must match the Chat CI strategy's candidate/base/profile fields. Only the strategy-specific evidence differs.

## Requalification

Read `references/requalification.md` when candidate, base, profile or validation contract changed after qualification. Never authorize merge with old-SHA or old-profile evidence.

## Failure boundaries

- Missing local runtime/service: report `LOCAL_QUALIFICATION_BLOCKED`; do not fall back to GitHub-native CI.
- Test/product failure: diagnose and correct only in scope, then create fresh candidate-bound evidence.
- Base/profile drift: stop and reconcile/requalify according to shared rules.
- Explicit Chat `ci` invocation: this skill is not the selected controller; leave GITHUB_NATIVE handling to the Chat prompt.
- Exceptional/HOTFIX lane: follow only repository-owned documentation and external authorization. This skill never invents or grants exceptional-lane authority.

## Expected Output

```text
Qualification state = READY_FOR_MERGE | DEVELOPMENT_CURRENTIZATION_REQUIRED | LOCAL_QUALIFICATION_BLOCKED | FAILED
Strategy = LOCAL
Issue = <identity>
PR = <number>
Candidate SHA = <sha>
Qualified base SHA = <sha>
CI profile fingerprint = <sha256>
Qualification evidence = <commands/results>
Review Critical = 0
Review Important = 0
Next stage = Merge (only when READY_FOR_MERGE)
```

## Output Location

This skill writes no repository configuration. Its durable workflow evidence is the managed PR comment defined by the shared protocol. Test/build artifacts remain owned by the target repository's commands.

## References

- Read `references/qualification-contract.md` before producing READY_FOR_MERGE evidence.
- Read `references/repository-profile.md` when interpreting `.coferlandia/ci/profile.json`.
- Read `references/failure-and-correction.md` for a failed local command or bounded correction.
- Read `references/requalification.md` whenever candidate/base/profile identity may have changed.
