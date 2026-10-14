# Contrato del Cubicador: qué cambia y qué tiene que hacer cada equipo

Dos cambios al contrato de `POST /recomendaciones`, en la misma entrega.
Cada equipo tiene su sección: **NEXO**, **portal (front)** y **Cotifly**.

| | Qué | Por qué |
|---|---|---|
| **S5-HU-17.7** | El request lleva `codigoMercancia` en vez de `idTipoMercancia` | revierte lo acordado el 2026-10-08 |
| **S5-HU-17.8** | El detalle por camión va **por posición**, con `indicePosicion` | desbloquea #217 (una línea por camión) |

> Además se corrigió un error de cálculo que no cambia el contrato pero sí los
> números: ver [Lo que se corrigió de paso](#lo-que-se-corrigió-de-paso).

---

## Antes y después

**Request** — cambia un campo:

```diff
  "mercancias": [
-   { "idTipoMercancia": "2B213679-A42E-5E30-A6AF-E3A4FB42BD92", "cantidad": 500, ... },
+   { "codigoMercancia": "CAJAS_CARTON", "cantidad": 500, ... },
  ]
```

**Respuesta** — una fila por posición, no por código. Con dos posiciones del
mismo tipo y medidas distintas:

```diff
  "mercancias": [
-   { "idTipoMercancia": "...", "codigoMercancia": "CAJAS_CARTON", "cantidad": 800, "pesoKg": 10900.0 }
+   { "indicePosicion": 0, "idTipoMercancia": "...", "codigoMercancia": "CAJAS_CARTON", "cantidad": 500, "pesoKg": 10000.0 },
+   { "indicePosicion": 1, "idTipoMercancia": "...", "codigoMercancia": "CAJAS_CARTON", "cantidad": 300, "pesoKg":   900.0 }
  ]
```

**Tres reglas nuevas que valen para todos:**

1. `indicePosicion` es el índice **base 0** en el arreglo `mercancias` del request.
2. Dos filas del mismo camión **pueden compartir `codigoMercancia`**. Quien
   agrupe por código **tiene que sumar**.
3. `indicePosicion` viaja en `null` al reconsultar una corrida guardada antes de
   este cambio. No es un error: esa corrida se guardó agrupada por código.

La respuesta **no devuelve medidas**: el llamante ya las tiene, las mandó él, y
el índice se las devuelve sin duplicarlas.

---

## NEXO

Todo está en `src/` y `tests/` de este mismo repositorio.

### Request — `codigoMercancia`

| Archivo | Qué |
|---|---|
| `src/Nexo.Api/Cubicacion/clsCubicacionController.cs` | `clsMercanciaSolicitadaRequest`: `string IdTipoMercancia` → `string CodigoMercancia`, y lo mismo dentro de `ADto()` |
| `src/Nexo.Application/CasosDeUso/Cubicacion/DTOs/CubicacionRapidaDtos.cs` | `clsMercanciaSolicitadaInput`: mismo renombre |

### Respuesta — `indicePosicion`

| Archivo | Qué |
|---|---|
| `CubicacionRapidaDtos.cs` | `clsMercanciaDeVehiculoDto` y `clsPiezaFueraDeMedidaDto` ganan `int? IndicePosicion` **como primer parámetro** |

`int?` y no `int`: llega `null` al reconsultar corridas viejas.

### Comentarios que quedaron desactualizados

Ninguno cambia comportamiento, pero los tres dicen algo que ya no es cierto y
alguien los va a leer como si lo fuera.

| Archivo | Qué dice hoy | Qué pasa |
|---|---|---|
| `src/Nexo.Domain/Cubicacion/Entidades/clsTipoMercanciaCatalogo.cs` | que `Id` *"es la llave que viaja en `idTipoMercancia` por toda la Captura y el contrato de cubicación"* | ya no es cierto para el contrato: viaja `codigoMercancia`. La entidad no cambia, sigue teniendo los dos campos |
| `src/Nexo.Domain/Solicitudes/Entidades/clsLineaSolicitud.cs` | que `GrupoRutaId` *"lo pone SIEMPRE Cotifly, nunca NEXO"* | ahora también lo pone el portal al crear una línea por camión. Ver la sección del front |
| `src/Nexo.Api/Solicitudes/CapturaRequests.cs` | lo mismo: *"SIEMPRE lo manda Cotifly"* | igual |

Los dejé sin tocar a propósito: son fuente de NEXO y esta entrega es del
Cubicador. El `CONTRATO.md` de la raíz sí quedó corregido en los tres sitios
donde lo decía.

### El adaptador: nada que hacer

`clsCubicacionRapidaAdapter` serializa con
`new JsonSerializerOptions(JsonSerializerDefaults.Web)` y `PostAsJsonAsync`, así
que renombrar la propiedad cambia el campo JSON solo. No hay ningún literal
`"idTipoMercancia"` dentro del adaptador.

### Pruebas

| Archivo | Qué |
|---|---|
| `tests/Nexo.Api.Tests/Cubicacion/clsCubicacionControllerE2ETests.cs` | las tres ocurrencias de `idTipoMercancia = ...` pasan a `codigoMercancia = ...`; la del caso negativo puede quedar como código inexistente, p. ej. `"NO_EXISTE"` |
| `tests/Nexo.Api.Tests/Cubicacion/clsCubicacionRapidaAdapterTests.cs` | el `Assert.Contains` del literal `idTipoMercancia` pasa a `codigoMercancia`. Es justo la prueba que verifica el nombre del campo en el cable |
| `tests/Nexo.Api.Tests/Infra/clsMercanciaDelCubicador.cs` | elige la mercancía por `id`: `IdSemilla = "m-CAJAS_CARTON"` y el `FirstOrDefault` leen el campo `id`. Pasan a leer `codigoMercancia`, con la semilla `"CAJAS_CARTON"` |
| `tests/Nexo.Api.Tests/Cubicacion/clsContratoCubicacionTests.cs` | revisar; hoy menciona `CodigoMercancia` pero no `IdTipoMercancia` |

> **`clsMercanciaDelCubicador.cs` todavía no está en QA.** Llegó con la rama de
> despliegue de Ismael (*fix: el PUT de carrocerías publica la fila entera...*,
> 2026-10-14), junto con un arreglo real a `cubicador/api/routers/catalogo.py`.
> No toca los mismos archivos que esta entrega, así que no hay conflicto de
> merge, pero hay que acordarse de los dos lados cuando esa rama entre.
>
> De paso, ese helper queda **más simple** con el código: elige la mercancía
> leyendo el mismo campo que después manda.

### Documentación de NEXO

`CONTRATO.md` (raíz) ya quedó actualizado en esta entrega: §3.1 y la tabla de
§3.14. No hay que volver a tocarlo.

---

## Portal (front)

Nada de esto vive en este repositorio: va en el repositorio del portal. Lo que
sigue es lo que el contrato le garantiza.

### Al pedir la recomendación

- Mandar `codigoMercancia` (el que devuelve `GET /tipos-mercancia`), no el uuid.
- `idLineaSolicitud` puede ser un **GUID temporal generado por el front**. El
  Cubicador no comprueba que la línea exista: es `nvarchar(100)` sin llave
  foránea ni unique, y lo único que valida es que no venga vacío. El vínculo
  duradero no es ese id, es el `idRecomendacionVehiculo` que se guarda en la
  línea.

### Al crear las líneas

Negocio ya lo decidió (#217, cerrada el 2026-09-26): **una línea de solicitud
nueva por cada camión recomendado**, cada una asociada a su propia línea de
cotización. No se guarda "el camión elegido": se guardan todos.

Por cada línea que cree, el front llena:

| Campo | Valor |
|---|---|
| `idRecomendacionVehiculo` | el de **ese** camión |
| `grupoRutaId` | **un GUID que genera el front**, igual en las N líneas de la misma recomendación |
| `numeroLinea` | 1, 2, 3… el orden dentro de la solicitud |

> **`numeroLinea` no es opcional en la práctica.** Es lo que define cuál línea
> del grupo es "la primera", que es donde se cuelgan los servicios conexos de la
> ruta. Si va en `null`, los conexos no saben dónde ir.

> **El contrato decía que `grupoRutaId` "lo pone SIEMPRE Cotifly".** Eso se
> escribió en #275, cuando el único origen de carga heterogénea era Cotifly. La
> captura del portal es un segundo origen que no existía entonces. **NEXO lo
> guarda tal cual y nunca lo genera** —en los tres comandos que lo tocan
> (`clsCapturarSolicitudCommand`, `clsCargarLineasBulkCommand` y
> `clsCrearSolicitudCommand`) siempre es `GrupoRutaId = lr.GrupoRutaId`—, así
> que lo pone quien crea las líneas del grupo. El texto del contrato ya se
> corrigió.

### Al repartir las posiciones entre camiones

Para eso es `indicePosicion`: dice cuántas unidades de **cada posición
capturada** van en cada camión. Las medidas las tiene el front, porque las mandó.

Cuidado con las tres reglas de arriba, sobre todo la 2: no se puede asumir que
`codigoMercancia` es único dentro de un camión.

### El desplegable de tipo de mercancía

- Mostrar las **31** mercancías de `GET /tipos-mercancia`.
- Ocultar solo las de `cubicable: false`. **Hoy no hay ninguna** —las 31 son
  cubicables— pero puede cambiar por el CRUD sin tocar código, así que el
  desplegable tiene que respetarlo.
- **El aviso va después, no en el desplegable.** Hay 7 mercancías
  *condicionadas* (`VIGAS_ESTRUCTURAS`, `BOBINAS_ACERO`, `POSTES_CONCRETO`,
  `PAPEL_ROLLOS`, `VIDRIO_PLANO`, `VENTANAS_FACHADAS`, `CILINDROS_GAS`) y **no
  son un error**: vuelven con **200**, con `condicionada: true` y
  `mercanciasCondicionadas` lleno. Ahí se muestra el `comentarioCondicion` de
  cada una. El motor sí recomendó vehículo; solo avisa que la carrocería elegida
  es la secundaria.

### Ojo con el checklist de Cotifly

`COTIFLY-CONEXION-RECOMENDACION-VEHICULO.md` manda a consultar `GET /vehiculos`
para el desplegable de tipo de vehículo. **Ese no es el catálogo canónico**: es
el de presets del motor 3D, que devuelve nombres como `"Turbo con estacas"`. El
de los códigos `2L2` / `C2` / `3S3` es **`GET /vehiculos-catalogo`**. Es el
problema de los dos catálogos de `cubicador/docs/DEUDA_DIFERIDA_3D.md` §1.

---

## Cotifly

Vive en el repositorio `Cotifly/backend-dotnet`.

**Hoy no manda el tipo de mercancía:** `clsItemCubicacion.TipoMercancia` va
siempre en `null`, a propósito, y no existe selector en el formulario de
captura. Así que **el cambio del request no les rompe nada**: cuando construyan
el campo, lo construyen directamente contra `codigoMercancia`.

Cuando conecten:

| Qué | Dónde / cómo |
|---|---|
| Tipo de mercancía por posición | el desplegable sale de `GET /tipos-mercancia`; se manda el `codigoMercancia` de esa misma respuesta |
| Llave de API | `Authorization: Bearer`. Para `/recomendaciones` **no sirve la `CUBICADOR_API_KEY` genérica**: va `CUBICADOR_API_KEY_COTIFLY`, porque de la llave sale el `origenSistema` verificado |
| `origenSistema` | sale de la llave, no del cuerpo. Si el cuerpo lo declara y no coincide, es **401** |
| Reparto entre camiones | `indicePosicion`, igual que el portal |

El resto del checklist de `COTIFLY-CONEXION-RECOMENDACION-VEHICULO.md` sigue
vigente, con la corrección de `GET /vehiculos-catalogo` señalada arriba.

---

## Base de datos

Un solo script, **aditivo e idempotente**:

```
cubicador/docs/Database/S5-HU-17.8_posicion_de_mercancia.sql
```

Agrega `recomendacionVehiculoMercancia.IndicePosicion int NULL` en
`dbo_cubicador`. Nullable a propósito: las corridas anteriores no tienen
posición y no se rellenan —esas tablas son append-only—. `NULL` significa
"corrida anterior al cambio".

> **`scripts/aplicar_sql.py` lee `DATABASE_URL` del `.env`, que apunta a QA
> corporativo (`10.10.40.85`).** Aplicarlo allá tiene que ser una decisión
> consciente, no un comando de paso. Mientras no se aplique,
> `verificar_mapeo_catalogo.py` reporta —correctamente— que el modelo declara
> una columna que la base no tiene.

---

## Orden de despliegue

El Cubicador **no acepta los dos nombres de campo a la vez**: un request con
`idTipoMercancia` responde **422**, verificado contra el servicio corriendo. Es
a propósito —un campo que acepta dos formas del mismo dato es la deuda que esto
viene a cerrar—, pero obliga a un orden:

1. **Aplicar el DDL** en la base del Cubicador.
2. **Desplegar NEXO** con `codigoMercancia`.
3. **Desplegar el Cubicador.**

Entre 2 y 3 hay una ventana corta en la que `POST /api/cubicacion` de NEXO
devuelve error. Se aceptó porque hoy **no hay tráfico real** en esa ruta: el
portal todavía no la consume y Cotifly manda el tipo en `null`. **Si eso ya no
fuera cierto cuando se despliegue, hay que revisar la decisión.**

La alternativa, si la ventana no sirve, es que el Cubicador acepte los dos
campos por un sprint y se retire `idTipoMercancia` después.

---

## Lo que se corrigió de paso

No cambia el contrato, pero sí los números que viajan.

El motor identificaba cada línea por su **código**, y el peso y el volumen por
camión se calculaban agrupando por código **antes** de multiplicar por la
cantidad. Con dos posiciones del mismo tipo, la segunda pisaba a la primera y
sus valores unitarios se aplicaban a todas las unidades: **10 cajas de 20 kg más
5 de 2 kg reportaban 30 kg donde había 210.**

`pesoTotalKg` es, por contrato, el peso con el que se costea el flete, así que
el error llegaba a la cotización dentro de una respuesta 200 de forma correcta.

El empaque y la elección de vehículo **nunca estuvieron mal**: los ítems se
construyen por línea con sus medidas reales y la flota mínima suma los totales
por línea. Lo roto era el reporte.

También cambió `heterogenea`: ahora cuenta códigos **distintos** y no filas, así
que dos posiciones del mismo producto en un camión ya no lo marcan como carga
heterogénea.

---

## Cómo se verificó

Contra SQL Server real, con el stack local:

| | |
|---|---|
| Suite del Cubicador | **247** pruebas |
| `scripts/verificar_contrato.py` | 34/34 |
| `scripts/verificar_crud_catalogos.py` | 31/31 |
| `scripts/verificar_mapeo_catalogo.py` | 1 diferencia esperada: el `IndicePosicion` pendiente en QA |
| DDL | aplicado dos veces seguidas en la base local: crea, y la segunda vez dice que ya existía |
| Compatibilidad | 43 filas sin posición releídas por la API: vuelven con `indicePosicion: null` y 200 |

`cubicador/docs/openapi.json` está regenerado.

El porqué de cada decisión está en `cubicador/docs/DECISIONES_TECNICAS.md`:
**#17** (el código en el cable), **#18** (la identidad es la posición) y **#19**
(el detalle por posición).
