# Failure and Correction

Classify a local qualification failure before editing code: product/regression, environment/service dependency, flaky/non-deterministic test, or stale candidate/profile/base.

For product/regression failures, use the active repository development workflow and smallest in-scope fix. Material fixes require fresh review/development evidence and a new READY_FOR_CI handoff before qualification restarts.

Environment/provider failures do not authorize switching to GitHub-native CI. Report the blocked local strategy so the controlling authority can explicitly choose the next action.
