# Qué falta construir — Motor de selección de tipo y cantidad de vehículos

Sigue de `PROPUESTA-BD-MOTOR-SELECCION-VEHICULOS.md` (diseño de tablas, ya
cerrado) y `propuesta-seleccion-vehiculos.md` (el flujo del motor). Este
documento es el plan de implementación: qué pantallas/CRUD, qué endpoints,
y qué falta para que Cotifly (u otro consumidor) pueda usarlo.

---

## 1. Prerrequisito: implementar la BD y migrar el motor a SQL Server 2019

Antes que cualquier CRUD o endpoint de los siguientes puntos, hay dos cosas
que hoy no existen en código, solo en estos documentos de diseño:

### 1.1 Crear las 9 tablas como modelos reales

Las tablas de `PROPUESTA-BD-MOTOR-SELECCION-VEHICULOS.md` hoy son solo
markdown — no hay modelos SQLAlchemy, no hay nada en
`infrastructure/persistence/modelos.py`. Hace falta:
- Un modelo SQLAlchemy por tabla (siguiendo el mismo patrón que ya existe
  ahí para `VehiculoCubicacionModel`/`PlanCargueModel`).
- Repositorios (`IVehiculoRepository`, etc.) y su implementación EF-like
  con SQLAlchemy, igual que `clsEfServiciosConexosRepository` en NEXO o
  `SqlVehiculoCubicacionRepository` aquí mismo en Cubicador.
- Cargar los 7 CSV de `cubicador/docs/seeder/` como siembra inicial (mismo
  patrón que `presets()` + `listar_con_siembra()` ya usa para
  `tbl_vehiculos_cubicacion`).
- Como este proyecto no usa Alembic ni migraciones (`Base.metadata.create_all`
  en `infrastructure/persistence/db.py:35`, igual que el resto de NEXO no
  usa migraciones de EF Core), las tablas nuevas se crean solas al arrancar
  — no hay que escribir un script de migración aparte para esta parte.

### 1.2 Migrar el motor de base de datos de PostgreSQL a SQL Server 2019

Esto es aparte y más grande — **afecta a TODO Cubicador, no solo a las 9
tablas nuevas.** Hoy (`infrastructure/persistence/db.py`,
`docker-compose.yml`) el motor en producción es PostgreSQL 16
(`postgresql+asyncpg`), decisión que el propio `DECISIONES_TECNICAS.md`
(línea 392-398) ya dejaba condicionada: *"Confirmar con el equipo si se
prefiere otro motor antes de desplegar a producción."* Esa confirmación ya
llegó: SQL Server 2019.

Qué implica, concretamente:

1. **Cambiar el dialecto de SQLAlchemy** — de `postgresql+asyncpg` a algo
   como `mssql+aioodbc` (o el driver async que se decida), con el ODBC
   Driver de SQL Server instalado en la imagen Docker.
2. **Revisar tipos de columna específicos de Postgres.** Por ejemplo, UUID
   nativo de Postgres vs `UNIQUEIDENTIFIER` de SQL Server — hay que
   verificar que los modelos usen un tipo neutral (`sqlalchemy.Uuid` o un
   `TypeDecorator` propio) que SQLAlchemy traduzca bien a los dos motores,
   no un tipo Postgres-específico.
3. **Revisar manejo de errores específico de Postgres.** Ya hay código que
   distingue excepciones de Postgres explícitamente
   (`infrastructure/persistence/repositorios/plan_cargue_repo.py:68-69`,
   comentario sobre `ForeignKeyViolationError`) — hace falta el equivalente
   para las excepciones que lanza el driver de SQL Server.
4. **`docker-compose.yml`**: cambiar el servicio `db` de `postgres:16-alpine`
   a una imagen de SQL Server 2019 (`mcr.microsoft.com/mssql/server:2019-latest`),
   con su propio healthcheck (el actual usa `pg_isready`, que no sirve para
   SQL Server).
5. **Correr toda la batería de pruebas de integración existente**
   (`testDocumentation/PRUEBAS_DE_INTEGRACION.md`) contra el SQL Server
   real, no solo contra Postgres — el smoke test end-to-end
   (`tests/api/test_smoke.py`) es justo lo que ya validó el flujo completo
   contra Postgres 16 real; hay que repetir esa validación contra el motor
   nuevo antes de dar por cerrada la migración.
6. **Actualizar `DECISIONES_TECNICAS.md`** dejando constancia de que la
   decisión de Postgres se revirtió, y por qué (alineación con el resto de
   NEXO, que ya es SQL Server).

**Orden recomendado:** primero migrar el motor existente (punto 1.2) con
las tablas que YA hay (`tbl_vehiculos_cubicacion`, `tbl_planes_cargue`,
etc.), validar que todo sigue funcionando igual contra SQL Server, y
DESPUÉS crear las 9 tablas nuevas (punto 1.1) ya directamente sobre SQL
Server — así no se duplica el trabajo de probar dos veces (una vez en
Postgres, otra en SQL Server).

---

## 2. CRUD de catálogos

Cinco catálogos que pediste, más uno (`parametrosMotor`) que falta para que
el motor no tenga nada hardcodeado — lo exige la propia propuesta madre
(§8.2).

### 2.1 Vehículos (Tabla 1 + Tabla 4 en el mismo formulario)

| Método | Ruta | Qué hace |
|---|---|---|
| `GET` | `/vehiculos` | Lista, con filtro `activo` |
| `GET` | `/vehiculos/{id}` | Detalle, incluyendo sus carrocerías asociadas |
| `POST` | `/vehiculos` | Crea el vehículo **y** las filas de `relacionVehiculosCarroceria` en una sola operación |
| `PUT` | `/vehiculos/{id}` | Actualiza datos del vehículo **y** reemplaza el set de carrocerías asociadas |
| `DELETE` | `/vehiculos/{id}` | Soft-delete (`Activo=false`) si tiene recomendaciones históricas asociadas; borrado real si no |

**El punto que pediste:** el `POST`/`PUT` reciben el vehículo **y** la lista
de carrocerías que aplica, en el mismo payload — no hay una pantalla aparte
para la Tabla 4. **Ajustado:** cada carrocería ya no es solo un id suelto —
va con las 2 columnas nuevas de la Tabla 4
(`UsaCapacidadMaximaDelVehiculo`/`CapacidadMaximaEspecifica`), para poder
resolver el caso Tractomula/Tractomula Furgón sin duplicar el código del
vehículo (ver nota de la Tabla 4 en `PROPUESTA-BD-MOTOR-SELECCION-VEHICULOS.md`).
Ejemplo de body:

```json
{
  "codigo": "3S3",
  "nombre": "Tractomula 3S3",
  "largoCm": 1220,
  "anchoCm": 240,
  "alturaMaximaReglamentariaCm": 260,
  "capacidadMaxima": 34000,
  "carrocerias": [
    { "idCarroceria": "<id-estacas>", "usaCapacidadMaximaDelVehiculo": true },
    { "idCarroceria": "<id-plataforma>", "usaCapacidadMaximaDelVehiculo": true },
    { "idCarroceria": "<id-furgon>", "usaCapacidadMaximaDelVehiculo": false, "capacidadMaximaEspecifica": 30000 }
  ]
}
```

Con Estacas y Plataforma usa los 34.000 kg normales del vehículo; con
Furgón, los 30.000 kg específicos de esa combinación.

El backend borra las filas de `relacionVehiculosCarroceria` que ya no estén
en la lista y crea las nuevas — no hay que mandar nada a la Tabla 4 por
separado.

### 2.2 Tipos de carrocería

| Método | Ruta | Qué hace |
|---|---|---|
| `GET` | `/tipos-carroceria` | Lista |
| `GET` | `/tipos-carroceria/{id}` | Detalle |
| `POST` | `/tipos-carroceria` | Crea |
| `PUT` | `/tipos-carroceria/{id}` | Actualiza |
| `DELETE` | `/tipos-carroceria/{id}` | Bloquear si algún `tiposMercancia` o `vehiculos` la referencia; si no, borrar |

Catálogo simple, sin complicaciones — pero igual necesita el bloqueo de
borrado si está en uso, porque es FK de otras dos tablas.

### 2.3 Tipos de mercancía

| Método | Ruta | Qué hace |
|---|---|---|
| `GET` | `/tipos-mercancia` | Lista, filtro por `categoria`, `cubicable`, `condicionadaCubicacion` |
| `GET` | `/tipos-mercancia/{id}` | Detalle |
| `POST` | `/tipos-mercancia` | Crea |
| `PUT` | `/tipos-mercancia/{id}` | Actualiza |
| `DELETE` | `/tipos-mercancia/{id}` | Soft-delete si tiene recomendaciones históricas; si no, borrado real |

**Validaciones que necesita el formulario, no solo la BD:**
- `carroceriaSecundaria` no puede ser igual a `carroceriaPrincipal`.
- Si `carroceriaSecundaria` viene vacía, `carroceriaSecundariaCondicionada`
  debe quedar en `false` automáticamente (no dejar que alguien marque
  "condicionada" sin tener alternativa).

### 2.4 Categoría de mercancía

| Método | Ruta | Qué hace |
|---|---|---|
| `GET` | `/categorias-mercancia` | Lista |
| `POST` | `/categorias-mercancia` | Crea |
| `PUT` | `/categorias-mercancia/{id}` | Actualiza |
| `DELETE` | `/categorias-mercancia/{id}` | Bloquear si algún `tiposMercancia` o `reglasIncompatibilidad` la referencia |

### 2.5 Reglas de incompatibilidad

| Método | Ruta | Qué hace |
|---|---|---|
| `GET` | `/reglas-incompatibilidad` | Lista, cada fila con el nombre de las 2 categorías ya resuelto |
| `POST` | `/reglas-incompatibilidad` | Crea un par |
| `DELETE` | `/reglas-incompatibilidad/{id}` | Elimina el par |

**Sin `PUT`:** no tiene sentido editar un par existente — si cambia la
regla, se borra y se crea una nueva. Las validaciones de backend que hacen
falta:
- No dejar guardar `(A,B)` si ya existe `(A,B)` o `(B,A)` — ya decidimos
  que el par se guarda una sola vez, en cualquier orden que lo manden.
- `IdCategoria1` no puede ser igual a `IdCategoria2`.

### 2.6 Parámetros del motor (no la pediste, pero hace falta)

| Método | Ruta | Qué hace |
|---|---|---|
| `GET` | `/parametros-motor` | Lista los 5 parámetros actuales |
| `PUT` | `/parametros-motor/{clave}` | Actualiza el valor de uno |

Sin esto, `k_max`, `factor_vol_util`, etc. quedarían hardcodeados en el
motor — exactamente lo que la propuesta madre pide evitar (§8.2).

---

## 3. El motor en sí (separado de los CRUD de catálogo)

Los CRUD de arriba son solo datos — el motor que calcula la recomendación
(Partes 1, 2 y 3 de `propuesta-seleccion-vehiculos.md` §5) todavía no
existe como código, solo como diseño. Necesita su propio endpoint:

| Método | Ruta | Qué hace |
|---|---|---|
| `POST` | `/recomendaciones` | Recibe una línea de solicitud (mercancías + dimensiones + cantidades + peso) y la carrocería/sistema de origen; corre el motor completo; persiste el resultado en `recomendacionVehiculo` + `recomendacionVehiculoMercancia`; devuelve el resultado |

Esto es la pieza más grande de trabajo real de todo el plan — las Partes 1,
2 y 3 del flujo (agrupar por compatibilidad, decidir cuántos vehículos,
decidir cuáles) no están implementadas todavía, solo diseñadas en pseudo-
código en la propuesta madre.

---

## 4. Endpoints de integración con Cotifly (y otros consumidores)

Para que Cotifly (u otro sistema externo) pueda usar el motor y consultar
sus resultados:

| Método | Ruta | Qué hace |
|---|---|---|
| `POST` | `/recomendaciones` | (el mismo de arriba) — Cotifly manda la carga de una línea y recibe la recomendación en la misma respuesta |
| `GET` | `/recomendaciones?idLineaSolicitud={codigo}&origenSistema=COTIFLY` | Vuelve a consultar una recomendación ya hecha, sin tener que recalcularla |
| `GET` | `/recomendaciones/{idRecomendacionVehiculo}/mercancias` | El detalle de qué mercancías quedaron en un vehículo puntual (Tabla 8), para un camión específico de una recomendación heterogénea |
| `GET` | `/tipos-mercancia` | Catálogo de mercancías (ya lo pediste arriba) — Cotifly lo necesita para que su propio formulario de captura muestre las mismas opciones que conoce el motor, no una lista inventada aparte |
| `GET` | `/tipos-carroceria` | Igual, para que el dropdown de carrocería en Cotifly use los mismos códigos |

**Importante:** como `IdLineaSolicitud` puede venir de NEXO o de Cotifly
(campo `OrigenSistema` que ya agregamos a la Tabla 7), el filtro por
`origenSistema` en el `GET` es obligatorio — sin eso, un código de línea
igual entre los dos sistemas podría devolver la recomendación equivocada.

---

## 5. Otros endpoints que hacen falta, no evidentes a primera vista

1. ~~Resolver nombre de vehículo en texto libre → código canónico~~ —
   **decisión tomada: esto NO va en el Cubicador.** Surgió de la
   conversación sobre el "Diccionario de Configuraciones de vehículos para
   Nexo.xlsx", pero el Cubicador nunca recibe texto libre — siempre recibe
   los campos ya identificados/resueltos (`idTipoVehiculo`, etc.), quien
   se los mande. El diccionario de sinónimos (`vehiculoSinonimos` o como se
   llame) vive en la base de datos de quien SÍ recibe texto libre e importa
   Excel con nombres coloquiales — NEXO o Cotifly, está por decidir cuál de
   los dos. No es una tabla ni un endpoint del Cubicador.

2. **Aprobación obligatoria para mercancía condicionada.**
   Cuando una línea incluye una mercancía con `CondicionadaCubicacion=true`,
   la recomendación se genera igual (flujo automático normal, como ya
   definimos), pero alguien de AdminTarifas tiene que aprobarla antes de
   darla por buena.

   | Método | Ruta | Qué hace |
   |---|---|---|
   | `GET` | `/recomendaciones/pendientes-aprobacion` | Lista las recomendaciones que incluyen mercancía condicionada, sin aprobar todavía |
   | `POST` | `/recomendaciones/{id}/aprobar` | AdminTarifas aprueba la recomendación |
   | `POST` | `/recomendaciones/{id}/rechazar` | AdminTarifas la rechaza, con motivo |

   Esto implica agregar un campo a la Tabla 7 que no estaba en el diseño
   original (`EstadoAprobacion`: `NO_APLICA` / `PENDIENTE` / `APROBADA` /
   `RECHAZADA`) — vale la pena volver al documento de BD y agregarlo si
   decides construir este flujo.

3. **Export/import Excel de los catálogos.**
   NEXO ya tiene este patrón para Tarifas de Plaza y Reglas Solo-SICETAC
   (exportar lo cargado, editar, reimportar). Si Cubicador va a tener
   varias pantallas de catálogo nuevas, valdría la pena el mismo patrón
   para `tiposMercancia` al menos (31+ filas es tedioso de editar fila por
   fila en un formulario web). No es obligatorio para la primera versión,
   pero lo dejo anotado porque ya es un patrón conocido y esperado en este
   proyecto.

---

## 6. Seguridad / roles

Ninguno de los endpoints de arriba debería estar abierto sin autenticación
— son catálogos que afectan directamente qué se recomienda y qué no. Antes
de construir, definir con el equipo qué rol(es) pueden:
- Editar catálogos (vehículos, carrocerías, mercancías, reglas) — probablemente
  un rol tipo `AdminTarifas` o equivalente del lado del Cubicador.
- Aprobar/rechazar recomendaciones condicionadas (punto 5.2) — probablemente
  el mismo rol, o uno más restringido.
- Consultar/crear recomendaciones vía API (Cotifly, NEXO) — esto es
  server-to-server, necesita su propio mecanismo de autenticación de
  servicio, no un usuario humano.
