# Diccionario de datos — Base de datos `CabinaFletes`

Descripción completa de las 24 tablas del sistema: qué contienen, qué significa cada columna, para qué sirven, sus llaves foráneas, y de dónde salen sus datos en la práctica (entrada manual, importación, generación automática, o siembra de demostración).

**Cómo usar este documento:** está organizado por área de negocio, en el orden en que probablemente tenga sentido explorarlo en DBeaver. Si tienes una duda puntual sobre algo que no quede claro aquí, pregúntame directamente.

---

## Índice

**A. Infraestructura y catálogos base**
1. [`usuarios`](#1-usuarios)
2. [`clientes`](#2-clientes)
3. [`rutas`](#3-rutas)
4. [`configuraciones_vehiculo`](#4-configuraciones_vehiculo)

**B. El núcleo del negocio: solicitudes**
5. [`solicitudes`](#5-solicitudes)
6. [`lineas_solicitud`](#6-lineas_solicitud)

**C. Tarifas (de dónde sale el precio)**
7. [`tarifas_sicetac`](#7-tarifas_sicetac)
8. [`tarifas_historicas`](#8-tarifas_historicas)

**D. Cotización (el precio que se propone al cliente)**
9. [`cotizaciones`](#9-cotizaciones)
10. [`cotizacion_lineas`](#10-cotizacion_lineas)
11. [`cotizacion_conexos`](#11-cotizacion_conexos)
12. [`servicios_conexos`](#12-servicios_conexos)

**E. Reglas de negocio y configuración**
13. [`ajustes_vigencia_plaza`](#13-ajustes_vigencia_plaza)
14. [`reglas_solo_sicetac`](#14-reglas_solo_sicetac)
15. [`parametros_score`](#15-parametros_score)

**F. Auditoría**
16. [`audit_log`](#16-audit_log)

**G. Cubicador 3D**
17. [`vehiculos_cubicacion`](#17-vehiculos_cubicacion)
18. [`planes_cargue`](#18-planes_cargue)
19. [`items_plan_cargue`](#19-items_plan_cargue)
20. [`colocaciones_cargue`](#20-colocaciones_cargue)

**H. Catálogos agregados después (post-renombrado a Nexo)**
21. [`municipios`](#21-municipios)
22. [`pesos`](#22-pesos)
23. [`tarifas_plaza`](#23-tarifas_plaza)
24. [`tarifas_plaza_historico`](#24-tarifas_plaza_historico)

**[Apéndice: todos los enums](#apéndice-enums-usados-en-todo-el-sistema)**

---

# A. Infraestructura y catálogos base

## 1. `usuarios`

**Entidad:** `Usuario` — **Para qué sirve:** las personas internas de Botero Soto que operan el sistema (coordinadores de fletes, comerciales, administradores). Toda solicitud, cotización o ajuste queda vinculado a un usuario como autor.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `Id` | Guid | No | PK |
| `Nombre` | texto(200) | No | Nombre completo |
| `Email` | texto(256) | No | Correo corporativo, único |
| `Rol` | texto(40) | No | Uno de `RolUsuario` (ver apéndice) |
| `AdObjectId` | texto(100) | Sí | Vínculo con el directorio corporativo (Azure AD); dato interno, no se expone en la API |
| `Activo` | booleano | No | Default `true` |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |

**FK entrantes** (otras tablas apuntan a esta): `Solicitud.CreadoPor`, `Cotizacion.EntregadaPor`, `ServicioConexo.ActualizadoPor`, `AuditLog.UsuarioId` — todas `Restrict` (no se puede borrar un usuario referenciado).

**De dónde salen los datos:**
- No hay ningún endpoint para crear/editar usuarios manualmente.
- Se siembra un usuario "Sistema" fijo (vía migración `HasData`) para que las FKs tengan a quién apuntar mientras no haya autenticación real.
- `DataSeeder.cs` siembra 10 usuarios de demostración.
- La intención de diseño es poblarla vía **Azure AD**, pero eso todavía no está implementado — hoy toda petición se autentica como el usuario "Sistema" (header `X-Rol` en desarrollo).

---

## 2. `clientes`

**Entidad:** `Cliente` — **Para qué sirve:** el dueño de la carga para el cual se cotiza el flete.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `Id` | Guid | No | PK |
| `Nombre` | texto(200) | No | Nombre comercial |
| `Nit` | texto(20) | No | NIT colombiano (5-15 dígitos + dígito de verificación opcional), único |
| `RazonSocial` | texto(250) | Sí | Razón social completa |
| `Telefonos` | JSON | Sí | Lista de teléfonos |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |

**FK:** ninguna saliente. Referenciada por `Solicitud.ClienteId` (`Restrict`).

**De dónde salen los datos:**
- No hay endpoint de alta manual directa.
- **Se crea automáticamente** cuando alguien captura una solicitud indicando el código de cliente (BP) de SAP: el sistema consulta el Data Warehouse corporativo (solo lectura) y, si el cliente no existe localmente por NIT, lo copia a esta tabla.
- `DataSeeder.cs` siembra 10 clientes de demostración (Postobón, Nutresa, Argos, etc.).

---

## 3. `rutas`

**Entidad:** `Ruta` — **Para qué sirve:** catálogo de pares origen-destino (texto libre), reutilizado por líneas de solicitud y tarifas.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `Id` | Guid | No | PK |
| `Origen` | texto(150) | No | Ciudad/lugar de origen |
| `Destino` | texto(150) | No | Ciudad/lugar de destino |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |

Índice único: `(Origen, Destino)` — no se duplican rutas.

**FK:** ninguna saliente. Referenciada (`Restrict`) por `LineaSolicitud.RutaId`, `TarifaSicetac.RutaId`, `TarifaHistorica.RutaId`.

**De dónde salen los datos:**
- No hay endpoint de alta manual dedicado.
- **Se crea dinámicamente** al capturar una solicitud: si el origen/destino en texto libre no coincide con una ruta existente (comparación normalizada), se crea sola.
- La importación de tarifas SICE-TAC **enlaza** rutas existentes por nombre, pero explícitamente **no crea** rutas nuevas huérfanas.
- `DataSeeder.cs` siembra 12 rutas de demostración.

---

## 4. `configuraciones_vehiculo`

**Entidad:** `ConfiguracionVehiculo` — **Para qué sirve:** catálogo de tipos/configuraciones de vehículo de carga (ej. "3S3" + "Furgón Refrigerado") — **no confundir con `vehiculos_cubicacion`**, que es un catálogo distinto usado solo por el Cubicador 3D.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `Id` | Guid | No | PK |
| `TipoVehiculo` | texto(100) | No | Código/tipo (ej. "C2", "3S3") |
| `Carroceria` | texto(100) | Sí | Estacas, Furgón, Plataforma, etc. |
| `RangoPeso` | texto(50) | Sí | Rango de peso admisible, texto libre |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |

**FK:** ninguna saliente. Referenciada (`Restrict`) por `LineaSolicitud.ConfigVehiculoId`, `TarifaHistorica.ConfigVehiculoId`.

**De dónde salen los datos:**
- Igual que `rutas`: **se crea dinámicamente** al capturar una solicitud si el tipo de vehículo (texto libre) no existe todavía.
- `DataSeeder.cs` siembra 10 configuraciones de demostración.

---

# B. El núcleo del negocio: solicitudes

## 5. `solicitudes`

**Entidad:** `Solicitud` — **Para qué sirve:** la cabecera de cada pedido de cotización de un cliente (spot puntual, o pliego de licitación).

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `Id` | Guid | No | PK |
| `ClienteId` | Guid | No | FK → `clientes` |
| `Tipo` | texto | No | `Spot` o `Licitacion` (ver enum) |
| `Modalidad` | texto | Sí | `Importacion`/`Exportacion`/`Nacional`/`Extradimensionada`/`CargaGeneral` |
| `CondicionesServicio` | JSON | Sí | Referencia del pliego, horas de cargue/descargue, motivo de rechazo si aplica |
| `Deadline` | fecha | Sí | Fecha límite de respuesta — **el único dato real que mueve el score de prioridad** |
| `ProbabilidadCierre` | decimal | Sí | 0-100 o 0-1; solo referencia visual |
| `ValorEstimado` | decimal | Sí | Valor económico en COP; solo referencia visual |
| `ScorePrioridad` | decimal | No | 0-100, calculado (ver tabla 15, `parametros_score`) |
| `Estado` | texto | No | `Pendiente → EnCosteo → Entregada/Descartada` |
| `CreadoPor` | Guid | No | FK → `usuarios` |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |
| `RowVersion` | binario | Sí | Control de concurrencia optimista (409 si dos personas editan a la vez) |

**FK:** `ClienteId` → `clientes` (`Restrict`), `CreadoPor` → `usuarios` (`Restrict`). Sus líneas (`LineaSolicitud`) se borran en cascada si se borra la solicitud (única cascada de esta tabla).

**De dónde salen los datos:** entrada manual, por 2 caminos:
- Formulario simple (`POST /api/solicitudes`) — crea la cabecera sin dimensiones todavía.
- Captura asistida con Excel (`POST /api/captura-solicitudes`) — el usuario sube el Excel del cliente, el sistema lo interpreta, el usuario confirma, y se crea la solicitud completa con sus líneas.
- `DataSeeder.cs` siembra 17 solicitudes de demostración (las que ya viste en tu base).

---

## 6. `lineas_solicitud`

**Entidad:** `LineaSolicitud` — **Para qué sirve:** cada combinación concreta a costear dentro de una solicitud (1 para spot, hasta ~500 para un pliego de licitación).

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `SolicitudId` | Guid | No | FK → `solicitudes` (cascada) |
| `RutaId` | Guid | Sí | FK → `rutas` |
| `ConfigVehiculoId` | Guid | Sí | FK → `configuraciones_vehiculo` |
| `Tipologia` | texto | Sí | Tipo físico de la carga (ver enum) |
| `LineaNegocio`/`ClaseViaje`/`ClaseContenedor` | texto | Sí | Catálogos alineados con SAP TM, para poder cruzar tarifas oficiales |
| `Exclusivo` | booleano | No | Vehículo exclusivo para el cliente |
| `Consolidado` | booleano | No | Carga consolidada |
| `RetornaContenedor` | booleano | No | Si incluye devolver el contenedor vacío |
| `CiudadDevolucionContenedor` | texto | Sí | A dónde se devuelve |
| `LevanteContenedorDiferente` | booleano | No | Si el contenedor se recoge en un punto distinto al origen |
| `CiudadLevanteContenedor` | texto | Sí | Dónde se recoge |
| `ValorAprovisionamiento` | decimal | Sí | Costo de reposicionar el contenedor vacío hasta el origen (piso regulado) |
| `Peso` | decimal | Sí | Peso declarado |
| `Dimensiones` | JSON | Sí | Lista de posiciones de carga (descripción, cantidad, L/A/H en cm, peso) — alimenta la Mesa de Costeo **y** el Cubicador 3D |
| `ValorMercancia` | decimal | Sí | Valor de la mercancía |
| `EstadoLinea` | texto | No | `PorCostear → AutoResuelta / Revisada` |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |

**FK:** `SolicitudId` → `solicitudes` (**Cascade**), `RutaId` → `rutas` (`Restrict`), `ConfigVehiculoId` → `configuraciones_vehiculo` (`Restrict`).

**De dónde salen los datos:** se crean junto con la solicitud en el mismo flujo de captura (manual o vía Excel) — no tienen un endpoint de alta independiente. `DataSeeder.cs` las siembra junto con las 17 solicitudes.

---

# C. Tarifas (de dónde sale el precio)

## 7. `tarifas_sicetac`

**Entidad:** `TarifaSicetac` — **Para qué sirve:** caché local del **piso regulatorio SICE-TAC** (Ministerio de Transporte) — el flete mínimo legal de referencia por ruta/vehículo/tipo de carga.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `SiceId` | long | Sí | Id del registro en la fuente — usado para idempotencia del import |
| `Periodo` | int | No | Formato AAAAMM — vigencia mensual |
| `RutaId` | Guid | Sí | FK → `rutas` |
| `OrigenDane`/`DestinoDane` | texto(12) | No | Código DANE de origen/destino |
| `NombreRuta` | texto(300) | Sí | Texto "ORIGEN - DESTINO" tal como viene del Excel |
| `Configuracion` | texto(50) | No | Configuración vehicular SICE (ej. "3S3") |
| `TipoCarga` | texto(80) | No | General, Granel Sólido, etc. |
| `UnidadTransporte` | texto(80) | No | Estibas, Furgón, etc. |
| `CondicionCarga` | texto(20) | No | `Cargado` / `Vacio` |
| `ValorPiso` | decimal | No | **El flete mínimo — el dato clave que se muestra al coordinador** |
| `ValorHora` | decimal | Sí | Valor de hora de tiempo logístico |
| `HorasPactadas`/`HorasRecorrido` | decimal | Sí | Horas logísticas/de viaje |
| `ValorSiceHora` | decimal | Sí | `ValorPiso + ValorHora × HorasPactadas` (referencia) |
| `ViaEstandar` | booleano | Sí | Si la vía es estándar |
| `ImportadoAt`/`ImportadoPor` | fecha/Guid | No/Sí | Cuándo y quién importó este registro |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |

Llave natural única: `(Periodo, OrigenDane, DestinoDane, Configuracion, TipoCarga, UnidadTransporte, CondicionCarga)`.

**FK:** `RutaId` → `rutas` (`Restrict`).

**De dónde salen los datos:** **importación masiva de Excel** (`POST /api/tarifas-sicetac/importar`) — es un *upsert* idempotente: filas nuevas se insertan, existentes se actualizan; cada import queda registrado en `audit_log`. `DataSeeder.cs` siembra 12 filas de demostración.

**Enlace con `rutas` — solo si ya existe, nunca se crea una nueva:** al importar, cada fila trae `NOMBRERUTA` ("ORIGEN - DESTINO") y el sistema busca una `Ruta` local ya existente con ese mismo origen/destino para completar `RutaId`. **Si no la encuentra, NO crea una ruta nueva** — la tarifa queda igual insertada/actualizada, pero con `RutaId = null`, y por lo tanto invisible en la Mesa de Costeo hasta que esa ruta exista (típicamente porque alguien capturó una solicitud real con ese origen/destino). Esto es deliberado: evita llenar `rutas` de filas "huérfanas" con el texto tal cual viene del Excel, que podría no coincidir exactamente con cómo se escribe la misma ruta al capturar una solicitud a mano.

**No confundir con la "Biblioteca SICE-TAC" (fuera de esta base de datos):** existe una segunda vía, totalmente separada, para consultar SICE-TAC — una **consulta en vivo, de solo lectura**, contra `DWSice.dbo.tblRutasSicetac` en el Data Warehouse corporativo (tabla con ~3.8 millones de filas, actualizada por un ETL mensual, fuera de nuestro control). Esa consulta **no toca ni la tabla `tarifas_sicetac` ni `rutas`** — es una pantalla de búsqueda pura sobre la matriz oficial completa, sin persistir ni cruzar nada localmente. Son dos features independientes que comparten el nombre "SICE-TAC" pero no la tabla ni la lógica.

---

## 8. `tarifas_historicas`

**Entidad:** `TarifaHistorica` — **Para qué sirve:** el banco de **precios reales ya ganados** ("Biblioteca de Tarifas Ganadas") que el sistema sugiere para nuevas líneas parecidas.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `RutaId` | Guid | Sí | FK → `rutas` |
| `ConfigVehiculoId` | Guid | Sí | FK → `configuraciones_vehiculo` |
| `Tipologia` | texto | Sí | Tipo de carga |
| `Valor` | decimal | No | Valor del flete ganado/histórico |
| `Fecha` | fecha | No | Fecha de la tarifa |
| `CotizacionId` | Guid | Sí | Cotización ganada que la originó — `NULL` = tarifa legada sin cotización de origen |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |

**FK:** `RutaId` (`Restrict`), `ConfigVehiculoId` (`Restrict`), `CotizacionId` → `cotizaciones` (`Restrict`).

**De dónde salen los datos:** **generación 100% automática** — cuando una `Cotizacion` se marca `Ganada` **por primera vez**, el sistema crea una fila aquí por cada línea de flete de esa cotización (copiando ruta, vehículo, tipología y valor). No hay ningún endpoint de alta manual. `DataSeeder.cs` siembra 12 filas de demostración (algunas ligadas a cotizaciones del seed, otras representando "tarifario legado" sin cotización).

---

# D. Cotización (el precio que se propone al cliente)

## 9. `cotizaciones`

**Entidad:** `Cotizacion` — **Para qué sirve:** el documento de costeo/venta consolidado (flete + servicios conexos) que se arma para una solicitud y se entrega al comercial.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `SolicitudId` | Guid | No | FK → `solicitudes` |
| `Estado` | texto | No | `Borrador` (default) / `Entregada` |
| `Resultado` | texto | No | `SinDefinir` (default) / `Ganada` / `Perdida` |
| `Total` | decimal | No | Suma de fletes + aprovisionamiento + retorno contenedor + conexos, recalculada automáticamente |
| `EntregadaPor`/`EntregadaAt` | Guid/fecha | Sí | Quién y cuándo se entregó |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |
| `RowVersion` | binario | Sí | Control de concurrencia optimista |

**FK:** `SolicitudId` → `solicitudes` (`Restrict`), `EntregadaPor` → `usuarios` (`Restrict`). Sus líneas y conexos se borran en cascada.

**De dónde salen los datos:** 100% vía API/aplicación — crear, entregar, y marcar resultado (`Ganada`/`Perdida`) son acciones explícitas del usuario. `DataSeeder.cs` siembra 10 cotizaciones de demostración en distintos estados.

---

## 10. `cotizacion_lineas`

**Entidad:** `CotizacionLinea` — **Para qué sirve:** cada renglón de flete costeado dentro de una cotización.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `CotizacionId` | Guid | No | FK → `cotizaciones` (cascada) |
| `LineaSolicitudId` | Guid | Sí | FK → `lineas_solicitud` (puede ser un flete manual sin línea asociada) |
| `ValorFlete` | decimal | No | Valor del flete costeado |
| `ValorAprovisionamiento` | decimal | Sí | Reposicionar contenedor vacío (concepto separado que suma al total) |
| `ValorRetornoContenedor` | decimal | Sí | Devolución del contenedor vacío (concepto separado que suma al total) |
| `Fuente` | texto | No | De dónde salió el valor (ver enum `FuenteFlete`) |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |

**FK:** `CotizacionId` → `cotizaciones` (**Cascade**), `LineaSolicitudId` → `lineas_solicitud` (`Restrict`).

**De dónde salen los datos:**
- Entrada manual: el usuario agrega un flete a la cotización.
- Generación semi-automática: al "aplicar propuestas", el sistema precarga como flete el valor más alto propuesto entre plaza y SICE-TAC en cada línea sin flete todavía.
- `DataSeeder.cs` las siembra junto con las cotizaciones de demostración.

---

## 11. `cotizacion_conexos`

**Entidad:** `CotizacionConexo` — **Para qué sirve:** cada servicio adicional (cargue, descargue, escolta, etc.) aplicado a una cotización, con su cantidad y valor calculado.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `CotizacionId` | Guid | No | FK → `cotizaciones` (cascada) |
| `ServicioConexoId` | Guid | Sí | FK → `servicios_conexos` |
| `Cantidad` | decimal | No | Toneladas, horas, puntos, etc. según la regla del servicio |
| `ValorAplicado` | decimal | No | `ValorBase × Cantidad`, o valor manual si se especifica |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |

**FK:** `CotizacionId` → `cotizaciones` (**Cascade**), `ServicioConexoId` → `servicios_conexos` (`Restrict`).

**De dónde salen los datos:** entrada manual (el usuario agrega servicios a la cotización). `DataSeeder.cs` las siembra junto con las cotizaciones de demostración.

---

## 12. `servicios_conexos`

**Entidad:** `ServicioConexo` — **Para qué sirve:** catálogo de servicios adicionales al flete (cargue, descargue, almacenamiento, ITR, escolta, candado satelital, acompañamiento vehicular, etc.), mantenido por el área de Servicios Logísticos.

> **Reestructurada en S4-HU-15.1:** antes era un catálogo de valor único (`Nombre`+`ValorBase`+`ReglaCobro`). Ahora unifica en una sola tabla dos categorías con dimensiones de precio distintas: **Servicios Logísticos** (Almacenamiento por ciudad; ITR por puerto + tipo de máquina + movimiento) y **Servicios Adicionales** (por ruta, o por origen-destino según el servicio, ej. acompañamiento vehicular). Los 10 servicios ya sembrados de valor único (Cargue, Descargue, Stand-by, Candado satelital, Almacenamiento, Distribución multipunto, ITR, Escolta, Reexpedición, Montacargas) siguen funcionando: son su propia variante "genérica" dentro de esta misma tabla, con `'NO APLICA'` en las columnas de dimensión que no les aplican.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `CodigoServicio` | texto(30) | No | Código corto del servicio (ej. `PRE_BSAT`, `AVT_AUTO`); si el servicio no trae un código propio, se usa el mismo valor de `TipoServicio` |
| `TipoServicio` | texto(120) | No | Nombre/descripción del servicio (antes `Nombre`) |
| `Origen` | texto(150) | No | Ciudad (logísticos) u origen (adicionales). Texto plano, NO FK a `municipios`: nombre canónico si cruza con un municipio real, texto crudo del Excel si es un peaje/corredor sin equivalente. Default `'NO APLICA'` |
| `Destino` | texto(150) | No | Solo aplica al servicio de acompañamiento vehicular (par origen-destino); misma regla que `Origen`. Default `'NO APLICA'` |
| `Puerto` | texto(50) | No | Terminal del puerto (solo ITR, ej. `Tcbuen - Sprbun`, `Aguadulce`). Default `'NO APLICA'` |
| `Ruta` | texto(50) | No | Tipo de ruta (solo servicios por ruta, ej. `Urbano`, `Colombia a Ecuador`). Default `'NO APLICA'` |
| `TipoMaquina` | texto(60) | No | Máquina + cantidad de movimientos combinados (solo ITR, ej. `Maquina 3.5 - 2 Movimientos`). Default `'NO APLICA'` |
| `CostoUnitario` | decimal | No | Valor unitario (antes `ValorBase`) |
| `Unidad` | texto(30) | No | Unidad de cobro (`Por estiba` en logísticos, `Por unidad` en el resto por ahora) |
| `Vigencia` | fecha | No | Fecha de vigencia del valor pactado (antes `VigenteHasta`) |
| `Categoria` | texto(20) | No | `"logistico"` (default) o `"adicional"` |
| `Activo` | booleano | No | Default `true` |
| `ActualizadoPor` | Guid | Sí | FK → `usuarios` |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |

**FK:** `ActualizadoPor` → `usuarios` (`Restrict`). Sin FK a `municipios` (`Origen`/`Destino` son texto plano, ver nota arriba).

**Único:** `(CodigoServicio, Origen, Destino, Puerto, Ruta, TipoMaquina)` — evita variantes de precio duplicadas para la misma combinación de dimensiones.

**De dónde salen los datos:**
- Entrada manual (crear/editar desde la pantalla de servicios conexos).
- **Importación masiva de Excel** (por servicio, según sus dimensiones aplicables).
- `DataSeeder.cs` sigue sembrando 10 servicios de demostración para ambientes locales/tests (ver nota de reestructuración arriba).
- **Seeder real (S4-HU-15.1):** [`docs/Database/seeder/servicios_conexos.csv`](./Database/seeder/README.md) — 1346 filas construidas a partir de `docs/docs_raw/conexos costos.xlsx` (2 hojas, 4 tablas) + los 10 servicios de valor único ya existentes, cruzando ciudades contra `tbl_municipios`.

---

# E. Reglas de negocio y configuración

## 13. `ajustes_vigencia_plaza`

**Entidad:** `AjusteVigenciaPlaza` — **Para qué sirve:** corrección local de la fecha de vigencia de una tarifa de "plaza" de SAP TM. Como la fuente original (SAP, en otra base de datos) es de solo lectura, aquí se guarda el override.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `TarifaPlazaId` | int | No | Id de la fila de la tarifa SAP a la que aplica (único) |
| `VigenciaAjustada` | fecha | No | Nueva vigencia efectiva |
| `VigenciaOriginal` | fecha | Sí | Vigencia original de SAP al momento del ajuste (auditoría) |
| `Motivo` | texto(300) | Sí | Motivo del ajuste |
| `AjustadoPor` | Guid | Sí | Usuario que hizo el ajuste (sin relación FK formal declarada) |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |

**FK:** ninguna configurada formalmente en el DbContext (la tarifa fuente vive en otra base de datos externa).

**De dónde salen los datos:** entrada manual — ajuste individual o masivo (por fecha fija o "extender X días/meses") desde la pantalla de biblioteca SAP plaza. No tiene siembra de demostración.

> **Actualización 2026-08-28 (rama `32-s2-hu-092-set-de-datos-provisionales-realistas-validado-por-el-negocio`, S2-HU-09.2):**
> se agregó [`docs/tarifas-plaza-provisionales/`](./tarifas-plaza-provisionales/README.md), un dataset curado de **210 filas reales** (150 "núcleo" utilizable + 60 casos borde) extraídas directamente de la fuente SAP (`dbSolucionesTI.dbo.tblSAPTarifas`), motivado por el hallazgo de que el **95.8%** de esa tabla ya está vencida hoy. **Esto no es un cambio de esquema ni de datos en esta tabla:** es un archivo JSON/Excel fuera de la base de datos, con su propio test de integridad, pensado como insumo para una futura fuente `FuenteFlete.Provisional` (issue #33, aún no construida). Hoy `ReglaDeCosteo`/`PropuestaTarifaAdapter` no leen este dataset, y `ajustes_vigencia_plaza` sigue siendo, como antes, el único mecanismo real en base de datos para corregir vigencias de plaza.

---

## 14. `reglas_solo_sicetac`

**Entidad:** `ReglaSoloSicetac` — **Para qué sirve:** regla que el Administrador de Fletes define para que, en una combinación origen+destino+tipo de vehículo, la propuesta de tarifa se resuelva **solo** con el piso SICE-TAC (sin comparar contra otras fuentes), auto-costeando esas líneas de inmediato.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `Origen`/`Destino` | texto(150) | No | Ciudades, normalizadas — **⚠️ desde S4-HU-10.1, las 348 filas sembradas por `docs/Database/seeder/reglas_solo_sicetac.csv` usan código DANE (ej. `76109000`) en vez de texto libre de ciudad; el resto del sistema/matching de reglas puede seguir esperando texto — revisar antes de confiar en el match** |
| `TipoVehiculo` | texto(40) | No | Tipo/configuración de vehículo |
| `CreadoPor` | Guid | Sí | Usuario que la creó |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |

Índice único: `(Origen, Destino, TipoVehiculo)`.

**FK:** ninguna relacional formal.

**De dónde salen los datos:** entrada manual, o **importación masiva de Excel** (columnas Origen/Destino/Tipo de vehículo). No tiene siembra de demostración — salvo las 348 filas de S4-HU-10.1 (ver `docs/Database/seeder/README.md`).

---

## 15. `parametros_score`

**Entidad:** `ParametrosScore` — **Para qué sirve:** fila única de configuración del algoritmo de score de prioridad de solicitudes.

| Columna | Tipo | Descripción |
|---|---|---|
| `Id` | int | Siempre `1` (singleton) |
| `PesoUrgencia`/`PesoValor`/`PesoProbabilidad`/`PesoTipo` | decimal | **Declarados pero nunca leídos en ningún cálculo real** |
| `HorizonteHoras` | decimal | Horas para normalizar la urgencia (default 240) — **el único valor que realmente afecta el score** |
| `ValorReferencia` | decimal | Referencia para el componente "valor" (no pondera el total) |
| `PuntajeLicitacion`/`PuntajeSpot` | decimal | Puntaje fijo del componente "tipo" (no pondera el total) |
| `ValorPorDefectoSinValor`/`ProbabilidadPorDefecto` | decimal | Valores por defecto de esos componentes (no ponderan el total) |
| `RowVersion` | binario | Control de concurrencia optimista |

**FK:** ninguna.

**De dónde salen los datos:** una única fila sembrada por una migración de EF Core (`HasData`) la primera vez que se aplican las migraciones. **No existe ningún endpoint para editarla después** — solo se lee (`AsNoTracking`) desde los servicios que calculan el score.

---

# F. Auditoría

## 16. `audit_log`

**Entidad:** `AuditLog` — **Para qué sirve:** registro de auditoría "append-only" (solo se agrega, nunca se edita) — quién hizo qué y cuándo, sobre cualquier entidad del sistema.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `UsuarioId` | Guid | Sí | FK → `usuarios` — quién ejecutó la acción |
| `Entidad` | texto(100) | Sí | Tabla/dominio afectado (ej. `"cotizaciones"`) |
| `EntidadId` | Guid | Sí | Id del registro afectado |
| `Accion` | texto(50) | Sí | `"crear"`, `"actualizar"`, `"entregar"`, `"importar"`, `"ajustar"`, etc. |
| `Diff` | JSON | Sí | Diferencia antes/después |
| `CreatedAt` | fecha | No | Momento del evento (**no tiene `UpdatedAt`** — nunca se modifica) |

**FK:** `UsuarioId` → `usuarios` (`Restrict`).

**De dónde salen los datos:** generado automáticamente por cada servicio de negocio tras una operación exitosa (cotizaciones, importaciones de tarifas SICE-TAC, servicios conexos, ajustes de vigencia). `ReglaSoloSicetacService` es la única excepción confirmada que no audita sus cambios. `DataSeeder.cs` siembra 12 filas de demostración.

---

# G. Cubicador 3D

*(Ya las conoces a fondo de nuestra conversación — las incluyo aquí resumidas para que el documento quede completo como referencia única.)*

## 17. `vehiculos_cubicacion`

**Para qué sirve:** catálogo de tipos de vehículo (dimensiones, capacidad, puerta, ejes) que usa el motor de colocación 3D.

| Columna | Descripción |
|---|---|
| `Nombre`/`Descripcion` | Nombre y descripción del vehículo |
| `LargoInternoMm`/`AnchoInternoMm`/`AltoInternoMm` | Dimensiones internas de carga |
| `CapacidadKg`/`TaraKg` | Capacidad de carga y peso vacío |
| `PuertaAnchoMm`/`PuertaAltoMm`/`PuertaPosicion` | Dimensiones y ubicación de la puerta |
| `EjesJson` | JSON de posiciones y capacidades de cada eje |
| `ToleranciaParedesMm`/`ToleranciaEntreUnidadesMm` | Holguras de empaque |
| `EsPreset` | Si vino sembrado por el sistema (`true`) o lo creó un usuario (`false`) |

**FK:** ninguna saliente; referenciada por `planes_cargue.VehiculoId`.

**De dónde salen los datos:** los 9 presets se auto-siembran (comparando por nombre) cada vez que se consulta la lista de vehículos; el usuario puede editarlos o crear nuevos desde la pantalla del Cubicador 3D.

---

## 18. `planes_cargue`

**Para qué sirve:** cada plan de carga = un vehículo específico con sus métricas de resultado.

| Columna | Descripción |
|---|---|
| `VehiculoId` | FK → `vehiculos_cubicacion` |
| `LineaSolicitudId` | Vínculo lógico con `lineas_solicitud` (puente Mesa de Costeo ↔ 3D) |
| `NumeroVehiculo` | Posición dentro de una flota (1, 2, 3...) |
| `Estado`/`Estrategia`/`Algoritmo` | `borrador\|optimizado\|ajustado`, estrategia de orden, `ffd\|bfd` |
| `PesoTotalKg`/`VolumenUtilizadoM3`/`UtilizacionVolumen`/`UtilizacionPiso`/`Puntaje` | Métricas del resultado |
| `IncidenciasJson` | JSON de incidencias (rechazos, advertencias) |

**FK:** `VehiculoId` → `vehiculos_cubicacion` (`Restrict`); sus ítems y colocaciones se borran en cascada.

**De dónde salen los datos:** creación manual, o automática vía el puente desde una `lineas_solicitud` (botón "Cubicar" de la Mesa de Costeo).

---

## 19. `items_plan_cargue`

**Para qué sirve:** cada ítem/producto a cargar dentro de un plan específico.

| Columna | Descripción |
|---|---|
| `PlanCargueId` | FK → `planes_cargue` (cascada) |
| `Sku`/`Descripcion`/`Pedido`/`Cliente`/`Destino` | Identificación del ítem |
| `LargoMm`/`AnchoMm`/`AltoMm`/`PesoKg`/`Cantidad` | Dimensiones, peso, cantidad |
| `SecuenciaEntrega` | Orden de entrega (afecta el puntaje, no bloquea) |
| `Apilable`/`MaxNiveles`/`MaxPesoEncimaKg`/`Rotable`/`MantenerVertical` | Reglas de apilamiento (sí afectan el algoritmo) |
| `Fragil`/`Peligroso`/`Refrigerado` | Solo `Fragil` afecta una validación real; los otros dos son campos "muertos" hoy |
| `GrupoCompatibilidad` | Comparación binaria simple de grupos |

**FK:** `PlanCargueId` → `planes_cargue` (**Cascade**).

**De dónde salen los datos:** manual, importación CSV, o generados automáticamente desde el JSON `Dimensiones` de una `lineas_solicitud`.

---

## 20. `colocaciones_cargue`

**Para qué sirve:** el resultado — una fila por cada unidad física, cargada o rechazada.

| Columna | Descripción |
|---|---|
| `PlanCargueId`/`ItemPlanCargueId` | FKs |
| `IndiceUnidad`/`Cargada` | Cuál instancia de un ítem, y si quedó cargada |
| `X`/`Y`/`Z` | Posición dentro del vehículo |
| `EstadoEstabilidad`/`Accesibilidad` | Resultado de validaciones |
| `CodigoRechazo`/`RazonRechazo` | Motivo si `Cargada=false` |
| `Bloqueada`/`AjusteManual` | Si el usuario fijó/ajustó manualmente esa colocación |

**FK:** `PlanCargueId` → `planes_cargue` (**Cascade**), `ItemPlanCargueId` → `items_plan_cargue` (`Restrict`).

**De dónde salen los datos:** 100% generado por el motor FFD/BFD al optimizar; el usuario puede ajustar manualmente una colocación puntual después.

---

# H. Catálogos agregados después (post-renombrado a Nexo)

## 21. `municipios`

**Entidad:** `clsMunicipio` — **Para qué sirve:** catálogo propio de municipios (Colombia/Venezuela/Ecuador) para el desplegable de Origen/Destino en Captura de Solicitudes — evita que el comercial digite la ciudad a mano. Poblado **una sola vez** (S4-HU-09.1, foto fija, sin sincronización periódica) cruzando `tblSAPDANE` + `tblZonasTarificacionRelacion` + `tblSAPTarifas`/`tblSAPTarifasLog` del datacenter corporativo (`dbSolucionesTI`) — el cruce se hizo una vez con consultas SQL directas, no con un adaptador de la app.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `Id` | int (autoincremental) | No | PK propia de Nexo — **no** es el código DANE: ese solo no es único (choca entre Venezuela y Ecuador, ~90 casos verificados) |
| `CodigoDane` | texto(20) | No | Código DANE/DIVIPOLA de origen (`tblSAPDANE.CODIGOCENTROPOBLADO`). Para Colombia siempre termina en "000" (nivel de municipio), salvo 5 excepciones de corregimiento con movimiento comercial real (Doradal, Catambuco, Bolombolo, Paraguachón, y Jerusalén con un código inventado `05756000-1` porque no tiene fila propia en la fuente) |
| `Municipio` | texto(150) | No | Nombre normalizado (sin tildes, mayúsculas) |
| `Departamento` | texto(150) | No | Nombre normalizado (sin tildes, mayúsculas) |
| `Pais` | texto(2) | No | `CO`/`VE`/`EC` — únicos 3 países presentes en `tblSAPDANE` |
| `NombreTarifasPlaza` | texto(60) | Sí | Código `ZC_...` del Tarifario SAP que matcheó con este municipio; `NULL` si no se encontró ninguno |
| `ZonaTarificacion` | texto(60) | Sí | Código `ZTC_...` de `tblZonasTarificacionRelacion` para `NombreTarifasPlaza`; `NULL` también cuando el `ZC_` sí existe en el Tarifario pero no está en la relación oficial (queda señalado, no se omite la fila) |

Índice único: `(CodigoDane, Pais)` — la llave natural real (el código DANE solo no alcanza). Índice simple sobre `NombreTarifasPlaza` (búsqueda del motor de costeo).

**FK:** ninguna, en ningún sentido (todavía) — es un catálogo de solo lectura para el desplegable, no referenciado aún por `solicitudes`/`lineas_solicitud`.

**De dónde salen los datos:**
- No hay CRUD — un CRUD sobre esta tabla queda **fuera de alcance** de S4-HU-09.1, es trabajo futuro.
- Tabla creada corriendo `docs/Database/ESQUEMA_NEXO.sql` a mano (no por migración de EF Core) y poblada corriendo el script de poblado con las filas ya calculadas — no hay endpoint ni comando que la siembre en vivo.
- Se lee con `GET /api/captura-solicitudes/municipios` (Comercial/Admin) — todas las columnas, sin filtro ni paginación.
- Ya ejecutado contra `dbInformesAutomatizados_Pruebas` (2026-09-26): 3.804 filas (Colombia 1.128, Venezuela 1.424, Ecuador 1.252).

---

## 22. `pesos`

**Entidad:** `clsPeso` — **Para qué sirve:** catálogo CRUD propio de bandas de peso (kg) para el formulario de Tarifas de Plaza (S4-HU-10.1) — reemplaza el número libre que tenía el sistema externo.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `Id` | int (autoincremental) | No | PK |
| `PesoMinimo` | int | No | Límite inferior de la banda (kg) |
| `PesoMaximo` | int | Sí | Límite superior (kg); `NULL` = banda abierta, sin tope (ej. PBV +10500) |

**FK:** ninguna saliente. Referenciada por `tarifas_plaza.PesoId` (`Restrict`).

**De dónde salen los datos:**
- Tabla creada corriendo `ESQUEMA_NEXO.sql` y poblada con `docs/Database/seeder/pesos.csv` (8 filas: las 7 bandas de negocio + 1 banda real verificada en vivo en el sistema externo que no calzaba con ninguna de las 7 — ver el seeder README).
- **No es un catálogo cerrado**: el sistema externo permite peso inferior/superior libres por tarifa puntual, así que este catálogo va a necesitar bandas nuevas con el tiempo. El CRUD (`api/pesos`, pendiente de construir por backend) es justo para eso.

---

## 23. `tarifas_plaza`

**Entidad:** *(pendiente — solo existe como tabla, sin entidad/CRUD de C# todavía, ver nota de alcance abajo)* — **Para qué sirve:** tarifa de plaza gobernada por Nexo (S4-HU-10.1), recrea el formulario de captura que hoy vive en un sistema externo. **NO** es una copia de solo lectura de `tblSAPTarifas` (a diferencia de `tarifas_sicetac`).

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `Id` | Guid | No | PK |
| `LineaNegocio` | texto(100) | No | Nombre completo de `tblSAPLineaNegocio` (dbSolucionesTI) — texto, no el `ID`: ese `ID` no significa nada dentro de Nexo |
| `ClaseViaje` | texto(50) | No | Nombre completo de `tblSAPClaseViaje` — mismo criterio |
| `OrigenMunicipioId`/`DestinoMunicipioId` | int | No | FK real a `municipios.Id` (el autoincremental de Nexo, **no** el código DANE) |
| `ClaseContenedor` | texto(50) | Sí | Código de `tblSAPClaseContenedor` (ej. `CNT40.HC`); `NULL` cuando la tarifa no es de contenedor (ej. carga suelta nacional) |
| `PesoId` | int | Sí | FK a `pesos.Id` — solo aplica a tarifas de contenedor por peso |
| `TipoVehiculo` | texto(40) | Sí | Código tipo `"2S2"`/`"3S3"`, o el nombre real (`"TURBO"`/`"SENCILLO"`) cuando el catálogo oficial no distingue variantes con precio distinto |
| `Valor` | decimal(18,2) | No | Valor de la tarifa (COP) |
| `Vigencia` | fecha | No | Fecha de vigencia |
| `DetallesTarifa` | texto(2000) | No | Texto libre obligatorio (condiciones, advertencias de negocio) |
| `CreadoPor` | Guid | Sí | Usuario que la creó |
| `CreatedAt`/`UpdatedAt` | fecha | No | Auditoría estándar |

Índice único: `(LineaNegocio, ClaseViaje, OrigenMunicipioId, DestinoMunicipioId, ClaseContenedor, PesoId, TipoVehiculo)` — sus 7 dimensiones; `Valor`/`Vigencia`/`DetallesTarifa` es lo que cambia con el tiempo para una misma combinación.

**FK:** `OrigenMunicipioId`/`DestinoMunicipioId` → `municipios` (`Restrict`), `PesoId` → `pesos` (`Restrict`).

**De dónde salen los datos:**
- Tabla creada corriendo `ESQUEMA_NEXO.sql` (no por migración de EF Core) y poblada con `docs/Database/seeder/tarifas_plaza.csv` — 271 filas extraídas de `docs/docs_raw/fletes-plaza-normalizado-plaza.csv`, verificadas contra `tblSAPTarifas` real (valores en el mismo rango, una coincidencia exacta).
- **No hay CRUD todavía** — entidad/comandos/endpoint quedan como trabajo pendiente de backend (Ismael); esta HU solo dejó el esquema + el dato ya poblado.
- El trigger `TRG_tarifas_plaza_historico` archiva el valor anterior en `tarifas_plaza_historico` cada vez que alguien actualice `Valor`/`Vigencia`/`DetallesTarifa` de una fila existente.

---

## 24. `tarifas_plaza_historico`

**Entidad:** *(pendiente, igual que `tarifas_plaza`)* — **Para qué sirve:** snapshot del valor ANTERIOR de una tarifa de plaza — igual que `tblSAPTarifasLog` es al `tblSAPTarifas` del sistema externo.

| Columna | Tipo | Nullable | Descripción |
|---|---|---|---|
| `Id` | Guid | No | PK |
| `TarifaPlazaId` | Guid | No | FK → `tarifas_plaza.Id` |
| (resto de columnas) | — | — | Espejo exacto de `tarifas_plaza` en el momento en que se reemplazó (mismas 7 dimensiones + `Valor`/`Vigencia`/`DetallesTarifa`/`CreadoPor`/`CreatedAt` originales) |
| `ReemplazadoAt` | fecha | No | Cuándo se archivó esta fila (cuándo se hizo el `UPDATE` que la reemplazó) |

**FK:** `TarifaPlazaId` → `tarifas_plaza` (**Cascade**).

**De dónde salen los datos:** **100% automático, por el trigger `TRG_tarifas_plaza_historico`** — nadie escribe aquí a mano ni por API. Nace vacía; se llena sola la primera vez que alguien actualice una fila de `tarifas_plaza`.

---

# Apéndice: todos los enums usados en todo el sistema

| Enum | Valores | Dónde se usa |
|---|---|---|
| `TipoSolicitud` | `Spot`, `Licitacion` | `Solicitud.Tipo` |
| `ModalidadSolicitud` | `Importacion`, `Exportacion`, `Nacional`, `Extradimensionada`, `CargaGeneral` | `Solicitud.Modalidad` |
| `EstadoSolicitud` | `Pendiente`, `EnCosteo`, `Entregada`, `Descartada` | `Solicitud.Estado` |
| `TipologiaCarga` | `Contenedor`, `CargaSuelta`, `GranelSolido`, `GranelLiquido`, `Alimentos`, `Refrigerados`, `CargaSeca` | `LineaSolicitud.Tipologia`, `TarifaHistorica.Tipologia` |
| `LineaNegocio` | `PuertoAInterior`, `InteriorAPuerto`, `Nacional`, `DevolucionContenedor`, `Andino`, `Licitaciones` | `LineaSolicitud.LineaNegocio` (catálogo SAP TM) |
| `ClaseViaje` | `Contenedor`, `Cupo`, `CargaSuelta`, `Extradimensionado` | `LineaSolicitud.ClaseViaje` (catálogo SAP TM) |
| `EstadoLinea` | `PorCostear`, `AutoResuelta`, `Revisada` | `LineaSolicitud.EstadoLinea` |
| `RolUsuario` | `AdminBiblioteca`, `LiderProceso`, `ServiciosLogisticos`, `Admin`, `Comercial`, `AdminTarifas` | `Usuario.Rol` — Comercial y AdminTarifas son los dos roles de negocio principales; Admin es superusuario técnico |
| `CondicionCarga` | `Cargado`, `Vacio` | `TarifaSicetac.CondicionCarga` |
| `FuenteFlete` | `Sicetac`, `Historica`, `Manual`, `Tarifario` | `CotizacionLinea.Fuente` — de dónde salió el valor del flete |
| `EstadoCotizacion` | `Borrador`, `Entregada` | `Cotizacion.Estado` |
| `ResultadoCotizacion` | `SinDefinir`, `Ganada`, `Perdida` | `Cotizacion.Resultado` |
