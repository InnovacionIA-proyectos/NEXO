# HU — Backend

_Última actualización: 2026-10-16_

Historias de usuario de **backend** (NEXO, .NET), de **todos los
módulos** que se van encontrando en la revisión paso a paso de NEXO (no
solo Mesa de Costeo — cada HU indica su módulo en el título de
sección). A diferencia de `2026-10-09_HU-FRONTEND.md`, este sí es el repo de
Sara — una HU marcada como ya resuelta aquí es código real,
compilado, en el repo real (pendiente a lo sumo de desplegar). La parte
de frontend que consume estas está en `2026-10-09_HU-FRONTEND.md`.

---

## Bandeja

### HU-B1 — El filtro "vigente" debe mirar la Fecha Límite, no la fecha de captura

**Como** usuario de la Bandeja, **quiero** que el filtro "vigente"/
"vencida" refleje la Fecha Límite real de la solicitud, **para** no ver
solicitudes vencidas marcadas como vigentes (o al revés) solo porque se
capturaron hace poco/mucho tiempo.

**Archivo:** `src/Nexo.Application/CasosDeUso/Solicitudes/clsObtenerBandejaQuery.cs`,
líneas 50-53 y 67.

**Causa raíz:**
```csharp
var limiteVigencia = ahora.AddDays(-7);
var vigenciaFiltro = filtro.Vigencia?.Trim().ToLowerInvariant();
if (vigenciaFiltro == "vigente") filas = filas.Where(f => f.CreatedAt >= limiteVigencia).ToList();
else if (vigenciaFiltro == "vencida") filas = filas.Where(f => f.CreatedAt < limiteVigencia).ToList();
...
var vigencia = f.CreatedAt >= limiteVigencia ? "vigente" : "vencida";
```
Hoy "vigente" significa **"se capturó hace menos de 7 días"** — nunca
mira `Deadline` (la Fecha Límite, `DateTimeOffset?` en
`clsBandejaItemDto`). No es un bug de timezone ni de operador
invertido: el código compara bien, pero compara el campo equivocado
(`CreatedAt` en vez de `Deadline`).

**Arreglo:** cambiar las 3 referencias de `f.CreatedAt`/`limiteVigencia`
a comparar `f.Deadline` contra `ahora`. Regla de negocio ya confirmada
con Sara: una solicitud **sin** `Deadline` cuenta como **vigente** por
defecto.

**Criterios de aceptación:**
- [ ] El filtro "vigente" solo muestra solicitudes cuyo `Deadline` no ha
      pasado (o no tiene `Deadline`).
- [ ] El filtro "vencida" solo muestra solicitudes cuyo `Deadline` ya
      pasó.
- [ ] Una solicitud sin `Deadline` aparece como "vigente" (regla de
      negocio confirmada con Sara).
- [ ] El badge de vigencia en `bandeja-page.component.ts` sigue
      pintando igual — no requiere cambio de frontend, solo consume el
      campo ya corregido.

---

### HU-B2 — "Atendidas por Administrador de Fletes" sin entregar deben verse también en "Pendientes"

**Como** usuario de la Bandeja, **quiero** que una solicitud ya revisada
por el Administrador de Fletes pero todavía sin entregar al comercial
siga apareciendo en "Pendientes", **para** no perder de vista que falta
el clic final de "Entregar".

**Archivo:** `src/Nexo.Infrastructure/Solicitudes/Repositorios/clsEfSolicitudesCatalogoRepository.cs`,
líneas 50-51 y 57-69.

**Causa raíz:** una solicitud puede tener **todas** sus líneas revisadas
a mano por el Administrador de Fletes y quedar indefinidamente sin
`Entregada` si nadie hace clic en "Entregar"
(`clsEntregarCotizacionCommand.cs`, único disparador) — nada lo marca ni
lo alerta hoy. Confirmado en pantalla real (2026-10-09): filtrando
Atención = "Atendidas por el Administrador de Fletes" aparecen mezcladas
filas en `Estado = En Costeo` y en `Estado = Entregada`.

**Código actual completo** (así está hoy, las 2 ramas relevantes):
```csharp
else if (atencion == "pendientes")
    q = q.Where(s => s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.EnCosteo);
...
// Atención: sin flete = línea sin clsCotizacionLinea; auto = AutoResuelta; admin = Revisada.
q = atencion switch
{
    "pendientes" => q.Where(s => s.Lineas.Any(l => !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id))),
    "atendidas" => q.Where(s => s.Lineas.Any() && !s.Lineas.Any(l => !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id))),
    "automatico" => q.Where(s => s.Lineas.Any()
        && !s.Lineas.Any(l => !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id))
        && !s.Lineas.Any(l => l.EstadoLinea == EstadoLinea.Revisada)
        && s.Lineas.Any(l => l.EstadoLinea == EstadoLinea.AutoResuelta)),
    "administrador" => q.Where(s => s.Lineas.Any()
        && !s.Lineas.Any(l => !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id))
        && s.Lineas.Any(l => l.EstadoLinea == EstadoLinea.Revisada)),
    _ => q, // "todas"
};
```
La rama `"pendientes"` hoy solo mira "hay alguna línea sin flete" —
nunca mira si ya todas tienen flete y están `Revisada` (eso es lo que
detecta la rama `"administrador"`).

**Decidido:** no se agrega una categoría de atención nueva — estas
solicitudes se suman directamente a "Pendientes" (sin dejar de aparecer
también en "Atendidas por el Administrador de Fletes"). **No aplica** a
"Automático" (auto-costeo): ese flujo ya tiene su propio atajo
(S4-HU-15.6/#252) para llegar a `Entregada` sin depender de un clic
aparte.

**Arreglo:** combinar (OR) la rama `"pendientes"` con la misma
condición que ya usa la rama `"administrador"`:
```csharp
"pendientes" => q.Where(s =>
    s.Lineas.Any(l => !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id))
    || (s.Lineas.Any() && !s.Lineas.Any(l => !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id)) && s.Lineas.Any(l => l.EstadoLinea == EstadoLinea.Revisada))),
```
El filtro externo de `Estado == Pendiente || Estado == EnCosteo` (línea
50-51) ya excluye `Entregada`/`Descartada`, no hace falta repetirlo
dentro de esta rama.

**Criterios de aceptación:**
- [ ] Una solicitud con todas sus líneas revisadas por el Administrador
      de Fletes, pero sin `Entregada` todavía, aparece en "Pendientes".
- [ ] Esa misma solicitud sigue apareciendo también en "Atendidas por el
      Administrador de Fletes" (se agrega a Pendientes, no se excluye de
      ahí).
- [ ] Una solicitud auto-costeada (Atención = Automático) sin
      `Entregada` todavía **no** cambia de comportamiento.
- [ ] Una vez la solicitud pasa a `Entregada`, deja de aparecer en
      "Pendientes".

---

## Biblioteca

### HU-B3 — CoordinadorFletes no puede buscar ciudades al crear un servicio "Almacenamiento"

**Como** Administrador de Fletes (rol `CoordinadorFletes`), **quiero**
poder buscar ciudades al crear un servicio "Almacenamiento" en
Biblioteca, **para** que el campo Origen funcione igual que para
cualquier otro rol que use esa pantalla.

**Archivo:** `src/Nexo.Api/Solicitudes/clsCapturaSolicitudesController.cs`,
línea 20.

**Causa raíz:** el endpoint `GET /api/captura-solicitudes/ciudades`
(línea 119-123) no tiene `[Authorize]` propio — hereda el de la clase:
```csharp
/// <summary>
/// Captura de necesidades del equipo comercial: solicitudes puntuales (spot) o
/// pliegos de licitación, ingresados a mano o importados desde el Excel del cliente.
/// Solo Comercial/Admin — es la puerta de entrada de nuevas solicitudes.
/// </summary>
[ApiController]
[Authorize(Roles = "Comercial,Admin")]
[Route("api/captura-solicitudes")]
public class clsCapturaSolicitudesController(...)
```
Ese `Comercial,Admin` fue pensado para los endpoints de **captura**
(crear solicitud, analizar Excel — "la puerta de entrada de nuevas
solicitudes", según el comentario del propio archivo). Pero
`/ciudades` (y también `/departamentos`, `/municipios`) son catálogos
de solo lectura que **Biblioteca reutiliza** para su propio formulario
de servicios (`biblioteca-page.component.ts:243`, buscador de ciudades
en "Nuevo servicio" → Almacenamiento) — y el rol `CoordinadorFletes`
(Administrador de Fletes) sí tiene el permiso `gestionarServicios` que
habilita esa pantalla en el frontend (`nexo-access.models.ts:58-68`),
pero **no** está en la lista de roles del controller. Cada búsqueda le
devuelve 403, y el frontend lo traga en silencio
(`catchError(() => of([]))` en `biblioteca-page.component.ts:243`) — el
campo simplemente se queda sin sugerencias, sin ningún error visible.
Confirmado en pantalla real con el rol `CoordinadorFletes`.

**Ojo — por qué NO basta con agregar `CoordinadorFletes` a la lista de
roles del método:** en ASP.NET Core, un `[Authorize(Roles=...)]` a nivel
de método **no reemplaza** el de la clase — se **combinan con AND**.
Si el método tuviera su propio `[Authorize(Roles = "Comercial,Admin,CoordinadorFletes")]`
mientras la clase sigue con `[Authorize(Roles = "Comercial,Admin")]`,
el usuario tendría que cumplir **las dos** listas a la vez — como
`CoordinadorFletes` no está en la de la clase, seguiría fallando igual.
La única forma real de abrir estos 3 endpoints es **sacar la
restricción de rol del nivel de clase** y ponerla solo en los métodos
que de verdad la necesitan.

**Arreglo correcto — mismo patrón que ya usa
`clsSolicitudesController.cs:15-16`** (clase con `[Authorize]` simple,
sin roles — exige estar autenticado con alguno de los 6 roles válidos
de NEXO vía la política de respaldo global de `clsProgram.cs:144-149`
— y cada método sensible agrega su propio `[Authorize(Roles=...)]`
encima):
```csharp
// clase: quitar el Roles, dejar solo autenticación
[ApiController]
[Authorize]
[Route("api/captura-solicitudes")]
public class clsCapturaSolicitudesController(...)
{
    [HttpPost("analizar-excel")]
    [Authorize(Roles = "Comercial,Admin")]   // <- se agrega aquí
    public async Task<ActionResult<clsAnalisisExcelDto>> AnalizarExcel(...) { ... }

    [HttpPost]
    [Authorize(Roles = "Comercial,Admin")]   // <- se agrega aquí
    public async Task<ActionResult<clsCapturaResultDto>> Capturar(...) { ... }

    // Ciudades, Departamentos, Municipios: SIN [Authorize] propio —
    // cualquier usuario autenticado con un rol válido de NEXO puede
    // llamarlos (son catálogo de solo lectura, sin dato sensible).
    [HttpGet("ciudades")]
    public async Task<ActionResult<IReadOnlyList<clsCiudadBusquedaDto>>> Ciudades(...) { ... }
}
```
Falta decidir si `Aprovisionamiento` y `Clientes` también se quedan
abiertos a cualquier rol válido, o si necesitan su propio
`[Authorize(Roles=...)]` como `Capturar`/`AnalizarExcel` — revisar caso
por caso antes de implementar (no asumir).

**Criterios de aceptación:**
- Un usuario con rol `CoordinadorFletes` puede buscar ciudades desde
  Biblioteca → Servicios → Nuevo servicio → Almacenamiento, y ve
  sugerencias reales.
- Cualquier otro rol válido de NEXO (no solo `CoordinadorFletes`) puede
  usar `/ciudades`, `/departamentos`, `/municipios` también — ya no
  están restringidos por rol, solo por estar autenticado.
- Un usuario `Comercial`/`Admin` sigue funcionando igual que hoy (sin
  regresión).
- `Capturar` y `AnalizarExcel` (crear solicitud, analizar Excel) siguen
  restringidos a `Comercial,Admin` — este arreglo no les da acceso a
  ningún otro rol.

---

## Tarifas Plaza

> **Ver también, al final del documento:** HU-B14 (la tarifa de plaza
> de NEXO entra al motor de costeo) y HU-B21 (departamento para ciudades
> con nombre repetido en la carga masiva).

### HU-B4 — La carga masiva (Excel) de Tarifas Plaza no lee bien la fecha de Vigencia

**Confirmado por partida doble:** este mismo hallazgo ya estaba
documentado antes, encontrado el 2026-10-05 al depurar un rechazo real
al reimportar un Excel exportado (`docs/2026-10-05_REPARACIONES-FINALES-TARIFAS-PLAZA.md`,
punto 1) — caso real: celda VIGENCIA en formato General con serial
`46315` (2026-10-20 real), rechazada porque `DateOnly.TryParse("46315", ...)`
falla. Dos investigaciones independientes llegaron a la misma causa
raíz y el mismo tipo de arreglo.

**Como** usuario que carga el Excel de Tarifas Plaza, **quiero** que la
columna VIGENCIA se lea igual sin importar cómo Excel haya guardado la
fecha, **para** no ver fechas transformadas/rechazadas en filas que en
el Excel se ven perfectamente bien.

**Archivo:** `src/Nexo.Infrastructure/TarifasPlaza/Repositorios/clsEfTarifasPlazaRepository.cs`,
líneas 93, 126, 198.

**Causa raíz:** la columna VIGENCIA se lee con el helper genérico de
texto, que siempre usa `GetString()` sin mirar el tipo nativo de la
celda, y se parsea sin especificar cultura:
```csharp
// como está hoy:
private static string Str(IXLRow row, Dictionary<string, int> cols, string col) => row.Cell(cols[col]).GetString().Trim();
...
var vigenciaTxt = Str(row, cols, "VIGENCIA");
...
if (!DateOnly.TryParse(vigenciaTxt, out var vigencia))
    throw new InvalidOperationException($"'{vigenciaTxt}' no es una fecha de vigencia válida");
```
Si Excel guarda la fecha como valor nativo (serial numérico con formato
de fecha), `GetString()` puede devolver el serial crudo o un texto que
`DateOnly.TryParse` no entiende bien según la cultura **del servidor**
(no fijada explícitamente) — de ahí que la fecha se vea "transformada"
o la fila se rechace con "no es una fecha de vigencia válida", aunque
en el Excel la celda se vea perfectamente normal.

**La propia columna de VALOR (numérica) ya resuelve este mismo problema
bien, con un patrón que la Vigencia debería imitar:**
```csharp
private static decimal? Num(IXLRow row, Dictionary<string, int> cols, string col)
{
    var celda = row.Cell(cols[col]);
    if (celda.IsEmpty()) return null;
    if (celda.TryGetValue<decimal>(out var v)) return v;   // tipo nativo primero
    return clsImporteParser.Parse(celda.GetString());       // texto heterogéneo como fallback
}
```

**El arreglo correcto ya existe, probado, en OTRO importador del mismo
proyecto** — `clsEfServiciosConexosRepository.cs:226-235`:
```csharp
private static DateOnly? LeerFecha(IXLCell celda)
{
    if (celda.DataType == XLDataType.DateTime) return DateOnly.FromDateTime(celda.GetDateTime());
    var t = celda.GetString()?.Trim();
    if (string.IsNullOrEmpty(t)) return null;
    foreach (var fmt in new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy" })
        if (DateTime.TryParseExact(t, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
            return DateOnly.FromDateTime(f);
    return DateTime.TryParse(t, CultureInfo.GetCultureInfo("es-CO"), DateTimeStyles.None, out var g) ? DateOnly.FromDateTime(g) : null;
}
```
Primero intenta el tipo nativo `DateTime` de la celda (si Excel la
guardó como fecha de verdad, la lee directo, sin pasar por texto).
Solo si no lo es, intenta varios formatos de texto explícitos, y al
final usa cultura `es-CO` **a propósito** (no la del servidor).

**Arreglo:** copiar este mismo `LeerFecha()` a
`clsEfTarifasPlazaRepository.cs` (adaptado para recibir `IXLCell`
igual, solo cambia cómo se obtiene la celda — vía `row.Cell(cols[col])`
en vez del índice directo que usa Servicios Conexos) y usarlo en vez de
`Str(row, cols, "VIGENCIA")` + `DateOnly.TryParse(...)`.

**Criterios de aceptación:**
- Una fecha guardada en Excel como valor nativo de fecha (serial +
  formato de celda tipo fecha) se lee correctamente sin importar la
  configuración regional del servidor.
- Una fecha guardada como texto en formato `dd/MM/yyyy`, `d/M/yyyy`,
  `yyyy-MM-dd` o `MM/dd/yyyy` también se lee bien.
- El mensaje de error "no es una fecha de vigencia válida" solo
  aparece cuando la celda de verdad no es ninguna fecha reconocible
  (no para fechas válidas mal interpretadas).

### Regla general — este mismo criterio aplica a CUALQUIER carga masiva de Excel con columna de fecha, no solo Tarifas Plaza

Se revisaron los 3 importadores de Excel que existen hoy en el
proyecto (todos usan ClosedXML), para saber dónde más aplica este
mismo arreglo:

| Importador | Archivo | ¿Tiene columna de fecha? | Estado |
|---|---|---|---|
| **Tarifas Plaza** (VIGENCIA) | `clsEfTarifasPlazaRepository.cs` | Sí | ❌ Roto — es el de HU-B4, arriba |
| **Servicios Conexos** (Vigencia) | `clsEfServiciosConexosRepository.cs` | Sí | ✅ Ya correcto — es la función `LeerFecha()` que se usa como referencia arriba, no necesita cambio |
| **Reglas Solo SICE-TAC** (Origen/Destino/Vehículo) | `clsExcelReglasSoloSicetacAdapter.cs` | No tiene ninguna columna de fecha | N/A — no aplica |
| **Licitación / Captura masiva de líneas** (Excel tolerante, mapeo libre) | `clsExcelSolicitudesAdapter.cs` | Es un sniffer de texto genérico (`GetFormattedString()`), no tipa columnas — no se encontró conversión de fecha por fila en el flujo de captura masiva | No se confirmó que tenga este bug — mecanismo distinto, revisar aparte si alguna vez se reporta un síntoma similar ahí |

**Regla a seguir de ahora en adelante:** cualquier importador de Excel
nuevo (o que se vuelva a tocar) que tenga una columna de fecha debe usar
el mismo patrón de `LeerFecha()` (tipo nativo `DateTime` de la celda
primero, formatos de texto explícitos después, cultura `es-CO` fijada a
propósito) — nunca el atajo genérico de `GetString()` +
`DateOnly.TryParse()` sin tipo nativo ni cultura explícita. Vale la
pena, de paso, mover `LeerFecha()` a un helper compartido (hoy está
duplicada como método privado dentro de
`clsEfServiciosConexosRepository.cs`) para que los importadores nuevos
la reusen directamente en vez de tener que acordarse de copiarla.

---

## Reglas Solo-SICETAC

### HU-B5 — Quitar los 5 "casos especiales" hardcodeados del catálogo de Tipos de Vehículo

**Como** usuario que crea una Regla Solo-SICETAC, **quiero** que el
desplegable de Tipo de Vehículo solo muestre el catálogo oficial real,
**para** no ver opciones (`CNT20/Consolidado`, `CNT20/Exclusivo`,
`CNT40/Consolidado`, `CNT40/Exclusivo`, `PBV 7500-8000`) que ya no
corresponden a ningún caso de negocio vigente.

**Archivo:** `src/Nexo.Infrastructure/Catalogos/Adaptadores/clsTiposVehiculoAdapter.cs`,
líneas 18-46.

**Causa raíz:** el catálogo real (`tblSAPTiposVehiculoTarifario`,
`dbSolucionesTI`) se complementa con 5 valores escritos a mano en el
código, nunca en base de datos:
```csharp
public static readonly IReadOnlyList<clsTipoVehiculoDto> CasosEspeciales =
[
    new("CNT20/Consolidado", "Devolución contenedor 20' — Consolidado", false),
    new("CNT20/Exclusivo", "Devolución contenedor 20' — Exclusivo", false),
    new("CNT40/Consolidado", "Devolución contenedor 40' — Consolidado", false),
    new("CNT40/Exclusivo", "Devolución contenedor 40' — Exclusivo", false),
    new("PBV 7500-8000", "Camión liviano PBV 7500-8000 kg (nuevo)", false),
];

public async Task<IReadOnlyList<clsTipoVehiculoDto>> ListarAsync(CancellationToken ct)
{
    if (_dc is null) return CasosEspeciales;
    var oficiales = await cache.GetOrCreateAsync("catalogos:tipos-vehiculo", async e => { ... });
    return oficiales!.Concat(CasosEspeciales).ToList();
}
```
El propio comentario de la clase ya marcaba esto como pendiente de
formalizar ("issue pendiente, ver comentario de Sara en #210"). Se
confirmó con Sara (2026-10-12): estos 5 casos **ya no son necesarios**
— no se trata de moverlos a una tabla editable, sino de **eliminarlos**
del desplegable.

**Arreglo:** quitar el arreglo `CasosEspeciales` y la concatenación —
`ListarAsync` devuelve únicamente `oficiales` (el catálogo real de
`tblSAPTiposVehiculoTarifario`).

**Criterios de aceptación:**
- El desplegable de Tipo de Vehículo en Reglas Solo-SICETAC ya no
  muestra `CNT20/Consolidado`, `CNT20/Exclusivo`, `CNT40/Consolidado`,
  `CNT40/Exclusivo` ni `PBV 7500-8000`.
- Solo quedan las opciones del catálogo oficial (`tblSAPTiposVehiculoTarifario`,
  filtradas por activas).
- Reglas Solo-SICETAC ya existentes que usaran alguno de esos 5
  códigos no se tocan (no se borran ni se invalidan) — el cambio es
  solo sobre el catálogo del desplegable, no sobre datos ya guardados.

**✅ Implementado (2026-10-12):** se quitó `CasosEspeciales` y la
concatenación de `ListarAsync` — ahora devuelve únicamente el catálogo
real (`oficiales`, de `tblSAPTiposVehiculoTarifario`), o `[]` si la
conexión a `dbSolucionesTI` no está disponible. Compilado, 0 errores.
Arregla el desplegable en **todas** las pantallas que consumen
`GET /api/catalogos/tipos-vehiculo` a la vez (Reglas Solo-SICETAC,
formulario manual de Tarifas Plaza, y la vista previa de carga masiva
de Tarifas Plaza) — mismo catálogo, una sola fuente. **Pendiente:**
desplegar, para que el frontend (que apunta al backend ya desplegado)
deje de ver los 5 casos especiales.

---

### HU-B6 — Reglas Solo-SICETAC necesita exportar a Excel, igual que Tarifas de Plaza

**Como** usuario de Reglas Solo-SICETAC, **quiero** poder exportar las
reglas ya cargadas a Excel, **para** poder editarlas y reimportarlas
(agregar una fila, corregir una existente) sin tener que reconstruir
el archivo desde cero.

**Archivo:** `src/Nexo.Api/ReglasSoloSicetac/clsReglasSoloSicetacController.cs`
— hoy solo tiene `POST importar-excel` (línea 59) y
`GET plantilla-excel` (línea 70, plantilla **en blanco**). No existe un
`GET exportar-excel` que descargue las reglas ya cargadas.

**Tarifas de Plaza ya tiene exactamente este patrón**
(`clsTarifasPlazaController.cs`, línea 84): un solo
`GET exportar-excel` que descarga el catálogo completo ya cargado, y
ese mismo archivo sirve de plantilla para reimportar (editar una fila,
agregar una nueva, subir de nuevo).

**Arreglo:** agregar `GET exportar-excel` a
`clsReglasSoloSicetacController.cs`, mismo patrón que Tarifas de Plaza
(mismo formato de columnas que ya acepta `importar-excel`).

**Criterios de aceptación:**
- Nuevo endpoint `GET exportar-excel` en Reglas Solo-SICETAC que
  descarga las reglas ya cargadas en el mismo formato que acepta
  `importar-excel`.
- El archivo exportado se puede editar y reimportar sin errores de
  formato.

---

## Mesa de Costeo

> **Ver también, al final del documento:** sección "Motor de decisión de
> tarifa" (HU-B14 a HU-B19, cómo se decide el flete), HU-B20 (valor de
> la mercancía en el costeo) y HU-B22 (traductor de zonas al buscar la
> plaza en SAP).

### HU-B7 — Consultar si ya existe una cotización, sin crear nada

**Como** frontend de Mesa de Costeo, **quiero** poder preguntar si una
solicitud ya tiene una cotización guardada, **para** mostrar el estado
real al abrir la pantalla sin depender de ningún permiso ni de que
alguien haga clic en un botón primero.

**Causa raíz del bug que esto resuelve:** hoy el único camino para traer
una cotización es "crear o recuperar" (`POST /api/cotizaciones`, usado
por `crearORecuperar()` del front) — no existe un **"solo consultar"**
que no tenga el efecto secundario de crear una cotización nueva si no
existía. Por eso el front de Mesa de Costeo solo llama esto cuando el
usuario hace clic en algo, o si tiene el permiso `autoAplicarPropuestas`
— para cualquier otro caso, la pantalla no sabe que ya existe una
cotización real, aunque la haya (ver `2026-10-09_HU-FRONTEND.md`, HU-9).

**Patrón a replicar, archivo por archivo** (mismo esquema Command/Query
que ya usa el proyecto, controller delgado):

`src/Nexo.Application/CasosDeUso/Cotizaciones/clsObtenerCotizacionQuery.cs` —
el query existente, que esta nueva HU debe imitar:
```csharp
public sealed record clsObtenerCotizacionQuery(Guid Id) : IQuery<clsCotizacionDto>;

public sealed class clsObtenerCotizacionQueryHandler(ICotizacionRepository cotizaciones)
    : IQueryHandler<clsObtenerCotizacionQuery, clsCotizacionDto>
{
    public async Task<clsCotizacionDto> Handle(clsObtenerCotizacionQuery query, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(query.Id, ct)
            ?? throw new clsNotFoundException("COTIZACION_NO_ENCONTRADA", "Cotización no encontrada.");
        return c.ToDto();
    }
}
```
Nuevo query equivalente, pero por `SolicitudId` y sin lanzar excepción
si no existe (devuelve `null` en vez de `throw`):
```csharp
public sealed record clsObtenerCotizacionPorSolicitudQuery(Guid SolicitudId) : IQuery<clsCotizacionDto?>;

public sealed class clsObtenerCotizacionPorSolicitudQueryHandler(ICotizacionRepository cotizaciones)
    : IQueryHandler<clsObtenerCotizacionPorSolicitudQuery, clsCotizacionDto?>
{
    public async Task<clsCotizacionDto?> Handle(clsObtenerCotizacionPorSolicitudQuery query, CancellationToken ct = default)
    {
        var c = await cotizaciones.ObtenerConLineasYConexosAsync(x => x.SolicitudId == query.SolicitudId, ct);
        return c?.ToDto();
    }
}
```
**No hace falta tocar el repositorio** — `ICotizacionRepository` ya
tiene el overload que filtra por expresión
(`src/Nexo.Application/CasosDeUso/Cotizaciones/Interfaces/ICotizacionRepository.cs:20`,
`ObtenerConLineasYConexosAsync(Expression<Func<clsCotizacion, bool>> filtro, CancellationToken ct)`),
ya implementado en `clsEfCotizacionRepository.cs:17`. Se reutiliza tal
cual.

Controller (`src/Nexo.Api/Cotizaciones/clsCotizacionesController.cs`,
junto al endpoint `Obtener` existente en la línea 48-52):
```csharp
/// <summary>Consolidado/total de la cotización de una solicitud, si ya existe. No crea nada.</summary>
[HttpGet("por-solicitud/{solicitudId:guid}")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status204NoContent)]
public async Task<ActionResult<clsCotizacionDto>> ObtenerPorSolicitud(Guid solicitudId, CancellationToken ct = default)
{
    var dto = await obtenerPorSolicitud.Handle(new clsObtenerCotizacionPorSolicitudQuery(solicitudId), ct);
    return dto is null ? NoContent() : Ok(dto);
}
```
(agregar `IQueryHandler<clsObtenerCotizacionPorSolicitudQuery, clsCotizacionDto?> obtenerPorSolicitud` al
constructor del controller, igual que el resto de handlers inyectados).

**Criterios de aceptación:**
- Nuevo endpoint de solo lectura: `GET /api/cotizaciones/por-solicitud/{solicitudId}`.
- Si existe una cotización para esa solicitud, la devuelve completa
  (mismo DTO que ya devuelve `GET /api/cotizaciones/{id}`).
- Si **no** existe ninguna, responde **204 No Content** (no 404 — no es
  un error, es un estado válido) — **nunca crea una cotización nueva**
  como efecto secundario de esta consulta.

---

### HU-B8 — Bug: se puede entregar una cotización con líneas del pliego sin costear

**Como** backend, **quiero** rechazar la entrega de una cotización
mientras queden líneas del pliego sin resolver, **para** que la regla
real no dependa solo de que el frontend se acuerde de validarlo.

**Archivo:** `clsEntregarCotizacionCommand.cs:47-49`
```csharp
if (c.Lineas.Count == 0)
    throw new clsCotizacionSinFleteException("entregar");
```

**Causa raíz:** la única validación exige que la cotización tenga **al
menos una** `clsCotizacionLinea` — nunca compara ese conteo contra el
total de líneas del pliego (`clsSolicitud.Lineas`) ni revisa si alguna
sigue en `EstadoLinea.PorCostear`. Confirmado que no hay ningún otro
validador, máquina de estados ni test que cubra este caso (ver
`2026-10-09_HU-FRONTEND.md`, HU-11, para el mismo hueco confirmado en el botón del
frontend).

**Cómo se construye hoy:** `clsCotizacion.Lineas` se arma
incrementalmente — cada vez que una línea del pliego se resuelve
(`clsAgregarLineaCotizacionCommand.cs:45`, marca
`lineaSol.EstadoLinea = EstadoLinea.Revisada` y agrega su
`clsCotizacionLinea`). Las líneas que quedan `PorCostear` simplemente
nunca generan su `clsCotizacionLinea` — y nada impide entregar con solo
un subconjunto ya agregado.

**Criterios de aceptación:**
- `clsEntregarCotizacionCommandHandler` valida que **todas** las líneas
  de `clsSolicitud.Lineas` (la solicitud asociada) estén en
  `EstadoLinea.AutoResuelta` o `Revisada` — si alguna sigue
  `PorCostear`, rechaza la entrega (nuevo código de error, p. ej.
  `COTIZACION_CON_LINEAS_PENDIENTES`).
- La validación existente de "sin líneas" (`COTIZACION_SIN_FLETE`) se
  mantiene para el caso de cero líneas.

---

### HU-B9 — Exponer el desglose "Piso + Tiempos logísticos" del candidato SICE-TAC

**Como** frontend de Mesa de Costeo, **quiero** recibir por separado el
piso base y el valor de los tiempos logísticos del candidato SICE-TAC,
**para** poder mostrar "$Piso + $Tiempos = $Total" en vez de un solo
número combinado, igual que ya se muestra el desglose de "Flete +
aprovisionamiento" en el flete guardado.

**Causa raíz:** `CandidatoSicetacAsync`
(`clsPropuestaTarifaAdapter.cs:362-376`) calcula el valor combinado con
`clsPisoConHoras.Calcular(fila.Valor, fila.ValorHora, fila.HorasPactadas, fila.ValorSiceConHora, horasSolicitud)`
(línea 371) — `fila.Valor` (el piso base, sin horas) está disponible
ahí mismo, pero **no se expone**: `clsCandidatoTarifaDto` (`PropuestaTarifaDtos.cs:21-22`)
solo tiene `Valor` (el total ya combinado), no hay forma de que el
frontend sepa cuánto del número es piso y cuánto son tiempos
logísticos.

**Arreglo:** agregar 2 campos opcionales a `clsCandidatoTarifaDto`
(`PropuestaTarifaDtos.cs:21-22`):
```csharp
public record clsCandidatoTarifaDto(
    string Fuente, decimal Valor, string Detalle, string Criterio, int Coincidencias,
    string? Advertencia = null,
    decimal? ValorPisoBase = null,
    decimal? ValorTiemposLogisticos = null);
```
Y poblarlos solo para el candidato SICE-TAC
(`CandidatoSicetacAsync`, línea 372-375):
```csharp
return new clsCandidatoTarifaDto(
    "sicetac", valor,
    $"{fila.NombreRuta} · {fila.Configuracion} · {fila.NombreTipoCarga} · {fila.NombreUnidadTransporte}",
    criterio, 1,
    ValorPisoBase: fila.Valor,
    ValorTiemposLogisticos: valor - fila.Valor);
```
`ValorTiemposLogisticos` calculado como la diferencia (no como
`ValorHora × horas` directo) para que sea correcto también en el caso
fallback (`valorSiceConHora` de DWSice, donde no hay una multiplicación
explícita que mostrar, solo la diferencia contra el piso base).

**Plaza e Histórico SAP no llevan este desglose** (quedan `null` en
esos 2 campos) — el concepto de "tiempos logísticos" solo aplica al
candidato SICE-TAC.

**Criterios de aceptación:**
- `GET` de propuesta (el que ya devuelve `clsPropuestaLineaDto`) trae,
  para el candidato `Sicetac`, `ValorPisoBase` y
  `ValorTiemposLogisticos` además del `Valor` combinado de siempre.
- `ValorPisoBase + ValorTiemposLogisticos == Valor` siempre que ambos
  estén informados (control de consistencia, no un campo inventado).
- Plaza e Histórico SAP no cambian — siguen solo con `Valor`.

Ver también la HU de frontend correspondiente (`2026-10-09_HU-FRONTEND.md`,
sección Mesa de Costeo) para la parte de pantalla.

---

### HU-B10 — Editar un flete MANUAL ya guardado, sin duplicar la línea

**Como** Administrador de Fletes, **quiero** poder corregir el valor de
un flete manual que ya guardé, **para** no tener que vivir con un error
de captura hasta el final de la cotización.

**Causa raíz (documentada antes en `2026-10-09_FUTURAS-PROPUESTAS.md`, punto 1):**
`clsAgregarLineaCotizacionCommand` siempre **agrega** una fila nueva
(`c.Lineas.Add(linea)`), nunca revisa si ya existe un flete para esa
línea — habilitar "editar" en el frontend sin un comando nuevo
duplicaría la línea (el viejo y el nuevo conviven) e inflaría el total
en silencio.

**✅ Implementado (2026-10-12), compilado 0 errores:**
- Nuevo archivo `clsEditarLineaCotizacionCommand.cs`
  (`src/Nexo.Application/CasosDeUso/Cotizaciones/`): busca la
  `clsCotizacionLinea` existente por `LineaSolicitudId` dentro de la
  cotización y la actualiza (`ValorFlete`/`ValorRetornoContenedor`), en
  vez de agregar una nueva. Validaciones: cotización debe seguir en
  `Borrador` (`COTIZACION_NO_EDITABLE` si no), la línea debe existir
  (`LINEA_NO_ENCONTRADA` si no) y ser de **fuente Manual**
  (`FLETE_NO_EDITABLE` si no — no se puede editar un flete que vino de
  SICE-TAC/tarifario/histórico). Queda auditado con valor anterior y
  nuevo.
- Nuevo endpoint `PUT /api/cotizaciones/{id}/lineas/{lineaSolicitudId}`
  (`clsCotizacionesController.cs`), mismos roles que agregar flete
  (`AdminTarifas,CoordinadorFletes,Admin`). No recibe `Fuente` en el
  body — la fuente nunca cambia en una edición.
- Registrado en DI (`clsDependencyInjection.cs`, Cotizaciones).

**Alcance decidido (resuelve las preguntas abiertas de
`2026-10-09_FUTURAS-PROPUESTAS.md` punto 1):** solo fletes de fuente **Manual**,
solo mientras la cotización siga en **Borrador** — igual criterio que
ya aplica para agregar un flete nuevo.

**Pendiente:** desplegar (el frontend apunta al backend ya desplegado,
no al código local). La parte de pantalla (HU-15 en `2026-10-09_HU-FRONTEND.md`)
ya está implementada en el sandbox y compilando — no se puede probar de
punta a punta hasta que este backend se despliegue.

---

### HU-B11 — ~~Exponer si una propuesta vino de la regla Solo-SICETAC~~ (REEMPLAZADA — no implementar)

**⚠️ No implementar esta HU.** Su contenido (un booleano `EsSoloSicetac`
en el DTO) quedó corto cuando Sara definió el flujo completo
(2026-10-12): ya no hay 2 casos ("Solo-SICETAC" vs. "comparación"),
hay **3 modos de decisión** — Solo-SICETAC, Plaza vigente mayor que el
piso, y Consulta completa. Un booleano no alcanza para que el frontend
sepa cuándo mostrar el botón "Usar este".

**Reemplazada por el campo `ModoDecision`, dentro de HU-B16** (sección
"Motor de decisión de tarifa", al final de este documento). Se deja
esta entrada solo para que las referencias anteriores
(`REVISION-NEXO-PASO-A-PASO.md`) no queden apuntando a nada.

---

### Ya resuelto (referencia) — 3 campos nuevos en el DTO de costeo

No es una HU pendiente, es contexto: para la HU-4 de frontend (chips de
Línea de negocio / Clase de viaje / Clase de contenedor), ya se hizo el
cambio de backend necesario:

- `clsLineaCosteoDto` (`SolicitudDtos.cs:69-75`) — 3 parámetros nuevos:
  `LineaNegocio? LineaNegocio`, `ClaseViaje? ClaseViaje`,
  `string? ClaseContenedor` (opcionales, no rompen nada existente).
- `clsObtenerCosteoQuery.cs:50-53` — los puebla desde
  `l.LineaNegocio`/`l.ClaseViaje`/`l.ClaseContenedor` (ya existían en la
  entidad `clsLineaSolicitud`, solo no se exponían a este DTO).
- Compilado, 0 errores. **Pendiente:** desplegar, para que el frontend
  (que apunta al backend ya desplegado, no al código local) pueda
  probarlo en pantalla.

---

## Tablero

> **De cara a qué rol:** `clsMetricasController.cs` está protegido con
> `[Authorize(Roles = "LiderProceso,AdminTarifas,Admin")]`. De los 3
> roles que realmente se entregan al cliente (Comercial/AdminTarifas/
> Admin), el Tablero es para **AdminTarifas** y **Admin** — Comercial
> no tiene acceso. `LiderProceso` está en el código pero no se asigna a
> usuarios reales en esta entrega.

### HU-B12 — Filtro de fecha para el bloque "Cumplimiento SLA / cola"

**Como** frontend del Tablero, **quiero** poder pedir
`GET /api/metricas/cumplimiento` con `desde`/`hasta` opcionales, **para**
que las tarjetas de Pendientes/En costeo/Entregadas/Descartadas/Total
solicitudes dejen de ser un acumulado desde el inicio del sistema y se
puedan ver por rango de fechas, igual que ya puede hacerse con "KPIs del
Piloto".

**Causa raíz:** `clsMetricasAdapter.CumplimientoAsync` (hoy,
`clsMetricasAdapter.cs:13-42`) agrupa `db.Solicitudes` por `Estado` sin
ningún filtro — por eso "Entregadas", "Descartadas" y "Total
solicitudes" crecen para siempre y no sirven para ver "qué pasó esta
semana", solo "qué ha pasado desde siempre".

**Qué cambiaría (mismo patrón que `KpisPilotoAsync`, que ya recibe
`desde`/`hasta`):**
- `IMetricasPort.CumplimientoAsync` y el adaptador reciben
  `DateTimeOffset? desde, DateTimeOffset? hasta`.
- La consulta de `db.Solicitudes` se filtra por `CreatedAt` cuando se
  informan (mismo campo que ya usa `KpisPilotoAsync` para
  `LineasSolicitud`).
- `clsCumplimientoDto` agrega `Desde`/`Hasta` como primeros 2 campos
  (igual que ya tiene `clsKpisPilotoDto`).
- El endpoint `GET /api/metricas/cumplimiento` acepta `?desde=&hasta=`
  opcionales por query string, igual que `kpis-piloto`.

**Corrección respecto a la primera versión de esta HU:** se había
decidido dejar Cumplimiento SLA **sin** filtro, a propósito. Sara dio
la especificación final y corrigió eso — **Cumplimiento SLA también se
filtra**, pero por un campo distinto a las otras 5 tarjetas:
- Pendientes/En costeo/Entregadas/Descartadas/Total solicitudes:
  filtran `db.Solicitudes` por `CreatedAt`.
- Cumplimiento SLA: filtra las `Cotizaciones` consideradas (las que
  tienen `EntregadaAt` y `Deadline`) por `EntregadaAt` en el rango —
  "de lo entregado en este periodo, cuánto cumplió su SLA". Las
  licitaciones (campo que ni siquiera se renderiza hoy) se dejan sin
  filtro, sin impacto real.

**Además, ahora es un solo filtro compartido con "KPIs del Piloto"**
(antes se había diseñado como 2 filtros independientes, uno por
bloque) — el mismo `desde`/`hasta` que ya recibe `KpisPilotoAsync` debe
usarse también para `CumplimientoAsync`. En la práctica: el controller
ya tiene ambos endpoints recibiendo los mismos query params; lo que
cambia es que el **frontend** llama a los 2 con el mismo rango desde un
solo control de fecha (ver HU-3 de frontend).

**Criterios de aceptación:**
- `GET /api/metricas/cumplimiento` sin `desde`/`hasta` se comporta
  exactamente igual que hoy (histórico completo) — no rompe nada
  existente.
- Con `desde`/`hasta`, Pendientes/En costeo/Entregadas/Descartadas/
  Total solicitudes solo cuentan solicitudes creadas en ese rango.
- Con `desde`/`hasta`, Cumplimiento SLA solo considera cotizaciones
  entregadas (`EntregadaAt`) dentro de ese rango.

**Nota de alcance:** para "Descartadas" no existe un campo de "fecha en
que se descartó" — Sara decidió aproximar con la fecha de creación de
la solicitud (igual que las demás), aceptando que no es exactamente
"se descartó en esa fecha" sino "se creó en ese rango y hoy está
descartada".

Ver también **HU-3** en `2026-10-09_HU-FRONTEND.md` (sección Tablero) para la
parte de pantalla.

**Nota de proceso (2026-10-12):** esta HU se implementó una primera
vez (backend completo, compilado 0 errores) con el diseño original
(filtros independientes, SLA sin filtro) y se le pidió al frontend que
la prototipara. Sara pidió detener eso y dejarlo solo documentado —
**se revirtió todo** (este backend y lo que el frontend había
alcanzado a tocar). El contenido de esta HU ya refleja el diseño final
corregido (filtro compartido, SLA incluido), no lo que se llegó a
implementar y revertir.

---

### HU-B13 — "Tiempo de costeo del pliego": tiempo híbrido + segmentado por tamaño

**Como** backend, **quiero** calcular el tiempo de costeo de un pliego
usando `EntregadaAt` real cuando hubo intervención manual, y poder
segmentarlo por tamaño de pliego, **para** que la métrica refleje el
ciclo completo y no solo lo que tardó el motor automático.

**Redefinición del tiempo, por pliego (confirmada por Sara — ver
también HU-4 en `2026-10-09_HU-FRONTEND.md`, sección Tablero):**
- Saga `Completado` (se auto-costeó 100% sola): tiempo = como hoy,
  `TerminadoEn - SolicitadoEn` (`clsEstadoAutoCosteo`).
- Saga `Parcial` (alguna línea requirió Admin Tarifas): tiempo = desde
  que se origina la solicitud (`clsSolicitud.CreatedAt`, equivalente a
  `SolicitadoEn`) hasta `Cotizacion.EntregadaAt` real de esa solicitud.
  Si todavía no se ha entregado, ese pliego no entra (igual criterio
  que ya usa Cumplimiento SLA: solo cuenta lo que ya tiene desenlace
  real).

Con eso se arma **una sola lista combinada de tiempos** (ambos
orígenes mezclados) y se calculan `TiempoPromedioResolucionMinutos`,
`TiempoMedianoResolucionMinutos` y `PorcentajePliegosBajo10Minutos`
igual que hoy, pero sobre esa lista combinada.

**Segmentación por tamaño:** agregar un parámetro de filtro por rango
de `TotalLineas` (de la saga) — `1-10`, `11-100`, `+100` — que recalcula
los mismos 3 números solo sobre los pliegos de ese tamaño. "General"
sigue siendo el cálculo sobre todos, sin filtrar por tamaño.

**Complejidad real:** esto no es solo agregar un filtro — hay que
cruzar 2 fuentes (`clsEstadoAutoCosteo` y `Cotizaciones`) por
`SolicitudId`, y el join con Cotizaciones trae la misma limitación ya
documentada en **HU-B8**: mientras no se cierre ese hueco, una
"entrega" podría ser parcial y el tiempo medido no reflejaría el
pliego 100% resuelto. Se documenta como limitación conocida, no como
bloqueante para implementar esto.

**Dependencias:** HU-B8 (idealmente resuelta primero, aunque no es
estrictamente bloqueante), y HU-B12 (mismo endpoint/filtro de fecha
compartido).

**Criterios de aceptación:**
- `KpisPilotoAsync` (o un cálculo equivalente) produce el tiempo
  híbrido descrito arriba, no solo el de la saga.
- Nuevo parámetro de tamaño de pliego (`1-10`/`11-100`/`+100`/sin
  filtro = todos) que recalcula promedio/mediana/%bajo10min.
- Sin el parámetro de tamaño, el resultado es el general (todos los
  pliegos), igual alcance que hoy pero con el tiempo híbrido.

---

## Carga masiva de Excel (Servicios de Biblioteca y Tarifas Plaza)

**Decidido (2026-10-09): no necesita backend.** Se evaluó un diseño con
2 endpoints nuevos (`analizar-excel`/`confirmar-excel`) para separar
"parsear sin guardar" de "guardar", pero Sara decidió resolver la vista
previa editable **100% en el frontend**, reusando el patrón que ya
existe en Servicios de Biblioteca (parsear el Excel en el navegador,
reconstruir un Excel nuevo con los datos ya editados, mandarlo al
mismo endpoint `importar-excel` que ya existe hoy — sin archivo nuevo
ni cambio de contrato). Para Tarifas Plaza esto implica portar a
JavaScript las reglas de validación que hoy solo viven aquí (catálogo
de municipios, rangos de peso, líneas de negocio, clases de viaje) —
riesgo aceptado conscientemente: si esas reglas cambian en el backend,
hay que acordarse de replicarlas también en el frontend. Detalle
completo de la implementación en `2026-10-09_HU-FRONTEND.md`, sección "Carga
masiva de Excel".

---

## Motor de decisión de tarifa (flujo de costeo)

> **Esta sección es la fuente de verdad del flujo de decisión de
> tarifa.** Nace de la auditoría del 2026-10-12: se comparó línea por
> línea el flujo que espera el negocio contra el código real
> (`clsPropuestaTarifaAdapter.cs`, `clsReglaDeCosteo.cs`,
> `clsCapturarSolicitudCommand.cs`, `mesa-costeo-page.component.ts`) y
> **no coinciden**. Aquí queda: (1) el flujo objetivo, confirmado por
> Sara punto por punto; (2) la tabla de casos con ejemplos; (3) qué hay
> hoy vs. qué debe quedar; (4) los casos borde ya decididos; (5) los
> hallazgos relacionados que quedan fuera de alcance; y (6) las HU de
> backend para cerrarlo. La parte de pantalla es **HU-16** en
> `2026-10-09_HU-FRONTEND.md` (sección Mesa de Costeo).
>
> **Aplica a:** todo cálculo de propuesta de flete — auto-costeo al
> capturar, auto-costeo de pliegos (saga) y la "Propuesta de la
> Biblioteca" que ve el Administrador de Tarifas en Mesa de Costeo.
> Todos pasan por el mismo método
> (`clsPropuestaTarifaAdapter.ProponerLineaAsync`, líneas 209-331), así
> que cambiarlo ahí cambia los 3 caminos a la vez.

### 1. Flujo objetivo (confirmado por Sara, 2026-10-12)

**Entrada:** una línea de solicitud con línea de negocio, clase de
viaje, origen, destino, tipo de vehículo, carrocería, clase de
contenedor, peso y horas de cargue/descargue.

**Paso 1 — Regla Solo-SICETAC** (clave ruta + tipo de vehículo,
`clsTexto.ClaveRutaVehiculo`, igual que hoy):
- **1A.** Hay regla **y** hay piso SICE-TAC en `tblRutasSicetac` →
  se usa el piso. **Modo = `SoloSicetac`.** No se consulta nada más,
  no se muestran opciones. **FIN.**
- **1B.** Hay regla pero **no** hay piso SICE-TAC → **Paso 4**
  (consulta completa). *(Cambio frente a hoy — ver tabla de la sección 3.)*
- **1C.** No hay regla → **Paso 2.**

**Paso 2 — Buscar la tarifa de plaza en las 2 tablas, en este orden:**
1. **Externa (SAP)** — `tblSAPTarifas`: primero el camino oficial
   (`ITarifarioSapPort.ConsultarAsync`) y, si no da una vigente, el
   selector tolerante (`CandidatoSapAsync` sobre `dc.TarifasPlazaSap`),
   igual que hoy.
2. **Interna (NEXO)** — `tbl_tarifas_plaza`: **solo si en la externa no
   apareció ninguna VIGENTE.**

Resultado del paso:
- **Plaza vigente** = la primera vigente que aparezca (externa primero,
  luego interna).
- **Plaza vencida (solo referencia)** = únicamente si no apareció
  ninguna vigente: la vencida encontrada (externa primero, luego
  interna).
- **Sin plaza** = no hay ninguna fila (ni vigente ni vencida) en
  ninguna de las 2 tablas → **Paso 4.**

**Paso 3 — Plaza vigente contra el piso SICE-TAC:**
- **3A.** Hay plaza vigente, hay piso SICE-TAC, y la plaza es
  **estrictamente mayor** que el piso → se usa la plaza. **Modo =
  `PlazaVigenteMayor`.** No se consulta el histórico, no se muestran
  opciones. **FIN.**
- **3B.** Cualquier otro caso (plaza vigente menor o igual al piso;
  solo hay plaza vencida; o hay plaza vigente pero no hay piso con qué
  compararla) → **Paso 4.**

**Paso 4 — Consulta completa:**
- **Candidatos:** SICE-TAC (si hay) · Plaza (la vigente si hay; si no,
  la vencida marcada como referencia) · Histórico, buscado también en
  2 tablas: **externo** (`tblSAPTarifasLog`, últimos 3 meses) y, solo
  si ahí no hay nada, **interno** (`tbl_tarifas_plaza_historico`).
- **Recomendado:** el que da la regla actual
  `clsReglaDeCosteo.DecidirConTarifario` — sin cambios: regla del
  mayor, nunca por debajo del piso, la plaza vencida no se propone, y
  sin piso el histórico solo queda como referencia.
- **Modo = `ConsultaCompleta`.**
- **En consulta completa el auto-costeo NO guarda precio** (decisión de
  Sara, 2026-10-12 — reemplaza la anterior "aplica el mayor y queda
  autocosteada"):
  - **Auto-costeo** (al capturar, o al cargar un pliego): la línea queda
    **pendiente** (`PorCostear`), sin flete, con el motivo "Consulta
    completa: el Administrador de Tarifas debe elegir la tarifa". El
    recomendado se calcula igual, pero solo se muestra — no se guarda.
  - **Mesa de Costeo:** el Administrador de Tarifas (Fredy) ve los 3
    valores con el recomendado marcado y **decide**: usa el
    recomendado, elige otro con un clic, o escribe un valor manual
    (HU-16 de frontend). Para un pliego grande puede usar **"Aplicar
    todas las propuestas"**, que aplica el recomendado a todas las
    líneas sin flete, **con confirmación** (HU-19 de frontend).
- **Solo se autocostean solas** (con precio guardado desde la captura)
  las líneas en modo `SoloSicetac` y `PlazaVigenteMayor`.

### 2. Tabla de casos (con ejemplos)

Piso SICE-TAC = 2.500.000 en los casos con piso. "—" = no hay.

| # | ¿Regla Solo-SICETAC? | Piso SICE-TAC | Plaza | Histórico | Modo | Auto-costeo | Recomendado (lo que ve Fredy) |
|---|---|---|---|---|---|---|---|
| 1 | Sí | 2,5 M | (no se mira) | (no se mira) | SoloSicetac | **Guarda 2,5 M** — SICE-TAC | — |
| 2 | Sí | — | 3,0 M vigente | 2,8 M | ConsultaCompleta | Pendiente | 3,0 M — plaza (sin piso, la plaza gobierna sola) |
| 3 | No | 2,5 M | 3,0 M vigente | 3,5 M | PlazaVigenteMayor | **Guarda 3,0 M** — plaza (el histórico no se mira) | — |
| 4 | No | 2,5 M | 2,0 M vigente | 2,2 M | ConsultaCompleta | Pendiente | 2,5 M — SICE-TAC (la plaza y el histórico están bajo el piso: no se pueden usar) |
| 5 | No | 2,5 M | 3,0 M **vencida** | 2,8 M | ConsultaCompleta | Pendiente | 2,8 M — histórico (la vencida no se propone) |
| 6 | No | 2,5 M | — | 2,8 M | ConsultaCompleta | Pendiente | 2,8 M — histórico |
| 7 | No | 2,5 M | — | — | ConsultaCompleta | Pendiente | 2,5 M — SICE-TAC (único valor) |
| 8 | No | — | 3,0 M vigente | — | ConsultaCompleta | Pendiente | 3,0 M — plaza (sin piso, la plaza gobierna sola) |
| 9 | No | — | — | 2,8 M | ConsultaCompleta | Pendiente | **Sin recomendado** (sin piso, el histórico es solo referencia; Fredy decide) |
| 10 | No | — | — | — | ConsultaCompleta | Pendiente | **Sin recomendado** (no hay ningún valor; Fredy escribe uno manual) |

**Consecuencia a tener en cuenta (caso 7):** una línea sin regla y sin
plaza en ninguna tabla, con solo el piso SICE-TAC, también queda
pendiente, aunque haya un solo valor para elegir. En el ambiente de
pruebas la mayoría de líneas no tienen plaza, así que muchas van a
llegar pendientes a Mesa de Costeo — "Aplicar todas las propuestas"
(con confirmación) permite resolverlas de una vez.

**Ojo con el caso 3 — es el cambio de comportamiento más importante:**
hoy el histórico (3,5 M) le ganaría a la plaza (3,0 M) por la regla del
mayor. Con el flujo objetivo, una plaza vigente que supera el piso
**gobierna sola** (así lo dice el PDF y lo confirmó Sara), y el
histórico ni se consulta.

### 3. Cómo está hoy vs. cómo debe quedar

| Punto | Hoy (código real) | Debe quedar | HU |
|---|---|---|---|
| Solo-SICETAC sin piso | Corta con "requiere atención manual" y ni consulta plaza/histórico — los deja en `null` a propósito (`clsPropuestaTarifaAdapter.cs:232-241`). | Pasa a consulta completa (Paso 4). | HU-B16 |
| Plaza vigente mayor que el piso | Entra a la regla del mayor junto con el histórico; si el histórico es mayor, gana el histórico (`clsReglaDeCosteo.cs:60-68`). | La plaza gobierna sola, sin mirar el histórico (Paso 3A). | HU-B16 |
| Plaza vigente menor o igual al piso | Regla del mayor (igual que el Paso 4). | Consulta completa (Paso 4) — mismo valor que hoy, cambia el modo. | HU-B16 |
| De dónde sale la plaza | Solo de SAP externo (`:243-264`). `tbl_tarifas_plaza` (la que se gestiona en Biblioteca → Tarifas de plaza) **nunca se consulta**. | Externa → interna (Paso 2). | HU-B14 |
| Plaza oficial vencida | Si el camino oficial devuelve una tarifa vencida, **no** se intenta el selector tolerante (que solo trae vigentes), porque `plaza` ya no es `null` (`:261-264`). | Se intenta igual el selector tolerante y después la interna, buscando una vigente; la vencida solo queda si no aparece ninguna vigente. | HU-B14 |
| De dónde sale el histórico | Solo del log de SAP (`dc.TarifasLogSap`, `:281`). `tbl_tarifas_plaza_historico` no tiene mapeo de EF — nada la lee, solo el trigger la llena. | Externo → interno (Paso 4). | HU-B15 |
| Saber en qué modo se decidió | No existe en el DTO. | Campo `ModoDecision`. | HU-B16 |
| Auto-costeo en consulta completa | Aplica el mayor y la línea queda `AutoResuelta` (`clsCapturarSolicitudCommand.cs:184-246`; en pliegos, `clsCostearLineaCommand` → `MarcarAutoResueltaAsync`). | **No guarda precio:** la línea queda `PorCostear` para que Fredy elija. Solo se autocostean `SoloSicetac` y `PlazaVigenteMayor`. | HU-B16 |
| Comercial al abrir Mesa de Costeo | Aplica **todas** las propuestas en silencio, sin ver los candidatos (`mesa-costeo-page.component.ts:183-207`, permiso `autoAplicarPropuestas` solo de Comercial). | Solo puede aplicar en silencio las de `SoloSicetac` y `PlazaVigenteMayor`; las de consulta completa se quedan para Fredy (si no, Comercial llenaría las líneas que Fredy debe decidir). | HU-B16 |
| "Iniciar costeo" parece aplicar todo de una vez | No aplica nada (`iniciarCosteo()`, ts:209-232; `clsCrearCotizacionCommand`): recupera la cotización que el auto-costeo ya llenó al capturar, y la pantalla no la mostraba antes por el error de HU-9/HU-B7. | Con el cambio de arriba ya no llega precio en consulta completa; y HU-9/HU-B7 hace que la pantalla muestre la cotización al abrir, sin depender del clic. | HU-B16 + HU-B7/HU-9 |
| Elegir otro candidato en Mesa de Costeo | Solo existe "Usar propuesta", que aplica el ganador; para otro valor hay que reescribirlo a mano. | Botón "Usar este" en cada candidato, solo en `ConsultaCompleta` y solo en líneas sin flete. | HU-16 (frontend) |
| Guardar el Histórico de la propuesta por el formulario | Puede fallar con `HISTORICA_VENCIDA`: la validación mira las "Históricas sugeridas" (fletes ganados), otra tabla (`clsAgregarLineaCotizacionCommand.cs:50-60`). | Se puede guardar si coincide con el histórico de la propuesta. | HU-B17 |
| Guardar un flete por debajo del piso SICE-TAC | Se puede (el backend no lo valida; en pantalla solo hay un popup de "confirmar de todas formas" para manual, HU-12). | Bloqueado para cualquier fuente, en pantalla y en el servidor. | HU-B18 + HU-17 (frontend) |

### 4. Decisiones confirmadas por Sara (2026-10-12)

1. **Orden:** primero Regla Solo-SICETAC, después Tarifas Plaza, después
   consulta completa.
2. **Solo-SICETAC sin piso** → consulta completa (no "atención manual").
3. **Plaza vigente** → se usa sola **solo si además es mayor que el piso
   SICE-TAC** (como el PDF `NEXO FLUJO.pdf`). Si es menor o igual →
   consulta completa.
4. **Sin plaza en ninguna de las 2 tablas** → consulta completa (igual
   que el PDF: la rama "No" de "¿Existe tarifa de plaza?" va a la
   consulta completa).
5. **Auto-costeo en consulta completa** → **no guarda precio**: la línea
   queda pendiente para que el Administrador de Tarifas elija (o use
   "Aplicar todas las propuestas", con confirmación). *(Cambió en la
   misma conversación: primero se había decidido "aplica el mayor y
   queda autocosteada"; Sara lo cambió al aclarar la confusión de
   "Iniciar costeo".)* Solo se autocostean solas `SoloSicetac` y
   `PlazaVigenteMayor`.
6. **Mesa de Costeo** → el sistema sugiere el mayor, pero el
   Administrador de Tarifas tiene la última palabra — solo en consulta
   completa; en los otros 2 modos no hay nada que elegir.
7. **Doble fuente:** plaza = externa (SAP) → interna (NEXO); histórico =
   externo (SAP log) → interno (`tbl_tarifas_plaza_historico`). Si no
   aparece en una, se busca en la otra.
8. **Vigente gana a vencida:** si la externa solo tiene vencida y la
   interna tiene vigente, se usa la interna vigente.
9. **Nunca por debajo del piso SICE-TAC:** no se puede guardar un flete
   por debajo del piso, sea de tarifario, histórico o manual — se
   **bloquea** con un aviso, no se pide confirmación (HU-B18 en backend,
   HU-17 en frontend; reemplaza el "confirmar de todas formas" de
   HU-12).
10. **Desviación 1 de QA:** se investiga (HU-B19).

### 5. Casos borde — decididos aquí para que no quede ambiguo

Estos no los preguntó nadie explícitamente; se dejan definidos para que
quien implemente no tenga que adivinar. Si Sara no está de acuerdo con
alguno, se cambia aquí.

- **Plaza vigente sin piso SICE-TAC** → Paso 4, no 3A: "mayor que el
  piso" no se puede comprobar sin piso. El valor que resulta es el
  mismo (la regla actual propone la plaza sola cuando no hay piso), solo
  cambia el modo — en Mesa de Costeo el Administrador puede elegir.
- **Empate exacto plaza = piso** → Paso 4 (el PDF dice "si es mayor").
- **Plaza vigente en las 2 tablas a la vez** → gana la externa (es la
  primera en el orden; la interna ni se consulta).
- **Plaza vencida en las 2 tablas** → la referencia es la externa.
- **Ajustes de vigencia de plaza** (`IAjustesVigenciaPlazaRepository`,
  usado en `CandidatoSapAsync` líneas 503-512): solo aplican a filas de
  SAP (su llave es el `Id` entero de SAP). A la tabla interna no se le
  aplican — ahí la vigencia se corrige editando la tarifa directamente.
- **Trazabilidad (RN-05):** el campo `Fuente` del candidato sigue siendo
  `"plaza"` / `"historico_sap"` aunque venga de la tabla interna — si se
  inventara un valor nuevo, el mapeo a `FuenteFlete`
  (`clsCapturarSolicitudCommand.cs:206-212` y
  `clsAplicarPropuestasCommand`) lo convertiría en `Manual` sin avisar.
  La tabla de origen va en un campo **nuevo** del candidato,
  `TablaOrigen` (`"SAP"` / `"NEXO"`), que es lo que el frontend usa para
  la etiqueta.
- **Histórico interno — ventana de 3 meses:** por `ReemplazadoAt` (el
  momento en que ese valor dejó de estar vigente en NEXO), que es el
  equivalente a `FechaCreacion` del log de SAP (`:514-519`). Y "último
  registro por tarifa" agrupando por `TarifaPlazaId`, igual que SAP
  agrupa por `IdTarifa` (`:550-558`).
- **El histórico interno nace vacío:** el trigger
  `TRG_tarifas_plaza_historico` (`docs/Database/ESQUEMA_NEXO.sql:782-802`)
  solo guarda una fila cuando alguien **edita** el valor, la vigencia o
  los detalles de una tarifa interna (también al reimportar por Excel
  una fila que ya existía). Es normal que al principio casi nunca
  aporte candidato.
- **Modos sin nada que elegir:** en `SoloSicetac` el DTO trae solo el
  candidato SICE-TAC (plaza e histórico en `null`, igual que hoy); en
  `PlazaVigenteMayor` trae SICE-TAC y plaza (histórico en `null`,
  porque no se consulta).

### 6. Hallazgos relacionados — FUERA del alcance de estas HU

Encontrados en la misma auditoría. **No se resuelven con HU-B14/B15/B16**
y quedan pendientes de decisión de Sara:

- ~~El cruce de ciudades contra la plaza externa es frágil~~ →
  **decidido: se conecta el traductor de zonas, ver HU-B22.**
- **En el ambiente de pruebas la plaza externa solo tiene Contenedor:**
  107 filas, todas `claseViaje = Contenedor`
  (`tests/QA/pr-253-fix-seeders-reglas-sicetac-tarifas-plaza/README.md:23`).
  Para Cupo/Carga Suelta/Extradimensionado la externa nunca encuentra
  plaza — con HU-B14 la interna sí podría.
- ~~QA "Desviación 1"~~ → **decidido: se investiga, ver HU-B19.**
- **Los 2 caminos de auto-costeo no dejan la línea igual:** al capturar,
  el flete se agrega a la cotización en el momento
  (`clsCapturarSolicitudCommand.cs:194-216`); al cargar un pliego (saga),
  la línea queda `AutoResuelta` pero el flete **no** se agrega
  (`clsEfLineasCosteadasRepository.cs:10-24`) — llega después, cuando
  alguien hace "Aplicar propuestas" (Comercial lo hace en silencio al
  abrir Mesa de Costeo). → **Cubierto:** con la decisión 5 nueva, en
  consulta completa ninguno de los 2 caminos guarda precio; para los
  modos automáticos, HU-B16 (Paso 4, punto 3) deja como recomendado que
  el pliego también guarde el flete, y mientras tanto "Aplicar todas
  las propuestas" (HU-19) las llena de una vez.

---

### HU-B14 — Doble fuente para Tarifas Plaza: externa (SAP) → interna (NEXO)

**Como** motor de costeo, **quiero** buscar la tarifa de plaza primero
en SAP y, si ahí no hay una vigente, en la tabla propia de NEXO, **para**
que lo que el Administrador de Tarifas gestiona en Biblioteca → Tarifas
de plaza se use de verdad al costear.

**Causa raíz:** `tbl_tarifas_plaza` se creó en el issue #210
(S4-HU-10.1, commits `d4fcb5e` y `c01b0f2`) y desde entonces ningún
commit la conectó al motor: `clsTarifaPlaza`, `ITarifasPlazaRepository`
y `clsEfTarifasPlazaRepository` tienen un solo commit en toda su
historia (su creación), y ninguno de los 10 commits posteriores de
`clsPropuestaTarifaAdapter.cs` la menciona. Hoy solo la usan el CRUD,
la exportación y la importación de Excel.

**Paso 1 — Puerto nuevo** (Application, junto a las demás interfaces de
`CasosDeUso/TarifasPlaza/Interfaces/`):
```csharp
public sealed record clsConsultaTarifaPlazaInterna(
    string Origen, string Destino,
    LineaNegocio? LineaNegocio, ClaseViaje? ClaseViaje,
    string? ClaseContenedor, string? TipoVehiculo, decimal? Peso);

public sealed record clsResultadoPlazaInterna(
    clsCandidatoTarifaDto? Vigente, clsCandidatoTarifaDto? Vencida);

public interface ITarifaPlazaInternaPort
{
    Task<clsResultadoPlazaInterna> BuscarAsync(clsConsultaTarifaPlazaInterna consulta, CancellationToken ct);
}
```

**Paso 2 — Implementación** (Infrastructure, p. ej.
`TarifasPlaza/Adaptadores/clsTarifaPlazaInternaAdapter.cs`, sobre
`clsNexoDbContext.TarifasPlaza` con `Include(Origen/Destino/Peso)`).
Criterio de cruce, en este orden:

1. **Origen y destino (obligatorios):** municipios de `tbl_municipios`
   cuyo nombre normalizado esté contenido en el texto de la ruta, o al
   revés — misma regla tolerante que ya usa `CodigosDaneAsync`
   (`clsPropuestaTarifaAdapter.cs:588-604`). La ruta guarda texto libre
   tipo `"Medellín (ANT)"`; `Normalizar` (`:621-631`) quita tildes y
   pasa a mayúsculas.
2. **Nivel exacto:** además de origen/destino, deben coincidir:
   - Línea de negocio: el enum de la línea → su código SAP
     (`CodigoLinea`, `clsTarifarioSapAdapter.cs:45-52`) → la
     `Descripcion` del catálogo (`ILineasNegocioPort`) → comparada
     normalizada contra `tbl_tarifas_plaza.LineaNegocio` (que guarda el
     nombre del catálogo, no el Id).
   - Clase de viaje: igual, con `CodigoClaseViaje`
     (`clsTarifarioSapAdapter.cs:56-60`) e `IClasesViajePort`.
   - Clase de contenedor: si la línea la trae.
   - Tipo de vehículo: si la fila lo trae.
   - Banda de peso: si la fila tiene `PesoId` y la línea trae peso
     (`PesoMinimo <= peso <= PesoMaximo`, o banda abierta si
     `PesoMaximo` es `null`).
3. **Nivel tolerante** (solo si el nivel exacto dejó 0 filas): origen y
   destino siguen siendo obligatorios; los demás filtros se aplican
   **solo si dejan al menos una fila** — mismo patrón que
   `CandidatoSapAsync` para vehículo y peso (`:521-533`, `:566-576`).
4. Separar en **vigentes** (`Vigencia >= hoy`) y **vencidas**; de cada
   grupo, la de **mayor `Valor`** (igual que SAP, `:578`).
5. Construir el candidato igual que los de SAP:
   `Fuente = "plaza"`, `TablaOrigen = "NEXO"`,
   `Detalle = "{Origen.Municipio} → {Destino.Municipio} · vigencia {Vigencia:yyyy-MM-dd} · {DetallesTarifa}"`,
   `Criterio` con los filtros que se aplicaron, `Coincidencias` con la
   cantidad de filas candidatas.

**Paso 3 — Integración** en `ProponerLineaAsync`
(`clsPropuestaTarifaAdapter.cs:243-264`), reemplazando la resolución
actual de `plaza`:
```csharp
// 1. Externa — camino oficial
var oficialTarifa = oficial?.Tarifa;
var oficialVencida = oficial?.Vencida == true;
clsCandidatoTarifaDto? plazaVigente = oficialVencida ? null : oficialTarifa;
clsCandidatoTarifaDto? plazaVencida = oficialVencida ? oficialTarifa : null;

// 2. Externa — selector tolerante (solo trae vigentes). Hoy no se intenta
//    si el oficial devolvió una vencida; ahora sí, buscando una vigente.
if (plazaVigente is null && dc is not null)
    plazaVigente = await CandidatoSapAsync(dc, dc.TarifasPlazaSap, "plaza", origen, destino, vehiculo, linea.Peso, soloVigentes: true, ajustesVigencia, ct);

// 3. Interna NEXO — solo si en SAP no apareció ninguna vigente
if (plazaVigente is null)
{
    var interna = await plazaInterna.BuscarAsync(new clsConsultaTarifaPlazaInterna(
        linea.clsRuta.Origen, linea.clsRuta.Destino, linea.LineaNegocio, linea.ClaseViaje,
        linea.ClaseContenedor, vehiculo, linea.Peso), ct);
    plazaVigente = interna.Vigente;
    plazaVencida ??= interna.Vencida; // la vencida externa tiene prioridad como referencia
}
if (plazaVigente is not null) plazaVencida = null; // hay vigente: la vencida no se muestra
```
Los candidatos de SAP (oficial y tolerante) llevan `TablaOrigen = "SAP"`.
La rama de plaza "en vivo" (`_sapEnVivo`, `:291-304`) se queda igual.
`ITarifaPlazaInternaPort` se inyecta en el constructor del adaptador
(`:54-69`) y se registra en `clsDependencyInjection.cs` de
Infrastructure, junto a `ITarifasPlazaRepository` (línea 201).

**Criterios de aceptación:**
- Una ruta/vehículo que solo existe en `tbl_tarifas_plaza` (no en SAP)
  muestra su tarifa como candidato de plaza, con `TablaOrigen = "NEXO"`.
- Si SAP tiene una vigente, se usa la de SAP y la interna ni se consulta.
- Si SAP solo tiene vencida y la interna tiene vigente, se usa la
  interna vigente.
- Si ninguna tiene vigente, la vencida que queda como referencia es la
  de SAP si existe; si no, la interna.
- `Fuente` sigue siendo `"plaza"` en todos los casos (no se rompe el
  mapeo a `FuenteFlete.Tarifario`).

**Pruebas sugeridas:** una por cada criterio de arriba, con el puerto
interno simulado, más pruebas del adaptador interno para el nivel
exacto vs. tolerante y para la banda de peso abierta.

---

### HU-B15 — Doble fuente para Histórico: externo (SAP) → interno (`tbl_tarifas_plaza_historico`)

**Como** motor de costeo, **quiero** buscar el histórico primero en el
log de SAP y, si ahí no hay nada, en el histórico propio de NEXO,
**para** que los valores anteriores de las tarifas que gestiona NEXO
también cuenten como referencia en la consulta completa.

**Causa raíz:** `tbl_tarifas_plaza_historico` existe en la base y el
trigger `TRG_tarifas_plaza_historico` la llena sola
(`docs/Database/ESQUEMA_NEXO.sql:441-458`, `:782-802`), pero **no está
mapeada en Entity Framework** — el propio código lo dice
(`clsNexoDbContext.cs:403-405`: "no mapeada: la llena solo un trigger
de BD"). Hoy no hay forma de consultarla desde la aplicación.

**Paso 1 — Mapear la tabla (solo lectura):**
- Entidad nueva `clsTarifaPlazaHistorico` en
  `src/Nexo.Domain/Costeo/Entidades/`, con las columnas de la tabla:
  `Id`, `TarifaPlazaId`, `LineaNegocio`, `ClaseViaje`,
  `OrigenMunicipioId`, `DestinoMunicipioId`, `ClaseContenedor`,
  `PesoId`, `TipoVehiculo`, `Valor`, `Vigencia`, `DetallesTarifa`,
  `CreadoPor`, `CreatedAt`, `ReemplazadoAt`.
- `DbSet<clsTarifaPlazaHistorico> TarifasPlazaHistorico` en
  `clsNexoDbContext` y `b.Entity<clsTarifaPlazaHistorico>(e => e.ToTable("tbl_tarifas_plaza_historico"))`,
  con navegación opcional a `clsMunicipio` (origen/destino) y
  `clsRangoPeso` por sus columnas de Id.
- **Sin migración de EF:** la tabla ya existe, mismo criterio que
  `tbl_tarifas_plaza` (`clsNexoDbContext.cs:403-405`).
- **La aplicación nunca escribe en esta tabla** — solo el trigger. No
  agregarle ningún método de alta/edición.

**Paso 2 — Puerto y búsqueda:** igual que HU-B14 (mismo
`clsConsultaTarifaPlazaInterna`, mismo cruce exacto → tolerante), con
estas diferencias:
- **Ventana:** solo filas con `ReemplazadoAt >= hoy − 3 meses`.
- **Último registro por tarifa:** agrupar por `TarifaPlazaId` y quedarse
  con el de `ReemplazadoAt` más reciente.
- **No se separa vigente/vencida** (el histórico de SAP tampoco lo hace:
  `soloVigentes: false`, `:281`).
- Descartar `Valor <= 0`; de las que quedan, la de mayor `Valor`.
- Candidato: `Fuente = "historico_sap"` (se conserva el nombre para no
  romper el mapeo a `FuenteFlete.Historica`), `TablaOrigen = "NEXO"`.

```csharp
public interface IHistoricoPlazaInternoPort
{
    Task<clsCandidatoTarifaDto?> BuscarAsync(clsConsultaTarifaPlazaInterna consulta, CancellationToken ct);
}
```

**Paso 3 — Integración** en `ProponerLineaAsync` (`:281`):
```csharp
var historico = dc is null ? null : await CandidatoSapAsync(dc, dc.TarifasLogSap, "historico_sap", origen, destino, vehiculo, linea.Peso, soloVigentes: false, ajustesVigencia, ct, ventanaMeses: 3);
historico ??= await historicoInterno.BuscarAsync(new clsConsultaTarifaPlazaInterna(
    linea.clsRuta.Origen, linea.clsRuta.Destino, linea.LineaNegocio, linea.ClaseViaje,
    linea.ClaseContenedor, vehiculo, linea.Peso), ct);
```
La regla 6 de `clsReglaDeCosteo` (sin piso, el histórico solo es
referencia) se mantiene sin cambios — aplica igual venga de donde venga.

**Criterios de aceptación:**
- Si el log de SAP tiene histórico para la ruta, se usa ese y el interno
  ni se consulta.
- Si SAP no tiene nada y en `tbl_tarifas_plaza_historico` hay un valor
  reemplazado en los últimos 3 meses, ese aparece como candidato de
  histórico, con `TablaOrigen = "NEXO"`.
- Un valor reemplazado hace más de 3 meses no aparece.
- Si una misma tarifa interna se editó varias veces, solo cuenta su
  valor anterior más reciente.

---

### HU-B16 — Nuevo orden de decisión (Pasos 1 a 4) + campo `ModoDecision`

**Como** motor de costeo, **quiero** decidir en el orden que definió el
negocio (Solo-SICETAC → plaza vigente mayor que el piso → consulta
completa) y decir en qué modo se decidió, **para** que el auto-costeo y
Mesa de Costeo se comporten como espera Sara, y el frontend sepa cuándo
el Administrador tiene algo que elegir.

**Reemplaza a HU-B11** (el booleano `EsSoloSicetac` no alcanzaba para 3
modos).

**Paso 1 — Dominio** (`src/Nexo.Domain/Costeo/Reglas/clsReglaDeCosteo.cs`).
`Decidir` y `DecidirConTarifario` **no se tocan** (siguen siendo la
regla del Paso 4); se agrega encima:
```csharp
public enum ModoDecision { SoloSicetac, PlazaVigenteMayor, ConsultaCompleta }

public sealed record clsDecisionDeFlujo(clsResultadoDeCosteo Resultado, ModoDecision Modo);

public static clsDecisionDeFlujo DecidirFlujo(
    bool hayReglaSoloSicetac,
    clsCandidatoDeTarifa? sicetac,
    clsCandidatoDeTarifa? plazaVigente,
    clsCandidatoDeTarifa? plazaVencida,
    clsCandidatoDeTarifa? historico)
{
    // Paso 1A
    if (hayReglaSoloSicetac && sicetac is not null)
        return new(new(sicetac.Valor, sicetac.Fuente,
            $"Ruta marcada como SOLO SICE-TAC por el Administrador: se usa el piso SICE-TAC ({sicetac.Valor:C0}) sin comparar con plaza ni histórico."),
            ModoDecision.SoloSicetac);

    // Paso 3A (1B cae aquí abajo si no hubo piso: sigue al Paso 4)
    if (!hayReglaSoloSicetac && plazaVigente is not null && sicetac is not null && plazaVigente.Valor > sicetac.Valor)
        return new(new(plazaVigente.Valor, plazaVigente.Fuente,
            $"Tarifa de plaza vigente ({plazaVigente.Valor:C0}) supera el piso SICE-TAC ({sicetac.Valor:C0}): se usa la plaza."),
            ModoDecision.PlazaVigenteMayor);

    // Paso 4 — consulta completa, con la regla de hoy
    var resultado = plazaVigente is not null
        ? Decidir(sicetac, plazaVigente, historico)
        : DecidirConTarifario(sicetac, plazaVencida, historico, tarifarioVencido: plazaVencida is not null);
    return new(resultado, ModoDecision.ConsultaCompleta);
}
```
(En el caso 1B — regla Solo-SICETAC sin piso — no se evalúa el Paso
3A: va directo al Paso 4, como decidió Sara.)

**Paso 2 — Adaptador** (`clsPropuestaTarifaAdapter.ProponerLineaAsync`,
`:209-331`):
- El bloque Solo-SICETAC (`:232-241`) **deja de hacer `return`
  cuando no hay piso**: solo retorna en el caso 1A (con piso). Sin piso,
  sigue al resto del método (plaza → histórico → consulta completa).
- Con la plaza ya resuelta por HU-B14 (`plazaVigente`/`plazaVencida`) y
  el piso, si se cumple el Paso 3A se puede **omitir la consulta del
  histórico** (no se muestra en ese modo).
- Reemplazar la llamada a `DecidirConTarifario` (`:306-307`) por
  `DecidirFlujo(...)`.
- `Vencida` (`:330`) pasa a significar "el candidato de plaza que se
  muestra es una vencida de referencia" (`plazaVencida is not null`),
  venga de SAP o de NEXO. La alerta de tarifa vencida
  (`PublicarVencidaSiAplicaAsync`, `:118-128`) sigue funcionando igual.

**Paso 3 — DTOs** (`src/Nexo.Application/Common/Dtos/PropuestaTarifaDtos.cs`):
```csharp
public record clsCandidatoTarifaDto(
    string Fuente, decimal Valor, string Detalle, string Criterio, int Coincidencias,
    string? Advertencia = null,
    string? TablaOrigen = null);            // "SAP" | "NEXO" (nuevo, HU-B14/B15)

public record clsPropuestaLineaDto(
    Guid LineaId, string? clsRuta, string? Vehiculo, decimal? Peso,
    clsCandidatoTarifaDto? Sicetac, clsCandidatoTarifaDto? Plaza, clsCandidatoTarifaDto? HistoricoSap,
    decimal? ValorPropuesto, string? FuentePropuesta, string Justificacion,
    clsCandidatoTarifaDto? RetornoContenedor = null,
    bool Vencida = false,
    string ModoDecision = "ConsultaCompleta"); // "SoloSicetac" | "PlazaVigenteMayor" | "ConsultaCompleta" (nuevo)
```
`ModoDecision` viaja como texto (no como número de enum) para que el
frontend lo lea sin depender de la configuración de serialización.
Ojo: si se implementa también HU-B9 (desglose piso + tiempos), los 2
campos que agrega a `clsCandidatoTarifaDto` van en el mismo record.

**Paso 4 — Quién usa la propuesta y qué cambia en cada uno** (por la
decisión 5: en consulta completa el auto-costeo **no** guarda precio):

1. **`ProponerParaLineaAsync`** (camino del pliego, `:79-107`): hoy
   devuelve solo valor/fuente/justificación (`clsResultadoDeCosteo`).
   Debe devolver también el **modo** — por ejemplo devolviendo
   `clsDecisionDeFlujo` (resultado + modo) en vez de
   `clsResultadoDeCosteo`, ajustando la interfaz `IPropuestaDeLineaPort`.

2. **Auto-costeo al capturar** (`AutoCostearAsync`,
   `clsCapturarSolicitudCommand.cs:194-222`): guardar el flete **solo si
   el modo es `SoloSicetac` o `PlazaVigenteMayor`**. Si es
   `ConsultaCompleta`, la línea queda `PorCostear` con
   `MotivoPendiente = "Consulta completa: el Administrador de Tarifas
   debe elegir la tarifa (recomendado: {valor} — {fuente})."` (o sin la
   parte del recomendado si no lo hay).

3. **Auto-costeo de pliegos** (`clsCostearLineaCommand.cs:39-62`): si el
   modo es `ConsultaCompleta`, ir por la misma rama que hoy usa
   `RequiereTarifaManual` (registrar pendiente `Manual`,
   `MarcarPendienteAsync` con el motivo de arriba y publicar
   `clsLineaRequiereTarifaManual`) en vez de `MarcarAutoResueltaAsync`.
   La saga lo cuenta como "requiere persona" y cierra el trabajo en
   `Parcial`, que es lo correcto. (Ese evento solo lo consume la saga
   — no manda correos.)
   - **Recomendado, de paso:** en los modos automáticos, guardar el
     flete en la cotización igual que el camino de captura — hoy el
     pliego solo marca la línea `AutoResuelta` sin guardar el precio
     (`clsEfLineasCosteadasRepository.cs:10-24`). Si no se hace, esas
     líneas quedan sin precio hasta que alguien use "Aplicar todas las
     propuestas".

4. **"Aplicar todas las propuestas"** (`clsAplicarPropuestasCommand`,
   botón de Fredy con confirmación, HU-19 de frontend): **sigue
   aplicando el recomendado a todas las líneas sin flete, también las
   de consulta completa** — es justo la forma rápida de que Fredy
   resuelva un pliego grande de una vez, confirmando antes.

5. **Auto-aplicado silencioso del rol Comercial** (mismo endpoint
   `POST /api/cotizaciones/{id}/aplicar-propuestas`, llamado por
   `intentarAutoAplicar()` al abrir Mesa de Costeo): **no debe** llenar
   las de consulta completa — si no, Comercial terminaría decidiendo
   las líneas que son de Fredy. Agregar al endpoint un parámetro
   opcional `soloAutomaticas` (por defecto `false`):
   ```csharp
   public async Task<ActionResult<clsAplicarPropuestasResultDto>> AplicarPropuestas(
       Guid id, [FromQuery] bool soloAutomaticas = false, CancellationToken ct = default)
   ```
   Con `soloAutomaticas = true`, `clsAplicarPropuestasCommand` salta las
   líneas cuyo `ModoDecision` sea `ConsultaCompleta`. El frontend del
   Comercial lo llama con `true` (ver HU-16 de frontend); el botón de
   Fredy, sin el parámetro.

6. Los valores de `Fuente` no cambian, así que el mapeo a `FuenteFlete`
   (`clsCapturarSolicitudCommand.cs:206-212`, `clsAplicarPropuestasCommand`)
   tampoco.

**Efectos esperados que conviene avisar:**
- La métrica "% líneas auto-costeadas" del Tablero va a **bajar**: las
  de consulta completa ya no cuentan como auto-costeadas.
- Llegan más líneas pendientes a Mesa de Costeo (caso 7 de la tabla:
  solo piso SICE-TAC, sin plaza → también pendiente).
- Las solicitudes con líneas de consulta completa ya no quedan listas
  solas: alguien tiene que decidir antes de poder entregarlas.

**Criterios de aceptación** (los 10 casos de la tabla de la sección 2):
- Cada caso de la tabla produce exactamente el modo y el valor de la
  tabla.
- Caso 1B: una ruta con regla Solo-SICETAC y sin piso ya **no** queda en
  "requiere atención manual" — muestra plaza/histórico y decide por
  consulta completa.
- Caso 3: plaza vigente mayor que el piso gana aunque haya un histórico
  más alto.
- El JSON de la propuesta trae `modoDecision` y, en cada candidato,
  `tablaOrigen`.
- Al capturar una solicitud o cargar un pliego, las líneas en
  `ConsultaCompleta` quedan `PorCostear` y **sin flete**, con el motivo
  "Consulta completa..."; las de `SoloSicetac` y `PlazaVigenteMayor`
  quedan con su flete.
- `aplicar-propuestas?soloAutomaticas=true` no toca las líneas de
  consulta completa; sin el parámetro, las aplica todas (como hoy).

**Pruebas:**
- Dominio: una prueba de `DecidirFlujo` por cada fila de la tabla de la
  sección 2. Las pruebas actuales de `Decidir`/`DecidirConTarifario`
  siguen valiendo (esas funciones no cambian).
- Revisar las pruebas de integración que asumen el resultado anterior
  del caso 3 (p. ej. `clsPliegoDatasetValidacionTests`) y el archivo de
  escenarios `docs/costeo/escenarios-reglas-negocio.feature`
  (`@regla-del-mayor`): actualizarlos al flujo nuevo.

**Orden sugerido de implementación:** HU-B15 (mapeo de la tabla) y
HU-B14 (puertos internos) primero, porque HU-B16 los usa; después
HU-B17; HU-16 de frontend al final, cuando el backend ya devuelva
`modoDecision` y `tablaOrigen`.

---

### HU-B17 — Poder guardar el candidato Histórico de la propuesta desde Mesa de Costeo

**Como** Administrador de Tarifas, **quiero** poder guardar como flete
el valor Histórico que me muestra la propuesta (con "Usar propuesta" o
"Usar este"), **para** que la "última palabra" de la consulta completa
funcione también con ese candidato.

**Causa raíz (bug que ya existe hoy, no lo crea HU-16):**
`clsAgregarLineaCotizacionCommand.cs:50-60` — cuando la fuente es
`Historica`, exige que exista una tarifa **histórica sugerida** vigente
de la última semana (`ITarifasHistoricasPort.HayVigenteAsync`, tabla
`tbl_tarifas_historicas`: los fletes que NEXO **ganó**, RN-04), y si no,
rechaza con `HISTORICA_VENCIDA`. Esa regla se pensó para el botón
"Usar" de "Históricas sugeridas". Pero la fuente `Historica` también es
la que usa el formulario cuando el valor viene del **histórico de la
propuesta** (`fuenteDesdePropuesta('historico_sap') → 'historica'`,
`mesa-costeo-page.component.ts:1060-1067`), que sale de **otra tabla**
(log de SAP, o `tbl_tarifas_plaza_historico` con HU-B15) y ya trae su
propia ventana de 3 meses. Resultado: guardar el histórico de la
propuesta por el formulario puede fallar aunque sea un valor válido.

Además hay una inconsistencia: "Aplicar propuestas"
(`clsAplicarPropuestasCommand`) agrega directo un flete con fuente
`Historica` cuando el ganador es el histórico, **sin** esa validación —
así que el mismo valor se puede guardar en lote pero no línea por línea.

**Opciones:**
- **A (recomendada):** en `clsAgregarLineaCotizacionCommand`, para
  fuente `Historica` aceptar si se cumple **cualquiera** de las 2:
  (1) hay una histórica sugerida vigente (regla de hoy), **o** (2) el
  valor coincide con el candidato Histórico de la propuesta de esa
  línea, recalculada en el servidor (`IPropuestaTarifaPort.ProponerAsync`,
  que ya tiene caché). El servidor no depende de lo que diga el cliente.
- **B (más simple, más débil):** agregar al request un campo
  `OrigenHistorica` (`"sugerida"` | `"propuesta"`) y saltar la validación
  de 1 semana cuando es `"propuesta"`. Más fácil, pero el servidor
  confía en lo que manda el navegador.

**✅ Decidido (Sara, 2026-10-12): opción A.** El servidor acepta el
histórico si coincide con el candidato Histórico de la propuesta de esa
línea, recalculada en el servidor — no depende de lo que diga el
navegador.

**Criterios de aceptación:**
- Guardar por el formulario el valor Histórico que muestra la propuesta
  funciona aunque no haya "Históricas sugeridas" vigentes.
- Guardar una histórica que **no** viene ni de las sugeridas vigentes ni
  de la propuesta sigue fallando con `HISTORICA_VENCIDA`.
- En el frontend (HU-16), el desplegable de fuente no debe bloquear
  "Histórica" cuando el valor lo puso "Usar este"/"Usar propuesta"
  desde el candidato histórico.

---

### HU-B18 — Nunca guardar un flete por debajo del piso SICE-TAC (validación en el servidor)

**Como** negocio, **quiero** que el sistema **no permita** guardar un
flete por debajo del piso SICE-TAC, venga de donde venga (tarifario,
histórico o manual), **para** no cotizar nunca por debajo del piso
legal (RN-02 — es sanción).

**Decisión de Sara (2026-10-12):** "nunca se puede usar un valor por
debajo del piso, ni de tarifas, ni histórico, ni manual; en este caso
ni siquiera se le permite usarlo y se le debe mandar un aviso". Esto
**reemplaza** lo que se había hecho en HU-12 (un popup que dejaba
"confirmar de todas formas"): ahora es un **bloqueo**, no una
confirmación.

**Por qué también en el backend (no solo en pantalla):** si solo se
bloquea en el frontend, cualquiera puede saltárselo llamando a la API
directo — y la regla es "nunca". La pantalla avisa (HU-17 de frontend);
el servidor garantiza.

**Dónde:**
- `clsAgregarLineaCotizacionCommand.cs` (agregar flete) — hoy no
  compara contra el piso (líneas 32-76).
- `clsEditarLineaCotizacionCommand.cs` (editar flete manual, HU-B10).

**Cómo:** antes de guardar, obtener el piso SICE-TAC de esa línea
**con el mismo cálculo de la propuesta** (piso + tiempos logísticos de
la línea): `IPropuestaTarifaPort.ProponerAsync(c.SolicitudId)` (ya
tiene caché) → la línea → `Sicetac?.Valor`. Si hay piso y
`ValorFlete < piso` → rechazar:
```csharp
throw new clsValidationFailedException("FLETE_BAJO_PISO_SICETAC",
    $"El flete ({command.ValorFlete:C0}) está por debajo del piso SICE-TAC ({piso:C0}). No se puede guardar un valor por debajo del piso.");
```
Si la línea **no tiene piso** SICE-TAC (no hay valor para la ruta), no
hay contra qué comparar y se guarda normal.

**Qué no se toca:** el auto-costeo y "Aplicar propuestas" ya nunca
proponen por debajo del piso (`clsReglaDeCosteo`, regla 1), así que no
necesitan esta validación.

**Criterios de aceptación:**
- Agregar o editar un flete por debajo del piso SICE-TAC de la línea
  falla con `FLETE_BAJO_PISO_SICETAC`, sea cual sea la fuente
  (`tarifario`, `historica`, `manual`).
- Un flete igual o mayor al piso se guarda normal.
- Una línea sin piso SICE-TAC se guarda normal (no hay con qué
  comparar).

---

### HU-B19 — Investigar la "Desviación 1" de QA: plaza vigente y mayor que el sistema no propuso

**Como** negocio, **quiero** saber por qué en líneas reales el sistema
propuso el piso SICE-TAC cuando existía una tarifa de plaza vigente
mucho mayor, **para** no cobrar de menos.

**Decisión:** Sara pidió que Claude decidiera (2026-10-12). Se decide
**investigarlo**: es un error que afecta directamente el valor que se
cotiza (cobrar menos de lo que dice la tarifa vigente), y QA ya lo
había dejado abierto sin causa raíz.

**Evidencia:** `tests/QA/hu-13.4-verificacion-reglas-negocio-datos-reales/esperado-final-pliego.md:134-159`
— en 4 de 12 líneas reales verificadas (líneas 1, 2, 3 y 11 del pliego
de prueba) había plaza SAP vigente hasta 3,9 veces mayor que el piso y
se propuso el piso. Las consultas SQL que usó QA están en
`tests/QA/hu-13.4-verificacion-reglas-negocio-datos-reales/consultas/01-pliego-licitacion-esperado.sql:52-154`.

**Qué hacer:**
1. Agregar logs temporales en `clsTarifarioSapAdapter.ConsultarAsync`
   y `BuscarConZonasAsync` con: ciudad de origen/destino que entra,
   códigos DANE resueltos, zonas resueltas (`zonasOrigen`/`zonasDestino`),
   `claseContId` y `vehiculoId` resueltos, y cuántas filas quedan
   después de cada filtro.
2. Correr la propuesta para esas 4 rutas puntuales contra la base real
   y ver en qué filtro se pierde la fila que SQL sí encuentra.
3. Hipótesis a confirmar o descartar (ninguna confirmada todavía):
   - el filtro exacto de clase de contenedor (`t.ClaseContenedor == cc`,
     `clsTarifarioSapAdapter.cs:332`) descarta la fila porque la clase
     capturada resuelve a otro Id;
   - falta la relación zona granular → zona agregada que usa el segundo
     nivel de `BuscarConZonasAsync` (líneas 129-164).
4. Con la causa encontrada, abrir la HU de corrección.

**Criterio de aceptación:** causa raíz identificada y documentada para
las 4 líneas, con la HU de corrección creada. (Esta HU es solo de
investigación — no corrige nada por sí sola.)

---

## Otros módulos — decisiones del 2026-10-12

> Las HU de esta sección pertenecen a módulos que aparecen más arriba
> (Mesa de Costeo, Tarifas Plaza). Se escriben aquí, al final, para que
> los números de HU sigan en orden dentro del archivo; cada módulo tiene
> una nota que apunta hasta aquí.

### HU-B20 — Mesa de Costeo: exponer el "Valor mercancía" de la línea

**Módulo:** Mesa de Costeo.

**Como** frontend de Mesa de Costeo, **quiero** recibir el valor de la
mercancía de cada línea, **para** mostrarlo como un chip más junto a
peso, línea de negocio y clase de viaje (HU-18 de frontend).

**Contexto:** el dato **ya existe y ya se guarda** — se captura en la
solicitud (`CapturaRequests.cs:102`) y queda en
`clsLineaSolicitud.ValorMercancia` (`clsLineaSolicitud.cs:93`). Lo que
falta es enviarlo en el DTO de Mesa de Costeo: `clsLineaCosteoDto`
(`SolicitudDtos.cs:69-79`) no lo tiene.

**Cambio** (mismo patrón que ya se hizo con línea de negocio/clase de
viaje/clase de contenedor — ver "Ya resuelto (referencia)" en la
sección Mesa de Costeo):
- `clsLineaCosteoDto`: agregar al final `decimal? ValorMercancia = null`.
- `clsObtenerCosteoQuery.cs:50-54`: pasar `l.ValorMercancia` al
  construir el DTO.

**Criterios de aceptación:**
- `GET` del costeo de una solicitud trae `valorMercancia` en cada línea
  (o `null` si no se capturó).
- No rompe nada existente (campo opcional al final).

### HU-B21 — Tarifas Plaza (carga masiva): poder indicar el departamento cuando la ciudad se repite

**Módulo:** Tarifas Plaza / Carga masiva de Excel.

**Como** Administrador de Tarifas, **quiero** que al importar el Excel
de Tarifas Plaza se respete el departamento que confirmé para una
ciudad con nombre repetido (ej. "Armenia", que existe en Antioquia y
en Quindío), **para** que la tarifa no quede en el municipio
equivocado.

**Causa raíz:** el importador agrupa los municipios por nombre y se
queda con el primero (`clsEfTarifasPlazaRepository.cs:64-66`:
`GroupBy(Normalizar(m.Municipio))` → Colombia primero → `.First()`).
Entre 2 municipios colombianos con el mismo nombre, el que gana depende
del orden en la base — es al azar y sin aviso. El formulario manual no
tiene este problema (elige el municipio por Id desde un desplegable).

**Cambio:** aceptar 2 columnas **opcionales** en el Excel,
`DEPARTAMENTO ORIGEN` y `DEPARTAMENTO DESTINO`. Cuando vienen con
valor, el municipio se busca por **nombre + departamento** (ambos
normalizados); cuando vienen vacías o no existen, se comporta igual que
hoy (no rompe los Excel que ya existen). Si el par nombre +
departamento no existe en el catálogo, la fila se rechaza con un error
claro ("'Armenia' no existe en el departamento 'X'").

**De dónde salen esas columnas:** las llena la vista previa del
frontend (HU-9 de "Carga masiva de Excel" en `2026-10-09_HU-FRONTEND.md`) cuando
el usuario confirma el departamento de una ciudad repetida — el
frontend ya reconstruye el Excel antes de mandarlo al mismo endpoint
`importar-excel`, así que no cambia el contrato del endpoint.

**Criterios de aceptación:**
- Con `DEPARTAMENTO ORIGEN`/`DESTINO` informados, la tarifa queda en el
  municipio de ese departamento.
- Sin esas columnas, el comportamiento es idéntico al de hoy.
- Un departamento que no corresponde a esa ciudad da error en esa fila,
  no un municipio cualquiera.

### HU-B22 — Usar el traductor de zonas (`clsResolucionZonaSap`) al buscar la tarifa de plaza en SAP

**Módulo:** Motor de decisión de tarifa (búsqueda de la plaza externa).

**Como** motor de costeo, **quiero** reconocer las ciudades aunque SAP
las tenga con un código de zona que no trae el nombre escrito tal cual,
**para** no responder "Sin coincidencias" cuando la tarifa de plaza sí
existe.

**Decisión de Sara (2026-10-12):** dejarlo en las HU para conectarlo.

**El problema, con un ejemplo:** SAP no guarda las ciudades por nombre
sino por código de zona. Casi siempre el código trae el nombre adentro
(`ZC_MEDELLIN`), pero hay casos donde no: Medellín también aparece como
zona agregada `ZTC_VABURRA` (Valle de Aburrá), y hay abreviaturas como
`PTO` (Puerto) o `STFE` (Santa Fe). La búsqueda de la plaza compara "¿el
código contiene el nombre de la ciudad?" y en esos casos no lo
encuentra.

**Lo que ya existe y no se está usando:**
`src/Nexo.Infrastructure/Solicitudes/Adaptadores/clsResolucionZonaSap.cs`
— resuelve ese problema y está probado contra los datos reales (de 577
códigos de zona, la regla con abreviaturas resuelve 564 y los 13
restantes están curados a mano en `AliasZonasAgregadas`). Expone
`Resuelve(...)`, `ExpandirAbreviaturas(...)`, `SinPrefijoZona(...)` y
`AliasZonasAgregadas`. **Hoy solo lo usa el catálogo de ciudades para
el desplegable de Captura** (`clsCiudadesDwAdapter.cs:42,49,65-66`).

**Dónde se compara "a mano":**
`clsTarifarioSapAdapter.BuscarConZonasAsync` (`clsTarifarioSapAdapter.cs:114-164`):
- paso directo (líneas 121-124): `EF.Functions.Like(t.Origen, $"%{origen}%")`;
- paso por zonas (líneas 129-131): `zonas.Where(z => z.Transporte.Contains(origen))`.

Ninguno de los 2 expande abreviaturas ni consulta los 13 alias. Lo
mismo pasa en el selector tolerante de la plaza y del histórico
(`clsPropuestaTarifaAdapter.CandidatoSapAsync`, líneas 499-501), que usa
la misma comparación por "contiene".

**Cambio:**
1. En el paso por zonas, comparar cada código de zona pasado por
   `SinPrefijoZona` + `ExpandirAbreviaturas` (las 2 direcciones de
   "contiene", igual que hace `Resuelve`), en vez del `Contains` directo.
2. Antes de rendirse, buscar la ciudad en `AliasZonasAgregadas` (ej.
   Medellín → `VABURRA`) y agregar esos códigos a las zonas de origen o
   destino.
3. Aplicar la misma resolución en `CandidatoSapAsync` para que la plaza
   tolerante y el histórico de SAP también la aprovechen.
4. Opcional, de orden: mover `clsResolucionZonaSap` a una carpeta común
   (hoy vive en `Solicitudes/Adaptadores`) ya que pasa a usarse en
   Biblioteca también.

**Relación con otras HU:** puede ser parte de la causa de la
"Desviación 1" de QA (HU-B19) — conviene hacer HU-B19 primero o en
paralelo, para medir cuánto mejora.

**Criterios de aceptación:**
- Una ruta cuya ciudad está en SAP solo como zona agregada (ej.
  Medellín como `ZTC_VABURRA`) encuentra su tarifa de plaza.
- Una ruta con abreviatura en el código de zona (ej. `PTO`) encuentra su
  tarifa.
- Las rutas que hoy ya encuentran tarifa siguen encontrando la misma
  (el cambio solo agrega coincidencias, no quita).


---

## Servicios conexos de la cotización — decisiones del 2026-10-12

> Pedido de Sara: poder QUITAR conexos ya agregados, y corregir una
> inconsistencia: hoy el Administrador de Tarifas no puede agregar conexos
> (ni con la cotización pendiente) y nadie puede agregarlos una vez
> Entregada. Regla acordada: se pueden agregar y quitar **antes y después de
> entregar**; solo se bloquea cuando la cotización está **Ganada o Perdida**.

### HU-B23 — Quitar un servicio conexo ya agregado

**Como** usuario que gestiona servicios, **quiero** quitar un conexo agregado
por error o que ya no aplica, **para** no tener que dejar el total mal.

**Hoy:** no existe ningún endpoint para quitar. Solo hay
`POST /api/cotizaciones/{id}/conexos` (`clsAgregarConexosCotizacionCommand.cs`).

**Cómo debe quedar:**
- Endpoint nuevo `DELETE /api/cotizaciones/{id}/conexos/{conexoId}` en
  `clsCotizacionesController.cs`, que devuelve la cotización completa
  (`clsCotizacionDto`) ya recalculada, igual que el de agregar.
- Roles: los mismos que agregan (ver HU-B24): `Comercial,AdminTarifas,CoordinadorFletes,Admin`.
- Comando nuevo `clsQuitarConexoCotizacionCommand` (patrón de
  `clsAgregarConexosCotizacionCommand`):
  1. Cargar la cotización con `ObtenerConLineasYConexosAsync`; 404
     `COTIZACION_NO_ENCONTRADA` si no existe.
  2. Aplicar la misma regla de estado de HU-B24 (409 `COTIZACION_CERRADA`
     si está Ganada o Perdida).
  3. Buscar el conexo por `conexoId` dentro de `c.Conexos`; 404
     `CONEXO_NO_ENCONTRADO` si no pertenece a esa cotización.
  4. Quitarlo (`c.Conexos.Remove(...)` y que EF lo borre), llamar
     `clsCotizacionCalculos.Recalcular(c)` y guardar.
  5. Auditoría `quitar_conexo` con el servicio, cantidad y valor que tenía
     (valor anterior), no solo el conteo.
  6. Si la cotización ya estaba Entregada, aplicar el efecto de HU-B24
     (marca + aviso condicional).
- Registrar el handler en `clsDependencyInjection.cs` de Cotizaciones.

**Criterios de aceptación:**
- [ ] Quitar un conexo existente devuelve 200 con la cotización sin ese
      conexo y el `Total` recalculado.
- [ ] Un `conexoId` de otra cotización o inexistente devuelve 404.
- [ ] Con la cotización Ganada o Perdida devuelve 409 y no cambia nada.
- [ ] La auditoría guarda qué servicio, cantidad y valor se quitó.
- [ ] Con rol sin permiso devuelve 403.

---

### HU-B24 — Reglas de edición de conexos: AdminTarifas puede, Entregada se permite, solo Ganada/Perdida bloquea

**Como** Administrador de Tarifas o Comercial, **quiero** agregar y quitar
servicios conexos con la cotización pendiente o ya entregada, **para** no
quedar atado a un estado que no cambia el trabajo real.

**Causa raíz (3 puntos):**
1. **Permiso:** `clsCotizacionesController.cs:90` autoriza el `POST .../conexos`
   solo a `Comercial,CoordinadorFletes,Admin`. **Falta `AdminTarifas`**
   (en el resto del controlador sí aparece, líneas 39, 60, 68, 78, 104).
2. **Estado:** `clsAgregarConexosCotizacionCommand.cs` lanza 409
   `COTIZACION_NO_EDITABLE` si `Estado != Borrador`, es decir, bloquea toda
   cotización Entregada.
3. (El frontend repite ambos bloqueos; ver HU-22 y HU-23 en `2026-10-09_HU-FRONTEND.md`.)

**Regla nueva (única, en backend):** agregar o quitar conexos se permite
mientras `Resultado == SinDefinir` (cotización Borrador o Entregada). Si
`Resultado` es `Ganada` o `Perdida`, 409 `COTIZACION_CERRADA` con mensaje
"La cotización ya está marcada como Ganada/Perdida: no admite cambios de
servicios."

**Cambios:**
- Roles del `POST .../conexos` (y del `DELETE` de HU-B23):
  `Comercial,AdminTarifas,CoordinadorFletes,Admin`. Recordar que el
  `[Authorize(Roles)]` de método se combina con AND con el de clase.
- En `clsAgregarConexosCotizacionCommand`, reemplazar la condición
  `Estado != Borrador` por `Resultado != SinDefinir` (mismo para quitar).
- **Marca "modificada después de entrega":** cuando se agrega o quita un
  conexo con `Estado == Entregada`, guardar la fecha en una columna nueva
  `ModificadaTrasEntregaAt` (`DateTimeOffset?`) de `clsCotizacion` (+ cambio en
  `ESQUEMA_NEXO.sql` y migración) y exponerla en `clsCotizacionDto`. Es
  informativa: no cambia `Estado` ni `EntregadaAt` (el SLA y las métricas
  siguen midiendo la entrega original).
- **Aviso al comercial, solo si el que cambia NO es el mismo comercial:**
  el destinatario es `clsSolicitud.Creador` (`CreadoPor`), igual que en la
  entrega (`clsNotificarEntregaCommand.cs`). Si `usuarioActual.Id ==
  solicitud.CreadoPor` no se envía nada (nadie se avisa a sí mismo). Si el
  cambio lo hace AdminTarifas, CoordinadorFletes o Admin sobre una cotización
  Entregada, se publica un evento (mismo patrón que la entrega: la
  petición responde y el correo sale después en un consumidor) con asunto y
  cuerpo del tipo "Cotización actualizada: se agregó/quitó [servicio] ×
  [cantidad]; nuevo total $X". Un cambio sobre una cotización en
  Borrador (aún no entregada) no avisa a nadie.
- Auditoría `agregar_conexos` / `quitar_conexo` registra quién, qué y, si
  aplica, que fue después de la entrega.

**Por decidir (no bloquea):** si varios cambios seguidos deben agruparse en un
solo correo (hoy sería uno por petición; "agregar a todas las líneas" ya es
una sola petición).

**Criterios de aceptación:**
- [ ] AdminTarifas puede agregar y quitar conexos (200), con la cotización
      Borrador y con la Entregada.
- [ ] Comercial, CoordinadorFletes y Admin también pueden, en ambos estados.
- [ ] Con la cotización Ganada o Perdida devuelve 409 `COTIZACION_CERRADA`
      al agregar y al quitar.
- [ ] Agregar/quitar sobre una cotización Entregada llena
      `ModificadaTrasEntregaAt`, pero deja `Estado` y `EntregadaAt` iguales.
- [ ] Si lo hace un usuario distinto al creador de la solicitud, el creador
      recibe el correo de "cotización actualizada"; si lo hace el mismo
      creador, no se envía nada; sobre una cotización Borrador no se envía
      nada.
- [ ] `Total` siempre recalculado y la auditoría completa.


---

## Bandeja — resultado de la cotización (decisión del 2026-10-12)

### HU-B25 — Mostrar el resultado (Sin definir / Ganada / Perdida) y filtrar "Esperando resultado" en la Bandeja

> Pedido de Sara (2026-10-12): al marcar una cotización como Ganada o Perdida,
> la solicitud pasa a estado `Entregada` y sale de la vista por defecto
> ("Pendientes"), y la Bandeja no muestra el resultado en ningún lado. Además,
> una cotización entregada que nadie ha cerrado queda en el limbo. Decidido
> (opción A): un **filtro nuevo "Esperando resultado"** más una **columna
> "Resultado"**; NO se mezclan estas solicitudes en "Pendientes" (esa vista es
> la cola de trabajo del Administrador de Tarifas).

**Como** comercial (y como Administrador de Tarifas), **quiero** ver el
resultado de cada solicitud y poder listar las que ya se entregaron pero aún
no se han marcado Ganada o Perdida, **para** no perder de vista cuáles falta
cerrar.

**Hoy:**
- `clsBandejaItemDto` (`SolicitudDtos.cs:24`) no trae el resultado de la
  cotización.
- `BandejaFilasAsync` (`clsEfSolicitudesCatalogoRepository.cs`) no lo proyecta
  y `clsBandejaFiltro` (`SolicitudDtos.cs:30`) no tiene filtro de resultado.
- `clsMarcarResultadoCommand` deja la solicitud en `Entregada` tanto para
  Ganada como para Perdida (la pérdida con motivo vive en
  `CondicionesServicio`), así que `Estado` no las distingue.

**Visibilidad (decidido por Sara, 2026-10-13):** el resultado lo marca y lo ve el
Comercial (y Admin). **AdminTarifas no ve cuáles están ganadas o perdidas**: para
ese rol la Bandeja devuelve `Resultado = null` y el parámetro `resultado` se
ignora o se rechaza con 403. Nexo no usa el resultado para decisiones de negocio
(eso corresponde a Cotifly).

**Cómo debe quedar:**
- **DTO:** `clsBandejaItemDto` agrega `Resultado` (`"SinDefinir"|"Ganada"|"Perdida"`,
  o `null` si la solicitud aún no tiene cotización). Regla cuando hay varias
  cotizaciones: la que tenga resultado distinto de `SinDefinir`; si ninguna, la
  más reciente; si no hay cotización, `null`.
- **Proyección:** `clsBandejaFilaRaw` y la consulta de `BandejaFilasAsync`
  incluyen ese resultado (sin traer cotizaciones enteras: una subconsulta).
- **Filtro nuevo `resultado`** en `GET /api/solicitudes` (parámetro opcional,
  junto a `estado`, `atencion` y `vigencia`; documentarlo en el comentario del
  controlador y agregarlo a `clsBandejaFiltro`):
  - `esperando`: solicitudes con cotización `Entregada`, o `Borrador` con
    `EsAutoCosteo`, y `Resultado == SinDefinir`, cuya solicitud no esté
    `Descartada`.
  - `ganada` / `perdida`: por el resultado de la cotización.
  - vacío / `todos`: sin filtro.
- **Interacción con "Pendientes":** la restricción por defecto
  `Estado ∈ {Pendiente, EnCosteo}` (rama `atencion == "pendientes"`) NO debe
  aplicarse cuando llega `resultado`. Para evitar sorpresas, cuando hay
  `resultado` se trata la atención vacía como `todas`.
- **Aislamiento por comercial:** sin cambios; `creadoPorFiltro` se aplica antes,
  así que el Comercial solo ve lo suyo con el filtro nuevo.
- Sin cambios al score, a la vigencia ni a las métricas del Tablero.

**Criterios de aceptación:**
- [ ] Cada fila de la Bandeja trae `Resultado` (`null` si no hay cotización).
- [ ] `resultado=esperando` lista solo cotizaciones Entregadas (o auto-costeadas)
      sin resultado y excluye las Ganadas, Perdidas y las solicitudes
      descartadas, aunque la solicitud esté en estado `Entregada`.
- [ ] `resultado=ganada` y `resultado=perdida` listan solo esas.
- [ ] Con `resultado` informado y sin `atencion`, el filtro por defecto de
      "Pendientes" no oculta las solicitudes `Entregada`.
- [ ] Con rol Comercial, el filtro solo devuelve solicitudes creadas por él.
- [ ] Con rol AdminTarifas, `Resultado` llega siempre `null` y el filtro `resultado` no devuelve información de resultados.
- [ ] Una solicitud rechazada sin cotización (descartada) no aparece como
      "esperando resultado".

---

## Contrato de captura con Cotifly — decisiones del 2026-10-13

### HU-B26 — Contrato de captura con Cotifly: ruta, orden, recomendación del cubicador, líneas sin vehículo y normalización

> Detalle completo en la issue S5-HU-20.5 (`docs/backlog/2026-10-12_ISSUES-NEXO-S5.md`).

Cotifly manda a `POST /api/captura-solicitudes` una línea por camión. NEXO necesita:
1. **`grupoRutaId` (GUID, opcional) y `orden` (int, opcional)** por línea: guardados en `tbl_lineas_solicitud`, devueltos por `GET /api/solicitudes/{id}`, que ordena las líneas por `orden` (hoy no hay orden estable: el número de línea no se persiste y el GET no ordena).
2. **`idRecomendacionVehiculo` (GUID, opcional)** por línea, guardado y devuelto.
3. **Línea de Cupo sin vehículo no se autocostea:** queda pendiente para consulta completa con el motivo visible (hoy el piso SICE-TAC sin filtro de vehículo tiende a quedar bajo). No aplica a contenedor.
4. **Normalizar vehículos y rutas** (`clsResolucionDeCatalogos.Normalizar`): quitar tildes y unificar separadores, para que `FURGON_REFRIGERADO` y «Furgón Refrigerado», o «Bogotá» y «Bogota», no creen duplicados.

Decisiones de Sara: el código de mercancía no se manda en la captura; el endpoint de cubicación de NEXO no se retira; «sin vehículo» aplica solo a Cupo; Cotifly puede dejar de mandar `posiciones[]`; los códigos del cubicador llegan en los campos `TipoVehiculo` y `Carroceria` actuales.
