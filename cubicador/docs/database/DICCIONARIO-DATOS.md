# Diccionario de datos del Cubicador

Las 16 tablas de la base `dbCubicador_Pruebas` (esquema `dbo`): para qué sirve cada una, de dónde salen sus datos y qué guarda cada columna. Tipos, nulos y valores por defecto corresponden a `../esquema/ESQUEMA_CUBICADOR.sql`.

**Origen de cada columna:**

- **Seeder**: viene del catálogo inicial (`../seeder/`) y después se puede editar desde la aplicación.
- **Aplicación**: la escribe la aplicación del Cubicador.
- **Motor**: la escribe el motor al calcular una recomendación o un plan de cargue.
- **Base de datos**: la llena SQL Server con su valor por defecto.

## Índice

| Grupo | Tabla | Seeder | Utilidad |
|---|---|---|---|
| Catálogo | [`vehiculos`](#vehiculos) | vehiculos.csv (5) | Configuraciones de vehículo entre las que el motor elige. |
| Catálogo | [`TiposCarroceria`](#tiposcarroceria) | tiposCarroceria.csv (17) | Catálogo de carrocerías. |
| Catálogo | [`categoriaMercancia`](#categoriamercancia) | categoriaMercancia.csv (13) | Categorías de mercancía. |
| Catálogo | [`tiposMercancia`](#tiposmercancia) | tiposMercancia.csv (31) | Mercancías que se pueden transportar, con su categoría, sus carrocerías y sus restricciones de manejo. |
| Catálogo | [`relacionVehiculosCarroceria`](#relacionvehiculoscarroceria) | relacionVehiculosCarroceria.csv (26) | Qué carrocerías acepta cada vehículo y con qué capacidad. |
| Catálogo | [`reglasIncompatibilidad`](#reglasincompatibilidad) | reglasIncompatibilidad.csv (3) | Pares de categorías que no pueden viajar en el mismo vehículo. |
| Catálogo | [`parametrosMotor`](#parametrosmotor) | parametrosMotor.csv (5) | Parámetros del motor de recomendación, editables sin desplegar. |
| Recomendación | [`recomendaciones`](#recomendaciones) | No | Una fila por cada vez que el motor recomienda vehículos para una línea de solicitud. |
| Recomendación | [`recomendacionVehiculo`](#recomendacionvehiculo) | No | Cada vehículo de una recomendación. |
| Recomendación | [`recomendacionVehiculoMercancia`](#recomendacionvehiculomercancia) | No | Qué mercancía y cuánta lleva cada vehículo recomendado. |
| Recomendación | [`iaSombra`](#iasombra) | No | Comparación entre la recomendación del motor y la de un modelo de lenguaje que corre en paralelo sin afectar la respuesta (modo sombra). |
| Motor 3D | [`parametros_cubicacion`](#parametros_cubicacion) | No | Límites generales de las piezas que acepta el motor 3D. |
| Motor 3D | [`vehiculos_cubicacion`](#vehiculos_cubicacion) | No | Vehículos que usa el motor 3D para acomodar la carga, con sus medidas internas. |
| Motor 3D | [`planes_cargue`](#planes_cargue) | No | Un plan de cargue. |
| Motor 3D | [`items_plan_cargue`](#items_plan_cargue) | No | Las piezas que se acomodan en un plan de cargue, con sus medidas y restricciones de manejo. |
| Motor 3D | [`colocaciones_cargue`](#colocaciones_cargue) | No | La posición de cada unidad dentro del vehículo en un plan de cargue. |

NEXO y Cotifly no consultan esta base directamente: piden las recomendaciones a la API del Cubicador.

---

## Catálogo

### vehiculos

**Utilidad:** Configuraciones de vehículo entre las que el motor elige: Turbo (2L2), Sencillo (C2), Dobletroque (C3), Minimula (2S2) y Tractomula (3S3).

**Datos:** Seeder `vehiculos.csv` (5 filas), tomado del Excel «Dimensiones y Peso por vehículo». Después se administra desde la aplicación.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `Id` | uniqueidentifier | No | nuevo GUID | Seeder | Identificador. |
| `CodigoVehiculo` | nvarchar(30) | No |  | Seeder | Código de la configuración (2L2, C2, C3, 2S2, 3S3). Único: se usa para cruzar con SICE-TAC y el tarifario. |
| `NombreVehiculo` | nvarchar(120) | No |  | Seeder | Nombre legible (Turbo 2L2, Sencillo C2...). |
| `LargoCm` | decimal(9,2) | No |  | Seeder | Largo útil de carga, en centímetros. |
| `AnchoCm` | decimal(9,2) | No |  | Seeder | Ancho útil de carga, en centímetros. |
| `AlturaMaximaReglamentariaCm` | decimal(9,2) | No | `440` | Seeder | Límite legal de altura del tipo de vehículo, en centímetros. No depende de la carrocería. |
| `CapacidadMaxima` | decimal(18,2) | No |  | Seeder | Capacidad de carga en kilos. Una carrocería puede reducirla (ver `relacionVehiculosCarroceria`). |
| `Activo` | bit | No | `1` | Seeder | Si el motor lo tiene en cuenta. |
| `CreadoPor` | nvarchar(100) | No |  | Aplicación | Quién creó el registro. En los datos iniciales, `seeder`. |
| `CreatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de creación. |
| `UpdatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de la última modificación. |
| `Version` | int | No | `1` | Aplicación | Versión del registro. La aplicación la incrementa en cada edición y rechaza un guardado hecho sobre una versión anterior. |

### TiposCarroceria

**Utilidad:** Catálogo de carrocerías. Las demás tablas se refieren a ellas por su Id.

**Datos:** Seeder `tiposCarroceria.csv` (17 filas), tomado de la Matriz cubicador, del catálogo de carrocerías de SAP y del catálogo anterior del motor de cubicación rápida. Después se administra desde la aplicación.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `Id` | uniqueidentifier | No | nuevo GUID | Seeder | Identificador. |
| `CodigoCarroceria` | nvarchar(50) | No |  | Seeder | Código fijo (FURGON, FURGON_REFRIGERADO, ESTACAS, PLATAFORMA...). Único. |
| `NombreCarroceria` | nvarchar(150) | No |  | Seeder | Nombre legible. |
| `Activo` | bit | No | `1` | Seeder | Si el motor la tiene en cuenta. |
| `CreadoPor` | nvarchar(100) | No |  | Aplicación | Quién creó el registro. En los datos iniciales, `seeder`. |
| `CreatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de creación. |
| `UpdatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de la última modificación. |
| `Version` | int | No | `1` | Aplicación | Versión del registro. La aplicación la incrementa en cada edición y rechaza un guardado hecho sobre una versión anterior. |

### categoriaMercancia

**Utilidad:** Categorías de mercancía. Las incompatibilidades se definen entre categorías, no entre mercancías.

**Datos:** Seeder `categoriaMercancia.csv` (13 filas). Después se administra desde la aplicación.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `Id` | uniqueidentifier | No | nuevo GUID | Seeder | Identificador. |
| `NombreCategoria` | nvarchar(100) | No |  | Seeder | Nombre de la categoría (GENERAL, ALIMENTOS, FARMACEUTICO...). Único. |
| `CreadoPor` | nvarchar(100) | No |  | Aplicación | Quién creó el registro. En los datos iniciales, `seeder`. |
| `CreatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de creación. |
| `UpdatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de la última modificación. |
| `Version` | int | No | `1` | Aplicación | Versión del registro. La aplicación la incrementa en cada edición y rechaza un guardado hecho sobre una versión anterior. |

### tiposMercancia

**Utilidad:** Mercancías que se pueden transportar, con su categoría, sus carrocerías y sus restricciones de manejo. Es la primera tabla que consulta el motor para decidir qué hacer con cada línea de carga.

**Datos:** Seeder `tiposMercancia.csv` (31 filas), tomado de la Matriz cubicador. Después se administra desde la aplicación.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `Id` | uniqueidentifier | No | nuevo GUID | Seeder | Identificador. |
| `CodigoMercancia` | nvarchar(50) | No |  | Seeder | Código corto (CAJAS_CARTON, CARNICOS...). Único. |
| `NombreMercancia` | nvarchar(200) | No |  | Seeder | Nombre legible. |
| `IdCategoria` | uniqueidentifier | No |  | Seeder | Categoría de la mercancía (`categoriaMercancia`). |
| `Cubicable` | bit | No | `1` | Seeder | Si el motor la procesa automáticamente. |
| `CondicionadaCubicacion` | bit | No | `0` | Seeder | Si además requiere validación manual. Es independiente de `Cubicable`. |
| `ComentarioCondicion` | nvarchar(500) | Sí |  | Seeder | Indicaciones de manejo o de la validación requerida. |
| `IdCarroceriaPrincipal` | uniqueidentifier | Sí |  | Seeder | Carrocería preferida (`TiposCarroceria`). Vacía si la mercancía no es cubicable. |
| `IdCarroceriaSecundaria` | uniqueidentifier | Sí |  | Seeder | Carrocería alternativa (`TiposCarroceria`). Debe ser distinta de la principal. |
| `CarroceriaSecundariaCondicionada` | bit | No | `0` | Seeder | Si la alternativa solo se usa bajo condiciones. Requiere que haya carrocería alternativa. |
| `ControlAmbientalRequerido` | nvarchar(40) | No | `'NINGUNO'` | Seeder | Control ambiental que exige (NINGUNO, REFRIGERADO, CLIMATIZADO_CONDICIONAL, VENTILADO_O_REFRIGERADO...). |
| `Apilable` | bit | No | `1` | Seeder | Si se puede poner carga encima. |
| `Fragil` | bit | No | `0` | Seeder | Si es frágil. |
| `RotacionLibre` | bit | No | `1` | Seeder | Si se puede girar al acomodarla. |
| `Activo` | bit | No | `1` | Seeder | Si el motor la tiene en cuenta. |
| `CreadoPor` | nvarchar(100) | No |  | Aplicación | Quién creó el registro. En los datos iniciales, `seeder`. |
| `CreatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de creación. |
| `UpdatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de la última modificación. |
| `Version` | int | No | `1` | Aplicación | Versión del registro. La aplicación la incrementa en cada edición y rechaza un guardado hecho sobre una versión anterior. |

### relacionVehiculosCarroceria

**Utilidad:** Qué carrocerías acepta cada vehículo y con qué capacidad. Permite que un vehículo cargue distinto según la carrocería sin repetir el vehículo.

**Datos:** Seeder `relacionVehiculosCarroceria.csv` (26 filas), tomado del Excel «Dimensiones y Peso por vehículo». Se edita junto con el vehículo desde la aplicación.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `Id` | uniqueidentifier | No | nuevo GUID | Seeder | Identificador. |
| `IdTipoVehiculo` | uniqueidentifier | No |  | Seeder | Vehículo (`vehiculos`). Si se borra el vehículo, se borran sus relaciones. |
| `IdCarroceria` | uniqueidentifier | No |  | Seeder | Carrocería (`TiposCarroceria`). Cada par vehículo-carrocería aparece una sola vez. |
| `UsaCapacidadMaximaDelVehiculo` | bit | No | `1` | Seeder | Si con esta carrocería aplica la capacidad del vehículo. |
| `CapacidadMaximaEspecifica` | decimal(18,2) | No | `0` | Seeder | Capacidad en kilos con esta carrocería. Solo se lee si la anterior es falsa, y en ese caso debe ser mayor que 0 (ejemplo: 3S3 con Furgón, 30.000 kg). |
| `CreadoPor` | nvarchar(100) | No |  | Aplicación | Quién creó el registro. En los datos iniciales, `seeder`. |
| `CreatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de creación. |
| `UpdatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de la última modificación. |

### reglasIncompatibilidad

**Utilidad:** Pares de categorías que no pueden viajar en el mismo vehículo. Es el primer filtro del motor: si dos mercancías de una solicitud son incompatibles, van en vehículos distintos.

**Datos:** Seeder `reglasIncompatibilidad.csv` (3 filas). Después se administra desde la aplicación.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `Id` | uniqueidentifier | No | nuevo GUID | Seeder | Identificador. |
| `IdCategoria1` | uniqueidentifier | No |  | Seeder | Primera categoría del par (`categoriaMercancia`). Es la de Id menor, comparado como texto. |
| `IdCategoria2` | uniqueidentifier | No |  | Seeder | Segunda categoría del par. Distinta de la primera; cada par aparece una sola vez. |
| `Motivo` | nvarchar(300) | No |  | Seeder | Por qué no pueden viajar juntas. |
| `CreadoPor` | nvarchar(100) | No |  | Aplicación | Quién creó el registro. En los datos iniciales, `seeder`. |
| `CreatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de creación. |
| `UpdatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de la última modificación. |
| `Version` | int | No | `1` | Aplicación | Versión del registro. La aplicación la incrementa en cada edición y rechaza un guardado hecho sobre una versión anterior. |

### parametrosMotor

**Utilidad:** Parámetros del motor de recomendación, editables sin desplegar: `k_max` (máximo de vehículos por grupo de carga), `factor_vol_util` (fracción del volumen útil aprovechable), `usar_alternativa` (cuándo usar la carrocería alternativa), `umbral_alerta_llenado` (llenado mínimo antes de alertar) y `margen_cg` (margen del centro de gravedad).

**Datos:** Seeder `parametrosMotor.csv` (5 filas). Después se administra desde la aplicación.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `Id` | uniqueidentifier | No | nuevo GUID | Seeder | Identificador. |
| `Clave` | nvarchar(50) | No |  | Seeder | Nombre del parámetro. Único. |
| `Valor` | nvarchar(100) | No |  | Seeder | Valor, guardado como texto. |
| `Descripcion` | nvarchar(300) | Sí |  | Seeder | Para qué sirve el parámetro. |
| `CreadoPor` | nvarchar(100) | No |  | Aplicación | Quién creó el registro. En los datos iniciales, `seeder`. |
| `CreatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de creación. |
| `UpdatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de la última modificación. |
| `Version` | int | No | `1` | Aplicación | Versión del registro. La aplicación la incrementa en cada edición y rechaza un guardado hecho sobre una versión anterior. |

---

## Recomendación

### recomendaciones

**Utilidad:** Una fila por cada vez que el motor recomienda vehículos para una línea de solicitud. Los registros no se editan: una recomendación consultada después debe decir lo mismo que el día que se hizo.

**Datos:** Sin seeder. La escribe el motor en cada recomendación que piden NEXO o Cotifly.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `Id` | uniqueidentifier | No | nuevo GUID | Motor | Identificador. |
| `IdLineaSolicitud` | nvarchar(100) | No |  | Motor | Línea de solicitud del sistema que pidió la recomendación. |
| `OrigenSistema` | nvarchar(20) | No |  | Motor | Sistema que la pidió: NEXO o COTIFLY. |
| `Resultado` | nvarchar(30) | No |  | Motor | ASIGNADO o SIN_SELECCION_AUTOMATICA. |
| `MotivoSinSeleccion` | nvarchar(500) | No | `'NINGUNO'` | Motor | Por qué no hubo selección automática. NINGUNO si sí la hubo. |
| `Condicionada` | bit | No | `0` | Motor | Si la recomendación requiere validación manual. |
| `AdvertenciasJson` | nvarchar(max) | No | `'[]'` | Motor | Advertencias para el usuario, en JSON. |
| `ExtradimensionJson` | nvarchar(max) | No | `'{}'` | Motor | Datos de carga extradimensionada, en JSON. |
| `MercanciasCondicionadasJson` | nvarchar(max) | No | `'[]'` | Motor | Mercancías que quedaron condicionadas, en JSON. |
| `CreadoPor` | nvarchar(100) | No |  | Motor | Quién generó la recomendación. |
| `CreatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de creación. |
| `UpdatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de la última modificación. |

### recomendacionVehiculo

**Utilidad:** Cada vehículo de una recomendación. Si una carga necesita tres vehículos, hay tres filas.

**Datos:** Sin seeder. La escribe el motor junto con `recomendaciones`.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `Id` | uniqueidentifier | No | nuevo GUID | Motor | Identificador. |
| `IdRecomendacion` | uniqueidentifier | No |  | Motor | Recomendación a la que pertenece (`recomendaciones`). |
| `IdLineaSolicitud` | nvarchar(100) | No |  | Motor | Línea de solicitud (repetida para consultar por línea sin unir tablas). |
| `OrigenSistema` | nvarchar(20) | No |  | Motor | NEXO o COTIFLY. |
| `NumeroVehiculo` | int | No |  | Motor | Número del vehículo dentro de la recomendación (1, 2, 3...). |
| `IdTipoVehiculo` | uniqueidentifier | Sí |  | Motor | Vehículo asignado (`vehiculos`). Vacío si no hubo selección automática. |
| `IdCarroceria` | uniqueidentifier | Sí |  | Motor | Carrocería asignada (`TiposCarroceria`). Vacía si no hubo selección automática. |
| `Heterogenea` | bit | No | `0` | Motor | Si el vehículo lleva más de una mercancía. |
| `PesoTotalKg` | decimal(18,2) | No |  | Motor | Peso total cargado, en kilos. |
| `VolumenTotalM3` | decimal(18,3) | No |  | Motor | Volumen total cargado, en metros cúbicos. |
| `PorcentajeLlenado` | decimal(5,2) | Sí |  | Motor | Porcentaje de llenado general. Vacío si no hubo selección automática. |
| `PorcentajeLlenadoVolumen` | decimal(5,2) | No |  | Motor | Porcentaje de llenado por volumen. |
| `PorcentajeLlenadoPeso` | decimal(5,2) | No |  | Motor | Porcentaje de llenado por peso. |
| `AlertaBajoLlenado` | bit | No | `0` | Motor | Si el llenado quedó por debajo de `umbral_alerta_llenado`. Se guarda calculada porque el umbral puede cambiar después. |
| `Resultado` | nvarchar(30) | No |  | Motor | ASIGNADO o SIN_SELECCION_AUTOMATICA. |
| `MotivoSinSeleccion` | nvarchar(500) | Sí |  | Motor | Por qué no se asignó vehículo. |
| `CreadoPor` | nvarchar(100) | No |  | Motor | Quién generó la recomendación. |
| `CreatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de creación. |
| `UpdatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de la última modificación. |

### recomendacionVehiculoMercancia

**Utilidad:** Qué mercancía y cuánta lleva cada vehículo recomendado. Si un vehículo lleva dos mercancías, hay dos filas.

**Datos:** Sin seeder. La escribe el motor junto con `recomendacionVehiculo`.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `Id` | uniqueidentifier | No | nuevo GUID | Motor | Identificador. |
| `IdRecomendacionVehiculo` | uniqueidentifier | No |  | Motor | Vehículo recomendado (`recomendacionVehiculo`). Si se borra el vehículo, se borra su detalle. |
| `IdMercancia` | uniqueidentifier | No |  | Motor | Mercancía (`tiposMercancia`). |
| `IndicePosicion` | int | Sí |  | Motor | Posición de la mercancía en la solicitud. Vacía en recomendaciones antiguas. |
| `CantidadUnidadesMercancia` | int | No |  | Motor | Unidades de esa mercancía en el vehículo. |
| `PesoMercanciaKg` | decimal(18,2) | No |  | Motor | Peso de esas unidades, en kilos. |
| `VolumenMercanciaM3` | decimal(18,4) | No |  | Motor | Volumen de esas unidades, en metros cúbicos. |
| `CreadoPor` | nvarchar(100) | No |  | Motor | Quién generó la recomendación. |
| `CreatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de creación. |
| `UpdatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de la última modificación. |

### iaSombra

**Utilidad:** Comparación entre la recomendación del motor y la de un modelo de lenguaje que corre en paralelo sin afectar la respuesta (modo sombra). Sirve para evaluar el modelo antes de usarlo. No guarda lo que se envía al modelo.

**Datos:** Sin seeder. La escribe la aplicación cuando el modo sombra está activo.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `Id` | nvarchar(36) | No |  | Aplicación | Identificador. |
| `IdRecomendacion` | nvarchar(36) | No |  | Aplicación | Recomendación comparada (`recomendaciones`, sin llave foránea). |
| `IdLineaSolicitud` | nvarchar(100) | No |  | Aplicación | Línea de solicitud. |
| `OrigenSistema` | nvarchar(20) | No |  | Aplicación | NEXO o COTIFLY. |
| `Estado` | nvarchar(40) | No |  | Aplicación | Resultado de la comparación o motivo por el que no se pudo comparar. |
| `Modelo` | nvarchar(100) | Sí |  | Aplicación | Modelo de lenguaje usado. |
| `Esfuerzo` | nvarchar(10) | Sí |  | Aplicación | Nivel de esfuerzo configurado para el modelo. |
| `ResultadoMotor` | nvarchar(40) | No |  | Aplicación | Resultado de la recomendación del motor. |
| `ResultadoIa` | nvarchar(40) | Sí |  | Aplicación | Resultado de la propuesta del modelo. |
| `MotivoMotor` | nvarchar(40) | Sí |  | Aplicación | Motivo del motor cuando no hubo selección automática. |
| `MotivoIa` | nvarchar(40) | Sí |  | Aplicación | Motivo del modelo cuando no propuso vehículos. |
| `CantidadMotor` | int | No |  | Aplicación | Cantidad de vehículos que recomendó el motor. |
| `CantidadIa` | int | Sí |  | Aplicación | Cantidad de vehículos que propuso el modelo. |
| `FlotaMotor` | nvarchar(max) | No |  | Aplicación | Vehículos recomendados por el motor, en JSON. |
| `FlotaIa` | nvarchar(max) | Sí |  | Aplicación | Vehículos propuestos por el modelo, en JSON. |
| `CoincideResultado` | bit | Sí |  | Aplicación | Si coinciden los resultados. |
| `CoincideFlota` | bit | Sí |  | Aplicación | Si coinciden los vehículos. |
| `CoincideConfiguracion` | bit | Sí |  | Aplicación | Si coinciden las configuraciones de vehículo. |
| `IaRespetaPeso` | bit | Sí |  | Aplicación | Si la propuesta del modelo respeta la capacidad de peso. |
| `IaRespetaVolumen` | bit | Sí |  | Aplicación | Si respeta el volumen. |
| `IaConservaUnidades` | bit | Sí |  | Aplicación | Si conserva todas las unidades de la carga. |
| `Violaciones` | nvarchar(max) | Sí |  | Aplicación | Reglas que incumple la propuesta del modelo, en JSON. |
| `Explicacion` | nvarchar(500) | Sí |  | Aplicación | Explicación del modelo. |
| `TokensEntrada` | int | No | `0` | Aplicación | Tokens enviados al modelo. |
| `TokensSalida` | int | No | `0` | Aplicación | Tokens de la respuesta del modelo. |
| `LatenciaIaMs` | int | Sí |  | Aplicación | Tiempo de respuesta del modelo, en milisegundos. |
| `CreatedAt` | datetimeoffset | No | fecha actual | Base de datos | Fecha y hora de la comparación. |

---

## Motor 3D

### parametros_cubicacion

**Utilidad:** Límites generales de las piezas que acepta el motor 3D.

**Datos:** Sin seeder. Se administra desde la aplicación.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `id` | int | No | autoincremental | Aplicación | Identificador numérico (autoincremental). |
| `largo_max_cm` | float | No |  | Aplicación | Largo máximo de una pieza, en centímetros. |
| `ancho_max_cm` | float | No |  | Aplicación | Ancho máximo de una pieza, en centímetros. |
| `alto_max_cm` | float | No |  | Aplicación | Alto máximo de una pieza, en centímetros. |
| `peso_max_kg` | float | No |  | Aplicación | Peso máximo de una pieza, en kilos. |
| `updated_at` | datetimeoffset | No |  | Aplicación | Fecha y hora de la última modificación. |

### vehiculos_cubicacion

**Utilidad:** Vehículos que usa el motor 3D para acomodar la carga, con sus medidas internas.

**Datos:** Sin seeder. La aplicación crea los vehículos predefinidos (`es_preset = 1`) la primera vez que arranca; los demás se crean desde la aplicación.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `id` | varchar(36) | No |  | Aplicación | Identificador. |
| `nombre` | varchar(100) | No |  | Aplicación | Nombre del vehículo. |
| `descripcion` | varchar(300) | Sí |  | Aplicación | Descripción. |
| `largo_interno_mm` | float | No |  | Aplicación | Largo interno de la caja, en milímetros. |
| `ancho_interno_mm` | float | No |  | Aplicación | Ancho interno, en milímetros. |
| `alto_interno_mm` | float | No |  | Aplicación | Alto interno, en milímetros. |
| `capacidad_kg` | float | No |  | Aplicación | Capacidad de carga, en kilos. |
| `tara_kg` | float | No |  | Aplicación | Peso del vehículo vacío, en kilos. |
| `puerta_ancho_mm` | float | No |  | Aplicación | Ancho de la puerta, en milímetros. |
| `puerta_alto_mm` | float | No |  | Aplicación | Alto de la puerta, en milímetros. |
| `puerta_posicion` | varchar(20) | No |  | Aplicación | Dónde está la puerta. |
| `ejes_json` | varchar(max) | No |  | Aplicación | Configuración de ejes, en JSON. |
| `tolerancia_paredes_mm` | float | No |  | Aplicación | Espacio que se deja contra las paredes, en milímetros. |
| `tolerancia_entre_unidades_mm` | float | No |  | Aplicación | Espacio que se deja entre unidades, en milímetros. |
| `es_preset` | bit | No |  | Aplicación | Si es un vehículo predefinido de la aplicación. |
| `created_at` | datetimeoffset | No |  | Aplicación | Fecha y hora de creación. |
| `updated_at` | datetimeoffset | No |  | Aplicación | Fecha y hora de la última modificación. |

### planes_cargue

**Utilidad:** Un plan de cargue: el acomodo calculado de una carga dentro de un vehículo.

**Datos:** Sin seeder. Lo escribe el motor 3D en cada cálculo.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `id` | varchar(36) | No |  | Motor | Identificador. |
| `nombre` | varchar(150) | No |  | Motor | Nombre del plan. |
| `vehiculo_id` | varchar(36) | No |  | Motor | Vehículo usado (`vehiculos_cubicacion`). |
| `linea_solicitud_id` | varchar(36) | Sí |  | Motor | Línea de solicitud asociada, si la hay. |
| `numero_vehiculo` | int | No |  | Motor | Número del vehículo cuando la carga usa varios. |
| `estado` | varchar(20) | No |  | Motor | Estado del cálculo. |
| `estrategia` | varchar(40) | No |  | Motor | Estrategia de acomodo usada. |
| `ponderaciones_json` | varchar(max) | Sí |  | Motor | Pesos de los criterios de la estrategia, en JSON. |
| `algoritmo` | varchar(10) | No |  | Motor | Algoritmo de acomodo usado. |
| `semilla` | int | Sí |  | Motor | Semilla del cálculo, para poder repetirlo igual. |
| `peso_total_kg` | float | No |  | Motor | Peso total cargado, en kilos. |
| `volumen_utilizado_m3` | float | No |  | Motor | Volumen ocupado, en metros cúbicos. |
| `utilizacion_volumen` | float | No |  | Motor | Fracción del volumen del vehículo que se ocupa. |
| `utilizacion_piso` | float | No |  | Motor | Fracción del piso del vehículo que se ocupa. |
| `puntaje` | float | No |  | Motor | Calificación del plan. |
| `incidencias_json` | varchar(max) | No |  | Motor | Problemas encontrados en el cálculo, en JSON. |
| `error_mensaje` | varchar(500) | Sí |  | Motor | Mensaje de error si el cálculo falló. |
| `created_at` | datetimeoffset | No |  | Aplicación | Fecha y hora de creación. |
| `updated_at` | datetimeoffset | No |  | Aplicación | Fecha y hora de la última modificación. |

### items_plan_cargue

**Utilidad:** Las piezas que se acomodan en un plan de cargue, con sus medidas y restricciones de manejo.

**Datos:** Sin seeder. Las escribe el motor 3D con cada plan.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `id` | varchar(36) | No |  | Motor | Identificador. |
| `plan_cargue_id` | varchar(36) | No |  | Motor | Plan de cargue (`planes_cargue`). |
| `sku` | varchar(60) | No |  | Motor | Código de la pieza. |
| `descripcion` | varchar(200) | No |  | Motor | Descripción. |
| `pedido` | varchar(60) | Sí |  | Motor | Pedido al que pertenece. |
| `cliente` | varchar(120) | Sí |  | Motor | Cliente. |
| `destino` | varchar(120) | Sí |  | Motor | Destino de entrega. |
| `secuencia_entrega` | int | No |  | Motor | Orden de entrega (define qué se carga primero). |
| `largo_mm` | float | No |  | Motor | Largo, en milímetros. |
| `ancho_mm` | float | No |  | Motor | Ancho, en milímetros. |
| `alto_mm` | float | No |  | Motor | Alto, en milímetros. |
| `peso_kg` | float | No |  | Motor | Peso de una unidad, en kilos. |
| `cantidad` | int | No |  | Motor | Cantidad de unidades. |
| `color` | varchar(9) | Sí |  | Motor | Color para mostrarla en la vista 3D. |
| `prioridad` | int | No |  | Motor | Prioridad al acomodar. |
| `tipo_embalaje` | varchar(40) | Sí |  | Motor | Tipo de embalaje. |
| `apilable` | bit | No |  | Motor | Si se puede poner carga encima. |
| `max_niveles` | int | No |  | Motor | Máximo de niveles apilados. |
| `max_peso_encima_kg` | float | No |  | Motor | Peso máximo que soporta encima, en kilos. |
| `rotable` | bit | No |  | Motor | Si se puede girar. |
| `mantener_vertical` | bit | No |  | Motor | Si debe ir siempre en posición vertical. |
| `fragil` | bit | No |  | Motor | Si es frágil. |
| `peligroso` | bit | No |  | Motor | Si es mercancía peligrosa. |
| `refrigerado` | bit | No |  | Motor | Si requiere refrigeración. |
| `grupo_compatibilidad` | varchar(40) | Sí |  | Motor | Grupo para separar piezas que no pueden ir juntas. |
| `observaciones` | varchar(300) | Sí |  | Motor | Observaciones. |
| `created_at` | datetimeoffset | No |  | Aplicación | Fecha y hora de creación. |
| `updated_at` | datetimeoffset | No |  | Aplicación | Fecha y hora de la última modificación. |

### colocaciones_cargue

**Utilidad:** La posición de cada unidad dentro del vehículo en un plan de cargue.

**Datos:** Sin seeder. Las escribe el motor 3D con cada plan.

| Columna | Tipo | Nulo | Por defecto | Origen | Descripción |
|---|---|---|---|---|---|
| `id` | varchar(36) | No |  | Motor | Identificador. |
| `plan_cargue_id` | varchar(36) | No |  | Motor | Plan de cargue (`planes_cargue`). |
| `item_plan_cargue_id` | varchar(36) | No |  | Motor | Pieza colocada (`items_plan_cargue`). |
| `indice_unidad` | int | No |  | Motor | Número de la unidad dentro de la pieza. |
| `cargada` | bit | No |  | Motor | Si la unidad cupo en el vehículo. |
| `x` | float | No |  | Motor | Posición a lo largo, en milímetros. |
| `y` | float | No |  | Motor | Posición a lo ancho, en milímetros. |
| `z` | float | No |  | Motor | Altura, en milímetros. |
| `largo_colocado_mm` | float | No |  | Motor | Largo según cómo quedó orientada, en milímetros. |
| `ancho_colocado_mm` | float | No |  | Motor | Ancho según cómo quedó orientada, en milímetros. |
| `alto_colocado_mm` | float | No |  | Motor | Alto según cómo quedó orientada, en milímetros. |
| `orientacion_indice` | int | No |  | Motor | Orientación usada. |
| `secuencia_cargue` | int | No |  | Motor | Orden en que se carga. |
| `soporte_colocacion_id` | varchar(36) | Sí |  | Motor | Unidad sobre la que se apoya, si va encima de otra. |
| `porcentaje_soporte` | float | No |  | Motor | Fracción de la base que queda apoyada. |
| `peso_encima_kg` | float | No |  | Motor | Peso que soporta encima, en kilos. |
| `estado_estabilidad` | varchar(20) | No |  | Motor | Resultado de la revisión de estabilidad. |
| `accesibilidad` | varchar(30) | No |  | Motor | Qué tan accesible queda para descargarla. |
| `bloqueada` | bit | No |  | Motor | Si el usuario la fijó en su posición. |
| `ajuste_manual` | bit | No |  | Motor | Si el usuario la movió a mano. |
| `codigo_rechazo` | varchar(30) | Sí |  | Motor | Código del motivo si no se pudo cargar. |
| `razon_rechazo` | varchar(500) | Sí |  | Motor | Motivo si no se pudo cargar. |
| `created_at` | datetimeoffset | No |  | Aplicación | Fecha y hora de creación. |
| `updated_at` | datetimeoffset | No |  | Aplicación | Fecha y hora de la última modificación. |

---

## Relaciones

```
categoriaMercancia ──┬── tiposMercancia ──── (carrocería principal y alternativa) ── TiposCarroceria
                     └── reglasIncompatibilidad (categoría 1, categoría 2)              │
                                                                                          │
vehiculos ──────────── relacionVehiculosCarroceria ───────────────────────────────────────┘

recomendaciones ── recomendacionVehiculo ── recomendacionVehiculoMercancia ── tiposMercancia
                        ├── vehiculos
                        └── TiposCarroceria

vehiculos_cubicacion ── planes_cargue ── items_plan_cargue ── colocaciones_cargue

parametrosMotor, parametros_cubicacion e iaSombra no tienen llaves foráneas.
```
