# Diccionario reconciliado — Lado externo (`fuentes-corporativas`)

Diccionario de las 4 tablas del lado externo (SICE-TAC, SAP Tarifario, SAP histórico, clientes SAP) del set `v1.0.0-piloto`, cruzando la definición oficial (`diccionario-y-linaje.md` y `DICCIONARIO_DATOS.md`, ambos de HU-10.3) con lo que confirmó el análisis a lo largo de los 8 notebooks de este EDA. Ver [`diccionario_reconciliado_bd_propia.md`](diccionario_reconciliado_bd_propia.md) para el lado propio (CabinaFletes), `hallazgos.md` para el detalle completo de cada hallazgo referenciado (H-0xx) y `limitaciones.md` para las preguntas que este set no puede responder (L-0x).

**Nombres de columna en código:** mismo criterio que el EDA-1 — camelCase, sin tildes ni espacios (notebook `00`, Paso 4).

**Ejemplos:** valores reales tomados directamente de los CSV de `v1.0.0-piloto` (no inventados).

---

### `piso-sicetac` (`tblRutasSicetac`, muestra `202608`)

Se carga separando dos subconjuntos, **`núcleo`** (126 filas) y **`raras`** (25 filas) — nunca se mezclan, para no sesgar hacia los casos extremos elegidos a propósito en la muestra (log de supuestos, #2).

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `ID` | `id` | Identificador de la fila en origen | — | `66621203` | Confiable |
| `PERIODO` | `periodo` | Mes de vigencia de la matriz (`AAAAMM`) | AAAAMM | `202608` | **Un solo período (`202608`) en todo el extracto** — no hay profundidad histórica en esta muestra (L-07). Las tablas `MesAnterior*` podrían tenerla, pero no se pudo confirmar con evidencia trazable (H-016, retirado) |
| `CONFIGURACION` | `configuracion` | Configuración de ejes del vehículo | código | `2L2`, `3S2` | Sin hallazgos relevantes |
| `VALOR` | `valor` | Componente base del piso, sin el ajuste por hora | COP | `2399182.00000` | **No es el valor que efectivamente se cotiza** — ver `VALORSICECONHORA` |
| `NOMBRERUTA` | `nombreruta` | **Llave de cruce dominante** (H-004) — texto libre `"ORIGEN - DESTINO"` | texto | `BARRANQUILLA ATLANTICO - LOS PATIOS NORTE DE SANTANDER` | El motor real hace *matching* por substring de ciudad contra este campo — nuestra aproximación con `rapidfuzz` (sobre `catalogo_rutas`, lado propio) coincide con el mecanismo real (H-004, memoria del proyecto) |
| `NOMBRETIPOCARGA` | `nombretipocarga` | Tipo de carga | categórico | `Carga Refrigerada`, `Contenedor cargado` | Sin hallazgos relevantes |
| `NOMBREUNIDADTRANSPORTE` | `nombreunidadtransporte` | Unidad de transporte | categórico | `FURGON REFRIGERADO`, `PORTACONTENEDORES` | Sin hallazgos relevantes |
| `ORIGEN`/`DESTINO` | `origen`/`destino` | Código de municipio DANE | código DANE | `8001000` / `54405000` | Sin hallazgos propios en este EDA (ver `NOMBRERUTA` para el *matching* real) |
| `VALORHORA` | `valorhora` | Componente del valor por hora de recorrido | COP | `76140.00000` | Sin hallazgos relevantes |
| `VALORSICECONHORA` | `valorsiceconhora` | **El valor que efectivamente se cotiza** | COP | `3008302.00000` | Heredado de HU-10.3 (confirmado 6/6 contra cotizaciones reales); en este EDA, 0 de 12 líneas `Fuente=Sicetac` válidas (lado propio) encontraron candidato comparable — no por falla del campo, sino porque la muestra de 151 filas no cubre esas rutas (H-005, H-007) |
| `HORASRECORRIDO`/`HORASPACTADAS` | `horasrecorrido`/`horaspactadas` | Horas reales estimadas vs. pactadas | horas | `12.70` / `8` | Sin hallazgos relevantes |
| `CONDICIONCARGA` | `condicioncarga` | `CARGADO` o `VACIO` | categórico | `CARGADO`, `VACIO` | Filtrar siempre por `CARGADO` al comparar contra carga real — un primer intento que no lo hizo produjo un falso "-54% bajo el piso legal" (H-005) |
| `VIAESTANDAR` | `viaestandar` | Si la vía es estándar | `SI`/`NO` | `SI`, `NO` | Sin hallazgos relevantes |
| `Muestra` | `muestra` | `nucleo` o una de las 5 categorías `rara_*` (agregada en la extracción, no existe en la tabla origen) | categórico | `nucleo`, `rara_ambiguedad_567_candidatos_misma_ruta`, `rara_valor_maximo` | 4 ID se repiten en `raras` (`rara_valor_maximo` + `rara_via_no_estandar` simultáneamente) — no es error, representa rutas extremas en 2 sentidos a la vez (H-012) |

### `tarifa-plaza` (`tblSAPTarifas`)

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `ID` | `id` | Identificador único de la tarifa en SAP | — | `9820` | Confiable |
| `LINEANEGOCIO`/`LineaNegocioDescripcion` | `lineanegocio`/`lineaNegocioDescripcion` | **Llave de cruce secundaria/complementaria** con `lineas_solicitud.LineaNegocio` (lado propio) — la ruta (`ORIGEN`/`DESTINO`, ver abajo) es la llave dominante, confirmado por el negocio (H-004) | código/texto | `4` / `Oferta de Expedición Nacional` | Requiere traducción fija (ver `lineas_solicitud.LineaNegocio`, lado propio) (H-003) |
| `ORIGEN`/`DESTINO` | `origen`/`destino` | **Llave de cruce dominante** — el motor real de costeo filtra principalmente por ruta, no por `LINEANEGOCIO`/`CLASEVIAJE`/`CLASECONTENEDOR` (confirmado por el negocio, H-004). Código DANE o zona comercial (`ZC_`/`ZTC_`) | código | `ZTC_VABURRA` / `ZTC_BOGOTA` | El mecanismo real de resolución (`tblsapdane`, luego `tblZonasTarificacionRelacion`) no está en los 4 extractos — solo aproximado con `rapidfuzz` (H-004, L-02, L-10) |
| `VALOR` | `valor` | El valor de la tarifa | COP | `1200000` | Confirmado como el campo que sí se cotiza en `Fuente=Tarifario` (32/32 casos reales coincidieron exacto, ampliando el 8/8 de HU-10.3) (H-007). Outliers de precio (IQR) concentrados en Devolución Contenedor (4,7%) y Nacional (2,4%, mayor cantidad absoluta); 9 filas en \$0 (0,04%), todas Devolución Contenedor sin vehículo especificado (H-014) |
| `VIGENCIA` | `vigencia` | Fecha hasta la que aplica | fecha | `2025-05-30 00:00:00` | Cruda, sin la corrección local `AjusteVigenciaPlaza` — no se sabe qué tarifa estaba realmente vigente en la práctica en cada momento (L-02) |
| `CLASECONTENEDOR`/`TAMANOCONTENEDOR` | `clasecontenedor`/`tamanocontenedor` | Clase y tamaño de contenedor, si aplica | código/pies | `1` / *(vacía en todo el extracto)* | `CLASECONTENEDOR` sin mapeo fijo confirmado (ver `lineas_solicitud.ClaseContenedor`, lado propio) (H-003, L-03) |
| `RANGOPESOINF`/`RANGOPESOSUP` | `rangopesoinf`/`rangopesosup` | Rango de peso al que aplica | kg | `4000` / `10000` | Sin hallazgos relevantes |
| `TIPOVEHICULO`/`VehiculoDescripcion` | `tipovehiculo`/`vehiculoDescripcion` | Tipo de vehículo | código/texto | `8` / `Liviano (capacidad 4 ton)` | Sin hallazgos relevantes |
| `TIPOCARROSERIA` | `tipocarroseria` | Tipo de carrocería | categórico | `9` | **Código sin traducción disponible** — a diferencia de `LINEANEGOCIO`/`TIPOVEHICULO`, no tiene columna de descripción emparejada en ningún extracto ni catálogo identificado; se cargó pero nunca se analizó en este EDA (H-017, L-12) |
| `PESOLIMITE` | `pesolimite` | Peso límite adicional | kg | `4100` | Sin hallazgos relevantes |
| `FECHACREACION`/`USUARIOCREACION` | `fechacreacion`/`usuariocreacion` | Auditoría de carga | fecha/hora, texto | `2024-03-26 07:40:46` / `0` | Sin hallazgos relevantes |
| `CLASEVIAJE` | `claseviaje` | **Llave de cruce secundaria/complementaria** con `lineas_solicitud.ClaseViaje` (lado propio) — la ruta sigue siendo la llave dominante (H-004) | código | `2` | Con la traducción fija sube a 100% de match (5/5) (H-003) |
| `DEVOLUCION` | `devolucion` | Si es tarifa de devolución (ej. contenedor vacío) | booleano | `False`, `True` | Se excluye (`lineanegocio="6"`) al cruzar por ruta normalizada — es reposicionamiento de vacío, no carga real (H-005) |
| `FECHAMODIFICACION`/`USUARIOMODIFICACION` | `fechamodificacion`/`usuariomodificacion` | Auditoría de última edición | fecha/hora, texto | `2026-09-01 16:50:13` / `0` | Sin hallazgos relevantes |
| `DETALLESTARIFA` | `detallestarifa` | Detalle en texto libre | texto | `Sujeto a Disponibilidad de Minimula en Plancha` | Sin hallazgos relevantes |
| `CONSOLIDADO`/`EXCLUSIVO` | `consolidado`/`exclusivo` | Banderas de condición del servicio | booleano | `False`, `True` | Sin hallazgos relevantes |

### `historico-sap` (`tblSAPTarifasLog`)

Mismas columnas que `tarifa-plaza` (sin `FECHAMODIFICACION`, `USUARIOMODIFICACION`, `DETALLESTARIFA`, `CONSOLIDADO`, `EXCLUSIVO` — cada cambio genera fila nueva en vez de editar), más:

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `ID_TARIFA` | `idTarifa` | Enlaza con la fila de `tblSAPTarifas` que la originó | — | `12644` | Sin hallazgos relevantes propios |
| *(resto de columnas, ver `tarifa-plaza` arriba)* | — | — | — | Ej. `LINEANEGOCIO=6`, `ORIGEN=ZTC_BOGOTA`, `DESTINO=ZC_BUENAVENTURA`, `VALOR=1000000`, `VIGENCIA=2024-05-30 00:00:00` | **La tabla completa se comporta más como auditoría de ediciones que como registro de "tarifa aplicada"** — solo 35% de las tarifas activas (8.135 de 23.020) tiene alguna versión aquí, mediana 2 versiones; evidencia indirecta, pendiente de confirmación de negocio (H-010, L-05). El salto de +17% que encontró el EDA-1 en marzo 2026 **no se replica limpio** aquí — las 4 categorías principales de `VALOR` bajan de febrero a marzo (H-011, L-09) |

### `catalogo-clientes-BUT000-organizaciones` (`BUT000`)

| Columna (original) | Nombre en código | Definición oficial | Unidad/moneda | Ejemplo | Confiabilidad y notas de reconciliación |
|---|---|---|---|---|---|
| `PARTNER` | `partner` | Identificador del socio de negocio | — | `1000` | Confiable |
| `TYPE` | `type` | Tipo de socio — siempre `2` (organización) en este extracto | código | `2` | Sin hallazgos relevantes |
| `BU_GROUP` | `buGroup` | Grupo de agrupación del partner | código | `MDM0` | Sin hallazgos relevantes |
| `NAME_ORG1`/`NAME_ORG2` | `nameOrg1`/`nameOrg2` | Razón social | texto | `Eduardo Botero Soto S.A.` / `.A.S.` | Sin hallazgos relevantes |
| `TAXNUM`/`TAXNUMXL` | `taxnum`/`taxnumxl` | **Llave de cruce** — NIT, mismo dato que `catalogo_clientes.Nit` (lado propio) | NIT | `8909013215` / *(vacía en todo el extracto)* | `TAXNUMXL` **100% nula** (0 de 38.995) — no aporta señal en este extracto (H-009) |
| `TEL_NUMBER` | `telNumber` | Teléfono de contacto | texto | `5765555` | Sin hallazgos relevantes |
| `CRDAT` | `crdat` | Fecha de creación en SAP | fecha/hora | `14/10/2016 2:29:34 p.m.` | Sin hallazgos relevantes |
| `VALID_TO` | `validTo` | Hasta cuándo es válido (normalmente sin vencimiento) | fecha | `31/12/9999 11:59:59 p.m.` | **100% de las 38.995 filas tiene el valor centinela `"9999-12-31 23:59:59"`** — ninguna fecha de vencimiento real, sin señal en este extracto (H-009) |

---

## Qué no cubre este diccionario (ver `limitaciones.md` para el detalle completo)

- El mapeo completo `ClaseContenedor` ↔ código SAP (falta `tblSAPClaseContenedor`, ausente en los 4 extractos).
- La resolución real ciudad → zona SAP (`tblsapdane`/`tblZonasTarificacionRelacion`, ausentes en los 4 extractos) — solo se aproximó con `rapidfuzz`.
- Si `tblSAPTarifasLog` (`historico-sap`) representa tarifa realmente aplicada o solo auditoría de ediciones — evidencia indirecta, no confirmación de negocio.
- Profundidad histórica real de SICE-TAC más allá de un mes (`piso-sicetac` es una sola foto de `202608`).
