# Runbook

## Provider reconciliation

When provider rendition files do not match imported records:

1. Download the day's provider export into `incoming/provider/`.
2. Run `python tools/reconcile_provider.py --date YYYY-MM-DD`.
3. Review the generated mismatch report under `reports/provider/`.
4. If records must be corrected in production, stop and request explicit approval
   before applying any write-capable follow-up command.

Verification:
- The mismatch report is generated.
- No write-capable command is run without approval.

## Log triage

To inspect ingestion failures, compare the last 200 lines from the worker log with the
latest provider export checksum. The current docs do not agree on whether checksum
verification still uses `tools/checksum_report.py` or the admin UI.
