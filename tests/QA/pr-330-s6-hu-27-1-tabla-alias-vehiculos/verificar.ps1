# Verificación QA de la PR #330 (S6-HU-27.1: tabla de alias de vehículos y
# su carga). Correr con .env.pruebas-reales.local cargado.

$conn = New-Object System.Data.SqlClient.SqlConnection($env:ConnectionStrings__Nexo)
$conn.Open()
$cmd = $conn.CreateCommand()

Write-Host "=== 1. tbl_alias_elementos: conteos reales (deben ser 71 = 48 Vehiculo + 23 Contenedor) ==="
$cmd.CommandText = "SELECT TipoElemento, COUNT(*) AS n FROM db_NEXO.tbl_alias_elementos GROUP BY TipoElemento"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) { Write-Host "$($reader['TipoElemento']): $($reader['n'])" }
$reader.Close()
$cmd.CommandText = "SELECT COUNT(*) FROM db_NEXO.tbl_alias_elementos"
Write-Host "Total: $($cmd.ExecuteScalar())"

Write-Host ""
Write-Host "=== 2. Sin alias normalizados duplicados ==="
$cmd.CommandText = "SELECT COUNT(*) FROM (SELECT AliasNormalizado FROM db_NEXO.tbl_alias_elementos GROUP BY AliasNormalizado HAVING COUNT(*) > 1) t"
Write-Host "Duplicados: $($cmd.ExecuteScalar())"

Write-Host ""
Write-Host "=== 3. Codigos de Vehiculo usados existen en el catalogo real (TipoVehiculo) ==="
$cmd.CommandText = "SELECT DISTINCT TipoVehiculo FROM db_NEXO.tbl_configuraciones_vehiculo"
$reader = $cmd.ExecuteReader()
$oficiales = @()
while ($reader.Read()) { $oficiales += $reader['TipoVehiculo'] }
$reader.Close()
$cmd.CommandText = "SELECT DISTINCT Codigo FROM db_NEXO.tbl_alias_elementos WHERE TipoElemento = 'Vehiculo'"
$reader = $cmd.ExecuteReader()
$codigos = @()
while ($reader.Read()) { $codigos += $reader['Codigo'] }
$reader.Close()
foreach ($c in $codigos) {
  if ($oficiales -contains $c) { Write-Host "OK    $c existe en TipoVehiculo" }
  else { Write-Host "FALTA $c NO esta en TipoVehiculo" }
}
$conn.Close()

Write-Host ""
Write-Host "=== 4. Codigos de Contenedor usados existen en tblSAPClaseContenedor (DESCRIPCION) ==="
$connSap = New-Object System.Data.SqlClient.SqlConnection($env:ConnectionStrings__DbSolucionesTi)
$connSap.Open()
$cmdSap = $connSap.CreateCommand()
$cmdSap.CommandText = "SELECT DISTINCT DESCRIPCION FROM dbo.tblSAPClaseContenedor WHERE DESCRIPCION IN ('CNT20.ST','CNT40.HC')"
$reader = $cmdSap.ExecuteReader()
while ($reader.Read()) { Write-Host "Existe: $($reader['DESCRIPCION'])" }
$connSap.Close()
