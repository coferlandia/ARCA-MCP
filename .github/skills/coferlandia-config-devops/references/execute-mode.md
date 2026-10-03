# Execute Mode

Use only after proving this agent can invoke the target CLI and access the intended environment.

## Normal sequence

- `capabilities` and contract compatibility;
- consolidated `prepare-change`;
- review exact delta, warnings, effects, and plan fingerprint;
- obtain authority where required;
- `apply-plan --confirm`;
- inspect post-change effective values and validation;
- separate `activate` when restart/migration/downtime is material;
- health check and final report.

A mutation result is not completion unless effective state and required activation are verified.
When the CLI rejects a stale plan, prepare again; never force it. When the contract marks a field
secret, use stdin/native provider and redact all reports.
