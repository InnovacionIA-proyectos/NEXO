# Diccionario de datos y linaje — Set final Habilitador B8

Referencia única para todas las columnas del set final: qué significa cada una,
de qué sistema/tabla real viene (linaje), y en qué unidad está. Cubre las 13
tablas del lado propio (`./datos/bd-propia/`, exportadas de las
vistas `analitica.*` de S2-HU-10.1) y las 4 del lado externo
(`./datos/fuentes-corporativas/`, S2-HU-10.2) — el diccionario es
autocontenido, no hace falta saltar de documento para entender una columna.

**Los datos NO están aplanados en una sola tabla.** Cada archivo conserva el
grano de su tabla de origen — el cruce entre ellos se hace por las llaves
documentadas en la sección final, no por un `JOIN` físico ya resuelto.

## Lado propio (`./datos/bd-propia/`) — filtrado a `CreatedAt >= 2026-08-22`

> Todas las columnas de fecha están en formato `AAAA-MM-DD HH:MM:SS`. Todos los
> valores monetarios están en pesos colombianos (COP). Fuente: vistas
> `analitica.*` sobre la BD transaccional `Nexo` (servidor `localhost,1434`
> en este entorno de desarrollo — no hay instancia de producción todavía).

### `solicitudes.csv`

| Columna | Significado | Origen (tabla.columna) | Unidad |
|---|---|---|---|
| `Id` | Identificador de la solicitud | `solicitudes.Id` | GUID |
| `ClienteId` | Cliente que solicita | `solicitudes.ClienteId` → `catalogo_clientes.Id` | GUID |
| `Tipo` | `Spot` o `Licitacion` | `solicitudes.Tipo` | categórico |
| `Modalidad` | `Importacion`/`Exportacion`/`Nacional`/`Extradimensionada`/`CargaGeneral` | `solicitudes.Modalidad` | categórico |
| `Deadline` | Fecha límite de respuesta (SLA) | `solicitudes.Deadline` | fecha/hora |
| `ProbabilidadCierre` | Probabilidad estimada de ganar | `solicitudes.ProbabilidadCierre` | % (0–100) |
| `ValorEstimado` | Valor económico estimado | `solicitudes.ValorEstimado` | COP |
| `ScorePrioridad` | Score de priorización, calculado por el dominio | `solicitudes.ScorePrioridad` | 0–100 |
| `Estado` | `Pendiente`/`EnCosteo`/`Entregada`/`Descartada` | `solicitudes.Estado` | categórico |
| `CreadoPor` | Usuario que la creó | `solicitudes.CreadoPor` → `catalogo_usuarios.Id` | GUID |
| `CreatedAt`/`UpdatedAt` | Auditoría técnica de la fila | `solicitudes.CreatedAt`/`UpdatedAt` | fecha/hora |

### `lineas_solicitud.csv`

| Columna | Significado | Origen | Unidad |
|---|---|---|---|
| `Id` | Identificador de la línea | `lineas_solicitud.Id` | GUID |
| `SolicitudId` | Solicitud a la que pertenece | → `solicitudes.Id` | GUID |
| `RutaId` | Ruta cotizada | → `catalogo_rutas.Id` | GUID |
| `ConfigVehiculoId` | Configuración de vehículo | → `catalogo_configuraciones_vehiculo.Id` | GUID |
| `Tipologia` | Tipo de carga (`Contenedor`, `GranelSolido`, ...) | `lineas_solicitud.Tipologia` | categórico |
| `LineaNegocio` | **Llave de cruce** — mismo código que `tblSAPTarifas.LINEANEGOCIO` (ver extractos externos) | `lineas_solicitud.LineaNegocio` | código SAP TM |
| `ClaseViaje` | **Llave de cruce** — mismo código que `tblSAPTarifas.CLASEVIAJE`. Determina si el motor real usó matching con escalado a zonas (si no es nulo) o matching simple | `lineas_solicitud.ClaseViaje` | código SAP TM |
| `ClaseContenedor` | Mismo código que `tblSAPTarifas.CLASECONTENEDOR` | `lineas_solicitud.ClaseContenedor` | código SAP TM |
| `Exclusivo`/`Consolidado`/`RetornaContenedor`/`LevanteContenedorDiferente` | Banderas de condición del servicio | `lineas_solicitud.*` | booleano |
| `ValorAprovisionamiento` | Costo de reposicionar contenedor vacío | `lineas_solicitud.ValorAprovisionamiento` | COP |
| `Peso` | Peso de la carga | `lineas_solicitud.Peso` | kg |
| `ValorMercancia` | Valor asegurado de la mercancía | `lineas_solicitud.ValorMercancia` | COP |
| `EstadoLinea` | `PorCostear`/`AutoResuelta`/`Revisada` | `lineas_solicitud.EstadoLinea` | categórico |
| `CreatedAt`/`UpdatedAt` | Auditoría técnica | `lineas_solicitud.*` | fecha/hora |

### `cotizaciones.csv`

| Columna | Significado | Origen | Unidad |
|---|---|---|---|
| `Id` | Identificador | `cotizaciones.Id` | GUID |
| `SolicitudId` | Solicitud origen | → `solicitudes.Id` | GUID |
| `Estado` | `Borrador`/`Entregada` | `cotizaciones.Estado` | categórico |
| `Resultado` | `SinDefinir`/`Ganada`/`Perdida` | `cotizaciones.Resultado` | categórico |
| `Total` | Suma de líneas de flete + conexos | `cotizaciones.Total` | COP |
| `EntregadaPor` | Usuario que entregó | → `catalogo_usuarios.Id` | GUID |
| `EntregadaAt` | Cuándo se entregó | `cotizaciones.EntregadaAt` | fecha/hora |
| `CreatedAt`/`UpdatedAt` | Auditoría técnica | `cotizaciones.*` | fecha/hora |

### `cotizacion_lineas.csv`

| Columna | Significado | Origen | Unidad |
|---|---|---|---|
| `Id` | Identificador | `cotizacion_lineas.Id` | GUID |
| `CotizacionId` | Cotización a la que pertenece | → `cotizaciones.Id` | GUID |
| `LineaSolicitudId` | Línea de solicitud que se costeó | → `lineas_solicitud.Id` | GUID |
| `ValorFlete` | El valor del flete cotizado | `cotizacion_lineas.ValorFlete` | COP |
| `ValorAprovisionamiento`/`ValorRetornoContenedor` | Conceptos adicionales, no suman al flete base | `cotizacion_lineas.*` | COP |
| `Fuente` | **La trazabilidad de origen del valor**: `Sicetac`/`Historica`/`Manual`/`Tarifario` — verificado en esta HU cruzando contra las fuentes externas (ver Reporte de Inconsistencias) | `cotizacion_lineas.Fuente` | categórico |
| `CreatedAt`/`UpdatedAt` | Auditoría técnica | `cotizacion_lineas.*` | fecha/hora |

### `cotizacion_conexos.csv`

| Columna | Significado | Origen | Unidad |
|---|---|---|---|
| `Id` | Identificador | `cotizacion_conexos.Id` | GUID |
| `CotizacionId` | Cotización a la que pertenece | → `cotizaciones.Id` | GUID |
| `ServicioConexoId` | Servicio aplicado | → `catalogo_servicios_conexos.Id` | GUID |
| `Cantidad` | Unidades del servicio aplicadas | `cotizacion_conexos.Cantidad` | unidades |
| `ValorAplicado` | Valor cobrado por ese servicio | `cotizacion_conexos.ValorAplicado` | COP |

### `tarifas_historicas.csv` — "Biblioteca de Tarifas Ganadas"

Tarifas que sí se cobraron en una cotización **ganada**, archivadas como referencia.
No confundir con `tblSAPTarifasLog` (histórico externo de SAP, en
`./datos/fuentes-corporativas/`) — son conceptos de "ganada" en dos
sistemas distintos, sin relación directa entre sí.

| Columna | Significado | Origen | Unidad |
|---|---|---|---|
| `Id` | Identificador | `tarifas_historicas.Id` | GUID |
| `RutaId`/`ConfigVehiculoId` | Ruta y vehículo de la tarifa archivada | → catálogos | GUID |
| `Tipologia` | Tipo de carga | `tarifas_historicas.Tipologia` | categórico |
| `Valor` | Tarifa que se cobró de verdad | `tarifas_historicas.Valor` (copiado de `cotizacion_lineas.ValorFlete` al ganar) | COP |
| `Fecha` | Fecha de la tarifa | `tarifas_historicas.Fecha` | fecha |
| `CotizacionId` | Cotización ganada que la originó | → `cotizaciones.Id` | GUID |

### `audit_log.csv`

| Columna | Significado | Origen | Unidad |
|---|---|---|---|
| `Id` | Identificador del evento | `audit_log.Id` | GUID |
| `UsuarioId` | Autor de la acción (nulo en registros previos a S2-HU-03) | → `catalogo_usuarios.Id` | GUID |
| `Entidad`/`EntidadId` | Sobre qué entidad de negocio actuó | `audit_log.*` | texto/GUID |
| `Accion` | Qué se hizo (`entregar`, `cargar_lineas_bulk`, ...) | `audit_log.Accion` | texto |
| `CreatedAt` | Cuándo | `audit_log.CreatedAt` | fecha/hora |

### Catálogos (`catalogo_*.csv`) — completos, sin filtrar por fecha

`catalogo_clientes`, `catalogo_rutas`, `catalogo_configuraciones_vehiculo`,
`catalogo_servicios_conexos`, `catalogo_usuarios`: tablas de referencia para
resolver los `*Id` de arriba. Se extrajeron completas porque son catálogos, no
transacciones — filtrarlas por fecha no aplica. Columnas descriptivas
(nombre, NIT, tipo de vehículo, etc.), documentadas en detalle en
`./datos/bd-propia/DICCIONARIO_DATOS.md` sección 4.

### `inconsistencias.csv`

| Columna | Significado | Origen |
|---|---|---|
| `Entidad` | Tabla donde se detectó | `analitica.inconsistencias` (9 reglas ya construidas y probadas en S2-HU-10.1) |
| `EntidadId` | Fila específica | ídem |
| `Motivo` | Cuál de las 9 reglas se incumplió | ídem |

**Filtrado**: de las 16 filas que produce la vista completa, 15 pertenecen al
`DataSeeder` (dato sintético) y se excluyeron de este archivo — queda solo **1
fila real**, tuya, no del seeder. Ver el Reporte de Inconsistencias para el
detalle.

## Lado externo (`./datos/fuentes-corporativas/`, S2-HU-10.2)

> Detalle completo de fecha de corte, volumen y metodología de muestreo en
> `./datos/fuentes-corporativas/README.md`. Las columnas se repiten
> aquí para que el diccionario sea autocontenido.

### `piso-sicetac-tblRutasSicetac-muestra-202608.csv`

| Columna | Significado | Origen | Unidad |
|---|---|---|---|
| `ID` | Identificador de la fila en origen | `DWSice.dbo.tblRutasSicetac.ID` | — |
| `PERIODO` | Mes de vigencia de la matriz (`AAAAMM`) — en este extracto siempre `202608` | `.PERIODO` | AAAAMM |
| `CONFIGURACION` | Configuración de ejes del vehículo | `.CONFIGURACION` | código |
| `VALOR` | Componente base del piso, sin el ajuste por hora — **no es el valor que efectivamente se cotiza** | `.VALOR` | COP |
| `NOMBRERUTA` | Texto libre `"ORIGEN - DESTINO"`, tal como lo usa el motor de matching por `LIKE` | `.NOMBRERUTA` | texto |
| `NOMBRETIPOCARGA` | Tipo de carga | `.NOMBRETIPOCARGA` | categórico |
| `NOMBREUNIDADTRANSPORTE` | Unidad de transporte | `.NOMBREUNIDADTRANSPORTE` | categórico |
| `ORIGEN` / `DESTINO` | Código de municipio DANE | `.ORIGEN`/`.DESTINO` | código DANE |
| `VALORHORA` | Componente del valor por hora de recorrido | `.VALORHORA` | COP |
| `VALORSICECONHORA` | **El valor que efectivamente se cotiza** — `VALOR` + el componente por hora. Verificado en esta HU cruzando 6 cotizaciones reales: el motor usa este campo, no `VALOR` | `.VALORSICECONHORA` | COP |
| `HORASRECORRIDO` / `HORASPACTADAS` | Horas reales estimadas vs. pactadas | `.HORASRECORRIDO`/`.HORASPACTADAS` | horas |
| `CONDICIONCARGA` | `CARGADO` o `VACIO` | `.CONDICIONCARGA` | categórico |
| `VIAESTANDAR` | Si la vía es estándar | `.VIAESTANDAR` | `SI`/`NO` |
| `Muestra` | Agregada en la extracción: `nucleo` o una de las 5 categorías `rara_*` | — (no existe en la tabla origen) | categórico |

### `tarifa-plaza-tblSAPTarifas-2022-09-01_a_hoy.csv`

| Columna | Significado | Origen | Unidad |
|---|---|---|---|
| `ID` | Identificador único de la tarifa en SAP | `dbSolucionesTI.dbo.tblSAPTarifas.ID` | — |
| `LINEANEGOCIO` / `LineaNegocioDescripcion` | **Llave de cruce** con `lineas_solicitud.LineaNegocio` | `.LINEANEGOCIO` + `tblSAPLineaNegocio.DESCRIPCION` | código / texto |
| `ORIGEN` / `DESTINO` | Código DANE o zona comercial (`ZC_`/`ZTC_`) | `.ORIGEN`/`.DESTINO` | código |
| `VALOR` | El valor de la tarifa — verificado como el campo que sí se cotiza en `Fuente=Tarifario` (8/8 casos reales coincidieron) | `.VALOR` | COP |
| `VIGENCIA` | Fecha hasta la que aplica — cruda, sin la corrección local `AjusteVigenciaPlaza` | `.VIGENCIA` | fecha |
| `CLASECONTENEDOR` / `TAMANOCONTENEDOR` | Clase y tamaño de contenedor, si aplica | `.CLASECONTENEDOR`/`.TAMANOCONTENEDOR` | código / pies |
| `RANGOPESOINF` / `RANGOPESOSUP` | Rango de peso al que aplica | `.RANGOPESOINF`/`.RANGOPESOSUP` | kg |
| `TIPOVEHICULO` / `VehiculoDescripcion` | Tipo de vehículo | `.TIPOVEHICULO` + `tblSAPTiposVehiculoTarifario.DESCRIPCION` | código / texto |
| `TIPOCARROSERIA` | Tipo de carrocería | `.TIPOCARROSERIA` | categórico |
| `PESOLIMITE` | Peso límite adicional | `.PESOLIMITE` | kg |
| `FECHACREACION` / `USUARIOCREACION` | Auditoría de carga | `.FECHACREACION`/`.USUARIOCREACION` | fecha/hora / texto |
| `CLASEVIAJE` | **Llave de cruce** con `lineas_solicitud.ClaseViaje** — determina si el matching real escala a zonas | `.CLASEVIAJE` | código |
| `DEVOLUCION` | Si es tarifa de devolución (ej. contenedor vacío) | `.DEVOLUCION` | booleano |
| `FECHAMODIFICACION` / `USUARIOMODIFICACION` | Auditoría de última edición | `.FECHAMODIFICACION`/`.USUARIOMODIFICACION` | fecha/hora / texto |
| `DETALLESTARIFA` | Detalle en texto libre | `.DETALLESTARIFA` | texto |
| `CONSOLIDADO` / `EXCLUSIVO` | Banderas de condición del servicio | `.CONSOLIDADO`/`.EXCLUSIVO` | booleano |

### `historico-sap-tblSAPTarifasLog-2022-09-01_a_hoy.csv`

Mismas columnas que `tblSAPTarifas` (sin `FECHAMODIFICACION`, `USUARIOMODIFICACION`,
`DETALLESTARIFA`, `CONSOLIDADO`, `EXCLUSIVO` — cada cambio genera fila nueva en
vez de editar), más:

| Columna | Significado | Origen | Unidad |
|---|---|---|---|
| `ID_TARIFA` | Enlaza con la fila de `tblSAPTarifas` que la originó | `.ID_TARIFA` | — |

**Pendiente de confirmar con negocio**: si una fila aquí es una tarifa que
realmente se aplicó a un envío ("tarifa ganada") o un registro de auditoría de
cada edición del catálogo `tblSAPTarifas` — ver
`./datos/fuentes-corporativas/README.md` para el detalle de por qué
no se pudo resolver solo con el código.

### `catalogo-clientes-BUT000-organizaciones.csv`

| Columna | Significado | Origen | Unidad |
|---|---|---|---|
| `PARTNER` | Identificador del socio de negocio | `DWCubosOperacionesEBS.dbo.BUT000.PARTNER` | — |
| `TYPE` | Tipo de socio — en este extracto siempre `2` (organización) | `.TYPE` | código |
| `BU_GROUP` | Grupo de agrupación del partner | `.BU_GROUP` | código |
| `NAME_ORG1` / `NAME_ORG2` | Razón social | `.NAME_ORG1`/`.NAME_ORG2` | texto |
| `TAXNUM` / `TAXNUMXL` | **Llave de cruce** — NIT, mismo dato que el NIT de `catalogo_clientes.csv` (lado propio) | `.TAXNUM`/`.TAXNUMXL` | NIT |
| `TEL_NUMBER` | Teléfono de contacto | `.TEL_NUMBER` | texto |
| `CRDAT` | Fecha de creación en SAP | `.CRDAT` | fecha/hora |
| `VALID_TO` | Hasta cuándo es válido (normalmente sin vencimiento) | `.VALID_TO` | fecha |

## Llaves de cruce (linaje entre lado propio y externo)

| Dimensión | Columna lado propio | Columna lado externo |
|---|---|---|
| Cliente | `catalogo_clientes.Nit` (o campo equivalente) | `catalogo-clientes-BUT000-organizaciones.csv → TAXNUM` |
| Vehículo/servicio | `lineas_solicitud.LineaNegocio`/`ClaseViaje`/`ClaseContenedor` | `tarifa-plaza-tblSAPTarifas...csv → LINEANEGOCIO`/`CLASEVIAJE`/`CLASECONTENEDOR` |
| Ruta | `catalogo_rutas.Origen`/`Destino` (texto libre) | `piso-sicetac...csv → NOMBRERUTA` (texto libre) / `tarifa-plaza...csv → ORIGEN`/`DESTINO` (código DANE o zona) |
| Período | `cotizacion_lineas.CreatedAt` / `cotizaciones.CreatedAt` | `tarifa-plaza...csv → VIGENCIA` / `piso-sicetac...csv → PERIODO` (un solo valor, `202608`) |
| Valor cotizado vs. valor de mercado | `cotizacion_lineas.ValorFlete` + `Fuente` | `VALORSICECONHORA` (si `Fuente=Sicetac`) o `VALOR` (si `Fuente=Tarifario`) |
