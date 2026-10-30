# QA — PR #424: Base propia del Cubicador con esquema, seeder y documentación únicos (#327)
# Esta PR no toca src/ de .NET ni la BD compartida de QA (ver README.md: el corte real queda
# para una issue futura, #423). Verifica que la solución principal y el Cubicador siguen en verde.

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\..\..\.."
Set-Location $root

Write-Host "== Build (solución principal, no debería cambiar) ==" -ForegroundColor Cyan
dotnet build -c Release
if ($LASTEXITCODE -ne 0) { throw "Build falló" }

Write-Host "== Nexo.Domain.Tests ==" -ForegroundColor Cyan
dotnet test tests/Nexo.Domain.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "Domain.Tests falló" }

Write-Host "== Nexo.Application.Tests ==" -ForegroundColor Cyan
dotnet test tests/Nexo.Application.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "Application.Tests falló" }

Write-Host "== Suite de Python del Cubicador ==" -ForegroundColor Cyan
Push-Location cubicador
try {
    & .\.venv\Scripts\python.exe -m pytest tests -q
    if ($LASTEXITCODE -ne 0) { throw "Pruebas de cubicador fallaron" }
} finally {
    Pop-Location
}

Write-Host "OK. Para validar el esquema nuevo de punta a punta, levantar un SQL Server desechable con Docker" -ForegroundColor Yellow
Write-Host "y correr: py cubicador/database/scripts/ejecutar_schema_y_seeders.py --server <host>" -ForegroundColor Yellow
