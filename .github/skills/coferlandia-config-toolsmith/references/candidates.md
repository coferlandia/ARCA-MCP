# Candidate omission and review

## Candidate record

A candidate must contain:

```text
id | status | proposed field/module/type | evidence | source fingerprint |
unresolved questions | omitted reason | impact | criticality | proposed mapping
```

The implementation log receives a matching warning with the candidate id, decision, impact, and
next action. Omission leaves the existing configuration path untouched.

## Statuses

```text
pending -> approved -> implemented
pending -> rejected
pending -> deferred
pending -> intentionally_unmanaged
any reviewable state -> stale when evidence changes
```

## Approval

Approval is explicit. It must resolve all material metadata and provide the expected source
fingerprint. The deterministic CLI then:

1. verifies the candidate is not stale;
2. validates the proposed field;
3. inserts it into the static contract;
4. appends a decision record and leaves the candidate `approved` while generation is pending;
5. regenerates the facade and documentation from the updated contract;
6. validates the generated surface and marks the candidate `implemented` only after success.

Approval does not migrate or copy the current native value.

## Completion coverage

Report counts for every disposition and critical candidate. A run may complete with pending
candidates, but must report `completed_with_review_required` when material omissions remain.
`UNCLASSIFIED` greater than zero is always a process failure.
