# Repository CI Profile

Canonical target path: `.coferlandia/ci/profile.json`.

The profile points to repository-owned documentation, local qualification commands, required services/environment, GitHub gates and identity sensitivity. It is not runtime state and must not contain credentials or current test results.

Prefer one repository-owned canonical validation command when available instead of enumerating internal subcommands redundantly. Validate/fingerprint the profile with `coferlandia-ci-adapter`.
