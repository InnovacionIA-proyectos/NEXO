# #304 — Qué deben hacer el front y el back de NEXO

Tres correcciones de borde del motor de selección del Cubicador. **Ninguna
cambia la forma del contrato**: el `openapi.json` quedó idéntico, no se agregan
ni se quitan campos, y las peticiones que hoy funcionan siguen funcionando.

Lo que sí cambia es **cuándo rechaza** y **cómo clasifica** algunos casos.

| Qué | A quién afecta |
|---|---|
| Tope de 200 posiciones por petición | front |
| Piezas sobreanchas se clasifican distinto | front |
| Una posición sin peso ya no se inventa 1 kg | front y NEXO |

---

## Front

### 1. Textos por motivo — OBLIGATORIO revisarlos

El umbral de extradimensión pasó de `1220 × 260 × 300` a **`1220 × 240 × 260`**,
que son los máximos del vehículo más grande que no es extradimensionado.

Eso mueve una franja de piezas de un motivo a otro:

| Pieza | Antes | Ahora |
|---|---|---|
| 241–260 cm de **ancho** | `SIN_CANDIDATOS_DIMENSIONALES` | **`EXTRADIMENSIONADO`** |
| 261–300 cm de **alto** | `SIN_CANDIDATOS_DIMENSIONALES` | **`EXTRADIMENSIONADO`** |

El usuario pasa de leer *"no hay candidatos dimensionales"* a *"extradimensionado"*.
Es lo correcto —esa pieza necesita permiso especial— pero **si hay copys por
motivo, hay que revisarlos**.

La respuesta ahora trae también el exceso concreto por pieza:

```json
"extradimension": {
  "medidasEstandar": { "largoMaxCm": 1220, "anchoMaxCm": 240, "altoMaxCm": 260, "pesoMaxKg": 34000 },
  "piezas": [
    { "indicePosicion": 0, "codigoMercancia": "CAJAS_CARTON", "excesos": ["ancho 255.0 cm > 240.0 cm"] }
  ]
}
```

`excesos` es texto listo para mostrar. `indicePosicion` dice **cuál** de las
posiciones capturadas es la que excede — útil cuando hay varias del mismo tipo.

> **El 260 de alto es el alto útil de la carga**, o sea el espacio para la
> mercancía dentro del vehículo. No se confunde con los **4,40 m**
> reglamentarios, que son la altura total del vehículo desde el suelo hasta el
> tope e incluyen chasis y piso del remolque. Si en pantalla se explica el
> límite, conviene que diga "alto de la carga", no "alto del vehículo".

### 2. Hasta 25 camiones por recomendación — NUEVO

El tope pasó de **10 a 25** vehículos. Con 10 se rechazaban cargas resolubles:
600 piezas de 1 m necesitan 13.

Por la decisión de #217, **cada camión recomendado crea una línea de solicitud**
con su propia línea de cotización. Una captura grande ahora puede generar hasta
**25 líneas** donde antes el máximo eran 10.

NEXO no limita el número de líneas por solicitud, así que no hay cambio de API.
Es un tema de pantalla: la vista que agrupa por `grupoRutaId` tiene que seguir
siendo legible con 25 sub-filas.

### 3. Validar 200 posiciones antes de llamar — RECOMENDADO

Una petición con más de 200 posiciones ahora se rechaza:

```
HTTP 400  CUBICACION_DEMASIADO_GRANDE
"La solicitud trae 201 posiciones; el maximo es 200."
```

Son las mismas 200 que admite una línea de captura en NEXO, así que una captura
válida nunca las supera. Validarlo en el front evita gastar el viaje.

Siguen vigentes los otros dos topes: **10.000 unidades totales** (mismo código
de error) y **cantidad ≥ 1** por posición.

### 4. Confirmar que el peso que mandan es POR UNIDAD — IMPORTANTE

`pesoKg` de cada posición es **el peso de una sola unidad**, no el total de esa
posición. El motor hace `peso_total = pesoKg × cantidad`.

Si el front manda el total, **el motor lo multiplica otra vez por la cantidad**
y pide camiones de más. Con 100 cajas de 20 kg: mandar `2000` en vez de `20`
produce 200.000 kg y una flota absurda.

Está en el contrato de la captura (*"peso unitario"*) pero no estaba escrito en
el código de ninguno de los dos lados, así que vale la pena confirmarlo contra
lo que el formulario captura hoy.

### 5. Lo que NO cambia

- La forma del request y de la respuesta.
- `indicePosicion`, `codigoMercancia` y todo lo de S5-HU-17.7/17.8.
- Los demás motivos: `SUPERA_K_MAX`, `SIN_CARROCERIA_COMPATIBLE`,
  `BUSQUEDA_AGOTADA`, `SIN_CANDIDATOS_DIMENSIONALES`.

`SIN_CANDIDATOS_DIMENSIONALES` sigue existiendo, pero ahora se alcanza por
**capacidad** y no por medidas: por ejemplo carne de 32.000 kg, que solo viaja
refrigerada y ahí el vehículo más grande topa en 30.000.

---

## Back de NEXO

### 1. El adaptador no se toca

`clsCubicacionRapidaAdapter` sigue igual. Los códigos de error ya existían y la
forma del DTO no cambió.

### 2. Código de rechazo nuevo en el plan de cargue

`POST /api/planes-cargue/desde-linea/{lineaId}` puede devolver ahora
**`PESO_NO_DECLARADO`**.

Antes, si una posición no traía peso, el puente le ponía **1 kg**. El peso
decide la capacidad del vehículo: un kilo inventado mete en un Turbo lo que
necesitaba una Minimula, y el plan salía creíble pero equivocado. Es el mismo
fallback silencioso que S5-HU-17.6 eliminó para el tipo de mercancía, y este se
había quedado vivo.

El cero cuenta como no declarado.

**Qué revisar:** si NEXO mapea o muestra los códigos del Cubicador en ese
endpoint, agregar este. Si los propaga tal cual, no hay nada que hacer.

### 3. Dos fuentes de verdad para el peso — DECISIÓN PENDIENTE

| Dónde | Qué dice hoy |
|---|---|
| `CapturaRequests.clsCapturaPosicionRequest.PesoKg` | **opcional** (`decimal?`) |
| El formulario del front | **obligatorio** |
| El Cubicador | **requerido > 0** |

El front ya lo exige por su cuenta, así que en la práctica no debería llegar una
posición sin peso. Pero el contrato de NEXO lo declara opcional, y eso significa
que una línea cargada por otro camino —Excel, carga masiva, Cotifly— sí puede
quedar sin peso y después no generar plan de cargue.

**Hay que decidir:** o `PesoKg` pasa a `[Required]` en la captura, o se acepta
que el rechazo ocurra más tarde. Lo primero da el error donde está el error.

### 4. Desajuste de cantidades — DECISIÓN PENDIENTE

`clsCapturaPosicionRequest.Cantidad` admite `[Range(1, 100000)]`. El Cubicador
corta en **10.000 unidades totales** por petición.

Una posición de 100.000 unidades **se captura y se guarda**, y después el
Cubicador rechaza la recomendación entera con `CUBICACION_DEMASIADO_GRANDE`. El
error aparece lejos de donde se originó.

### 5. Documentar que `PesoKg` es por unidad — RECOMENDADO

`CapturaRequests.cs` no tiene un solo comentario en ese campo, mientras su
vecino `CodigoTipoMercancia` tiene seis líneas explicando por qué va el código.
Quien lea solo el código tiene que adivinar, y adivinar mal multiplica el peso
por la cantidad.

El contrato raíz sí lo dice: *"0–999999.99 (peso unitario)"*, y la regla de
consistencia lo confirma — *"el peso de la línea es `peso` si viene; si no, la
suma de `cantidad × pesoKg` de las posiciones"*.

---

## Cotifly

**Nada.** Hoy no llama al Cubicador directo y manda el tipo de mercancía en
`null`. Cuando construyan su cliente, lo hacen ya contra esto.

---

## Orden de despliegue

**1. Comprobar el umbral en base antes de desplegar.**

```sql
SELECT * FROM dbo_cubicador.parametros_cubicacion;
```

| Qué devuelve | Qué significa |
|---|---|
| **cero filas** | manda el valor por defecto del código, que ya quedó en 1220 × 240 × 260. **Nada que hacer** |
| una fila con 240 / 260 o menos | ya está alineada. Nada que hacer |
| una fila con **260 de ancho** o **300 de alto** | hay que corregirla, o el umbral viejo sigue en pie pese al despliegue |

Se verificó el 2026-10-19 contra `10.10.40.85/dbTarifario_Nexo_Pruebas`:
**cero filas**. Vale la pena repetir la consulta igual, porque la fila aparece
en cuanto alguien edita por `PUT /medidas-estandar` —un endpoint expuesto, que
además NEXO proxea— y esa fila **manda sobre el default**.

Si hubiera que corregirla:

```sql
UPDATE dbo_cubicador.parametros_cubicacion
   SET ancho_max_cm = 240.0, alto_max_cm = 260.0, updated_at = SYSDATETIMEOFFSET()
 WHERE id = 1 AND (ancho_max_cm > 240.0 OR alto_max_cm > 260.0);
```

**No insertar la fila si no existe.** Crearla pone una segunda fuente de verdad
por encima del default, y desde ahí cualquier cambio futuro en el código queda
tapado en silencio.

**2. Desplegar el Cubicador.**

**3. El front y NEXO no tienen despliegue obligatorio:** lo suyo es revisión de
textos y las dos decisiones pendientes.

No hay ventana de incompatibilidad. Una petición que hoy funciona sigue
funcionando después.

---

## Qué probar en QA

- [ ] Pieza de **245 × 245 × 255** → `EXTRADIMENSIONADO` con el exceso de ancho
      (antes: `SIN_CANDIDATOS_DIMENSIONALES`)
- [ ] **200 posiciones** → 200 OK · **201 posiciones** → 400
- [ ] Línea capturada con una posición sin peso → `PESO_NO_DECLARADO` al generar
      el plan de cargue, **no** un plan calculado con 1 kg
- [ ] `extradimension.medidasEstandar` devuelve **1220 / 240 / 260 / 34000**
- [ ] Una petición que hoy funciona sigue devolviendo lo mismo

---

## Lo que esto NO arregla

Siguen abiertos en [#304](https://github.com/InnovacionIA-proyectos/NEXO/issues/304)
los dos puntos de mayor impacto, que son el mismo trabajo:

- **La cota mínima de vehículos es optimista** y hace que el motor se rinda ante
  cargas que sabe resolver (`BUSQUEDA_AGOTADA` con 200 cubos de 1 m).
- **El empaque es cuadrático**: la línea L5 del pliego real con bultos de 5 kg
  (6.400 unidades, entrada legal) tarda **84 segundos** contra un timeout de 15.

Los dos se cierran resolviendo la carga homogénea con una rejilla en vez de
pieza por pieza. Es lo siguiente.

El detalle completo, con las mediciones y dónde revisar cada cosa, está en la
issue y en `cubicador/docs/DECISIONES_TECNICAS.md` #20.
