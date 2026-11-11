# Contrato de integración del Cubicador

Todo lo que el Cubicador expone (API REST y mensajes de RabbitMQ), lo que consume de NEXO, la forma exacta de los datos, los
códigos de error y la autenticación. El mismo contrato de mensajes, visto desde NEXO, está en
[`docs/mensajeria/CUBICACION.md`](../docs/mensajeria/CUBICACION.md). Si cambia un endpoint, un DTO, un mensaje o un código de
error, este es el archivo que se actualiza.

## 1. Primeros pasos

| Entorno | URL base | Notas |
|---|---|---|
| Docker Compose local | `http://localhost:8081` | Puerto publicado al host (`CUBICADOR_HOST_PORT`, 8081 por defecto, para no chocar con el 8080 del backend de NEXO). Dentro del contenedor la API escucha en el 8000. |
| Local sin Docker (`uvicorn`) | `http://localhost:8000` | Ver [`README.md`](./README.md), «Puesta en marcha». |
| Dokploy | `http://<host-interno-de-cubicador-api>:8000` | Es el valor de `Cubicador3D__OutboundBaseUrl` en NEXO. |

- **Swagger:** `{URL_BASE}/docs`. En el botón *Authorize* se pega la llave sin la palabra `Bearer`. Para `/recomendaciones` hace
  falta una llave por sistema (sección 5); para el resto sirve cualquiera de las tres.
- **OpenAPI exportado:** [`docs/openapi.json`](./docs/openapi.json), para importarlo en Postman o Insomnia o generar un cliente
  sin levantar el servicio. Se regenera después de cambiar rutas o DTOs, y con `--verificar` solo comprueba que esté al día:

  ```bash
  python scripts/generar_openapi.py              # desde cubicador/
  python scripts/generar_openapi.py --verificar  # devuelve 1 si quedó desactualizado
  ```

### 1.1 Cómo se comunica NEXO con este servicio

| Dirección | Canal | Qué |
|---|---|---|
| NEXO → Cubicador | Cola, routing key `cubicacion.solicitada` | Toda escritura del plan de cargue y de los vehículos del motor 3D: 11 tipos de comando (sección 6.1). |
| Cubicador → NEXO | Cola, varias routing keys | Resultado del plan, rechazos y cambios en los catálogos (sección 6.2). |
| NEXO → Cubicador | HTTP | `POST /recomendaciones`, su reconsulta y `GET /recomendaciones/explicacion`, con `Cubicador3D:RecomendacionesApiKey`; y el reenvío de los CRUD de catálogo, con `Cubicador3D:OutboundApiKey` (secciones 3.1 y 3.2). |
| Cubicador → NEXO | HTTP | Solo `GET /api/lineas-solicitud/{id}/cubicacion`, para armar un plan desde una línea (sección 7). |

NEXO no llama a `/api/planes-cargue` de este servicio: publica los comandos por la cola y sirve sus lecturas desde su propia
proyección ([`CUBICACION.md`](../docs/mensajeria/CUBICACION.md), sección 6). Por qué cada llamada HTTP es una excepción a la regla
de comunicarse por cola está en la sección 7 de ese mismo documento.

Para conectar Cotifly, ver [`COTIFLY-COMO-CONECTARSE-AL-CUBICADOR.md`](../docs/integraciones/COTIFLY-COMO-CONECTARSE-AL-CUBICADOR.md).

### 1.2 Navegadores

Ningún navegador llama a este servicio. `api/main.py` no tiene middleware CORS y no existe una variable para activarlo: la
capacidad se retiró del todo, no está apagada por defecto. Las pantallas usan los endpoints de NEXO, que aplica los roles y
reenvía o publica lo que corresponde. Volver a exponer el servicio a un navegador sería una decisión nueva, no una variable que
encender.

Las llaves del Cubicador son secretos de backend. `CUBICADOR_API_KEY` se rotó después de haber estado expuesta en el bundle
público del portal, cuando el navegador todavía llamaba directo.

### 1.3 Antes de integrar

- [ ] El `rabbit` de la raíz de NEXO está corriendo antes de levantar `cubicador/` (`docker compose up -d --wait rabbit` desde
      la raíz). No hay `depends_on` entre proyectos Compose distintos.
- [ ] `RABBITMQ_USER` y `RABBITMQ_PASSWORD` son idénticos en el `.env` de la raíz de NEXO y en el de `cubicador/`.
- [ ] Las llaves están configuradas en pares (sección 5): `CUBICADOR_API_KEY` = `Cubicador3D__OutboundApiKey`,
      `CUBICADOR_API_KEY_NEXO` = `Cubicador3D__RecomendacionesApiKey` y `NEXO_CALLBACK_API_KEY` = `Cubicador3D__InboundApiKey`.
- [ ] En NEXO, la compuerta de paridad (`Cubicacion__ParidadVerificada`, `ParidadEvidencia`, `ParidadFecha`) está encendida. Si
      no, `POST /api/planes-cargue/{id}/optimizar` responde `503 CUBICACION_EN_VERIFICACION` sin publicar nada
      ([`CHECKLIST_DESPLIEGUE.md`](../docs/despliegue/CHECKLIST_DESPLIEGUE.md), sección 1).
- [ ] `DATABASE_URL` y `CUBICADOR_DB_ESQUEMA` apuntan a una base SQL Server con el esquema ya aplicado
      ([`database/README.md`](./database/README.md)). La aplicación no crea tablas en SQL Server.

---

## 2. Convenciones generales

- **JSON en camelCase** en todo request y response (`api/dtos/comun.py`: `ApiModel` con `alias_generator=to_camel`). Los
  atributos en Python van en snake_case.
- **Unidades:**
  - Motor 3D (`/api/planes-cargue`): milímetros y kilogramos.
  - Selección de flota y catálogo (`/recomendaciones`, `/vehiculos-catalogo`, `/medidas-estandar`): centímetros y kilogramos.

  Son dos sistemas heredados de prototipos distintos y no se unifican. Una integración que toque los dos no debe asumir la
  misma unidad.
- **Ejes del motor 3D:** X = largo, Y = ancho, Z = alto. El origen (0,0,0) es la esquina inferior interna opuesta a la puerta
  trasera.
- **Números:** el motor 3D trabaja con `float`. `/recomendaciones` y los catálogos usan `Decimal` internamente y lo serializan
  como número JSON, no como cadena (`DecimalJson` en `api/dtos/comun.py`).
- **Ids:** cadenas con formato GUID. Los de las recomendaciones vienen en mayúsculas (sección 3.1).

---

## 3. API REST

### 3.1 `/recomendaciones`: selección de flota

Dada una carga, responde qué vehículos hacen falta, cuántos y qué mercancía va en cada uno. Cada corrida se guarda. La
especificación funcional completa, con ejemplos de las tres formas de respuesta, está en la sección 2 de
[`docs/HOJA-DE-RUTA-CUBICADOR.md`](./docs/HOJA-DE-RUTA-CUBICADOR.md); aquí va lo operativo.

| Método | Ruta | Body | Respuesta |
|---|---|---|---|
| POST | `/recomendaciones` | `CrearRecomendacionDto` | `200 RecomendacionDto` |
| GET | `/recomendaciones?idLineaSolicitud=&origenSistema=` | — | `200 RecomendacionDto` (la última corrida de esa línea) |
| GET | `/recomendaciones/explicacion?idLineaSolicitud=&origenSistema=` | — | `200 ExplicacionDto` |
| GET | `/recomendaciones/{idRecomendacionVehiculo}/mercancias` | — | `200 MercanciaDeVehiculoDto[]` |

Las cuatro exigen una llave por sistema (sección 5); la genérica responde `401`.

**Request (`CrearRecomendacionDto`):**

| Campo | Obligatorio | Notas |
|---|---|---|
| `idLineaSolicitud` | Sí | Código de la línea en el sistema que llama. |
| `origenSistema` | No | El origen real sale de la llave. Si viene y no coincide, `401`. |
| `claseViajeSeleccionada` | No | Preferencia, no filtro: el motor corre completo y, si su resultado no coincide, lo dice en `advertencias`. |
| `usuarioId` | No | Declarado por el llamante, para auditoría. No se verifica. |
| `mercancias[]` | Sí | Una entrada por posición: `codigoMercancia`, `cantidad` (≥ 1), `largoCm`, `anchoCm`, `altoCm` y `pesoKg` (> 0; peso por unidad, no el total). |

**Response (`RecomendacionDto`):** `idRecomendacion`, `idLineaSolicitud`, `origenSistema`, `resultado` (`ASIGNADO` o
`SIN_SELECCION_AUTOMATICA`), `motivoSinSeleccion`, `condicionada`, `mercanciasCondicionadas[]`, `vehiculos[]`,
`advertencias[]` y `extradimension`.

- Cada vehículo trae `idRecomendacionVehiculo`, `numeroVehiculo`, `codigoVehiculo`, `nombreVehiculo`, `codigoCarroceria`,
  `pesoTotalKg`, `volumenTotalM3`, `porcentajeLlenadoVolumen`, `porcentajeLlenadoPeso`, `alertaBajoLlenado`, `heterogenea` y
  `mercancias[]`. Peso y volumen van con dos decimales y los porcentajes con uno, tanto en el `POST` como en la reconsulta.
- `extradimension` trae `hayExtradimension`, `medidasEstandar` (los topes de `/medidas-estandar`) y `piezas[]`
  (`indicePosicion`, `idTipoMercancia`, `codigoMercancia`, `excesos[]`).

**En el request entra el código; en la respuesta salen los dos.** El request lleva el `codigoMercancia` de
`GET /tipos-mercancia`, en cualquier combinación de mayúsculas y con espacios de sobra. La respuesta lleva `idTipoMercancia` y
`codigoMercancia`, así que nadie tiene que resolver uno a partir del otro. El dato viaja como código porque es la forma que ya
tiene en el resto del recorrido: NEXO lo persiste así en la captura, igual que `codigoVehiculo` y `codigoCarroceria`
([`DECISIONES_TECNICAS.md`](./docs/DECISIONES_TECNICAS.md) #17).

**El detalle por camión es por posición, no por código.** `mercancias[].indicePosicion` es el índice, base 0, de la posición en
el arreglo `mercancias` del request. Dos filas del mismo camión pueden traer el mismo `codigoMercancia` si la solicitud declaró
dos posiciones de ese tipo con medidas distintas: quien agrupe por código tiene que sumar. Las medidas no se devuelven porque el
llamante ya las tiene. `piezas[]` de `extradimension` lleva el índice por la misma razón: dos posiciones del mismo tipo pueden
tener medidas distintas y solo una exceder. `indicePosicion` viaja en `null` al reconsultar corridas guardadas antes de que
existiera el campo. `heterogenea` se calcula sobre códigos distintos, no sobre el número de filas
([`DECISIONES_TECNICAS.md`](./docs/DECISIONES_TECNICAS.md) #19).

**Los ids vienen en mayúsculas.** SQL Server devuelve así las columnas `uniqueidentifier`; se generan ya en mayúsculas para que
el id del `POST` y el de la reconsulta sean el mismo texto.

**Lo que antes era error ahora es resultado.** `SIN_SELECCION_AUTOMATICA`, `condicionada` y `extradimension` vuelven con `200`,
dentro de la respuesta. Valores de `motivoSinSeleccion`: `EXTRADIMENSIONADO`, `SIN_CANDIDATOS_DIMENSIONALES`, `SUPERA_K_MAX`,
`SIN_CARROCERIA_COMPATIBLE` y `BUSQUEDA_AGOTADA`. Este último es una condición del algoritmo, no del negocio: el buscador agotó
sus intentos de empaque antes de encontrar una flota, que no es lo mismo que no caber en `k_max` vehículos.

**Límites.** Máximo 200 posiciones (las mismas que admite una línea de captura en NEXO) y máximo 10.000 unidades en total
(`application/recomendaciones/casos_de_uso.py`). Pasarse de cualquiera de los dos es `400 CUBICACION_DEMASIADO_GRANDE`. El de
posiciones existe porque el detalle es una fila por posición: sin él, 10.000 posiciones de una unidad pasarían el tope de unidades
y producirían otras tantas filas en la respuesta y en la base.

**Reconsulta.** `origenSistema` es obligatorio en la query y no se toma de la llave: un sistema puede necesitar leer lo que
recomendó el otro, y un mismo código de línea puede existir en los dos. Debe ser `NEXO` o `COTIFLY`
(`400 ORIGEN_SISTEMA_INVALIDO`). Devuelve la última corrida de esa línea, o `404 RECOMENDACION_NO_ENCONTRADA`.

**Las corridas no se modifican.** Recalcular una ruta crea una corrida nueva. La anterior queda intacta, y un
`idRecomendacionVehiculo` guardado sigue devolviendo lo mismo en `GET /recomendaciones/{id}/mercancias`.

**Explicación.** `GET /recomendaciones/explicacion` devuelve `{ "explicacion": "..." }`: dos o tres frases, escritas por un
modelo de IA, sobre por qué se eligió la flota de la última corrida de esa línea. Va aparte del `POST` a propósito: el motor
responde en décimas de segundo y el modelo tarda segundos, así que la pantalla muestra las cifras primero y pide el texto
después. Responde `200` aunque no haya texto: `explicacion` es `null` si no está configurada `ANTHROPIC_API_KEY`, si el proveedor
falla o si el texto trae una cifra que no salió del motor. Solo responde `404 RECOMENDACION_NO_ENCONTRADA` si la línea no tiene
corridas.

**Modo sombra.** No es un endpoint. Con `IA_SOMBRA_HABILITADA=true` y `ANTHROPIC_API_KEY`, cada `POST /recomendaciones` agenda
en segundo plano una comparación entre la flota del motor y la que propone el modelo, y la guarda en la tabla `iaSombra`. La
respuesta no cambia ni espera por ella. Las demás variables `IA_SOMBRA_*` limitan el muestreo, el gasto diario de tokens y la
concurrencia ([`.env.example`](./.env.example)).

**Errores propios:** `MERCANCIAS_REQUERIDAS`, `MERCANCIA_DESCONOCIDA`, `MERCANCIA_NO_CUBICABLE`, `DIMENSIONES_INVALIDAS` y
`CUBICACION_DEMASIADO_GRANDE` (400), `NO_AUTORIZADO` (401) y `CUBICADOR_NO_DISPONIBLE` (503). Un campo con tipo o rango inválido
(por ejemplo, `cantidad: 0`) se rechaza antes, con `422` (sección 4).

DTOs en `api/dtos/recomendaciones.py`.

### 3.2 Catálogos de la selección de flota

El catálogo que alimenta `/recomendaciones` vive en el Cubicador, que es el dueño de ese dominio. El portal lo administra a
través de NEXO, que espeja estas rutas bajo `/api/...` y aplica el rol (`AdminTarifas` o `Admin` para escribir; cualquier
usuario autenticado para leer). Este servicio no puede aplicar roles: su llave autoriza a un backend, no a una persona.

Todas las rutas aceptan cualquiera de las tres llaves (sección 5).

| Catálogo | Rutas | Qué administra |
|---|---|---|
| Tipos de carrocería | `GET` `POST` `PUT /{id}` `DELETE /{id}` en `/tipos-carroceria`, y `POST /tipos-carroceria/republicar-catalogo` | Las carrocerías y su código. |
| Categorías de mercancía | `GET` `POST` `PUT /{id}` `DELETE /{id}` en `/categorias-mercancia` | Las familias que agrupan las mercancías y sobre las que se declaran las incompatibilidades. |
| Tipos de mercancía | `GET` `POST` `PUT /{id}` `DELETE /{id}` en `/tipos-mercancia`, y `POST /tipos-mercancia/republicar-catalogo` | Qué se transporta, con qué carrocería y bajo qué condiciones. |
| Vehículos del catálogo | `GET` `POST` `PUT /{id}` `DELETE /{id}` en `/vehiculos-catalogo`, y `POST /vehiculos-catalogo/republicar-catalogo` | Las configuraciones, con sus medidas, capacidad y carrocerías admitidas. |
| Reglas de incompatibilidad | `GET` `POST` `DELETE /{id}` en `/reglas-incompatibilidad`, sin `PUT` | Qué pares de categorías no pueden viajar juntos. Cambiar el par de una regla es, en los hechos, otra regla. |
| Parámetros del motor | `GET /parametros-motor` y `PUT /parametros-motor/{clave}` | `k_max`, `umbral_alerta_llenado`, `usar_alternativa`, `factor_vol_util` y `margen_cg`. No se crean ni se borran: agregar uno es un cambio de código. |
| Medidas estándar | `GET /medidas-estandar` y `PUT /medidas-estandar` | Los topes contra los que se detecta extradimensión (`largoMaxCm`, `anchoMaxCm`, `altoMaxCm`, `pesoMaxKg`). Sin versión ni `usuarioId`. |

> `/vehiculos-catalogo` **no es** `/api/planes-cargue/vehiculos`. Son tablas distintas: la primera alimenta la selección de
> flota y la segunda es el catálogo propio del motor 3D. Entre las dos hay un diccionario de compatibilidad en
> `application/plan_cargue/desde_linea.py` (sección 7).

**Lectura:**

| DTO | Campos |
|---|---|
| `TipoCarroceriaDto` | `id`, `codigoCarroceria`, `nombreCarroceria`, `activo`, `version` |
| `CategoriaMercanciaDto` | `id`, `nombreCategoria`, `version` |
| `TipoMercanciaDto` | `id`, `codigoMercancia`, `nombreMercancia`, `categoria` (nombre), `cubicable`, `condicionadaCubicacion`, `carroceriaPrincipal` y `carroceriaSecundaria` (códigos), `carroceriaSecundariaCondicionada`, `controlAmbientalRequerido`, `apilable`, `fragil`, `rotacionLibre`, `comentarioCondicion`, `activo`, `version` |
| `VehiculoCatalogoDto` | `id`, `codigoVehiculo`, `nombreVehiculo`, `largoCm`, `anchoCm`, `altoUtilCm`, `capacidadMaximaKg`, `activo`, `version`, `carrocerias[]` (`idCarroceria`, `codigoCarroceria`, `usaCapacidadMaximaDelVehiculo`, `capacidadMaximaEspecifica`) |
| `ReglaIncompatibilidadDto` | `id`, `idCategoria1`, `idCategoria2`, `motivo`, `version` |
| `ParametroMotorDto` | `id`, `clave`, `valor`, `descripcion`, `version` |

`controlAmbientalRequerido` toma uno de estos valores: `NINGUNO`, `REFRIGERADO`, `VENTILADO_CONDICIONAL`,
`CLIMATIZADO_CONDICIONAL`, `REFRIGERADO_O_CLIMATIZADO` o `VENTILADO_O_REFRIGERADO`.

**Escritura.** Los `POST` responden `201` con la fila creada, los `PUT` `200` con la fila actualizada y los `DELETE` `204`. El
`DELETE` lleva cuerpo: `{ "usuarioId", "version" }`.

| Ruta | Campos del body |
|---|---|
| `POST /tipos-carroceria` | `codigoCarroceria`, `nombreCarroceria` |
| `PUT /tipos-carroceria/{id}` | `nombreCarroceria`, `activo` (por defecto `true`). El código no se edita. |
| `POST` y `PUT /categorias-mercancia` | `nombreCategoria` (se guarda en mayúsculas) |
| `POST` y `PUT /tipos-mercancia` | `codigoMercancia`, `nombreMercancia`, `idCategoria`, `idCarroceriaPrincipal`, `idCarroceriaSecundaria`, `cubicable`, `condicionadaCubicacion`, `comentarioCondicion`, `carroceriaSecundariaCondicionada`, `controlAmbientalRequerido` (por defecto `NINGUNO`), `apilable`, `fragil`, `rotacionLibre`, `activo` |
| `POST` y `PUT /vehiculos-catalogo` | `codigoVehiculo`, `nombreVehiculo`, `largoCm`, `anchoCm`, `altoUtilCm`, `capacidadMaximaKg` (> 0), `activo`, `carrocerias[]` (`idCarroceria`, `usaCapacidadMaximaDelVehiculo`, por defecto `true`, y `capacidadMaximaEspecifica`, obligatoria y mayor que cero cuando la anterior es `false`) |
| `POST /reglas-incompatibilidad` | `idCategoria1`, `idCategoria2`, `motivo` |
| `PUT /parametros-motor/{clave}` | `valor` |

Al escribir, la mercancía referencia categoría y carrocerías por **id**; al leer, las devuelve por nombre y código. El vehículo
y sus carrocerías viajan en un solo payload, y el servicio reconcilia las relaciones.

**`usuarioId` es obligatorio en toda escritura** (salvo `/medidas-estandar`). El Cubicador no tiene usuarios: NEXO manda el id y
aquí queda en `CreadoPor` como dato declarado, no como identidad comprobada.

**Concurrencia optimista.** Todo `GET` devuelve `version` y todo `PUT` y `DELETE` la exige en el cuerpo. Si la fila cambió desde
que el formulario la leyó, el servicio responde `409 CONFLICTO_DE_VERSION` en vez de pisar el cambio ajeno: en un catálogo que
alimenta al motor, ese cambio perdido no se vería como un error sino como recomendaciones sutilmente equivocadas. Las carrocerías
de un vehículo no tienen versión propia: se exige y se sube la del vehículo, aunque el cambio solo toque las relaciones.

**Borrado.**

- Es físico y se bloquea con `409 CATALOGO_REFERENCIADO` si otras filas referencian la que se borra; el mensaje dice cuántas y
  de dónde.
- Las carrocerías de un vehículo le pertenecen: se borran con él.
- Una mercancía que ya participó en alguna recomendación no se borra: se desactiva (`activo = false`) y el `DELETE` responde
  `204` igual. Las recomendaciones guardadas tienen que seguir diciendo lo mismo.
- Las reglas se guardan con el par de categorías normalizado: `A-B` y `B-A` son la misma regla (`409 REGLA_DUPLICADA`).

**Filas inactivas.** Los `GET` de carrocerías, mercancías y vehículos devuelven también las filas inactivas, con su `activo`,
para que la pantalla de administración pueda verlas y reactivarlas. Quien arma un desplegable de captura filtra por `activo`.
Lo mismo aplica a la proyección de NEXO, que guarda la fila completa.

**Errores:**

| HTTP | Código | Cuándo |
|---|---|---|
| 400 | `CODIGO_REQUERIDO`, `NOMBRE_REQUERIDO`, `VALOR_REQUERIDO`, `MOTIVO_REQUERIDO` | Falta un dato obligatorio. |
| 400 | `CARROCERIA_PRINCIPAL_REQUERIDA`, `CARROCERIA_SECUNDARIA_IGUAL`, `CONDICIONADA_SIN_SECUNDARIA` | Carrocerías de una mercancía. |
| 400 | `SIN_CARROCERIAS`, `CARROCERIA_REPETIDA`, `CARROCERIA_DESCONOCIDA`, `CAPACIDAD_ESPECIFICA_INVALIDA` | Carrocerías de un vehículo. |
| 400 | `DOMINANCIA_ROTA` | El vehículo guardado rompería la regla de dominancia del catálogo. |
| 400 | `CATEGORIAS_IGUALES` | Una regla con la misma categoría en los dos lados. |
| 400 | `MEDIDAS_INVALIDAS` | `PUT /medidas-estandar` con valores no válidos. |
| 404 | `NO_ENCONTRADO`, `PARAMETRO_NO_ENCONTRADO` | El id o la clave no existen. |
| 409 | `CONFLICTO_DE_VERSION` | La fila cambió desde que se leyó. |
| 409 | `CATALOGO_REFERENCIADO` | Hay filas que la referencian. |
| 409 | `REGLA_DUPLICADA` | Ya existe esa regla, en un sentido o en el otro. |
| 409 | `CONFLICTO_DE_DATOS` | Un `UNIQUE`, un `CHECK` o una llave foránea de la base. |

**Eventos.** Las escrituras de mercancías, carrocerías y vehículos del catálogo publican un evento para que la proyección de
NEXO no quede vieja (sección 6.2). Categorías, reglas, parámetros y medidas no publican nada. El evento lleva la fila completa,
con la misma forma que devuelve el `GET`: NEXO hace upsert por id, y un payload parcial le dejaría los demás campos vacíos.

Si una proyección queda desalineada, `POST /{catálogo}/republicar-catalogo` reemite el evento de creación para todas las filas
de ese catálogo y responde con la lista. Se puede correr más de una vez, porque el consumidor hace upsert por id, y no forma
parte de ningún flujo automático: se invoca a mano.

DTOs en `api/dtos/catalogo.py` y, los de medidas estándar, en `api/dtos/recomendaciones.py`.

### 3.3 `/api/planes-cargue`: motor 3D

Planes de cargue, sus ítems, la optimización 3D y el catálogo de vehículos del motor 3D. Todas las rutas van bajo el prefijo
`/api/planes-cargue`.

| Método | Ruta | Respuesta | Notas |
|---|---|---|---|
| GET | `/vehiculos` | `VehiculoCubicacionDto[]` | Catálogo del motor 3D. Si falta alguno de los 10 presets, lo siembra y publica `vehiculo.creado` por cada uno. |
| POST | `/vehiculos` | `VehiculoCubicacionDto` | Crea, o actualiza si viene `id`. |
| DELETE | `/vehiculos/{id}` | `204` | `409 VEHICULO_EN_USO` si algún plan lo usa. |
| POST | `/vehiculos/republicar-catalogo` | `VehiculoCubicacionDto[]` | Reemite `vehiculo.creado` para todo el catálogo del motor 3D. Seguro de repetir; se invoca a mano. |
| GET | *(raíz)* | `PlanCargueDto[]` | Planes recientes. |
| POST | *(raíz)* | `PlanCargueDto` | Crea un plan vacío (`CrearPlanRequest`: `nombre`, `vehiculoId`). |
| GET | `/{id}` | `PlanCargueDto` | El plan con sus ítems. |
| GET | `/{id}/flota` | `PlanFlotaItemDto[]` | Los planes de la misma flota. |
| DELETE | `/{id}` | `204` | Borra el plan. |
| POST | `/demo` | `PlanCargueDto` | Plan de demostración. |
| POST | `/{id}/items` | `ItemPlanCargueDto` | Crea, o actualiza si viene `id`. |
| POST | `/{id}/items/importar` | `ItemPlanCargueDto[]` | De 1 a 2.000 ítems. |
| DELETE | `/{id}/items/{itemId}` | `204` | |
| POST | `/{id}/optimizar` | `202 PlanCargueDto` | Asíncrono: marca el plan `procesando`, publica `optimizar` y responde. `404` inmediato si el plan no existe. |
| POST | `/{id}/reoptimizar` | `202 PlanCargueDto` | Igual que `optimizar`, con `respetarBloqueadas = true`. |
| GET | `/{id}/resultado` y `/{id}/exportar` | `ResultadoPlanDto` | El resultado completo. |
| GET | `/{id}/colocaciones`, `/{id}/metricas`, `/{id}/no-cargadas` | Subconjuntos de `ResultadoPlanDto` | `metricas` es `null` si el plan no se ha optimizado. |
| PATCH | `/{id}/colocaciones/{colocacionId}` | `ResultadoPlanDto` | Ajuste manual, síncrono. |
| POST | `/desde-linea/{lineaSolicitudId}` | `202 SolicitudEncoladaDto` | Asíncrono: publica `crear_desde_linea` con el id de la línea como `planId` y `correlationId` (sección 7). |
| GET | `/por-linea/{lineaSolicitudId}` | `PlanFlotaItemDto[]` | La flota armada desde esa línea. Lista vacía mientras se procesa. |

**Síncrono y asíncrono en esta API.** Solo `optimizar`, `reoptimizar` y `desde-linea` publican a la cola y responden `202`; el
resultado se sigue consultando `GET /{id}/resultado` o `GET /por-linea/{lineaSolicitudId}`, porque este servicio no notifica
por HTTP. El resto de rutas escribe directo en la base y responde con el resultado. Estas escrituras directas no publican
eventos (salvo la siembra de presets del `GET /vehiculos`), así que NEXO no se entera de ellas.

**Quién usa esta API.** NEXO no la usa: publica los mismos cambios como comandos de la cola (sección 6.1), y el worker los aplica
con los mismos casos de uso de `application/plan_cargue/`. Esta API la usan las pruebas (`tests/api/test_smoke.py`), el humo de
CI (`scripts/ci_docker_smoke.py`) y la verificación manual. Por la cola, el ajuste de colocaciones también es asíncrono
([D-08](../docs/decisiones/DECISIONES-NEXO.md#d-08)).

DTOs reales (`api/dtos/plan_cargue.py`):

```python
class EjeVehiculoDto(ApiModel):
    posicion_mm: float
    capacidad_kg: float

class VehiculoCubicacionDto(ApiModel):
    id: str
    nombre: str
    descripcion: str | None
    largo_interno_mm: float
    ancho_interno_mm: float
    alto_interno_mm: float
    capacidad_kg: float
    tara_kg: float
    puerta_ancho_mm: float
    puerta_alto_mm: float
    puerta_posicion: str          # trasera | frontal | costado_izquierdo | costado_derecho
    ejes: list[EjeVehiculoDto]
    tolerancia_paredes_mm: float
    tolerancia_entre_unidades_mm: float
    es_preset: bool

class GuardarVehiculoInput(ApiModel):
    id: str | None = None         # None = crear; con id = actualizar
    nombre: str
    descripcion: str | None = None
    largo_interno_mm: float
    ancho_interno_mm: float
    alto_interno_mm: float
    capacidad_kg: float
    tara_kg: float
    puerta_ancho_mm: float
    puerta_alto_mm: float
    puerta_posicion: str
    ejes: list[EjeVehiculoDto]
    tolerancia_paredes_mm: float = 0.0
    tolerancia_entre_unidades_mm: float = 0.0

class ItemPlanCargueDto(ApiModel):
    id: str
    plan_cargue_id: str
    sku: str
    descripcion: str
    pedido: str | None
    cliente: str | None
    destino: str | None
    secuencia_entrega: int
    largo_mm: float
    ancho_mm: float
    alto_mm: float
    peso_kg: float
    cantidad: int
    color: str | None
    prioridad: int
    tipo_embalaje: str | None
    apilable: bool
    max_niveles: int
    max_peso_encima_kg: float     # -1.0 = sin restricción
    rotable: bool
    mantener_vertical: bool
    fragil: bool
    peligroso: bool               # declarado, sin efecto en la optimización
    refrigerado: bool             # declarado, sin efecto en la optimización
    grupo_compatibilidad: str | None
    observaciones: str | None

class GuardarItemPlanCargueInput(ApiModel):
    id: str | None = None
    sku: str = ""                 # acepta null explícito y lo normaliza a "" (ver nota abajo)
    descripcion: str
    pedido: str | None = None
    cliente: str | None = None
    destino: str | None = None
    secuencia_entrega: int = 1
    largo_mm: float
    ancho_mm: float
    alto_mm: float
    peso_kg: float
    cantidad: int = 1
    color: str | None = None
    prioridad: int = 3
    tipo_embalaje: str | None = None
    apilable: bool = True
    max_niveles: int = 0
    max_peso_encima_kg: float = -1.0
    rotable: bool = True
    mantener_vertical: bool = False
    fragil: bool = False
    peligroso: bool = False
    refrigerado: bool = False
    grupo_compatibilidad: str | None = None
    observaciones: str | None = None

class PlanCargueDto(ApiModel):
    id: str
    nombre: str
    vehiculo_id: str
    linea_solicitud_id: str | None
    numero_vehiculo: int
    estado: str                   # borrador | procesando | optimizado | ajustado | error
    estrategia: str               # por defecto "Combinada"
    algoritmo: str                # por defecto "bfd" (o "ffd")
    semilla: int | None
    peso_total_kg: float
    volumen_utilizado_m3: float
    utilizacion_volumen: float
    utilizacion_piso: float
    puntaje: float
    items: list[ItemPlanCargueDto]
    created_at: datetime
    updated_at: datetime

class ColocacionCargueDto(ApiModel):
    id: str
    plan_cargue_id: str
    item_plan_cargue_id: str
    indice_unidad: int
    cargada: bool                 # false = rechazada; ver codigo_rechazo y razon_rechazo
    x: float
    y: float
    z: float
    largo_colocado_mm: float
    ancho_colocado_mm: float
    alto_colocado_mm: float
    orientacion_indice: int
    secuencia_cargue: int
    soporte_colocacion_id: str | None
    porcentaje_soporte: float
    peso_encima_kg: float
    estado_estabilidad: str       # estable | advertencia
    accesibilidad: str
    bloqueada: bool
    ajuste_manual: bool
    codigo_rechazo: str | None    # ver sección 3.4
    razon_rechazo: str | None

class IncidenciaValidacionDto(ApiModel):
    severidad: str
    codigo_regla: str
    titulo: str
    descripcion: str
    accion_sugerida: str | None
    colocacion_item_id: str | None

class MetricasCubicacionDto(ApiModel):
    volumen_interno_m3: float
    volumen_utilizado_m3: float
    porcentaje_volumen: float
    area_piso_m2: float
    area_piso_utilizada_m2: float
    porcentaje_piso: float
    peso_maximo_kg: float
    peso_cargado_kg: float
    porcentaje_peso: float
    unidades_totales: int
    unidades_cargadas: int
    unidades_no_cargadas: int
    pedidos_completos: int
    pedidos_parciales: int
    destinos: int
    altura_maxima_mm: float
    centro_gravedad_mm: list[float]
    peso_por_eje_kg: list[float]
    diferencia_lados_kg: float
    advertencias: int
    violaciones_criticas: int
    puntaje: float
    puntajes_parciales: dict[str, float]
    espacio_vacio_util_m3: float
    espacio_fragmentado_m3: float

class ResultadoPlanDto(ApiModel):
    plan: PlanCargueDto
    vehiculo: VehiculoCubicacionDto
    colocaciones: list[ColocacionCargueDto]
    metricas: MetricasCubicacionDto | None
    incidencias: list[IncidenciaValidacionDto]

class PlanFlotaItemDto(ApiModel):
    id: str
    numero_vehiculo: int
    total_vehiculos: int
    unidades_cargadas: int
    unidades_totales: int

class OptimizarRequestDto(ApiModel):
    estrategia: str = "Combinada"
    algoritmo: str = "bfd"
    semilla: int | None = None
    ponderaciones: PonderacionesCombinadaDto | None = None
    respetar_bloqueadas: bool = False

class PonderacionesCombinadaDto(ApiModel):
    volumen: float = 0.25
    balance_ejes: float = 0.25
    estabilidad: float = 0.20
    secuencia: float = 0.15
    prioridad: float = 0.10
    unidades: float = 0.05

class AjusteColocacionInput(ApiModel):
    x: float | None = None
    y: float | None = None
    z: float | None = None
    orientacion_indice: int | None = None
    bloqueada: bool | None = None
    retirar: bool | None = None
    reinsertar: bool | None = None

class CrearPlanRequest(ApiModel):
    nombre: str
    vehiculo_id: str

class SolicitudEncoladaDto(ApiModel):
    correlation_id: str
    estado: str = "procesando"
```

> **`sku` acepta `null` explícito.** En NEXO, `GuardarItemRequest.Sku` es `string?`: un ítem sin SKU es un caso de negocio
> legítimo, y cuando no lo tiene NEXO manda la clave con `null`, no la omite. En Pydantic el valor por defecto solo aplica si la
> clave falta, así que ese `null` se rechazaba contra un campo `str`: el ítem fallaba tres veces, caía en la cola de error y
> dejaba todo el plan en `error`.
>
> Un `field_validator(mode="before")` lo convierte en `""` antes de validar el tipo. Se normaliza en lugar de declarar
> `str | None` porque la columna `sku` no admite nulos y la entidad también usa `""` como ausencia: aceptar el `null` solo en el
> borde habría movido el fallo a la base, no lo habría quitado.

### 3.4 Catálogo de `codigo_rechazo` en `ColocacionCargueDto`

No son errores HTTP: son motivos de negocio dentro de una respuesta exitosa, cuando una unidad no se pudo cargar.

| Código | Cuándo (`domain/motor/optimizadores.py`, `infrastructure/persistence/repositorios/plan_cargue_repo.py`) |
|---|---|
| `PUERTA` | Ninguna orientación permitida pasa por la puerta del vehículo. |
| `PESO_TOTAL` | Cargarla superaría la capacidad del vehículo. |
| `COMPATIBILIDAD` | Choca con el grupo de compatibilidad de otra unidad ya cargada. |
| `SIN_ESPACIO` | No se encontró posición válida (colisión, soporte, apilamiento o límites) en ninguna orientación. |
| `RETIRADA_MANUAL` | Se retiró con un ajuste manual (`{"retirar": true}`). |

---

## 4. Formato de error (API REST)

Los errores de la aplicación responden:

```json
{ "codigo": "PLAN_NO_ENCONTRADO", "mensaje": "Plan de cargue no encontrado." }
```

Hay dos excepciones de forma, que vienen de FastAPI y no de la aplicación:

- **`401`:** el código va anidado en `detail`: `{ "detail": { "codigo": "NO_AUTORIZADO", "mensaje": "..." } }`.
- **`422`:** un body que no cumple el DTO (falta un campo, un tipo equivocado, un valor fuera de rango) responde con el formato
  de validación de FastAPI, `{ "detail": [ ... ] }`, sin `codigo`.

Status HTTP por tipo de excepción (`api/main.py`, `application/excepciones.py`):

| Origen | Status |
|---|---|
| `ValidationFailedException` | 400 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| Fila cambiada desde que se leyó (`StaleDataError`) | 409 `CONFLICTO_DE_VERSION` |
| Restricción de la base (`IntegrityError`) | 409 `CONFLICTO_DE_DATOS` |
| Cualquier otro fallo de base (`SQLAlchemyError`) | 503 `CUBICADOR_NO_DISPONIBLE`; el detalle va al log, no a la respuesta |
| Llave inválida o ausente (`api/dependencias.py`) | 401 `NO_AUTORIZADO` |

Códigos por área:

| Área | Códigos |
|---|---|
| Recomendaciones (3.1) | `MERCANCIAS_REQUERIDAS`, `MERCANCIA_DESCONOCIDA`, `MERCANCIA_NO_CUBICABLE`, `DIMENSIONES_INVALIDAS`, `CUBICACION_DEMASIADO_GRANDE`, `ORIGEN_SISTEMA_INVALIDO`, `RECOMENDACION_NO_ENCONTRADA` |
| Catálogos (3.2) | Ver la tabla de errores de la sección 3.2. |
| Vehículos del motor 3D | `DIMENSIONES_INVALIDAS`, `CAPACIDAD_INVALIDA`, `PUERTA_INVALIDA`, `PUERTA_POSICION_INVALIDA`, `EJES_INVALIDOS`, `TOLERANCIA_INVALIDA`, `VEHICULO_EN_USO` |
| Planes e ítems | `PLAN_NO_ENCONTRADO`, `VEHICULO_INVALIDO`, `VEHICULO_NO_ENCONTRADO` (el vehículo del plan ya no existe), `DIMENSIONES_INVALIDAS`, `PESO_INVALIDO`, `CANTIDAD_INVALIDA`, `IMPORTACION_INVALIDA` |
| Optimización | `ESTRATEGIA_INVALIDA`, `ALGORITMO_INVALIDO`, `PLAN_DEMASIADO_GRANDE` |
| Ajuste manual | `COLOCACION_NO_ENCONTRADA`, `FUERA_DE_LIMITES`, `COLISION`, `SOPORTE_INSUFICIENTE`, `APILAMIENTO`, `ORIENTACION_NO_PERMITIDA` |
| Comunes | `NO_AUTORIZADO`, `CONFLICTO_DE_VERSION`, `CONFLICTO_DE_DATOS`, `CUBICADOR_NO_DISPONIBLE` |

Los códigos del plan desde una línea (sección 7) y los propios de la mensajería (`ERROR_INTERNO`, `TIMEOUT_PROCESAMIENTO`) solo
llegan en el evento `cubicacion.rechazada`: ninguna ruta REST los devuelve. `SIN_ESPACIO` y `RETIRADA_MANUAL` tampoco son
errores HTTP: son valores de `codigo_rechazo` (sección 3.4).

---

## 5. Autenticación

Todas las llamadas, en los dos sentidos, llevan:

```
Authorization: Bearer <llave>
```

| Llave (variable del Cubicador) | Sirve para | Quién la usa |
|---|---|---|
| `CUBICADOR_API_KEY_NEXO` | `/recomendaciones` y todo lo demás | NEXO, guardada como `Cubicador3D:RecomendacionesApiKey`. |
| `CUBICADOR_API_KEY_COTIFLY` | `/recomendaciones` y todo lo demás | Cotifly. La genera Cotifly y tiene que ser distinta de la de NEXO. |
| `CUBICADOR_API_KEY` | Todo menos `/recomendaciones` | NEXO para el reenvío de los catálogos, guardada como `Cubicador3D:OutboundApiKey`; también el humo de CI y los scripts de verificación. |

- **Una llave por sistema para `/recomendaciones`** (`api/dependencias.py`, `verificar_origen_sistema`). De la llave sale el
  `origenSistema` verificado, en vez de creerle al cuerpo de la petición: quien tiene la llave de Cotifly es Cotifly. Si el cuerpo
  declara un `origenSistema` que no corresponde a la llave, `401`.
- **La llave genérica no sirve para `/recomendaciones`**: identifica al servicio, no a un sistema, y una recomendación tiene que
  saber de quién viene para poder reconsultarla.
- **El resto del servicio** (`/api/planes-cargue`, catálogos, medidas estándar) acepta cualquiera de las tres
  (`verificar_api_key`).
- Las llaves se leen del entorno en cada llamada: rotarlas no exige reiniciar el proceso.
- **Este servicio → NEXO** (sección 7): el Cubicador manda `NEXO_CALLBACK_API_KEY`, y NEXO la valida contra
  `Cubicador3D:InboundApiKey` (`clsCubicadorApiKeyAuthenticationHandler`, esquema `CubicadorApiKey`, aparte de la sesión del
  portal). Las dos tienen que ser idénticas.

No hay identidad de usuario ni roles en ningún sentido: son secretos de servicio, no un inicio de sesión. Los pares de llaves
por ambiente están en [`CHECKLIST_DESPLIEGUE.md`](../docs/despliegue/CHECKLIST_DESPLIEGUE.md), sección 2.

---

## 6. Mensajería (RabbitMQ)

### 6.1 Comando entrante: `cubicacion.solicitada`

Lo publica NEXO con MassTransit, envuelto en su sobre (`messageType` y `message`), y también esta misma API en `optimizar`,
`reoptimizar` y `desde-linea`, sin sobre. El worker acepta las dos formas: si el JSON trae `message` y `messageType`, lo
desenvuelve; si no, lo usa tal cual (`worker/main.py`, `_procesar`).

```json
{
  "tipo": "<uno de los 11 tipos>",
  "planId": "<guid>",
  "correlationId": "<guid>",
  "solicitud": { }
}
```

| `tipo` | `planId` lleva | Qué hace el worker |
|---|---|---|
| `crear_plan` | El id que genera NEXO | Crea el plan con ese id. |
| `crear_plan_demo` | Un id de correlación | Crea un plan de demostración con id propio. |
| `crear_desde_linea` | Un id de correlación | Arma la flota de una línea (sección 7). |
| `optimizar` | El plan | Optimiza con la estrategia pedida. |
| `guardar_item` | El plan | Crea o actualiza un ítem. |
| `importar_items` | El plan | Importa de 1 a 2.000 ítems. |
| `eliminar_item` | El plan | Borra un ítem. |
| `eliminar_plan` | El plan | Borra el plan. |
| `ajustar_colocacion` | El plan | Mueve, rota, bloquea, retira o reinserta una colocación. |
| `guardar_vehiculo` | El id del vehículo | Crea o actualiza un vehículo del motor 3D. |
| `eliminar_vehiculo` | El id del vehículo | Borra un vehículo del motor 3D, o rechaza con `VEHICULO_EN_USO`. |

Son los mismos 11 tipos que NEXO admite en `clsCubicacionSolicitadaValidador` (`TiposConocidos`); el test
`clsContratoMessageTypeTests` falla si NEXO puede publicar uno que el worker no despacha. Los payloads de `solicitud` por tipo
están en [`CUBICACION.md`](../docs/mensajeria/CUBICACION.md), sección 3, y corresponden a los DTOs de la sección 3.3 de este
documento. Solo `optimizar` pasa por la compuerta de paridad de NEXO; los demás son operaciones de datos.

**Cómo se procesa:**

- **Éxito:** el worker confirma el mensaje y, salvo para `guardar_vehiculo`, `eliminar_vehiculo` y `eliminar_plan`, publica
  `plan-de-cargue.generado` con el resultado del plan.
- **Error de negocio** (`AppException`): no se reintenta, porque el resultado sería el mismo. Marca el plan en `error` si
  `planId` corresponde a uno y publica `cubicacion.rechazada` con el código y el mensaje del caso de uso.
- **Fallo inesperado:** se vuelve a publicar con la cabecera `x-intentos` incrementada, hasta tres intentos en total
  (`MAX_INTENTOS = 3`). En el tercero, el mensaje va a `cubicacion.dlq`, se alerta por correo, el plan queda en `error` y se
  publica `cubicacion.rechazada` con `ERROR_INTERNO` y un mensaje genérico para el usuario; el detalle técnico queda en el log
  con el `correlationId`.
- **Mensaje ilegible** (JSON inválido, un cuerpo que no es objeto, una cabecera corrupta): va directo a `cubicacion.dlq`, con su
  alerta, sin reintentos ni rechazo.

En todos los casos el mensaje se confirma, incluso si falla el reporte del error: con `prefetch_count = 2`, dos mensajes sin
confirmar bastarían para detener el worker.

### 6.2 Eventos de salida

| Routing key | Mensaje (`messageType`) | Payload | Cuándo |
|---|---|---|---|
| `plan-de-cargue.generado` | `PlanDeCargueGenerado` | `{ correlationId, planId, resultado }` | Al terminar bien un comando que deja un plan que reportar. |
| `cubicacion.rechazada` | `CubicacionRechazada` | `{ correlationId, planId, codigo, mensaje }` | Al fallar cualquiera de los 11 tipos, y desde el vigilante (6.4). |
| `vehiculo.creado` / `vehiculo.actualizado` | `VehiculoCreado` / `VehiculoActualizado` | `{ correlationId, vehiculo }` | `guardar_vehiculo`, la siembra de presets, `POST /api/planes-cargue/vehiculos/republicar-catalogo` y las escrituras de `/vehiculos-catalogo` (ver la advertencia de abajo). |
| `vehiculo.eliminado` | `VehiculoEliminado` | `{ correlationId, vehiculoId }` | `eliminar_vehiculo`, solo si el borrado se aplicó; también el `DELETE /vehiculos-catalogo/{id}`. |
| `mercancia.creada` / `.actualizada` | `MercanciaCreada` / `MercanciaActualizada` | `{ correlationId, mercancia }` | Escrituras de `/tipos-mercancia` (también su borrado lógico) y su `republicar-catalogo`. |
| `mercancia.eliminada` | `MercanciaEliminada` | `{ correlationId, mercanciaId }` | Borrado físico de una mercancía. |
| `carroceria.creada` / `.actualizada` | `CarroceriaCreada` / `CarroceriaActualizada` | `{ correlationId, carroceria }` | Escrituras de `/tipos-carroceria` y su `republicar-catalogo`. |
| `carroceria.eliminada` | `CarroceriaEliminada` | `{ correlationId, carroceriaId }` | Borrado de una carrocería. |

- **`plan-de-cargue.generado`** lleva en `resultado` el `ResultadoPlanDto` completo: ítems, colocaciones con su geometría,
  métricas e incidencias ([D-09](../docs/decisiones/DECISIONES-NEXO.md#d-09)). `correlationId` y `planId` llevan el id del plan
  resultante, no el `correlationId` del comando. El ajuste manual no tiene evento propio: reporta su desenlace con este mismo.
- **`cubicacion.rechazada`** lleva en `correlationId` y en `planId` el `correlationId` del comando que falló.
- **Eventos de catálogo:** `correlationId` es el id de la fila. Los de creación y actualización llevan la fila completa, con la
  forma del DTO de lectura (`VehiculoCubicacionDto`, `TipoMercanciaDto`, `TipoCarroceriaDto`); los de borrado, solo el id.

> **Advertencia: los vehículos del catálogo comparten routing key con los del motor 3D.** Las escrituras de
> `/vehiculos-catalogo` publican por `vehiculo.creado`, `vehiculo.actualizado` y `vehiculo.eliminado`, con la forma de
> `VehiculoCatalogoDto`. En NEXO, el único consumidor de esas routing keys (`clsVehiculoCatalogoConsumer`) espera un vehículo
> del motor 3D y lo proyecta en su tabla de vehículos del motor 3D: un vehículo del catálogo llega sin los campos que esa tabla
> necesita. NEXO no tiene proyección del catálogo de vehículos de la selección de flota.

**El sobre de MassTransit.** Todos los eventos salen envueltos para que NEXO los consuma con un `IConsumer<T>` normal
(`_sobre_masstransit` en `infrastructure/mensajeria/rabbitmq.py`):

```json
{
  "messageId": "uuid",
  "messageType": ["urn:message:Nexo.Mensajes.Cubicacion:PlanDeCargueGenerado"],
  "message": { "correlationId": "uuid", "planId": "uuid", "resultado": { } },
  "sentTime": "2026-08-27T21:00:00.000000+00:00"
}
```

El `messageType` tiene que coincidir exactamente con el namespace y el nombre de la clase C# del otro lado. Si NEXO renombra
una de esas clases, este valor queda desincronizado sin ningún error de compilación: solo se nota porque el mensaje deja de
entregarse. Lo protegen `tests/infrastructure/test_eventos_catalogo.py` de este lado y `clsContratoCubicacionTests` y
`clsContratoMessageTypeTests` del lado de NEXO ([`CUBICACION.md`](../docs/mensajeria/CUBICACION.md), sección 5).

### 6.3 Exchange y colas

Verificado contra `infrastructure/mensajeria/rabbitmq.py` y `worker/main.py`:

- **Exchange:** `cubicador.eventos`, de tipo topic y durable. Es el mismo para el comando y para todos los eventos.
- **Cola del worker:** `cubicador.optimizar`, durable, ligada a `cubicacion.solicitada`. El nombre es histórico: recibe los 11
  tipos. Los reintentos se vuelven a publicar en esa misma routing key.
- **Cola de error:** `cubicacion.dlq`, ligada a la routing key `cubicacion.dlq`. Nadie la consume automáticamente: se revisa a
  mano. Cada mensaje que entra dispara un correo al equipo técnico (`infrastructure/alertas/correo.py`, configurable con
  `ALERTAS_SMTP_*` y `ALERTAS_EQUIPO_TECNICO`); sin SMTP configurado, la alerta queda solo en el log y el worker sigue.
- **Colas de NEXO:** NEXO declara una cola por evento, con prefijo `Cubicacion.`
  ([`CUBICACION.md`](../docs/mensajeria/CUBICACION.md), sección 2).

### 6.4 Vigilante de timeouts

Cubre lo que los reintentos no cubren: que el worker se caiga a mitad de un mensaje, sin llegar a fallar ni a terminar. No hay
excepción que atrapar ni confirmación que emitir, así que el plan quedaría en `procesando` para siempre.

Cada `CUBICADOR_INTERVALO_VIGILANTE_SEGUNDOS` (60 por defecto), el worker busca los planes en `procesando` cuyo `updated_at`
supere `CUBICADOR_TIMEOUT_PROCESANDO_MINUTOS` (5 por defecto) y, por cada uno:

1. Publica `cubicacion.rechazada` con `codigo: "TIMEOUT_PROCESAMIENTO"`.
2. Después marca el plan en `error`.

El orden es deliberado. Si se marcara primero y el broker estuviera caído, el plan saldría de `procesando` sin que nadie se
enterara, y el ciclo siguiente ya no lo encontraría. Publicando primero, un fallo del broker deja el plan intacto y el próximo
ciclo lo reintenta. Es el mismo criterio que el Outbox de NEXO. El costo es un posible rechazo duplicado si el proceso muere
entre los dos pasos, que es inofensivo: el consumidor de NEXO solo vuelve a poner el plan en `error`.

**Alcance.** El vigilante solo ve los planes que la base del Cubicador tiene en `procesando`, y el único camino que pone ese
estado es `POST /api/planes-cargue/{id}/optimizar` de esta API. Un `optimizar` publicado por NEXO no cambia el estado del plan
en esta base antes de procesarlo, así que el vigilante no lo alcanza. `crear_desde_linea` tampoco: sus planes nacen en
`borrador`.

---

## 7. Lo que este servicio consume de NEXO

```
GET {NEXO_BASE_URL}/api/lineas-solicitud/{id}/cubicacion
Authorization: Bearer <NEXO_CALLBACK_API_KEY>

200 → { "lineaId": "guid",
        "posiciones": [ { "descripcion"?, "cantidad", "largoCm", "anchoCm", "altoCm", "pesoKg"?, "codigoTipoMercancia"? } ],
        "origen"?, "destino"? }
404 → la línea no existe (el cliente solo mira el status, no lee el cuerpo)
```

Lo usa el worker al procesar `crear_desde_linea`. El cliente es `infrastructure/clientes/nexo_client.py`: timeout fijo de 10
segundos y sin reintentos propios. Un `404` se rechaza con `LINEA_NO_ENCONTRADA`; cualquier otro status de error es un fallo
inesperado y sigue el camino de reintentos de la sección 6.1.

**Del lado de NEXO** lo atienden `clsLineasSolicitudCubicacionController`, `clsObtenerCubicacionLineaQuery` y
`clsLineaSolicitudCubicacionAdapter`, con la autenticación de la sección 5. El endpoint lee la línea con su ruta y devuelve las
posiciones ya capturadas en `LineaSolicitud.Dimensiones`. `codigoTipoMercancia` es el mismo `codigoMercancia` de
`GET /tipos-mercancia`. Fijan la forma del cuerpo `tests/infrastructure/test_nexo_client.py` de este lado y
`clsLineasSolicitudCubicacionControllerTests` del lado de NEXO.

**Cómo se arma la flota** (`application/plan_cargue/desde_linea.py`):

1. Valida las posiciones: sin posiciones, `SIN_POSICIONES`; una posición sin tipo de mercancía, `MERCANCIA_NO_DECLARADA`; sin
   peso, `PESO_NO_DECLARADO`. No se asume ningún valor por defecto.
2. Pide la flota al motor de selección, el mismo de `/recomendaciones`. Si no asigna vehículos, `SIN_FLOTA_RECOMENDADA`.
3. Traduce cada vehículo recomendado a un preset del motor 3D con un diccionario de compatibilidad, que es deuda declarada
   ([`docs/DEUDA_DIFERIDA_3D.md`](./docs/DEUDA_DIFERIDA_3D.md)). Si falta la equivalencia, `VEHICULO_SIN_PRESET`; si el preset
   no existe en el catálogo del motor 3D, `PRESET_VEHICULO_NO_ENCONTRADO`. Todo se traduce antes de crear nada.
4. Borra los planes que ya tuviera esa línea, crea un plan por vehículo y optimiza cada uno; lo que no cabe en un vehículo pasa
   al siguiente. Las medidas se convierten de centímetros a milímetros.
5. Si al final queda carga sin ubicar, rechaza con `FLOTA_INSUFICIENTE`: los dos motores no coincidieron.

El worker publica `plan-de-cargue.generado` solo del primer plan de la flota
([`CUBICACION.md`](../docs/mensajeria/CUBICACION.md), sección 9). La flota completa se consulta con
`GET /api/planes-cargue/por-linea/{lineaSolicitudId}`.

**Al combinar los dos stacks de Docker:** NEXO y el Cubicador tienen cada uno un servicio llamado `api` en su
`docker-compose.yml`. Si el worker del Cubicador queda conectado a las dos redes, `NEXO_BASE_URL=http://api:8080` puede resolver
al `api` del propio Cubicador. Usar el nombre de contenedor explícito (por ejemplo, `http://nexo-api-1:8080`) evita la
ambigüedad.

---

## 8. Límites y comportamiento a tener en cuenta

- Ítem del motor 3D: cada lado mayor que 0 y hasta 30.000 mm, peso mayor que 0 y hasta 100.000 kg, cantidad de 1 a 2.000
  (`application/plan_cargue/items.py`).
- Importación masiva: de 1 a 2.000 ítems por llamada.
- Optimización: máximo 8.000 unidades expandidas por plan (`PLAN_DEMASIADO_GRANDE`, `application/plan_cargue/optimizar.py`).
  No hay un timeout sobre el algoritmo en sí; si el worker se cae, actúa el vigilante (sección 6.4).
- `/recomendaciones`: máximo 200 posiciones y 10.000 unidades (sección 3.1).
- En esta API solo `optimizar`, `reoptimizar` y `desde-linea` son asíncronos; el resto, incluido el ajuste manual, es síncrono.
  Por la cola de NEXO todo es asíncrono.
- El arranque no valida la configuración. Sin `DATABASE_URL`, el proceso arranca contra un SQLite local que se pierde en cada
  despliegue. Sin `CUBICADOR_RABBITMQ_URL`, intenta `amqp://guest:guest@localhost:5672/` y, tras 30 intentos de 2 segundos, no
  arranca. Después de cada despliegue se verifica `/health` y el log.
