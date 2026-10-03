# Component extraction

Extraction requires explicit current authority.

## Source characterization

Lock repository/ref and identify behavior, consumers, tests, configuration, dependencies,
operational assumptions, secrets/data, provenance, ownership and license.

## Extraction design

Define boundary, preserved behavior, project-specific exclusions, public contract, extension points,
configuration/dependency policy, compatibility, characterization/component tests, example consumer
and artifact kind.

## Boundary

The Architect produces the contract. A coding agent implements it with independent review. The
source project is not automatically refactored to consume it; adoption is separate.
