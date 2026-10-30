# Nexo — Backend (Clean Architecture)

Sistema interno de pricing de fletes de Botero Soto · Soluciones Logísticas
(3PL): Bandeja Priorizada de solicitudes, Mesa de Costeo, Biblioteca de
tarifas (SICE-TAC en vivo, histórico propio y caché local) y el ciclo
completo de una cotización hasta su entrega al comercial.

Este repo cubre el backend .NET (`src/`) y un microservicio Python de
cubicación 3D (`cubicador/`), que se comunican por colas (RabbitMQ). No
incluye frontend.

## Arquitectura

Clean Architecture de 4 capas, dependencias siempre hacia el centro:

```
Api            Controllers, DTOs, middlewares, autenticación, Program
  -> Application  Commands/Queries (CQRS propio, sin MediatR), Ports, DTOs
       -> Domain     Entidades, enums, reglas de negocio puras
  <- Infrastructure  EF Core, Repositories, Adapters (SMTP, Excel, DW externos)
```

Application define los `Ports` que necesita cada caso de uso; Infrastructure
los implementa. Api solo conoce Application; todo se registra en el
composition root (`Program.cs` + `Infrastructure/DependencyInjection.cs`).

### Módulos de negocio

| Módulo | Ruta base | Resumen |
|---|---|---|
| Cotizaciones | `/api/cotizaciones` | Ciclo completo: crear, agregar líneas/conexos, aplicar tarifa, entregar, marcar resultado, exportar a Excel. |
| Solicitudes / Captura | `/api/solicitudes`, `/api/captura-solicitudes` | Alta de solicitudes, Bandeja Priorizada por score, captura de líneas, auto-costeo en segundo plano. |
| Biblioteca | `/api/biblioteca-sicetac`, `/api/biblioteca-sap-plaza` | Matriz SICE-TAC en vivo, tarifario SAP TM y motor de propuesta de tarifa. |
| Reglas Solo-SICETAC | `/api/reglas-solo-sicetac` | Rutas/vehículos que cotizan exclusivamente con el piso SICE-TAC. |
| Ajustes de Vigencia de Plaza | `/api/biblioteca-sap-plaza/{id}/vigencia` | Ajustar/quitar vigencia de tarifas de plaza SAP TM. |
| Servicios Conexos | `/api/servicios-conexos` | Catálogo de servicios logísticos adicionales. |
| Tarifas Ganadas | `/api/tarifas-historicas` | Histórico local de tarifas ya cobradas; sugerencias para el costeo. |
| Tarifas SICE-TAC Admin | `/api/tarifas-sicetac` | Caché local del piso SICE-TAC, cargada por import Excel. |
| Métricas | `/api/metricas` | KPIs del Tablero del Líder. |
| Plan de Cargue | `/api/planes-cargue` | Orquestación async de Cubicador: pide la optimización, avanza por SignalR. |
| Sesión | `/api/sesion` | Identidad resuelta del portal: id, nombre, rol, capacidades. |
| Priorización | `/api/priorizacion/parametros` | Recalibrar pesos del score (solo Admin). |
| Trabajos | `/api/trabajos` | Consulta de respaldo del estado de un trabajo pesado. |

## Stack tecnológico

- **.NET 10** — ASP.NET Core Web API
- **Entity Framework Core 10** — SQL Server
- **MassTransit + RabbitMQ** — mensajería asíncrona, sagas, Outbox
- **ClosedXML** — import/export de Excel
- **Python 3.12 / FastAPI** — Cubicador (motor de cubicación 3D)
- **PostgreSQL** — base de datos de Cubicador
- **Docker / Docker Compose** — empaquetado y orquestación
- Identidad delegada al portal de la Administración (sin login propio)

## Estructura del proyecto

```
src/            Nexo.Api, Nexo.Application, Nexo.Domain, Nexo.Infrastructure, Nexo.Mensajes
tests/          Nexo.Domain.Tests, Nexo.Application.Tests, Nexo.Api.Tests
cubicador/      microservicio Python (api, worker, domain, infrastructure)
docs/           documentación técnica (mensajería, roles, decisiones, DB)
```

## Ejecución local

### Sin Docker

```bash
dotnet restore Nexo.Clean.sln && dotnet build Nexo.Clean.sln
# Configura ConnectionStrings__Nexo (ver .env.example)
dotnet run --project src/Nexo.Api seed   # migra y siembra datos de demo
dotnet run --project src/Nexo.Api        # http://localhost:5080, /swagger en Development
```

### Con Docker Compose

```bash
cp .env.example .env   # completa como mínimo ConnectionStrings__Nexo
docker compose up -d --build
# http://localhost:${API_PORT:-8080} (health en /health)
```

Cubicador tiene su propio stack en `cubicador/docker-compose.yml` (requiere
que el `rabbit` del compose raíz ya esté corriendo, comparten red).

Para desplegar en Dokploy, ver [`DOKPLOY.md`](DOKPLOY.md).

## API

Todos los endpoints requieren `X-Portal-clsUsuario-Id`; algunos exigen además
uno de los 6 roles de Nexo. Errores con forma consistente:

```json
{ "code": "CODIGO_DE_ERROR", "message": "Descripción legible.", "timestamp": "...", "details": null }
```

Referencia completa de endpoints en [`CONTRATO.md`](CONTRATO.md). Permisos
por rol en [`docs/documentacion/ROLES_Y_PERMISOS.md`](docs/documentacion/ROLES_Y_PERMISOS.md).

## Testing

- `Nexo.Domain.Tests` — unitarios puros (score de priorización, parsing).
- `Nexo.Application.Tests` — unitarios con Ports mockeados (Command/Query Handlers).
- `Nexo.Api.Tests` — integración end-to-end con `WebApplicationFactory` contra SQL Server real.
- `build/e2e/` — recorrido de punta a punta contra Docker Compose real (API, Outbox, RabbitMQ, saga, SignalR).
- `cubicador/tests/` — unitarios de dominio, paridad contra el motor C# original, integración API/worker.

```bash
dotnet test Nexo.Clean.sln
docker compose --profile e2e up -d && docker compose --profile e2e run --rm e2e
cd cubicador && pytest
```

## Documentación adicional

- [`docs/Database/`](docs/Database/) — esquema, datos iniciales, script para crear la base y diccionario de datos.
- [`docs/mensajeria/`](docs/mensajeria/) — topología RabbitMQ/MassTransit.
- [`docs/decisiones/`](docs/decisiones/) — decisiones de diseño.
- [`cubicador/README.md`](cubicador/README.md) — detalle del microservicio de cubicación.
