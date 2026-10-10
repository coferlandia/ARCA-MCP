
#!/usr/bin/env bash
set -euo pipefail

SERVER="cadencia"
CONTAINER="dcarca-mcpserver"
REMOTE="/home/ubuntu/apps/dcarca-mcpserver-data"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

TIMESTAMP="$(date +%Y-%m-%d_%H-%M-%S)"
BACKUP_DIR="$ROOT/backups/$TIMESTAMP"
REMOTE_ARCHIVE="/tmp/dcarca-backup-$TIMESTAMP.tar.gz"

mkdir -p "$BACKUP_DIR"

WAS_RUNNING=false
STOPPED=false

cleanup() {
    if [ "$STOPPED" = true ] && [ "$WAS_RUNNING" = true ]; then
        echo "Iniciando ARCA-MCP..."
        ssh "$SERVER" "sudo docker start $CONTAINER"
    fi

    ssh "$SERVER" "sudo rm -f '$REMOTE_ARCHIVE'" || true
}

trap cleanup EXIT

# Comprobar si el contenedor estaba funcionando
STATUS=$(ssh "$SERVER" \
    "sudo docker inspect -f '{{.State.Running}}' $CONTAINER")

if [ "$STATUS" = "true" ]; then
    WAS_RUNNING=true

    echo "Deteniendo ARCA-MCP..."
    ssh "$SERVER" "sudo docker stop $CONTAINER"
    STOPPED=true
fi

echo "Creando backup comprimido en Cadencia..."

ssh "$SERVER" \
    "sudo tar -C '$REMOTE' -czf '$REMOTE_ARCHIVE' . && sudo chown ubuntu:ubuntu '$REMOTE_ARCHIVE' && sudo chmod 600 '$REMOTE_ARCHIVE'"

echo "Descargando backup..."
scp "$SERVER:$REMOTE_ARCHIVE" "$BACKUP_DIR/data.tar.gz"

echo "Extrayendo backup local..."
mkdir -p "$BACKUP_DIR/data"
tar -xzf "$BACKUP_DIR/data.tar.gz" -C "$BACKUP_DIR/data"

echo "Backup completado:"
echo "$BACKUP_DIR"
