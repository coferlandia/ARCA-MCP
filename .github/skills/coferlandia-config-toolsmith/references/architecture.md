# Architecture and authority

## Core boundary

The target application keeps its existing runtime configuration architecture. Config Toolsmith adds
an operational facade, not another configuration layer.

```text
application runtime -> native loader/API -> native stores/providers
operator/agent      -> generated CLI     -> same native loader/API or approved stores
```

The application must never import, parse, or depend on `.coferlandia/config-toolsmith/contract.yaml`.
The generated CLI may read the contract to discover approved operations, but the contract cannot
provide runtime values or defaults.

## Contract ownership

Contract schema v1 is owned by `coferlandia-config-toolsmith`. It describes:

- application identity and native authority;
- modules and canonical interface keys;
- native bindings and approved write methods;
- types, validation, dependencies, and conflicts;
- secret policy and operational effects;
- setup levels, user intents, examples, and recipes;
- generated documentation and facade capabilities.

It cannot describe an environment instance. Reject state-bearing keys such as `current_value`,
`effective_value`, `last_seen_value`, `production_value`, `environment_values`, `secret_value`, or
configuration snapshots.

## Generated versus handwritten

Generated files must carry a generator marker and be reproducible from the approved contract.
Project-specific adapters that require semantic design may be handwritten, but their public
capability and native authority must be declared in the contract and tested against the common
facade contract.

## Plans and audit

A prepared change plan is a transient operational artifact. It may contain expected non-secret
values and a native-state fingerprint, but it never becomes a source of configuration. Before
applying, the facade must re-read native state and reject stale plans.

Audit records describe operations and outcomes. They do not participate in resolution and never
contain secret values.

## Cross-skill boundary

- Config Toolsmith builds and maintains the facade and contract.
- Config DevOps consumes them to execute or guide configuration operations.
- Config DevOps cannot add fields, change bindings, or implement adapters.
- A missing/ambiguous capability returns to Config Toolsmith through explicit candidate review.
