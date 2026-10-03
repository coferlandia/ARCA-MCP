# Provider adapters

`CodexProvider` builds non-interactive `codex exec` arrays with exact working directory,
model, JSON events, final-message path, configured reasoning, and role-specific sandbox.
`OpenCodeProvider` builds `opencode run` arrays with exact directory, model, agent,
JSON format, prompt file, automatic approval for the isolated implementation worktree,
and resumable session. Both are probeable before use. Missing
or malformed protocol output is failure, never implicit completion. A future ZCode
adapter belongs behind the same interface only after an official non-interactive API.
