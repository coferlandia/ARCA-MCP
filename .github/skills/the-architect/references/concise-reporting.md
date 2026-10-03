# Concise reporting

Canonical rule: **Store depth in linked entities. Show only material deltas in reports.**

Default limits:

| Output | Words |
|---|---:|
| Architect Addendum | 800 |
| Project Architecture Record | 1,000 |
| Assessment Brief | 1,500 |
| Release Architecture Delta | 700 |
| Component Application Result | 700 |
| Component Extraction Summary | 1,000 |

The CLI warns above limits; `report validate --strict` fails. Reports do not copy unchanged stack,
existing decisions, accepted stable risks, detailed application results, or implementation evidence
already owned by GitHub/Git.

Assessment briefs show at most 3 critical risks, 5 important items, 3 reuse/extraction opportunities,
3 required decisions, and 1 Maintenance Epic Candidate.
