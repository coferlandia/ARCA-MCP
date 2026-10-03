# Provider authentication

The orchestrator never stores or prints provider credentials. Codex uses its
native `CODEX_HOME/auth.json`; keep `CODEX_HOME` outside the repository when
possible.

On the server, run:

```bash
export CODEX_HOME="$HOME/.codex-orchestrator"
mkdir -p "$CODEX_HOME"
codex login --device-auth
codex login status
```

Authorize the displayed device flow from an operator browser. The orchestrator
inherits `CODEX_HOME` when it launches `codex exec`. If an operational policy
requires a repository-local compatibility path, `.project-orchestrator/auth.json`
is ignored, but it must never be committed or included in a run artifact.

OpenCode credentials remain provider-native/environment-managed. OpenCode 1.18.1
lists the fallback as `opencode/big-pickle`; the provider prefix is required by
the `--model` option.
