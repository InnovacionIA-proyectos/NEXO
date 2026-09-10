# Revisión Sprint 2+3 (NEXO) + propuesta para cerrar el MVP end-to-end

Borrador para revisión — nada se ha creado en GitHub ni Notion todavía. Versión
actualizada con tus decisiones y con el archivo `HALLAZGOS-QA-CUBICACION.md`
que adjuntaste, más lo que se encontró revisando el PR #127 (ya mergeado a QA
después del #126).

## 0. Aclaración de alcance — Login

No se toca. El login (identidad delegada por el portal vía
`X-Portal-Usuario-Id`, resolución de los 6 roles) ya está implementado y
funcional — no es Azure AD directo, esa vía se descartó, y no forma parte de
ninguna propuesta de este documento. Donde antes decía "Login (Azure AD vía
portal)" en el mapa del flujo, se corrige a "Login (ya implementado y
funcional, identidad delegada del portal)". Lo único pendiente alrededor del
login son dos cosas puntuales que ya estaban abiertas como issues de Sprint 2
— la suite de pruebas de seguridad (#12) y el hallazgo de suplantación (#100,
que se deja como riesgo aceptado, ver sección 2) — ninguna de las dos es
"construir el login".

El flujo completo del PRD queda así: Login (ya funcional) → Bandeja
priorizada → Captura de solicitud → Mesa de costeo → Aplicación masiva →
Cubicador 3D → Cotización → Biblioteca → Tablero.

## 1. Novedad — Cubicación avanzó más de lo que sabíamos, incluso desde la última revisión

NEXO#126 (QA → main) sigue abierto esperando revisión, pero después de
abrirlo se mergeó otro PR más a QA: #127, que cierra 3 huecos adicionales que
ni siquiera eran issues:

- **`eliminar_vehiculo` (9º comando)** — S3-HU-02 dejó 8 comandos; faltaba
  este. Sin él, `eliminarVehiculo()` del frontend seguía sin ningún camino (y
  con el CORS ya cerrado en S3-HU-05, se había quedado sin ruta por ningún
  lado).
- **`vehiculo.eliminado` (3er evento de catálogo)** — sin este evento, borrar
  un vehículo lo sacaba de Cubicador pero lo dejaba fantasma en la proyección
  de NEXO: el `GET /api/vehiculos` lo seguía ofreciendo y `crear_plan` fallaba
  con `VEHICULO_INVALIDO`.
- **Corrección de una condición de carrera real** — la fila espejo de
  `crear_plan` ahora nace en "procesando", no en "borrador". Esto es un
  cambio de contrato (el campo `estado` del 202 cambia de valor) —
  documentado explícitamente en `docs/mensajeria/CUBICACION.md` §6.2,
  cumpliendo el compromiso de avisar cualquier cambio de contrato que ya se
  había puesto en S3-HU-02.6.

Inventario real de cobertura para S3-HU-04 (`docs/mensajeria/CUBICACION.md`
§8): de las 20 llamadas del frontend, 16 ya tienen camino en NEXO, 4 no:
`desde-linea/{lineaId}`, `por-linea/{lineaId}`, `{id}/colocaciones` y
`POST /api/cubicacion`. Esta última no encaja en el patrón de comando (no hay
qué persistir) ni en el de proyección (no hay qué proyectar) — necesita una
decisión de arquitectura antes de que nadie le escriba código. Se deja
señalada en la sección 5 (S3-HU-04) para resolverla o decidir a quién
preguntarle.

Corrección de documentación: `PENDIENTES.md`/`CONTRATO.md` afirmaban que el
frontend "ya consume solo los endpoints de NEXO, confirmado en QA" — falso,
verificado por `git grep` contra `origin/main` del frontend: S3-HU-04 no
tiene ni rama ni commits todavía. Ya lo corrigieron ellos mismos en el
PR #127.

Confirmado explícitamente por QA (Kevin) en el propio #127: los 3 bugs de la
ronda anterior siguen intactos, sin tocar por este PR — siguen siendo el
alcance completo de S3-HU-10 (sección 5).

Con el detalle exacto del archivo `HALLAZGOS-QA-CUBICACION.md`, esto es lo
que hay que corregir, sin adivinar nada:

| # | Archivo | Causa exacta | Efecto |
|---|---------|---------------|--------|
| 🔴 1 — Crítico | `cubicador/worker/main.py`, manejador de errores | Usa `cuerpo["planId"]` (indexación directa) en vez de `cuerpo.get("planId")`; si falta la clave, `KeyError` antes del `ack()` | Con `prefetch_count=2`, 2 mensajes sin `planId` bastan para atascar el worker para siempre (los 9 tipos de comando). Reiniciar el contenedor no lo arregla — RabbitMQ reentrega y vuelve a fallar. El propio `ci_docker_smoke.py` (job `docker-smoke`) lo dispara en cada corrida; en GitHub Actions no se nota porque el `docker compose down -v` destruye el entorno después — en QA/producción persistente sí queda atascado. |
| 2 | `PlanesCargueController.cs` (`GuardarItemRequest.Sku`, `string?`) vs. `cubicador/api/dtos/plan_cargue.py` (`GuardarItemPlanCargueInput.sku: str = ""`) | Pydantic rechaza un `null` explícito contra un campo `str` — el default `""` solo aplica si la clave falta, no si llega en `null` | Un ítem sin SKU (caso de negocio válido) falla 3 veces, termina en DLQ, y marca todo el plan como error. |
| 3 | `cubicador/infrastructure/persistence/repositorios/plan_cargue_repo.py::eliminar()` | Borra la fila del plan sin borrar antes sus `colocaciones_cargue` (a diferencia de `eliminar_item()`, que sí respeta el orden) | Cualquier plan ya optimizado revienta con `ForeignKeyViolationError`. Como NEXO hace borrado optimista (responde 404 sin esperar confirmación), NEXO cree que el plan no existe, Cubicador lo conserva intacto — sin reconciliación. |

## 2. Los 2 hallazgos críticos — estado según decisión

### 🔴 administracion_embarqa_nexo#66: 5 de 6 roles no pueden ni entrar al módulo — SIGUE COMO PRIORIDAD #1

`NexoPortalPermisoGuard` compara contra exactamente `'Nexo'`/`'AdminNEXO'`; el
backend real manda `ComercialNEXO`, `AdminTarifasNEXO`, etc. — ninguno calza.
Arreglo de una línea (`endsWith('NEXO')`), desbloquea en cascada #21, #23, #31
y #43. Entra a este sprint como S3-HU-07.1.

### 🟡 NEXO#100: suplantación de identidad — QUEDA FUERA DE ESTE SPRINT, riesgo aceptado

Confirmado: se documenta como riesgo aceptado del piloto y se pospone. No se
genera ningún issue para esto. Si más adelante se decide retomarlo, ya queda
registrado en este documento con el análisis completo para no repetir el
trabajo de diagnóstico.

## 3. Hallazgos que cambian el orden, no bloquean

- NEXO#42 (despliegue del backend) ya está **CERRADO**. El backend nuevo ya
  está desplegado con healthchecks verdes.
- administracion_embarqa_nexo#30 (despliegue del portal) sigue sin ningún
  avance.
- NEXO#3 (3 suites contra la BD corporativa): ya tienen la BD corporativa
  (punto 5) — deja de ser un punto de decisión y se vuelve el trabajo
  concreto de S3-HU-11.1 (sección 5): construir el CI/CD de la API para que
  corra contra ella de verdad.
- NEXO#43 (verificar reglas de negocio con datos reales): ya no bloqueada por
  despliegue; sigue necesitando acceso a la matriz SICE-TAC vigente y al
  histórico real.

## 4. Lo que NO entra a este sprint

- S2-HU-11 (EDA + propuesta de modelos) — ciencia de datos para un futuro
  modelo predictivo, no parte del flujo transaccional.
- S2-HU-07.4 (segunda estrategia de optimizador) — la paridad ya se logró con
  la primera; es redundancia demostrativa.
- QA suelto de Sprint 2 (#23, #34, #38, #25, #27, #29) — confirmado dejarlo
  abierto tal cual está: QA sigue trabajando en ello en paralelo, sin
  bloquear nada de lo de abajo. No se crea ninguna HU nueva para agruparlo;
  sigue viviendo donde ya vive.
- NEXO#100 — ver sección 2.

## 5. HUs nuevas para Sprint 3 — todas ADITIVAS, nada de lo existente se toca ni se edita

Numeración continúa después de S3-HU-06 (Cubicación, ya construida) y
S3-HU-10 (bugs de Cubicación). Ningún issue, milestone ni label existente se
modifica — esto es exclusivamente lo que se añade.

### S3-HU-07 — Desbloquear el acceso: los 6 roles entran

RESPONSABLE HU: Kevin Cano (@kcano-ux). Repos: `administracion_embarqa_nexo`
(fix del alias) + `NEXO` (suite de seguridad). Talla: S. Nota: la identidad
(#100) queda fuera por decisión de negocio (sección 2) — esta HU ya no
incluye ese frente.

- **S3-HU-07.1** (`administracion_embarqa_nexo`) — Corregir
  `NexoPortalPermisoGuard` y `navigation.component.ts` para aceptar cualquier
  rol que termine en `NEXO`. Verificar contra los 6 roles reales con cuentas
  corporativas reales (repetir la prueba de #66).
- **S3-HU-07.2** (`NEXO`) — Construir la suite automatizada rol×endpoint que
  pide #12 (matriz de 6 roles + las 2 pruebas negativas: header de rol
  falsificado ignorado, Comercial vs funciones de AdminTarifas denegado).

### S3-HU-08 — Desplegar el portal y verificar el flujo completo con datos reales

RESPONSABLE HU: Juan Pablo Velez (@jpvelez-bts) con Duban Guerra
(@dguerraBSSL) — despliegue de frontend es responsabilidad de ambos, según el
punto 3. Backend/automatización-IA NO participan de esta HU (su alcance de
despliegue es solo el lado backend, ver S3-HU-11). Depende de S3-HU-07.1 (sin
los 6 roles, verificar "con datos reales" no prueba nada representativo).

- **S3-HU-08.1** (`administracion_embarqa_nexo`) — Cerrar #30: build de
  producción del portal con el módulo cabina, configurado contra el backend
  nuevo (ya desplegado), sesión corporativa y push funcionando en el
  ambiente.
- **S3-HU-08.2** (`NEXO`, QA — Kevin) — Cerrar #43: verificación por
  muestreo de las reglas de negocio contra datos reales ya en el ambiente.
- **S3-HU-08.3** (`administracion_embarqa_nexo`, QA — Kevin) — Cerrar #31:
  checklist funcional de punta a punta, ahora que los 6 roles entran.

### S3-HU-10 — Cerrar los 3 bugs de Cubicación encontrados en QA

RESPONSABLE HU: Brayan Quintero (@bquinterobtst) — dueño de Cubicador/Python
(issues .1 y .2); Ismael Vasco (@ivasco-dev) en .3 (repositorio del lado
NEXO/Cubicador que toca la sincronización con el borrado optimista de NEXO).
HABILITADOR: continuación directa de S3-HU-06 (deuda técnica de la
integración por colas) — vinculada a esa HU, aunque queda como milestone
propia para no mezclar deuda técnica ya cerrada con bugs nuevos encontrados
en QA. Depende de: nada — puede arrancar ya, en paralelo a todo lo demás.

- **S3-HU-10.1** — 🔴 Crítico: en `cubicador/worker/main.py`, cambiar
  `cuerpo["planId"]` por `cuerpo.get("planId")` en las 2 líneas del manejador
  de errores, para que un mensaje sin `planId` no deje el worker atascado.
  Confirmar con `ci_docker_smoke.py` (job `docker-smoke`) que deja de
  reproducirse.
- **S3-HU-10.2** — Alinear `GuardarItemPlanCargueInput.sku` en
  `cubicador/api/dtos/plan_cargue.py` para aceptar `null` explícito igual que
  NEXO lo permite (`GuardarItemRequest.Sku` es `string?`), sin romper todo el
  plan por un ítem sin SKU.
- **S3-HU-10.3** — En
  `cubicador/infrastructure/persistence/repositorios/plan_cargue_repo.py::eliminar()`,
  borrar antes las `colocaciones_cargue` del plan (mismo orden que ya respeta
  `eliminar_item()`), para que borrar un plan optimizado no termine en
  `ForeignKeyViolationError` ni desincronice NEXO/Cubicador.

### S3-HU-11 — CI/CD en Dokploy por servicio (API, Cubicador-API, Cubicador-Worker, RabbitMQ)

RESPONSABLE HU: Ismael Vasco (@ivasco-dev) — API y RabbitMQ; Brayan Quintero
(@bquinterobtst) — Cubicador-API y Cubicador-Worker. Backend +
Automatización/IA exclusivamente — el despliegue de frontend NO es alcance de
esta HU (ver punto 3). Base: la plantilla dada (build-and-test → deploy
condicionado, self-hosted dokploy, disparo por webhook), adaptada por stack y
con filtro de rutas para que un cambio en un servicio no dispare el pipeline
de los otros 3.

Contexto técnico ya verificado en el repo, para no proponer algo que ya
existe: Cubicador ya tiene un workflow de CI (pytest + verificación contra
Docker Compose real) — corre en cada push/PR, pero sin filtro por servicio y
sin el job de despliegue a Dokploy. La API (.NET) no tiene ningún workflow de
CI/CD todavía — todo lo verificado hasta ahora fue con
`docker-compose.ci.yml` corrido a mano. RabbitMQ no tiene ninguna
automatización.

- **S3-HU-11.1** (API, .NET) — Workflow nuevo
  (`.github/workflows/ci-cd-api.yml`), disparado solo por cambios bajo
  `src/**`, `tests/**`, `CabinaFletes.Clean.sln` o el propio workflow.
  `build-and-test` corre `dotnet restore/build/test` contra
  `CabinaFletes.Clean.sln`; con la BD corporativa ya disponible (punto 5),
  `CabinaFletes.Api.Tests` corre contra ella vía `TEST_DB_CONNECTION`
  (secreto de GitHub, nunca en el repo) en vez de contra el arnés en
  contenedor — esto es lo que cierra de una vez NEXO#3 con evidencia real, no
  solo con Docker local. `deploy` solo en push a `main`/`Pre-Despliegue`,
  `needs: build-and-test`, `runs-on: [self-hosted, dokploy]`, dispara
  `DOKPLOY_DEPLOY_WEBHOOK_URL_API`.
- **S3-HU-11.2** (Cubicador-API, Python) — Separar del workflow combinado
  actual: `.github/workflows/ci-cd-cubicador-api.yml`, disparado por cambios
  en `cubicador/api/**` y en lo compartido de `cubicador/` que Cubicador-API
  usa (dominio, infraestructura de persistencia). `build-and-test` corre
  pytest sobre las pruebas de la API. `deploy` agrega el job self-hosted con
  `DOKPLOY_DEPLOY_WEBHOOK_URL_CUBICADOR_API`, que hoy no existe.
- **S3-HU-11.3** (Cubicador-Worker, Python) — Mismo patrón que .2 pero para
  `cubicador/worker/**`: `.github/workflows/ci-cd-cubicador-worker.yml`,
  pytest del worker + el smoke test de Docker Compose real
  (`ci_docker_smoke.py`) que ya existe, `deploy` con
  `DOKPLOY_DEPLOY_WEBHOOK_URL_CUBICADOR_WORKER`.
- **S3-HU-11.4** (RabbitMQ) — No tiene código fuente propio en este repo, así
  que "test" aquí es un smoke test post-deploy: conexión al broker,
  exchanges/colas/bindings esperados declarados (los 9 comandos + 3 eventos
  de catálogo + los ya existentes de optimizar/crear_desde_linea). Disparado
  por cambios en la configuración/definiciones del broker o en el
  `docker-compose.yml` que lo define — a definir exactamente qué archivo(s)
  lo disparan cuando se construya, ya que hoy la config de RabbitMQ vive
  repartida entre el `docker-compose.yml` raíz y el de `cubicador/`.
- **S3-HU-11.5** — Coordinar los filtros de ruta entre los 4 workflows para
  el caso borde: un cambio que toca código compartido entre Cubicador-API y
  Cubicador-Worker (p. ej. `cubicador/infrastructure/`, tocado justo por el
  bug de S3-HU-10.3) debe disparar ambos pipelines, no solo uno ni "todos
  siempre".

Nota para quien la tome: falta decidir la convención de nombres exacta de los
4 secretos `DOKPLOY_DEPLOY_WEBHOOK_URL_*` — se deja la sugerencia arriba,
confirmarla o ajustarla al crear los issues.

Punto sin resolver, no metido en ninguna HU todavía: `POST /api/cubicacion`
(uno de los 4 huecos de cobertura que reveló #127) no encaja en el patrón de
comando ni en el de proyección — necesita una decisión de arquitectura antes
de que alguien le escriba código. No se mete dentro de S3-HU-04 porque
cambiaría su alcance sin confirmación. Definir si se deja como una issue de
decisión suelta (parecido a como se resolvió S3-HU-01) o si se resuelve
directo con Brayan/Duban fuera de este tablero.

## 6. Prioridad sugerida — secuencia completa

1. Comentar en NEXO#126 señalando los 3 bugs de S3-HU-10 (confirmado que sí,
   antes de que Duban apruebe) — ya con el detalle exacto del archivo de
   hallazgos, no la versión genérica de antes.
2. S3-HU-10.1 (bug crítico del worker) en paralelo, sin esperar el merge —
   es reproducible a voluntad por el propio CI.
3. Duban revisa y mergea NEXO#126 (que para entonces ya incluye lo de #127).
4. S3-HU-07.1 (fix del alias de roles), en paralelo a todo lo anterior — no
   comparte código ni gente con Cubicación.
5. S3-HU-04 (migración del frontend) arranca ya — contra endpoints reales,
   con el inventario exacto de #127 (16/20 cubiertas, 4 pendientes, 1 de
   ellas — `POST /api/cubicacion` — bloqueada por una decisión de
   arquitectura, ver sección 5).
6. S3-HU-11 (CI/CD por servicio) en paralelo, todo el sprint — es trabajo de
   backend/automatización-IA exclusivamente, no compite por tiempo con
   frontend ni con QA.
7. S3-HU-10.2/.3 en paralelo, sin prisa relativa al resto.
8. S3-HU-08.1 (despliegue del portal) en cuanto S3-HU-07.1 esté verificado —
   no antes.
9. S3-HU-08.2/.3 al final, una vez S3-HU-07 y S3-HU-08.1 estén cerradas — es
   la prueba de que el piloto funciona.
10. S3-HU-07.2 (suite de seguridad) puede ir al final o deslizarse sin
    bloquear el piloto.
11. Después de que #126 (con #127 incluido) llegue a `main`, cerrar a mano
    en el tablero los issues #101-121 que ya quedaron resueltos por ese
    código (ninguno se autocierra solo — ningún PR de este lote usó "Closes
    #NNN"). Esto es limpieza del propio tablero de Sprint 3, no toca nada de
    otros sprints.

## 7. Notion

- Épica: la misma que ya se usa — "Épica 2 — Cubicación como servicio
  verificado" — para las 4 HU nuevas (S3-HU-07/08/10/11), como parte de
  Sprint 3, con la misma nomenclatura S3-HU-XX.
- Proyecto: el proyecto en Notion se llama "Tarifario NEXO" — confirmar que
  la épica/sprint que se va a usar cuelga de ese proyecto antes de crear las
  páginas, para que el rollup de "Proyecto" en cada HU resuelva
  correctamente.
- Responsables, ajustados según el punto 3:

| HU nueva | Responsable | Rol / motivo |
|----------|-------------|---------------|
| S3-HU-07 | Kevin Cano (@kcano-ux) | QA — dueño del hallazgo #66 y de la suite pendiente #12 |
| S3-HU-08 | Juan Pablo Velez (@jpvelez-bts) + Duban Guerra (@dguerraBSSL) | Frontend — despliegue del portal es responsabilidad de ambos |
| S3-HU-10 | Brayan Quintero (@bquinterobtst) | Automatización/IA — dueño de Cubicador; Ismael Vasco co-responsable en .3 |
| S3-HU-11 | Ismael Vasco (@ivasco-dev) | Backend/DevOps — API y RabbitMQ; Brayan Quintero co-responsable en Cubicador-API/Worker |

## 8. Antes de crear esto en GitHub y Notion

Todo lo que estaba pendiente en la versión anterior ya quedó resuelto por las
respuestas dadas. Solo faltan 2 cosas puntuales:

- [ ] `POST /api/cubicacion` (sección 5): ¿se deja como issue de decisión
      suelta, o se resuelve directo con Brayan/Duban fuera de este tablero?
- [ ] Nombres exactos de los 4 secretos de Dokploy
      (`DOKPLOY_DEPLOY_WEBHOOK_URL_API`/`_CUBICADOR_API`/`_CUBICADOR_WORKER`/
      `_RABBITMQ`) — se usa esa convención salvo que ya exista una distinta
      en el Dokploy real.

Fuera de eso, con la confirmación final se crea todo: S3-HU-07, S3-HU-08,
S3-HU-10, S3-HU-11 (16 issues en total) en GitHub, se comenta primero en
NEXO#126, y se crean las 4 páginas de Notion bajo el proyecto "Tarifario
NEXO" con los responsables de la tabla de arriba. Nada de Sprint 2, de
S3-HU-01 a 06, ni de ningún PR existente se toca — solo se añade.
