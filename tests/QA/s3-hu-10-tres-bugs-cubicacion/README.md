# QA — S3-HU-10: cerrar los 3 bugs de Cubicación encontrados en QA

**Issues:** #131 (10.1), #132 (10.2), #133 (10.3). **Autor:** bquinterobtst.
**Fecha:** 2026-09-10. **Rama:** sale de `QA` en `2af6c4c`, sin merges intermedios.

Los 3 bugs vienen de la verificación de QA de los PR #122/#126, documentados en
[`../s3-hu-01-02-03-061-cubicacion-por-colas/README.md`](../s3-hu-01-02-03-061-cubicacion-por-colas/README.md).
Los 3 seguían presentes sin tocar en `QA` — se confirmó antes de empezar.

## Resultado de las suites

| Suite | Resultado |
|---|---|
| Cubicador `pytest` | ✅ 103/103 (eran 84) |
| `ci_docker_smoke.py` (Postgres + RabbitMQ + api + worker reales) | ✅ pasa, corrido 3 veces |

No se tocó ningún archivo `.NET`: el alcance de la HU es enteramente `cubicador/`.

## Cómo se verificó cada bug

Para los tres se escribió primero el test, **se comprobó que fallara** contra el
código sin arreglar, y recién después se corrigió. Al final se revirtieron los 5
arreglos a la vez y fallaron 7 tests — un test que no falla ante la regresión que
dice cubrir no prueba nada.

### 10.1 — mensaje sin `planId` dejaba el worker atascado

El criterio de la issue pide confirmarlo con `ci_docker_smoke.py`. Pero el smoke
pasando no alcanza: el bug era que el worker quedaba atascado *después*. Se
verificó la cadena completa:

1. Se confirmó que el smoke manda exactamente el mensaje del bug —
   `{"tipo": "tipo_invalido_ci", "correlationId": "ci-dlq-test"}`, **sin `planId`**
   (`ci_docker_smoke.py:197`).
2. **`messages_unacknowledged = 0`** en `cubicador.optimizar` tras cada corrida. Es
   la evidencia directa: antes, ese mensaje quedaba sin confirmar para siempre.
3. Se reprodujo el escenario textual de la issue publicando **a mano los 2 mensajes**
   que agotan el `prefetch_count=2`. Fueron a la DLQ y `unacknowledged` siguió en 0.
4. Se corrió el smoke **otra vez después de eso** y completó el flujo E2E por cola.
   Antes del arreglo, el worker habría estado muerto.

### 10.2 — SKU nulo (contra Postgres real)

`POST /api/planes-cargue/{id}/items` con `"sku": null` explícito → **HTTP 200**, con
`sku` guardado como `''`, y el plan en `borrador` (no `error`, que era el desenlace
del bug).

### 10.3 — borrar un plan optimizado (contra Postgres real)

Plan optimizado con 3 colocaciones → `DELETE` → **HTTP 204**, y `GET` posterior →
**404**. Sin `ForeignKeyViolationError`. Después, consulta directa a Postgres:
**0 colocaciones huérfanas, 0 ítems huérfanos**.

#### El criterio "deja a NEXO y Cubicador consistentes", de punta a punta

Lo anterior prueba el lado de Cubicador. La consistencia entre las dos bases se
verificó aparte, levantando **los dos stacks completos** — NEXO (api + SQL Server)
más Cubicador (api + worker + Postgres) sobre el mismo RabbitMQ — porque inferirla
del código no es lo mismo que verla:

| Paso | Resultado |
|---|---|
| `POST /api/planes-cargue` **en NEXO** (rol Admin) | `202`, `estado: "procesando"` |
| El worker crea la fila en Postgres con el id que generó NEXO | ✅ `GET` en Cubicador → 200 |
| Ítem (con `sku: null`) + `optimizar` | `estado: optimizado`, **3 colocaciones** |
| `DELETE /api/planes-cargue/{id}` **en NEXO** | `202` |
| `GET` en NEXO / `GET` en Cubicador | **404 / 404 — consistentes** |
| Huérfanos en Postgres · `ForeignKeyViolationError` en el log | **0 · 0** |

Antes del arreglo, ese mismo recorrido terminaba con NEXO en 404 y Cubicador
conservando el plan intacto.

Dos apuntes de montaje, por si hay que repetirlo: el `.env` del repo apunta a
`Server=localhost;Trusted_Connection=True` (para `dotnet run` en Windows, no sirve
desde Docker), así que se levantó un SQL Server propio en la red `nexo_internal` y se
sobrescribió `ConnectionStrings__CabinaFletes`. Y el encabezado
`X-Portal-Usuario-Id` espera el **`IdentidadPortalId` numérico** de `Usuarios`
(`long.TryParse` en `IdentidadPortalAuthenticationHandler`), no el `Id` GUID — con el
GUID responde `401 SESION_INVALIDA`.

El `optimizar` se disparó por la API de Cubicador y no por NEXO a propósito: la
compuerta de paridad sigue cerrada (`Cubicacion__ParidadVerificada=false`) y abrirla
no es parte de esta HU. Lo que el criterio pide verificar —crear y **borrar** desde
NEXO— sí pasó por NEXO.

Esta verificación importa más que las otras dos porque los tests unitarios **no
podían** demostrarla: la suite corre sobre SQLite y `db.py` no activa
`PRAGMA foreign_keys=ON`, así que SQLite no aplica las FK. Un test que esperara la
excepción habría pasado siempre, con o sin arreglo. Los tests afirman entonces la
propiedad real (que no queden huérfanos) y esta corrida confirma que en el motor
que **sí** aplica las FK el resultado es el mismo.

## Dos defectos extra, de la misma causa, que las issues no mencionaban

### A — todo lo que fallaba *antes* del `try` de `_procesar`

Arreglar las 2 líneas del manejador cerraba una instancia del bug, no la clase.
Se escribió un test por cada punto y **los 5 fallaron**:

| Mensaje | Excepción |
|---|---|
| Body que no es JSON | `JSONDecodeError` |
| JSON válido que no es un objeto (una lista) | `TypeError` |
| Sin `correlationId` | `KeyError` |
| Cabecera `x-intentos` no numérica | `ValueError` |
| Sin `tipo`, en el camino a la DLQ | `KeyError` |

Los 4 primeros son **peores** que el bug original: ocurren antes de que exista
cualquier manejador, así que el mensaje ni siquiera llega a la DLQ. Uno solo basta
para perder un hueco de prefetch.

Verificado en vivo publicando los 6 casos contra el stack: `unacknowledged = 0`, y
en la DLQ los 2 ilegibles aparecen con **`x-intentos: 0`** — van derecho, sin gastar
reintentos que darían el mismo resultado.

### B — el mismo defecto de 10.3 en `eliminar_planes_de_linea()`

Hace un `delete()` **masivo**, y ahí es peor que en `eliminar()`: un delete masivo no
dispara la cascada del ORM, así que quedaban huérfanos los **ítems además de las
colocaciones**. No es teórico: lo llama `crear_plan_desde_linea` para reconstruir la
flota, o sea que **volver a cubicar una línea que ya tenía flota optimizada fallaba
en Postgres cada vez**.

## Un hallazgo de infraestructura de tests

Escribiendo los tests se rompieron dos tests ajenos, y ninguna rotura era una
regresión: **toda la suite compartía una sola base**. `DATABASE_URL` apunta a SQLite
`:memory:` y `db.py` usa `poolclass=StaticPool` para esa URL — necesario, porque sin
él SQLite crea una base nueva por conexión. El efecto colateral es que lo que un test
escribe sigue ahí para los siguientes.

Las dos trampas concretas: colgarle un plan a un preset lo deja "en uso" y rompe
`test_eliminar_vehiculo_borra_y_publica_vehiculo_eliminado`; agregar un vehículo rompe
`test_flujo_completo_crear_optimizar`, que afirma `len(vehiculos) == 9`.

Lo peor no es que fallen, es que **fallan en otro archivo con un error que no menciona
al culpable**. Se pierde más tiempo buscando una regresión inexistente que arreglando
el problema real.

Se cerró con un `tests/conftest.py` (no existía ninguno) que recrea el esquema antes de
cada test. Medido: **~1 s sobre la suite completa**, cero roturas. Comprobado con un test
contaminante deliberado — sin el fixture rompe un test ajeno, con él no afecta a nadie.
De paso permitió **borrar** las defensas que los tests nuevos habían tenido que
incorporar (vehículo propio, limpieza en `finally`), así que el cambio dejó el conjunto
más chico, no más grande.

## Lo que esta HU NO cierra

- **La compuerta de paridad sigue cerrada** (`Cubicacion__ParidadVerificada`), como
  corresponde: depende de S2-HU-07.3, no de esta HU.
- **No hay pipeline de CI para .NET** (ver `PENDIENTES.md`): el smoke de Cubicador sí
  corre en CI, las 3 suites de xUnit no. Ajeno a esta HU, pero sigue vigente.
