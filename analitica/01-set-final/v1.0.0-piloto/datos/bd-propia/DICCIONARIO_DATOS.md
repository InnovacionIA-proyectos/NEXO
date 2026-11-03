# Diccionario de datos — set analítico de Nexo (S2-HU-10.1, issue #35)

Este documento describe las vistas de solo lectura del esquema `analitica`,
creadas por la migración
[`20260831194721_VistasAnaliticaSoloLectura`](../../../src/Nexo.Infrastructure/Persistence/Migrations/20260831194721_VistasAnaliticaSoloLectura.cs)
sobre la base transaccional `Nexo`.

## 1. Qué es esto y qué NO es

Es una **extracción puntual** ("foto única", alcance acordado con el equipo
de datos): vistas simples que envuelven un `SELECT`, sin agregaciones ni
transformaciones. **No es un Data Warehouse ni un cubo** — esa decisión se
toma después del análisis exploratorio (S2-HU-11), con evidencia de qué
grano y qué variable objetivo tiene sentido.

Por eso estas vistas están **deliberadamente sin unir entre sí**: cada una
respeta el grano de su tabla de origen. Unir `solicitudes` con
`cotizaciones` con `tarifas_historicas` en una sola tabla ancha impondría un
grano y ocultaría relaciones 1-a-N reales (una solicitud tiene varias
líneas; una cotización puede repetirse en el histórico si tiene varias
líneas ganadas). Esa decisión de diseño le corresponde al análisis
exploratorio, no a esta extracción.

## 2. Cómo consumir esto sin afectar la operación

- Las vistas usan `WITH (NOLOCK)` en cada tabla: aceptan lectura sucia
  (leer una fila que otra transacción está escribiendo en ese instante, algo
  raro en este volumen de escritura) a cambio de **no bloquear ni ser
  bloqueadas** por la operación normal del sistema — es apropiado para una
  extracción analítica puntual, nunca para una consulta transaccional.
- Aun así, **ejecutar la extracción real (el export a CSV/Parquet/lo que
  use el equipo de datos) en una ventana de bajo uso** — de noche o fin de
  semana. `NOLOCK` reduce el riesgo de bloqueo, no lo elimina en un table
  scan grande sobre una tabla con escritura activa.
- Estas vistas viven en el esquema `analitica`, separado de `dbo`, para que
  a la cuenta que use el equipo de datos se le pueda otorgar
  `GRANT SELECT ON SCHEMA::analitica` sin exponer el resto del esquema
  transaccional.
- Verificado en esta sesión: la migración se aplicó contra una instancia
  SQL Server real (Docker, no producción), las 13 vistas se crearon sin
  error, y cada una devolvió filas contra el dataset de demostración
  (`DataSeeder.cs`). **No verificado**: comportamiento contra el volumen y
  la concurrencia reales de producción — eso requiere ejecutarlo ahí, con
  acceso que esta sesión no tiene.

## 3. Minimización de datos (decisión deliberada)

Se excluyeron de las vistas, a propósito:

| Campo excluido | Tabla | Por qué |
|---|---|---|
| `Usuario.Nombre`, `Usuario.Email`, `Usuario.IdentidadPortalId` | `usuarios` | Datos personales del personal interno, sin valor para un modelo de predicción sobre cotizaciones/tarifas. Se conserva `Rol` (si es predictivo, quién crea/entrega una solicitud) |
| `Cliente.Telefonos` | `clientes` | Dato de contacto, sin valor predictivo |
| `Solicitud.CondicionesServicio`, `LineaSolicitud.Dimensiones` | `solicitudes`, `lineas_solicitud` | JSON de texto libre sin esquema estable — un dataset "limpio" no debe incluir texto no estructurado sin normalizar primero |
| `ServicioConexo.ReglaCobro` | `servicios_conexos` | Fórmula interna de cobro, detalle operativo |
| `AuditLog.Diff` | `audit_log` | JSON del antes/después del cambio: puede contener cualquier campo de negocio, incluidos los ya excluidos arriba |

Ningún campo de identificación de cliente final (persona natural) existe en
este sistema: `Cliente` es siempre una empresa (NIT), no una persona.

## 4. Vistas — una por entidad con valor analítico

### `analitica.solicitudes`

Cabecera de una solicitud de cotización (spot o licitación). **Grano: una fila por solicitud.**

| Columna | Tipo | Semántica | Origen |
|---|---|---|---|
| `Id` | uniqueidentifier | Identificador de la solicitud | Generado por el sistema |
| `ClienteId` | uniqueidentifier | FK a `analitica.clientes` | Captura manual o catálogo local/DW de clientes |
| `Tipo` | nvarchar(20) | `Spot` \| `Licitacion` | Captura manual |
| `Modalidad` | nvarchar(30) | `Importacion`\|`Exportacion`\|`Nacional`\|`Extradimensionada`\|`CargaGeneral` | Captura manual |
| `Deadline` | datetimeoffset | Fecha límite de respuesta (SLA) | Captura manual |
| `ProbabilidadCierre` | decimal(9,4) | 0–100, probabilidad estimada de ganar | Captura manual |
| `ValorEstimado` | decimal(18,2) | Valor económico estimado en COP | Captura manual |
| `ScorePrioridad` | decimal(6,2) | 0–100, score de priorización calculado | Calculado por el sistema (`ScorePrioridadCalculator`, función pura del dominio) |
| `Estado` | nvarchar(20) | `Pendiente`\|`EnCosteo`\|`Entregada`\|`Descartada` | Máquina de estados del sistema |
| `CreadoPor` | uniqueidentifier | FK a `analitica.usuarios` | Sesión del usuario que la creó |
| `CreatedAt`/`UpdatedAt` | datetimeoffset | Auditoría técnica de fila | Generado por el sistema |

### `analitica.lineas_solicitud`

Cada combinación a costear dentro de una solicitud (1 para spot, ~cientos
para un pliego de licitación). **Grano: una fila por línea.**

| Columna | Tipo | Semántica | Origen |
|---|---|---|---|
| `Id` | uniqueidentifier | Identificador de la línea | Generado por el sistema |
| `SolicitudId` | uniqueidentifier | FK a `analitica.solicitudes` | — |
| `RutaId` | uniqueidentifier, nullable | FK a `analitica.rutas` | Captura manual, cruzada contra el catálogo de rutas |
| `ConfigVehiculoId` | uniqueidentifier, nullable | FK a `analitica.configuraciones_vehiculo` | Captura manual |
| `Tipologia` | nvarchar, nullable | Tipo de carga (`Contenedor`, `GranelSolido`, …) | Captura manual |
| `LineaNegocio`, `ClaseViaje`, `ClaseContenedor` | nvarchar, nullable | Catálogo SAP TM local, alineado al formulario "Consultar Tarifa" | Captura manual, catálogo replicado de SAP TM |
| `Exclusivo`, `Consolidado`, `RetornaContenedor`, `LevanteContenedorDiferente` | bit | Banderas de condiciones del servicio | Captura manual |
| `ValorAprovisionamiento` | decimal(18,2), nullable | Costo de reposicionar el contenedor vacío | Calculado/propuesto por el motor de costeo |
| `Peso` | decimal(9,2), nullable | Peso en kg | Captura manual |
| `ValorMercancia` | decimal(18,2), nullable | Valor asegurado de la mercancía | Captura manual |
| `EstadoLinea` | nvarchar(20) | `PorCostear`\|`AutoResuelta`\|`Revisada` | Motor de auto-costeo |
| `CreatedAt`/`UpdatedAt` | datetimeoffset | Auditoría técnica | Sistema |

### `analitica.cotizaciones`

Cotización integral (flete + servicios conexos) para una solicitud.
**Grano: una fila por cotización** — una solicitud puede tener más de una
(reintentos, licitaciones con varias rondas).

| Columna | Tipo | Semántica | Origen |
|---|---|---|---|
| `Id` | uniqueidentifier | Identificador | Sistema |
| `SolicitudId` | uniqueidentifier | FK a `analitica.solicitudes` | — |
| `Estado` | nvarchar(20) | `Borrador`\|`Entregada` | Máquina de estados |
| `Resultado` | nvarchar(20) | `SinDefinir`\|`Ganada`\|`Perdida` — **candidato natural a variable objetivo**, pero esta extracción NO lo impone como tal | Registrado manualmente por el comercial tras la respuesta del cliente |
| `Total` | decimal(18,2) | Suma de líneas de flete + conexos | Calculado |
| `EntregadaPor` | uniqueidentifier, nullable | FK a `analitica.usuarios` | Sesión de quien entregó |
| `EntregadaAt` | datetimeoffset, nullable | Cuándo se entregó | Sistema |
| `CreatedAt`/`UpdatedAt` | datetimeoffset | Auditoría técnica | Sistema |

### `analitica.cotizacion_lineas`

**Grano: una fila por línea de flete cotizada.**

| Columna | Tipo | Semántica | Origen |
|---|---|---|---|
| `Id` | uniqueidentifier | Identificador | Sistema |
| `CotizacionId` | uniqueidentifier | FK a `analitica.cotizaciones` | — |
| `LineaSolicitudId` | uniqueidentifier, nullable | FK a `analitica.lineas_solicitud` | — |
| `ValorFlete` | decimal(18,2) | Valor del flete cotizado | Motor de propuesta o ajuste manual |
| `ValorAprovisionamiento`, `ValorRetornoContenedor` | decimal(18,2), nullable | Conceptos adicionales, no suman al flete base | Motor de propuesta |
| `Fuente` | nvarchar(20) | `Sicetac`\|`Historica`\|`Manual`\|`Tarifario` — **ES la trazabilidad de origen del valor cotizado**, exigida por el criterio de cierre | Decidido por `ReglaDeCosteo` (Domain) al momento de costear |
| `CreatedAt`/`UpdatedAt` | datetimeoffset | Auditoría técnica | Sistema |

### `analitica.cotizacion_conexos`

**Grano: una fila por servicio conexo aplicado a una cotización.**

| Columna | Tipo | Semántica | Origen |
|---|---|---|---|
| `Id` | uniqueidentifier | Identificador | Sistema |
| `CotizacionId` | uniqueidentifier | FK a `analitica.cotizaciones` | — |
| `ServicioConexoId` | uniqueidentifier, nullable | FK a `analitica.servicios_conexos` | — |
| `Cantidad` | decimal(12,2) | Unidades del servicio aplicadas | Captura manual |
| `ValorAplicado` | decimal(18,2) | Valor cobrado por ese servicio | Calculado desde la regla de cobro del catálogo |

### `analitica.tarifas_historicas`

Biblioteca de Tarifas Ganadas: tarifas que sí se cobraron en una cotización
ganada, reutilizables como referencia. **Grano: una fila por tarifa
archivada.**

| Columna | Tipo | Semántica | Origen |
|---|---|---|---|
| `Id` | uniqueidentifier | Identificador | Sistema |
| `RutaId`, `ConfigVehiculoId` | uniqueidentifier, nullable | FK a catálogos | — |
| `Tipologia` | nvarchar, nullable | Tipo de carga | — |
| `Valor` | decimal(18,2) | Tarifa que se cobró de verdad | Copiado de `cotizacion_lineas.ValorFlete` al marcar la cotización como Ganada |
| `Fecha` | date | Fecha de la tarifa | Sistema |
| `CotizacionId` | uniqueidentifier, nullable | FK a `analitica.cotizaciones` — la cotización ganada que originó este registro | — |

### `analitica.audit_log`

Registro de auditoría append-only. **Grano: un evento de auditoría.**

| Columna | Tipo | Semántica | Origen |
|---|---|---|---|
| `Id` | uniqueidentifier | Identificador del evento | Sistema |
| `UsuarioId` | uniqueidentifier, nullable | FK a `analitica.usuarios` — autor de la acción | Sesión del usuario (nulo en registros anteriores a S2-HU-03, cuando la autoría real todavía no estaba garantizada — ver `analitica.inconsistencias`) |
| `Entidad`, `EntidadId` | nvarchar / uniqueidentifier | Sobre qué entidad de negocio actuó | Sistema |
| `Accion` | nvarchar(50) | Qué se hizo (`entregar`, `cargar_lineas_bulk`, …) | Sistema |
| `CreatedAt` | datetimeoffset | Cuándo | Sistema |

### Catálogos / dimensiones: `analitica.clientes`, `analitica.rutas`, `analitica.configuraciones_vehiculo`, `analitica.servicios_conexos`, `analitica.usuarios`

Tablas de referencia para hacer `JOIN` desde las vistas de arriba. Columnas
listadas en la sección 3 (las excluidas) y en la migración; el resto son
descriptivas y no requieren explicación adicional (nombre, NIT, tipo de
vehículo, etc.).

## 5. `analitica.inconsistencias` — registros identificados, no descartados

Cumple el criterio "registros inconsistentes se identifican y documentan,
no se descartan en silencio". Cada fila es un hallazgo: qué entidad, qué
registro (`EntidadId`), y por qué se marca. **No filtra nada de las vistas
de arriba** — un registro inconsistente sigue apareciendo en su vista
normal, y ADEMÁS aparece acá con su motivo.

| Motivo | Qué detecta |
|---|---|
| Entregada sin `EntregadaAt` o `EntregadaPor` | Cotización marcada como entregada sin los metadatos de la entrega |
| Resultado definido sin haber sido entregada | `Resultado <> SinDefinir` pero `Estado <> Entregada` — secuencia de estados irregular |
| Entregada sin ninguna línea de flete | Cotización "vacía" marcada como entregada |
| `ValorFlete` en cero o negativo | Dato de costeo inválido que pasó la validación de captura |
| Tarifa histórica de una cotización no Ganada | Contradice el propósito de la tabla (solo tarifas de cotizaciones ganadas) |
| `Valor` en cero o negativo (histórico) | Tarifa archivada sin valor económico real |
| Línea sin ruta asignada | No debería poder costearse sin ruta |
| Peso nulo o en cero | Dato físico faltante para una línea de carga |
| `Deadline` anterior a `CreatedAt` | Solicitud creada con un plazo ya vencido — puede ser captura tardía o error de fecha |
| Auditoría sin usuario identificado | Acción sin autor real (esperable solo en registros previos a S2-HU-03) |

Esta lista es el punto de partida, no exhaustiva: si el análisis
exploratorio (S2-HU-11) encuentra otros patrones, se agregan como una
cláusula `UNION ALL` más — la vista está diseñada para crecer así.

## 6. Cómo producir el export real

Estas vistas están pensadas para que el equipo de datos las consulte
directo (con una cuenta de solo lectura sobre el esquema `analitica`) o
para exportarlas a archivo. Ejemplo con `sqlcmd` (ejecutar en ventana de
bajo uso, contra un usuario de solo lectura):

```powershell
sqlcmd -S <servidor> -d Nexo -U <usuario_solo_lectura> -P <clave> `
  -Q "SET NOCOUNT ON; SELECT * FROM analitica.cotizaciones" `
  -s "," -W -o cotizaciones.csv
```

Repetir por cada vista de la sección 4, y una vez más para
`analitica.inconsistencias`.
