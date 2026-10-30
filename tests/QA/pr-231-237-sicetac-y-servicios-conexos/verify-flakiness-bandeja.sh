#!/usr/bin/env bash
# Reproduce la intermitencia encontrada en clsSolicitudesTests (aislamiento por
# comercial): corre Api.Tests N veces seguidas contra la base real y cuenta cuántas
# corridas fallan. Uso: ./verify-flakiness-bandeja.sh [N=5]
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

N="${1:-5}"
fallos=0

docker compose -f docker-compose.ci.yml build

for i in $(seq 1 "$N"); do
  echo "=== Corrida $i/$N ==="
  if docker compose -f docker-compose.ci.yml --env-file .env.pruebas-reales.local \
      run --rm pruebas-reales; then
    echo "Corrida $i: OK"
  else
    echo "Corrida $i: FALLO"
    fallos=$((fallos + 1))
  fi
done

echo ""
echo "Resultado: $fallos fallo(s) de $N corridas."
