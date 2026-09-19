# QA — S4-HU-03: 7 bugs del optimizador de plan de cargue

**Issues:** #158, #150, #151, #152, #153, #154 (Cubicador) y #155 (NEXO).
**Commit del arreglo:** `7902fdf`, autor vasco-dev. **Verificación:** bquintero.
**Fecha:** 2026-09-18. **Rama:** `S4-HU-03`, sale de `QA` en `1635df5`.

Los 6 de Cubicador los reportó jpvelez probando el **flujo normal** —vehículo y plan
propios, no el escenario demo— contra el ambiente desplegado. Esa distinción importa:
el demo nace con su vehículo y sus ítems ya puestos, y esquivaba varios de estos.

## Resultado de las suites

| Suite | Resultado |
|---|---|
| Cubicador `pytest` | ✅ 118/118 (eran 109) |
| `CabinaFletes.Application.Tests` | ✅ 142/142 |
| `ci_docker_smoke.py` (Postgres + RabbitMQ + api + worker) | ✅ 2 corridas |

## Los 6 de Cubicador, verificados contra Postgres real

Los tests unitarios corren sobre SQLite. Esto se corrió además **por la API**, pasando
por cola → worker → Postgres, que es donde se reportaron. Script:
[`verify-bugs-optimizador.py`](verify-bugs-optimizador.py).

Para cada uno se revirtió el archivo de código, se reconstruyeron las imágenes y se
volvió a correr. **Los 6 fallan sin su arreglo** — sin ese contraste, una corrida verde
no distingue entre "está arreglado" y "el escenario no ejercitaba nada":

| Bug | Con el arreglo | Sin el arreglo |
|---|---|---|
| **#158** vehículo con `toleranciaParedesMm=50` | coloca 4 unidades | **0 — rechazaba todo** |
| **#150** contadores de métricas | `violacionesCriticas: 1` | `0`, con la misma crítica presente |
| **#152** mensaje de rechazo | *"excede las dimensiones internas del vehiculo…"* | *"…espacio fragmentado o restricciones de apilamiento"* |
| **#153** `SECUENCIA_BLOQUEADA` | 1 incidencia por colocación | **2** sobre la misma colocación |
| **#154** apilamiento en cadena | carga A y B, rechaza C | carga **A, B y C** |
| **#151** flag `bloqueada` | sobrevive a reoptimizar | **0** colocaciones bloqueadas después |

### Detalle de los escenarios

**#158 — el crítico.** Un vehículo con tolerancia de paredes distinta de cero rechazaba
*cualquier* ítem, siempre, por un punto extremo semilla que nunca respetaba esa
tolerancia. Como los presets del sistema vienen con tolerancia en 0, solo aparecía al
crear un vehículo propio — exactamente el flujo que jpvelez estaba probando.

**#150.** `resultado.py::obtener_resultado` recalcula las métricas en cada lectura
reconstruyendo un `ResultadoCubicacion` desde las colocaciones crudas, pero lo armaba
sin `incidencias` (que tiene `default_factory=list`). El motor contaba sobre una lista
vacía mientras las incidencias reales viajaban a la respuesta por otro carril: de ahí
los dos contadores en 0 junto a 154 incidencias en el mismo body.

**#154.** `validar_apilamiento` solo miraba el soporte **directo** de cada unidad. Con
A (tope 250 kg) → B (200 kg) → C (60 kg), el chequeo directo de C contra B pasaba
—B no declara límite— pero nadie sumaba los 260 kg que bajan hasta A. Por eso apilar
dos unidades sí se detectaba y tres no.

**#153.** Un ítem tapado por dos bloqueadores generaba una incidencia por cada uno en
vez de una por colocación. Es lo que inflaba las 150 filas para 23 colocaciones del
reporte — y también las "150 advertencias" que se veían en #150.

## Mutación de los tests unitarios

Revirtiendo los 4 archivos de código de golpe, **6 de los 9 tests nuevos fallan**, uno
por bug. Los otros 3 son controles deliberados que deben pasar en ambos estados:
tolerancia en cero, fragmentación legítima y apilamiento dentro del límite. Sin ellos no
habría forma de saber si un arreglo rompió el comportamiento correcto.

Mapeo archivo → test, revirtiendo de a uno:

| Archivo | Bug | Test que falla |
|---|---|---|
| `application/plan_cargue/resultado.py` | #150 | `test_metricas_..._reflejan_incidencias_reales` |
| `domain/motor/optimizadores.py` | #158, #152 | `test_tolerancia_paredes_...` · `test_mensaje_sin_espacio_distingue_...` |
| `domain/motor/validadores.py` | #153, #154 | `test_secuencia_bloqueada_se_consolida_...` · `test_apilamiento_valida_max_peso_...cadena...` |
| `infrastructure/.../plan_cargue_repo.py` | #151 | `test_bloqueada_sobrevive_a_reoptimizar_...` |

Ningún arreglo queda sin cobertura.

## #155 — el de NEXO

Borrar un vehículo en uso devolvía **202** y el comando quedaba en `procesando` para
siempre. La causa es fea: Cubicador rechaza con `VEHICULO_EN_USO`, pero ese rechazo
vuelve async por `cubicacion.rechazada` con el **id del vehículo viajando en el campo
`PlanId`**; el consumer busca un plan con ese id, no encuentra fila y descarta el mensaje
en silencio.

El arreglo valida **síncrono, antes de publicar** —la fila espejo de `PlanCargue` sí vive
en la base de NEXO, así que no hace falta preguntarle a Cubicador— y devuelve **409**.

Verificado con `Application.Tests`: **142/142**, y desactivando la guarda falla
exactamente **1**. No necesita base: el test usa `Mock<IPlanCargueRepository>`.

⚠️ Eso deja **sin probar** el método nuevo `ExisteConVehiculoAsync` de
`EfPlanCargueRepository`, que el mock reemplaza por completo. Es una consulta EF de una
línea, pero solo se ejercita contra SQL Server real.

## Lo que esta verificación NO cubre

- **#155 de punta a punta**: crear un vehículo, darle un plan, borrarlo y confirmar **409**
  en vez de 202. Necesita el ambiente desplegado.
- **#158 control A/B en la UI** y **#150 métricas en pantalla**, los dos ítems que el propio
  test plan del PR dejó sin marcar.
- **Planes ya proyectados en NEXO conservan el `0` de #150** hasta que se vuelvan a
  optimizar: NEXO guarda las métricas que vienen en el evento, no las recalcula. El plan
  `0198cb60` del reporte original va a seguir mostrando 0 hasta que se le dé "Recubicar".
  Conviene verificar con un plan nuevo, o el arreglo va a parecer que no funcionó.

## Nota de montaje

Todo corrió en el `docker compose` de `cubicador/` local y efímero — Postgres propio, api
y worker propios. No se tocó ningún ambiente compartido y los contenedores se bajaron al
terminar.

Un detalle que cuesta tiempo si se repite: el `.env` de la raíz y el de `cubicador/` tenían
**contraseñas de RabbitMQ distintas**, y el broker se crea con la de la raíz. Con las dos
desalineadas el `api` queda `unhealthy` con `ACCESS_REFUSED`. Se resolvió pasando un
`--env-file` temporal con la contraseña alineada, sin tocar ninguno de los dos archivos.
