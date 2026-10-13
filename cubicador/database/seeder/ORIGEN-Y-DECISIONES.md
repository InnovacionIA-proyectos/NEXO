# Origen de los seeders y decisiones tomadas

De dónde sale cada dato de los 7 CSV de esta carpeta, qué se decidió en
cada tabla y qué casos puntuales/excepciones existen. Para qué sirve cada
tabla en sí, ver `../DICCIONARIO-DATOS.md`; para el DDL,
`../esquema/ESQUEMA_CUBICADOR.sql`.

## Fuentes

Cuatro fuentes distintas se cruzaron para llegar al estado actual:

1. **Excel "Dimensiones y Peso por vehículo"** (negocio) — el origen de
   `vehiculos` (5 configuraciones: 2L2, C2, C3, 2S2, 3S3) y de la parte
   base de `relacionVehiculosCarroceria` (qué vehículo acepta Estacas,
   Furgón o Plataforma, en términos genéricos — esa hoja no distingue
   Furgón refrigerado/climatizado/ventilado ni Planchón de Plataforma).
2. **Matriz cubicador** (negocio, 31 mercancías) — el origen de
   `tiposMercancia` (qué carrocería principal/secundaria admite cada
   mercancía, y si requiere validación condicionada de AdminTarifas).
3. **Catálogo viejo de cubicación rápida** (9 arquetipos hardcodeados en
   `cubicador/domain/motor/cubicacion_rapida.py`) — de ahí se rescataron
   `CAMA_BAJA` y `PORTACONTENEDORES`, que no estaban en ningún Excel nuevo
   pero sí existían antes.
4. **Seeder enviado por Brayan** (2026-10-08, comparado fila por fila
   contra el nuestro) y **catálogo real de SAP** (`tblSAPTiposCarroseria`,
   extraído en una EDA anterior — ver
   `analitica/00-fuentes/fuentes-corporativas/tblSAPTiposCarroseria_202609142052.csv`)
   — de ahí salieron la mayoría de los cambios de estructura que tiene
   hoy esta versión (ver abajo, tabla por tabla).

## Decisiones por tabla

### `vehiculos`

- Medidas convertidas a **centímetros** (`LargoCm`, `AnchoCm`,
  `AlturaMaximaReglamentariaCm`) — antes estaban en metros. Se cruzaron
  contra el seeder de Brayan y coinciden exacto.
- `CodigoVehiculo` para Sencillo/Dobletroque quedó como **`C2`/`C3`**
  (tal como está en el Excel y en el seeder de Brayan) — antes eran
  bare `"2"`/`"3"`, lo que de hecho causó un bug real de tipos al cargar
  a SQL Server (ver nota en `../scripts/ejecutar_schema_y_seeders.py`,
  función `valor_sql`/`COLUMNAS_NUMERICAS`).
- `AlturaMaximaReglamentariaCm` (antes `AltoVehiculoCm`, y antes de eso
  `AltoCarroceria` con default fijo 4.4 m universal) ya **no es un default
  igual para todos** — varía por vehículo: 2L2=230, C2=240, C3/2S2/3S3=260.
  Esto se decidió al confirmar que el seeder de Brayan trae el mismo valor
  repetido para cualquier carrocería de un mismo vehículo — es decir, es
  propiedad del vehículo, no de la combinación vehículo+carrocería.
  **Se renombró la columna** porque el seeder de Brayan la llama
  "altura máxima legal" — no es una medida física cualquiera del
  fabricante, es el límite que impone la reglamentación de tránsito para
  ese tipo de vehículo; el nombre anterior no dejaba eso claro.
- La columna sí quedó con `DEFAULT (440)` en el esquema — pero es solo un
  respaldo para cuando a futuro se cree un vehículo nuevo (vía el CRUD)
  sin especificar este dato todavía. Los 5 vehículos de hoy no lo usan:
  cada uno trae su propio valor explícito (230/240/260 cm).

### `TiposCarroceria`

- Estructura cambiada a **`CodigoCarroceria` + `NombreCarroceria`**
  separados (antes solo `TipoCarroceria`) — cruzada con la estructura del
  seeder de Brayan.
- El catálogo en sí sigue siendo el **nuestro**, más granular que el de
  Brayan (él no separa Furgón refrigerado/climatizado/ventilado ni
  Contenedor seco/refrigerado — ver "Casos específicos" más abajo).
- Se agregaron 5 carrocerías nuevas cruzando contra el catálogo real de
  SAP (`tblSAPTiposCarroseria`): **TANQUE, TOLVA, VOLCO, GRUA, NINERA**.
  Catálogo completo: 17 carrocerías.

### `categoriaMercancia`

- Se **adoptaron las 13 categorías de Brayan**, reemplazando las 9 que
  teníamos: separa `MATERIAL_INFLAMABLE`/`CARBURANTE` donde antes había
  un solo `COMBUSTIBLE`, y `METALICO`/`CONSTRUCCION` donde antes había un
  solo `METALICO_CONSTRUCCION`; agrega `QUIMICO_OXIDANTE` y
  `QUIMICO_CORROSIVO`.

### `tiposMercancia`

- Se agregaron las columnas nuevas que trae el seeder de Brayan:
  `CodigoMercancia`, `ControlAmbientalRequerido`, `Apilable`, `Fragil`,
  `RotacionLibre`.
- El **contenido** se cruzó fila por fila contra el de Brayan: mismo
  nombre/código, categoría de él (no la nuestra vieja), y los valores
  nuevos de control ambiental/apilable/frágil/rotación libre tal como él
  los trae.
- Las carrocerías principal/secundaria **siguen siendo las nuestras**
  (más granulares) — se resolvieron cruzando el valor genérico de Brayan
  (FURGON/CONTENEDOR) con su propio `ControlAmbientalRequerido`: por
  ejemplo, Furgón + `REFRIGERADO` (sin condicional) se tradujo a
  `FURGON_REFRIGERADO`; cuando el control es condicional o alternativo
  (`_CONDICIONAL`, `_O_...`), se mantuvo la misma resolución que ya tenía
  esta tabla antes de cruzarla con Brayan.
- **Decisión explícita: no se agregaron las 10 filas de carga NO
  cubicable** que trae el seeder de Brayan (granel sólido/líquido,
  cemento, concreto, combustibles, asfalto, vehículos en unidad,
  ingeniería especial). Se probaron y se sacaron a propósito — esta
  tabla se quedó en las 31 filas originales de la Matriz/Excel.

### `relacionVehiculosCarroceria`

- Sin cambio de estructura. El emparejamiento base (Estacas/Furgón/
  Plataforma por vehículo) ya coincidía exacto con el seeder de Brayan
  antes de tocar nada — ver "Casos específicos".
- Se agregaron las 3 variantes de Furgón (`FURGON_REFRIGERADO`,
  `FURGON_CLIMATIZADO`, `FURGON_VENTILADO`) para cada vehículo que ya
  tenía `FURGON` liso, con la misma regla de capacidad que ese vehículo
  ya tenía (mismo chasis, solo cambia el acondicionamiento de la caja).

### `reglasIncompatibilidad`

- Se **adoptaron las 3 reglas de Brayan**, reemplazando la única que
  teníamos (`COMBUSTIBLE`×`ALIMENTOS`, que ya no aplica porque esa
  categoría se partió en dos): `FERTILIZANTE`×`ALIMENTOS`,
  `QUIMICO_OXIDANTE`×`MATERIAL_INFLAMABLE`,
  `QUIMICO_CORROSIVO`×`ALIMENTOS`.

### `parametrosMotor`

- Único cambio: `k_max` pasó de `4` a **`10`** (valor de Brayan). Los
  otros 4 parámetros ya coincidían exacto entre ambos seeders.

## Casos específicos / excepciones

- **3S3 + Furgón (y sus 3 variantes) carga 30.000 kg, no 34.000** — única
  excepción real de capacidad detectada en el Excel. Vive en
  `relacionVehiculosCarroceria` (`UsaCapacidadMaximaDelVehiculo=false`,
  `CapacidadMaximaEspecifica=30000`), no se duplicó el vehículo.
- **C3 (Dobletroque) no lleva Furgón** ni ninguna de sus 3 variantes —
  así está en el Excel, y el seeder de Brayan confirma lo mismo de forma
  independiente (su `vehiculoCarroceria.csv` tampoco trae esa
  combinación).
- **Plataforma y Planchón siguen separados, no fusionados.** Se buscó
  evidencia real cruzando SICE-TAC (`NOMBREUNIDADTRANSPORTE` trae
  `PLATAFORMA`, nunca `PLANCHON`) contra SAP (`tblSAPTiposCarroseria`
  trae `PLANCHON` código 9, nunca `PLATAFORMA`) — son catálogos
  distintos que nunca traen ambos términos juntos, así que no hay con
  qué confirmar que son el mismo código con dos nombres. Queda como
  pregunta abierta para negocio/SAP, no se asumió nada.
- **7 carrocerías sin ninguna fila en `relacionVehiculosCarroceria`:**
  `CAMA_BAJA`, `PORTACONTENEDORES`, `TANQUE`, `TOLVA`, `VOLCO`, `GRUA`,
  `NINERA`. Existen en el catálogo pero a propósito no se les asignó
  ningún vehículo — con cuál(es) vehículo(s) emparejan cada una lo decide
  el Administrador de Fletes desde la pantalla del futuro CRUD, no es una
  decisión que se tomara de antemano aquí.
- **Ids determinísticos, no aleatorios** (`uuid.uuid5` sobre la clave de
  negocio — ej. `vehiculo:C2`, `carroceria:FURGON`,
  `mercancia:CAJAS_CARTON`). El mismo texto siempre genera el mismo Id,
  en cualquier máquina y cualquier día que se corra `_generar_seeders.py`
  — eso es lo que permite que `ejecutar_schema_y_seeders.py` cargue el
  seeder dos veces sin duplicar filas ni pisar una corrección hecha a
  mano por un futuro CRUD (busca el Id en la tabla real antes de
  insertar; si ya existe, no toca esa fila).

## Pendiente / preguntas abiertas

- Si Plataforma y Planchón son de verdad la misma carrocería física con
  nombre distinto según el sistema (SICE-TAC vs SAP) — no resuelto,
  necesita confirmación directa de negocio/SAP.
- Dimensiones y capacidad de los contenedores 40HC/20STD (negocio).
- Matriz de compatibilidad más allá de las 3 reglas actuales — el dueño
  del catálogo la completa por CRUD cuando salga a producción.
- Con qué vehículo(s) empareja cada una de las 7 carrocerías sin relación
  (ver arriba) — decisión del Administrador de Fletes, no nuestra.
