# Product Knowledge profile contract

Read this reference when creating, changing, or validating `.coferlandia/product-knowledge/profile.json`.

## Purpose

The profile declares where product-knowledge surfaces live and how release verification treats them. It is not a product catalog and must not contain current/effective capability values, runtime state, customer data, credentials, secrets, or marketing claims.

## Schema v1

```json
{
  "schema_version": 1,
  "surfaces": [
    {
      "id": "end-user-help",
      "kind": "repository",
      "paths": ["docs/help/**"],
      "release_policy": "required"
    },
    {
      "id": "commercial",
      "kind": "external",
      "release_policy": "review"
    }
  ],
  "evidence": {
    "github": "optional",
    "archivist": "auto"
  },
  "verification": {
    "require_impact_for_user_facing_changes": true,
    "block_on_required_surface": true
  }
}
```

### Surface fields

- `id`: unique stable lowercase-hyphen identifier.
- `kind`: `repository` or `external`.
- `release_policy`: `required`, `review`, or `optional`.
- `paths`: required non-empty list for repository surfaces; forbidden for external surfaces. Entries are normalized repository-relative path/glob patterns and may not be absolute or escape the project root.

### Evidence fields

Optional. When present:

- `github`: `optional`, `required`, or `disabled`.
- `archivist`: `auto`, `required`, or `disabled`.

These fields describe evidence availability policy, not product facts.

### Verification fields

Optional booleans:

- `require_impact_for_user_facing_changes`
- `block_on_required_surface`

Unknown semantic keys and enum values fail closed in schema v1.

## Repository paths

For a repository surface, validation expands the configured patterns relative to `--project-root`. A `required` repository surface must resolve to at least one existing path. Globs do not establish semantic coverage; they establish only that the configured destination exists.

## Fingerprint

The profile fingerprint is SHA-256 over canonical JSON (`sort_keys=true`, compact separators, UTF-8). Verification evidence is stale when its stored profile fingerprint differs from the current profile fingerprint.

## Compatibility

A repository without this profile does not acquire a persistent Product Knowledge release gate merely because the skill is installed. Explicit `impact`/`audit` analysis may still run with run-scoped surfaces; persistent release authority requires repository opt-in.
