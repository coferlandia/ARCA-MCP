# Profile Contract

Canonical path: `.coferlandia/ci/profile.json`; schema owner: `_protocol/delivery/ci-profile.schema.json`.

The profile describes static facts required by Qualification: repository identity, canonical docs, local qualification entrypoints/services/environment-variable names, GitHub submission/gates, merge-group authority, identity sensitivity and externally authorized exceptional lanes. It contains no current run/result state, environment values, or credentials.

The `fingerprint` is SHA-256 over canonical JSON excluding the fingerprint field itself. Any material profile change therefore creates a new qualification identity.
