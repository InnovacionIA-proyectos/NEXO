# HU — Frontend

_Última actualización: 2026-10-14_

Historias de usuario de **frontend**, pendientes de implementar por el
equipo de frontend, de **todos los módulos** que se van encontrando en
la revisión paso a paso de NEXO (no solo Mesa de Costeo — cada HU
indica su módulo en el título de sección).

**Importante:** el repo `administracion_embarqa_nexo` no es
responsabilidad de Sara. Cada HU de aquí se probó primero en un
sandbox descartable de ese repo (cambios **nunca commiteados**, se
borran al final) solo para confirmar que el criterio de aceptación es
exacto y realizable — no para dejar el código ya hecho. La
implementación real, de verdad, la hace el equipo de frontend sobre
estas HU. Detalle técnico de la investigación/prototipo (archivo:línea,
snippets) en `REVISION-NEXO-PASO-A-PASO.md`, por módulo. La parte de
backend que soporta algunas de estas está en `2026-10-09_HU-BACKEND.md`.

---

## Mesa de Costeo

> Archivos de este módulo:
> `src/app/cabina-fletes/pages/mesa-costeo/mesa-costeo-page.component.ts` / `.html` / `.css`

### HU-1 — Consolidar "Piso SICE-TAC" en una sola comparación de 3 fuentes

**Como** Administrador de Fletes, **quiero** ver el piso SICE-TAC ya con
los tiempos logísticos incluidos, dentro de la misma comparación de 3
fuentes, **para** no tener que sumar nada a mano ni ver un número
repetido en dos cajas distintas.

**Causa raíz (así estaba antes):** existía una caja `.mesa-rate-box`
separada "Piso SICE-TAC + tiempos logísticos" que leía
`entry.linea.pisoSicetac` con una función **propia del frontend**:
```ts
floorTotal(piso: PisoSicetacDto | null, tiempos: TiemposLogisticosDto): number | null {
  if (!piso) return null;
  const horas = tiempos.horasCargue + tiempos.horasDescargue;
  return piso.valorPiso + (piso.valorHora ? horas * piso.valorHora : 0);
}
```
El problema: `tiempos` siempre era `costeo.tiempos` (las horas
**generales de la solicitud**), nunca `entry.linea.horasCargue`/
`horasDescargue` (las horas propias de esa línea, cuando las tiene). El
backend sí las prioriza bien al calcular "Propuesta de la Biblioteca"
(`clsPropuestaTarifaAdapter.cs`, función `HorasEfectivasDeLinea`), así
que esta caja aparte podía mostrar un número distinto al candidato
SICE-TAC de la comparación de 3 — y como `floorTotal()` también
prellenaba `valorFlete` del formulario, el flete que se terminaba
guardando podía quedar mal.

**Implementación de referencia (validada en el sandbox):**
- Se eliminó la caja aparte y la función `floorTotal()` (y
  `horasTotalesDeLinea()`, y el import `PisoSicetacDto`, por quedar sin
  uso).
- El candidato SICE-TAC de las 3 cajas de "Propuesta de la Biblioteca"
  ya traía el valor correcto (piso + horas, calculado en backend con
  `clsPisoConHoras.Calcular`) — solo le faltaba el nombre:
  ```ts
  candidateSourceLabel(value: string): string {
    return value === 'sicetac' ? 'Piso SICE-TAC + tiempos logísticos' : value === 'plaza' ? 'Tarifario' : 'Histórico SAP';
  }
  ```
- El formulario de flete ahora prellena desde ese mismo candidato ya
  calculado, no desde `floorTotal()`:
  ```ts
  valorFlete: [lineaPropuesta?.sicetac?.valor ?? null, [Validators.required, Validators.min(0.01)]],
  fuente: [lineaPropuesta?.sicetac ? 'sicetac' : 'manual', Validators.required],
  ```
- La caja "Fuente propuesta" (antes el precio + botón "Usar propuesta"
  vivían sueltos arriba, en el encabezado "Propuesta de la Biblioteca")
  pasó a tener el precio, la fuente y el botón juntos, en 3 columnas:
  ```html
  <div class="mesa-rate-box__content">
    <strong class="mesa-rate-box__precio">{{ ...precio... }}</strong>
    <span class="mesa-rate-box__fuente">{{ ...fuente... }}</span>
    <button ... (click)="usarPropuesta(entry)">Usar propuesta</button>
  </div>
  ```
  con clase condicional `mesa-rate-box--success` (verde, hay
  propuesta/flete) o `mesa-rate-box--empty` (rojo claro, no hay
  ninguna) — y **prioriza el flete real ya guardado** sobre la
  propuesta original: si ya hay flete (de cualquier fuente: manual,
  SICE-TAC, tarifario, histórica), muestra ese valor/fuente real, no
  se queda pegada a lo que decía la Biblioteca al principio.

**Criterios de aceptación:**
- Dado que abro una línea con ruta definida, cuando reviso "Propuesta de
  la Biblioteca", entonces veo exactamente **3 cajas**: "Piso SICE-TAC +
  tiempos logísticos", "Tarifario", "Histórico SAP" — nunca una 4ª caja
  aparte de "Piso SICE-TAC" sola.
- El valor de "Piso SICE-TAC + tiempos logísticos" ya incluye las horas
  de cargue/descargue propias de la línea (o las de la solicitud si la
  línea no tiene las suyas) — coincide siempre con el valor que calcula
  el backend, nunca un cálculo aparte en el front.
- Debajo de las 3 cajas, una caja **"Fuente propuesta"** muestra el
  precio, la fuente y el botón **"Usar propuesta"** (solo si todavía no
  hay flete guardado) — los 3 juntos en la misma caja, no repartidos
  entre un encabezado de arriba y esta caja.
- La caja "Fuente propuesta" es **verde** si hay propuesta o flete
  guardado, **roja clara** si no hay ninguna de las dos.
- Si ya hay un flete guardado (de cualquier fuente: manual, SICE-TAC,
  tarifario, histórica), la caja muestra **ese** valor/fuente real, no
  el de la propuesta original de la Biblioteca.

---

### HU-2 — Ocultar "Valor estimado" de Mesa de Costeo

**Como** Administrador de Fletes, **quiero** que Mesa de Costeo no
muestre "Valor estimado", **para** no ver un dato que nunca se captura y
no aporta nada a la pantalla.

**Dónde está:** `mesa-costeo-page.component.html:65`, dentro del `<dl>`
de resumen de la solicitud (junto a Tipo/Estado/Prioridad/Fecha
límite):
```html
<!-- Quitar: -->
<div><dt>Valor estimado</dt><dd>{{ formatMoney(costeo.solicitud.valorEstimado) }}</dd></div>
```

**Criterios de aceptación:**
- El resumen de la solicitud (arriba de todo) **nunca** muestra "Valor
  estimado", en ningún caso.
- No requiere ningún cambio de backend — es solo ocultar el dato en el
  front.

**Nota aparte, NO parte de esta HU:** `clsScorePrioridadCalculator.cs:30`
(NEXO, backend) sigue usando `ValorEstimado` para el score de
prioridad, y nada lo captura en ningún formulario todavía. Eso es un
tema de negocio/backend independiente, sin HU escrita — no bloquea
esta.

---

### HU-3 — Tiempos logísticos por línea

**Como** Administrador de Fletes, **quiero** ver las horas de
cargue/descargue de cada línea individualmente, **para** saber si una
línea tiene horas distintas a las de la solicitud completa.

**Dónde va:** en el bloque `.mesa-data-chips` de cada línea (junto a
Tipología del vehículo/Carrocería/Peso/Tipología de carga):
```html
<span>
  <b>Tiempos logísticos</b>{{ tiemposDeLinea(entry.linea, costeo.tiempos).horasCargue }} h cargue + {{ tiemposDeLinea(entry.linea, costeo.tiempos).horasDescargue }} h descargue<ng-container *ngIf="entry.linea.horasCargue === null || entry.linea.horasDescargue === null"> (de la solicitud)</ng-container>
</span>
```
`tiemposDeLinea(linea, tiemposSolicitud)` (método nuevo en el `.ts`):
usa las horas propias de la línea (`entry.linea.horasCargue`/
`horasDescargue`) **solo si las dos** están resueltas (no `null`); si
falta cualquiera de las dos, devuelve completo `tiemposSolicitud`
(`costeo.tiempos`) — nunca mezcla una de cada lado. Mismo criterio que
ya usa el backend en `HorasEfectivasDeLinea`
(`clsPropuestaTarifaAdapter.cs`, NEXO) para no mostrar un número
distinto al que usa "Propuesta de la Biblioteca" (ver HU-1).

**Criterios de aceptación:**
- Cada línea muestra "Tiempos logísticos: X h cargue + Y h descargue",
  en el mismo bloque donde ya están Tipología del vehículo/Carrocería/
  Peso/Tipología de carga.
- Usa las horas propias de la línea solo si **las dos** (cargue y
  descargue) están resueltas; si falta cualquiera de las dos, usa las
  de la solicitud completa — nunca mezcla una de cada lado.
- Cuando hereda de la solicitud, el texto lo aclara con "(de la
  solicitud)".
- Es de **solo lectura** — no se edita desde Mesa de Costeo.

---

### HU-4 — Clasificación de la línea: Línea de negocio, Clase de viaje, Clase de contenedor

**Como** Administrador de Fletes, **quiero** ver la línea de negocio, la
clase de viaje y la clase de contenedor de cada línea, **para** tener el
contexto comercial completo sin ir a otra pantalla.

**Dónde va:** mismo bloque `.mesa-data-chips` de HU-3, 3 chips más:
```html
<span *ngIf="entry.linea.lineaNegocio"><b>Línea de negocio</b>{{ entry.linea.lineaNegocio }}</span>
<span *ngIf="entry.linea.claseViaje"><b>Clase de viaje</b>{{ entry.linea.claseViaje }}</span>
<span *ngIf="entry.linea.claseContenedor"><b>Clase de contenedor</b>{{ entry.linea.claseContenedor }}</span>
```

**Criterios de aceptación:**
- 3 chips nuevos, en el mismo bloque de HU-3: "Línea de negocio", "Clase
  de viaje", "Clase de contenedor".
- Cada uno se oculta si el dato es nulo (usar `*ngIf`, no mostrar vacío).
  "Clase de contenedor" en particular solo trae valor cuando la Clase
  de viaje es Contenedor.
- Depende de que el backend envíe estos 3 campos en el DTO de costeo —
  **ya resuelto del lado NEXO**, código real y compilado (ver
  `2026-10-09_HU-BACKEND.md`, nota de referencia bajo Mesa de Costeo), pendiente
  de que el backend desplegado se actualice para poder probarlo en
  pantalla.

---

### HU-5 — Numerar las líneas

**Como** Administrador de Fletes, **quiero** que cada línea diga "Línea
1", "Línea 2", etc., **para** poder referirme a una línea puntual sin
ambigüedad.

**Dónde va:** encabezado de cada línea del flujo de costeo:
```html
<p class="cabina-eyebrow">Línea {{ i + 1 }}</p>
```
(el índice `i` del `*ngFor` sobre las líneas — el mismo índice se
reusa en HU-8, para que el número coincida en los dos lugares).

**Criterios de aceptación:**
- Cada encabezado de línea en el flujo de costeo muestra "Línea N" (N =
  posición de la línea, empezando en 1).
- El mismo número se usa en el "Ver resumen completo" (HU-8) — nunca un
  número distinto para la misma línea en dos lugares de la pantalla.

---

### HU-6 — Indicador de avance del costeo

**Como** Administrador de Fletes, **quiero** ver cuántas líneas ya están
costeadas de un vistazo, **para** saber si falta trabajo sin tener que
contarlas a mano.

**Dónde va:** sección nueva, debajo del mensaje de éxito ("Flete
guardado correctamente"/similar):
```html
<section class="mesa-avance" [ngClass]="lineasCosteadas(vm.lineas) === vm.lineas.length ? 'mesa-avance--completo' : 'mesa-avance--pendiente'">
  <strong>Costeo terminado</strong> <!-- o -->
  <strong>{{ lineasCosteadas(vm.lineas) }} de {{ vm.lineas.length }} línea(s) costeadas</strong>
</section>
```
`lineasCosteadas(lineas)` (método nuevo en el `.ts`): cuenta líneas con
flete real guardado. **Ojo:** no reusar `lineasSinFlete()` si ya existe
en el componente — tiene un atajo/propósito distinto, no sirve para
este conteo.

**Criterios de aceptación:**
- Debajo del mensaje de "Costeo iniciado", un indicador muestra "X de Y
  línea(s) costeadas", en **rojo claro** mientras falte alguna.
- Cuando todas las líneas ya tienen flete, el texto cambia a **"Costeo
  terminado"**, en **verde** — sin ningún badge/ícono adentro de este
  banner (el aviso de "pendiente por enviar" vive aparte, ver HU-7).
- El indicador solo aparece si ya existe una cotización y la solicitud
  tiene al menos 1 línea.

**Decidido (Sara, 2026-10-12):** conviven los dos. El botón **se
queda**, renombrado "Aplicar todas las propuestas (N)" y con una
confirmación antes de aplicar — ver **HU-19**. El indicador de esta HU
solo informa el avance.

---

### HU-7 — Aviso de "pendiente por enviar al comercial"

**Como** Administrador de Fletes, **quiero** una señal clara cuando ya
costeé todo pero no he entregado la cotización, **para** no olvidarme de
mandarla al comercial.

**Dónde va:** junto al botón "Entregar a Comercial":
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
.mesa-header-actions__aviso { display: inline-flex; align-items: center; justify-content: center; width: 28px; height: 28px; border-radius: 50%; background: #fef3e2; color: #c62828; font-size: 16px; cursor: help; }
```
Ícono `warning_amber` (Material Icons, ya se usa en todo el archivo —
no inventar uno nuevo) y `matTooltip` con el mismo patrón que ya usa
`bandeja-page.component.html` (no un tooltip custom). Círculo de fondo
**amarillo claro** (`#fef3e2`), ícono/texto en **rojo** (`#c62828`) —
dos tonos distintos a propósito, no todo del mismo color.

**Criterios de aceptación:**
- Junto al botón "Entregar a Comercial", aparece un ícono de advertencia
  (círculo de fondo amarillo claro, 28px, ícono y color en rojo) cuando:
  la cotización existe, no está en estado Entregada, y todas las líneas
  ya tienen flete guardado.
- Al pasar el mouse por encima, un tooltip dice **"Pendiente por enviar
  al comercial. Envía para que le aparezca en su bandeja."**
- Una vez la cotización queda Entregada, el ícono desaparece.

---

### HU-8 — Ver resumen completo

**Como** Administrador de Fletes, **quiero** un resumen de todas las
líneas con su flete y fuente en un solo lugar, **para** revisar todo
antes de entregar sin tener que desplazarme línea por línea.

**Dónde va:** card propia, hermana de "Consolidado de tarifa" (no
anidada adentro):
```html
<section class="cabina-card mesa-resumen-completo-card">
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
</section>
```
`mostrarResumenCompleto` arranca en `false` (colapsado por defecto).

**Criterios de aceptación:**
- Vive en su **propia card**, hermana de "Consolidado de tarifa" — **no**
  anidada adentro de esa card.
- Un botón **"Ver resumen completo"** / **"Ocultar resumen completo"**
  alterna una lista **colapsada por defecto**.
- La lista muestra, por cada línea: **"Línea N: origen → destino, flete,
  fuente"** — o **"Línea N: origen → destino — sin flete todavía"** si
  esa línea no tiene flete.
- Cubre **todas** las líneas (costeadas y pendientes), no solo las que
  ya tienen flete.
- Usa la misma numeración de HU-5 (mismo índice `i`).

---

### HU-9 (parte frontend) — Siempre consultar si ya existe una cotización al abrir

**Como** Administrador de Fletes, **quiero** que al abrir una solicitud
ya entregada o auto-costeada vea de una vez su flete real, **para** no
ver "Iniciar costeo"/"Sin propuesta" en algo que ya se resolvió hace
rato.

**Causa raíz:** `mesa-costeo-page.component.ts:150` pone
`this.cotizacion = null` cada vez que se abre la pantalla, y solo se
vuelve a llenar si el rol tiene el permiso `autoAplicarPropuestas`
(hoy **solo** el rol `Comercial` lo tiene —
`nexo-access.models.ts:36-42`) o si alguien hace clic en un botón
(Iniciar costeo/Aplicar propuestas/Agregar flete). Para cualquier otro
rol (`CoordinadorFletes` = Admin Fletes, `AdminTarifas`, `Admin`) que
solo entra a **mirar** una solicitud ya costeada/entregada,
`this.cotizacion` nunca se llena.

**Por qué no basta con darle el permiso a otro rol:**
`autoAplicarPropuestas` no es "cargar los datos" — también **aplica
propuestas automáticamente en silencio** a cualquier línea sin flete (y
las marca `Revisada`). Agregárselo a `CoordinadorFletes` arreglaría
este síntoma pero activaría ese efecto secundario no deseado para
todos los Admin Fletes — **no es la solución**.

**Depende de:** la consulta nueva de backend (`2026-10-09_HU-BACKEND.md`, HU-B7) —
un método nuevo en `cotizaciones-api.service.ts` que la consuma, y que
`mesa-costeo-page.component.ts` la llame **siempre** al abrir la
pantalla, sin el `if (!this.access.puede('autoAplicarPropuestas'))` que
hoy lo bloquea — guardando el resultado en `this.cotizacion` si existe.
El auto-aplicar de propuestas en silencio se queda igual, solo gateado
por el permiso; lo que deja de estar gateado es nada más "mostrar lo
que ya existe".

**Criterios de aceptación:**
- Al abrir Mesa de Costeo, se llama **siempre** la consulta de
  solo-lectura (sin depender del permiso `autoAplicarPropuestas`) para
  poblar la cotización en pantalla si ya existe.
- Una solicitud en estado **Entregada** nunca muestra "Iniciar costeo" ni
  "Sin propuesta", para ningún rol.
- Una solicitud **auto-costeada** (la cotización se crea desde el
  momento de la captura, no al abrir Mesa de Costeo —
  `clsCapturarSolicitudCommand.cs`, función `AutoCostearAsync`, NEXO)
  tampoco muestra "Iniciar costeo", para ningún rol.
- Una solicitud que de verdad **nunca** se ha costeado no se ve afectada:
  abrir la pantalla a mirar no crea ninguna cotización en borrador por sí
  sola (la consulta nueva es de solo lectura).
- El comportamiento de auto-aplicar propuestas en silencio (solo rol
  Comercial) no cambia en nada.

---

### Pendiente de decidir (Mesa de Costeo) — todavía no tienen HU escrita

Estas cosas se mencionaron en algún momento pero **no se han pedido
formalmente ni implementado**. Hay que decidir el alcance antes de
convertirlas en HU:

1. ~~"Valor mercancía" por línea~~ — **decidido (2026-10-12): sí, como
   chip — ver HU-18** (y HU-B20 en backend: el dato no viajaba en
   `clsLineaCosteoDto`).
2. ~~Advertencia de "flete manual no se puede editar"~~ — **resuelto
   distinto, ver HU-15 más abajo**: en vez de la advertencia, Sara pidió
   ir directo por habilitar la edición real de un flete manual. Ya
   implementado en backend (HU-B10) y relayado al frontend.
3. ~~¿Se quita el botón "Aplicar propuestas"?~~ — **decidido
   (2026-10-12): no se quita; se renombra "Aplicar todas las
   propuestas" y pide confirmación — ver HU-19.**
4. ~~Legibilidad del tooltip de HU-7~~ — **Sara confirmó (2026-10-12):
   "se ve muy pequeño y no se entiende". Ver HU-20** (causa encontrada:
   el estilo legible solo existe en la pantalla de Bandeja).

---

### HU-10 — Bug: se puede marcar Ganada/Perdida sin pasar por Entregar ni auto-costeo

**Como** usuario de Mesa de Costeo, **quiero** que los botones "Marcar
como ganada"/"Marcar como perdida" respeten la misma regla que ya se
aplica correctamente en otra pantalla, **para** no poder registrar un
resultado sobre una cotización que ni siquiera se ha entregado.

**Archivos:**
- `cotizacion-resumen-page.component.ts`, líneas 67-74
  (`puedeRegistrarResultado`) — aquí la regla **sí** está bien aplicada.
- `mesa-costeo-page.component.html`, líneas 35-38 — aquí **no**.

**Causa raíz:** la regla correcta ya existe y funciona en
`cotizacion-resumen-page.component.ts`: solo se puede registrar
Ganada/Perdida si la cotización está **Entregada**, o si está en
**Borrador** y `esAutoCosteo = true` (comentario propio del código:
*"Mismo criterio del backend: una cotización auto-costeada se marca
sin pasar por Entregar"*). Pero Mesa de Costeo tiene sus propios
botones "Marcar como ganada"/"Marcar como perdida" que **no aplican
esa misma validación** — solo revisan el permiso del usuario
(`access.puede('marcarGanadaPerdida')`) y, para Ganada, que haya al
menos una línea. No revisan ni el estado de la cotización ni
`esAutoCosteo`.

**Resultado:** desde Mesa de Costeo se puede marcar una cotización
como Perdida (o Ganada) sin que haya sido Entregada ni auto-costeada
— saltándose la regla que sí está bien implementada en
Cotización-resumen.

**Criterios de aceptación:**
- Los botones "Marcar como ganada"/"Marcar como perdida" en Mesa de
  Costeo aplican el mismo criterio que `puedeRegistrarResultado()`:
  habilitados solo si la cotización está Entregada, o está en
  Borrador con `esAutoCosteo = true`.
- Una cotización en Borrador sin auto-costeo no se puede marcar
  Ganada/Perdida desde ninguna de las dos pantallas.

---

### HU-11 — Bug: se puede "Entregar a Comercial" con líneas del pliego sin costear

**Como** usuario de Mesa de Costeo, **quiero** que el botón "Entregar a
Comercial" solo se habilite cuando **todas** las líneas del pliego ya
tienen flete, **para** no poder entregarle al comercial una cotización
incompleta.

**Archivo:** `mesa-costeo-page.component.ts:663-664`
```ts
puedeEntregar(cotizacion: CotizacionDto | null): boolean {
  return !!cotizacion && cotizacion.lineas.length > 0 && cotizacion.estado !== 'Entregada';
}
```

**Causa raíz:** la validación solo exige "que haya al menos 1 línea ya
costeada" — no compara contra el total de líneas del pliego. El propio
componente ya calcula cuántas líneas están costeadas
(`lineasCosteadas(vm.lineas)`, usado hoy solo para mostrar un ícono de
advertencia cuando **ya** están todas listas, línea 43 del HTML) — ese
mismo cálculo no se usa para deshabilitar el botón cuando **faltan**
líneas.

**Resultado:** con 1 de 2 líneas costeadas, el botón ya aparece
habilitado — se puede entregar al comercial una cotización con líneas
del pliego todavía en `PorCostear`. Confirmado también en backend (ver
`2026-10-09_HU-BACKEND.md`, HU-B8): la API no bloquea esto tampoco.

**Criterios de aceptación:**
- `puedeEntregar()` agrega la condición de que
  `lineasCosteadas(vm.lineas) === vm.lineas.length` (reutilizando el
  cálculo que ya existe).
- Con el pliego incompleto, el botón "Entregar a Comercial" aparece
  deshabilitado — el ícono de advertencia ⚠️ sigue siendo el aviso para
  el caso contrario (todo listo, falta el clic).

---

### HU-12 — Avisar cuando un flete manual queda por debajo del piso SICE-TAC

> **⚠️ Reemplazada por HU-17 (decisión de Sara, 2026-10-12).** Esta HU
> dejaba "Confirmar de todas formas" y solo aplicaba a fuente manual.
> La regla quedó más estricta: **nunca** se puede guardar un valor por
> debajo del piso, de ninguna fuente — se bloquea con un aviso. El
> prototipo del sandbox (popup con "Confirmar de todas formas") hay que
> ajustarlo según HU-17 cuando se implemente. Se conserva esta HU como
> historial.

**Como** Administrador de Fletes, **quiero** que el sistema me avise si
el flete que estoy ingresando a mano queda por debajo del piso
SICE-TAC, **para** no dejar pasar por descuido una línea que viola la
regla de negocio (RN-01/RN-02).

**Causa raíz confirmada en el código:** la regla `MAYOR(plaza, piso)`
(`clsReglaDeCosteo.Decidir`) **solo se aplica cuando el sistema decide
automáticamente**. En el flujo manual
(`clsAgregarLineaCotizacionCommand.cs` + su validador,
`clsAgregarLineaCotizacionValidador.cs`) **no hay ninguna validación**
que compare `ValorFlete` contra el piso SICE-TAC — una persona puede
guardar cualquier número.

**Por qué se propone en frontend y no en backend:** el dato que hace
falta para comparar **ya está disponible en el frontend**, sin tocar el
backend — la "Propuesta de la Biblioteca" que ya se carga por línea
(`mesa-costeo-page.component.ts:906-910`, `lineaPropuesta?.sicetac?.valor`)
trae el piso SICE-TAC + tiempos logísticos ya calculado. No hace falta
ningún endpoint nuevo ni cambio de backend para esta validación.

**Dónde engancharla:** `agregarFlete(entry)`
(`mesa-costeo-page.component.ts:233-275`), antes de guardar: si
`value.fuente === 'manual'` y `entry.propuesta?.sicetac?.valor` existe,
comparar `Number(value.valorFlete)` contra ese piso.

**Decisión final (Sara, 2026-10-12):** ni aviso pasivo ni bloqueo duro
— un **popup de confirmación**. Al hacer clic en "Agregar flete" con un
valor manual por debajo del piso, aparece un modal preguntando si
quiere confirmar de todas formas; si confirma, se guarda igual (no es
un bloqueo real, es fricción + visibilidad).

**Implementación de referencia — mismo patrón de modal que ya usan
"Marcar como perdida"/"Cancelar pliego"** en este mismo componente
(`.mesa-modal-overlay`/`.mesa-modal`, `role="dialog"`, `cdkTrapFocus`,
`.mesa-modal__footer`; ver `mesa-costeo-page.component.html:448-465`
para el ejemplo de "Cancelar pliego").

`mesa-costeo-page.component.ts` — nuevo estado + `agregarFlete()` se
parte en 2 (detecta el caso y abre el modal, o guarda directo):
```ts
fleteBajoPisoAbierto = false;
fleteBajoPisoPendiente: MesaLineaEditorVm | null = null;
fleteBajoPisoMensaje = '';

agregarFlete(entry: MesaLineaEditorVm): void {
  if (!this.cotizacion || this.lineSavingId) return;
  if (entry.form.invalid) {
    entry.form.markAllAsTouched();
    return;
  }
  const value = entry.form.getRawValue() as { valorFlete: number | string; fuente: FuenteFlete; valorRetornoContenedor: number | string | null };
  const piso = entry.propuesta?.sicetac?.valor;
  if (value.fuente === 'manual' && piso != null && Number(value.valorFlete) < piso) {
    this.fleteBajoPisoPendiente = entry;
    this.fleteBajoPisoMensaje = `Este flete queda por debajo del piso SICE-TAC de ${this.moneda(piso)}. ¿Deseas confirmarlo de todas formas?`;
    this.fleteBajoPisoAbierto = true;
    return;
  }
  this.guardarFlete(entry);
}

cerrarFleteBajoPiso(): void {
  this.fleteBajoPisoAbierto = false;
  this.fleteBajoPisoPendiente = null;
}

confirmarFleteBajoPiso(): void {
  const entry = this.fleteBajoPisoPendiente;
  this.cerrarFleteBajoPiso();
  if (entry) this.guardarFlete(entry);
}

// cuerpo actual de agregarFlete (desde "const request..." hasta el final) se mueve tal cual a:
private guardarFlete(entry: MesaLineaEditorVm): void { /* ... */ }
```
`mesa-costeo-page.component.html` — nuevo modal, mismo markup que
"Cancelar pliego" (líneas 448-465), adaptado:
```html
<div *ngIf="fleteBajoPisoAbierto" class="mesa-modal-overlay" (click)="cerrarFleteBajoPiso()">
  <div class="cabina-card mesa-modal" role="dialog" aria-modal="true" cdkTrapFocus cdkTrapFocusAutoCapture="true" (keydown.escape)="cerrarFleteBajoPiso()" (click)="$event.stopPropagation()">
    <h2>Flete por debajo del piso SICE-TAC</h2>
    <p class="mesa-modal__hint">{{ fleteBajoPisoMensaje }}</p>
    <div class="mesa-modal__footer">
      <button type="button" class="cabina-button cabina-button--ghost" (click)="cerrarFleteBajoPiso()">Cancelar</button>
      <button type="button" class="cabina-button cabina-button--danger" (click)="confirmarFleteBajoPiso()">Confirmar de todas formas</button>
    </div>
  </div>
</div>
```

**✅ Prototipado en el sandbox (2026-10-12), con datos reales:**
confirmado por el peer compilando sin errores, exactamente como se
describió arriba. **Pendiente de que Sara lo pruebe en pantalla.**

**Importante — esto NO reemplaza un control real:** es una validación
de pantalla. Alguien podría seguir guardando un valor por debajo del
piso llamando a la API directo, sin pasar por el frontend. Si más
adelante se quiere un control que no se pueda saltar, eso sí requeriría
tocar backend (agregar la validación en
`clsAgregarLineaCotizacionCommand`) — se deja como alcance futuro,
fuera de esta HU.

**Criterios de aceptación:**
- Al hacer clic en "Agregar flete" con `fuente = manual` y un valor
  menor al piso SICE-TAC de esa línea, aparece el popup de
  confirmación antes de guardar nada.
- "Cancelar" cierra el popup sin guardar — la persona puede corregir el
  valor. "Confirmar de todas formas" guarda el flete igual que hoy.
- Si no hay candidato SICE-TAC para la línea (no hay ruta/match), se
  guarda directo, sin popup — no hay nada contra qué comparar.
- Fletes de fuente `sicetac`, `tarifario` o `historica` nunca muestran
  este popup (ya pasaron por la regla automática o por su propia
  validación de vigencia).

---

### HU-13 — Dar permiso de "Cubicar" a todos los roles

**Como** usuario de cualquier rol, **quiero** poder usar el botón
"Cubicar"/"Recubicar" de Mesa de Costeo, **para** verificar el vehículo
y generar el plan de cubicación 3D sin depender de mi rol.

**Causa raíz:** el botón vive en Mesa de Costeo
(`mesa-costeo-page.component.html:165`:
`*ngIf="entry.linea.posiciones.length > 0 && access.puede('cubicarLinea')"`)
y depende de la capacidad `cubicarLinea`, definida por rol en
`nexo-access.models.ts:35-89` (`NEXO_MATRIZ_ACCESO`). Hoy solo la
tienen **AdminTarifas** (línea 53) y **Admin** (línea 84) — **Comercial,
CoordinadorFletes, LiderProceso y ServiciosLogisticos no la tienen**.

**Arreglo:** agregar `'cubicarLinea'` al arreglo de capacidades de los
4 roles que no la tienen, en `NEXO_MATRIZ_ACCESO`
(`nexo-access.models.ts`).

**Nota de alcance:** para Comercial, LiderProceso y ServiciosLogisticos
esto no tiene efecto visible hoy porque ninguno de los 3 tiene acceso a
Mesa de Costeo (`verCotizacionIntegral`/`agregarFlete`) — el botón vive
dentro de esa pantalla, así que sin acceso a la pantalla, el permiso
queda sin uso práctico por ahora. Para CoordinadorFletes sí tiene
efecto inmediato, porque ese rol **sí** tiene `verCotizacionIntegral` y
`agregarFlete` (`nexo-access.models.ts:58-68`) — sería el único que
empieza a ver el botón de verdad con este cambio. Se documenta
igual para los 4 porque Sara pidió "a todos", dejando constancia de
cuáles tienen efecto inmediato y cuáles no.

**Criterios de aceptación:**
- `NEXO_MATRIZ_ACCESO` incluye `'cubicarLinea'` en Comercial,
  CoordinadorFletes, LiderProceso y ServiciosLogisticos (además de
  AdminTarifas/Admin, que ya la tienen).
- Actualizar `nexo-access.service.spec.ts:40` (`'Comercial: ...'`,
  hoy espera `access.puede('cubicarLinea')` en `false`) para que pase
  a `true` — es el único test existente que quedaría desactualizado
  con este cambio; el de AdminTarifas (línea 56) ya espera `true`, no
  se toca.

---

### HU-14 — Desglosar "Piso SICE-TAC + tiempos logísticos" en vez de un solo número

**Como** usuario de Mesa de Costeo, **quiero** ver el candidato SICE-TAC
desglosado en "Piso + Tiempos logísticos = Total", **para** entender de
dónde sale el número, igual que ya se ve el desglose "Flete +
aprovisionamiento" en el flete guardado más abajo en la misma pantalla.

**Cómo está hoy:** la primera de las 3 cajas de "Propuesta de la
Biblioteca" (`mesa-costeo-page.component.html:223-228`, título "Piso
SICE-TAC + tiempos logísticos" via `candidateSourceLabel()`,
`mesa-costeo-page.component.ts:507-509`) muestra un solo número
combinado — `candidateText()` (líneas 511-513) solo formatea
`candidate.valor`, sin desglose. Hoy el backend tampoco expone los 2
componentes por separado (ver **HU-B9** en `2026-10-09_HU-BACKEND.md`, que agrega
`ValorPisoBase`/`ValorTiemposLogisticos` al DTO).

**Depende de HU-B9** — sin esos 2 campos nuevos del backend, no hay
nada que desglosar en el frontend.

**¿Por qué no se puede armar el desglose solo en frontend, si la pestaña
"Tarifas SICE-TAC" de Biblioteca ya muestra `valor`/`valorHora`/
`horasPactadas`?** Se evaluó y se descartó — esa tabla
(`buscarMatrizSicetac()`/`MatrizSicetacResultDto`,
`biblioteca-page.component.ts:381-410`) es un **catálogo crudo**: lista
filas de SICE-TAC tal como están en la base, sin saber nada de ninguna
línea de ninguna cotización en particular, y sin aplicar el cálculo
real. El valor que necesita Mesa de Costeo usa las **horas reales
capturadas de esa línea específica** (que pueden diferir de las "horas
pactadas" por defecto que muestra Biblioteca) — ese cálculo
(`clsPisoConHoras.Calcular`) solo existe resuelto dentro del backend,
en el mismo paso donde arma el candidato SICE-TAC de esa línea.
Reconstruirlo en frontend con las piezas sueltas de Biblioteca
significaría reimplementar esa lógica de match + cálculo ahí —
exactamente el mismo riesgo de usar las horas equivocadas que **HU-1**
ya corrigió una vez (la función `floorTotal()` que se eliminó). Por
eso el desglose debe salir ya resuelto del backend (HU-B9), no
reconstruirse en el frontend con datos de otra pantalla.

**Implementación de referencia:** nuevo método, usado SOLO para el
candidato SICE-TAC (el primero de los 3, `i === 0` en el `*ngFor` de la
línea 224) — Plaza e Histórico SAP siguen mostrando un solo número con
`candidateText()`, tal cual hoy, porque no tienen este desglose:
```ts
candidateTextConDesglose(candidate: CandidatoTarifaDto | null): string {
  if (!candidate) return 'Sin coincidencias';
  if (candidate.valorPisoBase != null && candidate.valorTiemposLogisticos != null) {
    return `${this.formatMoney(candidate.valorPisoBase)} + ${this.formatMoney(candidate.valorTiemposLogisticos)} = ${this.formatMoney(candidate.valor)}`;
  }
  return this.formatMoney(candidate.valor); // fallback: sin los campos nuevos, igual que hoy
}
```
```html
<!-- línea 226, reemplaza candidateText(candidate) SOLO para i === 0 -->
<strong>{{ i === 0 ? candidateTextConDesglose(candidate) : candidateText(candidate) }}</strong>
```
`CandidatoTarifaDto` (modelo frontend) agrega
`valorPisoBase?: number | null` y `valorTiemposLogisticos?: number | null`,
igual que los nuevos campos del DTO de backend.

**Criterios de aceptación:**
- La caja "Piso SICE-TAC + tiempos logísticos" muestra
  "$Piso + $Tiempos = $Total" cuando el backend informa el desglose.
- Si el backend no informa el desglose (campos `null`, por ejemplo
  mientras no se despliegue HU-B9), se ve igual que hoy — un solo
  número, sin romper nada.
- Plaza e Histórico SAP no cambian — siguen mostrando un solo número.

---

### HU-15 — Editar un flete manual ya guardado (sin duplicar la línea)

**Como** Administrador de Fletes, **quiero** un botón "Editar" en el
flete manual ya guardado, **para** corregir el valor sin tener que
borrar y volver a capturar.

**Depende de HU-B10** (`2026-10-09_HU-BACKEND.md`, sección Mesa de Costeo) — ya
implementado y compilado, nuevo endpoint
`PUT /api/cotizaciones/{id}/lineas/{lineaSolicitudId}`.

**⚠️ No se puede probar de punta a punta todavía:** el sandbox de
Sara apunta al backend ya desplegado, no al código local — el endpoint
nuevo solo existe en el repo, no en el servidor real. El botón
"Editar" va a fallar en pantalla (probable 404) **hasta que se
despliegue HU-B10**. El frontend ya está listo y compilando, solo
falta ese despliegue para poder probarlo de verdad.

**Resuelve lo que estaba en "Pendiente de decidir" más arriba** (punto
2: "advertencia de flete manual no se puede editar") — se decidió ir
directo por la edición real en vez de solo la advertencia.

**Alcance:** solo fletes de **fuente Manual** — nunca para SICE-TAC,
tarifario ni histórico (esos ya pasaron por su propia regla/validación
de vigencia, no tiene sentido editarlos a mano encima). Solo mientras
la cotización siga en **Borrador**, igual que agregar un flete nuevo.

**✅ Implementado en el sandbox (2026-10-12), confirmado compilando:**
- Nuevo estado: `editandoFlete: Record<string, boolean>` (por línea) y
  `fleteBajoPisoEditando` (para que el popup de HU-12 sepa si viene de
  "agregar" o de "editar").
- La tarjeta de "Flete guardado"/"Flete actual" (`mesa-flete-success`/
  `fleteActualCard`, `mesa-costeo-page.component.html:286-301`) gana un
  botón **"Editar"**, visible solo si `flete.fuente === 'manual'`,
  cotización no entregada, y permiso `agregarFlete` — al hacer clic,
  pre-llena el formulario con el valor actual y lo vuelve a mostrar
  (reutilizando el mismo `editorFlete` de siempre).
- El formulario, al guardar en modo edición, llama al endpoint `PUT`
  nuevo (`editarFlete()` → `guardarEdicionFlete()`) en vez de crear una
  línea nueva — **y pasa por el MISMO popup de "flete bajo el piso
  SICE-TAC" de HU-12** si corresponde, para no abrir un atajo que se
  salte esa validación.
- El `<select>` de "Fuente" se deshabilita mientras se edita (el
  backend no acepta cambiar la fuente en una edición, se queda en
  Manual).
- Botón "Cancelar" (solo visible editando) vuelve a la tarjeta de solo
  lectura sin guardar nada.

**Criterios de aceptación:**
- El botón "Editar" solo aparece en fletes de fuente Manual.
- Editar y guardar un valor nuevo actualiza la MISMA línea (no aparece
  una línea duplicada ni el total se infla).
- Si el nuevo valor queda bajo el piso SICE-TAC, sale el mismo popup de
  confirmación que al agregar un flete nuevo (HU-12).
- "Cancelar" descarta los cambios y vuelve a mostrar el valor guardado
  tal cual estaba.

CSS agregado: `.mesa-flete-actual .cabina-button { display: block;
margin: 12px auto 0; }` — para que el botón "Editar" no quede pegado al
valor en la variante `fleteActualCard` (la variante
`mesa-flete-success` ya es flex y lo acomoda sin CSS extra).

---

### HU-16 — "Propuesta de la Biblioteca" según el modo de decisión: etiquetas SAP/NEXO, recomendado y botón "Usar este"

**Como** Administrador de Tarifas (Fredy), **quiero** que la Propuesta
de la Biblioteca me diga en qué modo se decidió la tarifa, de qué tabla
salió cada candidato, cuál es el recomendado, y —solo cuando hay algo
que elegir— poder tomar cualquiera de los candidatos con un clic,
**para** tener la última palabra sin reescribir valores a mano.

**Fuente de verdad del flujo:** `2026-10-09_HU-BACKEND.md`, sección **"Motor de
decisión de tarifa"** (flujo de 4 pasos, tabla de 10 casos y decisiones
de Sara del 2026-10-12). Esta HU es solo la parte de pantalla.

**Depende de:**
- **HU-B16** — el backend devuelve `modoDecision` en cada línea
  (`"SoloSicetac"` | `"PlazaVigenteMayor"` | `"ConsultaCompleta"`) y
  `tablaOrigen` en cada candidato (`"SAP"` | `"NEXO"`). Reemplaza al
  booleano `esSoloSicetac` de la versión anterior de esta HU (HU-B11,
  ya descartada).
- **HU-B14 / HU-B15** — para que existan candidatos con
  `tablaOrigen = "NEXO"`.
- **HU-B17** — para poder **guardar** un candidato Histórico (ver punto 6).

**Dónde** (líneas verificadas contra el código el 2026-10-12, después
de HU-15): `mesa-costeo-page.component.html:219-236` (bloque
`.mesa-proposal`, los 3 candidatos en el `*ngFor` de la línea 224) y
`mesa-costeo-page.component.ts` (`usarPropuesta()` línea 409,
`candidateSourceLabel()` 602, `candidateText()` 606,
`fuenteDesdePropuesta()` 1060). Solo lo ve quien tiene
`verPropuestaCosteo` — **AdminTarifas, CoordinadorFletes y Admin**
(`nexo-access.models.ts:43-88`); Comercial no cambia.

**1. Modelos** (`cabina-api.models.ts:203-223`):
```ts
export interface CandidatoTarifaDto {
  fuente: string;
  valor: number;
  detalle: string;
  criterio: string;
  coincidencias: number;
  advertencia?: string | null;          // ya lo manda el backend hoy, faltaba en el modelo
  tablaOrigen?: 'SAP' | 'NEXO' | null;  // nuevo (HU-B14/B15)
}

export type ModoDecision = 'SoloSicetac' | 'PlazaVigenteMayor' | 'ConsultaCompleta';

export interface PropuestaLineaDto {
  // ...campos actuales sin cambio...
  vencida?: boolean;                    // ya lo manda el backend hoy, faltaba en el modelo
  modoDecision?: ModoDecision;          // nuevo (HU-B16)
}
```

**2. Etiqueta de cada caja con su tabla de origen.** Hoy
`candidateSourceLabel()` devuelve fijo "Piso SICE-TAC + tiempos
logísticos" / "Tarifario" / "Histórico SAP". Debe agregar la tabla
cuando viene de NEXO, para no decir "SAP" de algo que no es de SAP:
- Plaza: "Tarifario" (SAP) · **"Tarifario (NEXO)"** (interna).
- Histórico: "Histórico SAP" (SAP) · **"Histórico (NEXO)"** (interna).
- SICE-TAC: sin cambio.

**3. Aviso del modo, encima de las 3 cajas** (texto corto, no botón):
- `SoloSicetac` → "Ruta marcada Solo SICE-TAC: se usa el piso
  SICE-TAC, sin comparar."
- `PlazaVigenteMayor` → "La tarifa de plaza vigente supera el piso
  SICE-TAC: se usa la plaza."
- `ConsultaCompleta` → "Consulta completa: elige el candidato que vas a
  usar (el recomendado está marcado)."

**4. Marca "Recomendado"** en la caja cuyo candidato coincide con
`fuentePropuesta` (en los 3 modos — en los 2 primeros es el único que
cuenta).

**5. Plaza vencida:** si `vencida === true`, la caja de plaza lleva la
marca **"Vencida — solo referencia"** y **no** tiene botón "Usar este"
(una tarifa vencida no se usa, RN-02).

**6. Botón "Usar este"** dentro de cada `.mesa-candidate`, junto al
valor. Visible **solo si se cumplen TODAS**:
- `entry.propuesta.modoDecision === 'ConsultaCompleta'` — en los otros
  2 modos no hay nada que elegir;
- el candidato existe (no es "Sin coincidencias") y no es la plaza
  vencida;
- la línea **todavía no tiene flete** (`!fleteDeLinea(entry.linea.lineaId)`)
  — una línea ya autocosteada queda como está, no se re-decide
  (decisión de Sara);
- no se está editando un flete manual (`!editandoFlete[entry.linea.lineaId]`,
  HU-15 — en edición la fuente queda fija en Manual);
- el candidato **no está por debajo del piso SICE-TAC** (cuando hay
  piso) — un valor bajo el piso nunca se puede usar (HU-17); esa caja
  lleva en su lugar la marca **"Por debajo del piso SICE-TAC — no se
  puede usar"** (caso 4 de la tabla: plaza 2,0 M con piso 2,5 M);
- cotización no entregada y permiso `agregarFlete`.

Igual que "Usar propuesta", **solo rellena el formulario** — no guarda;
Fredy confirma con "Agregar flete".
```html
<button
  *ngIf="entry.propuesta?.modoDecision === 'ConsultaCompleta'
         && candidate && !(i === 1 && entry.propuesta?.vencida)
         && !(entry.propuesta?.sicetac && candidate.valor < entry.propuesta.sicetac.valor)
         && !fleteDeLinea(entry.linea.lineaId) && !editandoFlete[entry.linea.lineaId]
         && cotizacion?.estado !== 'Entregada' && access.puede('agregarFlete')"
  type="button"
  class="cabina-button cabina-button--secondary cabina-button--small"
  (click)="usarCandidato(entry, candidate, i === 0 ? 'sicetac' : i === 1 ? 'tarifario' : 'historica')"
>Usar este</button>
```
```ts
usarCandidato(entry: MesaLineaEditorVm, candidate: CandidatoTarifaDto | null, fuente: FuenteFlete): void {
  if (!candidate) return;
  entry.form.patchValue({ valorFlete: candidate.valor, fuente });
  entry.form.get('valorFlete')!.markAsDirty();
  entry.form.get('fuente')!.markAsDirty();
  this.changeDetector.markForCheck();
}
```
Plaza mapea a la fuente `'tarifario'` e Histórico a `'historica'` —
los mismos valores que ya usa `fuenteDesdePropuesta()`, no son nuevos.

**7. Auto-aplicado silencioso del rol Comercial** (decisión de Sara,
2026-10-12: en consulta completa el auto-costeo no guarda precio, la
línea es de Fredy): `intentarAutoAplicar()`
(`mesa-costeo-page.component.ts:183-207`) hoy llama
`cotizacionesApi.aplicarPropuestas(cotizacion.id)` y llena **todas** las
líneas sin flete. Debe llamarlo con `soloAutomaticas = true` (parámetro
nuevo de HU-B16), para que solo llene las de `SoloSicetac` y
`PlazaVigenteMayor`:
```ts
// cotizaciones-api.service.ts:43-48 (hoy manda {} y tiene timeout de 5 min — se conservan)
aplicarPropuestas(id: string, soloAutomaticas = false): Observable<AplicarPropuestasResultDto> {
  const params = soloAutomaticas ? new HttpParams().set('soloAutomaticas', 'true') : undefined;
  return this.http.post<AplicarPropuestasResultDto>(
    `${this.baseUrl}/cotizaciones/${encodeURIComponent(id)}/aplicar-propuestas`,
    {},
    { params },
  ).pipe(timeout(300000));
}
// intentarAutoAplicar(), línea 195
return this.cotizacionesApi.aplicarPropuestas(cotizacion.id, true);
```
(`HttpParams` ya está importado en ese servicio, línea 2.) El botón
"Aplicar todas las propuestas" de Fredy (HU-19) lo sigue llamando sin
el parámetro (aplica todas, con confirmación).

**⚠️ Bloqueo conocido con el candidato Histórico (por eso depende de
HU-B17):** al guardar con fuente `historica`, el backend exige que haya
una "Histórica sugerida" vigente de la última semana
(`clsAgregarLineaCotizacionCommand.cs:50-60`, error `HISTORICA_VENCIDA`),
y el desplegable de fuente deshabilita "Histórica" si no hay sugeridas
vigentes (`[disabled]="!tieneHistoricaVigente(entry)"`). Pero esa
validación mira los fletes **ganados por NEXO** ("Históricas
sugeridas"), no el histórico de la propuesta (SAP log / histórico de
plaza NEXO). Resultado hoy: elegir el Histórico de la propuesta (con
"Usar este", o con "Usar propuesta" cuando el ganador es el histórico)
puede quedar imposible de guardar. Mientras HU-B17 no esté, ese caso
falla — no es un error de esta HU.

**Criterios de aceptación:**
- Cada caja dice de qué tabla sale ("Tarifario (NEXO)", "Histórico
  (NEXO)" cuando aplica).
- Se ve el aviso del modo y la marca "Recomendado" en el candidato de
  `fuentePropuesta`.
- En `SoloSicetac` y `PlazaVigenteMayor` no aparece ningún "Usar este".
- En `ConsultaCompleta`, en una línea sin flete, aparece "Usar este" en
  cada candidato con valor, salvo en la plaza vencida y en cualquier
  candidato por debajo del piso SICE-TAC (que muestra "Por debajo del
  piso SICE-TAC — no se puede usar").
- En una línea que ya tiene flete (autocosteada o guardada), no aparece
  ningún "Usar este".
- "Usar este" solo rellena el formulario; nada se guarda sin "Agregar
  flete".
- Cuando "Usar este" (o "Usar propuesta") pone un valor del candidato
  Histórico, la opción "Histórica" del desplegable **no queda
  deshabilitada**. Hoy (`mesa-costeo-page.component.html:334`) se
  deshabilita con `!tieneHistoricaVigente(entry)`, que solo mira las
  "Históricas sugeridas" (`entry.linea.historicas`, ts:517-519) — otra
  fuente de datos distinta al candidato `entry.propuesta?.historicoSap`.
  Si no se corrige, después de "Usar este" el desplegable mostraría
  "Histórica" seleccionada pero en gris. Cambio: la condición del
  `<option>` pasa a
  `[disabled]="!tieneHistoricaVigente(entry) && !entry.propuesta?.historicoSap"`.
  **No** cambiar `tieneHistoricaVigente()` en sí: también alimenta el
  aviso "Todas las tarifas históricas de esta ruta están vencidas"
  (html:283), que habla de las sugeridas y debe seguir igual. El
  backend debe aceptar ese guardado (HU-B17).
- "Usar propuesta" sigue funcionando igual.
- Cuando el Comercial abre Mesa de Costeo, solo se llenan solas las
  líneas de `SoloSicetac` y `PlazaVigenteMayor`; las de consulta
  completa siguen sin flete, esperando a Fredy.

---

### HU-17 — Bloquear (no solo confirmar) cualquier flete por debajo del piso SICE-TAC

**Como** negocio, **quiero** que Mesa de Costeo **no deje guardar** un
flete por debajo del piso SICE-TAC, sea tarifario, histórico o manual,
y que le avise al usuario por qué, **para** no cotizar nunca bajo el
piso legal (RN-02).

**Decisión de Sara (2026-10-12):** "nunca se puede usar un valor por
debajo del piso, ni de tarifas, ni histórico, ni manual; ni siquiera se
le permite usarlo y se le debe mandar un aviso". **Reemplaza a HU-12**,
que solo aplicaba a fuente manual y dejaba "Confirmar de todas formas".

**Depende de HU-B18** (backend) para que la regla no se pueda saltar
llamando a la API directo. Esta HU es el aviso en pantalla.

**Dónde** (líneas verificadas el 2026-10-12): `agregarFlete()`
(`mesa-costeo-page.component.ts:264`) y `editarFlete()` (línea 363), y
el modal de HU-12 en el html (después de la línea 465).

**Cambio en los 2 métodos** — la condición deja de mirar la fuente y,
si se cumple, **no guarda**:
```ts
const piso = entry.propuesta?.sicetac?.valor;
if (piso != null && Number(value.valorFlete) < piso) {
  this.fleteBajoPisoMensaje =
    `El flete (${this.moneda(Number(value.valorFlete))}) está por debajo del piso SICE-TAC ` +
    `(${this.moneda(piso)}). No se puede guardar un valor por debajo del piso: corrige el valor.`;
  this.fleteBajoPisoAbierto = true;
  return; // no se guarda nada
}
```
**Cambio en el modal:** título "No se puede guardar: flete por debajo
del piso SICE-TAC"; **un solo botón "Entendido"**
(`cerrarFleteBajoPiso()`), sin "Confirmar de todas formas". El
formulario queda tal cual para que el usuario corrija el valor.

**Se puede borrar** (queda sin uso): `confirmarFleteBajoPiso()`,
`fleteBajoPisoPendiente`, `fleteBajoPisoEditando`, y el botón
"Confirmar de todas formas" del html.

**Si el backend rechaza igual** (por ejemplo, porque el piso cambió
desde que se abrió la pantalla), el error `FLETE_BAJO_PISO_SICETAC` de
HU-B18 se muestra como cualquier otro error de guardado
(`mutationError`), con el mensaje del servidor.

**Relación con HU-16:** los candidatos de la propuesta que estén por
debajo del piso ni siquiera muestran "Usar este" — esta HU cubre el caso
de un valor escrito a mano.

**Criterios de aceptación:**
- Con piso SICE-TAC en la línea, intentar guardar (agregar o editar)
  un valor menor al piso muestra el aviso y **no guarda**, sea cual sea
  la fuente seleccionada.
- El aviso solo tiene "Entendido"; no existe forma de confirmar y
  guardar igual.
- Un valor igual o mayor al piso se guarda normal.
- Una línea sin piso SICE-TAC se guarda normal (no hay con qué
  comparar).

---

### HU-18 — Mostrar el "Valor mercancía" como chip de la línea

**Como** Administrador de Tarifas, **quiero** ver el valor de la
mercancía de cada línea junto a sus demás datos, **para** tenerlo a la
vista al costear.

**Decisión de Sara (2026-10-12):** sí, como chip (estaba en "Pendiente
de decidir", punto 1).

**Depende de HU-B20** (backend): el dato ya se captura y se guarda, pero
hoy no viaja en el DTO de Mesa de Costeo.

**Dónde:** el bloque de chips de la línea,
`mesa-costeo-page.component.html:142-153` (`.mesa-data-chips`), después
del chip "Clase de contenedor" (línea 152) — mismo formato que los demás:
```html
<span *ngIf="entry.linea.valorMercancia !== null && entry.linea.valorMercancia !== undefined"><b>Valor mercancía</b>{{ formatMoney(entry.linea.valorMercancia) }}</span>
```
Modelo `LineaCosteoDto` (`cabina-api.models.ts:119`): agregar
`valorMercancia?: number | null`. `formatMoney()` ya existe
(`mesa-costeo-page.component.ts:537`).

**Criterios de aceptación:**
- Si la línea tiene valor de mercancía, aparece el chip "Valor
  mercancía" con el valor en pesos.
- Si no lo tiene, el chip no aparece (igual que los demás chips
  opcionales).

---

### HU-19 — "Aplicar todas las propuestas" con confirmación (antes: quitar el botón)

**Como** Administrador de Tarifas, **quiero** un botón claro para
aplicar de una vez la propuesta a todas las líneas que no tienen flete,
que me pida confirmar antes, **para** llenar un pliego grande sin ir
línea por línea y sin aplicarlo por error.

**Decisión de Sara (2026-10-12), en 2 pasos:**
1. Primero decidió quitar el botón "Aplicar propuestas".
2. Después lo cambió: **se queda**, pero con el texto **"Aplicar todas
   las propuestas"** y un **botón de confirmar** antes de aplicar. Esta
   versión es la que vale. (Al dejar el botón, también se resuelve el
   problema de los pliegos: sus líneas quedan autocosteadas pero sin
   flete guardado, y este botón es la forma de llenarlas todas de una
   vez — ver `2026-10-09_HU-BACKEND.md`, "Motor de decisión de tarifa", punto 6.)

**Dónde:** `mesa-costeo-page.component.html:79-87` (la `<section>`
`mesa-propuestas-action`) y `aplicarPropuestas()` en el `.ts`
(línea ~652).

**Cambio 1 — texto del botón y de la ayuda:**
- Botón: `'Aplicar todas las propuestas (' + lineasSinFlete(vm.lineas) + ')'`
  (hoy dice "Aplicar propuestas (N)", línea 85).
- Texto de ayuda (línea 82): hoy dice "Aplica de una vez el valor más
  alto de la Biblioteca (plaza vs SICE-TAC) a cada línea pendiente" —
  queda: "Aplica de una vez el valor recomendado a cada línea que
  todavía no tiene flete."

**Cambio 2 — confirmación antes de aplicar:** el botón ya no llama
directo a `aplicarPropuestas()`; abre un modal con el **mismo patrón
que "Cancelar pliego"** (`.mesa-modal-overlay`/`.mesa-modal`, html
líneas ~448-465):
- Título: "Aplicar todas las propuestas"
- Texto: "Se aplicará el valor recomendado a {N} línea(s) sin flete.
  Las líneas que ya tienen flete no cambian. ¿Confirmas?"
- Botones: "Cancelar" (cierra sin hacer nada) y **"Confirmar"** (llama a
  `aplicarPropuestas()` como hoy).
```ts
aplicarTodasAbierto = false;
abrirAplicarTodas(): void { this.aplicarTodasAbierto = true; }
cerrarAplicarTodas(): void { this.aplicarTodasAbierto = false; }
confirmarAplicarTodas(): void {
  this.cerrarAplicarTodas();
  this.aplicarPropuestas(); // el método actual, sin cambios
}
```

**Qué NO se toca:**
- El endpoint del backend y `cotizacionesApi.aplicarPropuestas()`.
- El auto-aplicado silencioso del rol Comercial
  (`intentarAutoAplicar()`, ts:183-207) — sigue igual.
- "Usar propuesta" y "Usar este" por línea (HU-16).
- Las líneas que ya tienen flete: el backend ya las salta
  (`clsAplicarPropuestasCommand` ignora las líneas con flete).

**Criterios de aceptación:**
- El botón dice "Aplicar todas las propuestas (N)".
- Al hacer clic aparece la confirmación con la cantidad de líneas; nada
  se aplica hasta darle "Confirmar".
- "Cancelar" cierra sin aplicar nada.
- Las líneas que ya tenían flete no cambian.

---

### HU-20 — El aviso "Pendiente por enviar al comercial" se ve muy pequeño y no se entiende

**Como** Administrador de Tarifas, **quiero** que el aviso junto a
"Entregar a Comercial" (HU-7) se lea bien y se entienda sin adivinar,
**para** no olvidar enviar la cotización.

**Lo que reportó Sara (2026-10-12):** "se ve muy pequeño y no se
entiende" (era el punto 4 de "Pendiente de decidir" de esta sección).

**Causa raíz (confirmada en el código):** el estilo que hace legible el
texto al pasar el mouse (`.cabina-tooltip`: letra 14 px, espacio
interno, ancho máximo) está escrito **solo** en
`bandeja-page.component.css:69-74`, con `::ng-deep`. Ese tipo de estilo
solo se carga cuando la pantalla de Bandeja ya se abrió en la sesión.
Si el usuario entra directo a Mesa de Costeo (o refresca estando ahí),
el texto sale con el estilo por defecto de Angular Material, que es
diminuto. **Afecta las 7 pantallas** que usan `matTooltipClass="cabina-tooltip"`
(todos los íconos de información que se pusieron en Biblioteca,
Tarifas Plaza, Reglas Solo-SICETAC, Cubicador, Tablero, y este aviso).

**Parte A — texto legible en todas las pantallas (arreglo de la causa):**
mover la regla a la hoja de estilos global, `src/styles.css` (es la que
carga `angular.json` para toda la aplicación), y borrarla de
`bandeja-page.component.css`:
```css
.mat-tooltip.cabina-tooltip {
  font-size: 14px;
  line-height: 20px;
  max-width: 320px;
  padding: 8px 12px;
}
```
(en la hoja global no hace falta `::ng-deep`).

**Parte B — que se entienda sin pasar el mouse (✅ aprobada por Sara,
2026-10-12):** hoy el
aviso es solo un ícono redondo de 28 px
(`mesa-costeo-page.component.css:320`) y el significado solo aparece al
pasar el mouse. Recomendación: que junto al ícono se lea un texto corto
siempre visible, **"Pendiente por enviar"**, en la misma etiqueta
amarilla con letra roja (mismos colores de HU-7, `#fef3e2` / `#c62828`),
y conservar el tooltip con la frase completa ("Pendiente por enviar al
comercial. Envía para que le aparezca en su bandeja.").

**Criterios de aceptación:**
- Entrando directo a Mesa de Costeo (sin pasar por Bandeja), el texto
  del aviso al pasar el mouse se ve con letra de 14 px.
- Lo mismo en los íconos de información de las otras pantallas.
- (Parte B) Al lado de "Entregar a Comercial" se lee "Pendiente por
  enviar" sin tener que pasar el mouse, mientras falte enviar la
  cotización; desaparece cuando queda Entregada.


### HU-21 — Agregar servicios conexos paso a paso (tipo → desplegables propios de cada tipo → precio, unidad y subtotal)

> Pedido de Sara (2026-10-12), flujo de **Comercial / Captura / Mesa de
> Costeo**. **Implementado y validado en el sandbox** (compila sin
> errores; la lógica se corrió además en una simulación de Node contra
> las 1.336 filas reales del catálogo). **Falta la prueba visual en
> pantalla por Sara.** Sin cambios de backend.

**Como** usuario que agrega servicios conexos a una línea, **quiero**
elegir el servicio por pasos —primero el tipo y luego los desplegables
que tiene ese tipo, uno por uno— y ver al final el precio, la unidad y
el subtotal, **para** no tener que escribir y buscar a ciegas entre más
de 1.300 servicios.

**Cómo estaba:** el componente `servicio-conexo-selector`
(`components/servicio-conexo-selector/`, usado 2 veces en
`mesa-costeo-page.component.html`: una por línea y una general) era un
único buscador de texto libre ("Buscar tipo, ciudad, puerto o ruta…")
sobre todos los servicios activos, más Cantidad y el botón de agregar.

**Cómo queda (flujo en pasos):**
1. Primer desplegable **"Tipo de servicio"** con 4 opciones: Almacenamiento,
   ITR, Sellos (Precintos), Acompañamiento. Solo se ofrecen los grupos que
   tienen al menos un servicio activo.
2. Según el grupo, aparecen **solo sus desplegables**, en este orden, cada
   uno con las opciones que existen dado lo elegido antes:

| Grupo | TipoServicio del catálogo que agrupa | Pasos (etiqueta → columna) |
|---|---|---|
| Almacenamiento | "Almacenamiento" | 1. Ciudad → `origen` |
| ITR | "ITR" | 1. Ciudad → `origen` · 2. Puerto → `puerto` · 3. Tipo de máquina → `tipoMaquina` |
| Sellos (Precintos) | cualquier TipoServicio que contenga "precinto" (hoy solo "Precinto (Sello) Barra Satelital") | 1. Tipo de precinto → `tipoServicio` · 2. Ruta → `ruta` |
| Acompañamiento | cualquier TipoServicio que contenga "acompañante" (hoy 5: total en auto, total en moto, total en moto armada, parcial en moto, parcial en moto armada) | 1. Tipo de acompañante → `tipoServicio` · 2. Origen → `origen` · 3. Destino → `destino` |

   Los 8 TipoServicio reales del catálogo encajan, ninguno queda fuera
   (1 + 1 + 1 + 5). No existe "acompañante parcial en auto".
3. Al completar el último paso, el servicio queda identificado (debe haber
   **exactamente 1** servicio activo) y se muestra el resumen que ya existía:
   "{precio} {unidad} × {cantidad} = {subtotal}". Cantidad y "Agregar" se
   habilitan solo entonces.

**Reglas exactas:**
- **Opciones de cada paso:** valores distintos de esa columna entre los
  servicios activos del grupo cuyos pasos anteriores sean IGUALES (`===`) a lo
  elegido. Orden alfabético en español, sin distinguir mayúsculas/acentos y
  con numeración natural (`Intl.Collator('es', { sensitivity: 'base', numeric: true })`):
  "Maquina 3.5" antes que "Maquina 7.5".
- **Placeholder** de todos los desplegables: `— elegir —` (valor vacío).
- **Un paso con una sola opción se autoselecciona** (decidido por Sara,
  2026-10-12). Al hacerlo, el paso siguiente se habilita de inmediato; el
  usuario puede cambiarlo si hay más de una opción. Con una sola opción en
  el paso el desplegable queda con ese valor ya elegido (no en "— elegir —").
- **Sin servicio:** si todos los pasos están elegidos y no hay exactamente 1
  coincidencia (0 o 2+), aviso en rojo: "Esta combinación no tiene un
  servicio activo. Revisa los pasos elegidos." (no distingue 0 de 2+). Con
  el catálogo actual hay 0 combinaciones ambiguas: cada recorrido resuelve a
  1 fila.
- **Limpieza:** cambiar el tipo limpia todos los pasos y el servicio
  elegido; cambiar un paso limpia solo los siguientes. La Cantidad se
  conserva en ambos casos.
- **Tras agregar con éxito:** se reinicia todo (grupo, pasos, servicio) y
  la Cantidad vuelve a 1. No se conserva el grupo.
- **Buscador de texto:** se conserva completo detrás de un enlace
  "Buscar por texto" (en modo pasos) / "Volver a elegir por pasos" (en modo
  texto). Alternar de modo **conserva el servicio ya elegido** (decidido
  por Sara): si se eligió por pasos y luego se pasa a texto, el servicio
  queda seleccionado y visible en el buscador (y viceversa, al volver a
  pasos se reconstruyen grupo y pasos a partir del servicio elegido).

**Lo que no cambia:** `@Input`/`@Output` del selector
(`catalogo`, `agregando`, `bloqueado`, `motivoBloqueo`, `resultado`,
`agregar`, `reintentarCatalogo`), `cotizacionesApi.agregarConexos`, permisos
(`gestionarServicios`), resumen por línea, consolidado y PDF. `mesa-costeo-page`
no se tocó.

**Implementación de referencia (validada en el sandbox):**
- Archivo nuevo `servicio-conexo-pasos.util.ts`: `GRUPOS_CONEXO`
  (definición de los 4 grupos y sus pasos; `coincide()` usa
  `normalizarBusqueda`), `ORDEN_GRUPOS_CONEXO`, `grupoDeTipoServicio()`,
  `gruposDisponibles()`, `opcionesDelPaso()` y `servicioDelGrupo()` (resuelve
  el único servicio, o `null` si faltan pasos o no hay exactamente 1).
  Un 5º grupo futuro = una entrada más en `GRUPOS_CONEXO`, sin tocar el
  componente.
- `servicio-conexo-selector.component.ts`: estado `modoBusqueda`, `grupos`,
  `grupoElegido`, `valoresPasos`; métodos `elegirGrupo()`, `elegirPaso()`,
  `opcionesPaso()`, `alternarModoBusqueda()`. `confirmar()`, resumen y
  mensajes no cambian; solo cambia CÓMO se llena `seleccionado`.
- `.html`: `<select>` nativos con `<label [for]>` (`idBase + '-tipo'` /
  `idBase + '-paso-' + i`).
- `.css` (local al componente, nada global): `.selector__select`,
  `.selector__pasos`, `.selector__paso` (`flex: 1 1 160px`),
  `.selector__enlace` (link azul `#0055b8`), `.selector__aviso--error`.
- Accesibilidad: `<select>` nativos, orden de tab natural (Tipo → pasos →
  enlace → Cantidad → Agregar). Limitación: sin `aria-live` cuando cambian
  las opciones de un paso siguiente.
- Rendimiento: `opcionesPaso()` se evalúa desde la plantilla sin memoizar
  (componente `OnPush`); con 1.336 filas es imperceptible. Si el catálogo
  crece a decenas de miles, memoizar en `elegirGrupo`/`elegirPaso`/`ngOnChanges`.

**Recorridos probados con datos reales (simulación sobre el CSV):**
Almacenamiento → Barranquilla = $29.852,60 por estiba · ITR → Barranquilla
→ Todos → "Manual - 2 Movimientos" = $1.760.869,50 por estiba · Sellos →
Precinto Barra Satelital → "Colombia a Ecuador" = $401.824,92 por unidad ·
Acompañamiento → parcial en moto → Alto De La Tribuna → BOGOTA, D.C. =
$180.000,00 por unidad.

**Limitaciones conocidas:**
- Un TipoServicio futuro que no encaje en ningún grupo queda invisible en el
  modo por pasos (sigue apareciendo en "Buscar por texto").
- Si una columna usada trajera "NO APLICA" en una fila activa, aparecería
  como opción literal (hoy no pasa).
- Sin prueba visual en navegador todavía.

**Decidido por Sara (2026-10-12):** (1) un paso con una sola opción se
autoselecciona; (2) tras agregar se reinicia todo, no se conserva el
grupo; (3) alternar entre "por pasos" y "por texto" conserva la
selección. Los puntos 1 y 3 cambian lo que ya está en el sandbox y se le
pidieron al chat del front.

**Criterios de aceptación:**
- El primer control es el desplegable "Tipo de servicio", no un buscador libre.
- Cada tipo muestra solo sus desplegables, en el orden de la tabla, filtrados
  por lo elegido antes y ordenados alfabéticamente; un paso con una sola
  opción queda autoseleccionado.
- Al completar los pasos se ven precio, unidad y subtotal antes de agregar.
- Cambiar el tipo limpia todo; cambiar un paso limpia solo los siguientes.
- Una combinación sin servicio activo muestra el aviso y no deja agregar.
- Tras agregar, el selector queda vacío y la Cantidad en 1.
- "Buscar por texto" funciona igual que el buscador de antes y alternar de modo no pierde el servicio elegido.
- El servicio agregado, el resumen por línea, los totales y el PDF son los
  mismos de hoy.

---


### HU-22 — Quitar un servicio conexo ya agregado

> Pedido de Sara (2026-10-12). Depende de HU-B23 (endpoint `DELETE`).

**Como** usuario que gestiona servicios, **quiero** un botón "Quitar" en cada
conexo agregado a una línea, **para** corregir un error sin pedir ayuda.

**Dónde:** lista "Servicios conexos de esta línea"
(`mesa-costeo-page.component.html`, `.mesa-linea-servicios__lista`, un `<li>`
por conexo, con `trackByConexoId`).

**Cómo debe quedar:**
- Cada `<li>` agrega al final un botón "Quitar" (ícono `delete_outline` o
  texto, con `aria-label="Quitar {etiqueta del servicio} de la línea N"`).
  Solo se ve si `access.puede('gestionarServicios')` y la cotización no está
  Ganada ni Perdida (ver HU-23).
- Al pulsarlo pide confirmación breve en el mismo lugar o con el modal
  `.mesa-modal` ya usado ("Cancelar pliego"): "¿Quitar «{servicio}» de la
  línea N? El total se recalculará." Botones "Cancelar" / "Quitar".
- Al confirmar llama `cotizacionesApi.quitarConexo(cotizacionId, conexoId)`
  (nuevo en `cotizaciones-api.service.ts`: `DELETE /cotizaciones/{id}/conexos/{conexoId}`,
  devuelve `CotizacionDto`), reemplaza `this.cotizacion` con la respuesta y
  deja que el resumen por línea, el consolidado y el total se recalculen.
- Mientras corre, el botón del conexo queda deshabilitado y se reutiliza
  `conexoPendiente` para bloquear los selectores (igual que al agregar).
- Errores: se muestran con el mismo patrón de mensajes del selector
  (404 `CONEXO_NO_ENCONTRADO`, 409 `COTIZACION_CERRADA`, otros).

**Criterios de aceptación:**
- [ ] Cada conexo agregado tiene "Quitar" con confirmación.
- [ ] Al confirmar desaparece de la lista y del "Consolidado de tarifa": baja el
      conteo y el monto de "Servicios Logísticos" o "Servicios Adicionales"
      (según la categoría del conexo) y baja el Total; el resumen completo y
      el PDF dejan de incluirlo.
- [ ] El botón no aparece sin permiso ni con la cotización Ganada/Perdida.
- [ ] Un error del servidor se ve y no deja la pantalla en estado raro.

---

### HU-23 — Permitir agregar y quitar conexos con la cotización pendiente o entregada; bloquear solo si está Ganada/Perdida

> Pedido de Sara (2026-10-12). Depende de HU-B24.

**Como** Administrador de Tarifas o Comercial, **quiero** agregar y quitar
conexos tanto antes como después de entregar, y que solo se bloquee cuando la
cotización ya figura como Ganada o Perdida.

**Causa raíz en el frontend:**
- `nexo-access.models.ts`: `gestionarServicios` solo está en `Comercial`,
  `CoordinadorFletes` y `Admin` — **falta `AdminTarifas`** (por eso no ve la
  sección de servicios).
- `mesa-costeo-page.component.ts`: `motivoBloqueoLinea()` y
  `motivoBloqueoGeneral()` bloquean con `MENSAJE_COTIZACION_ENTREGADA`
  ("La cotización ya fue entregada: no admite más servicios") cuando
  `estado === 'Entregada'`; `mesa-costeo-page.component.html` también oculta
  las ayudas con `cotizacion.estado !== 'Entregada'`.

**Cómo debe quedar:**
- Agregar `'gestionarServicios'` a `AdminTarifas` en `NEXO_MATRIZ_ACCESO` y
  actualizar `nexo-access.service.spec.ts`.
- Los dos métodos de bloqueo dejan de mirar `Entregada` y miran el resultado:
  si `cotizacion.resultado` es `Ganada` o `Perdida`, el motivo es "La
  cotización ya está marcada como Ganada/Perdida: no admite cambios de
  servicios." (constante nueva; borrar `MENSAJE_COTIZACION_ENTREGADA`).
- Quitar las condiciones `estado !== 'Entregada'` de las ayudas y avisos de la
  sección "Servicios para todas las líneas".
- Con la cotización Entregada, mostrar una nota informativa sobre los
  selectores: "Esta cotización ya fue entregada. Los cambios en servicios
  quedan registrados y, si los hace alguien distinto al comercial, se le avisa
  por correo." (no bloquea nada).
- Si `cotizacion.modificadaTrasEntregaAt` viene lleno (campo nuevo del DTO,
  HU-B24), mostrar un chip "Modificada después de entregar" con la fecha.
- Agregar `resultado` y `modificadaTrasEntregaAt` a `CotizacionDto`
  (`cabina-api.models.ts`) si aún no están.

**Criterios de aceptación:**
- [ ] AdminTarifas ve y usa la sección de servicios (agregar y quitar), con la
      cotización pendiente y con la entregada.
- [ ] Con la cotización Entregada los selectores y "Quitar" funcionan y se ve
      la nota informativa.
- [ ] Con la cotización Ganada o Perdida todo queda bloqueado con el mensaje
      nuevo.
- [ ] Una cotización modificada después de entregar muestra el chip con la
      fecha.
- [ ] No queda ningún texto "no admite más servicios" por estar Entregada.

---



### HU-24 — Mesa de Costeo: líneas agrupadas por ruta y aviso de línea sin vehículo

> Detalle completo en la issue S5-HU-20.6. Depende de HU-B26.

Con una línea por camión, la Mesa agrupa las líneas por `grupoRutaId` (cada grupo muestra origen → destino y cuántas líneas tiene; dentro del grupo, el orden del servidor). Las líneas sin `grupoRutaId` se ven como hoy. Las líneas sin vehículo muestran el motivo «Línea sin vehículo: requiere consulta completa» y se pueden costear desde la Mesa.

---


### HU-25 — Guardar el valor manual como tarifa plaza desde la Mesa de Costeo

> Pedido de Sara (2026-10-14). Solo frontend. Prototipada en el sandbox (la escritura de la plaza se simula; compila, sin probar en navegador al momento de escribir esto). Detalle completo en la issue S5-HU-20.7.

**Como** Administrador de Tarifas, **quiero** que al escribir un valor manual en consulta completa el sistema me ofrezca guardarlo como nueva tarifa plaza, **para** no perder ese precio como referencia.

**Reglas:** aparece solo con valor manual (no al elegir SICE-TAC, plaza o histórico, ni con «Aplicar todas las propuestas»); solo para roles con `verTarifasPlaza` (AdminTarifas y Admin); después de validar que el valor no esté bajo el piso. Con «Sí» se abre la fila de tarifa prellenada con los datos de la línea y el valor manual; la vigencia es obligatoria y empieza vacía. Si ya existe la tarifa (misma combinación que la carga masiva) se avisa si está vigente o vencida y se muestra su valor y vigencia anteriores; actualizarla conserva el valor anterior en el histórico. El flete se guarda primero y la plaza después; si la plaza falla, el flete queda guardado.

**Criterios de aceptación:**
- [ ] El modal sale solo con valor manual y rol autorizado, tras pasar el piso.
- [ ] «No» guarda solo el flete; «Sí» muestra la fila prellenada y editable.
- [ ] El aviso distingue tarifa vigente, vencida y no existente.
- [ ] Crear o actualizar la tarifa funciona contra el backend real y el histórico queda registrado.
- [ ] Un fallo de la plaza no deshace el flete.

---

## Bandeja

> Archivos de este módulo:
> `src/app/cabina-fletes/pages/bandeja/bandeja-page.component.ts` / `.html` / `.css`

### HU-1 — Columna "Resultado" y filtro "Esperando resultado"

> Pedido de Sara (2026-10-12). Depende de HU-B25 (campo `resultado` en la fila y
> parámetro `resultado` del `GET`). Aún sin prototipo en el sandbox.

**Como** comercial, **quiero** ver en la Bandeja si cada solicitud está sin
definir, ganada o perdida, y poder listar las que falta cerrar, **para** no
perder de vista las cotizaciones entregadas sin marcar.

**Visibilidad (decidido por Sara, 2026-10-13):** el filtro y la columna solo se ven
para roles con `access.puede('marcarGanadaPerdida')` (Comercial y Admin).
**AdminTarifas no ve cuáles están ganadas o perdidas**: no se muestran la columna
ni el filtro.

**Cómo debe quedar:**
- **Filtro nuevo "Resultado"** en el formulario de filtros: un `<select>` igual
  a los de Atención, Estado y Vigencia (mismo markup, estilo, ancho y posición;
  `formControlName="resultado"`), con 4 opciones:
  "Todos" (por defecto), "Esperando resultado", "Ganada", "Perdida". Se envía
  como `resultado` al `GET` (vacío si es "Todos").
- **Al elegir "Esperando resultado", "Ganada" o "Perdida"** el control de
  Atención pasa a "Todas" automáticamente (si no, el filtro por defecto
  "Pendientes" oculta las solicitudes ya entregadas).
- **Columna nueva "Resultado"** en la tabla, con un badge `.cabina-badge`:
  "Sin definir" (neutro/ámbar) cuando `resultado === 'SinDefinir'` y la
  cotización ya se entregó, "Ganada" (verde), "Perdida" (rojo) y "—" cuando
  `resultado` es `null` (aún sin cotización).
- (Descartado por Sara: un chip/botón de acceso rápido. El filtro es solo el
  select, igual que los demás.)
- **Texto del ícono de información** (tooltip del título): agregar la mención
  de "resultado" a la lista de filtros y aclarar que "Pendientes" no incluye
  lo ya entregado.
- Sin cambios al orden ni a la paginación.

**Criterios de aceptación:**
- [ ] El filtro "Resultado" existe con las 4 opciones y filtra la tabla.
- [ ] Elegir un resultado distinto de "Todos" pone Atención en "Todas".
- [ ] La columna "Resultado" muestra el badge correcto en cada fila.
- [ ] El filtro "Resultado" se ve y se comporta igual que los otros filtros
      seleccionables (Atención, Estado, Vigencia).
- [ ] Con rol AdminTarifas no aparecen la columna ni el filtro de resultado.
- [ ] El tooltip del título menciona el filtro de resultado.

---


## Captura

> Archivos de este módulo:
> `src/app/cabina-fletes/pages/captura/captura-page.component.ts` / `.html`

### HU-1 — No autoseleccionar ciudad ni cliente al escribir; solo al cargar un Excel

> Pedido de Sara (2026-10-12). Solo frontend.

**Como** comercial que captura una solicitud, **quiero** que al escribir en
Origen, Destino o Cliente el sistema me muestre las opciones y me deje elegir,
**para** no encontrarme con una ciudad o un cliente ya elegido que no
seleccioné.

**Cómo está hoy:** cuando la búsqueda devuelve exactamente 1 resultado, el campo
se autoselecciona solo. Por ejemplo, al escribir "MEDELLIN" en Origen queda
elegida la ciudad sin mostrar la opción. El código está en
`captura-page.component.ts`: cliente en `ngOnInit` (~línea 404,
`if (clientes.length === 1) { this.selectCliente(clientes[0]); ...}`), origen y
destino en `createLinea` (~líneas 1111 y 1118, con `seleccionarCiudadOrigen` /
`seleccionarCiudadDestino`).

**Cómo debe quedar:**
- **Al escribir a mano** en Origen, Destino o Cliente: aunque haya una sola
  opción, NO se selecciona sola; se muestra en la lista de sugerencias y el
  usuario la elige con un clic.
- **Al cargar un Excel:** las ciudades de cada línea siguen resolviéndose solas
  cuando hay una sola coincidencia (es lo que hace el flujo de carga). El
  Excel nunca llena el cliente, así que el cliente no se autoselecciona en
  ningún caso.
- Misma regla para Ciudad de devolución del contenedor y Ciudad de levante si
  tienen el mismo patrón.

**Implementación sugerida:** una marca por línea y campo (por ejemplo un
`Set` de `${grupo}:origen` / `${grupo}:destino`) que `aplicarExcelALineas()`
llena justo antes de poner el valor y que el suscriptor consume y limpia
(aunque haya 0 o 2+ resultados), para que escribir después a mano no
autoseleccione. Un booleano temporal no sirve: la búsqueda tiene un debounce de
400 ms y una llamada HTTP antes de resolver. Cubrir también el modo matriz del
Excel si crea líneas por otro camino.

**Criterios de aceptación:**
- [ ] Escribir "medellin" a mano en Origen o Destino con un solo resultado
      muestra la sugerencia y NO selecciona la ciudad.
- [ ] Escribir un cliente con un solo resultado lo muestra en la lista y no lo
      selecciona solo.
- [ ] Cargar un Excel sigue resolviendo las ciudades de las líneas sin
      intervención.
- [ ] Después de cargar un Excel, escribir a mano en esa misma línea ya no
      autoselecciona.

---

### HU-2 — Campos obligatorios en Captura: fecha límite, vehículo + carrocería y clase de contenedor

> Pedido de Sara (2026-10-12). Solo frontend. Pedida al chat del front para
> el sandbox; aún sin probar en pantalla.

**Como** Administrador de Tarifas, **quiero** que Captura no deje enviar una
solicitud sin los datos que necesito para costear, **para** no recibir líneas
incompletas.

**Cómo está hoy** (`captura-page.component.ts` / `.html`):
- **Fecha límite** (`deadline`, html ~línea 36): opcional, sin asterisco.
- **Configuración vehículo** (`tipoVehiculo`): obligatoria solo si la clase de
  viaje es Cupo (`campoInvalido`, ~línea 441).
- **Carrocería**: opcional (y oculta en Devolución de contenedor).
- **Clase contenedor**: obligatoria solo si `isContainer`, aunque el campo
  también se muestra en Extradimensionado nacional (`mostrarClaseContenedor`).

**Cómo debe quedar:**
1. **Fecha límite:** obligatoria siempre (`Validators.required`, asterisco en
   el label, mensaje de error como los demás obligatorios).
2. **Configuración vehículo + Carrocería:** obligatorias cuando la clase de
   viaje es Cupo. La carrocería se suma a la regla que ya tiene el vehículo
   (asterisco, `aria-invalid`, `campoInvalido()` y mensaje por línea:
   "Línea N: selecciona la carrocería."). No aplica a Contenedor ni a
   Devolución de contenedor.
3. **Clase de contenedor:** obligatoria siempre que el campo se muestre
   (contenedor o extradimensionado nacional), no solo en contenedor.

**Criterios de aceptación:**
- [ ] Sin fecha límite no se puede enviar y se ve el mensaje.
- [ ] Una línea de Cupo sin vehículo o sin carrocería no se puede enviar y
      muestra su mensaje.
- [ ] Una línea con clase contenedor visible y vacía no se puede enviar.
- [ ] Los errores no aparecen antes de intentar enviar, igual que los otros campos.
- [ ] Las líneas que vienen de un Excel y quedan incompletas se marcan en
      la revisión, sin romper la carga.

**Por decidir con Sara (no bloquea; hoy aplica el valor por defecto):**
- Si Carga suelta y Extradimensionado también exigen vehículo + carrocería
  (por defecto: solo Cupo).
- Si el backend debe rechazar también una solicitud sin fecha límite
  (por defecto: no; `Deadline` es opcional en `clsCapturarSolicitudCommand`).

---

### HU-3 — Vista previa de la carga de Excel con todas las columnas mapeadas

> Pedido de Sara (2026-10-12). Solo frontend. Pedida al chat del front para el
> sandbox; aún sin probar en pantalla.

**Como** comercial que carga un Excel en Captura, **quiero** ver en la vista
previa todas las columnas que se van a aplicar a las líneas, **para** revisar
todo antes de confirmar.

**Cómo está hoy:** la tabla de vista previa (`captura-page.component.html` ~línea
146, `lineasExcelAplicables.slice(0, 8)`) muestra solo Origen, Destino, Línea
negocio, Clase viaje, Contenedor, Config. vehículo, Carrocería, Peso (kg),
Dimensiones (cm) y Valor mercancía. Se leen más campos (ver
`CAMPOS_MAPEO_EXCEL`) que no se ven: Exclusivo, Consolidado, Retorna
contenedor, Ciudad devolución y Levante diferente al origen. Además solo se
muestran las primeras 8 filas aunque el título diga, por ejemplo, "20
línea(s) válida(s) de 20 fila(s)".

**Cómo debe quedar:**
- La tabla muestra TODAS las columnas mapeadas, incluidas las 5 que faltan.
  Los booleanos se ven como "Sí"/"No" (o "—" si la columna no está mapeada),
  usando el mismo parseo que `aplicarExcelALineas()` (`parseBooleanoExcel`).
- Scroll horizontal si no cabe; encabezado legible.
- Todas las filas visibles (scroll vertical con altura máxima) o, si se
  mantiene un tope, una nota visible "Mostrando 8 de 20".
- El modo matriz conserva sus columnas propias.

**Criterios de aceptación:**
- [ ] Cada campo mapeado aparece como columna en la vista previa.
- [ ] Los valores mostrados son los que luego se aplican a las líneas.
- [ ] La tabla muestra todas las filas o avisa cuántas no se muestran.

---

## Biblioteca

> Archivos de este módulo:
> `src/app/cabina-fletes/pages/biblioteca/biblioteca-page.component.ts` / `.html` / `.css`

### HU-1 — Corregir semáforo de vigencia en Tarifas de plaza (bug)

**Como** usuario de Biblioteca, **quiero** que el semáforo de vigencia
(verde/amarillo/rojo) de la pestaña "Tarifas de plaza" refleje la fecha
real, **para** no confiar en un indicador que siempre dice "Vigente".

**Causa raíz:** `semaforo()` (`biblioteca-page.component.ts`) se copió
de Tarifas Plaza asumiendo que `vigencia` llega como fecha pura
(`"2026-10-08"`) y le concatena `T00:00:00` a ciegas:
```ts
// Como estaba (roto para Biblioteca):
semaforo(vigencia: string): SemaforoVigencia {
  const fecha = new Date(`${vigencia}T00:00:00`);
  if (Number.isNaN(fecha.getTime())) return 'vigente';
  ...
}
```
Pero el DTO real de Biblioteca (`clsSapTarifaHistoricoItemDto`,
`SapHistoricoDtos.cs:17,37`, NEXO) manda `DateTime`/`DateTime?`
**completo** (`"2026-10-08T00:00:00"`, con hora incluida) — al
concatenarle otra `T` queda un string inválido
(`"2026-10-08T00:00:00T00:00:00"`), `new Date(...)` no lo puede
parsear, y la función caía siempre en su propio fallback silencioso
(`return 'vigente'`). Por eso el badge mostraba "Vigente" sin importar
la fecha real.

**Por qué Tarifas Plaza (el módulo original, de donde se copió la
función) nunca mostró este bug:** su DTO (`clsTarifaPlazaDto`,
`TarifaPlazaDtos.cs:12`) usa `DateOnly`, que serializa como fecha pura
(`"2026-10-08"`, sin hora) — ahí el `T00:00:00` que concatena
`semaforo()` es intencional y correcto, fuerza que `new Date(...)`
parsee en hora **local** y no UTC (si no, en Colombia UTC-5 la fecha
se corre un día para atrás). Ese módulo no se toca.

**Implementación de referencia (validada en el sandbox):**
```ts
semaforo(vigencia: string): SemaforoVigencia {
  const fecha = new Date(vigencia.includes('T') ? vigencia : `${vigencia}T00:00:00`);
  ...
```
Detecta si el string ya trae una `T` (datetime completo) y, si es así,
no concatena nada más — maneja ambos formatos sin romper el ajuste de
timezone que necesita Tarifas Plaza.

**Criterios de aceptación:**
- Una tarifa con vigencia anterior a hoy muestra el badge **"Vencida"**
  (rojo), no "Vigente".
- El módulo Tarifas Plaza original no se toca — su propio DTO
  (`DateOnly`) no tiene este problema y su `semaforo()` sigue igual.

---

### HU-2 — Ordenar "Tarifas de plaza" por vigencia

**Como** usuario de Biblioteca, **quiero** que la pestaña "Tarifas de
plaza" se ordene igual que el módulo Tarifas Plaza (primero las que
están por vencer o ya vencidas, luego las vigentes), **para** ver de
una vez las que necesitan atención sin tener que revisar fila por fila.

**Referencia (patrón ya existente en Tarifas Plaza),
`tarifas-plaza-page.component.ts:22,111-115`:**
```ts
const ORDEN_SEVERIDAD: Record<SemaforoVigencia, number> = { vencida: 0, por_vencer: 1, vigente: 2 };
...
tarifasVisibles(): TarifaPlazaDto[] {
  return this.tarifas
    .filter((t) => !this.filtroVigencia || this.semaforo(t.vigencia) === this.filtroVigencia)
    .sort((a, b) => ORDEN_SEVERIDAD[this.semaforo(a.vigencia)] - ORDEN_SEVERIDAD[this.semaforo(b.vigencia)]);
}
```

**Diferencia importante a implementar bien:** Tarifas Plaza carga toda
la lista de una vez y ordena en el navegador. Biblioteca pagina **del
lado del servidor** (`cargarPlaza()`, llama `buscarSapPlaza()` con
`page`/`top`) — el orden debe aplicarse solo a los ítems de la página
ya cargada (`plazaResultado.items`), **no** pedir un `ORDER BY` al
backend — así lo confirmó Sara explícitamente, para no tocar backend
NEXO.

**Implementación de referencia (validada en el sandbox):**
```ts
const ORDEN_SEVERIDAD: Record<SemaforoVigencia, number> = { vencida: 0, por_vencer: 1, vigente: 2 };

plazaItemsOrdenados(): SapTarifaHistoricoItemDto[] {
  return (this.plazaResultado?.items ?? []).slice().sort((a, b) =>
    ORDEN_SEVERIDAD[this.semaforo(a.vigenciaAjustada || a.vigencia)] - ORDEN_SEVERIDAD[this.semaforo(b.vigenciaAjustada || b.vigencia)]);
}
```
El `*ngFor` de la tabla (antes `<tr *ngFor="let t of plazaResultado?.items">`)
pasa a iterar `plazaItemsOrdenados()`.

**Criterios de aceptación:**
- El orden es: vencida → por vencer → vigente (mismo criterio
  `ORDEN_SEVERIDAD` que ya usa Tarifas Plaza).
- Usa la misma fecha efectiva que ya pinta el badge (`vigenciaAjustada`
  si existe, si no `vigencia`).
- El orden aplica solo a la página ya cargada (Biblioteca pagina del
  lado del servidor) — no requiere cambio de backend, decisión tomada
  explícitamente por Sara.

---

### HU-3 — Separar columna "Vigencia" de la fecha

**Como** usuario de Biblioteca, **quiero** que el badge de vigencia y la
fecha vivan en columnas separadas, **para** que no se desalineen entre
filas cuando cambia el ancho del badge (Vencida/Por vencer/Vigente).

**Causa raíz:** el badge y la fecha estaban como texto inline dentro de
la misma celda de la tabla:
```html
<!-- Como estaba: -->
<td>
  <span class="cabina-badge" [ngClass]="vigenciaClass(semaforo(t.vigenciaAjustada || t.vigencia))">{{ vigenciaLabel(semaforo(t.vigenciaAjustada || t.vigencia)) }}</span> {{ formatDate(t.vigenciaAjustada || t.vigencia) }}
  <span *ngIf="t.vigenciaAjustada" class="cabina-badge cabina-badge--warning biblioteca-badge--separado" [title]="'Original SAP: ' + formatDate(t.vigencia)">Ajustada</span>
  <button *ngIf="access.puede('ajustarVigencia')" type="button" class="biblioteca-link" (click)="abrirAjuste([t.id])">Ajustar</button>
  <button *ngIf="t.vigenciaAjustada && access.puede('ajustarVigencia')" type="button" class="biblioteca-link biblioteca-link--muted" [disabled]="plazaQuitando === t.id" (click)="quitarAjuste(t.id)">Quitar</button>
</td>
```
Como el badge cambia de ancho según la etiqueta ("Vencida" es más
angosto que "Por vencer"), la fecha arrancaba en una posición
horizontal distinta en cada fila.

**Implementación de referencia (validada en el sandbox):** separar en
dos `<td>` reales — al ser columnas de tabla, se alinean solas entre
filas sin importar el ancho del badge, sin necesitar CSS a medida:
```html
<!-- header: nueva columna sin título, entre "Vigencia" y "Origen" -->
<th>Vigencia</th><th></th><th>Origen</th>...

<!-- fila -->
<td><span class="cabina-badge" [ngClass]="vigenciaClass(semaforo(t.vigenciaAjustada || t.vigencia))">{{ vigenciaLabel(semaforo(t.vigenciaAjustada || t.vigencia)) }}</span></td>
<td>
  {{ formatDate(t.vigenciaAjustada || t.vigencia) }}
  <span *ngIf="t.vigenciaAjustada" class="cabina-badge cabina-badge--warning biblioteca-badge--separado" [title]="'Original SAP: ' + formatDate(t.vigencia)">Ajustada</span>
  <button *ngIf="access.puede('ajustarVigencia')" type="button" class="biblioteca-link" (click)="abrirAjuste([t.id])">Ajustar</button>
  <button *ngIf="t.vigenciaAjustada && access.puede('ajustarVigencia')" type="button" class="biblioteca-link biblioteca-link--muted" [disabled]="plazaQuitando === t.id" (click)="quitarAjuste(t.id)">Quitar</button>
</td>
```
Ojo: el mensaje de "sin resultados" (`biblioteca-vacio`) es un `<p>`
fuera de la tabla, no un `<td colspan>` — no hay que ajustar ningún
colspan por la columna nueva.

**Criterios de aceptación:**
- El badge de estado vive en su propia columna.
- La fecha, el chip "Ajustada" y los botones "Ajustar"/"Quitar" viven
  en la columna siguiente.
- Al ser columnas de tabla reales, el inicio de la fecha queda alineado
  en la misma posición horizontal en todas las filas, sin importar el
  ancho del badge de cada una.
- No aplica a Tarifas Plaza (el módulo original) — cambio exclusivo de
  Biblioteca.

---

### HU-4 — Paginación en "Histórico SAP"

**Como** usuario de Biblioteca, **quiero** que la pestaña "Histórico
SAP" tenga los mismos botones "Anterior"/"Siguiente" y selector de
tamaño de página que ya tiene "Tarifas de plaza", **para** poder
recorrer todo el histórico y no solo las primeras filas que trae la
búsqueda.

**Causa raíz:** `buscarSap()` nunca mandaba `top`/`page` en
`FiltrosSapHistorico`, y no existía ningún estado de paginación
(`sapPagina`/`sapTamanoPagina`) ni los controles en el HTML — aunque el
backend ya soporta `top`/`page` ahí (`FiltrosSapHistorico`,
`biblioteca-api.models.ts:94-102`, mismo tipo que usa `buscarSapPlaza`,
es decir el mismo mecanismo que ya funciona en "Tarifas de plaza",
nunca conectado en esta pestaña).

**Implementación de referencia (validada en el sandbox) — espejo
exacto de "Tarifas de plaza", campo por campo:**
- Estado: `sapPagina = 1;` y `sapTamanoPagina = TAM_PAGINA_PLAZA_DEFAULT;`
  (misma constante que ya usa "plaza").
- Getter `totalPaginasSap` espejo de `totalPaginasPlaza`:
  `Math.max(1, Math.ceil((this.sapResultado?.total ?? 0) / this.sapTamanoPagina))`.
- `cambiarTamanoPaginaSap(event)` espejo de `cambiarTamanoPaginaPlaza`.
- `buscarSap()` ahora resetea `sapPagina = 1` y delega a un nuevo
  `private cargarSap()` (igual que `buscarPlaza()`→`cargarPlaza()`); el
  body viejo de `buscarSap()` (la llamada real a la API) se movió a
  `cargarSap()`, agregando `top: this.sapTamanoPagina, page: this.sapPagina`
  al objeto `filtros`.
- `irAPaginaSap(pagina)` espejo de `irAPaginaPlaza`.
- `reintentarSap()` llama `cargarSap()` directo (no `buscarSap()`) —
  igual que `reintentarPlaza()` llama `cargarPlaza()` directo, para no
  resetear la página al reintentar.
- HTML: mismo bloque de paginación de "plaza" (botones
  "Anterior"/"Siguiente", selector 25/50/100/200, texto "X tarifa(s) ·
  página N de M"), renombrado a `sap*`, en la misma posición relativa
  (antes de la tabla, después del estado de loading/error).

**Criterios de aceptación:**
- La pestaña "Histórico SAP" tiene los mismos controles de paginación
  que "Tarifas de plaza": "Anterior"/"Siguiente", selector de tamaño de
  página (25/50/100/200), y el texto "X tarifa(s) · página N de M".
- Cambiar cualquier filtro de búsqueda regresa a la página 1.
- El texto "mostrando las primeras X" de esta pestaña queda cubierto
  por HU-5 (abajo), que lo quita de las 3 pestañas a la vez.

---

### HU-5 — Quitar el texto "mostrando las primeras X" en las 3 pestañas con paginación

**Como** usuario de Biblioteca, **quiero** que el conteo de resultados
no diga "mostrando las primeras X" una vez que ya hay paginación real,
**para** no ver un mensaje que no tiene sentido al estar parado en
cualquier página distinta a la primera.

**Causa raíz:** el texto `(mostrando las primeras {{ items.length }})`
existe desde antes en "Tarifas de plaza" y "SICE-TAC" (de ahí se copió
también a "Histórico SAP" al agregarle paginación en HU-4) — tenía
sentido cuando no había páginas y solo se traían las primeras N filas,
pero con paginación real el bloque de "página N de M" ya comunica lo
mismo mejor, y este texto queda engañoso (dice "primeras X" aunque
estés en la página 3).

**Implementación de referencia (validada en el sandbox) — quitar el
`<ng-container>` completo en las 3 pestañas, dejando solo el total:**
```html
<!-- plaza, como estaba: -->
{{ plazaResultado.total }} tarifas<ng-container *ngIf="plazaResultado.total > plazaResultado.items.length"> (mostrando las primeras {{ plazaResultado.items.length }})</ng-container> · Fuente: ...

<!-- plaza, arreglado: -->
{{ plazaResultado.total }} tarifas · Fuente: dbsolucionesTI · tblSAPTarifas (solo lectura) · la vigencia es ajustable en Nexo (override local)

<!-- sap, arreglado: -->
{{ sapResultado.total }} tarifas · Fuente: dbsolucionesTI · tblSAPTarifasLog (solo lectura)

<!-- sicetac, arreglado: -->
Periodo <strong>{{ formatPeriodo(sicetacResultado.periodo) }}</strong> · {{ sicetacResultado.total }} tarifas encontradas · Fuente: DWSice · tblRutasSicetac (solo lectura)
```

**Criterios de aceptación:**
- En las **3 pestañas** ("Tarifas de plaza", "Histórico SAP" y
  "SICE-TAC"), el texto de conteo queda solo como se muestra arriba —
  sin el paréntesis "(mostrando las primeras X)".
- No cambia nada más del bloque de paginación ya existente.

---

### HU-6 — Reemplazar el texto descriptivo fijo por un ícono de información, igual que Bandeja

**Como** usuario de Biblioteca, **quiero** que la explicación de qué es
esta vista aparezca en un ícono de información (como ya pasa en
Bandeja), **para** no tener un párrafo fijo ocupando espacio bajo el
título.

**Causa raíz:** el encabezado tenía un `<h1>` + `<p>` con texto
descriptivo fijo, en vez del patrón de ícono+tooltip que ya usa
Bandeja. Único lugar con este patrón en todo el archivo — no hay otro
`<p>` descriptivo fijo en el resto de Biblioteca (los demás `<h2>` son
títulos de modales, no descripciones de vista).

**Referencia (patrón ya existente en Bandeja),
`bandeja-page.component.html:4-16`:**
```html
<h1>
  Bandeja de gestión tarifaria
  <button type="button" class="cabina-info-btn"
    matTooltip="Filtra por estado, vigencia, fecha de captura y atención (auto-costeo o Administrador de Fletes). Una solicitud es vigente si fue capturada hace una semana o menos."
    matTooltipClass="cabina-tooltip" matTooltipPosition="right"
    aria-label="Más información sobre esta vista">
    <mat-icon inline="true">info_outline</mat-icon>
  </button>
</h1>
```

**Implementación de referencia (validada en el sandbox),
`biblioteca-page.component.html:1-16`:**
```html
<h1>
  Biblioteca
  <button type="button" class="cabina-info-btn"
    matTooltip="Matriz oficial SICE-TAC, tarifas de plaza e histórico SAP TM (en vivo, solo lectura) y el catálogo de servicios logísticos y adicionales."
    matTooltipClass="cabina-tooltip" matTooltipPosition="right"
    aria-label="Más información sobre esta vista">
    <mat-icon inline="true">info_outline</mat-icon>
  </button>
</h1>
```
El `<p>` descriptivo fijo que estaba debajo del `<h1>` se quitó por
completo. La clase `.cabina-info-btn` es un componente de estilos
propio de cada página en Angular (no se comparte entre archivos) —
hubo que copiar el bloque CSS completo a `biblioteca-page.component.css`:
```css
.cabina-page-header h1 { display: flex; align-items: center; gap: 8px; margin: 0; color: #1a2430; font: 600 28px/36px Inter, system-ui, sans-serif; }
.cabina-info-btn { display: inline-flex; align-items: center; /* ...resto igual al de Bandeja... */ }
.cabina-info-btn mat-icon { font-size: 20px; width: 20px; height: 20px; }
.cabina-page-header h1 .cabina-info-btn { margin-top: 4px; }
.cabina-info-btn:hover, .cabina-info-btn:focus-visible { background: #cfe3f5; color: #00478f; }
.cabina-info-btn:focus-visible { outline: 2px solid #0055b8; outline-offset: 1px; }
```
(se agregó `display:flex; align-items:center; gap:8px` a
`.cabina-page-header h1`, que antes no lo tenía, para que el ícono
quede alineado junto al título).

**Criterios de aceptación:**
- El `<h1>Biblioteca</h1>` ya no tiene un párrafo descriptivo fijo
  debajo.
- Junto al título, un ícono de información (`info_outline`) muestra el
  mismo texto al pasar el mouse, con el mismo estilo/comportamiento que
  el de Bandeja (mismo `matTooltipClass`, misma posición).
- El resto de Biblioteca no tiene otro texto descriptivo fijo que
  convertir — este es el único caso.

---

### HU-7 — Unidad editable en el servicio "Almacenamiento"

**Como** usuario de Biblioteca, **quiero** poder elegir la unidad del
servicio "Almacenamiento" entre "Por estiba" y "Por unidad", **para**
no quedar forzado siempre a "Por estiba".

**Causa raíz:** el campo Unidad del formulario "Nuevo servicio" queda
**deshabilitado** para cualquier tipo que no sea `'valor_unico'`, con un
valor fijo que viene de un mapa constante:
```ts
// como estaba:
const UNIDAD_FIJA: Record<TipoMiniVista, string> = {
  ...
  almacenamiento: 'Por estiba',
  ...
};
```
```html
<!-- como estaba: -->
<label *ngIf="servicioForm.tipoMiniVista !== 'valor_unico'" class="biblioteca-field"><span>Unidad</span><input type="text" [value]="servicioForm.unidad" disabled /></label>
```

**Implementación de referencia (validada en el sandbox):** para
`tipoMiniVista === 'almacenamiento'` específicamente, reemplazar el
input deshabilitado por un `<select>` con las 2 opciones:
```html
<label *ngIf="servicioForm.tipoMiniVista === 'almacenamiento'" class="biblioteca-field"><span>Unidad</span>
  <select (change)="servicioForm.unidad = $any($event.target).value">
    <option value="Por estiba" [selected]="servicioForm.unidad === 'Por estiba'">Por estiba</option>
    <option value="Por unidad" [selected]="servicioForm.unidad === 'Por unidad'">Por unidad</option>
  </select>
</label>
```
La condición del input deshabilitado de siempre pasa de `!== 'valor_unico'`
a `!== 'valor_unico' && !== 'almacenamiento'` — para que ITR/por_ruta/
por_origen_destino sigan bloqueados igual que antes, solo Almacenamiento
cambia. `UNIDAD_FIJA.almacenamiento = 'Por estiba'` se mantiene como
valor por defecto al crear el formulario (no se toca), el `<select>`
solo permite cambiarlo después.

**Criterios de aceptación:**
- Para el tipo de servicio "Almacenamiento", el campo Unidad es un
  selector con exactamente 2 opciones: "Por estiba" y "Por unidad".
- El valor por defecto al crear un servicio nuevo sigue siendo "Por
  estiba".
- El resto de tipos de servicio (ITR, por ruta, por origen/destino,
  valor único) no cambian su comportamiento de Unidad.

---

### HU-8 — Código de "Almacenamiento" en mayúscula inicial, no todo mayúsculas

**Como** usuario de Biblioteca, **quiero** que el código del servicio
"Almacenamiento" se vea igual (mayúscula inicial) sin importar cuándo
se creó, **para** no ver inconsistencias como "Almacenamiento" junto a
"ALMACENAMIENTO" en la misma columna de la tabla.

**Causa raíz:** el código se deriva de un mapa fijo,
`biblioteca-page.component.ts:74-77`:
```ts
// como estaba:
const CODIGO_SERVICIO_FIJO: Partial<Record<TipoMiniVista, string>> = {
  almacenamiento: 'ALMACENAMIENTO',
  itr: 'ITR',
};
```
mientras que el "Tipo de servicio" (columna "Servicio" de la tabla) usa
mayúscula inicial para el mismo tipo (`TIPO_SERVICIO_FIJO.almacenamiento
= 'Almacenamiento'`, línea 71) — un registro creado antes de este
hardcodeo quedó con el código en mayúscula inicial también, y por eso
se ve distinto al lado de uno nuevo.

**Implementación de referencia (validada en el sandbox):**
```ts
const CODIGO_SERVICIO_FIJO: Partial<Record<TipoMiniVista, string>> = {
  almacenamiento: 'Almacenamiento',
  itr: 'ITR',
};
```
Solo cambia el valor de `almacenamiento` — `itr: 'ITR'` se deja igual
(ahí sí coincide con `TIPO_SERVICIO_FIJO.itr`, no hay nada que
corregir).

**Criterios de aceptación:**
- Al crear un nuevo servicio "Almacenamiento", la columna "Código"
  muestra "Almacenamiento" (mayúscula inicial), no "ALMACENAMIENTO".
- El tipo "ITR" no cambia.

---

### HU-9 — Separar "Movimiento" de "Tipo de máquina" en el formulario de ITR (manual), los dos como desplegable

**Como** usuario que crea un servicio "ITR", **quiero** elegir tanto
la máquina como el número de movimientos (1 o 2) de desplegables, en
vez de tener que escribirlos a mano, **para** no depender de que cada
quien escriba el mismo valor igual.

**Contexto — por qué no es una columna nueva en la base de datos:** el
campo real en la base de datos (`TipoMaquina`, columna única,
`clsServicioConexo.cs`) ya mezcla máquina + movimiento en un solo
texto — confirmado contra los datos reales ya cargados
(`docs/Database/seeder/servicios_conexos.csv`): `"Maquina 3.5 - 1
Movimiento"`, `"Maquina 7.5 - 2 Movimientos"`, `"Manual - 1
Movimiento"`, etc. No hay que agregar ninguna columna — el formulario
debe seguir guardando **un solo texto combinado** en ese mismo campo,
solo que ahora armado desde 2 inputs separados en vez de que el
usuario lo escriba todo junto a mano.

**Archivo:** `biblioteca-page.component.html:499` (el único input de
"Tipo de máquina" hoy, texto libre con placeholder de ejemplo).

**Formato exacto a generar, confirmado contra los datos reales:**
`{máquina} - {N} Movimiento` si N=1, `{máquina} - {N} Movimientos` si
N=2 (singular/plural, separador siempre `" - "`).

**Arreglo:** en el formulario de "Nuevo servicio"/"Editar servicio",
para `tipoMiniVista === 'itr'`:
- Cambiar el input de texto libre de "máquina" por un `<select>` con
  exactamente **3 opciones, cerradas** (confirmado contra el seeder —
  son los únicos 3 valores reales que existen hoy): "Manual",
  "Maquina 3.5", "Maquina 7.5".
- Agregar un `<select>` nuevo "Movimiento" con exactamente 2 opciones:
  "1" y "2".
- Al guardar, concatenar ambos en el formato de arriba y mandar ese
  texto combinado como `tipoMaquina` al backend — el backend no se
  entera de que antes eran 2 campos separados, sigue recibiendo un
  solo string en el mismo campo de siempre.

**Alcance — solo el formulario manual, no la carga masiva de Excel:**
cuando el servicio se carga por Excel, la columna ya trae el texto
combinado tal cual (igual que en el seeder) — no se parte en 2 columnas
de Excel ni se toca la vista previa de la carga masiva para esto.

**Criterios de aceptación:**
- El formulario de ITR tiene un desplegable "Máquina" con exactamente
  3 opciones ("Manual", "Maquina 3.5", "Maquina 7.5") y un desplegable
  separado "Movimiento" con opciones 1/2 — ningún texto libre.
- Al guardar, se genera el texto combinado con el formato exacto de
  arriba (singular/plural correcto) y se guarda en el mismo campo
  único de siempre.
- La carga masiva de Excel de Servicios no cambia — sigue esperando
  el texto ya combinado en una sola columna.
- Al editar un servicio "ITR" ya guardado, el formulario separa el
  texto combinado de vuelta en los 2 campos (máquina + movimiento),
  no muestra el string crudo.

**Implementación de referencia (primera versión validada en el
sandbox — el campo "máquina" todavía era texto libre, pendiente de
actualizar a desplegable):**
- `servicioForm` tiene un campo nuevo `movimientosMaquina: '1' | '2'`
  (default `'1'`) junto a `tipoMaquina`, que ahora representa **solo**
  la parte "máquina" (ej. "Maquina 7.5"), no el string combinado.
- Nuevo `<select>` "Movimiento" con exactamente "1"/"2", al lado del
  input de "Tipo de máquina" (placeholder actualizado a "Ej: Maquina
  7.5", ya no mezcla el movimiento en el ejemplo).
- Al guardar (`guardarServicio()`): `combinarTipoMaquina(maquina, movimientos)`
  → `"{maquina} - {N} Movimiento(s)"` (singular/plural correcto).
- Al editar un servicio existente (`abrirEditarServicio()`):
  `partirTipoMaquina()` separa el string combinado de vuelta con una
  regex (`/^(.*) - (\d+) Movimientos?$/`) para prellenar los 2 campos.
  Si un registro viejo no calza con ese formato exacto, cae a mostrar
  el string completo en "máquina" y "1" en movimiento (no rompe nada,
  solo no lo separa).
- No se tocó la carga masiva de Excel — esa columna sigue esperando el
  texto ya combinado en una sola celda.

**Actualizado (validado en el sandbox, 2026-10-12):** el input de
texto de "máquina" ya es un `<select>` con las 3 opciones cerradas:
```html
<select [value]="servicioForm.tipoMaquina" (change)="servicioForm.tipoMaquina = $any($event.target).value">
  <option value="">— seleccionar —</option>
  <option value="Manual">Manual</option>
  <option value="Maquina 3.5">Maquina 3.5</option>
  <option value="Maquina 7.5">Maquina 7.5</option>
</select>
```
Con placeholder "— seleccionar —" (mismo patrón que ya usa este
archivo para otros selects obligatorios sin default natural, ej.
Clase de contenedor en Captura), para no forzar "Manual" como
seleccionado cuando el estado real está vacío. `movimientosMaquina`,
el `<select>` de Movimiento y `combinarTipoMaquina()` no cambiaron.
Confirmado que `partirTipoMaquina()` sigue funcionando igual al editar
— ninguno de los 3 valores reales contiene el separador `" - "` que
usa la regex. Compiló bien, sandbox, sin commitear.

Compiló bien. Sandbox, sin commitear. Pendiente de que Sara lo revise
en pantalla.

---

## Tarifas Plaza

### HU-1 — Mostrar el detalle de errores que el backend ya devuelve al importar Excel

**Como** usuario que carga el Excel de Tarifas Plaza, **quiero** ver
exactamente qué fila falló y por qué, **para** no tener que adivinar o
depurar a mano cuál fila se rechazó.

**Archivo:** `tarifas-plaza-page.component.ts`, líneas 216-217.

**Causa raíz:** el backend ya devuelve `resultado.errores`
(`IReadOnlyList<string>`, confirmado en `clsImportarTarifasPlazaResultDto`,
`SolicitudDtos.cs`/`TarifaPlazaDtos.cs` línea 16) con el detalle **por
fila** de cada rechazo (ej. `"fila 109: '46315' no es una fecha de
vigencia válida"`). El frontend hoy solo pinta el conteo
(`"N rechazada(s)"`), nunca el detalle.

**Arreglo:** mostrar `resultado.errores` al usuario cuando
`rechazadas > 0` (en la misma notificación, o en un detalle
expandible), no solo el conteo.

**Criterios de aceptación:**
- Cuando `rechazadas > 0`, el usuario ve el detalle de
  `resultado.errores` (qué fila, qué dato, por qué se rechazó), no
  solo el número.
- El conteo resumen (`"N rechazada(s)"`) se conserva además del
  detalle.

---

### HU-2 — Editar una línea rechazada directo en pantalla, sin reimportar todo el Excel

**Como** usuario que carga el Excel de Tarifas Plaza, **quiero** poder
corregir una línea rechazada directamente en la pantalla, **para** no
tener que volver al Excel, arreglar la fila y reimportar el archivo
completo otra vez por un solo dato mal puesto.

**Depende de HU-1** — para poder editar la línea rechazada hace falta
saber antes qué campo específico falló.

**Criterios de aceptación:**
- Una línea rechazada en la importación se puede editar directo desde
  la pantalla (no solo corrigiendo el Excel y reimportando).
- Al guardar la corrección, se valida con el mismo criterio que usa
  el importador (mismo catálogo de municipios, rangos de peso, línea
  de negocio, clase de viaje).

---

### HU-3 — Reemplazar el texto descriptivo fijo por un ícono de información, igual que Bandeja/Biblioteca/Reglas Solo-SICETAC

**Como** usuario de Tarifas Plaza, **quiero** que la explicación de qué
es esta vista aparezca en un ícono de información, **para** no tener
un párrafo fijo ocupando espacio bajo el título.

**Mismo patrón exacto ya aplicado en Bandeja, Biblioteca y Reglas
Solo-SICETAC** — nada que inventar, solo reusarlo aquí.

**Archivo:** `tarifas-plaza-page.component.html:2-7`:
```html
<!-- como está hoy: -->
<header class="cabina-page-header">
  <div>
    <h1>Tarifas de plaza</h1>
    <p>Tarifas de plaza gobernadas por Nexo, con carga y descarga masiva por Excel.</p>
  </div>
</header>
```

**Implementación de referencia:**
```html
<header class="cabina-page-header">
  <h1>
    Tarifas de plaza
    <button type="button" class="cabina-info-btn"
      matTooltip="Tarifas de plaza gobernadas por Nexo, con carga y descarga masiva por Excel."
      matTooltipClass="cabina-tooltip" matTooltipPosition="right"
      aria-label="Más información sobre esta vista">
      <mat-icon inline="true">info_outline</mat-icon>
    </button>
  </h1>
</header>
```
El `<div>` envolvente de hoy puede quitarse si no tiene otro uso (no
parece necesitarse para nada más en este archivo — confirmar al
implementar).

**Criterios de aceptación:**
- El `<h1>Tarifas de plaza</h1>` ya no tiene un párrafo descriptivo
  fijo debajo.
- Junto al título, un ícono de información muestra el mismo texto
  exacto al pasar el mouse.

**Validado en el sandbox:** texto del tooltip idéntico. Se confirmó
que el `<div>` envolvente no tenía ningún CSS/uso propio y se quitó.
Se copió `.cabina-info-btn` (no lo tenía), se agregó
`display:flex;gap:8px` a `.cabina-page-header h1`, y se quitó
`.cabina-page-header p` por quedar huérfana. Compiló bien, sin
commitear.

---

### Hallazgos secundarios (no bloquean, pendientes de decidir alcance)

- ~~Ciudades ambiguas sin aviso~~ — **decidido (Sara, 2026-10-12):** la
  vista previa muestra cuál se usó y de qué departamento, y deja
  cambiarlo antes de confirmar. Ver **HU-9** en la sección "Carga masiva
  de Excel" y **HU-B21** en `2026-10-09_HU-BACKEND.md`.
- ~~Tipo de Vehículo no se valida contra Rango de Peso~~ — **decidido
  (Sara, 2026-10-12): no se hace.** El importador sigue aceptando el
  tipo de vehículo que venga en el Excel sin cruzarlo contra la banda
  de peso.

---

## Reglas Solo-SICETAC

### HU-1 — Botón "Exportar Excel", igual que Tarifas de Plaza

**Como** usuario de Reglas Solo-SICETAC, **quiero** un botón para
exportar las reglas ya cargadas a Excel, **para** poder editarlas y
reimportarlas sin reconstruir el archivo desde cero.

**Depende de:** el endpoint nuevo de backend (`2026-10-09_HU-BACKEND.md`, HU-B6).

**Archivo:** `reglas-solo-sicetac-api.service.ts` — solo tiene
`importarExcel` (línea 38), no hay método `exportarExcel`.

**Arreglo:** agregar el botón "Exportar Excel" en la pantalla de
Reglas Solo-SICETAC, igual que ya existe en Tarifas de Plaza, y el
método `exportarExcel()` correspondiente en el servicio.

**Criterios de aceptación:**
- Botón "Exportar Excel" en Reglas Solo-SICETAC, mismo lugar/estilo
  que en Tarifas de Plaza.
- El archivo exportado se puede editar y reimportar sin errores de
  formato.

---

### HU-2 — Reemplazar el texto descriptivo fijo por un ícono de información, igual que Bandeja/Biblioteca

**Como** usuario de Reglas Solo-SICETAC, **quiero** que la explicación
de qué es esta vista aparezca en un ícono de información, **para** no
tener un párrafo fijo ocupando espacio bajo el título.

**Mismo patrón exacto que ya se aplicó en Biblioteca** (ver
`2026-10-09_HU-FRONTEND.md`, sección Biblioteca, HU-6) y que ya existe en
Bandeja — nada que inventar, solo reusarlo aquí.

**Archivo:** `reglas-solo-sicetac-page.component.html:2-5`:
```html
<!-- como está hoy: -->
<header class="cabina-page-header">
  <h1>Rutas solo SICE</h1>
  <p>Demarca combinaciones de origen, destino y tipo de vehículo que se cotizan EXCLUSIVAMENTE con el piso SICE-TAC (sin comparar con plaza ni tarifario). Las solicitudes que hagan match se auto-costean y quedan atendidas para el comercial.</p>
</header>
```

**Implementación de referencia (mismo patrón ya usado en Bandeja y
Biblioteca):**
```html
<header class="cabina-page-header">
  <h1>
    Rutas solo SICE
    <button type="button" class="cabina-info-btn"
      matTooltip="Demarca combinaciones de origen, destino y tipo de vehículo que se cotizan EXCLUSIVAMENTE con el piso SICE-TAC (sin comparar con plaza ni tarifario). Las solicitudes que hagan match se auto-costean y quedan atendidas para el comercial."
      matTooltipClass="cabina-tooltip" matTooltipPosition="right"
      aria-label="Más información sobre esta vista">
      <mat-icon inline="true">info_outline</mat-icon>
    </button>
  </h1>
</header>
```
El texto del tooltip es **exactamente** el mismo párrafo que se quita
— no se resume ni se cambia. Si el componente no tiene ya el bloque
CSS `.cabina-info-btn` (copiado por página, no se comparte entre
componentes Angular), copiarlo igual que se hizo en Biblioteca.

**Criterios de aceptación:**
- El `<h1>Rutas solo SICE</h1>` ya no tiene un párrafo descriptivo fijo
  debajo.
- Junto al título, un ícono de información (`info_outline`) muestra el
  mismo texto exacto al pasar el mouse, con el mismo estilo/
  comportamiento que Bandeja/Biblioteca.

---

## Carga masiva de Excel (Servicios de Biblioteca y Tarifas Plaza)

> **Decidido (2026-10-09): 100% frontend, sin cambios de backend.** Se
> evaluó agregar 2 endpoints nuevos para separar "analizar" de
> "confirmar" (ver nota en `2026-10-09_HU-BACKEND.md`, misma sección — se descartó),
> pero Sara prefirió resolverlo todo en este repo, reusando el patrón
> que ya existe en Servicios de Biblioteca y reutilizando los endpoints
> de catálogo que ya existen para Tarifas Plaza.
>
> **Esta sección se prototipó extensamente en el sandbox** (HU-1 a
> HU-8 abajo documentan el recorrido completo, con reversiones
> incluidas) y ya fue **confirmada en pantalla real por Sara**. El
> resumen de estado final de hoy (2026-10-12), verificado contra el
> código real, está primero — es la referencia rápida de "cómo quedó
> todo"; las HU individuales de abajo quedan como el detalle/historial
> de cada pieza, para quien necesite el porqué de una decisión puntual.

### Estado final (2026-10-12) — referencia rápida

**Servicios (Biblioteca)** — `biblioteca-page.component.ts`/`.html`,
lógica en `servicios-excel.util.ts`:
- Código / Tipo de servicio / Categoría: `<input disabled readonly>`,
  nunca editables, nunca marcan error (se fuerzan al valor fijo del
  tipo de servicio o a la categoría activa).
- Unidad: `<select>` con 2 opciones fijas hardcodeadas ("Por
  estiba"/"Por unidad" — no vienen de catálogo async, por eso nunca
  tuvo el bug de preselección que sí afectó a Tarifas Plaza).
- Activo: `<select>` fijo (Sí/No).
- Origen / Destino: autocompletado de ciudad (texto + sugerencias).
- Vigencia: `<input type="date">`.
- Costo unitario: texto con separador de miles (`formatMilesPreview`/
  `onCostoPreviewInput`).
- Resto de columnas (puerto/ruta/tipo de máquina): texto simple.
- Filtro de filas ocultas: `*ngIf="fila.esNueva || fila.errores.length > 0"`
  (`contarFilasOcultasPreview`).
- Contrato actual: `validarValoresFila(tipo, categoriaEsperada,
  incluirActivo, valoresOriginales): {valores, errores}`;
  `revalidarLectura(...)` agrega duplicado-en-lote + choque con
  catálogo + `esNueva`; `crearArchivoCargaServicios` filtra
  `errores.length === 0` antes de exportar.
  `detectarChoquesConCatalogo` (función vieja) fue eliminada,
  reemplazada por `revalidarLectura`.

**Tarifas Plaza** — `tarifas-plaza-page.component.ts`/`.html`, lógica
en `tarifas-plaza-excel.util.ts`:
- **Marcan error** si no coinciden con el catálogo: Línea de negocio,
  Clase de viaje, Origen, Destino, Clase de contenedor (los 5,
  `<select>`/autocompletado cerrado).
- **No marca error** (solo canoniza si hay match, campo libre de
  verdad): Tipo de vehículo — el catálogo real tiene códigos
  incompletos (falta 3S2, 2L3, etc.) y Sara decidió no hardcodear
  nada; sigue siendo `<select>` cerrado (solo opciones del catálogo),
  mostrando "{código} · {descripción}" (ej. "3S3 · Tractomula"), igual
  que Reglas Solo-SICETAC.
- Rango de peso: `<select>` opcional, valida formato contra catálogo
  (tolera espacios), no obligatorio si hay Tipo de vehículo (y
  viceversa — regla "al menos uno de los dos").
- Vigencia: `<input type="date">`. Valor/Detalles: texto simple.
- Filtro de filas ocultas: mismo patrón que Servicios
  (`contarFilasOcultasPreviewPlaza`).
- **Bug de preselección del `<select>` — resuelto:** en
  `importarExcel()`, la fila se dibuja primero con los 5 `<select>` en
  blanco, y un `setTimeout` los rellena un instante después con el
  valor ya canonizado — evita que Angular omita la asignación real de
  `[value]` cuando sus `<option>` (`*ngFor` desde catálogo async) no
  estaban listas en el primer dibujado. Parpadeo breve aceptado por
  Sara como costo del fix.

**Archivos nuevos/movidos:**
- `src/app/cabina-fletes/util/excel-lectura.util.ts` (nuevo) — helpers
  genéricos de lectura/escritura Excel compartidos entre los dos
  módulos (antes duplicados dentro de `servicios-excel.util.ts`):
  `cargarXlsx`, `normalizar`, `iguales`, `parsearImporte`,
  `parsearFecha`, `leerCelda`, `celdasConDatos`, `escribirLibro`, etc.
- `src/app/cabina-fletes/pages/tarifas-plaza/tarifas-plaza-excel.util.ts`
  (nuevo) — espejo de `clsEfTarifasPlazaRepository.ImportarExcelAsync`
  del backend, mismo patrón que `servicios-excel.util.ts`.

**Pendiente (no es un bug de frontend):** los códigos de Tipo de
vehículo que faltan en el catálogo real de SAP (3S2, 2L3, etc.)
dependen de que alguien actualice `dbSolucionesTI`, o del despliegue
del fix de HU-B5 en NEXO (quita los 5 "casos especiales"
hardcodeados). Sara decidió esperar, sin parche temporal en frontend.
Nada más queda pendiente de esta sección — todo lo demás está
confirmado en pantalla real.

### HU-1 — Vista previa editable antes de confirmar la carga masiva

**Como** usuario que carga un Excel de Servicios (Biblioteca) o de
Tarifas Plaza, **quiero** ver una ventana con todas las filas que se
van a cargar, ya prellenadas con lo que se leyó del Excel, poder editar
cualquier campo de cualquier fila, y tener que confirmar explícitamente
antes de que se guarde en la base de datos, **para** poder corregir
errores (por ejemplo, una columna nueva que el sistema no reconoce) sin
tener que editar el Excel y volver a subirlo desde cero.

**Cómo está hoy (para que quede claro qué cambia):**
- **Servicios de Biblioteca** ya tiene una validación previa en el
  navegador (`servicios-excel.util.ts`, usa la librería `xlsx` —
  `biblioteca-page.component.ts:597-622`): `leerServiciosDesdeExcel()`
  parsea el Excel completo a datos estructurados en el cliente, y si
  **cualquier** fila tiene un problema, bloquea **todo el archivo** (no
  se carga nada) y muestra la lista de errores por fila. Si todo pasa,
  `crearArchivoCargaServicios()` **reconstruye un Excel nuevo** a partir
  de esos mismos datos estructurados y lo manda al backend — es decir,
  el frontend ya hace el viaje completo "archivo → datos → archivo"
  hoy mismo, solo que sin ninguna pausa para que el usuario revise.
- **Tarifas Plaza** es más simple todavía: `tarifas-plaza-page.component.ts:183-190`
  manda el archivo crudo directo al backend
  (`this.api.importarExcel(archivo)`), sin ningún parseo en el
  navegador — hoy el mapeo de columnas y las reglas de negocio
  (catálogo de municipios con desempate Colombia, formato de rangos de
  peso, líneas de negocio y clases de viaje válidas) viven solo en
  `clsEfTarifasPlazaRepository.cs`, del lado del backend.

**Cómo debería quedar — mismo patrón para los 2, sin tocar el backend:**
1. Al subir el archivo, se parsea **en el navegador** (con la librería
   `xlsx`, igual que ya hace Servicios) y se abre una ventana/modal con
   una **grilla editable**: una fila por cada fila del Excel, con todas
   sus columnas ya prellenadas con lo que se interpretó.
2. El usuario puede editar **cualquier campo de cualquier fila** ahí
   mismo, antes de confirmar — no hace falta volver a tocar el Excel.
3. Las filas con problema se resaltan **en rojo**, con el motivo visible
   (igual que ya hace el bloque de errores de hoy, pero dentro de la
   misma grilla en vez de aparte).
4. Nada se guarda en la base de datos hasta que el usuario confirme
   explícitamente (un botón tipo "Confirmar carga") — a diferencia de
   hoy, que en Servicios se guarda automáticamente apenas pasa la
   validación, sin pedir confirmación.
5. Al confirmar, se reconstruye el Excel a partir de los datos ya
   editados (mismo patrón que `crearArchivoCargaServicios()`) y se
   manda al **mismo endpoint que ya existe hoy** (`importar-excel`) —
   sin contrato nuevo, el backend sigue recibiendo un Excel normal.

**Para Tarifas Plaza específicamente — portar el parseo/validación al
frontend (hoy no existe nada de esto ahí):** las 4 reglas de negocio
que hay que replicar en JavaScript, y de dónde sacar los catálogos
(todos ya existen como endpoints de solo lectura, no hay que crear
ninguno):
- Municipios por nombre (con desempate Colombia si el nombre choca
  entre países) → `GET /api/catalogos/municipios` (mismo catálogo que
  ya usa el propio formulario manual de Tarifas Plaza).
- Rangos de peso por su formato de texto ("3-15000" / "10500+") →
  `GET /api/rangos-peso` (`clsRangosPesoController.cs`).
- Líneas de negocio válidas → `GET /api/catalogos/lineas-negocio`.
- Clases de viaje válidas → `GET /api/catalogos/clases-viaje`.

**Riesgo aceptado conscientemente:** los catálogos siempre se
consultan en vivo contra estos mismos endpoints (no hay datos
desactualizados), pero el **algoritmo** de validación (normalización de
texto, el formato exacto de rango de peso, el criterio de desempate)
se duplica entre backend y frontend — si alguien cambia esa lógica en
`clsEfTarifasPlazaRepository.cs` sin acordarse de replicarla aquí, la
vista previa podría aprobar/rechazar distinto a lo que el backend
termina aceptando de verdad al recibir el Excel reconstruido.

**Criterios de aceptación:**
- Subir un Excel nunca guarda nada en la BD de inmediato — siempre pasa
  primero por la vista previa.
- La vista previa muestra **todas** las filas del Excel (no solo las
  que tienen problema), cada una editable campo por campo.
- Una fila con problema se ve resaltada en rojo, con el motivo.
- Solo al confirmar explícitamente se guarda — y se guarda con los
  valores tal como quedaron en la grilla (incluyendo ediciones), no los
  valores originales del Excel si el usuario los cambió.
- Mismo comportamiento en Servicios de Biblioteca y en Tarifas Plaza.
- Ningún endpoint de backend nuevo — el envío final sigue usando
  `importar-excel` tal como existe hoy en los 2 módulos.

**Implementación de referencia (validada en el sandbox):**

*Compartido entre los 2 módulos:* se extrajo la lectura/escritura
genérica de Excel (lectura robusta de celdas — tipo nativo primero,
texto como respaldo — parseo de importes/fechas, escritura de .xlsx) a
un archivo nuevo compartido, `cabina-fletes/util/excel-lectura.util.ts`.
`servicios-excel.util.ts` se actualizó para importarlo de ahí en vez de
tener su propia copia — es la misma lógica de fechas ya validada en el
bug anterior, reusada tal cual, no reescrita.

*Servicios de Biblioteca:* se cambió `cargarExcelServicios()`
(`biblioteca-page.component.ts`) para que **siempre** abra la vista
previa con todas las filas (antes: bloqueaba todo el archivo si había
cualquier error, o importaba directo si no había ninguno). Para esto
`leerServiciosDesdeExcel()` tuvo que dejar de descartar los valores de
las filas con error (antes solo guardaba el mensaje en un array aparte
`problemas`, sin los datos) — ahora todas las filas, válidas e
inválidas, quedan en `lectura.filas` con sus propios `errores: string[]`.
Se agregaron `validarValoresFila()` y `revalidarLectura()` (mismas
reglas de negocio que ya existían, mismo criterio exacto), para
revalidar en vivo cada vez que el usuario edita una celda —
`revalidarLectura()` cubre reglas de negocio + duplicados dentro del
archivo + choque contra el catálogo ya guardado (reemplaza a
`detectarChoquesConCatalogo()`, que se eliminó por quedar reemplazada).
El modal nuevo usa el mismo patrón visual `.biblioteca-modal-overlay`/
`.biblioteca-modal` (con una variante de ancho nueva para la grilla):
un `<input>` por celda, fila completa en rojo si tiene error, mensajes
listados en la última columna, "Confirmar carga" deshabilitado
mientras quede alguna fila en rojo. Al confirmar, reconstruye el Excel
con `crearArchivoCargaServicios()` y lo manda a `importarExcel()`, el
mismo endpoint de siempre.

*Tarifas Plaza:* archivo nuevo `tarifas-plaza-excel.util.ts`, espejo en
TypeScript de `clsEfTarifasPlazaRepository.ImportarExcelAsync`
(backend): mismas columnas (LINEA DE NEGOCIO, CLASE DE VIAJE, ORIGEN,
DESTINO, VALOR, VIGENCIA, DETALLES obligatorias + CLASE CONTENEDOR,
RANGO PESO, TIPO VEHICULO opcionales), detección de encabezado por
texto exacto (igual que el backend, no "contiene" como sí hace
Servicios), municipio por nombre normalizado, rango de peso por
formato exacto `"min-max"`/`"min+"`, línea de negocio/clase de viaje
contra catálogo, vigencia con el mismo parseo robusto, y la regla de
"al menos rango de peso o tipo de vehículo". **Detalle fino replicado
correctamente:** el backend de Tarifas Plaza no trata los duplicados
dentro del archivo como error (los fusiona en silencio, gana la
última fila) — la vista previa hace lo mismo, no marca duplicados
como error ahí. Mismo patrón de modal/grilla que Servicios (estilos
CSS duplicados en `tarifas-plaza-page.component.css`, ya que no
cruzan entre componentes Angular).

Ambos módulos compilan. Sin commitear. Pendiente de que Sara lo pruebe
en pantalla.

---

### HU-2 — Auto-corregir columnas fijas en vez de marcar error (Servicios)

**Como** usuario que carga Servicios, **quiero** que Código/Tipo de
servicio/Unidad/Categoría se corrijan solas al valor correcto cuando el
tipo de servicio tiene uno fijo (Almacenamiento, ITR), **para** no
tener que arreglar a mano algo que el sistema ya sabe cuál es.

**Causa raíz:** en `servicios-excel.util.ts`, `tipoServicio`/`unidad`/
`categoria` ya se auto-corregían al guardarse en `valores` (`tipoServicio:
config.tipoServicioFijo ?? tipoServicio`, etc.) pero el código **de
todas formas** empujaba un error si no coincidían — la fila quedaba en
rojo aunque el valor ya estuviera bien. `codigoServicio` ni siquiera se
auto-corregía, se guardaba tal cual lo trajo el Excel. Y
`validarValoresFila()` (la que revalida cada edición en la grilla) no
auto-corregía nada en ningún caso, solo devolvía errores.

**Implementación de referencia (validada en el sandbox):**
- `validarFila()` (lectura inicial del Excel): `codigo`/`tipoServicio`/
  `unidad` ahora se resuelven desde el principio de la función como
  `config.tipoServicioFijo ?? textoDelExcel` (y lo mismo con
  `unidadFija`), en vez de leer el texto crudo y corregirlo solo al
  guardar. "Falta X" solo se dispara si **no** hay valor fijo y el
  campo vino vacío — si hay fijo, nunca falta, nunca está mal. Se
  quitaron los 4 `errores.push("...debe ser «X»...")` de mismatch.
  Categoría: se quitó el check completo (`categoriaEsperada` siempre
  tiene un valor resuelto, nunca hace falta, se fuerza siempre).
- `validarValoresFila()` (revalida al editar en la grilla): cambió el
  contrato de `string[]` a `{ valores, errores }` — ahora fuerza los 4
  campos (mismo criterio) en vez de solo devolver errores.
  `revalidarLectura()` se actualizó para usar los `valores` corregidos,
  así la grilla muestra el valor ya corregido apenas se revalida, no
  solo dice que estaba mal.
- **Bug encontrado y corregido de paso:** la detección de fila
  duplicada (`clave`) y el chequeo de longitud máxima
  (`textosConLimite`) usaban el `codigo` **crudo** (antes de forzarlo).
  Si una fila traía CODIGO vacío en un tipo con `tipoServicioFijo`, el
  valor se guardaba bien (forzado) pero la detección de duplicados la
  trataba como "clave incompleta" y nunca la comparaba contra otras
  filas — dejando pasar duplicados reales sin avisar. Ahora los dos
  usan el valor ya corregido.

Compiló bien. Sin commitear. Pendiente de que Sara lo pruebe en pantalla.

**Criterios de aceptación:**
- Si el Excel o la edición en la grilla trae otra cosa en Código/Tipo
  de servicio/Unidad/Categoría (cuando el tipo tiene un valor fijo), la
  vista previa lo muestra ya corregido automáticamente, sin marcar
  error ni fila en rojo por esa razón.
- Solo quedan en rojo las filas con problemas reales (falta un dato,
  costo inválido, fecha inválida, dimensión faltante, duplicado,
  choque con catálogo, etc.).
- Dos filas con el mismo código (ya corregido/forzado) y la misma
  combinación de dimensiones se detectan como duplicadas, aunque el
  Excel haya traído el código vacío o distinto.
- No aplica a Tarifas Plaza (no tiene columnas "fijas por tipo" de esta
  forma).
- **Código, Tipo de servicio y Categoría dejan de ser editables en la
  grilla** — ya que su valor siempre se fuerza (HU-2), no tiene sentido
  mostrarlos como input editable. Se ven como texto fijo o un input
  deshabilitado con el valor ya correcto.

---

### HU-3 — Inputs apropiados por tipo de columna en la grilla (Servicios)

**Como** usuario que revisa la vista previa de carga masiva de
Servicios, **quiero** que cada columna use el tipo de input que le
corresponde (selector de ciudad, calendario, desplegable, número con
separador), **para** no poder escribir cualquier cosa en campos que
tienen un catálogo o formato específico.

**Mismo patrón que ya existe en el formulario manual de "Nuevo
servicio"** (`biblioteca-page.component.html`/`.ts`) — nada que
inventar, solo reusarlo en la grilla:
- **Origen** (y Destino si aplica): autocompletado contra el catálogo
  real de ciudades, mismo mecanismo que ya usa el formulario manual —
  `buscarCiudadOrigenServicio()`/`ciudadesOrigenServicio`
  (`biblioteca-page.component.ts:741,743`). Hoy en la grilla es texto
  libre sin validar (se vio "peru", "manizales" en minúscula, sin
  catálogo detrás).
- **Vigencia:** `<input type="date">`, igual que la línea 481 del
  formulario manual. Hoy es texto libre, por eso sale "Falta VIGENCIA"
  con tanta facilidad.
- **Activo:** `<select>` con SI/NO, igual que el formulario manual
  (líneas 483-485 — ahí es booleano `'si'`/`'no'`, la grilla guarda
  string `'SI'`/`'NO'`, ajustar el `<select>` a ese formato).
- **Unidad:** se queda **editable**, pero como `<select>` (no texto
  libre) — mismas opciones que ya se agregaron al formulario manual de
  Almacenamiento ("Por estiba"/"Por unidad").
- **Costo Unitario:** input con separador de miles mientras se escribe,
  mismo patrón ya existente en `mesa-costeo-page.component.ts:414-425`
  (`formatMiles()` + `onValorMonetarioInput()` — formatea con
  `toLocaleString('es-CO')`, guarda el número limpio por dentro).

**Criterios de aceptación:**
- Origen (y Destino, donde aplique) solo acepta ciudades del catálogo
  real, con autocompletado — no texto libre sin validar.
- Vigencia se selecciona de un calendario, no se escribe a mano.
- Activo es un desplegable con SI/NO, no texto libre.
- Unidad es un desplegable con las opciones válidas, editable.
- Costo Unitario muestra separador de miles mientras se escribe.

**Implementación de referencia (validada en el sandbox):** los 6
puntos ya están aplicados en `biblioteca-page.component.html:376-411` —
Código/Tipo de servicio/Categoría como `<input disabled readonly>`;
Unidad y Activo como `<select>`; Vigencia como `<input type="date">`;
Origen/Destino con el mismo buscador de ciudades del formulario manual
(`buscarCiudadPreview()`/`seleccionarCiudadPreview()`, nuevo en
`biblioteca-page.component.ts:675-687`, reusando
`origenQuery$`/`destinoQuery$` que ya existían); Costo Unitario con
`formatMilesPreview()`/`onCostoPreviewInput()`. Compiló bien, sin
commitear.

**Bug encontrado al probar en pantalla — las casillas solo aceptan una
letra:** `*ngFor="let fila of preview.filas; let i = index"` (línea
376) no tenía `trackBy`. `editarCeldaPreviewServicios()`
(`biblioteca-page.component.ts:650-655`) reconstruye el array `filas`
completo con objetos nuevos en cada edición (`.map()` + spread) — sin
`trackBy`, Angular identifica las filas por referencia de objeto, así
que interpreta que todas cambiaron de identidad y destruye+recrea el
`<tr>` completo del DOM en cada tecla, incluido el `<input>` activo —
al recrearse, pierde el foco, por eso solo entraba una letra (pega más
fuerte en Origen/Destino, que disparan en cada tecla vía `(input)`, que
en los campos con `(change)`, que solo disparan al salir del campo).

**Implementación de referencia (validada en el sandbox):** se agregó
`trackBy: trackByIndice` (por índice de fila, no hace falta un id
estable ya que las filas no se reordenan) al `*ngFor` de las **dos**
grillas — Servicios y Tarifas Plaza, que tenía exactamente el mismo
patrón (array reconstruido completo en cada edición, sin `trackBy`).
Confirmado: ya se puede escribir normal en ambas. Compiló bien, sin
commitear. Pendiente de que Sara lo pruebe en pantalla.

---

### HU-4 — Ocultar de la vista previa las filas existentes que no tienen ningún error

**Como** usuario que revisa la vista previa de carga masiva (Servicios
o Tarifas Plaza), **quiero** ver solo las filas que de verdad necesitan
mi atención, **para** no tener que desplazarme entre cientos de filas
que ya están bien y no cambian nada.

**Regla exacta (confirmada con Sara):**
- **Fila nueva** (no existe todavía ningún registro con esa misma
  clave en la base de datos) → **siempre aparece**, tenga o no error —
  hay que poder revisar/corregir algo que se va a crear por primera
  vez.
- **Fila existente** (ya hay un registro con esa clave — es una
  actualización) → aparece **solo si tiene algún error**. Si no tiene
  ningún error, se **oculta** de la tabla.

**Importante — esto es solo de visualización, no cambia qué se
guarda:** una fila existente sin error que queda oculta de la tabla
**se sigue guardando igual** al confirmar — no se excluye de la carga,
solo no se muestra en la grilla para revisión.

**Por qué es viable y no complejo:** la clasificación "nueva vs.
existente" ya existe en el código que arma la vista previa — es la
misma lógica que hoy cuenta cuántas filas son "creadas" vs.
"actualizadas" (comparando la clave de la fila contra los registros ya
guardados). Solo hace falta **filtrar qué se renderiza** en la tabla
con esa misma clasificación + el array `errores` que ya tiene cada
fila — no hay que tocar la lógica de guardado para nada.

**También confirmado con Sara:** mostrar un conteo de cuántas filas
quedaron ocultas, para que no parezca que el Excel traía menos filas de
las que realmente trae — algo como "N fila(s) sin cambios, ya
aplicada(s) sin necesidad de revisión" arriba de la tabla.

**Criterios de aceptación:**
- Una fila nueva (sin registro existente con esa clave) siempre
  aparece en la vista previa, tenga o no error.
- Una fila existente (ya hay un registro con esa clave) solo aparece
  si tiene al menos un error.
- Una fila existente sin error, aunque no aparezca en la tabla, se
  guarda igual al confirmar.
- Se muestra un conteo de cuántas filas quedaron ocultas por no tener
  cambios ni errores.
- Aplica a las dos grillas (Servicios y Tarifas Plaza).

**Implementación de referencia (validada en el sandbox):** campo
nuevo `esNueva: boolean` en `FilaValidadaExcel`/`FilaPlazaExcel`,
calculado en `revalidarLectura()`/`revalidarLecturaPlaza()` reusando
la misma búsqueda de "¿ya existe esta combinación?" que ya existía
(Servicios) o se agregó para esto (Tarifas Plaza). En las dos tablas,
el `*ngFor` de filas pasó a `<ng-container *ngFor="...">` +
`<tr *ngIf="fila.esNueva || fila.errores.length > 0">` adentro — así
el índice `i` sigue siendo el índice real del array completo (clave
para que `editarCelda...(i, ...)` no edite la fila equivocada al haber
filas ocultas). Mensaje nuevo arriba de la tabla: "{{ ocultas }}
fila(s) sin cambios, ya aplicada(s) sin necesidad de revisión."
Confirmado: el guardado (`crearArchivoCargaServicios()`/
`crearArchivoCargaPlaza()`) sigue filtrando solo por
`errores.length === 0`, sin mirar si la fila está oculta — las
ocultas se guardan igual. Compiló bien, sin commitear.

---

### HU-5 — Usar los mismos controles de catálogo del formulario manual, en la vista previa de Tarifas Plaza

**Como** usuario que revisa la vista previa de carga masiva de Tarifas
Plaza, **quiero** que cada campo use el mismo control que ya tiene el
formulario manual de "Agregar tarifa" (desplegables, autocompletado de
ciudades, sugerencias), **para** no poder escribir cualquier cosa en
campos que tienen un catálogo detrás.

**Cómo está hoy:** la grilla de Tarifas Plaza es más simple que la de
Servicios — **todas** las columnas son `<input type="text">` genérico,
sin ningún control especial (`tarifas-plaza-page.component.html:185-189`):
```html
<td *ngFor="let columna of columnasPlaza">
  <input type="text" [value]="fila.valores[columna]" (change)="editarCeldaPreviewPlaza(i, columna, $any($event.target).value)" />
</td>
```

**El formulario manual de "Agregar tarifa" (mismo archivo,
líneas 113-151) ya tiene el control correcto para cada campo — replicar
cada uno tal cual, no inventar nada nuevo:**

| Campo | Control en el formulario manual | Catálogo/fuente |
|---|---|---|
| Línea de negocio | `<select>` | `lineasNegocio` (ya cargado en el componente) |
| Clase de viaje | `<select>` | `clasesViaje` (ya cargado) |
| Origen / Destino | autocompletado de ciudades (input + lista de sugerencias) | búsqueda de municipios (mismo mecanismo que ya tiene el formulario manual, y que la grilla de Servicios ya replicó para sus propias filas con `celdaCiudadActivaPreview`) |
| Rango de peso | `<select>` (value = id) | `rangosPeso` (ya cargado) |
| Clase de contenedor | input de texto + `<datalist>` (sugerencia, no obligatorio) | `clasesContenedor` (ya cargado) |
| Tipo de vehículo | input de texto + `<datalist>` (sugerencia, no obligatorio) | `tiposVehiculo` (ya cargado) |

Los 6 catálogos (`lineasNegocio`, `clasesViaje`, `rangosPeso`,
`tiposVehiculo`, `clasesContenedor`, municipios) **ya están cargados**
en el componente — son los mismos que usa `tarifas-plaza-excel.util.ts`
para validar cada fila de la vista previa. No hay que pedir ni cargar
nada nuevo, solo cambiar qué control de formulario usa cada columna
dentro del `*ngFor` de la tabla.

**Ojo — Clase de contenedor y Tipo de vehículo NO deben ser `<select>`
estrictos:** en el formulario manual son sugerencia (`<datalist>`), no
una lista cerrada obligatoria — el backend tampoco los valida contra
ningún catálogo al importar (son columnas opcionales de texto). Mismo
criterio en la grilla: sugerir, no forzar.

**Criterios de aceptación:**
- Línea de negocio y Clase de viaje son `<select>` en la grilla, con
  las mismas opciones que el formulario manual.
- Origen y Destino usan el mismo autocompletado de ciudades que ya usa
  el formulario manual (y que ya usa la grilla de Servicios para sus
  propias filas).
- Rango de peso es un `<select>` con las mismas opciones formateadas
  que el formulario manual (ej. "3 - 15000 kg").
- Clase de contenedor y Tipo de vehículo muestran sugerencias
  (`<datalist>`) pero siguen aceptando texto libre, igual que el
  formulario manual.
- Detalles y Valor se quedan como texto libre — no se pidió cambiarlos
  en esta HU. Vigencia tampoco se pidió aquí, aunque hoy también es
  texto libre en esta grilla (`"2026-09-30"` como string plano) — si
  Sara quiere el mismo `<input type="date">` que ya tiene la grilla de
  Servicios, es un pedido aparte.

**Implementación de referencia (validada en el sandbox):** hecho tal
cual el mapeo de la tabla. Para Origen/Destino, nuevo
`celdaCiudadActivaPreviewPlaza` (igual que en Servicios) para trackear
qué celda tiene el desplegable de sugerencias abierto — pero con
métodos propios (`buscarMunicipioPreviewPlaza`/
`seleccionarMunicipioPreviewPlaza`), no los que ya usa el formulario
manual (`seleccionarMunicipioOrigen`/`seleccionarMunicipioDestino`):
esos guardan `"{municipio} · {departamento}"` para mostrarlo en el
formulario, pero la grilla necesita guardar **solo** el nombre del
municipio (es lo que compara `validarValoresFilaPlaza()` contra el
catálogo, y lo que espera el backend en el Excel) — con el formato del
formulario manual, cada fila habría quedado marcada como error "no es
un municipio del catálogo" siempre.

**Bug real encontrado y corregido de paso, no pedido explícitamente:**
los 6 catálogos (`lineasNegocio`/`clasesViaje`/`municipios`/
`rangosPeso`/`clasesContenedor`/`tiposVehiculo`) solo se cargaban al
abrir el modal manual "Agregar tarifa" (`cargarCatalogos()`) — si
alguien entraba directo a "Importar Excel" sin haber abierto antes ese
modal, los selects/datalist de la grilla habrían salido vacíos.
`cargarCatalogos()` ahora devuelve un Observable en vez de dispararse
sola, y `importarExcel()` lo encadena antes de construir la vista
previa — los 6 catálogos quedan cargados siempre que se abra la
grilla, sin importar de dónde se entre. De paso, esto eliminó una
llamada duplicada a la API que pedía 4 de los 6 catálogos por separado
solo para validar. Compiló bien, sin commitear.

---

### HU-6 — Ajustes tras la primera prueba en pantalla de HU-5 (Tarifas Plaza)

Sara probó la vista previa ya con los controles de catálogo (HU-5) y
encontró 3 cosas:

**1. Posible bug — "Clase de viaje" no viene preseleccionado con el
valor que trae la fila.** En una fila con error, "Línea de negocio" sí
aparece preseleccionado con el valor correcto ("Oferta de Expedición
Puerto a interior"), pero "Clase de viaje" aparece en "— seleccionar —"
vacío, aunque la fila sí trae un valor de Clase de viaje. El markup de
los dos `<select>` es idéntico (`[value]="fila.valores.X"`,
`[value]="x.descripcion"` en las opciones) — **investigar antes de
asumir que es un bug de binding**: puede ser que el valor real de
Clase de viaje en esa fila específica (una fila de prueba con varios
datos inválidos a propósito — fecha `2026-09-31`, `"pruebaa"` en
Detalles) genuinamente no coincida con ningún valor del catálogo
`clasesViaje`, en cuyo caso no seleccionar nada es el comportamiento
correcto (y debería estar apareciendo en la lista de errores de esa
fila, si no está apareciendo ahí también revisar eso). Confirmar el
valor real contra el catálogo antes de tocar código.

**2. Cambio — Clase de contenedor pasa a ser `<select>` estricto,
igual que Rango de peso (ya no sugerencia con `<datalist>`).** Esto
**corrige lo que HU-5 pedía originalmente** (ahí se había especificado
como sugerencia, igual que el formulario manual) — Sara confirmó que
en la grilla lo quiere como lista cerrada, con el mismo catálogo
`clasesContenedor` que ya está cargado. Mismo patrón que Línea de
negocio/Clase de viaje/Rango de peso:
```html
<select *ngSwitchCase="'claseContenedor'" [value]="fila.valores.claseContenedor" (change)="editarCeldaPreviewPlaza(i, 'claseContenedor', $any($event.target).value)">
  <option value="">— ninguno —</option>
  <option *ngFor="let cc of clasesContenedor" [value]="cc.descripcion">{{ cc.descripcion }}</option>
</select>
```
**Nota:** esto deja a la grilla más estricta que el formulario manual
de "Agregar tarifa" (que sigue usando `<datalist>` para este campo, sin
tocarlo) — es una diferencia a propósito, confirmada con Sara, no un
descuido.

**3. Cambio — Vigencia debe ser un selector de calendario, no texto
libre.** Mismo patrón que ya usa el formulario manual (línea 155,
`<input type="date">`) y que ya usa la grilla de Servicios:
```html
<input *ngSwitchCase="'vigencia'" type="date" [value]="fila.valores.vigencia" (change)="editarCeldaPreviewPlaza(i, 'vigencia', $any($event.target).value)" />
```

**Criterios de aceptación:**
- Confirmado si "Clase de viaje" no preseleccionaba por un dato
  genuinamente inválido (comportamiento correcto) o por un bug de
  binding (corregido si aplica).
- Clase de contenedor es un `<select>` con el catálogo `clasesContenedor`
  y una opción "— ninguno —" (es opcional).
- Vigencia usa un selector de calendario (`<input type="date">`), no
  texto libre.
- Tipo de vehículo no cambia (se queda como `<datalist>`, no se pidió
  tocarlo).

**Investigación del punto 1 — causa raíz confirmada (era el bug, no un
dato inválido):** `validarValoresFilaPlaza` usa `iguales()`
(comparación normalizada: sin tildes, sin mayúsculas/minúsculas) para
decidir si Línea de negocio/Clase de viaje coinciden con el catálogo —
pero el `<select>` compara `[value]="fila.valores.claseViaje"` contra
`[value]="cv.descripcion"`, que es comparación **exacta** de string
(HTML nativo, sin normalizar). Si el Excel trae otra capitalización/
tilde que el catálogo, la validación la acepta (no error) pero el
select no la encuentra exacta, así que no preselecciona nada.
"Línea de negocio" de esa misma fila no mostró el problema solo porque
esa columna vino con la capitalización exacta del catálogo por
coincidencia.

**Confirmado que esto replica un comportamiento real del backend**
(`clsEfTarifasPlazaRepository.cs`,
`lineasNegocioValidas.TryGetValue(normalizado, out var lineaNegocioCanonica)`):
el backend normaliza para **matchear**, pero **guarda la forma
canónica del catálogo**, no el texto crudo del Excel — la vista previa
no estaba replicando esa segunda parte.

**Implementación de referencia (validada en el sandbox):**
`validarValoresFilaPlaza` ahora también **canoniza** — cuando hay
match, reemplaza el valor de la fila por la forma exacta del catálogo
(`match.descripcion`/`match.municipio`), para los 4 campos que
comparan contra catálogo con `iguales()`: Línea de negocio, Clase de
viaje, Origen y Destino. Cambió su contrato de `string[]` a
`{ valores, errores }` (mismo patrón ya usado en Servicios). Esto
resuelve el select sin preseleccionar, y de paso hace que la vista
previa muestre exactamente lo que el backend terminaría guardando, no
el texto crudo del Excel. **Mejora adicional, no pedida explícitamente:**
para Origen/Destino también se agregó el mismo desempate
Colombia-si-choca-el-nombre que ya usa el backend (antes la vista
previa solo chequeaba existencia del municipio, sin resolver cuál si
el nombre chocaba entre países). Compiló bien, sin commitear.

---

### HU-7 — Ajustes tras la segunda prueba en pantalla: Rango de peso/Clase de contenedor sin prellenar, Tipo de vehículo también a desplegable estricto, consistencia visual

**1. Rango de peso y Clase de contenedor tampoco vienen prellenados —
mismo tipo de causa que HU-6, investigar y extender el mismo arreglo.**
El patrón de HU-6 (`validarValoresFilaPlaza` canoniza el valor de la
fila a la forma exacta del catálogo cuando hay match) se aplicó solo a
Línea de negocio/Clase de viaje/Origen/Destino — Rango de peso y Clase
de contenedor quedaron afuera. Investigar primero si en las filas de
ejemplo el valor es genuinamente inválido (el mensaje de error
visible menciona "no coincide... esperado 3-15000", lo que podría ser
un dato de prueba real inválido) o si es el mismo problema de
comparación exacta vs. normalizada — y si corresponde, extender la
canonización a estos 2 campos también.

**2. Tipo de vehículo pasa a `<select>` estricto también — ya no
`<datalist>`.** Decisión nueva de Sara, por consistencia visual con el
resto de la grilla (ver punto 3) — revierte lo decidido en HU-5/HU-6
(ahí se había dejado como sugerencia, igual que el formulario manual).
Mismo patrón que los demás selects de catálogo, con `tiposVehiculo`
(ya cargado):
```html
<select *ngSwitchCase="'tipoVehiculo'" [value]="fila.valores.tipoVehiculo" (change)="editarCeldaPreviewPlaza(i, 'tipoVehiculo', $any($event.target).value)">
  <option value="">— ninguno —</option>
  <option *ngFor="let tv of tiposVehiculo" [value]="tv.codigo">{{ tv.descripcion }}</option>
</select>
```
**Nota:** el formulario manual sigue con `<datalist>` para este campo,
sin tocar — el cambio es solo en la grilla, igual que ya pasó con
Clase de contenedor en HU-6.

**3. Consistencia visual de todos los desplegables de la grilla con el
resto de la página.** Revisar que los `<select>` nuevos de la grilla
(Línea de negocio, Clase de viaje, Rango de peso, Clase de contenedor,
Tipo de vehículo) usen las mismas clases CSS que ya usan los
desplegables existentes en el resto de la página (ej. el formulario
manual de "Agregar tarifa"), para que no se vean con el estilo nativo
del navegador mientras el resto de la página tiene un estilo propio.

**Criterios de aceptación:**
- Rango de peso y Clase de contenedor se preseleccionan correctamente
  cuando la fila trae un valor que coincide (normalizado) con el
  catálogo.
- Tipo de vehículo es un `<select>` estricto en la grilla, con el
  catálogo `tiposVehiculo`.
- Todos los desplegables de la grilla se ven visualmente iguales a los
  demás desplegables de la página (misma clase CSS/estilo).

**Investigación y arreglo (validado en el sandbox):**
- **Rango de peso:** era el mismo tipo de causa que HU-6, pero por un
  motivo distinto — la comparación ya era insensible a mayúsculas,
  pero no toleraba espacios incidentales (ej. "3 - 15000" en vez de
  "3-15000" del catálogo). Se corrigió para ignorar espacios al
  comparar — los números siguen teniendo que coincidir exacto con el
  catálogo, igual que el backend, no se volvió más permisivo con datos
  genuinamente inválidos. Ahora también canoniza al valor exacto del
  catálogo cuando hay match, igual que los otros 4 campos de HU-6.
- **Clase de contenedor:** causa distinta — confirmado contra el
  código real que el backend **nunca** valida este campo contra ningún
  catálogo (es 100% libre), así que nunca iba a mostrar error ahí. El
  "no prellenado" era el mismo problema de comparación exacta del
  `<select>` vs. el texto crudo — ahora se canoniza cuando el texto
  coincide con el catálogo, sin agregar ningún error nuevo (porque el
  backend tampoco lo exige).
- Para esto se agregaron `clasesContenedor`/`tiposVehiculo` a un
  catálogo de canonización aparte (antes solo tenía los 4 campos que
  el backend sí valida) — existen **solo para canonizar, nunca para
  marcar error**, distinción dejada comentada en el código.
- **Tipo de vehículo:** ahora `<select>` estricto (value = código,
  label = descripción) — se quitó el `<datalist>` huérfano. El
  formulario manual sigue con datalist, sin tocar.
- **Consistencia visual:** los selects de la grilla no tenían ningún
  estilo propio (`.biblioteca-preview-tabla-wrap` solo estilaba
  `input`, nunca `select`) — se agregó `select` a esa misma regla en
  Tarifas Plaza. **De paso, se encontró y corrigió el mismo hueco en
  la grilla de Servicios** (sus selects de Unidad/Activo tampoco tenían
  estilo propio) — confirmado con Sara que se deja así, no se
  revierte.

Compiló bien. Sin commitear.

**⚠️ Bug en investigación activa (sin resolver todavía) — Línea de
negocio/Clase de viaje no preseleccionan en el `<select>` de la
grilla:**

Historial de la investigación, para no repetir pasos ya descartados:
1. **Hipótesis "catálogos vacíos" — descartada con evidencia.** Se
   agregó un contador de diagnóstico temporal arriba de la grilla
   (`.tarifas-preview-diagnostico`, a quitar una vez se confirme la
   causa) — mostró datos reales en los 6 catálogos (6 líneas de
   negocio, 4 clases de viaje, 3804 municipios, 8 rangos de peso, 5
   clases de contenedor, 11 tipos de vehículo). El primer contador
   tenía además un bug propio (mezclaba el catálogo "en vivo" del
   formulario con la "foto fija" que usa la validación) — corregido
   para mostrar ambos por separado; los dos salieron **iguales**,
   descartando también que la foto fija esté desactualizada.
2. **Hipótesis "catálogo de SAP cambió, texto de prueba obsoleto" —
   descartada con evidencia.** Sara confirmó en el formulario manual
   que "Oferta de Expedición Puerto a interior" y "Contenedor" existen
   tal cual, carácter por carácter, en las opciones reales de Línea de
   negocio/Clase de viaje.
3. **Confirmado que el match SÍ ocurre internamente, sin error:** la
   columna "Problemas" de la fila 109 solo muestra errores de RANGO
   PESO y VIGENCIA — nada de Línea de negocio ni Clase de viaje. Esto
   confirma que el código ya canonizó esos 2 valores correctamente — el
   problema es **puramente visual**, en cómo el `<select>` refleja un
   valor ya correcto.
4. **Primer experimento (retrasar la creación de la fila con
   `setTimeout`) — no funcionó, revertido.** Hipótesis: timing de
   Angular con `<select>`+`*ngFor` de opciones async (bug conocido,
   incluso en v14) — el select intenta fijar su valor antes de que sus
   `<option>` ya estén insertadas. El experimento solo retrasaba
   CUÁNDO se crea la fila, seguía siendo un solo intento simultáneo de
   select+opciones — no probaba la hipótesis correctamente.
5. **Segundo experimento (reasignar `previewPlaza` una segunda vez,
   después de que la fila ya se dibujó con sus opciones puestas, para
   forzar que Angular repase los bindings) — tampoco funcionó,
   revertido.** El código sí canoniza bien el valor (confirmado por
   "Problemas" sin error ahí) — el problema está en algo entre el valor
   ya canonizado y cómo el `<select>` lo refleja visualmente.
6. **Tercer experimento (✅ confirmado por Sara en pantalla real —
   funcionó, bug resuelto).** Explicación
   del peer de por qué los 2 experimentos anteriores no probaban bien
   la hipótesis: Angular no vuelve a aplicar un `[value]` si el dato no
   cambió desde la última vez que lo intentó poner — compara contra lo
   último que ÉL MISMO intentó escribir, no contra lo que de verdad hay
   en el DOM. En el primer dibujado, Angular "manda" el valor correcto
   pero el navegador lo ignora porque las `<option>` async todavía no
   estaban listas — Angular da eso por "hecho" y nunca lo reintenta,
   ni siquiera si se le obliga a repasar bindings (los 2 experimentos
   anteriores), porque el valor en sí nunca cambió entre intentos.
   **Arreglo probado:** dibujar primero la fila con Línea de
   negocio/Clase de viaje/Rango de peso/Clase de contenedor/Tipo de
   vehículo (los 5 `<select>`) **en blanco**, y un instante después
   reemplazarlos por los valores reales ya canonizados — esto sí
   obliga a Angular a notar el cambio y re-tocar el `<select>`, ya con
   las opciones puestas. Costo aceptado por Sara: un parpadeo breve en
   esos 5 campos antes de llenarse (Origen/Destino/Valor/Vigencia/
   Detalles no se ven afectados, nunca estuvieron en blanco).

**Nuevo criterio agregado durante esta investigación:** Clase de
contenedor **sí debe marcar error** en la grilla cuando no coincide con
el catálogo — aunque el backend real nunca la rechace (sigue siendo
libre ahí). Es una decisión consciente de hacer la grilla más estricta
que el backend para este campo específico, confirmada con Sara. **Ya
aplicado y compilado en el sandbox.**

**Mismo criterio extendido a Tipo de vehículo — luego revertido.**
Primero se aplicó el mismo "marcar error si no coincide" a Tipo de
vehículo (igual que Línea de negocio/Clase de viaje/Origen/Destino,
que ya lo tenían) y se confirmó compilando. **Pero Sara detectó un
problema real al probarlo:** el catálogo oficial de SAP
(`tblSAPTiposVehiculoTarifario`) no tiene todos los códigos que
realmente necesita (ej. "3S2", "2L3") — con la validación activa,
filas con esos códigos válidos de negocio quedaban marcadas como error
sin serlo. **Decidido:** no hardcodear esos códigos de nuevo (eso es
justo lo que se acababa de quitar en HU-B5), pero tampoco validar Tipo
de vehículo contra el catálogo — se revierte esa parte a como estaba
(canoniza si hay match, nunca marca error). El `<select>` **se queda
cerrado** (solo opciones del catálogo visibles, eso no cambia) — ojo:
esto significa que códigos reales que todavía no estén en el catálogo
de SAP (como 3S2/2L3) **no se pueden elegir** en ese desplegable
todavía; el arreglo de fondo es que alguien actualice el catálogo real
en `dbSolucionesTI`, no algo que se resuelva en este código. Clase de
contenedor **no se revierte**, se queda marcando error. Con esto, 5 de
los 6 campos de catálogo (Línea de negocio, Clase de viaje, Origen,
Destino, Clase de contenedor) marcan error; Tipo de vehículo vuelve a
ser el único que no.

---

### HU-8 — Mostrar el código en el desplegable de Tipo de vehículo, no solo el nombre

**Como** usuario que revisa la vista previa de Tarifas Plaza, **quiero**
ver el código del tipo de vehículo (ej. "3S3") en el desplegable, no
solo el nombre descriptivo ("Tractomula"), **para** reconocerlo igual
que en el resto de la aplicación.

**Archivo:** `tarifas-plaza-page.component.html:228` (la grilla):
```html
<!-- como está hoy: -->
<option *ngFor="let tv of tiposVehiculo" [value]="tv.codigo">{{ tv.descripcion }}</option>
```

**Mismo patrón que ya usa Reglas Solo-SICETAC**
(`reglas-solo-sicetac-page.component.html:48`):
```html
<option *ngFor="let tipo of tiposVehiculo" [value]="tipo.codigo">{{ tipo.codigo }} · {{ tipo.descripcion }}</option>
```

**Arreglo:** cambiar el texto del `<option>` de la grilla de
`{{ tv.descripcion }}` a `{{ tv.codigo }} · {{ tv.descripcion }}` (ej.
"3S3 · Tractomula"), igual que ya se ve en Reglas Solo-SICETAC. El
`[value]` (lo que realmente se guarda) no cambia, sigue siendo
`tv.codigo`.

**Criterios de aceptación:**
- El desplegable de Tipo de vehículo en la grilla muestra
  "{código} · {descripción}" (ej. "3S3 · Tractomula"), no solo la
  descripción.
- El valor guardado sigue siendo el código, sin cambios.

**✅ Confirmado por Sara en pantalla real** (el primer intento no se
veía por caché del navegador, no por el código — resuelto con un
refresco forzado).

---

### HU-9 — Tarifas Plaza: mostrar y dejar confirmar el departamento de las ciudades con nombre repetido

**Como** Administrador de Tarifas, **quiero** que en la vista previa de
la carga masiva de Tarifas Plaza, antes de confirmar, me aparezca qué
municipio (y de qué departamento) se tomó cuando la ciudad del Excel
tiene un nombre que existe en más de un departamento, **para**
confirmarlo o cambiarlo antes de que la tarifa quede en el municipio
equivocado.

**Decisión de Sara (2026-10-12):** que en la vista previa, antes de
confirmar, aparezca abajo cuál se usó y de qué departamento, para que
el usuario confirme. (Era el primer "hallazgo secundario" de la
sección Tarifas Plaza.)

**Causa raíz:** tanto la vista previa como el importador del backend
toman el primer municipio con ese nombre (Colombia primero) sin avisar
— `municipioCoincidente()` en `tarifas-plaza-excel.util.ts:85-89` y su
gemelo en `clsEfTarifasPlazaRepository.cs:64-66`. Ejemplo: "ARMENIA"
existe en Antioquia y en Quindío; cuál gana depende del orden en el
catálogo.

**Depende de HU-B21** (backend): sin ella, aunque el usuario elija el
departamento en pantalla, el importador del backend volvería a escoger
el primero.

**Cambio en `tarifas-plaza-excel.util.ts`:**
- `municipioCoincidente()` (líneas 85-89) debe poder devolver **todos**
  los candidatos con ese nombre, no solo el primero, para saber cuándo
  hay ambigüedad. El desempate por defecto (Colombia primero) se
  conserva como valor preseleccionado.
- En `validarValoresFilaPlaza()` (línea 179), cuando el nombre de
  origen o destino coincide con **2 o más municipios**, guardar en la
  fila el municipio elegido **con su departamento** y la lista de
  departamentos posibles.
- `crearArchivoCargaPlaza()` (línea 284), que reconstruye el Excel
  antes de enviarlo a `importar-excel`, agrega las columnas
  `DEPARTAMENTO ORIGEN` y `DEPARTAMENTO DESTINO` con el departamento
  elegido (las que el backend entiende por HU-B21).

**Cambio en la vista previa** (pantalla de Tarifas Plaza, debajo de la
grilla, antes del botón de confirmar): un recuadro **"Ciudades con el
mismo nombre en varios departamentos"** — una línea por cada caso:

> Fila 12 · Origen · **ARMENIA** → se usará **Armenia (Quindío)** ▾
> *(también existe en: Antioquia)*

El ▾ es un desplegable con los departamentos posibles, preseleccionado
con el que el sistema eligió. Si no hay ningún caso, el recuadro no
aparece.

**Criterios de aceptación:**
- Si el Excel trae una ciudad cuyo nombre existe en 2 o más
  departamentos, debajo de la grilla aparece cuál se usará y de qué
  departamento, y en qué otros existe.
- El usuario puede cambiar el departamento antes de confirmar.
- La tarifa queda guardada en el municipio del departamento elegido
  (junto con HU-B21).
- Si ninguna ciudad es ambigua, la vista previa se ve igual que hoy.

---

## Cubicador

### HU-1 — Reemplazar el texto descriptivo fijo por un ícono de información, igual que Bandeja/Biblioteca

**Como** usuario del Cubicador, **quiero** que la explicación de qué es
esta vista aparezca en un ícono de información, **para** no tener un
párrafo fijo ocupando espacio bajo el título.

**Archivo:** `cubicador-page.component.html:2-5`:
```html
<!-- como está hoy: -->
<header class="cabina-page-header">
  <h1>Cubicador 3D</h1>
  <p>Plan de cargue: registra el vehículo y la carga, optimiza la distribución y revisa la gráfica 3D interactiva.</p>
</header>
```

**Implementación de referencia:**
```html
<header class="cabina-page-header">
  <h1>
    Cubicador 3D
    <button type="button" class="cabina-info-btn"
      matTooltip="Plan de cargue: registra el vehículo y la carga, optimiza la distribución y revisa la gráfica 3D interactiva."
      matTooltipClass="cabina-tooltip" matTooltipPosition="right"
      aria-label="Más información sobre esta vista">
      <mat-icon inline="true">info_outline</mat-icon>
    </button>
  </h1>
</header>
```

**Criterios de aceptación:**
- El `<h1>Cubicador 3D</h1>` ya no tiene un párrafo descriptivo fijo
  debajo.
- Junto al título, un ícono de información muestra el mismo texto
  exacto al pasar el mouse.

**Validado en el sandbox:** texto del tooltip idéntico. Se copió el
bloque CSS `.cabina-info-btn` (este componente no lo tenía) y se
agregó `display:flex;gap:8px` a `.cabina-page-header h1`. Se quitó
`.cabina-page-header p` del CSS por quedar huérfana. Compiló bien, sin
commitear.

---

## Tablero

> **De cara a qué rol:** el Tablero es para **AdminTarifas** y **Admin**
> (`clsMetricasController.cs`: `[Authorize(Roles = "LiderProceso,AdminTarifas,Admin")]`).
> `LiderProceso` existe en el código pero no es uno de los 3 roles que
> se entregan al cliente (ver `MAPA-FUNCIONAL-NEXO.md`) — de los que sí
> se entregan, **Comercial no tiene acceso a esta vista**. Todas las HU
> de esta sección (orden/colores, filtro de fecha, tiempo de costeo,
> métricas nuevas) son mejoras para lo que ve AdminTarifas.

### HU-1 — Reemplazar el texto descriptivo fijo por un ícono de información, igual que Bandeja/Biblioteca

**Como** usuario del Tablero, **quiero** que la explicación de qué es
esta vista aparezca en un ícono de información, **para** no tener un
párrafo fijo ocupando espacio bajo el título.

**Archivo:** `tablero-page.component.html:2-5`:
```html
<!-- como está hoy: -->
<header class="cabina-page-header">
  <h1>Tablero de Estado</h1>
  <p>Cumplimiento de SLA y estado de la cola.</p>
</header>
```

**Implementación de referencia:**
```html
<header class="cabina-page-header">
  <h1>
    Tablero de Estado
    <button type="button" class="cabina-info-btn"
      matTooltip="Cumplimiento de SLA y estado de la cola."
      matTooltipClass="cabina-tooltip" matTooltipPosition="right"
      aria-label="Más información sobre esta vista">
      <mat-icon inline="true">info_outline</mat-icon>
    </button>
  </h1>
</header>
```

**Nota aparte, no parte de este cambio:** el texto actual
("Cumplimiento de SLA y estado de la cola") no menciona los otros 2
bloques que sí tiene la pantalla (KPIs del piloto, monitoreo de
tarifas SICE-TAC) — se mueve tal cual al tooltip, sin corregir el
contenido, porque no se pidió. Si Sara quiere actualizarlo para que
mencione los 3 bloques, es un pedido aparte.

**Validado en el sandbox:** texto del tooltip idéntico, sin corregir
el contenido (confirmado explícitamente). Mismo patrón de CSS que
Cubicador: se copió `.cabina-info-btn` (no lo tenía), se agregó
`display:flex;gap:8px` a `.cabina-page-header h1`, y se quitó
`.cabina-page-header p` por quedar huérfana. Compiló bien, sin
commitear.

**Criterios de aceptación:**
- El `<h1>Tablero de Estado</h1>` ya no tiene un párrafo descriptivo
  fijo debajo.
- Junto al título, un ícono de información muestra el mismo texto
  exacto al pasar el mouse.

---

### HU-2 — Dividir el Tablero en 2 subvistas: Productividad y SICE-TAC

**Como** usuario del Tablero, **quiero** que los KPIs de productividad
y el monitoreo de SICE-TAC vivan en pestañas separadas, **para** no
tener que desplazarme por los tres bloques juntos en una sola pantalla
cuando solo me interesa uno de los dos temas.

**Cómo está hoy:** una sola pantalla con 3 bloques seguidos
(`tablero-page.component.html`):
1. Cumplimiento de SLA y estado de la cola (`kpis`, líneas 21-29, sin
   título propio).
2. "KPIs del Piloto" (líneas 31-56, con su propio filtro de fechas
   desde/hasta).
3. "Monitoreo de Tarifas SICE-TAC" (líneas 58-65, iframe embebido a un
   dashboard externo, `environment.tableroKpisUrl`).

**Agrupación pedida:**
- **Tablero de Productividad** = bloques 1 + 2 (SLA/cola + KPIs del
  Piloto).
- **Tablero SICE-TAC** = bloque 3 (el iframe de monitoreo).

**Implementación de referencia — mismo patrón de pestañas que ya usa
Biblioteca** (`biblioteca-page.component.ts:181,268,309-330`:
`tabs`/`tab`/`cambiarTab()`/`onTabKeydown()`, con roles ARIA
`role="tablist"`/`role="tab"`/`role="tabpanel"` y navegación por
flechas):
```ts
export type TabTablero = 'productividad' | 'sicetac';
const TABS: Array<{ id: TabTablero; label: string }> = [
  { id: 'productividad', label: 'Productividad' },
  { id: 'sicetac', label: 'SICE-TAC' },
];
```
```html
<div class="biblioteca-tabs" role="tablist" aria-label="Vistas del Tablero">
  <button *ngFor="let t of tabs" id="tab-{{ t.id }}" type="button" role="tab" [attr.aria-selected]="tab === t.id" [attr.aria-controls]="'panel-' + t.id" [attr.tabindex]="tab === t.id ? 0 : -1" [class.biblioteca-tab--active]="tab === t.id" class="biblioteca-tab" (click)="cambiarTab(t.id)" (keydown)="onTabKeydown($event)">{{ t.label }}</button>
</div>

<section *ngIf="tab === 'productividad'" id="panel-productividad" role="tabpanel" aria-labelledby="tab-productividad">
  <!-- bloques 1 y 2, tal cual existen hoy -->
</section>

<section *ngIf="tab === 'sicetac'" id="panel-sicetac" role="tabpanel" aria-labelledby="tab-sicetac">
  <!-- bloque 3, tal cual existe hoy -->
</section>
```
Nota: no hace falta lazy-load por pestaña como sí tiene Biblioteca —
`cargar()` y `cargarPiloto()` (bloques 1 y 2) ya se disparan juntos en
`ngOnInit()` hoy, y el iframe de SICE-TAC no hace ninguna llamada a
API (`kpisSicetacUrl` es una URL fija de configuración) — se puede
dejar todo cargando igual que hoy, solo cambia qué se **muestra** según
la pestaña activa.

**Criterios de aceptación:**
- 2 pestañas: "Productividad" y "SICE-TAC", mismo patrón visual/
  accesibilidad que las pestañas de Biblioteca.
- "Productividad" muestra el bloque de SLA/cola y "KPIs del Piloto"
  (con su filtro de fechas), igual que se ven hoy.
- "SICE-TAC" muestra solo el iframe de monitoreo.
- No se pierde ninguna funcionalidad existente (filtro de fechas del
  piloto, botón "Abrir en pestaña nueva" del iframe, reintentos ante
  error) — solo cambia cómo se agrupan visualmente.

---

### HU-3 — Reordenar, recolorear y filtrar por fecha el bloque "Cumplimiento SLA / cola"

**Como** usuario del Tablero, **quiero** ver las 6 tarjetas de este
bloque en un orden fijo, con colores que reflejen su urgencia, y poder
filtrarlas por fecha junto con "KPIs del Piloto", **para** leer la cola
operativa de un vistazo y no un acumulado ciego desde el inicio del
sistema.

**Parte A — orden y colores (✅ implementado en el sandbox, 2026-10-12,
con datos reales, no mock):**
- Nuevo orden: Total solicitudes, Pendientes, En costeo, Entregadas,
  Descartadas, Cumplimiento SLA.
- Pendientes → tone `bad` (rojo), se quitó el hint "por costear".
- En costeo → tone nuevo `info` (azul, `#1565c0`), se quitó el hint "en
  proceso".
- Entregadas (`ok`/verde), Descartadas (`neutral`/negro) y Cumplimiento
  SLA (tone dinámico, con su hint) sin cambios.
- `construirKpis()` (`tablero-page.component.ts:114-124`) reescrito en
  ese orden; tipo `Tono` (línea 11) agrega `'info'`; CSS nuevo
  `.tablero-kpi__value--info { color: #1565c0; }`
  (`tablero-page.component.css`, junto a `--ok`/`--warn`/`--bad`/`--neutral`).
- Confirmado compilando sin errores.

**Parte B — filtro de fecha compartido (pendiente, necesita backend):**
Antes se había diseñado como un filtro **independiente** por bloque
(ver versión anterior de esta HU) — Sara corrigió eso: debe ser **un
solo filtro Desde/Hasta**, que alimente a la vez el bloque de
Cumplimiento SLA/cola **y** "KPIs del Piloto" (no 2 controles de fecha
separados en la misma pantalla).

**Cambio importante respecto a la primera versión:** Cumplimiento SLA
**ahora sí entra al filtro** (antes se había decidido dejarlo siempre
sin filtro) — Sara lo confirmó explícitamente al dar la especificación
final. Se filtra por `Cotizacion.EntregadaAt` (cumplimiento de lo
entregado en ese rango), no por `Solicitud.CreatedAt` como las otras 5
tarjetas.

**Depende de que se actualice HU-B12** (`2026-10-09_HU-BACKEND.md`, sección
Tablero, ya corregida con este mismo cambio) — el endpoint
`GET /api/metricas/cumplimiento` necesita el filtro de fecha, con
Cumplimiento SLA filtrado por `EntregadaAt` y las otras 5 tarjetas por
`CreatedAt`.

**Implementación de referencia:** un solo par de campos
`desdeInputTablero`/`hastaInputTablero` (en vez de
`desdeInputPiloto`/`hastaInputPiloto` separados), que al cambiar
disparan **las 2 cargas** (`cargar()` y `cargarPiloto()`) con el mismo
rango. Un solo bloque de filtro en el HTML (reutilizando el que ya
existe para Piloto, líneas 41-49), ubicado donde tenga sentido para que
se vea que aplica a ambos bloques — por ejemplo, arriba de las 6
tarjetas, antes de "KPIs del Piloto".

**Criterios de aceptación:**
- Un solo control Desde/Hasta en la pantalla, no dos.
- Al cambiar el rango, se actualizan **ambos** bloques a la vez.
- Cumplimiento SLA cambia con el filtro (filtrado por `EntregadaAt`);
  las otras 5 tarjetas del bloque se filtran por `CreatedAt` de la
  solicitud.
- Sin fechas (filtro vacío), ambos bloques muestran el histórico
  completo, igual que hoy.

---

### HU-4 — Redefinir "Tiempo de costeo del pliego": general + por tamaño, y tiempo real para pliegos con intervención manual

**Como** usuario del Tablero, **quiero** que "Tiempo de costeo del
pliego" refleje el tiempo real de principio a fin (no solo lo que tardó
el motor automático), y poder verlo general o segmentado por tamaño de
pliego, **para** tener una métrica que de verdad represente qué tan
rápido se está respondiendo.

**Redefinición del tiempo, por pliego (confirmada por Sara):**
- Si el pliego se auto-costeó **100% solo** (saga `Completado`, sin
  ninguna línea que haya requerido Admin Tarifas): tiempo = como se
  mide hoy (`TerminadoEn - SolicitadoEn` de la saga de auto-costeo).
- Si el pliego **requirió Admin Tarifas** (saga `Parcial`, alguna línea
  quedó en manual): tiempo = desde que se origina la solicitud hasta
  `Cotizacion.EntregadaAt` real — el ciclo completo, incluyendo el
  trabajo humano. (Depende de **HU-B8**: mientras no se cierre el hueco
  de "se puede entregar con líneas pendientes", este tiempo podría
  reflejar una entrega parcial, no el pliego 100% resuelto — limitación
  conocida y aceptada por ahora.)

Con esos tiempos (uno por pliego, de cualquiera de los 2 orígenes) se
calcula **un solo promedio y un solo % bajo 10 minutos**, sobre la
distribución combinada — no 2 números separados por tipo de pliego.

**Segmentación por tamaño (además del número general):** filtro
dinámico con 4 opciones — **Todos** (el general de arriba), **1-10
líneas**, **11-100 líneas**, **+100 líneas** — que recalculan el mismo
promedio/mediana/%bajo10min pero solo sobre los pliegos de ese tamaño
(tamaño = `TotalLineas` de la saga). Cambiar de opción actualiza los
resultados al instante.

**No es solo frontend** — depende de un cambio de backend real (ver
**HU-B13** en `2026-10-09_HU-BACKEND.md`, sección Tablero): hay que cruzar la saga
de auto-costeo con las cotizaciones entregadas, y agregar el filtro de
tamaño al query. Se documenta aquí la parte de pantalla; la lógica de
cálculo vive en el backend.

**Criterios de aceptación:**
- "Tiempo de costeo del pliego" y "Pliegos bajo 10 minutos" reflejan el
  tiempo híbrido (auto o hasta `EntregadaAt` según corresponda), no solo
  el tiempo del motor automático.
- Selector con 4 opciones (Todos/1-10/11-100/+100) que cambia los 2
  números de inmediato, sin recargar la pantalla completa.
- "Todos" sigue siendo el número principal/destacado; los 3 rangos son
  una vista adicional, no reemplazan el general.

---

### HU-5 — ~~Propuesta: "Antigüedad de pendientes" y "Evolución semanal"~~ (DESCARTADA — no implementar)

> **❌ Descartada por Sara (2026-10-12): "no se aplican".** Se conserva
> solo como registro de lo que se evaluó y prototipó (y ya se revirtió
> del sandbox).

**Como** usuario del Tablero, **quiero** ver 2 indicadores adicionales
que hoy no existen, **para** completar lo que el bloque de SLA/cola no
muestra: qué tan vencido está lo que sigue sin resolver, y si el
desempeño mejora o empeora con el tiempo.

**1. Antigüedad de solicitudes pendientes** (gráfico de barras): de las
solicitudes Pendientes/En costeo AHORA MISMO, cuántas llevan 0-2 días,
3-7 días, o +7 días esperando. Completa el hueco de Cumplimiento SLA
(que solo mira lo ya entregado, nunca lo que ya se está venciendo sin
resolver).

**2. Evolución semanal** (línea de tiempo): Cumplimiento SLA % y Tiempo
de costeo promedio, semana a semana durante el piloto — para ver la
tendencia, no solo el número total acumulado.

**Estado:** se prototipó en el sandbox con **datos falsos** (mock, sin
ningún endpoint real) usando `chart.js`/`ng2-charts` (ya están en
`package.json`, solo faltaba importar `NgChartsModule` en
`cabina-fletes.module.ts`) — gráfico de barras y de línea con doble eje
Y (%/min), cada tarjeta con un badge "Propuesta — datos de ejemplo, no
reales". **Sara pidió revertirlo** (2026-10-12) — "después miramos si
los aplicamos". Queda documentado como propuesta pendiente de decisión,
no como HU aprobada para construir.

**Si se decide seguir adelante, falta por definir:**
- "Antigüedad de pendientes": de dónde sale el corte de 0-2/3-7/+7 días
  (¿fijo o configurable?), y si cuenta por solicitud o por línea.
- "Evolución semanal": necesita un endpoint nuevo de backend que
  agrupe Cumplimiento SLA y tiempo de costeo por semana — hoy todo se
  calcula como un solo agregado sobre el rango, no hay nada agrupado
  por periodo.
