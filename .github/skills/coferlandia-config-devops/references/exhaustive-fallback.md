# Exhaustive documentation fallback

Deterministic search is non-authoritative. It may rank fields, modules, intents, recipes, and effects,
but cannot exclude documentation from semantic consideration.

Before reporting unavailable, unsupported, or impossible:

1. read `CONFIG-AGENT-HANDBOOK.md` completely;
2. inspect all relevant module and field descriptions;
3. inspect pending/stale candidates;
4. inspect intentionally unmanaged and read-only entries;
5. inspect unsupported adapters and documented custom procedures;
6. check whether multiple fields/recipes compose the requested outcome;
7. distinguish `not found by search` from `not managed`, `pending`, `read-only`, `unsupported`, and
   truly `not configurable`.

A search miss must never automatically produce a negative user answer.
