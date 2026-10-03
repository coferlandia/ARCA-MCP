# Static configuration contract v1

## Serialization

The default file is `.coferlandia/config-toolsmith/contract.yaml`. Version 1 uses a
JSON-compatible YAML subset so the deterministic CLI can parse it with the Python standard library.
A full YAML parser may be used by a project adapter, but generated behavior must remain equivalent.

## Minimum shape

```json
{
  "schema_version": 1,
  "application": {
    "name": "sample-app",
    "command": "sample",
    "native_authority": {
      "kind": "python-module",
      "reference": "sample.settings"
    }
  },
  "modules": [
    {
      "name": "notifications",
      "description": "Reminder behavior",
      "fields": [
        {
          "key": "notifications.reminder_lead_minutes",
          "description": "Minutes before the appointment",
          "type": "integer",
          "writable": true,
          "secret": false,
          "setup_level": "minimal",
          "binding": {
            "adapter": "dotenv",
            "path": ".env",
            "native_key": "REMINDER_LEAD_MINUTES"
          },
          "validation": {"minimum": 5, "maximum": 10080},
          "effects": {"restart_components": ["backend"]},
          "user_intents": ["change reminder timing"],
          "examples": [{"request": "Send reminders two hours before", "value": 120}]
        }
      ]
    }
  ]
}
```

## Required field metadata

Every managed field declares:

- unique canonical `key`;
- human and agent-oriented `description`;
- type: `string`, `integer`, `number`, `boolean`, `enum`, `array`, or `object`;
- `writable` and `secret` booleans;
- native `binding` with adapter and authority-specific coordinates;
- validation and safe examples where relevant;
- operational effects, including restart/migration/activation when applicable;
- user intents or an explicit reason why intent mapping does not apply.

## Supported standard adapters

The generated Python facade directly supports:

- `env` (read-only process environment);
- `dotenv` (native `.env` file);
- `json` (native JSON file plus dotted JSON path).

`python-api`, `dotnet-options`, `command`, `database`, `remote`, `toml`, and project-specific
adapters require native integration or a declared custom adapter. They remain valid contract
bindings but generation must report whether the selected platform pack implements them.

## Forbidden state

The validator rejects state-bearing keys at any depth, including current/effective/last-seen values,
per-environment value maps, snapshots, and secret values. Defaults may be documented only as
native-authority references; a copied default cannot become operational authority.

## Change-plan integrity

`prepare-change` emits a contract fingerprint, live-state fingerprint, and deterministic plan hash.
`apply-plan` requires the independently returned hash through `--expect-hash`, rejects modified or
stale plans, revalidates every field/value against the current contract, and rolls standard native
artifacts back when a write or post-write verification fails. Contract-native paths must remain
inside the target repository root.
