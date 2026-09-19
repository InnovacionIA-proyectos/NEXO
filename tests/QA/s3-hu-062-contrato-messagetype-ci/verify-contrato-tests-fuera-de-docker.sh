#!/usr/bin/env bash
# QA — S3-HU-06.2 (PR #128): reproduce el hallazgo de que ContratoMessageTypeTests
# pasa 5/5 en un checkout local completo, pero SIEMPRE falla dentro del arnes de CI
# (docker-compose.ci.yml) porque build/Dockerfile.ci solo copia src/, tests/ y docs/
# -nunca cubicador/-, y estos tests leen ese codigo fuente directo del disco.
#
# Uso: correr desde la raiz del repo, con `dotnet` y Docker disponibles.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

echo "=== 1) fuera de Docker (checkout local completo, cubicador/ presente) ==="
dotnet test tests/CabinaFletes.Api.Tests --filter "FullyQualifiedName~ContratoMessageTypeTests" -c Release

echo ""
echo "=== 2) dentro del arnes de CI (docker-compose.ci.yml, sin cubicador/ en la imagen) ==="
docker compose -f docker-compose.ci.yml build build pruebas
docker compose -f docker-compose.ci.yml run --rm pruebas 2>&1 | grep -A3 "ContratoMessageTypeTests" || true

echo ""
echo "Esperado: paso 1 en verde (5/5), paso 2 con las 5 fallando por"
echo "FileNotFoundException sobre cubicador/infrastructure/mensajeria/rabbitmq.py"
echo "-confirma que el problema es la imagen de Docker, no la logica del test."
