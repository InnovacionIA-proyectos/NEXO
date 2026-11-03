# Verificación QA de la PR #333 (S6-HU-25/28/24.1/29, #307-#313: rol
# AdminBiblioteca, Cubicador para AdminTarifas, campos de la línea, ajustes
# del API). Correr con .env.pruebas-reales.local cargado.

$conn = New-Object System.Data.SqlClient.SqlConnection($env:ConnectionStrings__Nexo)
$conn.Open()
$cmd = $conn.CreateCommand()

Write-Host "=== 1. El estado de la BD ya coincide con lo que produciria el script 307 (no hace falta correrlo) ==="
$cmd.CommandText = @"
SELECT 'usuarios con el rol viejo'  AS verificacion, COUNT(*) AS filas FROM [db_NEXO].[tbl_usuarios]         WHERE [Rol] = N'CoordinadorFletes'
UNION ALL
SELECT 'mapeos con el rol viejo',                    COUNT(*)          FROM [db_NEXO].[tbl_mapeo_rol_portal] WHERE [Rol] = N'CoordinadorFletes'
UNION ALL
SELECT 'usuarios AdminBiblioteca',                   COUNT(*)          FROM [db_NEXO].[tbl_usuarios]         WHERE [Rol] = N'AdminBiblioteca'
UNION ALL
SELECT 'mapeos AdminBiblioteca',                     COUNT(*)          FROM [db_NEXO].[tbl_mapeo_rol_portal] WHERE [Rol] = N'AdminBiblioteca'
"@
$reader = $cmd.ExecuteReader()
while ($reader.Read()) { Write-Host "$($reader['verificacion']): $($reader['filas'])" }
$reader.Close()
$conn.Close()

Write-Host ""
Write-Host "=== 2. Diagnostico del fallo de clsHistoricoSapPaginacionTests: tabla real vacia, no bug de codigo ==="
$connSap = New-Object System.Data.SqlClient.SqlConnection($env:ConnectionStrings__DbSolucionesTi)
$connSap.Open()
$cmdSap = $connSap.CreateCommand()
$cmdSap.CommandText = "SELECT DB_NAME()"
Write-Host "Base conectada: $($cmdSap.ExecuteScalar())"
$cmdSap.CommandText = "SELECT OBJECT_ID(N'dbo.tblSAPTarifasLog', N'U')"
Write-Host "tblSAPTarifasLog object_id (confirma que existe): $($cmdSap.ExecuteScalar())"
$cmdSap.CommandText = "SELECT COUNT(*) FROM dbo.tblSAPTarifasLog"
Write-Host "tblSAPTarifasLog filas: $($cmdSap.ExecuteScalar())"
$cmdSap.CommandText = "SELECT COUNT(*) FROM dbo.tblSAPTarifas"
Write-Host "tblSAPTarifas (vigentes, tabla hermana) filas: $($cmdSap.ExecuteScalar())"
$connSap.Close()
