# Verificación QA de la PR #343 (Estimación del levante: la ruta se compara
# por código DANE). Cierra el issue #339.
#
# El bug era que GET /api/captura-solicitudes/aprovisionamiento comparaba la
# ruta por texto y no encontraba "BOGOTA, D.C." (SICE-TAC escribe
# "BOGOTA D. C."). El fix resuelve ambas ciudades a su codigo DANE y compara
# por DANE, con fallback a texto si alguna no resuelve a un unico municipio.
#
# La verificacion real es correr Nexo.Api.Tests contra la BD y el DWSice
# reales: el test nuevo ya compara el VALOR EXACTO del piso entre las 3
# formas de escribir "Bogota".
#
# Correr desde la raiz del repo:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-343-aprovisionamiento-levante-dane/verificar.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../../..")
Set-Location $repoRoot

$envFile = Join-Path $repoRoot ".env.pruebas-reales.local"
if (-not (Test-Path $envFile)) {
    Write-Error ".env.pruebas-reales.local no existe en la raiz del repo. No se puede verificar contra la BD real sin él."
    exit 1
}

Write-Host "=== Cargando variables de entorno de .env.pruebas-reales.local ==="
Get-Content $envFile | Where-Object { $_ -match '^[A-Za-z_]+__?[A-Za-z]*=' } | ForEach-Object {
    $idx = $_.IndexOf('=')
    $name = $_.Substring(0, $idx)
    $value = $_.Substring($idx + 1)
    [System.Environment]::SetEnvironmentVariable($name, $value)
    if ($name -eq "ConnectionStrings__Nexo") {
        [System.Environment]::SetEnvironmentVariable("TEST_DB_CONNECTION", $value)
    }
}
Write-Host "Database__Esquema = $env:Database__Esquema"

Write-Host ""
Write-Host "=== dotnet build Nexo.Clean.sln -c Release ==="
dotnet build Nexo.Clean.sln -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "Build fallo"; exit 1 }

Write-Host ""
Write-Host "=== Test puntual: comparacion de valor exacto entre variantes de 'Bogota' ==="
dotnet test tests/Nexo.Api.Tests -c Release --no-build --filter "FullyQualifiedName~clsSicetacPeriodoYTramosVaciosTests"
if ($LASTEXITCODE -ne 0) { Write-Error "clsSicetacPeriodoYTramosVaciosTests fallo"; exit 1 }

Write-Host ""
Write-Host "=== Suite completa (esperado: 738/738, 10 omitidos esperados) ==="
dotnet test tests/Nexo.Domain.Tests -c Release --no-build
dotnet test tests/Nexo.Application.Tests -c Release --no-build
dotnet test tests/Nexo.Api.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Suite fallo"; exit 1 }

Write-Host ""
Write-Host "=== Verificación completa ==="
