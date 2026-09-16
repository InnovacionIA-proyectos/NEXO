-- Base de datos: dbsolucionesTI
-- Diagnóstico rápido de los 2 problemas encontrados en paso1_resultado.md.

-- A) ¿Qué formato tiene IDZONATRANSPORTE de verdad? (para saber cómo cruzarlo
-- contra CODIGOCENTROPOBLADO — probablemente hay que cortar a 5 dígitos)
SELECT TOP 20 * FROM dbo.tblZonasTarificacionRelacion;

-- B) Las 5 ciudades que no aparecieron en el paso 1 — buscarlas con LIKE en
-- vez de igualdad exacta, para ver cómo está guardado su nombre oficial real.
SELECT CODIGOCENTROPOBLADO, NOMBREMUNICIPIO, NOMBREDEPARTAMENTO, NOMBREMOSTRAR
FROM dbo.tblsapdane
WHERE NOMBREMUNICIPIO LIKE '%BOGOTA%'
   OR NOMBREMUNICIPIO LIKE '%MEDELLIN%'
   OR NOMBREMUNICIPIO LIKE '%CARTAGENA%'
   OR NOMBREMUNICIPIO LIKE '%CUCUTA%'
   OR NOMBREMUNICIPIO LIKE '%IBAGUE%'
ORDER BY NOMBREMUNICIPIO;

-- C) El catálogo completo de tipos de vehículo (es chico, mejor verlo entero
-- que seguir adivinando LIKE — así vemos de una vez si "Tractomula 3S2" y
-- "Camioneta" existen bajo otro nombre/código).
SELECT ID, CODIGO, DESCRIPCION, ESTADO FROM dbo.tblSAPTiposVehiculoTarifario;
