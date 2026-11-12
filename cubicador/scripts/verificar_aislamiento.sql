-- Verificación de la base propia del Cubicador: ¿está lista, y su usuario
-- alcanza solo lo que necesita? (S7-HU-31.3, #423)
--
-- Correr con el usuario de la aplicación, `userCubicador`, contra 10.10.40.85,
-- conectado a `dbCubicador_Pruebas`. Tiene que ser ESE usuario: las secciones
-- 2 y 4 miran los permisos de quien está conectado. Desde SSMS, Azure Data
-- Studio, o (`-f 65001` para que las tildes salgan bien):
--
--   sqlcmd -S 10.10.40.85,1433 -U userCubicador -P "<clave>" -f 65001 ^
--          -d dbCubicador_Pruebas -C -i verificar_aislamiento.sql
--
-- No escribe nada. Solo consulta. Sirve antes del traslado (para confirmar
-- que la base nueva está lista) y después (para confirmar que sigue sana).
-- Cada sección va en su propio lote (GO): si una falla, las demás corren.

-- 1 ------------------------------------------------------------------------
-- Dónde estoy parado. Debe decir dbCubicador_Pruebas.
SELECT
    'Base actual'            AS verificacion,
    DB_NAME()                AS valor,
    CASE WHEN DB_NAME() = 'dbCubicador_Pruebas'
         THEN 'OK' ELSE 'REVISAR: no es la base esperada' END AS resultado;
GO

-- 2 ------------------------------------------------------------------------
-- Qué bases alcanza este usuario. Lo ideal es que solo aparezca la suya (más
-- las del sistema, que son visibles para cualquiera). Alcanzar la de NEXO o
-- la de DEPLOY no lo necesita la aplicación.
SELECT
    name                     AS base_visible,
    CASE
        WHEN name = 'dbCubicador_Pruebas'                  THEN 'la suya'
        WHEN name IN ('master', 'tempdb', 'model', 'msdb') THEN 'del sistema'
        WHEN name = 'Nexo'                                 THEN 'ATENCION: alcanza la base de DEPLOY'
        WHEN name = 'dbTarifario_Nexo_Pruebas'             THEN 'ATENCION: alcanza la base de NEXO'
        ELSE 'REVISAR: no la necesita'
    END                      AS nota
FROM sys.databases
WHERE HAS_DBACCESS(name) = 1
ORDER BY name;
GO

-- 3 ------------------------------------------------------------------------
-- Las 16 tablas del Cubicador en `dbo`: 7 del catálogo, 3 de recomendación,
-- iaSombra y 5 del motor 3D. Se buscan por nombre: si falta alguna, aparece
-- con estado FALTA.
SELECT
    e.tabla,
    CASE WHEN t.object_id IS NULL THEN 'FALTA' ELSE 'OK' END AS estado
FROM (VALUES
    ('vehiculos'), ('TiposCarroceria'), ('relacionVehiculosCarroceria'), ('categoriaMercancia'),
    ('tiposMercancia'), ('reglasIncompatibilidad'), ('parametrosMotor'),
    ('recomendaciones'), ('recomendacionVehiculo'), ('recomendacionVehiculoMercancia'),
    ('iaSombra'),
    ('vehiculos_cubicacion'), ('planes_cargue'), ('items_plan_cargue'), ('colocaciones_cargue'), ('parametros_cubicacion')
) AS e(tabla)
LEFT JOIN sys.tables t
       ON t.name = e.tabla
      AND t.schema_id = SCHEMA_ID('dbo')
ORDER BY estado, e.tabla;
GO

-- 4 ------------------------------------------------------------------------
-- Permisos de la aplicación: solo lee y escribe datos. El esquema lo aplica
-- una persona con ESQUEMA_CUBICADOR.sql, así que el usuario de la aplicación
-- no necesita DDL ni ser dueño de la base. Lo que sobra se marca.
SELECT
    permission_name          AS permiso,
    CASE
        WHEN permission_name IN ('SELECT', 'INSERT', 'UPDATE', 'DELETE') THEN 'necesario'
        ELSE 'SOBRA: la aplicación no lo usa'
    END                      AS nota
FROM fn_my_permissions(NULL, 'DATABASE')
WHERE permission_name IN ('SELECT', 'INSERT', 'UPDATE', 'DELETE',
                          'CREATE TABLE', 'ALTER', 'CONTROL', 'ALTER ANY SCHEMA')
ORDER BY nota, permission_name;

SELECT
    rol,
    CASE WHEN IS_ROLEMEMBER(rol) = 1 THEN 'sí' ELSE 'no' END AS es_miembro,
    CASE
        WHEN rol IN ('db_datareader', 'db_datawriter') THEN 'esperado: sí'
        ELSE 'esperado: no (más de lo necesario)'
    END                      AS lo_esperado
FROM (VALUES ('db_datareader'), ('db_datawriter'), ('db_ddladmin'), ('db_owner')) AS r(rol);
GO

-- 5 ------------------------------------------------------------------------
-- El catálogo cargado por el equipo de Datos, con las cifras de la carga
-- inicial. Si una fila dice REVISAR, el catálogo no está completo.
SELECT tabla, filas, esperadas,
       CASE WHEN filas = esperadas THEN 'OK' ELSE 'REVISAR' END AS resultado
FROM (
    SELECT 'vehiculos'                   AS tabla, (SELECT COUNT(*) FROM dbo.vehiculos)                   AS filas,  5 AS esperadas
    UNION ALL SELECT 'TiposCarroceria',             (SELECT COUNT(*) FROM dbo.TiposCarroceria),             17
    UNION ALL SELECT 'relacionVehiculosCarroceria', (SELECT COUNT(*) FROM dbo.relacionVehiculosCarroceria), 26
    UNION ALL SELECT 'categoriaMercancia',          (SELECT COUNT(*) FROM dbo.categoriaMercancia),          13
    UNION ALL SELECT 'tiposMercancia',              (SELECT COUNT(*) FROM dbo.tiposMercancia),              31
    UNION ALL SELECT 'reglasIncompatibilidad',      (SELECT COUNT(*) FROM dbo.reglasIncompatibilidad),       3
) AS c
ORDER BY tabla;
GO

-- k_max = 25 en la base propia (en la compartida era 10). Cambia lo que se ve:
-- cargas que antes salían SUPERA_K_MAX ahora pueden recibir recomendación.
-- Si la clave no existe, también lo dice.
SELECT
    'k_max'                  AS verificacion,
    p.Valor                  AS valor,
    CASE WHEN p.Valor = '25' THEN 'OK'
         WHEN p.Valor IS NULL THEN 'REVISAR: no existe la clave k_max'
         ELSE 'REVISAR: se esperaba 25' END AS resultado
FROM (VALUES ('k_max')) AS e(clave)
LEFT JOIN dbo.parametrosMotor p ON p.Clave = e.clave;
GO

-- 6 ------------------------------------------------------------------------
-- Las tablas que llena la aplicación. Antes del traslado deben estar vacías:
-- el traslado es en limpio y no copia datos de la base compartida.
-- `vehiculos_cubicacion` se llena en la PRIMERA consulta que lo necesita (por
-- ejemplo GET /api/planes-cargue/vehiculos, o el worker al crear un plan desde
-- una línea), no al arrancar la aplicación.
SELECT
    t.name                   AS tabla,
    SUM(p.rows)              AS filas
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
WHERE t.schema_id = SCHEMA_ID('dbo')
  AND t.name IN ('recomendaciones', 'recomendacionVehiculo', 'recomendacionVehiculoMercancia',
                 'iaSombra', 'vehiculos_cubicacion', 'planes_cargue', 'items_plan_cargue',
                 'colocaciones_cargue', 'parametros_cubicacion')
GROUP BY t.name
ORDER BY t.name;
GO
