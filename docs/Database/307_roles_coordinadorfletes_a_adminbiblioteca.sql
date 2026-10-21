-- =============================================================================
-- S6-HU-25.1 (#307) — el rol CoordinadorFletes pasa a llamarse AdminBiblioteca
-- =============================================================================
-- El rol se guarda como TEXTO (sin restricción CHECK) en dos tablas:
--   db_NEXO.tbl_usuarios.Rol
--   db_NEXO.tbl_mapeo_rol_portal.Rol
-- El código nuevo ya NO conoce el valor 'CoordinadorFletes': una fila que lo conserve hace fallar la lectura de ese usuario
-- (el enumerado no lo reconoce). Por eso este script y el despliegue del backend van JUNTOS (ver el orden abajo).
--
-- El rol del PORTAL ("CoordinadorFletesNEXO", IdRolPortal 49) conserva su nombre: es el nombre del portal, no el de Nexo. Solo
-- cambia a qué rol de Nexo se traduce.
--
-- ORDEN (sección 2.3 del documento de issues; lo decide Ismael, D8):
--   1. El front acepta durante unos días los dos nombres (CoordinadorFletes y AdminBiblioteca).
--   2. Se corre ESTE script y, de inmediato, se despliega el backend con el nombre nuevo.
--   3. Se verifica con un usuario real del rol (GET /api/sesion/actual devuelve rol = AdminBiblioteca).
--   4. Se retira el nombre viejo del front.
--
-- PROBAR PRIMERO EN PRUEBAS. Es idempotente: se puede correr dos veces (la segunda no cambia nada). Reemplazar db_NEXO por el
-- valor de Database__Esquema del ambiente si es otro.
-- =============================================================================

-- 1) RESPALDO de las filas que se van a tocar (solo si todavía hay alguna y el respaldo no existe).
IF OBJECT_ID(N'[db_NEXO].[tbl_usuarios_bak_s6hu25]', N'U') IS NULL
    SELECT * INTO [db_NEXO].[tbl_usuarios_bak_s6hu25]
    FROM [db_NEXO].[tbl_usuarios] WHERE [Rol] = N'CoordinadorFletes';

IF OBJECT_ID(N'[db_NEXO].[tbl_mapeo_rol_portal_bak_s6hu25]', N'U') IS NULL
    SELECT * INTO [db_NEXO].[tbl_mapeo_rol_portal_bak_s6hu25]
    FROM [db_NEXO].[tbl_mapeo_rol_portal] WHERE [Rol] = N'CoordinadorFletes';

-- 2) CAMBIO, en una transacción.
BEGIN TRANSACTION;

UPDATE [db_NEXO].[tbl_usuarios]         SET [Rol] = N'AdminBiblioteca' WHERE [Rol] = N'CoordinadorFletes';
UPDATE [db_NEXO].[tbl_mapeo_rol_portal] SET [Rol] = N'AdminBiblioteca' WHERE [Rol] = N'CoordinadorFletes';

-- Ningún usuario ni mapeo puede quedar con el rol viejo: si queda alguno, se deshace todo.
IF EXISTS (SELECT 1 FROM [db_NEXO].[tbl_usuarios] WHERE [Rol] = N'CoordinadorFletes')
   OR EXISTS (SELECT 1 FROM [db_NEXO].[tbl_mapeo_rol_portal] WHERE [Rol] = N'CoordinadorFletes')
BEGIN
    ROLLBACK TRANSACTION;
    RAISERROR(N'Quedaron filas con el rol CoordinadorFletes: se deshizo el cambio.', 16, 1);
END
ELSE
    COMMIT TRANSACTION;

-- 3) VERIFICACIÓN (debe dar 0 en las dos primeras y > 0 en la tercera si había usuarios con ese rol).
SELECT 'usuarios con el rol viejo'  AS verificacion, COUNT(*) AS filas FROM [db_NEXO].[tbl_usuarios]         WHERE [Rol] = N'CoordinadorFletes'
UNION ALL
SELECT 'mapeos con el rol viejo',                    COUNT(*)          FROM [db_NEXO].[tbl_mapeo_rol_portal] WHERE [Rol] = N'CoordinadorFletes'
UNION ALL
SELECT 'usuarios AdminBiblioteca',                   COUNT(*)          FROM [db_NEXO].[tbl_usuarios]         WHERE [Rol] = N'AdminBiblioteca'
UNION ALL
SELECT 'mapeos AdminBiblioteca',                     COUNT(*)          FROM [db_NEXO].[tbl_mapeo_rol_portal] WHERE [Rol] = N'AdminBiblioteca';

-- =============================================================================
-- DESHACER (solo si hiciera falta; usa el respaldo del paso 1):
--   UPDATE u SET u.[Rol] = b.[Rol] FROM [db_NEXO].[tbl_usuarios] u
--     JOIN [db_NEXO].[tbl_usuarios_bak_s6hu25] b ON b.[Id] = u.[Id];
--   UPDATE m SET m.[Rol] = b.[Rol] FROM [db_NEXO].[tbl_mapeo_rol_portal] m
--     JOIN [db_NEXO].[tbl_mapeo_rol_portal_bak_s6hu25] b ON b.[Id] = m.[Id];
-- Las tablas *_bak_s6hu25 se pueden borrar cuando el cambio ya esté verificado en el ambiente.
-- =============================================================================
