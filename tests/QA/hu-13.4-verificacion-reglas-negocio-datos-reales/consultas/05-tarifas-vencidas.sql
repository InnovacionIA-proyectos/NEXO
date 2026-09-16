-- Paso 1 — Base de datos: dbSolucionesTI
-- Regla 4: tarifas de plaza ya vencidas.

SELECT TOP 30 ID, ORIGEN, DESTINO, VALOR, VIGENCIA, RANGOPESOINF, RANGOPESOSUP, TIPOVEHICULO
FROM dbo.tblSAPTarifas
WHERE VIGENCIA < CAST(GETDATE() AS DATE)
ORDER BY VIGENCIA DESC;

-- Paso 2 — Base de datos: CabinaFletes (la app)
-- Con 3-5 de esas rutas/pesos/fechas de vencimiento, reemplazar los
-- marcadores y confirmar que ninguna línea real costeada DESPUÉS del
-- vencimiento usó esa tarifa vencida como propuesta (su Fuente/ValorFlete no
-- debería coincidir con la fila vencida de arriba):

SELECT
    cl.Id, r.Origen, r.Destino, cl.ValorFlete, cl.Fuente, cl.CreatedAt
FROM dbo.cotizacion_lineas cl
JOIN dbo.lineas_solicitud ls ON ls.Id = cl.LineaSolicitudId
JOIN dbo.rutas r             ON r.Id = ls.RutaId
WHERE r.Origen LIKE '%<<ORIGEN_REAL>>%' AND r.Destino LIKE '%<<DESTINO_REAL>>%'
  AND cl.CreatedAt > '<<FECHA_VIGENCIA_VENCIDA>>'
ORDER BY cl.CreatedAt DESC;

-- Si ValorFlete coincide con el valor de la tarifa vencida (en vez de con el
-- piso SICE-TAC vigente en ese momento), es una desviación real de la regla 4.
