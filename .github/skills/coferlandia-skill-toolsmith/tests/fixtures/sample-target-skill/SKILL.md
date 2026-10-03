---
name: sample-target-skill
description: >
  Fixture only. A tiny, deliberately scattered skill used by
  coferlandia-skill-toolsmith's tests so the full analyze -> classify -> design
  -> implement -> rewire -> validate procedure can be exercised without touching
  a real skill. Not a usable skill; do not invoke it.
license: Apache-2.0
compatibility: >
  Requires Python 3.11+ and bash. Fixture only.
metadata:
  author: community
  version: "0.1"
  category: ops
  status: draft
  tested: "not tested — fixture only"
---

## Context

Fixture for the toolsmith. It simulates a skill that owns two scattered scripts
and repeats a mechanical command in prose — exactly the kind of duplication the
toolsmith is meant to consolidate.

## Steps

1. To add a log entry, the agent writes a line by hand:

   ```bash
   echo "[$(date -u +%FT%TZ)] <message>" >> logs/run.log
   ```

   (This is repeated below in Gotchas, duplicating the format.)

2. To validate the config, run:

   ```bash
   python scripts/validate_config.py config.json
   ```

3. To rebuild the index, run:

   ```bash
   python scripts/rebuild_index.py --quiet
   ```

4. Decide, based on the project's current priorities, whether the rebuild should
   run now or be deferred. This is a judgment call — do not automate it.

## Gotchas

- The log line format is `[$(date -u +%FT%TZ)] <message>` (duplicated from Step 1
  on purpose, to give the toolsmith a duplication to find).
- Never write a log line outside `logs/run.log`.

## Expected Output

A validated config and a rebuilt index, plus one appended log line per action.
