# Pruebas de NEXO

Cómo se corren las pruebas del backend y cómo se manejan los datos que crean en la base compartida.

| Carpeta | Qué prueba |
|---|---|
| `Nexo.Domain.Tests` | Reglas del dominio: costeo, priorización, máquina de estados. No usa base de datos. |
| `Nexo.Application.Tests` | Casos de uso con puertos simulados. |
| `Nexo.Api.Tests` | La API completa contra la base real (ver abajo). |
| `evaluacion-ia/` | Conjunto de prueba y arnés de evaluación de la IA de importación de Excel. No corre en el CI. |
| `QA/` | Evidencia de verificaciones hechas a mano (consultas y resultados). |

## 1. Las pruebas corren contra la base real

`Nexo.Api.Tests` se corre **contra la base real** definida en `.env`, nunca contra una base vacía o temporal: solo así se
valida el comportamiento con datos y relaciones reales (ver `tests/Nexo.Api.Tests/Infra/clsApiFixture.cs`). Hoy esa base es
la de QA (`dbInformesAutomatizados_Pruebas`, esquema `db_NEXO`); cambiará cuando QA pase a la base propia.

### Opción 1: PowerShell (rápida, sin Docker)

Todo en un solo bloque y en la misma consola que ejecuta `dotnet test`. Si se separa en dos consolas, las variables no se
heredan y `dotnet test` cae a `localhost`.

```powershell
Get-Content .env | Where-Object { $_ -match '^(ConnectionStrings__Nexo|Database__Esquema|ConnectionStrings__DwSice|ConnectionStrings__DbSolucionesTi|ConnectionStrings__DwCubos|ConnectionStrings__IdentidadPortal|Cubicador3D__OutboundBaseUrl|Cubicador3D__RecomendacionesApiKey)=' } | ForEach-Object {
    $k, $v = $_ -split '=', 2
    Set-Item -Path "env:$k" -Value $v
}
$env:TEST_DB_CONNECTION = $env:ConnectionStrings__Nexo
dotnet test Nexo.Clean.sln -c Release
```

Las dos variables `Cubicador3D__*` las necesita `tests/Nexo.Api.Tests/Cubicacion/clsCubicacionControllerE2ETests.cs`, que
llama al Cubicador real. Para esa prueba hay que levantar el Cubicador local: `docker compose up -d --wait` dentro de
`cubicador/` (con la red `nexo_internal` del `docker-compose.yml` raíz ya creada) y sembrar su catálogo con
`docker compose exec api python scripts/preparar_catalogo_local.py`. `Cubicador3D__OutboundBaseUrl` debe usar `127.0.0.1`,
no `localhost` (ver el comentario en `.env.example`).

### Opción 2: Docker Compose (sin el SDK de .NET)

```powershell
docker compose -f docker-compose.ci.yml build
docker compose -f docker-compose.ci.yml run --rm pruebas-reales
```

Compose lee `.env` directamente, así que no hay riesgo de que una variable no llegue.

**Limitación:** desde Docker en local, las pruebas que consultan el SICE-TAC real fallan con `500`. El servidor del DW
(SQL Server 2008 R2) no completa el handshake TLS con el cliente .NET de Linux que corre dentro del contenedor; con el
cliente de Windows sí. Se probaron cifrados, versión de TLS, `Encrypt`, MTU, los modos de red de Docker Desktop y OpenSSL
1.1, sin cambio. Para esas pruebas se usa la Opción 1. El CI (`.github/workflows/ci-cd-api.yml`) no tiene el problema
porque corre en Windows, en la red del DW.

### Qué no hacer

- **No usar los servicios `pruebas` ni `pruebas-integracion`** de `docker-compose.ci.yml` para esto: usan una base SQL Server
  temporal y vacía, pensada para verificar el comportamiento transaccional desde cero.
- **No definir `TEST_DB_MIGRATE_ON_STARTUP=true`** contra la base real. El usuario de pruebas no tiene permisos para crear
  tablas y no debe tenerlos: si el esquema no está aplicado, las pruebas deben fallar con un error claro.

### Problemas frecuentes

| Síntoma | Causa probable |
|---|---|
| `Named Pipes Provider, error 40` o tiempo de espera al conectar | `TEST_DB_CONNECTION` no llegó a `dotnet test`. Usar el bloque de PowerShell completo, en una sola consola. |
| Error de red al conectar a `10.10.40.85` | VPN desconectada. Probar con `Test-NetConnection -ComputerName 10.10.40.85 -Port 1433`. |
| `Invalid object name '<esquema>.tbl_...'` | La tabla no existe o `Database__Esquema` apunta a otro esquema. Ver `database/README.md`. |
| Tiempo de espera solo dentro de Docker | La VPN puede excluir la red virtual de Docker. Confirmar la conexión desde dentro del contenedor. |
| `500` al abrir o costear una solicitud con ruta real, solo en Docker | El DW no completa el TLS con el cliente de Linux (ver la limitación de la Opción 2). |

## 2. Datos de prueba

La base de pruebas es compartida: la usan las pruebas automáticas, el backend desplegado y quienes prueban a mano. La regla
es que **ninguna prueba deja datos en ella**.

### Pruebas automáticas

- Toda solicitud que crea una prueba lleva la referencia `TEST-AUTO-<guid>` (`clsDatosDePrueba.NuevaReferencia`).
- Cada prueba borra lo suyo en un `finally` (`clsDatosDePrueba.Eliminar`): la solicitud, sus líneas y cotizaciones, y las
  rutas y configuraciones de vehículo que creó y que ya no usa ninguna otra línea ni tarifa histórica.
- Al empezar, se barren los restos de una corrida interrumpida buscando la marca `TEST-AUTO-` (`clsDatosDePrueba.BarrerRestos`).
  Ese barrido solo borra rutas y vehículos nacidos con la solicitud marcada, nunca registros del catálogo que ya existían.
- Al cerrar la corrida, `clsApiFixture` borra las rutas y configuraciones de vehículo creadas durante ella que nada usa. La
  aplicación solo crea una ruta o un vehículo junto con la línea que lo usa, así que uno sin uso es un residuo de prueba.
- El borrado es de mejor esfuerzo: un bloqueo ajeno no debe tumbar una prueba que ya pasó.
- Las pruebas que dependen del SICE-TAC real usan `clsHechoConRutaSiceReal`, que las salta (diciendo por qué) si el entorno no
  tiene esa ruta en el período vigente. No afirman valores fijos, porque la tarifa cambia cada mes.

### Pruebas manuales contra el backend desplegado

El backend desplegado escribe en la misma base. Una solicitud creada a mano para probar debe llevar una marca reconocible en
la referencia (por ejemplo `TEST-QA-<tema>`) y borrarse al terminar con este script transaccional. Borra por la marca, en
orden de dependencias, y solo las rutas y vehículos nacidos para esas pruebas que ninguna otra línea use. Cambiar `[db_NEXO]`
por el esquema del ambiente.

```sql
-- Cambiar @marca por la que se haya usado y @desde por la fecha de las pruebas (protege rutas y vehículos anteriores).
DECLARE @marca nvarchar(60) = N'%TEST-QA-IA%';
DECLARE @desde datetimeoffset = '2026-10-21';

BEGIN TRAN;

SELECT Id INTO #sol FROM [db_NEXO].[tbl_solicitudes] WHERE CondicionesServicio LIKE @marca;
SELECT DISTINCT RutaId, ConfigVehiculoId INTO #usos FROM [db_NEXO].[tbl_lineas_solicitud] WHERE SolicitudId IN (SELECT Id FROM #sol);

DELETE FROM [db_NEXO].[tbl_tarifas_historicas]
 WHERE CotizacionId IN (SELECT Id FROM [db_NEXO].[tbl_cotizaciones] WHERE SolicitudId IN (SELECT Id FROM #sol));
DELETE FROM [db_NEXO].[tbl_rechazos_cubicacion_linea]
 WHERE LineaSolicitudId IN (SELECT Id FROM [db_NEXO].[tbl_lineas_solicitud] WHERE SolicitudId IN (SELECT Id FROM #sol));
DELETE FROM [db_NEXO].[tbl_cotizaciones] WHERE SolicitudId IN (SELECT Id FROM #sol);   -- sus líneas y conexos se borran en cascada
DELETE FROM [db_NEXO].[tbl_lineas_solicitud] WHERE SolicitudId IN (SELECT Id FROM #sol);
DELETE FROM [db_NEXO].[tbl_solicitudes] WHERE Id IN (SELECT Id FROM #sol);

DELETE r FROM [db_NEXO].[tbl_rutas] r
 WHERE r.Id IN (SELECT RutaId FROM #usos) AND r.CreatedAt >= @desde
   AND NOT EXISTS (SELECT 1 FROM [db_NEXO].[tbl_lineas_solicitud] l WHERE l.RutaId = r.Id)
   AND NOT EXISTS (SELECT 1 FROM [db_NEXO].[tbl_tarifas_historicas] h WHERE h.RutaId = r.Id);
DELETE v FROM [db_NEXO].[tbl_configuraciones_vehiculo] v
 WHERE v.Id IN (SELECT ConfigVehiculoId FROM #usos WHERE ConfigVehiculoId IS NOT NULL) AND v.CreatedAt >= @desde
   AND NOT EXISTS (SELECT 1 FROM [db_NEXO].[tbl_lineas_solicitud] l WHERE l.ConfigVehiculoId = v.Id)
   AND NOT EXISTS (SELECT 1 FROM [db_NEXO].[tbl_tarifas_historicas] h WHERE h.ConfigVehiculoId = v.Id);

-- Residuos de corridas cortadas: rutas y vehículos creados desde @desde que ninguna línea ni tarifa histórica usa.
DELETE v FROM [db_NEXO].[tbl_configuraciones_vehiculo] v
 WHERE v.CreatedAt >= @desde
   AND NOT EXISTS (SELECT 1 FROM [db_NEXO].[tbl_lineas_solicitud] l WHERE l.ConfigVehiculoId = v.Id)
   AND NOT EXISTS (SELECT 1 FROM [db_NEXO].[tbl_tarifas_historicas] h WHERE h.ConfigVehiculoId = v.Id);
DELETE r FROM [db_NEXO].[tbl_rutas] r
 WHERE r.CreatedAt >= @desde
   AND NOT EXISTS (SELECT 1 FROM [db_NEXO].[tbl_lineas_solicitud] l WHERE l.RutaId = r.Id)
   AND NOT EXISTS (SELECT 1 FROM [db_NEXO].[tbl_tarifas_historicas] h WHERE h.RutaId = r.Id);

DROP TABLE #usos; DROP TABLE #sol;
COMMIT;
```

Antes de ejecutarlo, cambiar `DELETE` por `SELECT` para ver qué se llevaría, o correrlo primero sin el `COMMIT` final.
