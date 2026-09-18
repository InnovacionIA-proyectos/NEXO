-- Verificación QA de S4-HU-06.3 (issue #174): confirmar que el fix de #173 elimina el
-- empate masivo en score 50/100 al recalcular con los 4 pesos.
--
-- Estado medido ANTES del fix (dado por el issue): de 387 solicitudes, 337 empatadas en
-- score exactamente 50 y 45 en exactamente 100 -- la firma de un score que en realidad
-- solo era un bucket discreto de urgencia (deadline lejano/medio/vencido). Oportunidades
-- de $9.000.000 y $3.900.000 con el mismo puntaje.
--
-- Esta consulta reproduce en SQL la fórmula de ScorePrioridadCalculator.Calcular
-- (src/CabinaFletes.Domain/Priorizacion/Reglas/ScorePrioridadCalculator.cs) contra las
-- solicitudes reales, usando los parámetros vigentes en parametros_score (Id=1). Solo
-- devuelve AGREGADOS (conteos por score, no filas por cliente) para minimizar los datos
-- expuestos -- no hace falta ver solicitud por solicitud para confirmar que el empate
-- masivo desapareció.
--
-- No se pudo correr desde este entorno de trabajo: no hay conectividad hacia la base de
-- datos de pruebas (dbInformesAutomatizados_Pruebas o la que contenga las 387 solicitudes)
-- desde este sandbox. Correr manualmente contra esa base y pegar el resultado de las 3
-- consultas de abajo en docs/decisiones/score-prioridad-4-pesos-vs-solo-urgencia.md.
--
-- Ejemplo de invocación (ajustar servidor/BD):
--   sqlcmd -S <servidor> -d dbInformesAutomatizados_Pruebas -E -i verify-score-4-pesos.sql

WITH parametros AS (
    SELECT TOP 1
        PesoUrgencia, PesoValor, PesoProbabilidad, PesoTipo,
        HorizonteHoras, ValorReferencia, PuntajeLicitacion, PuntajeSpot,
        ValorPorDefectoSinValor, ProbabilidadPorDefecto
    FROM parametros_score
    ORDER BY Id
),
componentes AS (
    SELECT
        s.Id,
        s.ValorEstimado,
        s.ScorePrioridad AS ScoreEnBD_ViejaFormula,
        CASE
            WHEN s.Deadline IS NULL THEN 50.0
            WHEN pr.HorizonteHoras <= 0 THEN 100.0
            WHEN DATEDIFF(MINUTE, SYSDATETIMEOFFSET(), s.Deadline) <= 0 THEN 100.0
            WHEN DATEDIFF(MINUTE, SYSDATETIMEOFFSET(), s.Deadline) / 60.0 >= pr.HorizonteHoras THEN 0.0
            ELSE 100.0 * (1.0 - (DATEDIFF(MINUTE, SYSDATETIMEOFFSET(), s.Deadline) / 60.0) / pr.HorizonteHoras)
        END AS Urgencia,
        CASE
            WHEN s.ValorEstimado IS NULL OR s.ValorEstimado <= 0 THEN pr.ValorPorDefectoSinValor
            WHEN pr.ValorReferencia <= 0 THEN 100.0
            ELSE 100.0 * LOG(1 + CAST(s.ValorEstimado AS FLOAT)) / LOG(1 + CAST(pr.ValorReferencia AS FLOAT))
        END AS Valor,
        CASE
            WHEN s.ProbabilidadCierre IS NULL THEN pr.ProbabilidadPorDefecto
            WHEN s.ProbabilidadCierre <= 1.0 THEN CAST(s.ProbabilidadCierre AS FLOAT) * 100.0
            ELSE CAST(s.ProbabilidadCierre AS FLOAT)
        END AS Probabilidad,
        CASE WHEN s.Tipo = 'Licitacion' THEN pr.PuntajeLicitacion ELSE pr.PuntajeSpot END AS Tipo,
        pr.PesoUrgencia, pr.PesoValor, pr.PesoProbabilidad, pr.PesoTipo
    FROM solicitudes s
    CROSS JOIN parametros pr
),
resultado AS (
    SELECT
        Id, ValorEstimado, ScoreEnBD_ViejaFormula,
        ROUND(PesoUrgencia * Urgencia + PesoValor * Valor + PesoProbabilidad * Probabilidad + PesoTipo * Tipo, 2) AS ScoreNuevo_4Pesos
    FROM componentes
)

-- 1) Conteo por score con la fórmula VIEJA (solo urgencia) tal como quedó persistida --
--    debería reproducir aprox. el 337 en 50 / 45 en 100 reportado en el issue.
SELECT ScoreEnBD_ViejaFormula AS Score, COUNT(*) AS Cantidad
FROM resultado
GROUP BY ScoreEnBD_ViejaFormula
ORDER BY Cantidad DESC;

-- 2) Conteo por score con la fórmula NUEVA (4 pesos) -- el criterio de aceptación de
--    #174 es que ya no haya un empate masivo comparable en un único valor.
SELECT ScoreNuevo_4Pesos AS Score, COUNT(*) AS Cantidad
FROM resultado
GROUP BY ScoreNuevo_4Pesos
ORDER BY Cantidad DESC;

-- 3) Resumen: total de solicitudes, score más repetido y qué porcentaje representa
--    (antes: ~87% del total -- 337/387 -- concentrado en un solo valor).
SELECT
    (SELECT COUNT(*) FROM resultado) AS TotalSolicitudes,
    (SELECT TOP 1 COUNT(*) FROM resultado GROUP BY ScoreNuevo_4Pesos ORDER BY COUNT(*) DESC) AS MaxEmpateEnUnScore,
    CAST((SELECT TOP 1 COUNT(*) FROM resultado GROUP BY ScoreNuevo_4Pesos ORDER BY COUNT(*) DESC) * 100.0
         / NULLIF((SELECT COUNT(*) FROM resultado), 0) AS DECIMAL(5,2)) AS PorcentajeDelMaxEmpate;

-- 4) Verificación puntual: las dos oportunidades de referencia del issue ($9.000.000 y
--    $3.900.000) ya no deben compartir score. Ajustar los valores si no calzan exacto.
SELECT ValorEstimado, ScoreEnBD_ViejaFormula, ScoreNuevo_4Pesos
FROM resultado
WHERE ValorEstimado IN (9000000, 3900000)
ORDER BY ValorEstimado;
