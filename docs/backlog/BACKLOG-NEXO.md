# Backlog de NEXO

Todas las issues, historias y pendientes que se planearon en `docs/backlog/` hasta el 2026-11-02, en un solo documento. Cada una trae su descripción, las decisiones tomadas y su fecha.

## Cómo leer este documento

- Las secciones siguen el orden en que se escribieron los documentos de planeación. Cada sección dice de qué documento sale.
- **Creada** es la fecha de creación de la issue en GitHub. Si la unidad nunca se creó como issue, aparece **Sin issue** y la fecha es la del documento donde se planeó.
- **Estado** es el de GitHub al 2026-11-02. Para el estado actual, abrir el enlace de la issue.
- Varias historias se agruparon en una sola issue al crearlas en GitHub. En ese caso la misma issue aparece en varias historias y se marca como *issue que agrupa varias historias*.
- Una misma necesidad puede aparecer en dos secciones: primero como hallazgo de la revisión y después como issue del sprint.

## Resumen

254 unidades de trabajo: 230 con issue en GitHub (40 abiertas y 190 cerradas) y 24 sin issue.

| # | Sección | Fecha | Unidades | Con issue | Abiertas | Sin issue |
|---|---|---|---|---|---|---|
| 1 | [Reparaciones de la importación de tarifas de plaza](#1-reparaciones-de-la-importación-de-tarifas-de-plaza) | 2026-10-05 | 8 | 7 | 0 | 1 |
| 2 | [Historias de frontend de la revisión de NEXO](#2-historias-de-frontend-de-la-revisión-de-nexo) | 2026-10-09 | 61 | 56 | 0 | 5 |
| 3 | [Historias de backend de la revisión de NEXO](#3-historias-de-backend-de-la-revisión-de-nexo) | 2026-10-09 | 31 | 29 | 0 | 2 |
| 4 | [Propuestas a futuro](#4-propuestas-a-futuro) | 2026-10-09 | 2 | 1 | 0 | 1 |
| 5 | [Issues del Sprint 5](#5-issues-del-sprint-5) | 2026-10-12 | 31 | 30 | 4 | 1 |
| 6 | [Ajustes varios de pantalla](#6-ajustes-varios-de-pantalla) | 2026-10-15 | 1 | 1 | 0 | 0 |
| 7 | [Issues por repositorio (revisión del 16 de octubre)](#7-issues-por-repositorio-revisión-del-16-de-octubre) | 2026-10-16 | 43 | 39 | 5 | 4 |
| 8 | [Migración de las bases de pruebas](#8-migración-de-las-bases-de-pruebas) | 2026-10-19 | 2 | 2 | 1 | 0 |
| 9 | [Rol CoordinadorFletes a AdminBiblioteca](#9-rol-coordinadorfletes-a-adminbiblioteca) | 2026-10-19 | 1 | 0 | 0 | 1 |
| 10 | [Plan de IA (Claude) para NEXO](#10-plan-de-ia-claude-para-nexo) | 2026-10-23 | 37 | 36 | 13 | 1 |
| 11 | [Pendientes de datos](#11-pendientes-de-datos) | 2026-10-26 | 2 | 0 | 0 | 2 |
| 12 | [Pendientes del mapeo de vehículo](#12-pendientes-del-mapeo-de-vehículo) | 2026-10-27 | 9 | 6 | 0 | 3 |
| 13 | [Precio de venta de los servicios conexos](#13-precio-de-venta-de-los-servicios-conexos) | 2026-10-28 | 9 | 7 | 3 | 2 |
| 14 | [Importación inteligente de Excel en Cotifly](#14-importación-inteligente-de-excel-en-cotifly) | 2026-10-29 | 17 | 16 | 14 | 1 |

## 1. Reparaciones de la importación de tarifas de plaza

*Documento de planeación del 2026-10-05.* Errores encontrados al reimportar un Excel de tarifas de plaza exportado desde la pantalla.

### Tarifas Plaza

#### El importador debe leer la Vigencia desde el valor real de la celda

**Issue:** [NEXO#288](https://github.com/InnovacionIA-proyectos/NEXO/issues/288) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Una fila agregada a mano en el Excel exportado se rechazó porque la celda de Vigencia tenía formato General y se leyó como número de serie. El importador debe intentar leer primero la fecha real de la celda y solo después parsear el texto.

**Decisiones:**

- Sin regresión para las fechas que ya vienen en texto bien formateado.

#### Mostrar en pantalla el detalle de errores de la importación

**Issue:** [administracion#303](https://github.com/InnovacionIA-proyectos/administracion/issues/303) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El backend ya devuelve el detalle por fila de cada rechazo, pero la pantalla de Tarifas Plaza solo muestra el número de filas rechazadas. Debe mostrar el detalle además del conteo.

#### Editar la línea rechazada directamente en pantalla

**Issue:** [administracion#303](https://github.com/InnovacionIA-proyectos/administracion/issues/303) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Hoy una línea rechazada en la importación solo se corrige editando el Excel y reimportándolo completo. Debe poder corregirse directamente en la pantalla, validando con el mismo criterio del importador.

**Decisiones:**

- Depende de mostrar primero el detalle del motivo de rechazo.

#### Ciudades ambiguas en la importación sin aviso

**Issue:** [NEXO#288](https://github.com/InnovacionIA-proyectos/NEXO/issues/288) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Si el nombre de una ciudad existe en varios departamentos, el importador toma uno sin avisar y la tarifa puede quedar en el municipio equivocado.

#### El tipo de vehículo no se valida contra la banda de peso en la importación

**Sin issue** · **Fecha:** 2026-10-05 (documento) · **Tipo:** Pendiente

**Estado según el documento:** Hallazgo secundario: no bloquea.

El importador inserta el tipo de vehículo que venga en el Excel sin comprobar que corresponda a la banda de peso de la fila según la convención esperada.

### Reglas Solo-SICETAC

#### Casos especiales de tipo de vehículo escritos a mano en el backend

**Issue:** [NEXO#288](https://github.com/InnovacionIA-proyectos/NEXO/issues/288) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Cinco tipos de vehículo especiales estaban fijos en el código en lugar de en base de datos. El documento proponía moverlos a una tabla editable para no depender de despliegues.

#### Exportar a Excel las Reglas Solo-SICETAC (backend y frontend)

**Issue:** [NEXO#288](https://github.com/InnovacionIA-proyectos/NEXO/issues/288) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Reglas Solo-SICETAC solo permite importar o bajar una plantilla vacía. Se necesita un endpoint de exportación de las reglas cargadas y un botón Exportar Excel en la pantalla, igual que en Tarifas Plaza.

### Mesa de Costeo

#### Mesa de Costeo permite marcar Ganada/Perdida sin pasar por Entregar

**Issue:** [administracion#293](https://github.com/InnovacionIA-proyectos/administracion/issues/293) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Los botones de marcar Ganada o Perdida de la Mesa de Costeo solo revisan el permiso del usuario, sin validar el estado de la cotización como sí lo hace el resumen de cotización. Deben habilitarse solo si la cotización está Entregada o en Borrador auto-costeado.

## 2. Historias de frontend de la revisión de NEXO

*Documento de planeación del 2026-10-09.* Historias de frontend (repositorio `administracion`) que salieron de la revisión paso a paso de NEXO, agrupadas por módulo. Las marcas de validación del documento original se hicieron sobre una copia de prueba del frontend; el estado real es el de la issue.

### Mesa de Costeo

#### HU-1 — Consolidar el piso SICE-TAC en una sola comparación de 3 fuentes

**Issue:** [administracion#300](https://github.com/InnovacionIA-proyectos/administracion/issues/300) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Existía una caja aparte de piso SICE-TAC calculada en el frontend con las horas generales de la solicitud, que podía diferir del candidato SICE-TAC del backend y prellenar mal el flete. Debe quedar una sola comparación de 3 cajas (SICE-TAC con tiempos logísticos, Tarifario, Histórico SAP) con el valor calculado en backend, y una caja "Fuente propuesta" con precio, fuente y botón "Usar propuesta" juntos.

**Decisiones:**

- Eliminar la caja aparte y la función floorTotal(): el valor debe salir siempre del backend.
- La caja "Fuente propuesta" prioriza el flete ya guardado sobre la propuesta original.
- Verde si hay propuesta o flete; rojo claro si no hay ninguno.

#### HU-2 — Ocultar "Valor estimado" en Mesa de Costeo

**Issue:** [administracion#300](https://github.com/InnovacionIA-proyectos/administracion/issues/300) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El resumen de la solicitud muestra "Valor estimado", un dato que nunca se captura. Debe dejar de mostrarse en todos los casos, sin cambios de backend.

**Decisiones:**

- Solo se oculta en el frontend; el uso de ValorEstimado en el score de prioridad del backend queda como tema aparte, sin HU.

#### HU-3 — Tiempos logísticos por línea

**Issue:** [administracion#300](https://github.com/InnovacionIA-proyectos/administracion/issues/300) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

No se ven las horas de cargue y descargue de cada línea. Cada línea debe mostrar un chip de solo lectura "Tiempos logísticos" con las horas propias de la línea, o las de la solicitud (aclarándolo) cuando falte alguna.

**Decisiones:**

- Usar las horas de la línea solo si cargue y descargue están ambas resueltas; nunca mezclar una de cada lado (mismo criterio que el backend).

#### HU-4 — Chips de línea de negocio, clase de viaje y clase de contenedor

**Issue:** [administracion#300](https://github.com/InnovacionIA-proyectos/administracion/issues/300) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La línea no muestra su clasificación comercial. Deben agregarse 3 chips (Línea de negocio, Clase de viaje, Clase de contenedor) que se ocultan cuando el dato es nulo.

**Decisiones:**

- Depende de que el backend envíe los 3 campos en el DTO de costeo (ya resuelto en NEXO, pendiente de despliegue al escribir el documento).

#### HU-5 — Numerar las líneas

**Issue:** [administracion#300](https://github.com/InnovacionIA-proyectos/administracion/issues/300) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Las líneas no tienen número, lo que impide referirse a una puntual. Cada encabezado debe mostrar "Línea N" y usar el mismo número en el resumen completo.

**Decisiones:**

- Reusar el mismo índice en el flujo de costeo y en "Ver resumen completo".

#### HU-6 — Indicador de avance del costeo

**Issue:** [administracion#300](https://github.com/InnovacionIA-proyectos/administracion/issues/300) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

No hay forma rápida de saber cuántas líneas faltan por costear. Debe aparecer un indicador "X de Y línea(s) costeadas" en rojo claro que cambia a "Costeo terminado" en verde cuando todas tienen flete.

**Decisiones:**

- No reusar lineasSinFlete(); contar líneas con flete real guardado.
- Decidido (Sara, 2026-10-12): conviven el indicador y el botón de aplicar propuestas, renombrado según HU-19.

#### HU-7 — Aviso de "pendiente por enviar al comercial"

**Issue:** [administracion#300](https://github.com/InnovacionIA-proyectos/administracion/issues/300) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Cuando todo está costeado pero la cotización no se ha entregado, no hay ninguna señal. Junto a "Entregar a Comercial" debe aparecer un ícono de advertencia con tooltip que desaparece al quedar Entregada.

**Decisiones:**

- Usar el ícono warning_amber y matTooltip con el patrón de Bandeja, no un tooltip propio.
- Fondo amarillo claro e ícono rojo, a propósito en dos tonos.

#### HU-8 — Ver resumen completo

**Issue:** [administracion#300](https://github.com/InnovacionIA-proyectos/administracion/issues/300) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

No hay un lugar para revisar todas las líneas con su flete y fuente antes de entregar. Debe existir una card propia, colapsada por defecto, que liste todas las líneas (costeadas o no) con ruta, flete y fuente.

**Decisiones:**

- Card hermana de "Consolidado de tarifa", no anidada.
- Usar la misma numeración de HU-5.

#### HU-9 (parte frontend) — Consultar siempre si ya existe una cotización al abrir

**Issue:** [administracion#293](https://github.com/InnovacionIA-proyectos/administracion/issues/293) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Al abrir la Mesa, la cotización solo se carga si el rol tiene autoAplicarPropuestas, así que otros roles ven "Iniciar costeo" en solicitudes ya entregadas o auto-costeadas. La pantalla debe llamar siempre a una consulta de solo lectura para mostrar la cotización existente.

**Decisiones:**

- No dar autoAplicarPropuestas a más roles, porque también aplica propuestas en silencio.
- Depende de la consulta nueva de backend (HU-B7); el auto-aplicado del Comercial no cambia.

#### Pendientes de decidir de Mesa de Costeo

**Sin issue** · **Fecha:** 2026-10-09 (documento) · **Tipo:** Pendiente

**Estado según el documento:** Todos los puntos tachados como decididos (2026-10-12)

Lista de temas mencionados sin HU formal: valor mercancía por línea, advertencia de flete manual no editable, quitar el botón "Aplicar propuestas" y legibilidad del tooltip de HU-7. Todos quedaron resueltos y remitidos a otras HU.

**Decisiones:**

- Valor mercancía: sí, como chip (HU-18).
- Flete manual: en vez de advertencia, habilitar la edición real (HU-15).
- Botón de aplicar propuestas: no se quita, se renombra y pide confirmación (HU-19).
- Tooltip de HU-7: se ve muy pequeño, se corrige en HU-20.

#### HU-10 — Bug: se puede marcar Ganada/Perdida sin entregar ni auto-costear

**Issue:** [administracion#293](https://github.com/InnovacionIA-proyectos/administracion/issues/293) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Los botones de Ganada/Perdida de la Mesa solo revisan el permiso y no el estado de la cotización, a diferencia de la pantalla de resumen. Deben habilitarse solo si la cotización está Entregada o en Borrador auto-costeada.

**Decisiones:**

- Aplicar el mismo criterio que puedeRegistrarResultado() de la pantalla de resumen.

#### HU-11 — Bug: se puede entregar a Comercial con líneas sin costear

**Issue:** [administracion#293](https://github.com/InnovacionIA-proyectos/administracion/issues/293) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El botón "Entregar a Comercial" se habilita con al menos una línea costeada, sin comparar contra el total del pliego. Debe quedar deshabilitado hasta que todas las líneas tengan flete.

**Decisiones:**

- Reutilizar lineasCosteadas() en puedeEntregar().
- El backend tampoco lo bloquea (HU-B8).

#### HU-12 — Avisar cuando un flete manual queda bajo el piso SICE-TAC

**Sin issue** · **Fecha:** 2026-10-09 (documento) · **Tipo:** Propuesta

**Estado según el documento:** Reemplazada por HU-17; prototipada en el sandbox

Proponía un popup de confirmación cuando un flete manual quedara por debajo del piso SICE-TAC, permitiendo guardarlo de todas formas. Fue reemplazada por una regla más estricta que bloquea el guardado.

**Decisiones:**

- Decisión inicial (Sara, 2026-10-12): popup de confirmación, no bloqueo.
- Luego reemplazada por HU-17: nunca se guarda un valor bajo el piso, de ninguna fuente.
- Se conserva solo como historial.

#### HU-13 — Dar permiso de "Cubicar" a todos los roles

**Issue:** [administracion#293](https://github.com/InnovacionIA-proyectos/administracion/issues/293) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El botón Cubicar/Recubicar depende de la capacidad cubicarLinea, que solo tienen AdminTarifas y Admin. Debe agregarse a Comercial, CoordinadorFletes, LiderProceso y ServiciosLogisticos, y actualizar el test que espera false para Comercial.

**Decisiones:**

- Se aplica a los 4 roles porque Sara pidió "a todos", aunque solo CoordinadorFletes lo verá de inmediato (los demás no entran a la Mesa).

#### HU-14 — Desglosar el piso SICE-TAC más tiempos logísticos

**Issue:** [administracion#293](https://github.com/InnovacionIA-proyectos/administracion/issues/293) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El candidato SICE-TAC se muestra como un solo número. Debe verse como "Piso + Tiempos = Total" cuando el backend envíe los dos componentes, y como hoy si no los envía.

**Decisiones:**

- Depende de HU-B9 (backend expone ValorPisoBase y ValorTiemposLogisticos).
- Descartado armar el desglose en el frontend con datos de Biblioteca, por el riesgo de usar horas equivocadas.
- Plaza e Histórico siguen mostrando un solo número.

#### HU-15 — Editar un flete manual ya guardado sin duplicar la línea

**Issue:** [administracion#293](https://github.com/InnovacionIA-proyectos/administracion/issues/293) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

No se puede corregir un flete manual sin borrarlo y volver a capturarlo. Debe haber un botón "Editar" en fletes de fuente Manual, con la cotización en Borrador, que actualice la misma línea por el endpoint PUT nuevo.

**Decisiones:**

- Solo para fuente Manual; la fuente queda fija durante la edición.
- La edición pasa por la misma validación de piso SICE-TAC que al agregar.
- Depende de HU-B10 (endpoint PUT).

#### HU-16 — Propuesta según el modo de decisión: etiquetas SAP/NEXO, recomendado y "Usar este"

**Issue:** [administracion#292](https://github.com/InnovacionIA-proyectos/administracion/issues/292) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La Propuesta de la Biblioteca no indica el modo de decisión, el origen de cada candidato ni cuál es el recomendado. Debe mostrar el aviso del modo, etiquetas SAP/NEXO, la marca "Recomendado" y, solo en consulta completa y sin flete, un botón "Usar este" por candidato que rellena el formulario.

**Decisiones:**

- "Usar este" no aparece en plaza vencida ni en candidatos bajo el piso SICE-TAC.
- Una línea ya autocosteada no se re-decide (decisión de Sara).
- El auto-aplicado del Comercial solo llena líneas SoloSicetac y PlazaVigenteMayor; en consulta completa la línea espera al Administrador de Tarifas.
- Depende de HU-B14, HU-B15, HU-B16 y HU-B17.

#### HU-17 — Bloquear cualquier flete por debajo del piso SICE-TAC

**Issue:** [administracion#292](https://github.com/InnovacionIA-proyectos/administracion/issues/292) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Se podía guardar un flete bajo el piso SICE-TAC. La Mesa no debe dejar guardar (al agregar o editar) ningún valor menor al piso, de ninguna fuente, y debe mostrar un aviso con un solo botón "Entendido".

**Decisiones:**

- Decisión de Sara (2026-10-12): nunca se usa un valor bajo el piso; reemplaza a HU-12.
- El control real se hace también en backend (HU-B18); esta HU es el aviso en pantalla.

#### HU-18 — Mostrar el valor de la mercancía como chip de la línea

**Issue:** [administracion#293](https://github.com/InnovacionIA-proyectos/administracion/issues/293) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El valor de la mercancía se captura pero no se ve al costear. Debe aparecer como chip en la línea cuando exista.

**Decisiones:**

- Decisión de Sara (2026-10-12): sí, como chip.
- Depende de HU-B20 (el dato no viajaba en el DTO de costeo).

#### HU-19 — "Aplicar todas las propuestas" con confirmación

**Issue:** [administracion#292](https://github.com/InnovacionIA-proyectos/administracion/issues/292) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El botón de aplicar propuestas no era claro y se ejecutaba sin confirmar. Debe llamarse "Aplicar todas las propuestas (N)" y abrir un modal de confirmación antes de aplicar.

**Decisiones:**

- Sara primero decidió quitar el botón y luego lo mantuvo con nuevo texto y confirmación (versión vigente).
- El botón es la forma de llenar de una vez las líneas de pliegos autocosteadas sin flete.
- No cambian el endpoint ni el auto-aplicado del Comercial.

#### HU-20 — El aviso "Pendiente por enviar" se ve muy pequeño y no se entiende

**Issue:** [administracion#293](https://github.com/InnovacionIA-proyectos/administracion/issues/293) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El estilo legible del tooltip solo existe en la hoja de Bandeja, por lo que en otras pantallas sale diminuto; además el aviso solo se entiende al pasar el mouse. Debe moverse el estilo a la hoja global y mostrar un texto visible "Pendiente por enviar" junto al ícono.

**Decisiones:**

- Causa: el estilo .cabina-tooltip está solo en bandeja-page.component.css; afecta las 7 pantallas que lo usan.
- Parte B aprobada por Sara (2026-10-12): texto corto siempre visible.

#### HU-21 — Agregar servicios conexos paso a paso

**Issue:** [administracion#305](https://github.com/InnovacionIA-proyectos/administracion/issues/305) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El selector de conexos era un buscador de texto libre sobre más de 1.300 servicios. Debe guiar por pasos: tipo de servicio (Almacenamiento, ITR, Sellos, Acompañamiento), luego los desplegables propios del tipo, y al final precio, unidad y subtotal, conservando el buscador de texto como alternativa.

**Decisiones:**

- Un paso con una sola opción se autoselecciona (Sara, 2026-10-12).
- Tras agregar se reinicia todo, sin conservar el grupo.
- Alternar entre modo por pasos y por texto conserva el servicio elegido.
- Sin cambios de backend.

#### HU-22 — Quitar un servicio conexo ya agregado

**Issue:** [administracion#306](https://github.com/InnovacionIA-proyectos/administracion/issues/306) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

No se puede eliminar un conexo agregado por error. Cada conexo debe tener un botón "Quitar" con confirmación que llame al endpoint DELETE y recalcule consolidado y total.

**Decisiones:**

- Visible solo con permiso gestionarServicios y cotización que no esté Ganada ni Perdida.
- Depende de HU-B23 (endpoint DELETE).

#### HU-23 — Agregar y quitar conexos con cotización pendiente o entregada

**Issue:** [administracion#306](https://github.com/InnovacionIA-proyectos/administracion/issues/306) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Los conexos se bloquean al entregar la cotización y AdminTarifas no tiene el permiso de servicios. Debe permitirse agregar y quitar antes y después de entregar, bloqueando solo si está Ganada o Perdida, con nota informativa y chip de "Modificada después de entregar".

**Decisiones:**

- Agregar gestionarServicios a AdminTarifas.
- El bloqueo pasa a depender del resultado (Ganada/Perdida), no del estado Entregada.
- Depende de HU-B24.

#### HU-24 — Líneas agrupadas por ruta y aviso de línea sin vehículo

**Issue:** [administracion#310](https://github.com/InnovacionIA-proyectos/administracion/issues/310) · **Creada:** 2026-10-14 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Con una línea por camión, la Mesa debe agrupar las líneas por grupo de ruta mostrando origen, destino y cantidad. Las líneas sin vehículo deben mostrar el motivo y poder costearse desde la Mesa.

**Decisiones:**

- Depende de HU-B26.

#### HU-25 — Guardar el valor manual como tarifa plaza desde la Mesa

**Issue:** [administracion#313](https://github.com/InnovacionIA-proyectos/administracion/issues/313) · **Creada:** 2026-10-14 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Un valor manual escrito en consulta completa se pierde como referencia. Tras validar el piso, el sistema debe ofrecer guardarlo como tarifa plaza, con la fila prellenada y vigencia obligatoria, avisando si ya existe una tarifa vigente o vencida.

**Decisiones:**

- Solo con valor manual y roles con verTarifasPlaza (AdminTarifas y Admin).
- Se guarda primero el flete y luego la plaza; si la plaza falla, el flete queda guardado.
- Solo frontend.

### Bandeja

#### HU-1 — Columna "Resultado" y filtro "Esperando resultado"

**Issue:** [administracion#299](https://github.com/InnovacionIA-proyectos/administracion/issues/299) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

En la Bandeja no se ve si una solicitud está ganada, perdida o sin definir. Debe agregarse un filtro "Resultado" con 4 opciones y una columna con badge de resultado.

**Decisiones:**

- Solo visible para roles con marcarGanadaPerdida (Comercial y Admin); AdminTarifas no lo ve (Sara, 2026-10-13).
- Al elegir un resultado, Atención pasa a "Todas".
- Descartado un chip o botón de acceso rápido.
- Depende de HU-B25.

### Captura

#### HU-1 — No autoseleccionar ciudad ni cliente al escribir

**Issue:** [administracion#307](https://github.com/InnovacionIA-proyectos/administracion/issues/307) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-16 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Cuando la búsqueda de Origen, Destino o Cliente devuelve un solo resultado, el campo se selecciona solo. Al escribir a mano debe mostrarse la sugerencia sin elegirla; la autoselección solo se mantiene al cargar un Excel.

**Decisiones:**

- Usar una marca por línea y campo que llene la carga de Excel; un booleano temporal no sirve por el debounce.
- El cliente nunca se autoselecciona, porque el Excel no lo llena.

#### HU-2 — Campos obligatorios: fecha límite, vehículo con carrocería y clase de contenedor

**Issue:** [administracion#307](https://github.com/InnovacionIA-proyectos/administracion/issues/307) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-16 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Captura deja enviar solicitudes sin datos necesarios para costear. Deben ser obligatorias la fecha límite, la carrocería junto al vehículo en Cupo y la clase de contenedor siempre que se muestre.

**Decisiones:**

- Por defecto, vehículo y carrocería solo se exigen en Cupo.
- Por decidir con Sara: si Carga suelta y Extradimensionado también los exigen, y si el backend debe rechazar solicitudes sin fecha límite.

#### HU-3 — Vista previa de la carga de Excel con todas las columnas mapeadas

**Issue:** [administracion#307](https://github.com/InnovacionIA-proyectos/administracion/issues/307) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-16 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La vista previa del Excel en Captura omite 5 columnas mapeadas y solo muestra 8 filas. Debe mostrar todas las columnas (booleanos como Sí/No) y todas las filas, o avisar cuántas no se muestran.

**Decisiones:**

- Usar el mismo parseo que se aplica luego a las líneas.

### Biblioteca

#### HU-1 — Corregir el semáforo de vigencia en Tarifas de plaza (bug)

**Issue:** [administracion#301](https://github.com/InnovacionIA-proyectos/administracion/issues/301) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-16 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El semáforo de la pestaña Tarifas de plaza siempre muestra "Vigente" porque concatena una hora a una fecha que ya trae hora. Debe reflejar la vigencia real.

**Decisiones:**

- Detectar si la fecha ya trae hora antes de concatenar.
- No tocar el módulo Tarifas Plaza, cuyo DTO de solo fecha no tiene el problema.

#### HU-2 — Ordenar Tarifas de plaza por vigencia

**Issue:** [administracion#301](https://github.com/InnovacionIA-proyectos/administracion/issues/301) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-16 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La pestaña no ordena por urgencia. Debe ordenar vencida, por vencer y vigente, igual que el módulo Tarifas Plaza.

**Decisiones:**

- El orden aplica solo a la página ya cargada, sin pedir orden al backend (decisión explícita de Sara).

#### HU-3 — Separar la columna de vigencia de la fecha

**Issue:** [administracion#301](https://github.com/InnovacionIA-proyectos/administracion/issues/301) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-16 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El badge de vigencia y la fecha comparten celda y se desalinean entre filas. Deben quedar en dos columnas reales de la tabla.

**Decisiones:**

- Cambio exclusivo de Biblioteca, no de Tarifas Plaza.

#### HU-4 — Paginación en Histórico SAP

**Issue:** [administracion#301](https://github.com/InnovacionIA-proyectos/administracion/issues/301) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-16 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La pestaña Histórico SAP no pagina aunque el backend lo soporta. Debe tener los mismos controles de paginación que Tarifas de plaza y volver a la página 1 al cambiar filtros.

**Decisiones:**

- Espejo exacto de la paginación de Tarifas de plaza.

#### HU-5 — Quitar "mostrando las primeras X" en las 3 pestañas paginadas

**Issue:** [administracion#301](https://github.com/InnovacionIA-proyectos/administracion/issues/301) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-16 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Con paginación real, el texto "mostrando las primeras X" es engañoso. Debe quitarse en Tarifas de plaza, Histórico SAP y SICE-TAC, dejando solo el total.

#### HU-6 — Ícono de información en lugar del texto descriptivo fijo

**Issue:** [administracion#304](https://github.com/InnovacionIA-proyectos/administracion/issues/304) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Biblioteca tiene un párrafo descriptivo fijo bajo el título. Debe reemplazarse por un ícono de información con tooltip, igual que en Bandeja.

**Decisiones:**

- Copiar el bloque CSS del botón de información en la página, porque los estilos no se comparten entre componentes.

#### HU-7 — Unidad editable en el servicio Almacenamiento

**Issue:** [administracion#301](https://github.com/InnovacionIA-proyectos/administracion/issues/301) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-16 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La unidad de Almacenamiento queda fija en "Por estiba". Debe poder elegirse entre "Por estiba" y "Por unidad", manteniendo "Por estiba" por defecto.

**Decisiones:**

- Los demás tipos de servicio no cambian su unidad.

#### HU-8 — Código de Almacenamiento con mayúscula inicial

**Issue:** [administracion#301](https://github.com/InnovacionIA-proyectos/administracion/issues/301) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-16 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El código de Almacenamiento se guarda en mayúsculas mientras registros anteriores usan mayúscula inicial. Debe generarse como "Almacenamiento".

**Decisiones:**

- ITR se deja igual.

#### HU-9 — Separar máquina y movimiento en el formulario de ITR

**Issue:** [administracion#301](https://github.com/InnovacionIA-proyectos/administracion/issues/301) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-16 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El tipo de máquina de ITR se escribe a mano mezclando máquina y movimiento. Deben ser dos desplegables (máquina con 3 opciones cerradas y movimiento 1 o 2) que se combinan en el mismo campo de texto de siempre.

**Decisiones:**

- No se agrega columna en la base: se sigue guardando un solo texto combinado.
- Solo aplica al formulario manual, no a la carga masiva de Excel.
- Al editar, el texto combinado se separa de nuevo en los dos campos.

### Tarifas Plaza

#### HU-1 — Mostrar el detalle de errores al importar Excel

**Issue:** [administracion#303](https://github.com/InnovacionIA-proyectos/administracion/issues/303) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El backend devuelve el detalle por fila de los rechazos, pero la pantalla solo muestra el conteo. Debe mostrarse el detalle además del conteo.

#### HU-2 — Editar una línea rechazada en pantalla sin reimportar

**Issue:** [administracion#303](https://github.com/InnovacionIA-proyectos/administracion/issues/303) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Corregir una fila rechazada obliga a editar el Excel y reimportarlo completo. Debe poder corregirse en pantalla con la misma validación del importador.

**Decisiones:**

- Depende de HU-1.

#### HU-3 — Ícono de información en lugar del texto descriptivo fijo

**Issue:** [administracion#304](https://github.com/InnovacionIA-proyectos/administracion/issues/304) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Tarifas Plaza tiene un párrafo descriptivo fijo bajo el título. Debe reemplazarse por un ícono de información con el mismo texto.

**Decisiones:**

- Mismo patrón que Bandeja, Biblioteca y Reglas Solo-SICETAC.

#### Hallazgos secundarios de Tarifas Plaza

**Sin issue** · **Fecha:** 2026-10-09 (documento) · **Tipo:** Pendiente

**Estado según el documento:** Ambos puntos tachados como decididos

Dos hallazgos del importador: ciudades con nombre repetido sin aviso y tipo de vehículo no validado contra el rango de peso. Ambos quedaron decididos.

**Decisiones:**

- Ciudades ambiguas: la vista previa muestra cuál se usó y deja cambiar el departamento (HU-9 de Carga masiva, HU-B21).
- Tipo de vehículo contra rango de peso: no se valida (Sara, 2026-10-12).

### Reglas Solo-SICETAC

#### HU-1 — Botón "Exportar Excel"

**Issue:** [administracion#297](https://github.com/InnovacionIA-proyectos/administracion/issues/297) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Reglas Solo-SICETAC solo permite importar. Debe tener un botón "Exportar Excel" como Tarifas de Plaza, cuyo archivo se pueda editar y reimportar.

**Decisiones:**

- Depende del endpoint nuevo de backend (HU-B6).

#### HU-2 — Ícono de información en lugar del texto descriptivo fijo

**Issue:** [administracion#304](https://github.com/InnovacionIA-proyectos/administracion/issues/304) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La pantalla tiene un párrafo descriptivo fijo bajo el título. Debe pasar a un ícono de información con exactamente el mismo texto.

**Decisiones:**

- El texto del tooltip no se resume ni se cambia.

### Carga masiva de Excel

#### Estado final de la carga masiva y códigos de vehículo faltantes

**Sin issue** · **Fecha:** 2026-10-09 (documento) · **Tipo:** Pendiente

**Estado según el documento:** Confirmado en pantalla real por Sara (2026-10-12) en el sandbox; pendiente el catálogo de vehículos

Resumen de cómo quedaron las vistas previas de Servicios y Tarifas Plaza tras el prototipo. Queda un pendiente que no es del frontend: faltan códigos de tipo de vehículo en el catálogo real de SAP.

**Decisiones:**

- Toda la sección es 100% frontend, sin endpoints nuevos (decidido 2026-10-09).
- Tipo de vehículo nunca marca error y no se hardcodean códigos.
- Esperar a que se actualice el catálogo o se despliegue HU-B5, sin parche temporal en el frontend.

#### HU-1 — Vista previa editable antes de confirmar la carga masiva

**Issue:** [administracion#302](https://github.com/InnovacionIA-proyectos/administracion/issues/302) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La carga de Servicios bloquea todo el archivo ante cualquier error y Tarifas Plaza envía el archivo sin revisión. Ambos deben abrir una grilla editable con todas las filas, resaltar errores y guardar solo al confirmar, usando el mismo endpoint de importación.

**Decisiones:**

- Sin endpoints nuevos: el Excel se reconstruye desde la grilla.
- Para Tarifas Plaza se porta la validación al frontend con catálogos en vivo.
- Riesgo aceptado: el algoritmo de validación queda duplicado entre backend y frontend.

#### HU-2 — Autocorregir columnas fijas en vez de marcar error (Servicios)

**Issue:** [administracion#302](https://github.com/InnovacionIA-proyectos/administracion/issues/302) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Código, tipo de servicio, unidad y categoría marcaban error aunque el tipo tuviera un valor fijo. Deben corregirse solos y dejar de ser editables en la grilla.

**Decisiones:**

- Se corrigió de paso la detección de duplicados, que usaba el código sin corregir.

#### HU-3 — Controles apropiados por tipo de columna en la grilla (Servicios)

**Issue:** [administracion#302](https://github.com/InnovacionIA-proyectos/administracion/issues/302) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Todas las celdas de la grilla de Servicios eran texto libre. Deben usar autocompletado de ciudades, calendario, desplegables y número con separador de miles, como el formulario manual.

**Decisiones:**

- Reusar los controles del formulario manual.
- Se agregó trackBy en ambas grillas para corregir que las casillas solo aceptaban una letra.

#### HU-4 — Ocultar filas existentes sin errores en la vista previa

**Issue:** [administracion#302](https://github.com/InnovacionIA-proyectos/administracion/issues/302) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La vista previa muestra cientos de filas que no requieren revisión. Las filas nuevas siempre se ven y las existentes solo si tienen error, con un conteo de las ocultas.

**Decisiones:**

- Es solo visual: las filas ocultas se guardan igual.
- Regla y conteo confirmados con Sara.

#### HU-5 — Controles de catálogo en la vista previa de Tarifas Plaza

**Issue:** [administracion#302](https://github.com/InnovacionIA-proyectos/administracion/issues/302) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La grilla de Tarifas Plaza era solo texto libre. Cada campo debe usar el mismo control del formulario manual (desplegables, autocompletado de ciudades, sugerencias).

**Decisiones:**

- Clase de contenedor y tipo de vehículo inicialmente como sugerencia, no lista cerrada (luego cambiado en HU-6 y HU-7).
- Se corrigió de paso que los catálogos solo se cargaban al abrir el formulario manual.

#### HU-6 — Ajustes tras la primera prueba de la grilla de Tarifas Plaza

**Issue:** [administracion#302](https://github.com/InnovacionIA-proyectos/administracion/issues/302) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

En la primera prueba, clase de viaje no se preseleccionaba, y se pidió cambiar controles. La validación debe canonizar el valor al del catálogo, clase de contenedor pasa a lista cerrada y vigencia a calendario.

**Decisiones:**

- La causa era la comparación exacta del desplegable frente a la normalizada de la validación.
- Clase de contenedor como lista cerrada en la grilla, más estricta que el formulario manual (confirmado con Sara).
- Se agregó el desempate de municipios igual al backend.

#### HU-7 — Ajustes tras la segunda prueba: prellenado, tipo de vehículo cerrado y estilo

**Issue:** [administracion#302](https://github.com/InnovacionIA-proyectos/administracion/issues/302) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Rango de peso y clase de contenedor tampoco se prellenaban y los desplegables tenían estilo nativo. Se extiende la canonización, tipo de vehículo pasa a lista cerrada y se unifica el estilo.

**Decisiones:**

- Clase de contenedor sí marca error en la grilla aunque el backend no lo valide (Sara).
- Tipo de vehículo no marca error porque el catálogo de SAP está incompleto; no se hardcodean códigos.
- Se acepta un parpadeo breve al prellenar los desplegables como costo del arreglo.

#### HU-8 — Mostrar el código en el desplegable de tipo de vehículo

**Issue:** [administracion#302](https://github.com/InnovacionIA-proyectos/administracion/issues/302) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El desplegable de tipo de vehículo de la grilla solo muestra el nombre. Debe mostrar "código · descripción", como en Reglas Solo-SICETAC, sin cambiar el valor guardado.

#### HU-9 — Confirmar el departamento de ciudades con nombre repetido (Tarifas Plaza)

**Issue:** [administracion#297](https://github.com/InnovacionIA-proyectos/administracion/issues/297) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La vista previa y el importador toman el primer municipio con ese nombre sin avisar. Debajo de la grilla debe listarse cada ciudad ambigua con el departamento elegido y un desplegable para cambiarlo antes de confirmar.

**Decisiones:**

- Decisión de Sara (2026-10-12): mostrar cuál se usó y de qué departamento para que el usuario confirme.
- El Excel reconstruido agrega columnas de departamento de origen y destino.
- Depende de HU-B21 en backend.

### Cubicador

#### HU-1 — Ícono de información en lugar del texto descriptivo fijo

**Issue:** [administracion#304](https://github.com/InnovacionIA-proyectos/administracion/issues/304) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El Cubicador tiene un párrafo descriptivo fijo bajo el título. Debe pasar a un ícono de información con el mismo texto.

### Tablero

#### HU-1 — Ícono de información en lugar del texto descriptivo fijo

**Issue:** [administracion#304](https://github.com/InnovacionIA-proyectos/administracion/issues/304) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El Tablero tiene un párrafo descriptivo fijo bajo el título. Debe pasar a un ícono de información con el mismo texto.

**Decisiones:**

- El texto se mueve tal cual, sin corregir que no menciona todos los bloques; actualizarlo sería un pedido aparte.

#### HU-2 — Dividir el Tablero en pestañas Productividad y SICE-TAC

**Issue:** [administracion#295](https://github.com/InnovacionIA-proyectos/administracion/issues/295) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Los tres bloques del Tablero están en una sola pantalla. Deben separarse en dos pestañas: Productividad (SLA/cola y KPIs del piloto) y SICE-TAC (monitoreo embebido), sin perder funciones.

**Decisiones:**

- Mismo patrón de pestañas accesibles de Biblioteca.
- No hace falta carga diferida por pestaña.

#### HU-3 — Reordenar, recolorear y filtrar por fecha el bloque de SLA y cola

**Issue:** [administracion#295](https://github.com/InnovacionIA-proyectos/administracion/issues/295) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Las tarjetas de SLA y cola no tienen orden ni colores de urgencia y no se filtran por fecha. Deben seguir un orden fijo con colores nuevos y compartir un único filtro Desde/Hasta con los KPIs del piloto.

**Decisiones:**

- Un solo filtro de fecha para ambos bloques (corrección de Sara).
- Cumplimiento SLA sí entra al filtro, por fecha de entrega; las demás tarjetas por fecha de creación.
- La parte de filtro depende de HU-B12.

#### HU-4 — Redefinir el tiempo de costeo del pliego, general y por tamaño

**Issue:** [administracion#295](https://github.com/InnovacionIA-proyectos/administracion/issues/295) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El tiempo de costeo solo mide el motor automático. Debe medir el ciclo real (hasta la entrega cuando hubo intervención manual) y permitir segmentar por tamaño de pliego.

**Decisiones:**

- Un solo promedio y porcentaje bajo 10 minutos sobre la distribución combinada.
- Filtro de tamaño: Todos, 1-10, 11-100 y más de 100 líneas; "Todos" sigue siendo el principal.
- Depende de HU-B13; limitación aceptada mientras no se cierre HU-B8.

#### HU-5 — Antigüedad de pendientes y evolución semanal

**Sin issue** · **Fecha:** 2026-10-09 (documento) · **Tipo:** Propuesta

**Estado según el documento:** Descartada — no implementar

Proponía dos indicadores nuevos: antigüedad de solicitudes pendientes y evolución semanal de SLA y tiempo de costeo. Se prototipó con datos de ejemplo y se revirtió.

**Decisiones:**

- Descartada por Sara (2026-10-12): no se aplican.
- Si se retomara, faltaría definir los cortes de antigüedad y crear un endpoint agrupado por semana.

## 3. Historias de backend de la revisión de NEXO

*Documento de planeación del 2026-10-09.* Historias de backend (repositorio `NEXO`) de la misma revisión. Varias se agruparon después en una sola issue del Sprint 5.

### Bandeja

#### [HU-B1] Filtro vigente/vencida por Fecha Límite y no por fecha de captura

**Issue:** [NEXO#289](https://github.com/InnovacionIA-proyectos/NEXO/issues/289) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

El filtro de vigencia de la Bandeja considera vigente lo capturado hace menos de 7 días, sin mirar la Fecha Límite (Deadline). Debe comparar el Deadline contra la fecha actual, tanto en el filtro como en el badge que se devuelve.

**Decisiones:**

- Una solicitud sin Fecha Límite cuenta como vigente por defecto (regla confirmada con Sara).
- No requiere cambio de frontend: el badge solo consume el campo corregido.

#### [HU-B2] Solicitudes revisadas por el Administrador de Fletes y no entregadas deben verse en Pendientes

**Issue:** [NEXO#289](https://github.com/InnovacionIA-proyectos/NEXO/issues/289) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Una solicitud con todas sus líneas revisadas a mano puede quedar indefinidamente sin entregar, y la vista Pendientes no la muestra porque solo mira líneas sin flete. Debe aparecer en Pendientes hasta que se haga clic en Entregar, sin dejar de verse en Atendidas por el Administrador de Fletes.

**Decisiones:**

- No se crea una categoría de atención nueva: estas solicitudes se suman a Pendientes.
- No aplica al auto-costeo (Automático), que ya tiene su propio camino a Entregada (S4-HU-15.6).

#### [HU-B25] Mostrar el resultado de la cotización y filtrar Esperando resultado en la Bandeja

**Issue:** [NEXO#289](https://github.com/InnovacionIA-proyectos/NEXO/issues/289) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Al marcar Ganada o Perdida la solicitud sale de la vista por defecto y la Bandeja no muestra el resultado; las entregadas sin cerrar quedan en el limbo. La Bandeja debe devolver el resultado por fila y aceptar un filtro por resultado (esperando, ganada, perdida).

**Decisiones:**

- Opción A de Sara: filtro nuevo Esperando resultado y columna Resultado, sin mezclarlas en Pendientes.
- El resultado lo ve el Comercial (y Admin); para AdminTarifas llega vacío (Sara, 2026-10-13).
- NEXO no usa el resultado para decisiones de negocio; eso corresponde a Cotifly.
- Con filtro de resultado no se aplica la restricción por defecto de Pendientes.

### Biblioteca

#### [HU-B3] CoordinadorFletes no puede buscar ciudades al crear un servicio de Almacenamiento

**Issue:** [NEXO#288](https://github.com/InnovacionIA-proyectos/NEXO/issues/288) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Los catálogos de ciudades, departamentos y municipios heredan la restricción de roles Comercial/Admin del controlador de captura, por lo que el rol CoordinadorFletes recibe 403 y el buscador de Biblioteca queda vacío sin aviso. Deben quedar abiertos a cualquier rol válido autenticado, manteniendo la creación de solicitudes y el análisis de Excel solo para Comercial/Admin.

**Decisiones:**

- Quitar los roles del nivel de clase y ponerlos solo en los métodos sensibles, porque en ASP.NET Core los atributos de clase y método se combinan con AND.
- Seguir el mismo patrón ya usado en el controlador de solicitudes.
- Queda por revisar caso a caso si Aprovisionamiento y Clientes también quedan abiertos.

### Tarifas Plaza

#### [HU-B4] La carga masiva de Tarifas Plaza no lee bien la fecha de Vigencia

**Issue:** [NEXO#288](https://github.com/InnovacionIA-proyectos/NEXO/issues/288) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

El importador lee la columna VIGENCIA como texto y la interpreta sin cultura fija, por lo que fechas guardadas como valor nativo de Excel se rechazan o se transforman. Debe leer primero el tipo nativo de fecha de la celda, luego formatos de texto explícitos y por último la cultura es-CO.

**Decisiones:**

- Reutilizar la función de lectura de fechas que ya funciona en el importador de Servicios Conexos.
- Hallazgo confirmado por dos investigaciones independientes (también en el documento de reparaciones del 2026-10-05).

#### [HU-B21] Indicar el departamento en la carga masiva cuando la ciudad se repite

**Issue:** [NEXO#288](https://github.com/InnovacionIA-proyectos/NEXO/issues/288) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

El importador agrupa municipios por nombre y toma el primero, así que una ciudad con nombre repetido puede quedar en el departamento equivocado sin aviso. Debe aceptar columnas opcionales de departamento de origen y destino y, si vienen, buscar por nombre y departamento.

**Decisiones:**

- Columnas opcionales: sin ellas el comportamiento es idéntico al actual.
- Si el par ciudad-departamento no existe, la fila se rechaza con un error claro.
- Las columnas las llena la vista previa del frontend, sin cambiar el contrato del endpoint.

### Carga masiva de Excel

#### Regla general de lectura de fechas en todo importador de Excel

**Issue:** [NEXO#288](https://github.com/InnovacionIA-proyectos/NEXO/issues/288) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Se revisaron los importadores de Excel existentes: solo Tarifas Plaza tiene el problema de fechas; Servicios Conexos ya está bien y Reglas Solo-SICETAC no tiene fechas. Cualquier importador nuevo o modificado con columna de fecha debe usar el mismo patrón (tipo nativo, formatos explícitos, cultura es-CO).

**Decisiones:**

- Prohibido el atajo de leer como texto y parsear sin cultura explícita.
- Conviene mover la función de lectura de fechas a un helper compartido para que se reutilice.

#### Vista previa editable de la carga masiva resuelta solo en frontend

**Issue:** [administracion#302](https://github.com/InnovacionIA-proyectos/administracion/issues/302) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Se evaluó crear endpoints separados para analizar y confirmar el Excel. Se decidió que la vista previa editable de Servicios de Biblioteca y Tarifas Plaza se resuelve completamente en el navegador, reenviando el Excel corregido al endpoint de importación existente.

**Decisiones:**

- No se necesita backend ni cambio de contrato (decisión de Sara, 2026-10-09).
- Riesgo aceptado: las validaciones de Tarifas Plaza se replican en JavaScript y deben mantenerse en sincronía con el backend.

### Reglas Solo-SICETAC

#### [HU-B5] Quitar los 5 casos especiales escritos a mano del catálogo de tipos de vehículo

**Issue:** [NEXO#288](https://github.com/InnovacionIA-proyectos/NEXO/issues/288) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

El catálogo de tipos de vehículo sumaba al catálogo oficial de SAP cinco valores fijos en el código (contenedores 20'/40' y un camión liviano) que ya no corresponden a ningún caso de negocio. Debe devolver solo el catálogo oficial, lo que corrige el desplegable en todas las pantallas que lo consumen.

**Decisiones:**

- Sara confirmó (2026-10-12) que los 5 casos ya no son necesarios: se eliminan, no se mueven a una tabla editable.
- Las reglas ya guardadas con esos códigos no se tocan.

#### [HU-B6] Exportar a Excel las Reglas Solo-SICETAC

**Issue:** [NEXO#288](https://github.com/InnovacionIA-proyectos/NEXO/issues/288) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Reglas Solo-SICETAC solo permite importar y descargar una plantilla en blanco, no las reglas ya cargadas. Debe existir un endpoint de exportación con el mismo formato que acepta la importación, para editar y reimportar.

**Decisiones:**

- Seguir el mismo patrón de exportación que ya tiene Tarifas Plaza.

### Mesa de Costeo

#### [HU-B7] Consultar si ya existe una cotización sin crear nada

**Issue:** [NEXO#286](https://github.com/InnovacionIA-proyectos/NEXO/issues/286) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

El único camino para traer una cotización es crear-o-recuperar, que tiene el efecto secundario de crearla; por eso la Mesa de Costeo no muestra la cotización real al abrir. Debe existir un endpoint de solo lectura por solicitud que devuelva la cotización o 204 si no existe.

**Decisiones:**

- Responder 204 (no 404) cuando no hay cotización, porque es un estado válido.
- Reutilizar el repositorio existente, que ya permite filtrar por expresión; no se toca.

#### [HU-B8] Impedir entregar una cotización con líneas del pliego sin costear

**Issue:** [NEXO#283](https://github.com/InnovacionIA-proyectos/NEXO/issues/283) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

La entrega solo valida que la cotización tenga al menos una línea, así que se puede entregar con líneas del pliego aún pendientes. El backend debe rechazar la entrega si alguna línea de la solicitud sigue por costear, con un código de error nuevo.

**Decisiones:**

- La regla debe vivir en el backend y no depender solo del frontend.
- Se mantiene la validación existente para el caso de cero líneas.

#### [HU-B9] Exponer el desglose piso + tiempos logísticos del candidato SICE-TAC

**Issue:** [NEXO#286](https://github.com/InnovacionIA-proyectos/NEXO/issues/286) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

El candidato SICE-TAC solo expone el valor total ya combinado, sin distinguir el piso base de los tiempos logísticos. Debe exponer ambos valores por separado para que la pantalla muestre el desglose.

**Decisiones:**

- Los tiempos logísticos se calculan como diferencia contra el piso, para que funcione también en el caso de respaldo de DWSice.
- Plaza e Histórico no llevan este desglose; solo aplica a SICE-TAC.

#### [HU-B10] Editar un flete manual ya guardado sin duplicar la línea

**Issue:** [NEXO#286](https://github.com/InnovacionIA-proyectos/NEXO/issues/286) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Agregar un flete siempre crea una fila nueva, así que habilitar la edición en pantalla duplicaría la línea e inflaría el total. Se creó un comando y un endpoint de edición que actualizan la línea existente, con auditoría del valor anterior y nuevo.

**Decisiones:**

- Solo se editan fletes de fuente Manual y solo con la cotización en Borrador.
- La fuente del flete nunca cambia en una edición.
- Mismos roles que para agregar un flete.

#### [HU-B11] Exponer si una propuesta vino de la regla Solo-SICETAC (reemplazada)

**Sin issue** · **Fecha:** 2026-10-09 (documento) · **Tipo:** Issue planeada

**Estado según el documento:** Reemplazada — no implementar.

Proponía un booleano en el DTO para indicar si la propuesta salió de la regla Solo-SICETAC. Quedó corto al definirse tres modos de decisión y no debe implementarse.

**Decisiones:**

- Reemplazada por el campo ModoDecision de HU-B16; se conserva solo para no romper referencias.

#### Tres campos de clasificación en el DTO de costeo (línea de negocio, clase de viaje, clase de contenedor)

**Issue:** [NEXO#311](https://github.com/InnovacionIA-proyectos/NEXO/issues/311) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-19 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Para los chips de la Mesa de Costeo, el DTO de la línea de costeo se amplió con línea de negocio, clase de viaje y clase de contenedor, que ya existían en la entidad. Es contexto, no una HU pendiente.

**Decisiones:**

- Campos opcionales para no romper nada existente.

#### [HU-B20] Exponer el valor de la mercancía de cada línea en el costeo

**Issue:** [NEXO#286](https://github.com/InnovacionIA-proyectos/NEXO/issues/286) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

El valor de la mercancía ya se captura y se guarda en la línea de solicitud, pero el DTO de Mesa de Costeo no lo envía. Debe agregarse como campo opcional para mostrarlo como un chip más.

**Decisiones:**

- Mismo patrón que los campos de línea de negocio, clase de viaje y clase de contenedor; campo opcional al final.

### Tablero

#### [HU-B12] Filtro de fecha para el bloque Cumplimiento SLA / cola

**Issue:** [NEXO#287](https://github.com/InnovacionIA-proyectos/NEXO/issues/287) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Las tarjetas de la cola (pendientes, en costeo, entregadas, descartadas y total) son un acumulado histórico sin filtro. El endpoint de cumplimiento debe aceptar desde/hasta opcionales y sin ellos comportarse igual que hoy.

**Decisiones:**

- Las tarjetas de la cola filtran por fecha de creación de la solicitud.
- Cumplimiento SLA también se filtra, pero por fecha de entrega de la cotización (corrige la primera versión, que lo dejaba sin filtro).
- Un solo filtro de fecha compartido con KPIs del Piloto.
- Descartadas se aproxima por fecha de creación, porque no existe fecha de descarte (decisión de Sara).

#### [HU-B13] Tiempo de costeo del pliego híbrido y segmentado por tamaño

**Issue:** [NEXO#287](https://github.com/InnovacionIA-proyectos/NEXO/issues/287) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

La métrica de tiempo de costeo solo mide lo que tarda el motor automático. Debe usar la duración de la saga cuando el pliego se auto-costeó completo y, si hubo intervención manual, el tiempo hasta la entrega real; además debe poder segmentarse por tamaño de pliego.

**Decisiones:**

- Pliegos parciales aún no entregados no entran en el cálculo.
- Rangos de tamaño: 1-10, 11-100 y más de 100 líneas; sin parámetro se calcula sobre todos.
- La limitación de HU-B8 se documenta como conocida, no como bloqueante.

### Motor de decisión de tarifa

#### Plaza externa de pruebas solo tiene tarifas de Contenedor

**Sin issue** · **Fecha:** 2026-10-09 (documento) · **Tipo:** Pendiente

**Estado según el documento:** Fuera del alcance de HU-B14/B15/B16; pendiente de decisión de Sara.

En el ambiente de pruebas la tabla de plaza externa solo tiene filas de clase de viaje Contenedor, así que para Cupo, Carga Suelta o Extradimensionado nunca encuentra plaza. Con la doble fuente (HU-B14) la tabla interna podría cubrir esos casos.

#### Los dos caminos de auto-costeo no dejan la línea igual

**Issue:** [NEXO#282](https://github.com/InnovacionIA-proyectos/NEXO/issues/282) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Al capturar, el flete se guarda en la cotización; al cargar un pliego, la línea queda auto-resuelta pero sin flete hasta que alguien aplica las propuestas. Se considera cubierto por la decisión de no guardar precio en consulta completa.

**Decisiones:**

- Para los modos automáticos se recomienda que el pliego también guarde el flete (HU-B16, paso 4).

#### [HU-B14] Doble fuente para Tarifas Plaza: SAP primero y tabla de NEXO después

**Issue:** [NEXO#281](https://github.com/InnovacionIA-proyectos/NEXO/issues/281) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

La tabla propia de tarifas de plaza que se gestiona en Biblioteca nunca se consulta al costear; solo se usa SAP. El motor debe buscar primero en SAP (camino oficial y selector tolerante) y, si no hay una vigente, en la tabla interna, con cruce exacto y luego tolerante.

**Decisiones:**

- Orden: externa (SAP) y luego interna (NEXO); vigente gana a vencida aunque venga de la interna.
- Si hay vigente en ambas gana la externa; si solo hay vencidas, la referencia es la externa.
- El campo Fuente sigue siendo plaza para no romper el mapeo a FuenteFlete; el origen va en un campo nuevo TablaOrigen (SAP/NEXO).
- Los ajustes de vigencia solo aplican a filas de SAP.

#### [HU-B15] Doble fuente para Histórico: log de SAP primero y tabla histórica de NEXO después

**Issue:** [NEXO#281](https://github.com/InnovacionIA-proyectos/NEXO/issues/281) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

La tabla histórica de tarifas de plaza se llena por trigger pero no está mapeada en la aplicación, así que nunca se consulta. Debe mapearse en solo lectura y usarse como segunda fuente del histórico cuando el log de SAP no tenga nada.

**Decisiones:**

- Ventana de 3 meses por fecha de reemplazo y último registro por tarifa.
- Sin migración de EF y sin escritura desde la aplicación: solo el trigger escribe.
- La fuente conserva el nombre historico_sap para no romper el mapeo; TablaOrigen indica NEXO.
- Se asume que el histórico interno nace vacío y aportará poco al inicio.

#### [HU-B16] Nuevo orden de decisión (pasos 1 a 4) y campo ModoDecision

**Issue:** [NEXO#282](https://github.com/InnovacionIA-proyectos/NEXO/issues/282) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

El flujo de decisión del código no coincide con el que espera el negocio. Debe decidir en orden regla Solo-SICETAC, plaza vigente mayor que el piso y consulta completa, informar el modo en la propuesta y, en consulta completa, dejar la línea pendiente para que el Administrador de Tarifas elija.

**Decisiones:**

- Orden confirmado por Sara (2026-10-12): Solo-SICETAC, Tarifas Plaza y consulta completa.
- Solo-SICETAC sin piso pasa a consulta completa, no a atención manual.
- La plaza vigente se usa sola solo si es estrictamente mayor que el piso; empate o sin piso va a consulta completa.
- En consulta completa el auto-costeo no guarda precio; solo se autocostean SoloSicetac y PlazaVigenteMayor.
- El auto-aplicado silencioso del Comercial no toca líneas de consulta completa (parámetro soloAutomaticas).
- ModoDecision viaja como texto; reemplaza a HU-B11.
- Efecto esperado: baja el porcentaje de líneas auto-costeadas y llegan más pendientes a Mesa de Costeo.

#### [HU-B17] Guardar el candidato Histórico de la propuesta desde Mesa de Costeo

**Issue:** [NEXO#283](https://github.com/InnovacionIA-proyectos/NEXO/issues/283) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Al guardar como flete el histórico que muestra la propuesta, la validación lo compara contra otra tabla (históricas sugeridas) y puede rechazarlo aunque sea válido; además, aplicar propuestas en lote sí lo permite. El servidor debe aceptar el histórico si coincide con el candidato de la propuesta recalculada.

**Decisiones:**

- Se elige la opción A (Sara, 2026-10-12): el servidor recalcula la propuesta y no confía en lo que mande el navegador.
- Una histórica que no viene de las sugeridas vigentes ni de la propuesta sigue rechazándose.

#### [HU-B18] Nunca guardar un flete por debajo del piso SICE-TAC

**Issue:** [NEXO#283](https://github.com/InnovacionIA-proyectos/NEXO/issues/283) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Hoy el backend permite guardar fletes por debajo del piso legal SICE-TAC y en pantalla solo se pide confirmación para los manuales. Agregar y editar un flete deben rechazarse si el valor queda bajo el piso de la línea, sea cual sea la fuente.

**Decisiones:**

- Bloqueo con aviso, sin opción de confirmar (decisión de Sara, 2026-10-12); reemplaza el popup de HU-12.
- Se valida también en el servidor para que no se pueda saltar llamando la API.
- Si la línea no tiene piso, se guarda normal.
- Auto-costeo y aplicar propuestas no necesitan la validación porque nunca proponen bajo el piso.

#### [HU-B19] Investigar la Desviación 1 de QA: plaza vigente y mayor que no se propuso

**Issue:** [NEXO#284](https://github.com/InnovacionIA-proyectos/NEXO/issues/284) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

En varias líneas reales verificadas por QA el sistema propuso el piso SICE-TAC habiendo una plaza SAP vigente mucho mayor. Hay que instrumentar la búsqueda de plaza, reproducir las rutas afectadas y encontrar en qué filtro se pierde la fila; es solo investigación.

**Decisiones:**

- Se decide investigar porque afecta directamente el valor cotizado (Sara delegó la decisión el 2026-10-12).
- Hipótesis por confirmar: filtro exacto de clase de contenedor o falta de relación entre zona granular y zona agregada.

#### [HU-B22] Usar el traductor de zonas al buscar la tarifa de plaza en SAP

**Issue:** [NEXO#281](https://github.com/InnovacionIA-proyectos/NEXO/issues/281) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

SAP guarda las ciudades por código de zona y la búsqueda compara si el código contiene el nombre, lo que falla con zonas agregadas y abreviaturas. Debe usarse el traductor de zonas ya existente (expansión de abreviaturas y alias curados) en la búsqueda de plaza y en el selector tolerante de plaza e histórico.

**Decisiones:**

- Decisión de Sara (2026-10-12): conectarlo.
- Hacerlo junto o después de HU-B19 para medir cuánto mejora.
- Opcional: mover el traductor a una carpeta común.

### Servicios conexos de la cotización

#### [HU-B23] Quitar un servicio conexo ya agregado

**Issue:** [NEXO#286](https://github.com/InnovacionIA-proyectos/NEXO/issues/286) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

No existe forma de quitar un conexo agregado por error a una cotización. Debe existir un endpoint de borrado que recalcule el total, valide el estado y deje auditoría del servicio, cantidad y valor quitados.

**Decisiones:**

- Mismos roles que para agregar (incluido AdminTarifas).
- Misma regla de estado de HU-B24: se bloquea solo con la cotización Ganada o Perdida.

#### [HU-B24] Reglas de edición de conexos: AdminTarifas puede y solo Ganada/Perdida bloquea

**Issue:** [NEXO#286](https://github.com/InnovacionIA-proyectos/NEXO/issues/286) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

AdminTarifas no tiene permiso para agregar conexos y nadie puede hacerlo con la cotización Entregada. Se debe permitir agregar y quitar conexos mientras el resultado esté sin definir, marcar la modificación posterior a la entrega y avisar al comercial cuando el cambio lo hace otra persona.

**Decisiones:**

- Regla única en backend: se permite mientras el resultado sea Sin definir; Ganada o Perdida responde 409.
- La marca de modificación tras la entrega es informativa y no cambia el estado ni la fecha de entrega.
- El aviso se envía solo si quien cambia no es el creador de la solicitud y la cotización ya estaba entregada.
- Queda por decidir si varios cambios seguidos se agrupan en un solo correo (no bloquea).

### Contrato de captura con Cotifly

#### [HU-B26] Contrato de captura con Cotifly: ruta, orden, recomendación, líneas sin vehículo y normalización

**Issue:** [NEXO#297](https://github.com/InnovacionIA-proyectos/NEXO/issues/297) · **Creada:** 2026-10-14 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Cotifly envía una línea por camión y NEXO necesita guardar y devolver el grupo de ruta, el orden y la recomendación del cubicador por línea, con las líneas ordenadas. Además, una línea de Cupo sin vehículo no debe autocostearse y los nombres de vehículos y rutas deben normalizarse para evitar duplicados.

**Decisiones:**

- El código de mercancía no se envía en la captura.
- El endpoint de cubicación de NEXO no se retira.
- La regla de línea sin vehículo aplica solo a Cupo, no a contenedor.
- Cotifly puede dejar de enviar posiciones; los códigos del cubicador llegan en los campos actuales de tipo de vehículo y carrocería.

## 4. Propuestas a futuro

*Documento de planeación del 2026-10-09.* Mejoras que no son errores ni decisiones inmediatas; se evalúan más adelante.

### Permitir editar un flete manual ya guardado

**Issue:** [NEXO#286](https://github.com/InnovacionIA-proyectos/NEXO/issues/286) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Una vez guardado un flete no hay forma de corregirlo en pantalla, y el backend solo sabe agregar filas, por lo que una edición duplicaría la línea e inflaría el total. Se proponía un comando de edición con auditoría y un formulario editable en la pantalla.

**Decisiones:**

- Sara pidió avanzar directamente (2026-10-12); el detalle quedó en HU-B10 (backend) y HU-15 (frontend).
- Alternativa más barata planteada: advertir antes de guardar que el valor no podrá editarse.

### Usar el nombre de la hoja del Excel al importar

**Sin issue** · **Fecha:** 2026-11-03 · **Tipo:** Propuesta · **Responsable:** ivasco-dev (backend)

Al importar un Excel, el usuario escoge la hoja, pero el mapeo solo recibe sus encabezados y filas: el nombre de la hoja se pierde. En los formularios de licitación ese nombre suele decir algo de todas las filas. Por ejemplo, «Formulario Oferta Impo y Expo.» indica importación o exportación y «Formulario Oferta Nacional» indica carga nacional. Si NEXO lo leyera, podría proponer `lineaNegocio` o `claseViaje` para todas las filas de la hoja, sin que el usuario lo indique línea por línea.

**Decisiones:**

- Salió al armar la carga inicial del diccionario (S6-HU-33.2, #357). No entra en esa issue porque requiere cambiar el backend.
- Implicaría que el mapeo reciba el nombre de la hoja y agregar un tipo de término `hoja` a `tbl_diccionario_cliente`. El `CHECK` de `Tipo` hoy solo admite `columna`, `unidad` y `valor`.

## 5. Issues del Sprint 5

*Documento de planeación del 2026-10-12.* Las issues del Sprint 5, en seis milestones (S5-HU-19 a S5-HU-24). En `administracion` se crearon con la numeración S5-HU-26 a S5-HU-30.

### S5-HU-19 Motor de decisión de tarifa

#### [S5-HU-19.1] Plaza e histórico en dos fuentes (SAP y NEXO) y traductor de zonas

**Issue:** [NEXO#281](https://github.com/InnovacionIA-proyectos/NEXO/issues/281) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

El motor busca la plaza y el histórico solo en SAP: la tabla propia tbl_tarifas_plaza nunca se consulta y el histórico propio no tiene mapeo EF. Además la búsqueda por zonas de SAP falla con zonas agregadas o abreviaturas. Debe quedar la búsqueda en cascada SAP oficial, SAP tolerante y luego NEXO, con el histórico de SAP y de respaldo el de NEXO, y el traductor de zonas aplicado al tarifario.

**Decisiones:**

- Gana la primera plaza vigente, externa (SAP) primero; la vencida solo queda como referencia si no hay ninguna vigente.
- La aplicación nunca escribe en tbl_tarifas_plaza_historico; solo la llena el trigger.
- Se mapea el histórico propio sin migración EF porque la tabla ya existe.
- Se conserva la fuente 'plaza' y se agrega TablaOrigen (SAP/NEXO) para no romper el mapeo a FuenteFlete.
- Ventana del histórico propio: 3 meses; los ajustes de vigencia solo aplican a filas de SAP.

#### [S5-HU-19.2] Nuevo orden de decisión (ModoDecision) y auto-costeo sin precio en consulta completa

**Issue:** [NEXO#282](https://github.com/InnovacionIA-proyectos/NEXO/issues/282) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

El motor hoy tiene dos caminos y decide por la regla del mayor, sin el paso de plaza vigente mayor; además el auto-costeo guarda precio incluso cuando debería decidir una persona. Debe quedar la decisión en tres modos (SoloSicetac, PlazaVigenteMayor, ConsultaCompleta) y que solo se auto-costeen los dos modos automáticos.

**Decisiones:**

- En consulta completa el auto-costeo no guarda precio: la línea queda PorCostear y decide el Administrador de Tarifas.
- La plaza vigente estrictamente mayor que el piso gana sola, sin consultar el histórico.
- Regla Solo-SICETAC sin piso pasa a consulta completa en vez de 'atención manual'.
- aplicar-propuestas recibe soloAutomaticas opcional; sin él aplica todas (botón del Administrador).
- ModoDecision viaja como texto en el DTO, no como número de enum.

#### [S5-HU-19.3] Validaciones de flete en el servidor: bajo el piso, histórico de la propuesta y entrega con líneas pendientes

**Issue:** [NEXO#283](https://github.com/InnovacionIA-proyectos/NEXO/issues/283) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Al agregar o editar un flete a mano nada lo compara con el piso SICE-TAC; guardar el histórico que muestra la propuesta puede fallar con HISTORICA_VENCIDA; y se puede entregar una cotización con líneas sin costear. Debe quedar la validación en el servidor para los tres casos.

**Decisiones:**

- Nunca se guarda un flete por debajo del piso, de ninguna fuente; se bloquea sin pedir confirmación (decisión de negocio 2026-10-12).
- El bloqueo va también en el servidor para que no se salte llamando la API.
- Opción A: una histórica se acepta si hay sugerida vigente o si coincide con el histórico de la propuesta recalculada en el servidor.
- La entrega exige todas las líneas AutoResuelta o Revisada (COTIZACION_CON_LINEAS_PENDIENTES).

#### [S5-HU-19.4] Investigar por qué no se propone una plaza vigente y mayor (desviación 1 de QA)

**Issue:** [NEXO#284](https://github.com/InnovacionIA-proyectos/NEXO/issues/284) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

QA encontró en un pliego real cuatro líneas donde existía una plaza SAP vigente mayor que el piso y el sistema propuso el piso, cobrando de menos. La causa raíz no está confirmada. Debe quedar la causa documentada y la issue de corrección creada.

**Decisiones:**

- La issue solo investiga, no corrige; se usan logs temporales que luego se retiran.
- Conviene correrla después de 19.1 para medir el efecto del traductor de zonas.
- Que la plaza SAP de pruebas solo tenga Contenedor es un tema de datos, no un bug.

#### [S5-HU-19.5] QA: verificación end-to-end del flujo de decisión de tarifa

**Issue:** [NEXO#285](https://github.com/InnovacionIA-proyectos/NEXO/issues/285) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-30 · **Responsable:** kcano-ux

Validar que el motor cumple el flujo definido (los 10 casos de la tabla, prueba de 20 líneas) y que las reglas nuevas no se pueden saltar por la API. Incluye plaza propia de NEXO, bloqueo bajo el piso, consulta completa sin precio, entrega con pendientes y baja del % auto-costeado.

**Decisiones:**

- Se requiere aprobación explícita de QA registrada en la issue.

#### Decidir si el caso 7 (solo piso SICE-TAC, sin plaza) se aplica solo

**Sin issue** · **Fecha:** 2026-10-12 (documento) · **Tipo:** Pendiente

**Estado según el documento:** Pendiente de decisión de negocio (no bloquea).

En el caso 7 de la tabla de decisión solo existe el piso SICE-TAC y no hay plaza; hoy la línea queda pendiente en consulta completa. Falta que negocio decida si debería aplicarse sola.

**Decisiones:**

- Mientras no se decida, queda pendiente, como se pidió.

### S5-HU-20 Mesa de Costeo

#### [S5-HU-20.1] API de Mesa de Costeo: valor de mercancía, desglose piso + tiempos, cotización existente y servicios conexos

**Issue:** [NEXO#286](https://github.com/InnovacionIA-proyectos/NEXO/issues/286) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

La Mesa no recibe el valor de mercancía, el desglose del piso SICE-TAC ni puede consultar la cotización existente sin crearla; tampoco se puede editar un flete manual sin duplicarlo. En conexos, AdminTarifas no puede agregar, no se puede cambiar una cotización entregada y no existe quitar. Debe quedar la API completa para esas necesidades, con auditoría y aviso por correo al comercial.

**Decisiones:**

- El desglose del piso lo calcula el backend; el frontend no lo reconstruye para no revivir el bug de horas.
- GET por-solicitud devuelve 204 y nunca crea cotización.
- Conexos se pueden cambiar mientras el resultado sea SinDefinir (Borrador o Entregada); Ganada/Perdida responde 409 COTIZACION_CERRADA.
- Cambiar una cotización entregada llena ModificadaTrasEntregaAt sin mover Estado ni EntregadaAt (no afecta SLA).
- El correo se envía solo si el cambio lo hace alguien distinto al creador sobre una cotización Entregada.
- Por decidir (no bloquea): agrupar varios cambios en un solo correo.

#### [S5-HU-20.2] Propuesta por modo, «Usar este», bloqueo bajo el piso y «Aplicar todas las propuestas»

**Issue:** [administracion#292](https://github.com/InnovacionIA-proyectos/administracion/issues/292) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La propuesta muestra tres cajas fijas y un único botón; para elegir otro candidato hay que reescribir el valor, y el bajo piso solo pedía confirmación. Debe quedar la tabla de origen y el modo visibles, un botón «Usar este» por candidato en consulta completa, el bloqueo bajo el piso para cualquier fuente y el botón de aplicar todas con confirmación.

**Decisiones:**

- En consulta completa el sistema sugiere y el Administrador de Tarifas decide con un clic; en los modos automáticos no hay nada que elegir.
- El popup «Confirmar de todas formas» se reemplaza por un bloqueo sin opción de guardar.
- El botón de aplicar propuestas no se quita: se renombra «Aplicar todas las propuestas» y pide confirmación.
- Al Comercial solo se le llenan solas las líneas de modos automáticos.

#### [S5-HU-20.3] Mesa de Costeo: ajustes menores y correcciones de pantalla

**Issue:** [administracion#293](https://github.com/InnovacionIA-proyectos/administracion/issues/293) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Agrupa varias correcciones de la Mesa: chip de valor de mercancía, desglose piso + tiempos, mostrar la cotización existente al abrir, edición de flete manual, entregar solo con todo costeado, Ganada/Perdida con el mismo criterio del resumen, permiso de cubicar para más roles y tamaño del tooltip.

**Decisiones:**

- Consultar siempre la cotización existente al abrir sin crear borrador solo por mirar.
- «Editar» solo para fletes de fuente Manual y sin cambiar la fuente.
- Mover el estilo cabina-tooltip a styles.css global para que se vea igual fuera de Bandeja.
- Agregar cubicarLinea a Comercial, CoordinadorFletes, LiderProceso y ServiciosLogisticos.

#### [S5-HU-20.4] QA: verificación end-to-end de Mesa de Costeo

**Issue:** [administracion#294](https://github.com/InnovacionIA-proyectos/administracion/issues/294) · **Creada:** 2026-10-13 · **Estado:** Abierta · **Responsable:** kcano-ux

Verificar con un pliego real los tres modos, «Usar este», aplicar todas, edición manual, bloqueo bajo el piso, roles, entrega con pendientes y la parte de conexos (quitar, entregada, Ganada/Perdida, correo, PDF).

**Decisiones:**

- Se requiere aprobación explícita de QA registrada en la issue.
- Probar después del despliegue del backend.

#### [S5-HU-20.5] Contrato de captura con Cotifly (backend): grupo de ruta, orden, recomendación de vehículo, líneas sin vehículo y normalización

**Issue:** [NEXO#297](https://github.com/InnovacionIA-proyectos/NEXO/issues/297) · **Creada:** 2026-10-14 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Cotifly manda una línea por camión y NEXO no sabe qué líneas son de la misma ruta, en qué orden van ni de qué recomendación salió cada camión; además una línea de Cupo sin vehículo puede autocostearse con un precio bajo y la normalización de catálogos crea duplicados por tildes o separadores. Debe quedar el contrato ampliado con campos opcionales, las líneas sin vehículo pendientes y la normalización corregida.

**Decisiones:**

- Todos los campos nuevos son opcionales: una captura sin ellos funciona igual que hoy.
- «Exige vehículo» solo aplica a Cupo; Carga suelta y Extradimensionado no (decidido por Sara).
- La línea sin vehículo queda PorCostear con motivo y el motor sigue mostrando candidatos de referencia.
- El código de mercancía no se manda en la captura.
- El endpoint de cubicación de NEXO no se retira; basta con que Cotifly deje de llamarlo.
- posiciones[] sigue aceptándose como opcional.

#### [S5-HU-20.6] Mesa de Costeo: líneas agrupadas por ruta y aviso de línea sin vehículo

**Issue:** [administracion#310](https://github.com/InnovacionIA-proyectos/administracion/issues/310) · **Creada:** 2026-10-14 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Con una línea por camión, una ruta puede tener varias líneas y la Mesa las muestra planas. Debe quedar la agrupación por ruta en el orden del servidor y un aviso en las líneas sin vehículo que explique por qué quedaron pendientes.

**Decisiones:**

- Una solicitud sin grupo de ruta se ve exactamente igual que hoy.
- El aviso de línea sin vehículo no bloquea costear desde la Mesa.

#### [S5-HU-20.7] Mesa de Costeo: ofrecer guardar el valor manual como tarifa plaza

**Issue:** [administracion#313](https://github.com/InnovacionIA-proyectos/administracion/issues/313) · **Creada:** 2026-10-14 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Cuando el Administrador de Tarifas escribe un valor manual en la Mesa, ese valor se pierde como referencia. Debe quedar un modal que ofrezca guardarlo como tarifa plaza (crear o actualizar), con la fila prellenada, usando los endpoints existentes y sin cambios de backend.

**Decisiones:**

- La pregunta aparece solo con valor manual (decisión de Sara), no al elegir candidatos ni con «Aplicar todas».
- Solo para roles con verTarifasPlaza (AdminTarifas y Admin), y después de validar el piso.
- Vigencia obligatoria y detalles opcionales en este flujo.
- Se guarda primero el flete y después la plaza; un fallo de la plaza no deshace el flete.

### S5-HU-21 Tablero

#### [S5-HU-21.1] API del Tablero: filtro de fecha en la cola y tiempo de costeo híbrido por tamaño

**Issue:** [NEXO#287](https://github.com/InnovacionIA-proyectos/NEXO/issues/287) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Las tarjetas de la cola del Tablero son acumulados sin filtro y el tiempo de costeo mide solo al robot, no cuándo la cotización queda lista. Debe quedar un filtro Desde/Hasta en el endpoint de cumplimiento y un tiempo de costeo que, para pliegos con intervención manual, llegue hasta la entrega, con corte por tamaño.

**Decisiones:**

- Un solo filtro de fecha compartido con los KPIs del Piloto; Cumplimiento SLA filtra por EntregadaAt y las demás tarjetas por fecha de creación.
- Descartadas se aproxima por fecha de creación (no existe fecha de descarte).
- La fórmula de SLA no cambia; el hueco de vencidas sin entregar fue aceptado por negocio.
- Pliego automático: fin de la saga menos inicio; con intervención manual: desde la solicitud hasta la entrega.
- Se segmenta por tamaño (1-10, 11-100, +100) porque el procesamiento es paralelo y dividir por línea no sirve.

#### [S5-HU-21.2] Tablero: pestañas, orden y color de la cola, filtro de fecha compartido y selector de tamaño

**Issue:** [administracion#295](https://github.com/InnovacionIA-proyectos/administracion/issues/295) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La pantalla tenía tres bloques seguidos. Debe quedar separada en pestañas Productividad y SICE-TAC, con la cola reordenada y coloreada por urgencia, un único filtro de fecha para cola y Piloto, y un selector de tamaño para el tiempo de costeo.

**Decisiones:**

- Los gráficos de antigüedad y evolución semanal se descartaron (no implementar).
- Un solo control de fecha en pantalla que aplica a los dos bloques.

#### [S5-HU-21.3] QA: verificación de los cálculos del Tablero

**Issue:** [administracion#296](https://github.com/InnovacionIA-proyectos/administracion/issues/296) · **Creada:** 2026-10-13 · **Estado:** Abierta · **Responsable:** kcano-ux

Validar cada tarjeta de la cola contra conteos en base, con y sin filtro, el tiempo de costeo con pliegos automáticos y manuales, el corte por tamaño y que solo AdminTarifas y Admin vean el Tablero.

**Decisiones:**

- Se espera que el % de líneas auto-costeadas baje tras 19.2.
- Se requiere aprobación explícita de QA registrada en la issue.

### S5-HU-22 Tarifas Plaza y carga masiva

#### [S5-HU-22.1] Carga masiva y catálogos: fecha de vigencia, casos especiales de vehículos, departamento, ciudades y export

**Issue:** [NEXO#288](https://github.com/InnovacionIA-proyectos/NEXO/issues/288) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

El importador de Tarifas Plaza no lee fechas nativas de Excel, el catálogo de vehículos suma cinco valores escritos a mano, las ciudades con nombre repetido caen en el departamento equivocado, CoordinadorFletes no recibe sugerencias de ciudades por permisos y Reglas Solo-SICETAC no se puede exportar. Debe quedar cada punto corregido.

**Decisiones:**

- Reutilizar la lectura de fechas ya probada en Servicios Conexos (tipo nativo, formatos explícitos y es-CO).
- Los cinco casos especiales de vehículos se eliminan, no se mueven a una tabla (negocio confirmó que no se necesitan).
- Los códigos 3S2 y 2L3 faltantes en SAP no se hardcodean.
- Columnas opcionales DEPARTAMENTO ORIGEN/DESTINO para desambiguar ciudades.
- Sacar los roles del nivel de clase del controlador de captura porque se combinan con AND.

#### [S5-HU-22.2] Carga masiva y reglas: elegir departamento de ciudades repetidas y exportar Excel

**Issue:** [administracion#297](https://github.com/InnovacionIA-proyectos/administracion/issues/297) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La vista previa de carga masiva toma el primer municipio con un nombre sin avisar, y Reglas Solo-SICETAC no tiene botón de exportar. Debe quedar un recuadro que muestre qué municipio y departamento se usará en ciudades repetidas, permitiendo cambiarlo, y el botón «Exportar Excel».

**Decisiones:**

- Mostrar el municipio elegido antes de confirmar para que el usuario lo valide o lo cambie (decisión de negocio).
- No se valida tipo de vehículo contra rango de peso.

#### [S5-HU-22.3] QA: verificación de Tarifas Plaza y carga masiva

**Issue:** [administracion#298](https://github.com/InnovacionIA-proyectos/administracion/issues/298) · **Creada:** 2026-10-13 · **Estado:** Abierta · **Responsable:** kcano-ux

Verificar la lectura de fechas en todos los formatos, la elección de departamento en ciudades repetidas, el catálogo de vehículos sin casos especiales, las sugerencias de ciudades para CoordinadorFletes y la exportación y reimportación de reglas.

**Decisiones:**

- Se requiere aprobación explícita de QA registrada en la issue.

### S5-HU-23 Bandeja

#### [S5-HU-23.1] Bandeja backend: vigencia por fecha límite, pendientes por entregar y resultado de la cotización

**Issue:** [NEXO#289](https://github.com/InnovacionIA-proyectos/NEXO/issues/289) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-13 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

La Bandeja calcula «vigente» por antigüedad de captura y no por fecha límite, no muestra en Pendientes las solicitudes revisadas sin entregar y no expone el resultado de la cotización. Debe quedar la vigencia por fecha límite, Pendientes ampliado y un campo y filtro de resultado.

**Decisiones:**

- Una solicitud sin fecha límite cuenta como vigente.
- No se crea categoría nueva: las revisadas sin entregar se suman a Pendientes.
- Resultado como columna y filtro «Esperando resultado» (opción A de Sara), sin mezclarlo en Pendientes.
- El resultado solo es visible para Comercial y Admin; AdminTarifas no lo ve.
- El aislamiento por comercial ya existe (#199 / S4-HU-19.1): solo se verifica.

#### [S5-HU-23.2] QA: verificación de la Bandeja

**Issue:** [NEXO#290](https://github.com/InnovacionIA-proyectos/NEXO/issues/290) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-30 · **Responsable:** kcano-ux

Verificar la vigencia por fecha límite, el nuevo criterio de Pendientes, la columna y el filtro de resultado, y la regresión del aislamiento por comercial con dos usuarios.

**Decisiones:**

- Se requiere aprobación explícita de QA registrada en la issue.

#### [S5-HU-23.3] Bandeja frontend: columna Resultado y filtro Esperando resultado

**Issue:** [administracion#299](https://github.com/InnovacionIA-proyectos/administracion/issues/299) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La Bandeja no muestra si una cotización está ganada o perdida. Debe quedar una columna Resultado con badge y un filtro de resultado igual a los demás filtros, que al usarse cambie Atención a «Todas».

**Decisiones:**

- Columna y filtro solo visibles con el permiso marcarGanadaPerdida (Comercial y Admin).
- Descartado un chip o botón de acceso rápido: el filtro es solo el select.
- No copiar al repo real los datos simulados del sandbox.

### S5-HU-24 Frontend validado en sandbox

#### [S5-HU-24.1] Mesa de Costeo: comparación de 3 fuentes, chips de cada línea, avance, aviso y resumen completo

**Issue:** [administracion#300](https://github.com/InnovacionIA-proyectos/administracion/issues/300) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

La Mesa tenía una caja de piso calculada en el frontend con horas de la solicitud, que podía diferir del backend y prellenar un flete erróneo. Debe quedar una sola comparación de tres fuentes calculada por el backend, más chips de tiempos y clasificación, numeración de líneas, indicador de avance, aviso de pendiente por enviar y resumen completo.

**Decisiones:**

- Eliminar la caja aparte y floorTotal(): el frontend no calcula el piso.
- Ocultar «Valor estimado» solo en pantalla, sin tocar el backend.
- Tiempos por línea solo si vienen las dos horas; si falta alguna se usan las de la solicitud, nunca mezcladas.
- Debe hacerse antes de 20.2 y 20.3.

#### [S5-HU-24.2] Biblioteca: semáforo, orden y columna de vigencia, paginación de Histórico SAP, Almacenamiento e ITR

**Issue:** [administracion#301](https://github.com/InnovacionIA-proyectos/administracion/issues/301) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-16 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El semáforo de Tarifas de plaza en Biblioteca siempre dice «Vigente» por un error de formato de fecha, y faltan orden por vigencia, columna separada, paginación en Histórico SAP, unidad editable en Almacenamiento y desplegables en ITR. Debe quedar cada ajuste, sin cambios de backend.

**Decisiones:**

- No tocar el semáforo de Tarifas Plaza, donde la concatenación es correcta.
- Ordenar solo la página cargada; no pedir ORDER BY al backend (decisión de Sara).
- ITR sigue guardando un texto combinado máquina-movimientos; no se crea columna nueva.
- La carga masiva de Servicios no cambia.

#### [S5-HU-24.3] Carga masiva de Excel: vista previa editable en Servicios y Tarifas Plaza

**Issue:** [administracion#302](https://github.com/InnovacionIA-proyectos/administracion/issues/302) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Servicios bloquea todo el archivo si una fila falla o importa sin pausa, y Tarifas Plaza manda el archivo crudo. Debe quedar una vista previa editable con todas las filas, revalidación en vivo y confirmación que reconstruye el Excel y usa el mismo endpoint de importación.

**Decisiones:**

- 100% frontend, sin endpoints nuevos (decidido 2026-10-09).
- Filas existentes sin error se ocultan con su conteo pero se guardan.
- Tipo de vehículo nunca marca error (catálogo SAP incompleto); Clase de contenedor sí, más estricta que el backend a propósito.
- Parpadeo breve al preseleccionar los select aceptado por Sara.
- Riesgo aceptado: la validación queda duplicada entre front y back.

#### [S5-HU-24.4] Tarifas Plaza: detalle de errores de la importación y edición de la línea rechazada

**Issue:** [administracion#303](https://github.com/InnovacionIA-proyectos/administracion/issues/303) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El backend ya devuelve el detalle de errores por fila, pero la pantalla solo muestra el conteo de rechazadas. Debe quedar visible el detalle y la posibilidad de corregir y reintentar una línea rechazada sin reimportar el Excel.

**Decisiones:**

- Reutilizar la validación de la vista previa de 24.3.

#### [S5-HU-24.5] Iconos de información en Biblioteca, Tarifas Plaza, Reglas Solo-SICETAC, Cubicador y Tablero

**Issue:** [administracion#304](https://github.com/InnovacionIA-proyectos/administracion/issues/304) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-14 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

Cinco vistas tienen un párrafo descriptivo fijo bajo el título. Debe quedar el mismo patrón de Bandeja: un icono de información junto al título con el texto en tooltip.

**Decisiones:**

- Mismo patrón en todas y con el mismo texto exacto, sin resumir ni corregir.
- Actualizar el texto del Tablero sería un pedido aparte.

#### [S5-HU-24.6] Mesa de Costeo: agregar servicios conexos paso a paso

**Issue:** [administracion#305](https://github.com/InnovacionIA-proyectos/administracion/issues/305) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

El selector de conexos es un buscador de texto libre sobre más de mil servicios. Debe quedar una selección por pasos: primero el tipo (Almacenamiento, ITR, Sellos, Acompañamiento) y luego sus desplegables, mostrando precio, unidad y subtotal antes de agregar, sin cambiar el contrato del componente.

**Decisiones:**

- Un paso con una sola opción se autoselecciona (Sara, 2026-10-12).
- Tras agregar se reinicia todo.
- Alternar entre búsqueda por pasos y por texto conserva la selección.
- Se conserva el buscador de texto como alternativa.

#### [S5-HU-24.7] Mesa de Costeo: quitar conexos y permitir agregar/quitar con cotización entregada

**Issue:** [administracion#306](https://github.com/InnovacionIA-proyectos/administracion/issues/306) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

AdminTarifas no ve la sección de servicios, la cotización entregada bloquea los conexos y no hay forma de quitar uno. Debe quedar el permiso para AdminTarifas, el bloqueo solo con Ganada/Perdida, una nota informativa y el chip de modificada tras entregar, y un botón Quitar con confirmación.

**Decisiones:**

- El bloqueo pasa de «Entregada» a «Ganada o Perdida».
- No copiar al repo real la botonera «Simular» del sandbox.

#### [S5-HU-24.8] Captura: no autoseleccionar al escribir y campos obligatorios

**Issue:** [administracion#307](https://github.com/InnovacionIA-proyectos/administracion/issues/307) · **Creada:** 2026-10-13 · **Estado:** Cerrada el 2026-10-16 · **Responsable:** jpvelez-bts · *issue que agrupa varias historias*

En Captura, si una búsqueda devuelve un solo resultado, Origen, Destino o Cliente se autoseleccionan al escribir. Debe quedar la autoselección solo al cargar un Excel, la vista previa del Excel con todas las columnas y filas, y obligatorias la fecha límite, vehículo y carrocería en Cupo y la clase de contenedor cuando se muestra.

**Decisiones:**

- Al escribir a mano nunca se autoselecciona (pedido de Sara).
- Por defecto, vehículo y carrocería solo obligatorios en Cupo y la fecha límite solo se exige en el frontend (pendiente de confirmar con Sara).

#### [S5-HU-24.9] QA: verificación de las pantallas del frontend

**Issue:** [administracion#308](https://github.com/InnovacionIA-proyectos/administracion/issues/308) · **Creada:** 2026-10-13 · **Estado:** Abierta · **Responsable:** kcano-ux

Verificar las pantallas de 24.1 a 24.8: Mesa de Costeo, Biblioteca, Tarifas Plaza, carga masiva, iconos de información, conexos por pasos y quitar, y Captura.

**Decisiones:**

- Se requiere aprobación explícita de QA registrada en la issue.

## 6. Ajustes varios de pantalla

*Documento de planeación del 2026-10-15.* Ajustes de botones, filtros, encabezados y horas, solo de frontend.

### Ajustes de pantalla: botones, filtros, encabezados fijos y horas de devolución

**Issue:** [administracion#328](https://github.com/InnovacionIA-proyectos/administracion/issues/328) · **Creada:** 2026-10-15 · **Estado:** Cerrada el 2026-10-15 · **Responsable:** jpvelez-bts

Cuatro ajustes de frontend: ocultar Ganada/Perdida al Comercial, quitar tres filtros de la matriz SICE-TAC en Biblioteca, fijar los encabezados de todas las tablas al desplazarse y mostrar y enviar 1 h + 1 h por defecto en la línea de devolución de contenedor (hoy el backend usa 4 + 4).

**Decisiones:**

- Todo en el frontend, sin cambios de backend.
- Se ocultan los botones al Comercial sin quitar el permiso marcarGanadaPerdida; Cotifly sigue marcando el resultado por API.
- La columna y el filtro de Resultado de la Bandeja no cambian.
- Las columnas de la matriz SICE-TAC se mantienen.
- Las horas 1 + 1 solo aplican a lo capturado en pantalla; lo que llega por la API de Cotifly sigue con 4 + 4.

## 7. Issues por repositorio (revisión del 16 de octubre)

*Documento de planeación del 2026-10-16.* Issues de la revisión del 16 de octubre, separadas por repositorio y responsable: NEXO, `administracion`, Cotifly, `clientepro-frontend`, datos y QA.

### Rechazo de línea

#### [S6-HU-24.3] Rechazar una línea con motivo (backend)

**Issue:** [NEXO#320](https://github.com/InnovacionIA-proyectos/NEXO/issues/320) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-20 · **Responsable:** ivasco-dev

Hoy solo se puede rechazar la solicitud completa; no existe rechazo por línea y Entregar exige que todas las líneas estén resueltas. Debe existir un estado Rechazada con motivo, autor y fecha, endpoints para rechazar y deshacer (solo AdminTarifas y Admin), y la línea rechazada no suma al total, cuenta como resuelta para entregar y sale como «Sin costeo» en el Excel.

**Decisiones:**

- La línea rechazada sale en PDF y Excel «sin costeo», sin precio y sin motivo.
- Deshacer el rechazo solo mientras la cotización esté en la Mesa (no entregada; si no, 409).
- Si todas las líneas están rechazadas se envía igual al comercial, con los motivos en el correo.
- D1: con todas las líneas rechazadas se bloquea el PDF; el Excel sí sale con todas en «Sin costeo».
- La línea rechazada no cuenta como «por costear» en las métricas.

#### [S6-HU-31.1] Rechazar línea con motivo (front de Nexo)

**Issue:** [administracion#363](https://github.com/InnovacionIA-proyectos/administracion/issues/363) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-22 · **Responsable:** jpvelez-bts

El front no tiene estado por línea. Debe agregar el botón «Rechazar línea» con motivo obligatorio para AdminTarifas y Admin, la opción de deshacer mientras la cotización no esté entregada, mostrar el motivo al Comercial y reflejar el rechazo en el PDF que arma el propio front.

**Decisiones:**

- En el PDF la línea rechazada sale «Sin costeo», sin precio y sin motivo.
- D1: si todas las líneas están rechazadas se bloquea el PDF con un mensaje claro; el Excel sí sale.

#### [HU-028.1] Soportar en Cotifly el rechazo de línea que viene de Nexo

**Issue:** [Cotifly#183](https://github.com/InnovacionIA-proyectos/Cotifly/issues/183) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** dguerraBSSL

Cotifly no distingue una línea rechazada de una pendiente o borrada, y una línea rechazada dejaría la cotización en «pendiente de costeo» para siempre. Debe leer estado y motivo de Nexo, guardar el estado «rechazada» por camión, excluirla del autocosteo, del total, del PDF y del Excel, y bloquear margen y conexos en esa ruta.

**Decisiones:**

- La ruta rechazada sale «Sin costeo», sin precio y sin motivo en PDF y Excel.
- Deshacer solo antes de la entrega.
- D1: si todas las rutas están rechazadas el PDF se bloquea con un error propio.
- Queda por decidir si las versiones -vN copian el rechazo.

#### [HU-028-FE.1] Mostrar el rechazo de línea en el costeo (front de Cotifly)

**Issue:** [clientepro-frontend#106](https://github.com/InnovacionIA-proyectos/clientepro-frontend/issues/106) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-29 · **Responsable:** scalderon-bs

La tabla de información de costeo debe mostrar el estado «rechazado» con su motivo y un botón de deshacer mientras no se entregue. También debe mostrar un mensaje cuando el PDF se bloquea por tener todas las rutas rechazadas.

**Decisiones:**

- D1: con todas las rutas rechazadas se bloquea el PDF.
- El PDF y el Excel los genera el backend de Cotifly, no este front.

### Mesa de Costeo / ajuste manual

#### [S6-HU-26.1] Editar un flete de plaza vigente (backend)

**Issue:** [NEXO#306](https://github.com/InnovacionIA-proyectos/NEXO/issues/306) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-19 · **Responsable:** ivasco-dev

El backend solo permite editar fletes de fuente Manual y responde 409 para las demás. Debe permitirse editar fletes de cualquier fuente mientras la ruta no tenga regla Solo-SICE, bloqueando con un código propio cuando la tenga, y exponer en la línea si tiene esa regla para que el front sepa cuándo ofrecer la edición.

**Decisiones:**

- Decisión 1: se puede editar a mano un flete de plaza vigente; solo se bloquea con regla Solo-SICE.
- D4: al editar, la fuente pasa a «Manual» y la auditoría guarda el valor y la fuente anteriores.
- Ningún valor puede quedar por debajo del piso SICE-TAC; hay que cerrar el hueco cuando no se encuentra piso.
- Actualizar o guardar como nueva la tarifa de plaza usa los endpoints de Tarifas Plaza existentes, sin cambio de backend.

#### [S6-HU-33.1] Editar el flete de una línea (plaza vigente) en el front

**Issue:** [administracion#355](https://github.com/InnovacionIA-proyectos/administracion/issues/355) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-21 · **Responsable:** jpvelez-bts

El front no tiene ningún flujo para editar un flete ya guardado: la Mesa solo muestra una tarjeta de lectura. Se pide un botón «Editar flete» cuando la línea no tiene regla Solo-SICE, mensajes claros para el 409 y el error de piso, y ofrecer actualizar la tarifa de plaza o guardarla como nueva cuando el valor es manual.

**Decisiones:**

- Decisión 1: si la tarifa de plaza ya existe, se ofrece actualizarla o guardarla como nueva.
- Se agrega una capacidad nueva en la matriz para AdminTarifas.

### Roles

#### [S6-HU-25.1] Roles: CoordinadorFletes pasa a AdminBiblioteca (backend)

**Issue:** [NEXO#307](https://github.com/InnovacionIA-proyectos/NEXO/issues/307) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-19 · **Responsable:** ivasco-dev

El rol CoordinadorFletes tiene hoy acceso a solicitudes, cotizaciones y planes de cargue. Debe renombrarse a AdminBiblioteca en código, pruebas y documentación, limitado a Biblioteca, Tarifas Plaza, Reglas Solo-SICE, rangos de peso y vigencia, sin Bandeja, Mesa ni cotizaciones; también se ajustan alertas, permisos de sesión y precedencia de roles.

**Decisiones:**

- Decisión 9: CoordinadorFletes pasa a AdminBiblioteca en backend y front; AdminTarifas, Admin y Comercial quedan igual.
- D5: crear solicitud, cargar líneas en lote y recalcular puntaje pasan a ser solo de Admin.
- D5: AdminBiblioteca conserva el permiso de cubicación en el backend, pero el front no le muestra el Cubicador.
- D5: rechazar tarifas con motivo y «Simular» quedan solo para AdminTarifas y Admin.
- La alerta de «línea que no pudo costearse» pasa a AdminTarifas.
- D8: el orden de despliegue lo decide Ismael; propuesta: el front acepta los dos nombres durante la transición.
- El Comercial debe conservar todos sus permisos para que Cotifly no reciba 403.

#### [S6-HU-32.1] Roles: AdminBiblioteca en el front

**Issue:** [administracion#360](https://github.com/InnovacionIA-proyectos/administracion/issues/360) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-22 · **Responsable:** jpvelez-bts

El front toma el nombre del rol del backend sin traducción, así que si cambia de un solo lado el rol pierde acceso. Debe aceptar los dos nombres durante la transición, definir la matriz de AdminBiblioteca (Biblioteca, Tarifas Plaza, Reglas Solo-SICE y vigencia, sin Bandeja, Mesa ni Cubicador) y retirar el nombre viejo tras verificar el despliegue.

**Decisiones:**

- El front no muestra el Cubicador a AdminBiblioteca aunque el backend conserve el permiso (D5).
- Durante la transición se aceptan CoordinadorFletes y AdminBiblioteca.

#### [HU-029.1] Corregir dos textos de error desactualizados en Cotifly

**Issue:** [Cotifly#180](https://github.com/InnovacionIA-proyectos/Cotifly/issues/180) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** dguerraBSSL

Dos mensajes de error de Cotifly nombran roles de Nexo que cambian con el renombre a AdminBiblioteca. Deben ajustarse para nombrar los roles vigentes cuando se despliegue el cambio.

### Catálogos / diccionario de alias

#### [S6-HU-27.2] Diccionario de alias de vehículos y de clases de contenedor (backend)

**Issue:** [NEXO#321](https://github.com/InnovacionIA-proyectos/NEXO/issues/321) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-20 · **Responsable:** ivasco-dev

En el Excel de licitación el vehículo y la clase de contenedor se escriben a mano con muchas variantes, que hoy caen en cuarentena (vehículo) o quedan como texto libre (contenedor). El backend debe consultar una tabla de alias antes de resolver ambos datos y ofrecer endpoints para listar alias, resolver un texto suelto y agregar alias desde la bandeja de normalización.

**Decisiones:**

- Decisión 7: una sola tabla con marca de tipo (Vehiculo/Contenedor), que vive en Nexo; no se duplica en Cotifly ni va en el Cubicador.
- El endpoint de «resolver» hace falta porque Cotifly necesita preguntar a Nexo en el momento de importar, no solo bajar la lista.
- Decidido el 2026-10-20: Nexo y el Cubicador usan el mismo vocabulario de códigos de vehículo, así que el alias resuelve directo sin segundo mapeo.
- El diccionario resuelve solo la columna de tipo de vehículo; la carrocería llega aparte y queda fuera de alcance.
- D6: el mapeo columna a código se define en DATA-2.

#### [HU-031.1] Consumir el endpoint de resolver vehículos y clases de contenedor de Nexo

**Issue:** [Cotifly#194](https://github.com/InnovacionIA-proyectos/Cotifly/issues/194) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** dguerraBSSL

El front de Cotifly no puede llamar a Nexo directamente, así que su backend debe servir de intermediario. Se pide un endpoint propio que resuelva un vehículo o una clase de contenedor escritos a mano consultando el diccionario de Nexo, e informe con claridad cuando no se reconoce.

**Decisiones:**

- El backend de Cotifly es el intermediario por CORS y porque la identidad viaja en un header que solo tiene el backend.
- Debe servir para los dos tipos (vehículo y contenedor).
- Queda por decidir si se resuelve en lote para no saturar a Nexo.

#### [S6-HU-27.1] Tabla de alias de vehículos y de clases de contenedor, y su carga

**Issue:** [NEXO#315](https://github.com/InnovacionIA-proyectos/NEXO/issues/315) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-20 · **Responsable:** scalderon-bs

Hay que crear en la base de Nexo una tabla de alias con tipo de elemento y código real, y cargarla con el diccionario crudo del Cubicador (cinco columnas de vehículo y dos de contenedor). También se documenta el mapeo de cada columna a su código y se actualizan el esquema y el README del seeder.

**Decisiones:**

- Nombre sugerido tbl_alias_elementos, porque guarda dos tipos de alias.
- D6 cerrada para contenedor: «Contenedor 40 HC» a CNT40.HC y «Contenedor 20» a CNT20.ST.
- Para vehículo se usan los códigos oficiales ya sembrados; la pareja exacta columna-código se confirma en esta issue.
- El diccionario resuelve solo la columna de tipo de vehículo; la carrocería queda fuera de alcance.

### Peso con tara

#### [S6-HU-28.1] Peso con tara: confirmar el efecto en el backend

**Issue:** [NEXO#308](https://github.com/InnovacionIA-proyectos/NEXO/issues/308) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-19 · **Responsable:** ivasco-dev

El peso de cada línea se usa para elegir el rango de la tarifa de plaza y se envía al Cubicador, mientras Cotifly manda el peso sin tara. Hay que documentar en el contrato que el peso de la línea incluye la tara y dejar por escrito qué peso usan el rango de plaza y la recomendación de vehículo; solo hay cambio de código si la revisión encuentra un problema.

**Decisiones:**

- D2: el peso total de cada línea incluye la tara, también en las solicitudes de Cotifly.
- D2b: la tara la escribe el Comercial en el peso de cada línea; el backend no la suma.

#### [S6-HU-35.1] Peso: nota de tara y quitar la validación (front de Nexo)

**Issue:** [administracion#359](https://github.com/InnovacionIA-proyectos/administracion/issues/359) · **Creada:** 2026-10-19 · **Estado:** Abierta · **Responsable:** jpvelez-bts

La captura bloquea el envío si el peso total no coincide con la suma de las posiciones. Hay que borrar esa validación y verificar que la nota del campo diga que el peso incluye la tara.

**Decisiones:**

- Decisión 6, D2 y D2b: la nota de tara aplica a todas las líneas, el Comercial escribe el peso ya con la tara y se quita la validación contra la suma de posiciones.

#### [HU-030.1] Peso con tara en lo que Cotifly manda a Nexo

**Issue:** [Cotifly#184](https://github.com/InnovacionIA-proyectos/Cotifly/issues/184) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** dguerraBSSL

Cotifly envía a Nexo el peso de la carga sin tara y no conoce la tara. Debe aceptar y guardar, por línea o camión, el peso total con tara que escribe el Comercial y mandarlo como peso de la línea, sin cambiar lo que se envía al Cubicador.

**Decisiones:**

- D2 y D2b: la regla del peso con tara aplica también a Cotifly y la tara la escribe el Comercial.
- Valor inicial sugerido: el peso de carga del camión; el Comercial le suma la tara.
- Lo que va al Cubicador (peso por mercancía, sin tara) no cambia.

#### [HU-030-FE.2] Etiqueta del peso (tara) en el front de Cotifly

**Issue:** [clientepro-frontend#107](https://github.com/InnovacionIA-proyectos/clientepro-frontend/issues/107) · **Creada:** 2026-10-19 · **Estado:** Abierta · **Responsable:** scalderon-bs

El campo de peso por mercancía no aclara si incluye la tara y a Nexo se manda el peso sin tara. Se pide un campo editable «Peso total con tara» por camión o línea, con nota explicativa, que viaje a Nexo como peso de la línea.

**Decisiones:**

- D2 y D2b: la regla aplica a la captura de Cotifly y el Comercial escribe el peso ya con la tara.
- El peso por mercancía que va al Cubicador no cambia.

### Contrato del API

#### [S6-HU-24.1] Exponer estado y motivo de cada línea en el API

**Issue:** [NEXO#309](https://github.com/InnovacionIA-proyectos/NEXO/issues/309) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-19 · **Responsable:** ivasco-dev

El detalle de la solicitud no devuelve el estado de cada línea ni su motivo pendiente, por lo que Cotifly solo ve «pendiente de costeo» sin saber por qué. Se agregan estado, motivo pendiente y motivo de rechazo por línea, como campos opcionales al final para no romper a los consumidores, y se documentan en el contrato.

**Decisiones:**

- Los campos nuevos van al final y son opcionales para no romper a Cotifly.

#### [S6-HU-29.1] La línea de costeo debe devolver línea de negocio, clase de viaje y clase de contenedor

**Issue:** [NEXO#311](https://github.com/InnovacionIA-proyectos/NEXO/issues/311) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-19 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

El DTO de la línea de costeo no trae línea de negocio, clase de viaje ni clase de contenedor, aunque la entidad sí los tiene, lo que bloquea los chips de la Mesa y el prellenado de la tarifa de plaza en el front. Se agregan como campos opcionales y, opcionalmente, los ids de municipio de origen y destino para que el front no deduzca la ciudad del texto.

**Decisiones:**

- Clase de contenedor en null salvo cuando la clase de viaje es Contenedor.
- Se entrega junto con NEXO-B6 porque es el mismo objeto.

#### [HU-028.4] (Opcional) Mostrar el motivo por el que una línea sigue pendiente

**Issue:** [Cotifly#181](https://github.com/InnovacionIA-proyectos/Cotifly/issues/181) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** dguerraBSSL

Con la regla de SICE-TAC del mes actual más líneas quedarán en consulta completa y Cotifly no sabe por qué. Si Nexo expone el motivo pendiente, Cotifly puede mostrarlo o guardarlo, sin romperse si no viene.

**Decisiones:**

- Es opcional y no urgente.

### Roles / Cubicador

#### [S6-HU-25.2] AdminTarifas debe poder usar el Cubicador completo

**Issue:** [NEXO#310](https://github.com/InnovacionIA-proyectos/NEXO/issues/310) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-19 · **Responsable:** ivasco-dev

Un PR anterior dio a AdminTarifas solo el permiso de cubicar una línea; los endpoints de escritura de planes de cargue y la creación de vehículos siguen sin incluirlo, así que puede ver un plan pero no crearlo ni editarlo. Hay que agregar AdminTarifas a esos 10 endpoints con pruebas de roles, dejando el borrado de vehículos solo para Admin.

**Decisiones:**

- El borrado de vehículos sigue siendo solo de Admin.
- Es independiente del renombre de roles; tras NEXO-B3, AdminBiblioteca sale de estos endpoints y AdminTarifas queda como rol de costeo que cubica.

### API y textos

#### [S6-HU-29.2] Ajustes pequeños del API y de los textos

**Issue:** [NEXO#312](https://github.com/InnovacionIA-proyectos/NEXO/issues/312) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-19 · **Responsable:** ivasco-dev

Agrupa cuatro ajustes: las justificaciones muestran un símbolo de moneda incorrecto y un resto del prefijo «cls»; el histórico SAP no se puede paginar; un caso sin recomendación del Cubicador se confunde con una caída (503); y las tarifas de plaza exigen un detalle aunque se guarden desde la Mesa. Debe quedar formato de moneda colombiano, paginación, un 404 propio y detalle opcional.

**Decisiones:**

- Decisión de Sara: al guardar como plaza desde la Mesa los detalles son opcionales; en la pantalla de Tarifas Plaza sí se exigen.
- El 503 queda solo para fallas reales del Cubicador; la falta de recomendación responde 404 con código propio.

### Mesa de Costeo / normalización de vehículos

#### [S6-HU-27.3] Cambiar el vehículo de una línea y bandeja de normalización de vehículos

**Issue:** [NEXO#322](https://github.com/InnovacionIA-proyectos/NEXO/issues/322) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-20 · **Responsable:** ivasco-dev

No existe forma de cambiar el vehículo de una línea ya creada ni rutas para la bandeja de configuraciones en cuarentena. Se pide un endpoint para cambiar el vehículo mientras la cotización no esté entregada, recalculando propuesta y piso, y rutas para listar, mapear o descartar configuraciones en cuarentena; mapear puede crear un alias para que no vuelva a caer.

**Decisiones:**

- D9 (pendiente en el doc, recomendada «sí»): construir el cambio de vehículo de una línea.
- Solo AdminTarifas y Admin pueden cambiar el vehículo; con la cotización entregada responde 409.
- Ningún valor puede quedar bajo SICE-TAC tras el cambio.

### Contrato de captura

#### [S6-HU-29.3] Aclaraciones pendientes del contrato de captura

**Issue:** [NEXO#313](https://github.com/InnovacionIA-proyectos/NEXO/issues/313) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-19 · **Responsable:** ivasco-dev

Quedan tres dudas del front sin respuesta: cómo usar el agrupador de ruta cuando una carga se reparte en varios camiones, cómo armar las posiciones de cada camión si la recomendación no trae medidas, y qué catálogo usa la clase de contenedor de Extradimensionado. Es una issue de investigación sin código hasta tener respuestas, que deben quedar en el contrato y avisarse al front.

**Decisiones:**

- D10 (pendiente): para la clase de contenedor de Extradimensionado se recomienda dejarla opcional y sin validar.
- D11 (pendiente): el agrupador de ruta y el reparto de posiciones los decide Ismael con los equipos de Cubicador y Cotifly.

### Mesa de Costeo / resultado

#### Comercial no marca Ganada ni Perdida desde Nexo

**Sin issue** · **Fecha:** 2026-10-16 (documento) · **Tipo:** Issue planeada · **Responsable:** Juan Pablo

**Estado según el documento:** Esfuerzo estimado 2 a 3 horas.

Los botones Ganada y Perdida de la Mesa y del Resumen solo revisan el permiso, que el Comercial tiene. Se pide ocultarlos al Comercial combinando el permiso con el rol, sin quitar el permiso de la matriz ni tocar el backend, porque Cotifly sigue marcando el resultado por API.

**Decisiones:**

- D7: la columna y el filtro «Resultado» de la Bandeja no se hacen (solo existían en el sandbox).
- No se quita el permiso de la matriz ni se cambia el backend.

### Biblioteca / SICE-TAC

#### Quitar tres filtros de la matriz SICE-TAC

**Sin issue** · **Fecha:** 2026-10-16 (documento) · **Tipo:** Issue planeada · **Responsable:** Juan Pablo

**Estado según el documento:** Esfuerzo 1 a 2 horas; falta verificar que el backend responda igual sin esos parámetros.

La pantalla de la matriz SICE-TAC en Biblioteca tiene tres selectores (tipo de carga, condición y vía estándar) que deben eliminarse con sus referencias. Las columnas de la tabla se mantienen y no se toca el backend.

**Decisiones:**

- Se quitan solo los filtros; las columnas de la tabla se quedan.

### Captura

#### [S6-HU-37.1] Lista de empaque y posiciones solo en Carga Suelta y Extradimensionado

**Issue:** [administracion#356](https://github.com/InnovacionIA-proyectos/administracion/issues/356) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-21 · **Responsable:** jpvelez-bts

La visibilidad de la lista de empaque y las posiciones depende de la clase de viaje, con una excepción para líneas Nacional que invierte la regla, y la importación de Excel llena posiciones aunque la sección esté oculta. Deben mostrarse solo en Carga Suelta y Extradimensionado y la importación no debe llenarlas en otras clases.

**Decisiones:**

- D3: solo se muestran en Carga Suelta y Extradimensionado, en todas las líneas, incluidas las Nacional (se quita la excepción).
- Supuesto: Admin sigue viéndolas porque comparte la pantalla de captura.

### Mesa de Costeo

#### [S6-HU-37.2] Desglose de flete, levante y retorno en la Mesa

**Issue:** [administracion#357](https://github.com/InnovacionIA-proyectos/administracion/issues/357) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-22 · **Responsable:** jpvelez-bts

El backend ya envía flete, levante y retorno por separado, pero el front los suma y los muestra como texto corrido. Deben verse como tres valores con etiqueta propia en la Mesa y en el Resumen, sin cambios de backend.

**Decisiones:**

- Decisión 5: el desglose se ve solo en la Mesa de Costeo; el Excel y el PDF siguen con el valor final de la línea.

### Tablas / usabilidad

#### [S6-HU-37.3] Encabezados fijos y barra de desplazamiento arriba y abajo

**Issue:** [administracion#358](https://github.com/InnovacionIA-proyectos/administracion/issues/358) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-21 · **Responsable:** jpvelez-bts

Ninguna de las nueve tablas del módulo tiene encabezado fijo ni barra horizontal arriba. Se pide un contenedor con altura máxima y encabezados fijos, más una directiva reutilizable de desplazamiento espejado para tener barra arriba y abajo, aplicada y probada en las nueve tablas.

**Decisiones:**

- La barra superior se resuelve con una directiva Angular reutilizable, porque no existe en CSS puro.

### Mesa de Costeo / SICE-TAC

#### [S6-HU-37.4] Mostrar por qué una línea quedó sin piso (SICE-TAC desactualizada)

**Issue:** [administracion#361](https://github.com/InnovacionIA-proyectos/administracion/issues/361) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-21 · **Responsable:** jpvelez-bts

El modelo del front no tiene el aviso de SICE-TAC ni el motivo pendiente de la línea. Deben agregarse y mostrarse junto al bloque «Piso SICE-TAC» de la Mesa, con un mensaje claro en los primeros días del mes cuando aún no se carga el período nuevo.

### Importación de licitación

#### [S6-HU-35.2] Importación de licitación: kg/t y lectura de números (front de Nexo)

**Issue:** [administracion#362](https://github.com/InnovacionIA-proyectos/administracion/issues/362) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-21 · **Responsable:** jpvelez-bts

El lector de números del Excel interpreta mal el formato colombiano de miles y no entiende pesos con letra como «18t». Se pide un selector kg/t en la columna de peso, separar el número del sufijo y corregir la lectura de miles en todos los campos numéricos del mapeo.

**Decisiones:**

- Decisión 8: selector kg/t en pantalla; el backend siempre recibe kg.
- Con el diccionario de NEXO-B4 el front manda el texto crudo del vehículo y el backend lo resuelve; el front no empareja.

#### [HU-030-FE.1] Selector kg/t y lectura de pesos con letra (front de Cotifly)

**Issue:** [clientepro-frontend#104](https://github.com/InnovacionIA-proyectos/clientepro-frontend/issues/104) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-29 · **Responsable:** scalderon-bs

El mapeo del Excel de licitación tiene un solo campo de peso en kg y un valor como «18t» queda vacío. Se pide un control de unidad kg/t y extender el lector para separar el número del sufijo y convertir, enviando siempre kg al backend.

**Decisiones:**

- Decisión 8: selector kg/t; el backend recibe siempre kg.

### Catálogos

#### [HU-027.1] Exponer las clases de contenedor en el catálogo agregado de Cotifly

**Issue:** [Cotifly#179](https://github.com/InnovacionIA-proyectos/Cotifly/issues/179) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-27 · **Responsable:** dguerraBSSL

El backend de Cotifly consume las clases de contenedor de Nexo pero no las expone en su catálogo de flete, y el front usa un catálogo fijo. Debe agregarlas al endpoint de catálogos y manejar el caso de lista vacía.

#### [HU-027-FE.1] Catálogo de clases de contenedor desde el backend (front de Cotifly)

**Issue:** [clientepro-frontend#105](https://github.com/InnovacionIA-proyectos/clientepro-frontend/issues/105) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-28 · **Responsable:** scalderon-bs

El front usa un catálogo fijo de cinco clases de contenedor. Debe reemplazarlo por la lectura del catálogo de flete del backend de Cotifly, igual que los demás catálogos.

### Importación de licitación / diccionario de alias

#### [HU-031-FE.1] Reconocer el vehículo y la clase de contenedor escritos a mano en el Excel de licitación

**Issue:** [clientepro-frontend#109](https://github.com/InnovacionIA-proyectos/clientepro-frontend/issues/109) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-29 · **Responsable:** scalderon-bs

El Excel de licitación de Cotifly no mapea ninguna columna de vehículo y la de clase de contenedor no se valida. Se pide agregar esos campos al mapeo, resolverlos con el endpoint propio de Cotifly y mostrar al usuario qué se reconoció en cada fila, o avisar si no, antes de enviar.

**Decisiones:**

- El diccionario no se duplica en Cotifly; todo pasa por el endpoint de su backend.
- Pendiente sin asumir: qué hacer si el usuario no corrige un valor no reconocido (enviar igual o bloquear la fila).

### Rechazo de línea / base de datos

#### [S6-HU-24.2] Columnas del rechazo en la línea de solicitud

**Issue:** [NEXO#314](https://github.com/InnovacionIA-proyectos/NEXO/issues/314) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-20 · **Responsable:** scalderon-bs

La tabla de líneas de solicitud necesita columnas para el motivo, el autor y la fecha del rechazo. Hay que agregarlas, actualizar el script de esquema y probarlas en una base de pruebas con respaldo previo.

**Decisiones:**

- El proyecto no usa migraciones: el script se corre a mano en cada base.

#### [HU-028.2] Base de datos de Cotifly para el rechazo

**Issue:** [Cotifly#182](https://github.com/InnovacionIA-proyectos/Cotifly/issues/182) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-23 · **Responsable:** scalderon-bs

La base de Cotifly necesita el estado «rechazada» y el motivo por camión. El script debe quedar en la carpeta de base de datos del repo Cotifly y probado.

### Roles / base de datos

#### [S6-HU-25.3] Script de usuarios para el cambio de rol

**Issue:** [NEXO#316](https://github.com/InnovacionIA-proyectos/NEXO/issues/316) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-22 · **Responsable:** scalderon-bs

Los usuarios y el mapeo de rol del portal deben pasar de CoordinadorFletes a AdminBiblioteca en la base de Nexo. Se corre primero en pruebas, con respaldo previo y en el orden de despliegue acordado.

**Decisiones:**

- El rol del portal puede conservar su nombre; solo cambia el mapeo interno.

### QA / cálculos

#### [S6-HU-30.1] Validar los cálculos 1+1 y 4+4

**Issue:** [NEXO#317](https://github.com/InnovacionIA-proyectos/NEXO/issues/317) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-10-30 · **Responsable:** kcano-ux

Hay que validar contra casos reales los tiempos de cargue y descargue que implementó un PR anterior: viaje cargado, devolución y retorno de contenedor, levante en ciudad diferente y solicitud por API con horas explícitas, comparando además el piso SICE-TAC. Las diferencias quedan como issue.

### QA / SICE-TAC

#### [S6-HU-30.2] Revisar los valores calculados con SICE-TAC

**Issue:** [NEXO#318](https://github.com/InnovacionIA-proyectos/NEXO/issues/318) · **Creada:** 2026-10-19 · **Estado:** Abierta · **Responsable:** kcano-ux

Se debe tomar un conjunto de rutas reales de spot y licitación y comparar el valor de Nexo contra lo esperado por el negocio, revisando la regla y su implementación. Las diferencias quedan como issue.

### QA / roles

#### [S6-HU-25.4] Pruebas de roles tras el cambio

**Issue:** [NEXO#319](https://github.com/InnovacionIA-proyectos/NEXO/issues/319) · **Creada:** 2026-10-19 · **Estado:** Abierta · **Responsable:** kcano-ux

Tras el cambio de roles se necesita una matriz de pruebas por rol y endpoint en la que cada rol entre a lo suyo y reciba 403 en lo demás. Incluye probar un usuario real de AdminBiblioteca después del despliegue y verificar que el Comercial de Cotifly siga funcionando.

### QA / rechazo de línea

#### [S6-HU-24.4] Prueba de punta a punta Nexo y Cotifly con rechazo de línea

**Issue:** [NEXO#323](https://github.com/InnovacionIA-proyectos/NEXO/issues/323) · **Creada:** 2026-10-19 · **Estado:** Abierta · **Responsable:** kcano-ux

Se debe probar de punta a punta el rechazo de línea: rechazar, deshacer, entregar con una y con todas rechazadas, PDF y Excel de Nexo y de Cotifly, correo con motivos y los contratos de solicitud y cotización. Lo que falle queda como issue.

### QA / verificación con datos

#### Verificar con datos reales los casos de front#353

**Sin issue** · **Fecha:** 2026-10-16 (documento) · **Tipo:** Issue planeada · **Responsable:** kcano

**Estado según el documento:** Esfuerzo 2 a 4 horas; repos administracion y NEXO.

El código ya está mergeado, pero faltan cuatro casillas de front#353 por ver con datos reales: plaza vencida, histórico con origen NEXO, histórico por encima del piso y el aviso de SICE-TAC. Es una verificación de datos, no de código, que puede requerir esperar al cambio de período.

**Decisiones:**

- El aviso de SICE-TAC se espera desde el 1 de noviembre, cuando el período cargado deje de ser el actual.

### Mesa de Costeo / piso SICE-TAC

#### Flete manual sin piso

**Sin issue** · **Fecha:** 2026-10-16 (documento) · **Tipo:** Propuesta

**Estado según el documento:** Sin decidir (observación de la sección 9).

No se verificó si guardar un flete manual sigue aceptando cualquier valor cuando no se encuentra el piso SICE-TAC. Se propone hacer una prueba antes de decidir si se abre una issue.

## 8. Migración de las bases de pruebas

*Documento de planeación del 2026-10-19.* Mover las bases de pruebas de NEXO y del Cubicador a bases propias.

### [S6-HU-31.1] Migrar la base de pruebas de NEXO a una base propia

**Issue:** [NEXO#326](https://github.com/InnovacionIA-proyectos/NEXO/issues/326) · **Creada:** 2026-10-19 · **Estado:** Abierta · **Responsable:** scalderon-bs

La base de pruebas de NEXO comparte base física con otra aplicación y solo se separa por esquema. Hay que crear una base propia, aplicar el script de esquema completo, copiar los datos verificando conteos por tabla, apuntar la conexión de QA a la base nueva y coordinar la ventana del cambio.

**Decisiones:**

- Alcance solo del ambiente de pruebas/QA; no incluye producción.
- No escribir la cadena de conexión real en ningún documento del repo.
- Comparar columnas contra la base real, no solo confiar en el .sql, porque no hay migraciones.
- Queda por decidir qué pasa con el esquema viejo (borrarlo o dejarlo como respaldo).

### [S6-HU-31.2] Migrar la base de pruebas del Cubicador a una base propia

**Issue:** [NEXO#327](https://github.com/InnovacionIA-proyectos/NEXO/issues/327) · **Creada:** 2026-10-19 · **Estado:** Cerrada el 2026-11-02 · **Responsable:** scalderon-bs

La base de pruebas del Cubicador comparte base física con Cotifly. Antes de migrar hay que inventariar las tablas reales en QA, porque puede haber tablas que no están en el script del repo; luego se crea la base nueva, se aplica el esquema completo, se copian y verifican los datos y se actualiza la conexión de QA.

**Decisiones:**

- El inventario de tablas reales es obligatorio antes de migrar.
- Si va en la misma base nueva de NEXO en esquema aparte o en una base separada se define con Ismael.
- Avisar a Cotifly antes de tocar la base compartida; su esquema no se toca.
- Las tablas no documentadas que aparezcan quedan documentadas en el repo.

## 9. Rol CoordinadorFletes a AdminBiblioteca

*Documento de planeación del 2026-10-19.* Cambio del rol del portal CoordinadorFletesNEXO al rol AdminBiblioteca de NEXO.

### Cierre del cambio de rol CoordinadorFletes a AdminBiblioteca en la base de pruebas

**Sin issue** · **Fecha:** 2026-10-19 (documento) · **Tipo:** Pendiente · **Responsable:** Sara

**Estado según el documento:** Doc del 2026-10-19; actualizado al 2026-10-27: el código en QA ya tiene AdminBiblioteca y no quedan filas con el rol viejo. Falta confirmar despliegue, probar con usuario real y borrar respaldos.

El script de usuarios ya se corrió en la base de pruebas con respaldo previo (cinco usuarios y un mapeo de portal pasaron a AdminBiblioteca). Falta confirmar que el backend y el front con el rol nuevo están desplegados en el ambiente que usa esa base, probar con un usuario real y luego borrar las dos tablas de respaldo.

**Decisiones:**

- Se corrió el script antes de desplegar el backend, a propósito y con visto bueno de Sara, aceptando que los usuarios quedaran sin acceso temporalmente.
- El nombre del rol en el portal no cambia; solo el mapeo interno.
- Las tablas de respaldo se borran solo tras verificar, por ser una base compartida; el doc deja el script para revertir.

## 10. Plan de IA (Claude) para NEXO

*Documento de planeación del 2026-10-23.* Plan de uso de Claude en NEXO y en el Cubicador. El documento numeraba los milestones como S7; en GitHub se crearon como S6 con el mismo número de HU.

### S7-HU-32 Base para usar IA en NEXO

#### [S7-HU-32.1] Decisión de seguridad y datos para el uso de Claude

**Issue:** [NEXO#349](https://github.com/InnovacionIA-proyectos/NEXO/issues/349) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** ivasco-dev

Antes de usar Claude hay que dejar por escrito, en un ADR, cómo se protegen la llave y los datos de clientes. Debe quedar definido qué campos nunca se envían, qué se anonimiza, la política de retención aceptada y el modelo y esfuerzo a usar.

**Decisiones:**

- La llave de Anthropic vive solo en el servidor, nunca en el front: en el navegador quedaría en el bundle, se saltaría roles, auditoría y límites, y contradice S3-HU-05.
- El front sigue subiendo el Excel; la llamada a Claude la hace el backend de NEXO.
- Revisar con jurídico la Ley 1581 de 2012 y los términos de la API antes de enviar datos reales; mientras tanto, datos anonimizados en desarrollo y pruebas.
- Minimizar lo que sale: solo encabezados, muestra y valores distintos, sin NIT ni nombres cuando no hagan falta.
- No se guarda el contenido completo de los Excel en logs.

#### [S7-HU-32.2] Puerto y adaptador de Anthropic en NEXO

**Issue:** [NEXO#350](https://github.com/InnovacionIA-proyectos/NEXO/issues/350) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** ivasco-dev

NEXO necesita una forma única y reemplazable de llamar a Claude. Debe quedar un puerto en Application y un adaptador en Infrastructure con configuración Anthropic:*, tiempos de espera, reintentos, salida estructurada, interruptor por función y un adaptador falso para pruebas.

**Decisiones:**

- La IA se integra como un adaptador más (patrón de puertos y adaptadores, igual que IPropuestaTarifaPort).
- Sin llave o con la función apagada, NEXO arranca y las funciones de IA responden 503 con código claro.
- Ninguna prueba del CI llama a la API real; se usan adaptador falso y respuestas grabadas.
- No se envían temperature, top_p ni top_k porque la API los rechaza desde 2026-10-21.

#### [S7-HU-32.3] Secretos, despliegue y salida a internet

**Issue:** [NEXO#351](https://github.com/InnovacionIA-proyectos/NEXO/issues/351) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** ivasco-dev

La llave debe manejarse como secreto de despliegue y el entorno debe poder salir a la API de Anthropic. Debe quedar la configuración en Dokploy y GitHub Secrets, una llave con límite de gasto por servicio, la salida verificada y una guía de rotación.

**Decisiones:**

- Una workspace y una llave por servicio (NEXO y Cubicador) con límite de gasto mensual, para aislar costos y poder revocar una sin afectar la otra.
- Secretos solo en variables de entorno de Dokploy y GitHub Secrets; nunca en el repo ni en el front.

#### [S7-HU-32.4] Tabla tbl_ia_uso

**Issue:** [NEXO#352](https://github.com/InnovacionIA-proyectos/NEXO/issues/352) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** scalderon-bs

Hace falta registrar cada uso de la IA para costo, auditoría y evaluación. Debe quedar la tabla tbl_ia_uso (caso de uso, usuario, modelo, tokens, latencia, resultado) con script idempotente y ESQUEMA_NEXO.sql actualizado.

#### [S7-HU-32.5] Dataset dorado y arnés de evaluación

**Issue:** [NEXO#353](https://github.com/InnovacionIA-proyectos/NEXO/issues/353) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-29 · **Responsable:** kcano-ux, scalderon-bs

Las metas de acierto y la elección del modelo no se pueden decidir a ojo. Debe quedar un conjunto de 8 a 10 Excels reales anonimizados con su mapeo correcto y un script que compare acierto por campo, costo y latencia entre modelos y esfuerzos, con una recomendación escrita.

**Decisiones:**

- Modelo y esfuerzo quedan en configuración y se fijan con una evaluación A/B, no a ojo.
- Si el tiempo apremia, se arranca con Sonnet 4.6 en esfuerzo medio y se cambia sin tocar código.
- El CI no ejecuta la evaluación; se corre bajo demanda.

#### [S7-HU-32.6] QA de seguridad de la capa de IA

**Issue:** [NEXO#354](https://github.com/InnovacionIA-proyectos/NEXO/issues/354) · **Creada:** 2026-10-26 · **Estado:** Abierta · **Responsable:** kcano-ux

Hay que comprobar que la capa de IA no abre huecos de seguridad. Debe quedar evidencia de que la llave no aparece en el bundle ni en respuestas, que se respetan los roles, que el apagado funciona y que un Excel con instrucciones escondidas no altera el mapeo validado.

**Decisiones:**

- El contenido de los Excel y del chat se trata como no confiable (inyección de instrucciones): esquema estricto y sin herramientas en el flujo de Excel.

#### [S7-HU-32.7] Registro de uso y control de costo (código)

**Issue:** [NEXO#355](https://github.com/InnovacionIA-proyectos/NEXO/issues/355) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** ivasco-dev

El gasto en IA debe ser visible y acotado. El backend debe escribir en tbl_ia_uso cada llamada sin guardar el contenido del Excel, aplicar un tope por usuario y por día (429 al superarlo) y tener un interruptor general de apagado.

**Decisiones:**

- Tope por usuario y por día más interruptor general como mitigación del riesgo de costo.

### S7-HU-33 Diccionario de términos por cliente

#### [S7-HU-33.1] Tabla del diccionario por cliente

**Issue:** [NEXO#356](https://github.com/InnovacionIA-proyectos/NEXO/issues/356) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** scalderon-bs

Hoy solo existe el diccionario de alias de vehículo y contenedor; no hay uno de términos por cliente. Debe quedar tbl_diccionario_cliente (cliente, término, concepto destino, tipo, ejemplo, activo), su relación con tbl_alias_elementos definida y el script idempotente.

#### [S7-HU-33.2] Carga inicial del diccionario

**Issue:** [NEXO#357](https://github.com/InnovacionIA-proyectos/NEXO/issues/357) · **Creada:** 2026-10-26 · **Estado:** Abierta · **Responsable:** scalderon-bs

El diccionario nuevo arranca vacío. Debe quedar cargado con los términos y conceptos que ya se tienen por cliente, verificado y sin duplicados.

**Decisiones:**

- (2026-11-03) Primera carga con un solo Excel de licitación (el cliente D del dataset dorado). Ese cliente no existe en la base, así que todos los términos quedan como generales: 27 en `seeder/diccionario_cliente.csv` (15 columnas → `ignorar`, 11 contenedores y «NPR / Turbo»). Se cargaron en las dos bases de pruebas, sin duplicados.
- No se cargan unidades porque el backend compara los encabezados contra términos de cualquier tipo. Quedan fuera «Ruta», «Tipo de vehículo», «Secuencia de entrega», NHR y C2 liviano. El detalle está en `ORIGEN-Y-DECISIONES-SEEDER.md`.

#### [S7-HU-33.3] CRUD del diccionario en el backend

**Issue:** [NEXO#358](https://github.com/InnovacionIA-proyectos/NEXO/issues/358) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** ivasco-dev

El diccionario necesita mantenerse desde la aplicación. Deben quedar endpoints para listar, crear, editar y borrar términos, con validación de repetidos y contrato documentado.

**Decisiones:**

- Escritura para AdminBiblioteca, AdminTarifas y Admin; lectura para quien lo necesite.

#### [S7-HU-33.4] Pantalla para administrar el diccionario

**Issue:** [administracion#448](https://github.com/InnovacionIA-proyectos/administracion/issues/448) · **Creada:** 2026-10-26 · **Estado:** Abierta · **Responsable:** jpvelez-bts

Los administradores necesitan una pantalla para dar de alta, editar y buscar términos por cliente. Debe respetar roles, mostrar los errores del backend y permitir opcionalmente importar desde Excel.

### S7-HU-34 Importación inteligente de Excel de clientes

#### [S7-HU-34.1] Endpoint que sugiere el mapeo con Claude

**Issue:** [NEXO#359](https://github.com/InnovacionIA-proyectos/NEXO/issues/359) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** ivasco-dev

Hoy cada usuario mapea a mano las columnas del Excel de cada cliente. Debe quedar POST /api/captura-solicitudes/sugerir-mapeo, que con el análisis existente y el diccionario del cliente devuelve, en salida estructurada, el campo destino, la transformación, la confianza y las dudas por columna.

**Decisiones:**

- Claude entiende y propone; el código transforma y valida; el usuario confirma.
- Solo se envía lo necesario: encabezados, 15 a 20 filas de muestra, valores distintos, diccionario y campos destino; sin NIT ni nombres personales.
- Si la IA falla o está apagada, se devuelve el análisis sin sugerencia y sigue el mapeo manual.
- El flujo de Excel no le da herramientas al modelo.

#### [S7-HU-34.2] Aplicar el mapeo de forma determinista y validar

**Issue:** [NEXO#360](https://github.com/InnovacionIA-proyectos/NEXO/issues/360) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** ivasco-dev

El mapeo confirmado debe aplicarse a todas las filas sin intervención del modelo. Deben quedar aplicadas las transformaciones declaradas (kg/t, separar origen-destino, coma decimal, fechas), validadas con las mismas reglas de la captura manual y con errores por número de fila.

**Decisiones:**

- Los números y las fechas no los transforma el modelo; solo propone cómo transformarlos.

#### [S7-HU-34.3] Tabla de plantillas de mapeo por cliente

**Issue:** [NEXO#361](https://github.com/InnovacionIA-proyectos/NEXO/issues/361) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-27 · **Responsable:** scalderon-bs

Para no volver a llamar a la IA con el mismo formato, hay que guardar el mapeo confirmado de cada cliente. Debe quedar la tabla creada y documentada, con script idempotente.

**Decisiones:**

- El mapeo confirmado se guarda por cliente como plantilla para reducir costo y variabilidad.

#### [S7-HU-34.4] Normalizar valores de texto libre (vehículo, carrocería, ciudad)

**Issue:** [NEXO#362](https://github.com/InnovacionIA-proyectos/NEXO/issues/362) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** ivasco-dev

Los textos libres del Excel no siempre resuelven con los catálogos. Deben resolverse primero con alias y DANE, mandar a Claude solo los valores distintos no resueltos en una llamada por lote, y enviar a la cuarentena lo que siga sin resolver.

**Decisiones:**

- Una llamada por lote de valores distintos, no por fila.
- Nada se asigna sin que una regla determinista lo valide.

#### [S7-HU-34.5] Pantalla de revisión del mapeo sugerido

**Issue:** [administracion#449](https://github.com/InnovacionIA-proyectos/administracion/issues/449) · **Creada:** 2026-10-26 · **Estado:** Abierta · **Responsable:** jpvelez-bts

El usuario debe ver y corregir la propuesta antes de usarla. La pantalla de mapeo actual debe mostrar la propuesta prellenada con confianza y dudas resaltadas, permitir corregir, confirmar y guardar como plantilla, y seguir en modo manual si la IA no responde.

**Decisiones:**

- No se avanza sin confirmación del usuario, para evitar la confianza ciega en un mapeo equivocado.

#### [S7-HU-34.6] Prompt, esquema y evaluación del mapeo

**Issue:** [NEXO#363](https://github.com/InnovacionIA-proyectos/NEXO/issues/363) · **Creada:** 2026-10-26 · **Estado:** Abierta · **Responsable:** bquinterobtst

El prompt y el esquema del mapeo deben afinarse con datos reales. Debe quedar un prompt versionado en el repo y un informe que muestre el acierto por campo frente a la meta acordada.

**Decisiones:**

- Meta inicial propuesta: 90 % de columnas correctas sin intervención.
- Conviene resolver HU-37 antes de medir el acierto, porque afecta la coincidencia con el Excel real.

#### [S7-HU-34.7] QA de punta a punta con Excels de clientes

**Issue:** [NEXO#364](https://github.com/InnovacionIA-proyectos/NEXO/issues/364) · **Creada:** 2026-10-26 · **Estado:** Abierta · **Responsable:** kcano-ux

Hay que probar el flujo completo (subir, sugerir, revisar, confirmar, capturar, costear) con Excels reales anonimizados, incluidos los de celdas combinadas, columnas extra y unidades mezcladas. Debe quedar una matriz de clientes y resultados y confirmado que el flujo manual de respaldo funciona.

#### [S7-HU-34.8] Backend reutiliza la plantilla guardada

**Issue:** [NEXO#365](https://github.com/InnovacionIA-proyectos/NEXO/issues/365) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-27 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Cuando llega un Excel con los mismos encabezados de un cliente, no tiene sentido volver a llamar a la IA. El backend debe usar la plantilla guardada y volver a sugerir solo si cambian los encabezados.

**Decisiones:**

- Con plantilla reconocida no se llama a la IA (cero costo la segunda vez).

### S7-HU-35 Cubicador asistido por IA

#### [S7-HU-35.1] Estudio comparativo: motor actual contra IA

**Issue:** [NEXO#366](https://github.com/InnovacionIA-proyectos/NEXO/issues/366) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-27 · **Responsable:** ivasco-dev, bquinterobtst

Antes de tocar el Cubicador hay que medir. Debe quedar un documento con métricas (acierto del vehículo, flota mínima, capacidad y volumen, latencia p95, costo), un dataset dorado de cargas reales con la decisión humana y la línea base del motor actual.

**Decisiones:**

- El Cubicador no se migra por completo a la IA: el motor determinista está verificado, es reproducible y más rápido.
- La IA se usa como apoyo para entender la carga y explicar el resultado.
- Se empieza por medir; no se migra nada sin números.

#### [S7-HU-35.2] Modo sombra de la IA en el Cubicador

**Issue:** [NEXO#367](https://github.com/InnovacionIA-proyectos/NEXO/issues/367) · **Creada:** 2026-10-26 · **Estado:** Abierta · **Responsable:** bquinterobtst

Para comparar sin riesgo, el servicio Python debe llamar a Claude en paralelo sin afectar ni retrasar la respuesta al usuario y registrar la diferencia con el motor. Debe tener interruptor de apagado y su propia llave con límite de gasto.

**Decisiones:**

- La IA propone en paralelo pero no decide.

#### [S7-HU-35.3] Interpretar descripciones de mercancía en posiciones estructuradas

**Issue:** [NEXO#368](https://github.com/InnovacionIA-proyectos/NEXO/issues/368) · **Creada:** 2026-10-26 · **Estado:** Abierta · **Responsable:** bquinterobtst

Las descripciones en texto libre de la carga dependen hoy de una captura correcta, y cuando falta el tipo el plan se rechaza (#337). Debe quedar la conversión de texto a posiciones con tipo, dimensiones, peso y cantidad, siempre validada por el motor.

**Decisiones:**

- Toda salida de la IA pasa por las validaciones del motor; lo que falle se rechaza con motivo, sin inventar valores.

#### [S7-HU-35.4] Explicación de la recomendación en lenguaje natural

**Issue:** [NEXO#369](https://github.com/InnovacionIA-proyectos/NEXO/issues/369) · **Creada:** 2026-10-26 · **Estado:** Abierta · **Responsable:** bquinterobtst, jpvelez-bts

El usuario no ve por qué se eligió un vehículo. Debe quedar un texto corto generado a partir de los números ya calculados por el motor, que coincida con ellos y que no impida usar la pantalla si la IA falla.

**Decisiones:**

- La explicación no recalcula: usa los valores ya calculados por el motor.

#### [S7-HU-35.5] Decisión de alcance (go / no-go)

**Issue:** [NEXO#370](https://github.com/InnovacionIA-proyectos/NEXO/issues/370) · **Creada:** 2026-10-26 · **Estado:** Abierta · **Responsable:** bquinterobtst

Con el informe del modo sombra, el equipo debe decidir si se amplía el papel de la IA en el Cubicador. Debe quedar un acta con la decisión y el alcance de una eventual HU siguiente.

**Decisiones:**

- Criterio propuesto para ampliar: acierto igual o superior al del motor y p95 de latencia no peor; si no, la IA se queda como capa de entendimiento y explicación.

### S7-HU-36 Asistente de cotización para AdminTarifas

#### [S7-HU-36.1] Herramientas de lectura del asistente

**Issue:** [NEXO#371](https://github.com/InnovacionIA-proyectos/NEXO/issues/371) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-27 · **Responsable:** ivasco-dev

El asistente necesita consultar lo que NEXO ya sabe sin inventar. Deben quedar envoltorios de lectura (piso SICE-TAC, plaza, histórico, aprovisionamiento, propuesta y motivo de pendiente, conexos, ciudades por DANE) ejecutados con la identidad del usuario y devolviendo valor y fuente.

**Decisiones:**

- Ningún valor sale del modelo: todo precio viene de una herramienta con su fuente.
- Permisos heredados: el asistente nunca puede más que el usuario.
- El asistente usa el motor de costeo existente (clsReglaDeCosteo), no lo reemplaza.
- La conexión de solo lectura al tarifario, SICE-TAC e histórico (§10.4) queda cubierta aquí, sin issue nueva.
- Regla del mayor vs. SICE-TAC puro, devolución de contenedor y Sencillo como Cupo: NEXO no cambia su comportamiento automático; el AdminTarifas lo pide en el chat.

#### [S7-HU-36.2] Conversación con respuesta en tiempo real, límites y auditoría

**Issue:** [NEXO#372](https://github.com/InnovacionIA-proyectos/NEXO/issues/372) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-27 · **Responsable:** ivasco-dev

Hace falta el endpoint conversacional del asistente para AdminTarifas y Admin. Debe responder en streaming, con límite de turnos, tope por usuario, interruptor de apagado y registro de cada conversación y herramienta usada.

**Decisiones:**

- No inventa precios: todo valor trae la fuente de una herramienta.
- Cada conversación y herramienta queda registrada, con tope por usuario y por día.

#### [S7-HU-36.3] Herramientas de escritura con confirmación

**Issue:** [NEXO#373](https://github.com/InnovacionIA-proyectos/NEXO/issues/373) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-27 · **Responsable:** ivasco-dev

El asistente debe poder proponer cambios (editar flete, agregar conexos, rechazar línea, cambiar vehículo) sin ejecutarlos por su cuenta. Cada escritura exige confirmación explícita, queda auditada y respeta las reglas del dominio.

**Decisiones:**

- El modelo solo propone; el usuario pulsa «Aplicar».
- Los rechazos del dominio (por ejemplo 409 FLETE_SOLO_SICETAC) se explican, no se evaden.
- No se pasa a la fase de escritura sin pruebas de permisos y de no inventar precios.

#### [S7-HU-36.4] Panel de chat en la Mesa de Costeo

**Issue:** [administracion#450](https://github.com/InnovacionIA-proyectos/administracion/issues/450) · **Creada:** 2026-10-26 · **Estado:** Abierta · **Responsable:** jpvelez-bts

AdminTarifas necesita el asistente dentro de la Mesa de Costeo. Debe quedar un panel con respuesta progresiva, tarjetas de desglose con fuente y botón «Aplicar», que distinga lo propuesto de lo guardado y que no afecte la Mesa si no hay IA.

#### [S7-HU-36.5] Instrucciones, guardarraíles y evaluación del asistente

**Issue:** [NEXO#374](https://github.com/InnovacionIA-proyectos/NEXO/issues/374) · **Creada:** 2026-10-26 · **Estado:** Abierta · **Responsable:** bquinterobtst

El comportamiento del asistente debe medirse, incluidos los intentos de saltarse reglas. Deben quedar las instrucciones, los casos de evaluación (con adversarios) y un informe versionado con cero precios sin fuente.

**Decisiones:**

- Los intentos de saltarse una regla (por ejemplo «ignora Solo-SICETAC») deben rechazarse.

#### [S7-HU-36.6] QA de permisos y de «no inventar precios»

**Issue:** [NEXO#375](https://github.com/InnovacionIA-proyectos/NEXO/issues/375) · **Creada:** 2026-10-26 · **Estado:** Abierta · **Responsable:** kcano-ux

Hay que verificar por rol y con casos adversarios que el asistente no da más poder que la pantalla normal. Debe quedar evidencia de que ninguna escritura ocurre sin confirmación y de que la auditoría y los topes funcionan.

#### [S7-HU-36.7] Reglas/preferencias propias del AdminTarifas

**Issue:** [NEXO#376](https://github.com/InnovacionIA-proyectos/NEXO/issues/376) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-27 · **Responsable:** ivasco-dev

El AdminTarifas quiere decirle al asistente sus propias preferencias (por ejemplo, avisar si plaza e histórico difieren mucho). El asistente debe guardarlas desde la conversación, aplicarlas en conversaciones futuras de forma visible y nunca usarlas para saltarse el piso SICE-TAC ni inventar precios.

**Decisiones:**

- La preferencia se dice directo en el chat, sin fase previa de levantamiento ni catálogo cerrado de tipos de regla.
- Son preferencias del usuario: nunca reemplazan ni saltan las reglas de clsReglaDeCosteo.

### S7-HU-37 Correcciones del análisis contra un Excel real de cliente

#### [S7-HU-37.1] Tabla de mapeo de vehículo (SICE-TAC + Plaza/SAP)

**Issue:** [NEXO#377](https://github.com/InnovacionIA-proyectos/NEXO/issues/377) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-28 · **Responsable:** scalderon-bs · *issue que agrupa varias historias*

NEXO asume que un mismo código de vehículo sirve para SICE-TAC y para Plaza/SAP, y no es así: Turbo y Sencillo caen en otro vehículo más barato sin fallar. Debe quedar una tabla que dé, por tipo de vehículo, su código real en SICE-TAC y en Plaza/SAP, revisando todos los vehículos contra datos reales y documentando el origen de cada código.

**Decisiones:**

- Se crea una tabla explícita de mapeo en vez de la conversión fija en código.
- Se revisan todos los tipos de vehículo, no solo Turbo y Sencillo, para detectar casos silenciosos.

#### [S7-HU-37.2] SICE-TAC para Contenedor/Cupo: buscar primero por DANE

**Issue:** [NEXO#378](https://github.com/InnovacionIA-proyectos/NEXO/issues/378) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-26 · **Responsable:** ivasco-dev

Para Contenedor y Cupo, la consulta oficial de SICE-TAC busca la ruta solo por texto y nunca pasa por el selector que usa DANE, por lo que 18 líneas del análisis eligieron una ciudad equivocada. Debe buscar primero por DANE y usar el texto solo como respaldo, sin romper los casos que hoy coinciden bien.

**Decisiones:**

- Decidido por Sara (2026-10-26): DANE primero y texto solo como respaldo, igual que el selector tolerante.
- No se persigue por ahora verificar si las 18 líneas son todas Contenedor/Cupo; queda cubierto lo confirmado en código.

#### [S7-HU-37.3] Tabla tbl_rangos_peso_contenedor

**Issue:** [NEXO#379](https://github.com/InnovacionIA-proyectos/NEXO/issues/379) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-28 · **Responsable:** scalderon-bs

El recomendador de vehículo por peso de contenedor usa reglas quemadas en código. Debe quedar la tabla tbl_rangos_peso_contenedor cargada desde el Excel normalizado de rangos, con los rangos confirmados por negocio.

**Decisiones:**

- Antes de cargar hay que decidir qué hacer con la fila sin tope de peso (vehículo «2», contenedor de 40 pies).

#### [S7-HU-37.4] Backend consulta la tabla de mapeo de vehículo

**Issue:** [NEXO#380](https://github.com/InnovacionIA-proyectos/NEXO/issues/380) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-27 · **Responsable:** ivasco-dev

Con la tabla de mapeo creada, el backend debe dejar de usar el código genérico del vehículo. Al consultar SICE-TAC y Plaza/SAP debe usar el código mapeado, y Turbo y Sencillo deben quedar resueltos en producción.

#### [S7-HU-37.5] Backend consume tbl_rangos_peso_contenedor + Portacontenedores por defecto

**Issue:** [NEXO#381](https://github.com/InnovacionIA-proyectos/NEXO/issues/381) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-27 · **Responsable:** ivasco-dev

En las líneas de contenedor del análisis NEXO usó carrocería general y un vehículo más barato que el esperado. El recomendador debe leer la tabla de rangos en vez de reglas quemadas y usar siempre PORTACONTENEDORES como carrocería por defecto en líneas de Contenedor.

**Decisiones:**

- Para líneas de Contenedor la carrocería por defecto es siempre PORTACONTENEDORES, no cualquiera ni la más barata.

### Hallazgos restantes del análisis de Ismael

#### Hallazgos del análisis que no se convirtieron en HU

**Sin issue** · **Fecha:** 2026-10-23 (documento) · **Tipo:** Propuesta

**Estado según el documento:** Decidido sin crear issue (2026-10-26).

Varios hallazgos de la comparación contra el Excel real de un cliente se revisaron y se decidió no crear issue. Queda registrado qué se hace con cada uno para no reabrir la discusión.

**Decisiones:**

- Regla del mayor vs. SICE-TAC puro: NEXO no cambia; el AdminTarifas pide el valor puro en el chat.
- Devolución de contenedor: se queda en VACÍO; un caso distinto se especifica en el chat.
- Sencillo como Carga Suelta: no se cambia la clasificación por defecto; se pide Cupo en el chat.
- Servicios conexos faltantes (standby, cuadrilla, parada adicional, vehículo dedicado, hora de espera): no se agregan por ahora.
- Verificar si las 18 líneas de ciudad equivocada son Contenedor/Cupo: no se persigue por ahora.

## 11. Pendientes de datos

*Documento de planeación del 2026-10-26.* Pendientes de datos y coordinación que no son historias de código.

### Catálogo de municipios

#### Ciudades de exportación faltantes en el catálogo de municipios

**Sin issue** · **Fecha:** 2026-10-26 (documento) · **Tipo:** Pendiente · **Responsable:** Sara

**Estado según el documento:** Pendiente; verificado contra un export de la tabla de municipios del 2026-10-21.

De tres destinos de exportación, uno existe en el catálogo pero sin zona de tarificación (por eso no aparecería en el selector), otro no existe y el tercero no se ha revisado. Hay que revisarlo con Ismael o el equipo de datos y decidir si se asigna la zona, se crea el municipio faltante y se verifica el tercero.

**Decisiones:**

- No es una HU de código: es un pendiente de datos y coordinación, aparte del plan del 2026-10-23.

### Tarifas de plaza

#### Tarifa de plaza que parece mal cargada (Funza a Cota, Mula)

**Sin issue** · **Fecha:** 2026-10-26 (documento) · **Tipo:** Pendiente · **Responsable:** Sara

**Estado según el documento:** Pendiente.

Para la ruta Funza a Cota con vehículo Mula, NEXO propuso un valor varias veces mayor que el del Excel de referencia. No parece un error de lógica sino una tarifa mal cargada; hay que revisar esa fila con quien administra las tarifas de plaza y corregirla si está mal.

**Decisiones:**

- Se considera un problema de datos, no de la lógica de NEXO.

## 12. Pendientes del mapeo de vehículo

*Documento de planeación del 2026-10-27.* Lo que faltaba para cerrar la tabla de mapeo de vehículos entre SICE-TAC y Plaza/SAP.

### Mapeo de vehículo (#377)

#### tbl_tarifas_plaza no usa los mismos códigos que SAP

**Issue:** [NEXO#377](https://github.com/InnovacionIA-proyectos/NEXO/issues/377) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-28 · **Responsable:** scalderon-bs · *issue que agrupa varias historias*

El mapeo de vehículo tiene una sola columna de código para tblSAPTarifas y tbl_tarifas_plaza, pero el seeder de tarifas de plaza usa códigos de SICE-TAC. Con el mapeo actual, la fila de 2L3 no se encontraría y las filas de 3S2 nunca se usarían. Hay que confirmar el contenido real de la tabla y elegir una solución.

**Decisiones:**

- Primero confirmar qué hay hoy en la tabla real, porque el CSV es solo la carga inicial.
- Opciones abiertas: corregir tbl_tarifas_plaza a códigos SAP (una columna) o separar una columna de código para tbl_tarifas_plaza.

#### Comparar los dos catálogos de vehículos de SAP

**Issue:** [NEXO#377](https://github.com/InnovacionIA-proyectos/NEXO/issues/377) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-28 · **Responsable:** scalderon-bs · *issue que agrupa varias historias*

La llave foránea de tblSAPTarifas apunta a tblSAPTiposVehiculo, pero NEXO traduce el ID con tblSAPTiposVehiculoTarifario. Si los dos catálogos no coinciden por ID, NEXO consulta SAP con el vehículo equivocado; hay que compararlos.

**Decisiones:**

- Cualquier diferencia que aparezca se documenta para #380.

#### Evidencia de que cada código de SICE-TAC existe

**Issue:** [NEXO#377](https://github.com/InnovacionIA-proyectos/NEXO/issues/377) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-28 · **Responsable:** scalderon-bs · *issue que agrupa varias historias*

Para respaldar el mapeo de todos los vehículos hay que contar, por código de configuración, cuántas filas tiene tblRutasSicetac. Un código con cero filas indica que está mal escrito o no existe en SICE-TAC.

#### Documentar el seeder mapeo_vehiculos.csv

**Issue:** [NEXO#377](https://github.com/InnovacionIA-proyectos/NEXO/issues/377) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-28 · **Responsable:** scalderon-bs · *issue que agrupa varias historias*

Falta agregar la sección de mapeo_vehiculos.csv en el README de seeders, igual que los demás. Depende de la decisión del punto 1.

#### Cerrar #377 (commit, push y PR a QA)

**Issue:** [NEXO#377](https://github.com/InnovacionIA-proyectos/NEXO/issues/377) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-28 · **Responsable:** scalderon-bs · *issue que agrupa varias historias*

Hay que subir el script, el CSV, ESQUEMA_NEXO.sql, .gitignore y el archivo de pendientes en una PR a QA.

**Decisiones:**

- Se anticipa un conflicto en .gitignore con la PR #395, fácil de resolver.

### Esquema NEXO

#### ESQUEMA_NEXO.sql se detenía a mitad de camino en una base nueva

**Sin issue** · **Fecha:** 2026-10-27 (documento) · **Tipo:** Pendiente

**Estado según el documento:** Resuelto (2026-10-27): probado en SQL Server 2019 vacío; coincide con la base de pruebas salvo las 8 llaves foráneas del punto 8.

Al correr ESQUEMA_NEXO.sql en una base vacía, la llave foránea de OutboxMessage fallaba porque el índice único de InboxState se creaba después, y lo siguiente no se creaba. Debía quedar el orden corregido y probado en una base vacía.

**Decisiones:**

- Los índices van antes que las llaves foráneas.
- El archivo crea el esquema db_NEXO si no existe y fija QUOTED_IDENTIFIER y ANSI_NULLS (sin eso sqlcmd fallaba en los índices filtrados).

#### A la base de pruebas le faltan 8 llaves foráneas

**Sin issue** · **Fecha:** 2026-10-27 (documento) · **Tipo:** Pendiente

**Estado según el documento:** Pendiente de decidir qué hacer con las 2 solicitudes huérfanas.

Al comparar el esquema contra la base de pruebas faltan 8 llaves foráneas que sí están en el esquema y en el modelo del backend; una de ellas la incumplen 2 solicitudes cuyo creador ya no existe. Hay que resolver esas filas huérfanas y crear las llaves.

**Decisiones:**

- Las llaves se crean WITH CHECK para validar los datos existentes.
- Avisar antes a Ismael: con las llaves, el código que hoy guarda datos inconsistentes empezaría a fallar.

#### Restos en la base de pruebas que no están en el esquema

**Sin issue** · **Fecha:** 2026-10-27 (documento) · **Tipo:** Pendiente

**Estado según el documento:** Pendiente de decisión del equipo.

En db_NEXO existen una caché retirada de SICE-TAC y varias tablas de respaldo que no forman parte del esquema y no se recrean en una base nueva. Hay que decidir con el equipo si se borran.

**Decisiones:**

- Los respaldos del cambio de rol tienen su condición de borrado en PENDIENTE-ROLES-COORDINADORFLETES-ADMINBIBLIOTECA.md.

### Plantillas de mapeo (#365)

#### Responder a Ismael las decisiones sobre plantillas

**Issue:** [NEXO#365](https://github.com/InnovacionIA-proyectos/NEXO/issues/365) · **Creada:** 2026-10-26 · **Estado:** Cerrada el 2026-10-27 · **Responsable:** ivasco-dev · *issue que agrupa varias historias*

Ismael planteó preguntas al implementar las plantillas de mapeo (#365). Quedan definidas las respuestas, pero falta comunicárselas.

**Decisiones:**

- Si el cliente cambia el orden de las columnas, se vuelve a sugerir.
- Con plantilla manda la plantilla; confirmar no agrega términos al diccionario; editar el diccionario no invalida plantillas.
- Consecuencia: el diccionario solo se llena a mano y arranca casi vacío, porque la carga inicial (#357) no tiene fuente.
- No hace falta clienteId desde la solicitud por ahora: NEXO no asigna clientes por usuario y tbl_audit_log ya registra quién toca cada plantilla.

## 13. Precio de venta de los servicios conexos

*Documento de planeación del 2026-10-28.* Precio de venta en la biblioteca de conexos y en la cotización, en NEXO y en Cotifly.

### Pendiente H: celda «PRECIO VENTA» vacía al importar

**Sin issue** · **Fecha:** 2026-10-28 (documento) · **Tipo:** Pendiente · **Responsable:** Sara (scalderon-bs)

**Estado según el documento:** Pendiente de Sara.

Al importar el Excel de la biblioteca, falta definir si una celda de precio de venta vacía borra el valor o lo deja como estaba. También qué pasa si el archivo no trae la columna.

**Decisiones:**

- Propuesta: la celda vacía deja el valor como estaba (igual que ACTIVO desde #261) y para borrarlo se usa la pantalla; un archivo sin la columna no toca nada.

### Pendiente I: precio de venta menor que el costo

**Sin issue** · **Fecha:** 2026-10-28 (documento) · **Tipo:** Pendiente · **Responsable:** Sara (scalderon-bs)

**Estado según el documento:** Pendiente de Sara.

Falta decidir si se permite registrar un precio de venta inferior al costo del servicio.

**Decisiones:**

- Propuesta: se permite (puede haber servicios vendidos a pérdida a propósito), pero la pantalla muestra un aviso.

### [S6-HU-38.1] Precio de venta en los servicios conexos (base de datos)

**Issue:** [NEXO#407](https://github.com/InnovacionIA-proyectos/NEXO/issues/407) · **Creada:** 2026-10-28 · **Estado:** Cerrada el 2026-10-29 · **Responsable:** scalderon-bs

Cada conexo solo tiene un costo, que es lo que hoy se cobra. Deben quedar PrecioVenta (opcional) en tbl_servicios_conexos y PrecioVentaUnitario (obligatorio) en tbl_cotizacion_conexos, con las filas existentes llenas con su costo, script idempotente y esquema, diccionario y seeder actualizados.

**Decisiones:**

- NEXO y Cotifly cotizan los conexos con el precio de venta (PrecioVentaUnitario × Cantidad) (Sara, 2026-10-28).
- PrecioVenta puede quedar vacío en la biblioteca; en la cotización nunca: si no hay precio de venta se guarda el costo.
- Las filas existentes se llenan con ValorAplicado / Cantidad, para que el total de las cotizaciones no cambie (todas tenían valor de catálogo, ninguno a mano).

### [S6-HU-38.2] Backend: precio de venta en la biblioteca y en la cotización

**Issue:** [NEXO#408](https://github.com/InnovacionIA-proyectos/NEXO/issues/408) · **Creada:** 2026-10-28 · **Estado:** Cerrada el 2026-10-29 · **Responsable:** ivasco-dev

Los endpoints de la biblioteca y de la cotización deben manejar el precio de venta. Debe quedar el precio en crear, editar (incluido borrarlo), importar, exportar y plantillas, el valor de venta fijado al agregar el conexo y usado en el total, el correo y el Excel, sin cambiar el total de las cotizaciones existentes.

**Decisiones:**

- NEXO y Cotifly cotizan los conexos con el precio de venta (PrecioVentaUnitario × Cantidad) (Sara, 2026-10-28).
- El valor de venta queda fijo en la cotización al agregar el conexo; cambios posteriores en la biblioteca no la afectan.
- NEXO sigue mandando el costo (valorAplicado) aparte del valor de venta (valorVenta), porque costo_final de Cotifly depende del costo real.
- La API sigue aceptando valorAplicado opcional; el bloqueo de escritura manual se hace en el front.
- El costo solo lo ve el personal interno; nunca el cliente, el correo ni el Excel de la cotización.
- La importación debe buscar primero la columna de venta para no leerla como costo.
- Hace falta una prueba que detecte un conexo cotizado en 0 por no llenar PrecioVentaUnitario.

### [S6-HU-38.3] QA: precio de venta de conexos de punta a punta

**Issue:** [NEXO#409](https://github.com/InnovacionIA-proyectos/NEXO/issues/409) · **Creada:** 2026-10-28 · **Estado:** Abierta · **Responsable:** kcano-ux

Hay que probar en QA el ciclo NEXO → Cotifly → clientepro con todas las partes integradas. Debe quedar comprobado que se cotiza con el precio de venta (o el costo si no hay), sin margen de Cotifly en conexos, sin mostrar el costo al cliente y sin alterar cotizaciones existentes.

**Decisiones:**

- El margen de Cotifly se aplica solo al flete, nunca a un conexo.
- Las cotizaciones de Cotifly ya finalizadas no se recalculan.

### [S6-HU-41.1] Servicios conexos: precio de venta en la biblioteca y en la Mesa de Costeo

**Issue:** [administracion#468](https://github.com/InnovacionIA-proyectos/administracion/issues/468) · **Creada:** 2026-10-28 · **Estado:** Abierta · **Responsable:** jpvelez-bts

El front de NEXO debe mostrar y editar el precio de venta en la biblioteca y usar el valor de venta en la Mesa de Costeo. Debe quitarse el campo para escribir a mano el valor de un conexo.

**Decisiones:**

- El Comercial no puede escribir a mano el valor de un conexo: sale siempre de la biblioteca.
- Los permisos de edición de la biblioteca no cambian (ServiciosLogisticos, AdminTarifas y Admin).
- El costo puede mostrarse en la Mesa solo como dato interno de referencia.

### [HU-043.1] Precio de venta en los conexos de la cotización (Cotifly, datos)

**Issue:** [Cotifly#245](https://github.com/InnovacionIA-proyectos/Cotifly/issues/245) · **Creada:** 2026-10-28 · **Estado:** Cerrada el 2026-10-29 · **Responsable:** scalderon-bs

Cotifly guarda los conexos con un solo valor, que es el costo. Debe quedar la columna precioVenta obligatoria en la tabla de conexos de la cotización, llena con el costo en las filas existentes, con script idempotente.

**Decisiones:**

- No cambia ninguna cotización existente.
- Se puede aplicar antes del backend.

### [HU-043.2] Servicios conexos con precio de venta y sin margen (Cotifly, backend)

**Issue:** [Cotifly#246](https://github.com/InnovacionIA-proyectos/Cotifly/issues/246) · **Creada:** 2026-10-28 · **Estado:** Cerrada el 2026-10-29 · **Responsable:** dguerraBSSL

Cotifly lee solo el costo de cada conexo y le aplica el margen junto con el flete. Debe leer y guardar el valor de venta, partir la fórmula para que el margen aplique solo a flete, retorno y levante, y usar el precio de venta en captura de rutas, detalle y licitación, manteniendo el costo real para ganancias y costo_final.

**Decisiones:**

- El margen de Cotifly se aplica solo al flete, nunca a un conexo.
- El comercial de Cotifly no puede cambiar el precio de venta para un cliente puntual.
- Las cotizaciones finalizadas no se recalculan.
- Mientras NEXO no envíe valorVenta, usar valorAplicado para no descartar el conexo.
- margen_promedio pasa a ser el margen sobre el flete.

### [HU-043-FE] Servicios conexos con precio de venta y sin margen (front de Cotifly)

**Issue:** [clientepro-frontend#127](https://github.com/InnovacionIA-proyectos/clientepro-frontend/issues/127) · **Creada:** 2026-10-28 · **Estado:** Abierta · **Responsable:** scalderon-bs

El detalle de cotización de clientepro calcula el flete restando y asume que el precio ya trae margen. Debe mostrar el valor de venta que manda el backend, usar los totales de transporte y conexos del backend, aclarar que el margen aplica solo al flete y no pintar nunca el costo de un conexo.

**Decisiones:**

- «Total costeo» sigue mostrando el costo real.

## 14. Importación inteligente de Excel en Cotifly

*Documento de planeación del 2026-10-29.* Cotifly reutiliza la importación inteligente de Excel de NEXO para licitaciones.

### S6-HU-34 Importación inteligente (NEXO)

#### [S6-HU-34.9] Importación inteligente con clientes reales: identificar al cliente por NIT

**Issue:** [NEXO#410](https://github.com/InnovacionIA-proyectos/NEXO/issues/410) · **Creada:** 2026-10-29 · **Estado:** Cerrada el 2026-10-29 · **Responsable:** ivasco-dev

Con el DW de clientes configurado, NEXO devuelve clienteId nulo para todos los clientes, así que en los ambientes reales no hay plantillas ni diccionario por cliente, ni en el portal ni en Cotifly. Los endpoints de importación deben aceptar también el NIT normalizado; las lecturas no crean clientes y guardar una plantilla crea el cliente local desde el DW si no existe.

**Decisiones:**

- El cliente se identifica ante NEXO por NIT (Sara, 2026-10-29): es llave única en tbl_clientes, sirve para prospectos y Cotifly siempre lo tiene.
- Descartado aceptar el BP: no cubre prospectos y Cotifly no siempre lo tiene.
- Descartado devolver el Id local en la búsqueda: no cubre clientes que nunca han cotizado en NEXO.
- P5: un prospecto sí guarda plantilla; NEXO crea su cliente local desde la tabla de prospectos del DW.

#### [S6-HU-34.10] El Comercial puede mantener el diccionario de términos por cliente

**Issue:** [NEXO#411](https://github.com/InnovacionIA-proyectos/NEXO/issues/411) · **Creada:** 2026-10-29 · **Estado:** Cerrada el 2026-10-29 · **Responsable:** ivasco-dev

Hoy solo AdminBiblioteca, AdminTarifas y Admin escriben en el diccionario por cliente. El rol Comercial también debe poder crear, editar y borrar términos, con auditoría, pruebas de roles y contrato actualizados.

**Decisiones:**

- P2: los comerciales pueden mantener el diccionario del cliente, en NEXO y desde Cotifly.

### HU-044 Importación inteligente de licitación (Cotifly)

#### [HU-044.1] Adaptador de importación inteligente hacia NEXO

**Issue:** [Cotifly#252](https://github.com/InnovacionIA-proyectos/Cotifly/issues/252) · **Creada:** 2026-10-29 · **Estado:** Abierta · **Responsable:** dguerraBSSL

Cotifly necesita consumir los endpoints de importación de NEXO desde su backend. Debe quedar un puerto y adaptador con cliente HTTP propio y timeout mayor que el de la IA, identidad del comercial, envío del NIT, mapeo de errores, interruptor de apagado, falso de NEXO para pruebas y el ADR-027.

**Decisiones:**

- Cotifly reutiliza los endpoints de NEXO; no replica el mapeo, la IA ni el diccionario.
- El front de clientepro nunca llama a NEXO; todo pasa por el backend de Cotifly.
- sugerir-mapeo y normalizar-valores no se reintentan porque cada llamada puede gastar IA.
- El 429 IA_TOPE_DIARIO se devuelve como error de negocio sin activar el bloqueo global, porque el tope es de un solo comercial.
- Propuesta por confirmar: no se crean tablas en Cotifly; plantillas y diccionario viven en NEXO.
- Propuesta por confirmar: la sesión con NEXO es la del comercial real, así que el tope diario de IA es por comercial.

#### [HU-044.2] POST /web/licitaciones/sugerir-mapeo

**Issue:** [Cotifly#253](https://github.com/InnovacionIA-proyectos/Cotifly/issues/253) · **Creada:** 2026-10-29 · **Estado:** Abierta · **Responsable:** dguerraBSSL

El front necesita una propuesta de mapeo de columnas en el formato de Cotifly. El endpoint debe pedir a NEXO la sugerencia (plantilla, diccionario, IA) con el NIT y hasta 200 filas, y devolver campo, transformación, confianza, origen y dudas por columna.

**Decisiones:**

- Con más de 100 encabezados no se llama a NEXO y el front sigue en manual.
- IA no disponible o NEXO caído responde 200 con lo que haya; el tope diario no se esconde.
- Se mandan hasta 200 filas porque NEXO las usa para decidir qué columnas oculta por datos personales.

#### [HU-044.3] POST /web/licitaciones/aplicar-mapeo y traducción a líneas de Cotifly

**Issue:** [Cotifly#254](https://github.com/InnovacionIA-proyectos/Cotifly/issues/254) · **Creada:** 2026-10-29 · **Estado:** Abierta · **Responsable:** dguerraBSSL

Hoy el front arma las líneas a mano desde las columnas. El endpoint debe aplicar en NEXO el mapeo confirmado a todas las filas, traducir cada línea al formato de licitación de Cotifly y devolver errores por fila con el número real del Excel.

**Decisiones:**

- Se pasa primera_fila = fila de encabezado + 1 para que los errores digan la fila real.
- No reemplaza las validaciones de POST /web/licitaciones; siguen todas.
- La mercancía sin código se sigue sugiriendo con el catálogo del cubicador.

#### [HU-044.4] POST /web/licitaciones/normalizar-valores

**Issue:** [Cotifly#255](https://github.com/InnovacionIA-proyectos/Cotifly/issues/255) · **Creada:** 2026-10-29 · **Estado:** Abierta · **Responsable:** dguerraBSSL

Los textos de vehículo, carrocería y ciudad deben llevarse a valores oficiales. El endpoint debe usar NEXO (diccionario, alias, catálogo e IA con lista cerrada) y devolver por valor si quedó resuelto, ambiguo con candidatos o sin resolver.

**Decisiones:**

- P4: la clase de contenedor no se agrega a normalizar-valores de NEXO; sigue por GET /web/alias/resolver.
- La validación de municipios al crear la licitación se mantiene.

#### [HU-044.5] Plantillas de mapeo y diccionario del cliente

**Issue:** [Cotifly#256](https://github.com/InnovacionIA-proyectos/Cotifly/issues/256) · **Creada:** 2026-10-29 · **Estado:** Abierta · **Responsable:** dguerraBSSL

Cotifly debe permitir guardar, listar y borrar plantillas de mapeo del cliente en NEXO, y consultar y mantener el diccionario del cliente. Con plantilla, la siguiente carga del mismo formato no llama a la IA.

**Decisiones:**

- Cotifly no guarda copia; la auditoría la lleva NEXO.
- P2: el comercial puede crear, editar y borrar términos del diccionario desde Cotifly.

#### [HU-044.6] QA: importación inteligente de licitaciones de punta a punta (backend)

**Issue:** [Cotifly#257](https://github.com/InnovacionIA-proyectos/Cotifly/issues/257) · **Creada:** 2026-10-29 · **Estado:** Abierta · **Responsable:** kcano-ux

Hay que probar contra un NEXO con los endpoints desplegados todo el flujo: subir, sugerir, confirmar, aplicar, normalizar, guardar plantilla, recargar el mismo formato y crear la licitación, con Excels anonimizados de formatos distintos.

**Decisiones:**

- El tope diario de IA de un comercial no debe bloquear a otro.

#### Campos por confirmar con Duban en el mapeo

**Sin issue** · **Fecha:** 2026-10-29 (documento) · **Tipo:** Pendiente · **Responsable:** Duban (dguerraBSSL)

**Estado según el documento:** Pendiente.

Algunos campos de NEXO no tienen equivalente claro en Cotifly. Falta confirmar el significado de peso y pesoKg, y el uso de tipologia, deadline y referencia.

### HU-044-FE Importación inteligente (front de Cotifly)

#### [HU-044-FE.1] Repositorio y modelos de la importación inteligente

**Issue:** [clientepro-frontend#135](https://github.com/InnovacionIA-proyectos/clientepro-frontend/issues/135) · **Creada:** 2026-10-29 · **Estado:** Abierta · **Responsable:** scalderon-bs

El front necesita los métodos y modelos para los nuevos endpoints de Cotifly. Deben quedar en el repositorio abstracto, HTTP y falso, con mensajes para los errores nuevos y un falso que simule plantilla, diccionario, IA, IA no disponible y tope diario.

**Decisiones:**

- Se puede empezar con el repositorio falso antes de que salgan los endpoints.

#### [HU-044-FE.2] Mapeo sugerido prellenado en la pantalla de mapeo

**Issue:** [clientepro-frontend#136](https://github.com/InnovacionIA-proyectos/clientepro-frontend/issues/136) · **Creada:** 2026-10-29 · **Estado:** Abierta · **Responsable:** scalderon-bs

Hoy el mapeo se hace con un auto-mapeo por sinónimos que se recalcula en cada carga. Al elegir la hoja, el front debe pedir la sugerencia y prellenar campo, transformación, confianza, origen y dudas, permitiendo corregir y cayendo al auto-mapeo actual si no hay IA.

**Decisiones:**

- No se avanza sin que el comercial confirme el mapeo.
- El formato matriz no llama a sugerir-mapeo: se convierte en tabla (FE.7).

#### [HU-044-FE.3] Aplicar el mapeo en el backend y mostrar errores por fila

**Issue:** [clientepro-frontend#137](https://github.com/InnovacionIA-proyectos/clientepro-frontend/issues/137) · **Creada:** 2026-10-29 · **Estado:** Abierta · **Responsable:** scalderon-bs

El front debe dejar de armar las líneas a mano y usar las que devuelve aplicar-mapeo. Los errores por fila van a la tabla existente y los errores del mapeo se muestran en la pantalla de mapeo.

**Decisiones:**

- Con el interruptor apagado o sin backend se usa el armado local de hoy.

#### [HU-044-FE.4] Normalizar vehículo, carrocería y ciudades en la revisión

**Issue:** [clientepro-frontend#138](https://github.com/InnovacionIA-proyectos/clientepro-frontend/issues/138) · **Creada:** 2026-10-29 · **Estado:** Abierta · **Responsable:** scalderon-bs

En la revisión, los textos distintos de vehículo, carrocería y ciudad deben normalizarse en una sola llamada y mostrar el resultado por valor. Lo ambiguo se elige entre candidatos y lo propuesto por la IA requiere confirmación.

**Decisiones:**

- Una sola llamada con los textos distintos, no por fila.
- La clase de contenedor sigue con GET /web/alias/resolver (P4).

#### [HU-044-FE.5] Guardar el mapeo como plantilla del cliente

**Issue:** [clientepro-frontend#139](https://github.com/InnovacionIA-proyectos/clientepro-frontend/issues/139) · **Creada:** 2026-10-29 · **Estado:** Abierta · **Responsable:** scalderon-bs

El comercial debe poder guardar el mapeo confirmado como plantilla del cliente y ver o borrar sus plantillas. La siguiente carga del mismo formato debe indicar que usa el mapeo guardado.

**Decisiones:**

- P1: la plantilla se guarda solo si el comercial marca «Guardar este mapeo para el cliente».
- P5: los prospectos también pueden guardar plantilla.

#### [HU-044-FE.6] Administrar el diccionario del cliente

**Issue:** [clientepro-frontend#140](https://github.com/InnovacionIA-proyectos/clientepro-frontend/issues/140) · **Creada:** 2026-10-29 · **Estado:** Abierta · **Responsable:** scalderon-bs

Desde la pantalla de mapeo, el comercial debe poder consultar los términos del cliente y los generales, y crear, editar o borrar términos, con un atajo para agregar lo que no se reconoció.

**Decisiones:**

- P2: los comerciales mantienen el diccionario.
- Solo el rol comercial edita; gerente y director, si lo ven, en solo lectura.

#### [HU-044-FE.7] Convertir la licitación en formato matriz a tabla

**Issue:** [clientepro-frontend#141](https://github.com/InnovacionIA-proyectos/clientepro-frontend/issues/141) · **Creada:** 2026-10-29 · **Estado:** Abierta · **Responsable:** scalderon-bs

NEXO procesa una línea por fila y no entiende licitaciones en formato matriz (rutas en filas, vehículos en columnas). El front debe convertir la matriz en tabla, con encabezados y mapeo fijos, conservando la fila real del Excel, y mandarla por el mismo camino.

**Decisiones:**

- P3: NEXO no soporta matrices; el front de Cotifly las convierte en tabla.

#### [HU-044-FE.8] QA de punta a punta de la importación inteligente (front)

**Issue:** [clientepro-frontend#142](https://github.com/InnovacionIA-proyectos/clientepro-frontend/issues/142) · **Creada:** 2026-10-29 · **Estado:** Abierta · **Responsable:** kcano-ux

Hay que recorrer el flujo completo en el portal con Excels anonimizados: plantilla reconocida, IA, sin IA, tope diario, más de 100 columnas, ciudad ambigua, matriz convertida y diccionario actualizado por el comercial.
