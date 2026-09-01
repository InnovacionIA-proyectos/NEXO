-- Verificación QA de analitica.inconsistencias (HU-10.1, issue #35, PR #82).
--
-- Inserta una fila LIMPIA (no debe aparecer en el resultado) y una fila por cada una
-- de las 9 reglas documentadas en la migración VistasAnaliticaSoloLectura, luego
-- consulta analitica.inconsistencias y confirma que solo las filas malas aparecen,
-- cada una con el Motivo exacto que le corresponde.
--
-- IDs fijos (prefijo AAAA=limpio, BBBB=inconsistente) para poder identificar y
-- limpiar todo después sin tocar nada del seed. Reemplazar los 4 IDs de abajo
-- (@cliente/@ruta/@vehiculo/@usuario) por IDs reales de tu propio ambiente antes de
-- correr (ver README de esta carpeta para cómo obtenerlos).
--
-- Requiere: app levantada con migraciones aplicadas (docker compose up -d --build --wait).
-- Correr contra la BD: docker exec -i nexo-db-1 /opt/mssql-tools18/bin/sqlcmd -C -S localhost
--   -U sa -P '<password>' -d CabinaFletes -i verify-inconsistencias.sql
--
-- AL TERMINAR: correr la sección "LIMPIEZA" al final de este archivo (comentada a
-- propósito, para no borrar nada por accidente si se corre este script sin querer).
DECLARE @cliente UNIQUEIDENTIFIER = '917DE804-AC9B-43BF-A608-DC4BBABC5D21';
DECLARE @ruta UNIQUEIDENTIFIER = '56773D80-C0C2-4A5E-825E-303FC31C2A68';
DECLARE @vehiculo UNIQUEIDENTIFIER = 'F2791DDF-07B2-4D81-9180-12E26F11443A';
DECLARE @usuario UNIQUEIDENTIFIER = 'AA79B75A-7325-4ED3-95EA-019B45B9D819';

DECLARE @sol_clean UNIQUEIDENTIFIER = 'AAAA0001-0000-0000-0000-000000000001';
DECLARE @cot_clean UNIQUEIDENTIFIER = 'AAAA0001-0000-0000-0000-000000000002';
DECLARE @lin_clean UNIQUEIDENTIFIER = 'AAAA0001-0000-0000-0000-000000000003';
DECLARE @cl_clean  UNIQUEIDENTIFIER = 'AAAA0001-0000-0000-0000-000000000004';
DECLARE @th_clean  UNIQUEIDENTIFIER = 'AAAA0001-0000-0000-0000-000000000005';
DECLARE @au_clean  UNIQUEIDENTIFIER = 'AAAA0001-0000-0000-0000-000000000006';

DECLARE @cot_bad1 UNIQUEIDENTIFIER = 'BBBB0001-0000-0000-0000-000000000001'; -- entregada sin fecha/entregadoPor
DECLARE @cot_bad2 UNIQUEIDENTIFIER = 'BBBB0001-0000-0000-0000-000000000002'; -- resultado sin entregar
DECLARE @cot_bad3 UNIQUEIDENTIFIER = 'BBBB0001-0000-0000-0000-000000000003'; -- entregada sin lineas
DECLARE @sol_bad UNIQUEIDENTIFIER  = 'BBBB0001-0000-0000-0000-000000000004'; -- deadline antes de creacion
DECLARE @lin_bad1 UNIQUEIDENTIFIER = 'BBBB0001-0000-0000-0000-000000000005'; -- sin ruta
DECLARE @lin_bad2 UNIQUEIDENTIFIER = 'BBBB0001-0000-0000-0000-000000000006'; -- peso 0
DECLARE @cl_bad   UNIQUEIDENTIFIER = 'BBBB0001-0000-0000-0000-000000000007'; -- valorflete 0
DECLARE @th_bad1  UNIQUEIDENTIFIER = 'BBBB0001-0000-0000-0000-000000000008'; -- cotizacion no ganada
DECLARE @th_bad2  UNIQUEIDENTIFIER = 'BBBB0001-0000-0000-0000-000000000009'; -- valor negativo
DECLARE @au_bad   UNIQUEIDENTIFIER = 'BBBB0001-0000-0000-0000-000000000010'; -- usuarioid nulo

-- ============================== BASELINE LIMPIO (no debe aparecer en inconsistencias) ==============================
INSERT INTO solicitudes (Id, ClienteId, Tipo, CreadoPor, Estado, Deadline, CreatedAt, UpdatedAt)
VALUES (@sol_clean, @cliente, 'Spot', @usuario, 'Entregada', DATEADD(day, 10, SYSDATETIMEOFFSET()), SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

INSERT INTO cotizaciones (Id, SolicitudId, Estado, Resultado, Total, EntregadaPor, EntregadaAt, CreatedAt, UpdatedAt)
VALUES (@cot_clean, @sol_clean, 'Entregada', 'Ganada', 500000, @usuario, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

INSERT INTO lineas_solicitud (Id, SolicitudId, RutaId, Peso, EstadoLinea, CreatedAt, UpdatedAt)
VALUES (@lin_clean, @sol_clean, @ruta, 1000, 'AutoResuelta', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

INSERT INTO cotizacion_lineas (Id, CotizacionId, LineaSolicitudId, ValorFlete, Fuente, CreatedAt, UpdatedAt)
VALUES (@cl_clean, @cot_clean, @lin_clean, 500000, 'Sicetac', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

INSERT INTO tarifas_historicas (Id, RutaId, ConfigVehiculoId, Valor, Fecha, CotizacionId, CreatedAt, UpdatedAt)
VALUES (@th_clean, @ruta, @vehiculo, 500000, CAST(GETDATE() AS DATE), @cot_clean, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

INSERT INTO audit_log (Id, UsuarioId, Entidad, EntidadId, Accion, CreatedAt)
VALUES (@au_clean, @usuario, 'test_qa', @sol_clean, 'crear', SYSDATETIMEOFFSET());

-- ============================== 9 CASOS DE INCONSISTENCIA (deben aparecer, uno por uno) ==============================

-- 1) cotizaciones: Entregada sin EntregadaAt o EntregadaPor
INSERT INTO cotizaciones (Id, SolicitudId, Estado, Resultado, Total, EntregadaPor, EntregadaAt, CreatedAt, UpdatedAt)
VALUES (@cot_bad1, @sol_clean, 'Entregada', 'SinDefinir', 100000, NULL, NULL, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

-- 2) cotizaciones: Resultado definido sin haber sido entregada
INSERT INTO cotizaciones (Id, SolicitudId, Estado, Resultado, Total, EntregadaPor, EntregadaAt, CreatedAt, UpdatedAt)
VALUES (@cot_bad2, @sol_clean, 'Borrador', 'Perdida', 100000, NULL, NULL, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

-- 3) cotizaciones: Entregada sin ninguna linea de flete
INSERT INTO cotizaciones (Id, SolicitudId, Estado, Resultado, Total, EntregadaPor, EntregadaAt, CreatedAt, UpdatedAt)
VALUES (@cot_bad3, @sol_clean, 'Entregada', 'SinDefinir', 0, @usuario, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

-- 4) cotizacion_lineas: ValorFlete en cero o negativo
INSERT INTO cotizacion_lineas (Id, CotizacionId, LineaSolicitudId, ValorFlete, Fuente, CreatedAt, UpdatedAt)
VALUES (@cl_bad, @cot_clean, @lin_clean, 0, 'Manual', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

-- 5) tarifas_historicas: referencia una cotizacion que no quedo Ganada (cot_bad1 tiene Resultado SinDefinir)
INSERT INTO tarifas_historicas (Id, RutaId, ConfigVehiculoId, Valor, Fecha, CotizacionId, CreatedAt, UpdatedAt)
VALUES (@th_bad1, @ruta, @vehiculo, 100000, CAST(GETDATE() AS DATE), @cot_bad1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

-- 6) tarifas_historicas: Valor en cero o negativo (asociada a la cotizacion limpia, SI ganada -- para que no dispare la regla 5 tambien)
INSERT INTO tarifas_historicas (Id, RutaId, ConfigVehiculoId, Valor, Fecha, CotizacionId, CreatedAt, UpdatedAt)
VALUES (@th_bad2, @ruta, @vehiculo, -5000, CAST(GETDATE() AS DATE), @cot_clean, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

-- 7) lineas_solicitud: sin ruta asignada
INSERT INTO lineas_solicitud (Id, SolicitudId, RutaId, Peso, EstadoLinea, CreatedAt, UpdatedAt)
VALUES (@lin_bad1, @sol_clean, NULL, 1000, 'PorCostear', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

-- 8) lineas_solicitud: Peso nulo o en cero
INSERT INTO lineas_solicitud (Id, SolicitudId, RutaId, Peso, EstadoLinea, CreatedAt, UpdatedAt)
VALUES (@lin_bad2, @sol_clean, @ruta, 0, 'PorCostear', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

-- 9) solicitudes: Deadline anterior a la fecha de creacion
INSERT INTO solicitudes (Id, ClienteId, Tipo, CreadoPor, Estado, Deadline, CreatedAt, UpdatedAt)
VALUES (@sol_bad, @cliente, 'Spot', @usuario, 'Pendiente', DATEADD(day, -5, SYSDATETIMEOFFSET()), SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());

-- 10) audit_log: sin usuario identificado
INSERT INTO audit_log (Id, UsuarioId, Entidad, EntidadId, Accion, CreatedAt)
VALUES (@au_bad, NULL, 'test_qa', @sol_bad, 'crear', SYSDATETIMEOFFSET());

-- ============================== CONSULTA ==============================
SELECT Entidad, EntidadId, Motivo FROM analitica.inconsistencias
WHERE EntidadId IN (@sol_clean, @cot_clean, @lin_clean, @cl_clean, @th_clean, @au_clean,
                    @cot_bad1, @cot_bad2, @cot_bad3, @sol_bad, @lin_bad1, @lin_bad2, @cl_bad, @th_bad1, @th_bad2, @au_bad)
ORDER BY Entidad, EntidadId;

-- ============================== LIMPIEZA ==============================
-- Descomentar y correr esta sección al terminar de verificar el resultado de arriba.
-- Orden FK-safe: cotizacion_lineas -> tarifas_historicas -> cotizaciones -> lineas_solicitud -> audit_log -> solicitudes.
/*
DELETE FROM cotizacion_lineas WHERE Id IN (@cl_clean, @cl_bad);
DELETE FROM tarifas_historicas WHERE Id IN (@th_clean, @th_bad1, @th_bad2);
DELETE FROM cotizaciones WHERE Id IN (@cot_clean, @cot_bad1, @cot_bad2, @cot_bad3);
DELETE FROM lineas_solicitud WHERE Id IN (@lin_clean, @lin_bad1, @lin_bad2);
DELETE FROM audit_log WHERE Id IN (@au_clean, @au_bad);
DELETE FROM solicitudes WHERE Id IN (@sol_clean, @sol_bad);
*/
