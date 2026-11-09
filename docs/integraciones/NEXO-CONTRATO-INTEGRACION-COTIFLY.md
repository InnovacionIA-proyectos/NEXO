# Contrato de integración NEXO ⇄ Cotifly (documento único)

Todo lo que Cotifly necesita para conectarse al backend de NEXO, en un solo lugar: cómo autenticarse, qué endpoints llamar y en qué
orden, qué mandar, qué se recibe, qué decisiones de negocio ya están tomadas y qué errores manejar.

> **Fuente de verdad.** Este documento explica **qué hace NEXO y por qué**, con los campos que Cotifly usa. El detalle campo por campo de cada
> endpoint (todas las restricciones y errores) está en [`CONTRATO.md`](../../CONTRATO.md); si algo no coincide, **manda `CONTRATO.md`**. La parte
> del Cubicador (llave, URL, formato de la recomendación) tiene su guía propia: [`COTIFLY-COMO-CONECTARSE-AL-CUBICADOR.md`](./COTIFLY-COMO-CONECTARSE-AL-CUBICADOR.md).

Verificado el **2026-11-04** contra el código de la rama `QA` y contra respuestas reales de la API. Reemplaza a los documentos anteriores de Cotifly (captura de solicitudes y conexión de la recomendación), ya retirados del repositorio.

---

## 1. Mapa de la integración

```
                  ┌──────────────────────── Cotifly ────────────────────────┐
                  │                                                          │
   (A) Bearer de Cotifly                                     (B) X-Portal-clsUsuario-Id
                  │                                                          │
                  ▼                                                          ▼
            Cubicador                                                      NEXO
   POST /recomendaciones  (qué camiones)                  catálogos · captura · lectura del costeo
   GET  /recomendaciones/explicacion                      importación inteligente de Excel
                                                          cotización (costo y venta de los conexos)
```

- **(A) Cubicador directo.** La recomendación de vehículo se pide **directo al Cubicador** con la llave propia de Cotifly (decidido). Detalle en la guía del Cubicador.
- **(B) NEXO.** Todo lo demás. NEXO **no llama a Cotifly**: no hay webhooks ni callbacks; Cotifly consulta cuando lo necesita.
- NEXO también expone la recomendación como **proxy** (`POST/GET /api/cubicacion`, §4.2). Sirve a quien no quiera hablar con el Cubicador, pero esas recomendaciones quedan guardadas
  con origen `NEXO`, no `COTIFLY`.

## 2. Conectarse a NEXO

| Qué | Valor |
|---|---|
| **Base URL** | La del ambiente (la entrega infraestructura). Swagger solo existe en Development |
| **Identidad** | Header **`X-Portal-clsUsuario-Id: <id numérico del portal>`** — el id de la persona que actúa (el comercial). No hay login, contraseña ni token |
| **Rol** | NEXO lo resuelve desde ese id. Para capturar y consultar, la persona debe tener rol **Comercial** (o Admin) |
| **Formato** | JSON en **camelCase**; enums como texto; fechas ISO 8601; peso en **kg**, medidas en **cm**, dinero en pesos |
| **Idempotencia** | `POST /api/captura-solicitudes` **no es idempotente**: cada llamada crea una solicitud. No reintentar a ciegas un `5xx`/timeout sin consultar antes |

> ⚠️ **El nombre real del header es `X-Portal-clsUsuario-Id`.** Algunos documentos viejos y los e2e dicen `X-Portal-Usuario-Id`: con ese nombre NEXO responde `401 SESION_INVALIDA` en
> todo. Está registrado como discrepancia abierta (`CONTRATO.md` §7 #1). Cuando se corrija el código se avisará aquí.

### 2.1 Errores

Siempre el mismo formato; **se ramifica por `code`**, nunca por `message`:

```json
{ "code": "PESO_INCONSISTENTE", "message": "…", "timestamp": "2026-11-04T13:07:31Z", "details": null }
```

| HTTP | `code` | Qué hacer |
|---|---|---|
| `400` | `VALIDATION_ERROR` | Falló la forma del body/query. `details` trae `{ "Campo": ["mensaje"] }` |
| `400` | un código de negocio (§9) | Corregir lo que mandó Cotifly |
| `401` | `SESION_INVALIDA` | Falta el header, no es numérico o el id no existe en el portal |
| `403` | `SIN_PERMISO_CABINA_FLETES` | La persona existe pero su rol no puede hacer eso |
| `404` | específico (`SOLICITUD_NO_ENCONTRADA`…) | El recurso no existe |
| `409` | específico | Conflicto de estado (p. ej. `COTIZACION_CERRADA`) |
| `429` | `DEMASIADAS_SOLICITUDES` | Reintentar tras `Retry-After` (30 s). `IA_TOPE_DIARIO` = la persona agotó su cupo diario de IA |
| `503` | específico (`CUBICADOR_NO_DISPONIBLE`, `DWCUBOS_NO_CONFIGURADO`…) | Fuente externa caída o sin configurar: reintentar más tarde |

## 3. Catálogos y búsquedas (para armar el formulario)

Todas son `GET`, JSON. Salvo `clientes` (Comercial/Admin), las abre cualquier rol válido.

| Endpoint | Para qué | Respuesta |
|---|---|---|
| `/api/captura-solicitudes/clientes?q=` | Buscar cliente por nombre, NIT o BP | `[{ clienteId, bp, nit, nitCliente, nombre, origen }]`; `origen` = `"cliente"` o `"prospecto"`. `nitCliente` es copia de `nit`. Con el DW configurado `clienteId` viene `null` (usar `bp`) |
| `/api/captura-solicitudes/ciudades?q=&departamento=` | Ciudades para origen/destino (máx. 50) | `[{ id, codigoDane, municipio, departamento, nombreTarifasPlaza, zonaTarificacion }]`. **Guardar `id`** y mandarlo como `origenMunicipioId`/`destinoMunicipioId` |
| `/api/captura-solicitudes/departamentos` | Lista de departamentos | `string[]` |
| `/api/captura-solicitudes/municipios` | Catálogo completo (~3.800 filas): cargarlo una vez | `MunicipioDto[]` |
| `/api/catalogos/tipos-vehiculo`, `/clases-contenedor`, `/clases-viaje`, `/lineas-negocio` | Valores válidos de cada lista | ver `CONTRATO.md` §3.19 |
| `/api/catalogos/alias/resolver?tipo=vehiculo&texto=T.MULA` | ¿NEXO reconoce este texto? | `{ texto, tipoElemento, reconocido, codigo, alias }`. `reconocido: false` = caería en cuarentena. `tipo` = `vehiculo` o `contenedor` |
| `/api/captura-solicitudes/aprovisionamiento?levante=&origen=&carroceria=` | Estimar el viaje vacío de levante (solo informativo) | `{ candidato }` (puede ser `null`). Faltan `levante`/`origen` → `400 PARAMETROS_REQUERIDOS` |

## 4. Recomendación de vehículo

### 4.1 Directo al Cubicador (el camino decidido)

`POST {Cubicador}/recomendaciones` con `Authorization: Bearer <llave de Cotifly>` y `origenSistema: "COTIFLY"`. La llave sale en el servidor de Cotifly, nunca en el navegador, y debe ser
**distinta** de la de NEXO. Todo el detalle (request, los tres resultados `ASIGNADO` / `SIN_SELECCION_AUTOMATICA` / `condicionada`, errores y reconsulta) está en
[`COTIFLY-COMO-CONECTARSE-AL-CUBICADOR.md`](./COTIFLY-COMO-CONECTARSE-AL-CUBICADOR.md). De cada camión de `vehiculos[]` salen los datos de la línea de captura (§5.1).

**Explicación en lenguaje natural** (S6-HU-35.4): `GET {Cubicador}/recomendaciones/explicacion?idLineaSolicitud=&origenSistema=`. Es opcional y **siempre responde `200`**
con `{ "explicacion": texto | null }`. Se pide **después** de mostrar los números (el modelo tarda varios segundos).

### 4.2 A través de NEXO (proxy)

| Endpoint | Roles | Qué hace |
|---|---|---|
| `POST /api/cubicacion` | Comercial, AdminBiblioteca, AdminTarifas, Admin | Crea la recomendación en el Cubicador con la llave de NEXO. Body: `{ idLineaSolicitud, mercancias[{ codigoMercancia, cantidad, largoCm, anchoCm, altoCm, pesoKg }], claseViajeSeleccionada? }` |
| `GET /api/cubicacion?idLineaSolicitud=&origenSistema=` | los mismos | Reconsulta la última corrida. `404 RECOMENDACION_NO_ENCONTRADA` si no hay |
| `GET /api/cubicacion/explicacion?idLineaSolicitud=&origenSistema=` | los mismos | La explicación. **Siempre `200`**, `{ "explicacion": null }` si el Cubicador está caído, sin configurar, sin respuesta o sin texto |

`pesoKg` es **por unidad**. `SIN_SELECCION_AUTOMATICA` y `condicionada` son resultados `200`, no errores. Errores `400` del Cubicador pasan con su mismo código
(`MERCANCIA_DESCONOCIDA`, `DIMENSIONES_INVALIDAS`, `CUBICACION_DEMASIADO_GRANDE`…); los `503` son `CUBICADOR_NO_DISPONIBLE`, `CUBICADOR_CREDENCIAL_INVALIDA`, `CUBICADOR_ERROR`
o `CUBICACION_RAPIDA_NO_CONFIGURADA`.

## 5. Capturar la solicitud — `POST /api/captura-solicitudes`

Rol **Comercial** o Admin. Crea la solicitud con N líneas y deja los fletes costeados (o pendientes). Respuesta `201` con `Location: /api/solicitudes/{id}`.

### 5.1 Una línea por camión (decidido, #217)

Cuando el Cubicador recomienda varios camiones, Cotifly manda **una línea por camión**:

| Campo de la línea | Qué poner |
|---|---|
| `origen` / `destino` | Los de la ruta. Mandar también `origenMunicipioId` / `destinoMunicipioId` (§3): sin ellos el costeo no resuelve el municipio |
| `tipoVehiculo` | El `codigoVehiculo` del camión (`2L2`, `C2`, `3S3`…) |
| `carroceria` | El `codigoCarroceria` del camión (`FURGON`, `ESTACAS`…) |
| `peso` | El `pesoTotalKg` **de ese camión**, en kg, **incluyendo la tara** (§8) |
| `idRecomendacionVehiculo` | El de **ese** camión (texto, máx. 64) |
| `grupoRutaId` | Un GUID **que genera Cotifly**, igual en todas las líneas de la misma recomendación y ruta; `null` en una línea suelta. NEXO lo guarda, nunca lo genera |
| `claseViaje` | `Contenedor`, `Cupo`, `Carga suelta` o `Extradimensionado` (§8: Cupo sin vehículo queda pendiente) |
| `lineaNegocio` | **Una de seis** (§8) o vacío |
| `posiciones[]` | Opcional. Cada una con `largoCm`, `anchoCm`, `altoCm`, **`pesoKg` por unidad (obligatorio)**, `cantidad` (1–10.000), `descripcion`, `codigoTipoMercancia` |

Cabecera de la solicitud: `clienteId` **o** `clienteBp` (uno de los dos; con el DW configurado se usa `clienteBp` del resultado de §3), `tipo` (`spot` | `licitacion`), `deadline`,
`valorEstimado`, `referencia`, `notas` (máx. 2000), `horasCargue`/`horasDescargue` (0–240, **mandar las dos o ninguna**; por defecto 4 + 4; 1 + 1 si la línea es una devolución de contenedor).

```json
POST /api/captura-solicitudes
{
  "clienteBp": "0001234567", "tipo": "spot", "referencia": "COTIFLY-8842",
  "lineas": [
    { "origen": "Buenaventura", "destino": "Bogotá", "origenMunicipioId": 1012, "destinoMunicipioId": 149,
      "tipoVehiculo": "3S3", "carroceria": "FURGON", "peso": 24500, "claseViaje": "Cupo",
      "idRecomendacionVehiculo": "9C1E…", "grupoRutaId": "6f0a3c64-2c25-4d4e-a0a7-2f6f1f0a9b11" },
    { "origen": "Buenaventura", "destino": "Bogotá", "origenMunicipioId": 1012, "destinoMunicipioId": 149,
      "tipoVehiculo": "2L2", "carroceria": "ESTACAS", "peso": 3200, "claseViaje": "Cupo",
      "idRecomendacionVehiculo": "B27A…", "grupoRutaId": "6f0a3c64-2c25-4d4e-a0a7-2f6f1f0a9b11" }
  ]
}
```

```json
201
{ "id": "…", "lineas": 2, "rutasCreadas": 1, "vehiculosCreados": 0,
  "scorePrioridad": 62.5, "fletesCalculados": 2, "lineasPorCostear": 0, "costeoEnProceso": false }
```

- **El orden de las líneas lo da el arreglo**: `lineas[0]` es la línea 1. Cotifly **no manda** número de línea. Las líneas del mismo grupo deben ir **contiguas** y en el orden en que se quiere verlas.
- Hasta **10 líneas** el costeo corre dentro de la petición; con más, en segundo plano (`costeoEnProceso: true`): se sigue consultando la solicitud (§7).
- **Un código que NEXO no conoce no se rechaza**: el vehículo/carrocería nace en **cuarentena** y un humano lo resuelve. Antes, el `tipoVehiculo` pasa por el diccionario de alias (`TB`, `T.B`, `Turbo`, `2L2` → `2L2`).
- **Un prospecto sin BP ni cliente local no se puede capturar hoy**: la captura exige `clienteId` o `clienteBp`. *Punto abierto*: confirmar con NEXO el camino para prospectos.

## 6. Importación inteligente de Excel (Cotifly reutiliza la de NEXO)

Decidido: Cotifly **no replica** la importación; usa la de NEXO. Todas las rutas son de rol Comercial/Admin y cuelgan de `/api/captura-solicitudes`.

| Paso | Endpoint | Qué hace |
|---|---|---|
| 1 | `POST analizar-excel` (`multipart`, campo `archivo`, máx. 10 MB, `.xlsx`/`.xlsm`) | Lee el archivo: hojas, encabezados y filas. No guarda nada |
| 2 | `POST sugerir-mapeo` `{ clienteId \| clienteNit, encabezados, filas }` | Propone a qué campo de NEXO va cada columna (plantilla guardada → diccionario del cliente → IA). **Solo sugiere** |
| 3 | `POST aplicar-mapeo` `{ clienteId \| clienteNit, columnas, filas, primeraFila }` | Aplica el mapeo confirmado a todas las filas: devuelve las líneas listas para capturar y los errores por fila |
| 4 | `POST normalizar-valores` `{ clienteId \| clienteNit, valores[{ campo, valor }] }` | Lleva los textos libres a valores de NEXO. `campo`: `tipoVehiculo`, `carroceria`, **`lineaNegocio`**, `ciudad` |
| 5 | `POST /api/captura-solicitudes` | Captura con las líneas ya normalizadas (§5) |
| opc. | `PUT plantillas-mapeo` · `GET plantillas-mapeo?clienteNit=` · `DELETE plantillas-mapeo/{id}` | Recordar el mapeo confirmado para el siguiente archivo igual |
| opc. | `GET/POST/PUT/DELETE /api/diccionario-cliente` (`GET ?clienteNit=`; `POST` con `clienteId` \| `clienteNit`) | Diccionario de términos por cliente (lo que significa «Outbound» para ese cliente) |
| alt. al 1 | `POST leer-estructura` (`multipart`: `archivo`, `clienteId` \| `clienteNit`, `instrucciones?`, `receta?`) | Para Excel de formato complejo (tabla que no empieza en la fila 1, encabezados de varias filas, origen o vehículo en las columnas, datos sueltos). La IA propone una receta de lectura y NEXO devuelve `tablas` (igual que `analizar-excel`, una por hoja o unidas), `resumen` y `dudas`. No guarda nada |
| opc. | `PUT recetas-lectura` (`multipart`: `archivo`, `receta`, `clienteId` \| `clienteNit`, `nombre?`, `instrucciones?`) · `GET recetas-lectura?clienteNit=` · `DELETE recetas-lectura/{id}` | Guardar la receta confirmada: el siguiente archivo del cliente con el mismo formato se lee sin IA |

- **Identificar al cliente por NIT.** Todos aceptan **`clienteNit`** como alternativa a `clienteId` (con los dos manda `clienteId`). El NIT se compara sin puntos, espacios ni dígito de verificación:
  `900.123.456-7` y `900123456` son el mismo cliente. Las **lecturas** no crean nada (sin cliente local se comportan como «sin cliente»: solo diccionario general, sin plantilla). `PUT plantillas-mapeo`
  con un NIT sin cliente local **lo crea** con los datos del DW (también prospectos); un NIT desconocido da `400 CLIENTE_NIT_INVALIDO`.
- **Excel de formato complejo (`leer-estructura`).** Cada elemento de `tablas` entra al paso 2 (`sugerir-mapeo`) igual que una hoja de `analizar-excel`. Para corregir una lectura, se llama otra vez con `instrucciones`
  (texto libre) y la `receta` que devolvió la llamada anterior; la conversación la lleva Cotifly, NEXO no guarda historial. `origenReceta` dice de dónde salió la lectura (`guardada`, `ia`, `manual`, `automatica`);
  con `guardada` no se llamó a la IA. Si la IA no está disponible responde igual (`iaUsada: false`, `motivoSinIa`). `leer-estructura` solo **busca** el cliente por NIT; `PUT recetas-lectura` con un NIT sin cliente local
  lo crea como `PUT plantillas-mapeo`. Detalle completo, la forma de la receta y los errores en `CONTRATO.md` §3.3.
- **Línea de negocio.** `normalizar-valores` con `campo: "lineaNegocio"` resuelve en este orden: diccionario del cliente → sinónimos inequívocos (`impo`/`importación` → Puerto a interior,
  `expo`/`exportación` → Interior a puerto) → IA con las seis líneas → `sin_resolver`. «Inbound»/«Outbound» **no** tienen regla: salen del diccionario del cliente o quedan sin resolver.
- **Es una sugerencia.** Nada se asigna por lo que diga la IA sin que una regla lo valide; si la IA está apagada o falla, la respuesta trae lo que resolvieron las reglas (`iaUsada: false`, `motivoSinIa`).
  `429 IA_TOPE_DIARIO` si la persona agotó su cupo diario.

## 7. Seguimiento: leer el costeo y la cotización

| Endpoint | Roles | Qué devuelve |
|---|---|---|
| `GET /api/solicitudes/{id}` | Comercial, AdminTarifas, ServiciosLogisticos, LiderProceso, Admin | La solicitud y sus líneas **ordenadas** por `numeroLinea` |
| `GET /api/solicitudes/{id}/propuesta-tarifa` | los mismos | Candidatos de tarifa por línea (SICE-TAC, plaza, histórico) y el valor propuesto |
| `GET /api/cotizaciones/por-solicitud/{solicitudId}` | los mismos | La cotización existente; **`204`** si la solicitud no tiene |
| `GET /api/cotizaciones/{id}` | los mismos | La cotización por id |
| `GET /api/cotizaciones/{id}/exportar-excel` | los mismos | Excel de la cotización (con el valor de **venta** de los conexos) |

> Los roles de lectura **no incluyen AdminBiblioteca**. Un Comercial ve solo las solicitudes que él creó (aislamiento por creador).

**Lo que Cotifly lee de cada línea** (campos nuevos al final del objeto, opcionales): `numeroLinea`, `estadoLinea` (`porCostear` | `autoResuelta` | `revisada` | `rechazada`), `motivoPendiente`,
`motivoRechazo`, `lineaNegocio`, `claseViaje`, `claseContenedor`, `origenMunicipioId`/`destinoMunicipioId`, `grupoRutaId`, `idRecomendacionVehiculo`, `tieneReglaSoloSicetac`.
Una línea **`rechazada`** no tiene flete ni suma al total: Cotifly **no** debe dejar la cotización «pendiente» por ella. Una línea `porCostear` trae el porqué en `motivoPendiente`.

> ⚠️ **Nombres con prefijo `cls` en el JSON de lectura** (discrepancia conocida, `CONTRATO.md` §7): el texto de la ruta viaja como **`clsRuta`** (no `ruta`) en las líneas de
> `GET /api/solicitudes/{id}` y de `propuesta-tarifa`, y el cliente de cada fila de la bandeja como `clsCliente`. Leerlos así hasta que se corrija el código.

### 7.1 Cotización: costo y venta de los servicios conexos

Cada conexo de la cotización trae **dos valores**, y no son lo mismo:

| Campo | Qué es | Quién lo usa |
|---|---|---|
| `valorAplicado` | El **costo** del conexo (costo unitario × cantidad) | Uso **interno**. De él depende `costo_final` en Cotifly: tiene que ser el costo real |
| `precioVentaUnitario` | Precio de venta de **una unidad**, **fijado al agregar el conexo** (el de la biblioteca o, si no tenía, el costo). No cambia si la biblioteca cambia después | — |
| `valorVenta` | `precioVentaUnitario × cantidad` (2 decimales). **Nunca `null`** | Lo que se **cotiza** al cliente |

- El `total` de la cotización suma **`valorVenta`**, no el costo. El correo de entrega y el Excel de la cotización muestran solo la venta.
- **El margen de Cotifly se aplica solo al flete**, nunca a un servicio conexo.
- El valor de un conexo **no se escribe a mano**: sale de la biblioteca de servicios.
- `GET /api/servicios-conexos` trae `costoUnitario` y `precioVenta` (puede ser `null`: se cotiza al costo).

## 8. Reglas de negocio ya decididas

| Tema | Regla |
|---|---|
| **Peso** | `peso` es el total que se transporta **incluyendo la tara**, siempre en **kg**; NEXO no suma ni resta tara. Con `posiciones[]`, la suma `cantidad × pesoKg` no puede superar `peso` (tolerancia 1 %): si lo supera, `400 PESO_INCONSISTENTE`. `peso` mayor que la suma (la tara) es válido |
| **`pesoKg` de una posición** | Es el de **una unidad**, no el total de la posición (NEXO y el Cubicador lo multiplican por `cantidad`) |
| **Línea sin vehículo** | Solo vale para **Cupo**: queda `porCostear`, sin precio, con el motivo «Línea sin vehículo: requiere consulta completa» y la resuelve el Administrador de Tarifas. Contenedor, Carga suelta y Extradimensionado deducen o exigen el vehículo. **Mandar el vehículo siempre que se tenga** |
| **Línea de negocio** | Exactamente seis: `Nacional`, `Puerto a interior`, `Interior a puerto`, `Devolución de contenedor`, `Andino`, `Licitaciones` (también como `puertoAInterior`, mayúsculas o con guiones). Vacío = sin línea. **Cualquier otro texto → `400 LINEA_NEGOCIO_INVALIDO`** con el número de línea y el valor recibido (antes se perdía en silencio) |
| **Códigos del Cubicador** | Un código desconocido **no se rechaza**: nace en cuarentena y un humano decide. El `tipoVehiculo` pasa antes por los alias; la `carroceria` no |
| **Horas** | Cargado: 4 + 4 por defecto; devolución de contenedor: 1 + 1. Los tramos vacíos (retorno del contenedor, levante en otra ciudad) los suma NEXO aparte, 1 + 1, y no se mandan |
| **Clase de contenedor** | Se resuelve por alias (`40 HC`, `40HC` → `CNT40.HC`; `20 STD` → `CNT20.ST`); si no está, se guarda como texto libre (máx. 20) |
| **Posiciones** | Opcionales. Máx. 200 por línea y 10.000 unidades en total (`400 CUBICACION_DEMASIADO_GRANDE`) |
| **Orden de líneas** | NEXO numera por el orden del arreglo; Cotifly no manda número |
| **Mercancía** | El código de tipo de mercancía **no** se manda en la captura (si hiciera falta iría por posición en `codigoTipoMercancia`); se manda solo al Cubicador |

## 9. Errores de negocio que Cotifly debe manejar

| `code` | Dónde | Qué significa |
|---|---|---|
| `CLIENTE_REQUERIDO`, `CLIENTE_INVALIDO`, `CLIENTE_BP_INVALIDO` | captura | Falta el cliente, no existe o el BP no está en el catálogo del DW |
| `DWCUBOS_NO_CONFIGURADO` | captura | Se mandó `clienteBp` y el DW de clientes no está configurado en el ambiente |
| `CLIENTE_NIT_INVALIDO` | plantillas | El NIT no es de ningún cliente ni prospecto |
| `LINEA_INCOMPLETA`, `PESO_REQUERIDO`, `PESO_INCONSISTENTE` | captura | Falta origen/destino, falta el peso o las posiciones suman más que `peso` |
| `LINEA_NEGOCIO_INVALIDO` | captura | La línea de negocio no es una de las seis |
| `ENUM_INVALIDO` | captura | Un valor de lista no reconocido (`tipo`, `modalidad`, `tipologia`) |
| `CUBICACION_DEMASIADO_GRANDE`, `MERCANCIA_DESCONOCIDA`, `DIMENSIONES_INVALIDAS` | recomendación | Los manda el Cubicador tal cual |
| `RECOMENDACION_NO_ENCONTRADA` (404) | reconsulta | El Cubicador no tiene recomendación para esa línea: «todavía no hay», no una caída |
| `SOLICITUD_NO_ENCONTRADA` (404), `SOLICITUD_DESCARTADA` (409) | lectura | La solicitud no existe o fue descartada |
| `IA_TOPE_DIARIO` (429) | importación | La persona agotó su cupo diario de IA |
| `ARCHIVO_REQUERIDO`, `FORMATO_NO_SOPORTADO`, `EXCEL_INVALIDO`, `EXCEL_SIN_DATOS` | `analizar-excel`, `leer-estructura` | Problemas con el archivo |
| `RECETA_INVALIDA`, `RECETA_REQUERIDA`, `RECETA_SIN_FILAS`, `INSTRUCCIONES_DEMASIADO_LARGAS` | `leer-estructura`, `PUT recetas-lectura` | La receta o las instrucciones no sirven; el mensaje dice qué |

## 10. Discrepancias conocidas que afectan a Cotifly

| # | Qué | Hoy | Qué hacer |
|---|---|---|---|
| 1 | Nombre del header de identidad | `X-Portal-clsUsuario-Id` | Usar ese nombre. Con `X-Portal-Usuario-Id` todo es `401` |
| 2 | Texto de la ruta en las líneas de `GET /api/solicitudes/{id}` y `propuesta-tarifa` | `clsRuta` | Leer `clsRuta` |
| 3 | Cliente de cada fila de la bandeja | `clsCliente` | Leer `clsCliente` |
| 4 | Capturar un prospecto sin BP ni cliente local | No hay camino hoy | Punto abierto con NEXO (§5) |

Si alguna se corrige, el cambio se anuncia en `CONTRATO.md` §7 y en este documento **antes** de desplegarse.

## 11. Checklist de conexión

- [ ] El header `X-Portal-clsUsuario-Id` lleva el id del portal de un usuario con rol **Comercial**.
- [ ] Una línea por camión recomendado: `tipoVehiculo` = `codigoVehiculo`, `carroceria` = `codigoCarroceria`, `peso` = `pesoTotalKg`, `idRecomendacionVehiculo` del camión.
- [ ] Un `grupoRutaId` (GUID propio) por recomendación, igual en todas sus líneas, líneas del grupo contiguas.
- [ ] `origenMunicipioId` / `destinoMunicipioId` tomados de `GET /api/captura-solicitudes/ciudades`.
- [ ] `lineaNegocio` es una de las seis (o vacío); si el origen trae texto libre, pasar antes por `normalizar-valores`.
- [ ] Vehículo siempre que se tenga (sobre todo en Cupo).
- [ ] `peso` en kg con tara; `posiciones[].pesoKg` por unidad.
- [ ] No mandar número de línea ni campo de orden; no reintentar la captura a ciegas.
- [ ] Se leen `clsRuta` y `clsCliente` (hasta que se corrijan) y se tratan `rechazada` y `porCostear` sin dejar la cotización pendiente.
- [ ] En la cotización, `valorAplicado` = costo y `valorVenta` = lo cotizado; el margen de Cotifly solo sobre el flete.
- [ ] `GET /api/cubicacion/explicacion` (o la del Cubicador) se trata como opcional: `explicacion: null` no es un error.
- [ ] Se maneja `429` (`Retry-After`) y `503` (reintentar más tarde).

## 12. Documentos relacionados

| Documento | Para qué |
|---|---|
| [`CONTRATO.md`](../../CONTRATO.md) | Todos los endpoints de NEXO campo por campo (§0 es la guía de consumo y trae el índice de rutas) |
| [`COTIFLY-COMO-CONECTARSE-AL-CUBICADOR.md`](./COTIFLY-COMO-CONECTARSE-AL-CUBICADOR.md) | La recomendación de vehículo directo con el Cubicador |
| [`docs/contrato-integracion/README.md`](../contrato-integracion/README.md) | Por qué la identidad va sin token, trabajos pesados y avisos en vivo |
