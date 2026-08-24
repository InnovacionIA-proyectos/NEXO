# Decisiones técnicas

Registro de cada decisión tomada antes de escribir la primera línea de este
servicio, con el motivo. Formato: decisión → por qué → implicación práctica.

## 1. Alcance: todo el dominio, no solo el algoritmo

**Decisión**: el microservicio es dueño completo de Cubicación — cubicador
rápido + motor 3D + persistencia de vehículos/planes/ítems/colocaciones/
parámetros. No es un servicio de cómputo puro sin estado.

**Por qué**: así se pueden usar y mover sistemas por separado; si el
microservicio solo calculara y NEXO guardara, quedarían "cosas de cubicador"
en dos repos a la vez, justo lo que se quería evitar.

**Implicación**: el microservicio necesita su propia base de datos con tablas
equivalentes a `VehiculoCubicacion`/`PlanCargue`/`ItemPlanCargue`/
`ColocacionCargue`/`ParametrosCubicacion`. NEXO retira por completo su versión
de esto cuando el corte esté listo (ver punto 11).

## 2. Fuente de verdad para portar

**Decisión**: el comportamiento se porta desde **cabinaFletes original**
(`Api/Services/Cubicacion3D/*`, `PlanCargueService.cs`, `CubicacionService.cs`)
— es el código más probado en el tiempo. La organización en capas se porta
desde el patrón ya aplicado en NEXO (Application/Domain/Infrastructure/Api),
como plantilla de estructura, no de código literal (es otro lenguaje).

**Criterio de fidelidad**: hay dos tipos de "diferencia" posibles al portar, y
se tratan distinto:
- **Error de traducción** (algo que se rompe al pasar C#→Python sin que sea
  intención de negocio) → corregir sin pedir permiso.
- **Rareza del comportamiento original** (algo que el motor actual hace raro
  a propósito o por costumbre, no un bug) → NO cambiarla en silencio;
  documentarla aparte para que negocio decida. El requisito es fidelidad, no
  "mejorarlo" de paso.

**Excepción explícita**: las 31 reglas geométricas/de negocio de
`testDocumentation/REGLAS_DE_NEGOCIO.md` (portadas desde los tests de cabinaFletes) SÍ deben
preservarse exactas — no son candidatas a "corrección de rareza", son el
contrato de comportamiento del motor.

**Adaptación de traducción ya aplicada**: el original usa `decimal` en C#
para todo; aquí se usa `float`, idiomático en Python para cálculo geométrico.
El algoritmo ya redondea explícitamente en los puntos donde una división
podría introducir imprecisión (igual que el original usaba `Math.Round` ahí
mismo) — verificado: las 31 reglas pasan sin ajustes contra la implementación
en `float`.

## 3. Sin comparación automatizada EN VIVO cross-motor (por ahora)

**Decisión**: no se construye un arnés que corra el motor Python y el motor
C# original lado a lado con el mismo input y compare la salida en tiempo de
ejecución.

**Por qué**: el motor original va a dejar de existir (este servicio lo
reemplaza); invertir en un arnés de comparación en vivo contra una versión
que se sabe temporal no es buen uso del tiempo.

**Lo que SÍ se hizo en su lugar**: portar las 31 reglas de negocio como tests
unitarios de este mismo motor — ver `testDocumentation/REGLAS_DE_NEGOCIO.md`,
verificadas con pytest (33 casos, incluye un test parametrizado con 3
variantes). **Actualización (validación contra S2-HU-072)**: de esas 31, las
26 que vienen de `cabinaFletes/src/CabinaFletes.Tests/MotorCubicacion3DTests.cs`
(el criterio de cierre "paridad con los 26 casos dorados") están portadas
**1:1** — mismo nombre, mismo orden, mismo escenario — confirmado comparando
ambos archivos método por método. La sustancia del requisito de paridad SÍ
está cubierta; lo que no existe es la comparación cruzada en vivo descrita
arriba, que es un mecanismo distinto y no era lo que hacía falta. La pasada
de humo end-to-end (crear plan, optimizar, ajustar) contra el entorno real
está en `tests/api/test_smoke.py` + verificación manual/CI contra Docker
real (ver `testDocumentation/PRUEBAS_DE_INTEGRACION.md`).

## 4. Contrato REST: mismas rutas, mismos DTOs, mismos códigos

**Decisión**:
- Mismas rutas que hoy expone NEXO: `/api/cubicacion`, `/api/planes-cargue`.
- DTOs en **camelCase** (igual que `JsonSerializerDefaults.Web` de NEXO) —
  implementado con `alias_generator=to_camel` + `populate_by_name=True` en
  Pydantic (ver `api/dtos/comun.py`).
- Catálogo de errores: reusar los códigos que ya existen en los Commands de
  NEXO (`PLAN_NO_ENCONTRADO`→404, `VALIDACION_FAILED`→400,
  `VEHICULO_EN_USO`→409, etc.), ajustables si en pruebas reales algún código
  no encaja.
- Mismos topes anti-abuso: 10.000 bultos en cubicación rápida, 8.000 unidades
  expandidas por plan, 20 vehículos por flota, 1-2.000 ítems por importación.

**Por qué**: minimiza la traducción que el futuro proxy en NEXO tiene que
hacer — reenvía método+ruta+cuerpo casi sin tocar nada.

## 5. Asíncrono con RabbitMQ — solo para lo pesado

**Decisión**: `optimizar`/`reoptimizar` y `crear-desde-línea` van por cola.
Todo lo demás — incluido el **ajuste manual de una colocación** — se queda
síncrono por REST.

**Por qué el ajuste manual NO va por cola**: es una interacción de
arrastrar-y-soltar en pantalla, necesita sentirse instantánea. Meterla en una
cola reintroduciría exactamente la latencia que se evitó a propósito cuando
se diseñó esto mismo en NEXO (`ValidarAjusteAsync` se resuelve siempre local
ahí, por la misma razón). Aquí ni siquiera hace falta un puerto separado para
lograrlo: `ajustar.py` llama directo a `domain.motor.validadores` en memoria.

**Eventos** (ya nombrados en la historia de usuario original, se usan tal
cual): `CubicaciónSolicitada`, `PlanDeCargueGenerado`, `CubicaciónRechazada`,
`PlanDeCargueAjustado`.

**Payload de los mensajes**: las mismas formas ya definidas como DTOs REST —
una sola forma de verdad, sea que viaje por HTTP o por cola.

**Correlación**: `planId` como identificador en cada mensaje.

**Estado de la implementación**: completo y verificado de punta a punta
contra Docker Compose real. `POST /{id}/optimizar` y `POST
/desde-linea/{lineaSolicitudId}` publican con
`publicar_cubicacion_solicitada` y responden 202 de inmediato
(`optimizar`/`reoptimizar` con el plan en `procesando`; `desde-línea` con
`SolicitudEncoladaDto`, porque ahí todavía no existe un plan que devolver).
El `worker` consume, corre el caso de uso pesado y publica el evento de
resultado — confirmado con `GET /{id}/resultado` que el plan queda
persistido como `optimizado` (no solo que el evento se emitió).

## 6. Estado del plan y cómo se entera el frontend

**Decisión**: nuevo estado `procesando` (y `error`) en el Plan mientras se
calcula. Notificación por **polling** desde el frontend (`GET .../resultado`
repetido cada pocos segundos mientras el estado sea `procesando`), no push.

**Por qué no push**: es una app interna de consumo web (usuario accede por IP
privada), no justifica la complejidad de WebSockets/SignalR para este caso —
el usuario puede seguir trabajando en otra pantalla mientras espera.

## 7. Autenticación entre servicios

**Decisión**: `Authorization: Bearer <secreto>` en ambas direcciones, dos
secretos independientes:
- `CUBICADOR_API_KEY` — NEXO → este servicio (ya reservado en NEXO como
  `Cubicador3D__ApiKey`).
- `NEXO_CALLBACK_API_KEY` — este servicio → NEXO (nuevo, para el endpoint de
  líneas de solicitud).

**Qué es y qué NO es este Bearer**: es un secreto compartido de
servicio-a-servicio — identifica "quién tiene permiso de llamarme", NO
identifica a un usuario final. Los roles de negocio (`Comercial`,
`AdminTarifas`, `Admin`) se siguen validando en NEXO, antes de reenviar la
petición. Este microservicio no conoce usuarios ni roles — se mantiene
reusable por cualquier backend que tenga la key correcta.

**Convención**: nombres documentados como placeholder en `.env.example` de
cada repo; valores reales solo en configuración local/secretos, nunca en git.
Con un solo backend consumidor hoy (NEXO), una key estática por dirección es
suficiente; si aparece un segundo backend consumidor en el futuro, ahí sí
conviene pasar a una key por cliente (revocable individualmente).

## 8. Qué necesita este servicio DE NEXO

**Decisión**: un único endpoint de solo lectura,
`GET /api/lineas-solicitud/{id}/cubicacion` → `{ posiciones, origen, destino }`
(misma forma que `LineaParaCubicacionDto`), para el puente Mesa de Costeo →
Cubicador (`crear-desde-línea`).

**Verificado**: grep exhaustivo sobre `src/` completo de NEXO y de
cabinaFletes confirmó que **no existe ningún otro cruce** — ningún otro
módulo (Cotizaciones, Solicitudes fuera de este puente, Biblioteca,
ServiciosConexos, Métricas, TarifasSicetacAdmin) referencia
`PlanCargue`/`VehiculoCubicacion`/`ColocacionCargue`/`ItemPlanCargue`/
`ParametrosCubicacion` ni los servicios/puertos del cubicador.

**Implementado y verificado**: este endpoint ya existe en NEXO
(`LineasSolicitudController`/`ObtenerCubicacionLineaQuery`, rama
`cubicador`, ver `MIGRACION.md` décima pasada de ese repo). Verificado de
punta a punta con los dos stacks de Docker reales corriendo a la vez (ver
`docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md` secciones 2.5 y 2.6):
`infrastructure/clientes/nexo_client.py` contra la API de NEXO directamente,
y `POST /desde-linea/{id}` disparando el camino completo (router → RabbitMQ
→ worker → NEXO → plan creado y optimizado). Esta segunda prueba encontró un
bug real en `worker/main.py` (leía `lineaSolicitudId` en el nivel raíz del
mensaje en vez de dentro de `solicitud`) — corregido.

## 9. Infraestructura y RabbitMQ

**Actualización**: este servicio se movió de ser un repo Git standalone a
vivir como carpeta (`cubicador/`) dentro del repo de NEXO — pero la decisión
de abajo sigue vigente tal cual: `docker-compose.yml` propio, sin red Docker
compartida con el backend C#. Compartir repo no significa compartir
despliegue ni red.

**Decisión**: `docker-compose.yml` propio (api + worker + rabbitmq + base de
datos) — sin red Docker compartida con NEXO. RabbitMQ es un **contenedor
propio** dentro de este compose (no embebido en el
contenedor de la API), con su puerto AMQP expuesto al host; NEXO se conecta
por connection string/credenciales (`Cubicador3D__RabbitMqUrl=amqp://...`),
igual que NEXO ya conecta a sus Data Warehouses externos.

**Por qué sin red compartida**: si algún día hay que mover RabbitMQ (a otro
host, a un servicio administrado), es cambiar una línea de configuración en
cada lado — no hay que reestructurar topología de red ni tocar el otro repo.

**Credenciales de RabbitMQ**: usuario real vía `RABBITMQ_DEFAULT_USER`/
`RABBITMQ_DEFAULT_PASS` — nunca el `guest`/`guest` por defecto (ese usuario
solo funciona desde `localhost`, y aquí se expone el puerto a otro host).

**Healthcheck**: `/health`, misma convención que ya usa NEXO.

## 10. Resiliencia de la mensajería (sin sobre-diseñar)

**Decisión** (pensada para no complicar la operación de un servicio interno,
no para escala masiva):
- Timeout de vigilancia (5 min, `worker/main.py:_vigilante_de_timeouts`, corre
  cada 60s): si un plan lleva más de eso en `procesando` sin recibir el
  evento de respuesta, se marca `error` automáticamente
  (`marcar_error_por_timeout`).
- Reintentos limitados vía un contador manual en la cabecera del mensaje
  (`x-intentos`, no vía TTL/DLX nativo de RabbitMQ — más simple de razonar
  con ack manual): 3 intentos antes de publicar en `cubicacion.dlq` para
  inspección manual (nadie la consume automáticamente) — nunca reintentos
  infinitos. Un error de negocio (`AppException`: validación, no encontrado)
  NO reintenta — se marca/reporta una sola vez, porque reintentar da siempre
  el mismo resultado; solo un error inesperado (bug, DB caída) entra al ciclo
  de reintentos.
- Idempotencia por diseño: reprocesar el mismo plan (por un reintento) no
  duplica nada — `guardar_resultado_optimizacion` borra e inserta, el
  resultado se reemplaza completo.
- Prefetch bajo en el worker (`set_qos(prefetch_count=2)` en
  `worker/main.py`); escalar con más réplicas si hace falta más throughput.
- Estado `error` visible (`PlanCargue.error_mensaje`) + acción de reintento
  manual para el usuario — se evita construir recuperación automática
  compleja en esta primera versión.

**Verificado contra RabbitMQ real** (ver
`docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md` sección 2.4): un mensaje
con un `tipo` inválido (error inesperado) se reintentó 3 veces con el
contador `x-intentos` subiendo en cada intento, y al agotarse apareció en
`cubicacion.dlq`; un mensaje con `planId` inexistente (`AppException`) se
procesó una sola vez, sin reintento; deteniendo el `worker` con un plan en
`procesando` y forzando el límite de tiempo, el plan pasó a `error`.

**Motor de base de datos**: se eligió **PostgreSQL** para producción
(`docker-compose.yml`) por ser el más común en stacks Python/FastAPI; el
desarrollo/tests usan SQLite (`DATABASE_URL` por defecto) — mismo código
SQLAlchemy sirve para ambos, solo cambia la URL. **Verificado**: el flujo
completo (crear vehículo → plan → ítem → optimizar → ajustar) corrió contra
un Postgres 16 real en Docker Compose sin cambios de código. Confirmar con
el equipo si se prefiere otro motor antes de desplegar a producción.

**Nota operativa encontrada al probar**: `api` y `worker` necesitan esperar a
que `db` y `rabbitmq` estén realmente listos (`condition: service_healthy`
en `depends_on`), no solo a que el contenedor haya arrancado — Postgres tarda
~15s en el ciclo de init/restart interno la primera vez, y sin el healthcheck
ambos servicios se caían al intentar conectar antes de tiempo. Ya corregido
en `docker-compose.yml`.

## 11. Transición en NEXO — reemplazo completo

**Decisión**: no hay coexistencia con feature flag. El día que el
microservicio esté validado (con la pasada de humo del punto 3), se retira
por completo el módulo interno de NEXO: `Application/Cubicacion3D/*`,
`Infrastructure/Cubicacion3D/Motor/*`, sus adapters, repositorios EF y las
migraciones de las 5 tablas — y `CubicacionController`/`PlanesCargueController`
pasan a ser proxies delgados hacia este microservicio.

**Por qué reemplazo total y no gradual**: la instrucción explícita fue "que
los sistemas puedan funcionar por separado, y si necesitan cosas uno del otro
sea por medio de APIs" — mantener dos implementaciones vivas a la vez (una
detrás de flag) es justo la situación mixta que se quería evitar.

**Acción cuando llegue el momento**: documentar en un `.md` de NEXO,
explícito, qué archivos/tablas se retiran — para que no queden restos a medio
camino en ninguno de los dos lados.
