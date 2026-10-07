# Diccionario de datos — BD del motor de selección de vehículo (Cubicador)

Para qué sirve cada tabla, en lenguaje llano. Para el detalle columna por
columna, ver `cubicador/docs/PROPUESTA-BD-MOTOR-SELECCION-VEHICULOS.md`;
para la lógica del motor que las usa, `cubicador/docs/propuesta-seleccion-vehiculos.md`.

Esta es la base de datos **propia del Cubicador** — no es `db_NEXO` ni la
BD de Cotifly. Ninguno de los otros dos sistemas la consulta directamente;
le hablan por API (`POST /recomendaciones` y los catálogos que exponga).

---

## `vehiculos`

**Qué es:** el catálogo de configuraciones de vehículo que el motor conoce
— Turbo, Sencillo, Dobletroque, Minimula, Tractomula, etc. Cada fila es un
tipo de camión real, con sus medidas internas útiles y cuánto peso puede
cargar.

**Para qué sirve:** es el universo de candidatos entre los que el motor
elige al recomendar un vehículo. `CodigoVehiculo` es la clave que se usa
para hacer matching contra SICE-TAC y el tarifario — por eso tiene que ser
única, aunque un mismo vehículo cargue distinto según la carrocería (ver
`relacionVehiculosCarroceria`).

## `TiposCarroceria`

**Qué es:** el catálogo cerrado de carrocerías — Furgón, Estacas,
Plataforma, Planchón, Furgón refrigerado/climatizado/ventilado, Contenedor
seco/refrigerado, Portavidrios.

**Para qué sirve:** normaliza los nombres de carrocería que en el Excel
original venían sueltos y con sinónimos ("Plancha", "Plataforma/planchón",
etc.) a un código fijo, para que el resto de las tablas puedan referenciarlo
sin ambigüedad.

## `categoriaMercancia`

**Qué es:** un catálogo plano de categorías de mercancía — GENERAL,
ALIMENTOS, FARMACEUTICO, COMBUSTIBLE, METALICO_CONSTRUCCION, etc.

**Para qué sirve:** es el nivel al que se define la compatibilidad entre
mercancías (`reglasIncompatibilidad`) — por categoría, no por mercancía
individual, porque así ya funciona el criterio real en el código actual de
cubicación rápida, y evita tener que enumerar una regla por cada par de las
31 mercancías.

## `tiposMercancia`

**Qué es:** las mercancías reales que se pueden transportar — cajas de
cartón, alimentos congelados, varillas metálicas, vidrio plano, etc. (31
tipos, sacados de la Matriz cubicador).

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
34.000 kg normalmente, pero solo 30.000 kg cuando va con Furgón (porque la
caja cerrada pesa más vacía) — en vez de inventar un segundo vehículo con
el mismo código `3S3` (lo que rompería el matching contra SICE-TAC), esa
diferencia de capacidad vive aquí, en la combinación específica
vehículo+carrocería.

## `reglasIncompatibilidad`

**Qué es:** pares de categorías de mercancía que nunca deben viajar juntas
en el mismo vehículo (ej. un químico oxidante con algo combustible).

**Para qué sirve:** es el primer filtro del motor, antes de pensar siquiera
en vehículos — si dos mercancías de una misma solicitud caen en categorías
incompatibles, se rechazan o se separan en grupos distintos, sin importar
que compartan carrocería. Hoy está vacía a propósito: no se inventó ninguna
regla sin respaldo real, queda pendiente de construir con operaciones.

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
