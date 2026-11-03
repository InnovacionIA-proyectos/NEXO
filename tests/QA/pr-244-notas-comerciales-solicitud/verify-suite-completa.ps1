# Verificación PR #244: build + las 3 suites contra la base real de pruebas.
# Usa la Opción 1 (PowerShell nativo, sin Docker) en vez del script bash habitual
# (docker-compose.ci.yml / pruebas-reales-completas) porque esta PR toca un flujo
# que abre/costea una solicitud con ruta real (Medellín->Bogotá), y ese caso falla
# en Docker por la limitación de TLS con DWSice documentada en
# docs/Testing/PRUEBAS-LOCALES.md ("Limitación conocida — DWSice desde Docker").
# Requiere VPN corporativa activa y .env.pruebas-reales.local en la raíz del repo
# (no versionado).
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
