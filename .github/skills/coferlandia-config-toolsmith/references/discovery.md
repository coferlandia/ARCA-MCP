# Discovery and classification

## Read-only inventory

Inspect every relevant location before editing:

- framework settings/options models and native loader composition;
- existing CLI/admin API surfaces;
- environment variables and `.env` examples;
- JSON, YAML, TOML, INI, appsettings, and deployment manifests;
- Docker Compose, Kubernetes, CI/CD, and startup scripts;
- secret managers and credential-source abstractions;
- database-backed settings and migrations;
- defaults embedded in code;
- validation performed at startup or write time;
- tests, examples, operations docs, and deprecated aliases.

For each signal, capture evidence for definition and at least one consumer when available. A similar
name is not proof of semantic equivalence. An example value is not an authoritative default.

## Bounded investigation

For one signal:

1. locate the strongest authoritative definition;
2. locate a consumer or corroborating use;
3. identify effective read resolution;
4. identify a safe writable target, if any;
5. determine validation, secret treatment, and effects;
6. resolve obvious contradictions;
7. when material uncertainty remains, create a candidate and continue.

Do not repeatedly ask the user about each field during the initial pass. Stop the entire run only
when no target, no configuration authority, or no safe integration location can be established.

## Dispositions

- `MANAGED`: sufficiently understood and safely representable.
- `CANDIDATE`: likely configurable but materially ambiguous.
- `INTENTIONALLY_UNMANAGED`: configuration remains outside the facade by explicit decision.
- `READ_ONLY_OR_DERIVED`: useful to inspect but not persist through the facade.
- `UNSUPPORTED`: requires an unavailable adapter/capability.
- `OBSOLETE`: historical signal not active in current behavior.

Every discovered signal appears in the ledger. `UNCLASSIFIED` must be zero before completion.
