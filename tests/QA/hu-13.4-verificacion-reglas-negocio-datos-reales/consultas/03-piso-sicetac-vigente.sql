-- Base de datos: DWSice (solo lectura)
-- Regla 1 (regla del mayor) + Regla 2 (piso inviolable) — lado SICE-TAC.
--
-- Reemplazar los marcadores <<...>> con los valores de la misma fila real
-- usada en 02-tarifa-sap-vigente.sql. El piso real es VALORSICECONHORA, NO
-- la columna VALOR (ya confirmado en sesiones anteriores).

SELECT TOP 10
    ID, PERIODO, NOMBRERUTA, ORIGEN, DESTINO,
    CONFIGURACION, NOMBRETIPOCARGA, NOMBREUNIDADTRANSPORTE, CONDICIONCARGA,
    VALORSICECONHORA, VIAESTANDAR, HORASRECORRIDO
FROM dbo.tblRutasSicetac
WHERE NOMBRERUTA LIKE '%<<ORIGEN_REAL>>%'
  AND NOMBRERUTA LIKE '%<<DESTINO_REAL>>%'
  AND PERIODO = <<AAAAMM_DE_LA_FECHA_DE_LA_LINEA>>   -- ej. fecha 2026-03-15 -> 202603
ORDER BY VIAESTANDAR DESC, VALORSICECONHORA ASC;

-- Si no hay filas para ese PERIODO exacto (recordar: tblRutasSicetac es
-- normalmente una sola foto del período más reciente, no tiene profundidad
-- histórica real salvo que se use tblRutasSicetacMesAnterior/2 — ver hallazgo
-- de la sesión de EDA-2), usar el período disponible más cercano y dejarlo
-- anotado como limitación en el README, no como coincidencia exacta.
