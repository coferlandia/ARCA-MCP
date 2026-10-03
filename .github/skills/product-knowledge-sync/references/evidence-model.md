# Evidence model

Read this reference when choosing authoritative evidence or resolving conflicts between implementation and explanatory surfaces.

## Evidence precedence

Use evidence according to the fact being established, not a universal source ranking:

- current implementation and executable tests: actual implemented behavior;
- repository-owned runtime/configuration contracts: current declared machine contract;
- Issues/PRs/commits/releases: intent, change scope, review and historical delivery evidence;
- Archivist canonical files: durable project knowledge when present;
- current Help/support/developer/commercial surfaces: what audiences are currently told.

Documentation is evidence of what is documented, not automatic proof of product behavior. A closed Issue is evidence that work was tracked/closed, not proof that the current implementation or Help is correct.

## Conflict handling

When sources contradict:

1. identify the specific fact and competing sources;
2. prefer executable/current implementation for implemented behavior unless an authoritative contract proves otherwise;
3. classify contradictory explanatory content as drift;
4. preserve uncertainty when implementation itself is ambiguous;
5. emit an owner handoff if durable project knowledge must be reconciled.

Do not silently choose a convenient source to make verification pass.

## Agentic vs deterministic evidence

Agentic reports own semantic judgments and cite evidence. The CLI verifies only that declared evidence fields and configured surfaces obey the contract. It must not scrape filenames/labels to manufacture semantic conclusions.
