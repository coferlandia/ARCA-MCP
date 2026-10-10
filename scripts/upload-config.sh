
#!/usr/bin/env bash
set -e

SERVER="cadencia"
REMOTE="/home/ubuntu/apps/dcarca-mcpserver-config"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

# Subir configuración de producción
scp "$ROOT/dcArca.McpServer/appsettings.prod.json" \
    "$SERVER:$REMOTE/appsettings.json"

# Subir todo el contenido de secrets
scp -r "$ROOT/secrets/." "$SERVER:$REMOTE/"

echo "Configuración y secretos subidos a Cadencia."
