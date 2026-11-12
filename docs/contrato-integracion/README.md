# Contrato de integración — NEXO ↔ Portal de la Administración

**Versión 1.7.0**

Este documento manda. El módulo `cabina-fletes` del portal está en desarrollo y no
es la referencia: lo que hoy tenga construido se adapta a lo que diga aquí, no al
revés. Cuando algo del código y algo de este documento se contradigan, gana el
documento y el código se corrige. Las contradicciones que ya se conocen están
señaladas en su sección como **discrepancia abierta**.

Cubre lo que el portal y NEXO tienen que acordar: identidad, roles, autorización
por operación, trabajos pesados, avisos en vivo y forma de los errores. El detalle
de cada endpoint (request, respuesta y errores) está en
[`CONTRATO.md`](../../CONTRATO.md), y el funcionamiento de punta a punta en
[`docs/funcionamiento/FUNCIONAMIENTO-NEXO.md`](../funcionamiento/FUNCIONAMIENTO-NEXO.md).

Cada decisión lleva su estado. **Acordada** salió de la reunión con el equipo de
desarrollo del portal. **Definida** la fijamos nosotros porque es nuestra
arquitectura y el portal solo la consume. **Resuelta** era un pendiente que ya se
cerró en los hechos. **Pendiente** espera a un tercero con nombre.

## Contenido

1. [Identidad](#1-identidad)
2. [Roles](#2-roles)
3. [Autorización por operación](#3-autorización-por-operación)
4. [Trabajos pesados](#4-trabajos-pesados)
5. [Avisos en vivo](#5-avisos-en-vivo)
6. [Recalibrar la priorización](#6-recalibrar-la-priorización)
7. [Entregar una cotización](#7-entregar-una-cotización)
8. [Cambiar los servicios conexos de una cotización entregada](#8-cambiar-los-servicios-conexos-de-una-cotización-entregada)
9. [Errores](#9-errores)
10. [Independencia del backend existente](#10-independencia-del-backend-existente)
11. [Versionado](#11-versionado)

---

## Estado de las decisiones

| # | Decisión | Estado | Dueño |
|---|---|---|---|
| D1 | NEXO no tiene login propio | Acordada | — |
| D2 | La identidad la aporta la sesión del portal | Acordada | — |
| D3 | El backend resuelve permisos contra la tabla de la administración | Acordada | — |
| D4 | Nuestro backend es independiente del backend existente del portal | Acordada | — |
| D5 | La identidad no viaja firmada; se acepta el riesgo | Acordada | ver la decisión D-12 |
| D6 | Encabezado de identidad y endpoint de sesión | Definida | Backend |
| D7 | Trabajos pesados con aceptado + identificador | Definida | Backend |
| D8 | Avisos por SignalR con consulta de respaldo | Definida | Backend |
| D9 | Forma única de error | Definida | Backend |
| P1 | Roles de NEXO en el portal | Resuelta: el portal tiene seis roles propios de NEXO (ids 48 a 53) | TI |
| P2 | Equivalencia de los roles del portal a los 6 de NEXO | Pendiente de confirmar; la equivalencia ya está cargada y en uso | Negocio |
| P3 | Precedencia cuando un usuario tiene varios roles | Pendiente de confirmar; el orden propuesto ya está en uso | Negocio |
| P4 | Token de sesión verificable | Pendiente | Equipo del portal, sin fecha |

---

## 1. Identidad

### Cómo llega

El módulo de NEXO solo es alcanzable después de que la persona ya inició sesión en
el portal. NEXO no pide credenciales, no emite tokens y no guarda sesión propia.

El portal deja los datos de la persona en el navegador. De ahí el frontend toma
**únicamente el identificador** y lo envía en cada petición:

```http
GET /api/sesion/actual HTTP/1.1
X-Portal-Usuario-Id: 10123
```

> ⚠️ **Discrepancia abierta:** el backend hoy lee el encabezado
> `X-Portal-clsUsuario-Id` (constante `HeaderIdUsuario` en
> `clsIdentidadPortalAuthenticationHandler`), no `X-Portal-Usuario-Id`. Mientras no
> se corrija el código, un cliente que siga este documento recibe
> `401 SESION_INVALIDA` en todos los endpoints, y solo funciona
> `X-Portal-clsUsuario-Id`. Estado y resto de discrepancias:
> [`CONTRATO.md` §7](../../CONTRATO.md#7-discrepancias-conocidas-entre-código-y-contrato).

### Qué NO se envía y por qué

El objeto que el portal deja en el navegador incluye la lista de permisos de la
persona. **Esa lista no viaja al backend y el backend no la aceptaría.**

Cualquier cosa que el navegador afirme sobre lo que puede hacer su dueño es una
afirmación del cliente, no un hecho. NEXO resuelve los permisos yendo a la tabla
de la administración con el identificador recibido. Enviar la lista solo serviría
para tentarnos a confiar en ella.

Consecuencia directa: **falsificar un rol editando el navegador no funciona.** Ese
es el escenario que la auditoría de QA dejó como criterio vigente.

Lo que sí sigue siendo posible es cambiar el identificador por el de otra persona
y recibir sus permisos. La identidad no viaja firmada. Está aceptado a conciencia y
está escrito en la [decisión D-12](../decisiones/DECISIONES-NEXO.md#d-12) con las condiciones que
obligan a revisarlo.

### Qué valida el backend

En **cada** petición, sin caché de sesión:

1. Que el encabezado llegue y sea un número entero.
2. Que el identificador exista en la tabla de usuarios de la administración.
3. Que alguno de los roles de esa persona en el portal se traduzca a uno de los 6
   roles de NEXO (sección 2). Si tiene varios, gana el de mayor precedencia.

Si falla el 1 o el 2, la respuesta es `401 SESION_INVALIDA`, siempre con el mismo
mensaje, para no revelar si el identificador existe. Si la base del portal no
responde, también es `401`: el backend cierra en vez de dejar pasar. Si falla el 3,
la respuesta es `403 SIN_PERMISO_CABINA_FLETES`.

**No hay rol por defecto** y no existe un modo permisivo. El esquema `X-Rol` del
prototipo ya fue retirado. En Producción, si la conexión a la tabla del portal no
está configurada, la API no arranca. En Desarrollo y QA, sin esa conexión, el rol se
resuelve contra un usuario real de la tabla local de NEXO: para probar otro rol se
edita ese registro, no hay encabezado que lo declare.

Con cada petición válida el backend crea o actualiza el usuario local de NEXO con
el nombre, el correo y el rol efectivo. Ese usuario es el autor que queda en la
auditoría.

> ⚠️ **Discrepancia abierta:** el paso 3 hoy solo se exige en los endpoints que
> declaran roles y en los que no declaran ningún `[Authorize]`. Los que declaran
> `[Authorize]` sin roles (las lecturas de la sección 3 marcadas como «cualquier
> persona autenticada», `GET /api/trabajos/{id}` y los hubs `/hubs/trabajos` y
> `/hubs/plan-cargue`) aplican la política por defecto, que solo pide identidad
> válida. Una persona que existe en el portal pero no tiene rol de NEXO puede
> llamarlos. Es un defecto del código.

### Endpoint de sesión

```http
GET /api/sesion/actual
X-Portal-Usuario-Id: 10123
```

```json
200 OK
{
  "id": 10123,
  "nombre": "Usuario de ejemplo",
  "rol": "AdminTarifas",
  "capacidades": ["verBiblioteca", "ajustarVigencia", "editarReglasSoloSicetac",
                  "gestionarCotizacion", "editarServiciosConexos"]
}
```

| Código | Significa | Qué debe hacer el frontend |
|---|---|---|
| `200` | Sesión válida | Entrar al módulo |
| `401` | Sin identidad utilizable: falta el encabezado o el identificador no existe en la tabla | Devolver a la persona al portal |
| `403` | Existe, pero ninguno de sus roles se traduce a un rol de NEXO | Mensaje y salida al portal |

Un `401` **nunca** significa "muestra una pantalla de acceso de NEXO", porque esa
pantalla no existe. Significa que la sesión del portal no sirve y la persona tiene
que volver allá.

El campo `id` es el identificador del portal, el mismo del encabezado. El campo
`nombre` sale de la tabla del portal (nombre completo, o el usuario si no lo hay).

El campo `capacidades` existe para que el frontend decida qué pinta, no para que
decida qué permite. La autorización se aplica en el backend en todos los casos, y
la pantalla es solo cortesía visual. Capacidades por rol:

| Rol | `capacidades` |
|---|---|
| `Comercial` | `capturarSolicitud`, `gestionarCotizacion`, `agregarConexos` |
| `AdminTarifas` | `verBiblioteca`, `ajustarVigencia`, `editarReglasSoloSicetac`, `gestionarCotizacion`, `editarServiciosConexos` |
| `AdminBiblioteca` | `verBiblioteca`, `ajustarVigencia`, `editarReglasSoloSicetac` |
| `ServiciosLogisticos` | `editarServiciosConexos` |
| `LiderProceso` | `verMetricas` |
| `Admin` | `*` (todas) |

---

## 2. Roles

NEXO trabaja con seis roles y solo seis:

```
AdminBiblioteca · LiderProceso · ServiciosLogisticos · Admin · Comercial · AdminTarifas
```

La administración tiene su propio catálogo, mucho más amplio y con otros nombres.
La traducción de uno a otro ocurre **en el backend de NEXO**, contra la tabla
`tbl_mapeo_rol_portal`, y no la puede tocar nadie desde el navegador.

### La equivalencia

El backend lee todos los roles que la persona tiene en el portal y traduce cada uno.
Primero busca por el id del rol en el portal (`IDROLES` de `tblSAPRoles`) y, si no lo
encuentra, por su nombre. La tabla trae nueve equivalencias, cargadas desde
`database/seeder/datos_base_nexo.sql`:

| Rol en el portal | Se reconoce por | Rol de NEXO |
|---|---|---|
| `TarifasComercial` | Nombre | `Comercial` |
| `ComercialNEXO` (id 48) | Id | `Comercial` |
| `TarifasAdministrador` | Nombre | `AdminTarifas` |
| `AdminTarifasNEXO` (id 50) | Id | `AdminTarifas` |
| `Administracion` | Nombre | `Admin` |
| `AdminNEXO` (id 53) | Id | `Admin` |
| `CoordinadorFletesNEXO` (id 49) | Id | `AdminBiblioteca` |
| `ServiciosLogisticosNEXO` (id 51) | Id | `ServiciosLogisticos` |
| `LiderProcesoNEXO` (id 52) | Id | `LiderProceso` |

El rol 49 del portal conserva el nombre `CoordinadorFletesNEXO`; en NEXO se llama
`AdminBiblioteca`.

Hoy se asignan a usuarios reales `Comercial`, `AdminTarifas`, `Admin` y
`AdminBiblioteca`. `ServiciosLogisticos` y `LiderProceso` existen, con sus permisos
y su equivalencia, pero no se asignan a nadie.

### Pendiente P2 — confirmar la equivalencia

La equivalencia de arriba es la que está en uso. Falta que negocio la confirme,
en especial el alcance de `Administracion` → `Admin`, y que decida si
`ServiciosLogisticos` y `LiderProceso` se van a asignar.

### Pendiente P3 — varios roles a la vez

Una persona puede tener varios roles en el portal, y ya hay casos reales con dos
roles de tarifas simultáneos. NEXO usa un solo rol efectivo por usuario, así que
desempata con este orden, del que más manda al que menos
(`clsResolucionRolPortal`):

```
Admin > AdminTarifas > AdminBiblioteca > Comercial > LiderProceso > ServiciosLogisticos
```

Sale de qué tanto escribe cada rol en los controladores actuales. Es una inferencia
técnica, no una decisión de negocio, y por eso queda pendiente de confirmar.

---

## 3. Autorización por operación

Esto no es propuesta: es lo que el backend exige hoy, leído de los atributos
`[Authorize]` de `src/Nexo.Api/**/cls*Controller.cs` y de los hubs. Cuando un
controlador declara roles y su método también, se exigen los dos; en la tabla
aparece el resultado. Es la referencia contra la que el frontend decide qué mostrar.

| Operación | Roles admitidos |
|---|---|
| **Captura:** `POST /api/captura-solicitudes` · `analizar-excel` · `leer-estructura` · `recetas-lectura` (`PUT`, `GET`, `DELETE`) · `plantillas-mapeo` (`PUT`, `GET`, `DELETE`) · `sugerir-mapeo` · `aplicar-mapeo` · `normalizar-valores` · `GET aprovisionamiento` · `GET clientes` | `Comercial`, `Admin` |
| `GET /api/captura-solicitudes/departamentos` · `ciudades` · `municipios` | Cualquier persona autenticada |
| **Bandeja y Mesa:** `GET /api/solicitudes` · `GET /api/solicitudes/{id}` · `GET /api/solicitudes/{id}/propuesta-tarifa` | `Comercial`, `AdminTarifas`, `ServiciosLogisticos`, `LiderProceso`, `Admin`. `AdminBiblioteca` no entra. En la bandeja, `Comercial` solo ve las solicitudes que creó |
| `POST /api/solicitudes/resolver-catalogos` | `Comercial`, `Admin` |
| `POST /api/solicitudes/recomendar-vehiculo-contenedor` | `Comercial`, `AdminTarifas`, `Admin` |
| `POST /api/solicitudes/{id}/rechazar` | `Comercial`, `Admin` |
| `POST /api/solicitudes` · `POST /api/solicitudes/{id}/lineas:bulk` · `POST /api/solicitudes/{id}/recalcular-score` | `Admin` |
| **Cotizaciones, lecturas:** `GET /api/cotizaciones/{id}` · `GET .../por-solicitud/{solicitudId}` · `GET .../{id}/exportar-excel` | `Comercial`, `AdminTarifas`, `ServiciosLogisticos`, `LiderProceso`, `Admin`. `AdminBiblioteca` no entra |
| `POST /api/cotizaciones` · `POST .../{id}/aplicar-propuestas` | `Comercial`, `AdminTarifas`, `Admin` |
| `POST .../{id}/lineas` · `PUT .../lineas/{lineaSolicitudId}` · `POST` y `DELETE .../lineas/{lineaSolicitudId}/rechazo` · `PUT .../lineas/{lineaSolicitudId}/vehiculo` · `POST .../{id}/entregar` | `AdminTarifas`, `Admin` |
| `POST .../{id}/conexos` · `DELETE .../{id}/conexos/{conexoId}` | `Comercial`, `AdminTarifas`, `Admin` |
| `PATCH .../{id}/conexos/{conexoId}` | `Comercial`, `Admin` |
| `POST .../{id}/resultado` | `Comercial`, `Admin` |
| **Cubicación:** `POST /api/cubicacion` · `GET /api/cubicacion` · `GET /api/cubicacion/explicacion` | `Comercial`, `AdminBiblioteca`, `AdminTarifas`, `Admin` |
| Escrituras de `/api/planes-cargue` (crear, `demo`, `desde-linea`, `optimizar`, ítems, `items/importar`, borrar ítem, borrar plan, ajustar colocación) · `POST /api/vehiculos` | `Comercial`, `AdminTarifas`, `Admin` |
| `DELETE /api/vehiculos/{id}` | `Admin` |
| Escrituras de los catálogos del Cubicador: `/api/vehiculos-catalogo`, `/api/tipos-carroceria`, `/api/tipos-mercancia`, `/api/categorias-mercancia`, `/api/reglas-incompatibilidad`, `/api/parametros-motor`, incluido `republicar-catalogo` | `AdminTarifas`, `Admin` |
| **Normalización:** `POST /api/catalogos/alias` · `GET /api/catalogos/vehiculos-cuarentena` · `POST .../vehiculos-cuarentena/{id}/mapear` · `POST .../vehiculos-cuarentena/{id}/descartar` | `AdminTarifas`, `Admin` |
| `POST`, `PUT` y `DELETE /api/diccionario-cliente` | `Comercial`, `AdminBiblioteca`, `AdminTarifas`, `Admin` |
| **Biblioteca:** `/api/tarifas-plaza` · `/api/pesos` · `/api/reglas-solo-sicetac` (todo, incluidas las lecturas y exportaciones) · vigencia de plaza SAP (`PATCH` y `DELETE /api/biblioteca-sap-plaza/{tarifaId}/vigencia`, `POST /api/biblioteca-sap-plaza/vigencia/masivo`) | `AdminTarifas`, `AdminBiblioteca`, `Admin` |
| `POST` y `PATCH /api/servicios-conexos` · `POST .../importar-excel` | `ServiciosLogisticos`, `AdminTarifas`, `Admin` |
| **Métricas:** `GET /api/metricas/cumplimiento` · `GET /api/metricas/kpis-piloto` | `LiderProceso`, `AdminTarifas`, `Admin` |
| **Asistente:** `/api/asistente/conversaciones` · `/api/asistente/acciones/{id}/confirmar` y `/cancelar` · `/api/asistente/preferencias` | `AdminTarifas`, `Admin` |
| `/api/asistente/herramientas` | Cualquier persona autenticada; cada herramienta exige los roles de la consulta que envuelve y responde `403 HERRAMIENTA_NO_PERMITIDA` si no los tiene |
| `GET /api/ia/uso/gasto` | `Admin` |
| **Priorización:** `GET` y `PUT /api/priorizacion/parametros` | `Admin` |
| `GET /api/sesion/actual` | Cualquiera de los seis roles |
| `GET /api/trabajos/{id}` · hubs `/hubs/trabajos` y `/hubs/plan-cargue` | Cualquier persona autenticada |
| Hub `/hubs/bandeja` | `Comercial`, `AdminTarifas`, `ServiciosLogisticos`, `LiderProceso`, `Admin`. `AdminBiblioteca` no entra |
| **Lecturas abiertas:** bibliotecas SICE-TAC, plaza SAP e histórico SAP · `/api/tarifas-historicas` · `GET /api/catalogos/*` (incluidos `alias` y `alias/resolver`) · `GET /api/servicios-conexos`, `exportar-excel` y `plantilla-excel` · `GET /api/diccionario-cliente` y `opciones` · `GET` de los catálogos del Cubicador · `GET /api/vehiculos` · lecturas de `/api/planes-cargue` | Cualquier persona autenticada |

«Cualquier persona autenticada» quiere decir que el endpoint solo declara
`[Authorize]`. La intención es que lo use cualquiera de los seis roles, pero hoy
basta con una identidad válida del portal: ver la discrepancia abierta de la
sección 1.

Dos endpoints quedan fuera de la integración con el portal: `GET /health`, que es
anónimo, y `GET /api/lineas-solicitud/{id}/cubicacion`, que solo consume el
Cubicador con su llave de servicio.

Cuando esta tabla y el código no coincidan, es un defecto y se corrige el código.

---

## 4. Trabajos pesados

Costear un pliego de trescientas líneas no cabe en una petición HTTP. Una operación
pesada no responde con su resultado: responde que lo aceptó.

### El patrón

```http
POST /api/solicitudes/{id}/lineas:bulk
```

```json
202 Accepted
{
  "trabajoId": "3f2b...",
  "estado": "Pendiente",
  "consultarEn": "/api/trabajos/3f2b..."
}
```

La petición vuelve de inmediato con un identificador. Las líneas quedan guardadas y
el costeo corre detrás, en la cola. El trabajo se registra en la misma transacción
que las líneas, así que el identificador se puede consultar desde el momento en que
el cliente recibe el `202`.

Hoy el patrón completo (`202`, trabajo consultable y avisos de avance) lo usa la
carga masiva de líneas de un pliego. Dos operaciones más trabajan en segundo plano
con su propia forma:

- **Captura con más de 10 líneas** (`POST /api/captura-solicitudes`): responde `201`
  con `costeoEnProceso: true` y costea en segundo plano, sin trabajo consultable. El
  avance se ve al volver a consultar la solicitud.
- **Planes de cargue** (crear, optimizar, ajustar colocaciones): responden `202` y
  el desenlace llega por `/hubs/plan-cargue` (sección 5.3) o con
  `GET /api/planes-cargue/{id}`.

### Estados

```
Pendiente → Procesando → Completado   el servicio resolvió todo por su cuenta
                       → Parcial      quedó alguna línea pendiente, con su motivo
```

`Pendiente` es el estado con el que responde el `202`: el trabajo existe pero todavía
no arrancó. `Parcial` no es un error y va a ser el desenlace más común. El contrato
reserva `Fallido` para cuando se rompe el servicio mismo, no para cuando simplemente
no había tarifa; hoy ningún camino del código lleva a ese estado.

### Hay dos tipos de pendiente, y no se manejan igual

Esto es lo que más importa entender del avance de un trabajo.

**Pendiente técnica (`Tecnica`).** Una fuente falló al costear la línea. El costeo se
reintenta solo y, si agota los reintentos, la línea queda pendiente con el error real
como motivo, para saber qué fuente falló. Entra en la ventana automática del trabajo.

**Requiere tarifa manual (`Manual`).** No hay tarifa válida para esa línea. El
servicio hizo su trabajo y la respuesta es que aquí hace falta el Administrador de
Tarifas. **No se reintenta y no tiene reloj**: nada va a cambiar hasta que esa
persona actúe, y puede tener otros pliegos delante. Poner un timeout sobre trabajo
humano solo sirve para marcar como fallido algo que estaba esperando bien.

Por eso el avance las devuelve por separado. Con un contador único habría que
mentirle al usuario en la mitad de los casos: "estamos reintentando" y "está en la
cola del Administrador de Tarifas" son mensajes distintos, y el segundo le dice a
quién ir a buscar.

### El timeout mide máquina, no personas

La ventana automática del trabajo es de 30 minutos y cubre lo que el servicio puede
hacer solo, cuya duración es predecible. Si vence con líneas sin desenlace, el
trabajo cierra en `Parcial` con lo que hay, y las que faltan conservan su motivo.

### Consulta de estado

```http
GET /api/trabajos/{trabajoId}
```

```json
200 OK
{
  "trabajoId": "3f2b...",
  "estado": "Procesando",
  "avance": {
    "total": 300,
    "resueltas": 187,
    "pendientes": 3,
    "pendientesTecnicas": 1,
    "requierenTarifaManual": 2
  },
  "solicitadoEn": "2026-08-25T12:00:00Z",
  "terminadoEn": null,
  "pendientes": [
    {
      "lineaId": "7c1e...",
      "tipo": "Manual",
      "motivo": "No hay tarifa de plaza vigente para la ruta.",
      "pendienteDesde": "2026-08-25T12:00:04Z"
    },
    {
      "lineaId": "9a40...",
      "tipo": "Tecnica",
      "motivo": "Microsoft.Data.SqlClient.SqlException: Execution Timeout Expired.",
      "pendienteDesde": "2026-08-25T12:00:09Z"
    }
  ],
  "secuencia": 191
}
```

La lista `pendientes` es lo que hace útil el aviso. El contador dice cuántas; la
lista dice **cuáles, de qué tipo y qué les falta**. Con eso el comercial sabe a quién
ir a buscar y el Administrador de Tarifas sabe qué cargar. Trae solo las líneas que
siguen sin resolver, en el orden en que quedaron pendientes, y va vacía cuando no
hay ninguna.

`avance.pendientes` es la suma de `pendientesTecnicas` y `requierenTarifaManual`,
para quien solo quiera un número.

`total` es nulo hasta que el pliego se reparte: antes de eso nadie sabe cuántas
líneas hay.

`secuencia` es un contador que solo crece dentro del trabajo. Guárdalo al consultar:
sirve para descartar los avisos rezagados que lleguen después. Está explicado en la
sección 5.1.

Un identificador que no existe responde `404 TRABAJO_NO_ENCONTRADO`.

---

## 5. Avisos en vivo

NEXO tiene tres canales de SignalR, separados porque cada uno avisa una cosa distinta:

| Hub | Para qué | Grupo | Evento |
|---|---|---|---|
| `/hubs/trabajos` | Avance de un trabajo pesado | Uno por trabajo | `avance` |
| `/hubs/bandeja` | La Bandeja Priorizada cambió | Uno solo | `bandeja` |
| `/hubs/plan-cargue` | Desenlace de un plan de cargue | Uno por plan | `avance` |

Los tres piden la misma identidad que el API, en el mismo encabezado. Quién puede
conectarse a cada uno está en la sección 3.

### 5.1 Avance de un trabajo

```
Hub:         /hubs/trabajos
Suscribir:   Suscribir(trabajoId)
Desuscribir: Desuscribir(trabajoId)
Evento:      "avance"
```

Cada trabajo es un grupo, así que solo llegan los avisos de los trabajos a los que
el cliente se suscribió. No hay difusión general.

#### Qué trae el aviso, y qué no

```json
{
  "trabajoId": "3f2b...",
  "estado": "Procesando",
  "avance": { "total": 300, "resueltas": 187, "pendientes": 3,
              "pendientesTecnicas": 1, "requierenTarifaManual": 2 },
  "terminadoEn": null,
  "secuencia": 191
}
```

Es el mismo estado que devuelve la consulta **sin `solicitadoEn` y sin la lista de
pendientes**, y se construye desde ella para que los campos compartidos no puedan
divergir.

La lista no viaja por una razón medida: un pliego de 300 líneas con 50 pendientes
pesa entre 12 y 25 KB por aviso, y se avisa unas 300 veces. Serían varios megabytes
por pliego y por cliente conectado. Solo con los contadores el aviso pesa unos 200
bytes.

**El reparto es ese: el aviso mueve la barra de progreso, la consulta trae el
detalle.** Quien necesite ver qué líneas están pendientes y por qué, llama a
`GET /api/trabajos/{id}` al terminar o cuando el usuario abra ese panel.

#### Los avisos pueden llegar desordenados: descarta por `secuencia`

Un trabajo grande genera cientos de avisos y varios se procesan a la vez, así que
pueden llegar al navegador en orden distinto al que se generaron. Sin cuidado, un
aviso viejo entregado tarde deja la barra de progreso retrocedida hasta el siguiente.

`secuencia` solo crece dentro de un trabajo. La regla del cliente es una línea:

```js
if (aviso.secuencia <= ultimaSecuenciaPintada) return;   // rezagado, se ignora
ultimaSecuenciaPintada = aviso.secuencia;
pintar(aviso);
```

Al reconectar, `GET /api/trabajos/{id}` devuelve su propia `secuencia`: arranca el
contador con ese valor y sigue con la misma regla.

Los números pueden saltar (191, 194, 195) y eso es normal: no todos los cambios
alcanzan a llegar. Lo único que se promete es que nunca retroceden.

### 5.2 Bandeja Priorizada

```
Hub:         /hubs/bandeja
Suscribir:   Suscribir()
Desuscribir: Desuscribir()
Evento:      "bandeja"
```

Hub aparte del de trabajos, y sin parámetro: hay un solo grupo y todos los
conectados reciben el mismo aviso.

#### El aviso NO trae la bandeja

```json
{ "motivo": "solicitud-nueva", "en": "2026-08-26T15:04:11Z" }
```

Y no es por peso: **no existe "la bandeja"**. La consulta admite seis filtros
(estado, atención, desde, hasta, vigencia y resultado), el `Comercial` solo ve las
solicitudes que creó, y cada cliente mira una porción distinta. El servidor no sabe
qué ve cada uno, así que empujar filas obligaría a mandarlas todas y filtrar en el
navegador.

**La regla del cliente es: al recibir el aviso, vuelve a consultar
`GET /api/solicitudes` con TUS filtros.** La pantalla se actualiza sola; lo que no se
hace es duplicar la consulta dentro del canal.

Tampoco lleva secuencia, al contrario que el aviso de avance de un trabajo. Allá el
aviso pinta un valor y uno rezagado haría retroceder la barra. Aquí solo dice
*reconsulta*, y eso es idempotente: dos avisos duplicados o desordenados producen dos
consultas y el mismo resultado.

#### Los cinco motivos

| `motivo` | Cuándo |
|---|---|
| `solicitud-nueva` | Entró una solicitud (por la captura o por `POST /api/solicitudes`), ya priorizada |
| `solicitud-rechazada` | Se descartó una: sale de la cola de trabajo |
| `trabajo-terminado` | Cerró el auto-costeo de un pliego, por desenlace de todas sus líneas o por vencimiento de la ventana |
| `parametros-recalibrados` | El Admin cambió los parámetros del puntaje: reordena de verdad |
| `refresco-periodico` | Latido del recálculo. Ver abajo |

**Un pliego de 300 líneas produce UN aviso de bandeja, no 300.** El aviso sale cuando
el trabajo cierra, no por línea. La barra de progreso necesita el detalle (eso es el
hub de trabajos), pero la bandeja solo necesita saber que reconsulte, y una vez basta.

#### Por qué existe el latido periódico

Es el motivo menos obvio y conviene entenderlo antes de "optimizarlo".

El orden de la bandeja depende del *ahora*: la urgencia sube sola a medida que se
acerca cada deadline. Eso significa que **el orden cambia continuamente sin que nadie
toque nada**, y ese cambio no tiene ningún evento que lo represente.

El latido lo cubre. Sale con cada recálculo (por defecto cada 15 minutos, configurable
con `Score:IntervaloRecalculoMinutos`) aunque no se haya actualizado ningún puntaje
persistido. Si alguien lo cambia para avisar solo cuando algo cambió, la bandeja
abierta se queda con el orden de hace horas.

### 5.3 Planes de cargue

```
Hub:         /hubs/plan-cargue
Suscribir:   Suscribir(planCargueId)
Desuscribir: Desuscribir(planCargueId)
Evento:      "avance"
```

Va aparte de `/hubs/trabajos` porque un plan de cargue no tiene líneas pendientes ni
desenlaces parciales: el Cubicador lo genera o lo rechaza. Cada plan es un grupo.

```json
{ "planCargueId": "5d1a...", "estado": "optimizado", "puntaje": 87.5, "motivo": null }
```

El aviso sale cuando el Cubicador devuelve el plan generado o cuando lo rechaza. En el
rechazo, `estado` es `error` y `motivo` trae el mensaje del Cubicador. No trae las
colocaciones, que pueden ser cientos: el aviso dice que hay desenlace y
`GET /api/planes-cargue/{id}` trae el detalle.

Los estados del plan son `borrador`, `procesando`, `optimizado`, `ajustado` y `error`.
Al crear u optimizar, la respuesta `202` trae `planCargueId`, `estado`
(`procesando`) y `consultarEn`.

### 5.4 El aviso es una mejora, no una garantía

Vale para los tres canales. La conexión se cae, el navegador se duerme, la persona
cambia de pestaña. La verdad siempre está en la consulta (`GET /api/trabajos/{id}`,
`GET /api/solicitudes`, `GET /api/planes-cargue/{id}`), y el frontend tiene que poder
reconstruir el estado completo con esa consulta sola, sin haber recibido un solo
aviso.

**SignalR NO reenvía lo que te perdiste mientras estabas desconectado.** Al reconectar
hay que consultar el estado; el canal no tiene memoria. Prometer otra cosa sería
mentir.

Quien construya la pantalla asumiendo que los avisos llegan siempre va a tener un
error que solo aparece con mala red y que es muy difícil de reproducir.

---

## 6. Recalibrar la priorización

```http
GET  /api/priorizacion/parametros
PUT  /api/priorizacion/parametros
```

Solo `Admin`. Cambiar estos parámetros reordena la bandeja de TODO el mundo: le cambia
el trabajo a los coordinadores sin que ellos hayan tocado nada. No es una preferencia
de pantalla, es política de operación.

```json
{
  "pesoUrgencia": 0.40,
  "pesoValor": 0.30,
  "pesoProbabilidad": 0.20,
  "pesoTipo": 0.10,
  "horizonteHoras": 240,
  "valorReferencia": 100000000,
  "puntajeLicitacion": 100,
  "puntajeSpot": 50,
  "valorPorDefectoSinValor": 30,
  "probabilidadPorDefecto": 50
}
```

### Todos los parámetros mueven el puntaje

El puntaje total es la suma ponderada de cuatro componentes: urgencia, valor,
probabilidad y tipo (ver `docs/decisiones/DECISIONES-NEXO.md`, D-04). Mover cualquier
peso cambia el puntaje de toda solicitud cuyos componentes no sean todos iguales.

| Parámetro | Qué mueve |
|---|---|
| `horizonteHoras` | La rampa del componente urgencia: vencido = 100, fuera del horizonte = 0 |
| `pesoUrgencia`, `pesoValor`, `pesoProbabilidad`, `pesoTipo` | Ponderan directamente el total |
| `valorReferencia`, `puntajeLicitacion`, `puntajeSpot` y los dos valores por defecto | Su componente (valor, tipo o probabilidad), y por lo tanto el total según su peso |

Ejemplo con los pesos por defecto (`pesoUrgencia=0.40, pesoValor=0.30,
pesoProbabilidad=0.20, pesoTipo=0.10`): una licitación sin valor ni probabilidad
estimados, a 720 horas de su deadline con `horizonteHoras=4000`, da urgencia = 82,
valor = 30 (por defecto), probabilidad = 50 (por defecto) y tipo = 100. El total es
0.40·82 + 0.30·30 + 0.20·50 + 0.10·100 = **61.8**, no 82.

**Los cuatro pesos deben sumar 1.0.** Si no, la respuesta es
`400 PARAMETROS_SCORE_INVALIDOS` y no se guarda nada; de lo contrario el total dejaría
de estar en escala 0 a 100.

**El `PUT` responde de inmediato y no espera el recálculo.** Guardar los parámetros y
refrescar el puntaje de todas las solicitudes activas son dos cosas: la segunda corre
aparte, con sus reintentos, y además dispara el aviso `parametros-recalibrados` a la
bandeja. Así recalibrar no deja al Admin esperando, y un fallo del refresco no le
devuelve un error por un cambio que sí se guardó.

Ese refresco es del valor PERSISTIDO, el que leen métricas y tableros. **La bandeja no
lo necesita**: recalcula el puntaje al leerlo, porque la urgencia depende del ahora y
una licitación tiene que subir sola a medida que se acerca su deadline. Por eso el
orden que se ve en pantalla ya está al día antes de recalibrar nada.

---

## 7. Entregar una cotización

```http
POST /api/cotizaciones/{id}/entregar
```

La entrega no espera al correo: el correo se manda en segundo plano, así que al
responder todavía no ha salido nada. Por eso la respuesta no dice si se envió ni a
quién:

```json
{
  "cotizacion": { },
  "estadoNotificacion": "encolada",
  "mensaje": "Cotización entregada. La notificación por correo sale en segundo plano."
}
```

**Qué se puede afirmar:** que la notificación quedó encolada. Eso está garantizado,
porque el evento se publica en la misma transacción que la entrega: si la entrega
quedó firme, la notificación está pendiente de salir. `estadoNotificacion` hoy siempre
vale `encolada`.

**Qué no:** si salió ni a quién. El destinatario se resuelve en el consumidor que
manda el correo, fuera de la petición. Medido: la entrega responde en **~100 ms** con
la salida de correo caída.

### La notificación es interna

Va al correo de quien creó la solicitud, con el PDF adjunto si la entrega lo trae.
**Nunca al cliente.** El backend toma el destinatario del usuario creador de la
solicitud, y una prueba automatizada comprueba que ninguna dirección de cliente puede
ser destinataria. Si la solicitud no tiene creador identificable o la salida de correo
no está configurada, no se envía nada y no se reintenta.

### Si el correo falla

No afecta la entrega, que ya quedó firme. El fallo se reintenta de forma escalonada y,
si se agotan los intentos, el mensaje queda en la cola de error, de donde sale la
alerta al equipo.

Con una distinción que importa: lo que **no tiene arreglo** (un adjunto que pesa más
de lo permitido, un correo que el relay rechaza por su forma) no se reintenta. Va
derecho a la cola de error, así que la alerta llega al primer intento en vez de
noventa segundos después.

---

## 8. Cambiar los servicios conexos de una cotización entregada

```http
POST   /api/cotizaciones/{id}/conexos
DELETE /api/cotizaciones/{id}/conexos/{conexoId}
PATCH  /api/cotizaciones/{id}/conexos/{conexoId}
```

Un servicio omitido se descubre justo **después** de entregar, así que agregar y
quitar conexos no exige `Borrador`: se admite mientras la cotización no tenga
resultado (`resultado == "SinDefinir"`), es decir, `Borrador` **o** `Entregada`. Con
`Ganada` o `Perdida` responde `409 COTIZACION_CERRADA` y no cambia nada, porque el
resultado ya realimentó la Biblioteca.

Corregir la cantidad o el valor de un conexo ya agregado (`PATCH`) sigue siendo solo
en `Borrador`; sobre una cotización entregada responde `409 COTIZACION_NO_EDITABLE`.

**Qué cambia en la respuesta (`CotizacionDto`):** el campo `modificadaTrasEntregaAt`
(`null` si nunca se tocó tras la entrega). Se llena cuando el cambio se hace sobre una
cotización `Entregada`, y **no** mueve `estado` ni `entregadaAt`: el SLA y las
métricas siguen midiendo la entrega original.

**El aviso al comercial.** Si la cotización estaba `Entregada` y quien hace el cambio
**no** es quien creó la solicitud, esa persona recibe un correo «Cotización
actualizada» con el servicio que se agregó o se quitó, la cantidad y el nuevo total.
No se envía nada si:

- la cotización es `Borrador` (todavía no hay nada entregado que corregir), o
- quien cambia es el mismo creador de la solicitud (no se le avisa lo que acaba de
  hacer).

Mismo patrón que la entrega (sección 7): el evento `clsConexosCotizacionCambiados`
se publica por el Outbox, en la **misma transacción** que el cambio, y el correo sale
en segundo plano desde un consumidor. El front no espera al correo y la respuesta no
dice si salió. Es interno (nunca al cliente), y un fallo de la salida de correo sube
al bus: se reintenta y, si no tiene arreglo, va a la cola de error. No se pierde en
silencio.

Una petición produce un aviso. «Agregar a todas las líneas» ya llega como **una**
petición con varios ítems y produce **un** correo. Agrupar peticiones sucesivas en un
solo correo queda por decidir.

---

## 9. Errores

Toda respuesta de error del API tiene la misma forma, la produzca quien la produzca:

```json
{
  "code": "SOLICITUD_INVALIDA",
  "message": "La solicitud indicada no existe.",
  "timestamp": "2026-08-24T19:40:00Z",
  "details": null
}
```

| Código HTTP | Cuándo |
|---|---|
| `400` | Validación de negocio |
| `401` | Sin identidad válida del portal (`SESION_INVALIDA`) |
| `403` | Identidad válida sin rol de NEXO, o con rol insuficiente para la operación (`SIN_PERMISO_CABINA_FLETES`) |
| `404` | El recurso no existe |
| `409` | Conflicto, incluida la edición simultánea (`CONFLICTO_CONCURRENCIA`) |
| `429` | Límite de peticiones en listados y consultas con tope (`DEMASIADAS_SOLICITUDES`, con `Retry-After: 30`), o tope de uso configurado, como el diario del asistente |
| `503` | Fuente externa no disponible, con motivo accionable |
| `500` | Error no controlado (`INTERNAL_ERROR`) |

El `code` es estable y pensado para que el frontend ramifique con él. El `message`
está en español y es para mostrar. Nadie debe leer el `message` para decidir.

---

## 10. Independencia del backend existente

NEXO vive detrás de su propia URL base y no comparte nada con el backend actual del
portal: ni sesión, ni base de datos, ni despliegue.

El único punto de contacto es la lectura de las tablas de usuarios y roles de la
administración, y es de **solo lectura**. NEXO nunca escribe ahí; el usuario que
sincroniza vive en su propia base.

Esa lectura sigue el mismo patrón que las otras tres fuentes externas que el backend
ya consume (DbSolucionesTi, DWSice y DWCubos): una interfaz en la capa de aplicación,
un adaptador en infraestructura y la cadena de conexión por configuración
(`ConnectionStrings:IdentidadPortal`). A diferencia de esas tres, no se degrada: si no
está configurada, el arranque falla en Producción en vez de pasar a un modo
permisivo.

El costo de leer una tabla ajena hay que decirlo: quedamos atados a un esquema que no
controlamos, y un cambio de columna del otro lado nos rompe sin aviso. Se mitiga
leyendo lo mínimo y aislándolo detrás de la interfaz, pero no desaparece.

---

## 11. Versionado

Este contrato se versiona con el código, en este repositorio. Cambiarlo es un PR que
revisan backend y frontend. `openapi.yaml` lleva la misma versión que este documento.

Un cambio que rompa a quien ya consume sube la versión mayor y se anuncia antes de
implementarse. Agregar campos opcionales o contenido nuevo sube la menor. Corregir
redacción sube la de parche.

---

## Archivos

| Archivo | Qué contiene |
|---|---|
| `README.md` | Este documento |
| `openapi.yaml` | La superficie del contrato, ejecutable |
| `escenarios.feature` | Los escenarios que implementan el contrato |
