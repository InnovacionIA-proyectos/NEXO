-- Verificación QA de S4-HU-07.1 (issue #175): confirmar con datos reales por qué el
-- camino "normal" de comparación (TarifarioSapAdapter.SicetacOficialAsync) no
-- encuentra la ruta Medellín-Guarne, mientras "Solo SICE-TAC"
-- (PropuestaTarifaAdapter.CandidatoSicetacAsync, selector tolerante) sí la encuentra
-- al instante.
--
-- Correr contra DWSice (piso SICE-TAC), solo lectura:
--   sqlcmd -S <servidor DWSice> -d DWSice -E -i verify-medellin-guarne.sql
--
-- Diagnóstico de código (sin acceso a esta base desde el sandbox de desarrollo):
-- SicetacOficialAsync (TarifarioSapAdapter.cs:384-393) exige TODAS estas igualdades
-- exactas a la vez, sin fallback si falla cualquiera:
--   CONDICIONCARGA = 'CARGADO'
--   NOMBRETIPOCARGA = <parámetro global SAP 457, normalmente 'General'>
--   NOMBREUNIDADTRANSPORTE = <parámetro global SAP 458, normalmente 'ESTACAS', o 'FURGON'>
--   CONFIGURACION = <config derivada del vehículo o del rango de peso>
--   VIAESTANDAR = 'SI'   (filtro duro, no solo preferencia)
-- mientras que CandidatoSicetacAsync/SeleccionarSice (PropuestaTarifaAdapter.cs:350-377)
-- solo exige CONDICIONCARGA + que el NOMBRERUTA calce, y trata Configuracion/TipoCarga/
-- ViaEstandar como preferencias con fallback, nunca como filtro que descarta la fila.
--
-- Esta consulta muestra TODAS las filas de Medellín-Guarne (en cualquier dirección) y
-- cuáles de los 5 criterios estrictos cumple cada una, para identificar cuál es el que
-- la descarta en el camino normal.

DECLARE @periodo INT = (SELECT MAX(PERIODO) FROM dbo.tblRutasSicetac WHERE PERIODO > 0);
PRINT 'Periodo vigente: ' + CAST(@periodo AS VARCHAR);

SELECT
    ID, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, NOMBREUNIDADTRANSPORTE,
    CONDICIONCARGA, VIAESTANDAR, VALOR, VALORSICECONHORA,
    -- Marca cuáles de los 5 criterios estrictos de SicetacOficialAsync cumple esta fila
    -- (ajustar 'General'/'ESTACAS' si los parámetros SAP 457/458 tienen otro valor en
    -- este ambiente -- confirmar con SELECT * FROM dbSolucionesTI.dbo.tblParametros
    -- WHERE IdParametro IN (457, 458)):
    CASE WHEN CONDICIONCARGA = 'CARGADO' THEN 'OK' ELSE 'FALLA' END AS Chk_CondicionCarga,
    CASE WHEN NOMBRETIPOCARGA = 'General' THEN 'OK' ELSE 'FALLA (' + NOMBRETIPOCARGA + ')' END AS Chk_TipoCarga,
    CASE WHEN NOMBREUNIDADTRANSPORTE IN ('ESTACAS', 'FURGON') THEN 'OK' ELSE 'FALLA (' + NOMBREUNIDADTRANSPORTE + ')' END AS Chk_UnidadTransporte,
    CASE WHEN VIAESTANDAR = 'SI' THEN 'OK' ELSE 'FALLA (' + ISNULL(VIAESTANDAR, 'NULL') + ')' END AS Chk_ViaEstandar
FROM dbo.tblRutasSicetac
WHERE PERIODO = @periodo
  AND (
    (NOMBRERUTA LIKE '%MEDELLIN%-%GUARNE%') OR (NOMBRERUTA LIKE '%GUARNE%-%MEDELLIN%')
  )
ORDER BY VALOR DESC;

-- Si esta consulta no trae filas, revisar sin filtrar PERIODO (puede que el período
-- vigente para esta ruta específica sea otro) y/o buscar por ORIGEN/DESTINO en vez de
-- NOMBRERUTA:
-- SELECT TOP 20 * FROM dbo.tblRutasSicetac WHERE ORIGEN LIKE '%MEDELLIN%' AND DESTINO LIKE '%GUARNE%' ORDER BY PERIODO DESC;
