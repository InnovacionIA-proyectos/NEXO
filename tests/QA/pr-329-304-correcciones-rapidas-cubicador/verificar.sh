#!/usr/bin/env bash
# Verificación QA de la PR #329 (lado NEXO de #304: validación de peso antes
# de encolar, tope de 10.000 unidades por línea, k_max 10->25 en Cubicador).
# Correr desde la raíz del repo, con la rama de la PR descargada localmente
# y mergeada con QA.
set -euo pipefail

echo "=== 1. Tabla nueva tbl_rechazos_cubicacion_linea ya existe en la BD real, con el esquema exacto ==="
set -a
while IFS='=' read -r key value; do
  [[ -z "$key" || "$key" == \#* ]] && continue
  export "$key=$value"
done < .env.pruebas-reales.local
set +a
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command '
$conn = New-Object System.Data.SqlClient.SqlConnection($env:ConnectionStrings__Nexo)
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT c.name AS Columna, t.name AS Tipo, c.max_length, c.is_nullable
FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID("db_NEXO.tbl_rechazos_cubicacion_linea")
ORDER BY c.column_id
"@
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
  Write-Host "$($reader["Columna"])  tipo=$($reader["Tipo"])  max_length=$($reader["max_length"])  nullable=$($reader["is_nullable"])"
}
$conn.Close()
'

echo
echo "=== 2. k_max vive consistente en los 4 sitios declarados (seed=25, respaldo=25) ==="
grep -n 'k_max' cubicador/domain/entidades/catalogo.py
grep -n 'k_max' cubicador/database/seeder/_generar_seeders.py
grep -n 'k_max' cubicador/database/seeder/parametrosMotor.csv
grep -n 'k_max' cubicador/scripts/preparar_catalogo_local.py

echo
echo "=== 3. Build en Release ==="
dotnet build Nexo.Clean.sln -c Release -v minimal

echo
echo "=== 4. Suite completa contra la base real de pruebas ==="
export TEST_DB_CONNECTION="$ConnectionStrings__Nexo"
dotnet test Nexo.Clean.sln -c Release -v minimal

echo
echo "=== 5. Boundary exacto del tope de 10.000 unidades (ya cubierto por clsCapturarSolicitudValidadorUnidadesTests) ==="
dotnet test tests/Nexo.Application.Tests --filter "FullyQualifiedName~clsCapturarSolicitudValidadorUnidadesTests" -c Release -v minimal
