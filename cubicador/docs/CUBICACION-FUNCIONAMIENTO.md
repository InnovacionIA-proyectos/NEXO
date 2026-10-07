# Cómo funciona la cubicación

Dos piezas separadas, que comparten catálogo pero resuelven preguntas distintas:

1. **Cubicación rápida** (`cubicador/domain/motor/cubicacion_rapida.py`) — sin
   persistencia, sin posiciones reales: dado un conjunto de bultos (dimensiones +
   peso), **recomienda qué tipo de vehículo/carrocería/clase de viaje usar**.
   Es lo que corre durante la captura de una solicitud, antes de que exista
   ningún Plan de Cargue.
2. **Plan de Cargue 3D** (`cubicador/domain/motor/`) — con persistencia real en
   Nexo: dado un vehículo YA elegido, calcula la disposición física exacta
   (X/Y/Z de cada unidad) y un puntaje de qué tan buena quedó esa distribución.

La cubicación rápida sugiere el vehículo; el Plan de Cargue 3D es el detalle de
cómo se acomoda la carga dentro de ese vehículo ya elegido. Son necesarios ambos
porque resuelven preguntas distintas: "¿qué camión pido?" vs "¿cómo cabe todo
adentro y qué tan bien quedó armado?".

---

## 1. Tablas de base de datos (todas en `db_NEXO`, esquema de Nexo)

Estas tablas son el **resultado persistido del Plan de Cargue 3D** (la
cubicación rápida no se persiste — es una respuesta HTTP efímera de Cubicador).

```
tbl_vehiculos_cubicacion        (catálogo de contenedores)
        │  FK VehiculoId
        ▼
tbl_planes_cargue               (una corrida del motor contra un vehículo)
        │  FK PlanCargueId
        ├──► tbl_items_plan_cargue      (insumo: qué hay que cargar)
        │
        └──► tbl_colocaciones_cargue    (resultado: dónde quedó cada unidad)
                 FK ItemPlanCargueId → el ítem que la originó
```

### `tbl_vehiculos_cubicacion`

Catálogo de contenedores/vehículos disponibles para el Plan de Cargue 3D (no
confundir con `tbl_configuraciones_vehiculo`, que es el catálogo *comercial*
de tipo/carrocería usado en cotización — este es geometría real para el
cubicador).

| Columna | Para qué sirve | De dónde sale |
|---|---|---|
| `Id` | PK | generado |
| `Nombre` | nombre visible del vehículo | capturado o copiado de un preset |
| `Descripcion` | detalle libre | capturado |
| `LargoInternoMm`/`AnchoInternoMm`/`AltoInternoMm` | dimensiones internas útiles | capturado (o preset) |
| `CapacidadKg` | peso bruto permitido | capturado |
| `TaraKg` | peso vacío del vehículo (capacidad de carga real = `CapacidadKg - TaraKg`) | capturado |
| `PuertaAnchoMm`/`PuertaAltoMm` | tamaño de la puerta — filtra qué orientaciones de carga pueden *entrar* aunque quepan adentro | capturado |
| `PuertaPosicion` | por dónde se carga (trasera/lateral) | capturado |
| `EjesJson` | posición + capacidad de cada eje (array) — insumo del balance de peso por eje | capturado |
| `ToleranciaParedesMm`/`ToleranciaEntreUnidadesMm` | holgura mínima exigida contra pared y entre bultos | capturado (parámetro de negocio) |
| `EsPreset` | `true` = viene del catálogo fijo (ver abajo), `false` = vehículo armado a mano | capturado |
| `CreatedAt`/`UpdatedAt` | auditoría estándar | automático |

### `tbl_planes_cargue`

Una fila = una corrida del motor contra un vehículo específico.

| Columna | Para qué sirve | De dónde sale |
|---|---|---|
| `Id` | PK | generado |
| `Nombre` | nombre visible del plan | capturado |
| `VehiculoId` | FK → `tbl_vehiculos_cubicacion` | elegido por el usuario (o sugerido por cubicación rápida) |
| `Estado` | estado del plan (ej. borrador/confirmado) | gestión de la app |
| `Estrategia` | una de las 8 estrategias de optimización (ver sección 2) | elegida por el usuario |
| `PonderacionesJson` | pesos custom por componente, solo si la estrategia es "Combinada" | capturado |
| `Algoritmo` | código corto del algoritmo usado (`FFD`/`BFD`, ver sección 2) | elegido/ejecutado |
| `Semilla` | semilla aleatoria, para poder reproducir la misma corrida | generado o capturado |
| `PesoTotalKg` | peso total cargado en el resultado | **calculado** por el motor |
| `VolumenUtilizadoM3` | volumen ocupado | **calculado** por el motor |
| `UtilizacionVolumen`/`UtilizacionPiso` | % de volumen/área de piso usado | **calculado** por el motor |
| `Puntaje` | 0-100, qué tan bueno quedó el plan según la estrategia elegida (fórmula en sección 2) | **calculado** por el motor |
| `IncidenciasJson` | lista de advertencias/violaciones críticas (colisión, soporte insuficiente, eje excedido, etc.) | **calculado** por el validador |
| `MetricasJson` | métricas adicionales (centro de gravedad, pedidos completos/parciales, etc.) | **calculado** por el motor |
| `LineaSolicitudId` | a qué línea de la solicitud pertenece este plan (nullable) | capturado |
| `NumeroVehiculo` | cuando una línea necesita varios camiones, cuál de la serie es este (1, 2, 3…) | calculado al repartir en cascada |

### `tbl_items_plan_cargue`

El insumo: qué SKUs hay que cargar y sus restricciones de empaque.

| Columna | Para qué sirve | De dónde sale |
|---|---|---|
| `Sku`/`Descripcion`/`Pedido`/`Cliente`/`Destino` | identificación del ítem y a qué pedido/entrega pertenece | capturado (manual o import) |
| `SecuenciaEntrega` | orden de entrega — usado por las estrategias "minimizar movimientos"/"priorizar secuencia" | capturado |
| `LargoMm`/`AnchoMm`/`AltoMm`/`PesoKg`/`Cantidad` | geometría y peso físico real | capturado |
| `Color` | solo visual, para distinguir ítems en la vista 3D | capturado |
| `Prioridad` | 1-2 = crítica; usada por la estrategia "priorizar pedidos críticos" | capturado |
| `TipoEmbalaje` | informativo | capturado |
| `Apilable`/`MaxNiveles`/`MaxPesoEncimaKg` | reglas de apilamiento — el validador las usa para aceptar/rechazar una colocación encima de otra | capturado |
| `Rotable`/`MantenerVertical` | qué orientaciones están permitidas al optimizar | capturado |
| `Fragil`/`Peligroso`/`Refrigerado` | restricciones de seguridad/ambientales | capturado |
| `GrupoCompatibilidad` | qué ítems no se pueden mezclar/tocar entre sí | capturado |
| `Observaciones` | libre | capturado |

### `tbl_colocaciones_cargue`

El resultado: una fila por **unidad física** (no por SKU — si `Cantidad=5`, salen
5 filas), con dónde quedó y si se pudo cargar.

| Columna | Para qué sirve | De dónde sale |
|---|---|---|
| `ItemPlanCargueId` | FK al SKU que la originó | — |
| `IndiceUnidad` | cuál unidad de ese SKU es (1 de 5, 2 de 5…) | calculado al expandir cantidades |
| `Cargada` | si el motor logró ubicarla (`false` = quedó en rechazos) | **calculado** |
| `X`/`Y`/`Z` | posición dentro del vehículo | **calculado** por el algoritmo de puntos extremos |
| `LargoColocadoMm`/`AnchoColocadoMm`/`AltoColocadoMm` | dimensiones YA rotadas según la orientación elegida | **calculado** |
| `OrientacionIndice` | cuál de las rotaciones permitidas se usó | **calculado** |
| `SecuenciaCargue` | orden en que se fue colocando (= orden de carga física real) | **calculado** |
| `SoporteColocacionId`/`PorcentajeSoporte` | qué otra colocación la sostiene y qué % de su base está apoyada | **calculado** por el validador de soporte |
| `PesoEncimaKg` | cuánto peso tiene encima (para validar `MaxPesoEncimaKg` del ítem de abajo) | **calculado** |
| `EstadoEstabilidad` | `estable`/`advertencia`/`inestable` según soporte y centro de masa | **calculado** |
| `Accesibilidad` | si queda bloqueada por otra unidad que se descarga después (rompería la secuencia de entrega) | **calculado** |
| `Bloqueada`/`AjusteManual` | si el usuario la fijó/movió a mano después de que corrió el motor | capturado |
| `CodigoRechazo`/`RazonRechazo` | por qué no se pudo cargar (`SIN_ESPACIO`, `PUERTA`, `PESO_TOTAL`, `COMPATIBILIDAD`) | **calculado** cuando `Cargada=false` |

---

## 2. Fórmulas del Plan de Cargue 3D (una vez el vehículo YA está elegido)

### Algoritmo de colocación: puntos extremos (Extreme Points)

Mantiene una lista de esquinas candidatas (empieza con el origen del vehículo;
cada colocación genera 3 esquinas nuevas). Por cada ítem, prueba cada
combinación (esquina × orientación permitida) y descarta las que violen
límites, colisión, soporte mínimo, apilamiento, peso o compatibilidad. Dos
variantes intercambiables:

- **FFD (First Fit Decreasing):** toma la **primera** esquina válida.
- **BFD (Best Fit Decreasing):** puntúa **todas** las esquinas válidas y toma
  la mejor.

El **orden** en que se intentan colocar los ítems también cambia según la
estrategia:

| Estrategia | Orden de colocación |
|---|---|
| Maximizar Peso | peso descendente, luego volumen |
| Priorizar Pedidos Críticos | prioridad ascendente (1 primero), luego volumen descendente |
| Priorizar Secuencia Entrega / Minimizar Movimientos | secuencia de entrega **descendente** (se carga primero lo que se entrega **último**, para que quede al fondo) |
| Cualquier otra | volumen descendente, luego área de base, luego peso |

### Fórmula del `Puntaje` (0-100)

7 componentes, cada uno normalizado a 0–1:

```
volumen        = Σ volumen colocado / volumen útil del vehículo
unidades       = unidades colocadas / unidades totales
peso           = peso cargado / capacidad del vehículo
estabilidad    = % de unidades con EstadoEstabilidad = "estable"
balanceEjes    = 1 − (peor_uso_de_eje × 0.2)     si peor_uso ≤ 100%
                 1 − peor_uso                     si peor_uso > 100% (castiga fuerte)
accesibilidad  = % de unidades no bloqueadas para descarga
prioridad      = % de ítems prioridad 1-2 que sí quedaron cargados
```

Cada una de las **8 estrategias** (`MaximizarVolumen`, `MaximizarUnidades`,
`MaximizarPeso`, `PriorizarPedidosCriticos`, `PriorizarSecuenciaEntrega`,
`MinimizarMovimientos`, `MejorarEstabilidad`, `BalancearEjes`) es un juego fijo
de pesos distinto sobre estos 7 componentes (ej. `MaximizarVolumen` pondera
volumen al 70%; `BalancearEjes` pondera balance de ejes al 60%). El puntaje
final es el promedio ponderado, llevado a escala 0-100.

---

## 3. Fórmulas de la recomendación de vehículo/carrocería/clase de viaje (cubicación rápida)

Esta es la pregunta "¿qué le recomiendo pedir?", **antes** de que exista un
Plan de Cargue — se resuelve contra un catálogo fijo de 9 arquetipos
(`cubicador/domain/motor/cubicacion_rapida.py`, constante `CATALOGO`):

| id | Arquetipo | Geometría | Control ambiental | Dimensiones útiles (m) | Peso bruto (kg) | Tara (kg) | Capacidad de carga (kg)* | Ejes (kg) | Config SICE-TAC | Carrocería | Clase de viaje | Clase contenedor |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `turbo_estacas` | Turbo con estacas | semicerrada | ninguno | 6.20×2.20×2.00 | 10.500 | 4.200 | 6.300 | [3.500, 7.000] | `2L3` | ESTACAS | Carga Suelta | — |
| `sencillo_estacas` | Sencillo con estacas | semicerrada | ninguno | 7.20×2.35×2.20 | 17.500 | 7.500 | 10.000 | [6.000, 11.000] | `2` | ESTACAS | Carga Suelta | — |
| `dobletroque_estacas` | Dobletroque con estacas | semicerrada | ninguno | 7.80×2.40×2.30 | 28.000 | 10.500 | 17.500 | [6.000, 22.000] | `3` | ESTACAS | Carga Suelta | — |
| `furgon_semirremolque` | Semirremolque furgón | cerrada | ninguno | 12.20×2.45×2.70 | 34.000 | 7.000 | 27.000 | [6.000, 10.500, 10.500] | `3S3` | FURGON | Carga Suelta | — |
| `plataforma_sencilla` | Plataforma (tractomula) | abierta | ninguno | 12.20×2.50×2.80 | 34.000 | 6.500 | 27.500 | [6.000, 10.500, 10.500] | `3S3` | PLATAFORMA | Carga Suelta | — |
| `cama_baja` | Cama baja (lowboy) | abierta | ninguno | 12.50×2.60×1.60 | 48.000 | 9.000 | 39.000 | [6.000, 10.500, 10.500, 10.500] | `3S3` | CAMABAJA | Extradimensionado | — |
| `contenedor_seco_20` | Contenedor seco 20 pies | cerrada | ninguno | 5.90×2.35×2.39 | 24.000 | 2.200 | 21.800 | [10.900, 10.900] | `2S2` | PORTACONTENEDORES | Contenedor | CNT20.ST |
| `contenedor_seco_40` | Contenedor seco 40 pies | cerrada | ninguno | 12.03×2.35×2.39 | 30.480 | 3.800 | 26.680 | [13.340, 13.340] | `3S3` | PORTACONTENEDORES | Contenedor | CNT40.HC |
| `contenedor_refrigerado_40` | Contenedor refrigerado 40 pies (reefer) | cerrada | **refrigeracion** | 11.56×2.28×2.25 | 29.500 | 4.800 | 24.700 | [12.350, 12.350] | `3S3` | PORTACONTENEDORES | Contenedor | CNT40.RE |

*Capacidad de carga = Peso bruto − Tara (propiedad calculada en el código,
`capacidad_carga_kg`, no un campo guardado aparte).

Es decir: la recomendación de **TipoVehiculo + Carrocería + ClaseViaje** sale
directamente de **cuál arquetipo del catálogo resultó elegido** — no hay una
fórmula separada para cada uno, los 3 datos viajan pegados al arquetipo.

### Paso 1 — Evaluar cada arquetipo contra la carga: 7 criterios, por VETO

**Nunca se promedia** — un solo criterio de seguridad rechazado descarta el
arquetipo entero, sin importar qué tan bien le vaya en los demás:

1. **Compatibilidad entre mercancías** (bloqueante de seguridad): clasifica
   cada ítem por palabras clave (`oxidante`/`hipoclorito`/`peroxido` →
   químico oxidante; `corrosivo`/`acido` → químico corrosivo;
   `textil`/`papel`/`carton`/`madera` → combustible; `alimento`/`comestible`
   → alimentos) y rechaza si hay un par declarado incompatible en la misma
   carga (químico oxidante + combustible, o químico corrosivo + alimentos).
2. **Compatibilidad volumétrica** (bloqueante): `Σ(largo×ancho×alto de cada
   ítem × cantidad) > volumen útil del arquetipo` → rechazado. Si pasa pero
   usa **>95%** del volumen, queda con advertencia.
3. **Compatibilidad dimensional** (bloqueante): cada ítem, en su orientación
   más favorable (las 3 dimensiones ordenadas), debe caber dentro de las 3
   dimensiones (también ordenadas) del arquetipo — si no, rechazado.
4. **Compatibilidad por peso** (bloqueante de seguridad): `Σ(peso × cantidad)
   > capacidad_carga_kg` (= `peso_bruto_permitido − peso_vacío`) → rechazado.
   Si falta el peso de algún ítem, "no evaluable" (también veta).
5. **Distribución de peso por eje** (bloqueante de seguridad): asume reparto
   **uniforme** entre ejes (el mejor caso posible, porque la cubicación
   rápida no tiene posiciones reales todavía) — `peso_total / N_ejes` contra
   la capacidad del eje **más débil**. Si ese reparto ya supera el 100% del
   eje más débil → rechazado. Entre **90% y 100%** → advertencia.
6. **Compatibilidad ambiental** (bloqueante): si algún ítem menciona
   "refrigerad…" en su descripción/tipo y el arquetipo no ofrece control
   ambiental → rechazado.
7. **Compatibilidad de seguridad** (criterio transversal, el más alto): si
   **cualquiera** de los 3 criterios marcados "bloqueante_seguridad" (1, 4 o
   5) quedó rechazado o no evaluable, este criterio también rechaza — es el
   veto final.

Con los 7 resultados, la banda del arquetipo es:
`incompatible` (algo rechazado) > `no_evaluable` (algo sin dato) >
`compatible_con_advertencias` (pasó todo, pero algo tocó un umbral) >
`compatible` (todo limpio).

### Paso 2 — Elegir el arquetipo recomendado

Entre los arquetipos que SÍ lograron ubicar toda la carga (vía la misma
heurística de estanterías del Plan 3D, simplificada), se elige por este
orden de criterios (el primero que desempata gana):

1. Sin advertencias (`compatible` antes que `compatible_con_advertencias`).
2. Su `ClaseViaje` coincide con la que ya traía la línea de la solicitud (si
   se indicó una).
3. **Mayor factor de utilización de volumen** — el arquetipo más ajustado a
   la carga, no el más grande disponible.

### Si no cabe en ningún arquetipo individual: sugerencia de flota

Si el problema es solo de capacidad (no de seguridad), reparte la carga en
varias unidades del **mismo tipo** (empaque voraz, hasta 20 vehículos), y
elige el tipo que necesite **menos vehículos** — empate se rompe primero por
`ClaseViaje` coincidente, luego por mayor utilización promedio.

### Detección de extradimensión

Independiente de lo anterior: cada pieza se compara contra una medida
estándar paramétrica (por defecto 1220×260×300 cm, 34.000 kg), en su
orientación más favorable (planta ordenada por largo/ancho). Si algo excede
largo, ancho, alto o peso, se marca para tratar como viaje
**EXTRADIMENSIONADO** (cama baja + permisos), con evidencia fotográfica.
