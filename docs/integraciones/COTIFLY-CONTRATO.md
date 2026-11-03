# Contrato NEXO ⇄ Cotifly

Lo que Cotifly **manda a NEXO** y lo que **recibe de vuelta**, con las decisiones ya tomadas por
negocio. Es el contrato de la *captura de solicitudes*; cómo pedirle la recomendación de vehículo al
Cubicador está en [`COTIFLY-COMO-CONECTARSE-AL-CUBICADOR.md`](./COTIFLY-COMO-CONECTARSE-AL-CUBICADOR.md).

> **Fuente de verdad.** El detalle campo por campo vive en [`CONTRATO.md`](../../CONTRATO.md) §3.3
> (`POST /api/captura-solicitudes`) y §3.2 (`GET /api/solicitudes/{id}`). Este documento explica
> **qué decide NEXO y por qué** para que Cotifly no tenga que adivinarlo. Si algo no coincide, manda
> `CONTRATO.md`.

Revisado contra el código el 2026-10-14 (S5-HU-17.7 / 17.8 / 18.2 / 18.3 / 20.5).

---

## 1. Dónde se hace cada cosa

```
Cotifly ──(1) POST /recomendaciones──────────► Cubicador   (qué camiones hacen falta)
Cotifly ──(2) POST /api/captura-solicitudes──► NEXO        (la solicitud, una línea por camión)
NEXO ─────(3) costea cada línea (SICE-TAC · plaza · histórico)
Cotifly ──(4) GET /api/solicitudes/{id}──────► NEXO        (ver el costeo, líneas ordenadas)
```

- El paso (1) es contra el **Cubicador directamente**, con la llave de Cotifly.
- Los pasos (2) y (4) son contra **NEXO**. NEXO **no** le pide nada de esto al Cubicador por
  Cotifly: lo que Cotifly captura llega a NEXO con los códigos que el Cubicador devolvió.

## 2. Autenticación contra NEXO

NEXO no emite tokens. La identidad es la de la persona que inició sesión en el portal y viaja en un
header con **solo su identificador**:

```
X-Portal-clsUsuario-Id: <id de la persona>
```

> El nombre real del header hoy es `X-Portal-clsUsuario-Id` (constante `HeaderIdUsuario`). Algunos
> documentos viejos dicen `X-Portal-Usuario-Id`: con ese nombre NEXO responde `401`. Está registrado
> como discrepancia abierta en `CONTRATO.md` §7.

`POST /api/captura-solicitudes` exige rol **Comercial o Admin**. Un usuario con otro rol recibe `403`.

## 3. `POST /api/captura-solicitudes` — lo que Cotifly manda

Una solicitud con N **líneas**. Los campos están en `CONTRATO.md` §3.3; estas son las reglas que importan.

### 3.1 Una línea por camión (decidido: #217, 2026-09-26)

Cuando el Cubicador recomienda varios camiones, Cotifly manda **una línea por cada camión**, no una
línea con "el camión elegido". Por cada línea:

| Campo | Qué poner |
|---|---|
| `origen` / `destino` | Los de la ruta (el mismo en las líneas del grupo). Mejor con `origenMunicipioId` / `destinoMunicipioId` (id de `GET /api/captura-solicitudes/ciudades`): sin ellos el motor de costeo no resuelve municipio |
| `tipoVehiculo` | El `codigoVehiculo` que devolvió el Cubicador (`2L2`, `C2`, `3S3`…) |
| `carroceria` | El `codigoCarroceria` que devolvió el Cubicador (`FURGON`, `ESTACAS`…) |
| `peso` | El `pesoTotalKg` **de ese camión** (es el peso con el que se costea el flete) |
| `idRecomendacionVehiculo` | El de **ese** camión (texto, máx. 64; un GUID en texto cabe) |
| `grupoRutaId` | **Un GUID que genera Cotifly**, el mismo en todas las líneas del mismo grupo (misma ruta y misma recomendación) |
| `claseViaje` | Contenedor, Cupo, Carga suelta o Extradimensionado — ver §4 |
| `posiciones[]` | **Opcional.** Ver §3.5 |

### 3.2 El orden de las líneas — NEXO numera, Cotifly no manda número

- Cotifly **no** manda un número de línea ni un campo "orden".
- NEXO numera por el **orden del arreglo `lineas`**: `lineas[0]` → 1, `lineas[1]` → 2…
- `GET /api/solicitudes/{id}` devuelve cada línea con `numeroLinea` y **ordenadas** por él (las
  líneas viejas, sin número, al final; luego por fecha de creación y por id). El orden es estable
  entre consultas, así que lo que Cotifly manda es lo que ve el comercial.
- `numeroLinea` define cuál línea de un grupo es "la primera": ahí se cuelgan los servicios conexos
  de la ruta. Por eso conviene mandar **las líneas del mismo grupo contiguas y en el orden en que
  se quieren ver**.

### 3.3 `grupoRutaId` — NEXO lo guarda, nunca lo genera

Agrupa las líneas de una misma carga heterogénea repartida en varios camiones. El contrato antes
decía "lo pone siempre Cotifly"; hoy lo pone **quien crea las líneas del grupo** (Cotifly, o el
portal al capturar una línea por camión). NEXO lo guarda tal cual. Una línea suelta lleva `null`.

### 3.4 Códigos del Cubicador — y qué pasa con uno desconocido

Los códigos de vehículo y carrocería llegan en `tipoVehiculo` y `carroceria` **tal como los devolvió
el Cubicador**. Un código que NEXO no conoce **no se rechaza**: la fila nace en **cuarentena**
(no oficial, S5-HU-18.4) y un humano decide a qué código canónico corresponde. Los nombres se
comparan sin tildes y con `_` y `-` equivalentes a espacio (`Bogotá` = `Bogota`,
`FURGON_REFRIGERADO` = `Furgon Refrigerado`).

**Diccionario de alias (S6-HU-27.2/#321).** Antes de caer en cuarentena, el `tipoVehiculo` se busca en el diccionario de alias de NEXO: `TB`, `T.B`, `Turbo` y `2L2`
resuelven al mismo código (`2L2`), y los códigos del Cubicador que tengan alias resuelven igual (la fila nace oficial). La `carroceria` **no** pasa por el diccionario.
Cotifly puede preguntar antes de mandar: `GET /api/catalogos/alias/resolver?tipo=vehiculo&texto=T.MULA` responde `{ reconocido, codigo }` (`reconocido: false` = ese texto caería
en cuarentena). La lista completa: `GET /api/catalogos/alias?tipo=vehiculo`. El diccionario es uno solo, en NEXO. La clase de contenedor también resuelve por alias
(`40 HC`, `40HC` → `CNT40.HC`; `20 STD` → `CNT20.ST`; `tipo=contenedor`). Lo que quede en cuarentena lo resuelve un humano en la bandeja de normalización de NEXO.

### 3.5 Posiciones y tipo de mercancía

- `posiciones[]` es **opcional**. Cotifly puede dejar de mandarlas; NEXO las sigue aceptando.
  Si las manda, cada una lleva `largoCm`, `anchoCm`, `altoCm` y **`pesoKg` (obligatorio desde #304)**, y
  opcionalmente `cantidad` (1–10.000) y `descripcion`. **`pesoKg` es el peso de UNA unidad, no el total de la
  posición**: NEXO y el Cubicador multiplican por `cantidad`; mandar el total lo cuenta dos veces. La suma de
  `cantidad` de las posiciones de una línea no puede pasar de **10.000** (`400 CUBICACION_DEMASIADO_GRANDE`).
- **El código de mercancía no se manda en la captura.** Si algún día hiciera falta, iría **por
  posición** (`posiciones[].codigoTipoMercancia`, código y no uuid). Hoy Cotifly manda el tipo de
  mercancía solo al Cubicador, para pedir la recomendación.
- Regla de peso: el peso de la línea es `peso` si viene; si no, la suma de `cantidad × pesoKg` de
  las posiciones. Si vienen los dos, `peso` debe ser **mayor o igual** a la suma de las posiciones
  (tolerancia 1 % por redondeos): es `400 PESO_INCONSISTENTE` solo cuando las posiciones suman **más** que
  `peso`. Cotifly envía el peso neto, así que `peso` = suma de las posiciones sigue siendo válido.

### 3.6 Horas de cargue y descargue por línea (#300)

- Cotifly puede mandar `horasCargue` y `horasDescargue` en la línea (0–240). Hay que mandar **las dos**: si
  falta una, NEXO ignora la que llegó y aplica el default. Si vienen las dos, **se respetan**.
- Default del viaje **cargado**: **4 + 4** (o las de la solicitud, si traen).
- Una línea que **es** una devolución de contenedor vacío (`lineaNegocio` = devolución) arranca con **1 + 1**.
- `retornaContenedor` + `ciudadDevolucionContenedor` **no cambian** las horas de la línea: NEXO suma aparte el
  tramo vacío de retorno (piso SICE-TAC de viaje vacío, 1 + 1). Lo mismo el levante en otra ciudad. Esas horas
  de los tramos vacíos no se mandan ni se guardan por línea: son fijas.

### 3.7 Peso de la línea: incluye la tara, y Nexo no la suma (S6-HU-28.1/#308)

Decisiones D2 y D2b de la revisión del 16 de octubre de 2026:

- El `peso` de cada línea es el peso **total que se transporta, INCLUYENDO la tara** (embalaje, estibas, contenedor vacío...). También
  en las solicitudes que llegan de Cotifly.
- **La tara la escribe quien captura** (el Comercial, o Cotifly dentro de su campo de peso). **Nexo NO la suma**: guarda `peso` tal cual.
- **Siempre kilogramos.** Si el origen trabaja en toneladas, la conversión es de quien captura; el backend de Nexo nunca recibe toneladas.

Qué peso usa cada cosa:

| Qué | Usa |
|---|---|
| Rango de peso de la tarifa de plaza (SAP y propia de Nexo) y configuración SICE-TAC de un contenedor | el `peso` de la línea (**con** tara) |
| Recomendación de vehículo (`POST /recomendaciones` del Cubicador, o `POST /api/cubicacion` de Nexo) | el de cada mercancía, `pesoKg` **por unidad** × `cantidad` (**sin** tara). El Cubicador lo compara contra la capacidad del vehículo **menos su tara** — hoy la tara de los vehículos está en 0 |
| Valor del piso SICE-TAC | no usa el peso (ruta, configuración y horas) |

Si se mandan `posiciones[]`, NEXO valida que la suma `cantidad × pesoKg` de las posiciones **no supere** el `peso` de la línea, con
tolerancia del 1 % (`400 PESO_INCONSISTENTE`). Como las posiciones van **sin** tara y `peso` va **con** tara, un `peso` mayor que la suma
(la tara) es válido y la línea se crea. Ejemplo: `peso` 1000 con una posición de 100 kg → `201`; posiciones que suman 1200 con `peso` 1000 → `400`.

## 4. "Línea sin vehículo": solo Cupo

Una línea **sin `tipoVehiculo`** se acepta. Pero:

| Clase de viaje | Sin vehículo |
|---|---|
| **Cupo** (o sin `claseViaje`) | **No se autocostea.** Queda `PorCostear`, **sin precio**, con el motivo `"Línea sin vehículo: requiere consulta completa"`, visible en la Mesa de Costeo. Los candidatos salen como referencia; el Administrador de Tarifas elige o ingresa el valor (nunca bajo el piso). Si luego se le pone vehículo, se costea normalmente |
| **Contenedor** | Sin cambios: el vehículo se deduce por peso y clase de contenedor |
| **Carga suelta** | Sin cambios |
| **Extradimensionado** | Sin cambios |

> **Por qué solo Cupo** (decidido por Sara): sin vehículo, NEXO buscaría el piso SICE-TAC sin filtro y
> tomaría el menor valor, y la plaza caería al "mayor valor vigente" solo con una advertencia — un
> precio bajo que nadie revisa. **Conclusión práctica: mande el vehículo siempre que lo tenga.** El
> Cubicador lo da en `codigoVehiculo` de cada camión recomendado.

## 5. Lo que NEXO responde

`201 CapturaResultDto` con `Location: /api/solicitudes/{id}`:

```json
{ "id": "…", "lineas": 3, "rutasCreadas": 1, "vehiculosCreados": 0,
  "scorePrioridad": 62.5, "fletesCalculados": 2, "lineasPorCostear": 1, "costeoEnProceso": false }
```

- Hasta **10 líneas** el costeo automático corre dentro de la petición. Más de 10 corre en segundo
  plano (`costeoEnProceso: true`): se sigue por la solicitud o por los avisos de la Bandeja.
- `lineasPorCostear` incluye las pendientes de la consulta completa (también las de §4).
- Errores: `400 CLIENTE_REQUERIDO` · `CLIENTE_INVALIDO` · `CLIENTE_BP_INVALIDO` ·
  `DWCUBOS_NO_CONFIGURADO` · `ENUM_INVALIDO` · `LINEA_INCOMPLETA` · `PESO_REQUERIDO` ·
  `PESO_INCONSISTENTE`.

### 5.1 Lo que Cotifly puede leer de cada línea (`GET /api/solicitudes/{id}`)

Todos los campos nuevos van **al final del objeto línea y son opcionales**: quien ya lee la línea sigue leyendo igual.

| Campo | Qué es | Issue |
|---|---|---|
| `estadoLinea` | `porCostear`, `autoResuelta`, `revisada` o `rechazada` (minúscula inicial). **`rechazada`**: el Administrador de Tarifas decidió no costear la línea (S6-HU-24.3/#320); Cotifly **no** debe dejar la cotización «pendiente» por ella | #309, #320 |
| `motivoRechazo` | por qué se rechazó la línea (máx. 500); null si no está rechazada. Una línea rechazada no tiene flete ni suma al total; en el Excel de la cotización sale como «Sin costeo» (sin precio ni motivo) y el correo al comercial trae el motivo | #320 |
| `motivoPendiente` | por qué sigue en `porCostear` (consulta completa, línea sin vehículo, SICE-TAC que no es del mes actual...); null si no está pendiente. Cotifly puede mostrarlo en vez de un "pendiente de costeo" mudo | #309 |
| `lineaNegocio` | `puertoAInterior`, `interiorAPuerto`, `nacional`, `devolucionContenedor`, `andino`, `licitaciones`; null si la línea no la trae | #311 |
| `claseViaje` | `contenedor`, `cupo`, `cargaSuelta`, `extradimensionado`; null si no | #311 |
| `claseContenedor` | tal como se capturó (`CNT20.IT`...). **null salvo que `claseViaje` sea `contenedor`** | #311 |
| `origenMunicipioId`, `destinoMunicipioId` | id de `tbl_municipios` (el de `GET /ciudades`); null si la ruta no resolvió municipio | #311 |
| `tieneReglaSoloSicetac` | la ruta y el vehículo tienen regla Solo-SICE (su flete no se edita) | S6-HU-26.1/#30 |


## 6. Decisiones ya tomadas (respuestas de NEXO a Cotifly)

| Pregunta de Cotifly | Respuesta |
|---|---|
| ¿Mandamos el código de mercancía en la captura? | **No.** Si hiciera falta, iría por posición |
| ¿Se retira el endpoint de cubicación de NEXO (`POST /api/cubicacion`)? | **No.** La pantalla de cubicar de NEXO lo usa; basta con que Cotifly deje de llamarlo y vaya directo al Cubicador |
| ¿Qué es una "línea sin vehículo"? | **Solo Cupo** (§4) |
| ¿Seguimos mandando `posiciones[]`? | Pueden dejar de mandarlas; NEXO las acepta como opcionales |
| ¿Mandamos número de orden de las líneas? | **No.** NEXO numera por el orden del arreglo (§3.2) |
| ¿Cómo llegan los códigos del Cubicador? | En `tipoVehiculo` y `carroceria`; uno desconocido nace en cuarentena (§3.4) |

## 7. Checklist para Cotifly

- [ ] Una línea por camión recomendado, con `tipoVehiculo` = `codigoVehiculo` y `carroceria` = `codigoCarroceria`.
- [ ] `peso` = `pesoTotalKg` del camión; `idRecomendacionVehiculo` = el de ese camión.
- [ ] Un `grupoRutaId` (GUID propio) por recomendación, igual en todas sus líneas; líneas del grupo contiguas en el arreglo.
- [ ] `origenMunicipioId` / `destinoMunicipioId` de `GET /api/captura-solicitudes/ciudades`.
- [ ] No mandar número de línea ni campo de orden.
- [ ] Mandar siempre vehículo en las líneas de Cupo (si no, quedan para consulta completa).
- [ ] Identidad en `X-Portal-clsUsuario-Id` (rol Comercial o Admin).
- [ ] Verificar el orden con `GET /api/solicitudes/{id}`: `numeroLinea` 1, 2, 3… en el orden enviado.

> **Ojo con un detalle del JSON de lectura:** en `GET /api/solicitudes/{id}` y en la propuesta de
> tarifa, el texto de la ruta sale hoy con la clave **`clsRuta`** (el `CONTRATO.md` la llama `ruta`).
> Es una discrepancia conocida del lado de NEXO; si Cotifly lee ese campo, que use `clsRuta`
> hasta que se corrija.
