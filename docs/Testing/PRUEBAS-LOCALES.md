# Pruebas locales contra la base real

Las pruebas de `Nexo.Api.Tests` (y el resto de la solución) se corren **contra la
base real** definida en `.env` (`dbInformesAutomatizados_Pruebas`, esquema
`db_NEXO`) — nunca contra una base de pruebas vacía o efímera. Es una decisión
explícita: solo así se valida que el comportamiento sea correcto contra datos
y relaciones reales. Ver `tests/Nexo.Api.Tests/Infra/clsApiFixture.cs`.

## Opción 1 — PowerShell (rápido, sin Docker)

Correr **todo en un solo bloque, en la misma consola** que ejecuta `dotnet test`
(si se separa en dos scripts/consolas distintas, las variables no se heredan y
`dotnet test` vuelve a caer al fallback de `localhost`):

```powershell
Get-Content .env | Where-Object { $_ -match '^(ConnectionStrings__Nexo|Database__Esquema|ConnectionStrings__DwSice|ConnectionStrings__DbSolucionesTi|ConnectionStrings__DwCubos|ConnectionStrings__IdentidadPortal|Cubicador3D__OutboundBaseUrl|Cubicador3D__RecomendacionesApiKey)=' } | ForEach-Object {
    $k, $v = $_ -split '=', 2
    Set-Item -Path "env:$k" -Value $v
}
$env:TEST_DB_CONNECTION = $env:ConnectionStrings__Nexo
dotnet test Nexo.Clean.sln -c Release
```

Las dos variables `Cubicador3D__*` (S5-HU-18.1/#273) son necesarias para
`tests/Nexo.Api.Tests/Cubicacion/clsCubicacionControllerE2ETests.cs`, que llama
al Cubicador real — requiere el stack de `cubicador/docker-compose.yml` arriba
(`docker compose up -d --wait` dentro de `cubicador/`, con la red compartida
`nexo_internal` del `docker-compose.yml` raíz ya levantada) y el catálogo local
sembrado (`docker compose exec api python scripts/preparar_catalogo_local.py`).
`Cubicador3D__OutboundBaseUrl` debe usar `127.0.0.1`, no `localhost` — ver el
comentario en `.env.example` sobre por qué (IPv6 + Docker Desktop en Windows).

## Opción 2 — Docker Compose (sin SDK de .NET instalado)

```powershell
docker compose -f docker-compose.ci.yml build
docker compose -f docker-compose.ci.yml run --rm pruebas-reales
```

Compose lee `.env` directamente (sin pasar por ninguna shell), así que no hay
riesgo de que una variable no propague.

## Limitación conocida — DWSice desde Docker (Opción 2) no funciona en local

Cualquier prueba que dispare una consulta real a DWSice (piso SICE-TAC, cuando
la línea de la solicitud tiene una ruta con origen/destino resuelto — ver
`clsObtenerCosteoQueryHandler`) falla con `500 Internal Server Error` al
correr `pruebas-reales`/`pruebas-reales-completas` en Docker en local. En la
Opción 1 (PowerShell, sin Docker) las mismas pruebas pasan sin problema.

**Causa:** DWSice corre en SQL Server 2008 R2 (SP3), con soporte TLS muy
limitado. El cliente .NET nativo de Windows (SChannel) y un cliente JDBC
(DBeaver) completan el handshake TLS con él sin problema; el cliente .NET de
Linux (OpenSSL, el que corre dentro del contenedor) no — el servidor cierra
la conexión justo después de recibir el ClientHello, sin responder nunca.
Se probó a fondo (cifrados/versión TLS, `Encrypt`, MTU, los tres modos de red
de Docker Desktop, `netsh portproxy`, incluso forzar OpenSSL 1.1 en vez de
3.0) sin que nada cambiara el resultado — no es algo ajustable desde este
repo ni desde la imagen Docker de pruebas.

**Para QA:** si una prueba abre/costea una solicitud con una ruta real
(origen/destino que resuelve), usar la **Opción 1 (PowerShell nativo)**. La
Opción 2 (Docker) sigue sirviendo para todo lo demás: build, `Domain.Tests`,
`Application.Tests`, y la mayoría de `Api.Tests` (los que no tocan DWSice).

El pipeline real de CI (`.github/workflows/ci-cd-api.yml`, runner self-hosted
`dokploy_`) no tiene este problema — corre nativo en Windows, en la misma red
donde vive Dokploy, y sí llega a DWSice sin problema.

## Qué NO hacer

- **No usar `pruebas` ni `pruebas-integracion`** de `docker-compose.ci.yml` para
  esto — esos dos usan una SQL Server efímera y vacía (`db-pruebas`), no la
  base real. Son para otro propósito (verificar que el comportamiento
  transaccional funciona desde cero).
- **No definir `TEST_DB_MIGRATE_ON_STARTUP=true`** contra la base real: el
  login de pruebas (`userInformesOw`) no tiene permisos DDL y no debe tenerlos.
  Si el esquema no está desplegado, las pruebas deben fallar con un error de
  SQL claro (tabla/columna inexistente), no migrar solas en silencio.

## Troubleshooting

| Síntoma | Causa probable |
|---|---|
| `Named Pipes Provider, error 40` / timeout de conexión | `TEST_DB_CONNECTION` no llegó al proceso de `dotnet test` (fallback a `localhost`) — usar el bloque de PowerShell completo de arriba, en una sola consola. |
| Error de red al conectar a `10.10.40.85` | VPN corporativa desconectada. Verificar con `Test-NetConnection -ComputerName 10.10.40.85 -Port 1433`. |
| `Invalid object name 'db_NEXO.tbl_...'` | El esquema/tabla todavía no existe en la base real — ver `docs/Database/MIGRACION-PREFIJO-TBL.md`. |
| Timeout solo dentro de Docker (Opción 2), pero el host sí conecta | La VPN puede hacer split-tunneling que excluye la red virtual de Docker — confirmar conectividad desde dentro del contenedor. |
| `500 Internal Server Error` al abrir/costear una solicitud con ruta real, solo en Docker (Opción 2) | DWSice no completa el TLS handshake con el cliente .NET Linux del contenedor — ver "Limitación conocida" arriba. Usar Opción 1. |
