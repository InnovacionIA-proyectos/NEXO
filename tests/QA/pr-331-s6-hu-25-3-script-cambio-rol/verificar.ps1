# Verificación QA de la PR #331 (S6-HU-25.3: script de usuarios para el
# cambio de rol CoordinadorFletes -> AdminBiblioteca, ya corrido contra la
# BD real). Correr con .env.pruebas-reales.local cargado.

$conn = New-Object System.Data.SqlClient.SqlConnection($env:ConnectionStrings__Nexo)
$conn.Open()
$cmd = $conn.CreateCommand()

Write-Host "=== 1. tbl_usuarios: conteo por rol (debe ser 5 AdminBiblioteca, 0 CoordinadorFletes) ==="
$cmd.CommandText = "SELECT Rol, COUNT(*) AS n FROM db_NEXO.tbl_usuarios WHERE Rol IN ('CoordinadorFletes','AdminBiblioteca') GROUP BY Rol"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) { Write-Host "$($reader['Rol']): $($reader['n'])" }
$reader.Close()

Write-Host ""
Write-Host "=== 2. tbl_mapeo_rol_portal: fila IdRolPortal=49 (descripcion del portal NO debe cambiar) ==="
$cmd.CommandText = "SELECT IdRolPortal, DescripcionRolPortal, Rol FROM db_NEXO.tbl_mapeo_rol_portal WHERE IdRolPortal = 49"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) { Write-Host "IdRolPortal=$($reader['IdRolPortal']) Descripcion=$($reader['DescripcionRolPortal']) Rol=$($reader['Rol'])" }
$reader.Close()

Write-Host ""
Write-Host "=== 3. 0 filas con CoordinadorFletes remanentes en cualquiera de las dos tablas ==="
$cmd.CommandText = "SELECT COUNT(*) FROM db_NEXO.tbl_usuarios WHERE Rol = 'CoordinadorFletes'"
Write-Host "tbl_usuarios: $($cmd.ExecuteScalar())"
$cmd.CommandText = "SELECT COUNT(*) FROM db_NEXO.tbl_mapeo_rol_portal WHERE Rol = 'CoordinadorFletes'"
Write-Host "tbl_mapeo_rol_portal: $($cmd.ExecuteScalar())"

Write-Host ""
Write-Host "=== 4. Tablas de respaldo existen con las filas exactas que cambiaron ==="
$cmd.CommandText = "SELECT OBJECT_ID(N'[db_NEXO].[tbl_usuarios_bak_20261019]', N'U')"
$r1 = $cmd.ExecuteScalar()
if ($r1 -eq [DBNull]::Value) { Write-Host "tbl_usuarios_bak_20261019: NO EXISTE" } else { Write-Host "tbl_usuarios_bak_20261019: existe id=$r1" }
$cmd.CommandText = "SELECT OBJECT_ID(N'[db_NEXO].[tbl_mapeo_rol_portal_bak_20261019]', N'U')"
$r2 = $cmd.ExecuteScalar()
if ($r2 -eq [DBNull]::Value) { Write-Host "tbl_mapeo_rol_portal_bak_20261019: NO EXISTE" } else { Write-Host "tbl_mapeo_rol_portal_bak_20261019: existe id=$r2" }
$cmd.CommandText = "SELECT COUNT(*) FROM db_NEXO.tbl_usuarios_bak_20261019 WHERE Rol = 'CoordinadorFletes'"
Write-Host "Backup usuarios con Rol=CoordinadorFletes: $($cmd.ExecuteScalar())"

$conn.Close()
