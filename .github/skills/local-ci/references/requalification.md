# Requalification

Follow `_protocol/delivery/REQUALIFICATION.md`.

Local-specific invariant: every command result is valid only for the exact Candidate SHA and profile fingerprint recorded with it. A new SHA or material profile change requires a complete new Local CI run. Base drift follows the profile's identity rule and fails closed by default.
