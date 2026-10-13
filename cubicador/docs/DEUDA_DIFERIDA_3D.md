# Deuda diferida del motor 3D

**Escrito en S5-HU-17.6.** La reestructuración del Cubicador dejó el motor 3D
intacto a propósito: tiene 33 reglas de negocio portadas de `cabinaFletes` y 27
casos dorados de paridad contra el motor C#, y tocarlo habría puesto en riesgo
esa compuerta en medio de un sprint que ya cambiaba el motor de base, el
catálogo, el contrato y los CRUD.

Esto es lo que quedó pendiente por esa decisión, con lo que cuesta cada punto y
qué hay que medir antes de moverlo. **No es una lista de deseos: es lo que hoy
se sostiene con un diccionario y una convención.**

---

## 1. Hay dos catálogos de vehículos

| | Catálogo de selección | Catálogo del 3D |
|---|---|---|
| Tabla | `dbo_cubicador.vehiculos` | `dbo_cubicador.vehiculos_cubicacion` |
| Quién lo administra | el equipo de Datos, más los CRUD de S5-HU-17.5 | se siembra por código, editable por API |
| Identidad | `CodigoVehiculo` (`2L2`, `C2`, `C3`, `2S2`, `3S3`) | el **nombre** (`"Turbo con estacas"`) |
| Qué guarda | medidas útiles en cm, capacidad por carrocería | medidas internas en mm, tara, puerta y ejes |
| Quién lo usa | el motor de selección de flota | el motor 3D |

Las medidas coinciden hoy porque las dos se corrigieron contra el mismo Excel de
Operaciones. **Nada garantiza que sigan coincidiendo**: un administrador puede
cambiar el largo de un vehículo por el CRUD de catálogo y el preset del 3D
seguirá diciendo lo de antes. El plan 3D aceptaría o rechazaría carga que el
otro motor ya decidió.

**Costo de unificarlos.** El 3D necesita tres datos que el catálogo no tiene:
tara, dimensiones de puerta y posición de ejes. Unificar significa decidir de
dónde salen —ver el punto 3— o aceptar que el 3D opere sin ellos. También hay
que migrar `planes_cargue.vehiculo_id`, que apunta a la tabla vieja.

**Qué medir antes.** Cuántos planes de cargue existentes apuntan a presets que
no tienen contraparte canónica (cama baja y los tres contenedores no la tienen).

---

## 2. El diccionario de compatibilidad

`application/plan_cargue/desde_linea.py` traduce el código canónico al nombre
del preset. Son dos diccionarios: uno por código y otro para las combinaciones
del `3S3`, que cae en un preset distinto según la carrocería porque su capacidad
cambia —30.000 kg con furgón, 34.000 con plataforma—.

**Es deuda, no diseño.** Existe solo porque existen los dos catálogos del punto
1: se retira el día que se unifiquen, no antes. Mientras tanto es el único lugar
donde se cruzan, y tiene pruebas que verifican que los presets declarados
existan y que sus capacidades coincidan con las del catálogo.

**Riesgo mientras viva.** Un vehículo nuevo creado por el CRUD no estará en el
diccionario: el puente lo rechaza con `VEHICULO_SIN_PRESET`, que es ruidoso y
correcto, pero significa que agregar una configuración al catálogo exige también
un cambio de código.

---

## 3. Qué pasa con puerta y ejes

El catálogo de selección no tiene dimensiones de puerta ni posición de ejes, y
el motor de selección no los usa: S5-HU-17.3 los dejó **fuera de alcance** por
falta de datos. El motor 3D sí los usa, y **cuatro de los 26 casos dorados
dependen de ellos**:

| Caso | De qué depende |
|---|---|
| 20, 21 y 22 | Balance de ejes — necesitan `ejes[]` con posición y capacidad |
| 23 | Puerta — necesita `puerta_ancho_mm` y `puerta_alto_mm` |

El caso **25 sobrevive** aunque se quiten las dimensiones: depende de la
*posición* de la puerta (trasera, frontal, lateral), no de su tamaño.

Hay tres caminos, y la decisión es de negocio, no técnica:

1. **Conseguir los datos.** Operaciones tendría que levantarlos por
   configuración. Es lo único que deja los cuatro casos como están.
2. **Declararlos fuera de alcance también en el 3D.** Los cuatro casos dorados
   se retiran de la compuerta y se documenta que el motor dejó de validar
   puerta y ejes. La paridad baja de 26 a 22 casos.
3. **Dejar los presets como están** y aceptar que esos tres campos son valores
   heredados sin fuente, que el 3D usa y nadie mantiene. Es el estado actual.

> La `Minimula 2S2` ya está en el caso 3 de forma visible: se agregó en
> S5-HU-17 con tara en 0 y **lista de ejes vacía**, porque el Excel no los trae.
> El motor lo soporta —no estima peso por eje cuando no hay ejes— pero esa
> configuración no pasa por los casos 20 a 22.

---

## 4. Re-evidenciar la compuerta de paridad si esos casos cambian

Los 27 casos de `tests/paridad/` comparan contra la **salida real** del motor
C#, no contra valores transcritos a mano. Si se toma el camino 1 o el 2 del
punto anterior, esas salidas cambian y la evidencia hay que volver a generarla
del lado C#, no editarla acá.

**Qué significa en la práctica.** Cualquiera de los dos caminos exige:

1. correr los casos afectados contra `cabinaFletes` con los datos nuevos;
2. regenerar el archivo de evidencia de `tests/paridad/`;
3. dejar escrito en `DECISIONES_TECNICAS.md` qué cambió y por qué, porque la
   compuerta de liberación cita un número de casos.

Editar la evidencia sin regenerarla convertiría la compuerta en una
tautología: estaría comparando el motor contra sí mismo.

---

## Lo que **no** es deuda

Para que no se confunda con lo de arriba:

- **El motor 3D sigue siendo el que empaca.** El motor de selección decide
  cuántos vehículos y de qué tipo; el 3D arma el plan de cargue dentro de cada
  uno. No se superponen.
- **DBLF no es un empacador nuevo.** Se agregó al registro de
  `optimizadores.py` reusando el mecanismo de puntos extremos de FFD y BFD y
  sus validadores. Ver `DECISIONES_TECNICAS.md` #13.
- **Los presets ya se corrigieron** contra el Excel en S5-HU-17: cinco
  configuraciones con medidas reales más la Minimula que faltaba. Lo que queda
  es el nombre `"Tractocamion furgon 13.6 m"`, desfasado —son 12,2 m— que no se
  renombró porque la siembra es por nombre y crearía duplicados. El renombre va
  por el CRUD.
