# Hoja de ruta — Reestructuración del Cubicador

**Fecha:** 2026-10-08 · **Cierre de sprint:** viernes 16 de octubre de 2026

El Cubicador pasa de un catálogo de 9 vehículos quemado en código a un motor de selección
que lee su catálogo desde SQL Server, y expone un contrato único que NEXO y Cotifly
consumen cada uno por su cuenta. Toca cinco repositorios y dos equipos.

**El motor 3D no se toca en esta etapa.** Se repara la cubicación rápida y se documenta
aparte qué habrá que ajustar en el 3D cuando le llegue el turno.

---

## 1. Principios de arquitectura

Cotifly no inventa un patrón nuevo: copia el que NEXO ya usa para hablarle al Cubicador.
Son seis reglas, todas presentes hoy en `clsCubicacionRapidaAdapter` de NEXO.

| Regla | Cómo se ve |
|---|---|
| Puerto en Application, adaptador en Infrastructure | `ICubicacionRapidaPort` + `clsCubicacionRapidaAdapter` |
| Adaptador de reemplazo cuando falta configuración | Responde 503 accionable en vez de tumbar la inyección de dependencias |
| `HttpClient` tipado, con el Bearer como header por defecto | Configurado una vez en DI, nunca por petición |
| Traducción de errores HTTP a códigos de dominio | 400 → validación, 401 → credencial, resto y red → servicio no disponible |
| El secreto vive solo en configuración del backend | Nunca en el bundle del navegador |
| Escrituras por cola, lecturas por HTTP | La cubicación es lectura sin efectos: es la excepción HTTP declarada |

**Cotifly ya cumple las seis.** Su `clsCubicacionNexo` tiene puerto, adaptador, fallback,
fábrica, `HttpClient` tipado y traducción de errores — y agrega resiliencia con Polly que
NEXO no tiene. Su cambio no es construir un patrón: es **reapuntar un adaptador que ya está
bien hecho**, de NEXO al Cubicador, y cambiar `X-Portal-Usuario-Id` por `Authorization:
Bearer` con llave propia.

Dos decisiones que esta hoja de ruta sostiene:

- **Un solo contrato.** La misma respuesta para NEXO y para Cotifly, sin variantes.
- **Cada sistema llama por lo que es suyo.** Cubica el sistema donde nace la solicitud. El
  Cubicador no le habla a NEXO en el flujo de Cotifly.

Y una tercera, para la administración de catálogos:

- **NEXO expone la superficie de administración del Cubicador.** El portal no puede llamar
  al Cubicador (sin CORS, y la llave terminaría en el bundle), así que NEXO hace de
  passthrough con controladores delgados, uno por catálogo, cada uno con su rol.

---

## 2. El contrato

Esta sección es contra lo que programan los dos equipos. Unidades: **centímetros y
kilogramos**. Porcentajes en **0–100**. JSON en camelCase.

### 2.1 Autenticación

```
Authorization: Bearer <llave>
```

Una llave **por sistema**. `verificar_api_key` pasa de comparar contra una sola llave a un
mapa `llave → sistema`, y de ahí sale el `origenSistema` **verificado**, no autodeclarado.

### 2.2 `POST /recomendaciones`

Una llamada por ruta o línea. **Es una ruta nueva, y `POST /api/cubicacion` se retira en el
mismo corte** — no queda como endpoint de transición. NEXO y Cotifly apuntan al nuevo en el
release que lo publica.

```json
{
  "idLineaSolicitud": "8f3a1c20-5b7d-4e8a-9c21-7d4e5f6a8b90",
  "origenSistema": "COTIFLY",
  "claseViajeSeleccionada": "Carga Suelta",
  "usuarioId": "1042",
  "mercancias": [
    {
      "idTipoMercancia": "2B213679-A42E-5E30-A6AF-E3A4FB42BD92",
      "cantidad": 1200,
      "largoCm": 50,
      "anchoCm": 40,
      "altoCm": 40,
      "pesoKg": 20
    }
  ]
}
```

| Campo | Tipo | Obligatorio | Nota |
|---|---|---|---|
| `idLineaSolicitud` | string | sí | El id de la ruta en el sistema que llama, en texto |
| `origenSistema` | enum `NEXO` / `COTIFLY` | sí | Se valida contra la llave: si no coincide, 401 |
| `claseViajeSeleccionada` | string | no | **Preferencia, no filtro.** El motor corre completo y da su mejor respuesta; si no coincide con lo que el usuario marcó, lo dice en `advertencias[]`. No hay campo dedicado: se evaluó y ningún consumidor lo necesita — el portal deja de escribir la clase de viaje al formulario, así que no queda quién lo lea |
| `usuarioId` | string | no | Dato declarado por el llamante, para auditoría. El Cubicador no lo verifica |
| `mercancias[].idTipoMercancia` | string | sí | El **id** que devolvió `GET /tipos-mercancia`. Acordado con Cotifly el 2026-10-08 (su HU-021.5); reemplaza al `codigoMercancia` que proponía la primera versión de esta hoja. Se acepta en cualquier caja: es el mismo uuid |
| `mercancias[].cantidad` | entero ≥ 1 | sí | |
| `mercancias[].largoCm` / `anchoCm` / `altoCm` | decimal > 0 | sí | |
| `mercancias[].pesoKg` | decimal > 0 | sí | Por unidad, no total |

> **Cerrado el 2026-10-08.** La primera versión de esta hoja proponía mandar el **código**
> y dejaba el punto por confirmar con Cotifly. Se adoptó lo de ellos: en el request viaja
> el **id**. A cambio, la **respuesta lleva los dos** —`idTipoMercancia` y
> `codigoMercancia`, igual que `GET /tipos-mercancia`—, así que nadie tiene que resolver el
> otro por su cuenta: Cotifly persiste el id y NEXO usa el código estable (S5-HU-18.2).

### 2.3 Respuesta — caso asignado

```json
{
  "idRecomendacion": "c17b9e44-2a31-4f6b-9d02-1e8c3a5f7b64",
  "idLineaSolicitud": "8f3a1c20-5b7d-4e8a-9c21-7d4e5f6a8b90",
  "origenSistema": "COTIFLY",
  "resultado": "ASIGNADO",
  "motivoSinSeleccion": null,
  "condicionada": false,
  "mercanciasCondicionadas": [],
  "vehiculos": [
    {
      "idRecomendacionVehiculo": "a4d2...",
      "numeroVehiculo": 1,
      "codigoVehiculo": "C2",
      "nombreVehiculo": "Sencillo C2",
      "codigoCarroceria": "FURGON",
      "pesoTotalKg": 8420.0,
      "volumenTotalM3": 33.68,
      "porcentajeLlenadoVolumen": 90.0,
      "porcentajeLlenadoPeso": 99.1,
      "alertaBajoLlenado": false,
      "heterogenea": false,
      "mercancias": [
        { "idTipoMercancia": "2B213679-...", "codigoMercancia": "BEBIDAS", "cantidad": 421, "pesoKg": 8420.0, "volumenM3": 33.68 }
      ]
    },
    {
      "idRecomendacionVehiculo": "b7e1...",
      "numeroVehiculo": 2,
      "codigoVehiculo": "2S2",
      "nombreVehiculo": "Minimula 2S2",
      "codigoCarroceria": "FURGON",
      "pesoTotalKg": 15580.0,
      "volumenTotalM3": 62.32,
      "porcentajeLlenadoVolumen": 82.0,
      "porcentajeLlenadoPeso": 86.6,
      "alertaBajoLlenado": false,
      "heterogenea": false,
      "mercancias": [
        { "idTipoMercancia": "2B213679-...", "codigoMercancia": "BEBIDAS", "cantidad": 779, "pesoKg": 15580.0, "volumenM3": 62.32 }
      ]
    }
  ],
  "advertencias": [],
  "extradimension": null
}
```

**La flota puede ser mixta.** El ejemplo devuelve un Sencillo y una Minimula. Ninguna
estructura del contrato asume N vehículos iguales — eso es lo que la forma vieja no podía
representar.

**Campos siempre presentes cuando `resultado = "ASIGNADO"`.** Cada entrada de `vehiculos`
trae, sin excepción y sin `null`:

| Campo | Por qué es obligatorio |
|---|---|
| `idRecomendacionVehiculo` | Es la referencia que el consumidor persiste en su camión |
| `codigoVehiculo` | Es lo que se reenvía a NEXO al crear la solicitud |
| `codigoCarroceria` | Idem |
| `pesoTotalKg` | **Es el peso que Cotifly le manda a NEXO por camión.** No alcanza con el porcentaje |
| `volumenTotalM3` | |
| `porcentajeLlenadoVolumen` y `porcentajeLlenadoPeso` | Los dos, siempre |

Está declarado así a propósito: cada lado puede validar que no le llegó algo incompleto en
vez de aceptar un `null` silencioso. Es la misma clase de fallo que describe la sección 5.

### 2.4 Respuesta — sin selección automática

```json
{
  "idRecomendacion": "...",
  "resultado": "SIN_SELECCION_AUTOMATICA",
  "motivoSinSeleccion": "EXTRADIMENSIONADO",
  "condicionada": false,
  "vehiculos": [],
  "advertencias": ["2 pieza(s) exceden las medidas estándar."],
  "extradimension": {
    "hayExtradimension": true,
    "medidasEstandar": { "largoMaxCm": 1220, "anchoMaxCm": 260, "altoMaxCm": 300, "pesoMaxKg": 34000 },
    "piezas": [
      { "idTipoMercancia": "9F2C...", "codigoMercancia": "VIGAS_ESTRUCTURAS", "excesos": ["largo 1400.0 cm > 1220.0 cm"] }
    ]
  }
}
```

Valores de `motivoSinSeleccion`: `EXTRADIMENSIONADO`, `SIN_CANDIDATOS_DIMENSIONALES`,
`SUPERA_K_MAX`, `SIN_CARROCERIA_COMPATIBLE`.

### 2.5 Respuesta — condicionada

Igual que el caso asignado, con:

```json
{
  "condicionada": true,
  "mercanciasCondicionadas": ["BOBINAS_ACERO", "PAPEL_ROLLOS"]
}
```

**No es un error.** La recomendación sale normal y además queda marcada. El consumidor
manda la solicitud completa a la Mesa de Costeo; el Cubicador no espera ninguna aprobación.

### 2.6 Esto es clave: lo que antes era error ahora es resultado

`SIN_SELECCION_AUTOMATICA`, `CONDICIONADA` y `EXTRADIMENSIONADO` vuelven con **200**,
dentro de `resultado`. Los códigos de error quedan solo para fallos reales:

| HTTP | Código | Cuándo |
|---|---|---|
| 400 | `MERCANCIAS_REQUERIDAS` | Lista vacía |
| 400 | `MERCANCIA_DESCONOCIDA` | Un código que no está en el catálogo. **Nunca un default silencioso** |
| 400 | `MERCANCIA_NO_CUBICABLE` | El tipo existe con `Cubicable = false` (graneles, combustibles) |
| 400 | `DIMENSIONES_INVALIDAS` | Medida o peso fuera de rango |
| 401 | `NO_AUTORIZADO` | Bearer ausente, inválido, u `origenSistema` que no corresponde a la llave |
| 503 | `CUBICADOR_NO_DISPONIBLE` | Base caída u otro fallo de infraestructura |

Se mantiene el formato que el Cubicador ya usa: `{ "codigo": "...", "mensaje": "..." }`.
NEXO lo traduce a su `clsApiError`; Cotifly a su `clsCodigosError`. **Se extiende el
catálogo existente, no se inventa uno nuevo.**

### 2.7 Catálogos

```
GET /tipos-mercancia
GET /tipos-carroceria
```

`tipos-mercancia` devuelve, por fila: `id`, `codigoMercancia`, `nombreMercancia`,
`categoria`, `cubicable`, `condicionadaCubicacion`, `carroceriaPrincipal`,
`carroceriaSecundaria`, `carroceriaSecundariaCondicionada`, `controlAmbientalRequerido`,
`apilable`, `fragil`, `rotacionLibre`, `comentarioCondicion`, `activo`.

Devuelve **id y código**: Cotifly decide cuál persiste, pero en el cable viaja el código.

`tipos-carroceria` devuelve `id`, `codigoCarroceria`, `nombreCarroceria`, `activo`.

### 2.8 Reconsulta

```
GET /recomendaciones?idLineaSolicitud={codigo}&origenSistema={NEXO|COTIFLY}
GET /recomendaciones/{idRecomendacionVehiculo}/mercancias
```

El filtro por `origenSistema` es **obligatorio**: un mismo código de línea puede existir en
los dos sistemas.

### 2.9 Invariante: append-only

Las tablas de recomendación **nunca se actualizan ni se borran**. Recalcular una ruta crea
una corrida nueva. Cotifly guarda `idRecomendacionVehiculo` dentro de una cotización y esa
fila tiene que seguir diciendo lo mismo dentro de dos años. Por eso esas tablas no llevan
`UpdatedAt`.

---

## 3. Plan y fechas

Cierre de sprint el **viernes 16**. El objetivo es que el cubicador nuevo **funcione para
empezar pruebas**, no que los cinco repositorios estén en producción.

| Fase | Qué entrega | Compuerta |
|---|---|---|
| 1 · SQL Server | Migración del motor de base con las tablas que ya existen, sin cambio funcional. El esquema lo gobiernan **scripts `.sql` versionados** | La batería de integración actual pasa contra SQL Server 2019 |
| 2 · Modelo de datos | Las 10 tablas y los seeders desde los 7 CSV | Catálogo sembrado y revisado por operaciones |
| 3 · Motor y CRUD | Partes 1, 2 y 3 del flujo con DBLF, los 6 CRUD y la persistencia append-only | 50 a 100 despachos históricos comparados |
| 4 · Contrato y consumidores | El contrato expuesto, y NEXO, el portal, Cotifly y su front adaptándose | Corrida de punta a punta desde Cotifly, con flota mixta entre los casos |
| 5 · Corte conjunto | Despliegue coordinado de los cinco repositorios | — |

**La fase 1 va primero a propósito:** migrar el motor de base **antes** de crear las tablas
nuevas evita probar todo dos veces, y si el driver async diera problemas se descubre con lo
que ya existe y no a mitad del trabajo nuevo.

**Las tres primeras fases son solo del Cubicador.** Los demás equipos entran en la fase 4.

**No hay colchón.** Si el motor no está listo a mitad de semana, no hay qué probar el
viernes. La forma de protegerlo es que la revisión de datos (sección 7) y la migración
arranquen en paralelo, no en secuencia.

---

## 4. Trabajo por repositorio

### 4.1 Cubicador (Python)

**Base de datos**
- Dialecto `mssql+aioodbc`, driver ODBC en la imagen Docker.
- Tipos neutrales en vez de UUID nativo de Postgres (`sqlalchemy.Uuid`).
- Manejo de excepciones del driver nuevo — hoy hay código que distingue `ForeignKeyViolationError` de Postgres explícitamente en `plan_cargue_repo.py`.
- `docker-compose.yml`: servicio `db` a `mcr.microsoft.com/mssql/server:2019-latest`, con healthcheck propio (el actual usa `pg_isready`).
- Los **scripts `.sql` de `docs/Database/`** reemplazan a `Base.metadata.create_all`, que nunca hace `ALTER`. Se aplican con `scripts/aplicar_sql.py`.
- Correr la batería de `testDocumentation/PRUEBAS_DE_INTEGRACION.md` contra SQL Server real.

**Modelo**
- 10 tablas: las 9 de la propuesta más la tabla padre `recomendaciones` (una fila por corrida).
- Todo en **centímetros**. `DECIMAL(18,3)` para medidas, `DECIMAL(18,2)` para kg.
- `UNIQUEIDENTIFIER` con `NEWSEQUENTIALID()`, PK no clustered.
- Nombres `snake_case` sin tildes. Collation `Modern_Spanish_CI_AI` en columnas de texto.
- `version_id_col` de SQLAlchemy en los seis catálogos (concurrencia optimista).
- Seeders desde los CSV de `docs/seeder/`, **idempotentes por código estable** (sección 7).
- **El Cubicador es el dueño de los códigos canónicos de vehículo** (`2L2`, `C2`, `C3`, `2S2`, `3S3`): son los del Excel, que además son los que entienden SICE-TAC y el tarifario. NEXO no es fuente — converge hacia ellos (sección 4.2).

**Motor** — ✅ hecho en S5-HU-17.3 (`domain/motor/seleccion_flota.py`)
- ✅ Partes 1, 2 y 3 del flujo de selección, con **DBLF** como constructor. No es un empacador nuevo: `DeepestBottomLeftFill` se agregó al registro de `domain/motor/optimizadores.py` reusando el mecanismo de puntos extremos de FFD y BFD y sus validadores.
- ✅ Validación de dominancia **por carrocería**, que falla ruidosamente. Se cumple en las 6 carrocerías con vehículo asignado.
- ✅ Comparación por `max(%volumen, %peso)`; se reportan los dos.
- ✅ `k_max = 10`, leído de `parametrosMotor`. Se aplica **por grupo de incompatibilidad**, como dice la descripción de ese parámetro en la tabla.
- ✅ Clase de viaje **derivada** de la carrocería. Corrección sobre lo escrito acá: en el catálogo no existe un `CONTENEDOR` suelto — son `CONTENEDOR_SECO` y `CONTENEDOR_REFRIGERADO`, así que se compara por **prefijo**.
- ⬜ Persistencia append-only en las tablas de recomendación → **va en S5-HU-17.4**, con el contrato. El motor devuelve el resultado; no escribe.

**Lo que la implementación agregó sobre lo escrito acá**
- **Las mercancías incompatibles se reparten en vehículos distintos**, no vetan la carga. El motor viejo la rechazaba entera. La regla es por **pares de categorías concretas**, así que no sirve el `grupo_compatibilidad` del motor 3D, que separa cualquier par de grupos distintos.
- **La carrocería de respaldo entra también cuando la principal no tiene vehículos asignados**, no solo cuando no hay carrocería común. Sin esto, `CARGA_CONTENERIZADA` (`CONTENEDOR_SECO`) y `VIDRIO_PLANO` (`PORTAVIDRIOS`) se quedaban sin recomendación.
- **DBLF ordena las orientaciones por cuál embaldosa mejor el vehículo.** Medido: tomando la orientación original primero quedaba 6,0 puntos porcentuales por debajo del óptimo de rejilla en promedio y 28,8 en el peor caso — pedir dos camiones donde alcanza uno. Con la preferencia, la brecha es cero en las 20 combinaciones medidas, y por eso **no hace falta Maximal Empty Spaces** (`scripts/medir_empaque_dblf.py`).
- **La validación contra 50 a 100 despachos históricos no se pudo hacer**: los datos no existen en `db_NEXO`. De 3.122 líneas, las que traen `Dimensiones` con contenido real son 182 y todas son la misma fila de prueba; con dimensiones y vehículo elegido hay 4; con posiciones capturadas, ninguna ligada a una línea. La captura de dimensiones por mercancía entra con este contrato, así que el histórico anterior no la tiene. Ver `DECISIONES_TECNICAS.md` #13.

**Contrato y catálogos**
- `POST /recomendaciones`, `GET /tipos-mercancia`, `GET /tipos-carroceria`, `GET /recomendaciones`, `GET /recomendaciones/{id}/mercancias`.
- Los 6 CRUD de catálogo (sección 6).
- Eventos de catálogo para que NEXO proyecte mercancías y carrocerías. **Cada escritura de CRUD publica su evento**, o la proyección de NEXO queda vieja sin avisar.
- `verificar_api_key` pasa a un mapa `llave → sistema`.

**Limpieza obligatoria**
- ✅ **Presets del 3D corregidos contra el Excel** (hecho). El diccionario del puente resuelve el *nombre*, no el *dato*: traducía `2L2` → "Turbo con estacas", y ese preset seguía diciendo 6,2 m y 6.300 kg cuando el Turbo real es 4,5 m y 4.000 kg — el motor nuevo decía "cabe en un Turbo" y el 3D acomodaba dentro de una caja 38% más larga que no existe. Se corrigieron largo, ancho, alto y capacidad de las 5 configuraciones con contraparte en el Excel, **se agregó la Minimula 2S2** (faltaba por completo) y se dejaron intactas cama baja y los 3 contenedores, que no tienen contraparte. Las 122 pruebas pasan, incluidos los 26 casos dorados.
- ⚠️ **La corrección NO llega sola a las bases que ya existen.** `listar_con_siembra` siembra **por nombre** y solo agrega los faltantes: nunca actualiza una fila. Así que aplica a bases nuevas, y como los datos **se migran** (no se arranca vacío), las filas viejas y erradas viajan a SQL Server. **La migración tiene que incluir un `UPDATE` puntual de esas 5 filas.** Es la misma consecuencia del seeder idempotente que describe la sección 6: las correcciones no entran por siembra.
- El nombre "Tractocamion furgon 13.6 m" quedó desfasado — son 12,2 m. No se renombró porque la siembra es por nombre y crearía duplicados; el renombre va por CRUD.
- ✅ El catálogo quemado de SICCE (`cubicacion_rapida.py::CATALOGO`, 9 arquetipos) **no se corrigió a propósito**: lo reemplaza el motor nuevo leyendo de base, que ya está escrito (S5-HU-17.3). El módulo entero se retira en S5-HU-17.4, junto con `POST /api/cubicacion`.
- **Matar el fallback silencioso.** Hoy `_PRESET_POR_UNIDAD_RAPIDA.get(unidad, "Tractocamion furgon 13.6 m")` asume un furgón de 13,6 m cuando no reconoce el vehículo. Pasa a rechazo explícito.
- **Reescribir el puente `crear_plan_desde_linea`** y sus pruebas. Se simplifica: hoy adivina cuántos vehículos hacen falta llenando uno y viendo qué sobra, hasta 20 veces. El motor nuevo ya responde cuántos y de qué tipo, así que la cascada a ciegas se reemplaza por "crear N planes con los vehículos indicados y optimizar cada uno". Es menos código del que hay.
- Documento de deuda diferida del 3D.

> La rápida se llama desde **tres puntos**: `api/routers/cubicacion.py`,
> `application/cubicacion_rapida/casos_de_uso.py` y `application/plan_cargue/desde_linea.py`.
> La superficie de cambio es chica y está acotada.

### 4.2 NEXO (.NET)

- Consumir el contrato nuevo en `clsCubicacionRapidaAdapter` y sus DTOs.
- **`Dimensiones` JSON**: agregar el **código** de mercancía por posición. Toca `CapturaRequests`, `clsCapturarSolicitudCommand`, `clsPosicionCargaDto` y el contrato de `GET /api/lineas-solicitud/{id}/cubicacion`. Las líneas ya capturadas no lo tienen y se tratan como tipo desconocido — no se les asigna un default, porque el tipo arrastra la carrocería.
- **Guardar `idRecomendacionVehiculo`** en la línea de solicitud, para poder cruzar la recomendación contra lo que se despachó.
- **Cuarentena en `ResolverVehiculo`.** Hoy compara texto exacto sobre `Normalizar(tipo)|Normalizar(carroceria)` y **crea** una configuración nueva cuando no coincide: `TMULA`, `T.MULA` y `Tractomula` son tres filas distintas. Pasa a crear con `EsCatalogoOficial = false`, fuera de los desplegables, más una bandeja de normalización donde alguien la mapea o la descarta.
- **Converger `tbl_configuraciones_vehiculo` hacia los códigos del Cubicador.** Esa tabla tiene `TipoVehiculo` y `Carroceria` como texto libre y **no tiene columna de código** — verificado, y tampoco existe otro catálogo de vehículos con código en NEXO. Así que no puede ser la fuente. El camino: marcar oficiales las filas que corresponden a un código canónico, dejar el resto en cuarentena, y que el diccionario de alias mapee la basura. La cuarentena tiende a cero y los dos catálogos terminan diciendo lo mismo — **se llega al catálogo único por convergencia, no heredando los duplicados el primer día**.
- **`GrupoRutaId`: una columna nueva en `tbl_lineas_solicitud`** (`uniqueidentifier NULL`). Cuando una carga heterogénea produce varios camiones, Cotifly manda una línea por camión y hoy nada dice que esas líneas son la misma ruta: la Mesa de Costeo las muestra sueltas. **No se parte la tabla** — es vieja y la usa todo el costeo. Tres condiciones: `CapturaLineaRequest` acepta el campo opcional y lo guarda tal cual; `GET /api/solicitudes/{id}` lo devuelve en cada línea; y **el valor lo pone Cotifly, nunca NEXO**, para que el emparejamiento sea estable aunque se reenvíe. Las líneas nacidas en NEXO quedan en `NULL` y eso no debe romper nada.
- **Orden dentro del grupo.** Los servicios conexos se pegan a la línea del primer camión, así que el orden importa. Antes de agregar otra columna, revisar si `numeroLinea` —que ya incrementa en orden de captura— alcanza: si Cotifly manda las líneas de un grupo seguidas, quedan contiguas y ordenadas sin nada nuevo.
- **Mesa de Costeo: habilitar las dos opciones.** Poder editar el vehículo asignado a una línea **y** poder dejar el precio sin vehículo asignado. No forzar una sola. Es lo que hace usable el resultado `SIN_SELECCION_AUTOMATICA`: la línea llega sin vehículo y el coordinador o le asigna uno o la cotiza igual.
- **Proyectar** mercancías y carrocerías por eventos, con el patrón de `VehiculoCatalogoConsumer`.
- **Seis controladores de passthrough** para los CRUD (sección 6).

### 4.3 Portal de NEXO (Angular)

| Cambio | Por qué |
|---|---|
| Desplegable de mercancía por posición | El tipo pasa a ser obligatorio. Hoy no se captura y la segregación química depende de que alguien escriba "ácido" en la descripción libre |
| Mostrar % de volumen y % de peso | Hoy solo muestra `factorUtilizacion`, volumétrico puro |
| Cubicar desde 1 posición | Hoy `showCubicacion` exige 3, salvo en Carga Suelta |
| Quitar el patch de `claseViaje` y `claseContenedor` | El Cubicador deja de sugerirlos |
| Pintar la flota como lista real | Hoy renderiza "N × Nombre", que asume N vehículos iguales |
| Pantallas de los 6 CRUD + bandeja de cuarentena | Sección 6 |
| Mesa de Costeo: editar el vehículo de una línea **y** dejar el precio sin vehículo | Las dos opciones, no una. Es lo que hace usable el resultado "sin selección automática" |
| Mostrar las líneas de un mismo `GrupoRutaId` agrupadas | Hoy los camiones de una ruta heterogénea se ven sueltos |

### 4.4 Backend de Cotifly (.NET)

- **Reapuntar el adaptador**: `clsCubicacionNexo` deja de llamar a NEXO y llama al Cubicador, con llave propia en vez de `X-Portal-Usuario-Id`.
- **DTOs**: `clsResultadoCubicacion` y `clsSugerenciaFlota` pasan de "un vehículo o N iguales" a una lista de vehículos con dos porcentajes cada uno.
- **Catálogos**: consumir `GET /tipos-mercancia` y `GET /tipos-carroceria` con el patrón de caché de `clsCatalogosMaestrosEnCache`.
- **Guardar `idRecomendacionVehiculo`** en `CotizacionVehiculosLineaSolicitud`, una fila por camión.

### 4.5 Front de Cotifly

- Desplegable de mercancía en el formulario de cotización.
- Render de los N vehículos con su porcentaje de volumen y de peso.

---

## 5. El hito conjunto con Cotifly

Cubicador, NEXO y el portal son del mismo ciclo. Cotifly es el único consumidor fuera de
ese ciclo, y su cambio tiene que coincidir con el de NEXO.

**Por qué no puede ir después.** Hoy Cotifly le habla a `POST /api/cubicacion` **de NEXO**.
Son dos cambios acoplados: NEXO cambia la forma de esa respuesta, y Cotifly deja de llamar
a NEXO para llamar al Cubicador. Si el primero llega antes, Cotifly queda roto en el medio.

**Y el fallo es silencioso.** `clsCubicacionNexo` deserializa con `System.Text.Json`, que
ignora las propiedades que no reconoce y deja en `null` las que no vienen. **No lanza
excepción.** Cotifly obtendría `Sugerencia = null` y devolvería un resultado vacío: el
comercial ve *"Sin vehículo sugerible con los datos actuales"* y asume que la carga no se
pudo cubicar. Sin error en los logs, sin alerta, sin 500.

**Qué tiene que aterrizar junto:** el Cubicador publicando el contrato, NEXO
consumiéndolo con su portal actualizado, y Cotifly apuntando directo con su front.

**Verificación antes de desplegar**
- Corrida de punta a punta desde el formulario de Cotifly contra el Cubicador de QA, con **flota mixta** entre los casos.
- Que el `idRecomendacionVehiculo` guardado devuelva la misma recomendación al reconsultarla.
- Que un código de mercancía desconocido se rechace con mensaje claro en los dos lados.

---

## 6. Los CRUD de catálogo

**Dónde viven: en el Cubicador.** Los datos, las reglas y los endpoints están donde está el
dominio. NEXO solo pinta.

**Cómo llega el front.** El portal no puede llamar al Cubicador — sin CORS, y la llave
terminaría en el bundle, que ya pasó una vez y obligó a rotarla. Entre el formulario y el
Cubicador hay un **passthrough en NEXO**: controladores delgados, uno por catálogo, que
espejan las rutas, aplican el rol y reenvían. **No un proxy comodín** `/{*path}`: no podría
aplicar roles por endpoint y dejaría el Swagger de NEXO sin contrato.

**Roles.** NEXO marca el endpoint, el Cubicador confía en el Bearer, que autoriza *un
backend* y no una persona.

- `GET` → `[Authorize]` sin rol específico, el criterio que NEXO ya aplica a sus consultas.
- `POST` / `PUT` / `DELETE` → `[Authorize(Roles = "AdminTarifas,Admin")]`.

> No existe `AdminFletes`. Los seis roles son `CoordinadorFletes`, `LiderProceso`,
> `ServiciosLogisticos`, `Admin`, `Comercial` y `AdminTarifas`. El que encaja es
> `AdminTarifas`: ya administra reglas Solo-SICETAC y tarifas de plaza.

### Los seis, en orden de construcción

El orden lo dictan las llaves foráneas.

| # | Catálogo | Validaciones propias |
|---|---|---|
| 1 | Parámetros del motor | Solo `GET` y `PUT` por clave |
| 2 | Categorías de mercancía | Bloquear borrado si una mercancía o una regla la referencia |
| 3 | Tipos de carrocería | Bloquear borrado si una mercancía o un vehículo la referencia |
| 4 | Tipos de mercancía | Secundaria distinta de principal; sin secundaria, la bandera condicionada en `false`. Borrado lógico si tiene recomendaciones |
| 5 | Vehículos + relación | Un solo payload con el vehículo y sus carrocerías; el backend reconcilia. `UNIQUE` en el código. Validar dominancia por carrocería al guardar. Capacidad específica > 0 cuando no usa la del vehículo, con `CHECK` en base además del formulario |
| 6 | Reglas de incompatibilidad | Sin `PUT`. No admitir `(A,B)` si existe `(B,A)`. Categoría 1 distinta de categoría 2 |

### Una séptima pantalla, en NEXO

La **bandeja de normalización de vehículos en cuarentena**. Lista las configuraciones con
`EsCatalogoOficial = false` y deja mapear cada una a un código canónico o descartarla. Sin
ella, la cuarentena solo acumula.

### Tres cosas que esto arrastra

**Concurrencia.** Dos administradores abren el mismo vehículo; el segundo en guardar
escribe la fila completa con los valores que tenía al abrirla, y el cambio del primero
desaparece sin aviso. Duele más de lo normal porque ese catálogo alimenta el motor: no se
manifiesta como error sino como recomendaciones sutilmente equivocadas. Se resuelve con
`version_id_col` de SQLAlchemy: el `GET` devuelve la versión, el `PUT` la exige, y si no
coincide responde 409.

**`CreadoPor` no se puede llenar solo.** El Cubicador no sabe quién es el usuario. NEXO
envía el id **como un campo del cuerpo**, declarado como dato del llamante — el Cubicador
no lo puede verificar y conviene ser honestos al respecto.

**Seeder y CRUD conviven.** El seeder es **idempotente**: identifica por código estable,
inserta solo lo que falta y nunca sobrescribe. Consecuencia a tener presente: si alguien
**corrige** una fila en el Excel, el seeder **no** la propaga — la fila ya existe y la
salta. Las correcciones van por CRUD; solo las filas nuevas entran por seeder.

---

## 7. Los datos de siembra

Los 7 CSV viven en `cubicador/docs/seeder/`. **El seeder del equipo es la fuente**; los que
se generaron aquí fueron un punto de partida y se descartan para que no haya dos versiones.

| Archivo | De dónde sale |
|---|---|
| `vehiculos.csv` | Excel de dimensiones, en cm. Aparece la Minimula; se corrige el Turbo |
| `vehiculoCarroceria.csv` | Alto útil por combinación. Único override: `3S3 + FURGON = 30.000 kg` |
| `tiposCarroceria.csv` | Los nombres de la matriz normalizados a códigos, más `CAMA_BAJA` y `PORTACONTENEDORES` |
| `categoriaMercancia.csv` | Las 9 que respaldan mercancías reales del Excel |
| `tiposMercancia.csv` | Las 31 de la matriz |
| `reglasIncompatibilidad.csv` | `FERTILIZANTE + ALIMENTOS`, que dispara con datos reales |
| `parametrosMotor.csv` | Con `k_max = 10` |

**Las relaciones vehículo-carrocería se limitan estrictamente al Excel.** `CAMA_BAJA` y
`PORTACONTENEDORES` entran como carrocerías **sin ningún vehículo asignado**: con cuál
emparejan lo decide el Administrador de Fletes desde el CRUD, no el seeder.

### Las categorías: 9, no 13

Se usan solo las que respaldan mercancías reales. Sin filas químicas en el Excel,
`QUIMICO_OXIDANTE` y `QUIMICO_CORROSIVO` no hacen falta: entran por CRUD, junto con sus
reglas, cuando aparezca una mercancía que las necesite.

**Consecuencia a tener presente:** las dos reglas que están hoy en el código
—`oxidante + combustible` y `corrosivo + alimentos`— **no se pueden portar**. La matriz
arranca con una sola regla. Está bien que sea así, pero si alguien asume que el filtro de
segregación cubre lo mismo que antes, se equivoca.

**Lo que sí hay que decidir: qué significa `COMBUSTIBLE`.** No es una categoría que falte,
es un nombre que significa dos cosas distintas en los dos lados. En el código actual
quiere decir **material inflamable**:

```python
if any(p in texto for p in ("textil", "papel", "carton", "cartón", "madera")):
    return "combustible"
```

En el CSV parece querer decir **carburante**, que la hoja "Alcance y exclusiones" excluye
del cubicador. Cualquiera de las dos lecturas sirve; lo que no sirve es dejarla sin
decidir. **Corrección mínima: renombrarla a `CARBURANTE`**, y que los tipos que le cuelguen
queden con `Cubicable = false`. Es un renombre, cero categorías nuevas, y el papel y la
madera se quedan en `GENERAL`.

**Criterio general:** la categoría describe el **contenido**, no el empaque. Por eso "Cajas
de cartón" es `GENERAL` — el contenido es desconocido.

### Los 10 tipos excluidos

Independientes de cuántas categorías haya, y valen la pena: graneles sólido y líquido,
combustibles, químicos a granel, cemento, concreto, residuos, asfalto, vehículos como
unidad e ingeniería especial. Diez filas con `Cubicable = false`, sacadas de la hoja
"Alcance y exclusiones".

Son las que hacen que quien capture "cemento a granel" reciba un **rechazo con motivo** en
vez de una recomendación inventada. Sin ellas ese tipo no existe, y el motor no puede
distinguir "no lo conozco" de "no se cubica".

### Qué tiene que revisar operaciones

1. **`Apilable`, `Fragil` y `RotacionLibre`.** La matriz los tiene en prosa, en la columna
   "Criterio / observación": *"validar apilabilidad y resistencia del empaque"*,
   *"controlar fragilidad y orientación"*. Hay que convertir eso en tres valores por fila.
   Donde la matriz no dice nada, se deja vacío: significa "no declarado" y el motor se
   comporta como hoy. Los que sí tienen señal son frágil en electrodomésticos, vidrio y
   ventanas; rotación restringida en varillas, tuberías, bobinas, postes, rollos, vidrio,
   ventanas y cilindros.
2. **Qué significa `COMBUSTIBLE`** y el renombre a `CARBURANTE`.
3. **Los 10 tipos excluidos**, si no están ya en el seeder.

Es una sesión de una o dos horas, y es el **camino crítico** de todo lo demás.

---

## 8. Decisiones cerradas

**Motor y algoritmo**
- Dos motores separados; el 3D no se toca en esta etapa.
- Oráculo de la rápida: heurística de estanterías + **DBLF**. Maximal Empty Spaces solo si la validación histórica lo justifica, y documentado.
- Filtrar por peso, **comparar por `max(%vol, %peso)`**, reportar ambos.
- `k_max = 10`, con medición.
- Dominancia validada **por carrocería**, con fallo ruidoso.
- **Fuera** el criterio de distribución por eje y las dimensiones de puerta: no hay datos. La posición de la puerta se conserva.

**Datos**
- 5 vehículos (`2L2`, `C2`, `C3`, `2S2`, `3S3`), 14 relaciones, **un** override de capacidad.
- "Tractomula Furgón 3S3" deja de ser un vehículo: es el par `(3S3, FURGON)`. Protege el matching contra SICE-TAC.
- Alto útil en la relación vehículo-carrocería, no en el vehículo: el mismo chasis tiene alturas distintas según la carrocería.
- Refrigerado = `FURGON` + alerta, no una carrocería propia. Resuelve 6 de las 7 mercancías que caían en "sin selección".
- Clase de viaje **derivada** de la carrocería, sin columna nueva.
- Códigos estables en los tres catálogos, no uuid.
- Todo en **centímetros**: los valores del Excel quedan enteros exactos, y `parametros_cubicacion` ya estaba en cm con 1220, que es el largo de la tractomula.

**Arquitectura**
- **SQL Server 2019**, en el esquema `dbo_cubicador` de `dbTarifario_Nexo_Pruebas` (compartido con Cotifly), con **scripts `.sql` versionados** para el esquema.
- El 3D se migra tal cual, para no tener dos motores de base adentro del Cubicador.
- El puente usa un diccionario de compatibilidad `CodigoVehiculo` → preset viejo.
- Un solo contrato, **corte coordinado**, sin endpoint de transición.
- Cotifly habla **directo** con el Cubicador, con llave propia.
- Catálogos: fuente única en el Cubicador, **dos vías** — eventos para NEXO (que ya tiene la infraestructura), HTTP con caché para Cotifly (que también la tiene).
- Los catálogos se editan **solo desde NEXO**. Cotifly es de solo lectura.
- Condicionada es una **marca**, no un flujo de aprobación en el Cubicador.
- Vehículos no reconocidos en NEXO se crean **en cuarentena**, fuera de los desplegables.
- Las recomendaciones de **los dos** orígenes se persisten; NEXO también guarda el id.
- `fragil` y `rotacion_libre` entran como columnas nullable ahora; los datos llegan después.
- **Scripts `.sql` versionados** en `docs/Database/` como fuente de verdad del esquema, aplicados con `scripts/aplicar_sql.py`. Decisión revisada en S5-HU-17.2 a pedido del equipo de Datos: la propuesta original era Alembic con `--autogenerate`, pero su autogenerado lee la base **entera**, y como `dbo_cubicador` vive en la misma base que `dbo_cotifly`, la primera corrida propuso 36 `drop_table` contra tablas de Cotifly. `create_all` tampoco sirve porque **nunca hace `ALTER`**. El DDL explícito es además lo que ya usan NEXO y Cotifly, así que el equipo de Datos revisa un solo formato.
- Los CRUD viven en el **backend del Cubicador**; el portal de NEXO los consume por **passthrough**, que es donde están los usuarios y los roles. El navegador nunca llama al Cubicador.
- Los **códigos canónicos de vehículo son del Cubicador**; NEXO converge hacia ellos vía la cuarentena. No se heredan los duplicados de `tbl_configuraciones_vehiculo`.
- **`GrupoRutaId`** como columna nueva en `tbl_lineas_solicitud`, puesta por Cotifly y solo guardada por NEXO. No se parte la tabla.
- El **espacio interior del furgón refrigerado** no se modela aparte: se usa la medida por defecto del vehículo. El porcentaje queda optimista en las 6 mercancías de frío, y se acepta.
- En Mesa de Costeo se habilitan **las dos** opciones: editar el vehículo y dejar el precio sin vehículo.
- Los datos actuales **se migran** a SQL Server; no se arranca vacío.
- **Sin plan de reversa.** Si el despliegue no funciona, los otros repositorios siguen desplegados y operativos. No se construye un camino de vuelta.
- `S4-HU-12` **queda descartada**: se hizo con supuestos que este diseño reemplaza. La reestructuración arranca en una rama nueva desde QA.
- `POST /recomendaciones` es ruta nueva y `POST /api/cubicacion` **se retira en el mismo corte**.
- El contrato no lleva `claseViajeDerivada` ni `alertaClaseViaje`: se validó que ningún consumidor los necesita y el aviso viaja en `advertencias[]`.
- Objetivo de tiempo de respuesta: **10 s**, a medir en la fase 3.

---

## 9. Pendientes

| Pendiente | Dueño | Qué bloquea |
|---|---|---|
| Qué significa `COMBUSTIBLE`, y renombrarla a `CARBURANTE` con `Cubicable = false` | Operaciones | Una mercancía mal clasificada. Es un renombre |
| Agregar los 10 tipos excluidos al seeder | Operaciones | Que "cemento a granel" reciba un rechazo con motivo y no una recomendación inventada |
| Llenar `apilable`, `fragil` y `rotacion_libre` en los 31 tipos | Operaciones | Vacío significa "no declarado" y el motor se comporta como hoy. No bloquea, pero deja el dato sin dueño |
| Confirmar que el código de mercancía viaja en el cable, no el uuid | Cotifly | Su adaptador. Es una línea de su lado |
| Dimensiones y capacidad de los contenedores 40HC y 20STD | Negocio | "Carga contenerizada" cae en sin selección hasta que se resuelva. Entra por CRUD |
| Con qué vehículos emparejan `CAMA_BAJA` y `PORTACONTENEDORES` | Administrador de Fletes | Nada: se decide desde el CRUD cuando se use |
| Matriz de compatibilidad más allá de la regla sembrada | Operaciones | Nada: el resto entra por CRUD |
| Nombres finales de campos y fechas del contrato | Cotifly (HU de Duban) | La firma final. La forma está en la sección 2 |
| De dónde salen los 50-100 despachos históricos | NEXO | La compuerta de la fase 3. Salen de solicitudes entregadas con posiciones capturadas |
| Si `numeroLinea` alcanza para ordenar las líneas de un `GrupoRutaId` | NEXO | Si no alcanza, hace falta una columna de orden |

**Ninguno de esos bloquea la revisión de este documento ni la escritura de las issues.**
Siguen su curso en paralelo y se cierran antes de que el motor los necesite.

### Dependencias de ejecución

No son decisiones: son cosas que tienen que estar cuando se implemente, no antes.

- **Credenciales del SQL Server 2019 de QA.** ✅ Entregadas. `userTarifarioNexo` con DDL **acotado a `dbo_cubicador`** (`CREATE TABLE`, `ALTER`, `REFERENCES`, `VIEW DEFINITION`); sin acceso a `dbo_cotifly` ni a `dbo`. Alcanzable desde desarrollo; CI usa su propio SQL Server en contenedor.
- **La llave de Cotifly.** La genera Cotifly. Solo tiene que ser **distinta de la de NEXO**: de ahí sale el `origenSistema` verificado.
- **El `UPDATE` de las filas de vehículos existentes.** Ya escrito en `docs/seeder/corregir_vehiculos_existentes.sql`. Corre como paso de datos de la migración, después de crear el esquema y antes del primer arranque.

**Cerrados desde la primera versión:** el espacio del furgón refrigerado (se usa la medida
por defecto), cama baja y portacontenedores (entran como carrocerías sin vehículo), las
categorías (9, las que respaldan mercancías reales), el mecanismo de migraciones (revisado
en S5-HU-17.2: scripts `.sql` versionados en vez de Alembic),
dónde viven los CRUD (Cubicador, con passthrough) y los códigos de vehículo (del Cubicador,
NEXO converge).

---

## 10. Riesgos

| Riesgo | Qué pasa si ocurre | Mitigación |
|---|---|---|
| **No hay colchón en el calendario** | Si el motor no está a mitad de semana, el viernes no hay qué probar | Arrancar la revisión de datos y la migración **en paralelo**, no en secuencia |
| Cotifly y NEXO no aterrizan juntos | Cotifly deja de mostrar recomendaciones **sin un solo error en los logs** | Hito conjunto con fecha acordada, y la verificación de la sección 5 |
| El motor pide un vehículo de más | La heurística deja fuera bultos que sí cabrían. Es el único error que importa cuando el objetivo es menos vehículos | Validar contra 50-100 despachos ya ejecutados. Si infla, ahí se invierte en un constructor mejor |
| Alguien agrega un vehículo que rompe la dominancia | El algoritmo devuelve un mínimo equivocado **en silencio** | Validar la dominancia por carrocería al guardar y fallar ruidosamente |
| El driver async de SQL Server se comporta distinto bajo carga | Habría que pasar los repositorios a acceso síncrono en threadpool | Riesgo asumido: el líder lo tiene andando. Si aparece, aparece en la fase 1, con las tablas que ya existen |

---

## 11. Deuda diferida del motor 3D

Se decidió **no tocar el 3D** para no romper lo que funciona mientras se repara la rápida.
Esto es lo que quedará pendiente cuando le llegue el turno:

1. **Unificar los dos catálogos de vehículos.** Durante esta etapa conviven `vehiculos_cubicacion` (en mm, para el 3D, con su proyección en NEXO) y `vehiculos` (en cm, para el motor nuevo). Nombrarlos distinto desde el principio para que nadie los confunda.
2. **Retirar el diccionario de compatibilidad** del puente `crear_plan_desde_linea`.
3. **Decidir qué pasa con puerta y ejes.** El 3D los sigue usando; el modelo nuevo no los tiene. Son 4 de los 26 casos dorados (balance de ejes 20, 21 y 22; puerta 23). El caso 25 sobrevive: depende de la *posición* de la puerta, no de sus dimensiones.
4. **Re-evidenciar la compuerta de paridad** si esos casos cambian. Hoy sigue en 26/26 porque el 3D no se toca.
