# Adapters and platform packs

## Selection order

1. Extend an existing CLI when it already has a stable command tree and can call the native loader.
2. Build a native companion CLI in the primary project platform.
3. Use the generated Python fallback over explicitly approved native stores/APIs.

## Adapter contract

Every adapter must provide semantic equivalents of:

```text
read_effective(field)
read_native(field)
validate(field, candidate)
set_native(field, value)
unset_native(field)
state_fingerprint(fields)
```

Writes must be atomic or transactional where the native mechanism supports it. The adapter must not
change native precedence, create a new settings store, or infer that a writable file is authoritative.

## Python pack

Prefer importing the project's settings module or configuration service. When that is impossible,
the generated fallback supports `env`, `dotenv`, and JSON bindings using standard-library code.
The fallback reads values live and writes only the contract-declared native target.

## .NET pack

Prefer integrating into the application's existing command host and resolving values through
`IConfiguration`/Options. Persist only through an existing project-owned provider or approved
appsettings file. The deterministic pack emits a dependency-free companion scaffold and integration
contract; project-specific provider registration stays in the target repository.

## Secret adapters

Secret values never appear in the contract, generated docs, command arguments, plans, logs, or
structured output. Use `secret set <key> --stdin` or the native secret provider. A secret read reports
presence/source, not the value.

## Custom adapters

When a database, remote service, or proprietary loader requires a custom adapter:

- keep the implementation small and project-local;
- bind it to an existing native authority;
- add conformance tests for read, validation, write, unset, dry-run, stale-state, and redaction;
- document deployment/access prerequisites;
- do not mark the field managed until the adapter is proven.
