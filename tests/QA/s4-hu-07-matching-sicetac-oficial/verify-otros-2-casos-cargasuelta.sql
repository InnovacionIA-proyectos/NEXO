-- Verificación QA de S4-HU-07.1 (issue #175): al menos 2 casos ADICIONALES a
-- Medellín-Guarne que fallan por el MISMO motivo estructural (no por diferencias de
-- texto en la ruta), sino porque TarifarioSapAdapter.SicetacOficialAsync tiene un gate
-- "aplica" (TarifarioSapAdapter.cs:358-361) que SOLO deja pasar líneas con
-- ClaseViaje = Contenedor o Cupo. Cualquier línea con ClaseViaje = CargaSuelta o
-- Extradimensionado nunca obtiene un piso SICE-TAC oficial, sin importar si la ruta
-- existe y tiene tarifa vigente -- el código ni siquiera llega a construir la consulta.
--
-- Correr contra la base propia de la app (solo lectura):
--   sqlcmd -S <servidor> -d dbInformesAutomatizados_Pruebas -E -i verify-otros-2-casos-cargasuelta.sql

USE dbInformesAutomatizados_Pruebas;
GO

SELECT TOP 10
    l.Id AS LineaId,
    r.Origen, r.Destino,
    l.ClaseViaje, l.LineaNegocio, l.Tipologia, l.Peso,
    l.EstadoLinea
FROM dbo_NEXO_prueba.lineas_solicitud l
JOIN dbo_NEXO_prueba.rutas r ON r.Id = l.RutaId
WHERE l.ClaseViaje IN ('CargaSuelta', 'Extradimensionado')
ORDER BY l.CreatedAt DESC;

-- Cuántas líneas en total caen en esta categoría (candidatas a "nunca tuvieron piso
-- automático posible", más allá de las que además fallan por texto de ruta):
SELECT l.ClaseViaje, COUNT(*) AS Cantidad
FROM dbo_NEXO_prueba.lineas_solicitud l
WHERE l.ClaseViaje IN ('CargaSuelta', 'Extradimensionado')
GROUP BY l.ClaseViaje;
