# Datos de prueba: cómo se crean y cómo se limpian

La base de pruebas de NEXO es compartida: la usan las pruebas automáticas, el backend desplegado y la gente que prueba a mano. La
regla es que **ninguna prueba deja datos en ella**.

## Pruebas automáticas (`Nexo.Api.Tests`)

- Toda solicitud que crea una prueba contra la base real lleva la referencia `TEST-AUTO-<guid>` (`clsDatosDePrueba.NuevaReferencia`).
- Cada prueba borra lo suyo en un `finally` (`clsDatosDePrueba.Eliminar`): la solicitud, sus líneas y cotizaciones, y las rutas y
  configuraciones de vehículo que la prueba creó (solo las nacidas durante la prueba y que ya no usa ninguna otra línea ni tarifa
  histórica).
- Al empezar, las pruebas que usan la clase barren los restos de una ejecución interrumpida (`clsDatosDePrueba.BarrerRestos`),
  buscando la marca `TEST-AUTO-` en las solicitudes. Si una corrida se corta a la mitad, la siguiente deja la base limpia.
- El borrado es de mejor esfuerzo: un bloqueo ajeno no debe tumbar una prueba que ya pasó.
- Al cerrar la corrida, el fixture (`clsApiFixture`) borra las rutas y configuraciones de vehículo creadas durante ella que ninguna
  línea ni tarifa histórica usa. La aplicación solo crea una ruta o un vehículo junto con la línea que lo usa, así que uno creado en la
  corrida y sin uso es un residuo de prueba (varias pruebas arman su catálogo a mano y solo borran la solicitud). Si el proceso de
  pruebas se corta antes de cerrar, ese barrido no corre y los residuos se limpian con el script de abajo.
- El barrido de restos por marca solo borra rutas y vehículos nacidos junto con la solicitud marcada, nunca registros del catálogo que
  ya existían.
- Las pruebas que dependen del SICE-TAC real usan `clsHechoConRutaSiceReal`, que las salta (diciendo por qué) si el entorno no tiene
  esa ruta en el período vigente. No afirman valores fijos, porque la tarifa cambia cada mes.

## Pruebas manuales contra el backend desplegado

El backend desplegado escribe en la misma base. Las solicitudes que se creen a mano para probar deben llevar una marca reconocible en
la referencia (por ejemplo `TEST-QA-<tema>`) y borrarse al terminar. La limpieza se hace con un script transaccional que se ejecuta en
la herramienta de base de datos: borra por la marca, en orden de dependencias, y solo las rutas y vehículos nacidos para esas pruebas
que ninguna otra línea use.

```sql
-- Ejemplo con la marca TEST-QA-IA: cambiar @marca por la que se haya usado.
DECLARE @marca nvarchar(60) = N'%TEST-QA-IA%';
DECLARE @desde datetimeoffset = '2026-10-21';   -- fecha del día de las pruebas: protege rutas y vehículos anteriores

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

Antes de ejecutarlo conviene cambiar `DELETE` por `SELECT` para ver qué se llevaría, o correrlo primero sin el `COMMIT` final.
