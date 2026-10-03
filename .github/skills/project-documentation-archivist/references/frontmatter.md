# Frontmatter

Use frontmatter only on Archivist-owned local source documents. GitHub entities and workflow-owned files are indexed externally and must not be modified for Archivist bookkeeping.

## Required local-source header

```yaml
---
catalog_status: processed
catalog_processed_at: YYYY-MM-DDTHH:mm:ssZ
catalog_processor: project-documentation-archivist
source_original_path: "<project-relative-path>"
source_archived_path: "<project-relative-archive-path-or-empty>"
source_sha256: "<sha256>"
git_commit_at_processing: "<commit-or-none>"
document_type: "<type>"
project_area: "<area>"
feeds:
  - README.md
references_detected:
  issues: []
  commits: []
  prs: []
  external_refs: []
catalog_notes:
  - "How the source was used."
---
```

`feeds` may contain only current durable Archivist outputs such as README, AGENTS, DECISIONS, RUNBOOK, SOURCE_INDEX, or PROCESSING_RUNS. Do not add TODO/HISTORY as feeds.

## Merge rules

Preserve unrelated existing keys and the original body. Replace only Archivist-owned keys. Normalize list fields and do not invent duplicate aliases.

## Remote GitHub sources

Do not add frontmatter. Track them in `SOURCE_INDEX.md` using repository, source type, number/SHA, URL when available, revision (`updatedAt` for Issues/PRs), processing timestamp, feeds, and notes.
