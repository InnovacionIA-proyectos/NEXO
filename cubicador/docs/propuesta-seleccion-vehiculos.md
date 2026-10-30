# Propuesta: motor de selección de tipo y cantidad de vehículos para carga empacada

**Empresa:** Eduardo Botero Soto S.A. (Botero Soto Soluciones Logísticas) **Área:** Innovación **Fecha:** 6 de octubre de 2026 **Estado:** propuesta de diseño, lista para prototipo

---

## 1\. Contexto

Hoy el cubicador del prototipo SICCE acomoda bultos dentro de un vehículo y calcula el porcentaje de llenado, pero la decisión de **qué vehículo y cuántos** se toma con un flujo manual de reglas que solo contempla dos resultados: un vehículo, o dos vehículos (una pareja "grande \+ sobrante" o dos iguales partiendo la carga a la mitad). Cuando la carga necesita tres o más vehículos, o cuando ninguna de las ramas produce un resultado, el flujo no tiene respuesta.

Además, la información que alimenta el proceso (qué mercancías existen, en qué carrocería van, qué vehículos hay y cuánto cargan) está en hojas de cálculo pensadas para personas, no para un programa, y va a cambiar con el tiempo: entran mercancías nuevas, vehículos nuevos y tipos de vehículo nuevos.

Esta propuesta define un motor de selección que:

1. Siempre produce una respuesta (una lista de vehículos o un "sin selección automática" con el motivo).  
2. Generaliza a N vehículos de tipos mixtos.  
3. Es liviano computacionalmente.  
4. No tiene ninguna regla de negocio escrita en el código: todo vive en tablas que operaciones mantiene.

### 1.1 Objetivo acordado

En orden de prioridad:

1. **La menor cantidad de vehículos posible.**  
2. Entre opciones con la misma cantidad, **la de mayor llenado promedio**.  
3. Cómputo liviano y diseño lo más simple posible.

---

## 2\. Decisiones tomadas en la sesión

| \# | Tema | Decisión |
| :---- | :---- | :---- |
| 1 | Compatibilidad entre mercancías | Se valida **primero**. Mercancías incompatibles (ej. oxidante \+ combustible) nunca viajan juntas, aunque compartan carrocería. |
| 2 | Agrupación por carrocería | Dentro de un grupo compatible, las líneas se agrupan por carrocería común. Si no comparten carrocería, se separan y cada grupo corre el proceso por su cuenta. Siempre salen tantos vehículos como grupos, mínimo. |
| 3 | Carrocerías por tipo de mercancía | Un tipo admite hasta **2 carrocerías** (principal y alternativa), en una tabla validable. La comparación "misma carrocería" es una intersección de conjuntos. |
| 4 | Validación dimensional | Se verifica que un bulto individual quepa en el vehículo en **cualquiera de las 6 rotaciones**. Vehículos donde no cabe se descartan. |
| 5 | Peso | Es un **filtro**: decide si un vehículo puede llevar la carga y cuántos bultos le caben como máximo. No forma parte del % de llenado reportado. |
| 6 | % de llenado | Se mide **solo por volumen** (volumen de los bultos acomodados sobre volumen útil del vehículo). |
| 7 | Banda 75–95 % | Deja de ser un filtro. El 75 % se conserva como **alerta** en el resultado para que el operador decida si consolida. |
| 8 | Sobrante | Cuando una carga no cabe en un vehículo, el sobrante se calcula con volumen **y** peso, y se acomoda en el siguiente vehículo. |
| 9 | Partición de la carga | Cuando se necesita más de un vehículo, la carga se reparte por **cantidad de bultos de cada línea**. Si la cantidad es impar, una parte lleva un bulto más. |
| 10 | Criterio de elección entre opciones con igual cantidad de vehículos | **Mayor llenado promedio** (promedio del % de llenado de los vehículos de la opción). Ver punto abierto en §9 sobre cargas pesadas. |
| 11 | Mercancías fuera de alcance | Graneles, combustibles, químicos a granel, vehículos como unidad y cargas de ingeniería especial se **rechazan**. Bobinas, rollos, vidrio en bastidor, cilindros y cargas con CG crítico van a **flujo especializado**. |

---

## 3\. Información disponible

### 3.1 Configuraciones de vehículo (hoja "Dimensiones y Peso por configuración para Cubicaje")

Medidas internas útiles, en metros. Volumen calculado a partir de ellas.

| Configuración | Largo | Ancho | Alto | Vol. útil | Peso máx. | Carrocerías |
| :---- | :---- | :---- | :---- | :---- | :---- | :---- |
| Turbo 2L2 | 4.5 | 2.2 | 2.3 | 22.8 m³ | 4 000 kg | Estacas, Furgón, Plancha |
| Sencillo C2 | 6.5 | 2.4 | 2.4 | 37.4 m³ | 8 500 kg | Estacas, Furgón, Plancha |
| Dobletroque C3 | 8.0 | 2.4 | 2.6 | 49.9 m³ | 17 000 kg | Estacas, Plancha |
| Minimula 2S2 | 12.2 | 2.4 | 2.6 | 76.1 m³ | 18 000 kg | Estacas, Furgón, Plancha |
| Tractomula 3S3 | 12.2 | 2.4 | 2.6 | 76.1 m³ | 34 000 kg | Estacas, Plancha |
| Tractomula Furgón 3S3 | 12.2 | 2.4 | 2.6 | 76.1 m³ | 30 000 kg | Furgón |

Observación clave: dentro de cada carrocería, la configuración más grande domina a las demás en las tres dimensiones, volumen y peso. Esto es lo que permite el algoritmo de §5 sin casos especiales. Minimula y las dos Tractomulas comparten espacio útil y solo difieren en peso máximo.

### 3.2 Matriz mercancía vs carrocería (hoja "Matriz cubicador")

31 tipos de mercancía con carrocería principal, alternativa condicionada, nivel de cubicabilidad y observaciones. Hoja adicional de alcance que define qué se excluye y qué va a flujo especializado.

### 3.3 Brechas detectadas

1. **Nombres de carrocería sin normalizar.** Aparecen "Plataforma", "Plancha", "Planchón", "Plataforma / planchón", "Furgón ventilado o refrigerado, según producto", "Furgón o contenedor seco". El motor necesita un catálogo cerrado.  
2. **Carrocerías sin vehículos.** Furgón refrigerado, ventilado, climatizado, contenedor seco y contenedor refrigerado no tienen ninguna configuración. Diez tipos de mercancía (farmacéuticos refrigerados, congelados, cárnicos, lácteos, flores, frutas, exportación contenerizada, etc.) hoy caerían en "sin selección".  
3. **Alternativas en texto libre.** "Si la operación lo permite", "según empaque", etc. Para el motor, la alternativa es válida o no lo es; la condición debe ser un flag que alguien marca al ingresar la carga.  
4. **Matriz de compatibilidad inexistente.** El primer filtro del flujo (paso 2\) depende de ella y no está en ninguno de los archivos.

---

## 4\. Modelo de datos

Principio: **todo es dato, el algoritmo es fijo.** El motor no conoce ningún nombre de mercancía, carrocería ni vehículo. Lee cuatro tablas y un conjunto de parámetros. Agregar un vehículo o una mercancía es insertar filas, no tocar código.

> **Nota (2026-10-06):** el diseño real de BD se resolvió en
> `PROPUESTA-BD-MOTOR-SELECCION-VEHICULOS.md`, en la misma carpeta, y en
> varios puntos difiere o resuelve cosas que acá seguían abiertas. Ver la
> nota al final de ese documento para el detalle completo de los ajustes.

### 4.1 Catálogo de carrocerías

| Campo | Tipo | Ejemplo |
| :---- | :---- | :---- |
| codigo | texto, clave | `FURGON`, `ESTACAS`, `PLATAFORMA`, `FURGON_REFRIG`, `FURGON_VENTILADO`, `CONTENEDOR_SECO`, `CONTENEDOR_REFRIG`, `PORTAVIDRIOS` |
| nombre | texto | Furgón |
| requiere\_alto | booleano | Furgón sí; Plataforma no |

### 4.2 Tipos de mercancía

| Campo | Tipo | Ejemplo |
| :---- | :---- | :---- |
| codigo | texto, clave | `BEBIDAS` |
| nombre | texto | Bebidas embotelladas o enlatadas |
| cubicabilidad | enum | `CUBICABLE` / `CONDICIONADO` / `EXCLUIDO` |
| carroceria\_principal | FK carrocerías | `FURGON` |
| carroceria\_alternativa | FK carrocerías, opcional | `ESTACAS` |
| alternativa\_requiere\_aprobacion | booleano | sí |
| rotaciones\_permitidas | enum | `TODAS` / `SOLO_VERTICAL` |
| apilable | entero, opcional | niveles máximos de apilado |
| observacion | texto | guía para el operador |

### 4.3 Configuraciones de vehículo

| Campo | Tipo | Ejemplo |
| :---- | :---- | :---- |
| codigo | texto, clave | `TURBO_2L2` |
| nombre | texto | Turbo 2L2 |
| largo\_m, ancho\_m | decimal | 4.5, 2.2 |
| alto\_m | decimal, opcional | 2.3 (vacío en Plataforma → no se valida alto) |
| peso\_max\_kg | entero | 4 000 |
| activo | booleano | sí |

Tabla relacionada `vehiculo_carroceria` con una fila por par (configuración, carrocería), reemplazando el texto "Estacas/Furgón/Plancha".

### 4.4 Compatibilidad entre tipos

Matriz N×N simétrica, almacenada como lista de pares **incompatibles** (lo no listado es compatible). Con 31 tipos, pocos "no" cubren los casos críticos.

| tipo\_a | tipo\_b | compatible | motivo |
| :---- | :---- | :---- | :---- |
| `FERTILIZANTE_SACO` | `ALIMENTO_SECO` | no | contaminación |

### 4.5 Parámetros

| Parámetro | Valor inicial | Uso |
| :---- | :---- | :---- |
| `k_max` | 4 | Máximo de vehículos por grupo antes de devolver "sin selección automática" |
| `factor_vol_util` | 0.90 | Fracción del volumen que la cubicación aprovecha en la práctica; se usa en la cota inferior |
| `usar_alternativa` | `solo_respaldo` | Si la carrocería alternativa entra como candidata desde el inicio (`siempre`) o solo cuando la principal no produce vehículos (`solo_respaldo`) |
| `umbral_alerta_llenado` | 0.75 | Por debajo se marca alerta en el resultado |
| `margen_cg` | 0.30 | Centro de gravedad dentro de ±30 % del centro (ya existe en SICCE) |

---

## 5\. Flujo de selección

### 5.1 Diagrama

```
flowchart TD
  IN["Llega la carga: líneas con tipo, dimensiones, cantidad, peso"]

  subgraph P1["Parte 1 · Preparar la carga"]
    S1{"¿Cada tipo es cubicable? (tabla tipos)"}
    S1NO["Excluido → rechazar / Condicionado → flujo especial"]
    S2["Agrupar por compatibilidad (matriz): incompatibles se separan"]
    S3["Dentro de cada grupo, agrupar por carrocería común (intersección)"]
    S4["N grupos, cada uno con una carrocería"]
    S1 -- No --> S1NO
    S1 -- Sí --> S2 --> S3 --> S4
  end

  subgraph P2["Parte 2 · ¿Cuántos vehículos? (por grupo)"]
    V1["Candidatos: configuraciones de esa carrocería donde cabe un bulto suelto"]
    V2["G = la más grande. k = cota inferior por peso y volumen"]
    V3["Llenar en cadena k vehículos G: todo en el 1°, sobrante al 2°, …"]
    V4{"¿Sobran bultos?"}
    V5["k = k + 1 (hasta k_max)"]
    V6["k es el mínimo"]
    V1 --> V2 --> V3 --> V4
    V4 -- Sí --> V5 --> V3
    V4 -- No --> V6
  end

  subgraph P3["Parte 3 · ¿Cuáles vehículos?"]
    C1["Con k fijo: cada tipo como primer vehículo, el resto achicado al más pequeño donde quepa"]
    C2["Descartar opciones que necesiten más de k"]
    C3["Elegir la de mayor llenado promedio"]
    C1 --> C2 --> C3
  end

  OUT["Resultado: vehículos con tipo, bultos asignados y % llenado; alerta si < 75 %"]

  IN --> S1
  S4 --> V1
  V6 --> C1
  C3 --> OUT
```

### 5.2 Parte 1: preparar la carga

1. **Cubicabilidad.** Para cada línea se consulta `tipos.cubicabilidad`. `EXCLUIDO` rechaza la carga completa con el motivo. `CONDICIONADO` saca la línea del motor y la envía al flujo especializado. `CUBICABLE` sigue.  
2. **Compatibilidad.** Se construye un grafo donde cada línea es un nodo y cada par incompatible una arista. Los grupos son las componentes conexas del grafo complementario; en la práctica, con pocos pares incompatibles, basta con: tomar la primera línea, agregarle todas las compatibles con todo lo ya agregado, cerrar el grupo, repetir con las que quedan.  
3. **Carrocería.** Dentro de cada grupo, cada línea tiene un conjunto de carrocerías admitidas: `{principal}` si `usar_alternativa = solo_respaldo`, `{principal, alternativa}` si `siempre`. Se agrupan las líneas cuya intersección no es vacía. Si una línea no comparte carrocería con ninguna otra, forma su propio grupo. Con `solo_respaldo`, si un grupo no produce vehículos en la Parte 2, se reintenta con la alternativa.  
4. Cada grupo resultante tiene una carrocería definida y corre las Partes 2 y 3 de forma independiente. El resultado final es la unión.

### 5.3 Parte 2: cuántos vehículos

**Propiedad que lo sustenta.** Si una carga cabe en k vehículos de cualquier mezcla de tipos, también cabe en k vehículos del tipo más grande (porque el más grande domina a los demás en todas las dimensiones y en peso). Por lo tanto, el mínimo número de vehículos se encuentra siempre con el más grande, sin buscar combinaciones.

```
seleccionar_cantidad(bultos, carroceria):
    candidatos = configuraciones activas que aceptan carroceria
                 y donde cabe un bulto individual (rotaciones permitidas)
    si candidatos vacío → sin selección (motivo: dimensiones)
    G = candidato de mayor volumen útil (empate: mayor peso máx.)

    k = max(1,
            ceil(peso_total / peso_max(G)),
            ceil(vol_total / (vol(G) * factor_vol_util)))
    mientras k ≤ k_max:
        asignacion, sobrantes = llenar_en_cadena(bultos, [G] * k)
        si sobrantes vacío: devolver k, asignacion
        k = k + 1
    devolver sin_seleccion_automatica(k_max, sobrantes, mejor_llenado)

llenar_en_cadena(bultos, vehiculos):
    pendientes = bultos ordenados por peso desc (criterio SICCE)
    asignacion = []
    para cada v en vehiculos:
        colocados, pendientes = cubicar(pendientes, v)
        asignacion.agregar((v, colocados))
    devolver asignacion, pendientes
```

`cubicar` es la heurística SICCE actual, con una adición: además de respetar el espacio, deja de colocar bultos cuando el peso acumulado alcanza `peso_max` del vehículo. Devuelve los colocados, los que no cupieron y el % de llenado volumétrico.

La cota inferior evita evaluar particiones que ya se sabe imposibles: si la carga pesa tres veces el máximo de G, se arranca en k \= 3\.

### 5.4 Parte 3: cuáles vehículos

Con k fijo, el sobrante varía según qué vehículo se llene primero, y cada sobrante lleva a un segundo vehículo distinto. Por eso se enumeran las opciones:

```
seleccionar_tipos(bultos, candidatos, k):
    opciones = []
    para cada tipo T en candidatos:
        pendientes = bultos ordenados por peso desc
        vehiculos = []
        para i en 1..k:
            si i < k:
                colocados, pendientes = cubicar(pendientes, T)
                vehiculos.agregar((T, colocados))
            si no:
                # último vehículo: el más pequeño donde cabe todo lo pendiente
                t_min = primer tipo (de menor a mayor) tal que
                        peso(pendientes) ≤ peso_max(t) y
                        cubicar(pendientes, t) no deja sobrantes
                si no existe t_min: descartar esta opción (necesita > k); continuar
                vehiculos.agregar((t_min, pendientes))
        opciones.agregar(vehiculos)

    devolver la opción con mayor promedio de % llenado
            (empate: menor suma de volumen útil, es decir, flota más pequeña)
```

Variante simple para k ≥ 3: los primeros k−1 vehículos usan el mismo tipo T y solo el último se achica. Es suficiente para el objetivo acordado; si en la validación con históricos se ve valor en mezclar más, se puede extender a probar pares (T1, T2) para los primeros, lo cual sigue siendo barato.

### 5.5 Resultado

Por cada grupo: lista de vehículos con configuración, bultos asignados (por línea y cantidad), % de llenado volumétrico, peso cargado, centro de gravedad y alertas (`llenado < umbral`, `alternativa de carrocería usada`). Si algún grupo terminó en "sin selección automática", se reporta k alcanzado, bultos que quedaron fuera y mejor llenado encontrado, para revisión manual.

### 5.6 Costo computacional

Lo único costoso es `cubicar`, que es O(n log n) sobre los bultos. Llamadas por grupo:

- Parte 2: k cubicaciones por intento; gracias a la cota inferior, casi siempre 1 o 2 intentos.  
- Parte 3: por cada tipo T (hasta 6), k−1 cubicaciones de llenado más, para el último vehículo, hasta T cubicaciones de achicado (la mayoría se descartan antes por el filtro de peso y volumen, que es inmediato).

Con 6 configuraciones y una carga de 3 vehículos: del orden de 40 cubicaciones en el peor caso, milisegundos en el prototipo actual. Se puede reducir más cacheando el resultado de `cubicar` por (conjunto de bultos, tipo).

---

## 6\. Ejemplos

### 6.1 Carga homogénea, dos vehículos

1 200 cajas de bebidas, 50 × 40 × 40 cm (0.08 m³), 20 kg. Total 96 m³, 24 000 kg. Carrocería Furgón. Factor de volumen útil 0.90.

Cuántas cajas caben por configuración:

| Vehículo | Por espacio | Por peso | Caben |
| :---- | :---- | :---- | :---- |
| Turbo | 256 | 200 | 200 (limita el peso) |
| Sencillo | 421 | 425 | 421 (limita el volumen) |
| Minimula | 856 | 900 | 856 (limita el volumen) |
| Tractomula Furgón | 856 | 1 500 | 856 (limita el volumen) |

Parte 2: Tractomula Furgón lleva 856, sobran 344, caben en una segunda. k \= 2\.

Parte 3:

| Opción | Primero | Sobran | Segundo | Llenado promedio |
| :---- | :---- | :---- | :---- | :---- |
| 1 | Tractomula F., 856 (90 %) | 344 | Sencillo (73 %) | 82 % |
| 2 | Minimula, 856 (90 %) | 344 | Sencillo (73 %) | 82 % |
| 3 | Sencillo, 421 (90 %) | 779 | Minimula (82 %) | 86 % |
| 4 | Turbo, 200 (70 %) | 1 000 | No cabe en ninguno → descartada | — |

Resultado: **Sencillo con 421 cajas \+ Minimula con 779**, 86 % promedio, sin alertas.

### 6.2 Dos mercancías, carga pesada

30 pallets de ladrillos (1.2 × 1.0 × 1.2 m, 1 200 kg, apilables a 2\) y 300 sacos de fertilizante (0.8 × 0.5 × 0.2 m, 50 kg).

Parte 1: ambas cubicables y compatibles entre sí, pero ladrillos va en Estacas y fertilizante en Furgón → dos grupos.

**Grupo fertilizante (Furgón):** 24 m³, 15 000 kg.

| Vehículo | Por espacio | Por peso | Caben |
| :---- | :---- | :---- | :---- |
| Turbo | 256 | 80 | 80 |
| Sencillo | 421 | 170 | 170 |
| Minimula | 856 | 360 | 360 |
| Tractomula Furgón | 856 | 600 | 600 |

k \= 1\. Achicando: Turbo y Sencillo no aguantan el peso; Minimula sí. Resultado: **1 Minimula al 32 %**, con alerta de bajo llenado.

**Grupo ladrillos (Estacas):** 43 m³, 36 000 kg. Pallets acomodados por filas (no por división de volumen):

| Vehículo | Por espacio | Por peso | Caben |
| :---- | :---- | :---- | :---- |
| Turbo | 6 | 3 | 3 |
| Sencillo | 24 | 7 | 7 |
| Dobletroque | 32 | 14 | 14 |
| Minimula | 48 | 15 | 15 |
| Tractomula 3S3 | 48 | 28 | 28 |

Parte 2: Tractomula lleva 28, sobran 2\. k \= 2\.

Parte 3:

| Opción | Primero | Sobran | Segundo | Llenado promedio |
| :---- | :---- | :---- | :---- | :---- |
| 1 | Tractomula, 28 (53 %) | 2 | Turbo (13 %) | 33 % |
| 2 | Minimula, 15 (28 %) | 15 | Minimula (28 %) | 28 % |
| 3 | Dobletroque, 14 (40 %) | 16 | Tractomula (30 %) | 35 % |
| 4 | Sencillo, 7 (27 %) | 23 | Tractomula (44 %) | 35 % |
| 5 | Turbo, 3 (19 %) | 27 | Tractomula (51 %) | 35 % |

Con el criterio acordado gana la opción 3 (Dobletroque \+ Tractomula), pero la opción 1 (Tractomula \+ Turbo) es la que un despachador escogería: más barata y más simple. Este ejemplo motiva el punto abierto de §9.

---

## 7\. Cubicación: estado actual y mejoras

### 7.1 Heurística actual (SICCE)

Heurística de estanterías (*shelf / layer building*): expande cantidades a bultos, los ordena por peso descendente, avanza en Y hasta llenar el ancho, salta de fila en X, y al agotar el largo sube de nivel en Z. Centra el bloque en el piso, calcula centro de gravedad ponderado, margen de estabilidad (±30 %) y factor de utilización volumétrica.

Funciona bien para bultos de tamaño parecido y deja huecos con mezcla de tamaños, porque nada rellena los espacios entre filas.

**Recomendación para la primera versión: mantenerla.** Es la única función costosa, el flujo la llama pocas veces y no es necesario cambiarla para que el motor funcione. Solo hay que agregarle el tope por peso y la salida de "bultos no colocados".

### 7.2 Cuándo mejorarla

Como es una heurística, puede dejar fuera bultos que en realidad cabrían, y eso infla k en uno de vez en cuando. Con el objetivo de menos vehículos, ese es el único error que importa. Si en la validación con históricos (§8.3) el motor pide un vehículo más que el operador, el lugar donde invertir es aquí, no en el flujo.

### 7.3 Algoritmos recomendados, en orden de esfuerzo

1. **Deepest Bottom-Left-Fill (DBLF).** Cada caja va a la posición más profunda, más baja y más a la izquierda disponible. Poco más código que la heurística actual, rellena huecos. Primer paso natural.  
2. **Extreme Points** (Crainic, Perboli y Tadei, 2008). Mantiene la lista de puntos donde puede apoyarse la siguiente caja. Estándar práctico para 3D: buena calidad, tiempo bajo.  
3. **Maximal Empty Spaces.** Lleva explícitamente los paralelepípedos vacíos restantes. Hace natural respetar restricciones de apoyo (porcentaje de base soportada) y de carga sobre carga (frágiles arriba).  
4. **Capa de mejora sobre cualquier constructor:** probar varios órdenes de inserción (volumen, peso, altura, aleatorio con sesgo) y quedarse con el mejor llenado; o un *Biased Random-Key Genetic Algorithm* (Gonçalves y Resende, 2013\) que evoluciona orden y rotaciones usando el constructor como decodificador. Con 5–10 s de cómputo supera con claridad una sola pasada.

Referencia para decidir qué restricciones modelar: Bortfeldt y Wäscher (2013), *Constraints in container loading: a state-of-the-art review*.

### 7.4 Alternativas descartadas para la capa de flota

- **Enumeración de composiciones de flota ordenada por costo** (variable-sized bin packing). Correcta y barata (con 8 tipos y k ≤ 4 son 495 composiciones), pero requiere tarifas por tipo y es más de lo que el objetivo acordado necesita.  
- **Solver exacto (OR-Tools CP-SAT) para asignación por peso y volumen.** Garantiza optimalidad de costo, pero no resuelve la geometría 3D y agrega una dependencia. Innecesario con el objetivo de "menos vehículos".  
- **Metaheurísticas integradas (Timefold / OptaPlanner).** Útiles si el problema crece (rutas, ventanas de tiempo, flota real disponible), no para esta versión.  
- **Librerías de empaque 3D en Python (`py3dbp` y similares).** Calidad básica; sirven como referencia de implementación, no como motor final.

---

## 8\. Recomendaciones de implementación

### 8.1 Preparación de datos (antes del prototipo)

1. Normalizar la matriz mercancía/carrocería a las tablas de §4: catálogo cerrado de carrocerías, códigos de principal y alternativa, flag de aprobación, nivel de cubicabilidad.  
2. Decidir qué hacer con las carrocerías sin vehículo (refrigeradas, ventiladas, contenedores): agregar configuraciones o marcar esos tipos como fuera de alcance de esta versión, para no producir "sin selección" silenciosos.  
3. Construir la matriz de compatibilidad con operaciones. Arrancar con los pares críticos conocidos; lo no listado es compatible.  
4. Convertir "Estacas/Furgón/Plancha" en filas de `vehiculo_carroceria`. Unificar Plancha, Plataforma y Planchón en un solo código.

### 8.2 Diseño del software

- Separar en cuatro módulos con interfaces claras: **clasificador** (Parte 1), **selector de cantidad** (Parte 2), **selector de tipos** (Parte 3\) y **cubicador** (SICCE). El cubicador es un oráculo: recibe bultos y una configuración, devuelve colocados, no colocados y métricas.  
- Parámetros de §4.5 en configuración, no como constantes.  
- El resultado incluye siempre la trazabilidad: qué grupos se formaron y por qué, qué opciones se evaluaron y con qué llenado, cuál ganó. Eso es lo que le permite al operador confiar en la recomendación y a ustedes ajustar parámetros.  
- Cachear `cubicar` por (conjunto de bultos, configuración) dentro de una misma ejecución.

### 8.3 Validación

Antes de desplegar, correr el motor sobre 50–100 despachos ya ejecutados y comparar número de vehículos, tipos y llenado contra lo decidido manualmente. Eso calibra `factor_vol_util`, muestra si la cubicación infla k, y da el argumento para operaciones.

### 8.4 Plan por fases

| Fase | Entregable |
| :---- | :---- |
| 1 | Tablas normalizadas (Excel o base de datos) con los datos actuales |
| 2 | Prototipo del motor en Python que lee las tablas y corre los dos ejemplos de §6 |
| 3 | Validación con despachos históricos; ajuste de parámetros |
| 4 | Integración con SICCE y la aplicación de ingreso de carga |
| 5 | Mejora de cubicación (DBLF) solo si la fase 3 lo justifica |

---

## 9\. Puntos abiertos

1. **Criterio de comparación en cargas pesadas.** Con el llenado medido solo por volumen, en cargas donde el peso limita (ladrillos, varillas, lámina) todas las opciones quedan con porcentajes bajos y casi empatados, y el criterio puede preferir una combinación más cara (§6.2). Dos salidas, ambas simples:  
   - **(a) Recomendada.** Para *comparar* opciones usar el aprovechamiento real \= max(% volumen, % peso) de cada vehículo; el % que se *reporta* sigue siendo el de volumen. El peso sigue siendo filtro; solo entra en la comparación.  
   - **(b)** Mantener llenado por volumen y desempatar por flota más pequeña (menor suma de volumen útil) cuando las opciones estén dentro de un margen.  
2. ~~**Alto en Plataforma.** ¿2.6 m es límite duro o aplica la altura total normativa? Queda como campo opcional por configuración; vacío \= no se valida.~~
   ~~**Resuelto (2026-10-06):** el alto siempre se valida, para todas las
   carrocerías, con un valor default de 4.4 m cuando el vehículo no trae uno
   propio.~~
   **Actualizado (2026-10-08):** esa resolución se revirtió al cruzar
   contra un seeder externo. La pregunta original tenía razón: sí es
   "la altura total normativa" — el campo (`AlturaMaximaReglamentariaCm`)
   es el límite legal de altura por reglamentación de tránsito para ese
   tipo de vehículo, no una medida física inventada, y varía por vehículo
   (no hay un default único de 4.4 m). Ver
   `cubicador/database/documentacion/ORIGEN-Y-DECISIONES-SEEDER.md`.
3. **Alternativa de carrocería.** `usar_alternativa` arranca en `solo_respaldo` porque la matriz las marca como condicionadas. Confirmar.  
4. **Carrocerías refrigeradas y contenedores.** Agregar configuraciones o excluir de esta versión.  
5. **Límite `k_max`.** Propuesto 4\. Confirmar con operaciones cuál es el máximo razonable de vehículos que el sistema debe proponer por sí solo.

---

## 10\. Glosario

- **Bulto:** unidad logística individual (caja, saco, pallet, pieza) con dimensiones y peso propios.  
- **Línea:** una entrada de la carga: tipo de mercancía, dimensiones del bulto, cantidad y peso unitario.  
- **Grupo:** conjunto de líneas compatibles entre sí y con una carrocería común, que se despacha de forma independiente.  
- **Configuración de vehículo:** tipo de vehículo con medidas útiles, peso máximo y carrocerías que acepta.  
- **Cubicar:** acomodar bultos dentro de una configuración y medir el resultado.  
- **Llenado:** volumen de los bultos colocados sobre volumen útil del vehículo.  
- **Cota inferior (k):** mínimo teórico de vehículos calculado por peso y volumen antes de cubicar.  
- **Llenar en cadena:** cubicar todo en el primer vehículo, el sobrante en el segundo, y así sucesivamente.  
- **Achicar:** reemplazar un vehículo por la configuración más pequeña donde su carga asignada aún cabe.
