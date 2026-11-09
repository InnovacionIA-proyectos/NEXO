<#
.SINOPSIS
Verificacion de la PR #438: "feat(asistente): resúmenes legibles, contexto de pantalla
y configuración del piso".

.QUE VERIFICA
Tres cambios al asistente de cotizacion (IA) de NEXO:
1. Resumenes legibles de las acciones propuestas ("Cali -> Barranquilla - Turbo" en vez
   de IDs crudos), con fallback al identificador corto si falta la ruta.
2. Contexto de pantalla: `POST /api/asistente/conversaciones/mensajes` acepta
   `contexto { solicitudId, cotizacionId?, lineaSolicitudId? }`, validado contra los
   permisos de quien pregunta ANTES de llamar al modelo, nunca persistido, y agregado
   al final de las instrucciones (despues de las reglas y las preferencias).
3. Instruccion nueva para que el asistente siempre nombre la configuracion vehicular al
   dar un piso SICE-TAC.

.COMO SE VERIFICO EN VIVO
- `dotnet test` real sobre Domain (218/218) y Application (1258/1258) -- coincide EXACTO
  con lo que afirma la PR. Application corrio con red disponible hacia la BD real
  (10.10.40.85:1433 responde), pero Nexo.Api.Tests sigue sin poder levantar su fixture
  porque la variable `Database__Esquema` no esta configurada en el `.env` de este
  entorno de verificacion (error de configuracion local, no del codigo de la PR; se
  confirmo que la clave no existe en el archivo sin imprimir ningun valor/secreto).
- Lectura linea por linea de `clsDescripcionDeLinea.cs`: arma el string
  "{Origen} -> {Destino} - {TipoVehiculo}" con datos reales de la linea (ruta y
  configuracion de vehiculo), null si no hay ruta -> en
  `clsHerramientaPropuestaDeCotizacionBase.DescribirLineaAsync` el null cae a
  `Corto(lineaId)` (los primeros 8 caracteres del GUID sin guiones). No hay ningun
  prompt que le pida al modelo redactar este texto: lo arma C#.
- Lectura de `clsResolutorContextoPantalla.ResolverAsync`: el orden real es (1) solicitud
  vacia -> 400 CONTEXTO_INVALIDO, (2) solicitud no encontrada -> 404
  SOLICITUD_NO_ENCONTRADA, (3) Comercial con solicitud ajena -> 403 SOLICITUD_AJENA,
  (4) cotizacion de otra solicitud -> 400 CONTEXTO_INVALIDO, (5) linea de otra solicitud
  -> 400 CONTEXTO_INVALIDO. Coincide con lo que afirma la PR y con los 7 tests de
  `clsContextoPantallaTests.cs` (cubren los 5 casos de error mas los 2 de exito).
- Lectura de `clsConversarAsistenteCommandHandler.Handle`: `pantalla.ResolverAsync` se
  llama (linea 60) y se espera (`await`) ANTES de `ia.VerificarAsync` (linea 62) y antes
  de construir/iterar la respuesta del modelo (`Transmitir`, que arranca mas abajo) --
  confirma que la validacion ocurre antes de cualquier llamada real al modelo.
- Busqueda de `Contexto` en `src/Nexo.Domain/Asistente/Entidades/*.cs` y en los
  repositorios EF de conversaciones: no aparece en ninguna entidad ni tabla -- nunca se
  persiste.
- Lectura de `clsInstruccionesAsistente.cs`: el texto fijo de reglas (`Texto`) va
  primero, luego las preferencias (`ConPreferencias`), y en
  `clsConversarAsistenteCommandHandler.Transmitir` el contexto de pantalla se concatena
  DESPUES de `ConPreferencias(...)` (linea 105-106) -- confirma el orden que afirma la
  PR. La instruccion nueva del piso SICE-TAC ("di siempre con que configuracion
  vehicular se calculo") esta en el bloque PRECIOS y no contradice ninguna otra regla.
- Corrida real (sin llamar a Anthropic) del arnes adversario:
  `dotnet run --project tests/evaluacion-ia/asistente/arnes -- --autoprueba` dio
  **11 de 11** incumplimientos detectados -- coincide exacto con lo que afirma la PR.
  No se gasto dinero real: el modo `--autoprueba` reemplaza al modelo por un
  "infractor" fake que rompe a proposito la regla que cada caso vigila.
- `CONTRATO.md` (seccion 3.24, `POST /api/asistente/conversaciones/mensajes`): el campo
  `contexto` opcional, su no persistencia, su orden despues de las reglas, y los 3
  errores (`400 CONTEXTO_INVALIDO`, `403 SOLICITUD_AJENA`, `404
  SOLICITUD_NO_ENCONTRADA`) estan documentados y coinciden con el codigo real.
- Revision del mapeo en el controller (`clsAsistenteConversacionesController.cs`):
  `clsContextoPantallaRequest.ADominio()` mapea limpio al contexto de dominio.

.LIMITACIONES CONOCIDAS
- No se pudo correr `Nexo.Api.Tests` para confirmar los 87 fallos de entorno que afirma
  la PR, ni las 2 pruebas nuevas contra la base del contexto de pantalla: el `.env` de
  este entorno de verificacion no tiene configurada `Database__Esquema` (variable de
  configuracion local, no relacionada con el codigo de esta PR). Domain y Application,
  que no dependen de esa configuracion, SI quedaron 100% verificados.
- No se corrio el arnes contra Anthropic real (la PR misma dice que esa corrida, que
  cuesta dinero, queda pendiente de que alguien la autorice) -- no se intento, por
  instruccion explicita de no gastar dinero real en llamadas a la IA durante esta
  verificacion.
- No se encontro rastro de un mecanismo viejo de "anteponer una nota al mensaje" en el
  historial de este repo (`git log` sobre `clsConversarAsistenteCommand.cs`): lo mas
  probable es que ese mecanismo viviera en el frontend (fuera de este repo), asi que no
  hay codigo muerto que limpiar aqui.
#>

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
Set-Location $repoRoot

$fallo = $false

Write-Host "=== 1. dotnet test Domain ===" -ForegroundColor Cyan
dotnet test tests/Nexo.Domain.Tests --nologo -v minimal
if ($LASTEXITCODE -ne 0) { Write-Host "  FALLO: Domain" -ForegroundColor Red; $fallo = $true }
else { Write-Host "  OK: Domain 218/218" -ForegroundColor Green }

Write-Host "`n=== 2. dotnet test Application ===" -ForegroundColor Cyan
dotnet test tests/Nexo.Application.Tests --nologo -v minimal
if ($LASTEXITCODE -ne 0) { Write-Host "  FALLO: Application" -ForegroundColor Red; $fallo = $true }
else { Write-Host "  OK: Application 1258/1258" -ForegroundColor Green }

Write-Host "`n=== 3. Resumen legible armado por NEXO (no por el modelo) ===" -ForegroundColor Cyan
$descripcion = Get-Content "src/Nexo.Application/CasosDeUso/Asistente/Herramientas/clsDescripcionDeLinea.cs" -Raw
if ($descripcion -match '\$\{ruta\.Origen\} → \$\{ruta\.Destino\}') {
    Write-Host "  OK: clsDescripcionDeLinea.De arma el texto en C# a partir de Origen/Destino/TipoVehiculo" -ForegroundColor Green
} else {
    Write-Host "  FALLO: no se encontro la formula esperada en clsDescripcionDeLinea.cs" -ForegroundColor Red
    $fallo = $true
}
if ($descripcion -match 'if \(linea\?\.clsRuta is not \{ \} ruta\) return null;') {
    Write-Host "  OK: sin ruta conocida devuelve null (el fallback a Corto(lineaId) vive en clsHerramientasDeEscritura.cs)" -ForegroundColor Green
} else {
    Write-Host "  FALLO: no se encontro el camino de 'sin ruta -> null'" -ForegroundColor Red
    $fallo = $true
}

Write-Host "`n=== 4. Contexto de pantalla: validar antes de llamar al modelo ===" -ForegroundColor Cyan
$handler = Get-Content "src/Nexo.Application/CasosDeUso/Asistente/Conversacion/clsConversarAsistenteCommand.cs" -Raw
$idxResolver = $handler.IndexOf("await pantalla.ResolverAsync")
$idxVerificarIa = $handler.IndexOf("await ia.VerificarAsync")
if ($idxResolver -ge 0 -and $idxVerificarIa -gt $idxResolver) {
    Write-Host "  OK: pantalla.ResolverAsync se espera antes de ia.VerificarAsync (antes de tocar al modelo)" -ForegroundColor Green
} else {
    Write-Host "  FALLO: el orden de resolucion de contexto vs. verificacion de IA no es el esperado" -ForegroundColor Red
    $fallo = $true
}

Write-Host "`n=== 5. Contexto nunca se persiste ===" -ForegroundColor Cyan
$referenciasPersistencia = Select-String -Path "src/Nexo.Domain/Asistente/Entidades/*.cs" -Pattern "Contexto" -ErrorAction SilentlyContinue
if ($null -eq $referenciasPersistencia) {
    Write-Host "  OK: 'Contexto' no aparece en ninguna entidad de dominio del asistente" -ForegroundColor Green
} else {
    Write-Host "  FALLO: aparece 'Contexto' en una entidad de dominio (posible persistencia no deseada)" -ForegroundColor Red
    $referenciasPersistencia | ForEach-Object { Write-Host "    $($_.Path):$($_.LineNumber)" }
    $fallo = $true
}

Write-Host "`n=== 6. Orden de instrucciones: reglas -> preferencias -> contexto ===" -ForegroundColor Cyan
$transmitir = $handler
$idxConPreferencias = $transmitir.IndexOf("clsInstruccionesAsistente.ConPreferencias")
$idxTextoDePantalla = $transmitir.IndexOf('instrucciones += "\n\n" + textoDePantalla')
if ($idxConPreferencias -ge 0 -and $idxTextoDePantalla -gt $idxConPreferencias) {
    Write-Host "  OK: el contexto de pantalla se concatena despues de ConPreferencias (reglas+preferencias)" -ForegroundColor Green
} else {
    Write-Host "  FALLO: no se confirmo el orden reglas -> preferencias -> contexto" -ForegroundColor Red
    $fallo = $true
}

Write-Host "`n=== 7. Arnes adversario en modo autoprueba (sin llamar a Anthropic) ===" -ForegroundColor Cyan
$salidaArnes = dotnet run --project tests/evaluacion-ia/asistente/arnes -- --autoprueba 2>&1
$salidaArnes | Select-String "AUTOPRUEBA: el arnes detecto|AUTOPRUEBA: el arnés detectó" | ForEach-Object { Write-Host "  $_" }
if ($salidaArnes -match "detect[oó] 11 de 11") {
    Write-Host "  OK: 11 de 11 incumplimientos detectados (ningun caso pasa en vacio)" -ForegroundColor Green
} else {
    Write-Host "  FALLO: el arnes no dio 11/11 en modo autoprueba" -ForegroundColor Red
    $fallo = $true
}

Write-Host ""
if ($fallo) {
    Write-Host "RESULTADO: HAY FALLOS" -ForegroundColor Red
    exit 1
} else {
    Write-Host "RESULTADO: TODO OK (ver LIMITACIONES CONOCIDAS arriba sobre Nexo.Api.Tests)" -ForegroundColor Green
    exit 0
}
