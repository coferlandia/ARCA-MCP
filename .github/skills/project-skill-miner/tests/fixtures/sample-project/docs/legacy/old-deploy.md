# Old Docker Compose Deploy

Legacy note from 2025.

1. Copy `docker-compose.prod.yml` to the server.
2. Run `docker compose -f docker-compose.prod.yml up -d`.
3. If the service fails, restart the container manually.

This flow predates the platform deployment pipeline.
