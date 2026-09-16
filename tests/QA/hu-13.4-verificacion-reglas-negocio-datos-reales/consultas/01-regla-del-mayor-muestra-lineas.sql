-- Base de datos: CabinaFletes (la app)
-- Regla 1 (regla del mayor) + Regla 2 (piso inviolable)
--
-- Objetivo: sacar una muestra real de líneas ya costeadas (Fuente = Sicetac o
-- Tarifario) con su ruta, vehículo, peso y fecha. Con estos resultados, tomar
-- 3-5 filas y buscar a mano (consultas 02 y 03) el candidato SAP y el piso
-- SICE-TAC vigentes a esa fecha, para verificar que ValorFlete = MAX(los dos)
-- y que ninguna fila del bloque de abajo queda por debajo de su piso.

SELECT TOP 30
    cl.Id                   AS CotizacionLineaId,
    c.Id                    AS CotizacionId,
    c.Resultado             AS ResultadoCotizacion,
    r.Origen, r.Destino,
    ls.ConfigVehiculoId,
    ls.Peso,
    ls.LineaNegocio, ls.ClaseViaje, ls.ClaseContenedor,
    cl.ValorFlete,
    cl.Fuente,
    cl.CreatedAt            AS FechaLinea
FROM dbo.cotizacion_lineas cl
JOIN dbo.cotizaciones c      ON c.Id = cl.CotizacionId
JOIN dbo.lineas_solicitud ls ON ls.Id = cl.LineaSolicitudId
JOIN dbo.rutas r             ON r.Id = ls.RutaId
WHERE cl.Fuente IN ('Sicetac', 'Tarifario')
  AND cl.ValorFlete IS NOT NULL
ORDER BY cl.CreatedAt DESC;

-- Invariante de piso (regla 2) sobre TODO el universo accesible, no solo la
-- muestra chica: esto se termina de verificar cruzando cada fila contra su
-- piso SICE-TAC real (consulta 03) — no hay forma de hacerlo en una sola
-- consulta porque el piso vive en otra base de datos (DWSice).
-- Contar cuántas filas hay en total, para saber qué tan grande es el universo:
SELECT COUNT(*) AS TotalLineasCosteadas
FROM dbo.cotizacion_lineas
WHERE Fuente IN ('Sicetac', 'Tarifario') AND ValorFlete IS NOT NULL;
