# Verificación NEXO ↔ Cubicador — local y Dokploy (2026-09-14)

> **Actualización 2026-09-14, tarde — retest post-fix de disco.** Tras
> liberar espacio en el host de Dokploy y redesplegar, se repitió toda la
> batería de pruebas contra la misma URL. Resultado: **los 16 endpoints de
> S3-HU-02/S3-HU-03 ya están y funcionan de punta a punta contra el
> Cubicador real** (`crear_plan_demo` verificado en vivo: el plan de prueba
> apareció proyectado en NEXO en segundos). El ÚNICO bloqueo que queda es la
> compuerta de paridad, que sigue cerrada (`503
> CUBICACION_EN_VERIFICACION`) — exactamente donde se dejó, es la decisión
> de negocio pendiente de Kevin Cano, no un problema técnico. Detalle en la
> sección 7, al final de este documento.

Registro de la verificación pedida antes del despliegue: primero en local
(`docker compose`, stack completo de NEXO + Cubicador), después contra la URL
ya desplegada en Dokploy. No se movió lógica de negocio — solo se corrigió
una variable de entorno faltante y se documentan los hallazgos.

## 1. Resumen ejecutivo

- **En local, el flujo completo NEXO ↔ Cubicador funciona de punta a punta**
  (mensajería, worker, callback inverso) una vez corregida una variable de
  entorno que faltaba (sección 2).
- **En Dokploy hay DOS motivos independientes por los que "el cubicador no
  funciona" hoy**, no uno solo:
  1. La compuerta de paridad sigue cerrada (`503 CUBICACION_EN_VERIFICACION`)
     — comportamiento esperado y documentado, pendiente de que Kevin Cano
     confirme la evidencia de paridad (ver `docs/CHECKLIST_DESPLIEGUE.md`).
  2. **El build desplegado en Dokploy es más viejo que la rama actual**: le
     faltan TODOS los endpoints de S3-HU-02/S3-HU-03 (crear plan, ítems,
     catálogo de vehículos, proyección de lectura del visor 3D). Confirmado
     contra `swagger.json` real del despliegue — ver sección 4.2. Esto es
     independiente de la compuerta de paridad: aunque se abriera la
     compuerta hoy, la mayoría de las pantallas de Plan de Cargue seguirían
     sin tener dónde pegar en el backend desplegado.

## 2. Cambio aplicado al `.env` (raíz de NEXO) — necesario para el despliegue

**Faltaba `CUBICADOR3D_INBOUND_API_KEY`** en la sección "DOCKER COMPOSE" del
`.env` de la raíz. Sin ella:

- `docker-compose.yml` interpola `Cubicador3D__InboundApiKey: ${CUBICADOR3D_INBOUND_API_KEY:-}`
  — al no existir esa variable plana, el contenedor recibe la clave **vacía**,
  aunque la variable con `__` (la que lee `dotnet run` directo, sin Docker) sí
  estuviera puesta más arriba en el mismo archivo. Docker Compose no reutiliza
  esa línea automáticamente: cada variable del bloque `environment:` del
  compose se resuelve por su cuenta.
- Con la clave vacía, `CubicadorApiKeyAuthenticationHandler` (fail-closed)
  rechaza CUALQUIER Bearer con `401`, incluso el correcto.
- **Efecto real observado antes del fix**: `GET /api/lineas-solicitud/{id}/cubicacion`
  (el único puente HTTP que existe, en sentido Cubicador → NEXO) devolvía
  `401` siempre — el puente "crear plan desde línea" (botón *Cubicar* de la
  Mesa de Costeo) habría fallado en cualquier ambiente desplegado con
  `docker compose` que reprodujera este mismo `.env`.

**Fix aplicado**: se agregó, en la sección DOCKER COMPOSE del `.env` raíz,

```
CUBICADOR3D_INBOUND_API_KEY=<mismo valor que Cubicador3D__InboundApiKey de arriba>
```

Verificado antes/después: `401` → `404` (línea inexistente, autenticación ya
pasa) contra una línea real sin cubicar: `200` con `posiciones: []`.

**Importante para Dokploy (que despliega por `Dockerfile`, no por
`docker-compose.yml`, según se confirmó en esta conversación)**: este bug
concreto (variable plana vs. variable con `__`) es una particularidad de
`docker-compose.yml`, no se traslada 1:1. En Dokploy, la variable que hay que
declarar en la app de la API de NEXO es directamente
`Cubicador3D__InboundApiKey` (con doble guion bajo, tal cual la lee
`IConfiguration`), no `CUBICADOR3D_INBOUND_API_KEY`. Vale la pena confirmar
en Dokploy que esa variable con `__` está puesta ahí — si alguien copió el
bloque "DOCKER COMPOSE" del `.env` literal a Dokploy, se llevó el nombre
equivocado.

Todo lo demás del `.env` raíz se dejó exactamente como estaba (no se tocaron
`Cors__OrigenesPermitidos`, credenciales de BD, SMTP, etc.).

**No se modificó `cubicador/.env`**: ya estaba correcto — `RABBITMQ_USER`/
`RABBITMQ_PASSWORD` coinciden con la raíz, y `NEXO_CALLBACK_API_KEY` ya
coincidía con `Cubicador3D__InboundApiKey`.

## 3. Prueba local — qué se levantó y qué se verificó

Stack levantado con `docker compose up -d --build` en la raíz (`rabbit` +
`api`, apuntando a la BD real de pruebas `10.10.40.85` — sí hay red hasta
ahí desde esta máquina) y en `cubicador/` (`db` Postgres propio + `api` +
`worker`, enganchado a la red `nexo_internal`).

Para poder ejercitar el flujo completo se abrió **temporalmente, solo en la
corrida local**, la compuerta de paridad
(`Cubicacion__ParidadVerificada=true` con una evidencia marcada
`PRUEBA-LOCAL-NO-VALIDA-PARA-PRODUCCION`) y `Migrate/SeedOnStartup=true`.
**Ambos se revirtieron a `false` al terminar** — el `.env` que queda en el
repo NO tiene la compuerta abierta ni migración/seed automáticos; solo
queda el fix real de la sección 2.

### 3.1 Autenticación (los 6 roles, contra la tabla real del portal)

`GET /api/sesion/actual` con `X-Portal-Usuario-Id` — probado con los 6 ids
que diste (10128 ivasco/Comercial, 10129 kcano/CoordinadorFletes, 10130
bquintero/AdminTarifas, 10131 jpvelez/ServiciosLogisticos, 10132
scalderon/LiderProceso, 10123 dguerra/Admin). **Los 6 resuelven
correctamente** contra `ConnectionStrings:IdentidadPortal` (la contraseña de
tu tabla no se usó para nada: Nexo no valida contraseña, solo el id contra
esa tabla — ver `IdentidadPortalAuthenticationHandler.cs`). Sin header: 401.
Id inexistente: 401.

### 3.2 Mensajería NEXO ↔ Cubicador, de punta a punta

1. `POST /api/planes-cargue/demo` → `202 {"estado":"procesando"}`.
2. El worker de Cubicador (contenedor separado, logs confirmando "Worker
   escuchando en 'cubicador.optimizar'") consumió el mensaje, creó el plan
   con 7 ítems en su Postgres propio, publicó `plan-de-cargue.generado`.
3. `PlanDeCargueGeneradoConsumer` (NEXO) lo recibió y proyectó: el plan
   apareció en `GET /api/planes-cargue` en estado `borrador`.
4. `POST /api/planes-cargue/{id}/optimizar` → `202 procesando` → tras ~6s,
   `GET /api/planes-cargue/{id}` mostró `estado: "optimizado"`, con métricas
   reales calculadas (`puntaje 59.00`, `59 colocaciones`, `62.7% volumen`,
   `89% piso`, etc. — ver `GET .../metricas`).
5. RabbitMQ (consola de administración, API `:15672`) revisado al final:
   **0 mensajes en todas las colas**, **0 colas `_error`**, `cubicacion.dlq`
   en 0 — ninguna cola quedó atascada durante la prueba.
6. Puente inverso (`GET /api/lineas-solicitud/{id}/cubicacion`, el que llama
   Cubicador): verificado con Bearer correcto/incorrecto/ausente (401/401/
   200 tras el fix de la sección 2) y contra una línea real ya sembrada en
   la BD (sin posiciones capturadas → `200` con `posiciones: []`, correcto).
   **No se ejercitó el flujo completo `crear_desde_linea`** (requeriría una
   línea con `Dimensiones` ya capturadas, ninguna de las sembradas la
   tenía) — la autenticación y el ruteo del puente sí quedaron probados.

**Conclusión local: la conectividad NEXO ↔ Cubicador (colas, worker, Postgres
propio de Cubicador, callback HTTP inverso) funciona correctamente una vez
con el fix de la sección 2 y la compuerta de paridad abierta.**

## 4. Prueba contra Dokploy — `http://nexo-predesplieguebackend-ijaiqn-cdc194-10-10-40-17.sslip.io`

Solo se pudo probar desde afuera, por HTTP — no hay acceso a Dokploy en sí
(dashboard, logs, variables de entorno) desde esta sesión.

### 4.1 Lo que SÍ funciona ahí

- `GET /health` → `200 Healthy`, con `masstransit-bus: Healthy` y
  `database: Healthy` — la API desplegada tiene conectividad real tanto a
  RabbitMQ como a la base de datos.
- `GET /api/sesion/actual` — probado con los mismos 6 ids: **los 6
  resuelven correctamente**, mismos roles/capacidades que en local.
- Módulos estables (`/api/solicitudes`, `/api/servicios-conexos`,
  `/api/cotizaciones`) responden con normalidad.
- `POST /api/planes-cargue/{id}/optimizar` existe y responde exactamente lo
  esperado: `503 CUBICACION_EN_VERIFICACION` (compuerta cerrada, sin
  fallback) — consistente con `Cubicacion:ParidadVerificada` en `false` (o
  sin configurar) en esa API.

### 4.2 Lo que NO existe en ese despliegue (hallazgo principal)

Se descargó el `swagger.json` real del despliegue
(`/swagger/v1/swagger.json`) y se filtraron las rutas de plan de cargue.
**Solo aparecen 3**:

```
/api/lineas-solicitud/{id}/cubicacion
/api/planes-cargue/{id}
/api/planes-cargue/{id}/optimizar
```

Eso es exactamente la superficie de **S2-HU-08** (la HU original). **Ninguno**
de los endpoints agregados por S3-HU-02 (crear plan, ítems, ajustar
colocación, catálogo de vehículos — 9 endpoints) ni por S3-HU-03 (7
endpoints de lectura del visor 3D: `GET /api/vehiculos`, `GET
/api/planes-cargue` listado, `/resultado`, `/flota`, `/metricas`,
`/no-cargadas`, `/exportar`) está en ese `swagger.json`. Confirmado también
por comportamiento real:

- `POST /api/planes-cargue/demo` → `405 Method Not Allowed` (la ruta no
  tiene un método POST registrado en esa versión).
- `GET /api/planes-cargue` (listado) → `404`.
- `GET /api/vehiculos` → `404`.

**Esto significa que el build corriendo en Dokploy es más viejo que el
código que está en esta rama** (`Pre-Despliegue-BACKEND`, donde S3-HU-02/03
ya existen y se probaron funcionando en el punto 3.2). Dokploy está
construyendo desde un commit/rama anterior a esos cambios, o el redeploy
automático no se disparó tras el último merge.

## 4.3 Causa raíz de por qué sigue desplegada la versión vieja

Confirmado con el log real de un intento de redeploy (2026-09-14): el build
falla en `dotnet restore` por **`No space left on device`** en el host de
Dokploy — el caché HTTP de NuGet no puede escribir (`masstransit.8.5.10`,
`documentformat.openxml`, etc.). No es un problema del `Dockerfile` ni del
código: el build es idéntico al que corrió bien en local en este mismo día
(sección 3). Dokploy no reemplaza el contenedor si el build no termina, así
que sigue sirviendo la última imagen que sí compiló — de ahí que el
`swagger.json` real (sección 4.2) sea más viejo que esta rama: **todos los
redeploys posteriores a S3-HU-02/03 han estado fallando en silencio por
disco lleno**, no por un rechazo de despliegue.

**Qué revisar en el host de Dokploy (por SSH/terminal del propio Dokploy),
no en este repo:**

```bash
df -h                      # confirmar el disco lleno
docker system df           # ver qué está pesando (imágenes, build cache, volúmenes)
docker builder prune -af   # limpiar el build cache (normalmente el más pesado en builds .NET multi-stage)
docker image prune -af     # imágenes sin usar (incluida la del segundo despliegue duplicado que ya apagaste)
docker system df           # confirmar que bajó
```

Con el disco liberado, reintentar el deploy de `Pre-Despliegue-BACKEND` —
debería traer ya los endpoints de S3-HU-02/03 (punto 4.2 resuelto). Vale la
pena revisar también si el segundo despliegue duplicado que mencionaste
(mismo puerto, misma red) dejó imágenes/volúmenes huérfanos ocupando disco
antes de apagarlo.

## 5. Qué falta / qué falla — resumen accionable

| # | Qué | Dónde | Impacto |
|---|---|---|---|
| 1 | `Cubicador3D__InboundApiKey` puede llegar vacía si se copia el `.env` tal cual a un `docker-compose` | Cualquier despliegue por compose | Puente inverso (`crear_desde_linea`) 401 siempre — **corregido en este `.env`, ver sección 2** |
| 2 | Compuerta de paridad cerrada (`Cubicacion:ParidadVerificada`) | Dokploy (API de NEXO) | `POST /optimizar` responde 503 siempre — decisión de negocio pendiente (Kevin Cano), no un bug |
| 3 | **Build desplegado en Dokploy no tiene S3-HU-02/S3-HU-03** — causa raíz confirmada: el host de Dokploy se queda sin disco durante `dotnet restore` (`No space left on device`), así que todo redeploy desde S3-HU-02/03 viene fallando en silencio y sigue sirviendo la imagen vieja | Host de Dokploy (disco) | Faltan 16 endpoints de Plan de Cargue completos — liberar disco (`docker builder prune -af`, `docker image prune -af`) y reintentar el deploy |
| 4 | No se pudo verificar si el `worker` de Cubicador está desplegado como app separada en Dokploy, ni la conectividad Dokploy↔RabbitMQ↔Cubicador | Dokploy (solo se dio la URL de NEXO, no la de Cubicador) | Sin poder confirmarlo desde afuera — pendiente que confirmes cuántas apps de Dokploy existen para `cubicador/` |

## 6. Próximos pasos recomendados

1. **Redesplegar la app de NEXO en Dokploy** desde el commit actual de
   `Pre-Despliegue-BACKEND` (o el que corresponda a producción) — eso por sí
   solo debería resolver el punto 3 de la tabla.
2. Confirmar en Dokploy, en la app de la API de NEXO, que existe
   `Cubicador3D__InboundApiKey` (con doble guion bajo) con el mismo valor
   que `NEXO_CALLBACK_API_KEY` en la app de Cubicador.
3. Decisión de negocio pendiente, no técnica: cuándo Kevin Cano confirma la
   evidencia de paridad para poner `Cubicacion__ParidadVerificada=true` +
   `ParidadEvidencia` + `ParidadFecha` en Dokploy (las tres juntas).
4. Confirmar cuántas aplicaciones de Dokploy existen para `cubicador/`
   (`api` y, sobre todo, `worker` — no tiene puerto ni healthcheck HTTP, es
   fácil de omitir sin que se note desde afuera) y revisar sus logs para
   confirmar que el `worker` está consumiendo la cola.

## 7. Retest post-fix de disco (2026-09-14, tarde)

Mismo `swagger.json` real descargado de nuevo:

```
/api/lineas-solicitud/{id}/cubicacion
/api/planes-cargue
/api/planes-cargue/demo
/api/planes-cargue/{id}
/api/planes-cargue/{id}/colocaciones/{colocacionId}
/api/planes-cargue/{id}/exportar
/api/planes-cargue/{id}/flota
/api/planes-cargue/{id}/items
/api/planes-cargue/{id}/items/importar
/api/planes-cargue/{id}/items/{itemId}
/api/planes-cargue/{id}/metricas
/api/planes-cargue/{id}/no-cargadas
/api/planes-cargue/{id}/optimizar
/api/planes-cargue/{id}/resultado
/api/vehiculos
/api/vehiculos/{id}
```

**Los 16 endpoints de S3-HU-02/S3-HU-03 ya están** (punto 3 de la tabla de
la sección 5, resuelto).

Probado en vivo contra esa URL:

- Los 6 usuarios (`X-Portal-Usuario-Id`) siguen resolviendo correctamente.
- `GET /api/planes-cargue` ya lista planes reales, incluidos varios creados
  hoy antes de este retest (estado `borrador`) — evidencia de que alguien ya
  venía probando `crear_plan_demo` contra el Cubicador real desplegado.
- `GET /api/vehiculos` devuelve el catálogo real (2 presets del sistema).
- `POST /api/planes-cargue/demo` → `202 procesando` → el plan apareció
  proyectado en el listado segundos después, con sus ítems — **confirma que
  la mensajería NEXO ↔ Cubicador funciona de punta a punta en el ambiente
  real de Dokploy**, no solo en local.
- `POST /api/planes-cargue/{id}/optimizar` sobre un plan real → sigue
  `503 CUBICACION_EN_VERIFICACION`. **La compuerta de paridad sigue
  cerrada** — el punto 2 de la tabla de la sección 5 NO se tocó (y no debía:
  es decisión de Kevin Cano, no algo para resolver en este redeploy).
- `GET /api/lineas-solicitud/{id}/cubicacion` con la key rotada que se ve en
  el `cubicador/.env` **local** → `401`. Esperado y correcto: Dokploy debe
  tener su PROPIO secreto de producción, distinto al de desarrollo local —
  no se puede (ni se debe) verificar esa coincidencia desde afuera con la
  clave de local. Falta confirmar directamente en Dokploy que
  `Cubicador3D__InboundApiKey` (app de NEXO) es idéntica a
  `NEXO_CALLBACK_API_KEY` (app de Cubicador) **en ese ambiente**.

### Estado actualizado de la tabla de la sección 5

| # | Qué | Estado |
|---|---|---|
| 1 | `CUBICADOR3D_INBOUND_API_KEY` faltante en compose | Corregido en `.env` local; no aplica a Dokploy (despliega por Dockerfile — ahí la variable correcta ya es `Cubicador3D__InboundApiKey` directo) |
| 2 | Compuerta de paridad cerrada | **Sigue cerrada** — pendiente decisión de Kevin Cano, comportamiento correcto |
| 3 | Build desatualizado por disco lleno en el host | **Resuelto** — redeploy trajo los 16 endpoints, verificados funcionando en vivo |
| 4 | Confirmar `Cubicador3D__InboundApiKey` == `NEXO_CALLBACK_API_KEY` en Dokploy (no en local) | **Pendiente de confirmar** — no verificable desde afuera sin el secreto real de producción |

## 10. Verificación local tras S3-HU-04.3 (2026-09-14, tarde-noche) — nuevos endpoints + bug real encontrado y corregido

Se pidió repetir la verificación local completa después de varios commits
nuevos, el más relevante `ccaa795` ([S3-HU-04.3], issue #78): agrega
`POST /api/planes-cargue/desde-linea/{lineaId}`,
`GET /api/planes-cargue/por-linea/{lineaId}` y
`GET /api/planes-cargue/{id}/colocaciones`. El propio commit avisa que NO se
compiló en el sandbox donde se escribió ("verificación manual línea por
línea").

### 10.1 Build y tests unitarios (antes de tocar Docker)

- `dotnet build CabinaFletes.Clean.sln -c Release` → **compila sin errores**
  (1 warning preexistente y no relacionado, `ASPDEPR005`).
- `dotnet test tests/CabinaFletes.Domain.Tests` → **127/127 passed**.
- `dotnet test tests/CabinaFletes.Application.Tests` → **139/139 passed**
  (incluye los tests nuevos de `CrearPlanDesdeLineaCommandTests`,
  `ObtenerColocacionesPlanQueryTests`, `ObtenerPlanesPorLineaQueryTests`,
  `CubicacionSolicitadaValidadorTests`).

### 10.2 Los 3 endpoints nuevos, probados contra contenedores reales

| Endpoint | Caso | Resultado |
|---|---|---|
| `GET /{id}/colocaciones` | Plan real ya optimizado | `200`, geometría completa de las 59 colocaciones |
| `GET /{id}/colocaciones` | Id inexistente | `404 PLAN_CARGUE_NO_ENCONTRADO` |
| `GET /por-linea/{lineaId}` | Línea sin ningún plan todavía | `200 []` (nunca 404, como documenta el código) |
| `POST /desde-linea/{lineaId}` | Línea inexistente | `404 LINEA_SOLICITUD_NO_ENCONTRADA` |
| `POST /desde-linea/{lineaId}` | Línea real, ya cotizada | `202 procesando` → ver 10.3 |

### 10.3 Bug real encontrado (config de desarrollo local, no de producción)

`POST /desde-linea` inicialmente terminaba en `cubicacion.dlq` — el worker
de Cubicador fallaba con `httpx.ConnectError: All connection attempts
failed` al intentar llamar de vuelta a
`GET /api/lineas-solicitud/{id}/cubicacion`. Causa, en dos capas:

1. `cubicador/.env` tenía `NEXO_BASE_URL=http://localhost:8080` — dentro del
   contenedor del worker, `localhost` es el propio contenedor, no el host ni
   el contenedor de NEXO.
2. Corregirlo a `http://api:8080` (el nombre de servicio "obvio") **tampoco
   funcionó**: `cubicador/docker-compose.yml` y el `docker-compose.yml`
   RAÍZ de NEXO tienen **cada uno su propio servicio llamado `api`**. Como
   el worker pertenece a ambas redes (la propia de `cubicador` y la
   compartida `nexo_internal`), el nombre corto `api` resuelve ambiguo al
   `api` LOCAL de `cubicador` (`cubicador-api-1`), no al de NEXO —
   confirmado con
   `docker exec cubicador-worker-1 python -c "import socket; print(socket.gethostbyname('api'))"`.

**Fix real**: usar el nombre de contenedor completo, que sí es único en
todo el host — `NEXO_BASE_URL=http://nexo-api-1:8080` (verificado por
resolución DNS antes de aplicarlo). Tras el fix: `httpx: HTTP Request: GET
http://nexo-api-1:8080/... "HTTP/1.1 200 OK"` — el puente conecta. La línea
de prueba usada no tenía posiciones capturadas (dato de seed, no del
código), así que el resultado final es un rechazo de negocio limpio (sin
excepción, sin log de error) — comportamiento correcto, no un fallo.

**Corregido en `cubicador/.env`**, documentado ahí mismo con el detalle de
las dos capas del problema para que nadie vuelva a perder tiempo con esto.
Este bug es de configuración de **desarrollo local únicamente** — no aplica
a Dokploy (ahí no hay dos proyectos Compose con nombres de servicio en
colisión; cada app tiene su propio contenedor con nombre fijado por
Dokploy).

### 10.4 Regresión general — todo lo de las secciones 1-9 se repitió sin cambios

Salud, los 6 roles, `crear_plan_demo` → `optimizar` → `optimizado` con
métricas reales: todo verde de nuevo. RabbitMQ quedó con 2 mensajes en
`cubicacion.dlq` (los intentos previos al fix del `NEXO_BASE_URL`, antes de
corregirlo) — esperado, la DLQ no se autolimpia, queda para inspección
manual. Sin colas `_error`.

## 9. Verificación final completa (2026-09-14, 17:48 UTC) — TODO EN VERDE

Última pasada contra `http://nexo-predesplieguebackend-ijaiqn-cdc194-10-10-40-17.sslip.io`,
después de resolver la causa raíz de la sección 8.0 (caché de build):

| # | Chequeo | Resultado |
|---|---|---|
| 1 | `GET /health` | `200 Healthy` — `masstransit-bus` y `database` OK |
| 2 | `GET /api/sesion/actual`, los 6 roles (ivasco/kcano/bquintero/jpvelez/scalderon/dguerra) | `200`, los 6 resuelven con su rol correcto |
| 3 | `GET /api/planes-cargue`, `GET /api/vehiculos` (S3-HU-02/03) | `200` ambos |
| 4 | `GET /api/lineas-solicitud/{id}/cubicacion` sin Bearer / con Bearer incorrecto / con `Cubicador3D__InboundApiKey` real de Dokploy | `401` / `401` / **`404`** (autentica correctamente; 404 es "línea no existe", no un fallo de auth) |
| 5 | Flujo completo `POST /demo` → `POST /{id}/optimizar` → `GET /{id}` | `202` → `202` → **`estado: "optimizado"`, `puntaje 59.00`, `59 colocaciones`** — motor real de Cubicador, no mock |

**Confirmado explícitamente en este pase**: `Cubicador3D__InboundApiKey`
configurada en la app de NEXO en Dokploy es la clave correcta — el puente
inverso "crear plan desde línea" (botón *Cubicar* de la Mesa de Costeo) ya
tiene su autenticación validada de punta a punta. Con esto, los 4 puntos de
la tabla de la sección 5 quedan resueltos — no hay pendientes técnicos
conocidos para el flujo NEXO ↔ Cubicador en este ambiente.

## 8. Evidencia de paridad y encendido de la compuerta (2026-09-14)

Kevin Cano (@kcano-ux) autorizó encender la compuerta de paridad
(`Domain.PlanDeCargue.Reglas.CompuertaDeParidad`) para el ambiente Dokploy
"Pre-Despliegue-BACKEND". No había una corrida de GitHub Actions ya
enlazada como evidencia formal (ver `docs/CHECKLIST_DESPLIEGUE.md`, que
además tenía una referencia desactualizada a `cubicador-ci.yml`, retirado en
S3-HU-11) ni acceso a `gh`/Actions desde esta sesión para traer una — en su
lugar se corrió la MISMA suite de paridad de forma directa y verificable,
contra el código exacto del commit desplegado.

**Comando**, contenedor efímero, sin nada instalado localmente más que
Docker:

```
docker run --rm -v "<ruta a cubicador/>:/app" -w /app python:3.14-slim \
  bash -c "pip install -q -r requirements.txt && pytest tests/paridad -v"
```

**Commit verificado**: `82fb52ba73083b7d94ea07d42a4774a2b9b8e1e3` (rama
`Pre-Despliegue-BACKEND`, 2026-09-14 08:10:33 -0500).

**Resultado — 27 passed in 3.60s** (los 26 casos dorados + el meta-test que
confirma que son exactamente 26 fixtures, ni más ni menos):

```
tests/paridad/test_paridad_casos_dorados.py::test_paridad_colisiones[01] PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_colisiones[02] PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_colisiones[03] PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_colisiones[04] PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_colisiones[05] PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_orientaciones[06] PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_orientaciones[07] PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_orientaciones[08] PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_orientaciones[09] PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_10_unidad_que_cabe_exactamente_se_carga PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_11_unidad_que_excede_una_dimension_sin_rotacion_se_rechaza PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_12_unidad_que_solo_cabe_rotada_se_carga_rotandola PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_13_unidad_que_solo_cabe_rotada_pero_no_rotable_se_rechaza PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_14_dos_unidades_caben_lado_a_lado_sin_colision PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_15_apilamiento_valido_segunda_encima_de_la_primera PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_16_apilamiento_sobre_no_apilable_se_rechaza PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_17_apilamiento_excede_peso_soportado_se_rechaza PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_18_soporte_insuficiente_no_deja_unidades_flotando PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_19_exceso_de_peso_total_se_rechaza_con_razon PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_20_balance_de_ejes_carga_centrada_estimacion_repartida PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_21_balance_de_ejes_carga_entre_ejes_reparto_inverso_a_la_distancia PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_22_balance_de_ejes_voladizo_efecto_palanca PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_23_unidad_que_no_pasa_por_la_puerta_se_rechaza PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_24_secuencia_entrega_temprana_al_fondo_genera_violacion PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_25_secuencia_con_estrategia_de_secuencia_entrega_temprana_queda_cerca_de_la_puerta PASSED
tests/paridad/test_paridad_casos_dorados.py::test_paridad_caso_26_metricas_utilizacion_volumetrica_es_volumen_cargado_sobre_util PASSED
tests/paridad/test_paridad_casos_dorados.py::test_hay_exactamente_26_fixtures_dorados PASSED
============================== 27 passed in 3.60s ==============================
```

### Valores configurados (Dokploy, app de la API de NEXO)

```
Cubicacion__ParidadVerificada=true
Cubicacion__ParidadEvidencia=82fb52ba73083b7d94ea07d42a4774a2b9b8e1e3
Cubicacion__ParidadFecha=2026-09-14
```

Nombres con doble guion bajo, tal cual — Dokploy despliega por `Dockerfile`
directo, sin la interpolación de `docker-compose.yml` (ver conversación
anterior sobre `CUBICADOR3D_INBOUND_API_KEY`).

### 8.0 Resuelto — la compuerta quedó abierta y verificada en vivo

Tras varias rondas de diagnóstico (ver 8.1), la compuerta finalmente abrió.
Verificado en producción, no solo por configuración: `POST /api/planes-cargue/demo`
→ `POST /{id}/optimizar` → `202` (ya no `503 CUBICACION_EN_VERIFICACION`) →
`GET /api/planes-cargue/{id}` con `estado: "optimizado"`, `puntaje: 59.00`,
`totalColocaciones: 59` — resultado real del motor de Cubicador desplegado,
no un mock.

**Causa raíz real, confirmada**: NO era la sintaxis del valor de
`ParidadEvidencia` (sección 8.1, hipótesis descartada), ni un duplicado de
la variable en el editor de Dokploy (se buscó en mayúsculas/minúsculas, no
había ninguno), ni la instancia duplicada que se había apagado antes. Era
**caché de build de Docker obsoleto en el host de Dokploy**: `docker
builder prune -af` fue lo que finalmente lo resolvió. Encaja con el mismo
síntoma de disco lleno visto antes en esta sesión (sección 4.3) — el host
llevaba varios `deploy` acumulando capas de BuildKit sin liberar, y en
algún punto una imagen vieja (con las variables de entorno "horneadas" de
una build anterior a los últimos cambios) se siguió sirviendo aunque el
`git clone` y el log de build de cada intento se vieran "frescos". La
lección para cualquier variable de entorno que no parezca aplicarse en
Dokploy después de un redeploy normal: **antes de sospechar del código o
del editor de variables, correr `docker builder prune -af` en el host y
volver a desplegar.**

### 8.1 Nota: `#` en un valor de variable de entorno en Dokploy (hipótesis descartada, se deja como referencia)

El primer valor de `Cubicacion__ParidadEvidencia` (más largo, con paréntesis
y un `#8` al final apuntando a esta sección) quedó guardado y visible en el
editor de Dokploy, y se confirmaron 3 redeploys completos ("Deploy", no solo
"Restart") sin que la compuerta se abriera — con el código verificado
correcto (`Program.cs` no tiene ningún `appsettings.*.local.json` en la
imagen, `.dockerignore` lo excluye explícitamente) y sin ninguna otra
variable en conflicto. Sospecha fuerte, no 100% confirmada: Dokploy
probablemente serializa las variables como un archivo estilo `.env` antes de
inyectarlas al contenedor, y un `#` sin comillas en el valor se interpreta
como inicio de comentario — puede truncar o descartar esa línea completa,
dejando `ParidadEvidencia` vacía para el proceso aunque se vea bien en el
editor. Se simplificó el valor a solo el hash del commit (sin espacios,
paréntesis, dos puntos ni `#`) para sacar esa variable de la ecuación.
**Lección para variables de entorno en Dokploy en general: evitar `#`, `:`
y comillas sin verificar primero cómo las serializa la plataforma.**
