# Propuesta de BD — Motor de selección de tipo y cantidad de vehículos

Diseño de tablas para el nuevo motor descrito en `propuesta-seleccion-vehiculos.md`
(§4 "Modelo de datos"). Vive en su propio esquema SQL, `dbo_cubicador`,
dentro de la base compartida `dbTarifario_Nexo_Pruebas` — no en `db_NEXO`
(ver `cubicador/database/README.md` para el detalle de dónde y cómo).

Documento vivo — se va ajustando tabla por tabla a medida que se revisa cada
una. **Actualizado 2026-10-08** cruzando esta propuesta contra un seeder
real enviado por un colega (Brayan) y contra el catálogo real de SAP
(`tblSAPTiposCarroseria`) — ver
`cubicador/database/seeder/ORIGEN-Y-DECISIONES.md` para el detalle completo
de qué cambió y por qué en cada tabla.

---

## Tabla 1 — `vehiculos` ✅ revisada

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid | PK |
| `CodigoVehiculo` | texto | clave corta, ej. `C2`, `C3`, `3S3` |
| `NombreVehiculo` | texto | ej. "Tractomula 3S3" |
| `LargoCm` | decimal (cm) | |
| `AnchoCm` | decimal (cm) | |
| `AlturaMaximaReglamentariaCm` | decimal (cm) | límite **legal** de altura para ese tipo de vehículo, no una medida de fábrica — **varía por vehículo**; `DEFAULT (440)` solo como respaldo para un vehículo nuevo sin este dato todavía |
| `CapacidadMaxima` | decimal (kg) | |
| `Activo` | booleano | |
| `CreadoPor` | texto/FK usuario | |
| `CreatedAt` / `UpdatedAt` | datetime | |

**Llenado:** seeder con Excel "Dimensiones y peso por vehículo".

**Cambios acordados sobre el borrador original:**
- Se separó `Codigo` de `Nombre` (antes mezclados en `tipoVehiculo`).
- Medidas convertidas a **centímetros** (antes metros) — cruzadas y
  confirmadas exactas contra un seeder externo.
- **`AlturaMaximaReglamentariaCm` dejó de ser un default fijo de 4.4 m
  para todos.** Esa decisión original se revirtió: el seeder externo trae
  un límite distinto por vehículo (230/240/260 cm) que se repite igual sin
  importar la carrocería, así que es propiedad del vehículo, no un valor
  inventado por defecto. Se renombró la columna también — el seeder
  externo la llama "altura máxima legal", un límite normativo de
  reglamentación de tránsito, no una medida física arbitraria del
  fabricante; el nombre anterior (`AltoVehiculoCm`) no dejaba eso claro.
  Ver `ORIGEN-Y-DECISIONES.md` para el detalle de cómo se confirmó.
- Se agregó `DEFAULT (440)` a la columna — no cambia ninguno de los 5
  vehículos actuales (cada uno trae su valor propio explícito), es solo
  un respaldo para cuando se cree un vehículo nuevo por CRUD sin
  especificar este dato todavía.

---

## Tabla 2 — `TiposCarroceria` ✅ revisada

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid | PK |
| `CodigoCarroceria` | texto | código fijo, ej. `FURGON`, `FURGON_REFRIGERADO`, `ESTACAS` |
| `NombreCarroceria` | texto | nombre legible, ej. "Furgón refrigerado" |
| `Activo` | booleano | |
| `CreadoPor` | texto/FK usuario | |
| `CreatedAt` / `UpdatedAt` | datetime | |

**Llenado:** seeder con Excel "Matriz cubicador", cruzado después con el
catálogo real de SAP (`tblSAPTiposCarroseria`). 17 carrocerías en total.

**Cambios acordados sobre el borrador original:**
- Se separó `Codigo` de `Nombre` (antes un solo `TipoCarroceria`) —
  cruzado contra la estructura de un seeder externo.
- El chequeo de altura queda cubierto a nivel de vehículo (Tabla 1,
  `AlturaMaximaReglamentariaCm`, siempre presente) — no hace falta que la
  carrocería decida si ese chequeo aplica o no, porque siempre aplica.
- Se agregaron 5 carrocerías que no estaban en ningún Excel propio pero sí
  existen en el catálogo real de SAP: `TANQUE`, `TOLVA`, `VOLCO`, `GRUA`,
  `NINERA` — sin emparejamiento con ningún vehículo todavía (queda para el
  futuro CRUD).

---

## Tabla 3 — `tiposMercancia` ✅ revisada

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid | PK |
| `CodigoMercancia` | texto | **agregado** — código corto, ej. `CAJAS_CARTON` |
| `NombreMercancia` | texto | cajas, alimentos empacados, etc. |
| `IdCategoria` | FK → `categoríaMercancia` | |
| `Cubicable` | booleano | ¿es cubicable o queda excluido del todo? |
| `CondicionadaCubicacion` | booleano | ¿requiere que AdminTarifas la valide obligatoriamente antes de aprobarla? — independiente de `Cubicable`, puede ser `true` aunque sí sea cubicable |
| `ComentarioCondicion` | texto | |
| `CarroceriaPrincipal` | FK → `TiposCarroceria`, nullable | nullable cuando `Cubicable=false` (sin carrocería aplicable) |
| `CarroceriaSecundaria` | FK → `TiposCarroceria` | |
| `CarroceríaSecundariaCondicionada` | booleano | |
| `ControlAmbientalRequerido` | texto | **agregado** — `NINGUNO`/`REFRIGERADO`/`VENTILADO`/`CLIMATIZADO`, variantes `_CONDICIONAL` y `_O_` |
| `Apilable` | booleano, nullable | **agregado** — `NULL` cuando no aplica/no se sabe |
| `Fragil` | booleano, nullable | **agregado** |
| `RotacionLibre` | booleano, nullable | **agregado** |
| `Activo` | booleano | |
| `CreadoPor` | texto/FK usuario | |
| `CreatedAt` / `UpdatedAt` | datetime | |

**Llenado:** seeder con Excel "Matriz cubicador" (31 filas), cruzado
después fila por fila contra un seeder externo (mismo contenido, 5
columnas nuevas adoptadas de ahí).

**Sin cambios de fondo sobre el borrador original** — `Cubicable` y
`CondicionadaCubicacion` se mantienen como dos campos independientes (no un
solo enum): una mercancía puede ser cubicable automáticamente Y a la vez
requerir validación obligatoria de AdminTarifas antes de aprobarse — son dos
preguntas distintas, no un solo estado.

**Pregunta abierta de la autora — resuelta:** se mantienen las dos columnas
fijas (`CarroceriaPrincipal`/`CarroceriaSecundaria`), no una tabla de
relación aparte. La propuesta madre ya decidió máximo 2 carrocerías por
tipo, y en los datos reales (31 tipos de mercancía) ninguno supera ese
límite — una tabla de relación resolvería un problema que no existe hoy.

---

## Tabla 4 — `relacionVehiculosCarroceria` ✅ revisada (ajustada)

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid | PK |
| `IdTipoVehiculo` | FK → `vehiculos` | |
| `IdCarroceria` | FK → `TiposCarroceria` | |
| `UsaCapacidadMaximaDelVehiculo` | booleano | **agregado** — default `true` |
| `CapacidadMaximaEspecifica` | decimal (kg) | **agregado** — default `0`; solo se lee cuando `UsaCapacidadMaximaDelVehiculo = false` |
| `CreadoPor` | texto/FK usuario | |
| `CreatedAt` / `UpdatedAt` | datetime | |

**Llenado:** seeder con Excel "Dimensiones y peso por vehículo".

**Por qué se agregaron esas 2 columnas:** resuelve el caso de la
"Tractomula Furgón 3S3" — es el mismo vehículo `3S3` (mismo `Codigo`, que
debe seguir siendo único para hacer matching limpio contra SICE-TAC/
tarifario), pero con Furgón pesa más vacío y carga menos (30.000 kg) que
con Estacas/Plataforma (34.000 kg). En vez de crear una segunda fila de
vehículo con el mismo código (lo que rompería la unicidad), la diferencia
de capacidad vive en la relación específica vehículo+carrocería: por
defecto usa la capacidad del vehículo (`UsaCapacidadMaximaDelVehiculo = true`),
y solo para la combinación `3S3` + `FURGON` se marca `false` y se pone
`CapacidadMaximaEspecifica = 30000` — esa misma restricción se heredó
también a las 3 variantes de Furgón (refrigerado/climatizado/ventilado)
para el `3S3`, no solo al Furgón liso.

---

## Tabla 5 — `categoríaMercancia` ✅ revisada

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid | PK |
| `NombreCategoria` | texto | 13 categorías (ver más abajo) |
| `CreadoPor` | texto/FK usuario | |
| `CreatedAt` / `UpdatedAt` | datetime | |

**Sin cambios de estructura sobre el borrador original** — catálogo
plano, base de la Tabla 3 (`IdCategoria`) y de la Tabla 6 (reglas de
incompatibilidad). **El contenido sí cambió**: se adoptaron las 13
categorías de un seeder externo (`GENERAL`, `ALIMENTOS`, `FARMACEUTICO`,
`PERECEDERO_NO_ALIMENTO`, `FERTILIZANTE`, `MATERIAL_INFLAMABLE`,
`CARBURANTE`, `METALICO`, `CONSTRUCCION`, `VIDRIO`, `GAS_INDUSTRIAL`,
`QUIMICO_OXIDANTE`, `QUIMICO_CORROSIVO`), reemplazando las 9 que había
antes.

---

## Tabla 6 — `reglasIncompatibilidad` ✅ revisada

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid | PK |
| `IdCategoria1` | FK → `categoríaMercancia` | |
| `IdCategoria2` | FK → `categoríaMercancia` | |
| `Motivo` | texto | **agregado** — ej. "riesgo de reacción/incendio sin separación certificada" |
| `CreadoPor` | texto/FK usuario | |
| `CreatedAt` / `UpdatedAt` | datetime | |

**Llenado:** reemplazadas por las 3 reglas de un seeder externo
(`FERTILIZANTE`×`ALIMENTOS`, `QUIMICO_OXIDANTE`×`MATERIAL_INFLAMABLE`,
`QUIMICO_CORROSIVO`×`ALIMENTOS`) — las del código viejo de cubicación
rápida (`_INCOMPATIBILIDADES`) ya no aplican tal cual porque esas
categorías se partieron en dos (ver Tabla 5). El resto de la matriz de
compatibilidad queda pendiente de completar con operaciones vía el futuro
CRUD.

**Cambios acordados:**
- Se agregó `Motivo` — explica el rechazo al usuario, no solo marca que son
  incompatibles.
- **Cada par se guarda una sola vez** (en un orden fijo, ej. siempre el Id
  menor primero como `IdCategoria1`), no `(A,B)` y `(B,A)` por separado. La
  aplicación revisa ambos sentidos al consultar.
- Confirmado: hacerlo por **categoría** (no por tipo de mercancía
  individual) coincide con cómo ya funciona hoy el criterio real en el
  código.

---

## Tabla 7 — `recomendacionVehiculo` ✅ revisada (un vehículo asignado = una fila)

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid | PK |
| `IdLineaSolicitud` | texto | código completo, viene de NEXO o Cotifly |
| `OrigenSistema` | enum: `NEXO` / `COTIFLY` | **agregado** |
| `IdTipoVehiculo` | FK → `vehiculos`, nullable | nullable por si queda "sin selección automática" |
| `IdCarroceria` | FK → `TiposCarroceria` | |
| `Heterogenea` | booleano | **agregado** — si este camión llevó más de un tipo de mercancía mezclado |
| `PorcentajeLlenado` | decimal (%) | del camión completo, una sola vez |
| `Resultado` | enum: `ASIGNADO` / `SIN_SELECCION_AUTOMATICA` | **agregado** |
| `MotivoSinSeleccion` | texto, nullable | **agregado** |
| `CreadoPor` | texto/FK usuario | |
| `CreatedAt` / `UpdatedAt` | datetime | |

Esta tabla no estaba en el modelo de datos de la propuesta madre (§4) — es
una adición propia, y muy útil: sirve exactamente para la validación
histórica que pide su §8.3 ("correr el motor sobre 50-100 despachos ya
ejecutados y comparar").

## Tabla 8 — `recomendacionVehiculoMercancia` ✅ nueva (una mercancía dentro de ese vehículo = una fila)

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid | PK |
| `IdRecomendacionVehiculo` | FK → Tabla 7 | agrupa todas las mercancías de un mismo camión |
| `IdMercancia` | FK → `tiposMercancia` | |
| `CantidadUnidadesMercancia` | entero (Und) | |
| `PesoMercanciaKg` | decimal | lo que aportó esta mercancía puntual, no el total del camión |
| `VolumenMercanciaM3` | decimal | lo que aportó esta mercancía puntual, no el total del camión |
| `CreadoPor` | texto/FK usuario | |
| `CreatedAt` / `UpdatedAt` | datetime | |

**Por qué se separó en dos tablas:** un mismo camión puede llevar varias
mercancías mezcladas (carga heterogénea) — si todo fuera una sola tabla, los
datos del camión (tipo de vehículo, % de llenado, resultado) se repetirían
en cada fila de mercancía, con riesgo de quedar desincronizados si alguien
corrige uno y no los demás. Con dos tablas, el dato del camión vive una sola
vez (Tabla 7), y cada mercancía que llevó es una fila aparte (Tabla 8) que
apunta a él por FK — un camión con una sola mercancía simplemente tiene 1
fila en la Tabla 8; uno heterogéneo tiene varias.

---

## Tabla 9 — `parametrosMotor` ✅ nueva

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid | PK |
| `Clave` | texto | ej. `k_max`, `factor_vol_util` |
| `Valor` | texto | se parsea al tipo que corresponda según la clave |
| `Descripcion` | texto | para qué sirve el parámetro |
| `CreadoPor` | texto/FK usuario | |
| `CreatedAt` / `UpdatedAt` | datetime | |

**Para qué sirve:** la propuesta madre (§4.5, §8.2) pide que los parámetros
del motor vivan en tabla, no en código, para que operaciones pueda ajustarlos
sin depender de un desarrollador ni un despliegue.

**Llenado inicial:**

| Clave | Valor | Descripción |
|---|---|---|
| `k_max` | `4` | Tope de vehículos por grupo antes de "sin selección automática" |
| `factor_vol_util` | `0.90` | Fracción de volumen aprovechable en la práctica |
| `usar_alternativa` | `solo_respaldo` | Si la carrocería alternativa entra desde el inicio o solo como respaldo |
| `umbral_alerta_llenado` | `0.75` | Por debajo, se marca alerta |
| `margen_cg` | `0.30` | Centro de gravedad dentro de ±30% del centro |

---

## Nota — ajustes que esta propuesta de BD le pega a `propuesta-seleccion-vehiculos.md`

Al cerrar el diseño de las 9 tablas, quedaron varios puntos donde esta BD
difiere de, o resuelve, lo que dice la propuesta madre (`propuesta-seleccion-vehiculos.md`,
misma carpeta). Vale la pena ajustar esos puntos allá para que los dos
documentos no queden contradictorios:

1. **§9.2 (punto abierto) queda resuelto — actualizado 2026-10-08.** La
   propuesta preguntaba si el alto en Plataforma es límite duro o aplica
   la altura total normativa. La respuesta correcta era la segunda opción:
   siempre se valida, pero **no** con un default fijo de 4.4 m — es
   `AlturaMaximaReglamentariaCm`, el límite legal de altura por
   reglamentación de tránsito, que **varía por vehículo** (230/240/260
   cm). Ver `ORIGEN-Y-DECISIONES.md`.

2. **§4.3 queda desactualizado.** Dice *"alto_m decimal, opcional (vacío en
   Plataforma → no se valida alto)"* — ya no aplica, el alto nunca es
   opcional (Tabla 1 de esta propuesta).

3. **§4.1 tiene un campo `requiere_alto` que ya no hace falta.** Decía
   "Furgón sí valida alto, Plataforma no" — se decidió lo contrario (siempre
   se valida, para todas las carrocerías), así que ese campo sobra en
   `TiposCarroceria` (Tabla 2).

4. **§4.2 `cubicabilidad` como un solo enum no alcanza a capturar la regla
   real.** La propuesta lo plantea como un solo valor
   (CUBICABLE/CONDICIONADO/EXCLUIDO), pero en esta BD `Cubicable` y
   `CondicionadaCubicacion` son dos campos **independientes** (Tabla 3):
   una mercancía puede ser cubicable automáticamente Y a la vez requerir
   validación obligatoria de AdminTarifas antes de aprobarse. Esto también
   le pega al flujo de §5.2 Parte 1, donde dice que "CONDICIONADO sale del
   motor hacia flujo especializado" — con este diseño, una mercancía
   condicionada sigue el flujo automático normal y además queda marcada
   para revisión obligatoria, no sale del motor.

5. **§4.4 (matriz de compatibilidad)** la plantea por tipo de mercancía
   individual (31×31 posibles). En esta BD (Tabla 6) se hizo por
   **categoría**, no por tipo — coincide con cómo ya funciona el criterio
   real en el código hoy (`_INCOMPATIBILIDADES`) y evita enumerar muchos
   más pares de los necesarios.
