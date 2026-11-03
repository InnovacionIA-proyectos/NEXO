# Verificacion QA de la PR #382 (SICE-TAC oficial de Contenedor/Cupo: la ruta se compara por codigo DANE).
# Cierra el issue #378.
#
# La verificacion real es correr contra DWSice REAL (clsSicetacOficialPorDaneTests: Yumbo-Cali no debe elegir
# Yumbo-Calima, Cartagena-Cienaga no debe elegir Cienaga de Oro) y re-correr las areas relacionadas
# (tarifario, propuesta de tarifa, SICE-TAC) y los tests de cubicador que tocan el cliente de Nexo.
# .env.pruebas-reales.local debe existir en la raiz del repo y la VPN corporativa debe estar conectada.
#
# Correr desde la raiz del repo:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-382-sicetac-oficial-dane/verificar.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../../..")
Set-Location $repoRoot

$envFile = Join-Path $repoRoot ".env.pruebas-reales.local"
if (-not (Test-Path $envFile)) {
    Write-Error ".env.pruebas-reales.local no existe en la raiz del repo. No se puede verificar contra la BD real sin el."
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
Write-Host "=== clsSicetacOficialPorDaneTests contra DWSice real (esperado: 6/6) ==="
dotnet test tests/Nexo.Api.Tests -c Release --no-build --filter "FullyQualifiedName~clsSicetacOficialPorDaneTests"
if ($LASTEXITCODE -ne 0) { Write-Error "clsSicetacOficialPorDaneTests fallo"; exit 1 }

Write-Host ""
Write-Host "=== Regresion: tarifario / propuesta / SICE-TAC (Api.Tests) ==="
dotnet test tests/Nexo.Api.Tests -c Release --no-build --filter "FullyQualifiedName~Sicetac|FullyQualifiedName~Tarifario|FullyQualifiedName~Propuesta|FullyQualifiedName~clsLineasSolicitudCubicacionControllerTests"
if ($LASTEXITCODE -ne 0) { Write-Error "Regresion Api.Tests fallo"; exit 1 }

Write-Host ""
Write-Host "=== Nexo.Domain.Tests y Nexo.Application.Tests ==="
dotnet test tests/Nexo.Domain.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Domain.Tests fallo"; exit 1 }
dotnet test tests/Nexo.Application.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Application.Tests fallo"; exit 1 }

Write-Host ""
Write-Host "=== cubicador: pytest (nexo_client y plan_cargue) ==="
Push-Location (Join-Path $repoRoot "cubicador")
try {
    $py = if (Test-Path ".venv/Scripts/python.exe") { ".venv/Scripts/python.exe" } else { "python" }
    & $py -m pytest -q
    if ($LASTEXITCODE -ne 0) { Write-Error "pytest cubicador fallo"; exit 1 }
}
finally { Pop-Location }

Write-Host ""
Write-Host "=== Verificacion completa ==="
