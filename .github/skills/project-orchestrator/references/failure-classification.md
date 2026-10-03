# Failure classification

Classify provider failures as transient network/provider, rate/usage/quota, capacity,
timeout, process crash, command missing, authentication, unknown model, malformed or
missing output, or unknown. Classify controller failures as invalid configuration,
dirty base, base moved, worktree conflict, merge conflict, unsafe path, test failure,
no progress, cancellation, or specification blocker. Missing executable, invalid
configuration/credentials/model, unsafe paths, specification blockers, and conflicts are
not endlessly retried.
