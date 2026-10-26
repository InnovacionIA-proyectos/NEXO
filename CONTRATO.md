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

`Comercial · AdminBiblioteca · AdminTarifas · ServiciosLogisticos · LiderProceso · Admin`

Un usuario tiene exactamente uno. `Admin` está incluido en todos los endpoints
restringidos. La matriz completa está en la [sección 5](#5-matriz-de-permisos) y
en [`docs/documentacion/ROLES_Y_PERMISOS.md`](docs/documentacion/ROLES_Y_PERMISOS.md).

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
| `429` | `IA_TOPE_DIARIO` | La persona ya consumió su tope diario de IA (§3.22) |
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
| `GET /api/biblioteca-sap-historico` | `page`, `top` (#312) | 1, 50 | `top` 500 |
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
  notas: string | null                           // observaciones comerciales (#227)
}
interface BandejaItemDto {                       // fila de GET /api/solicitudes
  id: UUID; cliente: string; tipo: string; estado: string
  deadline: FechaISO | null; valorEstimado: Decimal | null; scorePrioridad: Decimal
  lineas: number; fleteTotal: Decimal | null; lineasSinFlete: number
  fechaCaptura: FechaISO
  atencion: "pendiente" | "automatico" | "administrador" | string
  vigencia: "vigente" | "vencida"                // S5-HU-23.1/#289: por Fecha Límite (deadline), no por antigüedad de la captura; sin deadline = "vigente"
  resultado: "SinDefinir" | "Ganada" | "Perdida" | null // S5-HU-23.1/#289: de la cotización; null si no hay. Con varias: la que tenga resultado distinto de SinDefinir (la más reciente), si no la más reciente
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
  codigoTipoMercancia: string | null             // S5-HU-18.2/#274: código (no uuid) de tbl_tipos_mercancia; null = tipo desconocido, sin default
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
  posiciones: PosicionCargaDto[]
  pisoSicetac: PisoSicetacDto | null // #300: null también cuando el período más reciente cargado en DWSice NO es el del mes actual (ver avisoSicetac)
  historicas: HistoricaSugeridaDto[]
  horasCargue: Decimal | null; horasDescargue: Decimal | null   // #251, horas EFECTIVAS con las que se costeó
  grupoRutaId: UUID | null         // S5-HU-18.3/#275: agrupa las líneas de una misma carga heterogénea.
                                   // Lo pone QUIEN CREA las líneas del grupo (Cotifly o el portal); NEXO lo guarda tal cual.
  idRecomendacionVehiculo: string | null  // S5-HU-18.2/#274: recomendación del Cubicador usada para esta línea
  valorMercancia: Decimal | null   // S5-HU-20.1/#286: lo capturado en la línea (clsLineaSolicitud.ValorMercancia); null si no se capturó
  tieneReglaSoloSicetac: boolean   // S6-HU-26.1/#30: la ruta (por DANE) y el vehículo de la línea tienen regla Solo-SICE → su flete NO se puede editar (PUT responde 409 FLETE_SOLO_SICETAC): no ofrecer "Editar flete"
  // ---- campos nuevos, AL FINAL y opcionales (no rompen a quien ya lee el DTO) ----
  estadoLinea: "porCostear" | "autoResuelta" | "revisada" | "rechazada" | null  // S6-HU-24.1/#309 (minúscula inicial); "rechazada" desde S6-HU-24.3/#320
  motivoPendiente: string | null    // S6-HU-24.1/#309: por qué sigue en porCostear (consulta completa, línea sin vehículo, SICE-TAC que no es del mes actual...); null si no está pendiente
  lineaNegocio: string | null       // S6-HU-29.1/#311: "puertoAInterior" | "interiorAPuerto" | "nacional" | "devolucionContenedor" | "andino" | "licitaciones" (minúscula inicial); null si la línea no la trae
  claseViaje: string | null         // S6-HU-29.1/#311: "contenedor" | "cupo" | "cargaSuelta" | "extradimensionado"; null si no
  claseContenedor: string | null    // S6-HU-29.1/#311: tal como se capturó (CNT20.IT...). NULL salvo que claseViaje sea "contenedor"
  origenMunicipioId: number | null  // S6-HU-29.1/#311: id de tbl_municipios (el de GET /ciudades); null si la ruta no resolvió municipio
  destinoMunicipioId: number | null
  motivoRechazo: string | null      // S6-HU-24.3/#320: por qué se rechazó la línea (máx. 500); null si no está rechazada. Una línea rechazada NO tiene flete, no suma al total y no cuenta como pendiente
  numeroLinea: number | null       // S5-HU-20.5/#297: orden de la línea en el arreglo de la captura (1, 2, 3...); null en líneas capturadas antes de S5-HU-18.3. El GET ya las devuelve ORDENADAS por este campo
}
interface TiemposLogisticosDto { horasCargue: Decimal; horasDescargue: Decimal }
interface SolicitudCosteoDto {
  solicitud: SolicitudDto; tiempos: TiemposLogisticosDto; lineas: LineaCosteoDto[]
  // #300: aviso visible cuando alguna línea quedó sin piso porque el período SICE-TAC más reciente cargado NO es el del mes y año
  // actuales (p. ej. "SICE-TAC no se usa: el período más reciente cargado es 202609 y el del mes actual es 202610 (sin mes de gracia)."). null = el piso se usa con normalidad
  avisoSicetac: string | null
}
interface CapturaResultDto {
  id: UUID; lineas: number; rutasCreadas: number; vehiculosCreados: number
  scorePrioridad: Decimal; fletesCalculados: number; lineasPorCostear: number; costeoEnProceso: boolean
}
interface HojaExcelDto { nombre: string; filaEncabezado: number; encabezados: string[]; filas: string[][]; totalFilas: number }
interface AnalisisExcelDto { archivo: string; hojas: HojaExcelDto[] }
interface ClienteBusquedaDto { clienteId: UUID | null; bp: string | null; nit: string; nitCliente: string; nombre: string; origen: string } // origen: "cliente" | "prospecto"; nitCliente es alias de nit (mismo valor) para Cotifly
interface MunicipioDto { // catálogo completo, ~3.800 filas — id numérico, el mismo que usa TarifaPlazaDto.origenMunicipioId/destinoMunicipioId
  id: number; codigoDane: string; municipio: string; departamento: string; pais: string
  nombreTarifasPlaza: string | null; zonaTarificacion: string | null
}
interface CandidatoTarifaDto {
  fuente: string; valor: Decimal; detalle: string; criterio: string; coincidencias: number
  advertencia: string | null // no-nulo cuando NO es un match exacto — mostrar al usuario antes de confirmar
  tablaOrigen: "SAP" | "NEXO" // S5-HU-19.1/#281: "SAP" = tabla externa (tblSAPTarifas/tblSAPTarifasLog/DWSice), "NEXO" = tabla propia (tbl_tarifas_plaza/tbl_tarifas_plaza_historico)
  // S5-HU-20.1/#286: SOLO el candidato `sicetac` los trae (plaza e histórico vienen null). Invariante: valorPisoBase + valorTiemposLogisticos == valor.
  // Se calculan en el servidor con las horas REALES de la línea; el front no debe reconstruirlos (la tabla "Tarifas SICE-TAC" es catálogo crudo, con horas por defecto).
  valorPisoBase: Decimal | null          // el piso de la fila de DWSice, sin horas logísticas
  valorTiemposLogisticos: Decimal | null // lo que suman las horas logísticas (valor − piso base; sirve también con el valorSiceConHora de respaldo)
  configuracion: string | null           // #300: configuración SICE-TAC de la fila (2L2, C2, 3S3...) — solo el candidato `sicetac` (y los de tramo vacío) la traen
  vehiculoRelajado: boolean              // true = la línea pide un vehículo y la fuente no tiene tarifas de ese vehículo en la ruta: el valor es de otro vehículo. Se muestra como referencia (con `advertencia`), no entra al recomendado ni puede gobernar solo
}
interface AprovisionamientoDto { candidato: CandidatoTarifaDto | null }

interface CotizacionLineaDto {
  id: UUID; lineaSolicitudId: UUID | null; valorFlete: Decimal; fuente: FuenteFlete
  valorAprovisionamiento: Decimal | null; valorRetornoContenedor: Decimal | null
}
interface CotizacionConexoDto {
  id: UUID; cotizacionLineaId: UUID; servicioConexoId: UUID | null; nombre: string | null; categoria: string
  cantidad: Decimal; valorAplicado: Decimal
  origen: "individual" | "general" // S5-HU-18/#262: cómo se agregó — "general" = el usuario pidió agregarlo a todas las líneas de una vez (igual crea una fila por línea)
  servicio: ServicioConexoDto | null // S5-HU-18/#262: el servicio completo (dimensiones incluidas), para no tener que bajar el catálogo completo
}
interface CotizacionDto {
  id: UUID; solicitudId: UUID; estado: "Borrador" | "Entregada"
  resultado: "SinDefinir" | "Ganada" | "Perdida"; total: Decimal; entregadaAt: FechaISO | null
  lineas: CotizacionLineaDto[]; conexos: CotizacionConexoDto[]
  esAutoCosteo: boolean
  // S5-HU-20.1/#286: última vez que se agregó/quitó un conexo DESPUÉS de entregar (null si nunca). NO mueve estado ni entregadaAt: el SLA y las métricas siguen midiendo la entrega original.
  modificadaTrasEntregaAt: FechaISO | null
}
interface AplicarPropuestasResultDto { cotizacion: CotizacionDto; agregadas: number }
interface EntregarCotizacionResultDto { cotizacion: CotizacionDto; estadoNotificacion: string; mensaje: string }
interface PropuestaLineaDto {
  lineaId: UUID; ruta: string | null; vehiculo: string | null; peso: Decimal | null
  sicetac: CandidatoTarifaDto | null; plaza: CandidatoTarifaDto | null; historicoSap: CandidatoTarifaDto | null
  valorPropuesto: Decimal | null; fuentePropuesta: string | null; justificacion: string
  retornoContenedor: CandidatoTarifaDto | null // piso SICE-TAC de VIAJE VACÍO (destino → ciudad de devolución), 1h+1h, misma configuración y misma unidad de transporte (carrocería) que la línea; fuente "retorno_contenedor"
  aprovisionamientoContenedor: CandidatoTarifaDto | null // igual, del levante (ciudad de levante → origen de la línea); fuente "aprovisionamiento_contenedor"
  modoDecision: "SoloSicetac" | "PlazaVigenteMayor" | "ConsultaCompleta" // camino de decisión del flete principal: Solo SICE-TAC, plaza vigente mayor o proceso completo (ver FLUJO-DECISION-TARIFA.md)
  autocosteable: boolean // true = hay valorPropuesto y ningún motivoPendiente: el flete se guarda solo (en cualquiera de los tres caminos). false = la línea queda pendiente
  motivoPendiente: string | null // motivo con el que la línea NO se guarda sola aunque haya valor: "Línea sin vehículo: requiere consulta completa", "SICE-TAC no se usa: el período más reciente cargado es AAAAMM y el del mes actual es AAAAMM (sin mes de gracia)...", o "No se encontró piso SICE-TAC de viaje vacío para el retorno/levante del contenedor...". null = sin motivo
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

interface ReglaSoloSicetacDto {
  id: UUID; origen: string; destino: string
  origenMunicipio: string | null; destinoMunicipio: string | null // nombre resuelto para mostrar — null si origen/destino no matchea ningún código DANE conocido
  tipoVehiculo: string; createdAt: FechaISO
}
interface ImportarReglasResultDto { filas: number; creadas: number; duplicadas: number; invalidas: number }

interface ServicioConexoDto {
  id: UUID; codigoServicio: string; tipoServicio: string
  origen: string; destino: string; puerto: string; ruta: string; tipoMaquina: string // "NO APLICA" si no aplica a este servicio
  costoUnitario: Decimal; unidad: string; vigencia: Fecha; categoria: string; activo: boolean
}
interface ImportarServiciosResultDto {
  filas: number; creadas: number; actualizadas: number; duplicadas: number; invalidas: number
  errores: string[] // S5-HU-19/#261: detalle "Fila N: motivo" de cada fila inválida, máx. 20
}

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
  desde: FechaISO | null; hasta: FechaISO | null // S5-HU-21.1/#287: eco del filtro (null = sin límite), igual que KpisPilotoDto
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
  tamano: "1-10" | "11-100" | "+100" | null // S5-HU-21.1/#287: eco del corte por tamaño aplicado al tiempo de costeo; null = todos
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
interface EstadoCubicacionLineaDto { // #304
  estado: "procesando" | "listo" | "rechazado"  // rechazado manda sobre una flota vieja: el rechazo es del ÚLTIMO intento
  codigo: string | null; mensaje: string | null   // solo si rechazado: el código y el mensaje del Cubicador tal cual (FLOTA_INSUFICIENTE, VEHICULO_SIN_PRESET, ERROR_INTERNO...)
  rechazadoAt: FechaISO | null
  vehiculos: PlanFlotaItemDto[]                   // lo mismo que devuelve GET por-linea
}
interface EjeVehiculoDto { posicionMm: Decimal; capacidadKg: Decimal }
interface VehiculoCubicacionDto {
  id: UUID; nombre: string; descripcion: string | null
  largoInternoMm: Decimal; anchoInternoMm: Decimal; altoInternoMm: Decimal
  capacidadKg: Decimal; taraKg: Decimal; puertaAnchoMm: Decimal; puertaAltoMm: Decimal; puertaPosicion: string
  ejes: EjeVehiculoDto[]; toleranciaParedesMm: Decimal; toleranciaEntreUnidadesMm: Decimal
  esPreset: boolean
}
interface LineaParaCubicacionDto { lineaId: UUID; posiciones: PosicionCargaDto[]; origen: string | null; destino: string | null }
// posiciones[].codigoTipoMercancia viaja igual que en PosicionCargaDto (arriba) — null en líneas capturadas antes de S5-HU-18.2/#274.

// S5-HU-18.1/#273: espejo 1:1 de POST/GET /recomendaciones del Cubicador
// (cubicador/CONTRATO.md §3.1). Ver §3.14 más abajo para el detalle operativo.
interface MercanciaSolicitadaInput {
  codigoMercancia: string; cantidad: number           // >= 1
  largoCm: Decimal; anchoCm: Decimal; altoCm: Decimal; pesoKg: Decimal   // por unidad, > 0
}
interface CrearRecomendacionInput {
  idLineaSolicitud: string; mercancias: MercanciaSolicitadaInput[]
  claseViajeSeleccionada: string | null
}
interface MercanciaDeVehiculoDto { indicePosicion: number | null; idTipoMercancia: string; codigoMercancia: string; cantidad: number; pesoKg: Decimal; volumenM3: Decimal }
// S5-HU-17.8: UNA FILA POR POSICION del request, no por codigo. Dos filas del mismo
// camion pueden compartir codigoMercancia: quien agrupe por codigo tiene que sumar.
// null al reconsultar una corrida anterior al cambio.
interface VehiculoRecomendadoDto {               // TODOS obligatorios y nunca null cuando resultado = "ASIGNADO"
  idRecomendacionVehiculo: string; numeroVehiculo: number
  codigoVehiculo: string; nombreVehiculo: string; codigoCarroceria: string
  pesoTotalKg: Decimal; volumenTotalM3: Decimal
  porcentajeLlenadoVolumen: Decimal; porcentajeLlenadoPeso: Decimal      // dos porcentajes, no uno
  alertaBajoLlenado: boolean; heterogenea: boolean
  mercancias: MercanciaDeVehiculoDto[]
}
interface MedidasEstandarDto { largoMaxCm: Decimal; anchoMaxCm: Decimal; altoMaxCm: Decimal; pesoMaxKg: Decimal }
interface PiezaFueraDeMedidaDto { indicePosicion: number | null; idTipoMercancia: string; codigoMercancia: string; excesos: string[] }
interface ExtradimensionDto { hayExtradimension: boolean; medidasEstandar: MedidasEstandarDto; piezas: PiezaFueraDeMedidaDto[] }
interface RecomendacionDto {
  idRecomendacion: string; idLineaSolicitud: string; origenSistema: string
  resultado: "ASIGNADO" | "SIN_SELECCION_AUTOMATICA"          // ambos con 200 — no son error
  motivoSinSeleccion: "EXTRADIMENSIONADO" | "SIN_CANDIDATOS_DIMENSIONALES" | "SUPERA_K_MAX"
    | "SIN_CARROCERIA_COMPATIBLE" | "BUSQUEDA_AGOTADA" | null     // null cuando resultado = ASIGNADO
  condicionada: boolean; mercanciasCondicionadas: string[]
  vehiculos: VehiculoRecomendadoDto[]; advertencias: string[]
  extradimension: ExtradimensionDto | null
}
// IDs en MAYÚSCULAS (uniqueidentifier de SQL Server). Las decimales viajan como
// números JSON (no strings): 2 decimales en dinero/volumen, 1 en porcentajes.

interface TipoVehiculoDto { codigo: string; descripcion: string; esCatalogoOficial: boolean } // false = los 5 casos especiales sin código SAP (§3.19)
interface LineaNegocioDto { id: number; descripcion: string }
interface ClaseViajeDto { id: number; descripcion: string }
interface ClaseContenedorDto { id: number; descripcion: string; tamanoContenedor: number | null; pesoInfCnt: number | null; pesoSupCnt: number | null }

interface RangoPesoDto { id: number; pesoMinimo: number; pesoMaximo: number | null } // pesoMaximo null = banda abierta

interface TarifaPlazaDto {
  id: UUID; lineaNegocio: string; claseViaje: string
  origenMunicipioId: number; origen: string; zonaOrigen: string | null
  destinoMunicipioId: number; destino: string; zonaDestino: string | null
  claseContenedor: string | null; pesoId: number | null; rangoPeso: string | null; tipoVehiculo: string | null
  valor: Decimal; vigencia: Fecha; detallesTarifa: string; createdAt: FechaISO
}
interface ImportarTarifasPlazaResultDto { procesadas: number; insertadas: number; actualizadas: number; rechazadas: number; errores: string[] } // errores: tope 20 mensajes
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
| `atencion` | *opcional* | string | `pendientes` \| `automatico` \| `administrador` \| `atendidas` \| `todas`. Sin filtros → cola de pendientes (ver abajo) |
| `desde`, `hasta` | *opcional* | fecha ISO | rango de fecha de captura |
| `vigencia` | *opcional* | string | `vigente` \| `vencida`. S5-HU-23.1/#289: se mide contra la **Fecha Límite** (`deadline`) vs. "ahora"; una solicitud **sin deadline cuenta como vigente**. Antes: "capturada hace menos de 7 días" |
| `resultado` | *opcional* | string | S5-HU-23.1/#289 (HU-B25). `esperando` \| `ganada` \| `perdida`. Valor desconocido → `400 RESULTADO_INVALIDO` |
| `orderBy` | *opcional* | string | ignorado: siempre ordena por score |
| `page` | *opcional* | int | ≥ 1, def. 1 |
| `pageSize` | *opcional* | int | 1–200, def. 50 |

**La misma consulta sirve a todos los roles.** Para `Comercial` se limita **antes** a las solicitudes
que él mismo creó (aislamiento de #199 / S4-HU-19.1); el resto de los roles ve todo. Los filtros de
abajo operan sobre ese universo, así que un Comercial nunca ve filas de otro.

**`atencion=pendientes` (la cola de trabajo del Administrador de Tarifas)** —
S5-HU-23.1/#289— suma a las solicitudes con alguna línea **sin flete** las que tienen **todas las
líneas con flete y alguna `Revisada`** por el Administrador de Fletes pero que todavía no se
entregan (antes no salían en ninguna cola). No es una categoría nueva: esa solicitud **sigue**
apareciendo en `administrador`/`atendidas`. No aplica a `automatico` (el auto-costeo tiene su
propio atajo, S4-HU-15.6). Con estado `Pendiente`/`EnCosteo` implícito, al pasar a `Entregada`
deja de salir en Pendientes.

**`resultado`** — al marcar Ganada/Perdida la solicitud pasa a `Entregada` y sale de Pendientes;
esta es la forma de seguirla. **No se mezcla en Pendientes.**
- `esperando`: cotización `Entregada` (o auto-costeada en `Borrador`, que nunca se entrega) **sin**
  resultado, y solicitud no `Descartada` — aunque la solicitud esté `Entregada`. Excluye Ganadas,
  Perdidas y descartadas.
- `ganada` / `perdida`: solo esas.
- Con `resultado` informado y **sin `atencion`**, la atención se trata como `todas` (no se aplica la
  restricción por defecto a `Pendiente`/`EnCosteo`). Con `atencion` explícita se respeta y se combina.

- **→** `200 Pagina<BandejaItemDto>`
- **Errores**: `400 RESULTADO_INVALIDO`

#### `GET /api/solicitudes/{id}` — abrir solicitud (costeo pre-armado)
- **Roles**: Todos.
- **→** `200 SolicitudCosteoDto`
- **Orden de `lineas` (S5-HU-20.5/#297)**: estable. Por `numeroLinea` ascendente (las líneas viejas, sin
  número, al final), luego por fecha de creación y luego por `id`. Antes no se ordenaba y el orden podía
  cambiar entre consultas. **NEXO numera por el orden del arreglo** con que se capturó la solicitud
  (`lineas[0]` → 1, `lineas[1]` → 2...): no existe —ni se agrega— un campo "orden" de entrada, y Cotifly no
  manda un número. Varios camiones de la misma ruta (`grupoRutaId`) conservan el orden en que se enviaron.
- **Errores**: `404 SOLICITUD_NO_ENCONTRADA` · `409 SOLICITUD_DESCARTADA`

#### `POST /api/solicitudes` — alta spot (o cabecera de licitación)
- **Roles**: `Admin` (S6-HU-25.1/#307, decisión D5: `AdminBiblioteca` ya no entra).

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
| `horasCargue` | *opcional* | decimal | S5-HU-17/#263. 0–240. Antes se descartaba en silencio |
| `horasDescargue` | *opcional* | decimal | S5-HU-17/#263. 0–240. Antes se descartaba en silencio |
| `grupoRutaId` | *opcional* | UUID | S5-HU-18.3/#275. Lo pone **quien crea las líneas del grupo** (Cotifly, o el portal al crear una línea por camión recomendado) — NEXO solo lo guarda tal cual, nunca lo genera |

Sin horas propias, la línea usa las de la solicitud (`CondicionesServicio`, 4h+4h
si nunca se guardaron condiciones) — mismo criterio que `clsCapturaLineaRequest`
(§3.3, S4-HU-15.5/#251).

#### `POST /api/solicitudes/resolver-catalogos`
Resuelve texto libre a `rutaId`/`configVehiculoId`, creando lo que falte en el
catálogo. No crea ninguna solicitud; es el paso previo a `lineas:bulk`.

- **Roles**: `Comercial`, `Admin`. (#307: `AdminBiblioteca` ya no entra.)
- **Body**: **array** (no objeto) de al menos 1 elemento:

| Campo | | Tipo | Restricción |
|---|---|---|---|
| `origen` | **requerido** | string | máx. 150 |
| `destino` | **requerido** | string | máx. 150 |
| `tipoVehiculo` | *opcional* | string | máx. 100 |
| `carroceria` | *opcional* | string | máx. 100 |

- **→** `200 CatalogoLineaResueltoDto[]`
- **Errores**: `400 SIN_LINEAS` (array vacío)
- **Alias (S6-HU-27.2/#321)**: el `tipoVehiculo` se busca primero en el diccionario de alias (§3.19): `TB`, `T.B`, `Turbo` y `2L2` resuelven
  a la **misma** configuración (se guarda el código, `2L2`, no el texto). La `carroceria` no pasa por el diccionario. Un texto que el
  diccionario no conoce sigue naciendo en **cuarentena** (`EsCatalogoOficial = false`) hasta que alguien lo mapee en la bandeja (§3.19).

#### `POST /api/solicitudes/{id}/lineas:bulk` — carga masiva de un pliego
- **Roles**: `Admin` (#307, decisión D5: solo Admin).
- **Body**: `{ lineas: LineaBulk[] }`, **requerido**, mínimo 1 línea.
- **→** `202 TrabajoAceptadoDto`. El costeo corre en segundo plano (§1.10).
- **Errores**: `404 SOLICITUD_NO_ENCONTRADA` · `409 SOLICITUD_NO_EDITABLE`

#### `POST /api/solicitudes/{id}/rechazar`
- **Roles**: `Comercial`, `Admin`. (#307: `AdminBiblioteca` ya no entra.)

| Body | | Tipo | Restricción |
|---|---|---|---|
| `motivo` | **requerido** | string | máx. 100 |
| `comentario` | *opcional* | string | máx. 500 |

- **→** `204`
- **Errores**: `404 SOLICITUD_NO_ENCONTRADA` · `409 SOLICITUD_YA_DESCARTADA`

#### `POST /api/solicitudes/{id}/recalcular-score`
- **Roles**: `Admin` (#307, decisión D5: solo Admin). Sin body.
- **→** `200 DesgloseScoreDto`
- **Errores**: `404 SOLICITUD_NO_ENCONTRADA`

#### `GET /api/solicitudes/{id}/propuesta-tarifa`
Compara plaza (SAP y propia), SICE-TAC e histórico (SAP y propio) y propone el valor del flete por línea con el flujo de
`docs/documentacion/FLUJO-DECISION-TARIFA.md`: Solo SICE-TAC (regla y piso), plaza vigente mayor que el piso, o proceso completo
(recomendado = el valor más alto). No persiste nada.

**Cuándo se guarda solo.** El flete se guarda solo —al capturar, al costear una línea en segundo plano y con `aplicar-propuestas`—
cuando la línea es `autocosteable` (hay `valorPropuesto` y ningún `motivoPendiente`), sea cual sea `modoDecision`. Un proceso completo
con un único candidato válido también se guarda. Sin ningún valor, o con un motivo de pendiente, la línea queda `porCostear`.

**Vehículo relajado.** Cuando la línea pide un vehículo y una fuente no tiene tarifas de ese vehículo en la ruta, el candidato sale con
`vehiculoRelajado = true` y una `advertencia`: se muestra pero no entra al recomendado ni puede activar Solo SICE-TAC ni plaza vigente
mayor. Si todos los candidatos están relajados, no hay valor propuesto.

**Retorno del contenedor y levante.** `retornoContenedor` y `aprovisionamientoContenedor` se calculan en el servidor para los tres caminos,
con SICE-TAC de viaje VACÍO, 1 h + 1 h, el mismo vehículo y la misma unidad de transporte (carrocería) de la línea (sin carrocería solo en
una línea de contenedor, unidad PORTACONTENEDORES). Si la línea los pide y no hay piso exacto, la línea completa queda pendiente
(`motivoPendiente`) y no se guarda nada. Una línea de devolución de contenedor no los tiene. Se suman al flete en la cotización.

**Devolución de contenedor.** El flete principal se busca con viaje VACÍO y 1 h + 1 h por defecto; la plaza SAP solo cuenta si coincide
con la línea en consolidado, exclusivo y clase de contenedor.

S5-HU-19.1/#281: **orden de fuentes de plaza/histórico** — SAP oficial (match
exacto) → SAP tolerante (por zona) → propia de NEXO (`tbl_tarifas_plaza`/
`tbl_tarifas_plaza_historico`), ganando la primera VIGENTE (externa primero).
Una plaza SAP vencida ya NO detiene la búsqueda: se sigue intentando SAP
tolerante y luego la propia antes de rendirse a la vencida como referencia.
`plaza`/`historicoSap` llevan `tablaOrigen` ("SAP" | "NEXO") para saber de
cuál de las dos vino el candidato ganador.

**SICE-TAC solo del mes y año actuales (#300).** El piso SICE-TAC se usa únicamente si su período (AAAAMM)
es el del mes y año actuales **en hora de Colombia**, **sin mes de gracia**: el día 1 del mes siguiente el
período anterior ya no sirve. Antes se tomaba el período más alto cargado en DWSice sin compararlo con hoy.
Si no es el actual, `sicetac` sale `null`, la línea queda en `modoDecision = "ConsultaCompleta"` (sin piso nunca
hay modo automático), la `justificacion` empieza por el motivo y `motivoPendiente` lo trae — el usuario lo ve
en la Mesa. Lo mismo aplica al piso de la Mesa (`pisoSicetac` null + `avisoSicetac`) y al aprovisionamiento.
`POST .../aplicar-propuestas` **no** aplica una línea con `motivoPendiente`: sin piso contra el cual validar,
guardar el flete sería guardarlo sin validar.

**Tramos vacíos con 1 h + 1 h (#300).** Todo tramo vacío se calcula como piso SICE-TAC de viaje VACÍO con
1 h de cargue + 1 h de descargue (`clsResolucionHorasLinea.HorasTramoVacio`): el **retorno del contenedor**
(`retornoContenedor`, destino de la línea → `ciudadDevolucionContenedor`) y el **levante en otra ciudad**
(`GET /api/captura-solicitudes/aprovisionamiento`). Las horas son **por tramo**: el viaje cargado conserva las
de la línea (4 + 4 por defecto) y marcar `retornaContenedor` ya **no** las cambia. Las horas de los tramos
vacíos son una constante de negocio, no un campo de la línea.

- **Roles**: Todos.
- **→** `200 PropuestaSolicitudDto`
- **Errores**: `404 SOLICITUD_NO_ENCONTRADA` · `404 LINEA_NO_ENCONTRADA`

### 3.3 Captura de solicitudes — `/api/captura-solicitudes`

**Roles (S5-HU-22.1/#288)**: ya **no** están a nivel de controller. Un `[Authorize(Roles)]` de
método no reemplaza el de la clase —se combinan con AND—, así que con `Comercial,Admin` en la
clase, `GET .../ciudades` devolvía `403` al rol de Biblioteca (hoy `AdminBiblioteca`, que sí gestiona servicios en
Biblioteca) y el campo se quedaba sin sugerencias. Ahora:

| Endpoint | Roles |
|---|---|
| `POST analizar-excel`, `POST /` (capturar) | `Comercial`, `Admin` |
| `GET aprovisionamiento`, `GET clientes` | `Comercial`, `Admin` (revisado caso por caso: se mantienen) |
| `GET departamentos`, `GET ciudades`, `GET municipios` | **cualquier rol válido de Nexo** (hay que estar autenticado; sin sesión → `401`) |

#### `POST /api/captura-solicitudes/analizar-excel`
No persiste; lee el Excel para que el usuario mapee columnas.

- **Body**: `multipart/form-data`, campo `archivo` (§1.9).
- **→** `200 AnalisisExcelDto`
- **Errores**: `400 ARCHIVO_REQUERIDO` · `400 FORMATO_NO_SOPORTADO` · `400 EXCEL_INVALIDO` · `400 EXCEL_SIN_DATOS`

#### `POST /api/captura-solicitudes` — crear solicitud completa
Resuelve o crea rutas y vehículos a partir del texto libre de las líneas.

**Línea de Cupo sin vehículo (S5-HU-20.5/#297).** Una línea sin `tipoVehiculo` se acepta, pero si su
`claseViaje` es **Cupo** (o no trae `claseViaje`) **no se autocostea**: sin vehículo el motor busca el piso
SICE-TAC sin filtro y toma el menor valor, y la plaza cae al "mayor valor vigente" solo con una advertencia
— un precio bajo que nadie revisa. Queda `estadoLinea = "PorCostear"` sin ningún flete, con
`motivoPendiente = "Línea sin vehículo: requiere consulta completa"` (visible en la Mesa), y su propuesta
(`GET /api/solicitudes/{id}/propuesta-tarifa`) sale `modoDecision = "ConsultaCompleta"` con
`motivoPendiente` y los candidatos como **referencia**: el Administrador de Tarifas elige o ingresa el valor
(nunca bajo el piso). Una línea a la que luego se le pone vehículo se costea normalmente.
**No aplica** a Contenedor (el vehículo se deduce por peso y clase de contenedor), ni a Carga suelta, ni a
Extradimensionado (decidido por Sara: solo Cupo).

**Normalización de nombres.** `origen`, `destino`, `tipoVehiculo` y `carroceria` se comparan sin tildes y con
`_` y `-` equivalentes a espacio, así que "Bogotá"/"Bogota" o "Furgon Refrigerado"/"FURGON_REFRIGERADO"
resuelven a la **misma** fila en vez de crear otra. Lo que se guarda en una fila nueva sigue siendo el texto
tal como llegó (limpio de espacios).

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
| `notas` | *opcional* | string | máx. 2000. Observaciones comerciales (distinto de `referencia`) |
| `lineas` | **requerido** | array | mínimo 1 |

| Cada línea | | Tipo | Restricción |
|---|---|---|---|
| `origen` | **requerido** | string | máx. 150 |
| `destino` | **requerido** | string | máx. 150 |
| `origenMunicipioId` | *opcional* | int | S5-HU-16.1/#254. `id` elegido en `GET /ciudades` para `origen` — sin esto la línea no resuelve municipio y el motor de costeo (#255/#256) no aplica para ella |
| `destinoMunicipioId` | *opcional* | int | ídem, para `destino` |
| `peso` | **condicional** | decimal | 0–999999.99 kg. **Requerido (> 0) si las posiciones no traen peso** |
| `tipoVehiculo` | *opcional* | string | máx. 100 |
| `carroceria` | *opcional* | string | máx. 100 |
| `tipologia` | *opcional* | string | `TipologiaCarga` |
| `lineaNegocio` | *opcional* | string | máx. 100 |
| `claseViaje` | *opcional* | string | máx. 50 |
| `claseContenedor` | *opcional* | string | máx. 20 |
| `exclusivo`, `consolidado`, `retornaContenedor`, `levanteContenedorDiferente` | *opcional* | boolean | #300: `retornaContenedor` agrega el tramo vacío de retorno (1 h + 1 h, aparte) y **ya no cambia las horas de la línea** (RN-03) |
| `horasCargue`, `horasDescargue` | *opcional* | decimal | 0–240. S4-HU-15.5/#251. Hay que mandar **las dos** para que se respeten; si falta una, aplica el default. Default: **4 + 4** (o las de la solicitud); **1 + 1** si la línea ES una devolución de contenedor (`lineaNegocio` = devolución). #300 |
| `ciudadDevolucionContenedor` | *opcional* | string | máx. 150 |
| `ciudadLevanteContenedor` | *opcional* | string | máx. 150 |
| `valorAprovisionamiento` | *opcional* | decimal | monetario. Se ignora: el aprovisionamiento lo calcula el servidor con la ciudad de levante (`ciudadLevanteContenedor`) |
| `valorMercancia` | *opcional* | decimal | monetario |
| `posiciones` | *opcional* | array | máx. 200 por línea |
| `grupoRutaId` | *opcional* | UUID | S5-HU-18.3/#275. Agrupa las líneas de una misma carga heterogénea. Lo pone **quien crea las líneas del grupo** (Cotifly, o el portal al crear una línea por camión); null solo en la línea suelta |
| `idRecomendacionVehiculo` | *opcional* | string | máx. 64. S5-HU-18.2/#274. `idRecomendacionVehiculo` de `POST /api/cubicacion` (§1.2) si esta línea se capturó a partir de una cubicación |

| Cada posición | | Tipo | Restricción |
|---|---|---|---|
| `largoCm`, `anchoCm`, `altoCm` | **requerido** | decimal | 0.1–999999 |
| `cantidad` | *opcional* | int | 1–**10000**, def. 1 (#304: antes 100000; el Cubicador corta en 10.000 unidades por petición) |
| `descripcion` | *opcional* | string | máx. 200 |
| `pesoKg` | **requerido** | decimal | 0.01–999999.99. **Peso de UNA unidad, no el total de la posición** (el motor hace `pesoKg × cantidad`: mandar el total lo multiplica dos veces). #304: antes era opcional; ahora es obligatorio en cada posición (una línea **sin** posiciones se sigue capturando solo con su `peso`) |
| `codigoTipoMercancia` | *opcional* | string | máx. 40. S5-HU-18.2/#274. Código (no uuid) de `tbl_tipos_mercancia` — null = tipo desconocido, sin default asignado |

Regla de peso: el peso de la línea es `peso` si viene; si no, la suma de
`cantidad × pesoKg` de las posiciones. Debe ser > 0. Si vienen ambos, el `peso`
total debe ser **mayor o igual** a la suma de las posiciones (la diferencia es la
tara); solo se rechaza (`400 PESO_INCONSISTENTE`) cuando las posiciones suman **más**
que el peso total, con una tolerancia del 1 % por redondeos.

**Peso con tara (S6-HU-28.1/#308).** El `peso` de la línea es el peso **total que se transporta, INCLUYENDO la tara** (embalaje,
estibas, contenedor vacío...). **Nexo no la suma ni la resta**: la escribe quien captura (el Comercial, o Cotifly) dentro de `peso`, y
siempre en **kilogramos** (el backend nunca recibe toneladas; la conversión de unidades es del front). Qué peso usa cada cosa:

| Qué | Usa | Detalle |
|---|---|---|
| Rango de peso de la tarifa de plaza (SAP: `rangoPesoInf`–`rangoPesoSup` y `pesoLimite`; propia de Nexo: banda de `/api/pesos`) | **`peso` de la línea** (con tara) | por eso debe ser el peso real que sube al camión |
| Configuración SICE-TAC de un contenedor (rango de peso → config) y la regla C2 ≥ 4.301 kg | **`peso` de la línea** | idem |
| Recomendación de vehículo (`POST /api/cubicacion`) | **el de cada mercancía** (`mercancias[].pesoKg` × `cantidad`, SIN tara) | NO usa el `peso` de la línea. El Cubicador compara esa carga contra la capacidad del vehículo **menos su tara** (`taraKg` del vehículo) |
| El piso SICE-TAC (valor) | no usa el peso: usa la configuración, la ruta y las horas | |

Lo que hay que saber: la suma de las posiciones (`cantidad × pesoKg`, sin tara) y el `peso` de la línea (con tara) **no coinciden por
la tara**, y la regla `PESO_INCONSISTENTE` lo respeta: el `peso` total puede ser mayor que la suma de las posiciones (la tara) y la línea se
crea (`201`); solo se rechaza cuando las posiciones **suman más** que el `peso` total (más de un 1 %), porque eso no tiene sentido físico.
Ejemplo: `peso` 1000 con una posición de 100 kg → `201`; posiciones que suman 1200 con `peso` 1000 → `400 PESO_INCONSISTENTE`. Una línea sin
`posiciones` no se ve afectada. La regla vale igual para lo que llega desde la importación de una licitación, que captura por este mismo endpoint;
`POST /api/solicitudes/{id}/lineas:bulk` no valida la suma.

- **→** `201 CapturaResultDto`, header `Location: /api/solicitudes/{id}`
- **Errores**: `400 CLIENTE_REQUERIDO` · `400 CLIENTE_INVALIDO` · `400 CLIENTE_BP_INVALIDO` · `400 DWCUBOS_NO_CONFIGURADO` (se usó `clienteBp` y el DW no está configurado en el ambiente) · `400 ENUM_INVALIDO` · `400 LINEA_INCOMPLETA` · `400 PESO_REQUERIDO` · `400 PESO_INCONSISTENTE` · `400 CUBICACION_DEMASIADO_GRANDE` (#304: más de 10.000 unidades en las posiciones de una línea). Una posición sin `pesoKg`, con `pesoKg` 0 o con `cantidad` > 10000 sale como `400 VALIDATION_ERROR` (validación de campos)

**Preguntas del contrato de captura (S6-HU-29.3/#313).** Tres preguntas del informe del front de Nexo, con su respuesta:

*A. `grupoRutaId`: ¿lo manda el front de Nexo o va null?* **Lo manda quien crea las líneas del grupo — también el front de Nexo.** Si
parte una carga en varios camiones, genera **un GUID por recomendación** y lo pone **igual en todas las líneas de ese grupo** (una
línea por camión, #217). Va `null` solo en una línea suelta: un único camión, o una línea que no pasó por la cubicación. NEXO lo guarda
tal cual y **nunca lo genera**. (El texto anterior, "lo pone siempre Cotifly", era un resto de cuando solo Cotifly agrupaba.)

*B. Posiciones por camión: la recomendación trae cantidad, código, `pesoKg` y volumen, pero no medidas.* **Se arman con `indicePosicion`.**
Cada fila de `vehiculos[].mercancias[]` trae `indicePosicion` (base 0): el índice de la posición **en el arreglo `mercancias` del request
con que se pidió la recomendación** (S5-HU-17.8). El front toma esa posición de su propia lista — que sí tiene `largoCm`, `anchoCm`,
`altoCm`, `descripcion` y `codigoTipoMercancia` — y arma la posición del camión con la `cantidad` **de esa fila** (las unidades que van
en ese camión) y el `pesoKg` **unitario** de la posición original. No se agrupa por `codigoMercancia`: dos posiciones del mismo tipo
llegan como dos filas con índices distintos, y dos filas de un mismo camión pueden compartir código. Una recomendación guardada **antes**
de 17.8 trae `indicePosicion = null`: no hay forma de saber a qué posición corresponde una fila, así que hay que **volver a pedir la
recomendación** (se guarda como corrida nueva; la anterior no se toca).

*C. Clase de contenedor de Extradimensionado (D10).* **Opcional y sin validar** (la opción recomendada). S6-HU-27.2/#321: si el texto está en
el diccionario de alias de contenedor (`40 HC`, `40HC`, `20 STD`... → `CNT40.HC`, `CNT20.ST`) se guarda el código; si no, el backend la guarda como
texto libre (máx. 20 caracteres) y **no la compara con el catálogo**: los nombres provisionales del front (`CNT20.FT`, `CNT20.OP`,
`CNT40.FT`, `CNT40.OP`) se aceptan tal cual. Riesgo conocido: si algún día se valida contra el catálogo real (`CNT20.IT`, `RE`, `ST` y
`CNT40.HC`, `RE`), esos nombres provisionales se rechazarían; eso exigirá una issue propia y migrar los datos. Por ahora la
`claseContenedor` de una línea de Extradimensionado **no se expone** en `GET /api/solicitudes/{id}` (sale `null` salvo clase de viaje
`contenedor`, S6-HU-29.1), y los nombres provisionales **no filtran nada al costear**: el motor solo filtra por una clase que resuelve
contra el catálogo real, y esos nombres no existen en él.

#### `GET /api/captura-solicitudes/aprovisionamiento`
Estimación del piso SICE-TAC del viaje vacío (levante → origen del cargue), con **1 h + 1 h** (2 h en total) y solo con el período
SICE-TAC del mes actual (si no, `candidato: null`). Con `carroceria` elige la fila de esa unidad de transporte, igual que el costeo de la
línea; sin ella devuelve una aproximación (la menor) y lo dice en `advertencia`. Es solo para mostrar en el formulario: al costear, la
línea calcula su propio aprovisionamiento y el `valorAprovisionamiento` que mande el cliente en la captura se ignora.

`levante` y `origen` se resuelven a su municipio (`tbl_municipios`) y la ruta se compara por código DANE, igual que el costeo de la línea: sirven
«BOGOTA, D.C.» (como lo entrega `GET /api/captura-solicitudes/ciudades`) y «Bogotá». Si alguna de las dos no se resuelve a un único
municipio, se busca por el texto del nombre de la ruta de SICE-TAC; una ciudad que no existe devuelve `candidato: null`.

| Query | | Tipo | Restricción |
|---|---|---|---|
| `levante` | **requerido** | string | ciudad de levante del contenedor |
| `origen` | **requerido** | string | origen del cargue |
| `configuracion` | *opcional* | string | configuración vehicular |
| `carroceria` | *opcional* | string | carrocería de la línea (unidad de transporte). Sin ella, el valor es una aproximación |

- **→** `200 AprovisionamientoDto` (`candidato: null` si la matriz no trae la ruta)
- **Errores**: `400 PARAMETROS_REQUERIDOS` (falta `levante` u `origen`). Swagger
  los muestra como opcionales, pero son obligatorios.

#### `GET /api/captura-solicitudes/clientes`
- **Query**: `q` *opcional* (nombre, NIT o BP).
- **→** `200 ClienteBusquedaDto[]`. Con el DW configurado busca en clientes
  reales; sin él, en la tabla local.

#### `GET /api/captura-solicitudes/departamentos`
Departamentos de Colombia con municipio en el catálogo propio
(`tbl_municipios`, S5-HU-16.1). **Cambio de fuente**: hasta esta issue
resolvía contra `tblSAPDANE` en vivo, filtrado a ciudades "costeables" en el
tarifario SAP; ahora es `tbl_municipios` (más amplio — incluye municipios sin
tarifa SAP cargada, que de todas formas pueden costear por SICE-TAC).
- **→** `200 string[]`, ordenados.

#### `GET /api/captura-solicitudes/ciudades`
Búsqueda de ciudades para Origen/Destino, filtrable por `departamento` y/o
`q` (texto, sin tildes ni mayúsculas). **Cambio de forma (S5-HU-16.1/#254),
ruptura**: antes devolvía `{ municipio, departamento }`; ahora devuelve la
fila completa de `tbl_municipios`, con los datos que #255/#256 necesitan
para dejar de comparar por texto en el motor de costeo. El frontend debe
capturar `id` y enviarlo como `origenMunicipioId`/`destinoMunicipioId` al
crear la solicitud (ver tabla de línea en `POST /api/captura-solicitudes`
arriba) — sin eso, la línea queda sin municipio resuelto y #255/#256 no
aplican para ella (cae al comportamiento de siempre, sin error).

| Query | | Tipo | Restricción |
|---|---|---|---|
| `departamento` | *opcional* | string | nombre exacto (normalizado sin tildes/mayúsculas) |
| `q` | *opcional* | string | texto libre sobre el nombre del municipio |

- **→** `200 CiudadBusquedaDto[]`, máx. 50, ordenadas por nombre:
  `{ id: int, codigoDane: string, municipio: string, departamento: string, nombreTarifasPlaza: string|null, zonaTarificacion: string|null }`
- `nombreTarifasPlaza`/`zonaTarificacion` (códigos `ZC_`/`ZTC_` del
  Tarifario SAP) vienen `null` cuando el municipio no tiene movimiento
  comercial conocido — no es un error, el municipio sigue siendo válido
  para SICE-TAC.
- 8 ciudades que existían en el catálogo anterior ya no aparecen: eran
  zonas agregadas sin código DANE propio (Valle de Aburrá, Urabá, Oriente
  antioqueño) o localidades sub-municipales sin fila propia en
  `tbl_municipios` (Don Matías, Catambuco, Doradal, Bolombolo, Chibolo).

#### `GET /api/captura-solicitudes/municipios`
Catálogo completo de municipios (S4-HU-09.1), todas las columnas — para
poblar el desplegable de Origen/Destino. ~3.800 filas sin paginar ni
filtrar: se carga una sola vez y queda en memoria del front, no se consulta
en cada tecleo. Mismo catálogo que `GET /api/catalogos/municipios` (§3.19),
expuesto también ahí porque ese otro controller no está limitado a
Comercial/Admin.
- **→** `200 MunicipioDto[]`

### 3.4 Cotizaciones — `/api/cotizaciones`

#### `POST /api/cotizaciones` — crear o recuperar el borrador
- **Roles**: `Comercial`, `AdminTarifas`, `Admin`.
- **Body**: `{ solicitudId: UUID }` **requerido**.
- **→** `201 CotizacionDto`, header `Location: /api/cotizaciones/{id}`. Si ya
  existe un borrador para la solicitud, lo devuelve.
- **Errores**: `400 SOLICITUD_INVALIDA`

#### `GET /api/cotizaciones/{id}`
- **Roles**: Todos.
- **→** `200 CotizacionDto`
- **Errores**: `404 COTIZACION_NO_ENCONTRADA`

#### `GET /api/cotizaciones/por-solicitud/{solicitudId}` — la cotización que ya existe
S5-HU-20.1/#286. **Nunca crea nada** (a diferencia de `POST /api/cotizaciones`, que es
"crear o recuperar" y tiene efecto secundario). Mesa de Costeo la consulta al abrir
para saber si ya hay una cotización —p. ej. la auto-costeada al capturar— sin tener que
disparar el POST.

- **Roles**: Todos.
- **→** `200 CotizacionDto` · `204 No Content` si la solicitud no tiene cotización (o no existe).
- Elige igual que el POST: si la solicitud ya está `Entregada`, su cotización `Entregada`;
  si no, el borrador en curso. GET y POST nunca apuntan a cotizaciones distintas.

#### `POST /api/cotizaciones/{id}/aplicar-propuestas`
Precarga como fletes el valor propuesto por la Biblioteca en las líneas sin flete, con su aprovisionamiento y su retorno del
contenedor. Se saltan las líneas sin valor propuesto, las que traen `motivoPendiente` y las rechazadas. Sin body.

| Query | | Tipo | Restricción |
|---|---|---|---|
| `soloAutomaticas` | *opcional* | boolean | Se conserva por compatibilidad: ya no cambia el resultado, porque toda línea con valor y sin motivo de pendiente se guarda sola |

- **Roles**: `Comercial`, `AdminTarifas`, `Admin`.
- **→** `200 AplicarPropuestasResultDto`
- **Errores**: `404 COTIZACION_NO_ENCONTRADA` · `409 COTIZACION_NO_EDITABLE`

#### `POST /api/cotizaciones/{id}/lineas` — agregar flete de una línea
- **Roles**: `AdminTarifas`, `Admin`. (`Comercial` **no**.)

| Body | | Tipo | Restricción |
|---|---|---|---|
| `fuente` | **requerido** | string | `sicetac` \| `historica` \| `manual` \| `tarifario` |
| `valorFlete` | *(ver nota)* | decimal | monetario. Si se omite llega como `0` |
| `lineaSolicitudId` | *opcional* | UUID | debe pertenecer a la solicitud |
| `valorRetornoContenedor` | *opcional* | decimal | monetario; suma al total aparte |

S5-HU-19.3/#283 — con `lineaSolicitudId`, el servidor recalcula la propuesta de esa
línea (no confía en lo que diga el navegador) y valida:

- **Nunca bajo el piso SICE-TAC**, con cualquier `fuente` (tarifario, histórica o
  manual): `valorFlete < piso` → `400 FLETE_BAJO_PISO_SICETAC`. Se bloquea con aviso, no
  pide confirmación. Sin piso para la línea, se guarda normal.
- **Fuente `historica`**: se acepta si hay una "histórica sugerida" vigente (≤ 1 semana)
  **o** si `valorFlete` coincide con el histórico de la propuesta de esa línea
  (ventana de 3 meses). Si no viene de ninguna de las dos → `409 HISTORICA_VENCIDA`.

- **→** `200 CotizacionDto`
- **Errores**: `400 FUENTE_INVALIDA` · `400 LINEA_INVALIDA` · `400 FLETE_BAJO_PISO_SICETAC` · `404 COTIZACION_NO_ENCONTRADA` · `409 COTIZACION_NO_EDITABLE` · `409 HISTORICA_VENCIDA`

#### `PUT /api/cotizaciones/{id}/lineas/{lineaSolicitudId}` — editar un flete
S5-HU-20.1/#286; **S6-HU-26.1/#30: de cualquier fuente**. Modifica **la misma fila** de la cotización: `POST .../lineas` siempre
agrega una nueva, así que editar con él duplicaría la línea e inflaría el total. La ruta
usa el id de la línea del **pliego** (`lineaSolicitudId`, el mismo que manda el POST), no
el de la fila de la cotización.

- **Roles**: `AdminTarifas`, `Admin`. (`Comercial` **no**.)

| Body | | Tipo | Restricción |
|---|---|---|---|
| `valorFlete` | **requerido** | decimal | 0.01–9999999999999.99 |
| `valorRetornoContenedor` | *opcional* | decimal | monetario. Si se omite no se toca el que tenía |
| `confirmarSinPiso` | *opcional* | boolean | #30. Def. `false`. `true` = guardar aunque **no se haya podido verificar** el piso SICE-TAC (ver abajo). No salta un valor bajo un piso que SÍ se encontró |

No recibe `fuente`. Reglas (#30):

- **Cualquier fuente se edita** (`manual`, `tarifario`/plaza, `sicetac`, `historica`): una tarifa se corrige en su sitio, sin tener que
  reemplazarla eligiendo otro candidato. Antes solo la `manual` (`409 FLETE_NO_EDITABLE`, código que ya no existe).
- **Con regla Solo-SICE no se edita**: si la ruta (por código DANE) y el vehículo de la línea tienen regla en `tbl_reglas_solo_sicetac`,
  el flete lo fija el piso y responde **`409 FLETE_SOLO_SICETAC`** (también para un flete manual). Misma definición que usa el motor
  de propuesta; la Mesa la expone como `tieneReglaSoloSicetac` en cada línea de `GET /api/solicitudes/{id}`.
- **Al guardar, la fuente pasa a `Manual`** (decisión D4): el valor ya no sale de su fuente original sino de una persona.
- **Auditoría** (`editar_flete`): guarda `valorAnterior` y `fuenteAnterior`, `valorNuevo` y `fuenteNueva`, y `confirmadoSinPiso`.
- **Nunca bajo el piso SICE-TAC** (`400 FLETE_BAJO_PISO_SICETAC`, mismo cálculo del servidor que al agregar, #283).
- **Si el piso no se puede verificar, no se guarda en silencio** (`409 PISO_SICETAC_NO_VERIFICABLE`): la ruta no tiene fila en DWSice, el
  período más reciente cargado no es el del mes actual (#300), o la consulta falló. El mensaje dice la causa cuando es el período. Quien
  edita decide: reintenta con `confirmarSinPiso = true` y queda registrado en la auditoría. Antes (#283) esta validación simplemente se
  saltaba y cualquier valor entraba sin aviso.
- Solo cotización `Borrador`.

- **→** `200 CotizacionDto` (misma cantidad de líneas, total recalculado)
- **Errores**: `400 VALIDATION_ERROR` · `400 FLETE_BAJO_PISO_SICETAC` · `404 COTIZACION_NO_ENCONTRADA` · `404 LINEA_NO_ENCONTRADA` · `409 COTIZACION_NO_EDITABLE` · `409 FLETE_SOLO_SICETAC` · `409 PISO_SICETAC_NO_VERIFICABLE`

> `POST .../lineas` (agregar) **no cambió**: sin piso verificable sigue guardando el valor sin aviso. Está documentado con una prueba y
> pendiente de decidir (sección 9 del documento de issues, "flete manual sin piso").

#### `POST /api/cotizaciones/{id}/lineas/{lineaSolicitudId}/rechazo` — rechazar una línea con motivo
S6-HU-24.3/#320. Para entregar el resto de la cotización sin que una línea que no se puede costear la frene. Antes solo existía rechazar la
solicitud completa. La línea queda `rechazada`, **sin flete, sin sumar al total** y se considera resuelta para entregar.

- **Roles**: `AdminTarifas`, `Admin`.

| Body | | Tipo | Restricción |
|---|---|---|---|
| `motivo` | **requerido** | string | 1–500 caracteres |

- **→** `200 { lineaSolicitudId, estadoLinea: "rechazada", motivoRechazo, rechazadaAt }`
- Solo con la cotización en Borrador y una línea **sin flete** en la cotización (una línea ya costeada no se rechaza: así una rechazada nunca
  suma al total ni sale con precio). Se guarda quién, cuándo y por qué (`motivoRechazo`, `rechazadaPor`, `rechazadaAt`) y queda en la auditoría.
- Una línea rechazada: el costeo automático y "Aplicar propuestas" la saltan; `POST .../lineas` responde `409 LINEA_RECHAZADA`; el Excel la
  lista como «Sin costeo» (sin precio y **sin el motivo**); el correo de entrega al comercial trae el bloque «Líneas sin costeo» **con** el motivo;
  `GET /api/solicitudes/{id}` la trae con `estadoLinea = "rechazada"` y `motivoRechazo`; las métricas de líneas autocosteadas no la cuentan.
- **Errores**: `400 MOTIVO_RECHAZO_REQUERIDO` · `404 COTIZACION_NO_ENCONTRADA` · `404 LINEA_NO_ENCONTRADA` · `409 COTIZACION_NO_EDITABLE` · `409 LINEA_YA_RECHAZADA` · `409 LINEA_CON_FLETE`

#### `DELETE /api/cotizaciones/{id}/lineas/{lineaSolicitudId}/rechazo` — deshacer el rechazo
S6-HU-24.3/#320. Solo mientras la cotización **no esté entregada** (entregada → `409 COTIZACION_NO_EDITABLE`): la línea vuelve a `porCostear`
(hay que costearla) y el motivo, quién y cuándo se limpian; el rechazo anterior queda en la auditoría.

- **Roles**: `AdminTarifas`, `Admin`.
- **→** `200 { lineaSolicitudId, estadoLinea: "porCostear", motivoRechazo: null, rechazadaAt: null }`
- **Errores**: `404 COTIZACION_NO_ENCONTRADA` · `404 LINEA_NO_ENCONTRADA` · `409 COTIZACION_NO_EDITABLE` · `409 LINEA_NO_RECHAZADA`

#### `PUT /api/cotizaciones/{id}/lineas/{lineaSolicitudId}/vehiculo` — cambiar el vehículo de una línea
S6-HU-27.3/#322 (decisión D9). Antes el vehículo de la línea solo se escribía al crearla. Solo con la cotización en Borrador.

- **Roles**: `AdminTarifas`, `Admin`.

| Body (uno de los dos) | | Tipo | Restricción |
|---|---|---|---|
| `configVehiculoId` | *uno de los dos* | UUID | una configuración **oficial** (no en cuarentena ni descartada) |
| `tipoVehiculo` | *uno de los dos* | string | máx. 100. Texto o código: se resuelve con el diccionario de alias (§3.19) o contra una configuración oficial ya existente |
| `carroceria` | *opcional* | string | máx. 100; solo con `tipoVehiculo` |

- **→** `200 { lineaSolicitudId, configVehiculoId, tipoVehiculo, carroceria, esCatalogoOficial, cambioVehiculo, fleteEliminado, estadoLinea, motivoPendiente, propuesta }`
- El flete que la línea tuviera se calculó con el vehículo anterior: **se descarta** (`fleteEliminado = true`, el total se recalcula) y la línea vuelve a
  `porCostear`. `propuesta` es la propuesta recalculada de esa línea con el vehículo nuevo, con su piso SICE-TAC; el flete nuevo entra por los caminos de
  siempre (`POST .../lineas` / `aplicar-propuestas`), que siguen sin dejar pasar un valor bajo el piso. Mismo vehículo que ya tenía: `200` sin tocar nada
  (`cambioVehiculo = false`). Un texto desconocido **no** crea nada en cuarentena.
- **Errores**: `400 VEHICULO_REQUERIDO` (ninguno de los dos, o los dos) · `400 VEHICULO_NO_RECONOCIDO` · `404 COTIZACION_NO_ENCONTRADA` · `404 LINEA_NO_ENCONTRADA` · `404 VEHICULO_NO_ENCONTRADO` · `409 COTIZACION_NO_EDITABLE` · `409 LINEA_RECHAZADA` · `409 VEHICULO_NO_OFICIAL`

#### `POST /api/cotizaciones/{id}/conexos` — agregar servicios conexos
S5-HU-20.1/#286 (HU-B23): se admite mientras la cotización **no tenga resultado**
(`resultado == "SinDefinir"`), o sea `Borrador` **o `Entregada`** — un servicio omitido se
descubre justo después de entregar. `Ganada`/`Perdida` → `409 COTIZACION_CERRADA`.
Sobre una cotización `Entregada` deja `modificadaTrasEntregaAt` (sin tocar `estado` ni
`entregadaAt`) y, si quien lo hace **no** es quien creó la solicitud, le llega un correo
"Cotización actualizada" (ver [README §5.5](docs/contrato-integracion/README.md)). Sobre un
`Borrador`, o si el actor es el mismo creador, no se envía nada.

- **Roles**: `Comercial`, `AdminTarifas`, `Admin`. (S5-HU-20.1: se agregó `AdminTarifas`.)
- **Body**: `{ items: [...] }` **requerido**, mínimo 1.

| Cada item | | Tipo | Restricción |
|---|---|---|---|
| `lineaId` | **requerido** | UUID | id de la línea DE LA COTIZACIÓN (no de la solicitud) a la que aplica — S4-HU-15.4/#250, ya no existe el conexo "general" sin línea |
| `servicioConexoId` | **requerido** | UUID | servicio existente y activo |
| `cantidad` | *opcional* | decimal | 0.01–999999.99, máx. 2 decimales, def. 1 |
| `valorAplicado` | *opcional* | decimal | monetario. Si falta: `costoUnitario × cantidad` |
| `origen` | *opcional* | string | S5-HU-18/#262. `individual` (def.) \| `general` — si el usuario pidió agregarlo a TODAS las líneas de una vez (igual manda un item por línea) |

- **→** `200 CotizacionDto`
- **Errores**: `400 SERVICIO_INVALIDO` · `400 SERVICIO_INACTIVO` · `400 LINEA_INVALIDA` · `400 CANTIDAD_INVALIDA` · `400 ORIGEN_INVALIDO` · `404 COTIZACION_NO_ENCONTRADA` · `409 COTIZACION_CERRADA`

#### `PATCH /api/cotizaciones/{id}/conexos/{conexoId}` — corregir un conexo
S5-HU-18/#262: antes solo existía el POST (siempre crea una fila nueva); un error
de cantidad no se podía deshacer, solo compensar agregando otra fila.

- **Roles**: `Comercial`, `Admin`. Solo en cotización `Borrador` (S5-HU-20.1 no extendió este endpoint a `Entregada`: solo agregar y quitar).

| Body | | Tipo | Restricción |
|---|---|---|---|
| `cantidad` | **requerido** | decimal | 0.01–999999.99, máx. 2 decimales |
| `valorAplicado` | *opcional* | decimal | monetario. Si falta: `costoUnitario × cantidad` |

- **→** `200 CotizacionDto`
- **Errores**: `400 CANTIDAD_INVALIDA` · `400 VALOR_REQUERIDO` (sin servicio para recalcular y sin `valorAplicado`) · `404 COTIZACION_NO_ENCONTRADA` · `404 CONEXO_NO_ENCONTRADO` · `409 COTIZACION_NO_EDITABLE`

#### `DELETE /api/cotizaciones/{id}/conexos/{conexoId}` — quitar un conexo
S5-HU-18/#262. S5-HU-20.1/#286 (HU-B24): igual que agregar, se admite mientras la cotización
no tenga resultado (`Borrador` o `Entregada`); `Ganada`/`Perdida` → `409 COTIZACION_CERRADA`.
Tras la entrega deja `modificadaTrasEntregaAt` y avisa al creador de la solicitud si el
actor es otra persona.

- **Roles**: `Comercial`, `AdminTarifas`, `Admin`. (S5-HU-20.1: se agregó `AdminTarifas`.)
- **→** `200 CotizacionDto` (el conexo ya no aparece en `conexos`, `total` recalculado)
- **Errores**: `404 COTIZACION_NO_ENCONTRADA` · `404 CONEXO_NO_ENCONTRADO` (inexistente o de otra cotización) · `409 COTIZACION_CERRADA`

#### `POST /api/cotizaciones/{id}/entregar`
Marca la entrega y envía la cotización por correo al comercial responsable
(el correo es asíncrono, ver
[README §5.3](docs/contrato-integracion/README.md#53-entregar-una-cotización-ya-no-espera-al-correo)).

- **Roles**: `AdminTarifas`, `Admin`. (`Comercial` **no**.)
- **Body**: `multipart/form-data`, campo `formato` *opcional* (PDF, máx. 10 MB).
  Se puede enviar la petición sin cuerpo.
- S5-HU-19.3/#283: no se entrega mientras queden líneas del pliego `PorCostear` —
  todas deben estar `AutoResuelta`, `Revisada` o `Rechazada` (S6-HU-24.3/#320: una
  línea rechazada con motivo cuenta como resuelta). `COTIZACION_SIN_FLETE` sigue
  cubriendo el caso de cero líneas en la cotización, **salvo** que alguna esté rechazada:
  con todas las líneas rechazadas sí se entrega (la cotización queda en $0 y el correo
  lista cada línea sin costeo con su motivo).
- **→** `200 EntregarCotizacionResultDto`
- **Errores**: `400 COTIZACION_SIN_FLETE` · `404 COTIZACION_NO_ENCONTRADA` · `409 COTIZACION_CON_LINEAS_PENDIENTES` · `409 COTIZACION_YA_ENTREGADA` · `409 SOLICITUD_NO_TRANSICIONABLE`

#### `POST /api/cotizaciones/{id}/resultado` — ganada/perdida
Si `resultado = "ganada"`, realimenta la biblioteca de Tarifas Ganadas.
**Siempre hay que entregar primero** (`/entregar`).

- **Roles**: `Comercial`, `Admin` (#213 — decidir si una cotización se ganó
  o se perdió es criterio del Comercial dueño del negocio; ni AdminTarifas
  ni AdminBiblioteca tienen este permiso).

| Body | | Tipo | Restricción |
|---|---|---|---|
| `resultado` | **requerido** | string | `ganada` \| `perdida` |
| `motivo` | *opcional* | string | máx. 100 |
| `comentario` | *opcional* | string | máx. 500 |

- **→** `200 CotizacionDto`
- **Errores**: `400 RESULTADO_INVALIDO` · `404 COTIZACION_NO_ENCONTRADA` · `409 COTIZACION_NO_ENTREGADA` · `409 RESULTADO_YA_DEFINIDO` · `409 SOLICITUD_NO_TRANSICIONABLE`

#### `GET /api/cotizaciones/{id}/exportar-excel`
- **Roles**: Todos.
- S6-HU-24.3/#320: las líneas rechazadas salen en la hoja con su ruta y la palabra «Sin costeo» en vez del precio (sin precio y **sin el motivo**), y no suman al total.
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
- **Roles**: Todos. Rate limit estándar. **Pagina (S6-HU-29.2/#312)**: `page` desde 1 y `top` como tamaño de página; `total` es el total
  **sin paginar** y el orden es estable (vigencia, fecha de creación e id, todos descendentes), así que `page=2` devuelve filas
  distintas a `page=1`. Sin `page` responde la primera página, como siempre.

| Query | | Tipo | Restricción |
|---|---|---|---|
| `q` | *opcional* | string | código DIVIPOLA origen/destino o detalle |
| `lineaNegocio` | *opcional* | **int** | código numérico (de `/filtros`) |
| `tipoVehiculo`, `tipoCarroseria` | *opcional* | string | código SAP (de `/filtros`) |
| `claseViaje` | *opcional* | **int** | código numérico (de `/filtros`) |
| `top` | *opcional* | int | 1–500, def. 50 (tamaño de página). **El tamaño efectivo máximo es 200**: un `top` mayor se reduce a 200 (igual que en `biblioteca-sap-plaza`); calcular las páginas con ese tope |
| `page` | *opcional* | int | ≥ 1, def. 1 (#312) |

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
**Roles de los tres**: `AdminTarifas`, `AdminBiblioteca`, `Admin`. (#307: se agregó `AdminBiblioteca`.)

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
- `origen`/`destino` son el valor crudo guardado (código DANE en las filas cargadas por
  Excel, texto libre en las más viejas) — el frontend debe pintar
  `origenMunicipio`/`destinoMunicipio` (nombre resuelto), no `origen`/`destino`. Si
  vienen `null`, el valor crudo no matcheó ningún código DANE conocido; mostrar el
  crudo como respaldo en ese caso.

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
- S5-HU-22.1/#288: una regla que **ya existe** se cuenta como `duplicadas` aunque su código de
  vehículo no esté en el catálogo de SAP (hay reglas con `3S2`/`2L3`, que SAP no trae). Antes
  se contaban como `invalidas`. El catálogo solo se exige a las reglas que se van a crear.

#### `GET /api/reglas-solo-sicetac/exportar-excel`
S5-HU-22.1/#288. Descarga las reglas ya cargadas, con las **mismas 3 columnas que acepta
`importar-excel`** (`ORIGEN`, `DESTINO`, `TIPO VEHICULO`) y los valores tal como están
guardados (código DANE como **texto**, para no perder el cero inicial de `05001000`). El
archivo se puede editar y reimportar: las reglas que ya existen se reportan como `duplicadas`
(no se pisan) y solo entran las filas nuevas.
- **Roles**: `AdminTarifas`, `Admin`.
- **→** `200`, archivo `.xlsx` (`reglas-solo-sicetac-yyyyMMdd.xlsx`)

### 3.9 Servicios conexos — `/api/servicios-conexos`

> Reestructurado en S4-HU-15.1/#223: tarifas variables por ciudad/ruta/puerto en vez de
> un valor único genérico — reemplaza por completo el `nombre`/`reglaCobro`/`valorBase`/
> `vigenteHasta` que documentaba esta sección antes (esa forma ya no existe en el código).

#### `GET /api/servicios-conexos`
- **Roles**: Todos.

| Query | | Tipo | Restricción |
|---|---|---|---|
| `soloActivos` | *opcional* | boolean | def. `false` |
| `q` | *opcional* | string | texto libre sobre `tipoServicio`/`codigoServicio` |
| `categoria` | *opcional* | string | `logistico` \| `adicional` |
| `page` | *opcional* | int | ≥ 1, def. 1 |
| `pageSize` | *opcional* | int | 1–200, def. 50 |

- **→** `200 Pagina<ServicioConexoDto>`

#### `POST /api/servicios-conexos`
- **Roles**: `ServiciosLogisticos`, `AdminTarifas`, `Admin`.

| Body | | Tipo | Restricción |
|---|---|---|---|
| `codigoServicio` | **requerido** | string | máx. 30 |
| `tipoServicio` | **requerido** | string | máx. 120 |
| `origen`, `destino`, `puerto`, `ruta`, `tipoMaquina` | *opcional* | string | la dimensión que no aplique a este servicio se guarda como `"NO APLICA"` — máx. 150 (origen/destino), 50 (puerto/ruta), 60 (tipoMaquina) |
| `costoUnitario` | **requerido** | decimal | ≥ 0 |
| `unidad` | **requerido** | string | máx. 30 |
| `vigencia` | **requerido** | fecha | solo fecha `"2026-12-31"` |
| `categoria` | *opcional* | string | `logistico` \| `adicional`, def. `logistico` |
| `activo` | *opcional* | boolean | def. `true` |

- **→** `201 ServicioConexoDto`
- **Errores**: `400 CATEGORIA_INVALIDA` · `409 SERVICIO_DUPLICADO` (ya existe un servicio con las mismas 6 dimensiones: código, origen, destino, puerto, ruta, tipo de máquina)

#### `PATCH /api/servicios-conexos/{id}`
Actualización parcial: solo cambian los campos enviados y no nulos.

- **Roles**: `ServiciosLogisticos`, `AdminTarifas`, `Admin`.
- **Body**: los mismos campos que el POST, todos *opcionales*, mismas restricciones.
- **→** `200 ServicioConexoDto`
- **Errores**: `400 CATEGORIA_INVALIDA` · `404 SERVICIO_NO_ENCONTRADO`

#### `POST /api/servicios-conexos/importar-excel`
Columnas: Codigo, Tipo Servicio, Origen, Destino, Puerto, Ruta, Tipo Maquina, Costo
Unitario, Unidad, Vigencia, Categoria, Activo — detección de encabezado tolerante (no
importa el orden ni si falta alguna columna opcional). Hace **upsert** por las 6
dimensiones (código, origen, destino, puerto, ruta, tipo de máquina): una fila cuya
combinación ya existe ACTUALIZA ese servicio en vez de crear uno nuevo — así el mismo
archivo sirve para exportar, editar y recargar. La comparación de esa llave ignora
mayúsculas y la forma Unicode de composición de tildes (S5-HU-19/#261); sigue
distinguiendo acentos (`café` ≠ `cafe`).

Una fila inválida (campo obligatorio vacío, texto que excede el máximo de su columna,
costo negativo o con más de 16 dígitos enteros) **nunca descarta el archivo completo**:
se cuenta en `invalidas`, su detalle queda en `errores[]` (máx. 20 mensajes), y el resto
de las filas se sigue procesando.

- **Roles**: `ServiciosLogisticos`, `AdminTarifas`, `Admin`.
- **Body**: `multipart/form-data`, campo `archivo` **requerido** (§1.9).
- **→** `200 ImportarServiciosResultDto`
- **Errores**: `400 ARCHIVO_REQUERIDO` · `400 FORMATO_NO_SOPORTADO` · `400 EXCEL_INVALIDO` · `400 EXCEL_SIN_DATOS` · `400 SERVICIO_DUPLICADO` (choque residual contra la base, no detectado en memoria — caso raro, ver nota de #261 abajo)

#### `GET /api/servicios-conexos/exportar-excel`
Descarga el catálogo a Excel con las mismas 12 columnas que lee el importador — el mismo
archivo sirve para editar y reimportar como actualización. S5-HU-19/#261: admite los
mismos filtros que el listado.

- **Roles**: Todos.

| Query | | Tipo | Restricción |
|---|---|---|---|
| `q` | *opcional* | string | texto libre sobre `tipoServicio`/`codigoServicio` |
| `categoria` | *opcional* | string | `logistico` \| `adicional` |

- **→** `200`, archivo `.xlsx` (sin filtros, exporta el catálogo completo)

#### `GET /api/servicios-conexos/plantilla-excel/{tipo}`
Plantilla de Excel descargable por tipo de servicio — cada una trae SOLO las columnas
que ese tipo necesita (S5-HU-19/#261, corrige #249: antes las 4 traían las mismas 12).
Sin fila de datos de ejemplo cargable: la fila 2 solo pre-llena las columnas
verdaderamente fijas de ese tipo (p. ej. `unidad`/`categoria`); subida sin editar, no
crea ningún registro.

- **Roles**: Todos.
- **`tipo`** (ruta): `almacenamiento` \| `itr` \| `precinto` \| `acompanamiento`.
- **→** `200`, archivo `.xlsx`
- **Errores**: `400 TIPO_PLANTILLA_INVALIDO`

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

- **Roles**: **retirado en #179: este endpoint ya no existe** (la caché local SICE-TAC se reemplazó por la lectura directa de DWSice).
- **Body**: `multipart/form-data`, campo `archivo` **requerido** (§1.9).
- **→** `200 ImportResumenDto`
- **Errores**: `400 ARCHIVO_REQUERIDO` · `400 FORMATO_NO_SOPORTADO` · `400 EXCEL_INVALIDO` · `400 EXCEL_VACIO` · `400 EXCEL_COLUMNAS`

#### `GET /api/tarifas-sicetac/importaciones`
- **Roles**: Todos. **→** `200 ImportacionDto[]`

### 3.12 Métricas — `/api/metricas`

**Todo el controller**: `LiderProceso`, `AdminTarifas`, `Admin`.

#### `GET /api/metricas/cumplimiento`
Tarjetas de la cola y Cumplimiento SLA. S5-HU-21.1/#287: un **solo filtro `desde`/`hasta`**,
compartido con `kpis-piloto`.

| Query | | Tipo | Restricción |
|---|---|---|---|
| `desde` | *opcional* | fecha ISO | límite inferior, inclusivo |
| `hasta` | *opcional* | fecha ISO | límite superior, inclusivo |

Qué filtra cada cosa:

- **Pendientes, En costeo, Entregadas, Descartadas y Total** → solicitudes **creadas** en el
  rango (`Solicitud.CreatedAt`). Para *Descartadas* no existe fecha de descarte: se aproxima
  por la de creación.
- **Cumplimiento SLA** (`entregadasConDeadline`, `entregadasATiempo`, `cumplimientoSla`) →
  cotizaciones **entregadas** en el rango (`EntregadaAt`). Es el % de cotizaciones entregadas
  con `Deadline` informado que se entregaron a tiempo (`EntregadaAt <= Deadline`); la fórmula
  no cambió (no cuenta las vencidas sin entregar: aceptado por negocio). Por eso una solicitud
  creada antes del rango pero entregada dentro **sí** cuenta en el SLA y **no** en la tarjeta.
- **Licitaciones** (`licitacionesTotal`, `licitacionesPerdidas`, `porcentajeLicitacionesPerdidas`)
  → **sin filtro** (el Tablero ni siquiera las muestra).

Sin `desde`/`hasta` el resultado es idéntico al de antes (acumulado histórico). La respuesta
repite el filtro en `desde`/`hasta`.

- **→** `200 CumplimientoDto`

#### `GET /api/metricas/kpis-piloto`
Los 4 KPIs del piloto: tiempo de costeo del pliego, % de líneas auto-costeadas,
cotizaciones emitidas y líneas con flete manual.

| Query | | Tipo | Restricción |
|---|---|---|---|
| `desde`, `hasta` | *opcional* | fecha ISO | sin filtro, corre sobre todo el histórico. Filtran las sagas por `SolicitadoEn` |
| `tamano` | *opcional* | string | S5-HU-21.1/#287. `1-10` \| `11-100` \| `+100`, por total de líneas del pliego. Sin él, todos. Un `+` sin codificar llega como espacio y se acepta igual (`%2B100` es lo correcto) |

**Tiempo de costeo (S5-HU-21.1/#287)** — `pliegosResueltos`, `tiempoPromedioResolucionMinutos`,
`tiempoMedianoResolucionMinutos`, `pliegosBajo10Minutos`, `porcentajePliegosBajo10Minutos`
(≤ 10 min). Mide el ciclo **completo**, no cuánto tardó el robot en revisar todo:

- Pliego **100% automático** (saga `Completado`) → `TerminadoEn − SolicitadoEn`.
- Pliego **con intervención manual** (saga `Parcial`) → desde que se creó la solicitud hasta
  `Cotizacion.EntregadaAt`. Si todavía no se entregó, ese pliego **no entra** (mismo criterio
  que Cumplimiento SLA).
- El tiempo **no** es proporcional al número de líneas (se procesan en paralelo): por eso se
  corta por tamaño en vez de dividir por línea. El tramo `1-10` coincide con el umbral de
  costeo síncrono (`UmbralCosteoDiferido = 10`).
- Desde S5-HU-19.3 (`409 COTIZACION_CON_LINEAS_PENDIENTES`) `EntregadaAt` significa pliego completo. Las entregas anteriores a ese cambio
  pudieron salir con líneas pendientes y subestimar el tiempo.
- `tamano` solo recorta el tiempo de costeo; las otras tarjetas del endpoint no cambian.

- **→** `200 KpisPilotoDto`
- **Errores**: `400 TAMANO_INVALIDO`

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

### 3.14 Cubicación — `/api/cubicacion`

S5-HU-18.1/#273: consume el contrato nuevo del Cubicador
(`POST/GET /recomendaciones`, cubicador/CONTRATO.md §3.1) — reemplaza por
completo al viejo `POST /api/cubicacion` (cálculo sin persistencia, matrices
por unidad). **No hay convivencia de los dos contratos**: el Cubicador retiró
su `POST /api/cubicacion` en el mismo corte. Es la única llamada HTTP síncrona
de NEXO hacia el Cubicador — ver `ICubicacionRapidaPort` para el porqué.

#### `POST /api/cubicacion` — crear recomendación
Pide al Cubicador una recomendación de flota para una línea. A diferencia del
contrato viejo, **SÍ persiste** del lado del Cubicador (append-only: cada
corrida queda guardada, reconsultable con el GET de abajo).

- **Roles**: `Comercial`, `AdminBiblioteca`, `AdminTarifas`, `Admin` (#300 agregó `AdminTarifas`; #307: `AdminBiblioteca` conserva este permiso).
- `usuarioId` del contrato del Cubicador lo pone NEXO automáticamente (el id
  del usuario autenticado) — no es un campo del body.

| Body | | Tipo | Restricción |
|---|---|---|---|
| `idLineaSolicitud` | **requerido** | string | |
| `mercancias` | **requerido** | array | mínimo 1 |
| `claseViajeSeleccionada` | *opcional* | string | preferencia de clase de viaje |

| Cada mercancía | | Tipo | Restricción |
|---|---|---|---|
| `codigoMercancia` | **requerido** | string | el `codigoMercancia` (NO el id) de `GET /tipos-mercancia` del Cubicador. S5-HU-17.7: antes era `idTipoMercancia`. Se acepta en cualquier caja |
| `cantidad` | **requerido** | int | ≥ 1 |
| `largoCm`, `anchoCm`, `altoCm`, `pesoKg` | **requerido** | decimal | > 0, por unidad |

- **→** `200 RecomendacionDto` (ver §2, arriba). `resultado: "SIN_SELECCION_AUTOMATICA"`,
  `condicionada: true` y `extradimension` no-null son RESULTADOS normales con
  200, no errores.
- **Errores**: `400 MERCANCIAS_REQUERIDAS` · `400 MERCANCIA_DESCONOCIDA` ·
  `400 MERCANCIA_NO_CUBICABLE` · `400 DIMENSIONES_INVALIDAS` ·
  `400 CUBICACION_DEMASIADO_GRANDE` (> 200 posiciones o > 10.000 unidades) ·
  `503 CUBICACION_RAPIDA_NO_CONFIGURADA` · `503 CUBICADOR_NO_DISPONIBLE` ·
  `503 CUBICADOR_RESPUESTA_VACIA` · `503 CUBICADOR_CREDENCIAL_INVALIDA` ·
  `503 CUBICADOR_ERROR`. Los códigos 400 son los que manda el Cubicador tal
  cual (catálogo existente, sección 2.6 de su hoja de ruta) — NEXO no los
  reemplaza por uno propio.

#### `GET /api/cubicacion` — reconsultar
La última recomendación de una línea.

- **Roles**: `Comercial`, `AdminBiblioteca`, `AdminTarifas`, `Admin` (#300, #307).

| Query | | Tipo | Restricción |
|---|---|---|---|
| `idLineaSolicitud` | **requerido** | string | |
| `origenSistema` | **requerido** | string | `NEXO` \| `COTIFLY`. NO se infiere — un mismo código de línea puede existir en los dos sistemas, como rutas distintas |

- **→** `200 RecomendacionDto`
- **Errores**: `400 PARAMETROS_REQUERIDOS` · **`404 RECOMENDACION_NO_ENCONTRADA`** (S6-HU-29.2/#312: el Cubicador no tiene una
  recomendación para esa línea; antes salía como `503 CUBICADOR_ERROR` y el front no podía distinguir "todavía no hay" de una caída) ·
  mismos 503 que el POST. El `503` queda solo para fallas reales del Cubicador.

#### `GET /api/lineas-solicitud/{id}/cubicacion` — solo servicio a servicio
- **Auth**: `Authorization: Bearer <clave de servicio>` (§1.2). No acepta la
  identidad del portal. **No es para el frontend.**
- **→** `200 LineaParaCubicacionDto` — `{ lineaId, posiciones[], origen, destino }`; cada posición es la `PosicionCargaDto` de la captura, con su
  `codigoTipoMercancia` (null si se capturó sin tipo). El Cubicador lo lee por ese nombre; renombrarlo rompe la creación del plan 3D desde la línea.
- **Errores**: `401 NO_AUTORIZADO` · `404 LINEA_NO_ENCONTRADA`

### 3.14.1 Passthrough de catálogo del Cubicador

S5-HU-18.6/#278: seis controllers delgados que espejan los seis CRUD de
catálogo del Cubicador (cubicador/CONTRATO.md §3.2) y aplican el rol — el
portal nunca llama directo al Cubicador (no hay CORS, la llave terminaría en
el bundle, y ese Bearer autoriza un backend, no una persona). **No es un
proxy genérico** (`/{*path}`): cada ruta/verbo se declara a mano, así se
puede aplicar rol por endpoint y el contrato queda visible en Swagger.

Diseño deliberadamente "tonto": el `body` se reenvía tal cual (sin DTOs
tipados campo a campo — NEXO no necesita entender el contenido, solo
reenviarlo), salvo que SIEMPRE se le estampa `usuarioId` con el id del
usuario **autenticado**, nunca el que mande el cliente en el body.

| Método | Ruta en NEXO | Ruta en el Cubicador |
|---|---|---|
| `GET` `PUT` | `/api/parametros-motor`, `/api/parametros-motor/{clave}` | `/parametros-motor` |
| `GET` `POST` `PUT` `DELETE` | `/api/categorias-mercancia[/{id}]` | `/categorias-mercancia` |
| `GET` `POST` `PUT` `DELETE` `POST .../republicar-catalogo` | `/api/tipos-carroceria[/{id}]` | `/tipos-carroceria` |
| `GET` `POST` `PUT` `DELETE` `POST .../republicar-catalogo` | `/api/tipos-mercancia[/{id}]` | `/tipos-mercancia` |
| `GET` `POST` `PUT` `DELETE` `POST .../republicar-catalogo` | `/api/vehiculos-catalogo[/{id}]` | `/vehiculos-catalogo` |
| `GET` `POST` `DELETE` (sin `PUT`) | `/api/reglas-incompatibilidad[/{id}]` | `/reglas-incompatibilidad` |

- **Roles de lectura** (`GET`): `[Authorize]` sin rol específico — mismo
  criterio que el resto de las lecturas propias de NEXO.
- **Roles de escritura** (`POST`/`PUT`/`DELETE`): `AdminTarifas`, `Admin` —
  no existe `AdminFletes`; `AdminTarifas` ya administra Reglas Solo-SICETAC y
  Tarifas de Plaza, encaja con este catálogo.
- **`DELETE` lleva body** (`{ version, usuarioId }`) — igual de destructivo
  pisar una fila que borrar una que cambió desde que se leyó. `HttpClient` no
  manda body en `DeleteAsync`; el adaptador arma el `HttpRequestMessage` a
  mano.
- **Concurrencia optimista**: todo `GET` devuelve `version`; `PUT`/`DELETE`
  la exigen. Si cambió, el Cubicador responde `409 CONFLICTO_DE_VERSION` con
  un mensaje ya pensado para mostrarse al usuario del portal tal cual
  ("El registro cambió desde que se cargó. Volver a leerlo y reintentar.") —
  NEXO lo propaga sin tocarlo.
- **Errores**: el código REAL que manda el Cubicador se propaga tal cual
  (catálogo completo en cubicador/CONTRATO.md §4), más los `503` propios del
  adaptador: `CATALOGO_CUBICADOR_NO_CONFIGURADO` · `CUBICADOR_NO_DISPONIBLE` ·
  `CUBICADOR_CREDENCIAL_INVALIDA` · `CUBICADOR_ERROR`.
- Shape exacto de cada catálogo (campos de creación/edición, cuáles son
  obligatorios): `cubicador/docs/openapi.json` y cubicador/CONTRATO.md §3.2 —
  NEXO no los redefine, los reenvía.

### 3.15 Plan de cargue (cubicación 3D) — `/api/planes-cargue`

Todas las escrituras van por cola hacia el Cubicador y responden **202**. Todas
las lecturas van contra la proyección local de NEXO; ninguna llama al Cubicador.

- **Roles de escritura**: `Comercial`, `AdminTarifas`, `Admin` (#310: se agregó `AdminTarifas`; #307: `AdminBiblioteca` ya no está).
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
  `GET /api/planes-cargue/por-linea/{lineaId}/estado` (#304: dice si el Cubicador rechazó el pedido; la lista de
  `.../por-linea/{lineaId}` no puede decirlo). Volver a pedir el plan borra el rechazo del intento anterior.
- **Errores**: `404 LINEA_SOLICITUD_NO_ENCONTRADA` · `400 PESO_NO_DECLARADO` (#304)
- **`PESO_NO_DECLARADO` (#304)**: el puente del Cubicador ya no inventa 1 kg para una posición sin peso (el peso decide la
  capacidad del vehículo). NEXO lo valida **antes de encolar**, con el mismo código y mensaje del Cubicador, y responde `400`
  con las posiciones que faltan — sin publicar nada. El cero cuenta como no declarado. Antes ese rechazo nacía en el worker
  después del `202` y solo quedaba en el log (el front sondeaba para siempre). Se alcanza solo con líneas capturadas antes
  de #304 (la captura ya exige `pesoKg`) o creadas por otro camino.

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
| `GET /api/planes-cargue/por-linea/{lineaId}` | — | `200 PlanFlotaItemDto[]` (vacío mientras el Cubicador procesa; nunca 404). **No avisa un rechazo**: vacío es "procesando" o "rechazado" — usar `/estado` | — |
| `GET /api/planes-cargue/por-linea/{lineaId}/estado` | — | `200 EstadoCubicacionLineaDto` (#304): `procesando` · `listo` · `rechazado` con `codigo`/`mensaje` del Cubicador; nunca 404 | — |
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
- **Roles**: `Comercial`, `AdminTarifas`, `Admin` (#310 agregó `AdminTarifas`; #307 quitó `AdminBiblioteca`). `DELETE` sigue solo para `Admin`.

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

### 3.19 Catálogos — `/api/catalogos`

Catálogos oficiales de solo lectura (S4-HU-09/S4-HU-10) para los desplegables
de Reglas Solo-SICETAC y Tarifas de Plaza — reemplazan el texto libre.
**Los catálogos de solo lectura**: los 6 roles (solo requiere sesión válida). Los alias y la bandeja de vehículos están más abajo.

| Endpoint | Fuente | → |
|---|---|---|
| `GET /api/catalogos/tipos-vehiculo` | `tblSAPTiposVehiculoTarifario`, tal cual (S5-HU-22.1/#288: se eliminaron los 5 casos especiales; `[]` si dbSolucionesTI no está disponible; faltan 3S2 y 2L3, decisión de negocio) | `200 TipoVehiculoDto[]` |
| `GET /api/catalogos/lineas-negocio` | `tblSAPLineaNegocio` | `200 LineaNegocioDto[]` |
| `GET /api/catalogos/clases-viaje` | `tblSAPClaseViaje` | `200 ClaseViajeDto[]` |
| `GET /api/catalogos/clases-contenedor` | `tblSAPClaseContenedor` | `200 ClaseContenedorDto[]` |
| `GET /api/catalogos/municipios` | Catálogo propio de municipios (`tbl_municipios`) | `200 MunicipioDto[]` |

`GET /api/catalogos/municipios` (S4-HU-10.2/#242): resuelve `origenMunicipioId`/
`destinoMunicipioId` para el formulario de Tarifas de Plaza (§3.21) — es el
mismo id que trae `TarifaPlazaDto.origenMunicipioId`/`destinoMunicipioId`.
~3.800 filas sin paginar ni filtrar: se carga una sola vez y queda en
memoria del front, no se consulta en cada tecleo. Mismo catálogo que
`GET /api/captura-solicitudes/municipios` (§3.3), expuesto acá también
porque ese otro controller es solo `Comercial`/`Admin` y `AdminTarifas`
también lo necesita.

#### Diccionario de alias — S6-HU-27.2/#321

Una sola tabla (`tbl_alias_elementos`) para dos catálogos, distinguidos por `tipoElemento`: `Vehiculo` (código = `2L2`, `C2`, `C3`, `2S2`, `3S3`…) y
`Contenedor` (código = `CNT40.HC`, `CNT20.ST`…). Un alias es un texto libre que la gente escribe (`TB`, `T.B`, `Turbo`, `40HC`) y su código oficial. El texto
se compara **normalizado** (sin tildes, mayúsculas, `_` y `-` como espacio, espacios colapsados) y es **único en toda la tabla**.

| Endpoint | Roles | → |
|---|---|---|
| `GET /api/catalogos/alias?tipo=vehiculo\|contenedor` | todos (sin `tipo`, los dos) | `200 AliasDto[]` · `400 TIPO_ALIAS_INVALIDO` |
| `GET /api/catalogos/alias/resolver?tipo=&texto=` | todos | `200 ResolverAliasDto` · `400 TEXTO_REQUERIDO` · `400 TIPO_ALIAS_INVALIDO` |
| `POST /api/catalogos/alias` | `AdminTarifas`, `Admin` | `201 AliasDto` |

```
interface AliasDto { id: UUID; alias: string; aliasNormalizado: string; tipoElemento: "Vehiculo" | "Contenedor"; codigo: string; activo: boolean }
interface ResolverAliasDto { texto: string; tipoElemento: string; reconocido: boolean; codigo: string | null; alias: string | null }
```

- `resolver` (`tipo` = `vehiculo` por defecto): responde `reconocido: false` para un texto que el diccionario no conoce. Usa el mismo normalizador y diccionario que la
  captura, así que la respuesta es lo que va a pasar al capturar (Cotifly la usa al importar su Excel de licitación). Solo cuentan los alias activos del tipo pedido.
- `POST` body: `{ tipo: "vehiculo" | "contenedor", alias: string (máx. 100), codigo: string (máx. 20) }`. El código tiene que ser oficial: uno que el diccionario ya usa
  para ese tipo o del catálogo (`tblSAPTiposVehiculoTarifario` para vehículos; las clases de contenedor de SAP). **Errores**: `400 CODIGO_NO_RECONOCIDO` ·
  `400 ALIAS_REQUERIDO` · `400 TIPO_ALIAS_INVALIDO` · `409 ALIAS_DUPLICADO` (el texto normalizado ya existe, en cualquier tipo).
- En la captura: el `tipoVehiculo` resuelve por alias (el código es lo que se guarda y la fila nace **oficial**); la `carroceria` no pasa por el diccionario;
  la clase de contenedor resuelve por alias y, si no, como antes (código real de SAP dentro del texto, o texto libre ≤ 20). Un vehículo desconocido sigue yendo a cuarentena.

#### Bandeja de normalización de vehículos — S6-HU-27.3/#322

Las configuraciones de vehículo que la captura creó en **cuarentena** (`EsCatalogoOficial = false`) porque nadie sabía a qué código corresponden. Un humano las
**mapea** a un código (queda un alias, así el mismo texto no vuelve a caer en cuarentena) o las **descarta**. Ambas salen de la lista; ninguna borra la fila
(las líneas que ya la usan siguen apuntándole; se cambian con `PUT .../lineas/{id}/vehiculo`, §3.4). **Roles**: `AdminTarifas`, `Admin` (todo).

| Endpoint | → |
|---|---|
| `GET /api/catalogos/vehiculos-cuarentena` | `200 VehiculoCuarentenaDto[]` `{ id, tipoVehiculo, carroceria, rangoPeso, createdAt }` — sin las descartadas ni las de un tipo que ya tiene alias activo |
| `POST /api/catalogos/vehiculos-cuarentena/{id}/mapear` body `{ codigo }` | `200 { id, tipoVehiculo, carroceria, codigo, alias, aliasCreado }` |
| `POST /api/catalogos/vehiculos-cuarentena/{id}/descartar` | `204` |

- `mapear` crea el alias del **tipo de vehículo** de la fila (la carrocería no entra). Si ese texto ya apuntaba al mismo código no crea otro (`aliasCreado: false`).
  **Errores**: `400 CODIGO_NO_RECONOCIDO` · `404 VEHICULO_NO_ENCONTRADO` · `409 VEHICULO_NO_ESTA_EN_CUARENTENA` (ya es oficial) · `409 VEHICULO_YA_DESCARTADO` · `409 ALIAS_DUPLICADO` (el texto ya apunta a otro código).
- `descartar` marca `Descartada = true` (columna nueva de `tbl_configuraciones_vehiculo`). **Errores**: `404 VEHICULO_NO_ENCONTRADO` · `409 VEHICULO_NO_ESTA_EN_CUARENTENA` · `409 VEHICULO_YA_DESCARTADO`.

### 3.20 Rangos de peso — `/api/pesos`

Bandas de peso (kg) propias de Nexo para Tarifas de Plaza (S4-HU-10.1) — **no
es un catálogo cerrado**, se pueden agregar bandas nuevas con el tiempo.
`pesoMaximo = null` es una banda abierta (p. ej. "+10500 kg").
**Todo el controller**: `AdminTarifas`, `Admin`.

#### `GET /api/pesos`
- **→** `200 RangoPesoDto[]`

#### `POST /api/pesos` — crear banda

| Body | | Tipo | Restricción |
|---|---|---|---|
| `pesoMinimo` | **requerido** | int | ≥ 0 |
| `pesoMaximo` | *opcional* | int | > `pesoMinimo` si viene; `null` = banda abierta |

- **→** `201 RangoPesoDto`, header `Location: /api/pesos/{id}`
- **Errores**: `400 RANGO_PESO_INVALIDO` (mínimo negativo, o máximo ≤ mínimo) · `409 RANGO_PESO_DUPLICADO`

#### `PUT /api/pesos/{id}` — editar banda
Mismo body y errores que crear, más `404 RANGO_PESO_NO_ENCONTRADO`.
- **→** `200 RangoPesoDto`

#### `DELETE /api/pesos/{id}`
- **→** `204`
- **Errores**: `404 RANGO_PESO_NO_ENCONTRADO`

### 3.21 Tarifas de plaza — `/api/tarifas-plaza`

CRUD de tarifas de plaza gobernadas por Nexo (S4-HU-10.2) — recrea el
formulario de captura que hoy vive en un sistema externo, con carga/descarga
masiva por Excel. `zonaOrigen`/`zonaDestino` se resuelven solos desde el
catálogo de municipios al elegir `origenMunicipioId`/`destinoMunicipioId`.
Al menos uno de `pesoId`/`tipoVehiculo` es obligatorio — **no son mutuamente
excluyentes**: las tarifas de contenedor traen ambos (el tipo de vehículo se
deriva de la banda de peso), las nacionales solo `tipoVehiculo`.
**Todo el controller**: `AdminTarifas`, `Admin`.

#### `GET /api/tarifas-plaza`
- **→** `200 TarifaPlazaDto[]`

#### `POST /api/tarifas-plaza` — crear

| Body | | Tipo | Restricción |
|---|---|---|---|
| `lineaNegocio` | **requerido** | string | debe existir en el catálogo real (§3.19) |
| `claseViaje` | **requerido** | string | debe existir en el catálogo real (§3.19) |
| `origenMunicipioId` | **requerido** | int | debe existir |
| `destinoMunicipioId` | **requerido** | int | debe existir |
| `claseContenedor` | *opcional* | string | |
| `pesoId` | *opcional* | int | debe existir en `/api/pesos`; ver regla de arriba |
| `tipoVehiculo` | *opcional* | string | ver regla de arriba |
| `valor` | **requerido** | decimal | > 0 |
| `vigencia` | **requerido** | fecha | |
| `detallesTarifa` | *opcional* | string | máx. 2000. S6-HU-29.2/#312: vacío o null se guarda como texto vacío (antes `400 TARIFA_PLAZA_INVALIDA`). Al guardar como plaza desde la Mesa no hay un detalle que escribir; **la pantalla de Tarifas Plaza lo sigue exigiendo** (lo hace el front) y la carga por Excel también |

- **→** `201 TarifaPlazaDto`, header `Location: /api/tarifas-plaza/{id}`
- **Errores**: `400 TARIFA_PLAZA_INVALIDA` · `400 MUNICIPIO_INVALIDO` · `400 RANGO_PESO_INVALIDO` · `409 TARIFA_PLAZA_DUPLICADA` (misma combinación exacta de las 7 dimensiones)

#### `PUT /api/tarifas-plaza/{id}` — editar
Mismo body y errores que crear, más `404 TARIFA_PLAZA_NO_ENCONTRADA`. Editar
`valor`/`vigencia`/`detallesTarifa` archiva el valor anterior en el
histórico — nada especial que hacer del lado del cliente.
- **→** `200 TarifaPlazaDto`

#### `DELETE /api/tarifas-plaza/{id}`
- **→** `204`
- **Errores**: `404 TARIFA_PLAZA_NO_ENCONTRADA`

#### `POST /api/tarifas-plaza/importar-excel`
Upsert por línea de negocio + clase de viaje + origen + destino + clase de
contenedor + rango de peso + tipo de vehículo (las 7 dimensiones).

Columnas obligatorias: `LINEA DE NEGOCIO`, `CLASE DE VIAJE`, `ORIGEN`, `DESTINO`, `VALOR`,
`VIGENCIA`, `DETALLES`. Opcionales: `CLASE CONTENEDOR`, `RANGO PESO`, `TIPO VEHICULO` y,
desde S5-HU-22.1/#288, `DEPARTAMENTO ORIGEN` y `DEPARTAMENTO DESTINO`.

**`VIGENCIA` (S5-HU-22.1/#288).** Se lee sin depender de la cultura del servidor. Se acepta, en
este orden: fecha nativa de Excel; **serial de Excel** (una fecha en una celda con formato
*General* es el número de días desde 1899-12-30: `46315` = `2026-10-20`; solo se acepta entre
1970 y 2099); texto `dd/MM/yyyy`, `d/M/yyyy`, `yyyy-MM-dd`, `MM/dd/yyyy` (ante `05/10/2026`
gana día/mes: 5 de octubre) y, al final, `es-CO`. El error `'…' no es una fecha de vigencia
válida` solo sale cuando de verdad no es una fecha reconocible. Regla para todo importador
nuevo con fecha: usar `clsLectorFechasExcel`.

**`DEPARTAMENTO ORIGEN` / `DEPARTAMENTO DESTINO`.** 68 nombres de municipios colombianos se
repiten entre departamentos (`ARMENIA` en Antioquia y Quindío). Con la columna llena, el
municipio se busca por **nombre + departamento** (ambos sin tildes ni mayúsculas); si el par no
existe, la fila se rechaza con `'Armenia' no existe en el departamento 'X'`. Con la columna
ausente o la celda vacía, igual que antes: el primero de la lista (Colombia primero, y a
igualdad el de menor `id`). `exportar-excel` ya las trae para que exportar → reimportar no
mueva una ciudad repetida al primer departamento.

- **Body**: `multipart/form-data`, campo `archivo` (§1.9), tope 10 MB.
- **→** `200 ImportarTarifasPlazaResultDto`
- **Errores**: `400 ARCHIVO_REQUERIDO` · `400 FORMATO_NO_SOPORTADO` (solo `.xlsx`/`.xlsm`) ·
  `400 EXCEL_INVALIDO` · `400 EXCEL_COLUMNAS`. Los errores por fila van en `errores` (máx. 20).

#### `GET /api/tarifas-plaza/exportar-excel`
Descarga el catálogo completo — el mismo archivo sirve de plantilla para
`importar-excel`. Columnas: `LINEA DE NEGOCIO`, `CLASE DE VIAJE`, `ORIGEN`, `DESTINO`,
`CLASE CONTENEDOR`, `RANGO PESO`, `TIPO VEHICULO`, `VALOR`, `VIGENCIA`, `DETALLES` y, desde
S5-HU-22.1/#288, `DEPARTAMENTO ORIGEN` y `DEPARTAMENTO DESTINO` (al final).
- **→** `200`, archivo `.xlsx` (`Content-Type` de Excel)

---

### 3.22 Uso y gasto de la IA — `/api/ia/uso`

Cada llamada de NEXO a un modelo de lenguaje (Claude) queda registrada en `tbl_ia_uso` —función, usuario, modelo, tokens de
entrada, salida y caché, costo estimado, latencia y resultado— **sin guardar el contenido enviado ni la respuesta**. Las funciones
de IA que se construyan sobre esto no exponen la llave ni llaman al proveedor desde el front: llama NEXO.

**Tope diario por persona.** Cada persona puede consumir un máximo de tokens por día (`Anthropic__LimiteTokensPorUsuarioDia`, por
defecto 500 000; `0` lo desactiva). El día es el de Colombia (medianoche en UTC-5) y el tope es blando: se verifica antes de llamar
con lo ya registrado. Al superarlo, la función responde `429 IA_TOPE_DIARIO` con
`details: { limiteTokensPorDia, tokensConsumidosHoy, reiniciaEn }`.

**Interruptores.** `Anthropic__Habilitada=false` apaga toda la IA (`503 IA_APAGADA`); sin llave responde `503 IA_NO_CONFIGURADA`; y cada
función se enciende por separado (`503 IA_FUNCION_DESHABILITADA` si está apagada). Los demás `503` del proveedor son
`IA_TIEMPO_AGOTADO`, `IA_LIMITE_PROVEEDOR`, `IA_NO_DISPONIBLE`, `IA_CREDENCIAL_INVALIDA`, `IA_PETICION_RECHAZADA`, `IA_RECHAZADA`,
`IA_RESPUESTA_TRUNCADA` e `IA_RESPUESTA_INVALIDA`.

#### `GET /api/ia/uso/gasto`
Gasto por mes y por función, del mes más reciente al más antiguo.
- **Roles**: Admin.

| Query | | Tipo | Restricción |
|---|---|---|---|
| `desde` | opcional | fecha-hora | Por defecto, el primer día del mes de hace cinco meses (seis meses con el actual) |
| `hasta` | opcional | fecha-hora | Por defecto, ahora. Debe ser posterior a `desde` |
| `funcion` | opcional | string | `ImportacionExcel`, `AsistenteCotizacion` o `ApoyoCubicador` |

- **→** `200 [ GastoIaDto ]`
```ts
interface GastoIaDto {
  mes: string                       // AAAA-MM, mes calendario en hora de Colombia
  funcion: string
  llamadas: number
  errores: number                   // llamadas cuyo resultado no fue "ok"
  tokensEntrada: number; tokensSalida: number
  tokensCacheLectura: number; tokensCacheEscritura: number
  costoEstimadoUsd: Decimal         // precios de lista; suma solo las llamadas con precio conocido
  llamadasSinCosto: number          // llamadas de un modelo sin precio conocido (no están en el costo)
  latenciaPromedioMs: number
}
```
- **Errores**: `400 RANGO_INVALIDO` · `400 FUNCION_IA_INVALIDA`

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

| Operación | Com | AdmBib | AdmTar | ServLog | Líder | Admin |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| `POST /api/captura-solicitudes/analizar-excel` · `POST /api/captura-solicitudes` · `GET aprovisionamiento` · `GET clientes` | ✅ | | | | | ✅ |
| `GET /api/captura-solicitudes/ciudades` · `departamentos` · `municipios` (S5-HU-22.1: cualquier rol válido) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Bandeja y Mesa**: `GET /api/solicitudes` · `GET /api/solicitudes/{id}` · `GET .../propuesta-tarifa` · `/hubs/bandeja` (#307) | ✅ | | ✅ | ✅ | ✅ | ✅ |
| `POST /api/solicitudes` (#307, D5: solo Admin) | | | | | | ✅ |
| `POST /api/solicitudes/resolver-catalogos` | ✅ | | | | | ✅ |
| `POST /api/solicitudes/{id}/lineas:bulk` (#307, D5: solo Admin) | | | | | | ✅ |
| `POST /api/solicitudes/{id}/rechazar` | ✅ | | | | | ✅ |
| `POST /api/solicitudes/{id}/recalcular-score` (#307, D5: solo Admin) | | | | | | ✅ |
| `POST /api/cubicacion` · `GET /api/cubicacion` (#300 + AdminTarifas; #307: AdminBiblioteca lo conserva) | ✅ | ✅ | ✅ | | | ✅ |
| **Cotizaciones, lecturas**: `GET /api/cotizaciones/{id}` · `por-solicitud/{id}` · `exportar-excel` (#307) | ✅ | | ✅ | ✅ | ✅ | ✅ |
| `POST /api/cotizaciones` · `aplicar-propuestas` | ✅ | | ✅ | | | ✅ |
| `POST /api/cotizaciones/{id}/resultado` | ✅ | | | | | ✅ |
| `POST /api/cotizaciones/{id}/lineas` · `PUT .../lineas/{lineaSolicitudId}` · `entregar` | | | ✅ | | | ✅ |
| `POST/DELETE /api/cotizaciones/{id}/lineas/{lineaSolicitudId}/rechazo` (S6-HU-24.3/#320) · `PUT .../lineas/{lineaSolicitudId}/vehiculo` (S6-HU-27.3/#322) | | | ✅ | | | ✅ |
| `POST/DELETE /api/cotizaciones/{id}/conexos` (S5-HU-20.1: + AdminTarifas) | ✅ | | ✅ | | | ✅ |
| `PATCH /api/cotizaciones/{id}/conexos/{conexoId}` | ✅ | | | | | ✅ |
| `GET /api/catalogos/alias` · `GET /api/catalogos/alias/resolver` (S6-HU-27.2/#321: cualquier rol válido) | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `POST /api/catalogos/alias` (#321) · bandeja `/api/catalogos/vehiculos-cuarentena` (listar, `mapear`, `descartar`) (#322) | | | ✅ | | | ✅ |
| Vigencia plaza SAP (`PATCH`, `POST masivo`, `DELETE`) | | ✅ | ✅ | | | ✅ |
| `/api/reglas-solo-sicetac` (todo, incluido `GET` y `exportar-excel`) (#307: + AdminBiblioteca) | | ✅ | ✅ | | | ✅ |
| `POST/PATCH /api/servicios-conexos`, `importar-excel` | | | ✅ | ✅ | | ✅ |
| `/api/metricas/*` | | | ✅ | | ✅ | ✅ |
| `/api/priorizacion/parametros` (`GET` y `PUT`) | | | | | | ✅ |
| Escrituras de `/api/planes-cargue` (9 endpoints) (#310: + AdminTarifas; #307: sin AdminBiblioteca) | ✅ | | ✅ | | | ✅ |
| `POST /api/vehiculos` (#310: + AdminTarifas; #307: sin AdminBiblioteca) | ✅ | | ✅ | | | ✅ |
| `DELETE /api/vehiculos/{id}` | | | | | | ✅ |
| `/api/pesos` (todo, incluido `GET`) (#307: + AdminBiblioteca) | | ✅ | ✅ | | | ✅ |
| `/api/tarifas-plaza` (todo, incluido `GET`) (#307: + AdminBiblioteca) | | ✅ | ✅ | | | ✅ |
| Escrituras de los 6 passthrough de catálogo (§3.14.1) | | | ✅ | | | ✅ |

---

## 6. Enums

Viajan como el string exacto (ver [§1.6](#16-enums-se-mandan-y-devuelven-como-texto)).

```
RolUsuario            AdminBiblioteca · LiderProceso · ServiciosLogisticos · Admin · Comercial · AdminTarifas
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
