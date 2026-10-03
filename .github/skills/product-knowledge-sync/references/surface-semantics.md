# Surface semantics

Read this reference when deciding whether a product-knowledge surface is semantically complete.

## Presence is not completeness

A feature name, menu label, release-note bullet, or catalog entry proves only presence. Evaluate the information a real audience needs for the behavior.

Relevant dimensions vary by feature, but commonly include:

- purpose and user-visible outcome;
- setup/configuration;
- normal usage and workflow;
- roles/permissions;
- restrictions and preconditions;
- interactions with adjacent capabilities;
- failure/error behavior;
- troubleshooting/recovery;
- material timezone/location/data semantics;
- version, migration, deprecation, or compatibility notes.

Do not force irrelevant dimensions. `not-applicable` is valid when reasoned.

## Surface independence

One surface cannot stand in for another merely because content overlaps. Release notes summarize change; they do not automatically replace Help. Developer docs do not automatically replace operator guidance. Commercial material may intentionally be higher level but still must be reviewed against claims and restrictions when configured.

## External surfaces

External/manual surfaces stay in the matrix even when the agent lacks write access. Use `manual-review-required` and a handoff rather than dropping the surface.

## Audit classifications

- `covered`: relevant semantic dimensions are current and consistent.
- `partial`: some relevant dimensions are missing.
- `missing`: the behavior is not meaningfully represented.
- `stale`: content reflects older behavior.
- `contradictory`: content conflicts with authoritative behavior/evidence.
- `not-applicable`: surface intentionally does not apply, with reason.
- `manual-review-required`: semantic review cannot be completed automatically/in current authority.
