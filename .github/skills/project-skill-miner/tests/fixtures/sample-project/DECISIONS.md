# Decisions

## 2026-06-14 - Prefer reconciliation script over manual spreadsheet matching

The project now uses `tools/reconcile_provider.py` as the source-of-truth workflow for
provider mismatch analysis. Older spreadsheet-only instructions are obsolete.

## 2026-05-03 - Deployments moved to the platform pipeline

The old Docker Compose production deploy is retired. Production deploys now run through
the platform pipeline maintained outside this sample fixture.
