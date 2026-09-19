# Diccionario reconciliado — Lado propio (`bd-propia`)

Diccionario de las 13 tablas del lado propio (CabinaFletes) del set `v1.0.0-piloto`, cruzando la definición oficial (`diccionario-y-linaje.md` y `DICCIONARIO_DATOS.md`, ambos de HU-10.3) con lo que confirmó el análisis a lo largo de los 8 notebooks de este EDA. Ver [`diccionario_reconciliado_fuentes_corporativas.md`](diccionario_reconciliado_fuentes_corporativas.md) para el lado externo (SICE-TAC, SAP, clientes SAP), `hallazgos.md` para el detalle completo de cada hallazgo referenciado (H-0xx) y `limitaciones.md` para las preguntas que este set no puede responder (L-0x).

**Nombres de columna en código:** mismo criterio que el EDA-1 — camelCase, sin tildes ni espacios (notebook `00`, Paso 4).

**Ejemplos:** valores reales tomados directamente de los CSV de `v1.0.0-piloto` (no inventados), excluyendo la fila fantasma `"------"` (H-001 del EDA-2) y los `NULL` literales.

---

### `solicitudes`

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `Id` | `id` | Identificador de la solicitud | GUID | `CE7F580A-9997-4954-B358-0CC0BDA17461` | Confiable |
| `ClienteId` | `clienteId` | FK a `catalogo_clientes` | GUID | `1A239191-8A7D-46EF-AD79-69E83E89547F` | Confiable |
| `Tipo` | `tipo` | `Spot` / `Licitacion` | categórico | `Spot` | Confiable |
| `Modalidad` | `modalidad` | `Importacion`/`Exportacion`/`Nacional`/`Extradimensionada`/`CargaGeneral` | categórico | `Nacional` | **92,3% vacía** (12 de 13 solicitudes) — hueco conocido de la fase piloto de CabinaFletes, no un bug de este EDA (L-11) |
| `Deadline` | `deadline` | Fecha límite de respuesta (SLA) | fecha/hora | `2026-09-10 12:21:49` | Sin hallazgos relevantes |
| `ProbabilidadCierre` | `probabilidadCierre` | Probabilidad estimada de ganar | % (0–100) | `50.0000` | **92,3% vacía** (L-11) |
| `ValorEstimado` | `valorEstimado` | Valor económico estimado | COP | `40000000.00` | **92,3% vacía** (L-11) |
| `ScorePrioridad` | `scorePrioridad` | Score de priorización, calculado por el dominio | 0–100 | `51.45` | Sin hallazgos relevantes |
| `Estado` | `estado` | `Pendiente`/`EnCosteo`/`Entregada`/`Descartada` | categórico | `EnCosteo` | Sin hallazgos relevantes |
| `CreadoPor` | `creadoPor` | FK a `catalogo_usuarios` | GUID | `00000000-0000-0000-0000-000000000001` | Sin hallazgos relevantes |
| `CreatedAt`/`UpdatedAt` | `createdAt`/`updatedAt` | Auditoría técnica de la fila | fecha/hora | `2026-08-31 17:21:49` | Sin hallazgos relevantes |

### `lineas_solicitud`

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `Id` | `id` | Identificador de la línea | GUID | `2AD1544F-48F0-418E-A5E1-61D95263C718` | Confiable |
| `SolicitudId` | `solicitudId` | FK a `solicitudes` | GUID | `CE7F580A-9997-4954-B358-0CC0BDA17461` | Confiable |
| `RutaId` | `rutaId` | FK a `catalogo_rutas` | GUID | `7FFCB641-E70A-4554-8EED-7A7BD056CDA1` | Confiable |
| `ConfigVehiculoId` | `configVehiculoId` | FK a `catalogo_configuraciones_vehiculo` | GUID | `E26305EB-0CBD-41FF-9228-41580DFCF8CB` | Confiable |
| `Tipologia` | `tipologia` | Tipo de carga (`Contenedor`, `GranelSolido`, ...) | categórico | `CargaSeca` | **97,7% vacía** (42 de 43 filas) — mismo hueco conocido de la fase piloto (L-11) |
| `LineaNegocio` | `lineaNegocio` | **Llave de cruce secundaria/complementaria** — mismo código que `tarifa-plaza.LINEANEGOCIO` (lado externo). La llave dominante es la ruta (`RutaId` → `catalogo_rutas.Origen`/`Destino`, ver abajo), confirmado por el negocio (H-004) | texto | `Andino`, `DevolucionContenedor` | **No comparable directamente** con el código numérico externo — 0% de match en texto plano. Requiere la traducción fija confirmada en el backend (`Andino=7`, `DevolucionContenedor=6`, `Nacional=4`, `PuertoAInterior=1`, `InteriorAPuerto=2`, `Licitaciones=8`) (H-003) |
| `ClaseViaje` | `claseViaje` | **Llave de cruce secundaria/complementaria** — mismo código que `tarifa-plaza.CLASEVIAJE` (lado externo). La ruta sigue siendo la llave dominante (H-004) | texto | `Contenedor`, `CargaSuelta`, `Cupo` | Con la traducción fija del backend (`Contenedor=1`, `Cupo=2`, `CargaSuelta=4`, `Extradimensionado=5`) sube a 100% de match (5/5) (H-003) |
| `ClaseContenedor` | `claseContenedor` | Mismo código que `tarifa-plaza.CLASECONTENEDOR` (lado externo) | texto | `CNT20.IT`, `CNT20.ST` | **Sin mapeo fijo** — depende de `tblSAPClaseContenedor`, ausente en los 4 extractos, no verificable con este set (H-003, L-03) |
| `Exclusivo`/`Consolidado`/`RetornaContenedor`/`LevanteContenedorDiferente` | `exclusivo`/`consolidado`/`retornaContenedor`/`levanteContenedorDiferente` | Banderas de condición del servicio | booleano (`0`/`1`) | `0`, `1` | `RetornaContenedor=1` solo en 1 línea, con ruta de prueba malformada `"222"`/`"222"` — explica por qué `ValorAprovisionamiento`/`ValorRetornoContenedor` están 100% vacíos (H-008) |
| `ValorAprovisionamiento` | `valorAprovisionamiento` | Costo de reposicionar contenedor vacío | COP | *(vacío en todo el extracto)* | 100% vacía en este set — explicado, no es un bug (H-008) |
| `Peso` | `peso` | Peso de la carga | kg | `12000.00` | Confirmado por el usuario como valor puesto al azar durante pruebas manuales, sin relación con la realidad (H-002) |
| `ValorMercancia` | `valorMercancia` | Valor asegurado de la mercancía | COP | `40000000.00`; también `12.00` (uno de los 2 valores absurdos) | 2 de 38 valores absurdos (\$12 y \$27) — confirmado por el usuario como puesto al azar; sin columna `Fuente` que permita separar señal de ruido (H-002) |
| `EstadoLinea` | `estadoLinea` | `PorCostear`/`AutoResuelta`/`Revisada` | categórico | `PorCostear` | Sin hallazgos relevantes |
| `CreatedAt`/`UpdatedAt` | `createdAt`/`updatedAt` | Auditoría técnica | fecha/hora | `2026-08-31 17:21:49` | Sin hallazgos relevantes |

### `cotizaciones`

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `Id` | `id` | Identificador | GUID | `5986FF7E-F914-44ED-9C6A-060D9666E890` | Confiable |
| `SolicitudId` | `solicitudId` | FK a `solicitudes` | GUID | `6283409E-F76C-45C7-9F2E-913330444FFF` | **5 de 18 apuntan a un `SolicitudId` inexistente** (todas en estado `Borrador`) — huérfanas, causa no confirmada por el usuario (H-013, L-08) |
| `Estado` | `estado` | `Borrador`/`Entregada` | categórico | `Borrador` | Sin hallazgos relevantes |
| `Resultado` | `resultado` | `SinDefinir`/`Ganada`/`Perdida` | categórico | `SinDefinir` | Confirmado por el usuario: se marcó a mano durante pruebas, sin relación con una decisión real de cliente — cualquier cruce que lo involucre (ej. `Fuente`×`Resultado`) no aporta señal de negocio, sin importar el tamaño de muestra (memoria del proyecto, notebook 05) |
| `Total` | `total` | Suma de líneas de flete + conexos | COP | `14810032.00` | Sin hallazgos relevantes |
| `EntregadaPor` | `entregadaPor` | FK a `catalogo_usuarios` | GUID | `00000000-0000-0000-0000-000000000001` | Sin hallazgos relevantes |
| `EntregadaAt` | `entregadaAt` | Cuándo se entregó | fecha/hora | `2026-08-27 13:24:07` | Sin hallazgos relevantes |
| `CreatedAt`/`UpdatedAt` | `createdAt`/`updatedAt` | Auditoría técnica | fecha/hora | `2026-08-24 19:34:55` | Sin hallazgos relevantes |

### `cotizacion_lineas`

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `Id` | `id` | Identificador | GUID | `C5154B85-5774-43F8-8049-72B82F9AB805` | Confiable |
| `CotizacionId` | `cotizacionId` | FK a `cotizaciones` | GUID | `1EF83172-3574-4DC8-97A3-0B3635CBE281` | Confiable |
| `LineaSolicitudId` | `lineaSolicitudId` | FK a `lineas_solicitud` | GUID | `35BEB9C0-6497-41B8-9DA5-04E4B9170398` | Confiable |
| `ValorFlete` | `valorFlete` | Valor del flete cotizado | COP | `4832788.00` | 3 líneas `Fuente=Manual` con valores absurdos (\$45.555 y \$4.500.000.000 entre ellas) — confirmadas por el usuario como puestas al azar, excluir de cualquier estadístico (H-002). El resto (`Fuente=Tarifario`/`Sicetac`) es confiable: 32/32 `Tarifario` coincide exacto con `tarifa-plaza.VALOR` (H-007, lado externo) |
| `ValorAprovisionamiento`/`ValorRetornoContenedor` | `valorAprovisionamiento`/`valorRetornoContenedor` | Conceptos adicionales, no suman al flete base | COP | *(vacío en todo el extracto)* | 100% vacíos, explicado (H-008) |
| `Fuente` | `fuente` | **La trazabilidad de origen del valor**: `Sicetac`/`Historica`/`Manual`/`Tarifario` | categórico | `Sicetac`, `Tarifario`, `Manual` | Confiable como bandera — ver `ValorFlete` arriba para las 3 líneas `Manual` con ruido |
| `CreatedAt`/`UpdatedAt` | `createdAt`/`updatedAt` | Auditoría técnica | fecha/hora | `2026-08-31 16:53:59` | Sin hallazgos relevantes |

### `cotizacion_conexos`

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `Id` | `id` | Identificador | GUID | `EC7AECD1-C14D-4C82-97CD-19E9413C782A` | Confiable |
| `CotizacionId` | `cotizacionId` | FK a `cotizaciones` | GUID | `0EE0943A-81F2-49F0-A6B8-AF5B2B04FA51` | Confiable |
| `ServicioConexoId` | `servicioConexoId` | FK a `catalogo_servicios_conexos` | GUID | `C11BE376-96CA-4282-95A0-F261052BD486` | Confiable |
| `Cantidad` | `cantidad` | Unidades del servicio aplicadas | unidades | `1.00`, `700.00` | Sin hallazgos relevantes |
| `ValorAplicado` | `valorAplicado` | Valor cobrado por ese servicio | COP | `85000.00` | Sin hallazgos relevantes |

### `tarifas_historicas` — "Biblioteca de Tarifas Ganadas"

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `Id` | `id` | Identificador | GUID | `BFEF414B-5F9A-47B7-998C-1D9C11436580` | Confiable |
| `RutaId`/`ConfigVehiculoId` | `rutaId`/`configVehiculoId` | FK a catálogos | GUID | `7FFCB641-E70A-4554-8EED-7A7BD056CDA1` | Confiable |
| `Tipologia` | `tipologia` | Tipo de carga | categórico | `GranelSolido`, `Refrigerados` | Sin hallazgos relevantes |
| `Valor` | `valor` | Tarifa que se cobró de verdad (copiado de `cotizacion_lineas.ValorFlete` al ganar) | COP | `6788888.00` | Confiable donde existe |
| `Fecha` | `fecha` | Fecha de la tarifa | fecha | `2026-08-31` | Sin hallazgos relevantes |
| `CotizacionId` | `cotizacionId` | FK a la cotización ganada que la originó | GUID | `DCBFDB07-4763-4C82-BFB2-4D5B6E42482B` | **1 cotización referenciada aquí (por 3 filas) no existe en `cotizaciones.csv`** — huérfana, misma causa no confirmada que arriba (H-013, L-08) |

### `audit_log`

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `Id` | `id` | Identificador del evento | GUID | `50A6E1BF-E7F8-42D8-9D40-002FA749B109` | Confiable |
| `UsuarioId` | `usuarioId` | FK a `catalogo_usuarios`, autor de la acción | GUID | `00000000-0000-0000-0000-000000000001` | Nulo en registros previos a S2-HU-03 — esperado, no defecto |
| `Entidad`/`EntidadId` | `entidad`/`entidadId` | Sobre qué entidad de negocio actuó | texto/GUID | `cotizaciones` / `D782291B-35C9-4532-B845-4A765C1CFF3C` | Sin hallazgos relevantes |
| `Accion` | `accion` | Qué se hizo (`entregar`, `cargar_lineas_bulk`, ...) | texto | `entregar`, `captura`, `crear` | Sin hallazgos relevantes |
| `CreatedAt` | `createdAt` | Cuándo | fecha/hora | `2026-08-27 13:24:07` | Sin hallazgos relevantes |

### `catalogo_clientes`

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `Id` | `id` | Identificador | GUID | `C7F0815A-DC98-4AE0-BA4D-27CE7E9DECF9` | Confiable |
| `Nombre` | `nombre` | Nombre del cliente | texto | `Cementos Argos S.A.` | Sin hallazgos relevantes |
| `Nit` | `nit` | NIT — llave de cruce con `catalogo-clientes-BUT000.TAXNUM` (lado externo) | texto | `890100251-0` | El cruce geográfico (ciudad→zona) sí tiene hallazgo propio (H-006); el cruce por NIT en sí no se investigó a fondo en este EDA |
| `RazonSocial` | `razonSocial` | Razón social | texto | `Cementos Argos S.A.` | Sin hallazgos relevantes |
| `CreatedAt`/`UpdatedAt` | `createdAt`/`updatedAt` | Auditoría técnica | fecha/hora | `2026-08-21 18:31:22` | Sin hallazgos relevantes |

### `catalogo_rutas`

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `Id` | `id` | Identificador | GUID | `303AC7E7-4AC4-4717-A5D5-022F48DEE1B6` | Confiable |
| `Origen`/`Destino` | `origen`/`destino` | **Llave de cruce dominante** — el motor real de costeo filtra principalmente por ruta, no por `LineaNegocio`/`ClaseViaje`/`ClaseContenedor` (confirmado por el negocio, H-004). Población origen/destino, texto libre | texto | `Cartagena`, `Bogotá`, `barranquilla` (nótese la inconsistencia de mayúsculas entre filas) | 91,7% (22/24 ciudades) conecta con confianza (≥90%) al vocabulario de zonas SAP vía `rapidfuzz`; Leticia y Puerto Nariño sin match confiable (H-006). Es una aproximación nuestra, **no el mecanismo real** — el backend usa `tblsapdane`/`tblZonasTarificacionRelacion` (lado externo), ausentes en los 4 extractos (H-004, L-02, L-10) |
| `CreatedAt`/`UpdatedAt` | `createdAt`/`updatedAt` | Auditoría técnica | fecha/hora | `2026-08-21 18:31:22` | Sin hallazgos relevantes |

### `catalogo_configuraciones_vehiculo`

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `Id` | `id` | Identificador | GUID | `5CC0408D-E0CE-48CE-BCAB-183BF558C9C9` | Confiable |
| `TipoVehiculo` | `tipoVehiculo` | Tipo de vehículo | categórico | `2S3`, `3S3` | Sin hallazgos relevantes |
| `Carroceria` | `carroceria` | Tipo de carrocería | categórico | `Tanque`, `Furgón Refrigerado` | Sin hallazgos relevantes |
| `RangoPeso` | `rangoPeso` | Rango de peso que admite | texto/rango | `32 ton`, `15-17 ton` | Sin hallazgos relevantes |
| `CreatedAt`/`UpdatedAt` | `createdAt`/`updatedAt` | Auditoría técnica | fecha/hora | `2026-08-21 18:31:22` | Sin hallazgos relevantes |

### `catalogo_servicios_conexos`

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `Id` | `id` | Identificador | GUID | `D9B1ED68-8EA0-456D-9268-0D0AB890B577` | Confiable |
| `Nombre` | `nombre` | Nombre del servicio | texto | `Stand-by`, `Descargue`, `Escolta` | Sin hallazgos relevantes |
| `Categoria` | `categoria` | Categoría del servicio | categórico | `logistico`, `adicional` | Sin hallazgos relevantes |
| `ValorBase` | `valorBase` | Valor base del servicio | COP | `45000.00` | Sin hallazgos relevantes |
| `VigenteHasta` | `vigenteHasta` | Vigencia del servicio | fecha | `2026-12-31` | Sin hallazgos relevantes |
| `Activo` | `activo` | Si el servicio está activo | booleano (`0`/`1`) | `1`, `0` | Sin hallazgos relevantes |
| `CreatedAt`/`UpdatedAt` | `createdAt`/`updatedAt` | Auditoría técnica | fecha/hora | `2026-08-21 18:31:22` | Sin hallazgos relevantes |

### `catalogo_usuarios`

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `Id` | `id` | Identificador | GUID | `00000000-0000-0000-0000-000000000001` | Confiable |
| `Rol` | `rol` | Rol del usuario | categórico | `Admin`, `ServiciosLogisticos`, `LiderProceso` | Sin hallazgos relevantes |
| `Activo` | `activo` | Si el usuario está activo | booleano (`0`/`1`) | `1`, `0` | Sin hallazgos relevantes |
| `CreatedAt` | `createdAt` | Auditoría técnica | fecha/hora | `2026-01-01 00:00:00` | Sin hallazgos relevantes |

### `inconsistencias`

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `Entidad` | `entidad` | Tabla donde se detectó | texto | `solicitudes` | Solo 1 fila real (de 16 que produce la vista completa; 15 son del `DataSeeder` y se excluyeron) — ver README de `v1.0.0-piloto` |
| `EntidadId` | `entidadId` | Fila específica | GUID | `3EAB0AB6-ED6D-4983-A8EA-F9BF7406AB0A` | ídem |
| `Motivo` | `motivo` | Cuál de las 9 reglas se incumplió | texto | `Deadline anterior a la fecha de creacion` | ídem |
