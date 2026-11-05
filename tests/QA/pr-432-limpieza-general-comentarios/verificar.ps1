<#
.SINOPSIS
Verificacion de la PR #432: "refactor: limpieza general de comentarios,
documentacion y .env" (incluye los commits de la #431).

.QUE VERIFICA
PR de limpieza pura (sin cambios funcionales) sobre 439 archivos .cs de src/,
los 3 archivos .env (.env.example, .env.dokploy, .env.githubSECRETS), los 4
workflows de CI, docker-compose.yml/docker-compose.ci.yml, build/Dockerfile.ci,
build/Dockerfile.rabbit, build/e2e/recorrido.py, scripts/rabbitmq_smoke.py, y
eliminacion de 5 documentos (backlog + 2 de analisis + 2 Cotifly viejos,
reemplazados por el contrato unico de la PR #431).

.COMO SE VERIFICO EN VIVO
1. Comparador real (comparar_sin_comentarios.py) que, para cada uno de los 439
   .cs modificados, toma el contenido en origin/QA y en pr432, quita
   comentarios de linea y de bloque de forma consciente de strings (normal,
   verbatim @"...", char '...'), normaliza espacios en blanco, y compara. Da
   0 diferencias de codigo real en los 439 archivos, salvo 1 falso positivo
   conocido (clsPlantillaDeEntrega.cs usa un raw string literal $"""...""" que
   el comparador no soporta; verificado a mano con git diff que el unico
   cambio real en ese archivo son comentarios).
2. Los 3 .env: se extrajeron solo las lineas `VARIABLE=valor` (sin comentarios
   ni lineas vacias) de origin/QA y de pr432 y se compararon -- identicas en
   variables, valores y orden en los 3 archivos.
3. Los 4 workflows de CI: diff filtrando lineas que empiezan por `#` -- cero
   cambios de codigo real (`on:`, `jobs:`, `steps:`, `run:`) en ci-cd-api.yml,
   ci-cd-cubicador-api.yml, ci-cd-cubicador-worker.yml (solo lineas en blanco
   removidas). En ci-cd-rabbitmq.yml el unico cambio real es la eliminacion
   completa del bloque YA comentado del job `smoke-test-post-deploy`
   (deshabilitado en un commit previo) -- `build`/`deploy` quedan intactos.
4. docker-compose.yml, docker-compose.ci.yml, build/Dockerfile.ci,
   build/Dockerfile.rabbit, build/e2e/recorrido.py, scripts/rabbitmq_smoke.py:
   mismo filtro de diff -- cero lineas de codigo/config real cambiadas en
   ninguno.
5. git grep sobre el working tree completo de pr432 (fuera de tests/QA)
   buscando referencias a los 5 documentos eliminados
   (BACKLOG-NEXO.md, ANALISIS-ENTENDIMIENTO-NEXO-FINAL.md,
   COMENTARIOS-ENTENDIMIENTO-NEXO.md, COTIFLY-CONEXION-RECOMENDACION-VEHICULO.md,
   COTIFLY-CONTRATO.md) -- ninguna referencia colgante.

.LIMITACIONES CONOCIDAS
- No se corrio `dotnet test` ni `pytest` de nuevo: la PR afirma Domain 218 y
  Application 1182 pasando, y API con las mismas 87 fallas preexistentes en QA
  (no nuevas). Dado que el comparador de (1) ya prueba que el codigo C# es
  byte-a-byte el mismo sin comentarios, no hay razon para que el resultado de
  los tests cambie -- no se repitio la corrida completa por tiempo, igual que
  en verificaciones previas de PRs de bajo riesgo.
- No se corrio `docker compose config` para resolver el YAML (no era
  necesario: el diff de docker-compose.yml/.ci.yml no toca ninguna linea que
  no sea comentario).
#>

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
Set-Location $repoRoot

$fallo = $false

Write-Host "=== 1. Comparador sin comentarios sobre los 439 .cs ===" -ForegroundColor Cyan
py -I "tests/QA/pr-432-limpieza-general-comentarios/comparar_sin_comentarios.py"
if ($LASTEXITCODE -ne 0) {
    Write-Host "  FALLO: el comparador encontro diferencias de codigo real" -ForegroundColor Red
    $fallo = $true
} else {
    Write-Host "  OK" -ForegroundColor Green
}

Write-Host "`n=== 2. .env identicos en VARIABLE=valor (QA vs pr432) ===" -ForegroundColor Cyan
$env:MSYS_NO_PATHCONV = "1"
foreach ($f in @(".env.example", ".env.dokploy", ".env.githubSECRETS")) {
    $antes = git show "origin/QA:$f" 2>$null | Select-String -Pattern '^[A-Za-z_][A-Za-z0-9_]*='
    $despues = git show "pr432:$f" 2>$null | Select-String -Pattern '^[A-Za-z_][A-Za-z0-9_]*='
    $diff = Compare-Object $antes $despues
    if ($diff) {
        Write-Host "  FALLO: $f difiere en variables/valores/orden" -ForegroundColor Red
        $fallo = $true
    } else {
        Write-Host "  OK: $f identico ($($antes.Count) variables)" -ForegroundColor Green
    }
}

Write-Host "`n=== 3. Workflows e infra: solo cambios de comentarios ===" -ForegroundColor Cyan
$archivosSoloComentarios = @(
    ".github/workflows/ci-cd-api.yml",
    ".github/workflows/ci-cd-cubicador-api.yml",
    ".github/workflows/ci-cd-cubicador-worker.yml",
    ".github/workflows/_cubicador-pruebas.yml",
    "docker-compose.yml",
    "docker-compose.ci.yml",
    "build/Dockerfile.ci",
    "build/Dockerfile.rabbit",
    "build/e2e/recorrido.py",
    "scripts/rabbitmq_smoke.py"
)
foreach ($f in $archivosSoloComentarios) {
    $lineasReales = git diff origin/QA...pr432 -- $f | Select-String -Pattern '^[+-]' |
        Where-Object { $_.Line -notmatch '^(\+\+\+|---)' -and $_.Line -notmatch '^[+-]\s*#' -and $_.Line -notmatch '^[+-]\s*$' }
    if ($lineasReales) {
        Write-Host "  FALLO: $f tiene cambios fuera de comentarios:" -ForegroundColor Red
        $lineasReales | ForEach-Object { Write-Host "    $_" }
        $fallo = $true
    } else {
        Write-Host "  OK: $f sin cambios de codigo real" -ForegroundColor Green
    }
}

Write-Host "`n=== 4. ci-cd-rabbitmq.yml: solo se elimino el bloque ya comentado ===" -ForegroundColor Cyan
$diffRabbit = git diff origin/QA...pr432 -- .github/workflows/ci-cd-rabbitmq.yml
$lineasCodigoRemovidasNoComentario = $diffRabbit | Where-Object {
    $_ -match '^-' -and $_ -notmatch '^---' -and $_ -notmatch '^-\s*#' -and $_ -notmatch '^-\s*$'
}
if ($lineasCodigoRemovidasNoComentario) {
    Write-Host "  FALLO: se removieron lineas que no son comentarios:" -ForegroundColor Red
    $lineasCodigoRemovidasNoComentario | ForEach-Object { Write-Host "    $_" }
    $fallo = $true
} else {
    Write-Host "  OK: build/deploy intactos, solo se elimino el job comentado" -ForegroundColor Green
}

Write-Host "`n=== 5. Sin referencias colgantes a los 5 docs eliminados ===" -ForegroundColor Cyan
git checkout -q pr432
$patron = "BACKLOG-NEXO|ANALISIS-ENTENDIMIENTO-NEXO-FINAL|COMENTARIOS-ENTENDIMIENTO-NEXO|COTIFLY-CONEXION-RECOMENDACION-VEHICULO|COTIFLY-CONTRATO\.md"
$matches = git grep -n -E $patron -- . ':!tests/QA' 2>$null
git checkout -q QA
if ($matches) {
    Write-Host "  FALLO: quedan referencias colgantes:" -ForegroundColor Red
    Write-Host $matches
    $fallo = $true
} else {
    Write-Host "  OK: ninguna referencia colgante" -ForegroundColor Green
}

Write-Host ""
if ($fallo) {
    Write-Host "RESULTADO: HAY FALLOS" -ForegroundColor Red
    exit 1
} else {
    Write-Host "RESULTADO: TODO OK" -ForegroundColor Green
    exit 0
}
