#!/usr/bin/env bash
# Verificación PR #226: build + las 3 suites contra la base real de pruebas.
# Requiere .env.pruebas-reales.local en la raíz del repo (no versionado) con
# ConnectionStrings__Nexo y Database__Esquema apuntando a dbInformesAutomatizados_Pruebas.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

docker compose -f docker-compose.ci.yml build
docker compose -f docker-compose.ci.yml --env-file .env.pruebas-reales.local \
  run --rm pruebas-reales-completas
