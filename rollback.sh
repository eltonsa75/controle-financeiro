#!/bin/bash
set -euo pipefail

PROJECT="compras-inteligentes"
ENV_FILE="/opt/compras-inteligentes/.env"
BASE="/opt/compras-inteligentes-staging"

docker compose \
  -p "$PROJECT" \
  --env-file "$ENV_FILE" \
  -f "$BASE/docker-compose.prod.yml" \
  -f "$BASE/docker-compose.protection.yml" \
  -f "$BASE/docker-compose.rollback.yml" \
  -f "$BASE/docker-compose.resources.yml" \
  -f "$BASE/docker-compose.python.yml" \
  up -d --no-deps --no-build --pull never backend frontend
