# Milestones e Issues — Reestructuración del Cubicador Rápido · Sprint 5

**Fecha de redacción:** 2026-10-08. Borrador listo para copiar y pegar en GitHub.

Fuente: [`HOJA-DE-RUTA-CUBICADOR.md`](./HOJA-DE-RUTA-CUBICADOR.md), que contiene el
contrato completo, las decisiones cerradas y el detalle técnico que estas issues citan.

## Cómo usar este documento

1. Cada bloque `## S5-HU-NN` es una **milestone nueva**: copiar el cuerpo completo como
   descripción, con vencimiento el **16 de octubre de 2026**.
2. Cada `### [S5-HU-NN.X]` es un **issue nuevo**, asociado a esa milestone. El título es la
   línea del encabezado sin el `###`; labels, repo, asignado, Type, Priority y Effort ya
   están abajo de cada uno.
3. **Numeración:** la última HU creada fue S5-HU-16 (issues #254, #255, #256). Estas son
   las tres siguientes. Los `#NNN` de GitHub son consecutivos automáticos.
4. **Milestones:** la última existente es S4-HU-15 (#38) — **no hay ninguna de Sprint 5
   todavía**, así que estas serían #39, #40 y #41.
5. **Repos:** `NEXO` para S5-HU-17 y S5-HU-18 (el cubicador es la carpeta `cubicador/` de
   ese mismo repo), y `administracion` para S5-HU-19 (el portal; antes
   `administracion_embarqa_nexo`).
6. **No se crea ningún issue en `Cotifly`.** Lo que se les pide está en la sección 2 de la
   hoja de ruta y ellos escriben sus propias HU a partir de ahí — el mismo patrón que
   usaron con nosotros en su HU-021.
7. `HU completa en Notion` queda con placeholder: crear la HU y pegar el link real.
8. **Estilo:** los cuerpos de milestone van sin tildes, como las doce milestones más
   recientes (S4-HU-04 a S4-HU-15); los cuerpos de issue van con tildes, como #256 y #263.
9. **S4-HU-12 y su issue #218 no se tocan.** Se cierran aparte.

## Reparto

| Rol | Quién | Dónde |
|---|---|---|
| Automatización/IA | Brayan Quintero (@bquinterobtst) | Cubicador: motor, contrato, CRUD, limpieza |
| Datos | Sara Calderón (@scalderon-bs) | Cubicador: migración, modelo y seeders |
| Backend | Ismael Vasco (@ivasco-dev) | NEXO |
| Frontend | Juan Pablo Vélez (@jpvelez-bts) | Portal |
| QA | Kevin Cano (@kcano-ux) | Una issue por HU |

**Cada HU cierra con una issue de QA.** La evidencia se documenta en `tests/QA/` del repo
`NEXO`, una carpeta por HU con su README, igual que `s4-hu-07-matching-sicetac-oficial`.
Lo último registrado ahí es `s4-hu-07`, así que las tres carpetas nuevas se crean desde
cero.

## Orden y dependencias

| Fase | Issues | Se puede hacer sin el contrato |
|---|---|---|
| 1. Base de datos | 17.1, 17.2 | Sí |
| 2. Motor | 17.3 | Sí |
| 3. Contrato y CRUD | 17.4, 17.5, 17.6 | Sí |
| 4. Consumidores | 18.1 a 18.6, 19.1 a 19.4 | **Parcial**: se programa contra la forma de la sección 2, pero no se prueba de punta a punta hasta que 17.4 exista |
| 5. QA | 17.7, 18.7, 19.5 | No |

---

## S5-HU-17 — Reestructuracion del cubicador rapido: catalogo en base de datos, motor por peso y volumen, y contrato unico

> **Repo de la milestone:** `NEXO` · **Vence:** 16 de octubre de 2026

```
RESPONSABLE HU: Brayan Quintero (@bquinterobtst), Sara Calderon (@scalderon-bs)
[Sprint 5 · Nexo]
Como Comercial o Administrador de Fletes quiero que el cubicador recomiende la flota real que necesita una carga — cuantos camiones y de que tipo, mezclando configuraciones distintas si hace falta — a partir de un catalogo de vehiculos y mercancias administrable en base de datos, para dejar de depender de 9 vehiculos quemados en codigo con medidas que no corresponden a la flota real.

CRITERIOS DE ACEPTACION (resumen Gherkin):

- El catalogo de vehiculos, carrocerias, categorias y tipos de mercancia vive en SQL Server y se administra por CRUD, no quemado en codigo.
- La recomendacion filtra por peso y decide por el mayor entre el porcentaje de llenado de volumen y el de peso, y devuelve los dos.
- Una carga heterogenea puede devolver una flota mixta (por ejemplo un Sencillo y una Minimula), no N vehiculos iguales.
- Un tipo de mercancia desconocido o marcado como no cubicable se rechaza con motivo explicito; nunca se asume un vehiculo por defecto.
- "Sin seleccion automatica", "condicionada" y "extradimensionado" vuelven como resultado marcado con HTTP 200, no como codigo de error.
- El mismo contrato sirve a Nexo y a Cotifly, cada uno con su propia llave, y el origen del sistema se verifica contra la llave en vez de aceptarse autodeclarado.
- Las recomendaciones se persisten sin actualizarse nunca: recalcular una ruta crea una corrida nueva, para que una cotizacion guardada siga diciendo lo mismo dentro de dos años.
- El motor 3D no se toca en esta HU: sus 26 casos dorados siguen en verde.

HABILITADOR: hoja de ruta acordada con el equipo y revisada por Cotifly (cubicador/docs/HOJA-DE-RUTA-CUBICADOR.md, 2026-10-08), que contiene el contrato completo y las decisiones cerradas. Dato de vehiculos tomado del Excel "Dimenciones y Peso por configuracion para Cubicaje" aportado por Operaciones. Supera el planteamiento de S4-HU-12, que recomendaba solo por peso.
ROLES QUE INTERVIENEN: Automatizacion/IA, Datos, QA
ALCANCE EN ESTE REPO: todo en cubicador/ — motor de base, modelo, seeders, motor de seleccion, contrato, CRUD y limpieza del puente con Solicitud/Cotizacion.
HU GEMELA EN: S5-HU-18 (backend de Nexo) y S5-HU-19 (portal, repo administracion)
HU completa en Notion: (pendiente crear y pegar el link)
Las tareas tecnicas de esta HU se crean como ISSUES asociadas a este milestone.
```

### Issues en `NEXO`

---

### [S5-HU-17.1] Migrar el motor de base de Cubicador de PostgreSQL a SQL Server 2019, con Alembic

**Labels:** `nexo`, `datos`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `NEXO`
**Asignado a:** Sara Calderón (@scalderon-bs)
**Type:** `Task` · **Priority:** High · **Effort:** High
**Parte de S5-HU-17 (milestone). Va primero, antes de crear nada nuevo.** Estimado: 1 a 2 días.

Hoy `cubicador/` corre sobre PostgreSQL 16 (`docker-compose.yml`, servicio `db`, con healthcheck `pg_isready`) y el resto de NEXO sobre SQL Server. La reestructuración agrega diez tablas nuevas, así que conviene migrar el motor **antes** de crearlas: migrar primero significa probar una sola vez, y si el driver asíncrono se comporta distinto se descubre con las cinco tablas que ya existen y no a mitad del trabajo nuevo.

**Por qué Alembic y no `create_all`.** Hoy el esquema lo crea `Base.metadata.create_all`, que **solo crea tablas que no existen y nunca hace `ALTER`**: cualquier columna agregada al modelo después del primer arranque se ignora en silencio. La rama `S4-HU-12` dejó un paliativo (`_agregar_lo_que_falte` en `infrastructure/persistence/db.py`) que agrega columnas nullable al arrancar, y su propio comentario anticipa el límite: *"el día que haga falta renombrar, borrar o cambiar el tipo de una columna, esto no alcanza y hay que traerlo"*. Ese día es este — y hay un caso concreto inmediato: el token de concurrencia de 17.5 es `NOT NULL`, así que el paliativo lo saltaría con un warning en vez de agregarlo.

Alembic **no introduce una segunda descripción del esquema**: `alembic revision --autogenerate` compara los modelos contra la base y escribe el delta solo. Los modelos siguen siendo la única fuente de verdad.

**Criterios de aceptación:**

- El dialecto pasa a `mssql+aioodbc` y la imagen de Docker incluye el driver ODBC.
- Los tipos específicos de PostgreSQL se reemplazan por neutrales (`sqlalchemy.Uuid` en vez del UUID nativo).
- El manejo de excepciones del driver se adapta: hoy `plan_cargue_repo.py` distingue `ForeignKeyViolationError` de PostgreSQL de forma explícita.
- `docker-compose.yml` levanta `mcr.microsoft.com/mssql/server:2019-latest` con su propio healthcheck, no `pg_isready`.
- Alembic reemplaza a `create_all` y al paliativo `_agregar_lo_que_falte`, con la migración inicial generada por `--autogenerate` desde los modelos actuales.
- La batería completa de `cubicador/docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md` pasa contra SQL Server 2019 real, no contra un sustituto.
- Las 122 pruebas de `cubicador/` siguen en verde, incluidos los 26 casos dorados de paridad.

**Dependencia de ejecución:** credenciales del SQL Server de QA, **con un usuario de permisos DDL** — Alembic crea y altera tablas, un usuario de solo datos no alcanza. Confirmar también que la instancia es alcanzable desde las máquinas de desarrollo y desde CI.

---

### [S5-HU-17.2] Modelo de datos del catalogo y seeders idempotentes

**Labels:** `nexo`, `datos`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `NEXO`
**Asignado a:** Sara Calderón (@scalderon-bs)
**Type:** `Task` · **Priority:** High · **Effort:** High
**Parte de S5-HU-17 (milestone). Depende de 17.1.** Estimado: 1 día.

Crear las diez tablas que sostienen el catálogo y la persistencia de recomendaciones: vehículos, la relación vehículo-carrocería, tipos de carrocería, categorías de mercancía, tipos de mercancía, reglas de incompatibilidad, parámetros del motor, la tabla padre de recomendaciones, sus vehículos y sus mercancías.

**Decisiones de modelo ya cerradas** (detalle en la sección 4.1 de la hoja de ruta):

- Todo en **centímetros**. `DECIMAL(18,3)` para medidas, `DECIMAL(18,2)` para kilos. Los valores del Excel quedan enteros exactos y `parametros_cubicacion` ya estaba en cm.
- El **alto útil vive en la relación vehículo-carrocería**, no en el vehículo: el mismo chasis tiene alturas distintas según la carrocería. Único override de capacidad: `3S3 + FURGON = 30.000 kg`.
- `UNIQUEIDENTIFIER` con `NEWSEQUENTIALID()`, PK no clustered. Nombres `snake_case` sin tildes, collation `Modern_Spanish_CI_AI` en texto.
- **Las tres tablas de recomendación son append-only**: nunca se actualizan ni se borran, y por eso no llevan `UpdatedAt`.
- El Cubicador es **dueño de los códigos canónicos de vehículo** (`2L2`, `C2`, `C3`, `2S2`, `3S3`), que son los del Excel y los que entienden SICE-TAC y el tarifario. `UNIQUE` sobre el código.

**Sobre los seeders.** Son **idempotentes por código estable**: identifican por código, insertan solo lo que falta y nunca sobrescriben. Consecuencia a tener presente: si alguien **corrige** una fila en el Excel, el seeder **no** la propaga — la fila ya existe y la salta. Las correcciones van por CRUD; solo las filas nuevas entran por seeder.

**Criterios de aceptación:**

- Las diez tablas existen con su migración de Alembic, y las tres de recomendación sin `UpdatedAt`.
- Los seeders cargan los siete CSV de `cubicador/docs/seeder/` y correrlos dos veces no duplica ni sobrescribe nada.
- `CAMA_BAJA` y `PORTACONTENEDORES` quedan sembradas como carrocerías **sin ningún vehículo asignado**: con cuál emparejan lo decide el Administrador de Fletes desde el CRUD. Las relaciones vehículo-carrocería se limitan estrictamente a lo que sale del Excel.
- La categoría `COMBUSTIBLE` queda renombrada a `CARBURANTE` con sus tipos en `Cubicable = false`. Hoy ese nombre significa dos cosas distintas: en `cubicacion_rapida.py` quiere decir *material inflamable* (papel, cartón, madera, textil) y en el CSV *carburante*, que la hoja "Alcance y exclusiones" excluye del cubicador.
- Están sembrados los diez tipos excluidos con `Cubicable = false` — graneles sólido y líquido, combustibles, químicos a granel, cemento, concreto, residuos, asfalto, vehículos como unidad e ingeniería especial. Son los que hacen que quien capture "cemento a granel" reciba un rechazo con motivo en vez de una recomendación inventada.
- `apilable`, `fragil` y `rotacion_libre` quedan llenos donde la matriz da señal y vacíos donde no. Vacío significa "no declarado" y el motor se comporta como hoy.
- Las filas de `vehiculos_cubicacion` que ya existen se corrigen con `cubicador/docs/seeder/corregir_vehiculos_existentes.sql`, como paso de datos de la migración, **después de crear el esquema y antes del primer arranque**. Hace falta porque `listar_con_siembra` siembra por nombre y nunca actualiza una fila: sin ese paso, las medidas inventadas viajan a SQL Server.

---

### [S5-HU-17.3] Motor de seleccion de flota: filtro por peso, decision por volumen y peso, y flota mixta

**Labels:** `nexo`, `backend`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `NEXO`
**Asignado a:** Brayan Quintero (@bquinterobtst)
**Type:** `Task` · **Priority:** High · **Effort:** High
**Parte de S5-HU-17 (milestone). Depende de 17.2.** Estimado: 3 a 5 días. **Es la pieza grande y la de más incertidumbre.**

Reemplazar la heurística actual de `domain/motor/cubicacion_rapida.py`, que compara contra nueve arquetipos quemados y decide por volumen puro, por un motor que lee el catálogo de base y resuelve en tres partes: filtrar candidatos por carrocería compatible y dimensiones, construir el llenado con **DBLF** (Deepest Bottom-Left-Fill) como oráculo de empaquetado, y elegir la flota mínima.

**Decisiones de algoritmo ya cerradas:**

- Se **filtra por peso** pero se **compara por `max(%volumen, %peso)`**, y se reportan los dos. Hoy solo existe `factorUtilizacion`, volumétrico puro.
- `k_max = 10`, con medición del costo para revisarlo después.
- La **dominancia se valida por carrocería** y falla ruidosamente: si alguien agrega un vehículo que la rompe, el algoritmo devolvería un mínimo equivocado en silencio.
- La clase de viaje es **derivada** de la carrocería (`CONTENEDOR` → Contenedor, extradimensión detectada → Extradimensionado, resto → Carga Suelta), sin columna nueva. La `claseViajeSeleccionada` que llega es **preferencia, no filtro**: el motor corre completo y si no coincide lo dice en `advertencias[]`.
- **Fuera del alcance:** distribución por eje y dimensiones de puerta. No hay datos para ninguna de las dos. La posición de la puerta se conserva.
- Refrigerado es `FURGON` más alerta, no una carrocería propia — resuelve seis de las siete mercancías que hoy caen en "sin selección". El espacio interior no se modela aparte: se usa la medida por defecto del vehículo, y el porcentaje queda optimista en las mercancías de frío.

**Criterios de aceptación:**

- El motor lee vehículos, carrocerías y tipos de mercancía de base, sin ningún catálogo quemado.
- Una carga heterogénea devuelve una flota mixta cuando esa es la mejor respuesta.
- Se reportan `porcentajeLlenadoVolumen` y `porcentajeLlenadoPeso` por vehículo, y la decisión usa el mayor de los dos.
- La validación de dominancia por carrocería falla con error explícito, no en silencio.
- Una mercancía condicionada produce una recomendación normal **marcada** como condicionada; no se bloquea ni se espera ninguna aprobación.
- Validación contra **50 a 100 despachos históricos ya ejecutados** (solicitudes entregadas con posiciones capturadas): se mide si el motor pide vehículos de más. Ese es el único error que importa cuando el objetivo es reducir la flota, y si infla, se documenta y se decide si vale invertir en un constructor mejor.
- Si Maximal Empty Spaces resulta necesario sobre DBLF, se justifica con la medición y se documenta; si no, se deja constancia de que DBLF alcanzó.

---

### [S5-HU-17.4] Contrato unico: POST /recomendaciones, catalogos, eventos y llave por sistema

**Labels:** `nexo`, `backend`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `NEXO`
**Asignado a:** Brayan Quintero (@bquinterobtst)
**Type:** `Task` · **Priority:** High · **Effort:** Medium
**Parte de S5-HU-17 (milestone). Depende de 17.3. Bloquea a S5-HU-18 y S5-HU-19.** Estimado: 1 a 2 días.

Exponer el contrato que consumen NEXO y Cotifly. **La especificación completa — request, las tres formas de respuesta, la tabla de campos obligatorios, los códigos de error y los endpoints de catálogo — está en la sección 2 de la hoja de ruta.** Esta issue la implementa tal cual.

**Lo que no se puede perder de vista:**

- `POST /recomendaciones` es **ruta nueva** y `POST /api/cubicacion` **se retira en el mismo corte**. No queda endpoint de transición.
- `SIN_SELECCION_AUTOMATICA`, `condicionada` y `extradimension` vuelven con **HTTP 200** dentro de `resultado`. Los códigos de error quedan solo para fallos reales: mercancía desconocida, no cubicable, dimensiones inválidas, no autorizado y servicio no disponible.
- En un `ASIGNADO`, cada vehículo trae **siempre y sin `null`**: `idRecomendacionVehiculo`, `codigoVehiculo`, `codigoCarroceria`, `pesoTotalKg`, `volumenTotalM3` y los dos porcentajes. El peso en kilos importa especialmente porque **es el que Cotifly le manda a NEXO al crear la solicitud** — no alcanza con el porcentaje.
- El request lleva **`idTipoMercancia`**, el id que el cubicador devolvió en `GET /tipos-mercancia`. Acordado con Cotifly el 2026-10-08 (su HU-021.5). El `GET` devuelve id **y** código; el código estable se usa del lado de NEXO (S5-HU-18.2), no en el cable.
- `verificar_api_key` pasa de comparar contra una sola llave a un **mapa `llave → sistema`**, y de ahí sale el `origenSistema` **verificado**. Si el `origenSistema` del cuerpo no corresponde a la llave, responde 401.

**Criterios de aceptación:**

- `POST /recomendaciones`, `GET /tipos-mercancia`, `GET /tipos-carroceria`, `GET /recomendaciones` y `GET /recomendaciones/{id}/mercancias` responden según la sección 2.
- La reconsulta por `idLineaSolicitud` exige también `origenSistema`: un mismo código de línea puede existir en los dos sistemas.
- Las recomendaciones se persisten append-only: recalcular crea una corrida nueva, y el `idRecomendacionVehiculo` guardado por un consumidor sigue devolviendo lo mismo al reconsultarlo.
- Dos llaves distintas, una por sistema, y el origen verificado contra la llave.
- Cada escritura de catálogo publica su evento (`mercancia.creada`, `carroceria.actualizada`, etc.) para que la proyección de NEXO no quede vieja sin avisar.
- `openapi.json` queda regenerado.

---

### [S5-HU-17.5] CRUD de los seis catalogos, con concurrencia optimista

**Labels:** `nexo`, `backend`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `NEXO`
**Asignado a:** Brayan Quintero (@bquinterobtst)
**Type:** `Task` · **Priority:** High · **Effort:** High
**Parte de S5-HU-17 (milestone). Depende de 17.2.** Estimado: 2 a 3 días.

Los CRUD viven **en el backend del Cubicador**, donde está el dominio. El portal los consume por passthrough desde NEXO (S5-HU-18.6), nunca directo: el navegador no puede llamar al Cubicador porque el CORS se retiró a propósito en S3-HU-05, la llave terminaría en el bundle —que ya pasó una vez y obligó a rotarla— y sobre todo **el rol no se podría aplicar**, porque el Bearer del Cubicador autoriza un backend, no una persona.

**En orden de construcción**, que lo dictan las llaves foráneas:

| # | Catálogo | Validaciones propias |
|---|---|---|
| 1 | Parámetros del motor | Solo `GET` y `PUT` por clave |
| 2 | Categorías de mercancía | Bloquear borrado si una mercancía o una regla la referencia |
| 3 | Tipos de carrocería | Bloquear borrado si una mercancía o un vehículo la referencia |
| 4 | Tipos de mercancía | Secundaria distinta de principal; sin secundaria, la bandera condicionada en `false`. Borrado lógico si tiene recomendaciones |
| 5 | Vehículos y su relación con carrocerías | Un solo payload que el backend reconcilia. `UNIQUE` en el código. Validar dominancia al guardar. Capacidad específica > 0 cuando no usa la del vehículo, con `CHECK` en base además del formulario |
| 6 | Reglas de incompatibilidad | Sin `PUT`. No admitir `(A,B)` si existe `(B,A)`. Categoría 1 distinta de categoría 2 |

**Criterios de aceptación:**

- Los seis catálogos tienen sus endpoints, con las validaciones de la tabla.
- **Concurrencia optimista con `version_id_col` de SQLAlchemy**: el `GET` devuelve la versión, el `PUT` la exige, y si no coincide responde 409. Sin esto, dos administradores editando el mismo vehículo se pisan en silencio, y duele más de lo normal porque ese catálogo alimenta el motor: no se manifiesta como error sino como recomendaciones sutilmente equivocadas.
- `CreadoPor` se llena con el id de usuario que NEXO envía **como campo del cuerpo**, declarado como dato del llamante. El Cubicador no tiene usuarios y no lo puede verificar; conviene ser honestos al respecto en el código.
- Cada escritura publica su evento de catálogo.

---

### [S5-HU-17.6] Limpieza: matar el fallback silencioso y reescribir el puente desde_linea

**Labels:** `nexo`, `backend`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `NEXO`
**Asignado a:** Brayan Quintero (@bquinterobtst)
**Type:** `Task` · **Priority:** High · **Effort:** Medium
**Parte de S5-HU-17 (milestone). Depende de 17.3.** Estimado: 1 día.

**Matar el fallback silencioso.** Hoy `_PRESET_POR_UNIDAD_RAPIDA.get(unidad, "Tractocamion furgon 13.6 m")` asume un furgón de 13,6 metros cuando no reconoce el vehículo. Pasa a rechazo explícito.

**Reescribir el puente `crear_plan_desde_linea`** y sus pruebas. Se simplifica: hoy adivina cuántos vehículos hacen falta llenando uno y viendo qué sobra, hasta veinte veces. El motor nuevo ya responde cuántos y de qué tipo, así que la cascada a ciegas se reemplaza por "crear N planes con los vehículos indicados y optimizar cada uno". **Es menos código del que hay.**

El puente traduce el código canónico al preset del 3D mediante un **diccionario de compatibilidad** `CodigoVehiculo → preset`. El motor 3D no se toca.

**Ya hecho, para no repetirlo:** los presets del 3D se corrigieron contra el Excel (`application/plan_cargue/vehiculos.py`) — cinco configuraciones con medidas y capacidad reales, más la Minimula 2S2 que faltaba por completo. Cama baja y los tres contenedores quedaron intactos por no tener contraparte. Las 122 pruebas pasan.

**Criterios de aceptación:**

- Un vehículo no reconocido produce un rechazo con motivo, no un furgón de 13,6 m asumido.
- `crear_plan_desde_linea` consume la recomendación del motor nuevo en vez de la cascada de hasta veinte intentos, y sus pruebas se reescriben acordes.
- El diccionario de compatibilidad está escrito y documentado, cubriendo las cinco configuraciones canónicas.
- Se escribe el documento de **deuda diferida del 3D** con los cuatro puntos: unificar los dos catálogos de vehículos, retirar el diccionario, decidir qué pasa con puerta y ejes (4 de los 26 casos dorados los usan: balance de ejes 20, 21 y 22, y puerta 23; el 25 sobrevive porque depende de la *posición* de la puerta, no de sus dimensiones), y re-evidenciar la compuerta de paridad si esos casos cambian.
- El catálogo quemado de SICE-TAC (`cubicacion_rapida.py::CATALOGO`) queda retirado, reemplazado por la lectura de base.

---

### [S5-HU-17.7] QA: verificacion end-to-end del cubicador reestructurado

**Labels:** `nexo`, `qa`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `NEXO`
**Asignado a:** Kevin Cano (@kcano-ux)
**Type:** `Task` · **Priority:** High · **Effort:** Medium
**Parte de S5-HU-17 (milestone). Depende de 17.1 a 17.6.**

Verificar manualmente, además del CI, que la reestructuración hace lo que la hoja de ruta dice. Documentar la evidencia en **`tests/QA/s5-hu-17-reestructuracion-cubicador/README.md`**, con el mismo formato de las carpetas existentes: alcance verificado, entorno usado, resultados y bugs encontrados. Lo último registrado en `tests/QA/` es `s4-hu-07`, así que la carpeta se crea desde cero.

**Criterios de aceptación:**

- Las 122 pruebas de `cubicador/` corren en verde contra **SQL Server real**, no contra un sustituto, incluidos los 26 casos dorados de paridad.
- La batería de `docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md` pasa contra el motor de base nuevo.
- Se verifica a mano, con el contrato real: una carga homogénea, **una heterogénea que devuelva flota mixta**, una con mercancía condicionada, una extradimensionada y una con tipo de mercancía desconocido.
- Se verifica que recalcular una ruta **crea una corrida nueva** y que el `idRecomendacionVehiculo` anterior sigue devolviendo lo mismo.
- Se verifica que la llave de un sistema no sirve para declarar el otro `origenSistema` (401).
- Se verifica que correr los seeders dos veces no duplica ni sobrescribe.
- Se revisan los resultados de la validación contra los 50 a 100 despachos históricos de 17.3 y se deja registrado el número de vehículos de más, si lo hay.

---

## S5-HU-18 — Nexo consume el contrato nuevo del cubicador y agrupa las lineas por ruta

> **Repo de la milestone:** `NEXO` · **Vence:** 16 de octubre de 2026

```
RESPONSABLE HU: Ismael Vasco (@ivasco-dev)
[Sprint 5 · Nexo]
Como Administrador de Fletes quiero que Nexo reciba del cubicador la flota completa que necesita una carga, con su peso y su porcentaje de llenado por camion, y que las lineas que pertenecen a una misma ruta se vean relacionadas, para costear lo que de verdad se va a despachar en vez de una sugerencia de un solo vehiculo.

CRITERIOS DE ACEPTACION (resumen Gherkin):

- El adaptador de cubicacion rapida consume el contrato nuevo y entiende una flota de varios vehiculos distintos, no "N vehiculos iguales".
- La captura guarda el tipo de mercancia por posicion, y el id de la recomendacion por linea, para poder cruzar lo recomendado contra lo despachado.
- Varias lineas que pertenecen a la misma ruta quedan relacionadas por un campo nuevo, y la Mesa de Costeo deja de mostrarlas sueltas.
- Un vehiculo que el sistema no reconoce se crea en cuarentena, fuera de los desplegables, con una bandeja donde alguien lo mapea o lo descarta; deja de ensuciar el catalogo en silencio.
- Los catalogos de mercancias y carrocerias del cubicador se proyectan en Nexo por eventos, con el patron que ya existe para vehiculos.
- El portal puede administrar los catalogos del cubicador sin que el navegador le hable nunca al cubicador.

HABILITADOR: contrato definido en cubicador/docs/HOJA-DE-RUTA-CUBICADOR.md seccion 2 (2026-10-08), construido en S5-HU-17.4. El campo de agrupacion por ruta fue confirmado con el equipo de Cotifly el 2026-10-08, con sus tres condiciones.
ROLES QUE INTERVIENEN: Backend, QA
ALCANCE EN ESTE REPO: adaptador y DTOs de cubicacion, captura, lineas de solicitud, resolucion de vehiculos, proyeccion de catalogos y controladores de passthrough.
HU GEMELA EN: S5-HU-17 (cubicador) y S5-HU-19 (portal, repo administracion)
HU completa en Notion: (pendiente crear y pegar el link)
Las tareas tecnicas de esta HU se crean como ISSUES asociadas a este milestone.
```

### Issues en `NEXO`

---

### [S5-HU-18.1] Consumir el contrato nuevo en clsCubicacionRapidaAdapter

**Labels:** `nexo`, `backend`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `NEXO`
**Asignado a:** Ismael Vasco (@ivasco-dev)
**Type:** `Task` · **Priority:** High · **Effort:** Medium
**Parte de S5-HU-18 (milestone). Depende de S5-HU-17.4.** Estimado: 1 a 2 días.

`clsCubicacionRapidaAdapter` y sus DTOs asumen hoy una respuesta de un vehículo, o de N vehículos iguales. El contrato nuevo devuelve una **lista de vehículos que pueden ser distintos entre sí**, cada uno con su peso, su volumen y sus dos porcentajes de llenado.

**Criterios de aceptación:**

- Los DTOs representan una flota mixta; ninguna estructura asume N vehículos iguales.
- Se consumen los dos porcentajes, no solo el volumétrico que hoy llega como `factorUtilizacion`.
- `POST /api/cubicacion` adopta la forma nueva en el mismo corte; no se mantiene el contrato viejo.
- La traducción de errores HTTP a `clsApiError` se extiende con los códigos nuevos de la sección 2.6 — se **extiende el catálogo existente**, no se inventa uno nuevo.
- `SIN_SELECCION_AUTOMATICA` y `condicionada` se tratan como resultado, no como fallo: llegan con 200 y hay que distinguirlos de "la llamada falló".

---

### [S5-HU-18.2] Guardar el tipo de mercancia por posicion en Dimensiones y el id de la recomendacion

**Labels:** `nexo`, `backend`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `NEXO`
**Asignado a:** Ismael Vasco (@ivasco-dev)
**Type:** `Task` · **Priority:** High · **Effort:** Medium
**Parte de S5-HU-18 (milestone). Depende de S5-HU-17.4.** Estimado: 1 día.

El JSON de `Dimensiones` guarda hoy medidas y cantidad por posición, pero **no el tipo de mercancía**. Sin ese dato el cubicador no puede resolver la carrocería ni aplicar las reglas de incompatibilidad, porque la segregación depende hoy de que alguien haya escrito "ácido" en la descripción libre.

Toca `CapturaRequests`, `clsCapturarSolicitudCommand`, `clsPosicionCargaDto` y el contrato de `GET /api/lineas-solicitud/{id}/cubicacion`.

**Criterios de aceptación:**

- `Dimensiones` guarda el **código** de mercancía por posición. Se guarda el código y no el uuid porque es estable, sobrevive una resiembra y es legible en un log — a diferencia del cable hacia el cubicador, que lleva el id.
- Las líneas ya capturadas, que no lo tienen, se tratan como tipo desconocido. **No se les asigna un default**, porque el tipo arrastra la carrocería y un default silencioso produciría una recomendación equivocada con aspecto de correcta.
- La línea de solicitud guarda el `idRecomendacionVehiculo` que le corresponde, para poder cruzar lo recomendado contra lo despachado.

---

### [S5-HU-18.3] GrupoRutaId: relacionar las lineas que pertenecen a una misma ruta

**Labels:** `nexo`, `backend`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `NEXO`
**Asignado a:** Ismael Vasco (@ivasco-dev)
**Type:** `Task` · **Priority:** High · **Effort:** Medium
**Parte de S5-HU-18 (milestone).** Estimado: 1 día. **No depende del cubicador: se puede hacer en paralelo.**

Cuando una carga heterogénea produce varios camiones, Cotifly le manda a NEXO **una línea por camión** — pero hoy nada dice que esas líneas son la misma ruta, así que la Mesa de Costeo las muestra sueltas, sin relación visible.

**No se parte `tbl_lineas_solicitud`.** Eso tiene sentido en Cotifly, que está reestructurando todo su modelo, pero en NEXO esa tabla es vieja y la usa todo el sistema de costeo. Alcanza con **una columna nueva**: `GrupoRutaId`, `uniqueidentifier NULL` (texto de 64 caracteres también sirve). Confirmado con Cotifly el 2026-10-08.

**Criterios de aceptación:**

- `CapturaLineaRequest` acepta el campo, **opcional y nullable**, y lo guarda tal cual en la columna nueva.
- `GET /api/solicitudes/{id}` lo devuelve en cada línea — sin esto Cotifly no puede reconsultar ni emparejar los camiones de una ruta.
- **El valor lo pone Cotifly, nunca NEXO.** Cotifly manda el GUID de su propia ruta en todas las líneas-camión que vengan de ahí; NEXO solo lo guarda. Así el emparejamiento es estable aunque se reenvíe o se reconsulte.
- Las líneas que nacen directo en NEXO quedan en `NULL` y eso no rompe nada.
- **Orden dentro del grupo:** los servicios conexos de una ruta se pegan a la línea del primer camión, así que el orden importa. Antes de agregar otra columna, revisar si `numeroLinea` —que ya incrementa en orden de captura— alcanza: si Cotifly manda las líneas de un grupo seguidas, quedan contiguas y ordenadas sin nada nuevo.
- Un índice por `GrupoRutaId` es opcional; no hace falta para el volumen de Cotifly.

---

### [S5-HU-18.4] Cuarentena en ResolverVehiculo y bandeja de normalizacion

**Labels:** `nexo`, `backend`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `NEXO`
**Asignado a:** Ismael Vasco (@ivasco-dev)
**Type:** `Task` · **Priority:** High · **Effort:** Medium
**Parte de S5-HU-18 (milestone).** Estimado: 1 día. **No depende del cubicador.**

`ResolverVehiculo` compara hoy texto exacto sobre `Normalizar(tipo)|Normalizar(carroceria)` y, cuando no coincide, **crea una configuración nueva**. Por eso `TMULA`, `T.MULA` y `Tractomula` ya son tres filas distintas en `tbl_configuraciones_vehiculo`, que además no tiene columna de código — verificado: solo `Id`, `TipoVehiculo` (texto 100), `Carroceria` (texto 100) y `RangoPeso`. Tampoco existe otro catálogo de vehículos con código en NEXO.

Por eso esa tabla **no puede ser la fuente de los códigos**: heredaría los duplicados y el `UNIQUE` sobre el código sería imposible. El Cubicador es el dueño de los códigos canónicos y NEXO **converge hacia ellos**.

**Criterios de aceptación:**

- `tbl_configuraciones_vehiculo` gana la bandera `EsCatalogoOficial`.
- Las filas que corresponden a un código canónico (`2L2`, `C2`, `C3`, `2S2`, `3S3`) se marcan oficiales; el resto queda en cuarentena, **fuera de los desplegables**.
- `ResolverVehiculo` deja de crear filas oficiales en silencio: lo que no reconoce nace con `EsCatalogoOficial = false`.
- Existe una **bandeja de normalización** donde alguien mapea cada configuración en cuarentena a un código canónico o la descarta. Sin ella la cuarentena solo acumula. La pantalla vive en S5-HU-19.3.
- El diccionario de alias mapea la basura existente a los códigos canónicos. Se llega al catálogo único **por convergencia**, no heredando los duplicados el primer día.

---

### [S5-HU-18.5] Proyectar los catalogos de mercancias y carrocerias por eventos

**Labels:** `nexo`, `backend`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `NEXO`
**Asignado a:** Ismael Vasco (@ivasco-dev)
**Type:** `Task` · **Priority:** High · **Effort:** Medium
**Parte de S5-HU-18 (milestone). Depende de S5-HU-17.4.** Estimado: 1 día.

NEXO ya proyecta el catálogo de vehículos del Cubicador con `VehiculoCatalogoConsumer`. Extender ese mismo patrón a tipos de mercancía y tipos de carrocería, que son los que alimentan los desplegables de captura.

**Criterios de aceptación:**

- Dos consumers nuevos, con el patrón de `VehiculoCatalogoConsumer`, haciendo upsert por Id.
- La proyección se mantiene al día con cada escritura de CRUD del Cubicador.
- Existe una forma de **republicar todo el catálogo** para reparar una desincronización, como ya existe para vehículos (`republicar_todos`).

---

### [S5-HU-18.6] Passthrough de los seis CRUD, con rol por endpoint

**Labels:** `nexo`, `backend`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `NEXO`
**Asignado a:** Ismael Vasco (@ivasco-dev)
**Type:** `Task` · **Priority:** High · **Effort:** Medium
**Parte de S5-HU-18 (milestone). Depende de S5-HU-17.5.** Estimado: 1 a 2 días.

Seis controladores delgados, uno por catálogo, que espejan las rutas del Cubicador, aplican el rol y reenvían. **No un proxy comodín `/{*path}`**: no podría aplicar roles por endpoint y dejaría el Swagger de NEXO sin contrato.

El secreto vive solo en configuración del backend. El navegador nunca llama al Cubicador.

**Criterios de aceptación:**

- Seis controladores, uno por catálogo, con las rutas espejadas y el contrato visible en Swagger.
- `GET` con `[Authorize]` sin rol específico, el criterio que NEXO ya aplica a sus consultas.
- `POST`, `PUT` y `DELETE` con `[Authorize(Roles = "AdminTarifas,Admin")]`. **No existe `AdminFletes`**: los seis roles son `CoordinadorFletes`, `LiderProceso`, `ServiciosLogisticos`, `Admin`, `Comercial` y `AdminTarifas`, y el que encaja es `AdminTarifas`, que ya administra reglas Solo-SICETAC y tarifas de plaza.
- El id del usuario autenticado se reenvía como campo del cuerpo para que el Cubicador llene `CreadoPor`.
- El token de concurrencia del `GET` se devuelve al portal y se exige en el `PUT`; un 409 del Cubicador se propaga con un mensaje que el portal pueda mostrar.

---

### [S5-HU-18.7] QA: verificacion end-to-end de la integracion Nexo-Cubicador

**Labels:** `nexo`, `qa`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `NEXO`
**Asignado a:** Kevin Cano (@kcano-ux)
**Type:** `Task` · **Priority:** High · **Effort:** Medium
**Parte de S5-HU-18 (milestone). Depende de 18.1 a 18.6 y de S5-HU-17.**

Documentar la evidencia en **`tests/QA/s5-hu-18-nexo-contrato-cubicador/README.md`**.

**Criterios de aceptación:**

- La suite de `Nexo.Api.Tests` corre en verde contra la base de pruebas real.
- Se verifica de punta a punta: capturar una solicitud con tipo de mercancía por posición, recibir una **flota mixta**, y que cada línea guarde su `idRecomendacionVehiculo`.
- Se verifica que varias líneas con el mismo `GrupoRutaId` se devuelven relacionadas en `GET /api/solicitudes/{id}`, y que las líneas nacidas en NEXO con `NULL` no rompen nada.
- Se verifica que un vehículo no reconocido **no aparece en los desplegables** y sí en la bandeja de cuarentena.
- Se verifica que un usuario sin `AdminTarifas` recibe 403 en las escrituras de los seis CRUD, y que el `GET` sí le responde.
- Se verifica el 409 de concurrencia: dos ediciones del mismo vehículo, la segunda falla con mensaje claro en vez de pisar la primera.

---

## S5-HU-19 — Portal: captura con mercancia, flota mixta, administracion de catalogos y Mesa de Costeo

> **Repo de la milestone:** `administracion` · **Vence:** 16 de octubre de 2026

```
RESPONSABLE HU: Juan Pablo Velez (@jpvelez-bts)
[Sprint 5 · Nexo]
Como Comercial o Administrador de Fletes quiero capturar el tipo de mercancia de cada posicion, ver la flota completa que el cubicador recomienda con su llenado real, y poder administrar los catalogos del cubicador desde el portal, para trabajar con la recomendacion real en vez de una sugerencia de un solo vehiculo.

CRITERIOS DE ACEPTACION (resumen Gherkin):

- La captura tiene un desplegable de tipo de mercancia por posicion, obligatorio, alimentado por el catalogo del cubicador.
- La recomendacion se pinta como una lista real de camiones, que pueden ser de configuraciones distintas, cada uno con su peso y sus dos porcentajes de llenado.
- Se puede pedir cubicacion desde una sola posicion, no desde tres.
- El portal deja de escribir la clase de viaje y la clase de contenedor a partir de la sugerencia: las elige el usuario.
- Existen las pantallas de administracion de los seis catalogos y la bandeja de normalizacion de vehiculos en cuarentena.
- En Mesa de Costeo se puede editar el vehiculo de una linea Y dejar el precio sin vehiculo asignado; no se fuerza una sola de las dos.

HABILITADOR: contrato definido en cubicador/docs/HOJA-DE-RUTA-CUBICADOR.md seccion 2 (2026-10-08). El desplegable de mercancia es la pieza que hoy falta para que la segregacion quimica deje de depender de que alguien escriba "acido" en la descripcion libre.
ROLES QUE INTERVIENEN: Frontend, QA
ALCANCE EN ESTE REPO: todo el desarrollo de UI de esta HU.
HU GEMELA EN: S5-HU-17 (cubicador) y S5-HU-18 (backend de Nexo), repo InnovacionIA-proyectos/NEXO
HU completa en Notion: (pendiente crear y pegar el link)
Las tareas tecnicas de esta HU se crean como ISSUES asociadas a este milestone.
```

### Issues en `administracion`

---

### [S5-HU-19.1] Captura: desplegable de mercancia, los dos porcentajes, y cubicar desde una posicion

**Labels:** `frontend`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `administracion`
**Asignado a:** Juan Pablo Vélez (@jpvelez-bts)
**Type:** `Task` · **Priority:** High · **Effort:** High
**Parte de S5-HU-19 (milestone). Depende de S5-HU-18.2.** Estimado: 1 a 2 días.

Cuatro cambios en la captura:

**Desplegable de tipo de mercancía por posición**, obligatorio. Hoy no se captura, y la segregación química depende de que alguien escriba "ácido" en la descripción libre.

**Mostrar los dos porcentajes.** Hoy solo se muestra `factorUtilizacion`, que es volumétrico puro. El contrato nuevo devuelve llenado de volumen y de peso, y la decisión se toma con el mayor de los dos.

**Cubicar desde una sola posición.** Hoy `showCubicacion` exige tres, salvo en Carga Suelta.

**Quitar el patch de `claseViaje` y `claseContenedor`.** El cubicador deja de sugerirlos: los elige el usuario, y si la recomendación no coincide con lo que marcó, llega como advertencia.

**Criterios de aceptación:**

- El desplegable se alimenta del catálogo proyectado en NEXO, no de una lista quemada.
- No se puede enviar una posición sin tipo de mercancía.
- Se muestran los dos porcentajes por vehículo, identificados.
- Con una sola posición capturada se puede pedir cubicación, en cualquier clase de viaje.
- El formulario deja de recibir la clase de viaje y la clase de contenedor desde la respuesta del cubicador.
- Las advertencias que vienen en `advertencias[]` se muestran al usuario, incluida la de clase de viaje que no coincide.

---

### [S5-HU-19.2] Pintar la recomendacion como una lista real de camiones

**Labels:** `frontend`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `administracion`
**Asignado a:** Juan Pablo Vélez (@jpvelez-bts)
**Type:** `Task` · **Priority:** High · **Effort:** Medium
**Parte de S5-HU-19 (milestone). Depende de S5-HU-18.1.** Estimado: 1 día.

El portal renderiza hoy "N × Nombre", que asume N vehículos iguales. El contrato nuevo puede devolver una flota mixta — por ejemplo un Sencillo y una Minimula para la misma carga.

**Criterios de aceptación:**

- La flota se pinta como lista, un ítem por camión, con su configuración, su carrocería, su peso en kilos y sus dos porcentajes.
- Una flota mixta se ve correctamente; ninguna parte de la vista asume vehículos iguales.
- Los resultados `SIN_SELECCION_AUTOMATICA`, `condicionada` y `extradimension` se muestran como estados claros, distinguibles de un error de la llamada.
- Cuando el resultado es condicionado, se indica qué mercancías lo son y que la solicitud viaja completa a la Mesa de Costeo.

---

### [S5-HU-19.3] Pantallas de los seis catalogos y bandeja de normalizacion de vehiculos

**Labels:** `frontend`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `administracion`
**Asignado a:** Juan Pablo Vélez (@jpvelez-bts)
**Type:** `Task` · **Priority:** High · **Effort:** High
**Parte de S5-HU-19 (milestone). Depende de S5-HU-18.6.** Estimado: 2 a 3 días.

Seis pantallas de administración —parámetros del motor, categorías, tipos de carrocería, tipos de mercancía, vehículos con sus carrocerías, y reglas de incompatibilidad— más una séptima: la **bandeja de normalización** que lista las configuraciones con `EsCatalogoOficial = false` y deja mapear cada una a un código canónico o descartarla. Sin esa bandeja, la cuarentena solo acumula.

**Criterios de aceptación:**

- Las seis pantallas consumen el passthrough de NEXO, nunca el Cubicador directo.
- Las escrituras solo están disponibles para `AdminTarifas` y `Admin`; la consulta, para cualquier rol autenticado.
- La pantalla de vehículos edita el vehículo y sus carrocerías en **un solo formulario**, con un solo guardado.
- El **409 de concurrencia** se muestra con un mensaje accionable: alguien más modificó este registro, recargue y vuelva a intentar. No se pisa el cambio del otro en silencio.
- La bandeja de cuarentena permite mapear o descartar, y lo mapeado desaparece de la bandeja.

---

### [S5-HU-19.4] Mesa de Costeo: editar el vehiculo, dejar el precio sin vehiculo, y agrupar por ruta

**Labels:** `frontend`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `administracion`
**Asignado a:** Juan Pablo Vélez (@jpvelez-bts)
**Type:** `Task` · **Priority:** High · **Effort:** Medium
**Parte de S5-HU-19 (milestone). Depende de S5-HU-18.3.** Estimado: 1 día.

Dos cosas, y **las dos se habilitan, no una sola**: poder editar el vehículo asignado a una línea, y poder dejar el precio sin vehículo asignado. Es lo que hace usable el resultado `SIN_SELECCION_AUTOMATICA`: la línea llega sin vehículo y el coordinador o le asigna uno o la cotiza igual.

Además, las líneas que comparten `GrupoRutaId` se muestran agrupadas. Hoy los camiones de una ruta heterogénea se ven sueltos, sin relación visible.

**Criterios de aceptación:**

- Se puede cambiar el vehículo de una línea desde Mesa de Costeo.
- Se puede guardar el precio de una línea sin vehículo asignado, sin que el formulario lo exija.
- Las líneas de un mismo `GrupoRutaId` se muestran agrupadas y en orden estable.
- Las líneas con `GrupoRutaId` en `NULL` se siguen mostrando como siempre.

---

### [S5-HU-19.5] QA: verificacion end-to-end del portal

**Labels:** `frontend`, `qa`, `sprint-5`, `tarea-tecnica`
**Repo donde se crea este issue:** `administracion`
**Asignado a:** Kevin Cano (@kcano-ux)
**Type:** `Task` · **Priority:** High · **Effort:** Medium
**Parte de S5-HU-19 (milestone). Depende de 19.1 a 19.4.**

Documentar la evidencia en **`tests/QA/s5-hu-19-portal-cubicador/README.md`** del repo `NEXO`, que es donde vive el registro único de verificación manual. *(Si se prefiere que la evidencia del portal viva en su propio repo, decidirlo antes de crear la issue.)*

**Criterios de aceptación:**

- Recorrido completo con datos reales: login, captura con tipo de mercancía por posición, cubicación, revisión de la flota y Mesa de Costeo.
- Se verifica una carga que devuelve **flota mixta** y se confirma que la vista la pinta correctamente.
- Se verifica que no se puede enviar una posición sin tipo de mercancía.
- Se verifica el recorrido de los seis CRUD con un usuario `AdminTarifas` y con uno que no lo es.
- Se verifica el 409 de concurrencia desde dos sesiones simultáneas.
- Se verifica que una línea sin vehículo asignado se puede cotizar, y que una con vehículo se puede editar.
- Se verifica que la bandeja de cuarentena lista lo no reconocido y que mapear lo saca de la bandeja.

---

## Lo que sigue abierto

Ninguno bloquea la creación de estas issues; están en la sección 9 de la hoja de ruta con dueño.

**Datos, con Operaciones:** confirmar el renombre de `COMBUSTIBLE` a `CARBURANTE`, los diez tipos excluidos, y llenar `apilable`, `fragil` y `rotacion_libre`. Es una sesión de una o dos horas y es el **camino crítico** de S5-HU-17.2 y 17.3.

**Negocio:** dimensiones y capacidad de los contenedores 40HC y 20STD — hasta que lleguen, "carga contenerizada" cae en sin selección. Y con qué vehículos emparejan `CAMA_BAJA` y `PORTACONTENEDORES`, que decide el Administrador de Fletes desde el CRUD.

**Dependencias de ejecución:** las credenciales del SQL Server de QA con usuario DDL, y la llave de Cotifly, que generan ellos y solo tiene que ser distinta de la de NEXO.
