# Nota de ajustes a la hoja de ruta — para Bran

Revisamos tu "Hoja de ruta — Reestructuración del Cubicador" (8 oct 2026)
con Sara, y también la validamos con el equipo de Cotifly. Es un documento
sólido — verificamos 6 afirmaciones técnicas contra el código real y 5
salieron exactas. Estos son los ajustes/correcciones:

## 1. Dónde viven los CRUD — decisión distinta a la que propone tu hoja

Tu hoja propone pantallas en el portal de NEXO con escritura por cola. Ya
se decidió distinto con Duban: **los CRUD viven en el backend del
Cubicador** (endpoints propios), y se construye un contrato para que el
front que ya tiene NEXO los consuma directo. Hay que actualizar esa
sección.

## 2. Mecanismo de migraciones en SQL Server — recomendación

No mantener dos descripciones separadas del esquema (nuestro `.sql` +
modelos de Python). Como las 9 tablas de todas formas van a necesitar
modelos de Python para que el motor las lea/escriba, mejor que esos
modelos sean la única fuente de verdad, y que `create_all` (el mismo
mecanismo que ya usa el resto del Cubicador) sea el que las cree — no
mantener nuestro script aparte a largo plazo.

## 3. Categorías de mercancía — ya resuelto, no está pendiente

Usamos solo las 9 categorías que respaldan mercancías reales del Excel
(sin oxidante/corrosivo — no hay ninguna fila que los necesite). El seeder
completo (los 7 CSV) ya existe, no es "1 de 7" como dice tu hoja.

## 4. Conteo de comandos por cola — 11, no 10

El worker tiene 11 ramas de comando (el propio comentario del código tiene
un error de conteo, por eso el "10"): optimizar, crear_desde_linea,
crear_plan, crear_plan_demo, guardar_vehiculo, eliminar_vehiculo,
guardar_item, importar_items, eliminar_item, eliminar_plan,
ajustar_colocacion.

## 5. Cama baja y contenedores del catálogo viejo — parcialmente resuelto

Se agregaron `CAMA_BAJA` y `PORTACONTENEDORES` como carrocerías nuevas en
el catálogo. **A propósito no se les asignó ningún vehículo todavía** —
las relaciones vehículo-carrocería del seeder se limitan estrictamente a
lo que sale del Excel "Dimensiones y Peso por vehículo". Con qué
vehículo(s) empareja cada una lo decide el Administrador de Fletes, desde
la pantalla del CRUD, cuando la use — no es una decisión que tengamos que
tomar nosotros de antemano.

## 6. Códigos de vehículo/carrocería — se usan los de NEXO directamente

Por ahora, en vez de reconciliar dos catálogos separados, el Cubicador usa
los mismos códigos que ya existen en `tbl_configuraciones_vehiculo` de
NEXO.

## 7. Falta en el contrato: el peso real en kg de cada vehículo

Tu hoja dice que el contrato nuevo devuelve dos porcentajes (volumen y
peso) por vehículo. Necesitamos **los dos**: los porcentajes **y** el
peso real en kg de cada camión recomendado — el peso en kg hace falta
porque es lo que Cotifly le manda a NEXO al crear la solicitud, no alcanza
con el porcentaje solo. Hay que agregarlo como campo explícito, además de
los dos porcentajes que ya contempla la hoja.

## 8. Falta en el trabajo de NEXO: un campo que agrupe líneas de la misma ruta

Cuando una carga heterogénea produce varios camiones, Cotifly le manda a
NEXO una línea por camión — pero NEXO no tiene ningún campo que diga
"estas líneas son la misma ruta/solicitud". Sin él, la Mesa de Costeo las
muestra sueltas, sin relación visible.

**Recomendación de implementación — ya validada con Cotifly:** no partir
`tbl_lineas_solicitud` en dos tablas (eso tendría sentido en Cotifly porque
ya está reestructurando todo su modelo, pero en NEXO esa tabla es vieja y
la usa todo el sistema de costeo). Alcanza con **una columna nueva**
(`GrupoRutaId`, `uniqueidentifier NULL` — texto de 64 caracteres también
sirve si se prefiere). Cotifly ya confirmó que esto le alcanza, con 3
condiciones puntuales:

1. `CapturaLineaRequest` debe aceptar el campo (opcional, nullable) y
   guardarlo tal cual en la columna nueva.
2. `GET /api/solicitudes/{id}` debe devolverlo en cada línea — sin esto,
   Cotifly no puede reconsultar/emparejar los camiones de una ruta.
3. **El valor lo pone Cotifly, no NEXO** — Cotifly manda el GUID de su
   propia ruta en todas las líneas-camión que vengan de ahí; NEXO solo lo
   guarda, nunca genera uno propio (así el emparejamiento es estable aunque
   se reenvíe o se reconsulte).

Dos cuidados: las líneas que nacen directo en NEXO (sin este campo) quedan
en `NULL` y no deben romper nada; y los servicios conexos de una ruta se
pegan a la línea del PRIMER camión — si se puede ordenar las líneas de un
mismo grupo de forma estable, mejor. Un índice por `GrupoRutaId` es
opcional, no es necesario para el volumen de Cotifly.

## 9. Mesa de Costeo: habilitar las dos opciones, no una sola

Sobre poder editar el vehículo de una línea en Mesa de Costeo: que se
habiliten **las dos** — editar el vehículo asignado, Y poder dejar el
precio sin vehículo asignado. No forzar una sola de las dos.

## 10. Resultado marcado, no error

Cuando el motor devuelva "sin selección automática" o la línea tenga
mercancía condicionada pendiente de aprobación, que llegue como un
**resultado marcado explícitamente así** en la respuesta — no como un
código de error HTTP. Los dos lados (NEXO y Cotifly) necesitan poder
distinguir "no se pudo asignar un vehículo" de "la llamada falló".

## 11. Contrato: campos obligatorios explícitos

El contrato nuevo debería declarar qué campos son **siempre** obligatorios
en la respuesta (vehículo, carrocería, peso) — para que cada lado pueda
validar que no le llegó algo incompleto, en vez de aceptar un `null`
silencioso (el riesgo que tu hoja ya señala con `System.Text.Json`).

## Pendientes que siguen igual (sin resolver todavía)

- Dimensiones y capacidad de los contenedores 40HC y 20STD (negocio).
- Espacio interior real de furgón refrigerado: **resuelto** — se usa la
  medida por defecto del vehículo, igual para cualquier carrocería, no se
  modela un espacio reducido aparte.
- Matriz de compatibilidad más allá de la primera regla: no bloquea nada,
  el dueño del catálogo la completa por CRUD cuando salga a producción.
- Nombres exactos de campos, autenticación y fechas del contrato: sigue a
  cargo de la HU de comunicación Cotifly-Cubicador (Duban).
