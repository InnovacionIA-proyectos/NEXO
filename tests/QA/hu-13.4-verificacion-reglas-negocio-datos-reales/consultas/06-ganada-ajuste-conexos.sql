-- Base de datos: CabinaFletes (la app)
-- Reglas 5, 6 y 7 — todas viven solo en esta base, no requieren SAP/SICE-TAC.

-- ============================================================
-- Regla 5: "Ganada" archiva la tarifa (una por línea, sin duplicados)
-- ============================================================
SELECT
    c.Id AS CotizacionId, c.Resultado, c.EntregadaAt, c.CreatedAt,
    (SELECT COUNT(*) FROM dbo.cotizacion_lineas cl WHERE cl.CotizacionId = c.Id) AS NumLineas,
    (SELECT COUNT(*) FROM dbo.tarifas_historicas th WHERE th.CotizacionId = c.Id) AS NumTarifasArchivadas
FROM dbo.cotizaciones c
WHERE c.Resultado = 'Ganada'
ORDER BY c.UpdatedAt DESC;
-- Esperado: NumTarifasArchivadas = NumLineas para cada fila. Cualquier fila
-- donde no calce 1:1 (de más o de menos) es una desviación candidata.

-- ============================================================
-- Regla 6: ajuste de vigencia de plaza auditado con autoría real
-- ============================================================
SELECT Id, TarifaPlazaId, VigenciaOriginal, VigenciaAjustada, Motivo, AjustadoPor, CreatedAt
FROM dbo.ajustes_vigencia_plaza
ORDER BY CreatedAt DESC;
-- Esperado: AjustadoPor tiene un Id de usuario real (no NULL). Si la tabla
-- está vacía, esta regla queda "no alcanzada" en el ambiente real todavía
-- (no hay ningún ajuste manual hecho aún) — se documenta así, no se fuerza
-- un caso ficticio.

-- Verificación cruzada de auditoría genérica (si AjustadoPor sí trae datos):
SELECT TOP 20 *
FROM dbo.audit_log
WHERE Entidad = 'tarifas_plaza_vigencia'
ORDER BY Id DESC;

-- ============================================================
-- Regla 7: el Total se recalcula al agregar un conexo
-- ============================================================
SELECT
    c.Id AS CotizacionId, c.Total AS TotalActual, c.CreatedAt AS CotizacionCreada,
    cc.Id AS CotizacionConexoId, cc.ValorAplicado, cc.CreatedAt AS ConexoAgregado
FROM dbo.cotizaciones c
JOIN dbo.cotizacion_conexos cc ON cc.CotizacionId = c.Id
WHERE cc.CreatedAt > c.CreatedAt
ORDER BY cc.CreatedAt DESC;
-- Para 2-3 filas de este resultado, pedir al equipo (o revisar el AuditLog
-- si registra el Total anterior) el Total que tenía la cotización ANTES de
-- agregar el conexo, para comparar contra c.Total (el actual) y confirmar
-- que el conexo sí quedó reflejado. Si no hay forma de ver el Total anterior
-- (no hay versión/historial de Total), documentarlo como limitación de esta
-- verificación puntual, no como una desviación de la regla.
