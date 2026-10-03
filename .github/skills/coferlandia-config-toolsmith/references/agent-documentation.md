# Agent documentation contract

Generate these documents from the same static contract:

```text
docs/configuration/CONFIG-AGENT-HANDBOOK.md
docs/configuration/CONFIG-CLI-REFERENCE.md
docs/configuration/CONFIG-FIELD-REFERENCE.md
docs/configuration/CONFIG-INTENT-CATALOG.md
docs/configuration/CONFIG-RECIPES.md
docs/configuration/CONFIG-SAFETY.md
docs/configuration/CONFIG-TROUBLESHOOTING.md
```

## Operability requirement

An agent with no prior repository knowledge must be able to:

1. map a natural-language outcome to modules, fields, recipes, or candidates;
2. inspect live effective state;
3. construct the smallest valid change set;
4. preview effects and warnings;
5. apply through the standardized facade;
6. validate the result;
7. report activation/rollback and unresolved facts.

The handbook is the exhaustive fallback. It must state that search is non-authoritative and require
complete review before reporting a change unsupported.

## Field documentation

Every managed field documents purpose, user-visible effect, canonical key, native binding, type,
constraints, units, examples, native resolution authority, approved write target, secret policy,
dependencies/conflicts, operational effects, and exact inspect/preview/apply/validate/rollback forms.

Do not embed current production/test/development values.

## Example validity

Generated examples must parse against the generated CLI. Secrets use stdin. Mutating examples use
plan/dry-run and confirmation. Recipes identify preconditions, effects, stop conditions, and rollback.
