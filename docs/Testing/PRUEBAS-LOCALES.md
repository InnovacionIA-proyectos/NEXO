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
Get-Content .env | Where-Object { $_ -match '^(ConnectionStrings__Nexo|ConnectionStrings__DwSice|ConnectionStrings__DbSolucionesTi|ConnectionStrings__DwCubos|ConnectionStrings__IdentidadPortal)=' } | ForEach-Object {
    $k, $v = $_ -split '=', 2
    Set-Item -Path "env:$k" -Value $v
}
$env:TEST_DB_CONNECTION = $env:ConnectionStrings__Nexo
dotnet test Nexo.Clean.sln -c Release
```

## Opción 2 — Docker Compose (sin SDK de .NET instalado)

```powershell
docker compose -f docker-compose.ci.yml build
docker compose -f docker-compose.ci.yml run --rm pruebas-reales
```

Compose lee `.env` directamente (sin pasar por ninguna shell), así que no hay
riesgo de que una variable no propague.

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
