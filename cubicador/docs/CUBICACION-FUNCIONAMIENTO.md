# Cómo funciona la cubicación

Dos piezas separadas, que comparten catálogo pero resuelven preguntas distintas:

1. **Selección de flota** (`cubicador/domain/motor/seleccion_flota.py`,
   `POST /recomendaciones`) — dado un conjunto de mercancías (dimensiones, peso y
   tipo), **recomienda cuántos vehículos y de qué tipo y carrocería pedir**. Es lo
   que corre durante la captura de una solicitud, antes de que exista ningún Plan
   de Cargue. Su resultado se guarda en las tablas de recomendación del Cubicador.
2. **Plan de Cargue 3D** (`cubicador/domain/motor/`) — con persistencia real en
   Nexo: dado un vehículo YA elegido, calcula la disposición física exacta
   (X/Y/Z de cada unidad) y un puntaje de qué tan buena quedó esa distribución.

La selección de flota sugiere los vehículos; el Plan de Cargue 3D es el detalle de
cómo se acomoda la carga dentro de ese vehículo ya elegido. Son necesarios ambos
porque resuelven preguntas distintas: "¿qué camión pido?" vs "¿cómo cabe todo
adentro y qué tan bien quedó armado?".

---

## 1. Tablas de base de datos (todas en `db_NEXO`, esquema de Nexo)

Estas tablas son el **resultado persistido del Plan de Cargue 3D**. La selección
de flota guarda su resultado aparte, en las tablas de recomendación de la base del
Cubicador (ver `cubicador/docs/database/DICCIONARIO-DATOS.md`).

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
| `VehiculoId` | FK → `tbl_vehiculos_cubicacion` | elegido por el usuario (o sugerido por la selección de flota) |
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

## 3. Selección de flota: qué vehículos pedir

El motor (`cubicador/domain/motor/seleccion_flota.py`) lee el catálogo de la base
(vehículos, carrocerías, mercancías, relaciones y reglas de incompatibilidad) y
resuelve en tres pasos:

1. **Filtrar candidatos.** Combinaciones vehículo + carrocería compatibles con
   todas las mercancías, con medidas que admitan cada pieza en alguna orientación y
   con capacidad para la pieza más pesada. Si la carrocería principal de una
   mercancía no tiene vehículos, entra la de respaldo.
2. **Llenar con DBLF.** Usa el mismo empacador del Plan de Cargue 3D
   (`DeepestBottomLeftFill` en `optimizadores.py`), con sus validaciones de
   colisión, soporte y apilamiento. Prefiere la orientación con la que caben más
   unidades.
3. **Elegir la flota mínima.** Primero la menor cantidad de vehículos y, a igual
   cantidad, la combinación más ajustada. La flota puede ser mixta.

Reglas que conviene tener presentes:

- **Se decide por `max(%volumen, %peso)`** y se reportan los dos: una carga pesada
  llena el peso antes que el volumen.
- **La capacidad es la de la combinación vehículo + carrocería**, no la del chasis.
- **Las mercancías incompatibles se reparten** en vehículos distintos en lugar de
  rechazar la carga. La regla es por pares de categorías.
- **Extradimensión:** si una pieza no cabe en ningún vehículo del catálogo, no hay
  selección automática y la respuesta trae el motivo.

El porqué de cada decisión, las mediciones y lo que se descartó están en
`cubicador/docs/DECISIONES_TECNICAS.md`, sección 13.
