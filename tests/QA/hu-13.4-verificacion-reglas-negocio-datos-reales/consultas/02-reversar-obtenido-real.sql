-- ============================================================
-- Base de datos: DWSice
--
-- El cálculo manual del piso SICE-TAC esperado (basado en el vehículo y
-- tipología declarados en cada línea) no coincidió con los valores del PDF
-- de cotización real. Para identificar el mecanismo real, se hizo una
-- búsqueda inversa por valor exacto en tblRutasSicetac: dado el valor que
-- realmente salió en el PDF, se ubica la fila que lo produce y se lee su
-- combinación de columnas (CONFIGURACION, NOMBRETIPOCARGA, CONDICIONCARGA,
-- VIAESTANDAR).
--
-- Resultado: las 4 líneas probadas coincidieron en CONFIGURACION='CA' y
-- NOMBRETIPOCARGA='General', con independencia del vehículo/tipología real
-- de cada línea. Esto llevó a confirmar el mecanismo completo leyendo
-- TarifarioSapAdapter.SicetacOficialAsync — ver ../esperado-final-pliego.md
-- sección 4 para el detalle del método real. La confirmación para las 11
-- líneas restantes queda en 03-confirmar-config-ca-todas-las-lineas.sql.
-- ============================================================

-- Línea 1: Buenaventura -> Bogotá, valor obtenido en el PDF: $1.556.503
SELECT * FROM dbo.tblRutasSicetac WHERE ABS(VALORSICECONHORA - 1556503) < 1;

-- Línea 5: Cali -> Cartagena, valor obtenido en el PDF: $3.118.351
SELECT * FROM dbo.tblRutasSicetac WHERE ABS(VALORSICECONHORA - 3118351) < 1;

-- Línea 6: Bucaramanga -> Bogotá, valor obtenido en el PDF: $1.271.020
SELECT * FROM dbo.tblRutasSicetac WHERE ABS(VALORSICECONHORA - 1271020) < 1;

-- Línea 9: Bogotá -> Villavicencio, valor obtenido en el PDF: $867.043
-- Caso relevante: una verificación previa, sin restringir por CONFIGURACION,
-- no había encontrado ninguna fila para esta ruta en el período vigente —
-- de ahí la necesidad de la búsqueda por valor exacto en toda la tabla.
SELECT * FROM dbo.tblRutasSicetac WHERE ABS(VALORSICECONHORA - 867043) < 1;
