#!/usr/bin/env bash
# Verificación PR #228/#229 (S4-HU-09.1): confirma que tbl_municipios sigue existiendo
# de verdad en la base real, con las 3.804 filas esperadas, y muestra una muestra.
# Lectura pura, sin efectos secundarios.
#
# Variables de entorno requeridas (nunca hardcodear credenciales en este archivo):
#   NEXO_DB_HOST, NEXO_DB_USER, NEXO_DB_PASSWORD, NEXO_DB_NAME
set -euo pipefail

: "${NEXO_DB_HOST:?Falta NEXO_DB_HOST (ej. 10.10.40.85,1433)}"
: "${NEXO_DB_USER:?Falta NEXO_DB_USER}"
: "${NEXO_DB_PASSWORD:?Falta NEXO_DB_PASSWORD}"
: "${NEXO_DB_NAME:?Falta NEXO_DB_NAME (ej. dbInformesAutomatizados_Pruebas)}"

export MSYS_NO_PATHCONV=1
docker run --rm --entrypoint /opt/mssql-tools18/bin/sqlcmd mcr.microsoft.com/mssql/server:2022-latest \
  -C -S "$NEXO_DB_HOST" -U "$NEXO_DB_USER" -P "$NEXO_DB_PASSWORD" -d "$NEXO_DB_NAME" \
  -Q "SELECT COUNT(*) AS Filas FROM db_NEXO.tbl_municipios;
      SELECT TOP 5 CodigoDane, Municipio, Departamento, Pais, ZonaTarificacion
      FROM db_NEXO.tbl_municipios ORDER BY Id;"
