-- Base de datos: dbSolucionesTI (solo lectura)
-- Regla 1 (regla del mayor) — lado SAP/plaza.
--
-- Reemplazar los marcadores <<...>> con los valores de UNA fila real tomada
-- del resultado de 01-regla-del-mayor-muestra-lineas.sql.
--
-- OJO: ORIGEN/DESTINO en esta tabla casi nunca son el nombre de ciudad tal
-- cual — son código DIVIPOLA o zona de tarificación (ej. ZTC_BOGOTA,
-- ZC_CUCUTA). Si el LIKE de abajo no encuentra nada con el nombre de ciudad
-- de "rutas", es necesario resolver primero el código de zona
-- (tblSAPDANE / tblZonasTarificacionRelacion — mismo mapeo que ya se hizo en
-- la sesión de EDA-2 de Habilitador B8). Avisar si pasa esto y se arma la
-- consulta de resolución de zona antes de seguir.

SELECT TOP 10
    ID, ORIGEN, DESTINO, VALOR, VIGENCIA,
    RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO, TIPOCARROSERIA,
    LINEANEGOCIO, CLASEVIAJE, CLASECONTENEDOR, DEVOLUCION
FROM dbo.tblSAPTarifas
WHERE ORIGEN LIKE '%<<ORIGEN_REAL>>%'
  AND DESTINO LIKE '%<<DESTINO_REAL>>%'
  AND <<PESO_REAL>> BETWEEN RANGOPESOINF AND RANGOPESOSUP
  AND VIGENCIA >= '<<FECHA_LINEA>>'
ORDER BY VIGENCIA DESC, TRY_CAST(VALOR AS DECIMAL(18,2)) DESC;

-- Si la línea real es de fecha pasada y la tarifa vigente hoy no es la misma
-- que estaba vigente en ese momento, repetir contra el histórico:
SELECT TOP 10
    ID, ID_TARIFA, ORIGEN, DESTINO, VALOR, VIGENCIA,
    RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifasLog
WHERE ORIGEN LIKE '%<<ORIGEN_REAL>>%'
  AND DESTINO LIKE '%<<DESTINO_REAL>>%'
  AND <<PESO_REAL>> BETWEEN RANGOPESOINF AND RANGOPESOSUP
ORDER BY VIGENCIA DESC;
