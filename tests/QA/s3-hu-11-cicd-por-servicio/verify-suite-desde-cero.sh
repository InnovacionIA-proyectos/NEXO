#!/usr/bin/env bash
# QA — S3-HU-11 (PR #147): reproduce el hallazgo sobre docker-compose.ci.yml.
#
# ApiFixture.cs y OutboxConsistenciaTests.cs (S3-HU-11.1, issue #134) ya NO migran
# la base por defecto -correcto para la base corporativa real, que no debe
# recibirse un EnsureDeleted/Migrate accidental- pero docker-compose.ci.yml (el
# arnes LOCAL, con un SQL Server efimero que nace vacio en cada corrida) nunca se
# actualizo para pasarle TEST_DB_MIGRATE_ON_STARTUP=true. Sin ese flag, el
# esquema nunca se crea contra la base efimera.
#
# Uso: correr desde la raiz del repo, con Docker disponible.
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

echo "=== 1) SIN el flag, desde una base realmente vacia (el estado actual del PR) ==="
docker compose -f docker-compose.ci.yml down -v || true
docker compose -f docker-compose.ci.yml build build pruebas
docker compose -f docker-compose.ci.yml run --rm pruebas 2>&1 | grep -E "Passed!|Failed!"

echo ""
echo "=== 2) CON el flag (el arreglo sugerido), desde una base realmente vacia ==="
docker compose -f docker-compose.ci.yml down -v
docker compose -f docker-compose.ci.yml run --rm -e TEST_DB_MIGRATE_ON_STARTUP=true pruebas 2>&1 | grep -E "Passed!|Failed!"

echo ""
echo "Esperado:"
echo "  1) Api.Tests: 69 de 162 fallan (SqlException, la base 'CabinaFletes_Test'"
echo "     nunca llega a crearse)."
echo "  2) Api.Tests: baja a 1 sola falla -condicion de carrera real entre"
echo "     ApiFixture y OutboxConsistenciaTests migrando la misma base nueva al"
echo "     mismo tiempo ('Database CabinaFletes_Test already exists')- no las 69."

docker compose -f docker-compose.ci.yml down -v
