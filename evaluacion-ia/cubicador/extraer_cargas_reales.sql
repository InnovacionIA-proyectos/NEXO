-- =============================================================================
-- Cargas reales para el dataset dorado del Cubicador — SOLO LECTURA
-- =============================================================================
-- Qué es: un SELECT. No crea, modifica ni borra nada; se puede correr contra la base real.
-- Qué devuelve: una fila por POSICIÓN de carga de cada línea de solicitud que ya tiene vehículo
-- elegido por una persona, con las medidas y el peso de la mercancía y el vehículo que se eligió.
-- Con ese resultado `importar_cargas_reales.py` arma los casos que un experto de operaciones
-- revisa antes de que cuenten como "decisión humana correcta".
--
-- Privacidad: sale solo lo que el motor necesita (código de mercancía, cantidades, medidas, peso,
-- vehículo). NO sale cliente, ruta, descripción de la mercancía ni ningún texto libre, y los
-- identificadores van como un resumen irreversible de 12 caracteres. El archivo que resulte NO
-- se versiona en el repositorio: se guarda en la carpeta compartida del equipo.
--
-- Una carga es lo que viaja junta: las líneas que comparten GrupoRutaId (un pedido repartido en
-- varios camiones) son UNA carga; una línea sin grupo es una carga por sí sola.
--
-- Reemplazar db_NEXO por el valor de Database__Esquema del ambiente si es otro.
-- Cambiar @sal por cualquier texto: solo evita que el resumen del identificador sea reproducible fuera.
-- =============================================================================

DECLARE @sal nvarchar(50) = N'cubicador-ia-2026';

SELECT
    CONVERT(char(12), HASHBYTES('SHA2_256', CONCAT(@sal, CONVERT(nvarchar(36), COALESCE(l.GrupoRutaId, l.Id)))), 2) AS CargaId,
    l.NumeroLinea,
    l.ClaseViaje,
    cv.TipoVehiculo   AS VehiculoElegido,
    cv.Carroceria     AS CarroceriaElegida,
    -- 1 = la línea trae una recomendación del Cubicador (la persona pudo haberla aceptado tal cual);
    -- 0 = la persona eligió sin que el motor opinara: esas etiquetas no están sesgadas hacia el motor.
    CASE WHEN l.IdRecomendacionVehiculo IS NULL THEN 0 ELSE 1 END AS UsoRecomendacion,
    p.CodigoTipoMercancia,
    p.Cantidad,
    p.LargoCm,
    p.AnchoCm,
    p.AltoCm,
    p.PesoKg
FROM [db_NEXO].[tbl_lineas_solicitud] AS l
INNER JOIN [db_NEXO].[tbl_configuraciones_vehiculo] AS cv ON cv.Id = l.ConfigVehiculoId
CROSS APPLY OPENJSON(l.Dimensiones) WITH (
    CodigoTipoMercancia nvarchar(50)  '$.codigoTipoMercancia',
    Cantidad            int           '$.cantidad',
    LargoCm             decimal(18,2) '$.largoCm',
    AnchoCm             decimal(18,2) '$.anchoCm',
    AltoCm              decimal(18,2) '$.altoCm',
    PesoKg              decimal(18,2) '$.pesoKg'
) AS p
WHERE l.Dimensiones IS NOT NULL
  AND ISJSON(l.Dimensiones) = 1
  AND l.EstadoLinea <> N'Rechazada'
  AND p.CodigoTipoMercancia IS NOT NULL
  AND p.Cantidad > 0 AND p.LargoCm > 0 AND p.AnchoCm > 0 AND p.AltoCm > 0 AND p.PesoKg > 0
ORDER BY CargaId, l.NumeroLinea;
