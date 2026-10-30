# Origen y decisiones del seeder

De dónde sale cada dato inicial de NEXO (`../seeder/`) y qué reglas se aplicaron. Qué guarda
cada tabla está en `DICCIONARIO-DATOS.md`.

Los datos iniciales se calcularon una vez, con consultas directas a las fuentes, y quedaron
fijos en los archivos del seeder. No hay ningún proceso que los mantenga sincronizados: si una
fuente cambia (una ciudad nueva en el DW, un costo nuevo en el Excel), hay que regenerar el
archivo a mano. Los Excel de negocio se guardan fuera del repositorio.

## Resumen

| Archivo | Tabla | Filas | Fuente |
|---|---|---|---|
| `datos_base_nexo.sql` | `tbl_usuarios`, `tbl_mapeo_rol_portal`, `tbl_parametros_score` | 1 + 9 + 1 | Datos que el modelo del backend declara con `HasData` |
| `municipios.csv` | `tbl_municipios` | 3.804 | DW corporativo: `tblSAPDANE`, `tblZonasTarificacionRelacion`, `tblSAPTarifas` |
| `pesos.csv` | `tbl_pesos` | 8 | Bandas de negocio (`RANGOS PESOS APP`, `FLETES PLAZA.xlsx`) |
| `reglas_solo_sicetac.csv` | `tbl_reglas_solo_sicetac` | 260 | `FLETES PLAZA.xlsx`, celdas marcadas «sice» |
| `tarifas_plaza.csv` | `tbl_tarifas_plaza` | 107 | `FLETES PLAZA.xlsx`, hoja «Fletes puertos» |
| `servicios_conexos.csv` | `tbl_servicios_conexos` | 1.336 | Excel de costos de conexos 2026 |
| `alias_elementos.csv` | `tbl_alias_elementos` | 71 | Diccionario de configuraciones de vehículo de negocio |
| `mapeo_vehiculos.csv` | `tbl_mapeo_vehiculo_tarifario` | 15 | Revisión de cada vehículo contra SICE-TAC y Plaza/SAP |
| `rangos_peso_contenedor.csv` | `tbl_rangos_peso_contenedor` | 10 | Rangos de peso de contenedores de negocio |
| `tipos_carroceria_catalogo.csv` | `tbl_tipos_carroceria_catalogo` | 17 | Catálogo de carrocerías del Cubicador (mismos Id) |

## Datos base (`datos_base_nexo.sql`)

Filas sin las que una base nueva no funciona. El modelo del backend las declara con `HasData`,
pero como el proyecto no usa migraciones, nunca se insertan solas.

- **Usuario de sistema** (`Sistema (placeholder)`, Id `...0001`): autor de lo que NEXO hace por sí
  solo.
- **9 mapeos de rol del portal a rol de NEXO.** Los 3 primeros traducen el rol por su nombre; los
  demás, por el Id del rol en el portal (`IDROLES` de `tblSAPRoles`). El rol de portal 49 conserva
  el nombre «CoordinadorFletesNEXO» y se traduce a `AdminBiblioteca`. Sin estos mapeos, quien entra
  desde el portal se autentica pero queda sin rol.
- **Parámetros del score de prioridad** (fila única, Id 1).

## Municipios (`municipios.csv`)

- 3.804 municipios: Colombia 1.128, Venezuela 1.424 y Ecuador 1.252.
- Cruce de `tblSAPDANE` (códigos DANE/DIVIPOLA), `tblZonasTarificacionRelacion` y
  `tblSAPTarifas`/`tblSAPTarifasLog` (`dbSolucionesTI`).
- La llave es un `Id` autoincremental y no `CodigoDane`: el código DANE se repite entre Venezuela y
  Ecuador. La llave natural es `(CodigoDane, Pais)`, pero con `Id` las demás tablas (por ejemplo,
  `tbl_tarifas_plaza`) no cargan una llave compuesta.
- `CodigoDane` es texto (conserva los ceros a la izquierda).

## Bandas de peso (`pesos.csv`)

- 8 bandas: las 7 de negocio (3-15.000, 15.001-18.000, 18.001-23.000, 23.001-30.000,
  30.001-34.000, PBV 7.500-8.000 y PBV más de 10.500 kg) y una banda real del sistema externo
  (14.001-20.000) que no calzaba con ninguna de las 7.
- No es un catálogo cerrado: se pueden agregar bandas desde la aplicación.
- `PesoMaximo` vacío significa banda abierta, sin tope.

## Reglas Solo-SICETAC (`reglas_solo_sicetac.csv`)

- Cada celda de `FLETES PLAZA.xlsx` con el valor «sice» es una regla: para ese origen, destino y
  tipo de vehículo, la tarifa se resuelve solo con SICE-TAC.
- `Origen` y `Destino` son **códigos DANE** (por ejemplo, `76109000` = Buenaventura), alineados con
  `tbl_municipios`.
- `TipoVehiculo` usa la nomenclatura de **SICE-TAC**: Turbo y Sencillo son `2` (no `C2`, que es el
  código de SAP Tarifario), y el camión liviano PBV 7.500-8.000 kg es `2L3`.
- Las filas de «Devolución de contenedor vacío» no son un tipo de vehículo y no forman parte de esta
  tabla.
- 260 reglas, sin repetir la llave `(Origen, Destino, TipoVehiculo)`.

| Etiqueta en el Excel | `TipoVehiculo` |
|---|---|
| Camión dos ejes PBV más de 10.500 kg; TURBO; SENCILLO | `2` |
| Camión dos ejes livianos PBV 7.500 a 8.000 kg | `2L3` |
| Tractocamión dos ejes con semirremolque dos ejes (hasta 15.000 y 15.001-18.000 kg); MINI MULA | `2S2` |
| Tractocamión dos ejes con semirremolque tres ejes hasta 23.000 kg | `2S3` |
| Tractocamión tres ejes con semirremolque dos ejes hasta 30.000 kg | `3S2` |
| Tractocamión tres ejes con semirremolque tres ejes hasta 34.000 kg; MULA | `3S3` |

## Tarifas de plaza (`tarifas_plaza.csv`)

- Solo filas de la hoja «Fletes puertos», que es la que tiene precio real. Las hojas «Fletes
  Nacional y Expo» y «Devolución cont vacíos» son solo-SICE: sus rutas están en las reglas
  Solo-SICETAC, no aquí.
- Todas son importación por contenedor: `LineaNegocio` «Oferta de Expedición Puerto a interior»,
  `ClaseViaje` «Contenedor» y `ClaseContenedor` «CNT40.HC».
- `PesoId` apunta a `tbl_pesos` y `TipoVehiculo` se deriva de esa banda: 1 y 2 → `2S2`, 3 → `2S3`,
  4 → `3S2`, 5 → `3S3`, 6 → `2L3`, 7 → `2`.
- `OrigenMunicipioId` y `DestinoMunicipioId` son el `Id` de `tbl_municipios` (por ejemplo, `1012` =
  Buenaventura), no el código DANE. Si se carga en una base con otros Id de municipio, hay que
  resolverlos de nuevo.
- `LineaNegocio`, `ClaseViaje` y `ClaseContenedor` guardan el nombre completo del catálogo SAP, no
  su Id numérico: ese Id pertenece a otra base y no tiene llave foránea posible.
- 107 filas: las 97 de la hoja menos 5 que tenían un destino combinado
  («CAJICA-ZIPAQUIRA-TOCANCIPA»), separadas en una fila por municipio (15).
- `Vigencia`: el Excel no trae fecha por fila; se usó `2026-09-30` como fecha de referencia.
- `DetallesTarifa`: la nota del Excel cuando existe; si no, un texto genérico.
- `tbl_tarifas_plaza_historico` no se carga: la llena el trigger `TRG_tarifas_plaza_historico`
  cuando se modifica una tarifa.

## Servicios conexos (`servicios_conexos.csv`)

- Cruce del Excel de costos de conexos 2026 contra `tbl_municipios`. Cuatro tipos de servicio:
  Almacenamiento por ciudad (9), ITR por puerto, máquina y movimiento (21), Precinto por ruta (5) y
  Acompañamiento vehicular por origen-destino (1.301).
- Una sola tabla con columnas de dimensión (`Origen`, `Destino`, `Puerto`, `Ruta`, `TipoMaquina`);
  la que no aplica a un servicio vale `NO APLICA`.
- `CodigoServicio` es el código del Excel cuando existe (`PRE_BSAT`, `AVT_AUTO`...); si no, el
  mismo `TipoServicio`.
- `Origen` y `Destino` son texto, no llave foránea: el Excel mezcla ciudades con peajes y
  corredores viales («Peaje De Occidente», «Cali - Yumbo»). Si el nombre cruza con
  `tbl_municipios` (sin tildes ni mayúsculas, con un diccionario de alias como «Bogota» →
  «BOGOTA, D.C.» o «Cartagena» → «CARTAGENA DE INDIAS»), se guarda el nombre del catálogo; si no,
  el texto del Excel. Los nombres que existen en varios departamentos («Rionegro») se guardan sin
  departamento.
- `TipoMaquina` combina tipo de máquina y tipo de movimiento («Maquina 3.5 - 2 Movimientos»),
  porque juntos determinan el precio del ITR.
- `Unidad`: «Por estiba» para Almacenamiento e ITR y «Por unidad» para los demás.
- `PrecioVenta` va vacío en el repositorio: los precios de venta no se versionan. Para cargarlos,
  se genera en local `servicios_conexos_precios.local.csv` (con
  `generar_precios_venta_local.py`, que lee los Excel de precios); el script de carga lo usa en
  lugar de `servicios_conexos.csv` si existe. Un servicio sin precio de venta cotiza con su costo.

| Tipo | Columnas del Excel → tabla |
|---|---|
| Almacenamiento | Tipo de servicio → `CodigoServicio` y `TipoServicio`; Ciudad → `Origen`; Costo 2026 → `CostoUnitario`; Vigencia → `Vigencia` |
| ITR | Además: Puerto → `Puerto`; Tipo de máquina + Tipo de movimiento → `TipoMaquina` |
| Precinto / barra satelital | Código → `CodigoServicio`; Descripción → `TipoServicio`; Ruta → `Ruta` |
| Acompañamiento vehicular | Código → `CodigoServicio`; Descripción → `TipoServicio`; Origen → `Origen`; Destino → `Destino` |

## Alias de vehículos y contenedores (`alias_elementos.csv`)

- Cada variante de texto libre del diccionario de negocio (por ejemplo, «Turbo», «TB») es una fila
  con el código oficial de su columna.
- Un solo diccionario para vehículo y contenedor, con `TipoElemento` para distinguirlos.
- `Codigo` es texto, no llave foránea. Para vehículo es el código que comparten NEXO y el Cubicador
  (`2L2` Turbo, `C2` Sencillo, `C3` Dobletroque, `2S2` Minimula, `3S3` Tractomula); para contenedor,
  el de `tblSAPClaseContenedor` (`CNT40.HC`, `CNT20.ST`).
- `AliasNormalizado` es la clave de comparación (única): mayúsculas, sin tildes, `_` y `-` como
  espacio, con el mismo algoritmo que usa el backend al capturar.
- 71 alias (48 de vehículo y 23 de contenedor), sin choques entre columnas. Se conservan «40» y «20»
  a secas porque así vienen en el diccionario.

## Mapeo de vehículos (`mapeo_vehiculos.csv`)

SICE-TAC y Plaza/SAP nombran el mismo vehículo con códigos distintos. Esta tabla dice, por cada
tipo de vehículo de NEXO, con qué código se busca en cada fuente. Sin ella, una búsqueda con el
código equivocado no encuentra filas.

- `TipoVehiculo` es el código de NEXO (el que queda después de resolver los alias) y la llave.
- `CodigoSicetac` es el valor de `CONFIGURACION` en `DWSice.dbo.tblRutasSicetac`.
- `CodigoPlazaSap` es el código en `tblSAPTarifas` y en `tbl_tarifas_plaza` (usan el mismo
  vocabulario). Vacío significa que el vehículo no tiene tarifa de plaza.
- `Verificacion` explica de dónde salió cada código.

| Vehículo | Regla |
|---|---|
| Sencillo (`C2`) | `2` en SICE-TAC y en Plaza/SAP (en Plaza hay unas 5.000 tarifas con `2` y solo 14, vencidas, con `C2`) |
| Turbo (`2L1`, `2L2`, `2L3`) | `2L2` en Plaza/SAP; cada uno conserva su código en SICE-TAC |
| Dobletroque (`C3`) | `3` en SICE-TAC, `C3` en Plaza/SAP |
| `2_5`, `2_7`, `3S2`, `CA_`, `V2`, `V3`, `V4` | Solo existen en SICE-TAC: sin código de Plaza |

## Rangos de peso de contenedor (`rangos_peso_contenedor.csv`)

- Qué vehículo corresponde a un contenedor de 20 o 40 pies según su peso.
- `TipoVehiculo` es el código de NEXO, con llave foránea al mapeo de vehículos (por eso el camión de
  dos ejes es `C2` y no `2`).
- Se toma, entre las filas del tamaño de contenedor, la de menor `PesoMaximo` que alcance para el
  peso. Por encima del tope (34.000 kg en 40 pies, 30.000 kg en 20 pies) ningún vehículo aplica.
- No hay fila para el camión de dos ejes en 40 pies: el documento de negocio no le pone tope.
- Estos rangos son distintos de los de SAP (`tblRangoPeso`, `tblContenedorRangoPeso`). Se usan estos
  porque salen del documento de negocio y cuadran con `tbl_pesos` y las tarifas de plaza.

| Contenedor | Vehículo | SAP | Este seeder |
|---|---|---|---|
| 20 | Camión dos ejes | 0 a 8.500 | 0 a 8.000 |
| 20 | 2S2 | 8.501 a 14.000 | 8.001 a 14.000 |
| 20 | 2S3 | 14.001 a 20.000 | 14.001 a 18.000 |
| 20 | 3S2 | no existe | 18.001 a 25.000 |
| 20 | 3S3 | 20.001 a 30.000 | 25.001 a 30.000 |
| 40 | 2L3 | no existe | 0 a 8.000 |
| 40 | 2S2 | 0 a 18.000 | 8.001 a 18.000 |
| 40 | 2S3 | 18.001 a 22.000 | 18.001 a 23.000 |
| 40 | 3S2 | no existe | 23.001 a 30.000 |
| 40 | 3S3 | 22.001 a 34.000 | 30.001 a 34.000 |

## Catálogo de carrocerías (`tipos_carroceria_catalogo.csv`)

- Es la copia en NEXO del catálogo de carrocerías del Cubicador. En operación la mantiene
  sincronizada la aplicación con los eventos que publica el Cubicador al crear, editar o borrar una
  carrocería.
- El Cubicador no reenvía las carrocerías que ya existen, así que una base nueva la necesita
  cargada desde el inicio. Son las 17 del seeder del Cubicador, con los mismos Id.

## Tablas sin datos iniciales

- **Las llena la aplicación al trabajar:** `tbl_rutas` y `tbl_configuraciones_vehiculo` (al capturar
  solicitudes), `tbl_clientes` (copia local del DW al cotizar), `tbl_usuarios` (al entrar desde el
  portal), `tbl_cadena_priorizacion` (al arrancar), `tbl_tipos_mercancia_catalogo` (eventos del
  Cubicador) y `tbl_vehiculos_cubicacion` (planes de cargue).
- **Historial de operación:** solicitudes, cotizaciones, auditoría, tarifas históricas, planes de
  cargue, asistente, uso de IA y mensajería.

## Preguntas abiertas

- `tarifas_plaza.csv` usa `3S2` y `2L3`, que no coinciden con el código de plaza del mapeo de
  vehículos (vacío y `2L2`). Falta decidir si se corrige la tarifa o el mapeo.
- La vigencia `2026-09-30` de las tarifas de plaza es una fecha de referencia; falta confirmarla
  antes de usarlas en producción.
- `Unidad` de los servicios conexos es provisional hasta que negocio la defina.
