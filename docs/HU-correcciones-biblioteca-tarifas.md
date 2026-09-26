# HU de corrección — Biblioteca de Tarifas, Costeo y Cubicación

Historias de usuario levantadas a partir de una lista de correcciones pedida
directamente por negocio (2026-09-24). Cada una se contrastó contra el código
real antes de redactarla — backend en este repo (`src/CabinaFletes.*`) y
frontend real del portal en `administracion_embarqa_nexo` (Angular).

> ⚠️ **Nota sobre el frontend:** existe una carpeta hermana
> `cabinaFletes/frontend` (React) que es un prototipo/referencia antigua, **no**
> el frontend en producción. Todas las verificaciones de este documento se
> hicieron contra `administracion_embarqa_nexo`, que es el repo real del
> portal.

> No se les asigna número de sprint (`S3-HU-XX`) — eso lo define el equipo al
> planearlas. HU-700 a HU-705 son los **épicos** (uno por punto del pedido
> original); cada uno se divide en **sub-HUs** (`HU-N.M`), pensadas para
> convertirse 1 a 1 en issues de GitHub — cada sub-HU tiene su propio
> responsable, repositorio y criterios de aceptación, siguiendo el mismo
> patrón ya usado en este proyecto (ej. Habilitador B8 = HU-10, con HU-10.1/
> 10.2/10.3 como issues reales `#35`/`#36`/`#37`). La última sub-HU de cada
> épico es siempre la verificación QA de punta a punta.

## Responsables y repositorio (resumen)

| HU | Backend | Frontend | Datos | Repositorio(s) |
|---|---|---|---|---|
| HU-700 — Catálogos Solo-SICETAC | Ismael | Juan Pablo | Sara | `NEXO` (backend) + `administracion_embarqa_nexo` (frontend) |
| HU-701 — CRUD Tarifas de Plaza | Ismael | Juan Pablo | Sara | `NEXO` + `administracion_embarqa_nexo` |
| HU-702 — Tarifas Ganadas / permisos | Ismael | Juan Pablo | — | `NEXO` + `administracion_embarqa_nexo` |
| HU-703 — Recomendación de vehículos | Brayan | Juan Pablo (si aplica pantalla nueva, por confirmar) | — | `NEXO` (carpeta `cubicador/`) + `administracion_embarqa_nexo` (si hay UI nueva) |
| HU-704 — Antigüedad Tarifas Históricas | Ismael | — | — | `NEXO` |
| HU-705 — Horas logísticas en propuesta SICE-TAC | Ismael | — | — | `NEXO` |

`NEXO` es este repositorio (backend .NET + el microservicio `cubicador/`).
`administracion_embarqa_nexo` es el repositorio del portal (frontend Angular
real). Para las tareas de **Datos** (catálogos sobre `dbSolucionesTI`/`DWSice`)
no aplica un repositorio de código — es trabajo directo sobre las bases
corporativas de solo lectura; si se documenta, iría en
`analitica/00-fuentes/fuentes-corporativas/README.md` de este repo, que ya
describe esas fuentes.

---

## HU-700 — Catálogos oficiales de ciudades y tipos de vehículo en Reglas Solo-SICETAC

**Como** Administrador de Tarifas, **quiero** crear y cargar masivamente reglas
Solo-SICETAC usando catálogos oficiales de ciudades y tipos de vehículo (no
campos de texto libre), **para** eliminar errores de digitación/duplicados por
variantes de escritura y alinear la regla con la llave real que usa el motor
de costeo.

**Responsables:** Backend — Ismael · Frontend — Juan Pablo · Datos — Sara
**Repositorio(s):** `NEXO` (backend) + `administracion_embarqa_nexo` (frontend)

### Estado actual (verificado)

- **Carga masiva por Excel — ya existe, pero sin formato estandarizado.**
  `POST /api/reglas-solo-sicetac/importar-excel`
  (`src/CabinaFletes.Api/ReglasSoloSicetac/ReglasSoloSicetacController.cs:45-51`).
  El parser (`ExcelReglasSoloSicetacAdapter.cs`) lee 3 columnas por
  **posición** (Origen, Destino, Tipo de vehículo), detecta el encabezado de
  forma heurística (busca "ORIGEN"/"DESTINO"/"VEHIC"/"TIPO" en la fila 1; si no
  los encuentra, asume que la fila 1 ya es dato) y no valida el tipo de
  vehículo contra ningún catálogo. No hay plantilla descargable ni reporte de
  errores por fila — solo contadores agregados
  (`leídas/creadas/duplicadas/inválidas`,
  `ImportarReglasSoloSicetacExcelCommand.cs`). El frontend ya tiene el botón
  "Cargar Excel" (`reglas-solo-sicetac-page.component.html:10-13`).
- **Campos de creación manual — hoy texto libre, confirmado en el frontend
  real:** Origen/Destino/Tipo de vehículo son `<input type="text">` sin
  ningún catálogo (`reglas-solo-sicetac-page.component.html:17-19`).
- **Catálogo de tipos de vehículo — el mapeo a la tabla oficial ya existe, falta
  exponerlo.** `SapTipoVehiculo` (Id, Codigo, Descripcion, Estado) ya está
  mapeado a `tblSAPTiposVehiculoTarifario` en
  `src/CabinaFletes.Infrastructure/External/SapCatalogos.cs` y expuesto como
  `DbSet<SapTipoVehiculo>` en `DbSolucionesTiDbContext.cs:22` — hoy solo se usa
  internamente en `SapHistoricoAdapter` para traducir códigos del histórico.
  No existe ningún `Query`/endpoint que lo exponga como catálogo GET.
- **Catálogo de ciudades — confirmado por negocio (2026-09-25): sí debe ser
  una tabla NUEVA, propia de Nexo, no solo el mecanismo en vivo que ya
  existe.** Hoy `tblSAPDANE` (`CentrosPobladosDane`) y
  `tblZonasTarificacionRelacion` (`ZonasTarificacion`) ya se consultan **en
  vivo** en `CiudadesDwAdapter.cs` (caché de 1h), reutilizando la misma regla
  ciudad→zona que usa el motor de costeo real (`ResolucionZonaSap.cs`,
  incluye alias curados a mano), pero solo dentro del módulo de Captura de
  Solicitudes (`GET /api/solicitudes/captura/departamentos` y
  `.../ciudades?departamento=&q=`, `CapturaSolicitudesController.cs:90-102`).
  Eso es un mecanismo de **resolución en vivo**, no una tabla propia. Lo que
  pide esta HU es una **tabla local nueva dentro de la base de Nexo**, con
  las columnas del DANE (código y nombre de municipio/departamento) más
  **nombre de ciudad** y **zona de tarificación** (`ZC_<ciudad>` /
  `ZTC_<zona>`) ya resuelta — es decir, el resultado ya aterrizado como
  catálogo propio, gobernado por Nexo, no una consulta en vivo a la BD
  corporativa en cada request.

### Sub-HUs (issues de GitHub)

#### HU-700.1 — Crear y poblar la tabla local de ciudades
**Responsable:** Sara (Datos) · **Repositorio:** `NEXO`

Crear en la BD de Nexo una tabla nueva de ciudades con: código DANE de
municipio, código DANE de departamento, nombre de municipio, nombre de
departamento, **nombre de ciudad** y **zona de tarificación**
(`ZC_<ciudad>`/`ZTC_<zona>`, ya resuelta). Poblarla a partir de `tblSAPDANE` +
`tblZonasTarificacionRelacion`, reutilizando la regla de resolución ya
existente (`ResolucionZonaSap.cs`, alias curados incluidos) como proceso de
siembra — y cruzarla/completarla con los códigos ZC/ZTC que aparecen
realmente como `Origen`/`Destino` en el Tarifario SAP
(`tblSAPTarifas`/`tblSAPTarifasLog`, `SapTarifaHistorico.cs:13-14`), porque la
relación oficial por sí sola puede no cubrir zonas reales (ya pasó con
`ZTC_NORTE_BOGOTA`, ver memoria EDA-2 notebook 05).

**Criterios de aceptación:**
- [ ] Existe la tabla nueva con las columnas indicadas, migrada por EF Core.
- [ ] Un comando/proceso de siembra la puebla desde `tblSAPDANE` +
      `tblZonasTarificacionRelacion`.
- [ ] La siembra queda cruzada contra los códigos ZC/ZTC reales del Tarifario
      SAP; cualquier código encontrado en el tarifario que no esté en la
      relación oficial queda señalado (no silenciosamente omitido) — según lo
      que resuelva la pregunta abierta correspondiente.
- [ ] Documentado si la siembra es única (foto fija) o repetible
      (sincronización periódica) — ver Preguntas abiertas.

#### HU-700.2 — Backend: catálogos, matching y carga masiva
**Responsable:** Ismael (Backend) · **Repositorio:** `NEXO`

Agrupa 3 piezas backend, todas sobre el mismo módulo (Reglas Solo-SICETAC):

1. **Endpoints GET de catálogos** — exponer `GET` de ciudades leyendo de la
   tabla local de HU-700.1 (no en vivo del DW), y `GET` de tipos de vehículo
   reutilizando `SapTipoVehiculo` (`tblSAPTiposVehiculoTarifario`, ya
   mapeado). Ambos reutilizables a futuro desde Captura de Solicitudes (ver
   Ajustes en consecuencia).
2. **Corregir el matching para que valide también el tipo de vehículo** —
   hallazgo de correctness ya documentado en `docs/hallazgos-requisitos.md`
   (A.6): `ReglaSoloSicetac.TipoVehiculo` se captura como obligatorio pero el
   matching real (`if (soloSice.Contains(Texto.ClaveRuta(origen, destino)))`)
   solo usa origen+destino, ignorándolo. Sin este fix, el desplegable de
   HU-700.3 sería cosmético.
3. **Mejorar el importador de Excel** — hoy existe carga masiva
   (`POST /api/reglas-solo-sicetac/importar-excel`) pero sin plantilla
   descargable, sin validación contra catálogo y sin reporte de errores por
   fila (solo contadores agregados).

**Criterios de aceptación:**
- [ ] `GET` de ciudades responde desde la tabla local nueva; `GET` de tipos
      de vehículo responde desde `SapTipoVehiculo`. Ambos genéricos, no
      acoplados solo a este módulo.
- [ ] El matching de Solo-SICETAC valida origen + destino + tipo de
      vehículo (ya no solo origen+destino).
- [ ] Existe una plantilla descargable con el formato exacto esperado; cada
      fila se valida contra los catálogos; el resultado de la importación
      reporta el error específico por fila.

#### HU-700.3 — Desplegables en el formulario de Reglas Solo-SICETAC
**Responsable:** Juan Pablo (Frontend) · **Repositorio:** `administracion_embarqa_nexo`

Reemplazar los 3 `<input type="text">` de
`reglas-solo-sicetac-page.component.html` (origen, destino, tipo de vehículo)
por desplegables poblados desde los endpoints de HU-700.2.

**Criterios de aceptación:**
- [ ] Ciudad (origen y destino) y tipo de vehículo se seleccionan desde
      desplegable, sin texto libre.
- [ ] El formulario impide enviar un valor que no exista en el catálogo.

#### HU-700.4 — QA: verificación end-to-end de HU-700
**Responsable:** QA · **Repositorio:** `NEXO` + `administracion_embarqa_nexo`

- Verificar que los desplegables cargan las opciones reales desde los nuevos
  endpoints y que no queda ningún campo de texto libre.
- Cargar el Excel con la plantilla nueva: fila válida se inserta; fila con
  tipo de vehículo inexistente se rechaza con error específico; fila
  duplicada se reporta como tal.
- Crear dos reglas con el mismo origen+destino pero distinto tipo de
  vehículo y confirmar que el auto-costeo ya distingue entre ellas.
- Regresión: reglas creadas antes del cambio siguen funcionando o quedan
  migradas/revalidadas, sin romper el auto-costeo de solicitudes existentes.

---

## HU-701 — CRUD de Tarifas de Plaza con catálogos, carga masiva y semáforo de vigencia

**Como** Administrador de Tarifas, **quiero** recrear en Nexo el formulario de
captura de tarifas de plaza que hoy vive en un sistema externo (ver captura
de pantalla aportada 2026-09-25) — mismos campos, con la única diferencia de
que el rango de peso ya sea un catálogo/desplegable en vez de un número
libre — con carga/descarga masiva por Excel y un semáforo que priorice las
tarifas próximas a vencer, **para** dejar de depender de ese sistema externo
y reducir error de captura.

### Campos del formulario a recrear (captura de pantalla, sistema externo)

| Campo | Ejemplo en la captura | Catálogo/fuente candidata en Nexo |
|---|---|---|
| Línea de negocio / tipo de oferta | "Oferta de Expedición Puerto a interior" | `SapLineaNegocio` (`tblSAPLineaNegocio`) — **ya mapeado y conectado**, solo falta exponer un `GET` (mismo patrón que HU-700.2) |
| Clase de viaje | "Contenedor" | `SapClaseViaje` (`tblSAPClaseViaje`) — **ya mapeado y conectado** |
| Origen (ciudad, resuelve zona) | "ZTC_CARTAGENA" | Catálogo de ciudades/zona de **HU-700.1** (la tabla local nueva) — reutilizar, no crear un segundo catálogo de ciudades |
| Destino (ciudad, resuelve zona) | "ZTC_VABURRA" | Mismo catálogo de HU-700.1 |
| Clase de Contenedor | "CNT20.IT" | `SapClaseContenedorCatalogo` (`tblSAPClaseContenedor`) — **ya mapeado y conectado** |
| Peso Inferior / Peso Superior | 14001 / 20000 | Candidato: `tblRangoPeso` (`SapRangoPeso`) — **por verificar** si sus rangos coinciden con los que usa este formulario, o si hace falta un catálogo de rangos propio para esta tarifa (ver Preguntas abiertas) |
| Valor | $5.400.000 | Campo numérico (COP) |
| Vigencia | 12/10/2026 | Selector de fecha |
| Detalles de la tarifa | Texto libre con condiciones/advertencias del negocio, **obligatorio** (marcado `*` en la captura) | Campo de texto libre, requerido |

Tres de los ocho campos (línea de negocio, clase de viaje, clase de
contenedor) ya tienen su catálogo real conectado en el backend — **no hay que
crear tablas nuevas para ellos**, solo exponerlos como `GET`, igual que se
hizo para tipos de vehículo en HU-700.2. Origen/Destino reutilizan la tabla de
ciudades/zona que crea HU-700.1 (misma necesidad, un solo catálogo, no
duplicarlo entre HU-700 y HU-701). Rango de peso es el único campo donde de
verdad falta confirmar la fuente exacta.

**Responsables:** Backend — Ismael · Frontend — Juan Pablo · Datos — Sara
**Repositorio(s):** `NEXO` (backend) + `administracion_embarqa_nexo` (frontend)

### ✅ Decisión de arquitectura (confirmada por negocio, 2026-09-25): alcance completo

**Las tarifas de plaza NO son una entidad propia de Nexo hoy.** Se leen en
vivo y **solo lectura** de `dbsolucionesTI.dbo.tblSAPTarifas` (integración
SAP TM, mantenida por otro equipo). Nexo únicamente persiste un *override
local de vigencia* (`AjustesVigenciaPlaza`) — no el resto de los campos de la
tarifa. Frente a las dos alternativas que se habían planteado:

1. ~~Alcance acotado: limitar el CRUD y la carga masiva a los campos que Nexo
   ya puede escribir (vigencia), sin tocar el resto de `tblSAPTarifas`.~~
2. **Alcance completo (decidido): introducir una tabla local editable de
   tarifas de plaza en Nexo** — una entidad propia, gobernada por Nexo, que
   almacena estos valores; no una copia de solo lectura de SAP ni un override
   sobre ella.

Consistente con la evidencia ya identificada: la captura de pantalla del
sistema externo a recrear incluye un campo **"Detalles de la tarifa"** (texto
libre, obligatorio, con condiciones de negocio como disponibilidad de
vehículo) que no existe en ninguna columna de `tblSAPTarifas` — confirma que
es una entidad propia separada, no un reflejo 1:1 de una fila de SAP. Ver
HU-701.1 para la tabla nueva. Sigue pendiente (no bloqueante, ver Preguntas
abiertas): confirmar el nombre/origen exacto del sistema externo y si tiene
datos existentes que deban migrarse.

### Estado actual (verificado)

- **Rango de peso — hoy es texto libre, no catálogo.**
  `SapTarifaBase.RangoPesoInf/RangoPesoSup` son `int?` (kg), espejo directo de
  las columnas SAP `RANGOPESOINF/RANGOPESOSUP`
  (`src/CabinaFletes.Infrastructure/External/SapTarifaHistorico.cs:19-20`,
  `DbSolucionesTiDbContext.cs:146-147`). En el frontend real, "Rango peso
  (kg)" hoy es **columna de solo lectura** en la tabla, sin ningún filtro por
  rango (`biblioteca-page.component.html:129,178` en
  `administracion_embarqa_nexo`).
- **Sí existe ya un catálogo real de rangos de peso** (`tblRangoPeso`, mapeado
  como `SapRangoPeso`: Id, PesoMinimo, PesoMaximo, TipoVehiculo —
  `DbSolucionesTiDbContext.cs:26,78-79`), pero **solo se usa hoy para
  SICE-TAC** (derivar configuración vehicular en `TarifarioSapAdapter.cs`), sin
  ninguna relación con la tarifa de plaza. Es el catálogo candidato natural
  para el desplegable de esta HU.
- **Carga masiva Excel — no existe para plaza, pero el patrón ya está
  resuelto en 3 módulos hermanos** con upsert por llave (insertar si es nuevo,
  actualizar si ya existe):
  `ImportarTarifasSicetacExcelCommand.cs` (Tarifas SICE-TAC Admin),
  `ImportarServiciosConexosExcelCommand.cs` (Servicios Conexos) y el
  importador de Reglas Solo-SICETAC (HU-700). Confirmado también en el frontend
  real que hoy el tab "Plaza" de Biblioteca **no tiene** botón de carga Excel
  (sí lo tiene el tab "Servicios", otro molde reutilizable).
- **Semáforo de vigencia.** En la tabla local nueva (HU-701.1), `vigencia` es
  un único campo propio, directamente editable — no hay override que
  resolver. El semáforo (HU-701.3) se calcula en el frontend a partir de ese
  campo.

### Sub-HUs (issues de GitHub)

> La decisión de arquitectura (alcance completo) ya está confirmada — ver
> sección arriba — así que no queda como issue aparte, solo como contexto de
> las sub-HUs siguientes.

#### HU-701.1 — Crear la tabla local de tarifas de plaza + catálogos del formulario
**Responsable:** Sara (Datos) + Ismael (Backend) · **Repositorio:** `NEXO`

Crear en la BD de Nexo la tabla nueva que va a almacenar estos valores —
gobernada por Nexo, no una copia de solo lectura de SAP. Columnas según el
formulario a recrear: línea de negocio, clase de viaje, origen (ciudad/zona),
destino (ciudad/zona), clase de contenedor, rango de peso (catálogo, no
número libre), valor, vigencia y detalles de la tarifa (texto libre
obligatorio). Incluye exponer `GET` de línea de negocio, clase de viaje y
clase de contenedor — 3 catálogos ya mapeados y conectados en el backend
(`SapLineaNegocio`/`tblSAPLineaNegocio`, `SapClaseViaje`/`tblSAPClaseViaje`,
`SapClaseContenedorCatalogo`/`tblSAPClaseContenedor`) que solo necesitan el
endpoint, mismo patrón que HU-700.2 para tipos de vehículo.

**Criterios de aceptación:**
- [ ] Existe la tabla nueva, migrada por EF Core, con los 9 campos del
      formulario original.
- [ ] Línea de negocio, clase de viaje y clase de contenedor se guardan
      como referencia a los catálogos ya conectados, no como texto libre
      duplicado; existe `GET` de cada uno.
- [ ] Origen y destino se guardan como referencia al catálogo de
      ciudades/zona de HU-700.1 (no se crea un segundo catálogo de ciudades).
- [ ] Rango de peso se guarda como referencia a un catálogo (confirmada la
      fuente exacta — ver Preguntas abiertas), no como número libre.

#### HU-701.2 — CRUD de tarifas de plaza + carga/descarga masiva por Excel
**Responsable:** Ismael (Backend) + Juan Pablo (Frontend) · **Repositorio:** `NEXO` + `administracion_embarqa_nexo`

Depende de HU-701.1. Construir el CRUD (crear/editar/eliminar/listar) sobre la
tabla nueva, con el formulario recreando exactamente los 9 campos del
sistema externo (catálogos como desplegable, no texto libre), más
carga/descarga masiva por Excel reutilizando el patrón de upsert ya resuelto
en 3 módulos hermanos (`ImportarTarifasSicetacExcelCommand.cs`, Servicios
Conexos, Reglas Solo-SICETAC de HU-700.2).

**Criterios de aceptación:**
- [ ] El formulario tiene los 9 campos, con los de catálogo como desplegable.
- [ ] Al elegir origen/destino, se muestra la zona resuelta (igual que
      "Zona Origen"/"Zona Destino" en el sistema externo).
- [ ] "Detalles de la tarifa" es un campo de texto libre obligatorio.
- [ ] Existe exportación a Excel del listado actual; al recargar ese mismo
      archivo (editado): filas nuevas se insertan, filas existentes (por
      llave) se actualizan, sin duplicar.

#### HU-701.3 — Semáforo de vigencia (frontend)
**Responsable:** Juan Pablo (Frontend) · **Repositorio:** `administracion_embarqa_nexo`

Solo de frontend. Depende de que el DTO del CRUD (HU-701.2) exponga el campo
`vigencia` de la tabla nueva (HU-701.1) — a partir de esa fecha, calcular en el
cliente `DiasParaVencer`/`EstadoVigencia` (vencido/por_vencer/vigente) y el
orden rojo → amarillo (≤3 días) → verde es un simple cálculo de diferencia de
fechas, sin nada adicional de backend.

**Criterios de aceptación:**
- [ ] El listado de tarifas de plaza pinta el semáforo (rojo/amarillo/verde)
      calculado en el frontend a partir del campo `vigencia` que ya expone
      el CRUD de HU-701.2.
- [ ] El listado se puede ordenar/filtrar rojo→amarillo→verde.

#### HU-701.4 — QA: verificación end-to-end de HU-701
**Responsable:** QA · **Repositorio:** `NEXO` + `administracion_embarqa_nexo`

- Crear una tarifa de plaza completa (los 9 campos) y confirmar que queda
  guardada en la tabla nueva, no en `tblSAPTarifas`.
- Confirmar que línea de negocio, clase de viaje, clase de contenedor,
  origen/destino y rango de peso solo se pueden seleccionar desde catálogo,
  sin texto/número libre.
- Exportar el Excel actual, modificar una fila y recargar: se actualiza, no
  se duplica; agregar una fila nueva y confirmar que se inserta.
- Semáforo: crear/ajustar tarifas vencidas (rojo), a 1-2 días de vencer
  (amarillo) y a 30+ días (verde); confirmar el orden en el listado.

---

## HU-702 — Separar Tarifas Ganadas de la Biblioteca y cerrar el permiso de marcar resultado

**Como** Comercial, **quiero** que solo Comercial y Admin puedan marcar una
cotización como Ganada/Perdida — ni `AdminTarifas` ni `CoordinadorFletes` —,
y que "Tarifas ganadas" no aparezca dentro de la Biblioteca de tarifas que
administra el Administrador de Fletes, **para** que cada rol solo vea/opere
lo que le corresponde.

**Responsables:** Backend — Ismael · Frontend — Juan Pablo
**Repositorio(s):** `NEXO` (backend) + `administracion_embarqa_nexo` (frontend)

### Estado actual (verificado) — dos correcciones distintas, cada una con hallazgos propios

**3.a — "Tarifas ganadas" es literalmente un tab dentro de "Biblioteca"**, no
una confusión de nombres: `biblioteca-page.component.ts:23,29` (Angular real)
define `type TabBiblioteca = 'sicetac' | 'plaza' | 'sap' | 'ganadas' |
'servicios'` y el tab `ganadas` (`biblioteca-page.component.html:193-224`)
consulta el histórico local de tarifas ganadas
(`GET /api/tarifas-historicas`, módulo backend `TarifasGanadas`, ya separado
del resto en el código — no hay nada que "desacoplar" a nivel de datos, es
pura reubicación de navegación). **No existe hoy ningún otro menú/ruta** para
tarifas ganadas (revisado `cabina-header.component.html` y
`cabina-fletes-routing.module.ts` completos) — "quitarla de Biblioteca"
implica **crear una ubicación nueva**, no mover a un lugar que ya exista.

**3.b — Regla de negocio confirmada (2026-09-25): marcar Ganada/Perdida es
solo para Comercial y Admin.** `AdminTarifas` y `CoordinadorFletes` quedan
excluidos. Verificado en el frontend real, hay tres puntos que corregir:

- El permiso `marcarGanadaPerdida`
  (`src/app/cabina-fletes/models/nexo-access.models.ts:11-83`) hoy da `true`
  a `Comercial`, `CoordinadorFletes` y `Admin` (solo excluye a `AdminTarifas`)
  — hay que corregirlo para que quede en `true` solo para `Comercial` y
  `Admin`, quitando `CoordinadorFletes`.
- **Mesa de Costeo** aplica ese permiso para ocultar el botón
  (`mesa-costeo-page.component.html:35-36`,
  `*ngIf="access.puede('marcarGanadaPerdida')"`) — se corrige solo con el
  punto anterior, sin tocar esta pantalla.
- **`cotizacion-resumen-page` NO aplica el permiso en absoluto** — el botón
  de marcar resultado (html:75-80) solo depende de
  `puedeRegistrarResultado()` (`cotizacion-resumen-page.component.ts:65-67`,
  valida únicamente `estado === 'Entregada' && resultado === 'SinDefinir'`),
  sin ninguna verificación de rol — el componente ni siquiera inyecta
  `NexoAccessService`. Cualquier rol con acceso a esa pantalla puede usar ese
  botón hoy.
- **El backend permite explícitamente a `AdminTarifas`, `CoordinadorFletes` y
  `Admin`:** `[Authorize(Roles = "Comercial,AdminTarifas,CoordinadorFletes,Admin")]`
  sobre `MarcarResultado`
  (`src/CabinaFletes.Api/Cotizaciones/CotizacionesController.cs:91-94`) — ya
  documentado como pendiente en `docs/hallazgos-requisitos.md` (C.4), aunque
  ahí solo se cuestionaba `AdminTarifas` — la regla ahora también excluye
  `CoordinadorFletes`. Sin corregir el backend, una llamada directa a la API
  deja pasar a `AdminTarifas` o `CoordinadorFletes` igual.

### Sub-HUs (issues de GitHub)

#### HU-702.1 — Restringir el rol autorizado a marcar Ganada/Perdida (backend)
**Responsable:** Ismael (Backend) · **Repositorio:** `NEXO`

Dejar `Comercial` y `Admin` en el `[Authorize(Roles=...)]` de
`MarcarResultado` (`CotizacionesController.cs:91-94`) — quitar `AdminTarifas`
y `CoordinadorFletes`.

**Criterios de aceptación:**
- [ ] `AdminTarifas` y `CoordinadorFletes` reciben `403` al llamar
      `POST /api/cotizaciones/{id}/resultado` directamente.
- [ ] `Comercial` y `Admin` siguen funcionando sin regresión.

#### HU-702.2 — Corregir y aplicar el permiso `marcarGanadaPerdida` (frontend)
**Responsable:** Juan Pablo (Frontend) · **Repositorio:** `administracion_embarqa_nexo`

Dos correcciones sobre el mismo permiso:

1. En `nexo-access.models.ts:11-83`, dejar `marcarGanadaPerdida` en `true`
   para `Comercial` y `Admin` — quitar `CoordinadorFletes` (`AdminTarifas` ya
   estaba excluido y sigue así). Mesa de Costeo ya consume este permiso
   (`mesa-costeo-page.component.html:35-36`), así que se corrige sola con
   este cambio, sin tocar esa pantalla.
2. Aplicar ese mismo permiso en `cotizacion-resumen-page.component.ts`, que
   hoy NO lo aplica en absoluto — el botón de marcar resultado solo valida
   el estado de la cotización, sin verificar rol.

**Criterios de aceptación:**
- [ ] `access.puede('marcarGanadaPerdida')` es `true` solo para `Comercial` y
      `Admin`.
- [ ] Mesa de Costeo oculta el botón para `AdminTarifas` y `CoordinadorFletes`.
- [ ] El botón de marcar resultado en Cotización-Resumen respeta ese mismo
      permiso, igual que Mesa de Costeo.

#### HU-702.3 — Reubicar "Tarifas ganadas" fuera de Biblioteca (frontend)
**Responsable:** Juan Pablo (Frontend) · **Repositorio:** `administracion_embarqa_nexo`

Sacar el tab `ganadas` de `biblioteca-page.component.ts`/`.html` y darle una
ubicación de navegación propia (ver Preguntas abiertas: dónde debe vivir).

**Criterios de aceptación:**
- [ ] "Tarifas ganadas" ya no es un tab de "Biblioteca".
- [ ] Tiene su propia ubicación en el menú, accesible para los roles
      correctos.

#### HU-702.4 — QA: verificación end-to-end de HU-702
**Responsable:** QA · **Repositorio:** `NEXO` + `administracion_embarqa_nexo`

- Con `AdminTarifas` y `CoordinadorFletes`: confirmar que el botón queda
  oculto tanto en Mesa de Costeo como en Cotización-Resumen.
- Con esos mismos 2 roles, llamar directo la API — debe responder `403`.
- Con `Comercial` y `Admin`, marcar resultado sigue funcionando sin
  regresión.
- Confirmar que "Tarifas ganadas" ya no aparece en "Biblioteca" y que su
  nueva ubicación es accesible para los roles correctos.

---

## HU-703 — Recomendación de vehículo y cantidad a partir de peso/dimensiones

**Como** Comercial o Administrador de Fletes, **quiero** que, dado el peso y
las dimensiones de una carga, el sistema recomiende un vehículo del catálogo
y la cantidad necesaria (reusando el motor Cubicador), **para** no tener que
adivinar manualmente qué vehículo pedir.

> Los 3 puntos de esta HU vienen marcados como responsabilidad de Brayan
> (motor Cubicador) en el pedido original — se conserva esa asignación.

**Responsables:** Brayan (motor Cubicador, backend) · Frontend — Juan Pablo
(solo si se decide exponer una pantalla nueva de recomendación, por confirmar)
**Repositorio(s):** `NEXO` (carpeta `cubicador/`, mismo repo backend) +
`administracion_embarqa_nexo` (si aplica UI nueva)

### Estado actual (verificado)

- **La recomendación YA EXISTE como cálculo, pero aislada — no integrada al
  flujo de Solicitud/Plan de Cargue.** `POST /api/cubicacion`
  (`cubicador/api/routers/cubicacion.py:31`) → `motor.cubicar()`
  (`cubicador/domain/motor/cubicacion_rapida.py:231-284`) ya devuelve
  `SugerenciaVehiculo` (un solo vehículo, `_sugerir`, líneas 323-344) o, si no
  cabe en uno, `SugerenciaFlota` con `cantidad_vehiculos` (`_sugerir_flota`,
  líneas 347-392, reparto voraz, tope 20 vehículos). Es cálculo puro sin
  persistencia, llamado directo desde NEXO como excepción HTTP documentada
  (`PENDIENTES.md:82-89`, `docs/mensajeria/CUBICACION.md` §8.2) — **no pasa
  por `ICubicacionPort`** (que solo expone optimizar un plan con vehículo YA
  conocido) y **no se usa hoy para recomendar antes de crear un plan**.
- **Dos catálogos de vehículos duplicados, no uno solo:**
  `presets()` (`cubicador/application/plan_cargue/vehiculos.py:67-99`, 9
  `VehiculoCubicacion`, editable, en BD del Cubicador) vs. `CATALOGO`
  hardcodeado (`cubicador/domain/motor/cubicacion_rapida.py:196-224`, otros 9
  `UnidadCubicacion`, mismos arquetipos pero con campos distintos, no lee de
  BD). Antes de exponer "un" catálogo para recomendar, hay que decidir cuál es
  la fuente de verdad.
- **`LineaSolicitud.Dimensiones`** ya existe
  (`src/CabinaFletes.Domain/Solicitudes/Entidades/LineaSolicitud.cs:76-77`,
  JSON de posiciones, llenado en `CapturarSolicitudCommand.cs:121-123`) — pero
  puede quedar `null` si no hay posiciones capturadas, y el peso vive aparte
  (`Peso`, línea 74), no dentro del JSON.
- **Tabla corporativa de peso máximo por vehículo — existe, pero para otro
  propósito.** `tblRangoPeso`/`tblContenedorRangoPeso`
  (`SapTarifario.cs:22-41`, usadas en `TarifarioSapAdapter.cs:409`) sirven
  hoy para derivar configuración SICE-TAC en el costeo, no para
  cubicación/recomendación de vehículo — el Cubicador usa sus propias
  `MedidasEstandar` (parametrizables, desconectadas de las tablas SAP).
- **Limitación conocida que esta HU hereda:** el worker de Plan de Cargue
  **solo reporta el primer vehículo de una flota multi-vehículo**
  (`PENDIENTES.md:76-78`). Si la recomendación sugiere varios vehículos, hoy
  el plan resultante no los trackearía todos.

### Sub-HUs (issues de GitHub)

#### HU-703.1 — Motor de recomendación: catálogo único, integración al flujo y peso real
**Responsable:** Brayan · **Repositorio:** `NEXO` (`cubicador/` + puente con `ICubicacionPort`)

Agrupa las 3 piezas del motor, todas responsabilidad de Brayan:

1. **Unificar el catálogo de vehículos** — una única fuente de verdad entre
   `presets()` (BD, editable) y `CATALOGO` (hardcodeado en
   `cubicacion_rapida.py`) — hoy son 9+9 arquetipos paralelos que pueden
   divergir.
2. **Exponer la recomendación (vehículo + cantidad) integrada al flujo** —
   hoy ya existe como cálculo (`POST /api/cubicacion` → `motor.cubicar()`)
   pero aislada, sin integrarse al flujo de Solicitud/Línea. Definir si se
   integra vía `ICubicacionPort` o se mantiene como excepción HTTP igual que
   hoy.
3. **Conectar el peso máximo real por tipo de vehículo** —
   `tblRangoPeso`/`tblContenedorRangoPeso` (tablas corporativas reales) al
   criterio de selección, en vez de depender solo de `MedidasEstandar`
   propias y desconectadas.

Incluye también resolver (Brayan + Ismael, a confirmar con negocio): por
cada posición/línea recomendada, ¿se crea una línea de solicitud nueva o se
multiplica la cantidad sobre la existente? — punto explícitamente sin
resolver en el pedido original.

**Criterios de aceptación:**
- [ ] Existe un único catálogo de vehículos, sin duplicación. Documentado qué
      pasó con las divergencias encontradas entre los dos catálogos antes de
      unificar (ver Pruebas QA de HU-703.3).
- [ ] Dado peso + dimensiones de una línea de solicitud, el sistema devuelve
      vehículo recomendado + cantidad, disponible desde el flujo real.
- [ ] El criterio de selección de vehículo usa el peso máximo real de las
      tablas corporativas.
- [ ] Documentado y resuelto qué pasa si la recomendación excede 1 vehículo,
      dada la limitación conocida del worker (reporta solo el primero).
- [ ] Queda definido, documentado e implementado si se crea línea nueva o se
      multiplica cantidad.

#### HU-703.2 — Pantalla de recomendación (si aplica)
**Responsable:** Juan Pablo (Frontend) · **Repositorio:** `administracion_embarqa_nexo`
**Condicional:** solo si negocio confirma que se necesita una pantalla nueva
(el pedido original no la menciona explícitamente, ver tabla resumen).

**Criterios de aceptación:**
- [ ] Definido si esta sub-HU aplica o no.
- [ ] Si aplica: la recomendación (vehículo + cantidad) es visible y usable
      desde la pantalla correspondiente.

#### HU-703.3 — QA: verificación end-to-end de HU-703
**Responsable:** QA · **Repositorio:** `NEXO`

- Reutilizar (o extender) los "26/26 casos dorados" de paridad del Cubicador
  (`cubicador/tests/paridad/test_paridad_casos_dorados.py`) como set de
  referencia: para cada peso/dimensión conocido, la recomendación debe
  coincidir con el vehículo esperado.
- Caso límite: una carga que no cabe en un solo vehículo debe devolver una
  flota (`cantidad_vehiculos > 1`) — confirmar qué reporta el Plan de Cargue
  resultante, dada la limitación conocida del worker.
- Antes de unificar catálogos (HU-703.1): correr el mismo set de pesos/
  dimensiones contra `presets()` y `CATALOGO` y documentar divergencias.

---

## HU-704 — Límite de antigüedad de 3 meses en Tarifas Históricas

**Como** Administrador de Fletes, **quiero** que la Biblioteca solo muestre
tarifas históricas de hasta 3 meses de antigüedad, **para** no sugerir precios
de mercado ya desactualizados.

**Responsables:** Backend — Ismael
**Repositorio(s):** `NEXO`

### Estado actual (verificado)

- El endpoint `/api/tarifas-historicas` tiene dos ramas:
  `SugerirTarifaHistoricaQuery` (top-10 sin paginar, **sin ningún filtro de
  antigüedad**, solo `Take(10)` descendente) y `BuscarBibliotecaTarifasQuery`
  (paginada, delega a
  `EfTarifasHistoricasRepository.BuscarAsync`,
  `src/CabinaFletes.Infrastructure/TarifasGanadas/Repositorios/EfTarifasHistoricasRepository.cs:39-79`).
- **Hoy solo se filtra por fecha si el cliente envía el parámetro opcional
  `vigencia`** (`"vigente"|"por_vencer"|"vencida"`, líneas 56-71). Si no se
  envía, **no hay ningún corte de antigüedad** — se listan todas las tarifas
  que existan.
- Los cortes actuales son **6 y 12 meses** (`vigente` ≥ -6m, `por_vencer`
  entre -6/-12m, `vencida` < -12m), calculados sobre `TarifaHistorica.Fecha`
  (la fecha de la cotización ganada, no la de creación del registro) —
  documentado también en `MIGRACION.md:170-177`. **No existe hoy ningún
  breakpoint de 3 meses.** Confirmado que el frontend real ya tiene este
  mismo filtro de 3 opciones, sin ninguna cuarta opción
  (`biblioteca-page.component.html:200`, `administracion_embarqa_nexo`).

### Sub-HUs (issues de GitHub)

#### HU-704.1 — Aplicar el corte de 3 meses como filtro obligatorio en backend
**Responsable:** Ismael (Backend) · **Repositorio:** `NEXO`

**Antes de implementar, negocio debe definir** si el corte de 3 meses
**reemplaza** los umbrales actuales (`vigente`/`por_vencer`/`vencida`, hoy a
6/12 meses) o es un **tope adicional** que oculta todo lo mayor a 3 meses sin
importar cuál de esas 3 opciones se elija (ver Preguntas abiertas).

Hoy el endpoint solo filtra por fecha si el cliente envía `vigencia` — sin
ese parámetro, no hay ningún corte de antigüedad. Agregar el tope de 3 meses
como `Where` obligatorio en `Base()`/`BuscarAsync`/`SugerirAsync`
(`EfTarifasHistoricasRepository.cs`).

**Criterios de aceptación:**
- [ ] Ninguna tarifa histórica de más de 3 meses de antigüedad es visible ni
      recuperable desde `/api/tarifas-historicas`, con o sin filtro
      `vigencia` enviado por el cliente.
- [ ] `SugerirTarifaHistoricaQuery` (top-10) también respeta el corte.

#### HU-704.2 — QA: verificación end-to-end de HU-704
**Responsable:** QA · **Repositorio:** `NEXO`

- Tarifa histórica en el borde exacto (según lo definido en HU-704.1) —
  confirmar el comportamiento esperado en ese límite.
- Confirmar que `SugerirTarifaHistoricaQuery` respeta el corte de 3 meses.
- Llamar al endpoint sin enviar `vigencia` y confirmar que el corte igual se
  aplica.

---

## HU-705 — Las horas de cargue/descargue capturadas deben afectar el piso SICE-TAC de la propuesta automática

**Como** Comercial, **quiero** que si cambio las horas de cargue/descargue de
la solicitud (por defecto 4h+4h), el piso SICE-TAC que el sistema propone
automáticamente refleje esas horas reales, **para** que la propuesta no siga
asumiendo 8 horas cuando el negocio ya sabe que es distinto.

**Responsables:** Backend — Ismael
**Repositorio(s):** `NEXO`

### Estado actual (verificado) — el hallazgo es más preciso de lo que parecía

**`HorasCargue`/`HorasDescargue` ya se capturan de punta a punta y SÍ se usan
correctamente en un lugar — pero no en el que genera la propuesta
automática:**

- Captura real en frontend: `captura-page.component.html:38-39`
  (`administracion_embarqa_nexo`, default 4h+4h, "Estándar: 4 h").
- Persistencia backend: `CapturarSolicitudCommand.cs:290-296`
  (`SerializarCondiciones`) guarda `TiemposLogisticosDto` en
  `Solicitud.CondicionesServicio` (JSON).
- **✅ Ya funciona correctamente en Mesa de Costeo (flujo manual):**
  `mesa-costeo-page.component.ts:417-421`, función `floorTotal()`, calcula
  `piso.valorPiso + (horasCargue+horasDescargue) * piso.valorHora` y ese
  resultado **ya se usa como valor por defecto del campo "Valor del flete"**
  al costear manualmente (línea 787). Esto funciona porque el backend
  (`PisoSicetacDto`, expuesto por `ObtenerCosteoQuery` desde la caché
  `TarifaSicetac`) **ya separa** `valorPiso` y `valorHora` como campos propios
  (`src/CabinaFletes.Domain/BibliotecaTarifas/Entidades/Tarifas.cs:49,52`),
  no solo el valor combinado. El frontend incluso se lo muestra al usuario:
  *"Piso $X + Y h × $Z/h"* (`mesa-costeo-page.component.html:223`).
- **❌ NO funciona en la propuesta automática (auto-costeo / "Aplicar
  Propuestas"), que usa la matriz SICE-TAC en vivo del DW, no la caché
  admin.** En `PropuestaTarifaAdapter.cs:357`:
  ```csharp
  var valor = fila.ValorSiceConHora ?? fila.Valor;
  ```
  usa el valor **ya precalculado** con las horas fijas del DW
  (`HorasPactadas` de esa fila, típicamente 8 o 2), **ignorando las horas
  reales de la solicitud**. La firma de `ProponerLineaAsync`/
  `CandidatoSicetacAsync` (líneas 180-183, 327-334) ni siquiera recibe los
  tiempos logísticos como parámetro.
- **La corrección es más acotada de lo que parecía — no hay que inventar la
  fórmula, ya está probada en el frontend y los datos ya vienen separados
  también en el DW en vivo:** `RutaSicetacDw`
  (`src/CabinaFletes.Infrastructure/External/RutaSicetacDw.cs:13,21,22,24`)
  **ya expone** `Valor` (el piso base, sin horas — equivalente a
  `VALORMOVILIZA`), `ValorHora` y `HorasPactadas` por separado, además del
  combinado `ValorSiceConHora`. El backend puede recalcular
  `Valor + ValorHora × horasSolicitud` con los mismos ingredientes que ya usa
  el frontend para la caché admin — no requiere una fuente de datos nueva.

### Sub-HUs (issues de GitHub)

#### HU-705.1 — Recalcular el piso SICE-TAC con las horas reales en la propuesta automática
**Responsable:** Ismael (Backend) · **Repositorio:** `NEXO`

En `PropuestaTarifaAdapter.CandidatoSicetacAsync`/`ProponerLineaAsync`,
recibir `TiemposLogisticosDto` de la solicitud y, si las horas capturadas
difieren de `fila.HorasPactadas`, recalcular
`fila.Valor + (fila.ValorHora ?? 0) * horasSolicitud` en vez de usar
`fila.ValorSiceConHora` tal cual. Mantener `ValorSiceConHora` como fallback
cuando no haya horas capturadas o `ValorHora` sea `null`. Incluye auditar si
algún otro punto del backend lee `ValorSiceConHora`/`ValorSiceHora` directo
de una fuente precalculada (mismo problema, en otro lugar), para no dejar
una segunda ruta sin corregir.

**Criterios de aceptación:**
- [ ] Una solicitud con horas distintas a 4h+4h genera una propuesta
      automática de SICE-TAC recalculada con esas horas.
- [ ] Una solicitud con horas por defecto (4h+4h) no cambia de valor
      (regresión).
- [ ] Idealmente, esta función queda compartida con la que ya usa Mesa de
      Costeo (`floorTotal()`), en vez de reimplementar la fórmula por
      segunda vez en un lugar distinto.
- [ ] Queda documentado cada punto adicional encontrado con el mismo
      problema (si los hay) y si se corrigió o se dejó fuera de alcance
      explícitamente.

#### HU-705.2 — QA: verificación end-to-end de HU-705
**Responsable:** QA · **Repositorio:** `NEXO`

- Solicitud con horas por defecto (4h+4h): la propuesta automática da el
  mismo valor que hoy (regresión).
- Solicitud con horas distintas (ej. 6h cargue + 2h descargue): la propuesta
  automática recalcula, y coincide con lo que ya calcula Mesa de Costeo para
  el mismo caso.
- Caso con `ValorHora` nulo en la fila SICE-TAC: cae al fallback
  (`ValorSiceConHora`) sin lanzar error.
- Regresión sobre los 12/12 casos de SICE-TAC ya verificados en el EDA-2
  (fórmula `VALORMOVILIZA + VALORHORA × horas_pactadas`) para confirmar que
  el cambio no rompe nada de lo ya validado ahí.

---

## Ajustes en consecuencia (transversales)

1. **HU-700 crea una tabla local nueva de ciudades — no debe reconstruir la
   regla de resolución ciudad→zona desde cero para poblarla.** Reutilizar la
   lógica ya existente en `ResolucionZonaSap.cs`/`CiudadesDwAdapter.cs`
   (incluye los alias de ciudades satélite ya curados a mano, ver hallazgo de
   memoria del proyecto sobre Mosquera/Soacha/Rionegro) como el proceso de
   siembra de la tabla nueva, en vez de escribir el matching de zonas otra
   vez.
2. **HU-700 se solapa con un hallazgo ya documentado (A.8,
   `docs/hallazgos-requisitos.md`)**: unificar catálogo de tipo de
   vehículo/carrocería también hace falta en Captura de Solicitudes (front
   quemado hoy). Construir el catálogo compartido una sola vez y reutilizarlo
   en ambos módulos, no duplicar el trabajo.
3. **HU-701 ya tiene resuelta la decisión de arquitectura (alcance completo,
   2026-09-25)** — HU-701.1 crea la tabla local nueva; no queda ninguna
   estimación pendiente de esa decisión.
4. **HU-701 depende del catálogo de ciudades/zona de HU-700.1** para
   Origen/Destino — no crear un segundo catálogo de ciudades. Si HU-700.1 se
   retrasa, HU-701.1 queda bloqueada en esa parte.
5. **HU-701, semáforo (frontend):** con la tabla local de HU-701.1, `vigencia`
   es un único campo propio (no hay override que resolver, ese concepto era
   del sistema viejo de solo lectura de SAP) — el cálculo del semáforo
   depende únicamente de que el DTO del CRUD (HU-701.2) exponga ese campo, algo
   que va a exponer de todas formas por ser una columna central de la tabla.
6. **HU-702 tiene dos huecos, no uno:** el permiso `marcarGanadaPerdida` en sí
   está mal (da `true` a `CoordinadorFletes`, que ya no debe tenerlo, HU-702.2),
   y `cotizacion-resumen-page.component.ts` no lo aplica en absoluto (HU-702.3).
   Corregir solo uno de los dos no cierra la regla de "solo Comercial y
   Admin".
7. **HU-703 requiere decidir primero cuál de los dos catálogos de vehículos del
   Cubicador (`presets()` en BD vs. `CATALOGO` hardcodeado) es la fuente de
   verdad**, antes de construir la recomendación sobre uno de los dos y dejar
   el otro divergiendo con el tiempo.
8. **HU-703 hereda la limitación ya conocida** de que el worker de Plan de
   Cargue solo reporta el primer vehículo de una flota multi-vehículo — si se
   va a recomendar "cantidad > 1", esa limitación debería resolverse en la
   misma iteración o quedar explícitamente fuera de alcance.
9. **HU-704 debe implementarse como corte obligatorio de backend**, no como
   filtro de UI — el endpoint ya devuelve todo si el cliente omite
   `vigencia`.
10. **HU-705, si se corrige, conviene extraer el cálculo a una función
    compartida** (backend) en vez de dejarlo solo en el frontend
    (`floorTotal()`) y reimplementarlo aparte en `PropuestaTarifaAdapter` — dos
    implementaciones de la misma fórmula es el mismo tipo de riesgo que ya
    produjo inconsistencias en otros módulos de este sistema.

## Preguntas abiertas (a resolver con negocio antes de estimar)

- **HU-700:** el pedido original incluye la frase incompleta *"Incorporar
  lógica similar a tarifas solo sicetac, donde"* — no se adivinó su
  continuación. Confirmar qué lógica exactamente debía aplicarse aquí (¿se
  refiere a reutilizar el mismo patrón de catálogos en HU-701?).
- **HU-700:** la tabla local de ciudades — ¿se puebla una sola vez (foto fija,
  como el set `v1.0.0-piloto` de analítica) o necesita sincronizarse
  periódicamente contra `tblSAPDANE`/`tblZonasTarificacionRelacion`/Tarifario
  SAP? Si el DW corporativo agrega una ciudad o cambia una zona después de la
  siembra inicial, ¿quién es responsable de actualizar la copia local?
- **HU-700:** al cruzar `tblZonasTarificacionRelacion` (relación oficial)
  contra los códigos ZC/ZTC que realmente aparecen en `Origen`/`Destino` del
  Tarifario SAP, ¿qué pasa si aparece un código en el tarifario que no está
  en la relación oficial (como pasó con `ZTC_NORTE_BOGOTA`)? ¿Se agrega igual
  a la tabla local, se marca para revisión manual, o se descarta?
- **HU-701:** ¿cuál es el nombre/origen exacto del sistema externo de la
  captura de pantalla? ¿Tiene datos ya cargados que deban migrarse a Nexo, o
  se arranca en blanco?
- **HU-701:** para el desplegable de rango de peso, ¿`tblRangoPeso` (peso
  mínimo/máximo por tipo de vehículo) es la fuente correcta para los rangos
  que usa este formulario (ej. 14001–20000), o el sistema externo maneja sus
  propios rangos, no ligados a tipo de vehículo, que habría que capturar como
  catálogo nuevo?
- **HU-702:** ¿dónde debe vivir "Tarifas ganadas" si se saca de Biblioteca?
  (¿ítem de menú propio? ¿dentro de Cotizaciones?) — hoy no existe una
  ubicación alternativa ya construida.
- **HU-703:** "por cada línea se crea la línea solicitud o se multiplica por la
  cantidad — validar" quedó explícitamente sin resolver en el pedido
  original. Definir el comportamiento esperado.
- **HU-704:** el corte de 3 meses, ¿reemplaza los umbrales actuales de 6/12
  meses (`vigente`/`por_vencer`/`vencida`), o es un tope adicional que oculta
  todo lo mayor a 3 meses sin importar cuál de esas 3 opciones se elija?
