# Pendientes de la migración

## Estado: migración del backend COMPLETA

Todos los módulos de negocio de `NEXO/src` están migrados a
`NEXO-Clean/src` con la arquitectura de 4 capas: Cotizaciones (piloto),
ReglasSoloSicetac, AjustesVigenciaPlaza, Biblioteca (TarifarioSap +
PropuestaTarifa + matriz SICE-TAC), Solicitudes/Captura (Bandeja Priorizada +
auto-costeo + recálculo periódico), ServiciosConexos, TarifasGanadas
(Biblioteca de Tarifas Ganadas), TarifasSicetacAdmin, Métricas y Plan de
Cargue (S2-HU-08, orquestado por colas contra el motor Cubicador). Ver
MIGRACION.md para el detalle de cada pasada y las decisiones de diseño.

## Plan de cargue (S2-HU-08) — estado real, honesto

El plan original (`Api/Services/Cubicacion3D/*` del `NEXO/` original,
reemplazado por un adapter HTTP) se descartó a favor de una integración
100% por colas contra el motor Cubicador (`cubicador/`, microservicio
Python, mismo repo) — issue #28 (S2-HU-075) construyó el puente
(`ICubicacionPort`, consumers, ver `docs/mensajeria/CUBICACION.md`) y las
issues #29/#30 (S2-HU-08.1/08.2) lo conectaron a un flujo real: 202+id,
avance por SignalR (`/hubs/plan-cargue`), consulta de respaldo
(`GET /api/planes-cargue/{id}`) y la compuerta de paridad.

**Encendido de la función de plan de cargue (S2-HU-08.2, #30)**: cerrada por
defecto en el código (`Cubicacion:ParidadVerificada=false`) — todo intento
responde `503 CUBICACION_EN_VERIFICACION`, sin ningún fallback. Kevin
(@kcano-ux) es quien la enciende, y solo después de confirmar 26/26 en los
golden tests de paridad (`cubicador/tests/paridad/
test_paridad_casos_dorados.py`, S2-HU-07.3): configurar juntas
`Cubicacion:ParidadVerificada=true`, `Cubicacion:ParidadEvidencia` (la corrida
concreta que confirmó 26/26 — URL o commit) y `Cubicacion:ParidadFecha`.
Las tres son obligatorias a la vez (`Domain.PlanDeCargue.Reglas.
CompuertaDeParidad`): `ParidadVerificada=true` sin las otras dos dejas la
compuerta cerrada igual, a propósito — no es un booleano suelto. Checklist
formal (issue #118, S3-HU-05.3) en
[`docs/CHECKLIST_DESPLIEGUE.md`](docs/CHECKLIST_DESPLIEGUE.md).

> ✅ **Encendida en Dokploy (Pre-Despliegue-BACKEND) el 2026-09-14**,
> autorizado por Kevin Cano. Evidencia: corrida real de la suite de paridad
> (27 passed, 26/26 casos dorados) contra el commit `82fb52ba73083b7d94ea07d42a4774a2b9b8e1e3`
> — detalle completo y los tres valores exactos configurados en
> `docs/CHECKLIST_DESPLIEGUE.md`, sección 1 ("Encendido confirmado").
> `cubicador-ci.yml` (mencionado antes acá) ya no existe, se retiró en
> S3-HU-11 — la suite corre hoy vía `ci-cd-cubicador-api.yml`/
> `ci-cd-cubicador-worker.yml`.

**Cierre de seguridad (S3-HU-05, #116/#117)**: `CUBICADOR_CORS_ORIGENES` se
retiró por completo (código + `.env`/`.env.example`/`docker-compose.yml`) —
ningún navegador llama a Cubicador directo desde que S3-HU-02/03/04 movieron
toda escritura y lectura del plan de cargue a través de NEXO. `CUBICADOR_API_KEY`
y el par `NEXO_CALLBACK_API_KEY`/`Cubicador3D__InboundApiKey` se rotaron — el
valor que estuvo expuesto en el bundle público del portal queda inválido.
Pendiente fuera de este repo: aplicar el valor nuevo en el despliegue real de
producción (esta sesión solo rotó los `.env` locales de desarrollo).

> ⚠️ **Corrección al párrafo de arriba.** Dice que S3-HU-04 ya movió el frontend
> a NEXO. **Todavía no ocurrió**: `administracion_embarqa_nexo` sigue con 12
> puntos de llamada directa al Cubicador (`cubicadorBaseUrl` en
> `cubicador-api.service.ts` y `captura-api.service.ts`) — nadie tocó esos
> call-sites todavía.
>
> Qué implica hoy, en concreto: en producción no rompe nada (ese módulo ya
> estaba inerte, `environment.prod.ts` tiene `cubicadorApiUrl` y
> `cubicadorApiKey` vacíos), y en desarrollo el CORS tampoco muerde porque las
> llamadas van por el proxy de Angular, que para el navegador es mismo origen.
> **Lo que sí rompe es la clave rotada**: `environment.ts` lleva
> `dev_cubicador_api_key` escrita a mano, así que quien levante el frontend
> contra un Cubicador local recibe 401 en las 12 llamadas hasta que S3-HU-04
> las migre.
>
> ✅ **Actualización (issue #78, S3-HU-04.3)**: de los 4 endpoints que faltaban
> en NEXO, ya se agregaron 3 — `POST /api/planes-cargue/desde-linea/{lineaId}`,
> `GET /api/planes-cargue/por-linea/{lineaId}` y
> `GET /api/planes-cargue/{id}/colocaciones` — sobre el `crear_desde_linea` que
> el worker de Cubicador ya implementaba de punta a punta. Detalle, incluida
> una limitación conocida (el worker hoy solo reporta el primer vehículo de una
> flota multi-vehículo), en [`docs/mensajeria/CUBICACION.md` §8](docs/mensajeria/CUBICACION.md).
> Ojo: esto es solo el lado NEXO — los 12 call-sites del frontend siguen sin
> migrar, eso sigue siendo trabajo de S3-HU-04 aparte.
>
> **Sigue faltando 1 endpoint**: `POST /api/cubicacion` (método `cubicar()`,
> `captura-api.service.ts`). No tiene issue todavía y necesita una decisión de
> arquitectura antes de escribirse — es cálculo puro sin persistencia, no encaja
> ni en el patrón de cola ni en el de proyección que usa el resto del módulo.

**Resuelto — los 3 bugs de QA de Cubicación (S3-HU-10, #131/#132/#133)**: los tres
que la verificación de los PR #122/#126 encontró y que seguían presentes en `QA`.

1. Un mensaje sin `planId` dejaba el worker **atascado para siempre**: la línea que
   reporta el fallo final usaba indexación directa y lanzaba antes del `ack`. Con
   `prefetch_count=2`, dos mensajes así bloqueaban el worker completo y reiniciar el
   contenedor no lo recuperaba.
2. Un ítem **sin SKU** —caso de negocio legítimo, `string?` en NEXO— fallaba 3 veces
   y marcaba *todo el plan* en `error`.
3. Borrar un plan **ya optimizado** siempre fallaba con `ForeignKeyViolationError`, y
   como NEXO borra de forma optimista, dejaba las dos bases en desacuerdo sin nada que
   las reconciliara.

Al arreglarlos aparecieron **dos defectos más de la misma causa**, incluidos en la
misma HU: todo lo que fallaba *antes* del bloque de manejo de errores (body ilegible,
JSON que no es un objeto, falta `correlationId`, cabecera `x-intentos` corrupta)
atascaba el mensaje igual pero **sin llegar siquiera a la DLQ**; y
`eliminar_planes_de_linea()` tenía el bug 3 exacto, agravado porque un `delete()`
masivo tampoco dispara la cascada del ORM — o sea que **volver a cubicar una línea con
flota optimizada fallaba siempre**.

Verificado contra el stack real (Postgres + RabbitMQ + api + worker), no solo con
tests: evidencia completa en
[`tests/QA/s3-hu-10-tres-bugs-cubicacion/README.md`](tests/QA/s3-hu-10-tres-bugs-cubicacion/README.md).

**Resuelto — aislamiento de la base entre tests de Cubicador (S3-HU-10)**: la suite
compartía una sola base SQLite en memoria (`StaticPool`, necesario para que las
conexiones vean la misma base), así que lo que un test escribía quedaba para los
siguientes. Dos trampas concretas: colgarle un plan a un preset del catálogo lo dejaba
"en uso" y rompía el test que lo borra; agregar un vehículo rompía el que afirma
`len(vehiculos) == 9`. Lo peor era que fallaban **en otro archivo, con un error que no
mencionaba al culpable**. Cerrado con `cubicador/tests/conftest.py` (no existía
ninguno), que recrea el esquema antes de cada test: ~1 s sobre la suite completa, cero
roturas, comprobado con un test contaminante deliberado.

**Resuelto**: "pedir un plan de cargue desde una línea de solicitud"
(`crear_desde_linea` — botón Cubicar de la Mesa de Costeo → Cubicador 3D)
funciona de punta a punta. `LineaSolicitud.Dimensiones` **ya capturaba** las
posiciones (JSON escrito por `CapturarSolicitudCommand`, leído por
`ObtenerCosteoQuery` — ver `PosicionCargaDto` en
`Application/CasosDeUso/Solicitudes/DTOs`); lo único que faltaba era el
endpoint que el Cubicador ya llamaba. `GET /api/lineas-solicitud/{id}
/cubicacion` (`LineasSolicitudCubicacionController` +
`ObtenerCubicacionLineaQuery` + `LineaSolicitudCubicacionAdapter`, ver
`cubicador/CONTRATO.md` sección 6) lo expone ahora, con autenticación de
servicio-a-servicio dedicada (`CubicadorApiKeyAuthenticationHandler`, Bearer
contra `Cubicador3D:InboundApiKey` — separada de
`IdentidadPortalAuthenticationHandler`, S2-HU-03). Verificado con 6 tests de
integración HTTP contra SQL Server real y con una corrida real de punta a
punta cruzando NEXO y Cubicador en Docker (`POST /desde-linea` → RabbitMQ →
worker → este endpoint → motor 3D → plan optimizado).

**Lo que SÍ falta, sin fecha todavía**:
- No hay ajuste manual de colocaciones expuesto todavía (existe en el
  dominio — `ColocacionCargue.AjusteManual` — pero ningún caso de uso lo usa).

## Costeo con datos de plaza provisionales (S2-HU-09, #31/#33)

Contrato de la conexión SAP nueva (plaza en vivo, hoy inexistente) definido
en `ISapTarifaFleteEnVivoPort` (`Application/CasosDeUso/Biblioteca/Interfaces`,
documentación completa en el XML doc del archivo: campos, tipos, semántica,
vigencias). Mock sustituible (`SapTarifaFleteEnVivoMockAdapter`) conectado a
`PropuestaTarifaAdapter` como sustitución ADITIVA de la plaza — nunca del
piso SICE-TAC ni del histórico — activable solo por
`SapTarifaFleteEnVivo:Modo=Mock`. Por defecto (`Deshabilitado`) el costeo se
comporta exactamente igual que antes de esta HU: verificado corriendo la
suite completa contra Docker real sin regresión.

**Pendientes, sin fecha, dueño externo a este repo**:
- **Contrato revisado con "el equipo de conexiones"** (criterio de cierre de
  #31): no se hizo en esta sesión — no hay forma de contactar a ese equipo
  desde acá. Queda **registrado, no enviado**: alguien del equipo tiene que
  compartirles `ISapTarifaFleteEnVivoPort.cs` (el contrato) y este párrafo.
- **Dataset de plaza validado formalmente por negocio** (S2-HU-09.2): el
  dataset de `SapTarifaFleteEnVivoMockAdapter` es una estimación razonable a
  partir de las tarifas ya sembradas en `DataSeeder.cs` para las mismas
  rutas — NO es un valor que negocio haya confirmado como representativo.
  Marcado explícito en el código y en el `Detalle` que devuelve cada
  candidato (`"pendiente de validación formal por negocio"`).
- **El adaptador REAL no existe** (`SapTarifaFleteEnVivo:Modo=Real` hoy se
  comporta igual que `Deshabilitado`, con degradación segura). Cuando "el
  equipo de conexiones" lo construya: un adaptador nuevo que implemente
  `ISapTarifaFleteEnVivoPort`, un `if` más en
  `Infrastructure/DependencyInjection.cs` — cero cambios en
  `ReglaDeCosteo`, `PropuestaTarifaAdapter` (más allá de esa función) ni en
  pantallas.

## Set de datos para el equipo de datos (S2-HU-10, Habilitador B8)

Entregable completo en [`analitica/`](analitica/README.md), en 3 partes:

- **S2-HU-10.1 (`#35`)**: vistas de solo lectura en el esquema `analitica`
  (migración `VistasAnaliticaSoloLectura`, luego `RecrearVistasAnaliticaEn
  DboNexoPrueba` cuando el esquema base se movió a `dbo_NEXO_prueba`).
  Cobertura: solicitudes, líneas, cotizaciones, líneas de cotización,
  conexos, tarifas ganadas archivadas, auditoría, y catálogos (clientes/
  rutas/vehículos/servicios/usuarios) — más `analitica.inconsistencias`, que
  documenta registros con datos irregulares en vez de descartarlos en
  silencio. Diccionario en
  [`analitica/00-fuentes/bd-propia/DICCIONARIO_DATOS.md`](analitica/00-fuentes/bd-propia/DICCIONARIO_DATOS.md).
- **S2-HU-10.2 (`#36`)**: 4 extractos puntuales de las fuentes de lectura
  corporativas (piso SICE-TAC, tarifa de plaza SAP, histórico SAP, catálogo
  de clientes) — ver
  [`analitica/00-fuentes/fuentes-corporativas/README.md`](analitica/00-fuentes/fuentes-corporativas/README.md)
  para fecha de corte, volumen y llaves de cruce.
- **S2-HU-10.3 (`#37`)**: consolidación del set final versionado
  (`analitica/01-set-final/v1.0.0-piloto/`), con diccionario y linaje
  unificados de ambos lados y un reporte de control de calidad cruzado.

**Pendiente, fuera del alcance de esta sesión**: ejecutar la extracción real
contra la base de producción (no un ambiente de pruebas), en ventana de bajo
uso, con una cuenta de solo lectura acotada al esquema `analitica` — sigue
sin haber acceso a esa base.

## Transversales (no bloqueantes, a decidir cuando haga falta)

- RabbitMQ, Outbox y auditoría "de verdad" — el diagrama de referencia los
  sugiere, pero no existen en el sistema actual; se recomienda incorporarlos
  solo cuando un módulo migrado los necesite de verdad (evitar complejidad
  anticipada).
- **Identidad del portal (S2-HU-03) — implementada, con 3 pendientes de negocio/TI
  heredados del contrato** (`docs/contrato-integracion/README.md`, sección 2):
  Azure AD y `DevAuthenticationHandler` quedaron retirados por completo
  (S2-HU-03.3); `IdentidadPortalAuthenticationHandler` valida
  `X-Portal-Usuario-Id` en cada petición y `MapeoRolPortal` traduce el rol del
  portal a uno de los 6 de Nexo. Sin `ConnectionStrings:IdentidadPortal`
  configurada, Producción no arranca (fail-closed); Desarrollo/QA caen a un
  adapter que resuelve contra la tabla `Usuarios` local. Quedan abiertos, sin
  fecha, los mismos 3 pendientes que ya traía el contrato:
  - **P1** (dueño: TI) — confirmar las columnas reales de la tabla de
    identidad del portal contra `sys.columns`; hoy el esquema
    (`tblSAPUsuariosApp`/`tblSAPUsuarioRoles`/`tblSAPRoles`) es el de
    referencia de la integración, sin verificar contra la base real.
  - **P2** (dueño: Negocio) — solo 3 de los 6 roles de Nexo tienen
    equivalente sembrado en `MapeoRolPortal` (`TarifasComercial`,
    `TarifasAdministrador`, `Administracion`, resueltos por NOMBRE porque el
    `IDROLES` real todavía no está confirmado). `CoordinadorFletes`,
    `LiderProceso` y `ServiciosLogisticos` no tienen candidato.
  - **P3** (dueño: Negocio) — el orden de precedencia cuando una persona
    tiene varios roles a la vez (`Domain.Usuarios.Reglas.ResolucionRolPortal`)
    es una propuesta técnica interina, no confirmada por negocio.
- Ninguno de los archivos .cs de esta migración se compiló en este sandbox
  (sin SDK de .NET ni acceso a NuGet). Antes de desplegar: abrir la solución
  en Visual Studio/Rider/VS Code y revisar el panel de errores del
  compilador — la verificación aquí fue manual (balance de llaves,
  consistencia de nombres de entidades/enums contra Domain, revisión línea
  por línea de cada archivo).

## Testing (agregado en esta pasada)

Se agregaron tres proyectos de test bajo `tests/` y se registraron en
`CabinaFletes.Clean.sln`: `CabinaFletes.Domain.Tests` (unitarios puros,
portados de `NEXO/tests/CabinaFletes.Domain.Tests` y
`NEXO/src/CabinaFletes.Tests`), `CabinaFletes.Application.Tests` (nuevos,
con Moq, cubriendo Cotizaciones/ServiciosConexos/ReglasSoloSicetac/Solicitudes)
y `CabinaFletes.Api.Tests` (portados de `NEXO/tests/CabinaFletes.Api.Tests`,
integración con `WebApplicationFactory<Program>` contra SQL Server real,
incluyendo el flujo completo de una cotización de punta a punta). Ver la
sección 8 de `README.md` para el detalle de qué cubre cada proyecto y cómo
ejecutarlos.

Esto NO reemplaza el punto anterior: los tests tampoco se compilaron ni se
corrieron en este sandbox (sin SDK de .NET). Se escribieron releyendo
cuidadosamente cada firma real de Domain/Application/Infrastructure/Api, y
ese proceso encontró dos regresiones reales en `ServiciosConexosController`
(rol `ServiciosLogisticos` faltante en Crear/Editar/ImportarExcel, default
de `soloActivos` invertido), ya corregidas.

**Actualización**: se agregó `CabinaFletes.Application.Tests/AjustesVigenciaPlaza/AjustesVigenciaPlazaCommandTests.cs`,
cubriendo los tres Command Handlers del módulo (`AjustarVigenciaPlazaCommand`,
`AjustarVigenciaPlazaMasivoCommand`, `QuitarVigenciaPlazaCommand`): validaciones
de modo "fecha"/"extender", deduplicación de IDs, resolución de la fecha base al
extender (prioriza el override local sobre la vigencia SAP, y omite los IDs sin
base conocida), y que un ajuste nuevo consulta SAP solo para guardar la vigencia
original de referencia (un ajuste existente no vuelve a consultarla). Era el
único módulo restante con reglas de negocio propias en Application; se revisó
el resto (Biblioteca, TarifasGanadas, TarifasSicetacAdmin, Métricas) y sus
Command/Query Handlers son delegación pura hacia el Port correspondiente (la
lógica real vive en el repositorio de Infrastructure, por la misma decisión de
diseño ya documentada en MIGRACION.md para Biblioteca) — no hay regla de
negocio de Application que valga la pena aislar con un mock ahí; su cobertura
queda en `Api.Tests` (`TarifasHistoricasTests`, `TarifasSicetacTests`,
`MetricasTests`), que ya ejercitan esos flujos de punta a punta.

### No existe pipeline de CI para .NET (bloquea el cierre de S3-HU-06.2, #121)

Los tests de .NET **no corren en ningún pipeline**: solo se ejecutan cuando
alguien los lanza a mano. Verificado — `.github/workflows/` tiene únicamente
`cubicador-ci.yml` (pytest + smoke de Docker, solo Python) y
`add-to-project-nexo.yml` (automatización del tablero). **Ningún workflow
menciona `dotnet`.**

Es lo que impide cerrar el criterio de S3-HU-06.2: *"el test debe fallar el
build si algún nombre deja de coincidir — no debe depender de que alguien lo
note"*. La guarda ya existe y está verificada por mutación
(`tests/CabinaFletes.Api.Tests/Cubicacion/ContratoMessageTypeTests.cs`), pero
**sin build que falle, sigue dependiendo de que alguien la ejecute** — justo lo
que esa HU quería eliminar. El test queda listo para engancharse el día que el
workflow exista; no hace falta tocarlo.

Qué haría falta, con el detalle que ya conocemos por haberlo hecho a mano
decenas de veces en esta sesión:

- `dotnet build` de la solución más las tres suites.
- `Domain.Tests` y `Application.Tests` corren sin nada más.
- **`Api.Tests` necesita SQL Server**: hay que levantarlo como *service
  container* en el runner y pasarle `TEST_DB_CONNECTION`
  (`ApiFixture.cs:20` cae por defecto a `Trusted_Connection`, que no sirve
  contra un SQL Server en Linux). Ojo con la espera: el contenedor tarda
  ~15 s en dar *"Recovery is complete"*, y lanzar los tests antes hace fallar
  ~69 de ellos con `SqlException` — un falso rojo que se confunde con un bug
  real.
- Filtrar por `paths` igual que `cubicador-ci.yml`, para no correrlo en PRs que
  solo tocan Python o documentación.

Beneficio más allá de #121: hoy **nada** garantiza que `main`/`QA` compilen. Un
PR que rompa la compilación de .NET entra sin que nadie se entere hasta que
alguien clona y construye.

Consolidar los proyectos de test duplicados (`tests/` y
`src/CabinaFletes.Tests` en el original) sigue pendiente como limpieza
cosmética: el original tenía tests solo en `NEXO/tests` (Domain, Api) y
`NEXO/src/CabinaFletes.Tests` (Texto, ImporteParser); ambos se consolidaron
ya en `NEXO-Clean/tests/CabinaFletes.Domain.Tests`, así que en `NEXO-Clean`
no queda duplicación — el original (`NEXO/`) queda igual, sin tocar.

## Limpieza post-migración — CERRADA (issue #2)

La migración dejó 4 archivos que solo contenían un comentario marcador
("SUPERADO"/"movido a..."). No se pudieron borrar en su momento porque la
herramienta usada entonces no permitía `rm`, así que se apartaron a
`src/_to_delete/`, fuera de los `.csproj`.

Esa carpeta ya no existe: se eliminó en la issue #2. Los cuatro tipos que
mencionaban siguen vivos en su ubicación definitiva.

- `IAuditoriaPort` → `Application/Common/Ports/`
- `IPropuestaTarifaPort` → `Application/Common/Ports/`
- `PropuestaTarifaDtos` → `Application/Common/Dtos/`
- `PropuestaTarifaNoMigradaAdapter` → reemplazado por `PropuestaTarifaAdapter`
  y `TarifarioSapAdapter` en `Infrastructure/Adapters/`

## Antes de correr el proyecto

Falta `appsettings.{Environment}.local.json` (no versionado, uno por
desarrollador) con al menos `ConnectionStrings:CabinaFletes`. Las cadenas
`DwSice`, `DbSolucionesTi` y `DwCubos` son OPCIONALES: sin ellas, los
adapters correspondientes se degradan solos (503 accionable o
`Configurado = false`), igual que en el original — no es necesario tenerlas
todas para levantar el API. `DwCubosSap` se retiró en S2-HU-06.2: el
destinatario de la notificación de entrega ya no sale de ese DW (comercial
responsable por NIT), sale de la sesión de quien solicitó la consulta.
