<#
.SINOPSIS
Verificación de la PR #435: "feat(diccionario): términos por cliente con NIT y 409 ante
duplicados simultáneos" (los clientes del DW no traen clienteId en GET /api/solicitudes/clientes,
así que el diccionario por cliente no tenía cómo recibirlos).

.QUE VERIFICA
1. POST /api/diccionario-cliente acepta clienteNit en lugar de clienteId: usa el cliente local o
   lo crea con los datos del DW (mismo patrón clsClienteDeImportacion que ya usan plantillas-mapeo
   y recetas-lectura -- se confirmó leyendo ese código compartido, no se duplicó lógica).
2. clienteId + clienteNit a la vez -> 400 CLIENTE_AMBIGUO; NIT desconocido -> 400 CLIENTE_NIT_INVALIDO.
3. GET /api/diccionario-cliente?clienteNit= solo BUSCA (nunca crea) -- confirmado en
   clsListarDiccionarioClienteQueryHandler: clienteId ??= Guid.Empty si el NIT no tiene cliente local.
4. Concurrencia -> 409 TERMINO_DUPLICADO en vez de 500: reposa sobre el índice único YA EXISTENTE
   IX_diccionario_cliente_Unico (ClienteId, TerminoNormalizado, Tipo, Contexto) y el YA EXISTENTE
   IX_clientes_Nit -- la PR correctamente dice "sin cambios de base de datos" porque ambos índices
   son anteriores a esta PR. El código nuevo solo agrega el catch EsChoqueUnico (SQL 2601/2627) +
   reintento con recursión: si el choque fue en la creación del CLIENTE (no del término), el
   reintento vuelve a resolver el NIT -- esta vez encuentra el cliente ya comprometido por la
   petición ganadora -- y entonces sí encuentra/crea el término correctamente. Revisado línea por
   línea en clsDiccionarioClienteCasosDeUso.cs, sin bugs.
5. Domain, Application y API (contra la BD real de pruebas, con concurrencia real de 8 peticiones
   simultáneas) corridos de punta a punta.
6. CONTRATO.md y el contrato de Cotifly: los códigos de error documentados (CLIENTE_AMBIGUO,
   CLIENTE_NIT_INVALIDO, TERMINO_DUPLICADO) coinciden exactos con el código real.
7. tests/evaluacion-ia/arnes/Program.cs: el único cambio es agregar las 2 implementaciones nuevas
   de la interfaz (EsChoqueUnico/DescartarCambiosPendientes) al fake clsDiccionarioVacio -- necesario
   porque esta PR agrega esos 2 métodos a IDiccionarioClienteRepository, no es un descuido.

.COMO SE VERIFICO EN VIVO
- Se creó un worktree aislado (git worktree add) para no interferir con otras verificaciones
  paralelas en el mismo repo.
- dotnet test real sobre Domain (218/218) y Application (1256/1256): coincide exacto con lo
  que afirma la PR.
- dotnet test real sobre API, con TEST_DB_CONNECTION apuntando a la BD real de pruebas
  (dbInformesAutomatizados_Pruebas, 10.10.40.85) y Database__Esquema=db_NEXO: 1182 pasaron,
  22 omitidos (pruebas que dependen de credenciales de IA que no estaban configuradas en este
  entorno), 0 fallos -- MEJOR que los "87 fallos de entorno" que afirma la PR (ese número parece
  depender de condiciones del entorno del autor que no se reprodujeron acá; en este momento, con
  acceso real a la BD, la suite completa pasó limpia).
- El test clsClienteNitImportacionApiTests revisado a mano: lanza 8 POST genuinamente en paralelo
  (Task.WhenAll + Task.Run) contra el mismo NIT/término, y confirma exactamente 1 Created + 7
  Conflict(TERMINO_DUPLICADO), 1 solo cliente creado y 1 solo término -- no es una simulación
  secuencial. Limpia sus datos de prueba en un bloque finally (LimpiarConDiccionario).
- Diff de ESQUEMA_NEXO.sql: confirmado que NO hay cambios de esquema en esta PR (los índices
  que respaldan la concurrencia ya existían).

.LIMITACIONES CONOCIDAS
- No se reprodujeron exactamente los "87 fallos de entorno que ya tiene QA" que afirma la PR --
  en esta verificación, con conectividad real a la BD de pruebas, la suite de API dio 0 fallos.
  No es una discrepancia preocupante (todo lo que corrió, pasó), pero no se puede confirmar
  cifra por cifra ese número específico sin replicar las condiciones exactas del entorno del autor.
- No se ejecutó este script de forma automática de punta a punta (requiere dotnet test con
  TEST_DB_CONNECTION/Database__Esquema configurados y acceso de red a 10.10.40.85, que no siempre
  está disponible); se documentan aquí los comandos exactos para repetirlo.
#>

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
Set-Location $repoRoot

Write-Host "=== 1. Domain y Application (no dependen de la BD real) ===" -ForegroundColor Cyan
dotnet test tests/Nexo.Domain.Tests --nologo -v q
dotnet test tests/Nexo.Application.Tests --nologo -v q

Write-Host "`n=== 2. API contra la BD real de pruebas (requiere .env.pruebas-reales.local) ===" -ForegroundColor Cyan
if (-not (Test-Path ".env.pruebas-reales.local")) {
    Write-Host "  OMITIDO: no existe .env.pruebas-reales.local en este entorno." -ForegroundColor Yellow
} else {
    function Load-EnvVar($name) {
        $line = Get-Content ".env.pruebas-reales.local" | Where-Object { $_ -match "^$name=" } | Select-Object -First 1
        if (-not $line) { throw "No se encontro $name" }
        return $line.Substring($name.Length + 1)
    }
    $env:ConnectionStrings__Nexo = Load-EnvVar "ConnectionStrings__Nexo"
    $env:Database__Esquema = Load-EnvVar "Database__Esquema"
    $env:TEST_DB_CONNECTION = $env:ConnectionStrings__Nexo
    dotnet test tests/Nexo.Api.Tests --nologo -v q
}

Write-Host "`nRESULTADO: ver conteos arriba. Esperado: Domain 218/218, Application 1256/1256, API 0 fallos (algunos omitidos sin credenciales de IA)." -ForegroundColor Green
