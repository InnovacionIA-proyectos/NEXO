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
exclusivamente el backend (`src/`); no incluye frontend. El Plan de Cargue
(S2-HU-08) no se migró tal cual: se reemplazó por una integración por colas
con Cubicador, un motor de cubicación 3D aparte (microservicio Python,
carpeta `cubicador/` de este mismo repo) — ver sección 2. El detalle de cada
módulo migrado y las decisiones de diseño tomadas está en
[`MIGRACION.md`](MIGRACION.md); el trabajo pendiente está en
[`PENDIENTES.md`](PENDIENTES.md).

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
| Biblioteca | `/api/biblioteca-sicetac`, `/api/solicitudes/{id}/propuesta-tarifa`, `/api/biblioteca-sap-plaza` | Matriz SICE-TAC oficial EN VIVO desde el Data Warehouse, tarifario SAP TM y motor de propuesta de tarifa (compara plaza SAP vs SICE-TAC vs histórico). Plaza puede venir de un dataset provisional mientras la conexión SAP nueva no exista (S2-HU-09) — ver sección 3. |
| Reglas Solo-SICETAC | `/api/reglas-solo-sicetac` | Rutas + tipo de vehículo marcadas para cotizar exclusivamente con el piso SICE-TAC. |
| Ajustes de Vigencia de Plaza | `/api/biblioteca-sap-plaza/{id}/vigencia` | Ajustar/quitar vigencia de tarifas de plaza SAP TM, individual y masivo. |
| Servicios Conexos | `/api/servicios-conexos` | Catálogo administrado de servicios logísticos/adicionales (crear, editar, listar, import Excel). |
| Tarifas Ganadas | `/api/tarifas-historicas` | Biblioteca de Tarifas Ganadas: histórico LOCAL (tarifas ya cobradas en cotizaciones ganadas), sugerencias para el costeo y búsqueda con cortes de vigencia. |
| Tarifas SICE-TAC Admin | `/api/tarifas-sicetac` | Caché LOCAL del piso SICE-TAC, cargada por import Excel administrativo (distinta de Biblioteca, que consulta el DW en vivo). |
| Métricas | `/api/metricas` | KPIs del Tablero del Líder: cumplimiento de SLA, licitaciones perdidas, cola por estado. |
| Plan de Cargue | `/api/planes-cargue` | Orquestación async del motor Cubicador (S2-HU-08): pedir la optimización de un plan (202+id), avance por SignalR, consulta de respaldo. Cerrado por compuerta de paridad hasta que el motor confirme 26/26 en los golden tests — ver sección 3. |

## 3. Infraestructura

### Base de datos

SQL Server. `CabinaFletesDbContext` (EF Core 10) es la única fuente de
escritura; las migraciones ya versionadas viven en
`src/CabinaFletes.Infrastructure/Persistence/Migrations`.

**Set de datos para el equipo de datos** (S2-HU-10, `#35`): el esquema
`analitica` tiene vistas de solo lectura (`WITH (NOLOCK)`, sin interferir
con la operación) sobre cotizaciones, líneas, resultados, tarifas ganadas
archivadas y auditoría — más `analitica.inconsistencias`, que documenta
registros con datos irregulares en vez de descartarlos. Diccionario completo
en [`docs/analitica/DICCIONARIO_DATOS.md`](docs/analitica/DICCIONARIO_DATOS.md).

### Data Warehouses corporativos (solo lectura, todos opcionales)

| Cadena | Fuente | Usada por | Si no está configurada |
|---|---|---|---|
| `DwSice` | `DWSice.dbo.tblRutasSicetac` (SQL Server 2008 R2) | Biblioteca (matriz SICE-TAC en vivo) | El endpoint responde `503` accionable (`BibliotecaSicetacNoConfiguradoAdapter`) |
| `DbSolucionesTi` | `dbsolucionesTI.dbo.tblSAPTarifasLog` | Biblioteca (tarifario SAP TM / propuesta de tarifa) | El adapter correspondiente queda sin registrar el DbContext (se degrada) |
| `DwCubos` | `DWCubosOperacionesEBS.dbo.DimBPSolicitante` | Solicitudes/Captura (catálogo de clientes reales) | `ClientesDwAdapter.Configurado = false`; la búsqueda cae a la tabla local |

El DW `DwSice`/`DbSolucionesTi` corre en SQL Server 2008 R2 y solo negocia
TLS 1.0 con cifradores heredados; el `Dockerfile` de la Api incluye una
configuración OpenSSL propia (`/etc/ssl/openssl-legacy-tls.cnf`) para que el
contenedor pueda completar ese handshake sin afectar al resto del sistema.

### Servicios externos

- **SMTP corporativo** (opcional): notifica al comercial al entregar una
  cotización. Sin `Smtp:Host`/`Smtp:Remitente` configurados, la entrega
  sigue funcionando pero no envía el correo.
- **Conexión SAP nueva: plaza en vivo** (S2-HU-09, `#31`/`#33`): todavía NO
  EXISTE ("el equipo de conexiones" no la construyó). `ISapTarifaFleteEnVivoPort`
  es su contrato ya definido; `SapTarifaFleteEnVivo:Modo=Deshabilitado`
  (default) hace que el costeo se comporte exactamente igual que antes de
  esta HU — la plaza real sigue saliendo de `DbSolucionesTi`, sin tocar.
  `Modo=Mock` activa un dataset provisional (`SapTarifaFleteEnVivoMockAdapter`,
  pendiente de validación formal por negocio, S2-HU-09.2) para Desarrollo/QA
  sin acceso al DW corporativo — la regla del mayor y el piso SICE-TAC operan
  idéntico. El día que exista la conexión real, se agrega un adaptador nuevo
  y se cambia `Modo=Real`: cero cambios en `ReglaDeCosteo` ni en pantallas.
- **Identidad del portal de la Administración** (S2-HU-03, solo lectura):
  Nexo no tiene login propio (`docs/contrato-integracion`, D1). Cada
  petición valida el encabezado `X-Portal-Usuario-Id` contra esta tabla, sin
  caché de sesión. Sin `ConnectionStrings:IdentidadPortal`, la Api resuelve
  contra la tabla `Usuarios` local (útil en Desarrollo); en
  `ASPNETCORE_ENVIRONMENT=Production` el arranque EXIGE la cadena real
  (falla rápido, fail-closed).
- **Cubicador** (microservicio Python, carpeta `cubicador/` de este mismo
  repo, S2-HU-08): motor de cubicación 3D / plan de cargue. NEXO le habla
  SOLO por colas (MassTransit sobre el mismo RabbitMQ de abajo) — sin URL
  HTTP ni cliente propio, ver `docs/mensajeria/CUBICACION.md`. La función
  queda cerrada por una compuerta de paridad
  (`Cubicacion:ParidadVerificada`/`ParidadEvidencia`/`ParidadFecha`) hasta
  que el motor confirme 26/26 en sus golden tests — ver `PENDIENTES.md`,
  sección "Plan de cargue".

### Variables de entorno

Ver [`.env.example`](.env.example) para el listado completo y comentado.
Resumen de las obligatorias para levantar la Api:

- `ConnectionStrings__CabinaFletes` (o `MSSQL_SA_PASSWORD` si se usa Docker Compose).
- Todo lo demás (DWs, SMTP) es opcional: el sistema se degrada, nunca se
  cae, si falta. Dos excepciones:
  - `ConnectionStrings:IdentidadPortal`: opcional en Desarrollo/QA (cae al
    adapter local), pero OBLIGATORIA en Producción — sin ella el arranque
    falla (fail-closed, S2-HU-03.3).
  - `Cubicacion:ParidadVerificada`/`ParidadEvidencia`/`ParidadFecha`: no
    rompen el arranque si faltan, pero sin las tres juntas la función de
    plan de cargue responde 503 "en verificación" (S2-HU-08.2).

### Docker

- `src/CabinaFletes.Api/Dockerfile`: build multi-stage (.NET 10 SDK → ASP.NET
  Core runtime), usuario sin privilegios, healthcheck vía `/health`.
- `docker-compose.yml` (raíz): servicios `db` (SQL Server 2022) y `api`, más
  un perfil `seed` para sembrar datos de demostración en un solo comando.
  No incluye frontend (fuera de alcance de este repositorio). Cubicador
  (`cubicador/`) tiene su propio `docker-compose.yml`, despliegue
  independiente — solo comparte el broker `rabbit` de este stack.

### Integración con el portal de la Administración (S2-HU-02, issue #8)

Nexo se consume como un módulo más del portal, pero **no comparte dominio ni
despliegue con él**: mantiene su propia URL base y el módulo `cabina-fletes`
del portal la llama por HTTP, cross-origin. El contrato completo (identidad,
roles, trabajos pesados, avisos, errores) vive en
[`docs/contrato-integracion/`](docs/contrato-integracion/README.md) y ese
documento manda sobre cualquier otra descripción, incluida esta.

- **Prefijo/base path**: toda la API cuelga de `/api` (cada controller fija
  su propia ruta con `[Route("api/...")]`), igual que declara
  `docs/contrato-integracion/openapi.yaml` (`servers: - url: /api`). No hace
  falta un `PathBase` ni un proxy inverso: el portal llama a la URL propia de
  Nexo directamente, no a una ruta montada bajo el dominio del portal.
- **CORS**: `Cors:OrigenesPermitidos` (env `Cors__OrigenesPermitidos`, o
  `ORIGENES_PERMITIDOS_CORS` en Docker Compose) acepta una lista separada por
  coma. Debe incluir el origen exacto (esquema + dominio + puerto) de cada
  ambiente del portal que vaya a consumir la API — QA y Producción tienen
  dominios distintos — además del frontend standalone de desarrollo. Sin el
  origen exacto en la lista, el navegador bloquea la respuesta aunque la API
  responda `200`; `tests/CabinaFletes.Api.Tests/CorsTests.cs` verifica el
  mecanismo (un origen de la lista recibe `Access-Control-Allow-Origin`, uno
  fuera de ella no).
- **Healthcheck**: `/health` (sección 2, ya documentado) es público
  (`AllowAnonymous`) y responde JSON con el detalle por chequeo. Es el mismo
  endpoint que usa el healthcheck de Docker Compose; el orquestador de
  despliegue del portal puede sondear la misma ruta.
- **Identidad**: la resolución de la sesión contra la tabla de usuarios de la
  administración (`GET /api/sesion/actual`, encabezado
  `X-Portal-Usuario-Id`) es el alcance de las issues #9/#10/#11 del mismo
  milestone, no de esta — hoy el backend sigue autenticando con Azure AD/JWT
  (o el esquema de desarrollo sin Azure AD configurado). Ver
  `docs/contrato-integracion/README.md` sección 1 y `ADR-001` para el diseño
  ya acordado.

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
   conexos y se entrega (`POST /api/cotizaciones/{id}/entregar`). La entrega
   responde de inmediato; un consumidor aparte (S2-HU-06) notifica por
   correo a quien solicitó la consulta de flete — nunca al cliente — si la
   salida de correo está configurada.
5. Al marcar el resultado (`POST /api/cotizaciones/{id}/resultado`), si se
   gana, la tarifa aplicada realimenta la Biblioteca de Tarifas Ganadas
   (`TarifaHistorica`) para futuras sugerencias de costeo.

### Flujo de autenticación

Nexo no tiene login propio (S2-HU-03, `docs/contrato-integracion`, D1): la
identidad la aporta la sesión del portal de la Administración.
`IdentidadPortalAuthenticationHandler` valida en CADA petición el
encabezado `X-Portal-Usuario-Id` contra la tabla de usuarios/permisos del
portal (o contra `Usuarios` local si `ConnectionStrings:IdentidadPortal` no
está configurada — solo fuera de Producción), resuelve uno de los 6 roles
de Nexo vía `MapeoRolPortal` y deja los claims listos para el resto del
pipeline. `GET /api/sesion/actual` expone esa identidad resuelta al
frontend. Autorización por rol (`[Authorize(Roles = "...")]`) en cada
endpoint que lo requiere, más una `FallbackPolicy` que exige uno de los 6
roles en TODO endpoint (aunque no declare `Roles=`) — ver la tabla de
módulos en la sección 2 y el detalle de roles en cada controller.

## 5. Stack tecnológico

- **.NET 10** — ASP.NET Core Web API, C#.
- **Entity Framework Core 10** — proveedor SQL Server.
- **ClosedXML** — lectura/escritura de Excel (import de tarifas, servicios
  conexos, líneas de solicitud; exportación de cotizaciones).
- **CQRS propio** — sin MediatR ni otra librería de terceros.
- **SQL Server** — base de datos principal y (solo lectura) los cuatro Data
  Warehouses corporativos.
- **Docker / Docker Compose** — empaquetado y orquestación local.
- **Identidad delegada al portal de la Administración** — sin login propio ni JWT emitido por Nexo (S2-HU-03).

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

### Plan de Cargue — `/api/planes-cargue` (S2-HU-08)

| Método | Ruta | Rol | Propósito |
|---|---|---|---|
| POST | `/{id}/optimizar` | Comercial, CoordinadorFletes, Admin | Pide la optimización de un plan ya existente. `202` inmediato con `{planCargueId, estado, consultarEn}`; `503 CUBICACION_EN_VERIFICACION` si la compuerta de paridad está cerrada; `409` si el plan ya está procesando |
| GET | `/{id}` | cualquiera autenticado | Consulta de respaldo: estado y resultado del plan |

Avance por SignalR en `/hubs/plan-cargue` (`Suscribir`/`Desuscribir(planCargueId)`,
evento `"avance"`). Solo re-optimiza un plan que ya existe (con sus ítems)
en la base de Cubicador — el flujo "pedir un plan desde una línea de
solicitud" queda pendiente, ver `PENDIENTES.md`.

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

### Recorrido de punta a punta (`build/e2e/`)

Los tres proyectos de arriba corren con transporte en memoria y bases efímeras.
Eso los hace rápidos y bien aislados, y por eso mismo **ciegos a si las piezas
encajan de verdad**. El recorrido de punta a punta cubre ese hueco: habla con el
API real, con el Outbox entregando contra SQL Server, RabbitMQ con su plugin de
retardo, la saga persistida y SignalR empujando a un cliente de verdad.

```
docker compose up -d
docker compose --profile e2e run --rm e2e
```

No es teoría. Así se encontró que el arranque de la cadena de priorización
publicaba al Outbox **sin llamar a SaveChanges**: el log decía "cadena armada" y
no quedaba ni una fila. Ninguna prueba lo veía, porque ninguna publica a través
del Outbox. Y así se confirmó que los avisos del hub llegan desordenados y
repetidos de verdad —`[1, 2, 3, 4, 6, 5, 6, 7…]` en una corrida—, que es lo que
justifica la secuencia del contrato.

Solo usa la biblioteca estándar de Python, a propósito: una prueba que necesita
`pip install` falla por la red y no por el código. Sale con código distinto de
cero si algo falla, así que sirve tal cual en un pipeline.

### Cómo ejecutar

Todo en contenedor, sin depender del SDK instalado en la máquina:

```
# Compilar las 4 capas y los 3 proyectos de test
docker compose -f docker-compose.ci.yml build && docker compose -f docker-compose.ci.yml run --rm build

# La solución completa, sin filtros, con SQL Server efímero
docker compose -f docker-compose.ci.yml run --rm pruebas
```

El `&&` no es opcional: si la construcción de la imagen falla, `run` arranca con
la imagen anterior y compila código viejo — un verde falso, que es peor que un
rojo.

El servicio `pruebas` va **sin filtro y sobre el `.sln`** por un motivo concreto:
correr las suites con `--filter` dejó 42 pruebas de `Api.Tests` sin ejecutar
durante varias sesiones, y el verde de las otras tapaba el hueco. Sin filtro, si
alguien agrega un proyecto de pruebas, entra solo.

Fuera de contenedor:

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

Las cuatro capas se organizan **por responsabilidad de negocio**, no por tipo
técnico. La carpeta de primer nivel dice de qué trata el sistema, no con qué
está construido.

```
NEXO/
  CabinaFletes.Clean.sln
  .env.example                 # Variables de entorno (backend + Docker Compose)
  .dockerignore                # Qué NO entra al contexto de build
  docker-compose.yml           # db + api (+ perfil seed)
  docker-compose.ci.yml        # Arnés: compila la solución completa en contenedor
  build/Dockerfile.ci          # Imagen del arnés (las 4 capas + los 3 proyectos de test)
  docs/contrato-integracion/   # Contrato con el portal, OpenAPI, Gherkin y ADR
  MIGRACION.md · PENDIENTES.md · README.md

  src/
    CabinaFletes.Domain/              # Por AGREGADO del event storming
      Solicitudes/Entidades/          #   Solicitud, LineaSolicitud
      Cotizaciones/Entidades/         #   Cotizacion, CotizacionLinea, CotizacionConexo
      PlanDeCargue/Entidades/         #   PlanCargue, ItemPlanCargue, ColocacionCargue
      BibliotecaTarifas/Entidades/    #   TarifaSicetac, TarifaHistorica
      Costeo/Entidades/               #   ReglaSoloSicetac, AjusteVigenciaPlaza
      Priorizacion/{Entidades,Reglas} #   ParametrosScore, ScorePrioridadCalculator
      Catalogos/Entidades/            #   Ruta, ConfiguracionVehiculo, ServicioConexo, Cliente
      Usuarios/ · Auditoria/          #   Usuario · AuditLog
      Reglas/                         #   MaquinaEstados (cruza agregados)
      Enums/ · Common/                #   Enums compartidos · IAuditable, Texto, ImporteParser

    CabinaFletes.Application/
      CasosDeUso/<Contexto>/          # Los casos de uso, directos bajo su contexto
        DTOs/                         #   Entrada y salida de esos casos de uso
        Interfaces/                   #   Los puertos que el contexto define
        Mappers/                      #   Entidad -> DTO de salida
        Validadores/                  #   Forma de la entrada
        Excepciones/                  #   Solo cuando el contexto necesita una propia
      Common/                         # Abstractions, Ports y Dtos genuinamente transversales

    CabinaFletes.Infrastructure/
      <Contexto>/Adaptadores/         # SMTP, Excel, SAP, DW: lo que no es solo EF Core
      <Contexto>/Repositorios/        # Implementaciones EF Core de los puertos
      Common/                         # EfAuditoriaRepository, EfUnitOfWork
      Persistence/ · External/        # DbContext y migraciones · DbContexts de los DW
      DependencyInjection.cs

    CabinaFletes.Api/
      <Contexto>/                     # Controlador y DTOs de entrada de ese contexto
      Common/{Auth,Errors,Middleware} # Transversal
      Program.cs                      # Composition root
      Dockerfile

  tests/
    CabinaFletes.Domain.Tests/        # Unitarios puros
    CabinaFletes.Application.Tests/   # Unitarios con Moq
    CabinaFletes.Api.Tests/           # Integración contra SQL Server
```

### Reglas de frontera

El encarpetado sin estas reglas es cosmético: a los dos meses el desorden vuelve
por otro lado. Estas cuatro son las que hay que hacer respetar en revisión.

**`Validadores` valida la forma de la entrada, no el negocio.** Campos
obligatorios, formatos, rangos, enums que parseen. Las reglas inviolables del
costeo — nunca por debajo del piso SICE-TAC, la regla del mayor, la tarifa
vencida no se propone — viven en Domain. Si acaban en un validador, Domain se
vuelve anémico y esas reglas dejan de poder probarse solas.

Corolario: si una comprobación necesita consultar un repositorio, **no es
validación de entrada**. Es una precondición del caso de uso y se queda en el
handler. Sacarla obligaría a inyectarle repositorios al validador y tendríamos
la misma lógica en dos sitios.

**`Interfaces` son fronteras, no cualquier interfaz.** Aquí solo va lo que
Application define y que Infrastructure o Api implementan. Eso es la inversión
de dependencias, que es el corazón de Clean. Una interfaz auxiliar interna no
pertenece a esta carpeta.

**`Mappers` mapea de entidad de dominio al DTO de salida del caso de uso.** Nada
de formas HTTP, que son de Api, ni de formas de persistencia, que son de
Infrastructure.

**Las excepciones se reparten según de quién es la falla.** Violación de
invariante de negocio en Domain; falla de caso de uso en Application. Las de
contexto **heredan de las cuatro base** de `Common/Exceptions` — por eso esas
cuatro no son `sealed`. El middleware de Api traduce por tipo, así que una
excepción que herede de `AppException` a secas caería en el catch genérico y
respondería 500 en vez de su código.

No se crea una excepción por cada código de error: el código de texto ya lo
identifica y el frontend ya ramifica con él. Se crea cuando el contexto la
necesita de verdad, como `CotizacionSinFleteException`, que es una regla del
negocio presente en dos casos de uso.

**Y la que las gobierna a todas:** Domain no importa ninguna capa. Application
no importa Infrastructure ni Api. Si un `using` cruza hacia afuera, es un
defecto, no una excepción justificada.
