# Recovery

Persist each attempt and next retry time. Retry a transient provider per configuration,
then try fallbacks. If all are temporarily unavailable, transition to
`WAITING_FOR_PROVIDER`, persist, wait five minutes by default, probe, and resume the
same operation; `null` maximum cycles means continue until cancellation. Compare candidate
diff hashes, requirements, findings, test results, changed files, and commit hashes to
detect no progress; exhaust configured providers before `BLOCKED_BY_NO_PROGRESS`.

## Integration-check recovery

Queued, requested, waiting, pending, and in-progress integration checks are recoverable asynchronous states. Persist `WAITING_FOR_INTEGRATION_CHECKS` and retry by re-reading the current PR/candidate; never reuse an older green SHA. A terminal required-gate failure persists `INTEGRATION_CHECKS_FAILED`; fix or legitimately rerun the failing gate, then re-evaluate the newest authoritative observation.

Transient GitHub/API observation errors fail closed and never become GREEN. Permanent authentication/scope failures transition through the integration wait boundary into `BLOCKED_BY_AUTHENTICATION`. Base movement remains `BLOCKED_BY_BASE_MOVED` and requires the existing reconciliation/fresh-review path. Claims are not released from any of these states.
