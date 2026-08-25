# Migración de NEXO a Clean Architecture — estado

Este directorio (`NEXO-Clean`) es una migración EN CURSO de `NEXO/` (el proyecto
original, intacto) hacia la arquitectura de 4 capas del diagrama objetivo
(Api / Application con Handlers CQRS + Puertos + UoW / Domain / Infrastructure).
Se construyó con la skill `dotnet-enterprise-architect` y solo cubre el
backend (`src/`), tal como se acordó.

## Alcance de esta primera pasada

Módulo piloto migrado end-to-end: **Cotizaciones** (`/api/cotizaciones`).
Se eligió como piloto por su tamaño acotado y porque permite validar el patrón
completo (Api -> Application -> Domain, con Infrastructure implementando los
puertos) sin arrastrar toda la lógica de negocio del sistema de una vez.

## Segunda pasada: las "2 chicas" de Biblioteca

Al revisar qué hacía falta para que `AplicarPropuestas` funcionara, se
encontró que Biblioteca son en realidad 4 servicios encadenados (~90 KB en
total). Se decidió con el usuario migrar primero las 2 piezas pequeñas y
autocontenidas, dejando las 2 grandes (`TarifarioSapService` y
`PropuestaTarifaService`) para una iteración posterior:

- **Ajuste Vigencia Plaza** (`/api/biblioteca-sap-plaza/{id}/vigencia`,
  `TarifasPlazaVigenciaController`): ajustar/quitar vigencia de tarifas de
  plaza SAP TM, individual y masivo.
- **Reglas Solo-Sicetac** (`/api/reglas-solo-sicetac`,
  `ReglasSoloSicetacController`): CRUD de reglas + importación por Excel.

Ambos módulos siguen el mismo patrón que Cotizaciones (Commands/Queries +
Ports + Handlers propios) y quedaron correctamente registrados en el
composition root (`Api/Program.cs`: `AddReglasSoloSicetacUseCases()` y
`AddAjustesVigenciaPlazaUseCases()`, junto a `AddCotizacionesUseCases()`).
Trajeron consigo `DbSolucionesTiDbContext` y los catálogos SAP
(`SapCatalogos`, `SapDane`, `SapTarifaHistorico`, `SapTarifario`) que estas
dos piezas necesitan para resolver códigos/zonas.

## Decisiones tomadas con el usuario

- **.NET 10** (el diagrama lo pedía explícitamente; el original está en .NET 8).
- **CQRS con handlers propios**, sin MediatR ni otra librería de terceros
  (`ICommand<T>` / `ICommandHandler<TCommand,TResult>`, igual para Query).
  Evita una dependencia externa que además cambió su licencia para uso
  comercial/empresarial.
- **Cubicación 3D fuera de alcance**: sigue siendo lógica en C#, no se movió a
  un servicio Python. El día que exista ese servicio, solo cambia una
  implementación de Infrastructure.

## Qué se movió y por qué

- **Domain**: copia verbatim del original (Entities, Common, Scoring, Workflow,
  Enums). Esta capa no cambia de responsabilidad en la migración a Clean
  Architecture: entidades, value objects y reglas puras siguen igual.
- **Infrastructure/Persistence**: `CabinaFletesDbContext`, `DataSeeder` y las
  `Migrations` se copiaron verbatim (el esquema no cambió). `External/` solo
  trae `DwCubosSapDbContext` (el único DW que usa Cotizaciones); los demás DW
  (DwSice, DwCubos, DbSolucionesTi) se migran junto con los módulos que los usan.
- **La lógica de negocio de `CotizacionService.cs` (32 KB) se separó en**:
  - `Application/Cotizaciones/Commands/*` y `Queries/*` — un Handler por caso
    de uso (Crear, AgregarLinea, AgregarConexos, AplicarPropuestas, Entregar,
    MarcarResultado, Obtener, ExportarExcel), con la MISMA lógica y las MISMAS
    reglas de negocio (idempotencia, validaciones, máquina de estados) del
    original — se movieron, no se reescribieron.
  - `Application/Cotizaciones/Ports/*` — las interfaces (puertos) que
    Infrastructure implementa: `ICotizacionRepository`, `ISolicitudesPort`,
    `ILineasSolicitudPort`, `IServiciosConexosPort`, `ITarifasHistoricasPort`,
    `IAuditoriaPort`, `IComercialResponsablePort`, `ICorreoPort`,
    `IExportadorCotizacionExcelPort`, `IPropuestaTarifaPort`.
  - `Infrastructure/Repositories/*` — implementaciones EF Core de esos puertos.
  - `Infrastructure/Adapters/*` — SMTP (`SmtpCorreoAdapter`), comercial
    responsable vía DW SAP (`ComercialResponsableSapAdapter`).
  - `Infrastructure/Excel/ExportadorCotizacionExcelClosedXml.cs` — la
    exportación a Excel (antes un método privado del Service).
  - `Api/Controllers/CotizacionesController.cs` — quedó igual de delgado que
    antes, pero ahora inyecta los Handlers directamente (no un Service).

## Tercera pasada: Biblioteca completa (TarifarioSap + PropuestaTarifa)

Con las "2 chicas" ya migradas, se completó Biblioteca: `TarifarioSapAdapter`
(réplica de `getConsultarTarifaFlete`: reglas A/B/C/D, zonas de tarificación,
tarifas vencidas informadas como inactivas, comparación SICE-TAC oficial) y
`PropuestaTarifaAdapter` (compara plaza SAP vs SICE-TAC vs histórico SAP,
paralelizado por huella de línea única), más `BibliotecaSicetacAdapter` para
la matriz SICE-TAC de solo lectura (`/api/biblioteca-sicetac`). Con esto,
`AplicarPropuestas` en Cotizaciones ya funciona de punta a punta:
`PropuestaTarifaNoMigradaAdapter` (el stub 503) quedó superado.

Decisión de diseño explícita: a diferencia de Cotizaciones (donde Application
tiene las reglas y Infrastructure solo las consultas EF), aquí la lógica de
selección/decisión vive DENTRO de los adapters de Infrastructure
(`TarifarioSapAdapter`, `PropuestaTarifaAdapter`), no en Application. Motivo:
las reglas están indisolublemente ligadas a la forma de las tablas SAP/DW
(`IQueryable` construido dinámicamente según catálogos resueltos en runtime:
vehículo, carrocería, clase de contenedor). Extraerlas a Application habría
exigido un puerto "de bajo nivel" hacia esas mismas tablas, sin ganancia real
de testabilidad. `IPropuestaTarifaPort` e `ITarifarioSapPort` (el segundo,
`Application/Biblioteca/Ports`) son el límite Application/Infrastructure;
`IPropuestaTarifaPort` se movió a `Application/Common/Ports` (como
`IAuditoriaPort`) porque lo consumen dos módulos: Cotizaciones y Biblioteca.

También se completó el catálogo `DwSiceDbContext`/`RutaSicetacDw` (antes solo
declarado, no copiado) y se replicó el patrón "adapter no configurado" del
original (`BibliotecaSicetacNoConfiguradoAdapter`) para que, sin
`ConnectionStrings:DwSice`, el endpoint de matriz SICE-TAC responda 503
accionable en vez de fallar la inyección de dependencias.

## Pendiente — ver PENDIENTES.md

El resto de módulos del sistema (Solicitudes/Captura, Cubicación 3D — fuera
de alcance por decisión explícita, Plan de Cargue, Servicios Conexos admin,
Métricas) siguen viviendo en `NEXO/` sin cambios.

## Cómo compilar / correr

Este sandbox no tiene el SDK de .NET disponible (sin acceso de red a los
paquetes), así que este código NO se compiló aquí. Ábrelo en tu IDE (Visual
Studio / Rider / VS Code) o corre:

```
dotnet restore NEXO-Clean/CabinaFletes.Clean.sln
dotnet build NEXO-Clean/CabinaFletes.Clean.sln
```

y revisa el panel de errores. Dado que ya tenías carpetas `obj/` generándose
automáticamente en `CabinaFletes.Application` y `CabinaFletes.Api` mientras yo
escribía los archivos, tu editor probablemente ya está restaurando/compilando
en vivo — mira ahí primero.

Falta además `appsettings.*.local.json` (no versionado) con
`ConnectionStrings:CabinaFletes` como mínimo para poder correr `dotnet run`.

## Cuarta pasada: Solicitudes/Captura (Bandeja Priorizada)

Se migró `SolicitudService.cs` + `CapturaSolicitudService.cs` +
`RecalculoScoreService.cs` (hosted service) completos: alta de solicitudes
(spot/licitación), captura de líneas con Excel (ClosedXML tolerante a
encabezados), Bandeja Priorizada con `ScorePrioridad` (`Domain.Scoring.
ScorePrioridadCalculator`, reusado también en el recálculo periódico), y el
auto-costeo diferido de capturas grandes.

Decisión de diseño explícita: el auto-costeo en segundo plano usa
`IServiceScopeFactory` (abstracción pura de DI, no de ASP.NET Core) desde
`CapturarSolicitudCommand`, resolviendo sus dependencias en un scope propio
tras terminar la request — evita el riesgo de usar un `DbContext` ya
dispuesto. El `BackgroundService` de recálculo periódico vive en
`Api/BackgroundServices/RecalculoScoreHostedService.cs` (Application no debe
depender del hosting de ASP.NET Core), pero delega toda la lógica de negocio
en `Application.Solicitudes`.

`ISolicitudesPort`/`ILineasSolicitudPort` (los puertos mínimos que ya
consumía Cotizaciones) se mantuvieron separados de `ISolicitudesRepository`
(el repositorio completo de Solicitudes/Captura): fusionarlos habría hecho
que Cotizaciones dependiera de operaciones que no necesita.

## Quinta pasada: ServiciosConexos, TarifasGanadas, TarifasSicetacAdmin, Métricas

Con esto se completa el alcance acordado ("todo el backend, excluyendo el
cubicador 3D que será un microservicio Python"):

- **ServiciosConexos** (`/api/servicios-conexos`): catálogo administrado por
  Servicios Logísticos (crear/editar/listar + import Excel masivo). La
  categoría ("logistico"/"adicional") se valida estrictamente al
  crear/editar (un valor no reconocido se RECHAZA, QA-DATA-007) pero se
  normaliza tolerantemente al importar. `EfServiciosConexosRepository`
  implementa DOS puertos sobre la misma tabla: `IServiciosConexosRepository`
  (el nuevo, admin) y `IServiciosConexosPort` (el de Cotizaciones, ya
  existente, para `AgregarConexosCotizacionCommand` — lookup TRACKED a
  propósito, sin `AsNoTracking()`, porque el resultado se usa como
  navegación de un `CotizacionConexo` nuevo antes de `SaveChanges`).
- **TarifasGanadas** (`/api/tarifas-historicas`): la Biblioteca de Tarifas
  Ganadas (histórico LOCAL, `TarifaHistorica`) — sugerencias para el costeo y
  búsqueda con cortes de vigencia (vigente/por_vencer/vencida, por diferencia
  de meses vs `TimeProvider`). Distinta de Biblioteca (que consulta la
  matriz SICE-TAC EN VIVO del DW). `EfTarifasHistoricasRepository` también
  implementa dos puertos: `ITarifasHistoricasPort` (Cotizaciones, ya
  existente, registra tarifas al ganar) e `IHistoricoTarifasPort` (el nuevo,
  lectura).
  Este módulo cierra el pendiente de `BibliotecaItemDto` que había quedado
  abierto tras la tercera pasada.
- **TarifasSicetacAdmin** (`/api/tarifas-sicetac`): caché LOCAL del piso
  SICE-TAC (`TarifaSicetac`), cargada por Excel admin (import todo-en-uno con
  enlace a rutas locales por `NOMBRERUTA`, usando `Domain.Common.Texto`, y
  deduplicación dentro del mismo archivo). Distinta de Biblioteca (DW en
  vivo) y de TarifasGanadas (histórico de cotizaciones ganadas).
- **Métricas** (`/api/metricas/cumplimiento`): KPIs del Tablero del Líder
  (cumplimiento de SLA, licitaciones perdidas, cola por estado). Query único,
  sin escritura.

Mismo criterio pragmático que en Biblioteca para los tres módulos con Excel
(ServiciosConexos, TarifasSicetacAdmin): el parseo de celdas (ClosedXML) vive
completo en el repositorio de Infrastructure, no separado en Application, por
no haber una regla de negocio separable con provecho real de testabilidad.

## Fuera de alcance (decisión explícita, no pendiente)

`PlanCargueService.cs` (Plan de Cargue / cubicador 3D interactivo) se
revisó y se confirma que ES el backend del Cubicador 3D: depende
directamente de `IMotorCubicacion3D` (motor de optimización bin-packing) e
`ICubicacionService` (cubicador rápido) para crear vehículos, ejecutar la
optimización y persistir/ajustar colocaciones 3D. Por la misma decisión ya
tomada con el usuario para Cubicación 3D en general (queda en C# hasta que
exista el microservicio Python, y entonces se reemplaza por un puerto), Plan
de Cargue se deja completo en `NEXO/` sin migrar: migrarlo aquí significaría
migrar el cubicador mismo, que es justamente lo excluido.

Con las cinco pasadas anteriores, **el backend queda migrado en su
totalidad, con la única excepción intencional de Cubicación 3D y Plan de
Cargue** (ambos la misma pieza fuera de alcance).

## Sexta pasada: Docker, variables de entorno y documentación

Se agregaron los artefactos operativos que faltaban para poder levantar el
sistema fuera del IDE: `.env.example` (variables documentadas, incluida la
reserva `Cubicador3D__BaseUrl`/`Cubicador3D__ApiKey` para cuando exista el
microservicio Python — hoy sin ningún adapter que las use), `Dockerfile`
multi-stage (SDK .NET 10 -> runtime `aspnet:10.0`, con el workaround de
OpenSSL legacy TLS necesario para conectar contra el Data Warehouse SQL
Server 2008 R2 desde una imagen Debian moderna) y `docker-compose.yml`
(`db` + `api`, perfil `seed`, placeholder comentado para un futuro servicio
`cubicador3d`). Se corrigieron las claves de SMTP en `.env.example` y en el
compose (estaban en inglés; `SmtpCorreoAdapter` las lee en español:
`Smtp:Host/Puerto/Ssl/Remitente/Usuario/Clave`). Se agregó a `Program.cs` el
bloque `Database:MigrateOnStartup`/`Database:SeedOnStartup` que el compose
ya esperaba pero que no existía en `NEXO-Clean` (sí en el original).

## Séptima pasada: tests y flujos de negocio

Se agregaron tres proyectos de test bajo `tests/`, registrados en
`CabinaFletes.Clean.sln`:

- `CabinaFletes.Domain.Tests` — portados de `NEXO/tests/CabinaFletes.Domain.Tests`
  y de `NEXO/src/CabinaFletes.Tests` (`TextoTests`, `ImporteParserTests`).
- `CabinaFletes.Application.Tests` — nuevos, con Moq: `CrearCotizacionCommand`,
  `MarcarResultadoCommand`, `CrearServicioConexoCommand`,
  `CrearReglaSoloSicetacCommand`, `CrearSolicitudCommand`.
- `CabinaFletes.Api.Tests` — portados de `NEXO/tests/CabinaFletes.Api.Tests`,
  con `WebApplicationFactory<Program>` contra SQL Server real de pruebas.
  Requirió agregar `public partial class Program { }` al final de
  `Api/Program.cs` (con top-level statements, `WebApplicationFactory<Program>`
  no tiene de otra forma una clase `Program` a la que referenciarse).

El propio ejercicio de portar/escribir los tests —releer cada firma real de
Domain/Application/Infrastructure/Api para que mocks y payloads coincidieran
exactamente— funcionó como auditoría cruzada de la migración y encontró dos
regresiones reales en `Api/Controllers/ServiciosConexosController.cs`,
corregidas en esta misma pasada:

- Faltaba el rol `ServiciosLogisticos` en `Crear`, `Editar` e
  `ImportarExcel` (habían quedado solo con `AdminTarifas,Admin`, más
  restrictivo que el original).
- El valor por defecto de `soloActivos` en el listado estaba invertido
  (`true` en `NEXO-Clean` vs. `false` en `NEXO`).

También se restauró el parámetro vestigial `orderBy` en
`SolicitudesController` (se acepta por compatibilidad de forma de API pero
nunca se usó para ordenar, ni en el original: la Bandeja Priorizada siempre
ordena por `score`) — no es un bug, es paridad de firma HTTP.

Igual que el resto de la migración, estos archivos tampoco se compilaron ni
corrieron en este sandbox (sin SDK de .NET). Ver la sección 8 de
`README.md` para el detalle de cobertura y cómo ejecutar `dotnet test`.

## Octava pasada: cobertura de AjustesVigenciaPlaza y limpieza de marcadores

Se revisaron los módulos que habían quedado sin `Application.Tests` propios
(Biblioteca, AjustesVigenciaPlaza, TarifasGanadas, TarifasSicetacAdmin,
Métricas) para decidir dónde valía la pena agregar más unitarios. Conclusión:
salvo AjustesVigenciaPlaza, todos son Command/Query Handlers de delegación
pura hacia un Port (`Handle(...) => repo.XAsync(...)`) — la lógica real vive
en el repositorio de Infrastructure, la misma decisión de diseño ya tomada
para Biblioteca (ver tercera pasada) y documentada ahí. Aislar esos Handlers
con un mock no aportaría cobertura real; su comportamiento de punta a punta
ya lo cubren `Api.Tests` (`TarifasHistoricasTests`, `TarifasSicetacTests`,
`MetricasTests`).

**AjustesVigenciaPlaza sí tenía reglas de negocio propias en Application**, y
se agregó `tests/CabinaFletes.Application.Tests/AjustesVigenciaPlaza/AjustesVigenciaPlazaCommandTests.cs`
cubriendo los tres handlers:

- `AjustarVigenciaPlazaCommand`: un ajuste NUEVO consulta la vigencia SAP
  (solo para guardarla como referencia/auditoría); uno EXISTENTE se
  actualiza sin volver a consultar SAP, y un motivo en blanco no pisa el
  motivo ya guardado.
- `AjustarVigenciaPlazaMasivoCommand`: valida que el modo "fecha" traiga
  `NuevaVigencia` y que el modo "extender" traiga `Cantidad > 0` y
  `Unidad`; deduplica IDs repetidos; y, sobre todo, confirma la prioridad
  de resolución de la fecha base al extender — el override local (si ya
  existe) gana sobre la vigencia SAP, y un ID sin override NI vigencia SAP
  se omite del cómputo (no cuenta como aplicado, pero sí como solicitado).
- `QuitarVigenciaPlazaCommand`: retorna `false` sin guardar si no había
  ajuste previo; retorna `true` y guarda si sí lo había.

Por separado, se resolvió la limpieza pendiente de los 4 archivos marcadores
("SUPERADO"/"movido a...") que el puente al dispositivo no podía borrar con
`rm`: se movieron a `src/_to_delete/` (fuera de los `.csproj`, ya no forman
parte del árbol de código compilable). El borrado físico definitivo de esa
carpeta quedó pendiente en su momento; se hizo después, en la issue #2.
