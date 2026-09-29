#!/usr/bin/env bash
# Verificación del desfase de esquema encontrado en la PR #231: confirma las
# columnas reales de tbl_servicios_conexos en la base real (para contrastar contra
# lo que el código de cada rama espera). Lectura pura, sin efectos secundarios.
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
  -Q "SELECT COUNT(*) AS TotalFilas FROM db_NEXO.tbl_servicios_conexos;
      SELECT name AS Columna FROM sys.columns
      WHERE object_id = OBJECT_ID('db_NEXO.tbl_servicios_conexos')
      ORDER BY column_id;"
