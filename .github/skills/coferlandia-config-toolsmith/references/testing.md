# Testing and completion

## Contract tests

- valid contract passes;
- duplicate module/field keys fail;
- forbidden current/effective/snapshot/secret values fail at any depth;
- secrets require a safe write method;
- unsupported standard-pack bindings are reported, not silently generated.

## Candidate tests

- list/show are read-only;
- approve promotes exactly one field, regenerates facade/docs, validates drift, and only then marks implemented;
- stale fingerprint rejects approval;
- reject/defer/intentionally-unmanaged preserve the contract;
- dry-run changes no file;
- repeated decisions are idempotent or fail clearly.

## Generation tests

- identical inputs produce byte-identical outputs;
- dry-run reports paths without writing;
- every generated file has a generator marker;
- generated docs cover every managed field;
- generated Python facade reads live native values;
- dotenv/JSON writes are atomic, repository-confined, preserve unrelated content, verify unset/secret operations, and roll back failed multi-artifact application;
- plans reject changed native state, contract drift, duplicate keys, and tampered plan hashes;
- secret output is always redacted;
- no runtime configuration store is created.

## Behavioral tests

Use fixture projects for Python native integration, .NET integration metadata, Python fallback,
multiple native sources, secrets, read-only fields, ambiguous candidates, restart boundaries, and a
search miss resolved by exhaustive handbook review.

## Final review

Search the diff for copied live values, secrets, new settings stores, application imports of the
contract, direct-store edits bypassing the facade, unclassified signals, and documentation examples
that do not match the CLI. Run repository-wide skill validation and release maintenance before final
integration.
