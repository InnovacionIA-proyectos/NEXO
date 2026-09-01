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
defecto (`Cubicacion:ParidadVerificada=false`) — todo intento responde `503
CUBICACION_EN_VERIFICACION`, sin ningún fallback. Kevin (@kcano-ux) es quien
la enciende, y solo después de confirmar 26/26 en los golden tests de
paridad de `cubicador-ci.yml` (S2-HU-07.3): configurar juntas
`Cubicacion:ParidadVerificada=true`, `Cubicacion:ParidadEvidencia` (la corrida
de CI concreta que confirmó 26/26 — URL o commit) y `Cubicacion:ParidadFecha`.
Las tres son obligatorias a la vez (`Domain.PlanDeCargue.Reglas.
CompuertaDeParidad`): `ParidadVerificada=true` sin las otras dos dejas la
compuerta cerrada igual, a propósito — no es un booleano suelto.

**Lo que SÍ falta, sin fecha todavía**:
- **Solo soporta re-optimizar un plan que YA EXISTE** (con sus ítems) en la
  base de Cubicador. El flujo "pedir un plan de cargue desde una línea de
  solicitud" (`crear_desde_linea`, ver `cubicador/docs/CONTRATOS.md` sección
  B) depende de un endpoint `GET /api/lineas-solicitud/{id}/cubicacion` que
  Cubicador ya llama (`NexoLineaSolicitudClient`) pero que **NEXO todavía no
  implementa** — y de datos de dimensiones/cantidad de bultos por línea que
  hoy `LineaSolicitud` no captura. Es trabajo real, no trivial: requiere
  decidir dónde vive esa captura antes de construir el endpoint.
- El endpoint de arriba, cuando exista, es autenticación servicio-a-servicio
  (`Authorization: Bearer NEXO_CALLBACK_API_KEY`), no la sesión del portal —
  necesita su propio esquema de autenticación aparte de
  `IdentidadPortalAuthenticationHandler` (S2-HU-03).
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

## Set de datos para el equipo de datos (S2-HU-10.1, #35)

Vistas de solo lectura en el esquema `analitica` (migración
`VistasAnaliticaSoloLectura`), diccionario completo en
[`docs/analitica/DICCIONARIO_DATOS.md`](docs/analitica/DICCIONARIO_DATOS.md).
Cobertura: solicitudes, líneas, cotizaciones, líneas de cotización, conexos,
tarifas ganadas archivadas, auditoría, y catálogos (clientes/rutas/
vehículos/servicios/usuarios) — más `analitica.inconsistencias`, que
documenta registros con datos irregulares en vez de descartarlos en
silencio. Verificado contra SQL Server real (Docker): migración y rollback
aplicados sin error, las 13 vistas devuelven filas contra el dataset de
demostración.

**Pendiente, fuera del alcance de esta sesión**: ejecutar la extracción real
(el export que recibe el equipo de datos) contra la base de producción, en
ventana de bajo uso, con una cuenta de solo lectura sobre el esquema
`analitica` — esta sesión no tiene acceso a esa base. El alcance acordado
("foto única") también deja fuera de este repositorio los extractos de las
4 fuentes de lectura externas (DwSice/DwCubos/DbSolucionesTi): el issue #35
es específicamente sobre la BD propia (`CabinaFletes`).

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
