# CabinaFletes — Backend (Clean Architecture)

## 1. Descripción del proyecto

CabinaFletes es el sistema interno de pricing de fletes de Botero Soto ·
Soluciones Logísticas (3PL): gestiona la Bandeja Priorizada de solicitudes
de cotización (spot y licitación), la Mesa de Costeo, la Biblioteca de
tarifas (SICE-TAC en vivo, histórico propio y caché local administrada) y
el ciclo completo de una cotización hasta su entrega al comercial.

Este repositorio (`NEXO-Clean`) es la migración del backend original
(`NEXO/`, que se conserva intacto como referencia) a una arquitectura
Clean Architecture de cuatro capas. El alcance de la migración es
exclusivamente el backend (`src/`); no incluye frontend. Cubicación 3D y
Plan de Cargue quedaron fuera de alcance por decisión explícita: el plan es
reemplazarlos por un microservicio Python aparte, y por eso siguen viviendo,
sin cambios, en `NEXO/`. El detalle de cada módulo migrado y las decisiones
de diseño tomadas está en [`MIGRACION.md`](MIGRACION.md); el trabajo
pendiente y lo explícitamente excluido está en [`PENDIENTES.md`](PENDIENTES.md).

## 2. Arquitectura

Cuatro capas, con dependencias apuntando siempre hacia el centro (Domain no
depende de nada; Api es la única capa que conoce ASP.NET Core):

```
                    ┌─────────────────┐
                    │       Api        │  Controllers, DTOs de request/response,
                    │                   │  middlewares, autenticación, Program.cs
                    └─────────┬─────────┘
                              │ depende de
                    ┌─────────▼─────────┐
                    │   Application     │  Commands/Queries (CQRS propio, sin
                    │                   │  MediatR), Ports (interfaces),
                    │                   │  DTOs, reglas de orquestación
                    └─────────┬─────────┘
                              │ depende de
                    ┌─────────▼─────────┐
                    │      Domain       │  Entidades, enums, Scoring
                    │                   │  (ScorePrioridadCalculator, función pura),
                    │                   │  Common (Texto, ImporteParser)
                    └─────────▲─────────┘
                              │ implementa los Ports de
                    ┌─────────┴─────────┐
                    │  Infrastructure   │  EF Core (CabinaFletesDbContext,
                    │                   │  Migrations), Repositories, Adapters
                    │                   │  (SMTP, Excel, DW externos)
                    └───────────────────┘
```

Application define los `Ports` (interfaces) que necesita cada caso de uso;
Infrastructure los implementa. Api solo conoce Application (nunca
Infrastructure directamente) y se registra todo en el composition root
(`Api/Program.cs` + `Infrastructure/DependencyInjection.cs` +
un `AddXUseCases()` por módulo de Application).

### CQRS sin librerías de terceros

Cada caso de uso es un `Command`/`Query` + su `Handler`:

```csharp
public interface ICommand<TResult> { }
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken ct = default);
}
// IQuery<TResult> / IQueryHandler<TQuery, TResult> son el espejo de lectura.
```

No hay mediador/dispatcher: el controller inyecta directamente el
`ICommandHandler<TComando, TResultado>` o `IQueryHandler<...>` que necesita.
Se eligió deliberadamente NO usar MediatR (cambió su licencia a un modelo
comercial para uso empresarial) — la indirección de un mediador no aporta
valor aquí con un handler por caso de uso.

### Módulos de negocio (Monolito Modular)

| Módulo | Ruta base | Resumen |
|---|---|---|
| Cotizaciones | `/api/cotizaciones` | Ciclo de vida de una cotización: crear, agregar líneas/servicios conexos, aplicar propuestas de tarifa, entregar, marcar resultado, exportar a Excel. Módulo piloto de la migración. |
| Solicitudes / Captura | `/api/solicitudes`, `/api/captura-solicitudes` | Alta de solicitudes (spot/licitación), Bandeja Priorizada por `ScorePrioridad`, captura de líneas (manual o Excel), auto-costeo diferido en segundo plano, recálculo periódico de score (`RecalculoScoreHostedService`). |
| Biblioteca | `/api/biblioteca-sicetac`, `/api/solicitudes/{id}/propuesta-tarifa`, `/api/biblioteca-sap-plaza` | Matriz SICE-TAC oficial EN VIVO desde el Data Warehouse, tarifario SAP TM y motor de propuesta de tarifa (compara plaza SAP vs SICE-TAC vs histórico). |
| Reglas Solo-SICETAC | `/api/reglas-solo-sicetac` | Rutas + tipo de vehículo marcadas para cotizar exclusivamente con el piso SICE-TAC. |
| Ajustes de Vigencia de Plaza | `/api/biblioteca-sap-plaza/{id}/vigencia` | Ajustar/quitar vigencia de tarifas de plaza SAP TM, individual y masivo. |
| Servicios Conexos | `/api/servicios-conexos` | Catálogo administrado de servicios logísticos/adicionales (crear, editar, listar, import Excel). |
| Tarifas Ganadas | `/api/tarifas-historicas` | Biblioteca de Tarifas Ganadas: histórico LOCAL (tarifas ya cobradas en cotizaciones ganadas), sugerencias para el costeo y búsqueda con cortes de vigencia. |
| Tarifas SICE-TAC Admin | `/api/tarifas-sicetac` | Caché LOCAL del piso SICE-TAC, cargada por import Excel administrativo (distinta de Biblioteca, que consulta el DW en vivo). |
| Métricas | `/api/metricas` | KPIs del Tablero del Líder: cumplimiento de SLA, licitaciones perdidas, cola por estado. |

Fuera de este repositorio (viven en `NEXO/`, no migrados a propósito):
Cubicación 3D y Plan de Cargue.

## 3. Infraestructura

### Base de datos

SQL Server. `CabinaFletesDbContext` (EF Core 10) es la única fuente de
escritura; las migraciones ya versionadas viven en
`src/CabinaFletes.Infrastructure/Persistence/Migrations`.

### Data Warehouses corporativos (solo lectura, todos opcionales)

| Cadena | Fuente | Usada por | Si no está configurada |
|---|---|---|---|
| `DwSice` | `DWSice.dbo.tblRutasSicetac` (SQL Server 2008 R2) | Biblioteca (matriz SICE-TAC en vivo) | El endpoint responde `503` accionable (`BibliotecaSicetacNoConfiguradoAdapter`) |
| `DbSolucionesTi` | `dbsolucionesTI.dbo.tblSAPTarifasLog` | Biblioteca (tarifario SAP TM / propuesta de tarifa) | El adapter correspondiente queda sin registrar el DbContext (se degrada) |
| `DwCubos` | `DWCubosOperacionesEBS.dbo.DimBPSolicitante` | Solicitudes/Captura (catálogo de clientes reales) | `ClientesDwAdapter.Configurado = false`; la búsqueda cae a la tabla local |
| `DwCubosSap` | `DWCubosEBSSAP.dbo.DimCliente` | Cotizaciones (comercial responsable del cliente al entregar) | `ComercialResponsableSapAdapter.Configurado = false` |

El DW `DwSice`/`DbSolucionesTi` corre en SQL Server 2008 R2 y solo negocia
TLS 1.0 con cifradores heredados; el `Dockerfile` de la Api incluye una
configuración OpenSSL propia (`/etc/ssl/openssl-legacy-tls.cnf`) para que el
contenedor pueda completar ese handshake sin afectar al resto del sistema.

### Servicios externos

- **SMTP corporativo** (opcional): notifica al comercial al entregar una
  cotización. Sin `Smtp:Host`/`Smtp:Remitente` configurados, la entrega
  sigue funcionando pero no envía el correo.
- **Azure AD / Microsoft Entra ID**: autenticación JWT Bearer. Sin
  `AzureAd:ClientId`, la Api usa un esquema de desarrollo
  (`DevAuthenticationHandler`); en `ASPNETCORE_ENVIRONMENT=Production` el
  arranque EXIGE que Azure AD esté configurado (falla rápido, por seguridad).
- **Cubicador 3D** (microservicio Python, PENDIENTE): no implementado
  todavía. `Cubicador3D__BaseUrl`/`Cubicador3D__ApiKey` están reservadas en
  `.env.example` y `docker-compose.yml` para cuando exista, pero hoy ningún
  puerto ni adapter las lee. Ver `PENDIENTES.md`.

### Variables de entorno

Ver [`.env.example`](.env.example) para el listado completo y comentado.
Resumen de las obligatorias para levantar la Api:

- `ConnectionStrings__CabinaFletes` (o `MSSQL_SA_PASSWORD` si se usa Docker Compose).
- Todo lo demás (DWs, SMTP, Azure AD, Cubicador 3D) es opcional: el sistema
  se degrada, nunca se cae, si falta.

### Docker

- `src/CabinaFletes.Api/Dockerfile`: build multi-stage (.NET 10 SDK → ASP.NET
  Core runtime), usuario sin privilegios, healthcheck vía `/health`.
- `docker-compose.yml` (raíz): servicios `db` (SQL Server 2022) y `api`, más
  un perfil `seed` para sembrar datos de demostración en un solo comando.
  No incluye frontend (fuera de alcance de este repositorio) ni el
  Cubicador 3D (no implementado; queda un bloque comentado de ejemplo).

## 4. Flujo del sistema

### Flujo principal: de la solicitud a la cotización entregada

1. Se crea una `Solicitud` (spot o licitación) — `POST /api/solicitudes`.
   Se calcula su `ScorePrioridad` (`Domain.Scoring.ScorePrioridadCalculator`,
   función pura: urgencia por deadline, valor estimado, probabilidad de
   cierre y tipo, con pesos configurables en `ParametrosScore`).
2. Se capturan las líneas (`POST /api/solicitudes/{id}/lineas:bulk` o
   `/api/captura-solicitudes` desde Excel). Si la captura es grande, el
   auto-costeo corre en segundo plano (`IServiceScopeFactory`, resuelto en
   un scope propio para no arrastrar un `DbContext` ya dispuesto al terminar
   la request).
3. La Bandeja Priorizada (`GET /api/solicitudes?orderBy=score`) muestra las
   solicitudes ordenadas por score; un `BackgroundService`
   (`RecalculoScoreHostedService`) recalcula el score periódicamente sobre
   las solicitudes activas.
4. Se crea la `Cotizacion` (`POST /api/cotizaciones`), se aplican propuestas
   de tarifa (`POST /api/cotizaciones/{id}/aplicar-propuestas` — compara
   plaza SAP vs SICE-TAC vs histórico vía Biblioteca), se agregan servicios
   conexos y se entrega (`POST /api/cotizaciones/{id}/entregar`, notifica al
   comercial responsable por correo si SMTP está configurado).
5. Al marcar el resultado (`POST /api/cotizaciones/{id}/resultado`), si se
   gana, la tarifa aplicada realimenta la Biblioteca de Tarifas Ganadas
   (`TarifaHistorica`) para futuras sugerencias de costeo.

### Flujo de autenticación

JWT Bearer contra Azure AD (Microsoft Entra ID) cuando está configurado;
`DevAuthenticationHandler` en desarrollo sin Azure AD. Autorización por
rol (`[Authorize(Roles = "...")]`) en cada endpoint que lo requiere —
ver la tabla de módulos en la sección 2 y el detalle de roles en cada
controller.

## 5. Stack tecnológico

- **.NET 10** — ASP.NET Core Web API, C#.
- **Entity Framework Core 10** — proveedor SQL Server.
- **ClosedXML** — lectura/escritura de Excel (import de tarifas, servicios
  conexos, líneas de solicitud; exportación de cotizaciones).
- **CQRS propio** — sin MediatR ni otra librería de terceros.
- **SQL Server** — base de datos principal y (solo lectura) los cuatro Data
  Warehouses corporativos.
- **Docker / Docker Compose** — empaquetado y orquestación local.
- **Azure AD (Microsoft Entra ID)** — autenticación JWT Bearer en producción.

## 6. Ejecución local

### Sin Docker

```bash
# 1) Restaurar y compilar
dotnet restore CabinaFletes.Clean.sln
dotnet build CabinaFletes.Clean.sln

# 2) Configurar la cadena de conexión (elige una opción):
#    a) variables de entorno (ver .env.example), o
#    b) src/CabinaFletes.Api/appsettings.Development.local.json (no versionado):
#       { "ConnectionStrings": { "CabinaFletes": "Server=localhost;Database=CabinaFletes;Trusted_Connection=True;TrustServerCertificate=True;" } }

# 3) Aplicar migraciones y sembrar datos de demostración
dotnet run --project src/CabinaFletes.Api seed

# 4) Levantar la Api (por defecto en http://localhost:5080, ver ASPNETCORE_URLS)
dotnet run --project src/CabinaFletes.Api
```

Swagger UI queda disponible en `/swagger` cuando
`ASPNETCORE_ENVIRONMENT=Development`.

### Con Docker Compose

```bash
cp .env.example .env
# Editar .env: como mínimo, MSSQL_SA_PASSWORD (>= 8 caracteres, con
# mayúscula, minúscula, dígito y símbolo; evitar '$' y '#').

docker compose up -d --build
# La Api queda en http://localhost:${API_PORT:-8080} (health: /health).
# MIGRATE_ON_STARTUP=true y SEED_ON_STARTUP=true (valores por defecto)
# aplican migraciones y siembran datos de demostración automáticamente.

# Sembrar manualmente en cualquier momento (alternativa a *_ON_STARTUP):
docker compose --profile seed run --rm seed
```

### QA / Staging

Mismo `docker-compose.yml`, con `ASPNETCORE_ENVIRONMENT=Staging` y las
cadenas de conexión de QA en las variables de entorno del entorno de
despliegue (Dokploy, o el orquestador que corresponda). En Staging/QA no es
necesario tener las cadenas de los Data Warehouses corporativos: los
módulos que las usan se degradan solos.

## 7. API

Todos los endpoints requieren autenticación (`[Authorize]`); algunos exigen
además un rol específico (`Roles = "..."`), indicado entre paréntesis.
Los errores siguen un formato uniforme (`ErrorHandlingMiddleware`):

```json
{ "codigo": "CODIGO_DE_ERROR", "mensaje": "Descripción legible.", "fecha": "2026-08-13T12:00:00Z", "detalles": { } }
```

| Código HTTP | Origen |
|---|---|
| 400 | `ValidationFailedException` — regla de negocio violada o input inválido |
| 404 | `NotFoundException` — el recurso no existe |
| 409 | `ConflictException` — conflicto con el estado actual del recurso |
| 503 | `ServiceUnavailableException` — dependencia externa no configurada (p. ej. un DW opcional) |

### Cotizaciones — `/api/cotizaciones`

| Método | Ruta | Rol | Propósito |
|---|---|---|---|
| POST | `/` | Comercial, AdminTarifas, CoordinadorFletes, Admin | Crear cotización para una solicitud |
| GET | `/{id}` | cualquiera autenticado | Obtener cotización |
| POST | `/{id}/aplicar-propuestas` | Comercial, AdminTarifas, CoordinadorFletes, Admin | Aplicar propuestas de tarifa (Biblioteca) a las líneas |
| POST | `/{id}/lineas` | AdminTarifas, CoordinadorFletes, Admin | Agregar línea de flete |
| POST | `/{id}/conexos` | Comercial, CoordinadorFletes, Admin | Agregar servicios conexos |
| POST | `/{id}/entregar` | AdminTarifas, CoordinadorFletes, Admin | Entregar al comercial (dispara notificación SMTP) |
| POST | `/{id}/resultado` | Comercial, AdminTarifas, CoordinadorFletes, Admin | Marcar ganada/perdida (realimenta Tarifas Ganadas si gana) |
| GET | `/{id}/exportar-excel` | cualquiera autenticado | Exportar la cotización a Excel |

### Solicitudes / Captura — `/api/solicitudes`, `/api/captura-solicitudes`

| Método | Ruta | Rol | Propósito |
|---|---|---|---|
| GET | `/api/solicitudes` | cualquiera autenticado | Bandeja Priorizada (filtros por atención/estado/fechas) |
| GET | `/api/solicitudes/{id}` | cualquiera autenticado | Detalle con líneas |
| POST | `/api/solicitudes` | CoordinadorFletes, Admin | Alta de solicitud (spot/licitación) |
| POST | `/api/solicitudes/{id}/lineas:bulk` | CoordinadorFletes, Admin | Carga masiva de líneas |
| POST | `/api/solicitudes/{id}/rechazar` | Comercial, CoordinadorFletes, Admin | Rechazar/descartar |
| POST | `/api/solicitudes/{id}/recalcular-score` | CoordinadorFletes, Admin | Recalcular score manualmente |
| POST | `/api/captura-solicitudes/analizar-excel` | Comercial, Admin | Analizar un Excel antes de capturarlo |
| POST | `/api/captura-solicitudes` | Comercial, Admin | Capturar líneas desde Excel (dispara auto-costeo si aplica) |
| GET | `/api/captura-solicitudes/aprovisionamiento` | Comercial, Admin | Estimación de aprovisionamiento |
| GET | `/api/captura-solicitudes/clientes` | Comercial, Admin | Búsqueda de clientes (local + DW) |

### Biblioteca — `/api/biblioteca-sicetac`, `/api/solicitudes/{id}/propuesta-tarifa`, `/api/biblioteca-sap-plaza`

| Método | Ruta | Rol | Propósito |
|---|---|---|---|
| GET | `/api/biblioteca-sicetac` | cualquiera autenticado | Matriz SICE-TAC (filtros por ruta/config/carga/vía) |
| GET | `/api/biblioteca-sicetac/periodos` | cualquiera autenticado | Periodos disponibles en el DW |
| GET | `/api/biblioteca-sicetac/filtros` | cualquiera autenticado | Valores disponibles para los filtros |
| GET | `/api/solicitudes/{id}/propuesta-tarifa` | cualquiera autenticado | Propuesta de tarifa para una solicitud (plaza SAP vs SICE-TAC vs histórico) |
| PATCH | `/api/biblioteca-sap-plaza/{tarifaId}/vigencia` | AdminTarifas, CoordinadorFletes, Admin | Ajustar vigencia de una tarifa de plaza |
| POST | `/api/biblioteca-sap-plaza/vigencia/masivo` | AdminTarifas, CoordinadorFletes, Admin | Ajuste masivo de vigencia |
| DELETE | `/api/biblioteca-sap-plaza/{tarifaId}/vigencia` | AdminTarifas, CoordinadorFletes, Admin | Quitar el ajuste de vigencia |

### Reglas Solo-SICETAC — `/api/reglas-solo-sicetac` (rol: AdminTarifas, Admin)

| Método | Ruta | Propósito |
|---|---|---|
| GET | `/` | Listar reglas |
| POST | `/` | Crear regla (origen + destino + tipo de vehículo) |
| DELETE | `/{id}` | Eliminar regla |
| POST | `/importar-excel` | Carga masiva desde Excel |

### Servicios Conexos — `/api/servicios-conexos`

| Método | Ruta | Rol | Propósito |
|---|---|---|---|
| GET | `/` | cualquiera autenticado | Listar catálogo (paginado, filtros por texto/categoría/activos) |
| POST | `/` | AdminTarifas, Admin | Crear servicio |
| PATCH | `/{id}` | AdminTarifas, Admin | Editar servicio (parcial) |
| POST | `/importar-excel` | AdminTarifas, Admin | Carga masiva desde Excel |

### Tarifas Ganadas — `/api/tarifas-historicas` (cualquiera autenticado)

| Método | Ruta | Propósito |
|---|---|---|
| GET | `/?ruta=&vehiculo=&mercancia=` | Sugerencias para el costeo |
| GET | `/?q=&mercancia=&vigencia=&origen=` | Búsqueda filtrada de la Biblioteca |

### Tarifas SICE-TAC Admin — `/api/tarifas-sicetac`

| Método | Ruta | Rol | Propósito |
|---|---|---|---|
| GET | `/` | cualquiera autenticado | Consultar el piso SICE-TAC local |
| POST | `/importar` | CoordinadorFletes, Admin | Cargar Excel SICE-TAC (upsert idempotente) |
| GET | `/importaciones` | cualquiera autenticado | Historial de cargas |

### Métricas — `/api/metricas` (rol: LiderProceso, AdminTarifas, Admin)

| Método | Ruta | Propósito |
|---|---|---|
| GET | `/cumplimiento` | KPIs de cumplimiento de SLA y cola por estado |

## 8. Testing

La migración incluye tres proyectos de test bajo `tests/`, alineados con las
capas que tiene sentido probar de forma distinta:

- **`CabinaFletes.Domain.Tests`** — unitarios puros, sin dependencias.
  Cubren `ScorePrioridadCalculator` (confirma que el score total es
  exclusivamente el componente de urgencia por deadline, no una suma
  ponderada), `Texto` (normalización de tildes/mayúsculas, partición de
  rutas) e `ImporteParser` (formato colombiano vs. invariante).
- **`CabinaFletes.Application.Tests`** — unitarios con Moq, Ports mockeados
  (`ICotizacionRepository`, `ISolicitudesPort`, `IAuditoriaPort`,
  `ICurrentUserPort`, `IUnitOfWork`, etc.). Cubren los Command Handlers con
  más reglas de negocio: `CrearCotizacionCommand` (reutiliza borrador
  existente, reabre cotización entregada), `MarcarResultadoCommand`
  (idempotencia, transición Ganada→Perdida prohibida, realimentación de la
  Biblioteca de Tarifas Ganadas una sola vez por línea), `CrearServicioConexoCommand`
  (normalización de categoría), `CrearReglaSoloSicetacCommand` (normalización
  de texto, duplicados) y `CrearSolicitudCommand` (score calculado al crear).
- **`CabinaFletes.Api.Tests`** — integración end-to-end con
  `WebApplicationFactory<Program>` contra una base SQL Server real de
  pruebas (`TEST_DB_CONNECTION`, por defecto
  `Server=localhost;Database=CabinaFletesClean_Test;Trusted_Connection=True;TrustServerCertificate=True;`),
  con autenticación de prueba por header (`X-Test-Role`, ver
  `Infra/TestAuthHandler.cs`). Incluyen el flujo de negocio completo
  (`CotizacionesTests.Flujo_completo_cotizacion`: crear solicitud con línea
  → crear cotización → agregar línea de flete → agregar conexos → entregar
  → marcar resultado ganada → verificar que realimenta la Biblioteca de
  Tarifas Ganadas), además de Solicitudes (bandeja priorizada), Servicios
  Conexos, Tarifas Históricas, Tarifas SICE-TAC (import de Excel
  idempotente con ClosedXML) y Métricas.

Cómo ejecutar:

```
dotnet test CabinaFletes.Clean.sln
```

`CabinaFletes.Api.Tests` requiere una instancia de SQL Server accesible
(local o contenedor); los otros dos proyectos no necesitan base de datos.
`ApiFixture` crea la base de pruebas, corre las migraciones y siembra los
datos (`DataSeeder`) una sola vez por corrida de test.

**Nota importante sobre esta migración**: ninguno de los archivos `.cs` de
`src/` ni de `tests/` se compiló en el entorno donde se escribió esta
migración (sandbox sin SDK de .NET ni acceso a NuGet). La verificación
hecha aquí fue manual y exhaustiva: cada test —tanto los portados desde
`NEXO/tests` como los nuevos— se escribió releyendo el código fuente real
de Domain/Application/Infrastructure/Api (entidades, enums, firmas de
Ports, DTOs) para que mocks y aserciones coincidieran exactamente. Ese
mismo proceso de escribir los tests sirvió de auditoría cruzada y encontró
dos regresiones reales de la migración original, ya corregidas: en
`ServiciosConexosController` faltaba el rol `ServiciosLogisticos` en
Crear/Editar/ImportarExcel (quedó solo `AdminTarifas,Admin`) y el valor por
defecto de `soloActivos` estaba invertido (`true` en vez de `false`).
Antes de desplegar a producción, de todas formas:

1. Abrir la solución en Visual Studio / Rider / VS Code y revisar el panel
   de errores del compilador.
2. Ejecutar `dotnet build CabinaFletes.Clean.sln` y corregir lo que salga.
3. Ejecutar `dotnet test` con una base de datos de pruebas real disponible.

## 9. Estructura del proyecto

```
NEXO-Clean/
  CabinaFletes.Clean.sln
  .env.example              # Variables de entorno documentadas (backend + Docker Compose)
  .gitignore
  docker-compose.yml         # db + api (+ perfil seed); sin frontend, fuera de alcance
  MIGRACION.md               # Historial detallado de cada pasada de la migración
  PENDIENTES.md              # Trabajo pendiente y exclusiones explícitas
  README.md                  # Este documento
  src/
    CabinaFletes.Domain/            # Entidades, Enums, Scoring, Common (Texto, ImporteParser)
    CabinaFletes.Application/       # Un subdirectorio por módulo de negocio:
      Common/                       #   Abstractions (ICommand/IQuery/IUnitOfWork/Unit),
      Cotizaciones/                 #   Ports/Dtos compartidos entre módulos (IAuditoriaPort,
      Solicitudes/                  #   IPropuestaTarifaPort), ICurrentUserPort
      Biblioteca/                   #
      ReglasSoloSicetac/            #   Cada módulo: Commands/, Queries/, Dtos/, Ports/,
      AjustesVigenciaPlaza/         #   DependencyInjection.cs (AddXUseCases())
      ServiciosConexos/             #
      TarifasGanadas/               #
      TarifasSicetacAdmin/          #
      Metricas/                    #
    CabinaFletes.Infrastructure/
      Persistence/                  # CabinaFletesDbContext, Migrations, DataSeeder
      External/                    # DbContexts de los DW corporativos (opcionales)
      Repositories/                 # Implementaciones EF Core de los Ports de escritura/lectura
      Adapters/                     # SMTP, Excel, integraciones que no son solo EF Core
      Excel/                        # Exportación a Excel (ClosedXML)
      DependencyInjection.cs         # AddInfrastructure(): composition root de Infrastructure
    CabinaFletes.Api/
      Controllers/                  # Un controller por módulo, delegan en Handlers
      Dtos/                         # Request DTOs (con DataAnnotations)
      BackgroundServices/            # RecalculoScoreHostedService
      Middleware/                    # ErrorHandlingMiddleware
      Auth/                          # DevAuthenticationHandler, ICurrentUser
      Errors/                        # ApiError, ExcelUpload
      Dockerfile
      Program.cs                     # Composition root: AddInfrastructure() + AddXUseCases()
  tests/
    CabinaFletes.Domain.Tests/        # Unitarios puros (Score, Texto, ImporteParser)
    CabinaFletes.Application.Tests/   # Unitarios con Moq, un subdirectorio por módulo
    CabinaFletes.Api.Tests/           # Integración con WebApplicationFactory + SQL Server real
      Infra/                          # ApiFixture, TestAuthHandler, Http (helpers de test)
```
