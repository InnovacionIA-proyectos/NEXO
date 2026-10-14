# Diccionario de datos — BD del motor de selección de vehículo (Cubicador)

Para qué sirve cada tabla, en lenguaje llano. Para el detalle columna por
columna, ver `cubicador/docs/PROPUESTA-BD-MOTOR-SELECCION-VEHICULOS.md`;
para la lógica del motor que las usa, `cubicador/docs/propuesta-seleccion-vehiculos.md`.

Estas 9 tablas viven en su propio esquema SQL, **`dbo_cubicador`**, dentro
de la base compartida `dbTarifario_Nexo_Pruebas` — no es `db_NEXO` ni la
BD de Cotifly, ni una base dedicada aparte (ver `README.md` para el
detalle de dónde y cómo). Ninguno de los otros dos sistemas la consulta
directamente; le hablan por API (`POST /recomendaciones` y los catálogos
que exponga).

---

## `vehiculos`

**Qué es:** el catálogo de configuraciones de vehículo que el motor conoce
— Turbo (2L2), Sencillo (C2), Dobletroque (C3), Minimula (2S2), Tractomula
(3S3). Cada fila es un tipo de camión real, con sus medidas en
**centímetros** (`LargoCm`, `AnchoCm`, `AlturaMaximaReglamentariaCm`) y
cuánto peso puede cargar.

**Para qué sirve:** es el universo de candidatos entre los que el motor
elige al recomendar un vehículo. `CodigoVehiculo` es la clave que se usa
para hacer matching contra SICE-TAC y el tarifario — por eso tiene que ser
única, aunque un mismo vehículo cargue distinto según la carrocería (ver
`relacionVehiculosCarroceria`). `AlturaMaximaReglamentariaCm` **no es una
medida física cualquiera del fabricante** — es el límite legal de altura
para ese tipo de vehículo según la reglamentación de tránsito (así la
nombra el seeder externo: "altura máxima legal"). No es un default único
para todos, varía por vehículo (230/240/260 cm) — los 5 vehículos de hoy
traen su propio valor explícito. Sí tiene un `DEFAULT (440)` en la
columna, pero es solo un respaldo para cuando se cree un vehículo nuevo
sin este dato puntual todavía, no el valor real de ninguno de los 5.

## `TiposCarroceria`

**Qué es:** el catálogo cerrado de carrocerías, con `CodigoCarroceria`
(el código fijo que usa el resto de las tablas) y `NombreCarroceria`
(nombre legible) separados. 17 en total: Furgón, Furgón
refrigerado/climatizado/ventilado, Estacas, Plataforma, Planchón,
Estructura portavidrios, Contenedor seco/refrigerado, Cama baja,
Portacontenedores, Tanque, Tolva, Volco, Grúa, Niñera.

**Para qué sirve:** normaliza los nombres de carrocería que en el Excel
original venían sueltos y con sinónimos ("Plancha", etc.) a un código
fijo, para que el resto de las tablas puedan referenciarlo sin
ambigüedad. **Ojo:** `Plataforma` y `Planchón` se mantienen como dos
códigos distintos a propósito — no hay evidencia real (cruzando SICE-TAC
contra el catálogo de SAP) de que sean la misma carrocería física con dos
nombres, así que no se fusionaron. Las últimas 7 de la lista (Cama baja,
Portacontenedores, Tanque, Tolva, Volco, Grúa, Niñera) existen en el
catálogo pero **no tienen ninguna fila en `relacionVehiculosCarroceria`
todavía** — con qué vehículo(s) emparejan cada una lo decide el
Administrador de Fletes desde el futuro CRUD.

## `categoriaMercancia`

**Qué es:** un catálogo plano de 13 categorías de mercancía — GENERAL,
ALIMENTOS, FARMACEUTICO, PERECEDERO_NO_ALIMENTO, FERTILIZANTE,
MATERIAL_INFLAMABLE, CARBURANTE, METALICO, CONSTRUCCION, VIDRIO,
GAS_INDUSTRIAL, QUIMICO_OXIDANTE, QUIMICO_CORROSIVO.

**Para qué sirve:** es el nivel al que se define la compatibilidad entre
mercancías (`reglasIncompatibilidad`) — por categoría, no por mercancía
individual, porque así ya funciona el criterio real en el código actual de
cubicación rápida, y evita tener que enumerar una regla por cada par de las
31 mercancías.

## `tiposMercancia`

**Qué es:** las mercancías reales que se pueden transportar — cajas de
cartón, alimentos congelados, varillas metálicas, vidrio plano, etc. (31
tipos, sacados de la Matriz cubicador). Cada una trae, además de lo de
siempre, `CodigoMercancia` (código corto), `ControlAmbientalRequerido`
(si necesita refrigeración/climatización/ventilación, y si es obligatorio
o condicional), y tres flags opcionales (`Apilable`, `Fragil`,
`RotacionLibre` — `NULL` cuando no aplica/no se sabe).

**Para qué sirve:** cada mercancía trae su categoría (para la
compatibilidad), su carrocería principal y una alternativa condicionada
(para saber en qué vehículos puede ir), y dos flags independientes:
`Cubicable` (¿el motor la procesa automáticamente?) y
`CondicionadaCubicacion` (¿necesita que AdminTarifas la apruebe a mano,
aunque sí sea cubicable?). Es la tabla que el motor consulta primero para
decidir qué hacer con cada línea de carga.

## `relacionVehiculosCarroceria`

**Qué es:** la tabla puente entre `vehiculos` y `TiposCarroceria` — qué
carrocerías puede llevar cada vehículo.

**Para qué sirve:** sin ella, no habría forma de saber que un Turbo acepta
Estacas/Furgón/Plataforma pero un Dobletroque no acepta Furgón, por
ejemplo. También resuelve un caso puntual real: la Tractomula 3S3 carga
34.000 kg normalmente, pero solo 30.000 kg cuando va con Furgón —o
cualquiera de sus 3 variantes, que heredan la misma restricción— (porque
la caja cerrada pesa más vacía) — en vez de inventar un segundo vehículo
con el mismo código `3S3` (lo que rompería el matching contra SICE-TAC),
esa diferencia de capacidad vive aquí, en la combinación específica
vehículo+carrocería.

## `reglasIncompatibilidad`

**Qué es:** pares de categorías de mercancía que nunca deben viajar juntas
en el mismo vehículo. Hoy tiene 3 reglas:
`FERTILIZANTE`×`ALIMENTOS`, `QUIMICO_OXIDANTE`×`MATERIAL_INFLAMABLE`,
`QUIMICO_CORROSIVO`×`ALIMENTOS`.

**Para qué sirve:** es el primer filtro del motor, antes de pensar siquiera
en vehículos — si dos mercancías de una misma solicitud caen en categorías
incompatibles, se rechazan o se separan en grupos distintos, sin importar
que compartan carrocería. Son las únicas 3 confirmadas con respaldo real
hasta ahora — la matriz completa de compatibilidad queda pendiente de
construir con operaciones, vía el futuro CRUD.

## `parametrosMotor`

**Qué es:** los números que ajustan el comportamiento del motor —
`k_max` (cuántos vehículos máximo antes de rendirse), `factor_vol_util`
(cuánto del volumen útil se considera realmente aprovechable),
`umbral_alerta_llenado`, etc.

**Para qué sirve:** para que operaciones pueda ajustar estos valores desde
una pantalla simple, sin que un desarrollador tenga que tocar código ni
hacer un despliegue cada vez que se quiera subir o bajar un umbral.

## `recomendacionVehiculo`

**Qué es:** el resultado de correr el motor para una línea de solicitud —
un vehículo asignado (o la constancia de que no se pudo asignar ninguno
automáticamente).

**Para qué sirve:** es el historial de qué recomendó el motor, para cada
línea de NEXO o de Cotifly (`OrigenSistema` distingue de cuál). Sirve para
dos cosas: que el sistema que pidió la recomendación la pueda volver a
consultar sin recalcularla, y para la validación histórica (correr el
motor contra despachos ya ejecutados y comparar qué tan bien recomendó).
Una fila = un vehículo asignado; si una carga heterogénea necesitó 3
vehículos, hay 3 filas para esa misma línea.

## `recomendacionVehiculoMercancia`

**Qué es:** el detalle de qué mercancía, cuánta cantidad/peso/volumen, fue
en cada vehículo de una recomendación.

**Para qué sirve:** separa este detalle de `recomendacionVehiculo` para no
repetir los datos del camión (tipo, % de llenado, resultado) una vez por
cada mercancía que llevó — si un camión llevó 2 mercancías distintas
mezcladas, hay 2 filas aquí, ambas apuntando al mismo
`IdRecomendacionVehiculo`.

---

## Cómo se relacionan entre sí, de un vistazo

```
categoriaMercancia ──┬── tiposMercancia ──┬── reglasIncompatibilidad (categoria1, categoria2)
                     │                    │
TiposCarroceria ─────┴── (principal/      │
                          secundaria)     │
                                          │
vehiculos ── relacionVehiculosCarroceria ─┘
   │
   └── recomendacionVehiculo ── recomendacionVehiculoMercancia ── tiposMercancia

parametrosMotor: config suelta, no se relaciona con nada más.
```
