-- ============================================================
-- Base de datos: DWSice
--
-- Pendiente de ejecución por el usuario (vía DBeaver, conexión
-- "consultascubos" — DWSice no es accesible directamente en esta sesión).
--
-- Objetivo: confirmar el valor exacto del piso SICE-TAC (CONFIGURACION='CA')
-- para la línea 15 del pliego (Neiva -> Bogotá). El valor $1.104.014 citado
-- en versiones anteriores del informe no quedó trazado con certeza a esta
-- consulta específica y pudo haberse confundido con un valor de la hipótesis
-- descartada de código de vehículo '2'. Hasta que esta consulta se ejecute
-- y se confirme el resultado, el valor de la línea 15 se documenta como
-- "pendiente de reconfirmar" en README.md y esperado-final-pliego.md.
-- ============================================================

SELECT 15 AS Linea, NOMBRERUTA, CONFIGURACION, NOMBRETIPOCARGA, VALORSICECONHORA, VIAESTANDAR
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%Neiva%' AND NOMBRERUTA LIKE '%Bogot%'
  AND CONFIGURACION = 'CA' AND NOMBRETIPOCARGA = 'General'
  AND CONDICIONCARGA = 'CARGADO' AND VIAESTANDAR = 'SI'
  AND PERIODO = 202609;
