# Verificación QA de la PR #328 (S6-HU-24.2: columnas del rechazo en la línea
# de solicitud). Correr con las variables de .env.pruebas-reales.local
# cargadas (ConnectionStrings__Nexo apuntando a la base real de pruebas).

$conn = New-Object System.Data.SqlClient.SqlConnection($env:ConnectionStrings__Nexo)
$conn.Open()

Write-Host "=== 1. Las 3 columnas nuevas existen con el tipo/nulabilidad declarados ==="
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT c.name AS Columna, t.name AS Tipo, c.max_length, c.is_nullable
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('db_NEXO.tbl_lineas_solicitud')
  AND c.name IN ('MotivoRechazo','RechazadaPor','RechazadaAt')
ORDER BY c.name
"@
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
  Write-Host "$($reader['Columna'])  tipo=$($reader['Tipo'])  max_length=$($reader['max_length'])  nullable=$($reader['is_nullable'])"
}
$reader.Close()

Write-Host ""
Write-Host "=== 2. EstadoLinea sin CHECK constraint que bloquee 'Rechazada' ==="
$cmd.CommandText = @"
SELECT c.name AS Columna, t.name AS Tipo, c.max_length, c.is_nullable
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('db_NEXO.tbl_lineas_solicitud')
  AND c.name = 'EstadoLinea'
"@
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
  Write-Host "$($reader['Columna'])  tipo=$($reader['Tipo'])  max_length=$($reader['max_length'])  nullable=$($reader['is_nullable'])"
}
$reader.Close()

$cmd.CommandText = @"
SELECT cc.name AS ConstraintName, cc.definition
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('db_NEXO.tbl_lineas_solicitud')
"@
$reader = $cmd.ExecuteReader()
$found = $false
while ($reader.Read()) {
  $found = $true
  Write-Host "CHECK: $($reader['ConstraintName']) -> $($reader['definition'])"
}
if (-not $found) { Write-Host "Sin CHECK constraints en la tabla (correcto)." }
$reader.Close()

$conn.Close()
