# Testing

Before accepting a profile:

1. `profile validate` passes.
2. fingerprint is deterministic across JSON key order.
3. `profile render --dry-run` writes nothing.
4. render writes only `.coferlandia/ci/profile.json` beneath target root.
5. `profile check` detects fingerprint drift.
6. local commands and GitHub gates correspond to current repository evidence.
7. no secret-bearing keys/values or current CI result snapshots are stored.
8. Chat CI and Local CI can both consume the same profile without project-specific controller edits.
