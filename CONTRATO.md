# Contrato de la API — NEXO (Nexo)

Referencia completa, endpoint por endpoint, de **lo que el backend exige hoy**:
header de identidad, rol, parámetros, campos obligatorios con sus límites,
respuesta de éxito y códigos de error. Está verificada contra el código de
`src/Nexo.Api` (controllers, DTOs de request y validadores de Application).

Es un **complemento** de
[`docs/contrato-integracion/README.md`](docs/contrato-integracion/README.md), que
explica el *porqué* de la identidad, los trabajos pesados y los avisos en vivo.
En reglas de diseño, gana el README. **En campos, rutas, roles y validaciones,
gana el código**: si este documento y el código difieren, el código es la
verdad y este documento debe corregirse.

Base URL: la que publique cada ambiente (`ASPNETCORE_URLS` / `API_PORT`, ver
`.env.example`). Local con `dotnet run`: `http://localhost:5080`. Docker compose:
`http://localhost:${API_PORT:-8080}`. Swagger vive en `/swagger`, **solo en
Development**.

---

## 1. Convenciones generales

### 1.1 Autenticación de usuarios (identidad del portal)

No hay login, no hay contraseña y no hay token. Cada petición manda el id
numérico (`long`) de la persona ya autenticada en el portal de la
Administración, en un header:

```http
X-Portal-clsUsuario-Id: 10123
```

> ⚠️ **Discrepancia abierta — nombre del header.** El código
> (`clsIdentidadPortalAuthenticationHandler.HeaderIdUsuario`) espera hoy
> **`X-Portal-clsUsuario-Id`**. El contrato acordado con el portal
> ([README §1](docs/contrato-integracion/README.md#1-identidad),
> [ADR-001](docs/contrato-integracion/ADR-001-identidad-sin-token.md),
> [`openapi.yaml`](docs/contrato-integracion/openapi.yaml)), los e2e
> (`build/e2e/recorrido.py`) y los scripts de QA usan **`X-Portal-Usuario-Id`**.
> Todo indica que el renombrado masivo con prefijo `cls` alcanzó también esta
> constante. Mientras no se corrija, **solo funciona `X-Portal-clsUsuario-Id`**;
> un cliente que mande `X-Portal-Usuario-Id` recibe `401 SESION_INVALIDA` en
> todos los endpoints. Cuando se corrija el código, esta sección vuelve a
> `X-Portal-Usuario-Id`.

Cómo se resuelve cada petición (sin caché de sesión):

| Situación | Respuesta |
|---|---|
| Falta el header, o su valor no es un número entero | `401 SESION_INVALIDA` |
| El id no existe en la tabla de usuarios del portal | `401 SESION_INVALIDA` (mismo mensaje; no revela si el id existe) |
| El id existe, pero la persona no tiene uno de los 6 roles de Nexo | `403 SIN_PERMISO_CABINA_FLETES` en **todos** los endpoints |
| Tiene rol de Nexo, pero no uno de los que exige el endpoint | `403 SIN_PERMISO_CABINA_FLETES` |

La FallbackPolicy (`clsProgram.cs`) exige identidad **y** un rol válido en todo
endpoint que no diga otra cosa. Únicas excepciones:

- `GET /health`: anónimo.
- `GET /api/lineas-solicitud/{id}/cubicacion`: servicio a servicio (§1.2).

Qué se envía y qué no (la lista de permisos del portal nunca viaja):
[README §1](docs/contrato-integracion/README.md#1-identidad).

### 1.2 Autenticación servicio a servicio (Cubicador)

Solo para `GET /api/lineas-solicitud/{id}/cubicacion`, que consume el
microservicio Cubicador (Python), no el frontend:

```http
Authorization: Bearer <valor de Cubicador3D:InboundApiKey>
```

El valor es un secreto compartido de configuración (el mismo que
`NEXO_CALLBACK_API_KEY` del lado del Cubicador). Nunca se documenta ni se
comparte fuera de la configuración de cada ambiente. Sin clave configurada, sin
header o con un valor distinto → `401 NO_AUTORIZADO`. El header de identidad del
portal **no** sirve en este endpoint.

### 1.3 Los 6 roles

`Comercial · CoordinadorFletes · AdminTarifas · ServiciosLogisticos · LiderProceso · Admin`

Un usuario tiene exactamente uno. `Admin` está incluido en todos los endpoints
restringidos. La matriz completa está en la [sección 5](#5-matriz-de-permisos) y
en [`docs/ROLES_Y_PERMISOS.md`](docs/ROLES_Y_PERMISOS.md).

### 1.4 Formato de error (siempre el mismo)

```ts
interface ApiError {
  code: string          // estable, para ramificar (ej. "SOLICITUD_NO_ENCONTRADA")
  message: string        // en español, para mostrar — no decidir con esto
  timestamp: string       // ISO 8601
  details?: unknown        // en 400 VALIDATION_ERROR: { "Campo": ["mensaje", ...] }
}
```

| HTTP | Código | Cuándo |
|---|---|---|
| `400` | `VALIDATION_ERROR` | Falló la validación automática del body/query (DataAnnotations o JSON mal formado) |
| `400` | código específico (ver cada endpoint) | Regla de negocio de Application |
| `401` | `SESION_INVALIDA` | Sin identidad válida del portal |
| `401` | `NO_AUTORIZADO` | Bearer de servicio inválido (solo §1.2) |
| `403` | `SIN_PERMISO_CABINA_FLETES` | Identidad válida, rol insuficiente |
| `404` | específico del recurso | No existe |
| `409` | específico, o `CONFLICTO_CONCURRENCIA` | Conflicto de estado o edición simultánea (RowVersion) |
| `429` | `DEMASIADAS_SOLICITUDES` | Rate limit (§1.8), con header `Retry-After: 30` |
| `503` | específico | Fuente externa no configurada o no responde |
| `500` | `INTERNAL_ERROR` | Error no controlado (en Production el mensaje es genérico) |

Detalle de `400 VALIDATION_ERROR`:

- `details` trae una entrada por campo que falló. La clave es el nombre del
  campo como lo ve el model binder (puede venir en PascalCase o con ruta, ej.
  `"Lineas[0].Origen"`, `"$.deadline"` para JSON inválido).
- Los mensajes de las DataAnnotations sin `ErrorMessage` propio salen en el
  idioma por defecto del framework (inglés). No se debe ramificar por el
  mensaje, solo por `code` y por la clave del campo.

### 1.5 Reglas de validación comunes

Aplican a todos los bodies JSON salvo que el endpoint diga otra cosa.

- **Campos obligatorios.** Se marcan como **requerido** en cada tabla. Un campo
  de texto requerido que falta, es `null` o viene vacío produce
  `400 VALIDATION_ERROR`.
- **Tipos de referencia no anulables en records.** En los endpoints de
  plan de cargue y vehículos, los campos de texto no anulables (ej. `nombre`,
  `descripcion`) son obligatorios implícitamente por Nullable Reference Types.
  Faltarlos da `400 VALIDATION_ERROR`.
- **Números sin `?` no son obligatorios.** Si se omiten, llegan como `0` o
  `false` (ej. `valorFlete`, `largoMm`). El endpoint puede rechazarlos luego
  por regla de negocio, pero no por falta del campo.
- **Guid no anulable.** `clienteId`, `solicitudId`, `servicioConexoId` son
  obligatorios y deben ser un Guid válido. Un Guid mal formado da
  `400 VALIDATION_ERROR`.
- **Fechas.** ISO 8601. `DateTimeOffset` acepta `"2026-09-30"` o
  `"2026-09-30T17:00:00-05:00"`. `DateOnly` (`vigenteHasta`) acepta solo
  `"2026-09-30"`.
- **Decimales monetarios.** Rango `0`–`9999999999999.99` salvo que se indique
  otro.
- **Rutas con `{id}`.** Todos los ids de ruta son Guid (`{id:guid}`) salvo
  `tarifaId`, que es entero (`{tarifaId:int}`). Un id con formato inválido no
  calza con ninguna ruta y responde `404` sin cuerpo `ApiError`.

### 1.6 Enums: se mandan y devuelven como texto

Todos los enums de C# viajan como **string exacto del nombre del valor**
(`PascalCase`, ej. `"Ganada"`, `"EnCosteo"`), nunca como número. El JSON de
entrada y salida es camelCase. Valores completos en la [sección 6](#6-enums).

**Asimetría en los campos de escritura validados por regex** (case-insensitive,
no son el enum real): el body acepta el valor en minúscula, pero la respuesta
devuelve el enum real en PascalCase.

| Campo | Acepta | Devuelve |
|---|---|---|
| `tipo` (crear/capturar solicitud) | `"spot"`, `"licitacion"` | `"Spot"`, `"Licitacion"` |
| `fuente` (línea de cotización) | `"sicetac"`, `"historica"`, `"manual"`, `"tarifario"` | `"Sicetac"`, ... |
| `resultado` (cotización) | `"ganada"`, `"perdida"` | `"Ganada"`, `"Perdida"` |

Los campos de texto que se convierten a enum en Application (`modalidad`,
`tipologia`) aceptan el nombre del valor sin distinguir mayúsculas. Un valor
desconocido da `400 ENUM_INVALIDO`.

### 1.7 Paginación

Donde existe es `page` (1-based, mínimo 1) + `pageSize` o `top` según el
endpoint. La respuesta trae `{ items, total }` o el equivalente propio del
endpoint. Todos los parámetros de paginación son opcionales. Un valor fuera de
rango da `400 VALIDATION_ERROR`.

| Endpoint | Parámetros | Default | Máximo |
|---|---|---|---|
| `GET /api/solicitudes` | `page`, `pageSize` | 1, 50 | `pageSize` 200 |
| `GET /api/tarifas-historicas` (solo rama "búsqueda") | `page`, `pageSize` | 1, 50 | 200 |
| `GET /api/servicios-conexos` | `page`, `pageSize` | 1, 50 | 200 |
| `GET /api/vehiculos` | `page`, `pageSize` | 1, 50 | 200 |
| `GET /api/planes-cargue` | `page`, `pageSize` | 1, 50 | 200 |
| `GET /api/biblioteca-sicetac` | `page`, `top` | 1, 50 | `top` 500 |
| `GET /api/biblioteca-sap-plaza` | `page`, `top` | 1, 50 | 500 |
| `GET /api/biblioteca-sap-historico` | solo `top` (no pagina) | 50 | 500 |
| `GET /api/reglas-solo-sicetac` | no pagina | — | — |

Contexto de la decisión: `docs/decisiones/paginacion-endpoints-listado.md` y
`docs/decisiones/timeout-biblioteca-sicetac.md`.

### 1.8 Rate limiting

Particionado por rol del usuario autenticado (o IP si no hay rol resuelto). Al
exceder: `429 DEMASIADAS_SOLICITUDES` + `Retry-After: 30`.

| Política | Endpoints | Límite |
|---|---|---|
| Estricta (ventana deslizante) | `GET /api/biblioteca-sicetac`, `GET /api/biblioteca-sap-plaza` | 20/min por rol; 60/min para `Admin` y `AdminTarifas` |
| Estándar (ventana fija) | `GET` de `solicitudes`, `planes-cargue`, `vehiculos`, `tarifas-historicas`, `reglas-solo-sicetac`, `biblioteca-sap-historico` | 120/min por rol |

El resto de endpoints no tiene rate limit.

### 1.9 Carga de archivos

`multipart/form-data`. Límite de la petición: 10 MB (`[RequestSizeLimit]`); una
petición más grande la corta el servidor antes de llegar al endpoint.

| Endpoint | Campo | Extensiones | Si falta el archivo |
|---|---|---|---|
| `POST /api/captura-solicitudes/analizar-excel` | `archivo` | `.xlsx`, `.xlsm` | `400 ARCHIVO_REQUERIDO` |
| `POST /api/tarifas-sicetac/importar` | `archivo` | `.xlsx`, `.xlsm` | `400 ARCHIVO_REQUERIDO` |
| `POST /api/reglas-solo-sicetac/importar-excel` | `archivo` | `.xlsx`, `.xlsm` | `400 VALIDATION_ERROR` (ver nota) |
| `POST /api/servicios-conexos/importar-excel` | `archivo` | `.xlsx`, `.xlsm` | `400 VALIDATION_ERROR` (ver nota) |
| `POST /api/cotizaciones/{id}/entregar` | `formato` (opcional) | PDF, sin validación de extensión | se entrega sin adjunto |

Extensión no permitida → `400 FORMATO_NO_SOPORTADO`. Archivo vacío (0 bytes) →
`400 ARCHIVO_REQUERIDO`.

> Nota: en reglas-solo-sicetac y servicios-conexos el parámetro `archivo` está
> declarado no anulable, así que si falta el campo el framework responde
> `400 VALIDATION_ERROR` con `details.archivo` antes de llegar a la validación
> propia. Con el campo presente pero vacío sí responde `ARCHIVO_REQUERIDO`.

### 1.10 Trabajos pesados: 202 + consulta de respaldo

Las operaciones pesadas no devuelven su resultado en la misma petición.
Responden **202** con un identificador; el resultado llega por SignalR o se
consulta con `GET /api/trabajos/{id}` (pliegos) o `GET /api/planes-cargue/{id}`
(planes de cargue). El aviso en vivo es una mejora, no una garantía. Detalle:
[README §4](docs/contrato-integracion/README.md#4-trabajos-pesados).

### 1.11 Avisos en vivo (SignalR)

| Hub | Suscribir | Evento | Trae |
|---|---|---|---|
| `/hubs/trabajos` | `Suscribir(trabajoId: Guid)` | `"avance"` | `AvisoDeTrabajo` (descarta si `secuencia <=` la última pintada) |
| `/hubs/bandeja` | `Suscribir()` (sin parámetro) | `"bandeja"` | `{ motivo, en }` — no trae datos: vuelve a consultar `GET /api/solicitudes` |
| `/hubs/plan-cargue` | `Suscribir(planCargueId: Guid)` | `"avance"` | `AvisoPlanCargueDto` |

Los tres exigen la misma identidad y rol que el resto del API (§1.1) y tienen
`Desuscribir(...)` con la misma firma. Ninguno reenvía lo perdido mientras el
cliente estuvo desconectado. Detalle:
[README §5](docs/contrato-integracion/README.md#5-avisos-en-vivo).

---

## 2. Tipos comunes

Referenciados por nombre en los endpoints de abajo. Todos los nombres de campo
viajan en camelCase.

```ts
type UUID = string
type FechaISO = string          // "2026-08-24T19:40:00Z"
type Fecha = string             // date-only, "2026-08-24"
type Decimal = number

interface Pagina<T> { items: T[]; total: number }

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
interface TrabajoAceptadoDto { trabajoId: UUID; estado: "Pendiente"; consultarEn: string }

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
interface CatalogoLineaResueltoDto {             // respuesta de resolver-catalogos
  indice: number                                 // posición de la línea en el array enviado
  rutaId: UUID; configVehiculoId: UUID | null
  rutaCreada: boolean
  clsVehiculoCreado: boolean                     // ⚠️ así se serializa hoy (artefacto del renombrado "cls"); se esperaría "vehiculoCreado"
}
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
  advertencia: string | null // no-nulo cuando NO es un match exacto — mostrar al usuario antes de confirmar
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
interface KpisPilotoDto {
  desde: FechaISO | null; hasta: FechaISO | null
  pliegosResueltos: number
  tiempoPromedioResolucionMinutos: number | null; tiempoMedianoResolucionMinutos: number | null
  pliegosBajo10Minutos: number; porcentajePliegosBajo10Minutos: number | null
  totalLineas: number; lineasAutoCosteadas: number; porcentajeLineasAutoCosteadas: number | null
  cotizacionesEmitidas: number; lineasConFleteManual: number
}

interface ParametrosScoreDto {
  pesoUrgencia: number; pesoValor: number; pesoProbabilidad: number; pesoTipo: number   // cada uno 0–1, suman 1.0 (±0.001)
  horizonteHoras: number                                                                 // > 0
  valorReferencia: Decimal                                                               // > 0
  puntajeLicitacion: number; puntajeSpot: number                                         // 0–100
  valorPorDefectoSinValor: number; probabilidadPorDefecto: number                        // 0–100
}

interface SesionActualDto { id: number; nombre: string; rol: RolUsuario; capacidades: string[] }

// ---- Cubicación / plan de cargue
interface SolicitudDeCubicacionAceptadaDto { planCargueId: UUID; estado: string; consultarEn: string }
interface ComandoAceptadoDto { correlationId: UUID; estado: string }   // 202 de los comandos por cola
interface PlanCargueDto {
  id: UUID; nombre: string; estado: string; estrategia: string; algoritmo: string
  puntaje: Decimal; pesoTotalKg: Decimal; volumenUtilizadoM3: Decimal
  utilizacionVolumen: Decimal; utilizacionPiso: Decimal
  totalItems: number; totalColocaciones: number; updatedAt: FechaISO
}
interface AvisoPlanCargueDto { planCargueId: UUID; estado: string; puntaje: Decimal; motivo: string | null }
interface PlanFlotaItemDto { id: UUID; numeroVehiculo: number; totalVehiculos: number; unidadesCargadas: number; unidadesTotales: number }
interface EjeVehiculoDto { posicionMm: Decimal; capacidadKg: Decimal }
interface VehiculoCubicacionDto {
  id: UUID; nombre: string; descripcion: string | null
  largoInternoMm: Decimal; anchoInternoMm: Decimal; altoInternoMm: Decimal
  capacidadKg: Decimal; taraKg: Decimal; puertaAnchoMm: Decimal; puertaAltoMm: Decimal; puertaPosicion: string
  ejes: EjeVehiculoDto[]; toleranciaParedesMm: Decimal; toleranciaEntreUnidadesMm: Decimal
  esPreset: boolean
}
interface LineaParaCubicacionDto { lineaId: UUID; posiciones: PosicionCargaDto[]; origen: string | null; destino: string | null }
interface CubicacionRapidaResultado {
  matrices: object[]                 // matriz de evaluación por unidad
  sugerencia: object | null          // vehículo sugerido
  advertencias: string[]
  sugerenciaFlota: object | null
  extradimension: object | null
}
// ResultadoPlanDto (resultado/exportar), ColocacionCargueDto y MetricasCubicacion:
// shape completo en Nexo.Application/CasosDeUso/PlanDeCargue/DTOs/PlanCargueDtos.cs y
// Nexo.Application/CasosDeUso/Cubicacion/DTOs/CubicacionRapidaDtos.cs; espejan 1:1 lo
// documentado en cubicador/CONTRATO.md §3.2.
```

---

## 3. Endpoints por módulo

Formato de cada endpoint:

- **Roles**: "Todos" = cualquiera de los 6 roles. Si no, la lista exacta.
- **Query / Body**: tabla con **requerido** o *opcional*, tipo y restricciones.
- **→** respuesta de éxito.
- **Errores**: los propios del endpoint, además de los generales de §1.4
  (`401`, `403`, `400 VALIDATION_ERROR`, `500`), que aplican siempre.

### 3.1 Sesión — `/api/sesion`

#### `GET /api/sesion/actual`
- **Roles**: Todos.
- **→** `200 SesionActualDto`. No crea sesión; se llama al entrar al módulo.
- `id` es el id del portal (el mismo del header), no un id interno.

### 3.2 Bandeja y solicitudes — `/api/solicitudes`

#### `GET /api/solicitudes` — Bandeja Priorizada
- **Roles**: Todos. Rate limit estándar.

| Query | | Tipo | Restricción |
|---|---|---|---|
| `estado` | *opcional* | string | `EstadoSolicitud` |
| `atencion` | *opcional* | string | `pendientes` \| `automatico` \| `administrador` \| `atendidas` \| `todas`. Sin filtros → cola de pendientes |
| `desde`, `hasta` | *opcional* | fecha ISO | rango de fecha de captura |
| `vigencia` | *opcional* | string | `vigente` \| `vencida` |
| `orderBy` | *opcional* | string | ignorado: siempre ordena por score |
| `page` | *opcional* | int | ≥ 1, def. 1 |
| `pageSize` | *opcional* | int | 1–200, def. 50 |

- **→** `200 Pagina<BandejaItemDto>`

#### `GET /api/solicitudes/{id}` — abrir solicitud (costeo pre-armado)
- **Roles**: Todos.
- **→** `200 SolicitudCosteoDto`
- **Errores**: `404 SOLICITUD_NO_ENCONTRADA` · `409 SOLICITUD_DESCARTADA`

#### `POST /api/solicitudes` — alta spot (o cabecera de licitación)
- **Roles**: `CoordinadorFletes`, `Admin`.

| Body | | Tipo | Restricción |
|---|---|---|---|
| `clienteId` | **requerido** | UUID | debe existir |
| `tipo` | **requerido** | string | `spot` \| `licitacion` |
| `modalidad` | *opcional* | string | `ModalidadSolicitud` |
| `deadline` | *opcional* | fecha ISO | |
| `valorEstimado` | *opcional* | decimal | monetario |
| `probabilidadCierre` | *opcional* | decimal | 0–100 |
| `linea` | *opcional* | objeto | ver `LineaBulk` abajo |

- **→** `201 { id: UUID }`, header `Location: /api/solicitudes/{id}`
- **Errores**: `400 CLIENTE_INVALIDO` · `400 ENUM_INVALIDO`

`LineaBulk` (usada por `linea` y por `lineas:bulk`):

| Campo | | Tipo | Restricción |
|---|---|---|---|
| `rutaId` | *opcional* | UUID | ruta del catálogo (obtenerla con `resolver-catalogos`) |
| `configVehiculoId` | *opcional* | UUID | |
| `tipologia` | *opcional* | string | `TipologiaCarga` |
| `peso` | *opcional* | decimal | 0–999999.99 kg |
| `valorMercancia` | *opcional* | decimal | monetario |

#### `POST /api/solicitudes/resolver-catalogos`
Resuelve texto libre a `rutaId`/`configVehiculoId`, creando lo que falte en el
catálogo. No crea ninguna solicitud; es el paso previo a `lineas:bulk`.

- **Roles**: `Comercial`, `CoordinadorFletes`, `Admin`.
- **Body**: **array** (no objeto) de al menos 1 elemento:

| Campo | | Tipo | Restricción |
|---|---|---|---|
| `origen` | **requerido** | string | máx. 150 |
| `destino` | **requerido** | string | máx. 150 |
| `tipoVehiculo` | *opcional* | string | máx. 100 |
| `carroceria` | *opcional* | string | máx. 100 |

- **→** `200 CatalogoLineaResueltoDto[]`
- **Errores**: `400 SIN_LINEAS` (array vacío)

#### `POST /api/solicitudes/{id}/lineas:bulk` — carga masiva de un pliego
- **Roles**: `CoordinadorFletes`, `Admin`.
- **Body**: `{ lineas: LineaBulk[] }`, **requerido**, mínimo 1 línea.
- **→** `202 TrabajoAceptadoDto`. El costeo corre en segundo plano (§1.10).
- **Errores**: `404 SOLICITUD_NO_ENCONTRADA` · `409 SOLICITUD_NO_EDITABLE`

#### `POST /api/solicitudes/{id}/rechazar`
- **Roles**: `Comercial`, `CoordinadorFletes`, `Admin`.

| Body | | Tipo | Restricción |
|---|---|---|---|
| `motivo` | **requerido** | string | máx. 100 |
| `comentario` | *opcional* | string | máx. 500 |

- **→** `204`
- **Errores**: `404 SOLICITUD_NO_ENCONTRADA` · `409 SOLICITUD_YA_DESCARTADA`

#### `POST /api/solicitudes/{id}/recalcular-score`
- **Roles**: `CoordinadorFletes`, `Admin`. Sin body.
- **→** `200 DesgloseScoreDto`
- **Errores**: `404 SOLICITUD_NO_ENCONTRADA`

#### `GET /api/solicitudes/{id}/propuesta-tarifa`
Compara plaza SAP, SICE-TAC e histórico SAP y propone el valor más alto por
línea. No persiste nada.

- **Roles**: Todos.
- **→** `200 PropuestaSolicitudDto`
- **Errores**: `404 SOLICITUD_NO_ENCONTRADA` · `404 LINEA_NO_ENCONTRADA`

### 3.3 Captura de solicitudes — `/api/captura-solicitudes`

**Todo el controller**: `Comercial`, `Admin`.

#### `POST /api/captura-solicitudes/analizar-excel`
No persiste; lee el Excel para que el usuario mapee columnas.

- **Body**: `multipart/form-data`, campo `archivo` (§1.9).
- **→** `200 AnalisisExcelDto`
- **Errores**: `400 ARCHIVO_REQUERIDO` · `400 FORMATO_NO_SOPORTADO` · `400 EXCEL_INVALIDO` · `400 EXCEL_SIN_DATOS`

#### `POST /api/captura-solicitudes` — crear solicitud completa
Resuelve o crea rutas y vehículos a partir del texto libre de las líneas.

| Body (cabecera) | | Tipo | Restricción |
|---|---|---|---|
| `clienteId` | condicional | UUID | **requerido si no viene `clienteBp`**; debe existir |
| `clienteBp` | condicional | string | máx. 20. **Requerido si no viene `clienteId`**; debe existir en el catálogo del DW |
| `tipo` | **requerido** | string | `spot` \| `licitacion` |
| `modalidad` | *opcional* | string | `ModalidadSolicitud` |
| `deadline` | *opcional* | fecha ISO | |
| `valorEstimado` | *opcional* | decimal | monetario |
| `referencia` | *opcional* | string | |
| `horasCargue` | *opcional* | decimal | 0–240 |
| `horasDescargue` | *opcional* | decimal | 0–240 |
| `lineas` | **requerido** | array | mínimo 1 |

| Cada línea | | Tipo | Restricción |
|---|---|---|---|
| `origen` | **requerido** | string | máx. 150 |
| `destino` | **requerido** | string | máx. 150 |
| `peso` | **condicional** | decimal | 0–999999.99 kg. **Requerido (> 0) si las posiciones no traen peso** |
| `tipoVehiculo` | *opcional* | string | máx. 100 |
| `carroceria` | *opcional* | string | máx. 100 |
| `tipologia` | *opcional* | string | `TipologiaCarga` |
| `lineaNegocio` | *opcional* | string | máx. 100 |
| `claseViaje` | *opcional* | string | máx. 50 |
| `claseContenedor` | *opcional* | string | máx. 20 |
| `exclusivo`, `consolidado`, `retornaContenedor`, `levanteContenedorDiferente` | *opcional* | boolean | |
| `ciudadDevolucionContenedor` | *opcional* | string | máx. 150 |
| `ciudadLevanteContenedor` | *opcional* | string | máx. 150 |
| `valorAprovisionamiento` | *opcional* | decimal | monetario |
| `valorMercancia` | *opcional* | decimal | monetario |
| `posiciones` | *opcional* | array | máx. 200 por línea |

| Cada posición | | Tipo | Restricción |
|---|---|---|---|
| `largoCm`, `anchoCm`, `altoCm` | **requerido** | decimal | 0.1–999999 |
| `cantidad` | *opcional* | int | 1–100000, def. 1 |
| `descripcion` | *opcional* | string | máx. 200 |
| `pesoKg` | *opcional* | decimal | 0–999999.99 (peso unitario) |

Regla de peso: el peso de la línea es `peso` si viene; si no, la suma de
`cantidad × pesoKg` de las posiciones. Debe ser > 0. Si vienen ambos, deben
coincidir con tolerancia del 1 %.

- **→** `201 CapturaResultDto`, header `Location: /api/solicitudes/{id}`
- **Errores**: `400 CLIENTE_REQUERIDO` · `400 CLIENTE_INVALIDO` · `400 CLIENTE_BP_INVALIDO` · `400 DWCUBOS_NO_CONFIGURADO` (se usó `clienteBp` y el DW no está configurado en el ambiente) · `400 ENUM_INVALIDO` · `400 LINEA_INCOMPLETA` · `400 PESO_REQUERIDO` · `400 PESO_INCONSISTENTE`

#### `GET /api/captura-solicitudes/aprovisionamiento`
Piso SICE-TAC del viaje vacío (levante → origen del cargue).

| Query | | Tipo | Restricción |
|---|---|---|---|
| `levante` | **requerido** | string | ciudad de levante del contenedor |
| `origen` | **requerido** | string | origen del cargue |
| `configuracion` | *opcional* | string | configuración vehicular |

- **→** `200 AprovisionamientoDto` (`candidato: null` si la matriz no trae la ruta)
- **Errores**: `400 PARAMETROS_REQUERIDOS` (falta `levante` u `origen`). Swagger
  los muestra como opcionales, pero son obligatorios.

#### `GET /api/captura-solicitudes/clientes`
- **Query**: `q` *opcional* (nombre, NIT o BP).
- **→** `200 ClienteBusquedaDto[]`. Con el DW configurado busca en clientes
  reales; sin él, en la tabla local.

### 3.4 Cotizaciones — `/api/cotizaciones`

#### `POST /api/cotizaciones` — crear o recuperar el borrador
- **Roles**: `Comercial`, `AdminTarifas`, `CoordinadorFletes`, `Admin`.
- **Body**: `{ solicitudId: UUID }` **requerido**.
- **→** `201 CotizacionDto`, header `Location: /api/cotizaciones/{id}`. Si ya
  existe un borrador para la solicitud, lo devuelve.
- **Errores**: `400 SOLICITUD_INVALIDA`

#### `GET /api/cotizaciones/{id}`
- **Roles**: Todos.
- **→** `200 CotizacionDto`
- **Errores**: `404 COTIZACION_NO_ENCONTRADA`

#### `POST /api/cotizaciones/{id}/aplicar-propuestas`
Precarga como fletes el valor más alto propuesto por la Biblioteca en las
líneas pendientes. Sin body.

- **Roles**: `Comercial`, `AdminTarifas`, `CoordinadorFletes`, `Admin`.
- **→** `200 AplicarPropuestasResultDto`
- **Errores**: `404 COTIZACION_NO_ENCONTRADA` · `409 COTIZACION_NO_EDITABLE`

#### `POST /api/cotizaciones/{id}/lineas` — agregar flete de una línea
- **Roles**: `AdminTarifas`, `CoordinadorFletes`, `Admin`. (`Comercial` **no**.)

| Body | | Tipo | Restricción |
|---|---|---|---|
| `fuente` | **requerido** | string | `sicetac` \| `historica` \| `manual` \| `tarifario` |
| `valorFlete` | *(ver nota)* | decimal | monetario. Si se omite llega como `0` |
| `lineaSolicitudId` | *opcional* | UUID | debe pertenecer a la solicitud |
| `valorRetornoContenedor` | *opcional* | decimal | monetario; suma al total aparte |

- **→** `200 CotizacionDto`
- **Errores**: `400 FUENTE_INVALIDA` · `400 LINEA_INVALIDA` · `404 COTIZACION_NO_ENCONTRADA` · `409 COTIZACION_NO_EDITABLE` · `409 HISTORICA_VENCIDA`

#### `POST /api/cotizaciones/{id}/conexos` — agregar servicios conexos
- **Roles**: `Comercial`, `CoordinadorFletes`, `Admin`. (`AdminTarifas` **no**.)
- **Body**: `{ items: [...] }` **requerido**, mínimo 1.

| Cada item | | Tipo | Restricción |
|---|---|---|---|
| `servicioConexoId` | **requerido** | UUID | servicio existente y activo |
| `cantidad` | *opcional* | decimal | 0.01–999999.99, def. 1 |
| `valorAplicado` | *opcional* | decimal | monetario. Si falta: `valorBase × cantidad` |

- **→** `200 CotizacionDto`
- **Errores**: `400 SERVICIO_INVALIDO` · `400 SERVICIO_INACTIVO` · `404 COTIZACION_NO_ENCONTRADA` · `409 COTIZACION_NO_EDITABLE`

#### `POST /api/cotizaciones/{id}/entregar`
Marca la entrega y envía la cotización por correo al comercial responsable
(el correo es asíncrono, ver
[README §5.3](docs/contrato-integracion/README.md#53-entregar-una-cotización-ya-no-espera-al-correo)).

- **Roles**: `AdminTarifas`, `CoordinadorFletes`, `Admin`. (`Comercial` **no**.)
- **Body**: `multipart/form-data`, campo `formato` *opcional* (PDF, máx. 10 MB).
  Se puede enviar la petición sin cuerpo.
- **→** `200 EntregarCotizacionResultDto`
- **Errores**: `400 COTIZACION_SIN_FLETE` · `404 COTIZACION_NO_ENCONTRADA` · `409 COTIZACION_YA_ENTREGADA` · `409 SOLICITUD_NO_TRANSICIONABLE`

#### `POST /api/cotizaciones/{id}/resultado` — ganada/perdida
Si `resultado = "ganada"`, realimenta la biblioteca de Tarifas Ganadas.
**Siempre hay que entregar primero** (`/entregar`).

- **Roles**: `Comercial`, `AdminTarifas`, `CoordinadorFletes`, `Admin`.

| Body | | Tipo | Restricción |
|---|---|---|---|
| `resultado` | **requerido** | string | `ganada` \| `perdida` |
| `motivo` | *opcional* | string | máx. 100 |
| `comentario` | *opcional* | string | máx. 500 |

- **→** `200 CotizacionDto`
- **Errores**: `400 RESULTADO_INVALIDO` · `404 COTIZACION_NO_ENCONTRADA` · `409 COTIZACION_NO_ENTREGADA` · `409 RESULTADO_YA_DEFINIDO` · `409 SOLICITUD_NO_TRANSICIONABLE`

#### `GET /api/cotizaciones/{id}/exportar-excel`
- **Roles**: Todos.
- **→** `200` binario `.xlsx` con `Content-Disposition: attachment`.
- **Errores**: `404 COTIZACION_NO_ENCONTRADA`

### 3.5 Biblioteca SICE-TAC (DW, solo lectura) — `/api/biblioteca-sicetac`

#### `GET /api/biblioteca-sicetac`
- **Roles**: Todos. Rate limit estricto.

| Query | | Tipo | Restricción |
|---|---|---|---|
| `q` | *opcional* | string | texto de ruta |
| `periodo` | *opcional* | int | def. el más reciente |
| `configuracion`, `tipoCarga`, `unidadTransporte`, `condicionCarga`, `viaEstandar` | *opcional* | string | valores de `/filtros` |
| `top` | *opcional* | int | 1–500, def. 50 (tamaño de página) |
| `page` | *opcional* | int | ≥ 1, def. 1 |

- **→** `200 MatrizSicetacResultDto`
- **Errores**: `503 DWSICE_SIN_DATOS`

#### `GET /api/biblioteca-sicetac/periodos`
- **Roles**: Todos. **→** `200 number[]` (los 12 más recientes).

#### `GET /api/biblioteca-sicetac/filtros`
- **Roles**: Todos. **→** `200 SicetacFiltrosDto` (periodo más reciente).

### 3.6 Biblioteca histórico SAP (solo lectura) — `/api/biblioteca-sap-historico`

#### `GET /api/biblioteca-sap-historico`
- **Roles**: Todos. Rate limit estándar. **No pagina.**

| Query | | Tipo | Restricción |
|---|---|---|---|
| `q` | *opcional* | string | código DIVIPOLA origen/destino o detalle |
| `lineaNegocio` | *opcional* | **int** | código numérico (de `/filtros`) |
| `tipoVehiculo`, `tipoCarroseria` | *opcional* | string | código SAP (de `/filtros`) |
| `claseViaje` | *opcional* | **int** | código numérico (de `/filtros`) |
| `top` | *opcional* | int | 1–500, def. 50 |

- **→** `200 SapHistoricoResultDto`

#### `GET /api/biblioteca-sap-historico/filtros`
- **Roles**: Todos. **→** `200 SapHistoricoFiltrosDto`

### 3.7 Biblioteca plaza SAP — `/api/biblioteca-sap-plaza`

#### `GET /api/biblioteca-sap-plaza`
- **Roles**: Todos. Rate limit estricto.
- **Query**: los mismos que el histórico (§3.6) + `page` *opcional* (≥ 1, def. 1).
- **→** `200 SapHistoricoResultDto`

#### `GET /api/biblioteca-sap-plaza/filtros`
- **Roles**: Todos. **→** `200 SapHistoricoFiltrosDto`

#### Ajuste local de vigencia (no toca SAP)
**Roles de los tres**: `AdminTarifas`, `CoordinadorFletes`, `Admin`.

##### `PATCH /api/biblioteca-sap-plaza/{tarifaId}/vigencia`
`tarifaId` es **entero**.

| Body | | Tipo | Restricción |
|---|---|---|---|
| `nuevaVigencia` | **requerido** | fecha ISO | ej. `"2026-12-31"` |
| `motivo` | *opcional* | string | máx. 300 |

- **→** `204`. No valida que `tarifaId` exista en SAP; un id inexistente deja
  un ajuste huérfano.
- **Errores**: `400 FECHA_REQUERIDA`

##### `POST /api/biblioteca-sap-plaza/vigencia/masivo`

| Body | | Tipo | Restricción |
|---|---|---|---|
| `tarifaIds` | **requerido** | int[] | mínimo 1 |
| `modo` | **requerido** | string | `fecha` \| `extender` |
| `nuevaVigencia` | condicional | fecha ISO | **requerido si `modo = fecha`** |
| `cantidad` | condicional | int | **requerido y > 0 si `modo = extender`** |
| `unidad` | condicional | string | `dias` \| `meses`. **Requerido si `modo = extender`** |
| `motivo` | *opcional* | string | máx. 300 |

- **→** `200 AjusteVigenciaResultDto`
- **Errores**: `400 FECHA_REQUERIDA` · `400 CANTIDAD_INVALIDA` · `400 UNIDAD_REQUERIDA`

##### `DELETE /api/biblioteca-sap-plaza/{tarifaId}/vigencia`
Quita el ajuste; la tarifa vuelve a regirse por la vigencia de SAP.

- **→** `204`
- **Errores**: `404 AJUSTE_VIGENCIA_NO_ENCONTRADO`

### 3.8 Reglas Solo-SICETAC — `/api/reglas-solo-sicetac`

**Todo el controller**: `AdminTarifas`, `Admin`.

#### `GET /api/reglas-solo-sicetac`
- Rate limit estándar. **→** `200 ReglaSoloSicetacDto[]`

#### `POST /api/reglas-solo-sicetac`

| Body | | Tipo | Restricción |
|---|---|---|---|
| `origen` | **requerido** | string | máx. 150 |
| `destino` | **requerido** | string | máx. 150 |
| `tipoVehiculo` | **requerido** | string | máx. **40** |

- **→** `201 ReglaSoloSicetacDto`
- **Errores**: `400 REGLA_INVALIDA` (algún campo queda vacío tras normalizar) · `409 REGLA_DUPLICADA`

#### `DELETE /api/reglas-solo-sicetac/{id}`
- **→** `204`
- **Errores**: `404 REGLA_NO_ENCONTRADA`

#### `POST /api/reglas-solo-sicetac/importar-excel`
Columnas: Origen, Destino, Tipo de vehículo.

- **Body**: `multipart/form-data`, campo `archivo` **requerido** (§1.9).
- **→** `200 ImportarReglasResultDto`
- **Errores**: `400 ARCHIVO_REQUERIDO` · `400 FORMATO_NO_SOPORTADO` · `400 EXCEL_INVALIDO` · `400 EXCEL_SIN_DATOS`

### 3.9 Servicios conexos — `/api/servicios-conexos`

#### `GET /api/servicios-conexos`
- **Roles**: Todos.

| Query | | Tipo | Restricción |
|---|---|---|---|
| `soloActivos` | *opcional* | boolean | def. `false` |
| `q` | *opcional* | string | búsqueda por nombre |
| `categoria` | *opcional* | string | `logistico` \| `adicional` |
| `page` | *opcional* | int | ≥ 1, def. 1 |
| `pageSize` | *opcional* | int | 1–200, def. 50 |

- **→** `200 Pagina<ServicioConexoDto>`

#### `POST /api/servicios-conexos`
- **Roles**: `ServiciosLogisticos`, `AdminTarifas`, `Admin`.

| Body | | Tipo | Restricción |
|---|---|---|---|
| `nombre` | **requerido** | string | máx. 120 |
| `categoria` | *opcional* | string | `logistico` \| `adicional`, def. `logistico`; máx. 20 |
| `reglaCobro` | *opcional* | string | JSON serializado como texto, ej. `"{\"tipo\":\"por_hora\",\"valor\":45000}"` |
| `valorBase` | *opcional* | decimal | monetario |
| `vigenteHasta` | *opcional* | fecha | solo fecha `"2026-12-31"` |
| `activo` | *opcional* | boolean | def. `true` |

- **→** `201 ServicioConexoDto`
- **Errores**: `400 CATEGORIA_INVALIDA` · `409 SERVICIO_DUPLICADO` (ya hay uno activo con el mismo nombre)

#### `PATCH /api/servicios-conexos/{id}`
Actualización parcial: solo cambian los campos enviados y no nulos.

- **Roles**: `ServiciosLogisticos`, `AdminTarifas`, `Admin`.
- **Body**: los mismos campos que el POST, todos *opcionales*, mismas restricciones.
- **→** `200 ServicioConexoDto`
- **Errores**: `400 CATEGORIA_INVALIDA` · `404 SERVICIO_NO_ENCONTRADO`

#### `POST /api/servicios-conexos/importar-excel`
Columnas: Nombre, Valor base, Vigente hasta, Categoría, Activo.

- **Roles**: `ServiciosLogisticos`, `AdminTarifas`, `Admin`.
- **Body**: `multipart/form-data`, campo `archivo` **requerido** (§1.9).
- **→** `200 ImportarServiciosResultDto`
- **Errores**: `400 ARCHIVO_REQUERIDO` · `400 FORMATO_NO_SOPORTADO` · `400 EXCEL_SIN_DATOS`

### 3.10 Tarifas Ganadas (biblioteca propia) — `/api/tarifas-historicas`

#### `GET /api/tarifas-historicas`
- **Roles**: Todos. Rate limit estándar.

| Query | | Tipo | Restricción |
|---|---|---|---|
| `ruta` | *opcional* | UUID | activa la rama "sugerencias" |
| `vehiculo` | *opcional* | UUID | activa la rama "sugerencias" |
| `q`, `mercancia`, `vigencia` | *opcional* | string | activan la rama "búsqueda" |
| `origen` | *opcional* | string | `ganada` \| `legado`; activa la rama "búsqueda" |
| `page` | *opcional* | int | ≥ 1, def. 1 (solo rama "búsqueda") |
| `pageSize` | *opcional* | int | 1–200, def. 50 (solo rama "búsqueda") |

**El tipo de respuesta cambia según la rama:**

| Rama | Condición | Respuesta |
|---|---|---|
| Sugerencias | viene `ruta` o `vehiculo` | `200 BibliotecaItemDto[]` (las 10 más recientes) |
| Búsqueda | viene `q`, `mercancia`, `vigencia` u `origen` (y no `ruta`/`vehiculo`) | `200 Pagina<BibliotecaItemDto>` |
| Recientes | sin filtros | `200 BibliotecaItemDto[]` |

### 3.11 Tarifas SICE-TAC (caché local) — `/api/tarifas-sicetac`

#### `GET /api/tarifas-sicetac`
- **Roles**: Todos.
- **Query**, todos *opcionales*: `origen`, `destino`, `config`, `tipoCarga`,
  `unidad`, `condicion` (string) y `periodo` (int).
- **→** `200 TarifaSicetacDto[]`

#### `POST /api/tarifas-sicetac/importar`
Upsert idempotente del Excel SICE-TAC.

- **Roles**: `CoordinadorFletes`, `Admin`.
- **Body**: `multipart/form-data`, campo `archivo` **requerido** (§1.9).
- **→** `200 ImportResumenDto`
- **Errores**: `400 ARCHIVO_REQUERIDO` · `400 FORMATO_NO_SOPORTADO` · `400 EXCEL_INVALIDO` · `400 EXCEL_VACIO` · `400 EXCEL_COLUMNAS`

#### `GET /api/tarifas-sicetac/importaciones`
- **Roles**: Todos. **→** `200 ImportacionDto[]`

### 3.12 Métricas — `/api/metricas`

**Todo el controller**: `LiderProceso`, `AdminTarifas`, `Admin`.

#### `GET /api/metricas/cumplimiento`
- **→** `200 CumplimientoDto`

#### `GET /api/metricas/kpis-piloto`
Los 4 KPIs del piloto: tiempo de costeo del pliego, % de líneas auto-costeadas,
cotizaciones emitidas y líneas con flete manual.

- **Query**: `desde`, `hasta` *opcionales* (fecha ISO). Sin filtro, corre sobre
  todo el histórico.
- **→** `200 KpisPilotoDto`

### 3.13 Priorización — `/api/priorizacion/parametros`

**Todo el controller**: solo `Admin`. Cambiar esto reordena la bandeja de todo
el mundo (ver
[README §5.1](docs/contrato-integracion/README.md#51-recalibrar-la-priorización)).

#### `GET /api/priorizacion/parametros`
- **→** `200 ParametrosScoreDto`

#### `PUT /api/priorizacion/parametros`
- **Body**: `ParametrosScoreDto` completo; **los 10 campos son obligatorios en
  la práctica** (un campo omitido llega como `0` y casi siempre rompe alguna
  regla).
- Reglas: cada peso entre 0 y 1; los 4 pesos suman 1.0 (±0.001);
  `horizonteHoras > 0`; `valorReferencia > 0`; `puntajeLicitacion`,
  `puntajeSpot`, `valorPorDefectoSinValor` y `probabilidadPorDefecto` entre 0 y
  100.
- **→** `200 ParametrosScoreDto`. No espera el recálculo (corre aparte).
- **Errores**: `400 PARAMETROS_SCORE_INVALIDOS` (el `message` lista todos los motivos)

### 3.14 Cubicación rápida — `/api/cubicacion`

#### `POST /api/cubicacion`
Cubicación sin persistencia: matrices por unidad y sugerencia de
vehículo/flota. Es la única llamada HTTP síncrona de NEXO hacia el Cubicador.

- **Roles**: `Comercial`, `CoordinadorFletes`, `Admin`.

| Body | | Tipo | Restricción |
|---|---|---|---|
| `items` | **requerido** | array | mínimo 1 |
| `unidadIds` | *opcional* | string[] | limitar la evaluación a esas unidades |
| `claseViaje` | *opcional* | string | |

| Cada item | | Tipo | Restricción |
|---|---|---|---|
| `cantidad` | **requerido** | int | > 0 (lo valida el Cubicador) |
| `largoCm`, `anchoCm`, `altoCm` | **requerido** | decimal | > 0 (lo valida el Cubicador) |
| `descripcion` | *opcional* | string | |
| `pesoKg` | *opcional* | decimal | |
| `tipoMercancia` | *opcional* | string | |

- **→** `200 CubicacionRapidaResultado`
- **Errores**: `400 ITEMS_REQUERIDOS` · `503 CUBICACION_RAPIDA_NO_CONFIGURADA` · `503 CUBICADOR_NO_DISPONIBLE` · `503 CUBICADOR_RESPUESTA_VACIA` · `503 CUBICADOR_CREDENCIAL_INVALIDA` · `503 CUBICADOR_ERROR`

#### `GET /api/lineas-solicitud/{id}/cubicacion` — solo servicio a servicio
- **Auth**: `Authorization: Bearer <clave de servicio>` (§1.2). No acepta la
  identidad del portal. **No es para el frontend.**
- **→** `200 LineaParaCubicacionDto`
- **Errores**: `401 NO_AUTORIZADO` · `404 LINEA_NO_ENCONTRADA`

### 3.15 Plan de cargue (cubicación 3D) — `/api/planes-cargue`

Todas las escrituras van por cola hacia el Cubicador y responden **202**. Todas
las lecturas van contra la proyección local de NEXO; ninguna llama al Cubicador.

- **Roles de escritura**: `Comercial`, `CoordinadorFletes`, `Admin`.
- **Roles de lectura**: Todos.

#### `POST /api/planes-cargue` — crear plan

| Body | | Tipo | Restricción |
|---|---|---|---|
| `nombre` | **requerido** | string | no vacío |
| `vehiculoId` | **requerido** | UUID | vehículo del catálogo |

- **→** `202 SolicitudDeCubicacionAceptadaDto`, header `Location` = `consultarEn`.
  La fila de NEXO queda creada en estado `procesando` hasta que confirme el
  Cubicador.
- **Errores**: `400 NOMBRE_REQUERIDO`

#### `POST /api/planes-cargue/demo`
Escenario de demostración; el Cubicador elige el vehículo. Sin body.

- **→** `202 ComandoAceptadoDto`. No hay `planCargueId` todavía: el plan
  resultante se sigue por `/hubs/plan-cargue`.

#### `POST /api/planes-cargue/desde-linea/{lineaId}`
Puente Mesa de Costeo → 3D: el Cubicador arma la flota a partir de las
posiciones de la línea. Sin body. No pasa por la compuerta de paridad.

- **→** `202 ComandoAceptadoDto` (`correlationId` = `lineaId`). Sondear con
  `GET /api/planes-cargue/por-linea/{lineaId}`.
- **Errores**: `404 LINEA_SOLICITUD_NO_ENCONTRADA`

#### `POST /api/planes-cargue/{id}/optimizar`
- **Body** *opcional* (se puede omitir entero):

| Campo | | Tipo |
|---|---|---|
| `estrategia` | *opcional* | string |
| `algoritmo` | *opcional* | string |
| `semilla` | *opcional* | int |
| `respetarBloqueadas` | *opcional* | boolean |

- **→** `202 SolicitudDeCubicacionAceptadaDto`
- **Errores**: `404 PLAN_CARGUE_NO_ENCONTRADO` · `409 PLAN_CARGUE_EN_PROCESO` · `503 CUBICACION_EN_VERIFICACION` (compuerta de paridad cerrada)

#### `POST /api/planes-cargue/{id}/items` — alta o edición de un ítem
`id` nulo o ausente en el body = alta.

| Body | | Tipo | Restricción |
|---|---|---|---|
| `descripcion` | **requerido** | string | no vacío |
| `largoMm`, `anchoMm`, `altoMm` | ver nota | decimal | milímetros |
| `pesoKg` | ver nota | decimal | |
| `cantidad` | ver nota | int | |
| `secuenciaEntrega`, `prioridad`, `maxNiveles` | ver nota | int | |
| `maxPesoEncimaKg` | ver nota | decimal | |
| `apilable`, `rotable`, `mantenerVertical`, `fragil`, `peligroso`, `refrigerado` | ver nota | boolean | |
| `id` | *opcional* | UUID | ítem a editar |
| `sku`, `pedido`, `destino`, `color`, `tipoEmbalaje`, `grupoCompatibilidad`, `observaciones` | *opcional* | string | |
| `clsCliente` | *opcional* | string | ⚠️ así se llama hoy el campo (artefacto del renombrado "cls"); se esperaría `cliente` |

Nota: los campos numéricos y booleanos no son anulables. Si se omiten llegan
como `0` o `false`, y el Cubicador puede rechazar el ítem (el rechazo llega por
`/hubs/plan-cargue`). Envíalos siempre.

- **→** `202 ComandoAceptadoDto`
- **Errores**: `400 DESCRIPCION_REQUERIDA` · `404 PLAN_CARGUE_NO_ENCONTRADO`

#### `POST /api/planes-cargue/{id}/items/importar`
- **Body**: `{ items: Item[] }` **requerido**, entre 1 y 2.000 ítems, con el
  mismo shape del endpoint anterior (`id` se ignora).
- **→** `202 ComandoAceptadoDto`
- **Errores**: `400 IMPORTACION_INVALIDA` · `404 PLAN_CARGUE_NO_ENCONTRADO`

#### `DELETE /api/planes-cargue/{id}/items/{itemId}`
- **→** `202 ComandoAceptadoDto`
- **Errores**: `404 PLAN_CARGUE_NO_ENCONTRADO`

#### `DELETE /api/planes-cargue/{id}`
- **→** `202 ComandoAceptadoDto` (no `204`: NEXO borra su fila de inmediato,
  pero el borrado en el Cubicador es asíncrono).
- **Errores**: `404 PLAN_CARGUE_NO_ENCONTRADO`

#### `PATCH /api/planes-cargue/{id}/colocaciones/{colocacionId}` — ajuste manual
Todos los campos *opcionales*: `x`, `y`, `z` (decimal), `orientacionIndice`
(int), `bloqueada`, `retirar`, `reinsertar` (boolean).

- **→** `202 ComandoAceptadoDto`. Si el Cubicador lo rechaza, llega un aviso de
  rechazo por `/hubs/plan-cargue`.
- **Errores**: `404 PLAN_CARGUE_NO_ENCONTRADO`

#### Lecturas (Todos los roles)

| Endpoint | Query | Respuesta | Errores |
|---|---|---|---|
| `GET /api/planes-cargue` | `page` (≥1, def. 1), `pageSize` (1–200, def. 50) | `200 Pagina<PlanCargueDto>` | — (rate limit estándar) |
| `GET /api/planes-cargue/{id}` | — | `200 PlanCargueDto` | `404 PLAN_CARGUE_NO_ENCONTRADO` |
| `GET /api/planes-cargue/por-linea/{lineaId}` | — | `200 PlanFlotaItemDto[]` (vacío mientras el Cubicador procesa; nunca 404) | — |
| `GET /api/planes-cargue/{id}/resultado` | — | `200 ResultadoPlanDto` (ítems, colocaciones, métricas, incidencias) | `404 PLAN_CARGUE_NO_ENCONTRADO` · `404 VEHICULO_NO_ENCONTRADO` |
| `GET /api/planes-cargue/{id}/flota` | — | `200 PlanFlotaItemDto[]` | — |
| `GET /api/planes-cargue/{id}/metricas` | — | `200 MetricasCubicacion` o `200 null` si nunca se optimizó | `404 PLAN_CARGUE_NO_ENCONTRADO` |
| `GET /api/planes-cargue/{id}/no-cargadas` | — | `200 ColocacionCargueDto[]` (solo rechazadas) | `404 PLAN_CARGUE_NO_ENCONTRADO` |
| `GET /api/planes-cargue/{id}/colocaciones` | — | `200 ColocacionCargueDto[]` (cargadas y rechazadas) | `404 PLAN_CARGUE_NO_ENCONTRADO` |
| `GET /api/planes-cargue/{id}/exportar` | — | `200 ResultadoPlanDto` (hoy igual a `/resultado`) | `404 PLAN_CARGUE_NO_ENCONTRADO` |

⚠️ Limitación conocida de `por-linea`: hoy el worker del Cubicador solo reporta
el primer vehículo de una flota multi-vehículo.

### 3.16 Catálogo de vehículos (Cubicador) — `/api/vehiculos`

#### `GET /api/vehiculos`
- **Roles**: Todos. Rate limit estándar.
- **Query**: `page` (≥ 1, def. 1), `pageSize` (1–200, def. 50).
- **→** `200 Pagina<VehiculoCubicacionDto>`

#### `POST /api/vehiculos` — alta/edición (por cola)
- **Roles**: `Comercial`, `CoordinadorFletes`, `Admin`.

| Body | | Tipo | Restricción |
|---|---|---|---|
| `nombre` | **requerido** | string | no vacío |
| `puertaPosicion` | **requerido** | string | `trasera` \| `frontal` \| `costado_izquierdo` \| `costado_derecho` |
| `ejes` | **requerido** | array | `[{ posicionMm, capacidadKg }]` |
| `largoInternoMm`, `anchoInternoMm`, `altoInternoMm` | ver nota | decimal | milímetros |
| `capacidadKg`, `taraKg` | ver nota | decimal | |
| `puertaAnchoMm`, `puertaAltoMm` | ver nota | decimal | |
| `descripcion` | *opcional* | string | |
| `toleranciaParedesMm`, `toleranciaEntreUnidadesMm` | *opcional* | decimal | def. 0 |

Nota: las dimensiones no son anulables; si se omiten llegan como `0` y el
Cubicador rechaza el vehículo. Envíalas siempre.

- **→** `202 ComandoAceptadoDto`
- **Errores**: `400 NOMBRE_REQUERIDO`

#### `DELETE /api/vehiculos/{id}`
- **Roles**: solo `Admin`.
- **→** `202 ComandoAceptadoDto`
- **Errores**: `409 VEHICULO_EN_USO` (algún plan de cargue lo referencia)

### 3.17 Trabajos — `/api/trabajos`

#### `GET /api/trabajos/{id}`
- **Roles**: Todos.
- **→** `200 EstadoTrabajoDto`
- **Errores**: `404 TRABAJO_NO_ENCONTRADO`

### 3.18 Salud — `/health`

#### `GET /health`
- **Auth**: ninguna (anónimo).
- **→** `200` / `503` `{ status, duracion, chequeos: { [nombre]: { status, descripcion, error } } }`

---

## 4. Hubs de SignalR — conexión

```js
const conn = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/trabajos", { headers: { "X-Portal-clsUsuario-Id": id } }) // ver discrepancia en §1.1
  .build();
conn.on("avance", (aviso) => { /* ... */ });
await conn.start();
await conn.invoke("Suscribir", trabajoId);
// al salir de la pantalla:
await conn.invoke("Desuscribir", trabajoId);
```

Mismo patrón para `/hubs/bandeja` (sin parámetro, evento `"bandeja"`) y
`/hubs/plan-cargue` (con `planCargueId`, evento `"avance"`). Por qué son 3 hubs
separados: [README §5](docs/contrato-integracion/README.md#5-avisos-en-vivo).

---

## 5. Matriz de permisos

✅ = permitido. Fuente: los atributos `[Authorize(Roles=...)]` de
`src/Nexo.Api/*/cls*Controller.cs`. Los `GET` que no aparecen aquí están
abiertos a los 6 roles.

| Operación | Com | Coord | AdmTar | ServLog | Líder | Admin |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| `POST /api/captura-solicitudes/*` y sus `GET` (aprovisionamiento, clientes) | ✅ | | | | | ✅ |
| `POST /api/solicitudes` | | ✅ | | | | ✅ |
| `POST /api/solicitudes/resolver-catalogos` | ✅ | ✅ | | | | ✅ |
| `POST /api/solicitudes/{id}/lineas:bulk` | | ✅ | | | | ✅ |
| `POST /api/solicitudes/{id}/rechazar` | ✅ | ✅ | | | | ✅ |
| `POST /api/solicitudes/{id}/recalcular-score` | | ✅ | | | | ✅ |
| `POST /api/cotizaciones` · `aplicar-propuestas` · `resultado` | ✅ | ✅ | ✅ | | | ✅ |
| `POST /api/cotizaciones/{id}/lineas` · `entregar` | | ✅ | ✅ | | | ✅ |
| `POST /api/cotizaciones/{id}/conexos` | ✅ | ✅ | | | | ✅ |
| Vigencia plaza SAP (`PATCH`, `POST masivo`, `DELETE`) | | ✅ | ✅ | | | ✅ |
| `/api/reglas-solo-sicetac` (todo, incluido `GET`) | | | ✅ | | | ✅ |
| `POST/PATCH /api/servicios-conexos`, `importar-excel` | | | ✅ | ✅ | | ✅ |
| `POST /api/tarifas-sicetac/importar` | | ✅ | | | | ✅ |
| `/api/metricas/*` | | | ✅ | | ✅ | ✅ |
| `/api/priorizacion/parametros` (`GET` y `PUT`) | | | | | | ✅ |
| `POST /api/cubicacion` | ✅ | ✅ | | | | ✅ |
| Escrituras de `/api/planes-cargue` | ✅ | ✅ | | | | ✅ |
| `POST /api/vehiculos` | ✅ | ✅ | | | | ✅ |
| `DELETE /api/vehiculos/{id}` | | | | | | ✅ |

---

## 6. Enums

Viajan como el string exacto (ver [§1.6](#16-enums-se-mandan-y-devuelven-como-texto)).

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

`src/Nexo.Domain/Enums/Enums.cs` es la fuente de la verdad; si este apéndice y
el código difieren, gana el código.

---

## 7. Discrepancias conocidas entre código y contrato

Pendientes de decisión. Mientras sigan abiertas, este documento describe el
comportamiento real del código.

| # | Dónde | Hoy (código) | Esperado (contrato/docs) | Impacto |
|---|---|---|---|---|
| 1 | Header de identidad | `X-Portal-clsUsuario-Id` | `X-Portal-Usuario-Id` | Frontend, e2e y scripts de QA reciben `401` en todo |
| 2 | `CatalogoLineaResueltoDto` | `clsVehiculoCreado` | `vehiculoCreado` | Nombre de campo JSON en `resolver-catalogos` |
| 3 | Ítem de plan de cargue | `clsCliente` | `cliente` | Nombre de campo JSON en `items` e `items/importar` |
| 4 | `importar-excel` de reglas y servicios | sin archivo → `400 VALIDATION_ERROR` | `400 ARCHIVO_REQUERIDO` | Código de error distinto al de los otros uploads |

Los tres primeros parecen efecto del renombrado masivo con prefijo `cls`.
