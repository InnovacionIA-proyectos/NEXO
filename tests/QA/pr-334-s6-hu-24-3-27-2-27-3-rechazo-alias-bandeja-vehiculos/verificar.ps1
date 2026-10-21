# Verificación QA de la PR #334 (S6-HU-24.3/27.2/27.3, #320-#322: rechazo de
# línea, diccionario de alias, bandeja de vehículos). Correr con
# .env.pruebas-reales.local cargado.

$conn = New-Object System.Data.SqlClient.SqlConnection($env:ConnectionStrings__Nexo)
$conn.Open()
$cmd = $conn.CreateCommand()

Write-Host "=== 1. Columna Descartada ya existe en tbl_configuraciones_vehiculo (prerrequisito critico: sin ella el costeo falla con 500) ==="
$cmd.CommandText = @"
SELECT c.name AS Columna, t.name AS Tipo, c.is_nullable
FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('db_NEXO.tbl_configuraciones_vehiculo') AND c.name = 'Descartada'
"@
$reader = $cmd.ExecuteReader()
$found = $false
while ($reader.Read()) { $found = $true; Write-Host "$($reader['Columna'])  tipo=$($reader['Tipo'])  nullable=$($reader['is_nullable'])" }
if (-not $found) { Write-Host "FALTA la columna Descartada" }
$reader.Close()

Write-Host ""
Write-Host "=== 2. tbl_alias_elementos (de la PR #330) sigue intacta: 71 filas ==="
$cmd.CommandText = "SELECT COUNT(*) FROM db_NEXO.tbl_alias_elementos"
Write-Host "Total: $($cmd.ExecuteScalar())"

$conn.Close()
