# Verificacion QA de la PR #384 (puerto IAsistenteIaPort y adaptador clsAnthropicAdapter). Cierra el issue #350.
#
# La PR toca el composition root (clsDependencyInjection.cs) y agrega el paquete NuGet Anthropic, asi que ademas de los
# tests de IA se corre la suite completa de Api.Tests: si el registro del adaptador rompiera el arranque, caerian todos.
# Ningun test debe llamar a la API real de Anthropic: el adaptador se construye siempre con un HttpClient falso.
# Para comprobarlo, el script corre los tests de IA con ANTHROPIC_API_KEY vacia y deja constancia de ello.
#
# .env.pruebas-reales.local debe existir en la raiz del repo y la VPN corporativa debe estar conectada.
#
# Correr desde la raiz del repo:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-384-puerto-adaptador-anthropic/verificar.ps1

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
# Sin llave real en el entorno: nada de lo que se corre aqui puede autenticarse contra Anthropic.
foreach ($n in "ANTHROPIC_API_KEY", "Anthropic__ApiKey") { [System.Environment]::SetEnvironmentVariable($n, "") }
Write-Host "Database__Esquema = $env:Database__Esquema"
Write-Host "Anthropic__ApiKey y ANTHROPIC_API_KEY forzadas a vacio para esta corrida"

Write-Host ""
Write-Host "=== dotnet build Nexo.Clean.sln -c Release (restaura el paquete Anthropic) ==="
dotnet build Nexo.Clean.sln -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "Build fallo"; exit 1 }

Write-Host ""
Write-Host "=== Tests de IA (clsAnthropicAdapterTests, clsAsistenteIaConfiguracionTests) ==="
dotnet test tests/Nexo.Api.Tests -c Release --no-build --filter "FullyQualifiedName~Nexo.Api.Tests.IA"
if ($LASTEXITCODE -ne 0) { Write-Error "Tests de IA fallaron"; exit 1 }

Write-Host ""
Write-Host "=== Nexo.Domain.Tests y Nexo.Application.Tests ==="
dotnet test tests/Nexo.Domain.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Domain.Tests fallo"; exit 1 }
dotnet test tests/Nexo.Application.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Application.Tests fallo"; exit 1 }

Write-Host ""
Write-Host "=== Nexo.Api.Tests completo contra la BD real (regresion del arranque por el composition root) ==="
dotnet test tests/Nexo.Api.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Error "Api.Tests fallo"; exit 1 }

Write-Host ""
Write-Host "=== Verificacion completa ==="
