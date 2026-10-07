# Propuesta de BD — Motor de selección de tipo y cantidad de vehículos

Diseño de tablas para el nuevo motor descrito en `propuesta-seleccion-vehiculos.md`
(§4 "Modelo de datos"). Vive en la base de datos propia del Cubicador, no en
`db_NEXO`.

Documento vivo — se va ajustando tabla por tabla a medida que se revisa cada
una.

---

## Tabla 1 — `vehiculos` ✅ revisada

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid | PK |
| `CodigoVehiculo` | texto | clave corta, ej. `3S3` |
| `NombreVehiculo` | texto | ej. "Tractomula 3S3" |
| `LargoPlancha` | decimal (m) | |
| `AnchoPlancha` | decimal (m) | |
| `AltoCarroceria` | decimal (m) | **default 4.4 m** si no se especifica |
| `CapacidadMaxima` | decimal (kg) | |
| `Activo` | booleano | |
| `CreadoPor` | texto/FK usuario | |
| `CreatedAt` / `UpdatedAt` | datetime | |

**Llenado:** seeder con Excel "Dimensiones y peso por vehículo".

**Cambios acordados sobre el borrador original:**
- Se separó `Codigo` de `Nombre` (antes mezclados en `tipoVehiculo`).
- `AltoCarroceria` queda como campo normal (no nullable), con **valor
  default de 4.4 m** cuando el vehículo no trae uno propio especificado.

---

## Tabla 2 — `TiposCarroceria` ✅ revisada

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid | PK |
| `TipoCarroceria` | texto | furgón, estacas, etc. |
| `Activo` | booleano | |
| `CreadoPor` | texto/FK usuario | |
| `CreatedAt` / `UpdatedAt` | datetime | |

**Llenado:** seeder con Excel "Matriz cubicador".

**Sin cambios sobre el borrador original.** El chequeo de altura ya queda
cubierto a nivel de vehículo (Tabla 1, `AltoCarroceria` con default 4.4 m,
siempre presente) — no hace falta que la carrocería decida si ese chequeo
aplica o no, porque siempre aplica.

---

## Tabla 3 — `tiposMercancia` ✅ revisada

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid | PK |
| `NombreMercancia` | texto | cajas, alimentos empacados, etc. |
| `IdCategoria` | FK → `categoríaMercancia` | |
| `Cubicable` | booleano | ¿es cubicable o queda excluido del todo? |
| `CondicionadaCubicacion` | booleano | ¿requiere que AdminTarifas la valide obligatoriamente antes de aprobarla? — independiente de `Cubicable`, puede ser `true` aunque sí sea cubicable |
| `ComentarioCondicion` | texto | |
| `CarroceriaPrincipal` | FK → `TiposCarroceria` | |
| `CarroceriaSecundaria` | FK → `TiposCarroceria` | |
| `CarroceríaSecundariaCondicionada` | booleano | |
| `Activo` | booleano | |
| `CreadoPor` | texto/FK usuario | |
| `CreatedAt` / `UpdatedAt` | datetime | |

**Llenado:** seeder con Excel "Matriz cubicador".

**Sin cambios sobre el borrador original** — `Cubicable` y
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

## Tabla 4 — `relacionVehiculosCarroceria` ✅ revisada

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid | PK |
| `IdTipoVehiculo` | FK → `vehiculos` | |
| `IdCarroceria` | FK → `TiposCarroceria` | |
| `CreadoPor` | texto/FK usuario | |
| `CreatedAt` / `UpdatedAt` | datetime | |

**Llenado:** seeder con Excel "Dimensiones y peso por vehículo".

**Sin cambios sobre el borrador original** — resuelve bien la relación N:N
entre vehículo y carrocería (un vehículo puede aplicar a varias carrocerías:
Estacas/Furgón/Plancha, como en el Excel original).

---

## Tabla 5 — `categoríaMercancia` ✅ revisada

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | uuid | PK |
| `NombreCategoria` | texto | alimentos, químico, refrigerada, etc. |
| `CreadoPor` | texto/FK usuario | |
| `CreatedAt` / `UpdatedAt` | datetime | |

**Sin cambios sobre el borrador original** — catálogo plano, base de la
Tabla 3 (`IdCategoria`) y de la Tabla 6 (reglas de incompatibilidad).

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

**Llenado:** reglas existentes en el código actual
(`cubicacion_rapida.py::_INCOMPATIBILIDADES`: químico oxidante + combustible,
químico corrosivo + alimentos) más las que operaciones defina.

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

1. **§9.2 (punto abierto) queda resuelto.** La propuesta preguntaba si el
   alto en Plataforma es límite duro o no se valida. Se resolvió: **siempre
   se valida**, con default de 4.4 m, independiente de la carrocería.

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
