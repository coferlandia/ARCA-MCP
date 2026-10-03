# Architecture Gate

```md
## Architecture Gate

Mode: the-architect | none
Status: required | passed | blocked | not-required
Assessment reference: <stable reference or none>
Addendum updated: <ISO-8601 or none>
Blocker: <none or concise reason>
```

Use the gate for new/cross-cutting subsystems, shared contracts, persistence/migrations,
security/trust boundaries, reliability/concurrency/transactions/eventing, deployment topology,
reusable component selection/extraction, major modernization or material performance constraints.
Do not require it for Retouch Mode or ordinary localized work.

When mode is `the-architect`, only `passed` permits execution. Absent and explicit `not-required`
gates are backward compatible. The addendum stays inside the plan between stable managed markers;
do not create a duplicate architecture file.

## Managed Architect Addendum

Keep the `## Architect Addendum` inside the Epic/specification between the stable
`coferlandia-architect-addendum` markers. Project Orchestrator materializes that complete Epic body
and must not create a second `ARCHITECTURE.md` artifact.
