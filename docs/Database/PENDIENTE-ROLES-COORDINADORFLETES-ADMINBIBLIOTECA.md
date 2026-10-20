# Pendiente: rol CoordinadorFletes → AdminBiblioteca

_Fecha: 2026-10-19_

## Qué se hizo ya (DATA-3)

Se corrió contra `dbInformesAutomatizados_Pruebas.db_NEXO` (base de pruebas):

```sql
UPDATE db_NEXO.tbl_usuarios SET Rol = 'AdminBiblioteca' WHERE Rol = 'CoordinadorFletes';
UPDATE db_NEXO.tbl_mapeo_rol_portal SET Rol = 'AdminBiblioteca' WHERE Rol = 'CoordinadorFletes';
```

- **5 usuarios** pasaron de `CoordinadorFletes` a `AdminBiblioteca` en `tbl_usuarios`.
- La fila de mapeo del rol de portal (`IdRolPortal = 49`, `DescripcionRolPortal = 'CoordinadorFletesNEXO'`) en `tbl_mapeo_rol_portal` también pasó a `AdminBiblioteca`. El nombre del rol en el portal (`CoordinadorFletesNEXO`) **no cambió** — solo el mapeo interno.
- Verificado: 0 filas con `Rol = 'CoordinadorFletes'` en ninguna de las dos tablas.
- **Respaldo previo** (antes del `UPDATE`), por si hay que revertir:
  - `db_NEXO.tbl_usuarios_bak_20261019`
  - `db_NEXO.tbl_mapeo_rol_portal_bak_20261019`

Esto es solo el script de datos (DATA-3, decisión #9 de
`docs/backlog/2026-10-16_ISSUES-POR-REPOSITORIO.md`). No incluye ningún cambio
de código — eso es la issue de backend **NEXO-B3** (Ismael), todavía sin
desplegar.

## Por qué esto es un riesgo temporal, a propósito

El código del backend (rama actual) **todavía no reconoce `AdminBiblioteca`**
como rol válido (`RolUsuario` en `Enums.cs` solo tiene `CoordinadorFletes`).
Mientras NEXO-B3 no se despliegue, **los 5 usuarios de la base de pruebas
quedan sin acceso** (el backend no puede resolver su rol). Se corrió así, a
propósito, con el visto bueno explícito de Sara, en vez de esperar — ver la
sección "2.3 Orden de despliegue del cambio de roles" del documento de
decisiones para el orden recomendado (que normalmente sería: desplegar
backend primero, correr este script después).

## Qué falta (cuando Ismael despliegue NEXO-B3)

1. Confirmar que NEXO-B3 (backend) y ADM-F9 (front) ya están desplegados en el
   ambiente que usa esta base de pruebas.
2. Verificar con un usuario real de `AdminBiblioteca` que entra a Biblioteca,
   Tarifas Plaza, Reglas Solo-SICE y ajuste de vigencia, y recibe 403 en
   Bandeja/Mesa/cotizaciones (criterio de aceptación de NEXO-B3).
3. Una vez verificado, es seguro **borrar las 2 tablas de respaldo** (ya no
   hacen falta, y es una base compartida — mejor no dejarlas indefinidamente):
   ```sql
   DROP TABLE db_NEXO.tbl_usuarios_bak_20261019;
   DROP TABLE db_NEXO.tbl_mapeo_rol_portal_bak_20261019;
   ```

## Cómo revertir (si hace falta, antes del paso 3)

```sql
UPDATE db_NEXO.tbl_usuarios SET Rol = 'CoordinadorFletes'
WHERE Rol = 'AdminBiblioteca' AND Id IN (
    SELECT Id FROM db_NEXO.tbl_usuarios_bak_20261019 WHERE Rol = 'CoordinadorFletes'
);
UPDATE db_NEXO.tbl_mapeo_rol_portal SET Rol = 'CoordinadorFletes'
WHERE Rol = 'AdminBiblioteca' AND Id IN (
    SELECT Id FROM db_NEXO.tbl_mapeo_rol_portal_bak_20261019 WHERE Rol = 'CoordinadorFletes'
);
```
