
#!/usr/bin/env bash
set -e

SERVER="cadencia"
REMOTE="/home/ubuntu/apps/dcarca-mcpserver-config"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

mkdir -p "$ROOT/secrets"

# Descargar configuración de producción
scp "$SERVER:$REMOTE/appsettings.json" \
    "$ROOT/dcArca.McpServer/appsettings.prod.json"

# Descargar todo el contenido de la carpeta remota
scp -r "$SERVER:$REMOTE/." "$ROOT/secrets/"

# Evitar duplicar appsettings.json dentro de secrets
rm -f "$ROOT/secrets/appsettings.json"

echo "Configuración y secretos descargados desde Cadencia."
