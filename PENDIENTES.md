# Pendientes de la migración

## Estado: migración del backend COMPLETA (excepto Cubicación 3D, exclusión intencional)

Todos los módulos de negocio de `NEXO/src` están migrados a
`NEXO-Clean/src` con la arquitectura de 4 capas: Cotizaciones (piloto),
ReglasSoloSicetac, AjustesVigenciaPlaza, Biblioteca (TarifarioSap +
PropuestaTarifa + matriz SICE-TAC), Solicitudes/Captura (Bandeja Priorizada +
auto-costeo + recálculo periódico), ServiciosConexos, TarifasGanadas
(Biblioteca de Tarifas Ganadas), TarifasSicetacAdmin y Métricas. Ver
MIGRACION.md para el detalle de cada pasada y las decisiones de diseño.

## Fuera de alcance (decisión explícita del usuario, no un pendiente)

- **Cubicación 3D** (`Api/Services/Cubicacion3D/*`) y **Plan de Cargue**
  (`PlanCargueService.cs`, el backend que ORQUESTA el cubicador 3D) — el plan
  es reemplazarlos por un microservicio Python. Quedan completos en `NEXO/`
  sin tocar. El día que exista ese microservicio, la integración desde
  Clean Architecture es un puerto nuevo en `Application` (p. ej.
  `ICubicacion3DPort`) implementado por un adapter HTTP en `Infrastructure`,
  sin tocar Api ni el resto de Application.

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
