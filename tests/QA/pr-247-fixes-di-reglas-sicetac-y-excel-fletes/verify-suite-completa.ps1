# Verificación PR #247: build + las 3 suites contra la base real de pruebas.
# Requiere VPN corporativa activa y .env.pruebas-reales.local en la raíz del repo
# (no versionado). Todo en un solo bloque de PowerShell (si se separa en dos
# consolas/scripts, las variables no se heredan y dotnet test cae al fallback
# de localhost).
$ErrorActionPreference = "Stop"
Set-Location (git rev-parse --show-toplevel)

Get-Content .env.pruebas-reales.local | Where-Object {
    $_ -match '^(ConnectionStrings__Nexo|Database__Esquema|ConnectionStrings__DwSice|ConnectionStrings__DbSolucionesTi|ConnectionStrings__DwCubos|ConnectionStrings__IdentidadPortal)='
} | ForEach-Object {
    $k, $v = $_ -split '=', 2
    Set-Item -Path "env:$k" -Value $v
}
$env:TEST_DB_CONNECTION = $env:ConnectionStrings__Nexo

dotnet test Nexo.Clean.sln -c Release -v minimal --logger "trx;LogFileName=results.trx"
