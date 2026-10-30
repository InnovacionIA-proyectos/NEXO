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

## 3. Comparación automatizada cross-motor (revertido)

**Decisión ORIGINAL (ya no vigente)**: no se construía un arnés que corriera
el motor Python y el motor C# original lado a lado con el mismo input y
comparara la salida en tiempo de ejecución. Razón entonces: el motor
original iba a dejar de existir, y no parecía buen uso del tiempo invertir
en comparar contra una versión temporal.

**Por qué se revirtió**: un requisito posterior pidió exactamente eso como
criterio de liberación del motor nuevo — "verde solo con 26/26", con reporte
de diferencias por caso y evidencia repetible. La
razón original (no vale la pena comparar contra algo temporal) sigue siendo
válida como argumento, pero el negocio decidió que la evidencia de paridad
en sí es lo que importa para liberar, no si el motor viejo sigue vivo
después.

**Lo que se construyó**: `herramientas/generador_casos_dorados/`
— un harness .NET que referencia el `.csproj` REAL de `cabinaFletes` (sin
tocarlo, solo lo compila/ejecuta) y corre cada uno de los 26 casos de
`MotorCubicacion3DTests.cs` contra el motor C# de verdad, grabando su salida
en `tests/paridad/fixtures_dorados/*.json`. `tests/paridad/
test_paridad_casos_dorados.py` corre en cada `pytest` (incluido el CI) y
compara el motor Python contra esos 26 JSON — no contra un valor
transcrito a mano. Falla el build si algún caso no coincide, con el
esperado/obtenido exactos en el mensaje de error. Ver el README de esa
carpeta para regenerar los fixtures si el motor C# cambiara alguna vez (no
debería).

**Resultado de la primera corrida**: 26/26 coinciden exacto contra el motor
C# real — sin necesidad de ajustar el port, confirmando lo que ya se sabía
por la vía informal (ver más abajo).

**Lo que ya existía antes del harness**: portar las 31 reglas de negocio
como tests unitarios de este mismo motor — ver
`testDocumentation/REGLAS_DE_NEGOCIO.md`, verificadas con pytest (33 casos,
incluye un test parametrizado con 3 variantes). De esas 31, las 26 que
vienen de `MotorCubicacion3DTests.cs` ya estaban portadas **1:1** — mismo
nombre, mismo orden, mismo escenario — confirmado comparando ambos archivos
método por método antes de construir el harness. Esa cobertura por lectura
ya daba confianza informal de que el port era fiel; el harness + la suite
de `tests/paridad/` (arriba) la reemplazan por evidencia formal y
automatizada — exactamente lo que se pedía y que antes,
correctamente, no existía.

La pasada de humo end-to-end (crear plan, optimizar, ajustar) contra el
entorno real está en `tests/api/test_smoke.py` + verificación manual/CI
contra Docker real (ver `testDocumentation/PRUEBAS_DE_INTEGRACION.md`) — es
un nivel distinto (flujo completo vía HTTP/RabbitMQ/base de datos), no paridad
del motor.

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

**Eventos** (ya nombrados desde el diseño original, se usan tal
cual): `CubicaciónSolicitada`, `PlanDeCargueGenerado`, `CubicaciónRechazada`.
(`PlanDeCargueAjustado`, nombrado originalmente acá como reservado, nunca se
implementó — ver la actualización siguiente.)

**Actualización (ver ADR-ajuste-manual-async.md del lado de NEXO)**: el
razonamiento de arriba ("el ajuste manual NO va por cola") sigue siendo
válido para la API SÍNCRONA de Cubicador (`PATCH /{id}/colocaciones/{id}`,
que no cambió) — pero NEXO, para no exponer Cubicador directo al navegador,
agregó su propio endpoint que SÍ publica `ajustar_colocacion` por cola
(reutilizando `PlanDeCargueGenerado`/`CubicaciónRechazada`, sin un evento
`PlanDeCargueAjustado` separado). La UI optimista del frontend es lo que
compensa la latencia que este párrafo quería evitar — ver el ADR para el
detalle completo de esa decisión.

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

**Implementado y verificado** (2026-09-02): este endpoint ya existe en NEXO
(`LineasSolicitudCubicacionController`/`ObtenerCubicacionLineaQuery`/
`LineaSolicitudCubicacionAdapter`, autenticación dedicada
`CubicadorApiKeyAuthenticationHandler`). ⚠️ Nota histórica: una versión
anterior de este párrafo decía que el endpoint vivía en la rama `cubicador`
de NEXO — esa rama **nunca se mezcló** y quedó descolgada antes de la
reorganización del código por feature; el diseño de ahí se portó (adaptado
a la estructura actual) al NEXO real recién ahora. Verificado de punta a
punta con los dos stacks de Docker reales corriendo a la vez (ver
`docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md` secciones 2.5 y 2.6):
`infrastructure/clientes/nexo_client.py` contra la API de NEXO directamente,
y `POST /desde-linea/{id}` disparando el camino completo (router → RabbitMQ
→ worker → NEXO → plan creado y optimizado, 3/3 unidades cargadas). Esta
segunda prueba encontró un bug real de infraestructura: NEXO y Cubicador
tienen cada uno un servicio llamado `api` en su propio `docker-compose.yml`
— con el worker conectado a las dos redes Docker a la vez,
`NEXO_BASE_URL=http://api:8080` resolvía al `api` del propio Cubicador, no
al de NEXO. Corregido usando el nombre de contenedor explícito
(`http://nexo-api-1:8080`).

## 9. Infraestructura y RabbitMQ

**Decisión REVERTIDA (2026-08-27)** — la de abajo (RabbitMQ como contenedor
propio, sin red compartida) fue la decisión ORIGINAL y ya NO aplica. Se
mantiene documentada por su razonamiento, no como estado actual — ver la
decisión vigente más abajo.

<details>
<summary>Decisión original (histórica, ya no vigente)</summary>

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

</details>

**Por qué se revirtió**: cuando se tomó la decisión original, Cubicador
todavía era (o se planeaba como) un repo separado. Al moverlo a
`NEXO/cubicador/` (ver la actualización que ya estaba aquí), ambos servicios
quedaron en el MISMO repo, y además NEXO ya construyó un broker RabbitMQ
propio con su topología (`docker-compose.yml` raíz,
servicio `rabbit`, ver `docs/mensajeria/TOPOLOGIA.md` en la raíz de NEXO).
Mantener un SEGUNDO broker RabbitMQ solo para Cubicador significaba
infraestructura duplicada sin ningún beneficio real ya que "mover RabbitMQ a
otro host" (la razón original para mantenerlos separados) sigue siendo
igual de fácil compartiendo un broker: es la MISMA operación (cambiar una
connection string), y ahora solo hay que hacerla una vez, no dos.

**Actualizado en S5-HU-17.1 (2026-10-09)**: la base dejó de ser Postgres y
dejó de ser propia. Ahora es **SQL Server**, y en QA y producción el Cubicador
vive en el esquema `dbo_cubicador` de `dbTarifario_Nexo_Pruebas`, una base
**compartida con Cotifly** (`dbo_cotifly`) — NEXO no está ahí, usa la suya. El
servicio `db` del `docker-compose.yml` sigue existiendo pero ya es un SQL
Server **local**, para desarrollo y para el smoke de CI, que imita esa forma
(misma base, mismo esquema, otro host) sin tocar la compartida. El esquema lo
lo gobierna `docs/Database/ESQUEMA_CUBICADOR.sql`, que se aplica a mano, no
`create_all` ni migraciones automaticas.

**Actualizado en S6-HU-31.2 (#327, 2026-10-29)**: se preparó la **base propia** del
Cubicador, `dbCubicador_Pruebas` (mismo servidor), con las tablas en el esquema `dbo`,
para dejar de compartir base con Cotifly. Se creó con el esquema completo y el catálogo
del seeder (`database/scripts/ejecutar_schema_y_seeders.py`); el historial de la base
compartida eran pruebas y no se copia. El esquema se comparó contra la base real
(columnas, valores por defecto, CHECK, llaves e índices) y el catálogo quedó idéntico
al de QA. El esquema quedó en **un solo archivo**, `database/esquema/ESQUEMA_CUBICADOR.sql`
(16 tablas), y el seeder en `database/seeder/`. QA sigue en la base compartida hasta el
cambio de configuración y de esquema por defecto (S6-HU-31.3, #423).

**Decisión vigente**: RabbitMQ SÍ se comparte: `api`
y `worker` se conectan al `rabbit` del `docker-compose.yml` raíz de NEXO
(proyecto Compose separado — no se fusionaron los archivos, siguen siendo dos
`docker compose up` independientes) enganchándose a su red `internal` por
nombre externo (`nexo_internal`, fijado por el `name: nexo` de la cabecera de
ese archivo — ver `docker-compose.yml` de este repo, servicios `api`/`worker`,
bloque `networks:`).

**Por qué no colisiona con la topología de NEXO (MassTransit)**: Cubicador
declara su propio exchange topic (`cubicador.eventos`) y sus propias colas
(`cubicador.optimizar`, `cubicacion.dlq`) por nombre explícito vía `aio_pika`
— nombres que no se cruzan con ninguno de los que MassTransit genera para
NEXO (`CostearLinea`, `AutoCosteoSolicitado`, `MassTransit:Fault--...--`,
etc. — ver `docs/mensajeria/TOPOLOGIA.md` en la raíz). Un mismo broker físico
puede alojar topologías completamente independientes sin interferencia,
siempre que no compartan nombres — y no los comparten.

**Consecuencia operativa real (encontrada verificando esto, no teórica)**: al
no poder haber `depends_on: rabbit: condition: service_healthy` ENTRE
proyectos Compose distintos, no hay ninguna garantía de orden de arranque
entre los dos stacks. `infrastructure/mensajeria/rabbitmq.py::
conectar_con_reintentos` se ajustó de 5 intentos/2s (~8-10s) a 30 intentos/2s
(~60s) para cubrir tanto el arranque en frío real del broker (medido: hasta
~22s de hueco entre que Docker lo marca "healthy" y que el puerto AMQP
realmente acepta conexiones — el mismo problema ya conocido de la nota
operativa de más abajo, pero ahora sin ningún `depends_on` que lo amortigüe)
como el caso de levantar este stack antes que el de NEXO.

**Credenciales de RabbitMQ**: `RABBITMQ_USER`/`RABBITMQ_PASSWORD` en el `.env`
de este repo DEBEN coincidir con los mismos nombres en el `.env` de la raíz
de NEXO — es el mismo broker, el mismo usuario. `.env.example` lo documenta.

**Healthcheck**: `/health`, misma convención que ya usa NEXO.

## 10. Resiliencia de la mensajería (sin sobre-diseñar)

**Decisión** (pensada para no complicar la operación de un servicio interno,
no para escala masiva):
- Timeout de vigilancia (`worker/main.py:_vigilante_de_timeouts`, cada 60s y
  5 min de umbral por defecto, ambos configurables vía
  `CUBICADOR_INTERVALO_VIGILANTE_SEGUNDOS`/`CUBICADOR_TIMEOUT_PROCESANDO_MINUTOS`):
  si un plan lleva más de eso en `procesando` sin recibir el
  evento de respuesta, el worker publica `cubicacion.rechazada` con
  `codigo: "TIMEOUT_PROCESAMIENTO"` y **después** lo marca `error`
  (`obtener_atascados` + `marcar_error`, en ese orden — ver CONTRATO.md 6.4:
  publicar antes de persistir es lo que evita que un broker caído deje el plan
  fuera de `procesando` sin haber avisado a nadie). Antes, el vigilante solo
  marcaba `error` sin publicar, y NEXO se quedaba en `procesando` para siempre.
- Reintentos limitados vía un contador manual en la cabecera del mensaje
  (`x-intentos`, no vía TTL/DLX nativo de RabbitMQ — más simple de razonar
  con ack manual): 3 intentos antes de publicar en `cubicacion.dlq` para
  inspección manual — nunca reintentos infinitos. Un error de negocio
  (`AppException`: validación, no encontrado) NO reintenta — se
  marca/reporta una sola vez, porque reintentar da siempre el mismo
  resultado; solo un error inesperado (bug, DB caída) entra al ciclo de
  reintentos.
- **Mensaje ilegible: 0 reintentos, directo a la DLQ**.
  Si el cuerpo no se puede ni interpretar —no es JSON, es un JSON que no es un
  objeto, le falta `correlationId`, o la cabecera `x-intentos` no es numérica—
  no hay reintento que pueda cambiar el resultado, así que va a
  `cubicacion.dlq` con su alerta y se ackea de inmediato.

  La línea está trazada en **"¿se puede leer el mensaje?"**, no en "¿se puede
  procesar?": un mensaje legible con un `tipo` desconocido (o sin `tipo`) SÍ pasa
  por los 3 reintentos, como cualquier otro fallo del despacho. Darle trato
  distinto a "falta `tipo`" que a "`tipo` inválido" sería una inconsistencia
  nueva.

  Existe porque todo eso corría ANTES del bloque de manejo de errores, sin red
  ninguna: cualquiera de esos casos lanzaba antes del `ack` y dejaba el mensaje
  `unacknowledged` para siempre. Con el prefetch de abajo, **dos mensajes así
  bastaban para que el worker dejara de procesar cualquier cosa**, y reiniciar el
  contenedor no ayudaba: RabbitMQ los reentrega y se vuelven a atascar. Perder un
  mensaje corrupto es aceptable; perder el worker, no.
- **Alerta al equipo** (2026-08-27, agregada para cubrir "Reintentos
  + DLQ + alerta ante fallos repetidos del motor"): al caer un mensaje en
  `cubicacion.dlq`, además del log, se envía un correo al equipo técnico —
  `infrastructure/alertas/correo.py::alertar_mensaje_en_dlq`, llamado desde
  `worker/main.py::_mover_a_dlq`. SMTP directo (sin depender de que NEXO esté
  arriba), mismo destinatarios por defecto que la alerta equivalente de NEXO
  (`Alertas:EquipoTecnico`). Nadie CONSUME `cubicacion.dlq`
  automáticamente (el mensaje sigue ahí para inspección manual) — pero ya no
  hace falta ir a mirar la cola para enterarse de que algo falló.
- Idempotencia por diseño: reprocesar el mismo plan (por un reintento) no
  duplica nada — `guardar_resultado_optimizacion` borra e inserta, el
  resultado se reemplaza completo.
- Prefetch bajo en el worker (`set_qos(prefetch_count=2)` en
  `worker/main.py`); escalar con más réplicas si hace falta más throughput.
  ⚠️ Contrapartida que costó un bug crítico: un prefetch bajo hace que **muy pocos
  mensajes sin ackear alcancen para bloquear el worker entero**. La decisión sigue
  siendo la correcta —la cubicación es intensiva en CPU y no tiene sentido tomar 20
  mensajes que se procesarán en fila—, pero obliga a que ningún camino de código
  pueda terminar sin `ack`. De ahí la guarda de mensajes ilegibles de arriba.
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

**Alerta verificada contra un capturador SMTP real** (no el corporativo,
`rnwood/smtp4dev`, 2026-08-27): mismo escenario de arriba
(`tipo_invalido_ci`), log real del worker:

```
ERROR:cubicador.worker:Fallo procesando mensaje (intento 1/3)
ERROR:cubicador.worker:Fallo procesando mensaje (intento 2/3)
ERROR:cubicador.worker:Fallo procesando mensaje (intento 3/3)
ERROR:cubicador.worker:Mensaje movido a cubicacion.dlq tras 3 intentos: Tipo de mensaje desconocido: tipo_invalido_ci
INFO:cubicador.alertas:Correo de alerta enviado a scalderon@boterosoto.com.co
INFO:cubicador.alertas:Correo de alerta enviado a dguerra@boterosoto.com.co
INFO:cubicador.alertas:Correo de alerta enviado a jpvelez@boterosoto.com.co
INFO:cubicador.alertas:Correo de alerta enviado a kcano@boterosoto.com.co
INFO:cubicador.alertas:Correo de alerta enviado a ivasco@boterosoto.com.co
INFO:cubicador.alertas:Correo de alerta enviado a bquintero@boterosoto.com.co
```

Confirmado también en el capturador (`GET /api/Messages`): `rowCount: 6`,
los 6 destinatarios, asunto `[Cubicador] Mensaje tipo_invalido_ci cayo a la
cola de error tras 3 intentos`.

**Motor de base de datos** — *reemplazado en S5-HU-17.1 (2026-10-09)*: ahora
es **SQL Server**, el mismo motor que el resto de NEXO. Se dejó de elegir
PostgreSQL "por ser el más común en stacks Python/FastAPI" porque mantener dos
motores en la casa costaba más que la comodidad que daba, y porque el catálogo
nuevo lo administra gente que ya trabaja contra SQL Server.

Dialecto `mssql+aioodbc`, con el *ODBC Driver 18* instalado en la imagen (ver
`Dockerfile`, que tiene documentadas tres trampas nada evidentes de esa
instalación). Desarrollo y tests siguen usando SQLite por defecto: el mismo
código SQLAlchemy sirve para ambos, solo cambia la URL — con una excepción, el
esquema, que solo se declara contra SQL Server porque SQLite no los soporta
(ver `infrastructure/persistence/db.py`).

**Verificado el 2026-10-09**: la suite entera en verde dentro de la imagen (142 pruebas a esa fecha), el
esquema del motor 3D aplicado contra SQL Server 2019 real, y la api
arrancando y sirviendo el catálogo de vehículos desde ahí.

**Cómo se gobierna el esquema** — *decidido en S5-HU-17.2 (2026-10-09),
reemplaza a Alembic*: con **scripts `.sql` versionados** en `docs/Database/`,
aplicados con `scripts/aplicar_sql.py`. La propuesta original era Alembic con
`--autogenerate` desde los modelos, y se retiró al probarla: su autogenerado
lee la base **entera**, no solo nuestro esquema, y como `dbo_cubicador` vive en
la misma base que `dbo_cotifly`, la primera corrida propuso **36 `op.drop_table`
contra tablas de Cotifly**. `include_object` no alcanzaba — filtra por nombre de
tabla, no por esquema — y localmente el problema era invisible, porque el SQL
Server de desarrollo no tiene `dbo_cotifly`. Un `upgrade head` en QA habría
borrado datos de otro equipo. DDL explícito es además el formato que ya usan
NEXO y Cotifly, así que el equipo de Datos revisa uno solo.

El costo que se acepta: el modelo de SQLAlchemy y el DDL son dos
descripciones del mismo esquema y pueden separarse en silencio.
`scripts/verificar_mapeo_catalogo.py` es lo que lo detecta — compara cada
columna de los modelos contra `sys.columns` de la base real (tipo, largo,
precisión y nulabilidad). Las pruebas de pytest corren contra SQLite y por
definición no pueden ver esa divergencia. En su primera corrida encontró una
real: `recomendacionVehiculoMercancia.VolumenMercanciaM3` es `DECIMAL(18,4)` en
la base y el modelo la declaraba `(18,3)`.

**Dos `MetaData` separados** (S5-HU-17.2): `modelos.py` tiene las 5 tablas del
motor 3D, que gobierna este repo; `modelos_catalogo.py` tiene las 10 del
catálogo, que administra el equipo de Datos. Tienen `MetaData` propio cada uno
porque compartirlo haría que un `create_all` las tratara igual, y el Cubicador
nunca debe crear ni alterar las del catálogo: solo leerlas.

**Nota operativa encontrada al probar**: `api` y `worker` necesitan esperar a
que `db` esté realmente lista (`condition: service_healthy` en `depends_on`),
no solo a que el contenedor haya arrancado — SQL Server tarda bastante mas que
Postgres en quedar listo (de ahi el `start_period: 20s` del healthcheck); Postgres tardaba ~15s en el ciclo
de init/restart interno la primera vez, y sin el healthcheck ambos servicios
se caían al intentar conectar antes de tiempo. Ya corregido en
`docker-compose.yml`. (RabbitMQ tenía el mismo problema cuando era un
contenedor de este compose — ver la sección de arriba: ahora que es el
`rabbit` compartido de NEXO, en otro proyecto Compose, ni siquiera hay
`depends_on` posible, así que se resuelve solo con reintentos, no con
healthcheck.)

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

## 12. Interoperabilidad con MassTransit

**Contexto**: NEXO construyó un puente hacia Cubicador con `IConsumer<T>`
reales de MassTransit (decisión del equipo: MassTransit nativo, no un cliente
RabbitMQ crudo del lado de NEXO — ver `NEXO/docs/mensajeria/CUBICACION.md`).
MassTransit espera que los mensajes vengan envueltos en su propio "sobre"
(`messageType` + `message` anidado) para poder deserializarlos.

**Decisión**: Cubicador SIGUE sin depender de MassTransit ni de ninguna
librería .NET — solo aprende a hablar el shape de JSON que ese "sobre"
necesita, a mano, en los 2 puntos exactos donde cruza la frontera con NEXO:

- **Al consumir** `cubicacion.solicitada`: si el JSON trae `message`+
  `messageType`, se desenvuelve antes de procesar; si no, se usa tal cual
  (`worker/main.py::_procesar`). Fallback transparente: Cubicador consigo
  mismo (su propia API, sus propios tests) sigue publicando sin sobre, sin
  ningún cambio.
- **Al publicar** `plan-de-cargue.generado`/`cubicacion.rechazada` (los 2
  eventos que SI consume NEXO): se envuelven con `_sobre_masstransit()`
  (`infrastructure/mensajeria/rabbitmq.py`). `cubicacion.solicitada` (el que
  Cubicador consume de sí mismo) NUNCA se envuelve desde este lado — nadie
  más lo necesita.

**Por qué no ir más allá** (ej. no se adopta MassTransit-para-Python, no se
imita su topología de exchanges por tipo): seguiría siendo un acoplamiento
más profundo del que hace falta. El único costo real que se acepta es el
`messageType`, que debe coincidir exacto con el namespace/nombre de la clase
C# del otro lado — un acoplamiento cruzado real, sin verificación en tiempo
de compilación en ninguno de los dos lados, protegido hoy solo por las
pruebas de contrato de cada lado (`tests/Nexo.Api.Tests/Cubicacion/
ContratoCubicacionTests.cs` en NEXO, y la verificación E2E documentada en
`tests/QA/28-.../README.md` de NEXO).

**Verificado en runtime, no solo diseñado**: un mensaje `CubicacionSolicitada`
publicado por NEXO con el sobre real llegó al worker, se desenvolvió
correctamente y se procesó igual que uno sin sobre — sin este cambio fallaba
con `KeyError: 'tipo'` (documentado con la traza real en
`NEXO/docs/mensajeria/CUBICACION.md` #4, incluido un bug que ese mismo
`KeyError` exponía en el manejo de error de `_mover_a_dlq`, ya corregido de
paso al aplicar el mismo desenvuelto ahí).

## 13. Motor de selección de flota (S5-HU-17.3)

**Decisión**: reemplazar la heurística de `cubicacion_rapida.py` por
`domain/motor/seleccion_flota.py`, que lee el catálogo de base y resuelve en
tres partes: filtrar candidatos, empacar con DBLF y elegir la flota mínima.

**Por qué un módulo nuevo y no reescribir el viejo en sitio.** De las 692
líneas de `cubicacion_rapida.py` sobreviven unas 35 (`detectar_extradimension`).
Se van el catálogo de 9 arquetipos quemados, la heurística de estanterías, el
clasificador de categorías por palabras (`"oxidante"`, `"papel"`, `"madera"`),
el criterio de reparto por eje —fuera del alcance por falta de datos— y la
forma de respuesta entera. No es un refactor del mismo algoritmo: es otro
algoritmo, con otra entrada y otra salida. El viejo sigue en pie solo porque
`POST /api/cubicacion` y `application/plan_cargue/desde_linea.py` todavía lo
llaman; **muere en S5-HU-17.4**, que retira ese endpoint.

**DBLF no es un empacador nuevo.** `DeepestBottomLeftFill` se agregó al
registro de `optimizadores.py` junto a FFD y BFD, reusando el mecanismo de
puntos extremos y sus validadores de colisión, soporte y apilamiento. Cambia
una sola cosa: el orden de recorrido de los puntos pasa de `(z, x, y)` a
`(x, z, y)`, que llena la rebanada del fondo antes de avanzar hacia la puerta.
Escribir un segundo empacador habría duplicado la lógica que ya sostienen 33
casos dorados y 27 de paridad con el motor C#.

**Se decide por `max(%volumen, %peso)`.** Una carga de bebidas llena el peso
mucho antes que el volumen; una de cajas vacías al revés. El motor viejo solo
tenía `factorUtilizacion`, volumétrico puro, así que en el primer caso pedía
vehículos de menos. Los dos porcentajes se reportan siempre.

**La capacidad es la de la combinación, no la del chasis.** `3S3 + FURGON` son
30.000 kg porque el furgón pesa; usar los 34.000 del chasis sería creer que
caben 4 toneladas de más.

**Las mercancías incompatibles se reparten, no vetan.** El motor viejo
rechazaba la carga entera al encontrar un par incompatible. Ahora van en
vehículos distintos, que es lo que haría un planillador. La regla es por
**pares de categorías concretas**, así que no sirve el `grupo_compatibilidad`
del motor 3D, que separa cualquier par de grupos distintos: bebidas y cajas de
cartón son categorías distintas y comparten camión sin problema.

**La carrocería de respaldo entra también cuando la principal no tiene
vehículos.** No solo cuando no hay carrocería común. Es el caso real de
`CARGA_CONTENERIZADA` (su principal `CONTENEDOR_SECO` está sembrada sin
vehículo) y de `VIDRIO_PLANO` (`PORTAVIDRIOS`). Sin esto, 2 de las 31
mercancías se quedaban sin recomendación.

**Maximal Empty Spaces no hace falta** — *medido, no supuesto*. El último
criterio de la issue pedía justificar con medición si MES era necesario sobre
DBLF. `scripts/medir_empaque_dblf.py` compara DBLF contra la rejilla óptima
(el tope exacto alcanzable con una orientación uniforme) en 20 combinaciones de
vehículo y bulto.

La primera corrida encontró una brecha real: **6,0 puntos porcentuales por
debajo en promedio y 28,8 en el peor caso** — una estiba de 120×100×150 en un
Sencillo daba 48,1% contra 76,9%, que es pedir dos camiones donde alcanza uno.
La causa **no era fragmentación del espacio vacío**, que es lo que resolvería
MES, sino la elección de orientación: DBLF tomaba la primera válida, que es la
original. Ordenar las orientaciones por cuántas unidades entrarían en el
vehículo vacío cuesta una división entera por orientación y cierra la brecha a
**0,0 puntos en las 20 combinaciones**.

Esa preferencia es **solo de DBLF**: FFD y BFD conservan el orden histórico de
`orientaciones_de`, porque los 27 casos dorados de paridad con el motor C#
dependen de él.

**La validación contra despachos históricos no se pudo hacer: los datos no
existen.** El criterio pedía comparar el motor contra 50 a 100 solicitudes
entregadas con posiciones capturadas. Medido sobre `db_NEXO`, que es la fuente
de despachos del sistema:

| | |
|---|---|
| Líneas de solicitud | 3.122 |
| Con `Dimensiones` no vacío | **182**, y las 182 son la misma fila de prueba repetida: `Pallet, 3 unidades, 120×100×100 cm, 500 kg` |
| Con dimensiones **y** vehículo elegido | **4** |
| Con peso y vehículo canónico (2L2/C2/C3/2S2/3S3) | 84, de las cuales **18** pertenecen a una solicitud entregada |
| Con posiciones capturadas | **ninguna** — las 417 colocaciones que existen son de 12 planes de cargue de prueba, y los 12 tienen `LineaSolicitudId` en NULL |

Lo único reconstruible es `(peso, vehículo elegido)` en 84 líneas: sirve para
el filtro por peso, no para el volumen ni el empaque, que es lo que el criterio
quería medir. La captura de dimensiones por mercancía **entra con este
contrato**, así que el histórico anterior no la tiene ni la va a tener. La
comparación contra despachos reales se puede hacer después del corte, cuando se
acumulen recomendaciones con su resultado.

Lo que sí acota el error mientras tanto es la medición de arriba: DBLF contra
el tope geométrico exacto, con brecha cero. La única fuente restante de
"vehículos de más" es el catálogo —por ejemplo que `C3` no admita furgón, que
deja un salto de 8.500 a 18.000 kg sin escalón intermedio—, no el algoritmo.

## 14. El contrato único (S5-HU-17.4)

**Decisión**: `POST /recomendaciones` reemplaza a `POST /api/cubicacion`, que se
retiró en el mismo corte. No queda endpoint de transición.

**Lo que antes era error ahora es resultado.** `SIN_SELECCION_AUTOMATICA`,
`condicionada` y `extradimension` vuelven con **200**, dentro de `resultado`.
Los códigos de error quedan solo para fallos reales. Un vehículo que no se pudo
recomendar no es una falla del servicio: es una respuesta, y el consumidor tiene
que poder distinguirla de un 500.

**El `origenSistema` sale de la llave, no del cuerpo.** Dos variables,
`CUBICADOR_API_KEY_NEXO` y `CUBICADOR_API_KEY_COTIFLY`, en vez de un mapa en una
sola cadena: se leen mejor en Dokploy y rotar una no obliga a tocar la otra. La
llave genérica `CUBICADOR_API_KEY` sigue sirviendo para `/api/planes-cargue` y
los catálogos, pero **no** para `/recomendaciones`: identifica al servicio, no a
un sistema, y una recomendación tiene que saber de quién viene para poder
reconsultarla. Si el cuerpo declara un `origenSistema` que no corresponde a la
llave, 401 — declararlo es opcional, mentir no.

**Las mercancías viajan con id y código.** En el request va el id (acordado con
Cotifly el 2026-10-08, su HU-021.5; la primera versión de la hoja de ruta
proponía el código). En la respuesta van los dos, igual que
`GET /tipos-mercancia`: Cotifly persiste el id y NEXO usa el código estable.
Darlos juntos cuesta una columna más en el JSON y evita que cada consumidor
tenga que resolver el otro por su cuenta.

**Dos columnas nuevas en `dbo_cubicador`**
(`docs/Database/S5-HU-17.4_columnas_recomendacion.sql`), las dos `NOT NULL` con
default según el criterio del equipo de Datos:

- `recomendaciones.MercanciasCondicionadasJson` — no se puede derivar al releer.
  Una mercancía queda condicionada por su propia marca **o** por haberse usado
  su carrocería de respaldo, y esa segunda razón se pierde si no se guarda.
- `recomendacionVehiculo.AlertaBajoLlenado` — no se puede recalcular.
  `umbral_alerta_llenado` es editable por CRUD y estas tablas son append-only:
  una fila consultada dentro de dos años tiene que decir lo que dijo el día que
  se calculó, no lo que diría con el umbral de hoy.

**Los ids se generan en mayúsculas** — *encontrado al probar contra QA real, no
al diseñar*. SQL Server devuelve las columnas `uniqueidentifier` en mayúsculas y
no es configurable. Escribiendo `b9c435cf-...`, la reconsulta traía
`B9C435CF-...`: el `idRecomendacionVehiculo` del POST y el de la reconsulta no
eran el mismo texto, y guardar ese id es exactamente para lo que existe. SQLite
no lo muestra, porque devuelve lo que se escribió. Se generan ya en mayúsculas,
y además las búsquedas por uuid no distinguen caja: un consumidor puede
guardarlo en minúsculas de su lado y es el mismo uuid.

**La escala decimal se normaliza en el borde HTTP.** La misma cifra llegaba al
DTO con distinta precisión según el camino: en el POST sale del motor y en la
reconsulta de la columna, que tiene su propia escala (`VolumenTotalM3` es
`DECIMAL(18,3)`). Sin normalizar, un consumidor que guarda la respuesta del POST
y después reconsulta recibe `9.60` y `9.600` para el mismo volumen.

**`docs/openapi.json` se regenera, no se edita.** `scripts/generar_openapi.py`
lo escribe desde la app, y con `--verificar` falla si quedó desactualizado.
Hasta ahora se mantenía a mano, que es justo lo que no puede pasar con un
contrato que programan dos equipos de afuera.

### Las dos bases: QA y la local del compose

Es la confusión más fácil de este servicio y conviene tenerla escrita.

| | **QA corporativo** | **Local del `docker-compose`** |
|---|---|---|
| Host | `10.10.40.85:1433` | `127.0.0.1:1434` (servicio `db`) |
| Base | `dbTarifario_Nexo_Pruebas` | `dbTarifario_Nexo_Pruebas` (mismo nombre, otro servidor) |
| Esquema | `dbo_cubicador`, **compartido con `dbo_cotifly`** | `dbo_cubicador`, solo |
| Quién lo administra | el equipo de Datos | este repo |
| Qué lo llena | `sqlcmd` del equipo de Datos + `docs/Database/*.sql` | `docs/Database/*.sql` + `scripts/preparar_catalogo_local.py` |

El `.env` apunta a **QA** — lo usan pytest contra base real y los scripts de
verificación. El `docker-compose.yml` arma su propia `DATABASE_URL` apuntando al
contenedor `db`, así que el servicio levantado con `docker compose up` **no habla
con QA**.

Por eso el stack local arrancaba con `GET /tipos-carroceria` en 503 aunque QA
tuviera sus 15 tablas desde el principio: el contenedor local solo había corrido
`ESQUEMA_CUBICADOR.sql`, que son las 5 tablas del motor 3D. El DDL de las 10 del
catálogo nunca estuvo en el repo porque el equipo de Datos lo aplicó directo en
QA. `scripts/preparar_catalogo_local.py` lo resuelve creando y sembrando esas
tablas **solo en la base local**: se niega a correr contra cualquier host que no
sea `db`, `localhost`, `127.0.0.1` o `host.docker.internal`, porque crea tablas y
apuntarlo por error a QA es exactamente lo que no puede pasar.

### Verificación del contrato contra el servicio real

`scripts/verificar_contrato.py` recorre los criterios de aceptación de
S5-HU-17.4 contra el servicio corriendo, por HTTP. No reemplaza a pytest: lo
complementa donde pytest no llega, que es el motor de base real y la
serialización de verdad. Las dos diferencias que encontró —los `uniqueidentifier`
en mayúsculas y los decimales como texto— eran invisibles contra SQLite y por
ASGI.

## 15. CRUD de los seis catálogos (S5-HU-17.5)

**Dónde viven: en el Cubicador**, que es donde está el dominio. El portal los
consume por passthrough desde NEXO, nunca directo: no hay CORS —se retiró a
propósito—, la llave terminaría en el bundle (ya pasó una vez y obligó a
rotarla) y, sobre todo, **el rol no se podría aplicar**, porque el Bearer del
Cubicador autoriza un backend y no a una persona.

**Lo que la base ya hacía, y por eso no se repite.** Buena parte de las
validaciones de la issue ya estaban puestas por el equipo de Datos: las llaves
foráneas impiden borrar una categoría o una carrocería referenciada, hay
`UNIQUE` en las siete claves de negocio, y tres `CHECK` —categoría 1 distinta de
categoría 2, secundaria distinta de principal, y la bandera condicionada en
falso cuando no hay secundaria—. Lo que agrega esta capa es **traducir esos
choques a un 400 o un 409 con un mensaje que diga qué pasó**, en vez de dejar
salir el error de SQL. Por ejemplo, el borrado de una categoría cuenta las
referencias antes para responder *"hay 4 en mercancías"*, porque el error 547 de
SQL Server no dice cuántas ni de dónde.

**Concurrencia optimista con un entero, no con `ROWVERSION`.** Se agregó
`Version INT NOT NULL DEFAULT 1` a los seis catálogos
(`docs/Database/S5-HU-17.5_crud_catalogos.sql`). Un `ROWVERSION` lo llevaría el
motor —más robusto ante escrituras que no pasen por el ORM— pero SQLite no lo
tiene y las pruebas no podrían verificarlo. El criterio de la issue pide
`version_id_col` de SQLAlchemy, y con un entero funciona igual en los dos
motores.

Un detalle que no es evidente: **la versión que manda el cliente se compara a
mano**, no se le asigna a la instancia. `version_id_col` arma el `WHERE` del
`UPDATE` con el valor **cargado**, no con el asignado, así que escribirle encima
no compara nada y la concurrencia no protegería de nada. Las dos capas conviven:
la comparación explícita rechaza al segundo editor, y `version_id_col` cubre la
ventana entre el `SELECT` y el `UPDATE`.

**`relacionVehiculosCarroceria` no lleva versión propia.** No se edita sola:
viaja dentro del payload del vehículo, y es la versión del **vehículo** la que
se exige y se sube, aunque el cambio solo toque las relaciones. Si no se subiera,
dos administradores editando las carrocerías del mismo vehículo no verían
conflicto, porque la fila del vehículo no cambia. Las relaciones se **reconcilian**
en vez de borrarse y reinsertarse: las que no cambian conservan su `CreatedAt`,
que es lo único que dice desde cuándo existe esa combinación.

**La dominancia se valida antes de escribir.** Se proyecta cómo quedaría el
catálogo con el vehículo ya cambiado y se corre la misma validación que el motor
hace al arrancar. Hacerlo después obligaría a deshacer una escritura ya
confirmada; hacerlo antes cuesta una lectura.

**El par de las reglas se guarda normalizado.** El `UNIQUE` de la tabla es sobre
el par *ordenado*, así que por sí solo no impide que entre `(B,A)` habiendo
`(A,B)`. Guardando siempre el id menor primero, ese índice pasa a garantizarlo.
La incompatibilidad es simétrica —`aplica_a` la evalúa en las dos direcciones—
así que el orden no significa nada. La comparación es **por texto**, no con el
orden nativo de `uniqueidentifier`: SQL Server ordena ese tipo por los últimos
seis bytes primero, y comparar distinto en cada lado dejaría filas que un lado
cree normalizadas y el otro no. El script normalizó la única fila que estaba al
revés.

**Solo tres catálogos publican evento.** NEXO proyecta mercancías, carrocerías y
vehículos. Categorías, parámetros y reglas no los proyecta nadie: publicarles un
evento sería mandar mensajes que ninguna cola consume.

**`CreadoPor` lo declara el llamante.** El Cubicador no tiene usuarios. NEXO
manda el id como campo del cuerpo y aquí se guarda como lo que es, un dato
declarado y no una identidad comprobada. Es obligatorio en toda escritura para
que ninguna quede sin rastro de quién la pidió.

**Las carrocerías de un vehículo le pertenecen.** No son una referencia de
afuera que deba bloquear su borrado: se van con él, igual que se reconcilian con
él al guardarlo. Lo que sí bloquea a un vehículo es una recomendación que lo
nombre, porque esa es de otro y es append-only. Sin esta distinción, la relación
que el propio vehículo creó le impedía borrarse y salía como un
`CONFLICTO_DE_DATOS` genérico.

## 16. El puente desde línea y el fin del fallback silencioso (S5-HU-17.6)

**Murió el default silencioso.** `crear_plan_desde_linea` asumía un furgón de
13,6 metros cuando no reconocía el vehículo sugerido
(`_PRESET_POR_UNIDAD_RAPIDA.get(unidad, "Tractocamion furgon 13.6 m")`). Ponía
la carga en una caja que podía no existir y el plan salía creíble pero
equivocado. Ahora cada traducción que falla es un rechazo con motivo:
`MERCANCIA_NO_DECLARADA`, `PESO_NO_DECLARADO`, `MERCANCIA_DESCONOCIDA`,
`SIN_FLOTA_RECOMENDADA`, `VEHICULO_SIN_PRESET` o
`PRESET_VEHICULO_NO_ENCONTRADO`.

> `PESO_NO_DECLARADO` se agregó en #304. Hasta entonces el puente ponía **1 kg**
> cuando la posición no traía peso (`posicion.peso_kg or 1`): otro default
> silencioso, del mismo tipo que este apartado dice haber matado, que se había
> quedado vivo. El peso decide la capacidad del vehículo, así que un kilo
> inventado puede meter en un Turbo lo que necesitaba una Minimula.

**El puente consume la recomendación en vez de adivinar.** Antes llenaba un
vehículo con el optimizador 3D, miraba qué sobraba, abría otro igual, y así
hasta veinte veces. Ahora el motor de selección responde **cuántos y de qué
tipo** —y la flota puede ser mixta—, así que el bucle recorre los vehículos que
el motor indicó. Si después del último queda carga sin ubicar, los dos motores
no coincidieron y se rechaza con `FLOTA_INSUFICIENTE`: el 3D valida soporte y
apilamiento por unidad, el de selección empaca con DBLF, y devolver un plan
incompleto en silencio sería peor que avisar.

**Las posiciones declaran su tipo de mercancía.** `PosicionCarga` ganó
`codigo_mercancia`, que NEXO agrega al JSON de `Dimensiones` por posición
(S5-HU-18). Las líneas capturadas antes no lo traen y **se rechazan**: no se les
pone un default porque el tipo arrastra la carrocería, y asumirlo elegiría el
vehículo equivocado sin que nada avise.

**El diccionario de compatibilidad es deuda declarada.** Entre el código
canónico (`2L2`, `C2`, `C3`, `2S2`, `3S3`) y el nombre del preset del 3D hay dos
diccionarios en `desde_linea.py`. El `3S3` es el único que depende de la
carrocería, y no es cosmético: con furgón carga 30.000 kg —el furgón pesa— y con
plataforma los 34.000 del chasis. Mandarlo siempre al mismo preset regalaría o
quitaría cuatro toneladas. Hay pruebas que verifican que los presets declarados
existan y que sus capacidades coincidan con las del catálogo.

Ese diccionario existe solo porque existen **dos catálogos de vehículos**;
se retira el día que se unifiquen. El detalle, con lo que cuesta cada punto,
está en [`DEUDA_DIFERIDA_3D.md`](./DEUDA_DIFERIDA_3D.md).

**`domain/motor/cubicacion_rapida.py` se retiró.** Con él se va el catálogo de
nueve arquetipos quemados, la heurística de estanterías, el clasificador de
categorías por palabras y el criterio de reparto por eje. Lo único que
sobrevivía —`detectar_extradimension`— ya vive en `seleccion_flota.py` con la
forma que pide el contrato.

**`SqlParametrosCubicacionRepository` devuelve la entidad de dominio.** Devolvía
`MedidasEstandar` de ese módulo mientras el puerto declaraba
`ParametrosCubicacion`: funcionaba por casualidad, porque los dos tipos tienen
los mismos cuatro campos. Ahora devuelve lo que el puerto dice, y la
infraestructura deja de depender del motor viejo.


**Los eventos de catálogo llevan la fila entera.** NEXO hace upsert por Id, así
que un payload parcial le dejaría la proyección con los demás campos vacíos y
sus desplegables mostrarían filas a medias. Se reusa el DTO del `GET` para
armarlo —mismo criterio ya documentado en `vehiculo_repo._a_evento_payload`—
para que la forma del evento no diverja de la que devuelve la consulta.

**Hay `republicar-catalogo` por catálogo proyectado**
(`POST /tipos-carroceria/republicar-catalogo` y sus equivalentes para
mercancías y vehículos). Reemite el evento de creación para todas las filas, no
solo las nuevas. Es la misma herramienta que ya existía para los vehículos del
motor 3D, que hizo falta cuando esa proyección quedó en 13 filas contra 20 de
este lado. Seguro de correr más de una vez, y no es parte de ningún flujo
automático. Los tres catálogos que NEXO no proyecta no la tienen: no habría a
quién reemitirle.


## 17. En el cable viaja el código de mercancía, no el id (S5-HU-17.7)

El `POST /recomendaciones` recibe `codigoMercancia` (`BEBIDAS`,
`CAJAS_CARTON`), no `idTipoMercancia`. Revierte el acuerdo del 2026-10-08 con
Cotifly, que había cambiado el campo del código al id.

**Por qué se revirtió: el dato tenía dos formas en el mismo recorrido.** NEXO
persiste el **código** en la captura —`CapturaRequests.CodigoTipoMercancia`,
con la razón escrita en su propio comentario: *"se guarda el código porque es
estable, sobrevive un reseed del catálogo y es legible en logs"*— y acto
seguido mandaba el **id** por el cable. Los dos campos salen del mismo
desplegable. `codigoVehiculo` y `codigoCarroceria` ya viajaban como código, así
que la mercancía era la única excepción. Con el id, NEXO tenía que traducir de
un campo al otro para llamar a este servicio, teniendo los dos a la mano.

**No se revirtió por riesgo, y conviene dejarlo claro para que nadie repita el
argumento equivocado.** Los ids del catálogo **no** son aleatorios: son `uuid5`
deterministas, sembrados por `database/seeder/_generar_seeders.py` con el
namespace `c9b1b1a0-6f3e-4b1a-9c2d-a1b2c3d40001` sobre la semilla
`mercancia:{codigo}`. Un reseed los reproduce idénticos —se verificó contra las
71 filas reales—, así que el id es **tan estable como el código**. El cambio es
por coherencia y por quitarle a NEXO un paso de traducción, no porque el id
fuera frágil.

**La respuesta sigue llevando los dos.** `MercanciaDeVehiculoDto` y
`PiezaFueraDeMedidaDto` devuelven `idTipoMercancia` **y** `codigoMercancia`,
igual que `GET /tipos-mercancia`. No se quitó el id: es la llave para unir
contra el catálogo y contra las tablas de recomendación, que siguen guardando
`MercanciaId`. Lo que cambió es **solo lo que entra**.

**Lo que persiste no cambió.** `recomendacion_vehiculo_mercancia.MercanciaId`
sigue siendo el uuid, que es la FK correcta. El código se resuelve a id en
`recomendar_flota` y de ahí para adentro nada se enteró del cambio.

**El puente quedó más corto.** `application/plan_cargue/desde_linea.py` hacía
`mercancia_por_codigo(...)` solo para sacar el `tipo.id` y pasárselo al caso de
uso, que volvía a resolverlo con `mercancia_por_id(...)`. Ahora pasa el código
tal cual: una sola resolución, en un solo lugar, y el puente dejó de cargar el
catálogo completo para algo que el caso de uso ya hacía.

**`mercancia_por_codigo` tolera caja y espacios**, igual que `mercancia_por_id`
ya toleraba la caja del uuid. Los códigos son mayúsculas con guion bajo por
convención y son únicos sin importar la caja, así que aceptar `bebidas` por
`BEBIDAS` no crea ambigüedad y evita un `MERCANCIA_DESCONOCIDA` falso por cómo
el llamante guarde la cadena.


## 18. La identidad de una mercancía es su posición, no su código

El motor identificaba cada línea de la solicitud por el `codigoMercancia`. Pero
una solicitud puede traer **dos posiciones del mismo tipo con medidas y pesos
distintos** —dos tamaños de caja del mismo producto— y ahí el código deja de
identificar nada.

**Lo que pasaba.** `_armar_vehiculo` construía
`unitarios = {m.tipo.codigo: m for m in mercancias}`: un diccionario indexado
por código, donde la segunda posición pisaba a la primera. Después multiplicaba
el peso y el volumen **unitarios de la superviviente** por la cantidad total de
unidades de ese código. Con 10 cajas de 20 kg más 5 de 2 kg, reportaba **30 kg
donde había 210**. Los `id` de ítem también colisionaban:
`f"{codigo}#{i}"` producía `CAJAS_CARTON#0` dos veces.

**Por qué importaba.** `pesoTotalKg` es, por contrato, *"el peso que Cotifly le
manda a NEXO al crear la solicitud"*. El flete se habría costeado sobre el peso
equivocado, sin que nada avisara: la respuesta era un 200 con forma correcta.

**El empaque nunca estuvo mal.** Los ítems se construyen por línea con sus
medidas reales y la selección de flota suma `peso_total_kg` por línea, así que
el vehículo elegido siempre fue el correcto. Lo roto era solo el reporte.

**El arreglo.** `MercanciaSolicitada` gana `indice`, la posición de la línea en
la solicitud, y esa pasa a ser su identidad: `sku = str(indice)` e
`id = f"{indice}#{i}"`. El peso y el volumen se acumulan **por posición** y
recién después se suman por código, así que la respuesta no cambia de forma
—sigue trayendo una entrada por tipo de mercancía— pero los números quedan
bien.

El `indice` lo asigna `recomendar()` a partir del orden recibido, en el único
punto por el que entra todo. Por eso tiene default: ningún llamante ni ninguna
prueba necesita calcularlo. Cambiar `sku` es seguro porque **nada en el motor
3D ramifica por él** —es una etiqueta que viaja y se guarda—, y el puente
`desde_linea.py` ya usaba `sku=f"POS-{idx}"` para los planes de cargue: la
posición como identidad ya era el criterio en la otra mitad del sistema.

**Lo que queda pendiente.** El contrato sigue devolviendo una entrada por
código, así que un consumidor que mande dos posiciones del mismo tipo recibe
los totales correctos pero **no sabe cuántas unidades de cada posición** fueron
a cada camión. Para repartir las posiciones capturadas entre las líneas que se
crean por camión hace falta exponer el `indice` en la respuesta —y una columna
en `recomendacionVehiculoMercancia` para que la reconsulta no lo pierda—. Eso
es un cambio de contrato y va aparte; este es solo el arreglo de corrección.


## 19. El detalle por camión es por posición (S5-HU-17.8)

`MercanciaDeVehiculoDto` y `PiezaFueraDeMedidaDto` llevan `indicePosicion`: el
índice —base 0— de la posición en el arreglo `mercancias` del request. Una fila
por posición, no por código.

**Por qué.** Es la mitad que faltaba de la #18. Esa arregló los *números*; esta
arregla la *trazabilidad*. Con el detalle agrupado por código, un consumidor
recibía "800 unidades de `CAJAS_CARTON` en este camión" sin saber cuántas eran
de cada posición capturada, y sin medidas en la respuesta no podía deducirlo.
Sin ese dato no puede hacer lo que el negocio ya decidió en #217: **crear una
línea de solicitud por camión recomendado**, con las posiciones que le tocan.

**No se devuelven las medidas, se devuelve el índice.** El llamante ya tiene las
medidas: las mandó él. Devolvérselas sería duplicar un dato que podría quedar
desincronizado; el índice se las devuelve sin copiarlas.

**`heterogenea` pasa a contar códigos distintos.** Era `len(detalle) > 1`, que
con filas por posición habría marcado como heterogéneo un camión con dos
posiciones del mismo producto. Carga heterogénea significa **tipos** distintos
compartiendo camión, no filas.

**La columna es nullable y las filas viejas no se rellenan.** Las corridas
anteriores no tienen posición; ponerles `0` sería inventarse que todas eran la
primera. `NULL` dice lo que es. Es el mismo criterio de
`CapturaRequests.CodigoTipoMercancia` en NEXO (*"Null = tipo desconocido; no se
asigna un default"*), y las tablas de recomendación son append-only: lo guardado
no se reescribe. Verificado contra 43 filas reales que vuelven con
`indicePosicion: null` y 200.

**Las piezas extradimensionadas no necesitaron DDL**: ya se guardaban como JSON
en `MercanciasCondicionadasJson`, así que el índice entró como una clave más y
las corridas viejas lo leen como `null`.

**Lo que esto le rompe a quien agrupe por código.** Dos filas del mismo camión
pueden compartir `codigoMercancia`. Nadie puede asumir que el código es único
dentro de un vehículo; hay que sumar. Queda dicho en `CONTRATO.md` §3.1 y en la
§2.3 de la hoja de ruta.


## 20. Tres correcciones de borde del motor de selección (#304)

Salieron de reproducir las dos pruebas que el front hizo contra el despliegue y
que fallaron, con la suite en verde. Ninguna es de las reglas de negocio: las
tres están en los límites de entrada.

**Hay tope de posiciones, y es distinto del de unidades.** `_MAX_UNIDADES_TOTALES`
limita cuántas unidades físicas se expanden; `_MAX_POSICIONES` limita cuántas
filas trae la solicitud. Son dos costos distintos: 10.000 posiciones de una
unidad pasaban el tope de unidades y producían 10.000 filas en la respuesta y
otras tantas en `recomendacionVehiculoMercancia`. Medido: 3.000 posiciones
tardaban 13 s.

S5-HU-17.8 agravó esto sin querer — antes el detalle se agrupaba por código,
con un máximo de 31 filas por camión; ahora es una fila por posición, sin techo.
El cambio es correcto; lo que faltaba era el límite.

El número sale de NEXO: `clsCapturaLineaRequest.Posiciones` admite máximo 200
por línea, así que aceptar más acá sería aceptar algo que la captura no puede
haber producido. Se reusa `CUBICACION_DEMASIADO_GRANDE` en vez de agregar un
código nuevo: el contrato ya lo declara y significa exactamente esto.

**El peso tampoco se asume.** El puente ponía 1 kg cuando la posición no lo
traía (`posicion.peso_kg or 1`). El peso decide la capacidad del vehículo: un
kilo inventado mete en un Turbo lo que necesitaba una Minimula, y el plan sale
creíble pero equivocado. Es el mismo fallback silencioso que la decisión #16
dice haber matado para el tipo de mercancía, y este se había quedado vivo. Se
alcanza porque `PesoKg` es opcional en la captura de NEXO. Ahora es
`PESO_NO_DECLARADO`, y el cero cuenta como no declarado.

**El umbral de extradimensión se alineó con la flota.** Era 1220 × **260** ×
**300**, por encima de los 1220 × 240 × 260 del vehículo más grande. Una pieza
de 245 × 245 × 255 no excedía el umbral pero no cabía en ningún vehículo en
ninguna orientación: salía `SIN_CANDIDATOS_DIMENSIONALES`, que le decía al
operario *"no hay candidatos"* cuando lo que tenía era una pieza sobreancha
que necesita permiso especial.

Ahora el umbral son los máximos del vehículo más grande que no es
extradimensionado: **1220 × 240 × 260 × 34000**, corregido en los dos defaults
de código —el de `ParametrosCubicacion` y el del modelo—.

**No hizo falta tocar la base.** `parametros_cubicacion` es una fila única
*opcional*: si no existe, `SqlParametrosCubicacionRepository.obtener()` devuelve
el default, y la fila aparece solo cuando alguien la edita con
`PUT /medidas-estandar` (`guardar()` la crea si falta). Verificado el
2026-10-19 contra `10.10.40.85/dbTarifario_Nexo_Pruebas`: **cero filas**.

Se llegó a escribir un script de migración y se descartó. Insertar la fila
habría puesto una segunda fuente de verdad por encima del default, y desde ahí
un cambio en el código quedaría tapado en silencio por un dato que nadie pidió.
Queda en su lugar una **comprobación** antes de desplegar —ver el orden de
despliegue en `docs/304-QUE-DEBEN-HACER-FRONT-Y-BACK.md`—, porque si alguien
edita el umbral por el CRUD antes del despliegue, esa fila mandaría sobre el
default y el valor viejo seguiría en pie.

> **Cambia lo que ve el usuario.** Piezas entre 241 y 260 de ancho, o entre 261
> y 300 de alto, pasan de `SIN_CANDIDATOS_DIMENSIONALES` a `EXTRADIMENSIONADO`.
>
> **El 260 de alto es el alto útil de la carga**, confirmado con Operaciones:
> el espacio que queda para la mercancía dentro del vehículo. No se confunde
> con los **4,40 m** reglamentarios, que son la altura *total* del vehículo
> desde el suelo hasta el tope e incluyen chasis y piso del remolque. Lo que se
> compara acá es la pieza contra el espacio de carga, no el conjunto contra el
> gálibo de la vía.

`SIN_CANDIDATOS_DIMENSIONALES` sigue vivo, pero ahora se alcanza por
**capacidad** y no por medidas: una pieza dentro del máximo estándar de peso
cuya carrocería solo existe en vehículos que no la aguantan —por ejemplo
`CARNICOS` de 32.000 kg, que solo viaja en furgón refrigerado y ahí el 3S3 topa
en 30.000—.

**Lo que esto NO arregla.** Siguen abiertos en #304 el cálculo de la cota
mínima de vehículos y el costo cuadrático del empaque, que son el mismo trabajo
—resolver carga homogénea con la rejilla en vez de pieza por pieza— y el de
mayor impacto.


## 21. La carga homogénea se resuelve con una rejilla, no empacándola (#304)

Cierra los dos problemas de fondo de #304, que resultaron ser el mismo: el
motor empacaba pieza por pieza para averiguar cosas que podía calcular.

**La identidad del problema.** Cuando todas las unidades son idénticas, cuántas
caben en un vehículo es una división entera probando las seis orientaciones —
una fórmula—, no una búsqueda. `_caben_por_rejilla` la implementa, igual que
`rejilla_optima` de `scripts/medir_empaque_dblf.py`, respetando las banderas del
catálogo: sin rotación libre solo la orientación original, y no apilable limita
a una capa.

**No es una aproximación.** Colocar en filas rectas siempre es realizable, así
que es un piso garantizado. Y no es un piso flojo: la medición de #268 sigue
dando **0,0 puntos de brecha** entre DBLF y la rejilla en los 16 casos, desde
que las orientaciones se ordenan por capacidad (decisión #13). Para carga
homogénea el resultado es el mismo; lo que cambia es el costo.

**Tres usos, un cálculo.**

1. **La cota mínima de vehículos.** Era volumen de carga entre volumen
   geométrico, y el volumen miente cuando la pieza no tesela: un cubo de 1 m
   deja 48 de los 76,1 m³ de una tractomula, el 63 %. El motor pedía 3 donde
   hacían falta 5, agotaba sus 25 intentos probando combinaciones imposibles y
   devolvía `BUSQUEDA_AGOTADA` ante una carga que sí sabía resolver — se
   comprobó subiendo el presupuesto a 500 intentos: salían 5 camiones.

2. **El filtro de cada combinación.** Corregir solo la cota no alcanzaba: las
   combinaciones se ordenan por volumen ascendente, así que seguía mandando a
   empacar flotas de vehículos chicos que suman volumen de sobra y aun así no
   admiten las piezas. Ahora también se exige que la suma de unidades que
   caben alcance. Es aritmética: descartarlas no cuesta nada.

3. **El reparto.** Con una sola posición, `_repartir_homogenea` asigna a cada
   vehículo el menor entre lo que cabe por geometría y lo que aguanta la
   capacidad de esa combinación. Sin empacar.

**Lo medido**, con el catálogo real contra SQL Server:

| Caso | Antes | Ahora |
|---|---|---|
| L5 del pliego, 6.400 bultos de 5 kg | 83,6 s | **0,61 s** |
| 10.000 ladrillos de 10×10×10 | ~145 s | **0,27 s**, 2 camiones |
| 200 láminas de 1 m | `BUSQUEDA_AGOTADA` | **5 camiones** |
| 400 láminas de 1 m | `BUSQUEDA_AGOTADA` | **9 camiones** |
| 600 láminas de 1 m | `BUSQUEDA_AGOTADA` | **`SUPERA_K_MAX`** |

Las dos últimas filas son las pruebas que el front hizo contra el despliegue y
que fallaron con la suite en verde. El cambio de `BUSQUEDA_AGOTADA` a
`SUPERA_K_MAX` importa tanto como el tiempo: *"me quedé sin intentos"* no le
dice nada al comercial; *"supera el máximo de vehículos"* sí.

**Hay techo de segundos, y está incompleto.** `_MAX_SEGUNDOS_BUSQUEDA` corta la
búsqueda, pero se revisa **entre** intentos de empaque. La carga heterogénea
masiva —muchas posiciones de medidas distintas— no pasa por la rejilla, sigue
empacando pieza por pieza, y ahí un solo intento puede tardar minutos sin que
el techo lo interrumpa. El tope de 200 posiciones acota el problema pero no lo
cierra: 200 × 50 son 10.000 unidades heterogéneas, entrada legal.

**Por qué el camino rápido es solo para una posición.** Con varias, aunque
compartan medidas, el peso unitario puede diferir y entonces cuántas entran
depende de cuáles entren. Generalizarlo a "todas las posiciones con la misma
geometría y el mismo peso" es posible y no se hizo: una sola posición cubre
todos los casos que se rompían, y ampliarlo sin un caso que lo pida sería
adivinar.

**`k_max` pasa de 10 a 25.** Con el tope en 10 se rechazaban cargas
resolubles: 600 piezas de 1 m necesitan 13 vehículos. Se midió qué cuesta
subirlo —son `C(k+4, 4)` combinaciones con 5 tipos de vehículo, o sea **k⁴**—:

| k_max | Tiempo |
|---|---|
| 25 | 0,01 s |
| 50 | 0,75 s |
| 100 | 9,8 s |

25 es gratis y cubre los casos reales que aparecieron. Más allá habría que
dejar de enumerar combinaciones, que es otro trabajo.

**Vive en cuatro sitios y manda el primero:** la fila `k_max` de
`parametrosMotor` —editable por `PUT /parametros-motor/k_max`, sin desplegar—,
el respaldo en `ParametrosMotor.k_max` por si la fila falta, la semilla
(`_generar_seeders.py` y `parametrosMotor.csv`) para que un entorno nuevo nazca
en 25, y el fixture de pruebas. **Cambiar solo la fila deja los otros tres en
10**, y un entorno nuevo volvería a nacer con el tope viejo.

Tiene consecuencia operativa, no solo técnica: por #217 cada vehículo
recomendado crea una línea de solicitud, así que este tope decide cuántas
líneas puede producir una sola captura. Con 25, una carga grande puede generar
25 líneas con sus 25 líneas de cotización.
