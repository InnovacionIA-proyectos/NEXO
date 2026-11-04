<#
.SINOPSIS
Verificacion de la PR #431: "docs: contrato para el frontend y contrato unico NEXO <-> Cotifly".

.QUE VERIFICA
PR puramente documental (CONTRATO.md + docs/integraciones/*.md), CERO cambios de codigo.
Agrega la seccion "0. Como consumir este contrato" con un indice de TODOS los endpoints
(metodo + ruta + roles) generado a mano a partir de los controllers, mas correcciones de
roles en varias secciones existentes (AdminBiblioteca agregado donde faltaba) y dos nombres
de campo JSON (`clsCliente`, `clsRuta`) marcados como verificados contra la respuesta real.

Este script NO confia en "se ve bien": extrae programaticamente, de CADA controller real
bajo src/Nexo.Api, el verbo HTTP + ruta ([HttpGet]/[HttpPost]/.../[Route]) y el rol
EFECTIVO (combinando [Authorize] de clase y de metodo con AND, como lo hace ASP.NET Core
-- ver el comentario de S6-HU-25.1/#307 en varios controllers), y compara esa lista,
endpoint por endpoint, contra TODAS las filas de la tabla del indice nuevo en CONTRATO.md.
Tambien reporta cualquier ruta real que NO aparezca en el indice documentado.

.COMO SE VERIFICO EN VIVO
- Se escribio extraer_rutas.py (en esta misma carpeta) con un parser de atributos C# por
  regex, robusto al orden de atributos (en este codebase [HttpPost] a veces aparece ANTES
  de [Authorize] para el mismo metodo, y viceversa -- el parser escanea el bloque de
  atributos completo alrededor de cada [HttpX], no solo "hacia atras").
- Resuelve constantes locales (`private const string RolesEscritura = "...";`) usadas en
  `[Authorize(Roles = RolesEscritura)]` (patron real de clsDiccionarioClienteController.cs).
- Se corrio contra la rama de la PR (checkout real + `python extraer_rutas.py`), NO contra
  un diff textual: 151 endpoints reales extraidos, 154 filas en el indice de CONTRATO.md
  (154 incluye las 3 filas de SignalR "HUB", que no tienen controller y se excluyen de la
  comparacion automatica).
- Dos falsos positivos iniciales del propio script se investigaron y descartaron leyendo
  clsProgram.cs: `GET /api/sesion/actual` y `GET /health` no tienen `[Authorize]` propio,
  pero clsProgram.cs define un `FallbackPolicy` global (linea ~156) que exige usuario
  autenticado + un rol valido de Nexo para CUALQUIER endpoint sin `[Authorize]` explicito
  -- excepto `/health`, que tiene `.AllowAnonymous()` explicito (linea ~318). El contrato
  documenta ambos correctamente ("Todos" y "Anonimo" respectivamente); el script fue
  actualizado para no tratarlos como fallo real sino como limitacion conocida (no modela
  el FallbackPolicy global ni los endpoints de Program.cs fuera de controllers).
- Los nombres de campo `clsCliente` (clsBandejaItemDto) y `clsRuta` (clsLineaCosteoDto,
  clsPropuestaLineaDto) se verificaron leyendo directamente el record de C# en
  src/Nexo.Application/.../DTOs/SolicitudDtos.cs y PropuestaTarifaDtos.cs: la propiedad se
  llama LITERALMENTE `clsCliente`/`clsRuta` (no es un alias de serializacion), asi que
  System.Text.Json (camelCase) la serializa tal cual. Confirmado desde el codigo fuente,
  mas fuerte que solo "verificado contra la respuesta real".

.HALLAZGOS (reportados, NO corregidos por este script)
1. El indice nuevo (S3.2) dice `POST /api/solicitudes/{id}/lineas`, pero la ruta real
   (y la seccion detallada de la MISMA PR, un poco mas abajo en el archivo) es
   `POST /api/solicitudes/{id}/lineas:bulk`. Inconsistencia interna del propio indice
   nuevo contra su propia seccion detallada.
2. El endpoint real `POST /api/planes-cargue/desde-linea/{id}` (documentado en su seccion
   detallada mas abajo) no aparece en el indice nuevo (S3.15 Plan de cargue).
Ambos son omisiones menores en la tabla-indice nueva (bajo riesgo: la documentacion
detallada de cada endpoint, mas abajo en el mismo archivo, SI es correcta en ambos casos).

.LIMITACIONES CONOCIDAS
- No valida los ~140 endpoints restantes linea por linea en prosa (fuera del indice), solo
  el indice estructurado de la seccion 0.4 contra el codigo real.
- No levanta la API real; los nombres de campo JSON se confirmaron por lectura directa del
  codigo fuente (mas confiable que una llamada HTTP puntual, pero no es lo mismo que un
  test de integracion).
- No modela atributos de autorizacion fuera de controllers (minimal APIs en clsProgram.cs,
  SignalR Hubs) -- se excluyen explicitamente de la comparacion automatica y se revisaron
  a mano los 2 casos que el script marco como posible falso positivo.
#>

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
Set-Location $repoRoot

Write-Host "=== Comparando el indice de endpoints de CONTRATO.md contra los controllers reales ===" -ForegroundColor Cyan
python (Join-Path $PSScriptRoot "extraer_rutas.py")
$codigoExtractor = $LASTEXITCODE

Write-Host ""
if ($codigoExtractor -ne 0) {
    Write-Host "RESULTADO: HAY DISCREPANCIAS (ver detalle arriba; los 2 hallazgos conocidos de omision en el indice nuevo estan documentados en .HALLAZGOS de este script, no bloquean)" -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "RESULTADO: TODO OK" -ForegroundColor Green
    exit 0
}
