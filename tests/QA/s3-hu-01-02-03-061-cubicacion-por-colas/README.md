# QA — S3-HU-01/02/03/06.1: escritura y lectura del plan de cargue por colas

**PRs verificados:** #122 (S3-HU-02, escritura por colas), #124 (S3-HU-03, proyección de
lectura enriquecida), #123 (S3-HU-06.1, vigilante de timeout).
**Autor:** ivasco-dev (#122, #124), bquinterobtst (#123).
**Fecha:** 2026-09-09. **Verificado por:** Kevin Cano (QA).

Los tres PRs se probaron **integrados en una sola rama** (`test-pr-124` + merge de la
rama de #123), porque #124 está construido directamente encima de #122, y #123 —aunque
no toca los mismos archivos— va a convivir con ambos en `QA`. El merge de las 3 ramas
fue limpio (sin conflictos).

## Scripts de esta carpeta

| Script | Qué prueba |
|---|---|
| [`verify-flujo-plan-cargue.mjs`](verify-flujo-plan-cargue.mjs) | Flujo feliz completo: crear vehículo → plan → ítem → optimizar → `/resultado` → ajustar colocación |
| [`verify-bug-worker-atascado.mjs`](verify-bug-worker-atascado.mjs) | Bug 1: publica mensajes sin `planId` vía la API de gestión de RabbitMQ y confirma si el worker queda atascado |
| [`verify-bug-sku-nulo.mjs`](verify-bug-sku-nulo.mjs) | Bug 2: agrega un ítem con `sku: null` y confirma si tumba el plan a `error` |
| [`verify-bug-eliminar-plan-optimizado.mjs`](verify-bug-eliminar-plan-optimizado.mjs) | Bug 3: optimiza un plan y lo borra, confirma si queda inconsistencia NEXO/Cubicador |
| [`verify-vigilante-timeout.mjs`](verify-vigilante-timeout.mjs) + [`force-plan-procesando.sql`](force-plan-procesando.sql) | PR #123: fuerza un plan atascado en `procesando` y confirma que el vigilante lo marca `error` |

Los 5 se corrieron contra un stack real (SQL Server + RabbitMQ + Cubicador) durante esta verificación.

## Resultado de las suites automatizadas

| Suite | Resultado |
|---|---|
| `dotnet build` (Release) | ✅ 0 errores, 1 warning preexistente (ASPDEPR005, no relacionado) |
| Domain.Tests | ✅ 127/127 |
| Application.Tests | ✅ 128/128 |
| Api.Tests (unitarias) | ✅ 149/151 (2 skip esperados, MailSenderRealTests) |
| Api.Tests (integración, SQL Server real) | ✅ 16/16 |
| Cubicador `pytest` | ✅ 82/82 |
| `ci_docker_smoke.py` (Postgres+RabbitMQ+api+worker reales) | ✅ pasa, pero ver Bug 1 |

Todas las suites del autor pasan tal como las describen los 3 PRs. La verificación en
vivo de abajo fue más allá de esa cobertura.

## Verificación en vivo

Se levantó el stack completo real: NEXO (SQL Server + RabbitMQ compartido) + Cubicador
(Postgres + RabbitMQ compartido + worker), con la compuerta de paridad abierta.

Flujo feliz confirmado end-to-end, cruzando la cola real en ambas direcciones:
- Crear vehículo (`POST /api/vehiculos`) → proyección `GET /api/vehiculos` en NEXO
  refleja los datos completos (antes de #124 esto no existía: PR #96/#90 documentaba
  que "no hay endpoint para crear el espejo, se inserta por SQL directo" — **ya resuelto**).
- Crear plan (`POST /api/planes-cargue`) → ítem (`POST .../items`) → optimizar
  (`POST .../optimizar`) → los 6 endpoints nuevos de lectura (`/resultado`, `/metricas`,
  `/no-cargadas`, `/flota`, `/exportar`, listado) devuelven la proyección enriquecida
  real (ítems, colocaciones, métricas), sin ninguna llamada directa a Cubicador.
- Ajustar una colocación (`PATCH .../colocaciones/{id}`, bloquear) → persistida en
  Cubicador.
- Borrado optimista de un plan sin colocaciones → funciona.
- Vigilante de timeout (#123): con `CUBICADOR_TIMEOUT_PROCESANDO_MINUTOS=1` y
  `CUBICADOR_INTERVALO_VIGILANTE_SEGUNDOS=10` para acelerar la prueba, se forzó un plan
  a `procesando` con timestamp viejo y el vigilante lo marcó `error` en Cubicador **y**
  en NEXO (vía `CubicacionRechazada`) dentro del intervalo esperado. Funciona como se
  describe.

## Bugs encontrados (ninguno bloqueó las suites del autor; los 3 primeros sí importan para producción)

> ✅ **Los 3 primeros están CERRADOS** en S3-HU-10 (issues #131/#132/#133). El relato de
> abajo se deja tal como se escribió al encontrarlos: es el registro de qué se probó y
> qué se vio, y reescribirlo lo volvería inútil. La verificación del arreglo —incluida
> la confirmación con `ci_docker_smoke.py` que pedía el bug 1— está en
> [`../s3-hu-10-tres-bugs-cubicacion/README.md`](../s3-hu-10-tres-bugs-cubicacion/README.md).
>
> Al arreglarlos aparecieron **dos defectos más de la misma causa**, que no se habían
> visto acá: todo lo que fallaba *antes* del `try` de `_procesar` (body ilegible, JSON
> que no es un objeto, falta `correlationId`, cabecera `x-intentos` corrupta) dejaba el
> mensaje atascado igual que el bug 1 pero **sin llegar siquiera a la DLQ**; y
> `eliminar_planes_de_linea()` tenía el bug 3 exacto, con el agravante de que un
> `delete()` masivo tampoco dispara la cascada del ORM. Los dos van en la misma HU.

### Bug 1 — CRÍTICO: un mensaje malformado puede dejar el worker de Cubicador atascado para siempre

`cubicador/worker/main.py`, manejador de errores (`except Exception`), usa
`cuerpo["planId"]` (indexación directa) en vez de `cuerpo.get("planId")` al reportar el
fallo final tras agotar reintentos. Si el mensaje no trae esa clave, esa misma línea
lanza un `KeyError` **antes de llegar al `ack()`**, así que el mensaje queda
`unacknowledged` para siempre.

El motivo por el que esto es grave: `set_qos(prefetch_count=2)` es una decisión
deliberada (`DECISIONES_TECNICAS.md #10`, "prefetch bajo"). Verificado en vivo: **basta
con 2 mensajes que disparen este `KeyError`** para agotar el prefetch — a partir de ahí
el worker no vuelve a procesar NINGÚN mensaje nuevo, de ningún tipo (los 9 comandos
quedan bloqueados), y reiniciar el contenedor **no lo arregla**: RabbitMQ reentrega los
mensajes atascados al reconectar, vuelven a fallar de la misma forma, y se quedan
atascados otra vez (confirmado: cada reintento además duplica una copia en
`cubicacion.dlq`). La única recuperación que encontré fue purgar la cola a mano.

Confirmado reproducible con el propio arnés oficial: el job `docker-smoke` del CI
(`ci_docker_smoke.py`, el que prueba la ruta de reintentos/DLQ) dispara este `KeyError`
**cada vez que corre**, porque manda un mensaje de prueba sin `planId`. En la pipeline
de GitHub Actions esto no se nota porque el `docker compose down -v` de después destruye
el entorno igual — pero cualquier entorno más largo (QA persistente, preproducción,
producción) donde el worker siga vivo después de eso queda con esos 2 mensajes zombis
ocupando el prefetch para siempre.

Con tráfico real de NEXO esto no debería dispararse — verifiqué que el contrato de C#
siempre manda el campo `PlanId` (incluso para `guardar_vehiculo`, donde se reutiliza
para mandar el id del vehículo) — pero sigue siendo una ruta de código real y alcanzable
por cualquier mensaje que no pase por ese publicador exacto (el propio smoke test de CI,
un futuro productor con un bug, o acceso directo a la cola).

**Sugerencia (no aplicada, queda a criterio del responsable):** `cuerpo.get("planId")`
en vez de `cuerpo["planId"]` en las dos líneas del manejador de errores.

### Bug 2 — SKU nulo rompe `guardar_item` para un caso de negocio legítimo

`GuardarItemRequest.Sku` en NEXO es `string?` (opcional — no todos los ítems tienen
SKU). `GuardarItemPlanCargueInput.sku` en Cubicador es `str = ""` (Pydantic): el default
solo aplica si la clave falta, pero NEXO siempre manda la clave con valor `null`
explícito cuando el ítem no tiene SKU, y Pydantic rechaza ese `null` contra un campo
`str`. Resultado: cualquier ítem real sin SKU falla 3 veces y termina en la DLQ,
marcando **todo el plan** en estado `error` (no solo el ítem). Reproducido en vivo.

### Bug 3 — Borrar un plan optimizado siempre falla y deja NEXO y Cubicador desincronizados

`cubicador/infrastructure/persistence/repositorios/plan_cargue_repo.py::eliminar()`
borra la fila del plan sin borrar antes sus `colocaciones_cargue` — a diferencia de
`eliminar_item()`, que sí borra en el orden correcto (colocaciones → fila). Cualquier
plan que ya fue optimizado (tiene colocaciones) revienta con
`ForeignKeyViolationError`, agota reintentos y termina en DLQ.

Como el `DELETE /api/planes-cargue/{id}` de NEXO hace un "borrado optimista" (borra su
propia fila y responde 202/404 antes de saber si Cubicador lo logró), el resultado
verificado en vivo es: **NEXO cree que el plan ya no existe (404 inmediato), pero
Cubicador lo conserva intacto con todos sus datos** — sin ningún mecanismo que
reconcilie esa diferencia después.

## Algo que investigué y NO pude confirmar como bug

Durante las pruebas, un par de comandos encadenados muy rápido (crear plan → agregar
ítem, sin esperar) fallaron una vez con "el plan no existe" en Cubicador, a pesar de que
NEXO ya lo mostraba en borrador. Investigué a fondo (incluyendo una sospecha inicial de
condición de carrera con el Outbox de MassTransit, `QueryDelay=5s`) pero terminé
encontrando que, en ese caso puntual, el id de vehículo que estaba reutilizando ya no
existía tras un reinicio del volumen de Postgres que yo mismo había hecho (los presets
de vehículos se siembran de forma perezosa con id aleatorio en cada base nueva — no hay
seed determinístico). No lo reporto como bug porque no pude aislarlo de mi propia
manipulación del entorno durante la sesión de pruebas; queda anotado por transparencia,
no como hallazgo confirmado.

## Decisión pendiente

Los 3 PRs cumplen su propio criterio de cierre y pasan sus propias suites. Los bugs 1-3
son reales pero ninguno impide que las suites automatizadas pasen — quedan a criterio de
Kevin si bloquean el merge o se documentan para una corrección posterior.
