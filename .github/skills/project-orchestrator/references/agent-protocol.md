# Agent protocol

Every semantic attempt stores `request.json`, `instructions.md`, `agent-result.json`,
`agent-report.md`, execution metadata, and retained event evidence beneath the run directory.
JSON is authoritative; Markdown is a human-readable projection. Never request or persist
chain-of-thought.

In v2, the semantic execution identity is a **work item**, not a phase. Provider result schemas may
retain `phase_id` as a compatibility field during migration, but the controller fills it with the
current work-item ID and treats `work_item_id`/manifest task identity as authoritative runtime
semantics.

Worker instructions contain only bounded execution context:

- exact assigned implementation/review worktree;
- execution mode;
- local Epic contract path when present;
- exactly one assigned task/work-contract path;
- immutable candidate SHA for review;
- in-scope review findings for fix-agent;
- holistic-review marker when reviewing the integrated Epic.

Workers must not browse GitHub, perform Git lifecycle operations, or reconstruct the project plan. The supplied local Epic/analysis/task files are the frozen contract snapshot for the run; workers never check remote freshness.
Coding agents execute the supplied low-context contract; reviewers compare the exact immutable SHA
to that contract; fix agents address only approved in-scope findings.

Validate every result against the role schema before a state transition. Provider/model/session,
run/work-item/attempt identity, status, changed files, tests, blockers, deviations, findings, and
candidate/base SHAs remain durable controller evidence.
