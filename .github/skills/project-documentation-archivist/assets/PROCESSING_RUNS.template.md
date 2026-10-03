# Processing Runs

## YYYY-MM-DD-HHMM-processing-run

Date: YYYY-MM-DDTHH:mm:ssZ
Mode: normal | migration | resolution
State: completed | completed-with-temporary-uncertainty | failed
Branch: none
Base commit: none
Sources processed:
- local/path-or-github-reference
GitHub mutations:
- none
Updated files:
- README.md
- AGENTS.md
- DECISIONS.md
- RUNBOOK.md
- .agent/catalog/SOURCE_INDEX.md
- .agent/catalog/PROCESSING_RUNS.md
Temporary uncertainties:
- none
Validations run:
- python scripts/validate_catalog.py --project-root .
Summary:
- Brief factual summary
Suggested commit:
- docs: update durable project knowledge
