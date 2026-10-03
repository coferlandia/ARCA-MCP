# Reporting contract

Separate facts by evidence source:

- known from static contract;
- observed live through the CLI;
- requested by the user;
- inferred operationally;
- unknown/unverified.

Always report mode, target, execution status, contract/CLI version, exact changed fields (secrets
redacted), native artifacts/providers, prepare/apply/validation/activation outcomes, warnings,
rollback, and unresolved candidates.

Guide Mode must explicitly state that commands were not executed by the guiding agent. Execute Mode
must include actual command results and may claim completion only after post-change validation and
required activation/health checks succeed.
