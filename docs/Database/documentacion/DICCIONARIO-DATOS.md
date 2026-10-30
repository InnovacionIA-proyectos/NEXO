# Diccionario de datos de NEXO

Qué guarda cada una de las 45 tablas de la base de NEXO (`dbTarifario_Nexo_Pruebas`, esquema `dbo`), qué significa cada columna, sus llaves e índices y de dónde salen sus datos.

- **Fuente:** `../esquema/ESQUEMA_NEXO.sql`. Tipos, nulos, valores por defecto, índices y `CHECK` corresponden a ese archivo.
- **Esquema:** la aplicación lo lee de la variable `Database__Esquema` (`dbo`).
- **Sin migraciones:** el esquema se crea con `ESQUEMA_NEXO.sql` y los datos iniciales se cargan desde `../seeder/` (ver `ORIGEN-Y-DECISIONES-SEEDER.md`). Las filas que el modelo declara con `HasData` (usuario de sistema, mapeo de roles del portal, parámetros del score) se insertan con `../seeder/datos_base_nexo.sql`.
- **Sin datos reales:** el repositorio es público; aquí solo hay estructura y significado.

---

## Índice

**A. Identidad y autorización**
1. [`tbl_usuarios`](#1-tbl_usuarios)
2. [`tbl_mapeo_rol_portal`](#2-tbl_mapeo_rol_portal)

**B. Catálogos base**
3. [`tbl_clientes`](#3-tbl_clientes)
4. [`tbl_rutas`](#4-tbl_rutas)
5. [`tbl_configuraciones_vehiculo`](#5-tbl_configuraciones_vehiculo)
6. [`tbl_alias_elementos`](#6-tbl_alias_elementos)
7. [`tbl_diccionario_cliente`](#7-tbl_diccionario_cliente)
8. [`tbl_plantillas_mapeo_cliente`](#8-tbl_plantillas_mapeo_cliente)
9. [`tbl_mapeo_vehiculo_tarifario`](#9-tbl_mapeo_vehiculo_tarifario)
10. [`tbl_rangos_peso_contenedor`](#10-tbl_rangos_peso_contenedor)
11. [`tbl_municipios`](#11-tbl_municipios)
12. [`tbl_pesos`](#12-tbl_pesos)
13. [`tbl_vehiculos_cubicacion`](#13-tbl_vehiculos_cubicacion)
14. [`tbl_tipos_mercancia_catalogo`](#14-tbl_tipos_mercancia_catalogo)
15. [`tbl_tipos_carroceria_catalogo`](#15-tbl_tipos_carroceria_catalogo)
16. [`tbl_servicios_conexos`](#16-tbl_servicios_conexos)

**C. Solicitudes y costeo**
17. [`tbl_solicitudes`](#17-tbl_solicitudes)
18. [`tbl_lineas_solicitud`](#18-tbl_lineas_solicitud)
19. [`tbl_lineas_pendientes_de_costeo`](#19-tbl_lineas_pendientes_de_costeo)
20. [`tbl_trabajos_auto_costeo`](#20-tbl_trabajos_auto_costeo)

**D. Cotizaciones**
21. [`tbl_cotizaciones`](#21-tbl_cotizaciones)
22. [`tbl_cotizacion_lineas`](#22-tbl_cotizacion_lineas)
23. [`tbl_cotizacion_conexos`](#23-tbl_cotizacion_conexos)

**E. Biblioteca de tarifas**
24. [`tbl_tarifas_historicas`](#24-tbl_tarifas_historicas)
25. [`tbl_reglas_solo_sicetac`](#25-tbl_reglas_solo_sicetac)
26. [`tbl_ajustes_vigencia_plaza`](#26-tbl_ajustes_vigencia_plaza)
27. [`tbl_tarifas_plaza`](#27-tbl_tarifas_plaza)
28. [`tbl_tarifas_plaza_historico`](#28-tbl_tarifas_plaza_historico)

**F. Priorización**
29. [`tbl_cadena_priorizacion`](#29-tbl_cadena_priorizacion)
30. [`tbl_parametros_score`](#30-tbl_parametros_score)

**G. Plan de cargue (cubicación 3D)**
31. [`tbl_planes_cargue`](#31-tbl_planes_cargue)
32. [`tbl_rechazos_cubicacion_linea`](#32-tbl_rechazos_cubicacion_linea)
33. [`tbl_items_plan_cargue`](#33-tbl_items_plan_cargue)
34. [`tbl_colocaciones_cargue`](#34-tbl_colocaciones_cargue)

**H. Auditoría**
35. [`tbl_audit_log`](#35-tbl_audit_log)

**I. Inteligencia artificial**
36. [`tbl_ia_uso`](#36-tbl_ia_uso)
37. [`tbl_asistente_conversaciones`](#37-tbl_asistente_conversaciones)
38. [`tbl_asistente_mensajes`](#38-tbl_asistente_mensajes)
39. [`tbl_asistente_herramientas_uso`](#39-tbl_asistente_herramientas_uso)
40. [`tbl_asistente_acciones`](#40-tbl_asistente_acciones)
41. [`tbl_asistente_preferencias`](#41-tbl_asistente_preferencias)

**J. Mensajería e infraestructura**
42. [`InboxState`](#42-inboxstate)
43. [`OutboxMessage`](#43-outboxmessage)
44. [`OutboxState`](#44-outboxstate)
45. [`__EFMigrationsHistory`](#45-__efmigrationshistory)

**[Apéndice: enums](#apéndice-enums)**

---

# A. Identidad y autorización

## 1. `tbl_usuarios`

**Entidad:** `clsUsuario` — **Para qué sirve:** Cuenta local de cada persona que ha entrado a NEXO. NEXO no tiene login propio: la identidad la resuelve el portal de la Administración (`X-Portal-Usuario-Id` en cada petición) y esta tabla es su copia local, con el rol de NEXO ya traducido.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `Nombre` | `nvarchar(200)` | No |  | Nombre completo |
| `Email` | `nvarchar(256)` | No |  | Correo corporativo, único |
| `Rol` | `nvarchar(40)` | No |  | Uno de `RolUsuario` (ver apéndice), guardado como texto |
| `Activo` | `bit` | No | `1` | Si el registro está vigente; los inactivos no se usan |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |
| `IdentidadPortalId` | `bigint` | Sí |  | Id de la persona en el portal. `NULL` = usuario que todavía no ha entrado por el portal (ej. el usuario de sistema). Único cuando no es `NULL` |

**Llaves e índices:** PK `PK_usuarios` (Id); Único `IX_usuarios_Email` (Email); Único `IX_usuarios_IdentidadPortalId` (IdentidadPortalId) — filtrado `WHERE [IdentidadPortalId] IS NOT NULL`.

**FK salientes:** ninguna.  
**FK entrantes:** `tbl_audit_log.UsuarioId` (NO ACTION); `tbl_ia_uso.UsuarioId` (NO ACTION); `tbl_cotizaciones.EntregadaPor` (NO ACTION); `tbl_servicios_conexos.ActualizadoPor` (NO ACTION); `tbl_solicitudes.CreadoPor` (NO ACTION); `tbl_asistente_conversaciones.UsuarioId`, `tbl_asistente_acciones.UsuarioId`, `tbl_asistente_preferencias.UsuarioId` (NO ACTION).

**De dónde salen los datos:**
- **Automático en el primer ingreso:** `clsResolverIdentidadPortalAdapter` crea la fila la primera vez que una persona entra por el portal, con el rol que resulte de `tbl_mapeo_rol_portal`.
- **Usuario de sistema** (`Sistema (placeholder)`, rol `Admin`): declarado en el modelo (`HasData` en `clsNexoDbContext`) para que las FK tengan a quién apuntar en procesos sin persona. Como el proyecto no usa migraciones, en una base nueva **no se crea solo**: lo inserta `../seeder/datos_base_nexo.sql`.
- `clsDataSeeder.cs` siembra usuarios de demostración solo en ambientes locales (`Database__SeedOnStartup`, siempre `false` en producción).

---

## 2. `tbl_mapeo_rol_portal`

**Entidad:** `clsMapeoRolPortal` — **Para qué sirve:** Traduce el rol o permiso tal como lo nombra el portal (por id o por texto) a uno de los roles de NEXO. Sin una fila aquí, el usuario se autentica pero queda sin rol (responde 403, nunca 401).

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `IdRolPortal` | `bigint` | Sí |  | Id del rol en el portal. Único cuando no es `NULL` (índice filtrado) |
| `DescripcionRolPortal` | `nvarchar(200)` | Sí |  | Nombre del rol en el portal (ej. `ComercialNEXO`, `AdminTarifasNEXO`) |
| `Rol` | `nvarchar(40)` | No |  | Rol de NEXO al que se traduce (`RolUsuario`) |
| `CreatedAt` | `datetimeoffset` | No |  | Fecha de creación (auditoría) |

**Llaves e índices:** PK `PK_mapeo_rol_portal` (Id); Único `IX_mapeo_rol_portal_IdRolPortal` (IdRolPortal) — filtrado `WHERE [IdRolPortal] IS NOT NULL`.

**FK salientes:** ninguna.  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- Declarado en el modelo (`HasData` en `clsNexoDbContext`): 9 filas (3 por texto y 6 por id de portal, del 48 al 53). Como el proyecto no usa migraciones, en una base nueva **no se crean solas**: las inserta `../seeder/datos_base_nexo.sql`.
- No hay endpoint para editarla.

---

# B. Catálogos base

## 3. `tbl_clientes`

**Entidad:** `clsCliente` — **Para qué sirve:** Cliente al que se le cotiza el flete (dueño de la carga).

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `Nombre` | `nvarchar(200)` | No |  | Nombre comercial |
| `Nit` | `nvarchar(20)` | No |  | NIT, único |
| `RazonSocial` | `nvarchar(250)` | Sí |  | Razón social completa |
| `Telefonos` | `nvarchar(max)` | Sí |  | Lista de teléfonos en JSON |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_clientes` (Id); Único `IX_clientes_Nit` (Nit).

**FK salientes:** ninguna.  
**FK entrantes:** `tbl_solicitudes.ClienteId` (NO ACTION); `tbl_diccionario_cliente.ClienteId` (NO ACTION); `tbl_plantillas_mapeo_cliente.ClienteId` (NO ACTION).

**De dónde salen los datos:**
- **Se crea al capturar una solicitud** (`clsResolucionDeCliente`): si el cliente no existe por NIT, se trae del Data Warehouse corporativo (`clsClientesDwAdapter`, solo lectura) y se copia aquí.
- No hay endpoint de alta manual directa.
- `clsDataSeeder.cs` siembra clientes de demostración solo en ambientes locales.

---

## 4. `tbl_rutas`

**Entidad:** `clsRuta` — **Para qué sirve:** Par origen-destino (único por par) sobre el que se cotiza y se registran tarifas.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `Origen` | `nvarchar(150)` | No |  | Origen en texto |
| `Destino` | `nvarchar(150)` | No |  | Destino en texto |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |
| `OrigenMunicipioId` | `int` | Sí |  | Municipio de origen en `tbl_municipios`. `NULL` en rutas anteriores o cuyo texto no resolvió contra el catálogo |
| `DestinoMunicipioId` | `int` | Sí |  | Municipio de destino, misma regla |

**Llaves e índices:** PK `PK_rutas` (Id); Único `IX_rutas_Origen_Destino` (Origen, Destino).

**FK salientes:** `OrigenMunicipioId` → `tbl_municipios.Id` (NO ACTION); `DestinoMunicipioId` → `tbl_municipios.Id` (NO ACTION).  
**FK entrantes:** `tbl_lineas_solicitud.RutaId` (NO ACTION); `tbl_tarifas_historicas.RutaId` (NO ACTION).

**De dónde salen los datos:**
- **Se crea al capturar una solicitud** (`clsResolucionDeCatalogos`) cuando el origen/destino no coincide con una ruta existente.
- `clsDataSeeder.cs` siembra rutas de demostración solo en ambientes locales.

---

## 5. `tbl_configuraciones_vehiculo`

**Entidad:** `clsConfiguracionVehiculo` — **Para qué sirve:** Tipo de vehículo comercial (código, carrocería y rango de peso) que se asigna a una línea. No confundir con `tbl_vehiculos_cubicacion` (geometría para el Cubicador) ni con `tbl_mapeo_vehiculo_tarifario` (códigos por fuente de tarifa).

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `TipoVehiculo` | `nvarchar(100)` | No |  | Código del vehículo (ej. `C2`, `2L2`, `3S3`), ya resuelto por `tbl_alias_elementos` cuando aplica |
| `Carroceria` | `nvarchar(100)` | Sí |  | Estacas, Furgón, Plataforma, etc. |
| `RangoPeso` | `nvarchar(50)` | Sí |  | Rango de peso en texto libre |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |
| `EsCatalogoOficial` | `bit` | No | `0` | la fila calza con un código canónico del catálogo. `0` = quedó en cuarentena para la bandeja de normalización |
| `Descartada` | `bit` | No | `0` | alguien la descartó en la bandeja de normalización. Sale de la bandeja pero no se borra |

**Llaves e índices:** PK `PK_configuraciones_vehiculo` (Id).

**FK salientes:** ninguna.  
**FK entrantes:** `tbl_lineas_solicitud.ConfigVehiculoId` (NO ACTION); `tbl_tarifas_historicas.ConfigVehiculoId` (NO ACTION).

**De dónde salen los datos:**
- **Se crea al capturar una solicitud** (`clsResolucionDeCatalogos`) si el vehículo no existe; si el texto no calza con el catálogo oficial queda con `EsCatalogoOficial = 0`.
- La bandeja de normalización (`clsBandejaVehiculosCommands`) la corrige, la descarta o crea un alias.
- `clsDataSeeder.cs` siembra filas de demostración solo en ambientes locales.

---

## 6. `tbl_alias_elementos`

**Entidad:** `clsAliasElemento` — **Para qué sirve:** Diccionario GLOBAL de apodos y variantes de texto libre: traduce lo que la gente escribe ("TMULA", "40HC") al código oficial de vehículo o de clase de contenedor. Aplica a todos los clientes; lo propio de un cliente va en `tbl_diccionario_cliente`.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `Alias` | `nvarchar(100)` | No |  | Texto tal como se escribió |
| `AliasNormalizado` | `nvarchar(100)` | No |  | Clave de comparación: mayúsculas, sin tildes, `_`/`-` como espacio, espacios colapsados (`clsResolucionDeCatalogos.Normalizar`). Única |
| `TipoElemento` | `nvarchar(20)` | No |  | `Vehiculo` o `Contenedor`: dice a qué catálogo pertenece `Codigo` |
| `Codigo` | `nvarchar(20)` | No |  | Código oficial en texto, no FK (ej. `2L2`, `CNT40.HC`) |
| `Activo` | `bit` | No | `1` | Si el registro está vigente; los inactivos no se usan |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |

**Llaves e índices:** PK `PK_alias_elementos` (Id); Único `UQ_alias_elementos_AliasNormalizado` (AliasNormalizado).

**FK salientes:** ninguna.  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- Carga inicial: `../seeder/alias_elementos.csv` (71 filas).
- Se amplía desde la API (`clsAliasCommands`) y desde la bandeja de normalización de vehículos.

---

## 7. `tbl_diccionario_cliente`

**Entidad:** `clsTerminoCliente` — **Para qué sirve:** Términos que usa cada cliente en sus archivos y a qué concepto de NEXO corresponden. Sirve de pista a la IA cuando llega un formato de Excel sin plantilla. Complementa a `tbl_alias_elementos`: lo de aquí es propio de un cliente y `Concepto` no se valida contra el catálogo oficial (el Turbo de un cliente puede ser `2_7`).

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `ClienteId` | `uniqueidentifier` | Sí |  | Cliente dueño del término. `NULL` = aplica a todos los clientes |
| `Termino` | `nvarchar(200)` | No |  | Texto tal como lo usa el cliente |
| `TerminoNormalizado` | `nvarchar(200)` | No |  | Clave de comparación del término |
| `Tipo` | `nvarchar(20)` | No |  | `columna` (encabezado -> campo de NEXO), `unidad` (-> `kg` o `t`) o `valor` (valor de una columna categórica) |
| `Concepto` | `nvarchar(100)` | No |  | A qué corresponde en NEXO |
| `Contexto` | `nvarchar(50)` | Sí |  | Solo para `Tipo = valor`: de qué campo es el valor (obligatorio en ese caso, `NULL` en los demás) |
| `Ejemplo` | `nvarchar(500)` | Sí |  | Ejemplo de uso |
| `Activo` | `bit` | No | `1` | Si el registro está vigente; los inactivos no se usan |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | Sí |  | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_diccionario_cliente` (Id); Único `IX_diccionario_cliente_Unico` (ClienteId, TerminoNormalizado, Tipo, Contexto); Índice `IX_diccionario_cliente_ClienteId_Tipo` (ClienteId, Tipo).

**CHECK** `CK_diccionario_cliente_Contexto`: `([Tipo]=N'valor' AND [Contexto] IS NOT NULL OR [Tipo]<>N'valor' AND [Contexto] IS NULL)`

**CHECK** `CK_diccionario_cliente_Tipo`: `([Tipo]=N'valor' OR [Tipo]=N'unidad' OR [Tipo]=N'columna')`

**FK salientes:** `ClienteId` → `tbl_clientes.Id` (NO ACTION).  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- CRUD en `/api/diccionario-cliente` (`clsDiccionarioClienteCasosDeUso`).
- Carga inicial pendiente: no hay todavía una lista armada de términos por cliente.
- Confirmar un mapeo de Excel **no** agrega términos aquí automáticamente.

---

## 8. `tbl_plantillas_mapeo_cliente`

**Entidad:** `clsPlantillaMapeoCliente` — **Para qué sirve:** Mapeo de columnas ya confirmado para un formato de Excel de un cliente. Si llega un Excel con la misma firma de encabezados se aplica la plantilla sin llamar a la IA. Si hay plantilla, manda la plantilla y no se consulta `tbl_diccionario_cliente`.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `ClienteId` | `uniqueidentifier` | No |  | Cliente dueño del formato |
| `Nombre` | `nvarchar(100)` | Sí |  | Etiqueta opcional para reconocer la plantilla |
| `FirmaEncabezados` | `nvarchar(64)` | No |  | SHA-256 (64 caracteres hex) de los encabezados normalizados y en orden. Es lo que se busca |
| `Encabezados` | `nvarchar(max)` | No |  | JSON con los encabezados tal como venían |
| `Columnas` | `nvarchar(max)` | No |  | JSON con el mapeo confirmado de cada columna al campo de NEXO |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | Sí |  | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_plantillas_mapeo_cliente` (Id); Único `IX_plantillas_mapeo_cliente_Firma` (ClienteId, FirmaEncabezados).

**CHECK** `CK_plantillas_mapeo_cliente_Columnas`: `(isjson([Columnas])=(1))`

**CHECK** `CK_plantillas_mapeo_cliente_Encabezados`: `(isjson([Encabezados])=(1))`

**FK salientes:** `ClienteId` → `tbl_clientes.Id` (NO ACTION).  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- La escribe el backend al confirmar un mapeo y la reutiliza al reconocer la firma. Si el cliente cambia el orden o los encabezados, la firma cambia y se vuelve a sugerir.

---

## 9. `tbl_mapeo_vehiculo_tarifario`

**Entidad:** `clsMapeoVehiculoTarifario` — **Para qué sirve:** Código con el que se busca cada tipo de vehículo de NEXO en cada fuente de tarifa. SICE-TAC y Plaza/SAP nombran el mismo vehículo con códigos distintos; antes NEXO usaba el mismo código para todo y, si no encontraba filas, tomaba el vehículo más barato de la ruta sin avisar. Es global, no por cliente, y parte del `TipoVehiculo` ya resuelto por `tbl_alias_elementos`.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `TipoVehiculo` | `nvarchar(20)` | No |  | Código de NEXO. Único (índice `IX_mapeo_vehiculo_tarifario_TipoVehiculo`, al que apunta la FK de `tbl_rangos_peso_contenedor`) |
| `CodigoSicetac` | `nvarchar(20)` | No |  | Valor de `CONFIGURACION` en `DWSice.dbo.tblRutasSicetac` |
| `NombreSicetac` | `nvarchar(200)` | Sí |  | Cómo describe SICE-TAC esa configuración (incluye el rango de peso bruto vehicular) |
| `CodigoPlazaSap` | `nvarchar(20)` | Sí |  | Código en Plaza/SAP, el mismo para `tblSAPTarifas` y `tbl_tarifas_plaza`. En texto; para `tblSAPTarifas` se traduce al ID del catálogo de SAP. `NULL` = sin tarifa de plaza |
| `NombreTarifasPlaza` | `nvarchar(100)` | Sí |  | Cómo se le dice en Plaza/SAP (SENCILLO, TURBO, Minimula…) |
| `Verificacion` | `nvarchar(500)` | Sí |  | De dónde salió el código y cómo se confirmó |
| `Activo` | `bit` | No | `1` | Si el registro está vigente; los inactivos no se usan |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | Sí |  | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_mapeo_vehiculo_tarifario` (Id); Único `IX_mapeo_vehiculo_tarifario_TipoVehiculo` (TipoVehiculo).

**FK salientes:** ninguna.  
**FK entrantes:** `tbl_rangos_peso_contenedor.TipoVehiculo` (NO ACTION).

**De dónde salen los datos:**
- Script con la carga inicial: `../seeder/mapeo_vehiculos.csv` (seeder `mapeo_vehiculos.csv`, 15 filas, 8 con código de plaza).
- Decisiones: Sencillo `C2` -> `2` en SICE-TAC y en plaza; Turbo `2L1`/`2L2`/`2L3` -> `2L2` en plaza (en SICE-TAC cada uno conserva su código); Dobletroque `C3` -> `3` en SICE-TAC y `C3` en plaza.
- Pendiente: el seeder de `tbl_tarifas_plaza` usa `3S2` y `2L3`, que no coinciden con `CodigoPlazaSap` de esos dos vehículos.

---

## 10. `tbl_rangos_peso_contenedor`

**Entidad:** `clsRangoPesoContenedor` — **Para qué sirve:** Qué vehículo lleva un contenedor de 20 o 40 pies según su peso. Reemplazará la lectura de las tablas de SAP `tblRangoPeso`/`tblContenedorRangoPeso`. Regla: entre las filas del tamaño de contenedor, la de menor `PesoMaximo` que alcance para el peso; por encima del tope más alto ningún vehículo aplica.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `Contenedor` | `int` | No |  | Tamaño en pies: `20` o `40` |
| `TipoVehiculo` | `nvarchar(20)` | No |  | Código de NEXO, el mismo de `tbl_mapeo_vehiculo_tarifario.TipoVehiculo` (FK). Para buscar en SICE-TAC o plaza se traduce con el mapeo (ej. `C2` -> `2`) |
| `PesoMaximo` | `int` | No |  | Peso máximo del contenedor (kg) que puede llevar ese vehículo |
| `Activo` | `bit` | No | `1` | Si el registro está vigente; los inactivos no se usan |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | Sí |  | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_rangos_peso_contenedor` (Id); Único `IX_rangos_peso_contenedor_Contenedor_TipoVehiculo` (Contenedor, TipoVehiculo).

**CHECK** `CK_rangos_peso_contenedor_Contenedor`: `([Contenedor]=(40) OR [Contenedor]=(20))`

**CHECK** `CK_rangos_peso_contenedor_PesoMaximo`: `([PesoMaximo]>(0))`

**FK salientes:** `TipoVehiculo` → `tbl_mapeo_vehiculo_tarifario.TipoVehiculo` (NO ACTION).  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- Script con la carga inicial: `../seeder/rangos_peso_contenedor.csv` (seeder `rangos_peso_contenedor.csv`, 10 filas: 5 de 40 pies y 5 de 20 pies). Hay que correrlo después del 377.
- La fila del camión de dos ejes en 40 pies, sin tope de peso en el documento original, se descartó.

---

## 11. `tbl_municipios`

**Entidad:** `clsMunicipio` — **Para qué sirve:** Catálogo propio de municipios (Colombia, Venezuela, Ecuador) para el desplegable de Origen/Destino en la captura. Foto fija, sin sincronización periódica.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `int` | No | IDENTITY(1,1) | PK autoincremental de NEXO. No es el código DANE: ese solo no es único (choca entre Venezuela y Ecuador) |
| `CodigoDane` | `nvarchar(20)` | No |  | Código DANE/DIVIPOLA (`tblSAPDANE.CODIGOCENTROPOBLADO`) |
| `Municipio` | `nvarchar(150)` | No |  | Nombre normalizado (mayúsculas, sin tildes) |
| `Departamento` | `nvarchar(150)` | No |  | Nombre normalizado |
| `Pais` | `nvarchar(2)` | No |  | `CO`, `VE` o `EC` |
| `NombreTarifasPlaza` | `nvarchar(60)` | Sí |  | Código `ZC_...` del Tarifario SAP que calzó con el municipio; `NULL` si ninguno |
| `ZonaTarificacion` | `nvarchar(60)` | Sí |  | Código `ZTC_...` de `tblZonasTarificacionRelacion`; `NULL` si no está en la relación oficial |

**Llaves e índices:** PK `PK_tbl_municipios` (Id); Único `IX_tbl_municipios_CodigoDane_Pais` (CodigoDane, Pais); Índice `IX_tbl_municipios_NombreTarifasPlaza` (NombreTarifasPlaza).

**FK salientes:** ninguna.  
**FK entrantes:** `tbl_rutas.OrigenMunicipioId` (NO ACTION); `tbl_rutas.DestinoMunicipioId` (NO ACTION); `tbl_tarifas_plaza.OrigenMunicipioId` (NO ACTION); `tbl_tarifas_plaza.DestinoMunicipioId` (NO ACTION).

**De dónde salen los datos:**
- Carga única: `../seeder/municipios.csv` (3.804 filas), calculado una vez cruzando `tblSAPDANE`, `tblZonasTarificacionRelacion` y `tblSAPTarifas`/`tblSAPTarifasLog` de `dbSolucionesTI`.
- Se lee con `GET /api/captura-solicitudes/municipios`. No hay CRUD.

---

## 12. `tbl_pesos`

**Entidad:** `clsRangoPeso` — **Para qué sirve:** Catálogo de bandas de peso (kg) para el formulario de Tarifas de Plaza. No es un catálogo cerrado: se pueden agregar bandas.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `int` | No | IDENTITY(1,1) | PK autoincremental |
| `PesoMinimo` | `int` | No |  | Límite inferior de la banda (kg) |
| `PesoMaximo` | `int` | Sí |  | Límite superior (kg). `NULL` = banda abierta, sin tope |

**Llaves e índices:** PK `PK_tbl_pesos` (Id).

**FK salientes:** ninguna.  
**FK entrantes:** `tbl_tarifas_plaza.PesoId` (NO ACTION).

**De dónde salen los datos:**
- Carga inicial: `../seeder/pesos.csv` (8 bandas).
- CRUD en `/api/pesos` (`clsCrearRangoPesoCommand`, `clsEditarRangoPesoCommand`).

---

## 13. `tbl_vehiculos_cubicacion`

**Entidad:** `clsVehiculoCubicacion` — **Para qué sirve:** Dimensiones y capacidad físicas de un vehículo para el plan de cargue (cubicación 3D). Es distinto de `tbl_configuraciones_vehiculo`, que es solo el catálogo comercial.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `Nombre` | `nvarchar(100)` | No |  | Nombre del vehículo |
| `Descripcion` | `nvarchar(300)` | Sí |  | Descripción |
| `LargoInternoMm` | `decimal(12,2)` | No |  | Largo interno de carga (mm) |
| `AnchoInternoMm` | `decimal(12,2)` | No |  | Ancho interno (mm) |
| `AltoInternoMm` | `decimal(12,2)` | No |  | Alto interno (mm) |
| `CapacidadKg` | `decimal(12,2)` | No |  | Capacidad de carga (kg) |
| `TaraKg` | `decimal(12,2)` | No |  | Peso vacío (kg) |
| `PuertaAnchoMm` | `decimal(12,2)` | No |  | Ancho de la puerta (mm) |
| `PuertaAltoMm` | `decimal(12,2)` | No |  | Alto de la puerta (mm) |
| `PuertaPosicion` | `nvarchar(20)` | No |  | Ubicación de la puerta |
| `EjesJson` | `nvarchar(max)` | No |  | JSON con posición y capacidad de cada eje |
| `ToleranciaParedesMm` | `decimal(12,2)` | No |  | Holgura contra las paredes (mm) |
| `ToleranciaEntreUnidadesMm` | `decimal(12,2)` | No |  | Holgura entre unidades (mm) |
| `EsPreset` | `bit` | No |  | `1` = vehículo predefinido del sistema; `0` = creado por un usuario |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_vehiculos_cubicacion` (Id).

**FK salientes:** ninguna.  
**FK entrantes:** `tbl_planes_cargue.VehiculoId` (NO ACTION).

**De dónde salen los datos:**
- Se crea o actualiza con los resultados del Cubicador (`clsPlanDeCargueGeneradoConsumer`) y desde la API de planes de cargue (`/api/vehiculos`, `/api/planes-cargue`).

---

## 14. `tbl_tipos_mercancia_catalogo`

**Entidad:** `clsTipoMercanciaCatalogo` — **Para qué sirve:** Proyección local del catálogo de tipos de mercancía del Cubicador, mantenida por eventos. `Id` es el texto que asigna el Cubicador y viaja como `idTipoMercancia` por toda la captura (§3.14 de `CONTRATO.md`).

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `nvarchar(64)` | No |  | PK. Id asignado por el Cubicador (texto) |
| `CodigoMercancia` | `nvarchar(40)` | No |  | Código de la mercancía |
| `NombreMercancia` | `nvarchar(150)` | No |  | Nombre |
| `Categoria` | `nvarchar(100)` | No |  | Categoría |
| `Cubicable` | `bit` | No |  | Si se puede cubicar |
| `CondicionadaCubicacion` | `bit` | No |  | Si la cubicación tiene condiciones |
| `CarroceriaPrincipal` | `nvarchar(40)` | Sí |  | Carrocería principal sugerida |
| `CarroceriaSecundaria` | `nvarchar(40)` | Sí |  | Carrocería alternativa |
| `CarroceriaSecundariaCondicionada` | `bit` | No |  | Si la alternativa tiene condiciones |
| `ControlAmbientalRequerido` | `nvarchar(40)` | No |  | Control ambiental que exige |
| `Apilable` | `bit` | No |  | Si se puede apilar |
| `Fragil` | `bit` | No |  | Si es frágil |
| `RotacionLibre` | `bit` | No |  | Si se puede rotar libremente |
| `ComentarioCondicion` | `nvarchar(500)` | Sí |  | Comentario sobre las condiciones |
| `Activo` | `bit` | No |  | Si el registro está vigente; los inactivos no se usan |
| `Version` | `int` | No |  | Versión del evento aplicado (descarta eventos viejos) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_tipos_mercancia_catalogo` (Id).

**FK salientes:** ninguna.  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- La mantiene `clsMercanciaCatalogoConsumer` con los eventos `mercancia.*` del Cubicador (MassTransit). No se edita a mano.

---

## 15. `tbl_tipos_carroceria_catalogo`

**Entidad:** `clsTipoCarroceriaCatalogo` — **Para qué sirve:** Proyección local del catálogo de tipos de carrocería del Cubicador, mantenida por eventos.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `nvarchar(64)` | No |  | PK. Id asignado por el Cubicador (texto) |
| `CodigoCarroceria` | `nvarchar(40)` | No |  | Código de la carrocería |
| `NombreCarroceria` | `nvarchar(150)` | No |  | Nombre |
| `Activo` | `bit` | No |  | Si el registro está vigente; los inactivos no se usan |
| `Version` | `int` | No |  | Versión del evento aplicado (descarta eventos viejos) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_tipos_carroceria_catalogo` (Id).

**FK salientes:** ninguna.  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- La mantiene `clsCarroceriaCatalogoConsumer` con los eventos `carroceria.*` del Cubicador (MassTransit). No se edita a mano.

---

## 16. `tbl_servicios_conexos`

**Entidad:** `clsServicioConexo` — **Para qué sirve:** Catálogo de servicios adicionales al flete (cargue, descargue, ITR, almacenamiento, acompañamiento vehicular, etc.). Reestructurada para unificar dos categorías con dimensiones de precio distintas; las dimensiones que no aplican a un servicio llevan `NO APLICA`.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `CodigoServicio` | `nvarchar(30)` | No |  | Código corto del servicio (ej. `PRE_BSAT`) |
| `TipoServicio` | `nvarchar(120)` | No |  | Nombre del servicio |
| `Origen` | `nvarchar(150)` | No | `'NO APLICA'` | Ciudad (logísticos) u origen (adicionales). Texto plano, no FK: nombre canónico de `tbl_municipios` si cruza, texto del Excel si es un peaje o corredor |
| `Destino` | `nvarchar(150)` | No | `'NO APLICA'` | Solo acompañamiento vehicular; misma regla que `Origen` |
| `Puerto` | `nvarchar(50)` | No | `'NO APLICA'` | Terminal del puerto (solo ITR) |
| `Ruta` | `nvarchar(50)` | No | `'NO APLICA'` | Tipo de ruta (solo servicios por ruta) |
| `TipoMaquina` | `nvarchar(60)` | No | `'NO APLICA'` | Máquina y movimientos (solo ITR) |
| `CostoUnitario` | `decimal(18,2)` | No |  | Valor unitario |
| `Unidad` | `nvarchar(30)` | No |  | Unidad de cobro |
| `Vigencia` | `date` | No |  | Fecha de vigencia del valor |
| `Categoria` | `nvarchar(20)` | No | `'logistico'` | `logistico` o `adicional` |
| `Activo` | `bit` | No | `1` | Si el registro está vigente; los inactivos no se usan |
| `ActualizadoPor` | `uniqueidentifier` | Sí |  | Usuario que lo modificó por última vez |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |
| `PrecioVenta` | `decimal(18,2)` | Sí |  | precio al cliente, con su margen ya incluido. Vacío = el servicio todavía no tiene precio de venta y la cotización usa el costo |

**Llaves e índices:** PK `PK_servicios_conexos` (Id); Único `UQ_servicios_conexos_dimensiones` (CodigoServicio, Origen, Destino, Puerto, Ruta, TipoMaquina); Índice `IX_servicios_conexos_ActualizadoPor` (ActualizadoPor); Índice `IX_servicios_conexos_CodigoServicio` (CodigoServicio); Check `CK_servicios_conexos_PrecioVenta` (vacío o ≥ 0).

**FK salientes:** `ActualizadoPor` → `tbl_usuarios.Id` (NO ACTION).  
**FK entrantes:** `tbl_cotizacion_conexos.ServicioConexoId` (NO ACTION).

**De dónde salen los datos:**
- Carga inicial: `../seeder/servicios_conexos.csv` (1.346 filas).
- CRUD e importación desde Excel en `/api/servicios-conexos`; cada cambio queda en `tbl_audit_log`.

---

# C. Solicitudes y costeo

## 17. `tbl_solicitudes`

**Entidad:** `clsSolicitud` — **Para qué sirve:** Cabecera de una solicitud de flete (spot o licitación) de un cliente, con el puntaje que la prioriza en la bandeja.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `ClienteId` | `uniqueidentifier` | No |  | Cliente que pide la cotización |
| `Tipo` | `nvarchar(20)` | No |  | `TipoSolicitud`: `Spot` o `Licitacion` |
| `Modalidad` | `nvarchar(30)` | Sí |  | `ModalidadSolicitud` |
| `CondicionesServicio` | `nvarchar(max)` | Sí |  | JSON con condiciones del servicio (referencia del pliego, horas de cargue/descargue, etc.) |
| `Deadline` | `datetimeoffset` | Sí |  | Fecha límite de respuesta |
| `ProbabilidadCierre` | `decimal(9,4)` | Sí |  | Probabilidad de cierre (referencia) |
| `ValorEstimado` | `decimal(18,2)` | Sí |  | Valor económico estimado (referencia) |
| `ScorePrioridad` | `decimal(6,2)` | No | `0.0` | Puntaje de prioridad, calculado (ver `tbl_parametros_score`) |
| `Estado` | `nvarchar(20)` | No | `'Pendiente'` | `EstadoSolicitud` |
| `CreadoPor` | `uniqueidentifier` | No |  | Usuario que la capturó |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |
| `RowVersion` | `rowversion` | Sí |  | Control de concurrencia optimista: si dos personas guardan a la vez, la segunda recibe 409 |

**Llaves e índices:** PK `PK_solicitudes` (Id); Índice `IX_solicitudes_ClienteId` (ClienteId); Índice `IX_solicitudes_CreadoPor` (CreadoPor); Índice `IX_solicitudes_Deadline` (Deadline); Índice `IX_solicitudes_Estado_ScorePrioridad` (Estado, ScorePrioridad).

**FK salientes:** `ClienteId` → `tbl_clientes.Id` (NO ACTION); `CreadoPor` → `tbl_usuarios.Id` (NO ACTION).  
**FK entrantes:** `tbl_cotizaciones.SolicitudId` (NO ACTION); `tbl_lineas_solicitud.SolicitudId` (ON DELETE CASCADE).

**De dónde salen los datos:**
- Captura manual o asistida con Excel (`/api/captura-solicitudes`, `/api/solicitudes`).
- `ScorePrioridad` lo recalcula la saga de priorización (`tbl_cadena_priorizacion`).
- `clsDataSeeder.cs` siembra solicitudes de demostración solo en ambientes locales.

---

## 18. `tbl_lineas_solicitud`

**Entidad:** `clsLineaSolicitud` — **Para qué sirve:** Cada línea de carga de una solicitud: ruta, vehículo, peso y condiciones. Es la unidad que se costea o se cubica.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `SolicitudId` | `uniqueidentifier` | No |  | Solicitud a la que pertenece |
| `RutaId` | `uniqueidentifier` | Sí |  | Ruta |
| `ConfigVehiculoId` | `uniqueidentifier` | Sí |  | Vehículo comercial |
| `Tipologia` | `nvarchar(30)` | Sí |  | `TipologiaCarga` |
| `Peso` | `decimal(18,2)` | Sí |  | Peso declarado (kg) |
| `Dimensiones` | `nvarchar(max)` | Sí |  | JSON con las posiciones de carga (descripción, cantidad, medidas, peso); alimenta la Mesa de Costeo y el Cubicador |
| `ValorMercancia` | `decimal(18,2)` | Sí |  | Valor de la mercancía |
| `EstadoLinea` | `nvarchar(20)` | No | `'PorCostear'` | `EstadoLinea`: `PorCostear`, `AutoResuelta`, `Revisada` o `Rechazada` |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |
| `ClaseContenedor` | `nvarchar(20)` | Sí |  | Clase de contenedor (catálogo SAP, ej. `CNT40.HC`) |
| `ClaseViaje` | `nvarchar(20)` | Sí |  | `ClaseViaje` (catálogo SAP TM) |
| `Exclusivo` | `bit` | No | `0` | Vehículo exclusivo para el cliente |
| `LevanteContenedorDiferente` | `bit` | No | `0` | El contenedor se recoge en otra ciudad |
| `LineaNegocio` | `nvarchar(30)` | Sí |  | `LineaNegocio` (catálogo SAP TM) |
| `CiudadLevanteContenedor` | `nvarchar(150)` | Sí |  | Dónde se recoge el contenedor |
| `ValorAprovisionamiento` | `decimal(18,2)` | Sí |  | Costo de traer el contenedor vacío hasta el origen |
| `CiudadDevolucionContenedor` | `nvarchar(150)` | Sí |  | Dónde se devuelve el contenedor |
| `Consolidado` | `bit` | No | `0` | Carga consolidada |
| `RetornaContenedor` | `bit` | No | `0` | Incluye devolver el contenedor vacío |
| `MotivoPendiente` | `nvarchar(500)` | Sí |  | Por qué quedó sin costear sola |
| `HorasCargue` | `decimal(6,2)` | Sí |  | horas de cargue efectivas de la línea |
| `HorasDescargue` | `decimal(6,2)` | Sí |  | Horas de descargue efectivas |
| `GrupoRutaId` | `uniqueidentifier` | Sí |  | agrupa líneas de una misma ruta heterogénea (lo pone Cotifly) |
| `NumeroLinea` | `int` | Sí |  | orden de captura dentro de la solicitud |
| `IdRecomendacionVehiculo` | `nvarchar(64)` | Sí |  | recomendación del Cubicador usada para la línea |
| `MotivoRechazo` | `nvarchar(500)` | Sí |  | por qué el Administrador de Tarifas la rechazó |
| `RechazadaPor` | `uniqueidentifier` | Sí |  | Quién la rechazó |
| `RechazadaAt` | `datetimeoffset` | Sí |  | Cuándo se rechazó |

**Llaves e índices:** PK `PK_lineas_solicitud` (Id); Índice `IX_lineas_solicitud_ConfigVehiculoId` (ConfigVehiculoId); Índice `IX_lineas_solicitud_RutaId` (RutaId); Índice `IX_lineas_solicitud_SolicitudId` (SolicitudId).

**FK salientes:** `ConfigVehiculoId` → `tbl_configuraciones_vehiculo.Id` (NO ACTION); `RutaId` → `tbl_rutas.Id` (NO ACTION); `SolicitudId` → `tbl_solicitudes.Id` (ON DELETE CASCADE).  
**FK entrantes:** `tbl_cotizacion_lineas.LineaSolicitudId` (NO ACTION); `tbl_rechazos_cubicacion_linea.LineaSolicitudId` (ON DELETE CASCADE).

**De dónde salen los datos:**
- Se crean junto con la solicitud en la captura; no tienen alta independiente.
- El costeo (`/api/lineas-solicitud`, auto-costeo) cambia `EstadoLinea`, `MotivoPendiente` y los campos de rechazo.

---

## 19. `tbl_lineas_pendientes_de_costeo`

**Entidad:** `clsLineaPendienteDeCosteo` — **Para qué sirve:** Líneas que el auto-costeo de un Excel no pudo resolver solo, con el motivo, para que alguien las resuelva. Un solo sitio para el motivo, para que no haya dos versiones que se contradigan.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `TrabajoId` | `uniqueidentifier` | No |  | Trabajo de auto-costeo (`tbl_trabajos_auto_costeo.CorrelationId`) |
| `SolicitudId` | `uniqueidentifier` | No |  | Solicitud |
| `LineaSolicitudId` | `uniqueidentifier` | No |  | Línea pendiente |
| `Tipo` | `nvarchar(20)` | No |  | `TipoPendienteDeCosteo`: `Tecnica` (falló una fuente, se reintenta) o `Manual` (no hay tarifa válida, espera al Administrador de Tarifas) |
| `Motivo` | `nvarchar(500)` | No |  | Por qué quedó pendiente |
| `ResueltaEn` | `datetimeoffset` | Sí |  | Cuándo se resolvió. `NULL` = sigue pendiente (índice filtrado sobre estas) |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_lineas_pendientes_de_costeo` (Id); Índice `IX_lineas_pendientes_de_costeo_SolicitudId` (SolicitudId) — filtrado `WHERE [ResueltaEn] IS NULL`; Índice `IX_lineas_pendientes_de_costeo_TrabajoId_LineaSolicitudId_Tipo` (TrabajoId, LineaSolicitudId, Tipo).

**FK salientes:** ninguna.  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- La escribe el auto-costeo (`clsEfPendientesDeCosteoRepository`); se consulta en `/api/trabajos`.

---

## 20. `tbl_trabajos_auto_costeo`

**Entidad:** `clsEstadoAutoCosteo` — **Para qué sirve:** Estado de la saga de MassTransit que auto-costea un Excel capturado: cuántas líneas se resolvieron y cuántas quedaron pendientes. Vive en esta base porque es la respuesta que consulta el cliente sobre su trabajo y un reinicio no puede borrarla.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `CorrelationId` | `uniqueidentifier` | No |  | PK. Id del trabajo |
| `Version` | `int` | No |  | Versión de la saga (concurrencia de MassTransit) |
| `CurrentState` | `nvarchar(64)` | No |  | Estado actual de la saga |
| `SolicitudId` | `uniqueidentifier` | No |  | Solicitud que se está costeando |
| `SolicitadoPor` | `uniqueidentifier` | No |  | Usuario que lo pidió |
| `SolicitadoEn` | `datetimeoffset` | No |  | Cuándo se pidió |
| `TerminadoEn` | `datetimeoffset` | Sí |  | Cuándo terminó |
| `TotalLineas` | `int` | Sí |  | Total de líneas |
| `Resueltas` | `int` | No |  | Líneas resueltas solas |
| `PendientesTecnicas` | `int` | No |  | Pendientes por falla técnica |
| `RequierenManual` | `int` | No |  | Pendientes que requieren al Administrador de Tarifas |
| `TokenExpiracion` | `uniqueidentifier` | Sí |  | Token del vencimiento programado |
| `Secuencia` | `int` | No | `0` | Contador de eventos aplicados |

**Llaves e índices:** PK `PK_trabajos_auto_costeo` (CorrelationId).

**FK salientes:** ninguna.  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- La escribe y actualiza la saga de auto-costeo (MassTransit). Se consulta en `/api/trabajos`.

---

# D. Cotizaciones

## 21. `tbl_cotizaciones`

**Entidad:** `clsCotizacion` — **Para qué sirve:** Cabecera de la cotización de una solicitud: estado, resultado y total.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `SolicitudId` | `uniqueidentifier` | No |  | Solicitud cotizada |
| `Estado` | `nvarchar(20)` | No | `'Borrador'` | `EstadoCotizacion`: `Borrador` o `Entregada` |
| `Resultado` | `nvarchar(20)` | No | `'SinDefinir'` | `ResultadoCotizacion`: `SinDefinir`, `Ganada` o `Perdida` |
| `Total` | `decimal(18,2)` | No | `0.0` | Suma de fletes, aprovisionamiento, retorno de contenedor y conexos |
| `EntregadaPor` | `uniqueidentifier` | Sí |  | Quién la entregó |
| `EntregadaAt` | `datetimeoffset` | Sí |  | Cuándo se entregó |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |
| `RowVersion` | `rowversion` | Sí |  | Control de concurrencia optimista: si dos personas guardan a la vez, la segunda recibe 409 |
| `EsAutoCosteo` | `bit` | No | `0` | la armó el auto-costeo; nunca pasa por `/entregar` |
| `ModificadaTrasEntregaAt` | `datetimeoffset` | Sí |  | último cambio de conexos después de entregar. No mueve `Estado` ni `EntregadaAt` |

**Llaves e índices:** PK `PK_cotizaciones` (Id); Índice `IX_cotizaciones_EntregadaPor` (EntregadaPor); Índice `IX_cotizaciones_SolicitudId` (SolicitudId).

**FK salientes:** `SolicitudId` → `tbl_solicitudes.Id` (NO ACTION); `EntregadaPor` → `tbl_usuarios.Id` (NO ACTION).  
**FK entrantes:** `tbl_cotizacion_conexos.CotizacionId` (ON DELETE CASCADE); `tbl_cotizacion_lineas.CotizacionId` (ON DELETE CASCADE); `tbl_tarifas_historicas.CotizacionId` (NO ACTION).

**De dónde salen los datos:**
- Se crea, entrega y marca con resultado desde `/api/cotizaciones`, o la arma el auto-costeo.
- Al marcarla `Ganada` por primera vez se generan filas en `tbl_tarifas_historicas`.

---

## 22. `tbl_cotizacion_lineas`

**Entidad:** `clsCotizacionLinea` — **Para qué sirve:** Valor del flete cotizado para una línea de solicitud y de dónde salió.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `CotizacionId` | `uniqueidentifier` | No |  | Cotización |
| `LineaSolicitudId` | `uniqueidentifier` | Sí |  | Línea de solicitud costeada (`NULL` = flete manual sin línea) |
| `ValorFlete` | `decimal(18,2)` | No |  | Valor del flete |
| `Fuente` | `nvarchar(20)` | No |  | `FuenteFlete`: de dónde salió el valor |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |
| `ValorAprovisionamiento` | `decimal(18,2)` | Sí |  | Costo de traer el contenedor vacío (suma al total) |
| `ValorRetornoContenedor` | `decimal(18,2)` | Sí |  | Costo de devolver el contenedor vacío (suma al total) |

**Llaves e índices:** PK `PK_cotizacion_lineas` (Id); Índice `IX_cotizacion_lineas_CotizacionId` (CotizacionId); Índice `IX_cotizacion_lineas_LineaSolicitudId` (LineaSolicitudId).

**FK salientes:** `CotizacionId` → `tbl_cotizaciones.Id` (ON DELETE CASCADE); `LineaSolicitudId` → `tbl_lineas_solicitud.Id` (NO ACTION).  
**FK entrantes:** `tbl_cotizacion_conexos.CotizacionLineaId` (NO ACTION).

**De dónde salen los datos:**
- Manual desde la cotización, o precargada al aplicar la propuesta de tarifa / el auto-costeo.

---

## 23. `tbl_cotizacion_conexos`

**Entidad:** `clsCotizacionConexo` — **Para qué sirve:** Servicio conexo aplicado a una línea de la cotización, con la cantidad, su costo y su precio de venta guardados al momento de agregarlo.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `CotizacionId` | `uniqueidentifier` | No |  | Cotización |
| `ServicioConexoId` | `uniqueidentifier` | Sí |  | Servicio del catálogo |
| `Cantidad` | `decimal(12,2)` | No |  | Cantidad (toneladas, horas, puntos…) |
| `ValorAplicado` | `decimal(18,2)` | No |  | Costo del conexo (costo unitario × cantidad). Lo que se cotiza es `PrecioVentaUnitario × Cantidad` |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |
| `CotizacionLineaId` | `uniqueidentifier` | No |  | línea de la cotización a la que se liga |
| `Origen` | `nvarchar(20)` | No | `'individual'` | `individual` (se agregó a esta línea) o `general` (se pidió para todas las líneas; igual crea una fila por línea) |
| `PrecioVentaUnitario` | `decimal(18,2)` | No (*) |  | precio de venta del servicio al agregarlo a la cotización (queda fijo aunque cambie la biblioteca); si el servicio no tenía precio de venta, el costo. Las filas anteriores al cambio se llenaron con su costo (`ValorAplicado / Cantidad`) |

(*) En la base anterior la columna permite vacío hasta que se corra la parte B de `../scripts/407_precio_venta_conexos.sql`; en una base nueva (`ESQUEMA_NEXO.sql`) nace `NOT NULL`.

**Llaves e índices:** PK `PK_cotizacion_conexos` (Id); Índice `IX_cotizacion_conexos_CotizacionId` (CotizacionId); Índice `IX_cotizacion_conexos_CotizacionLineaId` (CotizacionLineaId); Índice `IX_cotizacion_conexos_ServicioConexoId` (ServicioConexoId); Check `CK_cotizacion_conexos_PrecioVentaUnitario` (≥ 0).

**FK salientes:** `CotizacionId` → `tbl_cotizaciones.Id` (ON DELETE CASCADE); `ServicioConexoId` → `tbl_servicios_conexos.Id` (NO ACTION); `CotizacionLineaId` → `tbl_cotizacion_lineas.Id` (NO ACTION).  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- Se agregan desde la cotización (`/api/cotizaciones`).

---

# E. Biblioteca de tarifas

## 24. `tbl_tarifas_historicas`

**Entidad:** `clsTarifaHistorica` — **Para qué sirve:** Lo que realmente se cobró en cotizaciones ganadas ("Tarifas Ganadas"), para proponer tarifa por precedente.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `RutaId` | `uniqueidentifier` | Sí |  | Ruta |
| `ConfigVehiculoId` | `uniqueidentifier` | Sí |  | Vehículo |
| `Tipologia` | `nvarchar(30)` | Sí |  | `TipologiaCarga` |
| `Valor` | `decimal(18,2)` | No |  | Valor cobrado |
| `Fecha` | `date` | No |  | Fecha de la tarifa |
| `CotizacionId` | `uniqueidentifier` | Sí |  | Cotización ganada de origen. `NULL` = tarifa legada sin cotización |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_tarifas_historicas` (Id); Índice `IX_tarifas_historicas_ConfigVehiculoId` (ConfigVehiculoId); Índice `IX_tarifas_historicas_CotizacionId` (CotizacionId); Índice `IX_tarifas_historicas_RutaId_ConfigVehiculoId_Tipologia` (RutaId, ConfigVehiculoId, Tipologia).

**FK salientes:** `ConfigVehiculoId` → `tbl_configuraciones_vehiculo.Id` (NO ACTION); `CotizacionId` → `tbl_cotizaciones.Id` (NO ACTION); `RutaId` → `tbl_rutas.Id` (NO ACTION).  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- **Automático:** `clsMarcarResultadoCommand` crea una fila por línea de flete cuando una cotización se marca `Ganada` por primera vez.
- Se consulta en `/api/tarifas-historicas`.

---

## 25. `tbl_reglas_solo_sicetac`

**Entidad:** `clsReglaSoloSicetac` — **Para qué sirve:** Ruta + vehículo donde la propuesta se resuelve SOLO con el piso SICE-TAC (no se compara con plaza ni histórico). Decisión manual de AdminTarifas/Admin, única por combinación.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `Origen` | `nvarchar(150)` | No |  | Origen. Las filas del seeder usan código DANE, no texto de ciudad |
| `Destino` | `nvarchar(150)` | No |  | Destino, misma regla |
| `TipoVehiculo` | `nvarchar(40)` | No |  | Código de vehículo (las filas del seeder usan el código de SICE-TAC, ej. `2` para el Sencillo) |
| `CreadoPor` | `uniqueidentifier` | Sí |  | Usuario que la creó |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_reglas_solo_sicetac` (Id); Único `IX_reglas_solo_sicetac_Origen_Destino_TipoVehiculo` (Origen, Destino, TipoVehiculo).

**FK salientes:** ninguna.  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- Carga inicial: `../seeder/reglas_solo_sicetac.csv` (260 filas).
- CRUD e importación desde Excel en `/api/reglas-solo-sicetac`.

---

## 26. `tbl_ajustes_vigencia_plaza`

**Entidad:** `clsAjusteVigenciaPlaza` — **Para qué sirve:** Corrección local de la vigencia de una tarifa de plaza de SAP (`dbSolucionesTI.tblSAPTarifas`, de solo lectura). Una por tarifa.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `TarifaPlazaId` | `int` | No |  | Id de la fila de `tblSAPTarifas` a la que aplica. Único. No es FK (otra base) |
| `VigenciaAjustada` | `datetime2` | No |  | Nueva vigencia efectiva |
| `VigenciaOriginal` | `datetime2` | Sí |  | Vigencia de SAP al momento del ajuste |
| `Motivo` | `nvarchar(300)` | Sí |  | Motivo del ajuste |
| `AjustadoPor` | `uniqueidentifier` | Sí |  | Usuario que lo ajustó (sin FK) |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_ajustes_vigencia_plaza` (Id); Único `IX_ajustes_vigencia_plaza_TarifaPlazaId` (TarifaPlazaId).

**FK salientes:** ninguna.  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- Ajuste individual o masivo desde la biblioteca de plaza SAP (`/api/biblioteca-sap-plaza`).

---

## 27. `tbl_tarifas_plaza`

**Entidad:** `clsTarifaPlaza` — **Para qué sirve:** Tarifa de plaza gobernada por NEXO: recrea el formulario de captura del sistema externo. No es una copia de `tblSAPTarifas`. Única por sus 7 dimensiones; `Valor`, `Vigencia` y `DetallesTarifa` son lo que cambia con el tiempo.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `LineaNegocio` | `nvarchar(100)` | No |  | Nombre completo de `tblSAPLineaNegocio` (texto, no el ID de la otra base) |
| `ClaseViaje` | `nvarchar(50)` | No |  | Nombre completo de `tblSAPClaseViaje` |
| `OrigenMunicipioId` | `int` | No |  | Municipio de origen (`tbl_municipios.Id`, no el código DANE) |
| `DestinoMunicipioId` | `int` | No |  | Municipio de destino |
| `ClaseContenedor` | `nvarchar(50)` | Sí |  | Código de `tblSAPClaseContenedor`; `NULL` si no es de contenedor |
| `PesoId` | `int` | Sí |  | Banda de peso (`tbl_pesos`), para tarifas de contenedor por peso |
| `TipoVehiculo` | `nvarchar(40)` | Sí |  | Código de vehículo. Ver pendiente: el seeder usa `3S2` y `2L3`, que no coinciden con `CodigoPlazaSap` del mapeo |
| `Valor` | `decimal(18,2)` | No |  | Valor de la tarifa |
| `Vigencia` | `date` | No |  | Fecha de vigencia |
| `DetallesTarifa` | `nvarchar(2000)` | No |  | Condiciones en texto libre (obligatorio) |
| `CreadoPor` | `uniqueidentifier` | Sí |  | Usuario que la creó |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_tarifas_plaza` (Id); Único `UQ_tarifas_plaza_dimensiones` (LineaNegocio, ClaseViaje, OrigenMunicipioId, DestinoMunicipioId, ClaseContenedor, PesoId, TipoVehiculo); Índice `IX_tarifas_plaza_DestinoMunicipioId` (DestinoMunicipioId); Índice `IX_tarifas_plaza_OrigenMunicipioId` (OrigenMunicipioId); Índice `IX_tarifas_plaza_PesoId` (PesoId).

**FK salientes:** `OrigenMunicipioId` → `tbl_municipios.Id` (NO ACTION); `DestinoMunicipioId` → `tbl_municipios.Id` (NO ACTION); `PesoId` → `tbl_pesos.Id` (NO ACTION).  
**FK entrantes:** `tbl_tarifas_plaza_historico.TarifaPlazaId` (ON DELETE CASCADE).

**Trigger:** `TRG_tarifas_plaza_historico` (`AFTER UPDATE`): si cambió `Valor`, `Vigencia` o `DetallesTarifa`, copia la fila anterior completa a `tbl_tarifas_plaza_historico`. Opera sobre todas las filas de la sentencia.

**De dónde salen los datos:**
- Carga inicial: `../seeder/tarifas_plaza.csv` (107 filas).
- CRUD en `/api/tarifas-plaza` (`clsCrearTarifaPlazaCommand`); cada cambio queda en `tbl_audit_log`.
- El trigger `TRG_tarifas_plaza_historico` archiva el valor anterior en cada actualización de `Valor`, `Vigencia` o `DetallesTarifa`.

---

## 28. `tbl_tarifas_plaza_historico`

**Entidad:** `clsTarifaPlazaHistorico` — **Para qué sirve:** Foto del valor ANTERIOR de una tarifa de plaza cada vez que se actualiza (como `tblSAPTarifasLog` para `tblSAPTarifas`).

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `TarifaPlazaId` | `uniqueidentifier` | No |  | Tarifa archivada |
| `LineaNegocio` | `nvarchar(100)` | No |  | Valor anterior; igual que en `tbl_tarifas_plaza` |
| `ClaseViaje` | `nvarchar(50)` | No |  | Valor anterior; igual que en `tbl_tarifas_plaza` |
| `OrigenMunicipioId` | `int` | No |  | Valor anterior; igual que en `tbl_tarifas_plaza` |
| `DestinoMunicipioId` | `int` | No |  | Valor anterior; igual que en `tbl_tarifas_plaza` |
| `ClaseContenedor` | `nvarchar(50)` | Sí |  | Valor anterior; igual que en `tbl_tarifas_plaza` |
| `PesoId` | `int` | Sí |  | Valor anterior; igual que en `tbl_tarifas_plaza` |
| `TipoVehiculo` | `nvarchar(40)` | Sí |  | Valor anterior; igual que en `tbl_tarifas_plaza` |
| `Valor` | `decimal(18,2)` | No |  | Valor anterior; igual que en `tbl_tarifas_plaza` |
| `Vigencia` | `date` | No |  | Valor anterior; igual que en `tbl_tarifas_plaza` |
| `DetallesTarifa` | `nvarchar(2000)` | No |  | Valor anterior; igual que en `tbl_tarifas_plaza` |
| `CreadoPor` | `uniqueidentifier` | Sí |  | Valor anterior; igual que en `tbl_tarifas_plaza` |
| `CreatedAt` | `datetimeoffset` | No |  | Fecha de creación original de la tarifa |
| `ReemplazadoAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Cuándo se archivó (cuándo se hizo el `UPDATE`) |

**Llaves e índices:** PK `PK_tarifas_plaza_historico` (Id); Índice `IX_tarifas_plaza_historico_TarifaPlazaId` (TarifaPlazaId).

**FK salientes:** `TarifaPlazaId` → `tbl_tarifas_plaza.Id` (ON DELETE CASCADE).  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- **100% automático** por el trigger `TRG_tarifas_plaza_historico`. Nace vacía; nadie escribe aquí a mano ni por API.

---

# F. Priorización

## 29. `tbl_cadena_priorizacion`

**Entidad:** `clsEstadoPriorizacion` — **Para qué sirve:** Estado de la saga de MassTransit que rearma el puntaje de una solicitud cuando cambian sus datos o pasa el tiempo. El reloj vive en el broker, así que escalar el API no lo duplica.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `CorrelationId` | `uniqueidentifier` | No |  | PK. Id de la saga (la solicitud) |
| `Version` | `int` | No |  | Versión de la saga (concurrencia de MassTransit) |
| `CurrentState` | `nvarchar(64)` | No |  | Estado actual |
| `TokenTic` | `uniqueidentifier` | Sí |  | Token del próximo tic programado |
| `ArmadaEn` | `datetimeoffset` | No |  | Cuándo se armó |
| `RearmadaEn` | `datetimeoffset` | No |  | Cuándo se rearmó por última vez |
| `UltimoRecalculoEn` | `datetimeoffset` | Sí |  | Último recálculo |
| `RecalculosPorReloj` | `int` | No |  | Recálculos disparados por el tiempo |
| `RecalculosPorParametros` | `int` | No |  | Recálculos disparados por cambio de parámetros |

**Llaves e índices:** PK `PK_cadena_priorizacion` (CorrelationId).

**FK salientes:** ninguna.  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- La escribe la saga de priorización (MassTransit). No se edita a mano.

---

## 30. `tbl_parametros_score`

**Entidad:** `clsParametrosScore` — **Para qué sirve:** Fila única con los pesos y constantes de la fórmula del puntaje de prioridad.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `int` | No | IDENTITY(1,1) | PK. Siempre `1` (fila única) |
| `PesoUrgencia` | `float` | No |  | Peso del componente urgencia |
| `PesoValor` | `float` | No |  | Peso del componente valor |
| `PesoProbabilidad` | `float` | No |  | Peso del componente probabilidad |
| `PesoTipo` | `float` | No |  | Peso del componente tipo |
| `HorizonteHoras` | `float` | No |  | Horas para normalizar la urgencia |
| `ValorReferencia` | `decimal(18,2)` | No |  | Referencia del componente valor |
| `PuntajeLicitacion` | `float` | No |  | Puntaje del tipo licitación |
| `PuntajeSpot` | `float` | No |  | Puntaje del tipo spot |
| `ValorPorDefectoSinValor` | `float` | No |  | Valor por defecto si la solicitud no trae valor |
| `ProbabilidadPorDefecto` | `float` | No |  | Probabilidad por defecto |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |
| `RowVersion` | `rowversion` | Sí |  | Control de concurrencia optimista: si dos personas guardan a la vez, la segunda recibe 409 |

**Llaves e índices:** PK `PK_parametros_score` (Id).

**FK salientes:** ninguna.  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- La fila se declara en el modelo (`HasData` en `clsNexoDbContext`, valores por defecto de `clsParametrosScore`). Como el proyecto no usa migraciones, en una base nueva **no se crea sola**: la inserta `../seeder/datos_base_nexo.sql`.
- Se lee y se edita en `/api/priorizacion/parametros` (solo Admin); un cambio dispara el recálculo de puntajes.

---

# G. Plan de cargue (cubicación 3D)

## 31. `tbl_planes_cargue`

**Entidad:** `clsPlanCargue` — **Para qué sirve:** Un plan de cargue optimizado para un vehículo: estrategia, algoritmo, utilización lograda e incidencias.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `Nombre` | `nvarchar(150)` | No |  | Nombre del plan |
| `VehiculoId` | `uniqueidentifier` | No |  | Vehículo (`tbl_vehiculos_cubicacion`) |
| `Estado` | `nvarchar(20)` | No |  | Estado del plan |
| `Estrategia` | `nvarchar(40)` | No |  | Estrategia de orden |
| `PonderacionesJson` | `nvarchar(max)` | Sí |  | JSON con ponderaciones del puntaje |
| `Algoritmo` | `nvarchar(10)` | No |  | Algoritmo usado |
| `Semilla` | `int` | Sí |  | Semilla del algoritmo |
| `PesoTotalKg` | `decimal(12,2)` | No |  | Peso total cargado (kg) |
| `VolumenUtilizadoM3` | `decimal(12,3)` | No |  | Volumen usado (m³) |
| `UtilizacionVolumen` | `decimal(6,2)` | No |  | Porcentaje de volumen usado |
| `UtilizacionPiso` | `decimal(6,2)` | No |  | Porcentaje de piso usado |
| `Puntaje` | `decimal(6,2)` | No |  | Puntaje del plan |
| `IncidenciasJson` | `nvarchar(max)` | No |  | JSON con incidencias (rechazos, advertencias) |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |
| `LineaSolicitudId` | `uniqueidentifier` | Sí |  | Línea de solicitud de la que salió (vínculo lógico, sin FK) |
| `NumeroVehiculo` | `int` | No | `0` | Posición dentro de una flota (1, 2, 3…) |
| `MetricasJson` | `nvarchar(max)` | Sí |  | JSON con métricas adicionales |

**Llaves e índices:** PK `PK_planes_cargue` (Id); Índice `IX_planes_cargue_LineaSolicitudId` (LineaSolicitudId); Índice `IX_planes_cargue_VehiculoId` (VehiculoId).

**FK salientes:** `VehiculoId` → `tbl_vehiculos_cubicacion.Id` (NO ACTION).  
**FK entrantes:** `tbl_colocaciones_cargue.PlanCargueId` (ON DELETE CASCADE); `tbl_items_plan_cargue.PlanCargueId` (ON DELETE CASCADE).

**De dónde salen los datos:**
- Se crea desde `/api/planes-cargue`, o desde una línea de solicitud con los resultados del Cubicador (`clsPlanDeCargueGeneradoConsumer`).

---

## 32. `tbl_rechazos_cubicacion_linea`

**Entidad:** `clsRechazoCubicacionLinea` — **Para qué sirve:** por qué el Cubicador rechazó "crear plan desde línea" para una línea. Una fila por línea: un rechazo nuevo reemplaza al anterior y pedir el plan otra vez la borra.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `LineaSolicitudId` | `uniqueidentifier` | No |  | PK y FK a la línea |
| `Codigo` | `nvarchar(60)` | No |  | Código del rechazo |
| `Mensaje` | `nvarchar(1000)` | No |  | Mensaje del rechazo |
| `RechazadoAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Cuándo se rechazó |

**Llaves e índices:** PK `PK_rechazos_cubicacion_linea` (LineaSolicitudId).

**FK salientes:** `LineaSolicitudId` → `tbl_lineas_solicitud.Id` (ON DELETE CASCADE).  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- La escribe `clsRechazoCubicacionLineaAdapter` al recibir el rechazo del Cubicador.

---

## 33. `tbl_items_plan_cargue`

**Entidad:** `clsItemPlanCargue` — **Para qué sirve:** Mercancía a cargar dentro de un plan: dimensiones, peso y reglas físicas que el algoritmo debe respetar.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `PlanCargueId` | `uniqueidentifier` | No |  | Plan |
| `Sku` | `nvarchar(60)` | No |  | Código del ítem |
| `Descripcion` | `nvarchar(200)` | No |  | Descripción |
| `Pedido` | `nvarchar(60)` | Sí |  | Pedido |
| `Cliente` | `nvarchar(120)` | Sí |  | Cliente |
| `Destino` | `nvarchar(120)` | Sí |  | Destino |
| `SecuenciaEntrega` | `int` | No |  | Orden de entrega |
| `LargoMm` | `decimal(12,2)` | No |  | Largo (mm) |
| `AnchoMm` | `decimal(12,2)` | No |  | Ancho (mm) |
| `AltoMm` | `decimal(12,2)` | No |  | Alto (mm) |
| `PesoKg` | `decimal(12,2)` | No |  | Peso (kg) |
| `Cantidad` | `int` | No |  | Unidades |
| `Color` | `nvarchar(9)` | Sí |  | Color para la vista 3D |
| `Prioridad` | `int` | No |  | Prioridad |
| `TipoEmbalaje` | `nvarchar(40)` | Sí |  | Tipo de embalaje |
| `Apilable` | `bit` | No |  | Si se puede apilar |
| `MaxNiveles` | `int` | No |  | Niveles máximos de apilamiento |
| `MaxPesoEncimaKg` | `decimal(12,2)` | No |  | Peso máximo encima (kg) |
| `Rotable` | `bit` | No |  | Si se puede rotar |
| `MantenerVertical` | `bit` | No |  | Si debe ir vertical |
| `Fragil` | `bit` | No |  | Si es frágil |
| `Peligroso` | `bit` | No |  | Si es peligroso |
| `Refrigerado` | `bit` | No |  | Si es refrigerado |
| `GrupoCompatibilidad` | `nvarchar(40)` | Sí |  | Grupo de compatibilidad con otros ítems |
| `Observaciones` | `nvarchar(300)` | Sí |  | Observaciones |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_items_plan_cargue` (Id); Índice `IX_items_plan_cargue_PlanCargueId` (PlanCargueId).

**FK salientes:** `PlanCargueId` → `tbl_planes_cargue.Id` (ON DELETE CASCADE).  
**FK entrantes:** `tbl_colocaciones_cargue.ItemPlanCargueId` (NO ACTION).

**De dónde salen los datos:**
- Manual, por importación, o generados desde el JSON `Dimensiones` de una línea de solicitud.

---

## 34. `tbl_colocaciones_cargue`

**Entidad:** `clsColocacionCargue` — **Para qué sirve:** Dónde quedó cada unidad de un ítem dentro del vehículo (posición, orientación, secuencia), o por qué se rechazó.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `PlanCargueId` | `uniqueidentifier` | No |  | Plan |
| `ItemPlanCargueId` | `uniqueidentifier` | No |  | Ítem |
| `IndiceUnidad` | `int` | No |  | Cuál unidad del ítem |
| `Cargada` | `bit` | No |  | Si quedó cargada |
| `X` | `decimal(12,2)` | No |  | Posición X (mm) |
| `Y` | `decimal(12,2)` | No |  | Posición Y (mm) |
| `Z` | `decimal(12,2)` | No |  | Posición Z (mm) |
| `LargoColocadoMm` | `decimal(12,2)` | No |  | Largo ya orientado (mm) |
| `AnchoColocadoMm` | `decimal(12,2)` | No |  | Ancho ya orientado (mm) |
| `AltoColocadoMm` | `decimal(12,2)` | No |  | Alto ya orientado (mm) |
| `OrientacionIndice` | `int` | No |  | Orientación usada |
| `SecuenciaCargue` | `int` | No |  | Orden de cargue |
| `SoporteColocacionId` | `uniqueidentifier` | Sí |  | Colocación sobre la que se apoya |
| `PorcentajeSoporte` | `decimal(12,2)` | No |  | Porcentaje apoyado |
| `PesoEncimaKg` | `decimal(12,2)` | No |  | Peso encima (kg) |
| `EstadoEstabilidad` | `nvarchar(20)` | No |  | Resultado de estabilidad |
| `Accesibilidad` | `nvarchar(30)` | No |  | Resultado de accesibilidad |
| `Bloqueada` | `bit` | No |  | El usuario la fijó |
| `AjusteManual` | `bit` | No |  | El usuario la movió |
| `CodigoRechazo` | `nvarchar(30)` | Sí |  | Código si no se cargó |
| `RazonRechazo` | `nvarchar(500)` | Sí |  | Motivo si no se cargó |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de última modificación (auditoría) |

**Llaves e índices:** PK `PK_colocaciones_cargue` (Id); Índice `IX_colocaciones_cargue_ItemPlanCargueId` (ItemPlanCargueId); Índice `IX_colocaciones_cargue_PlanCargueId_Cargada` (PlanCargueId, Cargada).

**FK salientes:** `ItemPlanCargueId` → `tbl_items_plan_cargue.Id` (NO ACTION); `PlanCargueId` → `tbl_planes_cargue.Id` (ON DELETE CASCADE).  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- La genera el motor de colocación al optimizar; el usuario puede ajustar una colocación después.

---

# H. Auditoría

## 35. `tbl_audit_log`

**Entidad:** `clsAuditLog` — **Para qué sirve:** Rastro de solo inserción: quién cambió qué y cuándo, sobre cualquier entidad.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `UsuarioId` | `uniqueidentifier` | Sí |  | Quién ejecutó la acción |
| `Entidad` | `nvarchar(100)` | Sí |  | Entidad afectada |
| `EntidadId` | `uniqueidentifier` | Sí |  | Id del registro afectado |
| `Accion` | `nvarchar(50)` | Sí |  | Acción (`crear`, `actualizar`, `entregar`, `importar`, `ajustar`…) |
| `Diff` | `nvarchar(max)` | Sí |  | JSON con el antes y el después |
| `CreatedAt` | `datetimeoffset` | No |  | Momento del evento (no tiene `UpdatedAt`: nunca se modifica) |

**Llaves e índices:** PK `PK_audit_log` (Id); Índice `IX_audit_log_Entidad_EntidadId` (Entidad, EntidadId); Índice `IX_audit_log_UsuarioId` (UsuarioId).

**FK salientes:** `UsuarioId` → `tbl_usuarios.Id` (NO ACTION).  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- La escriben los casos de uso después de una operación exitosa (`clsEfAuditoriaRepository`, servicios conexos, tarifas de plaza, etc.).

---

# I. Inteligencia artificial

## 36. `tbl_ia_uso`

**Entidad:** `clsUsoIa` — **Para qué sirve:** Una fila por cada llamada a un modelo de lenguaje (Claude), con éxito o con error. Sirve para el gasto por función y por mes, el tope por usuario y por día, y la auditoría. **No guarda el contenido enviado ni la respuesta** (regla de seguridad de la IA): solo cifras y resultado.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `Funcion` | `nvarchar(40)` | No |  | Función de NEXO que llamó al modelo (ej. `ImportacionExcel`, `AsistenteCotizacion`, `ApoyoCubicador`) |
| `UsuarioId` | `uniqueidentifier` | Sí |  | Quién la pidió. `NULL` = proceso interno |
| `Modelo` | `nvarchar(80)` | No |  | Identificador del modelo que devolvió el proveedor |
| `TokensEntrada` | `int` | No | `0` | Tokens de entrada |
| `TokensSalida` | `int` | No | `0` | Tokens de salida |
| `TokensCacheLectura` | `int` | No | `0` | Tokens leídos del caché de prompt |
| `TokensCacheEscritura` | `int` | No | `0` | Tokens escritos en el caché de prompt |
| `CostoEstimadoUsd` | `decimal(12,6)` | Sí |  | Costo estimado con el precio vigente al momento de la llamada; no se recalcula. `NULL` si no se pudo estimar |
| `LatenciaMs` | `int` | No | `0` | Duración de la llamada (ms) |
| `Resultado` | `nvarchar(40)` | No |  | `ok` o el código de error (`IA_LIMITE_PROVEEDOR`, `IA_TIEMPO_AGOTADO`…) |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación (auditoría) |

**Llaves e índices:** PK `PK_ia_uso` (Id); Índice `IX_ia_uso_Funcion_CreatedAt` (Funcion, CreatedAt) — incluye Modelo, TokensEntrada, TokensSalida, CostoEstimadoUsd, Resultado; Índice `IX_ia_uso_UsuarioId_CreatedAt` (UsuarioId, CreatedAt) — incluye Funcion, TokensEntrada, TokensSalida, CostoEstimadoUsd.

**CHECK** `CK_ia_uso_cifras_no_negativas`: `([TokensEntrada]>=(0) AND [TokensSalida]>=(0) AND [TokensCacheLectura]>=(0) AND [TokensCacheEscritura]>=(0) AND [LatenciaMs]>=(0) AND ([CostoEstimadoUsd] IS NULL OR [CostoEstimadoUsd]>=(0)))`

**FK salientes:** `UsuarioId` → `tbl_usuarios.Id` (NO ACTION).  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- La escribe `clsAsistenteIaConRegistro` después de cada llamada; el gasto se consulta en `/api/ia/uso`.

---

## 37. `tbl_asistente_conversaciones`

**Entidad:** `clsConversacionAsistente` — **Para qué sirve:** Una conversación con el asistente de cotización, de quien la inicia. Sus mensajes van en `tbl_asistente_mensajes`.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `UsuarioId` | `uniqueidentifier` | No |  | Quién inició la conversación |
| `Titulo` | `nvarchar(120)` | No |  | Título de la conversación |
| `Turnos` | `int` | No | `0` | Cantidad de turnos (pregunta y respuesta) |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación |
| `UpdatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de la última actividad |

**Llaves e índices:** PK `PK_asistente_conversaciones` (Id); Índice `IX_asistente_conversaciones_UsuarioId_UpdatedAt` (UsuarioId, UpdatedAt).

**CHECK** `CK_asistente_conversaciones_turnos`: `([Turnos]>=(0))`

**FK salientes:** `UsuarioId` → `tbl_usuarios.Id` (NO ACTION).
**FK entrantes:** `tbl_asistente_mensajes.ConversacionId`, `tbl_asistente_herramientas_uso.ConversacionId`, `tbl_asistente_acciones.ConversacionId`, `tbl_asistente_preferencias.ConversacionId` (NO ACTION).

**De dónde salen los datos:** la escribe la aplicación cuando alguien conversa con el asistente. Sin datos iniciales.

---

## 38. `tbl_asistente_mensajes`

**Entidad:** `clsMensajeAsistente` — **Para qué sirve:** Los mensajes de cada conversación, en orden.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `ConversacionId` | `uniqueidentifier` | No |  | Conversación a la que pertenece |
| `Orden` | `int` | No |  | Posición del mensaje dentro de la conversación |
| `Rol` | `nvarchar(10)` | No |  | `user` (la persona) o `assistant` (el asistente) |
| `Contenido` | `nvarchar(max)` | No | `''` | Texto del mensaje |
| `Estado` | `nvarchar(20)` | No |  | `en_curso`, `completo`, `incompleto` o `descartado` |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación |

**Llaves e índices:** PK `PK_asistente_mensajes` (Id); Índice único `IX_asistente_mensajes_ConversacionId_Orden` (ConversacionId, Orden).

**CHECK** `CK_asistente_mensajes_rol`: `([Rol] IN ('user','assistant'))`; `CK_asistente_mensajes_estado`: `([Estado] IN ('en_curso','completo','incompleto','descartado'))`

**FK salientes:** `ConversacionId` → `tbl_asistente_conversaciones.Id` (NO ACTION).
**FK entrantes:** `tbl_asistente_herramientas_uso.MensajeId`, `tbl_asistente_acciones.MensajeId` (NO ACTION).

**De dónde salen los datos:** la escribe la aplicación. Sin datos iniciales.

---

## 39. `tbl_asistente_herramientas_uso`

**Entidad:** `clsUsoHerramientaAsistente` — **Para qué sirve:** Una fila por cada herramienta que usó el asistente para responder: qué consultó, qué obtuvo y de qué fuente. Es la prueba de dónde salió cada valor que mostró.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `ConversacionId` | `uniqueidentifier` | No |  | Conversación |
| `MensajeId` | `uniqueidentifier` | No |  | Mensaje del asistente que usó la herramienta |
| `Herramienta` | `nvarchar(60)` | No |  | Nombre de la herramienta |
| `Entrada` | `nvarchar(max)` | No |  | Parámetros de la consulta, en JSON |
| `Salida` | `nvarchar(max)` | Sí |  | Resultado, en JSON |
| `Encontrado` | `bit` | Sí |  | Si la herramienta encontró datos |
| `Fuente` | `nvarchar(200)` | Sí |  | Fuente del dato (por ejemplo, SICE-TAC o tarifa de plaza) |
| `Resultado` | `nvarchar(40)` | No |  | `ok` o el código de error |
| `LatenciaMs` | `int` | No | `0` | Duración de la consulta (ms) |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación |

**Llaves e índices:** PK `PK_asistente_herramientas_uso` (Id); Índice `IX_asistente_herramientas_uso_ConversacionId_CreatedAt` (ConversacionId, CreatedAt); Índice `IX_asistente_herramientas_uso_MensajeId` (MensajeId).

**CHECK** `CK_asistente_herramientas_uso_entrada_json`: `(isjson([Entrada])=(1))`; `CK_asistente_herramientas_uso_salida_json`: `([Salida] IS NULL OR isjson([Salida])=(1))`

**FK salientes:** `ConversacionId` → `tbl_asistente_conversaciones.Id`; `MensajeId` → `tbl_asistente_mensajes.Id` (NO ACTION).
**FK entrantes:** ninguna.

**De dónde salen los datos:** la escribe la aplicación. Sin datos iniciales.

---

## 40. `tbl_asistente_acciones`

**Entidad:** `clsAccionAsistente` — **Para qué sirve:** Cambios que el asistente propone y la persona confirma o cancela: agregar o editar un flete, agregar conexos, rechazar una línea, cambiar el vehículo o guardar una preferencia. El asistente nunca escribe por su cuenta; cada fila es también la auditoría del cambio.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `ConversacionId` | `uniqueidentifier` | No |  | Conversación |
| `MensajeId` | `uniqueidentifier` | No |  | Mensaje del asistente que propuso el cambio |
| `UsuarioId` | `uniqueidentifier` | No |  | Quién debe confirmarlo |
| `Tipo` | `nvarchar(40)` | No |  | `agregar_flete`, `editar_flete`, `agregar_conexos`, `rechazar_linea`, `cambiar_vehiculo` o `guardar_preferencia` |
| `Parametros` | `nvarchar(max)` | No |  | Datos del cambio, en JSON |
| `Resumen` | `nvarchar(500)` | No |  | Descripción del cambio para la persona |
| `Estado` | `nvarchar(20)` | No | `'pendiente'` | `pendiente`, `ejecutando`, `ejecutada`, `fallida`, `cancelada` o `vencida` |
| `VenceAt` | `datetimeoffset` | No |  | Hasta cuándo se puede confirmar |
| `ResueltaAt` | `datetimeoffset` | Sí |  | Cuándo se confirmó, canceló o venció |
| `ResultadoCodigo` | `nvarchar(60)` | Sí |  | Código del resultado |
| `ResultadoMensaje` | `nvarchar(500)` | Sí |  | Mensaje del resultado |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación |

**Llaves e índices:** PK `PK_asistente_acciones` (Id); Índice `IX_asistente_acciones_ConversacionId_CreatedAt` (ConversacionId, CreatedAt); Índice `IX_asistente_acciones_MensajeId` (MensajeId).

**CHECK** `CK_asistente_acciones_tipo` (valores de `Tipo`); `CK_asistente_acciones_estado` (valores de `Estado`); `CK_asistente_acciones_parametros_json`: `(isjson([Parametros])=(1))`

**FK salientes:** `ConversacionId` → `tbl_asistente_conversaciones.Id`; `MensajeId` → `tbl_asistente_mensajes.Id`; `UsuarioId` → `tbl_usuarios.Id` (NO ACTION).
**FK entrantes:** ninguna.

**De dónde salen los datos:** la escribe la aplicación. Sin datos iniciales.

---

## 41. `tbl_asistente_preferencias`

**Entidad:** `clsPreferenciaAsistente` — **Para qué sirve:** Reglas y preferencias propias de cada persona para el asistente. El asistente las recibe al inicio de sus conversaciones y nunca reemplazan las reglas del sistema.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `uniqueidentifier` | No |  | PK |
| `UsuarioId` | `uniqueidentifier` | No |  | Dueño de la preferencia |
| `Texto` | `nvarchar(300)` | No |  | La preferencia (mínimo 5 caracteres) |
| `ConversacionId` | `uniqueidentifier` | Sí |  | Conversación donde se guardó |
| `CreatedAt` | `datetimeoffset` | No | `sysdatetimeoffset` | Fecha de creación |

**Llaves e índices:** PK `PK_asistente_preferencias` (Id); Índice `IX_asistente_preferencias_UsuarioId_CreatedAt` (UsuarioId, CreatedAt).

**CHECK** `CK_asistente_preferencias_texto`: `(len(ltrim(rtrim([Texto])))>=(5))`

**FK salientes:** `UsuarioId` → `tbl_usuarios.Id`; `ConversacionId` → `tbl_asistente_conversaciones.Id` (NO ACTION).
**FK entrantes:** ninguna.

**De dónde salen los datos:** la escribe la aplicación. Sin datos iniciales.

---

# J. Mensajería e infraestructura

## 42. `InboxState`

**Entidad:** (MassTransit, `AddInboxStateEntity`) — **Para qué sirve:** Deduplicación de mensajes entrantes por consumidor: evita procesar dos veces un mensaje que el broker reentrega. Vive en esta base porque la atomicidad depende de guardar el mensaje y los datos en la misma transacción.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `Id` | `bigint` | No | IDENTITY(1,1) | PK autoincremental |
| `MessageId` | `uniqueidentifier` | No |  | Id del mensaje |
| `ConsumerId` | `uniqueidentifier` | No |  | Id del consumidor |
| `LockId` | `uniqueidentifier` | No |  | Bloqueo de MassTransit |
| `RowVersion` | `rowversion` | Sí |  | Control de concurrencia optimista: si dos personas guardan a la vez, la segunda recibe 409 |
| `Received` | `datetime2` | No |  | Cuándo llegó |
| `ReceiveCount` | `int` | No |  | Veces recibido |
| `ExpirationTime` | `datetime2` | Sí |  | Vencimiento |
| `Consumed` | `datetime2` | Sí |  | Cuándo se consumió |
| `Delivered` | `datetime2` | Sí |  | Cuándo se entregó |
| `LastSequenceNumber` | `bigint` | Sí |  | Último número de secuencia |

**Llaves e índices:** PK `PK_InboxState` (Id); Único `AK_InboxState_MessageId_ConsumerId` (MessageId, ConsumerId); Índice `IX_InboxState_Delivered` (Delivered).

**FK salientes:** ninguna.  
**FK entrantes:** `OutboxMessage.InboxMessageId, InboxConsumerId` (NO ACTION).

**De dónde salen los datos:**
- La administra MassTransit (Transactional Inbox). No se edita a mano.

---

## 43. `OutboxMessage`

**Entidad:** (MassTransit, `AddOutboxMessageEntity`) — **Para qué sirve:** Cola transaccional de mensajes salientes: se escriben en la misma transacción del caso de uso y un proceso aparte los publica al broker.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `SequenceNumber` | `bigint` | No | IDENTITY(1,1) | PK autoincremental |
| `EnqueueTime` | `datetime2` | Sí |  | Cuándo se encoló |
| `SentTime` | `datetime2` | No |  | Cuándo se envió |
| `Headers` | `nvarchar(max)` | Sí |  | Encabezados (JSON) |
| `Properties` | `nvarchar(max)` | Sí |  | Propiedades (JSON) |
| `InboxMessageId` | `uniqueidentifier` | Sí |  | Mensaje entrante que lo originó |
| `InboxConsumerId` | `uniqueidentifier` | Sí |  | Consumidor que lo originó |
| `OutboxId` | `uniqueidentifier` | Sí |  | Outbox al que pertenece |
| `MessageId` | `uniqueidentifier` | No |  | Id del mensaje |
| `ContentType` | `nvarchar(256)` | No |  | Tipo de contenido |
| `MessageType` | `nvarchar(max)` | No |  | Tipo de mensaje |
| `Body` | `nvarchar(max)` | No |  | Cuerpo del mensaje |
| `ConversationId` | `uniqueidentifier` | Sí |  | Conversación |
| `CorrelationId` | `uniqueidentifier` | Sí |  | Correlación |
| `InitiatorId` | `uniqueidentifier` | Sí |  | Iniciador |
| `RequestId` | `uniqueidentifier` | Sí |  | Solicitud |
| `SourceAddress` | `nvarchar(256)` | Sí |  | Origen |
| `DestinationAddress` | `nvarchar(256)` | Sí |  | Destino |
| `ResponseAddress` | `nvarchar(256)` | Sí |  | Dirección de respuesta |
| `FaultAddress` | `nvarchar(256)` | Sí |  | Dirección de fallas |
| `ExpirationTime` | `datetime2` | Sí |  | Vencimiento |

**Llaves e índices:** PK `PK_OutboxMessage` (SequenceNumber); Único `IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber` (InboxMessageId, InboxConsumerId, SequenceNumber) — filtrado `WHERE [InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL`; Único `IX_OutboxMessage_OutboxId_SequenceNumber` (OutboxId, SequenceNumber) — filtrado `WHERE [OutboxId] IS NOT NULL`; Índice `IX_OutboxMessage_EnqueueTime` (EnqueueTime); Índice `IX_OutboxMessage_ExpirationTime` (ExpirationTime).

**FK salientes:** `InboxMessageId, InboxConsumerId` → `InboxState.MessageId, ConsumerId` (NO ACTION); `OutboxId` → `OutboxState.OutboxId` (NO ACTION).  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- La administra MassTransit (Transactional Outbox). No se edita a mano.

---

## 44. `OutboxState`

**Entidad:** (MassTransit, `AddOutboxStateEntity`) — **Para qué sirve:** Un outbox por unidad lógica de trabajo (por ejemplo una saga): agrupa y ordena sus `OutboxMessage`.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `OutboxId` | `uniqueidentifier` | No |  | PK |
| `LockId` | `uniqueidentifier` | No |  | Bloqueo de MassTransit |
| `RowVersion` | `rowversion` | Sí |  | Control de concurrencia optimista: si dos personas guardan a la vez, la segunda recibe 409 |
| `Created` | `datetime2` | No |  | Cuándo se creó |
| `Delivered` | `datetime2` | Sí |  | Cuándo se entregó |
| `LastSequenceNumber` | `bigint` | Sí |  | Último número de secuencia |

**Llaves e índices:** PK `PK_OutboxState` (OutboxId); Índice `IX_OutboxState_Created` (Created).

**FK salientes:** ninguna.  
**FK entrantes:** `OutboxMessage.OutboxId` (NO ACTION).

**De dónde salen los datos:**
- La administra MassTransit. No se edita a mano.

---

## 45. `__EFMigrationsHistory`

**Entidad:** (EF Core) — **Para qué sirve:** Libro de control de EF Core. El proyecto no usa migraciones (el esquema es `ESQUEMA_NEXO.sql`), pero la tabla existe en la base y el script la crea para que EF no intente migrar.

| Columna | Tipo | Nullable | Default | Descripción |
|---|---|---|---|---|
| `MigrationId` | `nvarchar(150)` | No |  | PK. Nombre de la migración |
| `ProductVersion` | `nvarchar(32)` | No |  | Versión de EF Core |

**Llaves e índices:** PK `PK___EFMigrationsHistory` (MigrationId).

**FK salientes:** ninguna.  
**FK entrantes:** ninguna.

**De dónde salen los datos:**
- No se edita a mano.

---

# Apéndice: enums

Todos se guardan como texto (el nombre del valor), no como número.

| Enum | Valores | Dónde se usa |
|---|---|---|
| `RolUsuario` | `AdminBiblioteca`, `LiderProceso`, `ServiciosLogisticos`, `Admin`, `Comercial`, `AdminTarifas` | `tbl_usuarios.Rol`, `tbl_mapeo_rol_portal.Rol`. Comercial y AdminTarifas son los roles de negocio; Admin es el superusuario técnico. `AdminBiblioteca` reemplazó a `CoordinadorFletes` |
| `TipoSolicitud` | `Spot`, `Licitacion` | `tbl_solicitudes.Tipo` |
| `ModalidadSolicitud` | `Importacion`, `Exportacion`, `Nacional`, `Extradimensionada`, `CargaGeneral` | `tbl_solicitudes.Modalidad` |
| `EstadoSolicitud` | `Pendiente`, `EnCosteo`, `Entregada`, `Descartada` | `tbl_solicitudes.Estado` |
| `TipologiaCarga` | `Contenedor`, `CargaSuelta`, `GranelSolido`, `GranelLiquido`, `Alimentos`, `Refrigerados`, `CargaSeca` | `tbl_lineas_solicitud.Tipologia`, `tbl_tarifas_historicas.Tipologia` |
| `LineaNegocio` | `PuertoAInterior`, `InteriorAPuerto`, `Nacional`, `DevolucionContenedor`, `Andino`, `Licitaciones` | `tbl_lineas_solicitud.LineaNegocio` (catálogo SAP TM) |
| `ClaseViaje` | `Contenedor`, `Cupo`, `CargaSuelta`, `Extradimensionado` | `tbl_lineas_solicitud.ClaseViaje` (catálogo SAP TM) |
| `EstadoLinea` | `PorCostear`, `AutoResuelta`, `Revisada`, `Rechazada` | `tbl_lineas_solicitud.EstadoLinea`. `Rechazada`: el Administrador de Tarifas la dejó sin costear a propósito, con motivo |
| `TipoPendienteDeCosteo` | `Tecnica`, `Manual` | `tbl_lineas_pendientes_de_costeo.Tipo`. `Tecnica` se reintenta y tiene reloj; `Manual` espera al Administrador de Tarifas, sin reloj |
| `CondicionCarga` | `Cargado`, `Vacio` | Consultas a SICE-TAC (ya no hay tabla local) |
| `FuenteFlete` | `Sicetac`, `Historica`, `Manual`, `Tarifario` | `tbl_cotizacion_lineas.Fuente` |
| `EstadoCotizacion` | `Borrador`, `Entregada` | `tbl_cotizaciones.Estado` |
| `ResultadoCotizacion` | `SinDefinir`, `Ganada`, `Perdida` | `tbl_cotizaciones.Resultado` |
