# PR #330 (S6-HU-27.1, cierra #315) — Tabla de alias de vehículos y su carga

## Qué cambia

Solo esquema y seeders (sin código C#): `db_NEXO.tbl_alias_elementos`
traduce apodos de texto libre ("T.MULA", "40HC") al código oficial. `Codigo`
es texto (no FK): para `Vehiculo` usa el mismo código que comparten NEXO y
Cubicador (`TipoVehiculo`); para `Contenedor` usa `tblSAPClaseContenedor`.
`AliasNormalizado` es columna única. 71 filas (48 Vehículo + 23 Contenedor).

## Qué se verificó (ver `verificar.ps1`, script real ejecutado)

**No se confió en el conteo declarado por la PR** — se consultó la BD real
directamente:

1. **Conteo exacto**: `tbl_alias_elementos` en
   `dbInformesAutomatizados_Pruebas` tiene **71 filas totales: 48
   `Vehiculo` + 23 `Contenedor`**, coincide cifra por cifra con lo declarado.
2. **Sin duplicados**: 0 filas con `AliasNormalizado` repetido (columna
   `UNIQUE` del esquema, confirmado también a nivel de datos).
3. **Los 5 códigos de vehículo usados (`2L2`, `2S2`, `3S3`, `C2`, `C3`)
   existen realmente** en `db_NEXO.tbl_configuraciones_vehiculo.TipoVehiculo`
   — no son inventados.
4. **Los 2 códigos de contenedor usados (`CNT20.ST`, `CNT40.HC`) existen
   realmente** en `dbSolucionesTI_Prueba.dbo.tblSAPClaseContenedor.DESCRIPCION`
   (la columna real se llama `DESCRIPCION`, no `Codigo` — confirmado al
   consultar el esquema de esa tabla directamente).
5. **Merge con `QA`**: coexisten sin conflicto real la tabla nueva de esta
   PR (`tbl_alias_elementos`) y la de la PR #329
   (`tbl_rechazos_cubicacion_linea`), ambas en partes distintas del archivo.
6. **Build**: `dotnet build Nexo.Clean.sln -c Release` → compilación
   correcta, 0 errores (1 warning preexistente ajeno a esta PR) — confirma
   que no hay cambio de código.

## Nota

Al consultar `TipoVehiculo` en `tbl_configuraciones_vehiculo` aparecieron
muchas filas de ruido de pruebas (`Tipo <guid>`) — no las generó esta PR, son
residuo preexistente de otras ejecuciones de tests contra la base
compartida; no afecta la verificación.

## Resultado

Sin hallazgos. Los datos cargados coinciden exactamente con lo declarado y
los códigos usados son reales, no inventados.
