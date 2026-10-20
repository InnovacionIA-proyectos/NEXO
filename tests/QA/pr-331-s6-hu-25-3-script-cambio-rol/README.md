# PR #331 (S6-HU-25.3, DATA-3, issue #316) — Script de usuarios para el cambio de rol

## Qué cambia

Documenta un script de datos **ya corrido** contra la base real de pruebas
(`dbInformesAutomatizados_Pruebas.db_NEXO`): cambia el rol `CoordinadorFletes`
por `AdminBiblioteca` en `tbl_usuarios` (5 usuarios) y en
`tbl_mapeo_rol_portal` (fila `IdRolPortal=49`), con respaldo previo. Sin
cambios de esquema ni código C# en esta PR — solo el documento de
seguimiento `docs/Database/PENDIENTE-ROLES-COORDINADORFLETES-ADMINBIBLIOTECA.md`.

**Riesgo temporal reconocido explícitamente por la PR**: el backend actual
todavía no reconoce `AdminBiblioteca` (issue NEXO-B3 pendiente), así que los
5 usuarios quedan sin acceso en pruebas hasta que se despliegue. Hecho a
propósito, con aprobación de Sara.

## Qué se verificó (ver `verificar.ps1`, script real ejecutado)

**No se confió en ninguna cifra ni afirmación de la PR** — todo se consultó
directamente contra `dbInformesAutomatizados_Pruebas`:

1. **`tbl_usuarios`**: exactamente **5 filas en `AdminBiblioteca`, 0 en
   `CoordinadorFletes`**.
2. **`tbl_mapeo_rol_portal`**: la fila `IdRolPortal=49` tiene
   `DescripcionRolPortal='CoordinadorFletesNEXO'` (sin cambiar, como dice la
   PR) y `Rol='AdminBiblioteca'`.
3. **Cero filas remanentes con `CoordinadorFletes`** en ninguna de las dos
   tablas.
4. **Ambos respaldos existen** (`tbl_usuarios_bak_20261019`,
   `tbl_mapeo_rol_portal_bak_20261019`) y el primero contiene exactamente
   **5 filas con `Rol='CoordinadorFletes'`** — el respaldo captura
   precisamente lo que cambió, permitiendo revertir si hace falta.
5. **El riesgo temporal declarado es real, no solo redacción**: confirmé en
   `src/Nexo.Domain/Enums/Enums.cs` que `enum RolUsuario` hoy es
   `{ CoordinadorFletes, LiderProceso, ServiciosLogisticos, Admin, Comercial,
   AdminTarifas }` — **no incluye `AdminBiblioteca`**. El backend
   efectivamente no puede resolver ese rol todavía.
6. **Diff real contra `QA`**: solo agrega el documento de seguimiento (62
   líneas); los demás archivos que aparecían en el listado de GitHub
   (`alias_elementos.csv`, etc.) ya habían entrado a `QA` por la PR #330 y
   desaparecen del diff real tras el merge.

## Resultado

Sin hallazgos. Los datos de la migración ya corrida coinciden exactamente
con lo declarado, el respaldo es correcto y completo, y el riesgo temporal
que la PR advierte está confirmado en el código real, no es una
sobre-advertencia ni un descuido sin avisar.
