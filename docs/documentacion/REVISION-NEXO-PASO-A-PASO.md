# Revisión paso a paso de NEXO — hallazgos

Revisión guiada por Sara, módulo por módulo, verificando contra el código
real (no solo lo que se ve en pantalla). Cada módulo es un ítem, y cada
hallazgo dentro de ese módulo queda anotado como subítem para arreglarlo
después — no se toca nada en el momento del hallazgo.

## Bandeja

### 1. BACKEND — el filtro "vigente" de la bandeja no mira la Fecha Límite

**Archivo:** `src/Nexo.Application/CasosDeUso/Solicitudes/clsObtenerBandejaQuery.cs`,
líneas 50-53 y 67.

```csharp
var limiteVigencia = ahora.AddDays(-7);
var vigenciaFiltro = filtro.Vigencia?.Trim().ToLowerInvariant();
if (vigenciaFiltro == "vigente") filas = filas.Where(f => f.CreatedAt >= limiteVigencia).ToList();
else if (vigenciaFiltro == "vencida") filas = filas.Where(f => f.CreatedAt < limiteVigencia).ToList();
...
var vigencia = f.CreatedAt >= limiteVigencia ? "vigente" : "vencida";
```

Hoy "vigente" significa **"se capturó hace menos de 7 días"** — nunca mira
`Deadline` (la Fecha Límite, `DateTimeOffset?` en `clsBandejaItemDto`). Por
eso el filtro muestra solicitudes con Fecha Límite ya vencida como
"vigentes" (si se capturaron hace poco), y puede marcar como "vencidas"
solicitudes capturadas hace tiempo cuya Fecha Límite real todavía no pasa.
No es un problema de timezone ni de operador invertido — el código compara
bien, pero compara el campo equivocado (`CreatedAt` en vez de `Deadline`).

Confirmado en dos sesiones de Claude independientes (esta y
`administracion-embarqa-nexo-7d`) y verificado directamente en el código.

**Arreglo:** cambiar las 3 referencias de `f.CreatedAt`/`limiteVigencia` a
comparar `f.Deadline` contra `ahora`. Regla de negocio ya confirmada con
Sara: una solicitud **sin** `Deadline` cuenta como **vigente** por defecto.

Criterios de aceptación:
- [ ] El filtro "vigente" solo muestra solicitudes cuyo `Deadline` no ha
      pasado (o no tiene `Deadline`).
- [ ] El filtro "vencida" solo muestra solicitudes cuyo `Deadline` ya pasó.
- [ ] Una solicitud sin `Deadline` aparece como "vigente".
- [ ] El badge de vigencia en `bandeja-page.component.ts` sigue pintando
      igual (no requiere cambio, solo consume el campo ya corregido).

### 2. BACKEND — no hay forma de ver "revisada pero todavía no entregada" en la bandeja

**Contexto (bandeja, filtro "Atención"):** `clsEfSolicitudesCatalogoRepository.cs:56-69`
calcula la atención (Pendientes / Atendidas / Automático / Administrador de
Fletes / Todas) mirando si cada línea ya tiene flete y quién la resolvió —
**sin mirar nunca `Estado`** de la solicitud, salvo en "Pendientes".

**El hueco:** una solicitud puede tener **todas** sus líneas resueltas (por
el motor automático o revisadas a mano por el Administrador de Fletes) y
seguir **sin `Entregada`** indefinidamente si nadie le da clic al botón
"Entregar" — nada lo marca ni lo alerta. Para auto-costeo esto se mitiga
parcialmente (S4-HU-15.6/#252: `clsMarcarResultadoCommand.cs:52-68` salta el
paso de Entregar y pasa a `Entregada` en cuanto Comercial marca
Ganada/Perdida), pero para cotizaciones manuales (Administrador de Fletes)
**siempre** se exige el clic explícito a "Entregar"
(`clsEntregarCotizacionCommand.cs`, único disparador, invocado solo desde
`POST .../entregar`) — si se olvida, la solicitud queda "atendida" pero
nunca entregada, sin que nada lo señale.

**El dato ya existe:** el backend ya devuelve, por fila, tanto la etiqueta
de atención como el `Estado` real (`clsBandejaItemDto`) — el frontend hoy
simplemente no cruza ambos campos para resaltar la combinación rara.

**Confirmado en pantalla real (2026-10-09):** filtrando Atención =
"Atendidas por el Administrador de Fletes", aparecen mezcladas filas en
`Estado = En Costeo` (clientes A y B) junto con otras en
`Estado = Entregada` (clientes C y D) — confirma que el
filtro no distingue "ya la terminé de revisar" de "ya se entregó".

**Decidido (2026-10-09):** no se agrega una opción de atención nueva —
las solicitudes "Atendidas por el Administrador de Fletes" que **todavía
no estén `Entregada`** deben contarse directamente como **"Pendientes"**
(no como una categoría aparte), porque en la práctica significa que
nadie se las ha mandado al comercial. **No aplica a "Automático"**
(auto-costeo): esas no necesitan revisión del Administrador de Fletes —
el flujo de auto-costeo ya tiene su propio atajo (S4-HU-15.6/#252) para
llegar a `Entregada` sin depender de que alguien se acuerde de un clic
aparte, así que no corren el mismo riesgo de quedarse "perdidas".

**Arreglo (backend):** en el `switch` de
`clsEfSolicitudesCatalogoRepository.cs:57-69`, la rama `"pendientes"` hoy
es:
```csharp
"pendientes" => q.Where(s => s.Lineas.Any(l => !db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id))),
```
Hay que combinarla con (OR) la misma condición que ya usa la rama
`"administrador"` (todas las líneas con flete, al menos una `Revisada`) —
el filtro externo de `Estado == Pendiente || Estado == EnCosteo` (línea
50-51) ya se encarga de excluir las `Entregada`/`Descartada`, así que no
hace falta agregar esa condición de nuevo ahí.

Criterios de aceptación:
- [ ] Una solicitud con todas sus líneas revisadas por el Administrador
      de Fletes, pero sin `Entregada` todavía, aparece en el filtro
      "Pendientes".
- [ ] Esa misma solicitud sigue apareciendo también en "Atendidas por el
      Administrador de Fletes" (no se excluye de ahí, solo se **agrega**
      a Pendientes también).
- [ ] Una solicitud auto-costeada (Atención = Automático) sin
      `Entregada` todavía **no** cambia de comportamiento — sigue sin
      aparecer en "Pendientes" por esa sola razón.
- [ ] Una vez la solicitud pasa a `Entregada`, deja de aparecer en
      "Pendientes" (vuelve al comportamiento de hoy).

## Biblioteca

### Mejora — Reemplazar texto descriptivo fijo por ícono de información, igual que Bandeja (validado en sandbox, pendiente para el front)

**Pedido:** el encabezado de Biblioteca tenía un `<h1>Biblioteca</h1>`
seguido de un `<p>` con texto descriptivo fijo ("Matriz oficial
SICE-TAC, tarifas de plaza e histórico SAP TM..."). Se pidió imitar el
patrón que ya usa Bandeja: un ícono de información junto al título,
que muestra ese mismo texto en un tooltip al pasar el mouse, en vez de
ocupar espacio fijo en pantalla.

**Investigado:** confirmado que este es el **único** lugar en todo
`biblioteca-page.component.html` con un `<p>` descriptivo fijo bajo un
título — los demás `<h2>` del archivo son títulos de modales, no
descripciones de vista, así que no hay ningún otro caso que convertir.

**Prototipo validado en el sandbox:** se copió el patrón exacto de
`bandeja-page.component.html:4-16` (botón `.cabina-info-btn` +
`mat-icon info_outline` + `matTooltip`/`matTooltipClass`/
`matTooltipPosition`), reemplazando el `<p>` fijo. Como los estilos de
componente en Angular no se comparten entre páginas, hubo que copiar
también el bloque CSS `.cabina-info-btn` completo a
`biblioteca-page.component.css`, y agregar `display:flex;
align-items:center; gap:8px` a `.cabina-page-header h1` (que antes no
lo tenía) para que el ícono quedara alineado junto al título. Compiló
bien.

Esto queda como **HU-6** en `2026-10-09_HU-FRONTEND.md` — pendiente de que el
equipo de frontend la implemente de verdad en el repo real.

### Bug — Semáforo de vigencia siempre mostraba "Vigente" (✅ Resuelto)

**Reportado:** Sara vio una tarifa con vigencia 8/10/2026 (vencida un día
antes del "hoy" 9/10/2026) mostrando el badge en verde "Vigente" en vez
de "Vencida".

**Causa raíz:** `semaforo()` (copiada tal cual de Tarifas Plaza a
`biblioteca-page.component.ts`) concatena siempre `T00:00:00` a la
fecha recibida, asumiendo que llega como fecha pura (`"2026-10-08"`).
Pero el DTO real detrás de la pestaña "Tarifas de plaza" de Biblioteca
(`clsSapTarifaHistoricoItemDto`, `SapHistoricoDtos.cs:17,37`) tiene
`Vigencia`/`VigenciaAjustada` como `DateTime`/`DateTime?` **completo**,
que .NET serializa ya con hora (`"2026-10-08T00:00:00"`). Al
concatenarle otra `T` queda un string inválido
(`"2026-10-08T00:00:00T00:00:00"`), `new Date(...)` no lo puede
parsear, y la función cae siempre en su propio fallback silencioso
(`if (Number.isNaN(fecha.getTime())) return 'vigente';`) — por eso el
badge mostraba "Vigente" sin importar la fecha real.

**Por qué Tarifas Plaza (el módulo original, de donde se copió la
función) nunca mostró este bug:** su DTO (`clsTarifaPlazaDto`,
`TarifaPlazaDtos.cs:12`) usa `DateOnly`, que serializa como fecha pura
(`"2026-10-08"`, sin hora). Ahí el `T00:00:00` que concatena
`semaforo()` es intencional y correcto — fuerza que `new Date(...)`
parsee en hora **local** y no UTC (si no, en Colombia UTC-5 la fecha se
corre un día para atrás). Ese módulo quedó intacto, sin cambios.

**Arreglo aplicado** (solo en `biblioteca-page.component.ts`):

```ts
semaforo(vigencia: string): SemaforoVigencia {
  const fecha = new Date(vigencia.includes('T') ? vigencia : `${vigencia}T00:00:00`);
  ...
```

Detecta si el string ya trae una `T` (datetime completo) y, si es así,
no concatena nada más — maneja ambos formatos sin romper el ajuste de
timezone que ya tenía el original.

**Verificado:** Sara confirmó en pantalla, con la misma tarifa del
8/10/2026, que ahora se muestra correctamente como "Vencida".

### Mejora — Ordenar "Tarifas de plaza" por vigencia, igual que Tarifas Plaza (✅ Resuelto)

**Pedido:** que la pestaña "Tarifas de plaza" de Biblioteca se ordene
igual que el módulo Tarifas Plaza — primero las que están por vencer o
ya vencidas, luego las vigentes.

**Referencia (patrón ya existente en Tarifas Plaza):**
`tarifas-plaza-page.component.ts:22,111-115`:
```ts
const ORDEN_SEVERIDAD: Record<SemaforoVigencia, number> = { vencida: 0, por_vencer: 1, vigente: 2 };
...
tarifasVisibles(): TarifaPlazaDto[] {
  return this.tarifas
    .filter((t) => !this.filtroVigencia || this.semaforo(t.vigencia) === this.filtroVigencia)
    .sort((a, b) => ORDEN_SEVERIDAD[this.semaforo(a.vigencia)] - ORDEN_SEVERIDAD[this.semaforo(b.vigencia)]);
}
```

**Diferencia importante:** Tarifas Plaza carga toda la lista de una vez
y ordena en el navegador. Biblioteca pagina **del lado del servidor**
(`cargarPlaza()`, `biblioteca-page.component.ts:386-403`, llama
`buscarSapPlaza()` con `page`/`top`) — un sort en frontend solo alcanza
a ordenar los ítems de la página ya cargada, no la lista completa a
través de páginas. Se le preguntó a Sara y **eligió explícitamente la
versión solo-frontend** (ordenar cada página ya traída), sin tocar
backend NEXO.

**Arreglo aplicado** (solo en `biblioteca-page.component.ts`):
```ts
const ORDEN_SEVERIDAD: Record<SemaforoVigencia, number> = { vencida: 0, por_vencer: 1, vigente: 2 };

plazaItemsOrdenados(): SapTarifaHistoricoItemDto[] {
  return (this.plazaResultado?.items ?? []).slice().sort((a, b) =>
    ORDEN_SEVERIDAD[this.semaforo(a.vigenciaAjustada || a.vigencia)] - ORDEN_SEVERIDAD[this.semaforo(b.vigenciaAjustada || b.vigencia)]);
}
```
`ORDEN_SEVERIDAD` se copió localmente a este archivo (mismo criterio ya
usado con `DIAS_POR_VENCER`/`semaforo()` — copia local, no refactor
compartido). El `*ngFor` de la tabla (`biblioteca-page.component.html:169`)
ahora itera `plazaItemsOrdenados()` en vez de `plazaResultado?.items`
directo.

**Verificado:** compiló bien, pendiente de que Sara lo confirme en
pantalla (no bloqueante — mismo patrón ya probado en Tarifas Plaza).

### Mejora — Separar columna "Vigencia" de la fecha, en "Tarifas de plaza" (✅ Resuelto)

**Reportado:** en la tabla, el badge de estado (Vencida/Por vencer/
Vigente) y la fecha estaban pegados como texto dentro de la misma
celda (`biblioteca-page.component.html:171-176`). Como el badge cambia
de ancho según la etiqueta, la fecha arrancaba en una posición
horizontal distinta en cada fila — se veía torcido.

**Arreglo aplicado:** se separó en dos `<td>` reales — una columna solo
para el badge de estado, la siguiente para la fecha + el chip
"Ajustada" + los botones "Ajustar"/"Quitar" (header: nueva `<th></th>`
sin título, entre "Vigencia" y "Origen"). Al ser columnas de tabla
reales, se alinean solas entre filas sin importar el ancho del badge —
no fue necesario ningún CSS a medida.

El "sin resultados" (`biblioteca-vacio`) es un `<p>` fuera de la tabla,
no un `<td colspan>` — no hubo que ajustar nada ahí.

**No aplica a Tarifas Plaza** (el módulo original) — mismo criterio que
la mejora anterior: solo se pidió en Biblioteca, aunque Tarifas Plaza
tiene la misma estructura inline y podría verse igual de torcido.

**Verificado:** compiló bien, pendiente de que Sara lo confirme en
pantalla.

### Mejora — Paginación real en "Histórico SAP" (validado en sandbox, pendiente para el front)

**Reportado:** la pestaña "Histórico SAP" no tenía botones "Anterior"/
"Siguiente" ni selector de tamaño de página, a diferencia de "Tarifas
de plaza". Confirmado: `buscarSap()` (`biblioteca-page.component.ts`)
nunca mandaba `top`/`page` en `FiltrosSapHistorico`, aunque el backend
ya soporta esos dos parámetros (mismo contrato que usa `buscarSapPlaza`,
`biblioteca-api.models.ts:94-102`) — es el mismo mecanismo que ya
funciona en "plaza", solo nunca se conectó en esta pestaña.

**Prototipo validado en el sandbox:** se replicó el patrón de "plaza"
campo por campo — `sapPagina`/`sapTamanoPagina`, `totalPaginasSap`,
`cambiarTamanoPaginaSap`, `irAPaginaSap`, `buscarSap()` reseteando a
página 1 y delegando a un `cargarSap()` privado que sí manda `top`/
`page`, `reintentarSap()` llamando `cargarSap()` directo (no resetea
página). HTML: mismo bloque de paginación de "plaza", renombrado a
`sap*`, antes de la tabla. Compiló bien.

Esto queda como **HU-4** en `2026-10-09_HU-FRONTEND.md` — pendiente de que el
equipo de frontend la implemente de verdad en el repo real.

### Mejora — Quitar "mostrando las primeras X" en plaza y sap (validado en sandbox, pendiente para el front)

**Encontrado de paso:** con paginación real, el texto `(mostrando las
primeras {{ items.length }})` queda engañoso — dice "primeras X" aunque
se esté parado en cualquier página distinta a la primera. Existía
desde antes en "plaza" (se había copiado tal cual a "sap" al agregarle
paginación, sin que nadie lo notara hasta ahora).

**Prototipo validado en el sandbox:** se quitó el `<ng-container
*ngIf="...total > ...items.length">(mostrando las primeras X)</ng-container>`
completo en las dos pestañas, dejando solo `{{ total }} tarifas · Fuente: ...`.
Compiló bien.

**Dato adicional del peer de frontend:** la pestaña **SICE-TAC** tiene
exactamente el mismo texto y el mismo caso (paginación real ya
funcionando desde antes). Sara confirmó extenderlo ahí también por
consistencia — las 3 pestañas quedan iguales. Se validó también en el
sandbox: `Periodo <strong>{{ formatPeriodo(sicetacResultado.periodo) }}</strong> · {{ sicetacResultado.total }} tarifas encontradas · Fuente: DWSice · tblRutasSicetac (solo lectura)`
— mismo criterio, solo se quitó el `<ng-container>`. Compiló bien.

Esto queda como **HU-5** en `2026-10-09_HU-FRONTEND.md` (ahora cubriendo las 3
pestañas) — pendiente de que el equipo de frontend la implemente de
verdad en el repo real.

### Mejora — Unidad editable en servicio "Almacenamiento" (validado en sandbox, pendiente para el front)

**Reportado:** al crear un nuevo servicio "Almacenamiento" (Biblioteca
→ Servicios), el campo Unidad queda fijo en "Por estiba" sin poder
elegir "Por unidad".

**Causa raíz:** `UNIDAD_FIJA.almacenamiento = 'Por estiba'`
(`biblioteca-page.component.ts:60`) fija el valor, y el input queda
`disabled` para cualquier tipo que no sea `'valor_unico'`
(`biblioteca-page.component.html:438`).

**Prototipo validado en el sandbox:** se cambió el input deshabilitado
por un `<select>` con "Por estiba"/"Por unidad", solo para
`tipoMiniVista === 'almacenamiento'` — el resto de tipos sigue
bloqueado igual que antes. Compiló bien.

Esto queda como **HU-7** en `2026-10-09_HU-FRONTEND.md` — pendiente de que el
equipo de frontend la implemente de verdad en el repo real.

### Bug confirmado — CoordinadorFletes no puede buscar ciudades al crear un servicio "Almacenamiento"

**Reportado:** mismo formulario que la mejora anterior — el campo
Origen no mostraba ninguna sugerencia de ciudad al escribir.

**Investigado:** el mecanismo del frontend (`biblioteca-page.component.ts:239-244`,
`services/servicios-conexos-api.service.ts:66-70`) es idéntico al que
ya funciona en Captura — mismo endpoint
(`GET /api/captura-solicitudes/ciudades`), mismo patrón de debounce. No
se encontró el bug ahí.

**Causa raíz real, confirmada contra el código de NEXO:**
`clsCapturaSolicitudesController.cs:20` tiene
`[Authorize(Roles = "Comercial,Admin")]` a nivel de **todo el
controller** (comentario del propio archivo: "Solo Comercial/Admin — es
la puerta de entrada de nuevas solicitudes", pensado para crear/
analizar solicitudes). Pero Biblioteca reutiliza 3 endpoints de solo
lectura de ese mismo controller (`/ciudades`, `/departamentos`,
`/municipios`) para su propio formulario de servicios — y el rol
`CoordinadorFletes` (Administrador de Fletes) sí tiene el permiso
`gestionarServicios` que habilita esa pantalla en el frontend
(`nexo-access.models.ts:58-68`), pero **no** está en la lista de roles
del controller. Cada búsqueda le devuelve 403, que el frontend traga en
silencio (`catchError(() => of([]))`) — el campo se queda sin
sugerencias, sin ningún error visible.

**Confirmado con Sara:** estaba probando con el rol `CoordinadorFletes`
exactamente — coincide con la causa raíz.

**Corrección sobre el arreglo propuesto inicialmente:** Sara señaló que
`/ciudades` en sí mismo no debería tener restricción de rol (es
catálogo de solo lectura, sin dato sensible) — y tenía razón por una
razón técnica además de la de negocio: en ASP.NET Core, un
`[Authorize(Roles=...)]` a nivel de método **no reemplaza** el de la
clase, se combinan con AND. Agregar `CoordinadorFletes` solo al método
(dejando `[Authorize(Roles="Comercial,Admin")]` en la clase) no habría
arreglado nada — el usuario tendría que cumplir las dos listas a la
vez. El arreglo real: sacar la restricción de rol del nivel de clase y
dejarla solo en los métodos que sí la necesitan (`Capturar`,
`AnalizarExcel`) — mismo patrón que ya usa
`clsSolicitudesController.cs:15-16` en este mismo proyecto (clase con
`[Authorize]` simple, exige solo estar autenticado con alguno de los 6
roles válidos de NEXO vía la política de respaldo de
`clsProgram.cs:144-149`).

Esto queda como **HU-B3** en `2026-10-09_HU-BACKEND.md` (ya corregida con el
diseño real) — pendiente de implementar.

### Mejora — Código de "Almacenamiento" en mayúscula inicial (validado en sandbox, pendiente para el front)

**Reportado:** al crear un servicio "Almacenamiento" nuevo, la columna
"Código" sale todo en mayúsculas ("ALMACENAMIENTO"), mientras un
registro existente anterior lo tenía en mayúscula inicial
("Almacenamiento") — inconsistente en la misma tabla.

**Causa raíz:** `CODIGO_SERVICIO_FIJO.almacenamiento = 'ALMACENAMIENTO'`
(`biblioteca-page.component.ts:74-77`) no coincide con
`TIPO_SERVICIO_FIJO.almacenamiento = 'Almacenamiento'` (línea 71, lo
que se ve en la columna "Servicio"). Es puro frontend, 100% Angular —
no toca NEXO backend para nada.

**Prototipo validado en el sandbox:** se cambió el valor a
`almacenamiento: 'Almacenamiento'` (mayúscula inicial, igual al Tipo de
servicio) — `itr: 'ITR'` se dejó igual, ahí no había inconsistencia.
Compiló bien.

Esto queda como **HU-8** en `2026-10-09_HU-FRONTEND.md` — pendiente de que el
equipo de frontend la implemente de verdad en el repo real.

### Futuro alcance — decidir si usar solo las tarifas de plaza propias de NEXO, o seguir usando también las de DbSolucionesTI

NEXO **ya tiene su propia tabla** de tarifas de plaza (el módulo Tarifas
Plaza, cargada por importación de Excel). Pero la pestaña "Tarifas de
plaza" de Biblioteca **además** lee en vivo, de solo lectura, de
`dbSolucionesTI.dbo.tblSAPTarifas` (confirmado en
`biblioteca-page.component.html:123`: "Fuente: dbsolucionesTI ·
tblSAPTarifas (solo lectura) · la vigencia es ajustable en Nexo (override
local)") — hoy conviven las dos fuentes, no es que falte la propia.
Queda pendiente, como alcance futuro, decidir si Biblioteca se queda
usando **las dos** (la propia de NEXO + la externa en vivo), o si en
algún momento se consolida para usar **solo** la propia. No es un bug ni
algo urgente — es una decisión de arquitectura para más adelante.

### Pedido — Vista previa editable antes de confirmar la carga masiva de Excel

**Pedido:** al recargar el Excel de Servicios (ej. Almacenamiento) y
salir un error por una columna nueva no reconocida, Sara pidió un paso
intermedio: una ventana que muestre **todas** las filas ya prellenadas
con lo leído del Excel, editables campo por campo, con las filas
problemáticas resaltadas en rojo, y que solo guarde en la BD cuando el
usuario confirme explícitamente — en vez de bloquear todo el archivo o
guardarlo directo sin revisión.

**Investigado (cómo está hoy):** Servicios de Biblioteca ya valida en
el navegador antes de enviar (`servicios-excel.util.ts`, con la
librería `xlsx`) pero es todo-o-nada: si una fila falla, se bloquea el
archivo completo. Tarifas Plaza es más simple — manda el archivo crudo
directo al backend, sin ningún parseo ni validación en el navegador.

**Alcance confirmado con Sara:** aplica a Servicios de Biblioteca y a
Tarifas Plaza (no a Reglas Solo SICE-TAC). Edición: cualquier campo de
cualquier fila, no solo poder excluir filas.

Es una funcionalidad nueva (no un fix quirúrgico). Se evaluaron 2
caminos: separar "analizar sin guardar" de "confirmar y guardar" en el
backend (requeriría 2 endpoints nuevos), o resolverlo 100% en el
frontend reusando el patrón que ya existe en Servicios de Biblioteca
(parsear el Excel en el navegador, reconstruir uno nuevo con los datos
editados, mandarlo al mismo endpoint `importar-excel` de siempre).
**Sara eligió la segunda opción** — para Tarifas Plaza esto implica
portar a JavaScript el parseo y las reglas de validación que hoy solo
viven en el backend, pero los 4 catálogos que necesita (municipios,
rangos de peso, líneas de negocio, clases de viaje) ya existen como
endpoints de solo lectura (`/api/catalogos/*`, `/api/rangos-peso`) —
no hay que crear ninguno nuevo, solo consumirlos desde el frontend.
Riesgo aceptado: el **algoritmo** de validación (no los datos, que
siempre se consultan en vivo) queda duplicado entre backend y
frontend.

Documentado completo como **HU-1** en `2026-10-09_HU-FRONTEND.md`, sección "Carga
masiva de Excel" (al final del archivo) — `2026-10-09_HU-BACKEND.md` solo tiene
una nota explicando por qué no hizo falta tocar nada ahí.

**Prototipo validado en el sandbox — ambos módulos, de punta a punta:**
- Se extrajo la lectura/escritura genérica de Excel a un util
  compartido nuevo (`excel-lectura.util.ts`), reusando la misma lógica
  robusta de fechas ya validada en el bug anterior (nativo primero,
  texto como respaldo) — no se reescribió.
- **Servicios:** `cargarExcelServicios()` ahora siempre abre la vista
  previa con todas las filas (antes bloqueaba todo el archivo ante
  cualquier error, o importaba directo si no había ninguno).
  `leerServiciosDesdeExcel()` dejó de descartar los valores de filas
  con error. Nuevo `revalidarLectura()` cubre reglas de negocio +
  duplicados + choque con catálogo en vivo, cada vez que se edita una
  celda.
- **Tarifas Plaza:** `tarifas-plaza-excel.util.ts` nuevo, espejo en
  TypeScript de `clsEfTarifasPlazaRepository.ImportarExcelAsync` —
  mismas columnas, mismo criterio de detección de encabezado (texto
  exacto, no "contiene"), municipios/rangos de peso/líneas de
  negocio/clases de viaje contra los catálogos ya existentes, misma
  fecha robusta. Detalle fino replicado bien: el backend de Plaza
  **no** trata duplicados dentro del archivo como error (fusiona en
  silencio, gana la última fila) — la vista previa hace lo mismo.
- Mismo modal/grilla en los 2 (fila roja + mensajes, "Confirmar carga"
  deshabilitado mientras haya errores).
- Ambos compilan. Sin commitear. Pendiente de que Sara lo pruebe en
  pantalla.

**Pedido adicional durante la implementación — auto-corregir columnas
fijas en vez de marcar error (Servicios):** Sara notó que Código/Tipo
de servicio/Unidad/Categoría, cuando el tipo de servicio tiene un valor
fijo (Almacenamiento, ITR), deberían corregirse solas en vez de
marcarse como error. Se confirmó que el código ya auto-corregía
Tipo de servicio/Unidad/Categoría al guardarlos, pero igual marcaba
error (inconsistencia); Código ni se auto-corregía. Queda como **HU-2**
en `2026-10-09_HU-FRONTEND.md`, misma sección.

**Prototipo validado en el sandbox:** `validarFila()` ahora resuelve
`codigo`/`tipoServicio`/`unidad` como `config.tipoServicioFijo ??
textoDelExcel` (y `unidadFija` igual) desde el inicio de la función —
"Falta X" solo se dispara si no hay valor fijo y el campo vino vacío.
Categoría se fuerza siempre, sin check. `validarValoresFila()` cambió
su contrato de `string[]` a `{ valores, errores }`, forzando también
los 4 campos al revalidar una edición en la grilla (antes solo
devolvía errores, sin corregir nada).

**Bug encontrado y corregido de paso:** la detección de fila duplicada
y el chequeo de longitud máxima usaban el código **crudo** (antes de
forzarlo) — una fila con CODIGO vacío en un tipo con valor fijo se
guardaba bien pero la detección de duplicados la trataba como "clave
incompleta" y dejaba pasar duplicados reales sin avisar. Ya corregido,
los dos usan el valor ya forzado.

Compiló bien. Sin commitear. Con esto, los 3 pendientes de esta sección
(vista previa en Servicios, vista previa en Tarifas Plaza, y esta
auto-corrección) quedan completos en el sandbox — pendiente de que
Sara los pruebe en pantalla.

**Pedido adicional tras la primera prueba en pantalla — inputs
apropiados por columna (Servicios):** Sara probó la grilla y pidió: (1)
que Código/Tipo de servicio/Categoría dejen de ser editables, ya que
de todas formas se fuerzan solos (HU-2); (2) que Origen se conecte al
catálogo real de ciudades (como ya hace el formulario manual de "Nuevo
servicio"), en vez de texto libre sin validar (se vio "peru",
"manizales" en minúscula); (3) Vigencia con selector de calendario;
(4) Activo como desplegable; (5) Unidad editable pero como
desplegable, no texto libre; (6) Costo Unitario con separador de miles
mientras se escribe. Todos estos patrones ya existen en otras partes
del mismo repo (formulario manual de Servicios, `mesa-costeo-page.component.ts`)
— no hay que inventar nada nuevo, solo reusarlos en la grilla. Queda
como **HU-3** en `2026-10-09_HU-FRONTEND.md`, misma sección.

**Prototipo validado en el sandbox:** los 6 puntos ya están aplicados
(`biblioteca-page.component.html:376-411`): Código/Tipo de servicio/
Categoría como input deshabilitado; Unidad y Activo como `<select>`;
Vigencia como `<input type="date">`; Origen/Destino con el buscador de
ciudades del formulario manual (nuevo `buscarCiudadPreview()`,
`biblioteca-page.component.ts:675-687`); Costo Unitario con separador
de miles. Compiló bien, sin commitear.

**Bug encontrado al probar en pantalla — las casillas de la grilla solo
dejaban escribir una letra:** `*ngFor` sobre las filas de la grilla
(`biblioteca-page.component.html:376`) no tenía `trackBy`, y
`editarCeldaPreviewServicios()` reconstruye el array completo con
objetos nuevos en cada edición — sin `trackBy`, Angular destruye y
recrea el `<tr>`/`<input>` completo en el DOM en cada tecla (interpreta
que todas las filas cambiaron de identidad), y el input recreado pierde
el foco — por eso solo entraba una letra. Pega más fuerte en
Origen/Destino (disparan en cada tecla vía `(input)`) que en los campos
con `(change)` (solo disparan al salir del campo). Causa raíz
confirmada. **Arreglado:** se agregó `trackBy: trackByIndice` (por
índice de fila) en las **dos** grillas — Servicios y Tarifas Plaza
tenía exactamente el mismo patrón sin `trackBy`. Confirmado que ya se
puede escribir normal en ambas. Compiló bien, sin commitear.

### Pedido — Cada tipo de servicio debe pedir campos distintos en su formulario de creación

**Pedido:** Almacenamiento = Ciudad, Costo, Vigencia, Unidad, Activo;
ITR = Ciudad, Puerto, Tipo de máquina, Movimiento (1 o 2), Costo,
Vigencia, Unidad, Activo; Servicio Adicional 1 = Código, Descripción,
Ruta, Costo, Unidad, Vigencia, Activo; Servicio Adicional 2 = Código,
Descripción, Origen, Destino, Costo, Unidad, Vigencia, Activo.

**Verificado contra el código actual:** Almacenamiento y los dos
"Servicio Adicional" (hoy modelados como `'por_ruta'`/
`'por_origen_destino'`) **ya piden exactamente esos campos** — no hay
nada que cambiar ahí. Lo único que de verdad falta es **ITR**: no
existe ningún campo "Movimiento" — hoy "Tipo de máquina" es un solo
texto libre con un placeholder de ejemplo ("Ej: Grúa pórtico — 2
movimientos"), sin un campo separado real.

**Investigado el campo "Movimiento":** confirmado con Sara que es un
desplegable de 2 opciones (1/2), y que la respuesta debe
**concatenarse con la máquina** porque en la base de datos es un solo
campo (`TipoMaquina`, `clsServicioConexo.cs`) — no se agrega ninguna
columna nueva. El formato exacto de concatenación se confirmó contra
los datos reales ya cargados (`docs/Database/seeder/servicios_conexos.csv`):
`"Maquina 3.5 - 1 Movimiento"`, `"Maquina 7.5 - 2 Movimientos"`,
`"Manual - 1 Movimiento"` — patrón `{máquina} - {N} Movimiento(s)`
(singular si N=1).

Queda como **HU-9** en `2026-10-09_HU-FRONTEND.md`, sección Biblioteca — aplica
solo al formulario manual de "Nuevo servicio"/"Editar servicio", no a
la carga masiva de Excel (que ya trae el texto combinado en una sola
columna).

**Prototipo validado en el sandbox:** nuevo campo `movimientosMaquina`
en el formulario + `<select>` "Movimiento" (1/2), y
`combinarTipoMaquina()` que arma el texto final al guardar. **Detalle
encontrado y resuelto de paso, no pedido explícitamente:** al editar
un servicio ITR ya guardado, el formulario mostraría el string crudo
combinado en vez de los 2 campos separados — se agregó
`partirTipoMaquina()` (con una regex) para separarlo de vuelta al
abrir el editor, con una caída segura (muestra el string completo si
algún registro viejo no calza con el formato exacto). Compiló bien,
sin commitear.

**Pedido adicional:** que "máquina" también sea desplegable (no texto
libre), con las 3 opciones reales confirmadas contra el seeder
("Manual", "Maquina 3.5", "Maquina 7.5"). **Validado en el sandbox:**
cambiado a `<select>` con esas 3 opciones + un placeholder
"— seleccionar —" (mismo patrón ya usado en otros selects obligatorios
del archivo, ej. Clase de contenedor en Captura), para no forzar
"Manual" como seleccionado por defecto. `partirTipoMaquina()` se
revisó y sigue funcionando igual (ninguno de los 3 valores reales
contiene el separador `" - "` que usa la regex). Compiló bien, sin
commitear.

### Pedido — Ocultar de la vista previa las filas existentes sin error (Servicios y Tarifas Plaza)

**Pedido:** al revisar la vista previa de Tarifas Plaza con un Excel
de reimportación grande (`tarifas-plaza-20261012.xlsx`), Sara notó que
se muestran todas las filas, incluidas decenas que ya existían y no
tienen ningún problema — pidió que solo se muestren: filas **nuevas**
(siempre, tengan o no error) y filas **existentes/editadas** solo si
tienen algún error. Las existentes sin error se ocultan de la tabla,
pero se siguen guardando igual al confirmar — es solo un filtro visual.
También pidió un conteo de cuántas quedaron ocultas, para que no
parezca que el Excel trae menos filas de las que trae.

**Investigado:** es viable y no complejo — la clasificación "nueva vs.
existente" ya existe en el código (misma lógica que cuenta "creadas"
vs. "actualizadas"), solo hace falta filtrar qué se renderiza, sin
tocar la lógica de guardado.

Queda como **HU-4** en `2026-10-09_HU-FRONTEND.md`, sección "Carga masiva de
Excel".

**Validado en el sandbox:** campo `esNueva` calculado en la
revalidación de cada grilla, filas ocultas vía `*ngIf` dentro de un
`<ng-container *ngFor>` (para no romper el índice real que usan los
métodos de edición), mensaje de conteo de ocultas, y confirmado que el
guardado no cambia (sigue filtrando solo por errores). Compiló bien,
sin commitear.

### Pedido — Usar en la vista previa de Tarifas Plaza los mismos controles del formulario manual

**Pedido:** que la grilla de Tarifas Plaza use los mismos
desplegables/autocompletados que ya tiene el formulario manual de
"Agregar tarifa", en vez de texto libre en todas las columnas.

**Investigado:** la grilla de Tarifas Plaza hoy es más simple que la
de Servicios — todas las columnas son `<input type="text">` genérico
(`tarifas-plaza-page.component.html:185-189`), sin ningún control
especial. El formulario manual (mismo archivo, líneas 113-151) ya
tiene el control correcto para cada campo: Línea de negocio y Clase de
viaje son `<select>`; Origen/Destino usan autocompletado de ciudades;
Rango de peso es `<select>`; Clase de contenedor y Tipo de vehículo
son texto con sugerencia (`<datalist>`, no obligatorio — ni el
formulario manual ni el backend los fuerzan contra un catálogo
cerrado). Los 6 catálogos ya están cargados en el componente (son los
mismos que usa `tarifas-plaza-excel.util.ts` para validar cada fila),
no hay que pedir nada nuevo.

Queda como **HU-5** en `2026-10-09_HU-FRONTEND.md`, sección "Carga masiva de
Excel".

**Validado en el sandbox:** hecho tal cual el mapeo pedido. **Bug real
encontrado y corregido de paso:** los 6 catálogos solo se cargaban al
abrir el modal manual "Agregar tarifa" — si alguien entraba directo a
"Importar Excel", los selects/datalist de la grilla habrían salido
vacíos. Se corrigió para que se carguen siempre que se abra la vista
previa, sin importar de dónde se entre — de paso eliminó una llamada
duplicada a la API. Compiló bien, sin commitear.

### Ajustes tras la primera prueba en pantalla de HU-5

Sara probó la grilla ya con los controles de catálogo y encontró 3
cosas: (1) **posible bug** — "Clase de viaje" no viene preseleccionado
con el valor de la fila, aunque "Línea de negocio" sí, en una fila de
prueba con varios datos inválidos a propósito — queda por investigar
si es un dato real que no coincide con el catálogo (comportamiento
correcto) o un bug de binding; (2) **Clase de contenedor** pasa de
sugerencia (`<datalist>`) a `<select>` estricto, igual que Rango de
peso — corrige lo que HU-5 había especificado originalmente,
confirmado con Sara que en la grilla lo quiere cerrado (el formulario
manual se queda igual, sin tocar); (3) **Vigencia** debe ser un
selector de calendario (`<input type="date">`), no texto libre —
mismo patrón que ya usa el formulario manual y la grilla de Servicios.

Queda como **HU-6** en `2026-10-09_HU-FRONTEND.md`, sección "Carga masiva de
Excel".

**Investigado y validado en el sandbox:** confirmado que era el bug
real, no un dato inválido — la validación compara normalizado (sin
tildes/mayúsculas) pero el `<select>` compara exacto, así que una
capitalización distinta a la del catálogo pasaba la validación pero no
preseleccionaba. Esto replica un comportamiento real del backend
(normaliza para matchear, pero guarda la forma canónica del catálogo,
no el texto crudo) — la vista previa no lo estaba replicando.
**Arreglo:** la validación ahora también canoniza el valor de la fila
(Línea de negocio, Clase de viaje, Origen, Destino) a la forma exacta
del catálogo cuando hay match. **Mejora adicional, no pedida:** se
agregó también el desempate Colombia-si-choca-el-nombre para Origen/
Destino, igual que ya hace el backend (antes la vista previa solo
chequeaba existencia). Clase de contenedor ya es `<select>` estricto y
Vigencia ya es calendario. Compiló bien, sin commitear.

### Ajustes tras la segunda prueba en pantalla

Sara probó de nuevo y encontró: (1) Rango de peso y Clase de
contenedor tampoco vienen prellenados — mismo tipo de causa que HU-6
(la canonización se aplicó solo a 4 de los 6 campos de catálogo),
pendiente investigar si estas filas de ejemplo tienen un dato
genuinamente inválido o es el mismo problema de comparación; (2) pidió
que Tipo de vehículo pase a `<select>` estricto también (revierte lo
decidido antes de dejarlo como sugerencia), por consistencia visual;
(3) pidió revisar que todos los desplegables de la grilla usen el
mismo estilo CSS que el resto de la página.

Queda como **HU-7** en `2026-10-09_HU-FRONTEND.md`, sección "Carga masiva de
Excel".

**Investigado y validado en el sandbox:** eran 2 causas distintas.
Rango de peso tenía el mismo patrón de HU-6 pero por espacios
incidentales en el formato (no por tildes/mayúsculas) — corregido para
ignorar espacios, sin volverse más permisivo con datos realmente
inválidos. Clase de contenedor tiene una causa distinta: el backend
**nunca** la valida contra ningún catálogo (campo 100% libre) —
confirmado contra el código real — así que el problema era solo la
comparación exacta del select, no una validación faltante; se agregó
un catálogo de canonización aparte para Clase de contenedor/Tipo de
vehículo, que existe solo para prellenar, nunca para marcar error.
Tipo de vehículo ya es `<select>` estricto. Consistencia visual
corregida en Tarifas Plaza — y de paso se encontró y corrigió el mismo
hueco en los selects de Servicios (Unidad/Activo), confirmado con Sara
que se deja. Compiló bien, sin commitear.

### ⚠️ Bug en investigación activa — Línea de negocio/Clase de viaje no preseleccionan (sin resolver)

Historial de la investigación sobre la misma fila de prueba (109,
SANTA MARTA → MEDELLIN):

1. **"Catálogos vacíos" — descartada.** Contador de diagnóstico
   temporal confirmó datos reales en los 6 catálogos (6/4/3804/8/5/11).
   El contador tenía su propio bug (mezclaba catálogo en vivo del
   formulario vs. la foto fija de la validación) — corregido, los dos
   salieron iguales, también descarta foto fija desactualizada.
2. **"Catálogo de SAP cambió, texto de prueba obsoleto" — descartada.**
   Sara confirmó en el formulario manual que "Oferta de Expedición
   Puerto a interior"/"Contenedor" existen tal cual en el catálogo
   real.
3. **Confirmado: el match SÍ ocurre internamente, sin error.** La
   columna "Problemas" de la fila 109 solo muestra RANGO PESO y
   VIGENCIA — nada de Línea de negocio/Clase de viaje. El código ya
   canonizó esos 2 valores bien — el problema es puramente visual en
   el `<select>`.
4. **Primer experimento (retrasar creación de la fila con
   `setTimeout`) — no funcionó, revertido.** No probaba bien la
   hipótesis del timing de Angular con select+opciones async.
5. **Segundo experimento (reasignar `previewPlaza` una segunda vez
   después de dibujada la fila, para forzar que Angular repase los
   bindings) — tampoco funcionó, revertido.** El código sí canoniza
   bien el valor, el problema está entre eso y cómo el `<select>` lo
   refleja.
6. **✅ Tercer experimento — confirmado por Sara en pantalla real,
   funcionó, bug resuelto.** El peer explicó
   por qué los 2 anteriores no probaban bien la hipótesis: Angular
   compara contra lo último que ÉL MISMO intentó escribir en el
   `[value]`, no contra lo que de verdad quedó en el DOM — si el valor
   nunca cambia entre intentos, nunca reintenta, aunque se le obligue a
   repasar bindings. **Arreglo probado:** dibujar primero los 5
   `<select>` de la fila en blanco, y un instante después reemplazarlos
   por los valores ya canonizados — así Angular sí nota el cambio. Costo
   aceptado por Sara: parpadeo breve en esos 5 campos.

**Decisión nueva durante esta investigación:** Clase de contenedor
pasa a marcar error en la grilla cuando no coincide con el catálogo,
aunque el backend real nunca la rechace — decisión consciente de
hacer la grilla más estricta que el backend para este campo
específico, confirmada con Sara. **Ya aplicado y compilado.**

**Extendido a Tipo de vehículo — luego revertido.** Se aplicó el mismo
criterio de error a Tipo de vehículo, compiló bien. Al probarlo, Sara
encontró que el catálogo real de SAP no tiene todos los códigos que
necesita (ej. "3S2", "2L3") — con la validación activa, filas con
esos códigos válidos quedaban marcadas como error. **Decidido:** no
hardcodear esos códigos (eso deshace lo que se acababa de limpiar en
HU-B5), tampoco validar Tipo de vehículo contra el catálogo — se
revierte a canonizar sin marcar error. El `<select>` se queda cerrado
(mismo problema de fondo: códigos reales que falten en el catálogo de
SAP no se pueden elegir ahí hasta que alguien actualice
`dbSolucionesTI` — no es algo que se resuelva en este código). Clase
de contenedor no se revierte, sigue marcando error.

**Decisión final de Sara:** dejar el hueco del catálogo (3S2/2L3
faltantes en `dbSolucionesTI`) como está por ahora, sin perseguirlo
más — no se agrega como pendiente ni se escala a mantenimiento de
datos de SAP, al menos por el momento.

**✅ Confirmado el código en el desplegable (HU-8):** Sara verificó en
pantalla real — el primer intento no se veía por caché del navegador,
no por el código; resuelto con un refresco forzado.

### Pedido — Mostrar el código en el desplegable de Tipo de vehículo

Una vez confirmado el prellenado (punto anterior), Sara notó en el
desplegable de Tipo de vehículo que solo se ve el nombre descriptivo
("Tractomula", "Minimula"), no el código ("3S3", "2S2") que sí
reconoce. Reglas Solo-SICETAC ya resuelve esto bien
(`reglas-solo-sicetac-page.component.html:48`, `{{ tipo.codigo }} ·
{{ tipo.descripcion }}`) — mismo arreglo para la grilla. Queda como
**HU-8** en `2026-10-09_HU-FRONTEND.md`, sección "Carga masiva de Excel". **Ya
aplicado y compilado.**

**Nota de contexto:** en la misma captura siguen apareciendo los 5
casos especiales que ya se eliminaron del backend (HU-B5) — confirmado
que es solo porque el backend real desplegado todavía no tiene ese
cambio. Sara decidió **no** agregar un parche temporal en el frontend,
esperar el despliegue real.

## Tarifas Plaza

### Bug confirmado — la carga masiva (Excel) no lee bien la fecha de Vigencia

**Reportado:** al cargar el Excel de carga masiva de Tarifas Plaza, la
fecha de Vigencia queda "transformada" en algunas filas.

**Causa raíz confirmada contra el código de NEXO:**
`clsEfTarifasPlazaRepository.cs:93,126,198` lee la columna VIGENCIA con
el helper genérico de texto (`Str()` → `GetString().Trim()`, sin mirar
si la celda es una fecha nativa de Excel) y la parsea con
`DateOnly.TryParse(vigenciaTxt, out var vigencia)` sin especificar
cultura (usa la del servidor). Si Excel guardó la fecha como valor
nativo (serial + formato de celda), esto puede devolver el serial
crudo o un texto mal interpretado — de ahí la fecha transformada o el
error "no es una fecha de vigencia válida" en filas que en el Excel se
ven perfectamente normales.

**La columna VALOR (numérica) ya resuelve el mismo tipo de problema
bien** (`Num()`, línea 203-209: tipo nativo primero, texto como
fallback) — pero el mismo criterio nunca se aplicó a VIGENCIA.

**El arreglo correcto ya existe, probado, en otro importador del mismo
proyecto:** `clsEfServiciosConexosRepository.cs:226-235`, función
`LeerFecha(IXLCell celda)` — primero intenta el tipo nativo `DateTime`
de la celda, y solo si no es nativo intenta varios formatos de texto
explícitos (`dd/MM/yyyy`, `d/M/yyyy`, `yyyy-MM-dd`, `MM/dd/yyyy`),
terminando con cultura `es-CO` fijada a propósito (no la del servidor).

**Arreglo:** copiar `LeerFecha()` a `clsEfTarifasPlazaRepository.cs` y
usarlo en vez de `Str(row, cols, "VIGENCIA")` + `DateOnly.TryParse(...)`.

Esto queda como **HU-B4** en `2026-10-09_HU-BACKEND.md` — pendiente de
implementar (Sara decidió solo documentarlo por ahora, no aplicarlo
todavía).

**Pedido de alcance:** Sara pidió dejar claro que este mismo criterio
aplica a **cualquier carga masiva de Excel** del proyecto que tenga
columna de fecha, no solo Tarifas Plaza. Se revisaron los otros 3
importadores de Excel existentes: Servicios Conexos ya está correcto
(es la referencia); Reglas Solo SICE-TAC no tiene columna de fecha (no
aplica); la carga masiva de líneas de licitación (`clsExcelSolicitudesAdapter.cs`)
usa un mecanismo distinto (sniffer de texto genérico, mapeo libre) y no
se confirmó que tenga este bug — queda para revisar aparte si algún día
se reporta algo similar ahí. Detalle completo de la tabla y la regla
general en `2026-10-09_HU-BACKEND.md`, HU-B4.

### Hallazgo — mismo bug de fecha ya documentado antes, por partida doble

Al revisar el desplegable de Tipo de Vehículo de Reglas Solo-SICETAC
apareció `docs/backlog/2026-10-05_REPARACIONES-FINALES-TARIFAS-PLAZA.md`, un documento
previo (2026-10-05) con 6 hallazgos propios, nunca fusionados a
`2026-10-09_HU-FRONTEND.md`/`2026-10-09_HU-BACKEND.md`. Su punto 1 es **el mismo bug** que
HU-B4 (fecha VIGENCIA leída como texto crudo en vez de como valor
nativo de la celda) — confirmado con un caso real (`46315` = serial de
2026-10-20, rechazado al reimportar). Ya se fusionó la nota en HU-B4.
Sara pidió fusionar también los otros 5 hallazgos de ese documento —
quedan repartidos abajo (Tarifas Plaza, Reglas Solo-SICETAC, Mesa de
Costeo) y en `2026-10-09_HU-BACKEND.md`.

### Mostrar detalle de errores de importación (frontend) — ya documentado, no implementado

El backend ya devuelve el detalle por fila de cada rechazo
(`resultado.errores`), pero el frontend solo muestra el conteo
("N rechazada(s)"). Queda como **HU-1** en `2026-10-09_HU-FRONTEND.md`, sección
Tarifas Plaza.

### Editar línea rechazada sin reimportar todo el Excel — ya documentado, no implementado

Depende de la HU anterior (hace falta saber qué campo falló antes de
poder editarlo). Queda como **HU-2** en `2026-10-09_HU-FRONTEND.md`, sección
Tarifas Plaza.

### 2 hallazgos secundarios, sin HU todavía (pendientes de decidir alcance)

Ciudades ambiguas resueltas en silencio (toma un departamento sin
avisar si el nombre choca entre 2+) y Tipo de Vehículo no validado
contra Rango de Peso en la carga masiva. Detalle en `2026-10-09_HU-FRONTEND.md`,
sección Tarifas Plaza, al final.

## Reglas Solo-SICETAC

### Bug confirmado — casos especiales de Tipo de Vehículo hardcodeados en el código

El desplegable de Tipo de Vehículo combina el catálogo real
(`tblSAPTiposVehiculoTarifario`) con 5 valores escritos a mano en
`clsTiposVehiculoAdapter.cs` (`CNT20/Consolidado`, `CNT20/Exclusivo`,
`CNT40/Consolidado`, `CNT40/Exclusivo`, `PBV 7500-8000`), no en base de
datos — el propio comentario del código ya lo marcaba como pendiente
("issue pendiente, ver comentario de Sara en #210"). **Confirmado con
Sara (2026-10-12): esos 5 casos ya no son necesarios** — no se trata
de moverlos a una tabla, sino de **eliminarlos** del desplegable. Las
reglas ya existentes que los usen no se tocan. Queda como **HU-B5** en
`2026-10-09_HU-BACKEND.md`.

**✅ Implementado (2026-10-12):** se detectó de nuevo este mismo
catálogo (el desplegable con los 5 casos especiales) apareciendo en el
formulario manual de Tarifas Plaza y en la vista previa de carga
masiva — confirmó que era la misma fuente sin arreglar. Se implementó
directo en NEXO: `clsTiposVehiculoAdapter.ListarAsync` ya no concatena
`CasosEspeciales`, devuelve solo el catálogo real. Compilado, 0
errores. Arregla las 3 pantallas a la vez (mismo endpoint). Pendiente
de desplegar.

### Falta exportar a Excel, igual que Tarifas de Plaza

Reglas Solo-SICETAC solo tiene importar + plantilla en blanco; Tarifas
de Plaza ya tiene exportar-el-catálogo-cargado (que sirve de plantilla
para reimportar). Queda como **HU-B6** en `2026-10-09_HU-BACKEND.md` (backend) y
**HU-1** en `2026-10-09_HU-FRONTEND.md`, sección Reglas Solo-SICETAC (botón).

### Mejora — Reemplazar texto descriptivo fijo por ícono de información, mismo patrón de Bandeja/Biblioteca

**Pedido:** mismo tratamiento que ya se hizo en Biblioteca — el
párrafo fijo bajo el título ("Demarca combinaciones de origen, destino
y tipo de vehículo que se cotizan EXCLUSIVAMENTE con el piso SICE-TAC...")
debe desaparecer y quedar como tooltip de un ícono de información,
junto al título "Rutas solo SICE".

Queda como **HU-2** en `2026-10-09_HU-FRONTEND.md`, sección Reglas Solo-SICETAC —
pendiente de que el equipo de frontend la implemente.

### Mejora — Mismo tratamiento de ícono de información en Cubicador, Tablero y Tarifas Plaza

**Pedido:** extender el mismo patrón (párrafo fijo bajo el título →
ícono de información con tooltip) a 3 pantallas más:
- **Cubicador**: "Plan de cargue: registra el vehículo y la carga,
  optimiza la distribución y revisa la gráfica 3D interactiva."
- **Tablero**: "Cumplimiento de SLA y estado de la cola." (nota: este
  texto no menciona los otros 2 bloques reales de la pantalla — KPIs
  del piloto, monitoreo SICE-TAC — se mueve tal cual, sin corregir,
  porque no se pidió corregirlo).
- **Tarifas Plaza**: "Tarifas de plaza gobernadas por Nexo, con carga
  y descarga masiva por Excel."

Quedan como **HU-1** en `2026-10-09_HU-FRONTEND.md` (sección Cubicador), **HU-1**
(sección Tablero) y **HU-3** (sección Tarifas Plaza).

**Validado en el sandbox, las 3:** texto del tooltip idéntico en cada
una (el de Tablero sin corregir el contenido, tal como se pidió). En
Tarifas Plaza se confirmó que el `<div>` envolvente no tenía uso
propio y se quitó. Ninguna de las 3 tenía el bloque CSS
`.cabina-info-btn` — se copió en las 3, más `display:flex;gap:8px` en
`.cabina-page-header h1`, y se quitó `.cabina-page-header p` por
quedar huérfana. Compiló bien, sin commitear.

## Tablero

**De cara a qué rol:** confirmado en `clsMetricasController.cs`
(`[Authorize(Roles = "LiderProceso,AdminTarifas,Admin")]`) — de los 3
roles entregados al cliente, el Tablero es para **AdminTarifas** y
**Admin**; Comercial no lo ve. Todo lo que sigue en esta sección
(división en pestañas, filtro de fecha, redefinición de tiempo de
costeo, métricas propuestas) es de cara a esos 2 roles.

### Pedido — Dividir en 2 subvistas: Productividad y SICE-TAC

**Pedido:** hoy el Tablero tiene 3 bloques seguidos en una sola
pantalla (Cumplimiento de SLA/cola, KPIs del Piloto, Monitoreo de
Tarifas SICE-TAC — un iframe embebido). Sara pidió separarlos en 2
pestañas: **"Tablero de Productividad"** (los 2 primeros bloques,
SLA/cola + KPIs del Piloto) y **"Tablero SICE-TAC"** (el iframe).

**Investigado:** el iframe de SICE-TAC no hace ninguna llamada a API
(`kpisSicetacUrl` es una URL fija de `environment.tableroKpisUrl`), así
que no hace falta ningún lazy-load por pestaña como sí tiene
Biblioteca — los 2 bloques de "Productividad" ya se cargan juntos hoy
en `ngOnInit()`. Mismo patrón visual/accesibilidad de pestañas que ya
usa Biblioteca (`tabs`/`tab`/`cambiarTab()`/`onTabKeydown()`, roles
ARIA, navegación por flechas).

Queda como **HU-2** en `2026-10-09_HU-FRONTEND.md`, sección Tablero — pendiente
de implementar.

### Pedido — Filtro de fecha para Pendientes/En costeo/Entregadas/Descartadas/Total

Surgió de la discusión sobre cómo medir de forma confiable el "tiempo
de costeo del pliego" (ver bug de Mesa de Costeo más abajo): al revisar
las 6 tarjetas del bloque "Cumplimiento SLA / cola", se confirmó que 3
de ellas (Entregadas, Descartadas, Total solicitudes) son un acumulado
desde que existe el sistema, sin ningún filtro de fecha — crecen para
siempre y no sirven para ver "qué pasó esta semana".

**Decisión de Sara:** el filtro de fecha aplica a las 6 tarjetas del
bloque (no solo a esas 3) — Pendientes/En costeo se filtran igual por
consistencia, aunque conceptualmente son "la cola ahora mismo". Para
"Descartadas" no existe una fecha real de "cuándo se descartó" en la
base de datos — se aproxima con la fecha de creación de la solicitud,
aceptando que no es 100% exacto. **Cumplimiento SLA se deja sin
filtro, a propósito** — es un resultado sobre lo ya entregado, no una
foto de la cola.

Se implementó primero el backend completo (compilado, 0 errores) y se
le pidió al chat del frontend que lo prototipara — **Sara pidió
detener eso y dejarlo solo documentado por ahora**, así que se revirtió
todo (backend acá, y el frontend también revirtió lo que había
alcanzado a aplicar). Queda como **HU-B12** en `2026-10-09_HU-BACKEND.md` y
**HU-3** en `2026-10-09_HU-FRONTEND.md` (ambos en sección Tablero) — pendiente de
implementar, ninguno de los dos lados.

### Especificación final del bloque "Cumplimiento SLA / cola" y "KPIs del Piloto" (2026-10-12)

Sara dio la especificación final de cómo debe quedar este bloque,
corrigiendo varias cosas de las rondas anteriores:

- **Orden y color — ✅ implementado de verdad, con datos reales**:
  Total solicitudes, Pendientes (rojo), En costeo (azul, tone nuevo
  `info`), Entregadas (verde), Descartadas (negro), Cumplimiento SLA
  (tone dinámico). Se quitaron los hints "por costear" y "en proceso".
  Confirmado compilando sin errores — este cambio sí quedó aplicado en
  el sandbox, a diferencia del resto de esta sección.
- **Corrección sobre el filtro de fecha (HU-B12/HU-3):** ya no son 2
  filtros independientes por bloque — es **un solo filtro compartido**
  entre "Cumplimiento SLA / cola" y "KPIs del Piloto". Y Cumplimiento
  SLA **sí entra al filtro** (se había documentado lo contrario antes)
  — se filtra por `EntregadaAt`, las otras 5 tarjetas por `CreatedAt`.
  HU-B12/HU-3 ya se corrigieron para reflejar esto.
- **Redefinición de "Tiempo de costeo del pliego":** si el pliego se
  auto-costeó 100% solo, se mide como hoy (fin de la saga); si
  requirió Admin Tarifas, se mide hasta `EntregadaAt` real (el híbrido
  que se había discutido antes, con el hueco conocido de HU-B8). Se
  suma un filtro de tamaño de pliego (1-10/11-100/+100 líneas) que
  recalcula el promedio dinámicamente, sin perder el número general.
  Documentado como **HU-B13** (`2026-10-09_HU-BACKEND.md`) y **HU-4**
  (`2026-10-09_HU-FRONTEND.md`), sección Tablero — pendiente de implementar.
- **% líneas auto-costeadas** y sus 2 derivados: confirmado que quedan
  igual, sin cambios.
- **2 métricas nuevas propuestas** (recomendadas por Claude, a
  pedido de Sara): "Antigüedad de solicitudes pendientes" (barras,
  0-2/3-7/+7 días) y "Evolución semanal" (línea de tiempo,
  Cumplimiento SLA % y tiempo de costeo promedio por semana). Se
  prototiparon en el sandbox con **datos falsos** (`chart.js`/
  `ng2-charts`) para que Sara viera el diseño visual — confirmado que
  compilaba bien. **Sara pidió revertirlo** ("después miramos si los
  aplicamos") — confirmado revertido por completo (HTML, TS, CSS,
  import del módulo). Queda documentado como **HU-5** en
  `2026-10-09_HU-FRONTEND.md` (sección Tablero), como propuesta sin decisión
  todavía, no como HU aprobada.

## Mesa de Costeo

### Bug confirmado — se puede marcar Ganada/Perdida sin pasar por Entregar ni auto-costeo

La regla correcta (solo registrar resultado si la cotización está
Entregada, o en Borrador con `esAutoCosteo = true`) está bien aplicada
en `cotizacion-resumen-page.component.ts`, pero Mesa de Costeo tiene
sus propios botones que no la replican — solo revisan el permiso del
usuario. Se puede marcar Perdida/Ganada sin que la cotización se haya
entregado ni auto-costeado. Queda como **HU-10** en `2026-10-09_HU-FRONTEND.md`,
sección Mesa de Costeo.

### 1. FRONTEND — "Piso SICE-TAC + tiempos logísticos" ignora las horas propias de la línea

**Archivo:** `mesa-costeo-page.component.ts:417-420` (`floorTotal`) y
`:787` (donde se usa para prellenar `valorFlete`).

```ts
floorTotal(piso: PisoSicetacDto | null, tiempos: TiemposLogisticosDto): number | null {
  if (!piso) return null;
  const horas = tiempos.horasCargue + tiempos.horasDescargue;
  return piso.valorPiso + (piso.valorHora ? horas * piso.valorHora : 0);
}
```

Cada línea puede tener sus propias `HorasCargue`/`HorasDescargue`
(capturadas aparte de las de la solicitud completa, S4-HU-15.5/#251) — y
ese dato **ya le llega al frontend** dentro de `entry.linea`
(`clsLineaCosteoDto`, `clsObtenerCosteoQuery.cs:53`). El backend sí las usa
correctamente al calcular la "Propuesta de la Biblioteca"
(`clsPropuestaTarifaAdapter.cs`, función `HorasEfectivasDeLinea`: prioriza
las horas de la línea sobre las de la solicitud). Pero `floorTotal()`
**nunca mira `entry.linea.horasCargue`/`horasDescargue`** — siempre usa
`costeo.tiempos` (las horas generales de la solicitud) para cualquier
línea. Cuando una línea tiene horas propias distintas a las de la
solicitud, "Propuesta de la Biblioteca" y "Piso SICE-TAC + tiempos
logísticos" muestran números distintos para el mismo piso SICE-TAC — y
como este valor prellena `valorFlete` del formulario, el flete que se
termina cobrando puede quedar mal.

**Arreglo:** que `floorTotal()` reciba (o calcule) las horas efectivas de
la línea con la misma prioridad que ya usa el backend: horas propias de
la línea si existen, si no las de la solicitud.

Criterios de aceptación:
- [ ] Con una línea que tenga `horasCargue`/`horasDescargue` propias
      distintas a las de la solicitud, "Piso SICE-TAC + tiempos
      logísticos" coincide con el candidato SICE-TAC de "Propuesta de la
      Biblioteca".
- [ ] Una línea sin horas propias sigue usando las de la solicitud, sin
      regresión.

**✅ Resuelto (2026-10-09) — así debe quedar la pantalla, exacto:**
```
PROPUESTA DE LA BIBLIOTECA
┌─────────────────────┬──────────────┬────────────────┐
│ PISO SICE-TAC +      │ TARIFARIO    │ HISTÓRICO SAP  │   ← solo 3 cajas, ya NO hay
│ TIEMPOS LOGÍSTICOS   │              │                │     una 4ª caja aparte de
│ $2.510.931           │ Sin coincid. │ Sin coincid.   │     "Piso SICE-TAC" solo
└─────────────────────┴──────────────┴────────────────┘
(justificación de la ruta, si aplica)

┌───────────────────────────────────────────────────────┐
│ FUENTE PROPUESTA                                       │  ← caja verde (o roja
│ $2.510.931                              SICE-TAC       │     clara si no hay
│                           [Usar propuesta] (si no hay   │     propuesta), precio
│                            flete guardado todavía)      │     y fuente grandes,
└───────────────────────────────────────────────────────┘     botón adentro

Flete guardado · SICE-TAC    Flete $X + aprovisionamiento $Y    $Total
```
No queda ninguna caja "Piso SICE-TAC" suelta aparte de las 3 de la
comparación — el candidato SICE-TAC de esas 3 **ya es** "Piso SICE-TAC +
tiempos logísticos" con el valor correcto. El botón "Usar propuesta" vive
dentro de la caja verde "Fuente propuesta", no arriba. Confirmado en
pantalla real (línea Medellín → Buenaventura, captura 2026-10-09).

### 2. FRONTEND — "Valor estimado" no se muestra en Mesa de Costeo

**Dónde se mostraba:** `mesa-costeo-page.component.html:65`
(`costeo.solicitud.valorEstimado`), en el resumen de la solicitud.

**✅ Resuelto (2026-10-09) — tarea cerrada, 100% frontend.** Se quitó el
`<div>` del `<dl>` de resumen — nunca se vuelve a mostrar en Mesa de
Costeo, sin importar si el dato existe o no. No hace falta ningún cambio
de backend para esta tarea puntual.

**Nota aparte, NO parte de esta tarea:** `clsScorePrioridadCalculator.cs:30`
sigue usando `ValorEstimado` para el score de prioridad, y nada lo
captura en ningún formulario todavía — eso sigue abierto como su propio
tema (si en algún momento importa arreglarlo), pero es independiente de
"ocultarlo en Mesa de Costeo", que ya quedó resuelto.

**Dónde se muestra:** `mesa-costeo-page.component.html:65`
(`costeo.solicitud.valorEstimado`).

**El hueco:** el campo existe de punta a punta en el backend (columna en
`clsSolicitud`, DTOs de captura y de solicitud, comando de creación/
captura) pero **no hay ningún input en los formularios de captura**
(`captura-page.component.ts`/`.html`) que lo llene — ni en el modelo de
TypeScript (`captura-api.models.ts:44`, `valorEstimado?: number`, sin
ningún control de formulario asociado). Siempre llega vacío.

**Por qué importa más de lo que parece:** `clsScorePrioridadCalculator.cs:30`
usa `ValorEstimado` para calcular el score de prioridad de cada solicitud.
Como siempre está vacío, esa parte de la fórmula cae siempre al mismo
valor por defecto (`ValorPorDefectoSinValor`) para **todas** las
solicitudes — el score de prioridad nunca varía por el valor real del
negocio potencial, solo por los demás componentes de la fórmula.

**Decidido (2026-10-09):** se quita de la pantalla de Mesa de Costeo (el
resumen de la solicitud ya no muestra "Valor estimado"). **Sigue
pendiente** la otra mitad: qué hacer con el campo en el backend —
`clsScorePrioridadCalculator.cs:30` lo sigue usando para el score de
prioridad, y sigue sin existir ningún formulario que lo capture. Falta
decidir si se agrega un campo de captura real, o si se quita también del
cálculo del score (ya que hoy es puro relleno que nunca cambia).

### 3. PENDIENTE DE DECIDIR — los tiempos logísticos por línea no se ven en Mesa de Costeo

**Contexto:** cada línea puede tener sus propias `horasCargue`/
`horasDescargue`, distintas a las de la solicitud completa (S4-HU-15.5/
#251) — el backend ya las manda en `clsLineaCosteoDto`
(`entry.linea.horasCargue`/`horasDescargue`) y ya las prioriza sobre las de
la solicitud al calcular el piso SICE-TAC
(`clsPropuestaTarifaAdapter.cs:206-207`, `HorasEfectivasDeLinea`: usa las
de la línea **solo si las dos** (`HorasCargue` y `HorasDescargue`) están
resueltas; si falta cualquiera de las dos, cae completo al valor de la
solicitud, nunca mezcla una de cada lado).

**El hueco:** en Mesa de Costeo, "Tiempos logísticos" se muestra **una sola
vez**, global, en el resumen de arriba (`mesa-costeo-page.component.html:66`)
— no hay nada por línea. Investigado también: la única pantalla donde hoy
se ve/edita horas por línea es Captura (`captura-page.component.html:186-189`);
Mesa de Costeo no edita nada de la solicitud/línea en ningún lado (solo
edita valores propios del costeo: flete, retorno contenedor, servicios
conexos), así que la propuesta es que esto sea **de solo lectura**, no un
campo editable ahí.

**Propuesta (de `administracion-embarqa-nexo-79`, pendiente de confirmar):**
agregar un chip más en `.mesa-data-chips` (el mismo bloque donde ya están
Tipología del vehículo/Carrocería/Peso/Tipología de carga), mostrando las
horas efectivas de esa línea (propias si están completas, si no las de la
solicitud) y aclarando "(de la solicitud)" cuando está heredando:

```
Tiempos logísticos: 4 h cargue + 4 h descargue (de la solicitud)
```

Reintroduciría `tiemposDeLinea()` (la misma función que se había borrado
por limpieza al quitar `floorTotal()`, ahora hace falta de nuevo para esto).

**Falta decidir:** ¿se confirma este enfoque (chip de solo lectura en el
mismo bloque de chips), o prefiere algo distinto (badge aparte, tooltip,
etc.)?

**Resuelto (2026-10-09):** confirmado, se aplicó el chip de solo lectura.

### 4. Chips nuevos por línea: Línea de negocio, Clase de viaje, Clase de contenedor — aplicado, sin probar todavía

Igual que con los chips anteriores, pero este sí requirió tocar backend
(`LineaNegocio`/`ClaseViaje`/`ClaseContenedor` no viajaban antes a Mesa de
Costeo):
- `SolicitudDtos.cs:69-75` (`clsLineaCosteoDto`) — 3 campos nuevos, opcionales.
- `clsObtenerCosteoQuery.cs:50-53` — ahora los puebla desde
  `l.LineaNegocio`/`l.ClaseViaje`/`l.ClaseContenedor` (ya existían en la
  entidad, solo no se exponían). Compilado, 0 errores.
- Frontend: 3 chips agregados en `.mesa-data-chips`, ocultos con `*ngIf`
  cuando el dato es `null` (mismo patrón que los chips existentes).

**Sin probar todavía — a propósito.** El frontend de
`administracion_embarqa_nexo` apunta al backend **ya desplegado**
(sslip.io), no al código fuente local recién editado. Hasta que ese
backend se vuelva a desplegar con este cambio, los 3 campos van a llegar
`null` y los chips no se van a ver, aunque el código esté correcto. Sara
decidió no probarlo por ahora — queda pendiente de retomar cuando haya un
despliegue real (o se decida apuntar el front al backend local para
probar antes).

### 5. Edición completa en Mesa de Costeo — detalle para las HU del front

Spec basada en lo que se probó en vivo en esta sesión de QA, contra
`administracion_embarqa_nexo` (rama `main`, sin commitear todavía).
Verificado con `git diff` real de los 3 archivos tocados:

- `src/app/cabina-fletes/pages/mesa-costeo/mesa-costeo-page.component.html`
- `src/app/cabina-fletes/pages/mesa-costeo/mesa-costeo-page.component.ts`
- `src/app/cabina-fletes/pages/mesa-costeo/mesa-costeo-page.component.css`

Cada punto es tal como lo escribió Sara, con el estado real (✅ listo /
⚠️ parcial / ❌ falta) y el detalle técnico exacto de qué se movió, para
que el front lo replique igual en sus propias HU.

#### 5.1. "Recordar que horas de cargue y descargue son por línea de negocio por lo que en la mesa de costeo también se deben mostrar" — ✅ listo

Se agregó un chip más por línea, en el bloque `.mesa-data-chips` (junto a
Tipología del vehículo/Carrocería/Peso/Tipología de carga):

```html
<span>
  <b>Tiempos logísticos</b>{{ tiemposDeLinea(entry.linea, costeo.tiempos).horasCargue }} h cargue + {{ tiemposDeLinea(entry.linea, costeo.tiempos).horasDescargue }} h descargue<ng-container *ngIf="entry.linea.horasCargue === null || entry.linea.horasDescargue === null"> (de la solicitud)</ng-container>
</span>
```

`tiemposDeLinea(linea, tiemposSolicitud)` (en el `.ts`) usa las horas
propias de la línea **solo si las dos** (`horasCargue`/`horasDescargue`)
están resueltas; si falta cualquiera, cae completo a las de la solicitud —
mismo criterio que ya usa el backend (`HorasEfectivasDeLinea` en
`clsPropuestaTarifaAdapter.cs`, NEXO). Cuando hereda de la solicitud, se
aclara con "(de la solicitud)".

De paso se quitó el resumen **global** de tiempos logísticos (ver 5.2).

#### 5.2. "Valor estimado siempre es null no mostrarlo en la mesa de costeo" — ✅ listo

Se quitaron 2 líneas del `<dl>` de resumen de la solicitud (arriba de
todo, junto a Tipo/Estado/Prioridad/Fecha límite):

```html
<!-- Quitado: -->
<div><dt>Valor estimado</dt><dd>{{ formatMoney(costeo.solicitud.valorEstimado) }}</dd></div>
<div><dt>Tiempos logísticos</dt><dd>{{ costeo.tiempos.horasCargue }} h cargue + {{ costeo.tiempos.horasDescargue }} h descargue</dd></div>
```

**Nota para backend/negocio, pendiente aparte (ver hallazgo #2 de esta
misma sección):** `valorEstimado` sigue existiendo en el backend y lo
sigue usando `clsScorePrioridadCalculator` para el score de prioridad —
quitarlo de esta pantalla no resuelve que nunca se capture en ningún
formulario. Eso no es un cambio de front, queda pendiente de decidir
aparte.

#### 5.3. "Que no aparezca aparte piso sicetac + tiempos logísticos, que aparezcan únicamente los 3, y el de arriba que dice piso sicetac aparezca de una vez piso sicetac + tiempos logísticos" — ✅ listo

- Se quitó por completo la caja separada `.mesa-rate-box` "Piso SICE-TAC +
  tiempos logísticos" (la que leía `entry.linea.pisoSicetac` con
  `floorTotal()`). Solo quedan las 3 cajas de "Propuesta de la Biblioteca"
  (SICE-TAC/Tarifario/Histórico SAP) + la caja "Fuente propuesta" (ver
  5.4).
- El candidato SICE-TAC de esas 3 cajas ya traía el valor correcto (piso +
  horas, calculado en backend con `clsPisoConHoras.Calcular`) — solo le
  faltaba el nombre. Se cambió su label:
  ```ts
  candidateSourceLabel(value: string): string {
    return value === 'sicetac' ? 'Piso SICE-TAC + tiempos logísticos' : value === 'plaza' ? 'Tarifario' : 'Histórico SAP';
  }
  ```
- Se corrigió de raíz el bug de fondo que motivó esto: el campo de flete
  se prellenaba con `floorTotal(linea.pisoSicetac, costeo.tiempos)`, que
  **ignoraba** las horas propias de cada línea (siempre usaba las de la
  solicitud completa). Ahora usa el valor ya calculado bien por el
  backend:
  ```ts
  valorFlete: [lineaPropuesta?.sicetac?.valor ?? null, [Validators.required, Validators.min(0.01)]],
  fuente: [lineaPropuesta?.sicetac ? 'sicetac' : 'manual', Validators.required],
  ```
- Se borraron `floorTotal()`, `horasTotalesDeLinea()` y el import
  `PisoSicetacDto` por quedar sin uso (`tiemposDeLinea()` se reintrodujo
  después, para 5.1).

#### 5.4. "Aparece fuente de propuesta y arriba el botón de usar propuesta, que abajo aparezca en verde o algo así, la propuesta, la fuente y el botón para que aparezca todo así" — ✅ listo

- Se quitó el valor + botón "Usar propuesta" del encabezado
  "Propuesta de la Biblioteca" (arriba).
- Se metieron los dos dentro de la caja "Fuente propuesta", en 3 columnas
  (precio grande, fuente grande, botón):
  ```html
  <div class="mesa-rate-box__content">
    <strong class="mesa-rate-box__precio">{{ ...precio... }}</strong>
    <span class="mesa-rate-box__fuente">{{ ...fuente... }}</span>
    <button ... (click)="usarPropuesta(entry)">Usar propuesta</button>
  </div>
  ```
  `.mesa-rate-box__precio`/`.mesa-rate-box__fuente` con el mismo tamaño
  (700 20px/26px) — ambos se ven grandes, no solo el precio.
- Color condicional: verde (`mesa-rate-box--success`) si hay
  propuesta/flete, rojo claro (`mesa-rate-box--empty`) si no.
- **Ajuste posterior importante:** la caja ahora prioriza el **flete real
  guardado** sobre la propuesta original — si ya hay flete (de cualquier
  fuente: Manual, SICE-TAC, Tarifario, Histórica), muestra ese valor/esa
  fuente real, no se queda pegada a lo que decía la Biblioteca al
  principio (antes podía decir "Sin propuesta" aunque ya hubiera un flete
  manual guardado).

#### 5.5. "Colocar clase viaje, línea negocio, valor mercancía por línea solicitud" — ⚠️ PARCIAL, falta "valor mercancía"

Se agregaron 2 de los 3 chips pedidos, junto a Tiempos logísticos:

```html
<span *ngIf="entry.linea.lineaNegocio"><b>Línea de negocio</b>{{ entry.linea.lineaNegocio }}</span>
<span *ngIf="entry.linea.claseViaje"><b>Clase de viaje</b>{{ entry.linea.claseViaje }}</span>
<span *ngIf="entry.linea.claseContenedor"><b>Clase de contenedor</b>{{ entry.linea.claseContenedor }}</span>
```
(se agregó también "Clase de contenedor", que no estaba en esta lista
pero sí se había pedido antes — solo se muestra si tiene valor, ya que
solo aplica cuando Clase de viaje = Contenedor).

Esto necesitó cambio de **backend** en NEXO (no en este repo): estos 3
campos no viajaban antes a Mesa de Costeo. `clsLineaCosteoDto`
(`SolicitudDtos.cs:69-75`) y `clsObtenerCosteoQuery.cs:50-53` se
ajustaron para poblarlos desde
`l.LineaNegocio`/`l.ClaseViaje`/`l.ClaseContenedor` (ya existían en la
entidad, solo no se exponían) — ver hallazgo #4 de esta misma sección
(sin probar todavía contra el backend desplegado).

**⚠️ Falta "Valor mercancía" por línea.** Este NUNCA se pidió al front
todavía — no está en el diff. Si Sara lo sigue queriendo, hay que
mandarlo como pedido nuevo (y revisar si `clsLineaCosteoDto` ya trae
`ValorMercancia` o si también hace falta agregarlo ahí, como pasó con los
otros 3).

#### 5.6. "Que cada línea inicie como línea 1, línea 2, etc." — ✅ listo

```html
<p class="cabina-eyebrow">Línea {{ i + 1 }}</p>
```
Agregado en el encabezado de cada línea del costeo, y el mismo índice `i`
se reusa en el "Ver resumen completo" (5.10) — el número coincide en los
dos lugares.

#### 5.7. "Colocar advertencia de que flete manual al agregarse no se puede editar" — ❌ NO SE HA HECHO

Esto **no está en el diff todavía** — se discutió como recomendación (en
vez de habilitar edición, que sí requeriría cambios de backend — ver
`2026-10-09_FUTURAS-PROPUESTAS.md` punto 1), pero nunca se mandó como pedido concreto
al front. Falta pedirlo: un texto de advertencia visible en el formulario
de `agregarFlete` (`mesa-flete-editor`), algo como "Verifique el valor,
no podrá editarlo después de guardar."

#### 5.8. "En lugar de colocar el botón de aplicar propuestas, una barra de progreso que diga faltan 2 de 10 por costear o algo así" — ⚠️ PARCIAL, el botón NO se quitó

Se agregó el indicador de avance (no es una barra visual, es texto con
color), pero el botón **"Aplicar propuestas (N)" sigue ahí, sin tocar** —
quedaron los dos al tiempo, no se reemplazó uno por el otro como pide el
"en lugar de" original.

Lo que sí se agregó, en su propia sección debajo del mensaje de éxito:
```html
<section class="mesa-avance" [ngClass]="lineasCosteadas(vm.lineas) === vm.lineas.length ? 'mesa-avance--completo' : 'mesa-avance--pendiente'">
  <strong>Costeo terminado</strong> <!-- o -->
  <strong>{{ lineasCosteadas(vm.lineas) }} de {{ vm.lineas.length }} línea(s) costeadas</strong>
</section>
```
Rojo claro mientras falte alguna, verde cuando estén todas. Usa
`lineasCosteadas()` (método nuevo en el `.ts`, cuenta fletes reales
guardados — ojo, no es lo mismo que `lineasSinFlete()`, que tiene un
atajo distinto para otro propósito, ver comentario en el código).

**⚠️ Falta decidir:** ¿de verdad se quita el botón "Aplicar propuestas", o
se dejan los dos (uno informa, el otro actúa)? Si se quita, alguien
tendría que poder aplicar propuestas de otra forma (¿un botón dentro del
mismo indicador de avance?) — no se asumió la respuesta, queda para que
Sara decida y se mande como pedido aparte si quiere el reemplazo real.

#### 5.9. "Símbolo de advertencia al lado del botón entregar al comercial, dentro de un círculo amarillito, que al pasar el mouse aparezca el texto de que se debe entregar para finalizar" — ✅ listo (confirmar legibilidad del tooltip)

```html
<span class="mesa-header-actions__entregar">
  <button ...>Entregar a Comercial</button>
  <mat-icon
    *ngIf="cotizacion && cotizacion.estado !== 'Entregada' && vm.lineas.length > 0 && lineasCosteadas(vm.lineas) === vm.lineas.length"
    class="mesa-header-actions__aviso"
    matTooltip="Pendiente por enviar al comercial. Envía para que le aparezca en su bandeja."
    matTooltipClass="cabina-tooltip"
    matTooltipPosition="right"
  >warning_amber</mat-icon>
</span>
```
```css
.mesa-header-actions__aviso { display: inline-flex; align-items: center; justify-content: center; width: 24px; height: 24px; border-radius: 50%; background: #fef3e2; color: #b45309; font-size: 16px; cursor: help; }
```
Círculo amarillo de 24px, mismo ícono (`warning_amber`, Material Icons —
ya se usaba en todo el archivo, no se inventó uno nuevo) y mismo
`matTooltip` que ya usa `bandeja-page.component.html` (mismo patrón, no
un tooltip custom). El badge que antes estaba en el banner verde se quitó
de ahí para no duplicar el aviso en dos lugares — ahora solo vive en este
ícono.

**Sin confirmar todavía:** se pidió además revisar la legibilidad del
texto del tooltip al pasar el mouse. Investigado: `.cabina-tooltip` no
define `color` ni `background` propios (eso sale del tema por defecto de
Angular Material, sin overrides en todo el proyecto) — así que el
problema, si es real, no se arregla tocando esa clase. Falta que Sara
confirme qué se ve mal exactamente: ¿contraste real, o el tooltip se
recorta/tapa con los botones de exportar PDF/Excel de al lado?

#### 5.10. "Botón de ver resumen completo y detalle por línea" — ✅ listo

Dentro de la card "Consolidado de tarifa", debajo del total:

```html
<button type="button" (click)="toggleResumenCompleto()">
  {{ mostrarResumenCompleto ? 'Ocultar resumen completo' : 'Ver resumen completo' }}
</button>
<ul *ngIf="mostrarResumenCompleto" class="mesa-resumen-completo">
  <li *ngFor="let entry of vm.lineas; let i = index">
    <strong>Línea {{ i + 1 }}:</strong> {{ ruta }}, {{ flete }}, {{ fuente }}
    <!-- o, si no tiene flete todavía: -->
    <strong>Línea {{ i + 1 }}:</strong> {{ ruta }} — sin flete todavía
  </li>
</ul>
```
Colapsado por defecto (`mostrarResumenCompleto = false`), cubre **todas**
las líneas (costeadas y pendientes), con la misma numeración de 5.6.

#### Resumen para priorizar

| # | Pedido | Estado |
|---|---|---|
| 5.1 | Horas por línea en Mesa de Costeo | ✅ Listo |
| 5.2 | Quitar Valor estimado | ✅ Listo |
| 5.3 | Consolidar Piso SICE-TAC + tiempos | ✅ Listo |
| 5.4 | Fuente propuesta + botón juntos, en verde | ✅ Listo |
| 5.5 | Clase viaje / línea negocio / valor mercancía | ⚠️ Falta "valor mercancía" (nunca se pidió) |
| 5.6 | Numerar "Línea 1, 2..." | ✅ Listo |
| 5.7 | Advertencia de flete manual no editable | ❌ No se ha pedido/hecho |
| 5.8 | Barra de progreso en vez del botón | ⚠️ Se agregó el indicador, el botón NO se quitó |
| 5.9 | Ícono de advertencia junto a Entregar | ✅ Listo (confirmar legibilidad del tooltip) |
| 5.10 | Ver resumen completo | ✅ Listo |

### 6. FRONTEND — "Iniciar costeo" y "Sin propuesta" aparecen en solicitudes ya Entregadas

**Confirmado en pantalla real (2026-10-09, un cliente):** Estado =
`Entregada`, pero la pantalla igual mostraba el botón "Iniciar costeo" y
"Fuente propuesta: Sin propuesta/Sin fuente", como si nunca se hubiera
costeado nada.

**Causa raíz:** `mesa-costeo-page.component.ts:150` pone
`this.cotizacion = null` cada vez que se abre la pantalla, y solo se
vuelve a llenar si el rol tiene el permiso `autoAplicarPropuestas` (hoy
**solo** el rol `Comercial` lo tiene — `nexo-access.models.ts:36-42`) o
si alguien hace clic en un botón (Iniciar costeo/Aplicar
propuestas/Agregar flete). Para cualquier otro rol (`CoordinadorFletes`
= Admin Fletes, `AdminTarifas`, `Admin`) que solo entra a **mirar** una
solicitud ya costeada/entregada, `this.cotizacion` nunca se llena — la
pantalla no sabe que ya existe una cotización real guardada.

**Por qué no se arregla solo dándole el permiso a otro rol:**
`autoAplicarPropuestas` no es "cargar los datos" — también **aplica
propuestas automáticamente en silencio** a cualquier línea sin flete (y
las marca `Revisada`, el mismo problema del hallazgo #2 de Bandeja).
Agregárselo a `CoordinadorFletes` arreglaría este síntoma pero activaría
ese efecto secundario no deseado para todos los Admin Fletes.

**Arreglo de fondo (chico, acotado — no toca tablas ni lógica de
negocio):**
- Backend: una consulta nueva, igual de simple que
  `clsObtenerCotizacionQuery` (que ya existe y busca por Id de
  cotización), pero que busque **por Id de solicitud** y devuelva "no
  hay nada" en vez de error si todavía no existe ninguna.
- Frontend: un método nuevo en `cotizaciones-api.service.ts` que la
  consuma, y que `mesa-costeo-page.component.ts` la llame siempre al
  abrir la pantalla (sin el `if (!this.access.puede('autoAplicarPropuestas'))`
  que hoy lo bloquea) — guardando el resultado en `this.cotizacion` si
  existe. El auto-aplicar de propuestas en silencio se queda igual,
  solo gateado por el permiso; lo que deja de estar gateado es nada más
  "mostrar lo que ya existe".

Criterios de aceptación:
- [ ] Un usuario sin `autoAplicarPropuestas` (ej. Admin Fletes) que abre
      una solicitud ya `Entregada` ve de una el flete/propuesta real
      guardado, sin tener que hacer clic en nada.
- [ ] Ese mismo usuario, en una solicitud que nunca se ha costeado, NO
      ve que se le cree una cotización en borrador solo por abrir la
      pantalla a mirar — la consulta nueva es de solo lectura.
- [ ] El comportamiento de auto-aplicar propuestas en silencio (solo
      para `Comercial`) no cambia en nada.
- [ ] Mismo caso, pero para una solicitud **auto-costeada**: la
      cotización ya existe desde que se capturó
      (`clsCapturarSolicitudCommand.cs`, función `AutoCostearAsync`, la
      crea en el momento de la captura, no al abrir Mesa de Costeo) —
      un usuario sin `autoAplicarPropuestas` que entra a verla tampoco
      debe ver "Iniciar costeo", por la misma causa raíz de arriba.

### Bug — "Entregar a Comercial" no exige que todas las líneas estén costeadas

Investigado a raíz de la discusión sobre cómo medir de forma confiable
el "tiempo de costeo del pliego" (ver KPIs del Tablero): se evaluó usar
`Cotizacion.EntregadaAt` como marca de "la cotización ya quedó lista",
y al verificarlo se encontró que **nada impide entregar con líneas del
pliego todavía sin costear**.

- Backend: `clsEntregarCotizacionCommandHandler`
  (`clsEntregarCotizacionCommand.cs:47-49`) solo exige "al menos 1
  línea ya costeada" — no compara contra el total del pliego.
- Frontend: `puedeEntregar()` (`mesa-costeo-page.component.ts:663-664`)
  tiene exactamente la misma validación incompleta — con 1 de 2 líneas
  costeadas, el botón "Entregar a Comercial" ya aparece habilitado.
  Confirmado con Sara contra una captura de pantalla real (pliego de
  un cliente, "0 de 2 líneas costeadas").
- Confirmado también que no hay ningún validador, máquina de estados
  (`clsMaquinaEstados.cs`) ni test que cubra "pliego completo" antes de
  entregar — es un hueco real, no una regla que exista en otro lado.

Queda documentado como **HU-B8** en `2026-10-09_HU-BACKEND.md` y **HU-11** en
`2026-10-09_HU-FRONTEND.md` (sección Mesa de Costeo) — pendiente de implementar,
los dos lados.

### Hallazgo — Un flete manual puede quedar por debajo del piso SICE-TAC sin ningún aviso

Surgió al retomar la evaluación del KPI "Fletes por debajo del piso
SICE-TAC". Sara propuso documentar que el sistema ya garantiza que
ningún valor (plaza, histórico o manual) baje del piso — se verificó
en el código y **eso no es correcto para el flujo manual**:

- La regla `MAYOR(plaza, piso)` (`clsReglaDeCosteo.Decidir`) solo
  aplica cuando el sistema decide automáticamente.
- `clsAgregarLineaCotizacionCommand.cs` (flujo manual, Administrador de
  Fletes) y su validador no comparan el valor ingresado contra el piso
  SICE-TAC — una persona puede guardar cualquier número.

Se corrigió la nota propuesta y se decidió, en su lugar, documentar un
aviso (no silencioso) cuando esto pase. Como el frontend **ya tiene**
el piso SICE-TAC cargado por línea (misma "Propuesta de la Biblioteca"
de HU-1 de Mesa de Costeo), se puede resolver sin tocar backend —
decisión explícita de Sara de no mover tanto el backend por ahora.

Queda como **HU-12** en `2026-10-09_HU-FRONTEND.md` (sección Mesa de Costeo) —
pendiente de implementar, con la decisión de aviso-vs-bloqueo todavía
abierta.

**Actualización (2026-10-12):** Sara definió la decisión — ni aviso
pasivo ni bloqueo, un **popup de confirmación** al hacer clic en
"Agregar flete" (reutilizando el mismo patrón de modal que ya usan
"Marcar como perdida"/"Cancelar pliego" en este componente). Se
prototipó en el sandbox con datos reales, confirmado compilando sin
errores — pendiente de que Sara lo pruebe en pantalla.

### Pedido — Permiso de "Cubicar" para todos los roles

Sara preguntó para qué sirve el botón "Cubicar"/"Recubicar" que
aparece en Mesa de Costeo junto a las posiciones de la mercancía:
hace 2 cosas a la vez (`cubicarLinea()`,
`mesa-costeo-page.component.ts:342-375`) — valida rápido si el
vehículo propuesto alcanza para la carga, y en paralelo manda a crear
el plan de cubicación 3D real en el módulo Cubicador.

Pidió darle este permiso (`cubicarLinea`) a todos los roles. Se
confirmó en `nexo-access.models.ts:35-89` que hoy solo lo tienen
AdminTarifas y Admin — Comercial, CoordinadorFletes, LiderProceso y
ServiciosLogisticos no. De esos 4, solo **CoordinadorFletes** vería el
botón de verdad hoy (es el único que también tiene acceso a Mesa de
Costeo); los otros 3 no llegan a esa pantalla, así que el permiso
quedaría sin efecto práctico inmediato para ellos.

Queda como **HU-13** en `2026-10-09_HU-FRONTEND.md` (sección Mesa de Costeo) —
**solo documentado, sin implementar**, por pedido explícito de Sara.

### Pedido — Desglosar "Piso SICE-TAC + tiempos logísticos"

Sara pidió que la caja "Piso SICE-TAC + tiempos logísticos" (la
primera de las 3 de "Propuesta de la Biblioteca") muestre el
desglose con un "+" (piso y tiempos logísticos por separado), en vez
de un solo número combinado — igual que ya se ve el desglose "Flete +
aprovisionamiento" más abajo en la misma pantalla, en el flete
guardado.

Se confirmó que el backend calcula internamente el piso base y el
valor de las horas por separado (`clsPisoConHoras.Calcular`,
`clsPropuestaTarifaAdapter.cs:371`) pero solo expone el total
combinado — el desglose se descarta después de calcularlo, mismo
patrón que otros huecos ya encontrados en esta revisión (se calcula,
no se guarda/expone). Requiere agregar 2 campos nuevos al DTO de
candidato de tarifa.

Queda como **HU-B9** en `2026-10-09_HU-BACKEND.md` y **HU-14** en
`2026-10-09_HU-FRONTEND.md` (ambos en sección Mesa de Costeo) — pendiente de
implementar, ninguno de los dos lados.

### Pedido — Editar un flete manual ya guardado, sin duplicar la línea

Sara pidió implementar directamente lo que ya estaba anotado en
`2026-10-09_FUTURAS-PROPUESTAS.md` punto 1: poder corregir el valor de un flete
MANUAL ya guardado, en vez de solo mostrar una advertencia antes de
guardar (que era la alternativa barata que se había dejado anotada).

**Backend implementado y compilado (0 errores):** nuevo
`clsEditarLineaCotizacionCommand.cs` que busca la
`clsCotizacionLinea` existente por `LineaSolicitudId` y la actualiza en
vez de agregar una fila nueva (evitando el riesgo de duplicado y total
inflado que ya se había documentado) — solo si la cotización sigue en
Borrador y el flete existente es de fuente Manual. Nuevo endpoint
`PUT /api/cotizaciones/{id}/lineas/{lineaSolicitudId}`.

**Frontend relayado al peer de sandbox:** botón "Editar" en la tarjeta
de flete guardado (solo para fuente manual), reutiliza el mismo
formulario y el mismo popup de "flete bajo el piso SICE-TAC" de HU-12
— pendiente de confirmar compilando.

Queda como **HU-B10** en `2026-10-09_HU-BACKEND.md` y **HU-15** en
`2026-10-09_HU-FRONTEND.md` (ambos en sección Mesa de Costeo). `2026-10-09_FUTURAS-PROPUESTAS.md`
punto 1 actualizado para reflejar que ya no es una propuesta futura,
está en implementación.

### Auditoría — Flujo real de decisión de tarifa vs. entendimiento de negocio (2026-10-12)

Sara describió el flujo de negocio esperado (línea de negocio/origen/
destino/etc. → 1. Reglas Solo-SICETAC → 2. Tarifas Plaza vigente sola →
3. "consulta completa" con los 3 candidatos para que el Administrador
de Tarifas elija) y pidió verificar si el backend/frontend lo
implementan tal cual. Se investigó a fondo (`clsReglaDeCosteo.cs`,
`clsPropuestaTarifaAdapter.cs`, `mesa-costeo-page.component.ts`) y
**el flujo real NO coincide**:

- **No hay 3 pasos secuenciales independientes.** Solo existen 2
  caminos: (a) Regla Solo-SICETAC (si no encuentra valor en
  `tblRutasSicetac`, corta con "requiere atención manual" SIN
  consultar plaza/histórico — no cae a comparación), y (b) una rama
  única que SIEMPRE trae los 3 candidatos y SIEMPRE decide
  automáticamente con la "regla del mayor" (`clsReglaDeCosteo.Decidir`/
  `DecidirConTarifario`) — no existe un paso separado "plaza vigente,
  usar sola sin comparar".
- **"Consulta completa" (mostrar los 3 para que el humano elija) no
  existe como modo distinto** — los 3 candidatos y la decisión
  automática siempre se calculan juntos; "Usar propuesta"/"Aplicar
  propuestas" solo aplican el ganador ya decidido.
- **Confirmado el riesgo que Sara sospechaba, aunque en otro punto**:
  el rol Comercial auto-aplica el ganador en silencio al cargar la
  pantalla (no al hacer clic en "Iniciar costeo") y ni siquiera puede
  ver los 3 candidatos (no tiene el permiso `verPropuestaCosteo`).
- **Hallazgo adicional, ya conocido por QA** (`tests/QA/hu-13.4-.../esperado-final-pliego.md`):
  en 4 de 12 líneas reales verificadas, había una tarifa de plaza
  vigente hasta 3.9x mayor que el piso SICE-TAC, y el sistema propuso
  igual el piso (el valor MENOR) — ni la propia regla del mayor se
  cumplió. Causa raíz no confirmada por QA, pendiente de revisión
  aparte (no es parte de esta sesión de documentación de HUs).

**Aclaración de Sara tras el hallazgo:** el criterio de negocio correcto
es más fino de lo que el primer reporte sugería — cuando decide el
motor solo (auto-costeo puro, o la regla Solo-SICETAC), está bien que
aplique automático sin preguntar. El problema es específicamente cuando
un humano (Administrador de Tarifas) está revisando en Mesa de
Costeo: ahí el sistema debe **sugerir**, no imponer — y hoy, aunque
Fredy nunca tiene nada auto-aplicado en silencio (eso es exclusivo de
Comercial), tampoco tiene una forma de un clic para elegir un candidato
distinto al ganador — solo puede aceptar el ganador o reescribir el
valor a mano desde cero.

**Arreglo documentado:** un botón "Usar este" en cada uno de los 3
candidatos (no solo en el ganador), visible ÚNICAMENTE cuando la línea
pasó por la comparación real (no por Solo-SICETAC) — necesita que el
backend exponga explícitamente si la propuesta vino de Solo-SICETAC
(`EsSoloSicetac`), porque hoy no se puede inferir con certeza mirando
si plaza/histórico están en `null` (pueden estarlo por Solo-SICETAC O
por simple falta de coincidencia en la comparación real).

Queda como **HU-B11** en `2026-10-09_HU-BACKEND.md` y **HU-16** en
`2026-10-09_HU-FRONTEND.md` (ambos en sección Mesa de Costeo) — pendiente de
implementar, ninguno de los dos lados.

**Pendiente de decisión de Sara, no resuelto en esta sesión:** si el
modelo de 3 pasos que ella describe es el objetivo al que el sistema
debería migrar (cambio de regla de negocio más grande, afecta
`clsReglaDeCosteo`), o si el modelo actual ("regla del mayor" + Solo-
SICETAC + sugerencia editable) ya es aceptable con el ajuste de
HU-B11/HU-16. Tampoco se dio seguimiento en esta sesión al hallazgo de
QA (plaza vigente ignorada en producción).

**Actualización — especificación final del flujo (2026-10-12, más
tarde):** el pendiente de arriba ya se resolvió. Sara confirmó que el
modelo de 4 pasos es el objetivo, y en la misma conversación se
encontraron 2 cosas más:

- **La plaza y el histórico propios de NEXO nunca se consultan.**
  `tbl_tarifas_plaza` (la que se gestiona en Biblioteca → Tarifas de
  plaza, issue #210 / S4-HU-10.1) y `tbl_tarifas_plaza_historico`
  (llenada por trigger) existen, pero el motor solo busca en SAP.
  Revisando git: el código de la tabla interna tiene un solo commit (su
  creación) y ninguno de los 10 commits posteriores del motor la
  conectó; no hay rastro en el repo de que se haya planteado "consultar
  las 2" (el texto del issue #210 vive en el tracker, no en el repo).
  Sara definió la regla: **buscar en la externa y, si no aparece,
  en la interna — para plaza y para histórico.**
- **Guardar el Histórico de la propuesta por el formulario puede
  fallar** (`HISTORICA_VENCIDA`), porque la validación de "1 semana"
  mira las Históricas sugeridas (fletes ganados), otra tabla.

Decisiones de Sara (con 4 preguntas puntuales donde el PDF, lo dicho
hoy y el código no coincidían): plaza vigente se usa sola **solo si es
mayor que el piso** (como el PDF); sin plaza en ninguna tabla →
consulta completa (igual que el PDF — ver corrección abajo); en auto-costeo la consulta
completa aplica el mayor y **queda autocosteada**; si la externa solo
tiene vencida y la interna vigente, gana la interna vigente.

**Todo quedó escrito como una sola especificación** en
`2026-10-09_HU-BACKEND.md`, sección nueva **"Motor de decisión de tarifa"**: flujo
de 4 pasos, tabla de 10 casos con ejemplos, hoy vs. debe quedar, casos
borde ya decididos, hallazgos fuera de alcance, y las HU:
- **HU-B14** — doble fuente para plaza (externa → interna).
- **HU-B15** — doble fuente para histórico (incluye mapear
  `tbl_tarifas_plaza_historico` en EF, hoy sin mapeo).
- **HU-B16** — nuevo orden de decisión + campo `ModoDecision`
  (reemplaza a HU-B11, que queda marcada "no implementar").
- **HU-B17** — poder guardar el Histórico de la propuesta (2 opciones,
  pendiente elegir).
- **HU-16** (frontend, reescrita) — etiquetas SAP/NEXO, aviso del modo,
  "Recomendado", y "Usar este" solo en consulta completa y en líneas
  sin flete. Queda un punto a confirmar por Sara: que el popup del piso
  (HU-12) salte también al elegir un candidato bajo el piso, no solo en
  fuente manual.

`docs/documentacion/MAPA-FUNCIONAL-NEXO.md` sección 6.1 corregida: decía
que el sistema "resuelve en este orden", y no es cierto — ahora dice que
es el flujo objetivo y apunta a la especificación.

**Renumeración en `2026-10-09_HU-BACKEND.md`** (para que los números vuelvan a
quedar en orden dentro del archivo; las referencias cruzadas de los 4
documentos ya se actualizaron):

| Número viejo | Número nuevo | Qué es |
|---|---|---|
| HU-B12 | **HU-B10** | Editar un flete manual ya guardado |
| HU-B13 | **HU-B11** | ~~`EsSoloSicetac`~~ (reemplazada por HU-B16) |
| HU-B10 | **HU-B12** | Filtro de fecha del Tablero |
| HU-B11 | **HU-B13** | Tiempo de costeo híbrido del Tablero |

**Corrección (al armar el documento del flujo con el diagrama):**
releyendo el diagrama de `NEXO FLUJO.pdf`, la rama "No" de "¿Existe
tarifa de plaza?" va a la **consulta completa**, no a "solo SICE-TAC"
como decía `MAPA-FUNCIONAL-NEXO.md` (y como se le planteó a Sara en la
pregunta). La decisión de Sara (sin plaza → consulta completa) coincide
con el PDF. Corregido en `MAPA-FUNCIONAL-NEXO.md` 6.1 y en
`2026-10-09_HU-BACKEND.md` (sección "Motor de decisión de tarifa", decisión 4).
Documento del flujo para adjuntar a la issue:
`docs/documentacion/FLUJO-DECISION-TARIFA.md`.

### Cierre de decisiones pendientes (2026-10-12)

Se repasaron todas las decisiones que seguían abiertas en las HU. Sara
decidió:

- **Nunca por debajo del piso SICE-TAC, para ninguna fuente**
  (tarifario, histórico o manual): se **bloquea** con un aviso, no se
  pide confirmación. Reemplaza el "Confirmar de todas formas" de HU-12.
  → **HU-17** (frontend) + **HU-B18** (validación en el servidor, para
  que no se pueda saltar). En HU-16, los candidatos bajo el piso no
  muestran "Usar este". El prototipo del sandbox de HU-12 queda
  desactualizado hasta implementar HU-17.
- **Desviación 1 de QA:** Sara pidió que Claude decidiera — se
  investiga. → **HU-B19** (solo investigación, con logs en las 4 rutas).
- **Quitar el botón "Aplicar propuestas".** → **HU-19**. Ojo anotado en
  la HU: sin ese botón, los pliegos (cuyas líneas quedan autocosteadas
  pero sin flete guardado) habría que llenarlos línea por línea —
  conviene hacerlo junto con la corrección de "los 2 caminos de
  auto-costeo".
- **"Valor mercancía" como chip.** → **HU-18** (frontend) + **HU-B20**
  (backend: el dato ya se guarda pero no viajaba al costeo).
- **Ciudades con nombre repetido en la carga masiva de Tarifas Plaza:**
  la vista previa muestra cuál se usó y de qué departamento, y deja
  cambiarlo. → **HU-9** (Carga masiva) + **HU-B21** (el importador
  acepta columnas opcionales de departamento).
- **Gráficos del Tablero (HU-5):** no se aplican — marcada descartada.

Siguen abiertos, a la espera de Sara: HU-B17 (opción A o B para guardar
el histórico de la propuesta), conectar el traductor de zonas al cruce
de la plaza de SAP, e igualar los 2 caminos de auto-costeo.

Resueltos después en la misma conversación:
- **Tooltip de HU-7:** "se ve muy pequeño y no se entiende". Causa
  encontrada: el estilo legible de `.cabina-tooltip` solo existe en
  `bandeja-page.component.css` (con `::ng-deep`), así que fuera de
  Bandeja el texto sale diminuto — afecta las 7 pantallas con íconos de
  información. → **HU-20** (mover el estilo a `src/styles.css`, y
  recomendación de mostrar "Pendiente por enviar" como texto visible).
- **Tipo de vehículo vs. rango de peso en la carga masiva:** no se hace.
- **HU-20 parte B** (texto "Pendiente por enviar" visible): aprobada.
- **HU-B17:** opción A (el servidor valida contra la propuesta).
- **Traductor de zonas:** se conecta → **HU-B22**.
- **"Aplicar propuestas" — cambio de decisión:** ya no se quita; se
  renombra "Aplicar todas las propuestas" y pide confirmación
  (**HU-19 reescrita**). Con eso también se resuelve llenar los pliegos
  de una vez.
- **Aclaración de la confusión de "Iniciar costeo":** Sara veía que al
  darle "Iniciar costeo" se aplicaban todas las propuestas de una vez.
  Verificado en el código: ni el botón (`iniciarCosteo()`,
  `mesa-costeo-page.component.ts:209-232`) ni el servidor
  (`clsCrearCotizacionCommand`) aplican nada — solo crean o recuperan
  la cotización. Lo que pasa es que el **auto-costeo al capturar ya
  dejó los fletes guardados**, y la pantalla no lo sabe hasta que se le
  da "Iniciar costeo" (bug ya documentado como HU-9 / HU-B7: la
  pantalla no consulta la cotización existente al abrir). Al hacer
  clic, recupera esa cotización y los fletes "aparecen" de una.
- **Con esa aclaración, Sara cambió la decisión 5:** en consulta
  completa el auto-costeo **ya no guarda precio** — la línea queda
  pendiente para el Administrador de Tarifas. Solo se autocostean solas
  Solo-SICETAC (con valor) y plaza vigente mayor que el piso.
  Consecuencias documentadas en HU-B16 (Paso 4): cambia el auto-costeo
  de captura y de pliego; el auto-aplicado silencioso del Comercial
  pasa a aplicar solo las automáticas (parámetro nuevo
  `soloAutomaticas` en `aplicar-propuestas`, HU-16 punto 7); la
  métrica "% líneas auto-costeadas" del Tablero va a bajar; y llegan
  más líneas pendientes a Mesa de Costeo (incluso las que solo tienen
  piso SICE-TAC, caso 7) — "Aplicar todas las propuestas" con
  confirmación (HU-19) es la forma de resolverlas de una vez.
  Actualizados también `FLUJO-DECISION-TARIFA.md` (diagrama, tabla,
  descripción) y `MAPA-FUNCIONAL-NEXO.md` 6.1.

Las HU de backend nuevas de otros módulos (HU-B20, HU-B21) quedaron al
final de `2026-10-09_HU-BACKEND.md` en una sección "Otros módulos — decisiones del
2026-10-12", con notas que apuntan a ellas desde Mesa de Costeo y
Tarifas Plaza, para no volver a desordenar la numeración.

## Nota enviada a Brayan (Cubicador) sobre #266/#267 (2026-10-09)

Verificado contra el código real de `cubicador/` (no contra planes): de
los 8 criterios del issue #266 (migrar motor de Postgres a SQL Server +
Alembic), solo 1 ya estaba satisfecho por casualidad de diseño (tipos de
Id ya neutrales, `String(36)`); los otros 7 no tienen ni un solo rastro
de trabajo. Contra #267 (modelo de datos de las 9/10 tablas) se
encontraron 3 conflictos de contenido reales entre lo ya sembrado y lo
que decide esa issue — 2 de ellos son decisiones firmes nuestras que no
se van a revertir (los 10 tipos excluidos no se siembran; `apilable`/
`fragil`/`rotacion_libre` se quedan `NOT NULL` con default en vez de
`NULL`), y 1 sigue pendiente de conciliar de verdad (`COMBUSTIBLE` →
`CARBURANTE` como una sola categoría, con papel/madera en `GENERAL`).

**Principio general para la nota:** de ahora en adelante, al diseñar
columnas nuevas, **evitar dejar campos en `NULL`** — si existe un valor
por defecto razonable, usarlo (`NOT NULL DEFAULT (...)`) en vez de un
tercer estado "no declarado/no sabemos". Mismo criterio que ya se aplicó
a `apilable`/`fragil`/`rotacion_libre` en nuestras tablas.
