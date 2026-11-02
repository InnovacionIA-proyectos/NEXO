-- =============================================================================
-- Limpieza de los servicios conexos que dejaron las pruebas automáticas (#421)
-- =============================================================================
-- Qué es: borra de la base de PRUEBAS los servicios que crearon las pruebas de la API y que nunca se borraron (más de mil filas con tipo
-- «Precinto» en tbl_servicios_conexos), junto con su auditoría. NO es para producción y no cambia el esquema.
--
-- Cómo reconoce un servicio de prueba (las tres condiciones a la vez, para no tocar el catálogo real):
--   1. CodigoServicio = PREFIJO + '_' + sufijo de 11 o más caracteres hexadecimales (lo que genera el helper de las pruebas con un Guid).
--      Prefijos: COD, CAT, DUP, EDIT, UPSERT, NUEVO, FILEDUP, MAY, OK, MONTO, NEG, ACT, QDIM, PV, PVN, PVB.
--      Un código real como PRE_BSAT o ALM_CALI no cumple: su sufijo es corto o no es hexadecimal.
--   2. TipoServicio es 'Precinto' o 'Precio de venta' (los que usan esas pruebas).
--   3. Ninguna cotización lo usa (tbl_cotizacion_conexos): un servicio ya cotizado no se borra.
-- Desde esta issue las pruebas borran lo que crean; este script solo limpia lo acumulado antes.
--
-- Idempotente: correrlo otra vez no encuentra nada más. Reemplazar db_NEXO por el valor de Database__Esquema si es otro.
-- Pasos: 1) correr la verificación de «antes» (solo lectura), 2) correr el borrado, 3) correr la verificación de «después».
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1) Verificación de ANTES (solo lectura). candidatos = lo que el borrado va a quitar; catalogo_protegido = lo que NO toca.
-- -----------------------------------------------------------------------------
;WITH prefijos(p) AS (
    SELECT v FROM (VALUES (N'COD'), (N'CAT'), (N'DUP'), (N'EDIT'), (N'UPSERT'), (N'NUEVO'), (N'FILEDUP'), (N'MAY'), (N'OK'),
                          (N'MONTO'), (N'NEG'), (N'ACT'), (N'QDIM'), (N'PV'), (N'PVN'), (N'PVB')) AS t(v)
), candidatos AS (
    SELECT s.[Id]
      FROM [db_NEXO].[tbl_servicios_conexos] s
      JOIN prefijos x ON s.[CodigoServicio] LIKE x.p + N'[_]%'
     WHERE s.[TipoServicio] IN (N'Precinto', N'Precio de venta')
       AND LEN(SUBSTRING(s.[CodigoServicio], LEN(x.p) + 2, 100)) >= 11
       AND SUBSTRING(s.[CodigoServicio], LEN(x.p) + 2, 100) NOT LIKE N'%[^0-9a-fA-F]%'
       AND NOT EXISTS (SELECT 1 FROM [db_NEXO].[tbl_cotizacion_conexos] c WHERE c.[ServicioConexoId] = s.[Id])
)
SELECT (SELECT COUNT(*) FROM candidatos) AS candidatos_a_borrar,
       (SELECT COUNT(*) FROM [db_NEXO].[tbl_servicios_conexos]) AS servicios_total,
       (SELECT COUNT(*) FROM [db_NEXO].[tbl_servicios_conexos]) - (SELECT COUNT(*) FROM candidatos) AS servicios_que_quedan;

-- -----------------------------------------------------------------------------
-- 2) Borrado: primero la auditoría de esos servicios y luego los servicios.
-- -----------------------------------------------------------------------------
;WITH prefijos(p) AS (
    SELECT v FROM (VALUES (N'COD'), (N'CAT'), (N'DUP'), (N'EDIT'), (N'UPSERT'), (N'NUEVO'), (N'FILEDUP'), (N'MAY'), (N'OK'),
                          (N'MONTO'), (N'NEG'), (N'ACT'), (N'QDIM'), (N'PV'), (N'PVN'), (N'PVB')) AS t(v)
), candidatos AS (
    SELECT s.[Id]
      FROM [db_NEXO].[tbl_servicios_conexos] s
      JOIN prefijos x ON s.[CodigoServicio] LIKE x.p + N'[_]%'
     WHERE s.[TipoServicio] IN (N'Precinto', N'Precio de venta')
       AND LEN(SUBSTRING(s.[CodigoServicio], LEN(x.p) + 2, 100)) >= 11
       AND SUBSTRING(s.[CodigoServicio], LEN(x.p) + 2, 100) NOT LIKE N'%[^0-9a-fA-F]%'
       AND NOT EXISTS (SELECT 1 FROM [db_NEXO].[tbl_cotizacion_conexos] c WHERE c.[ServicioConexoId] = s.[Id])
)
DELETE a
  FROM [db_NEXO].[tbl_audit_log] a
 WHERE a.[Entidad] = N'servicios_conexos' AND a.[EntidadId] IN (SELECT [Id] FROM candidatos);

;WITH prefijos(p) AS (
    SELECT v FROM (VALUES (N'COD'), (N'CAT'), (N'DUP'), (N'EDIT'), (N'UPSERT'), (N'NUEVO'), (N'FILEDUP'), (N'MAY'), (N'OK'),
                          (N'MONTO'), (N'NEG'), (N'ACT'), (N'QDIM'), (N'PV'), (N'PVN'), (N'PVB')) AS t(v)
), candidatos AS (
    SELECT s.[Id]
      FROM [db_NEXO].[tbl_servicios_conexos] s
      JOIN prefijos x ON s.[CodigoServicio] LIKE x.p + N'[_]%'
     WHERE s.[TipoServicio] IN (N'Precinto', N'Precio de venta')
       AND LEN(SUBSTRING(s.[CodigoServicio], LEN(x.p) + 2, 100)) >= 11
       AND SUBSTRING(s.[CodigoServicio], LEN(x.p) + 2, 100) NOT LIKE N'%[^0-9a-fA-F]%'
       AND NOT EXISTS (SELECT 1 FROM [db_NEXO].[tbl_cotizacion_conexos] c WHERE c.[ServicioConexoId] = s.[Id])
)
DELETE s
  FROM [db_NEXO].[tbl_servicios_conexos] s
 WHERE s.[Id] IN (SELECT [Id] FROM candidatos);

-- -----------------------------------------------------------------------------
-- 3) Verificación de DESPUÉS (solo lectura). Esperado: candidatos_a_borrar = 0.
-- -----------------------------------------------------------------------------
;WITH prefijos(p) AS (
    SELECT v FROM (VALUES (N'COD'), (N'CAT'), (N'DUP'), (N'EDIT'), (N'UPSERT'), (N'NUEVO'), (N'FILEDUP'), (N'MAY'), (N'OK'),
                          (N'MONTO'), (N'NEG'), (N'ACT'), (N'QDIM'), (N'PV'), (N'PVN'), (N'PVB')) AS t(v)
)
SELECT (SELECT COUNT(*)
          FROM [db_NEXO].[tbl_servicios_conexos] s
          JOIN prefijos x ON s.[CodigoServicio] LIKE x.p + N'[_]%'
         WHERE s.[TipoServicio] IN (N'Precinto', N'Precio de venta')
           AND LEN(SUBSTRING(s.[CodigoServicio], LEN(x.p) + 2, 100)) >= 11
           AND SUBSTRING(s.[CodigoServicio], LEN(x.p) + 2, 100) NOT LIKE N'%[^0-9a-fA-F]%'
           AND NOT EXISTS (SELECT 1 FROM [db_NEXO].[tbl_cotizacion_conexos] c WHERE c.[ServicioConexoId] = s.[Id])) AS candidatos_a_borrar,
       (SELECT COUNT(*) FROM [db_NEXO].[tbl_servicios_conexos]) AS servicios_total;
