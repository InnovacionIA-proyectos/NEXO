-- Verificación de aislamiento: ¿puede el usuario del Cubicador tocar algo de
-- lo que está desplegado?
--
-- Correr con el usuario `userTarifarioNexo` contra 10.10.40.85, conectado a
-- `dbTarifario_Nexo_Pruebas`. Desde SSMS, Azure Data Studio, o:
--
--   sqlcmd -S 10.10.40.85,1433 -U userTarifarioNexo -P "<clave>" ^
--          -d dbTarifario_Nexo_Pruebas -C -i verificar_aislamiento.sql
--
-- No escribe nada. Solo consulta.

-- 1 ------------------------------------------------------------------------
-- Dónde estoy parado. Debe decir dbTarifario_Nexo_Pruebas, nunca Nexo.
SELECT
    'Base actual'            AS verificacion,
    DB_NAME()                AS valor,
    CASE WHEN DB_NAME() = 'dbTarifario_Nexo_Pruebas'
         THEN 'OK' ELSE 'REVISAR: no es la base esperada' END AS resultado;

-- 2 ------------------------------------------------------------------------
-- Qué bases alcanza este usuario. Lo ideal es que `Nexo` NO aparezca: ahí
-- vive lo que está desplegado y lo que el equipo está probando.
SELECT
    name                     AS base_visible,
    CASE WHEN name = 'Nexo'
         THEN 'ATENCION: alcanza la base de DEPLOY'
         ELSE 'ok' END       AS nota
FROM sys.databases
WHERE HAS_DBACCESS(name) = 1
ORDER BY name;

-- 3 ------------------------------------------------------------------------
-- El esquema del Cubicador debe existir y estar separado del de Cotifly.
SELECT
    s.name                   AS esquema,
    COUNT(t.object_id)       AS tablas,
    CASE s.name
         WHEN 'dbo_cubicador' THEN 'nuestro'
         WHEN 'dbo_cotifly'   THEN 'de Cotifly: no tocar'
         WHEN 'dbo'           THEN 'compartido: aqui NO deben caer nuestras tablas'
         ELSE '' END         AS de_quien
FROM sys.schemas s
LEFT JOIN sys.tables t ON t.schema_id = s.schema_id
WHERE s.name IN ('dbo_cubicador', 'dbo_cotifly', 'dbo')
GROUP BY s.name
ORDER BY s.name;

-- 4 ------------------------------------------------------------------------
-- Permisos efectivos sobre la base actual. Hacen falta los de DDL para que
-- Alembic pueda crear y alterar tablas.
SELECT
    permission_name          AS permiso,
    'presente'               AS estado
FROM fn_my_permissions(NULL, 'DATABASE')
WHERE permission_name IN ('CREATE TABLE', 'ALTER', 'INSERT', 'UPDATE', 'DELETE', 'SELECT')
ORDER BY permission_name;

-- 5 ------------------------------------------------------------------------
-- Si `dbo_cubicador` ya tiene tablas, cuáles son. En una base limpia esto
-- devuelve cero filas: es lo esperado antes de correr la primera migración.
SELECT
    t.name                   AS tabla,
    (SELECT COUNT(*) FROM sys.columns c WHERE c.object_id = t.object_id) AS columnas
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = 'dbo_cubicador'
ORDER BY t.name;
