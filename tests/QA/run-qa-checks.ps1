<#
.SYNOPSIS
    Reproduce la verificación de QA documentada en tests/QA/README.md:
    corre las 3 suites .NET de la solución y, si la rama actual trae el
    servicio `cubicador/` (Python), su suite de pytest y el smoke test
    contra el stack real de Docker Compose.

.DESCRIPTION
    No modifica ningún archivo de código fuente. Crea artefactos temporales
    (venv de Python, contenedores Docker) que limpia al terminar.

.PARAMETER SkipDocker
    Omite el smoke test de Docker Compose (más lento, requiere Docker
    Desktop corriendo). Los tests unitarios/integración de pytest y dotnet
    igual se ejecutan.

.EXAMPLE
    ./tests/QA/run-qa-checks.ps1
    Corre todo: dotnet test + (si aplica) pytest + smoke Docker.

.EXAMPLE
    ./tests/QA/run-qa-checks.ps1 -SkipDocker
    Corre dotnet test + pytest, sin levantar el stack de Docker.
#>
param(
    [switch]$SkipDocker
)

$ErrorActionPreference = "Stop"
$repoRoot = git rev-parse --show-toplevel
Set-Location $repoRoot

$failures = @()

function Write-Section($title) {
    Write-Output ""
    Write-Output "=== $title ==="
}

# --------------------------------------------------------------- .NET
$sln = Join-Path $repoRoot "CabinaFletes.Clean.sln"
if (Test-Path $sln) {
    Write-Section ".NET: dotnet test CabinaFletes.Clean.sln"
    dotnet test $sln --logger "console;verbosity=normal"
    if ($LASTEXITCODE -ne 0) {
        $failures += ".NET (dotnet test) — revisa si CabinaFletes.Api.Tests falla por falta de conexion a la BD SQL Server corporativa de pruebas (esperado fuera de un runner con VPN/acceso); cualquier otra falla es real."
    }
} else {
    Write-Output "(sin $sln en esta rama, se omite la parte .NET)"
}

# --------------------------------------------------------------- Python (cubicador)
$cubicador = Join-Path $repoRoot "cubicador"
if (Test-Path $cubicador) {
    Push-Location $cubicador
    try {
        Write-Section "Python: pytest (cubicador)"
        if (-not (Test-Path ".venv")) {
            python -m venv .venv
        }
        & ".venv/Scripts/pip.exe" install -q -r requirements.txt
        & ".venv/Scripts/python.exe" -m pytest -v
        if ($LASTEXITCODE -ne 0) {
            $failures += "Python (pytest) en cubicador/"
        }

        if (-not $SkipDocker) {
            Write-Section "Docker: smoke test contra el stack real (Postgres + RabbitMQ + api + worker)"
            if (-not (Test-Path ".env")) {
                @"
POSTGRES_PASSWORD=ci_password
RABBITMQ_DEFAULT_USER=ci_user
RABBITMQ_DEFAULT_PASS=ci_password
CUBICADOR_API_KEY=ci-api-key
NEXO_CALLBACK_API_KEY=ci-nexo-key
NEXO_BASE_URL=http://localhost:5080
"@ | Set-Content -Encoding utf8 ".env"
            }
            docker compose up -d --build --wait --wait-timeout 180
            try {
                $env:CUBICADOR_API_KEY = "ci-api-key"
                $env:RABBITMQ_DEFAULT_USER = "ci_user"
                $env:RABBITMQ_DEFAULT_PASS = "ci_password"
                & ".venv/Scripts/python.exe" "scripts/ci_docker_smoke.py"
                if ($LASTEXITCODE -ne 0) {
                    $failures += "Docker smoke test (cubicador/scripts/ci_docker_smoke.py)"
                }
            } finally {
                docker compose logs --tail 50
                docker compose down -v
            }
        } else {
            Write-Output "(smoke test de Docker omitido por -SkipDocker)"
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Output "(sin carpeta cubicador/ en esta rama, se omite la parte Python)"
}

# --------------------------------------------------------------- Resumen
Write-Section "Resumen"
if ($failures.Count -eq 0) {
    Write-Output "Todo verde."
    exit 0
} else {
    Write-Output "Fallas encontradas:"
    $failures | ForEach-Object { Write-Output " - $_" }
    exit 1
}
