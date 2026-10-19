# Mapa funcional de NEXO · Cabina de Fletes

**Documento de trabajo, no el entregable final.** Construido uniendo
`Entendimiento_Nexo (2).docx` (documento original de negocio),
`Nexo_Comparacion_Backend_vs_Entendimiento.md` (auditoría del backend
real), `NEXO PENDIENTES.xlsx` (segunda barrida) y una revisión directa
y sistemática del código real — frontend (`administracion_embarqa_nexo`)
y backend (`NEXO`) — módulo por módulo, rol por rol. Fecha: 2026-10-09.

Es insumo para construir el documento final de entendimiento funcional
(no técnico) que se entregará al cliente, bajo lineamientos que Sara
dará después. Describe FLUJOS y FUNCIONALIDAD, no código.

---

## 1. Qué es NEXO, en una frase

Reemplaza el costeo manual de fletes (cruzar a mano SICE-TAC + SAP +
Excel) por un sistema que propone automáticamente el flete por línea,
respetando el piso legal SICE-TAC y la tarifa de plaza, y resuelve
además la cubicación 3D del vehículo, los servicios adicionales y la
cotización final — todo auditado y trazable.

---

## 2. Los roles: 6 existen en el código, pero **solo se entregan 3**

El sistema (backend + frontend) tiene **6 roles** construidos en el
modelo de permisos. **Decisión de negocio (confirmada con Sara,
2026-10-09): al cliente se le entregan y se le asignan a usuarios
reales solo 3 — `Comercial`, `AdminTarifas` y `Admin`.**
`CoordinadorFletes`, `ServiciosLogisticos` y `LiderProceso` existen en
el modelo de permisos del código, pero **quedan fuera de esta
entrega** — no se asignan a ningún usuario real.

| Rol real (código) | Nombre de negocio | ¿Se entrega? |
|---|---|:---:|
| `Comercial` | Comercial | ✅ Sí |
| `AdminTarifas` | Administrador de Fletes y Tarifas* | ✅ Sí |
| `Admin` | Administrador general (superusuario) | ✅ Sí |
| `CoordinadorFletes` | Administrador de Fletes (operativo) | ❌ No |
| `ServiciosLogisticos` | Servicios Logísticos | ❌ No |
| `LiderProceso` | Líder de Proceso | ❌ No |

\* *Nombre de negocio a confirmar con Sara* — el código lo llama
"Administrador de Reglas/Tarifas", pero al no entregarse
`CoordinadorFletes`, este rol termina absorbiendo también el trabajo
operativo del día a día (Bandeja, Mesa de Costeo, entregar
cotizaciones) que en el modelo de 6 roles estaba pensado para un rol
aparte. Ver el porqué exacto en la sección 3.

**Consecuencia práctica importante:** en el modelo de 6 roles,
`AdminTarifas` y `CoordinadorFletes` compartían casi todas las mismas
capacidades operativas (Bandeja, Mesa de Costeo, agregar flete,
entregar cotización, aplicar propuestas) — la diferencia entre ambos
era menor de lo que sus nombres sugieren. Por eso, al no entregarse
`CoordinadorFletes`, `AdminTarifas` se queda como el único rol
"administrador/operativo" del día a día, además de sus funciones
propias de reglas/tarifas/cubicación. **Hay una excepción real que no
se resuelve solo por esta superposición** — ver el hallazgo marcado
abajo en la sección 3 (gestión del catálogo de Servicios en
Biblioteca).

**Nota de contexto (ya no aplica a esta entrega, se deja como
antecedente):** el documento original de entendimiento planteaba 3
roles distintos (Comercial, Administrador de Fletes, Administrador de
Reglas); durante la construcción el sistema terminó con 6. El propio
código de permisos trae una nota de los desarrolladores: para
`Comercial` y `AdminTarifas` la matriz de permisos se heredó del
comportamiento de una versión anterior (React); para los otros 4
roles, se basó directamente en las restricciones ya puestas en el
backend — es decir, no todo el mapa de "quién puede qué" tiene el
mismo nivel de certeza, aunque para los 3 roles que sí se entregan
(`Comercial`, `AdminTarifas`, `Admin`) la base es la más confiable (la
heredada de React, ya probada en producción).

**Otro matiz técnico, no menor:** el backend manda su propia lista de
"capacidades" por usuario al iniciar sesión, pero con un vocabulario
distinto al que usa el frontend — y el frontend **no la usa**, confía
solo en su propia tabla interna de permisos. Son dos fuentes de verdad
que hoy no se hablan entre sí (riesgo latente: si alguien ajusta
permisos del lado del backend, el frontend no se entera).

---

## 3. Matriz real: qué ve cada rol, módulo por módulo

Solo los 3 roles que se entregan. (Para referencia, `CoordinadorFletes`
tenía casi la misma columna que `AdminTarifas` — ver nota al pie de la
tabla — y `ServiciosLogisticos`/`LiderProceso` eran de solo lectura;
ninguno de los tres se pierde en funcionalidad real al no entregarse,
excepto el matiz marcado con ⚠️.)

| Módulo / capacidad | Comercial | AdminTarifas | Admin |
|---|:---:|:---:|:---:|
| Bandeja | ✅ | ✅ | ✅ |
| Captura (nueva solicitud) | ✅ | — | ✅ |
| Biblioteca (ver) | — | ✅ | ✅ |
| Biblioteca → gestionar Servicios | ✅ | ⚠️ **No** | ✅ |
| Biblioteca → ajustar vigencias | — | ✅ | ✅ |
| Tarifas Plaza | — | ✅ | ✅ |
| Reglas Solo-SICETAC | — | ✅ | ✅ |
| Mesa de Costeo / Cotización | — | ✅ | ✅ |
| Agregar flete manual | — | ✅ | ✅ |
| Aplicar propuestas en bloque | — | ✅ | ✅ |
| Aplicar propuestas **en silencio al capturar** | ✅ | — | — |
| Entregar cotización al comercial | — | ✅ | ✅ |
| Marcar cotización ganada/perdida | ✅ | — | ✅ |
| Cubicador 3D | — | ✅ | ✅ |
| Tablero (KPIs, SLA, SICE-TAC) | — | ✅ | ✅ |

Lectura de la tabla:

- **`AdminTarifas` termina siendo el rol operativo "todo terreno"**:
  además de Tarifas Plaza/Reglas Solo-SICETAC/Cubicador (lo suyo desde
  siempre), hace Bandeja, Mesa de Costeo, agrega fletes, aplica
  propuestas en bloque, entrega cotizaciones y ve el Tablero — todo lo
  que antes hacía `CoordinadorFletes` en el modelo de 6 roles, porque
  ambos roles ya compartían casi la misma lista de capacidades (la
  diferencia real entre ellos era menor de lo que sus nombres
  sugerían).
- **⚠️ Hallazgo real que SÍ cambia algo — gestión de "Servicios" en
  Biblioteca:** `CoordinadorFletes` tenía el permiso `gestionarServicios`
  (crear/editar servicios conexos en Biblioteca); `AdminTarifas`
  **nunca lo tuvo**, ni en el modelo de 6 roles ni en este. Con la
  entrega de 3 roles, **quien administra tarifas y reglas
  (`AdminTarifas`) no puede gestionar el catálogo de servicios
  conexos** — ese catálogo solo lo gestiona Comercial (y Admin).
  **Esto vale la pena validarlo con el cliente**: ¿de verdad quieren
  que sea Comercial quien mantenga el catálogo de servicios
  logísticos/adicionales, o se le debería dar ese permiso también a
  `AdminTarifas`?
- **Solo Comercial tiene "aplicar propuestas en silencio"** al capturar
  — ni siquiera Admin tiene este permiso específico (Admin, al ser
  superusuario en casi todo lo demás, probablemente no lo necesite por
  otras vías).
- Comercial sigue sin ver Biblioteca directamente (no tiene
  `verBiblioteca`) — su consulta de tarifas pasa por la Mesa de Costeo
  (dentro de su propia solicitud), no por la Biblioteca completa.

---

## 4. Las vistas, una por una

### 4.1 Bienvenida — la puerta de entrada

Pantalla simple tras iniciar sesión: muestra nombre y rol del usuario,
botón "Continuar". No hay contenido distinto por rol — lo único que
cambia es a dónde lo manda el botón: cada rol aterriza en su propia
pantalla principal según la primera capacidad que tenga de esta lista
de prioridad: Bandeja → Tablero → Biblioteca. Con los 3 roles que se
entregan, los 3 (Comercial, AdminTarifas, Admin) tienen `verBandeja`,
así que los 3 aterrizan en **Bandeja**.

### 4.2 Captura — alta de una solicitud nueva

**Quién:** solo Comercial (y Admin como superusuario).

Dos modos:

- **Solicitud puntual (spot):** formulario línea por línea. Por cada
  ruta: cliente, línea de negocio, origen/destino (ciudades reales,
  autocompletado), peso, clase de viaje, carrocería, configuración de
  vehículo (14 configuraciones estándar), clase de contenedor cuando
  aplica, valor de mercancía, horas de cargue/descargue (4h por
  defecto, 1h si hay retorno de contenedor), exclusivo/consolidado,
  retorno de contenedor + ciudad de devolución, levante en ciudad
  distinta al origen. Cada línea admite "posiciones" (bultos con
  dimensiones), a mano o importadas de una lista de empaque.
  **Recomendación automática de vehículo:** con las posiciones
  cargadas, un botón "Cubicar" sugiere el vehículo+carrocería más
  adecuado con una banda de compatibilidad (Compatible/Con
  advertencias/Incompatible/No evaluable) y % de utilización — un
  clic aplica la sugerencia.
- **Pliego masivo (Excel de licitación):** sube un Excel, el sistema
  detecta hojas/encabezados y ofrece mapeo columna→campo, o detecta
  automáticamente un "modo matriz" (una columna de ruta + una columna
  por cada tipo de vehículo con su capacidad) y genera una línea por
  cada combinación ruta×vehículo. El usuario revisa un preview antes
  de aplicar.

**Qué pasa después:** al enviar, el costeo automático arranca en
segundo plano (la pantalla sondea el estado hasta ~3 min), y redirige
a Mesa de Costeo. De ahí en adelante, la solicitud pasa a ser propiedad
de Bandeja/Mesa de Costeo — Captura es solo el punto de entrada.

### 4.3 Bandeja — la cola priorizada de trabajo

**Quién:** Comercial, AdminTarifas, Admin (cada uno ve/filtra sus
propias solicitudes o todas, según el rol — Comercial solo ve las
suyas).

Lista de solicitudes ordenada por **score de priorización**
(combinación de urgencia + valor + probabilidad), con filtros por
estado, vigencia (Fecha Límite) y "atención" (Pendientes / Atendidas
por Admin. de Fletes y Tarifas / Automático / Todas). Desde acá se
entra con un clic a la Mesa de Costeo de cada solicitud.

**2 bugs reales confirmados esta sesión, documentados y pendientes de
implementar** (ver `2026-10-09_HU-BACKEND.md`, sección Bandeja): el filtro
"vigente" compara la fecha de captura en vez de la Fecha Límite real;
y las solicitudes ya revisadas pero sin entregar todavía no aparecen
en "Pendientes" como deberían.

### 4.4 Mesa de Costeo — el corazón del costeo por línea

**Quién:** AdminTarifas, Admin (Comercial no entra aquí directamente —
su equivalente de "ver/ajustar" vive dentro del mismo componente pero
con botones distintos, gateado por si `esComercial`).

Por cada línea de la solicitud, compara 3 fuentes de tarifa (Piso
SICE-TAC + tiempos logísticos, Tarifario/plaza, Histórico SAP),
siempre mostrando la fuente — nunca un número sin explicar de dónde
sale (RN-05). El usuario puede usar la propuesta tal cual o ajustar
manualmente. Por línea se ven también: tiempos logísticos (cargue/
descargue, propios de la línea o heredados de la solicitud), línea de
negocio, clase de viaje, clase de contenedor. Indicador de avance
("X de Y líneas costeadas" / "Costeo terminado"), aviso visual cuando
ya está todo costeado pero falta entregar al comercial, y un resumen
colapsable de todas las líneas antes de confirmar. Desde aquí también
se puede "Cubicar" una línea con mercancía detallada (abre el
Cubicador) y se exporta/entrega la cotización final (Excel/PDF por
correo).

Esta pantalla fue objeto de una sesión completa de ajustes de UX este
mes — ver `2026-10-09_HU-FRONTEND.md`/`2026-10-09_HU-BACKEND.md`, sección Mesa de Costeo,
para el detalle exacto de cada cambio (consolidación de cajas de
propuesta, numeración de líneas, etc.) y un bug real encontrado y
documentado ("Iniciar costeo" aparece en solicitudes ya entregadas o
auto-costeadas, para roles sin el permiso de auto-aplicar).

### 4.5 Biblioteca — consulta y mantenimiento de tarifas y servicios

**Quién (ver):** AdminTarifas, Admin. **Comercial no ve esta pantalla**
(no tiene `verBiblioteca`).

4 pestañas:
- **SICE-TAC:** consulta en vivo del piso legal (DWSice), con
  paginación.
- **Tarifas de plaza:** consulta en vivo de `dbSolucionesTI` (SAP),
  con semáforo de vigencia (verde/ámbar/rojo) y posibilidad de ajustar
  la vigencia localmente en NEXO sin tocar el dato de origen. Solo
  AdminTarifas/Admin pueden ajustar vigencias.
- **Histórico SAP:** tarifas realmente cotizadas antes (< 3 meses,
  sin las ganadas — esas se van a "Tarifas Ganadas").
- **Servicios (logísticos y adicionales):** catálogo editable de
  servicios conexos (ej. Almacenamiento, ITR, Precinto), con alta
  individual o por Excel. **⚠️ Esta pestaña la gestiona Comercial (y
  Admin) — `AdminTarifas` no tiene permiso para gestionarla**, aunque
  gestione todo lo demás de Biblioteca. Ver el hallazgo en la sección 3
  — vale la pena confirmar con el cliente si esto es lo que quieren.

Varios hallazgos y mejoras de esta pestaña de Servicios (bug del
semáforo de vigencia, orden por vigencia, paginación de Histórico SAP,
vista previa editable de la carga masiva de Excel) están documentados
en `2026-10-09_HU-FRONTEND.md`/`2026-10-09_HU-BACKEND.md`, sección Biblioteca, y en curso de
implementación en este momento.

### 4.6 Tarifas Plaza — mantenimiento manual del dato de plaza

**Quién:** solo AdminTarifas y Admin.

Módulo **nuevo**, no estaba en el documento original de entendimiento
— es la "segunda barrida". CRUD individual o por Excel de tarifas de
plaza (línea de negocio + clase de viaje + origen + destino + rango de
peso/tipo de vehículo + valor + vigencia + detalles), con el mismo
semáforo de vigencia que Biblioteca. Tiene un bug real de lectura de
fechas en la carga masiva (documentado, pendiente de arreglar) y está
recibiendo la misma vista previa editable antes de confirmar que
Servicios.

### 4.7 Reglas Solo-SICETAC — excepciones de costeo

**Quién:** solo AdminTarifas y Admin.

Catálogo de combinaciones Origen + Destino + Tipo de vehículo donde la
empresa decidió **no comparar contra plaza**: cualquier solicitud que
haga match exacto se auto-costea solo con el piso SICE-TAC y queda
lista sin pasar por revisión manual. Sirve para estandarizar rutas
donde ya no se negocia, y acelera la atención de esas solicitudes.
Alta individual o por Excel; sin edición in-place (se borra y se
vuelve a crear); origen/destino obligados a coincidir con el catálogo
real de ciudades (no texto libre).

### 4.8 Cubicador — acomodo 3D de la carga

**Quién:** solo AdminTarifas y Admin — Comercial no tiene acceso.

Se entra desde Mesa de Costeo (botón "Cubicar" en una línea con
mercancía detallada) o de forma independiente. Trabaja sobre un "plan
de cargue": vehículo + lista de unidades de carga (dimensiones, peso,
restricciones como frágil/apilable/refrigerado) y, tras optimizar, el
resultado con la posición exacta de cada unidad. El usuario configura
la estrategia de optimización (volumen, peso, balance de ejes,
estabilidad, secuencia de entrega, o combinada) y el motor decide
automáticamente el acomodo — el usuario no decide la posición inicial,
solo ajusta después si hace falta (mover/rotar/bloquear/retirar una
unidad, siempre validado por el servidor). Si la carga no cabe en un
vehículo, genera automáticamente una flota de varios vehículos. Se
exporta a PNG, CSV, JSON o **PDF** (el documento formal del plan de
cargue, generado en el navegador).

### 4.9 Tablero — métricas y salud del piloto

**Quién:** AdminTarifas, Admin — Comercial no lo ve.

3 bloques sin interacción entre sí: (1) cumplimiento de SLA y estado
de la cola (pendientes/en costeo/entregadas/descartadas); (2) KPIs del
piloto (tiempo de resolución de un pliego, % de pliegos < 10 min, %
de líneas auto-costeadas vs. manuales, cotizaciones emitidas desde la
app) — los indicadores de éxito que definió el documento original; (3)
un iframe embebido con un dashboard externo de evolución de tarifas
SICE-TAC (no es dato propio de NEXO). El score de priorización de la
Bandeja **no** vive aquí.

### 4.10 Cotización-resumen — ficha de una cotización puntual

**Quién (ver):** AdminTarifas, Admin (Comercial no entra por esta
ruta). Marcar ganada/perdida: solo Comercial y Admin.

Vista de solo lectura de una cotización específica (fletes por línea
con su fuente, servicios logísticos/adicionales, total). **No hay
ningún enlace interno que lleve aquí hoy** — todo el flujo de trabajo
real (agregar flete, exportar, entregar, marcar resultado) ya se
resuelve dentro de Mesa de Costeo, que es adonde Bandeja navega. Esta
pantalla parece pensada para compartir un link directo a una
cotización puntual, no para ser un paso del flujo clic a clic actual.

---

## 5. Flujo end-to-end (confirmado, con los 3 roles que se entregan)

1. **Comercial** captura la solicitud (spot o pliego Excel) en
   **Captura**.
2. El costeo automático arranca solo (en segundo plano); si la ruta
   hace match con una **Regla Solo-SICETAC** (mantenida por
   `AdminTarifas`), se auto-costea sin pasar por revisión.
3. La solicitud entra a la **Bandeja**, priorizada por score — la ven
   Comercial (las suyas), `AdminTarifas` y Admin.
4. **`AdminTarifas`** entra a **Mesa de Costeo**: revisa/ajusta la
   propuesta por línea (Piso SICE-TAC+tiempos vs. Tarifario vs.
   Histórico, siempre con fuente visible — RN-05), usa
   **Biblioteca**/**Tarifas Plaza** como consulta de apoyo.
5. Si una línea trae mercancía detallada, puede pasar por el
   **Cubicador** (`AdminTarifas`/Admin) para el acomodo 3D y el plan
   de cargue en PDF.
6. Se agregan servicios conexos (catálogo gestionado por **Comercial**
   en Biblioteca → Servicios — ver el ⚠️ de la sección 3), se entrega
   la cotización (Excel/PDF por correo).
7. **Comercial** (o Admin) marca el resultado: ganada/perdida. Si es
   ganada, la tarifa se realimenta a la Biblioteca (Tarifas Ganadas) —
   RN-04.
8. **`AdminTarifas`** supervisa el **Tablero** (SLA, KPIs del piloto)
   sin operar la cola directamente.

Cada paso queda auditado (`AuditLog`).

---

## 6. Reglas de negocio (vigentes, confirmadas contra el código)

| ID | Regla |
|---|---|
| RN-01 | El flete propuesto es el MAYOR entre tarifa de plaza y piso SICE-TAC; sin plaza, gobierna SICE-TAC. |
| RN-02 | Nunca se propone bajo el piso SICE-TAC vigente; las tarifas vencidas se marcan y no se usan como piso válido. |
| RN-03 | Retorno de contenedor vacío: 1 h cargue + 1 h descargue (en vez de 4+4), costeado por ruta de devolución, suma aparte al total. |
| RN-04 | Una cotización marcada "ganada" crea una tarifa histórica reutilizable en Biblioteca. |
| RN-05 | Toda propuesta de tarifa muestra siempre su fuente (SICE-TAC/plaza/histórico). |

### 6.1 El motor de decisión de tarifa, paso a paso (detalle de RN-01)

> **⚠️ Esto es el flujo OBJETIVO, no lo que hace hoy el código.** La
> auditoría del 2026-10-12 comparó este flujo contra el código y no
> coinciden (hoy: Solo-SICETAC sin piso corta en "atención manual", no
> existe el paso "plaza vigente mayor gana sola", y la plaza/histórico
> solo se buscan en SAP, nunca en las tablas propias de NEXO). La
> especificación completa —tabla de 10 casos, qué cambia, casos borde
> y HU para implementarlo— está en `docs/backlog/2026-10-09_HU-BACKEND.md`, sección
> **"Motor de decisión de tarifa"** (HU-B14, B15, B16, B17) y la parte
> de pantalla en `docs/backlog/2026-10-09_HU-FRONTEND.md`, **HU-16**.

Basado en el diagrama `NEXO FLUJO.pdf`, con los ajustes que confirmó
Sara el 2026-10-12. Ante una línea de solicitud (línea de negocio,
origen, destino, tipo de vehículo, carrocería/contenedor, peso):

1. **¿Hay una Regla Solo-SICETAC para esa combinación?**
   - Sí, y hay piso SICE-TAC → se usa **únicamente** el piso. Fin — el
     usuario no elige.
   - Sí, pero no hay piso SICE-TAC → pasa a la consulta completa (paso 4).
2. **Si no hay regla: buscar la tarifa de plaza en 2 tablas** — primero
   la externa (SAP), y si ahí no hay una vigente, la propia de NEXO
   (`tbl_tarifas_plaza`, la que se gestiona en Biblioteca → Tarifas de
   plaza). Si no aparece en ninguna → consulta completa (paso 4).
   *(Igual que el PDF. Una versión anterior de este documento decía
   "si no existe plaza → solo SICE-TAC": era una mala lectura del
   diagrama, corregida el 2026-10-12.)*
3. **Si hay plaza vigente y es MAYOR que el piso SICE-TAC** → se usa
   **únicamente** la plaza. Fin — tampoco aquí elige el usuario. Si la
   plaza está vencida, o es menor o igual al piso → paso 4.
4. **Consulta completa:** SICE-TAC + plaza (vigente, o la vencida como
   referencia) + histórico de plaza de los últimos 3 meses (también en 2
   tablas: log de SAP y, si ahí no hay, `tbl_tarifas_plaza_historico`
   de NEXO). Se marca como **recomendado** el mayor (sin bajar nunca del
   piso). Después:
   - **En el auto-costeo** (al capturar o cargar un pliego) **no se
     guarda precio**: la línea queda pendiente.
   - **En Mesa de Costeo**, el Administrador de Tarifas ve los 3 y tiene
     la última palabra: el recomendado, otro candidato, un valor manual,
     o "Aplicar todas las propuestas" (con confirmación) para resolver
     todas las pendientes de una vez.
   - Nunca se puede guardar un valor por debajo del piso SICE-TAC.

En criollo: el sistema solo pone a elegir a una persona cuando de
verdad hay ambigüedad entre fuentes y la línea está en manos del
Administrador de Tarifas; en los casos claros (hay regla con piso, o la
plaza vigente ya es la mayor) resuelve solo y ni presenta opciones.

---

## 7. Qué cambió frente al documento original de entendimiento

- Roles: el código tiene 6, pero **se entregan 3** (`Comercial`,
  `AdminTarifas`, `Admin` — ver sección 2). El tercer rol del
  documento original ("Administrador de Fletes" operativo) no se
  entrega como rol aparte — su trabajo lo absorbe `AdminTarifas`.
- Tarifas Plaza y Reglas Solo-SICETAC son módulos nuevos, no estaban
  en el alcance original (parte de la "segunda barrida").
- El Cubicador y el Tablero quedan reservados a `AdminTarifas`/Admin,
  no a un rol de "Administrador de Fletes" operativo separado.
- **Pendiente de validar con el cliente:** que `AdminTarifas` no
  gestiona el catálogo de Servicios en Biblioteca (lo gestiona
  Comercial) — ver el ⚠️ de la sección 3.
- Alcance original: 11 de 11 ítems cumplidos (100%), más trabajo
  adicional entregado (ver `Nexo_Comparacion_Backend_vs_Entendimiento.md`,
  sección 12, para el detalle completo).

---

## 8. Fuentes

- `docs/documentacion/Entendimiento_Nexo (2).docx` — entendimiento
  original de negocio.
- `docs/documentacion/Nexo_Comparacion_Backend_vs_Entendimiento.md` —
  auditoría backend vs. entendimiento (8-oct-2026).
- `docs/documentacion/NEXO PENDIENTES.xlsx` — 16 tareas de la segunda
  barrida.
- `docs/documentacion/NEXO FLUJO.pdf` — diagrama de flujo del motor de
  decisión de tarifa (detalle de RN-01, sección 6.1).
- Revisión directa del código: `administracion_embarqa_nexo/src/app/cabina-fletes/`
  (routing, guards, `nexo-access.models.ts`, cada página) y
  `NEXO/src/Nexo.Api/` (`[Authorize(Roles=...)]` de cada controller).
- `docs/documentacion/REVISION-NEXO-PASO-A-PASO.md`, `docs/backlog/2026-10-09_HU-FRONTEND.md`,
  `docs/backlog/2026-10-09_HU-BACKEND.md` — hallazgos y mejoras de esta misma revisión,
  para el detalle técnico de cada bug/mejora mencionado arriba.
