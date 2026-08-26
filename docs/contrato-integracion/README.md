# Contrato de integración — Nexo (Cabina de Fletes) ↔ Portal de la Administración

**Versión 1.3.0** · Sprint 2 · Issues #7, #15, #16 y #17

Este documento manda. El módulo `cabina-fletes` del portal está en desarrollo y no
es la referencia: lo que hoy tenga construido se adapta a lo que diga aquí, no al
revés. Cuando algo del código y algo de este documento se contradigan, gana el
documento y el código se corrige.

Cada decisión lleva su estado. **Acordada** salió de la sesión con el equipo de
desarrollo del portal. **Definida** la fijamos nosotros porque es nuestra
arquitectura y el portal solo la consume. **Pendiente** espera a un tercero con
nombre.

---

## Estado de las decisiones

| # | Decisión | Estado | Dueño |
|---|---|---|---|
| D1 | Nexo no tiene login propio | Acordada | — |
| D2 | La identidad la aporta la sesión del portal | Acordada | — |
| D3 | El backend resuelve permisos contra la tabla de la administración | Acordada | — |
| D4 | Nuestro backend es independiente del backend existente del portal | Acordada | — |
| D5 | La identidad no viaja firmada; se acepta el riesgo | Acordada | ver `ADR-001` |
| D6 | Encabezado de identidad y endpoint de sesión | Definida | Backend |
| D7 | Trabajos pesados con aceptado + identificador | Definida | Backend |
| D8 | Avisos por SignalR con consulta de respaldo | Definida | Backend |
| D9 | Forma única de error | Definida | Backend |
| P1 | Opciones de Cabina de Fletes en la tabla de permisos | Pendiente | TI |
| P2 | Equivalencia de los roles del portal a los 6 de Nexo | Pendiente | Negocio |
| P3 | Precedencia cuando un usuario tiene varios roles | Pendiente | Negocio |
| P4 | Token de sesión verificable | Pendiente | Equipo del portal, sin fecha |

---

## 1. Identidad

### Cómo llega

El módulo de Cabina de Fletes solo es alcanzable después de que la persona ya
inició sesión en el portal. Nexo no pide credenciales, no emite tokens y no
guarda sesión propia.

El portal deja los datos de la persona en el navegador. De ahí el frontend toma
**únicamente el identificador** y lo envía en cada petición:

```http
GET /api/sesion/actual HTTP/1.1
X-Portal-Usuario-Id: 10123
```

### Qué NO se envía y por qué

El objeto que el portal deja en el navegador incluye la lista de permisos de la
persona. **Esa lista no viaja al backend y el backend no la aceptaría.**

Cualquier cosa que el navegador afirme sobre lo que puede hacer su dueño es una
afirmación del cliente, no un hecho. Nexo resuelve los permisos yendo a la tabla
de la administración con el identificador recibido. Enviar la lista solo serviría
para tentarnos a confiar en ella.

Consecuencia directa: **falsificar un rol editando el navegador no funciona.** Ese
es el escenario que la auditoría de QA dejó como criterio vigente.

Lo que sí sigue siendo posible es cambiar el identificador por el de otra persona
y recibir sus permisos. La identidad no viaja firmada. Está aceptado a conciencia
para este sprint y está escrito en [`ADR-001`](ADR-001-identidad-sin-token.md) con
las condiciones que obligan a revisarlo.

### Qué valida el backend

En **cada** petición, sin caché de sesión:

1. Que el identificador exista en la tabla de usuarios de la administración.
2. Que esa persona tenga permiso sobre Cabina de Fletes.
3. Que su permiso corresponda a alguno de los 6 roles de Nexo.

Si cualquiera de los tres falla, la petición se rechaza. **No hay rol por
defecto.** No existe un modo permisivo, ni siquiera en desarrollo: el esquema
`X-Rol` del prototipo se retira en la issue #11.

### Endpoint de sesión

```http
GET /api/sesion/actual
X-Portal-Usuario-Id: 10123
```

```json
200 OK
{
  "id": 10123,
  "nombre": "dguerra",
  "rol": "AdminTarifas",
  "capacidades": ["verBiblioteca", "ajustarVigencia", "..."]
}
```

| Código | Significa | Qué debe hacer el frontend |
|---|---|---|
| `200` | Sesión válida | Entrar al módulo |
| `401` | El identificador no existe en la tabla | Devolver a la persona al portal |
| `403` | Existe, pero sin permiso sobre Cabina de Fletes | Mensaje y salida al portal |

Un `401` **nunca** significa "muestra una pantalla de acceso de Nexo", porque esa
pantalla no existe. Significa que la sesión del portal no sirve y la persona tiene
que volver allá.

El campo `capacidades` existe para que el frontend decida qué pinta, no para que
decida qué permite. La autorización se aplica en el backend en todos los casos, y
la pantalla es solo cortesía visual.

---

## 2. Roles

Nexo trabaja con seis roles y solo seis:

```
CoordinadorFletes · LiderProceso · ServiciosLogisticos · Admin · Comercial · AdminTarifas
```

La administración tiene su propio catálogo, mucho más amplio y con otros nombres.
La traducción de uno a otro ocurre **en el backend de Nexo**, contra la tabla, y
no la puede tocar nadie desde el navegador.

### Pendiente P2 — la equivalencia

Hoy solo hay dos correspondencias con candidato evidente:

| Rol en la administración | Rol de Nexo | Confianza |
|---|---|---|
| `TarifasComercial` | `Comercial` | Alta, por nombre |
| `TarifasAdministrador` | `AdminTarifas` | Alta, por nombre |
| `Administracion` | `Admin` | Media, revisar alcance |
| — | `CoordinadorFletes` | Sin candidato |
| — | `LiderProceso` | Sin candidato |
| — | `ServiciosLogisticos` | Sin candidato |

Tres de los seis roles de Nexo no tienen equivalente. Negocio decide si se mapean
contra roles existentes o si se pide crearlos.

### Pendiente P1 — qué pedirle a TI

La unidad de acceso en la administración no es el rol sino la **opción**: cada
permiso es un par de rol y opción, y es la opción la que abre una pantalla.

Por eso conviene pedir a TI las **opciones de Cabina de Fletes** y que las cuelgue
de los roles que ya existen, en vez de pedir tres roles nuevos. Es una solicitud
más pequeña, no ensucia el catálogo con roles que solo usa un módulo, y deja la
asignación de personas donde ya la administran.

### Pendiente P3 — varios roles a la vez

Una persona puede tener varios permisos, y ya hay casos reales con dos roles de
tarifas simultáneos. Nexo asume un rol efectivo por usuario, así que hace falta
un orden de precedencia.

Propuesta a confirmar por negocio, del que más manda al que menos:

```
Admin > CoordinadorFletes > AdminTarifas > Comercial > LiderProceso > ServiciosLogisticos
```

Sale de qué tan a menudo aparece cada rol autorizando operaciones de escritura en
los controladores actuales. Es una inferencia técnica, no una decisión de negocio,
y por eso queda pendiente.

---

## 3. Autorización por operación

Esto no es propuesta: es lo que el backend exige hoy, leído de los atributos
`[Authorize]` de cada controlador. Es la referencia contra la que el frontend
decide qué mostrar.

| Operación | Roles admitidos |
|---|---|
| `POST /api/captura-solicitudes` y todo el módulo de captura | `Comercial`, `Admin` |
| `POST /api/solicitudes` · carga masiva de líneas · recalcular score | `CoordinadorFletes`, `Admin` |
| `POST /api/solicitudes/{id}/rechazar` | `Comercial`, `CoordinadorFletes`, `Admin` |
| `POST /api/cotizaciones` · aplicar propuestas · registrar resultado | `Comercial`, `AdminTarifas`, `CoordinadorFletes`, `Admin` |
| `POST /api/cotizaciones/{id}/lineas` · entregar | `AdminTarifas`, `CoordinadorFletes`, `Admin` |
| `POST /api/cotizaciones/{id}/conexos` | `Comercial`, `CoordinadorFletes`, `Admin` |
| Servicios conexos: crear, editar, importar | `ServiciosLogisticos`, `AdminTarifas`, `Admin` |
| Reglas solo SICE-TAC (todo) | `AdminTarifas`, `Admin` |
| Métricas de cumplimiento | `LiderProceso`, `AdminTarifas`, `Admin` |
| Lecturas de biblioteca, solicitudes, cotizaciones, conexos | Cualquier rol autenticado |

Cuando esta tabla y el código no coincidan, es un defecto y se corrige el código.

---

## 4. Trabajos pesados

Costear un pliego de trescientas líneas no cabe en una petición HTTP. Ninguna
operación pesada responde con su resultado.

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

La petición vuelve de inmediato con un identificador. El trabajo real ocurre
detrás, en la cola.

### Estados

```
Procesando → Completado   el servicio resolvió todo por su cuenta
           → Parcial      quedó algo pendiente: reintento o espera a una persona
           → Fallido      se rompió el servicio mismo
```

`Parcial` no es un error y va a ser el desenlace más común. `Fallido` está
reservado para cuando el servicio se rompe, no para cuando simplemente no había
tarifa.

### Hay dos tipos de pendiente, y no se manejan igual

Esto es lo que más importa entender del avance de un trabajo.

**Pendiente técnica.** Una fuente se cayó. Se reintenta y se resuelve solo. Tiene
reloj: entra en la ventana automática del trabajo.

**Requiere tarifa manual.** No hay tarifa válida para esa línea. El servicio hizo
su trabajo y la respuesta es que aquí hace falta el Administrador de Tarifas. **No
se reintenta y no tiene reloj**: nada va a cambiar hasta que esa persona actúe, y
puede tener otros pliegos delante. Poner un timeout sobre trabajo humano solo
sirve para marcar como fallido algo que estaba esperando bien.

Por eso el avance las devuelve por separado. Con un contador único habría que
mentirle al usuario en la mitad de los casos: "estamos reintentando" cuando la
verdad es "está en la cola del administrador de tarifas" son mensajes distintos,
y el segundo le dice a quién ir a buscar.

### El timeout mide máquina, no personas

La ventana automática del trabajo cubre lo que el servicio puede hacer solo, cuya
duración es predecible. Si vence con líneas sin desenlace, el trabajo cierra en
`Parcial` con lo que hay, y las que faltan conservan su motivo.

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
  "secuencia": 191,
  "esperandoTarifa": [
    {
      "lineaId": "7c1e...",
      "motivo": "No hay tarifa de plaza vigente para la ruta.",
      "esperandoDesde": "2026-08-25T12:00:04Z"
    }
  ]
}
```

`esperandoTarifa` es lo que hace útil el aviso. El contador dice cuántas; esto dice
**cuáles y qué les falta**, y con eso el comercial sabe a quién ir a buscar y el
Administrador de Tarifas sabe qué cargar.

`total` es nulo hasta que el pliego se reparte: antes de eso nadie sabe cuántas
líneas hay.

`secuencia` es un contador que solo crece dentro del trabajo. Guárdalo al consultar:
sirve para descartar los avisos rezagados que lleguen después. Está explicado en el
punto 5.

---

## 5. Avisos en vivo

```
Hub:      /hubs/trabajos          (requiere la misma identidad que el API)
Suscribir:   Suscribir(trabajoId)
Desuscribir: Desuscribir(trabajoId)
Evento:      "avance"
```

Cada trabajo es un grupo, así que solo llegan los avisos de los trabajos a los que
el cliente se suscribió. No hay difusión general.

### Qué trae el aviso, y qué no

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

Es el mismo estado que devuelve la consulta **sin la lista de pendientes**, y se
construye desde ella para que los campos compartidos no puedan divergir.

La lista no viaja por una razón medida: un pliego de 300 líneas con 50 pendientes
pesa entre 12 y 25 KB por aviso, y se avisa unas 300 veces. Serían varios megabytes
por pliego y por cliente conectado. Solo con los contadores el aviso pesa unos 200
bytes.

**El reparto es ese: el aviso mueve la barra de progreso, la consulta trae el
detalle.** Quien necesite ver qué líneas están pendientes y por qué, llama a
`GET /api/trabajos/{id}` — al terminar, o cuando el usuario abra ese panel.

### Los avisos pueden llegar desordenados: descarta por `secuencia`

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

Los números pueden saltar (191, 194, 195) y eso es normal — no todos los cambios
alcanzan a llegar. Lo único que se promete es que nunca retroceden.

### El aviso es una mejora, no una garantía

La conexión se cae, el navegador se duerme, la persona cambia de pestaña. La verdad
siempre está en `GET /api/trabajos/{id}`, y el frontend tiene que poder reconstruir
el estado completo con esa consulta sola, sin haber recibido un solo aviso.

**SignalR NO reenvía lo que te perdiste mientras estabas desconectado.** Al
reconectar hay que consultar el estado; el canal no tiene memoria. Prometer otra cosa
sería mentir.

Quien construya la pantalla asumiendo que los avisos llegan siempre va a tener un
error que solo aparece con mala red y que es muy difícil de reproducir.

---

## 6. Errores

Toda respuesta de error del API tiene la misma forma, la produzca quien la
produzca:

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
| `401` | Sin identidad válida del portal |
| `403` | Identidad válida, rol insuficiente |
| `404` | El recurso no existe |
| `409` | Conflicto, incluida edición simultánea |
| `503` | Fuente externa no disponible, con motivo accionable |
| `500` | Error no controlado |

El `code` es estable y pensado para que el frontend ramifique con él. El `message`
está en español y es para mostrar. Nadie debe leer el `message` para decidir.

---

## 7. Independencia del backend existente

Nexo vive detrás de su propia URL base y no comparte nada con el backend actual
del portal. Ni sesión, ni base de datos propia, ni despliegue.

El único punto de contacto es la lectura de la tabla de usuarios y permisos de la
administración, y es de **solo lectura**. Nexo nunca escribe ahí.

Esa lectura sigue el mismo patrón que las otras cuatro fuentes externas que el
backend ya consume: una interfaz en la capa de aplicación, un adaptador en
infraestructura, cadena de conexión por configuración. Si la fuente no está
configurada, el arranque falla en Producción en vez de degradarse a un modo
permisivo.

El costo de leer una tabla ajena hay que decirlo: quedamos atados a un esquema
que no controlamos, y un cambio de columna del otro lado nos rompe sin aviso. Se
mitiga leyendo lo mínimo y aislándolo detrás de la interfaz, pero no desaparece.

---

## 8. Versionado

Este contrato se versiona con el código, en este repositorio. Cambiarlo es un PR
que revisan backend y frontend.

Un cambio que rompa a quien ya consume sube la versión mayor y se anuncia antes de
implementarse. Agregar campos opcionales sube la menor. Corregir redacción sube la
de parche.

---

## Archivos

| Archivo | Qué contiene |
|---|---|
| `README.md` | Este documento |
| `openapi.yaml` | La superficie del contrato, ejecutable |
| `escenarios.feature` | Los escenarios que implementan las issues #9, #10 y #11 |
| `ADR-001-identidad-sin-token.md` | Por qué la identidad no viaja firmada |
