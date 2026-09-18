# Contrato de la API — NEXO (Cabina de Fletes)

Referencia completa, endpoint por endpoint, para que el frontend sepa qué llamar,
qué mandar y qué esperar. Es un **complemento** de
[`docs/contrato-integracion/README.md`](docs/contrato-integracion/README.md), que
explica el *porqué* de la identidad, los trabajos pesados y los avisos en vivo —
acá solo van las reglas cortas + la superficie completa de la API. Ante cualquier
contradicción entre los dos, gana `docs/contrato-integracion/README.md`.

Base URL: la que publique cada ambiente (`ASPNETCORE_URLS` / `API_PORT`, ver
`.env.example`). Swagger vive en `/swagger`, solo en Development.

---

## 1. Convenciones generales

### Autenticación

No hay login propio. Cada petición manda el id de la persona ya autenticada en
el portal:

```http
X-Portal-Usuario-Id: 10123
```

Sin este header, o si el id no resuelve a una identidad válida → **401**. Con
identidad válida pero sin uno de los 6 roles de Nexo sobre Cabina de Fletes →
**403**. Ningún endpoint es público salvo `GET /health`. Detalle completo:
[`docs/contrato-integracion/README.md`, sección 1](docs/contrato-integracion/README.md#1-identidad).

### Los 6 roles

`Comercial · CoordinadorFletes · AdminTarifas · ServiciosLogisticos · LiderProceso · Admin`

`Admin` pasa todo. Qué puede hacer cada uno: [`docs/ROLES_Y_PERMISOS.md`](docs/ROLES_Y_PERMISOS.md).

### Formato de error (siempre el mismo)

```ts
interface ApiError {
  code: string          // estable, para ramificar (ej. "SOLICITUD_INVALIDA")
  message: string        // en español, para mostrar — no decidir con esto
  timestamp: string       // ISO 8601
  details?: unknown        // ej. { "campo": ["error1", "error2"] } en 400 de validación
}
```

| HTTP | Código típico | Cuándo |
|---|---|---|
| `400` | `VALIDATION_ERROR` (bind/DataAnnotations) u otro específico | Input inválido o regla de negocio |
| `401` | `SESION_INVALIDA` | Sin identidad válida del portal |
| `403` | `SIN_PERMISO_CABINA_FLETES` | Identidad válida, rol insuficiente |
| `404` | específico del recurso | No existe |
| `409` | específico, o `CONFLICTO_CONCURRENCIA` | Conflicto de estado o edición simultánea (RowVersion) |
| `503` | específico | Fuente externa no configurada o no responde |
| `500` | `INTERNAL_ERROR` | Error no controlado |

`400` de validación automática (DataAnnotations) trae `details` como
`{ "campoDelBody": ["mensaje"] }` por cada campo que falló.

### Enums: se mandan y devuelven como texto

Todos los enums de C# viajan como **string exacto del nombre del valor**
(`PascalCase`, ej. `"Ganada"`, `"EnCosteo"`), nunca como número. Valores
completos en la [sección 6](#6-enums).

**Ojo con la asimetría en tres endpoints de escritura**: el `body` acepta el
valor en minúscula (`"ganada"`, `"spot"`, `"sicetac"` — validado con regex
case-insensitive, no es el enum real), pero la respuesta siempre devuelve el
enum real en PascalCase (`"Ganada"`, `"Spot"`, `"Sicetac"`). Son:
`MarcarResultado.resultado`, `Crear/CapturarSolicitud.tipo`,
`AgregarLinea.fuente`.

### Paginación

Donde existe es `page` (1-based) + `pageSize`/`top` (según el endpoint), y la
respuesta trae `{ items, total }` (o el equivalente propio del endpoint, ver
cada sección). `page`/`pageSize` son opcionales (default `page=1`,
`pageSize=50` salvo donde se indique otro) — omitirlos no cambia el
comportamiento de un cliente existente.

Historial:
- Issue #156: Servicios Conexos (`page`+`pageSize`, patrón original), Biblioteca
  SICE-TAC y Biblioteca plaza SAP (`page`+`top`) — medido en vivo, ~2,15 M y
  ~22,8 K filas respectivamente, truncaban en la primera página sin forma de
  pedir la siguiente aunque `total` ya viajaba en la respuesta.
- Issue #166 (S4-HU-02): se agrega `page`/`pageSize` al resto de los listados
  — `solicitudes` (Bandeja Priorizada), `tarifas-historicas` (rama "buscar"),
  `vehiculos`, `planes-cargue` — reemplazando los topes fijos que tenían antes
  (`Take(50)`, `Take(100)`, sin tope en `solicitudes`). Todos responden
  `{ items, total }`. `biblioteca-sap-historico` y `reglas-solo-sicetac`
  siguen SIN paginar (bajo volumen medido, ver
  `docs/decisiones/paginacion-endpoints-listado.md`).
- Issue #167: el timeout real de `biblioteca-sicetac` (causa: falta de índice
  en la tabla del Data Warehouse externo, fuera del alcance de este repo) se
  mitigó con un `CommandTimeout` más generoso y un caché más largo — ver
  `docs/decisiones/timeout-biblioteca-sicetac.md`.

### Rate limiting (issue #168)

Los endpoints de listado tienen rate limiting básico (`Microsoft.AspNetCore.
RateLimiting`, nativo del framework), particionado por rol del usuario
autenticado (o IP si no hay rol resuelto). Al exceder el límite responden
`429` con el mismo shape de error que el resto de la API
(`{ code: "DEMASIADAS_SOLICITUDES", message, timestamp }`) y un header
`Retry-After: 30`.

- **Política estricta** (`biblioteca-sicetac`, `biblioteca-sap-plaza` — bases
  externas compartidas con otros consumidores corporativos): 20 solicitudes/min
  por rol (60 para `Admin`/`AdminTarifas`), ventana deslizante.
- **Política estándar** (resto de los listados — `solicitudes`,
  `planes-cargue`, `vehiculos`, `tarifas-historicas`, `reglas-solo-sicetac`,
  `biblioteca-sap-historico`): 120 solicitudes/min, ventana fija.

### Carga de archivos Excel

`multipart/form-data`, campo `archivo` (o `formato` en Entregar cotización).
Límite 10 MB. Solo `.xlsx`/`.xlsm`. Errores: `ARCHIVO_REQUERIDO` (falta o vacío),
`FORMATO_NO_SOPORTADO` (otra extensión).

### Trabajos pesados: 202 + consulta de respaldo

Ninguna operación pesada devuelve su resultado en la misma petición. Responde
**202** con un identificador; el resultado llega por SignalR o se consulta con
`GET /api/trabajos/{id}` (o el equivalente del módulo). Nunca asumas que el
aviso llegó — es una mejora, no una garantía. Detalle:
[`docs/contrato-integracion/README.md`, sección 4](docs/contrato-integracion/README.md#4-trabajos-pesados).

### Avisos en vivo (SignalR)

| Hub | Suscribir | Evento | Trae |
|---|---|---|---|
| `/hubs/trabajos` | `Suscribir(trabajoId)` | `"avance"` | `AvisoDeTrabajo` (con `secuencia`, descarta si `<=` la última pintada) |
| `/hubs/bandeja` | `Suscribir()` (sin parámetro, un solo grupo) | `"bandeja"` | `{ motivo, en }` — **no trae datos**, solo dice "vuelve a consultar `GET /api/solicitudes` con tus filtros" |
| `/hubs/plan-cargue` | `Suscribir(planCargueId)` | `"avance"` | `AvisoPlanCargueDto` |

Los tres exigen la misma identidad que el resto del API (`[Authorize]` en la
conexión). Ninguno reenvía lo perdido mientras el cliente estuvo desconectado —
al reconectar, se reconsulta por HTTP. Detalle:
[`docs/contrato-integracion/README.md`, secciones 5 y 5.2](docs/contrato-integracion/README.md#5-avisos-en-vivo).

---

## 2. Tipos comunes

Referenciados por nombre en los endpoints de abajo, para no repetirlos.

```ts
type UUID = string
type FechaISO = string          // "2026-08-24T19:40:00Z"
type Fecha = string             // date-only, "2026-08-24"
type Decimal = number

interface AvanceTrabajoDto {
  total: number | null          // null hasta que el pliego se reparte
  resueltas: number
  pendientes: number
  pendientesTecnicas: number
  requierenTarifaManual: number
}
interface PendienteDeCosteoDto {
  lineaId: UUID
  tipo: "Tecnica" | "Manual"
  motivo: string
  pendienteDesde: FechaISO
}
interface EstadoTrabajoDto {
  trabajoId: UUID
  estado: "Procesando" | "Completado" | "Parcial" | "Fallido"
  avance: AvanceTrabajoDto
  solicitadoEn: FechaISO
  terminadoEn: FechaISO | null
  pendientes: PendienteDeCosteoDto[]
  secuencia: number
}
interface AvisoDeTrabajo {                       // lo que llega por /hubs/trabajos
  trabajoId: UUID; estado: string; avance: AvanceTrabajoDto
  terminadoEn: FechaISO | null; secuencia: number
}

interface LineaSolicitudDto {
  id: UUID; rutaId: UUID | null; configVehiculoId: UUID | null
  tipologia: TipologiaCarga | null; peso: Decimal | null; valorMercancia: Decimal | null
  estadoLinea: "PorCostear" | "AutoResuelta" | "Revisada"
}
interface SolicitudDto {
  id: UUID; clienteId: UUID; clienteNombre: string | null
  tipo: "Spot" | "Licitacion"; modalidad: ModalidadSolicitud | null
  deadline: FechaISO | null; probabilidadCierre: Decimal | null; valorEstimado: Decimal | null
  scorePrioridad: Decimal; estado: "Pendiente" | "EnCosteo" | "Entregada" | "Descartada"
  createdAt: FechaISO; lineas: LineaSolicitudDto[]
}
interface BandejaItemDto {                       // fila de GET /api/solicitudes
  id: UUID; cliente: string; tipo: string; estado: string
  deadline: FechaISO | null; valorEstimado: Decimal | null; scorePrioridad: Decimal
  lineas: number; fleteTotal: Decimal | null; lineasSinFlete: number
  fechaCaptura: FechaISO
  atencion: "pendiente" | "automatico" | "administrador" | string
  vigencia: "vigente" | "vencida"
}
interface DesgloseScoreDto { total: number; urgencia: number; valor: number; probabilidad: number; tipo: number }
interface PosicionCargaDto {                     // pieza/bulto, dimensiones en CENTÍMETROS
  descripcion: string | null; cantidad: number
  largoCm: Decimal; anchoCm: Decimal; altoCm: Decimal; pesoKg: Decimal | null
}
interface PisoSicetacDto {
  id: UUID; valorPiso: Decimal; valorHora: Decimal | null; horasPactadas: Decimal | null
  periodo: number; vigencia: string; configuracion: string; tipoCarga: string
  unidadTransporte: string; condicionCarga: string
}
interface HistoricaSugeridaDto { id: UUID; valor: Decimal; fecha: Fecha; tipologia: TipologiaCarga | null; vigencia: string }
interface LineaCosteoDto {
  lineaId: UUID; rutaId: UUID | null; ruta: string | null; tipologia: TipologiaCarga | null
  peso: Decimal | null; tipoVehiculo: string | null; carroceria: string | null
  posiciones: PosicionCargaDto[]; pisoSicetac: PisoSicetacDto | null; historicas: HistoricaSugeridaDto[]
}
interface TiemposLogisticosDto { horasCargue: Decimal; horasDescargue: Decimal }
interface SolicitudCosteoDto { solicitud: SolicitudDto; tiempos: TiemposLogisticosDto; lineas: LineaCosteoDto[] }
interface CapturaResultDto {
  id: UUID; lineas: number; rutasCreadas: number; vehiculosCreados: number
  scorePrioridad: Decimal; fletesCalculados: number; lineasPorCostear: number; costeoEnProceso: boolean
}
interface HojaExcelDto { nombre: string; filaEncabezado: number; encabezados: string[]; filas: string[][]; totalFilas: number }
interface AnalisisExcelDto { archivo: string; hojas: HojaExcelDto[] }
interface ClienteBusquedaDto { clienteId: UUID | null; bp: string | null; nit: string; nombre: string }
interface CandidatoTarifaDto {
  fuente: string; valor: Decimal; detalle: string; criterio: string; coincidencias: number
  advertencia: string | null // issue #169: no-nulo cuando NO es un match exacto (fallback "mayor valor vigente" por peso fuera de rango o vehículo no resuelto en el catálogo SAP) — mostrar al usuario antes de confirmar
}
interface AprovisionamientoDto { candidato: CandidatoTarifaDto | null }

interface CotizacionLineaDto {
  id: UUID; lineaSolicitudId: UUID | null; valorFlete: Decimal; fuente: FuenteFlete
  valorAprovisionamiento: Decimal | null; valorRetornoContenedor: Decimal | null
}
interface CotizacionConexoDto { id: UUID; servicioConexoId: UUID | null; nombre: string | null; categoria: string; cantidad: Decimal; valorAplicado: Decimal }
interface CotizacionDto {
  id: UUID; solicitudId: UUID; estado: "Borrador" | "Entregada"
  resultado: "SinDefinir" | "Ganada" | "Perdida"; total: Decimal; entregadaAt: FechaISO | null
  lineas: CotizacionLineaDto[]; conexos: CotizacionConexoDto[]
}
interface AplicarPropuestasResultDto { cotizacion: CotizacionDto; agregadas: number }
interface EntregarCotizacionResultDto { cotizacion: CotizacionDto; estadoNotificacion: string; mensaje: string }
interface PropuestaLineaDto {
  lineaId: UUID; ruta: string | null; vehiculo: string | null; peso: Decimal | null
  sicetac: CandidatoTarifaDto | null; plaza: CandidatoTarifaDto | null; historicoSap: CandidatoTarifaDto | null
  valorPropuesto: Decimal | null; fuentePropuesta: string | null; justificacion: string
  retornoContenedor: CandidatoTarifaDto | null
}
interface PropuestaSolicitudDto { solicitudId: UUID; lineas: PropuestaLineaDto[]; advertencias: string[] }

interface RutaSicetacItemDto {
  id: number; periodo: number; nombreRuta: string; origen: string | null; destino: string | null
  configuracion: string; nombreTipoCarga: string; nombreUnidadTransporte: string; condicionCarga: string | null
  valor: Decimal; valorHora: Decimal | null; valorSiceConHora: Decimal | null
  horasRecorrido: Decimal | null; horasPactadas: number | null; viaEstandar: string | null
  fechaCreacion: FechaISO; fechaModificacion: FechaISO
}
interface MatrizSicetacResultDto { periodo: number; total: number; items: RutaSicetacItemDto[] }
interface SicetacFiltrosDto { periodos: number[]; configuraciones: string[]; tiposCarga: string[]; unidadesTransporte: string[] }

interface SapTarifaHistoricoItemDto {                    // histórico y plaza SAP comparten esta forma
  id: number; lineaNegocio: number; lineaNegocioNombre: string | null
  origen: string; origenNombre: string | null; destino: string; destinoNombre: string | null
  valor: Decimal | null; valorTexto: string; vigencia: FechaISO
  claseContenedor: number | null; tamanoContenedor: string | null
  rangoPesoInf: number | null; rangoPesoSup: number | null
  tipoVehiculo: string | null; tipoVehiculoNombre: string | null
  tipoCarroseria: string | null; tipoCarroseriaNombre: string | null
  pesoLimite: string | null; claseViaje: number; claseViajeNombre: string | null
  devolucion: boolean | null; consolidado: boolean | null; exclusivo: boolean | null
  detallesTarifa: string | null; fechaCreacion: FechaISO; fechaModificacion: FechaISO
  vigenciaAjustada: FechaISO | null                       // override local; null = rige la vigencia SAP
}
interface SapHistoricoResultDto { total: number; items: SapTarifaHistoricoItemDto[] }
interface OpcionFiltroDto { valor: string; etiqueta: string }
interface SapHistoricoFiltrosDto { lineasNegocio: OpcionFiltroDto[]; tiposVehiculo: OpcionFiltroDto[]; tiposCarroseria: OpcionFiltroDto[]; clasesViaje: OpcionFiltroDto[] }

interface AjusteVigenciaResultDto { aplicados: number; solicitados: number }

interface ReglaSoloSicetacDto { id: UUID; origen: string; destino: string; tipoVehiculo: string; createdAt: FechaISO }
interface ImportarReglasResultDto { filas: number; creadas: number; duplicadas: number; invalidas: number }

interface ServicioConexoDto { id: UUID; nombre: string; categoria: string; reglaCobro: string | null; valorBase: Decimal | null; vigenteHasta: Fecha | null; activo: boolean }
interface PaginaServiciosDto { items: ServicioConexoDto[]; total: number }
interface ImportarServiciosResultDto { filas: number; creadas: number; duplicadas: number; invalidas: number }

interface TarifaSicetacDto {
  id: UUID; periodo: number; origenDane: string; destinoDane: string; nombreRuta: string | null
  configuracion: string; tipoCarga: string; unidadTransporte: string; condicionCarga: "Cargado" | "Vacio"
  valorPiso: Decimal; valorHora: Decimal | null; horasPactadas: Decimal | null
}
interface ImportResumenDto { procesadas: number; insertadas: number; actualizadas: number; rechazadas: number; errores: string[]; sinRutaLocal: number }
interface ImportacionDto { id: UUID; usuario: string | null; detalle: string | null; fecha: FechaISO }

interface BibliotecaItemDto {
  id: UUID; ruta: string | null; vehiculo: string | null; tipologia: TipologiaCarga | null
  valor: Decimal; fecha: Fecha; vigencia: "vigente" | "por_vencer" | "vencida"; deCotizacionGanada: boolean
}

interface CumplimientoDto {
  totalSolicitudes: number; pendientes: number; enCosteo: number; entregadas: number; descartadas: number
  licitacionesTotal: number; licitacionesPerdidas: number; porcentajeLicitacionesPerdidas: number
  entregadasConDeadline: number; entregadasATiempo: number; cumplimientoSla: number
}

interface ParametrosScoreDto {
  pesoUrgencia: number; pesoValor: number; pesoProbabilidad: number; pesoTipo: number   // deben sumar 1.0
  horizonteHoras: number; valorReferencia: Decimal
  puntajeLicitacion: number; puntajeSpot: number
  valorPorDefectoSinValor: number; probabilidadPorDefecto: number
}

interface SolicitudDeCubicacionAceptadaDto { planCargueId: UUID; estado: string; consultarEn: string }
interface PlanCargueDto {
  id: UUID; nombre: string; estado: string; estrategia: string; algoritmo: string
  puntaje: Decimal; pesoTotalKg: Decimal; volumenUtilizadoM3: Decimal
  utilizacionVolumen: Decimal; utilizacionPiso: Decimal
  totalItems: number; totalColocaciones: number; updatedAt: FechaISO
}
interface AvisoPlanCargueDto { planCargueId: UUID; estado: string; puntaje: Decimal; motivo: string | null }

// [S3-HU-04.3, issue #78] respuesta común de los comandos async del puente Mesa de Costeo → 3D
interface ComandoAceptadoDto { correlationId: UUID; estado: string }
interface PlanFlotaItemDto { id: UUID; numeroVehiculo: number; totalVehiculos: number; unidadesCargadas: number; unidadesTotales: number }
// ColocacionCargueDto: mismo shape que ya devuelve el `resultado` de `.../optimizar` dentro de `colocaciones[]`
// (geometría + estado de cada bulto) — ver `CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs.PlanCargueDtos.cs`
// para el shape completo; espeja 1:1 el `ColocacionCargueDto` documentado en `cubicador/CONTRATO.md` §3.2.

interface SesionActualDto { id: number; nombre: string; rol: RolUsuario; capacidades: string[] }
```

---

## 3. Endpoints por módulo

Formato: `MÉTODO ruta` — roles (cuando restringe más que "cualquier rol
autenticado") — qué hace si no es obvio. `Body`/`Query` cuando aplica.
`→ NNN Tipo` es la respuesta de éxito.

### Sesión — `/api/sesion`

Detalle completo del flujo en
[`docs/contrato-integracion/README.md`, sección 1](docs/contrato-integracion/README.md#1-identidad).

- `GET /api/sesion/actual` — cualquier rol → `200 SesionActualDto`

### Bandeja y Solicitudes — `/api/solicitudes` (`[Authorize]`)

- `GET /api/solicitudes` — Bandeja Priorizada.
  `Query: estado?, atencion? ("pendientes"|"automatico"|"administrador"|"atendidas"|"todas"), desde?, hasta?, vigencia? ("vigente"|"vencida"), page? (>=1, def. 1), pageSize? (1-200, def. 50)`
  → `200 { items: BandejaItemDto[], total }` (issue #166 — antes devolvía el array completo sin tope; el orden por score se recalcula en memoria y la paginación se aplica después, no reduce el trabajo del backend pero sí el tamaño de la respuesta)
- `GET /api/solicitudes/{id}` — costeo pre-armado (SICE-TAC + históricos) por línea → `200 SolicitudCosteoDto`
- `POST /api/solicitudes` — `CoordinadorFletes, Admin`. Alta spot (o cabecera de licitación).
  `Body: { clienteId, tipo: "spot"|"licitacion", modalidad?, deadline?, valorEstimado?, probabilidadCierre?, linea?: { rutaId?, configVehiculoId?, tipologia?, peso?, valorMercancia? } }`
  → `201 { id: UUID }`
- `POST /api/solicitudes/{id}/lineas:bulk` — `CoordinadorFletes, Admin`. Carga masiva de un pliego; el costeo corre en segundo plano (patrón de trabajos pesados).
  `Body: { lineas: [{ rutaId?, configVehiculoId?, tipologia?, peso?, valorMercancia? }] }`
  → `202 { trabajoId: UUID, estado: "Pendiente", consultarEn: string }`
- `POST /api/solicitudes/{id}/rechazar` — `Comercial, CoordinadorFletes, Admin`.
  `Body: { motivo, comentario? }` → `204`
- `POST /api/solicitudes/{id}/recalcular-score` — `CoordinadorFletes, Admin` → `200 DesgloseScoreDto`

### Captura — `/api/captura-solicitudes` (`Comercial, Admin`)

- `POST /api/captura-solicitudes/analizar-excel` — no persiste; solo lee el Excel para que el usuario mapee columnas.
  `multipart/form-data: archivo` → `200 AnalisisExcelDto`
- `POST /api/captura-solicitudes` — crea la solicitud completa; resuelve/crea rutas y vehículos por texto libre.
  `Body: { clienteId?, clienteBp?, tipo: "spot"|"licitacion", modalidad?, deadline?, valorEstimado?, referencia?, horasCargue?, horasDescargue?, lineas: [{ origen, destino, tipoVehiculo?, carroceria?, tipologia?, lineaNegocio?, claseViaje?, claseContenedor?, exclusivo?, consolidado?, retornaContenedor?, ciudadDevolucionContenedor?, levanteContenedorDiferente?, ciudadLevanteContenedor?, valorAprovisionamiento?, peso?, valorMercancia?, posiciones?: PosicionCargaDto[] }] }`
  → `201 CapturaResultDto`
- `GET /api/captura-solicitudes/aprovisionamiento` — piso SICE-TAC del viaje vacío (levante → origen).
  `Query: levante, origen, configuracion?` → `200 AprovisionamientoDto`
- `GET /api/captura-solicitudes/clientes` — `Query: q?` → `200 ClienteBusquedaDto[]`

### Propuesta de tarifa — `/api/solicitudes/{id}/propuesta-tarifa` (`[Authorize]`)

- `GET` — compara plaza SAP / SICE-TAC / histórico y propone el valor más alto por línea. No persiste → `200 PropuestaSolicitudDto`

### Cotizaciones — `/api/cotizaciones` (`[Authorize]`)

- `POST /api/cotizaciones` — `Comercial, AdminTarifas, CoordinadorFletes, Admin`. Crea o recupera el borrador de una solicitud.
  `Body: { solicitudId }` → `201 CotizacionDto`
- `GET /api/cotizaciones/{id}` → `200 CotizacionDto`
- `POST /api/cotizaciones/{id}/aplicar-propuestas` — `Comercial, AdminTarifas, CoordinadorFletes, Admin`. Precarga como fletes el valor propuesto por la Biblioteca → `200 AplicarPropuestasResultDto`
- `POST /api/cotizaciones/{id}/lineas` — `AdminTarifas, CoordinadorFletes, Admin`.
  `Body: { lineaSolicitudId?, valorFlete, valorRetornoContenedor?, fuente: "sicetac"|"historica"|"manual"|"tarifario" }` → `200 CotizacionDto`
- `POST /api/cotizaciones/{id}/conexos` — `Comercial, CoordinadorFletes, Admin`.
  `Body: { items: [{ servicioConexoId, cantidad?, valorAplicado? }] }` (sin `valorAplicado`, se calcula `valorBase × cantidad`) → `200 CotizacionDto`
- `POST /api/cotizaciones/{id}/entregar` — `AdminTarifas, CoordinadorFletes, Admin`. Ver
  [nota sobre el correo asíncrono](docs/contrato-integracion/README.md#53-entregar-una-cotización-ya-no-espera-al-correo).
  `multipart/form-data: formato?` (PDF, opcional, máx. 10 MB) → `200 EntregarCotizacionResultDto`
- `POST /api/cotizaciones/{id}/resultado` — `Comercial, AdminTarifas, CoordinadorFletes, Admin`. Realimenta Tarifas Ganadas si `resultado="ganada"`.
  `Body: { resultado: "ganada"|"perdida", motivo?, comentario? }` → `200 CotizacionDto`
- `GET /api/cotizaciones/{id}/exportar-excel` → `200`, `Content-Disposition: attachment` (binario .xlsx)

### Biblioteca SICE-TAC (matriz oficial, solo lectura) — `/api/biblioteca-sicetac` (`[Authorize]`)

- `GET` — `Query: q?, periodo?, configuracion?, tipoCarga?, unidadTransporte?, condicionCarga?, viaEstandar?, top? (1-500, def. 50), page? (>=1, def. 1)` → `200 MatrizSicetacResultDto`
- `GET /periodos` → `200 number[]` (los 12 más recientes)
- `GET /filtros` → `200 SicetacFiltrosDto`

### Biblioteca histórico SAP (solo lectura) — `/api/biblioteca-sap-historico` (`[Authorize]`)

- `GET` — `Query: q?, lineaNegocio?, tipoVehiculo?, tipoCarroseria?, claseViaje?, top? (1-500, def. 50)` → `200 SapHistoricoResultDto`
- `GET /filtros` → `200 SapHistoricoFiltrosDto`

### Biblioteca plaza SAP (solo lectura) — `/api/biblioteca-sap-plaza` (`[Authorize]`)

- `GET` — mismos parámetros que el histórico + `page? (>=1, def. 1)` (issue #156: ~22,8 K filas medidas en vivo, este SÍ pagina; el histórico NO — ver nota de la sección de Paginación) → `200 SapHistoricoResultDto`
- `GET /filtros` → `200 SapHistoricoFiltrosDto`
- Ajuste **local** de vigencia sobre esta misma ruta (`AdminTarifas, CoordinadorFletes, Admin`):
  - `PATCH /{tarifaId}/vigencia` — `Body: { nuevaVigencia, motivo? }` → `204`
  - `POST /vigencia/masivo` — `Body: { tarifaIds: number[], modo: "fecha"|"extender", nuevaVigencia?, cantidad?, unidad?: "dias"|"meses", motivo? }` → `200 AjusteVigenciaResultDto`
  - `DELETE /{tarifaId}/vigencia` — vuelve a regirse por la vigencia de SAP → `204` / `404`

### Reglas Solo-SICETAC — `/api/reglas-solo-sicetac` (`AdminTarifas, Admin`)

- `GET` → `200 ReglaSoloSicetacDto[]`
- `POST` — `Body: { origen, destino, tipoVehiculo }` → `201 ReglaSoloSicetacDto`
- `DELETE /{id}` → `204` / `404`
- `POST /importar-excel` — columnas Origen, Destino, Tipo de vehículo.
  `multipart/form-data: archivo` → `200 ImportarReglasResultDto`

### Servicios conexos — `/api/servicios-conexos` (`[Authorize]`)

- `GET` — `Query: soloActivos? (def. false), q?, categoria?, page? (def. 1), pageSize? (def. 50, máx. 200)` → `200 PaginaServiciosDto`
- `POST` — `ServiciosLogisticos, AdminTarifas, Admin`.
  `Body: { nombre, categoria? ("logistico"|"adicional", def. logistico), reglaCobro?, valorBase?, vigenteHasta?, activo? (def. true) }` → `201 ServicioConexoDto`
- `PATCH /{id}` — `ServiciosLogisticos, AdminTarifas, Admin`. Solo se actualizan los campos presentes → `200 ServicioConexoDto`
- `POST /importar-excel` — `ServiciosLogisticos, AdminTarifas, Admin`. Columnas Nombre, Valor base, Vigente hasta, Categoría, Activo.
  `multipart/form-data: archivo` → `200 ImportarServiciosResultDto`

### Tarifas Ganadas (biblioteca propia, solo lectura) — `/api/tarifas-historicas` (`[Authorize]`)

- `GET` — con `ruta` o `vehiculo` → sugerencias para el costeo (siempre las 10 más recientes, sin paginar); con `q`/`mercancia`/`vigencia`/`origen` → búsqueda filtrada, PAGINADA; sin nada → las más recientes sin paginar.
  `Query: q?, ruta? (uuid), vehiculo? (uuid), mercancia?, vigencia?, origen? ("ganada"|"legado"), page? (>=1, def. 1, solo aplica a la búsqueda filtrada), pageSize? (1-200, def. 50, ídem)`
  → `200 BibliotecaItemDto[]` (sugerencias/recientes) **o** `200 { items: BibliotecaItemDto[], total }` (búsqueda filtrada, issue #166 — antes `Take(50)` fijo sin `total` ni forma de pedir más)

### Tarifas SICE-TAC (caché local admin) — `/api/tarifas-sicetac` (`[Authorize]`)

- `GET` — `Query: origen?, destino?, config?, tipoCarga?, unidad?, condicion?, periodo?` → `200 TarifaSicetacDto[]`
- `POST /importar` — `CoordinadorFletes, Admin`. Upsert idempotente.
  `multipart/form-data: archivo` → `200 ImportResumenDto`
- `GET /importaciones` → `200 ImportacionDto[]`

### Métricas — `/api/metricas` (`LiderProceso, AdminTarifas, Admin`)

- `GET /cumplimiento` — KPIs del Tablero del Líder → `200 CumplimientoDto`

### Priorización — `/api/priorizacion/parametros` (`Admin`)

Cambiar esto reordena la bandeja de todo el mundo — ver
[nota sobre qué pesos mueven el puntaje](docs/contrato-integracion/README.md#51-recalibrar-la-priorización).

- `GET` → `200 ParametrosScoreDto`
- `PUT` — no espera el recálculo (corre aparte). Los 4 pesos deben sumar 1.0 (`400 PARAMETROS_SCORE_INVALIDOS` si no).
  `Body: ParametrosScoreDto` → `200 ParametrosScoreDto`

### Plan de cargue (cubicación 3D) — `/api/planes-cargue`

- `GET` — `[Authorize]`. Listado contra la proyección local.
  `Query: page? (>=1, def. 1), pageSize? (1-200, def. 50)` → `200 { items: PlanCargueDto[], total }`
  (issue #166 — antes tope fijo de 100 sin `total` ni forma de pedir más)
- `POST /{id}/optimizar` — `Comercial, CoordinadorFletes, Admin`. No bloquea; `503 CUBICACION_EN_VERIFICACION` si la compuerta de paridad está cerrada, `409` si ya está procesando.
  `Body?: { estrategia?, algoritmo?, semilla?, respetarBloqueadas? }` → `202 SolicitudDeCubicacionAceptadaDto`
- `GET /{id}` — `[Authorize]`. Consulta de respaldo → `200 PlanCargueDto` / `404`
- `POST /desde-linea/{lineaId}` — `Comercial, CoordinadorFletes, Admin` (S3-HU-04.3, issue #78). Puente Mesa
  de Costeo → 3D: publica `crear_desde_linea` para que Cubicador arme la flota a partir de las posiciones de
  la línea. No pasa por la compuerta de paridad (es una operación de datos, no de cálculo, mismo criterio que
  los 8 comandos de la sección 6 de `docs/mensajeria/CUBICACION.md`). `404 LINEA_SOLICITUD_NO_ENCONTRADA` si
  la línea no existe.
  → `202 ComandoAceptadoDto`
- `GET /por-linea/{lineaId}` — `[Authorize]` (S3-HU-04.3, issue #78). Sondeo del `POST` de arriba contra la
  proyección local: lista vacía mientras Cubicador sigue `procesando`, se llena cuando llega
  `plan-de-cargue.generado`. ⚠️ Limitación conocida: hoy el worker de Cubicador solo reporta el primer
  vehículo de una flota multi-vehículo, así que esta lista puede no reflejar el resto de la flota — pendiente
  de resolver del lado del motor, fuera de alcance de este endpoint.
  → `200 PlanFlotaItemDto[]`
- `GET /{id}/colocaciones` — `[Authorize]` (S3-HU-04.3, issue #78). Todas las colocaciones del plan, cargadas
  y rechazadas — a diferencia de `.../no-cargadas` (sección 7.2 de `docs/mensajeria/CUBICACION.md`), que
  filtra por `Cargada = false`. `404` si el plan no existe.
  → `200 ColocacionCargueDto[]`

### Catálogo de vehículos (Cubicador) — `/api/vehiculos`

- `GET` — `[Authorize]`. Catálogo contra la proyección local (issue #112) — nunca llama a Cubicador.
  `Query: page? (>=1, def. 1), pageSize? (1-200, def. 50)` → `200 { items: VehiculoCubicacionDto[], total }`
  (issue #166 — antes devolvía el array completo sin tope; catálogo hoy chico, pagina igual por consistencia)
- `POST` — `Comercial, CoordinadorFletes, Admin`. Alta/edición, publica por cola. → `202 ComandoAceptadoDto`
- `DELETE /{id}` — `Admin`. `409 VEHICULO_EN_USO` si algún plan de cargue todavía lo referencia (issue #155).
  → `202 ComandoAceptadoDto`

### Trabajos — `/api/trabajos` (`[Authorize]`)

- `GET /{id}` — estado y avance de cualquier trabajo en segundo plano → `200 EstadoTrabajoDto` / `404`

### Salud — `/health` (anónimo, sin auth)

- `GET` → `200`/`503` `{ status, duracion, chequeos: { [nombre]: { status, descripcion, error } } }`

---

## 4. Hubs de SignalR — resumen de conexión

```js
const conn = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/trabajos", { headers: { "X-Portal-Usuario-Id": id } }) // o negotiate con el header
  .build();
conn.on("avance", (aviso) => { /* ... */ });
await conn.start();
await conn.invoke("Suscribir", trabajoId);
// al salir de la pantalla:
await conn.invoke("Desuscribir", trabajoId);
```

Mismo patrón para `/hubs/bandeja` (sin parámetro) y `/hubs/plan-cargue`
(con `planCargueId`). Detalle de por qué son 3 hubs separados y no uno:
[`docs/contrato-integracion/README.md`, sección 5](docs/contrato-integracion/README.md#5-avisos-en-vivo).

---

## 5. Excepciones a "cualquier rol autenticado"

Todo lo que no está en esta lista, y no tiene roles anotados arriba en su
propio endpoint, solo exige una identidad válida con alguno de los 6 roles —
no importa cuál. Referencia completa de quién puede qué:
[`docs/ROLES_Y_PERMISOS.md`](docs/ROLES_Y_PERMISOS.md).

---

## 6. Enums

Viajan como el string exacto (ver [sección 1](#enums-se-mandan-y-devuelven-como-texto)).

```
RolUsuario            CoordinadorFletes · LiderProceso · ServiciosLogisticos · Admin · Comercial · AdminTarifas
TipoSolicitud          Spot · Licitacion
ModalidadSolicitud     Importacion · Exportacion · Nacional · Extradimensionada · CargaGeneral
EstadoSolicitud        Pendiente · EnCosteo · Entregada · Descartada
TipologiaCarga         Contenedor · CargaSuelta · GranelSolido · GranelLiquido · Alimentos · Refrigerados · CargaSeca
EstadoLinea            PorCostear · AutoResuelta · Revisada
TipoPendienteDeCosteo  Tecnica · Manual
CondicionCarga         Cargado · Vacio
FuenteFlete            Sicetac · Historica · Manual · Tarifario
EstadoCotizacion       Borrador · Entregada
ResultadoCotizacion    SinDefinir · Ganada · Perdida
LineaNegocio           PuertoAInterior · InteriorAPuerto · Nacional · DevolucionContenedor · Andino · Licitaciones
ClaseViaje             Contenedor · Cupo · CargaSuelta · Extradimensionado
```

`src/CabinaFletes.Domain/Enums/Enums.cs` es la fuente de la verdad; si este
apéndice y el código difieren, gana el código.
