# Agile architecture survey

Survey incrementally and mark each important fact `confirmed`, `inferred`, `unknown`, `stale`,
`conflicting`, or `not-applicable`, with source/confidence for material inferences.

Capture only decision-relevant information:

- product purpose, criticality, lifecycle, roadmap and failure consequences;
- system boundary, actors, containers, integrations and trust boundaries;
- module responsibilities, dependency direction, contracts, state and concurrency;
- data ownership, sensitive boundaries, consistency, retention, backup/recovery and auditability;
- deployment topology, configuration/secrets, observability, rollback and operational ownership;
- testability, change hotspots, dependency health and onboarding risk;
- three to six prioritized quality attributes and five to ten measurable quality scenarios;
- near-term Architectural Runway tied to probable product work.

Prefer C4 context/container depth for the baseline. Add deeper diagrams only when they change a
decision. Do not survey every possible quality characteristic.
