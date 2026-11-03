# Verificacion QA de la PR #385 (Anthropic: workspace configurable, guia de despliegue, Sonnet 5.5 por defecto). Refs #351.
#
# La PR NO toca el composition root: cambia el adaptador y las opciones de IA, las plantillas de despliegue y agrega una
# prueba que escanea los archivos versionados en busca de llaves. Por eso la regresion se acota al espacio de nombres
# Nexo.Api.Tests.IA (incluye clsSecretosEnElRepositorioTests) mas Domain y Application.
# Ninguna prueba debe llamar a la API real: el script fuerza vacias ANTHROPIC_API_KEY y Anthropic__ApiKey.
#
# .env.pruebas-reales.local debe existir en la raiz del repo y la VPN corporativa debe estar conectada.
#
# Correr desde la raiz del repo:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-385-workspace-despliegue-sonnet55/verificar.ps1

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
foreach ($n in "ANTHROPIC_API_KEY", "Anthropic__ApiKey") { [System.Environment]::SetEnvironmentVariable($n, "") }
Write-Host "Anthropic__ApiKey y ANTHROPIC_API_KEY forzadas a vacio para esta corrida"

Write-Host ""
Write-Host "=== dotnet build Nexo.Clean.sln -c Release ==="
dotnet build Nexo.Clean.sln -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "Build fallo"; exit 1 }

Write-Host ""
Write-Host "=== Tests de IA (adaptador, configuracion y escaneo de secretos en archivos versionados) ==="
dotnet test tests/Nexo.Api.Tests -c Release --no-build --filter "FullyQualifiedName~Nexo.Api.Tests.IA"
if ($LASTEXITCODE -ne 0) { Write-Error "Tests de IA fallaron"; exit 1 }

Write-Host ""
Write-Host "=== Nexo.Domain.Tests y Nexo.Application.Tests ==="
dotnet test tests/Nexo.Domain.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Domain.Tests fallo"; exit 1 }
dotnet test tests/Nexo.Application.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Application.Tests fallo"; exit 1 }

Write-Host ""
Write-Host "=== Verificacion completa ==="
