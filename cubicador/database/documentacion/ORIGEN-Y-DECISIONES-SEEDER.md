# Origen y decisiones del seeder

De dónde sale cada dato del catálogo inicial (`../seeder/`) y qué reglas se aplicaron. Qué
guarda cada tabla está en `DICCIONARIO-DATOS.md`.

## Fuentes

| Fuente | Qué aporta |
|---|---|
| Excel «Dimensiones y Peso por vehículo» (negocio) | Los 5 vehículos con sus medidas y capacidad, y qué carrocería básica acepta cada uno (Estacas, Furgón, Plataforma). |
| Matriz cubicador (negocio) | Las 31 mercancías: carrocería principal y alternativa, control ambiental y si requieren validación manual. |
| Catálogo de carrocerías de SAP (`tblSAPTiposCarroseria`) | Las carrocerías Tanque, Tolva, Volco, Grúa y Niñera. |
| Catálogo anterior del motor de cubicación rápida | Las carrocerías Cama baja y Portacontenedores. |

Los Excel de negocio se guardan fuera del repositorio.

## Reglas por tabla

### `vehiculos`

- 5 vehículos: Turbo 2L2, Sencillo C2, Dobletroque C3, Minimula 2S2 y Tractomula 3S3.
- Medidas en centímetros.
- `AlturaMaximaReglamentariaCm` es el límite legal de altura del tipo de vehículo: 230 cm
  (2L2), 240 cm (C2) y 260 cm (C3, 2S2 y 3S3). No depende de la carrocería.
- Un vehículo no se repite para expresar otra capacidad: esa diferencia va en
  `relacionVehiculosCarroceria`.

### `TiposCarroceria`

- 17 carrocerías, cada una con código fijo y nombre.
- No se fusionan sinónimos: Furgón refrigerado, climatizado y ventilado son códigos distintos
  porque son necesidades distintas, y Plataforma y Planchón también (ver «Preguntas abiertas»).

### `categoriaMercancia`

- 13 categorías. Las incompatibilidades se definen a este nivel.

### `tiposMercancia`

- Las 31 mercancías de la Matriz cubicador, todas cubicables.
- La carrocería es la de la Matriz, en la variante que pide el control ambiental: una mercancía
  refrigerada va en `FURGON_REFRIGERADO` y una climatizada en `FURGON_CLIMATIZADO`. Cuando el
  control admite dos opciones (`VENTILADO_O_REFRIGERADO`, `REFRIGERADO_O_CLIMATIZADO`), la
  principal es la primera. Los cilindros de gas industrial van en Plataforma, con Furgón
  ventilado como alternativa.
- `Apilable`, `Fragil` y `RotacionLibre`: si la Matriz no dice nada, una mercancía es apilable,
  no frágil y de rotación libre.
- Las cargas no cubicables (granel sólido y líquido, cemento, concreto, combustibles, asfalto,
  residuos, vehículos como unidad e ingeniería especial) no forman parte del catálogo inicial.

### `relacionVehiculosCarroceria`

- 26 combinaciones, tomadas del Excel de dimensiones. «Plancha» en el Excel corresponde a
  `PLATAFORMA`.
- Las variantes de Furgón (refrigerado, climatizado, ventilado) van en los mismos vehículos que
  llevan Furgón y con la misma capacidad: es el mismo chasis.
- El Dobletroque C3 no lleva Furgón ni sus variantes.

### `reglasIncompatibilidad`

- 3 reglas: FERTILIZANTE con ALIMENTOS, QUIMICO_OXIDANTE con MATERIAL_INFLAMABLE y
  QUIMICO_CORROSIVO con ALIMENTOS.
- El par se guarda con el Id menor primero (comparado como texto), igual que lo hace la
  aplicación.

### `parametrosMotor`

| Clave | Valor | Motivo |
|---|---|---|
| `k_max` | 25 | Con 10 se rechazaban cargas con solución (600 piezas de 1 m necesitan 13 vehículos). Con 25 el cálculo tarda centésimas de segundo; con 100 tardaría casi 10 s. |
| `factor_vol_util` | 0.90 | |
| `usar_alternativa` | `solo_respaldo` | |
| `umbral_alerta_llenado` | 0.75 | |
| `margen_cg` | 0.30 | |

## Excepciones

- **Tractomula 3S3 con Furgón o sus variantes: 30.000 kg en vez de 34.000.** Es la única
  excepción de capacidad del Excel. Se registra en `relacionVehiculosCarroceria` con
  `UsaCapacidadMaximaDelVehiculo = false` y `CapacidadMaximaEspecifica = 30000`.
- **Carrocerías sin vehículo asignado:** Cama baja, Portacontenedores, Tanque, Tolva, Volco,
  Grúa y Niñera están en el catálogo pero no en `relacionVehiculosCarroceria`. Se asignan
  desde la administración de la aplicación cuando haya un emparejamiento confirmado.

## Id de las filas

Cada Id se calcula con `uuid5` a partir de la clave de negocio de la fila (por ejemplo,
`vehiculo:C2`, `carroceria:FURGON`, `mercancia:CAJAS_CARTON`). La misma fila produce siempre
el mismo Id, así que:

- todas las bases cargadas con estos CSV tienen los mismos Id;
- regenerar los CSV no cambia los Id de las filas existentes;
- la carga se puede repetir sin duplicar filas.

## Preguntas abiertas

- Si Plataforma y Planchón son la misma carrocería física. SICE-TAC usa «PLATAFORMA» y SAP usa
  «PLANCHON», pero ninguna fuente trae los dos términos juntos para confirmarlo. Necesita
  respuesta de negocio.
- Dimensiones y capacidad de los contenedores de 20 y 40 pies.
- Reglas de incompatibilidad adicionales a las 3 actuales.
- Con qué vehículos se emparejan las 7 carrocerías sin asignar.
