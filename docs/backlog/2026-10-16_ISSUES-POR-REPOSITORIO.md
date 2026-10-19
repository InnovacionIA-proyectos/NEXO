# Issues por repositorio — revisión del 16 de octubre de 2026

_Fecha: 2026-10-16_

Este documento reúne las issues que salen de la revisión de Nexo, separadas por **repositorio y responsable**, con el análisis del código de los cuatro proyectos y el efecto de cada cambio sobre los demás.

## Recomendación para quien cree las issues en GitHub

Si necesitas más contexto de cualquier issue (por qué se pidió, qué hay hoy en el código, cómo afecta a los otros repos, qué decisiones se tomaron), **consulta este documento**: `docs/backlog/2026-10-16_ISSUES-POR-REPOSITORIO.md`, en el repo `NEXO`.

**Al crear cada issue, agrega esta referencia al final del cuerpo**, con el identificador de esa issue en el documento:

```
Contexto completo: docs/backlog/2026-10-16_ISSUES-POR-REPOSITORIO.md (repo NEXO), issue <ID>, p. ej. NEXO-B7.
Decisiones relacionadas: sección 1 del mismo documento.
```

Además:
- Una issue de GitHub por cada sección con identificador (NEXO-B1, ADM-F3, CTF-B1, DATA-2, QA-1…). Copia el bloque de texto tal cual y el título.
- Pon el repositorio y el responsable de la tabla «Cómo leerlo». Confirma antes el usuario de GitHub de cada persona.
- Si una issue depende de otra, escribe el número de GitHub de la dependencia en cuanto exista.
- Antes de crear una, revisa que no exista ya una igual. Algunos cambios pedidos ya los resolvió el PR #301 y no llevan issue (ver la sección 10 de este documento).
- Este archivo debe estar subido a una rama del repo `NEXO` para que la referencia se pueda abrir desde GitHub. Mientras no esté subido, deja solo la ruta y el identificador.

---

## Cómo leerlo

| Repositorio | Qué contiene | Responsable | Sigla de issue |
|---|---|---|---|
| `NEXO` (backend y Cubicador) | API de Nexo en .NET y la carpeta `cubicador/` | Ismael | NEXO-B |
| `administracion` (front de Nexo) | Pantallas de Nexo en Angular | Juan Pablo | ADM-F |
| `NEXO` / pruebas | Validaciones y QA | kcano | QA |
| `Cotifly` (carpeta `backend-dotnet/`) | Backend de Cotifly | Duban | CTF-B |
| `clientepro-frontend` (módulo Cotifly) | Front de Cotifly | Sara | CTF-F |
| Datos y tablas (cualquier repo) | Scripts SQL, tablas, cargas de datos | Sara | DATA |

**Usuarios de GitHub:** solo está confirmado el de Sara en `clientepro-frontend` (`scalderon-bs`, según el chat de ese repo). Los demás hay que confirmarlos antes de asignar. En documentos anteriores aparecían `@ivasco-dev`, `@jpvelez-bts` y `@kcano`, sin confirmar.

**Cómo se verificó cada dato:**
- «Verificado» es código de Nexo leído en la rama QA al 16 de octubre (commit `5164629` y posteriores).
- «Según el chat del front de Nexo / de Cotifly» es lo que reportaron las sesiones que tienen esos repos abiertos. Yo no leí ese código.
- «No verificado» está marcado donde nadie pudo comprobarlo.

**Estados de las decisiones:** ✔ tomada por Sara · ❓ pendiente.

---

## 1. Decisiones

### Tomadas

| # | Tema | Decisión |
|---|---|---|
| 1 | Ajuste manual | Se puede editar a mano un flete de plaza vigente. Solo se bloquea si la ruta tiene regla Solo-SICE. Si la tarifa de plaza ya existe, se ofrece actualizarla o guardarla como nueva. |
| 2 | Rechazo de línea: PDF y Excel | La línea sale «sin costeo», sin precio y **sin motivo**. |
| 3 | Rechazo de línea: deshacer | Solo mientras la cotización esté en la Mesa de Costeo. |
| 4 | Rechazo de línea: todas rechazadas | Se envía igual al comercial, con los motivos de cada línea. |
| 5 | Desglose | Flete, levante y retorno se ven separados **solo en la Mesa de Costeo**. El Excel y el PDF siguen con el valor final de la línea. |
| 6 | Tara | La nota de tara aplica a todas las líneas. Se quita la validación contra la suma de las posiciones. |
| 7 | Diccionario de vehículos | Vive en **Nexo**. No se duplica en Cotifly ni va en el Cubicador. |
| 8 | Unidad del peso | Selector kg/t en la pantalla (opción A). El backend siempre recibe kg. |
| 9 | Roles | CoordinadorFletes pasa a AdminBiblioteca, en backend y front. AdminTarifas, Admin y Comercial quedan igual. |
| 10 | Costo de conexos | El Comercial sigue viendo el costo unitario de los servicios conexos. Los permisos se manejan desde el front. |
| 11 | Foto en carga extradimensionada | Descartada. |
| D1 | PDF con todas las líneas rechazadas | **Se bloquea** la generación del PDF, en Nexo y en Cotifly, con un error propio. El Excel sale con todas las líneas en «Sin costeo». |
| D2 | Peso con tara | Aplica **también a la captura de Cotifly**. Se edita en front y en backend donde haga falta. El valor lo escribe el Comercial (ver D2b). |
| D3 | Lista de empaque y posiciones | **Solo se muestran en Carga Suelta y Extradimensionado**, en todas las líneas, incluidas las «Nacional» (se quita la excepción actual). Supuesto: Admin sigue viéndolas porque comparte la pantalla de captura. |
| D4 | Ajuste manual: fuente y alcance | Al editar un flete, la fuente pasa a **«Manual»** y se guarda el valor anterior. Se puede editar también un flete de SICE-TAC **mientras la ruta no tenga regla Solo-SICE**. Siempre con la regla de que **ningún valor puede quedar por debajo de SICE-TAC**. |
| D6 | Mapeo del diccionario | Se define durante la issue de bases de datos (DATA-2). |
| D8 | Orden de despliegue de los roles | Lo decide **Ismael**. |

| D2b | De dónde sale la tara | La **escribe el Comercial** en el peso de cada línea, ya con la tara incluida. No se suma automáticamente. |
| D5 | Roles: endpoints sin dueño | (1) Crear solicitud, cargar líneas en lote y recalcular puntaje: **solo Admin**. (2) **AdminBiblioteca conserva el permiso de cubicación en el backend, pero el front no le muestra el Cubicador.** (3) AdminTarifas debe poder usar el Cubicador completo: se agrega a los endpoints de planes de cargue (NEXO-B7). (4) Rechazar tarifas con motivo y «Simular»: solo AdminTarifas y Admin. |
| D7 | Columna y filtro «Resultado» en la Bandeja | **No se hace.** Solo existía en el sandbox. Se quita de los criterios de ADM-F1. |

### Pendientes (necesitan respuesta de Sara)

| # | Pregunta | Quién la necesita |
|---|---|---|
| D9 | **Cambiar el vehículo de una línea ya creada** (hoy no existe endpoint, ni siquiera en el backend). ¿Se construye? Recomendación: **sí**. Lo necesitan la normalización de vehículos en cuarentena (D6/NEXO-B4) y las líneas de Cupo que quedan sin vehículo. | NEXO-B10, front#343 |
| D10 | **Clase de contenedor para Extradimensionado:** el front pide una clase con nombres provisionales (CNT20.FT, CNT20.OP, CNT40.FT, CNT40.OP) que no existen en el catálogo real (CNT20.IT, RE, ST y CNT40.HC, RE) y el backend la guarda como texto libre. ¿Extradimensionado tiene su propio catálogo, usa el existente, o el campo es opcional y no se valida? (Es la misma duda de la issue 30.8 de hace unos días.) | NEXO-B11 |
| D11 | **`grupoRutaId` y reparto de posiciones entre camiones** (dos aclaraciones que el front dejó abiertas para el backend). Las decide Ismael con el equipo de Cubicador y Cotifly. | NEXO-B11 |

---

## 2. Mapa de impactos entre sistemas

Lectura: **A** = hay que cambiar algo · **R** = riesgo si no se coordina · **—** = no le afecta.

### 2.1 Cambios de Nexo y su efecto

| Cambio | Nexo back | Nexo front | Cotifly back | Cotifly front | Cubicador | Datos |
|---|---|---|---|---|---|---|
| Rechazo de línea | A | A | A | A | — | A (columnas nuevas) |
| Ajuste manual con plaza | A | A | — | — | — | — |
| Roles (AdminBiblioteca) | A | A | R (solo textos de error) | — | — | A (script de usuarios) |
| Diccionario de vehículos | A | — | — (hoy ignora el vehículo) | — | — | A (tabla y carga) |
| Unidad de peso kg/t | — | A | — | A (su propio importador) | — | — |
| Desglose en la Mesa | — | A | — | — | — | — |
| Lista de empaque/posiciones | — | A | — | — | — | — |
| Peso con tara | R (solo documentar) | A | A (acepta y manda el peso con tara) | A (campo y nota) | R (capacidad del camión) | — |
| Ganada/perdida oculto al Comercial | — | A | — | — | — | — |
| Filtros SICE-TAC fuera | — | A | — | — | — | — |
| Encabezados fijos y barra doble | — | A | — | — | — | — |
| Aviso de SICE-TAC desactualizada | A (exponer campos) | A | A (opcional) | — | — | — |

### 2.2 Qué usa Cotifly de Nexo (verificado por el chat de Cotifly)

Cotifly **no usa llave ni token con Nexo**: manda el encabezado `X-Portal-Usuario-Id` con el usuario del comercial, y Nexo resuelve sus roles. Cotifly no envía ni mapea roles de Nexo.

Endpoints que consume: `POST /api/captura-solicitudes`, `POST /api/captura-solicitudes/analizar-excel`, `GET /api/captura-solicitudes/clientes`, `/municipios` y `/ciudades`, `GET /api/catalogos/{tipos-vehiculo, clases-viaje, clases-contenedor, lineas-negocio}`, `GET /api/solicitudes` y `GET /api/solicitudes/{id}` (polling), `POST /api/cotizaciones`, `GET /api/cotizaciones/{id}`, `POST /api/cotizaciones/{id}/conexos`, `POST /api/cotizaciones/{id}/resultado` y `GET /api/servicios-conexos?soloActivos=true`.

**Lo crítico:** el rol **Comercial** de Nexo debe conservar permisos sobre captura, solicitudes, cotizaciones, conexos y resultado. Si pierde alguno, Cotifly recibe 403 (`NEXO_SIN_PERMISO`).

Otros datos que importan:
- Cotifly **no lee la fuente del flete** ni el motivo pendiente.
- Lee `valorFlete`, `valorAprovisionamiento` y `valorRetornoContenedor` por separado, pero el levante lo arma con su **propia copia** (lo manda como `valorAprovisionamiento` en la captura).
- Con la bandera `Nexo__EnviarCodigosCubicador` en verdadero, manda códigos del Cubicador (2S2, FURGON…) que en Nexo nacen en cuarentena. Con la bandera apagada (valor por defecto), no manda vehículo.
- Una línea en consulta completa se ve en Cotifly como «pendiente de costeo». Cotifly sigue consultando hasta 72 horas y luego la pasa a «costeo vencido».

### 2.3 Orden de despliegue del cambio de roles

El front toma el nombre del rol de `GET /api/sesion/actual`, sin traducción. Si cambia solo uno de los dos lados, **todos los usuarios de ese rol pierden acceso**. Propuesta:
1. El front acepta durante unos días los dos nombres (`CoordinadorFletes` y `AdminBiblioteca`).
2. Se despliega el backend con el nombre nuevo y se corre el script de usuarios.
3. Se verifica con un usuario real del rol.
4. Se retira el nombre viejo del front.

### 2.4 Qué usa Nexo del Cubicador

- Nexo llama al Cubicador por `/api/cubicacion` (cubicación rápida) y sus endpoints de planes de cargue. El Cubicador autentica con una llave por sistema consumidor (esquema Bearer); no depende de los roles de Nexo.
- Cotifly también llama al Cubicador directamente, con su propia llave.
- El Cubicador está en la carpeta `cubicador/` del repo NEXO. Sus códigos de vehículo (2L2, C2, C3, 2S2, 3S3) **no son los de Nexo** (2, 2L3, 2S2…). Esa diferencia es la razón por la que los códigos que manda Cotifly caen en cuarentena y el diccionario necesita apuntar a los códigos de Nexo.
- El diccionario de alias es un CSV crudo (`cubicador/database/datos_raw/diccionario_configuraciones_vehiculo_RAW.csv`). No es una tabla del Cubicador.
- **No hay issues de código para el Cubicador.** Sus impactos están en las filas de la matriz anterior: peso con tara y los endpoints de plan de cargue por rol.

---

## 3. Repo NEXO — Backend (Ismael)

### NEXO-B1 — Rechazar una línea con motivo
Labels: `nexo`, `sprint-5`, `backend` · Depende de: DATA-1 · Esfuerzo: 12 a 18 horas
```
Contexto (verificado)
- La línea de solicitud tiene EstadoLinea { PorCostear, AutoResuelta, Revisada } (Enums.cs). Hoy no existe un estado de rechazo por línea. Solo hay rechazo de la solicitud completa (POST /api/solicitudes/{id}/rechazar, estado Descartada).
- clsEntregarCotizacionCommand exige que TODAS las líneas estén AutoResuelta o Revisada (COTIZACION_CON_LINEAS_PENDIENTES) y al menos una línea de cotización (clsCotizacionSinFleteException si Lineas.Count == 0).
- El Excel de Nexo (clsExportadorCotizacionExcelClosedXml) se arma desde las líneas de la COTIZACIÓN. Una línea rechazada no tiene fila de cotización, así que no saldría.
- GET /api/solicitudes/{id} (clsLineaCosteoDto) no trae estado ni motivo por línea. Es el contrato que lee Cotifly.

Qué hacer
1. Estado nuevo EstadoLinea.Rechazada, con MotivoRechazo (obligatorio, hasta 500), RechazadaPor y RechazadaAt en clsLineaSolicitud.
2. Endpoints, solo roles AdminTarifas y Admin:
   - POST /api/cotizaciones/{id}/lineas/{lineaSolicitudId}/rechazo (cuerpo: motivo).
   - DELETE /api/cotizaciones/{id}/lineas/{lineaSolicitudId}/rechazo (deshacer). Solo si la cotización NO está Entregada (409 en otro caso).
3. Una línea rechazada cuenta como resuelta para Entregar. Si TODAS están rechazadas, se permite entregar (hoy lo impide la regla de al menos un flete).
4. No suma al total (clsCotizacionCalculos).
5. Excel: la línea rechazada aparece como "Sin costeo", sin precio y sin motivo.
6. Correo de entrega al comercial: incluir los motivos de cada línea rechazada.
7. Exponer estado y motivo por línea en el API (ver NEXO-B6).
8. Métricas: una línea rechazada no cuenta como "por costear" en la proporción de líneas autocosteadas (clsMetricasAdapter).

Decisiones ya tomadas: sin motivo en PDF/Excel; deshacer solo en la Mesa; todas rechazadas se envía igual al comercial con los motivos.
Decisión D1: si TODAS las líneas están rechazadas, se BLOQUEA la generación del PDF (el de Nexo lo arma el front y el de Cotifly su backend). El Excel de Nexo sí sale, con todas las líneas en "Sin costeo".

Impacto en otros sistemas
- Cotifly back: debe leer el estado y no dejar la cotización en "pendiente" (CTF-B1).
- Cotifly front: muestra el estado (CTF-F2).
- Front de Nexo: botón y vistas (ADM-F4).
- Cubicador: ninguno.

Checklist
- [ ] Estado y columnas (con DATA-1)
- [ ] Endpoints de rechazar y deshacer, con roles
- [ ] Entregar permitido con líneas rechazadas y con todas rechazadas
- [ ] Total sin las rechazadas
- [ ] Excel con "Sin costeo"
- [ ] Correo con motivos
- [ ] Métricas
- [ ] Pruebas: rechazar, deshacer, deshacer tras entregar (409), entregar con todas rechazadas, total, Excel
- [ ] Actualizar COTIFLY-CONTRATO.md y CONTRATO.md

CRITERIOS DE ACEPTACION
- [ ] Un AdminTarifas puede rechazar una línea con motivo; sin motivo responde error de validación.
- [ ] Comercial y los demás roles reciben 403 al intentar rechazar.
- [ ] La cotización con líneas rechazadas se puede entregar, y también con todas rechazadas.
- [ ] El total no suma las líneas rechazadas.
- [ ] Deshacer funciona mientras la cotización no está entregada; entregada responde 409.
- [ ] El Excel muestra la línea como «Sin costeo», sin precio y sin motivo.
- [ ] GET /api/solicitudes/{id} devuelve estado y motivo de cada línea.
- [ ] El correo de entrega al comercial incluye los motivos.
- [ ] Las pruebas automáticas están en verde.
```

### NEXO-B2 — Editar un flete de plaza vigente
Labels: `nexo`, `sprint-5`, `backend` · Esfuerzo: 8 a 14 horas
```
Contexto (verificado)
- clsEditarLineaCotizacionCommand solo edita fletes de fuente Manual. Cualquier otra fuente responde 409 FLETE_NO_EDITABLE ("Reemplácelo eligiendo otro candidato").
- La plaza se guarda con FuenteFlete.Tarifario. Las fuentes son Sicetac, Historica, Manual y Tarifario.
- clsAgregarLineaCotizacionCommand acepta cualquier fuente al agregar.
- La Mesa recibe ModoDecision (SoloSicetac, PlazaVigenteMayor, ConsultaCompleta) en las propuestas, pero clsLineaCosteoDto NO dice si la ruta tiene regla Solo-SICE.

Qué hacer
1. Permitir editar fletes de cualquier fuente (Manual, Tarifario o plaza, Sicetac e Historica), mientras la ruta NO tenga regla Solo-SICE.
2. Bloquear la edición cuando la ruta y el vehículo de la línea tienen regla Solo-SICE: nuevo código 409 FLETE_SOLO_SICETAC. El comando debe consultar la regla (clave por código DANE, como hace clsPropuestaTarifaAdapter).
3. Decisión D4: al editar, la fuente pasa a Manual y se registra en la auditoría el valor y la fuente anteriores.
4. NINGÚN valor puede quedar por debajo del piso SICE-TAC (FLETE_BAJO_PISO_SICETAC). Hoy esa validación se salta cuando no se encuentra piso o SICE-TAC falla: en la edición hay que cerrar ese hueco (ver sección 9).
4. Agregar a clsLineaCosteoDto un campo tieneReglaSoloSicetac para que el front sepa cuándo ofrecer la edición.
5. Para "actualizar la tarifa de plaza o guardarla como nueva": no cambia el backend; usa los endpoints de Tarifas Plaza que ya existen (GET, POST, PUT). El front necesita buscar la tarifa por ruta: hoy solo hay listado completo.

Decisión D4 (tomada): ver los puntos 3 y 4 de arriba.

Impacto: Cotifly no lee la fuente del flete, así que no le afecta. El front de Nexo no tiene hoy ningún flujo de edición (ver ADM-F3).

Checklist
- [ ] Editar fuente Tarifario
- [ ] Bloqueo con regla Solo-SICE (FLETE_SOLO_SICETAC)
- [ ] Campo tieneReglaSoloSicetac en el DTO
- [ ] Pruebas: con plaza sin regla (pasa), con regla (409), bajo el piso (400/422 existente)

CRITERIOS DE ACEPTACION
- [ ] Un flete de plaza vigente (y de las demás fuentes) se puede editar si la ruta no tiene regla Solo-SICE.
- [ ] Con regla Solo-SICE responde 409 FLETE_SOLO_SICETAC.
- [ ] Ningún valor puede quedar por debajo del piso SICE-TAC al editar; si no se puede verificar el piso, no se guarda sin aviso.
- [ ] Tras editar, la fuente queda en Manual y la auditoría guarda el valor y la fuente anteriores.
- [ ] El DTO de la línea trae tieneReglaSoloSicetac.
- [ ] Pruebas: plaza sin regla, con regla y bajo el piso.
```

### NEXO-B3 — Roles: CoordinadorFletes pasa a AdminBiblioteca
Labels: `nexo`, `sprint-5`, `backend` · Esfuerzo: 2 a 3 días · El orden de despliegue (sección 2.3) lo decide Ismael
```
Contexto (verificado)
- RolUsuario es CoordinadorFletes, LiderProceso, ServiciosLogisticos, Admin, Comercial, AdminTarifas (Enums.cs). El rol se guarda como TEXTO en db_NEXO.tbl_usuarios.Rol y tbl_mapeo_rol_portal.Rol (sin restricción CHECK).
- CoordinadorFletes aparece en 15 archivos de src y 12 de tests. Las filas de mapeo del portal están sembradas en clsNexoDbContext (HasData).
- Hoy CoordinadorFletes (el "Administrador de Fletes" en el código) puede: crear solicitud, cargar líneas, recalcular puntaje, rechazar solicitud, cotizaciones (crear, aplicar propuestas, agregar/editar fletes, conexos, entregar), planes de cargue del Cubicador, ajustar vigencia de plaza y cubicar.

Nuevo modelo
- AdminBiblioteca (antes CoordinadorFletes): Biblioteca, Tarifas Plaza, Reglas Solo-SICE y ajuste de vigencia. SIN Bandeja, Mesa de Costeo ni cotizaciones.
- AdminTarifas, Admin y Comercial: igual que hoy.

Qué hacer
1. Renombrar el valor del enumerado y las referencias (src y tests).
2. Quitar AdminBiblioteca de los [Authorize] de Cotizaciones, Solicitudes y PlanDeCargue. Mantenerlo en TarifasPlaza, ReglasSoloSicetac, RangosPeso, AjustesVigenciaPlaza y servicios conexos (consulta).
3. clsSesionController: actualizar la lista de permisos del rol.
4. Alertas (AlertasDeNegocioCommand): "línea que no pudo costearse" hoy va al CoordinadorFletes; pasa a AdminTarifas. "Tarifa vencida" ya llega a AdminTarifas y ServiciosLogisticos; agregar AdminBiblioteca si corresponde.
5. clsResolucionRolPortal: orden de precedencia de roles.
6. Actualizar docs/documentacion/ROLES_Y_PERMISOS.md y CONTRATO.md.
7. Pruebas de roles por endpoint (patrón de clsCubicacionRolesTests).

Decisión D5 (tomada)
- POST /api/solicitudes, /lineas:bulk y /recalcular-score eran "CoordinadorFletes,Admin": pasan a SOLO Admin.
- AdminBiblioteca CONSERVA el permiso de cubicación en el backend (/api/cubicacion). El front no le muestra el Cubicador.
- AdminTarifas debe poder usar el Cubicador completo: ver NEXO-B7 (los planes de cargue hoy no lo incluyen).
- Rechazar tarifas con motivo y "Simular": solo AdminTarifas y Admin.
- Los endpoints de escritura de planes de cargue y POST /api/vehiculos que tenía CoordinadorFletes (Comercial,CoordinadorFletes,Admin) quedan sin AdminBiblioteca. Los de lectura no cambian (solo exigen sesión).

Impacto en otros sistemas
- Cotifly: no usa roles de Nexo; solo exige que Comercial conserve sus permisos. Dos textos de error de Cotifly mencionan "CoordinadorFletes" (clsServiciosConexosNexo, clsCosteoNexo): CTF-B3.
- Front de Nexo: ver ADM-F9 y la sección 2.3 (orden de despliegue).
- Cubicador: no usa roles de Nexo.

Checklist
- [ ] Renombrar y ajustar [Authorize]
- [ ] Alertas
- [ ] Permisos de sesión
- [ ] Script de usuarios (DATA-3)
- [ ] Pruebas de roles por endpoint
- [ ] Documentación de roles
- [ ] Desplegar según el orden de la sección 2.3

CRITERIOS DE ACEPTACION
- [ ] Existe el rol AdminBiblioteca y ya no queda CoordinadorFletes en el código, las pruebas ni la documentación.
- [ ] AdminBiblioteca entra a Biblioteca, Tarifas Plaza, Reglas Solo-SICE, rangos de peso, vigencia y /api/cubicacion, y recibe 403 en Bandeja, Mesa, cotizaciones y solicitudes.
- [ ] Crear solicitud, cargar líneas en lote y recalcular puntaje responden solo a Admin.
- [ ] La alerta de «línea que no pudo costearse» llega a AdminTarifas.
- [ ] El Comercial conserva captura, solicitudes, cotizaciones, conexos y resultado (Cotifly sigue funcionando).
- [ ] Pruebas de roles por endpoint en verde y script de usuarios probado en una base de pruebas.
```

### NEXO-B4 — Diccionario de alias de vehículos
Labels: `nexo`, `sprint-5`, `backend` · Depende de: DATA-2 · Esfuerzo: 2 a 3 días
```
Contexto (verificado)
- ResolverVehiculo (clsResolucionDeCatalogos) busca una configuración de vehículo por clave normalizada (tipo + carrocería). Si no existe, crea una fila nueva en CUARENTENA (EsCatalogoOficial = false) y un humano decide después.
- Normalizar ya ignora tildes y unifica "_" y "-" (S5-HU-20.5). A propósito NO unifica formas como "T.MULA" y "Tractomula".
- El Excel de licitación se analiza en el backend (POST /api/captura-solicitudes/analizar-excel) solo para devolver hojas, encabezados y filas. El mapeo de columnas y la lectura de valores ocurren en el front.
- Los códigos del Cubicador (2S2, FURGON) que manda Cotifly con la bandera encendida también nacen en cuarentena.
- Fuente de alias: cubicador/database/datos_raw/diccionario_configuraciones_vehiculo_RAW.csv (7 columnas canónicas, unos 19 alias por columna). No está en ninguna tabla.

Qué hacer
1. Tabla de alias en la base de Nexo (DATA-2): alias, alias normalizado, configuración de vehículo de destino, activo.
2. ResolverVehiculo busca primero el alias y, si lo encuentra, usa la configuración oficial. Si no, sigue a cuarentena.
3. Endpoint de lectura (GET /api/catalogos/alias-vehiculos) para quien lo necesite. Cotifly hoy no lo usa.
4. Endpoint o proceso para agregar alias desde la bandeja de normalización (ver S5-HU-18.4).
5. Pruebas: "TB", "T.B", "Turbo" y "2L2" resuelven a la misma configuración; un alias desconocido va a cuarentena.

Decisión D6: el mapeo de cada columna del archivo a un código de Nexo se define dentro de DATA-2.

Impacto
- Cotifly: nada que hacer ahora (ignora el vehículo). Podría pedir el endpoint más adelante.
- Front de Nexo: el campo de vehículo en el mapeo del Excel ya manda texto; el backend resuelve.
- Cubicador: el archivo fuente sigue donde está; no se modifica.

CRITERIOS DE ACEPTACION
- [ ] «TB», «T.B», «Turbo» y «2L2» resuelven a la misma configuración de vehículo.
- [ ] Un alias desconocido sigue yendo a cuarentena.
- [ ] Existe el endpoint de lectura de alias.
- [ ] Los códigos del Cubicador que manda Cotifly resuelven con el diccionario cuando hay alias.
- [ ] Las pruebas están en verde.
```

### NEXO-B5 — Peso con tara: confirmar el efecto en el backend
Labels: `nexo`, `sprint-5`, `backend` · Esfuerzo: 2 a 4 horas
```
Contexto
- El peso por línea (Peso) se usa en Nexo para elegir el rango de peso de la tarifa de plaza (PesoEnRango en clsTarifarioSapAdapter) y se envía al Cubicador.
- El backend no valida la suma de posiciones contra el peso total (solo lo hace el front).
- Cotifly manda el peso de la mercancía SIN tara.
- El Cubicador calcula la capacidad real de un camión como capacidad menos tara.

Qué hacer
1. Decisión D2 (tomada): el peso total de cada línea incluye la tara, también en las solicitudes que llegan de Cotifly.
2. Decisión D2b: la tara la escribe el Comercial en el peso de cada línea. El backend NO la suma. Dejarlo documentado en COTIFLY-CONTRATO.md (el "peso" de cada línea incluye la tara).
3. Revisar el efecto sobre el rango de peso de la plaza y sobre la recomendación de vehículo (el Cubicador calcula capacidad menos tara y hoy la tara de los vehículos está en 0) y dejar por escrito qué peso usa cada uno.
4. No requiere cambios de código si la revisión no encuentra un problema.

Impacto: Cotifly (envía neto), Cubicador (capacidad menos tara), front de Nexo (ADM-F8).

CRITERIOS DE ACEPTACION
- [ ] COTIFLY-CONTRATO.md dice que el peso de la línea incluye la tara y que Nexo no la suma.
- [ ] Queda por escrito qué peso usan el rango de la plaza y la recomendación de vehículo.
- [ ] Si la revisión encuentra un problema, se abre una issue aparte.
```

### NEXO-B6 — Exponer estado y motivo de cada línea en el API
Labels: `nexo`, `sprint-5`, `backend` · Esfuerzo: 4 a 6 horas
```
Contexto (verificado)
- clsLineaCosteoDto (GET /api/solicitudes/{id}) no trae el estado de la línea (EstadoLinea) ni su MotivoPendiente, aunque la columna MotivoPendiente existe en tbl_lineas_solicitud.
- clsSolicitudCosteoDto ya trae AvisoSicetac (cuando el período de SICE-TAC no es el del mes actual).
- Cotifly ve una línea en consulta completa solo como "pendiente de costeo" y no sabe por qué.

Qué hacer
1. Agregar a clsLineaCosteoDto: estadoLinea, motivoPendiente y (con NEXO-B1) motivoRechazo. Los campos nuevos van al final y opcionales, para no romper a los consumidores.
2. Documentar los campos nuevos en COTIFLY-CONTRATO.md.

Impacto: Cotifly los necesita para el rechazo (CTF-B1) y podría mostrar el motivo pendiente. El front de Nexo los usa en ADM-F4 y ADM-F10.

CRITERIOS DE ACEPTACION
- [ ] GET /api/solicitudes/{id} devuelve por línea estadoLinea y motivoPendiente (y motivoRechazo cuando exista el rechazo).
- [ ] Los campos son opcionales y van al final: Cotifly sigue leyendo sin cambios.
- [ ] El contrato queda actualizado.
```

### NEXO-B7 — AdminTarifas debe poder usar el Cubicador completo
Labels: `nexo`, `sprint-5`, `backend`, `bug` · Esfuerzo: 2 a 4 horas · Conviene hacerla antes o junto con NEXO-B3
```
Qué se pidió
AdminTarifas debe poder usar el Cubicador completo. Sara entendía que el permiso ya estaba corregido; no lo está.

Qué hay hoy (verificado en la rama QA más reciente, 16 de octubre)
- El PR #301 agregó AdminTarifas SOLO a POST y GET /api/cubicacion (cubicar una línea). El PR #303 agregó pruebas de roles de ese endpoint, y solo de ese.
- Los endpoints de ESCRITURA de planes de cargue siguen siendo "Comercial,CoordinadorFletes,Admin". AdminTarifas NO está:
  - clsPlanesCargueController (api/planes-cargue):
    - POST  api/planes-cargue                              (línea 74)
    - POST  api/planes-cargue/demo                         (línea 91)
    - POST  api/planes-cargue/desde-linea/{lineaId}        (línea 106)
    - POST  api/planes-cargue/{id}/optimizar               (línea 118)
    - POST  api/planes-cargue/{id}/items                   (línea 144)
    - POST  api/planes-cargue/{id}/items/importar          (línea 161)
    - DELETE api/planes-cargue/{id}/items/{itemId}         (línea 175)
    - DELETE api/planes-cargue/{id}                        (línea 188)
    - PATCH api/planes-cargue/{id}/colocaciones/{colocacionId} (línea 200)
  - clsVehiculosController (api/vehiculos): POST api/vehiculos (línea 61). DELETE api/vehiculos/{id} es solo Admin.
- Los endpoints de LECTURA (GET de planes, resultado, flota, métricas, no-cargadas, colocaciones, exportar, y GET api/vehiculos) solo exigen sesión: AdminTarifas sí puede leer.
- Resultado: AdminTarifas puede ver un plan de cargue, pero no puede crear uno, optimizarlo, editar ítems ni mover colocaciones.
- El front le muestra la pantalla del Cubicador (capacidad verCubicador). No verificado: qué endpoints llama esa pantalla; si usa estos de escritura, recibe 403.

Qué hacer
1. Agregar AdminTarifas a los 10 endpoints de escritura de la lista (9 de planes de cargue y POST api/vehiculos).
2. Dejar DELETE api/vehiculos/{id} como está (solo Admin).
3. Pruebas de roles por endpoint (patrón de clsCubicacionRolesTests): AdminTarifas pasa; ServiciosLogisticos y LiderProceso reciben 403; sin sesión, 401.
4. Esta issue es independiente del renombre de roles (NEXO-B3): después de ese cambio, AdminBiblioteca deja de estar en estos endpoints (decisión D5) y AdminTarifas queda como el rol de costeo que cubica.

Impacto en otros sistemas
- Front de Nexo: no cambia. Verificar con la pantalla del Cubicador de un usuario AdminTarifas que ya no recibe 403.
- Cotifly: no usa estos endpoints de Nexo para planes de cargue.
- Cubicador: sin cambios; sigue autenticando por su llave de sistema.

Checklist
- [ ] Agregar AdminTarifas a los 10 endpoints
- [ ] Pruebas de roles por endpoint
- [ ] Verificar la pantalla del Cubicador con un usuario AdminTarifas
- [ ] Actualizar docs/documentacion/ROLES_Y_PERMISOS.md

CRITERIOS DE ACEPTACION
- [ ] AdminTarifas puede crear, optimizar y editar planes de cargue, mover colocaciones y crear vehículos (los 10 endpoints).
- [ ] DELETE de vehículos sigue solo para Admin.
- [ ] ServiciosLogisticos y LiderProceso reciben 403; sin sesión, 401.
- [ ] La pantalla del Cubicador funciona completa con un usuario AdminTarifas, sin 403.
- [ ] Pruebas de roles por endpoint en verde.
```

### NEXO-B8 — La línea de costeo debe devolver línea de negocio, clase de viaje y clase de contenedor
Labels: `nexo`, `sprint-5`, `backend` · Esfuerzo: 2 a 4 horas · Se entrega junto con NEXO-B6 (mismo objeto)
```
Origen: informe del front de Nexo (bloque 2). Desbloquea front#345 (chips en la Mesa) y front#347 (prellenar la tarifa de plaza).

Verificado en el código
- clsLineaCosteoDto (SolicitudDtos.cs) no trae lineaNegocio, claseViaje ni claseContenedor. Los datos SÍ existen en la entidad clsLineaSolicitud (LineaNegocio, ClaseViaje, ClaseContenedor) y el único rastro en el DTO es un comentario sobre ValorMercancia.
- Tampoco los trae clsLineaSolicitudDto.

Qué hacer
1. Agregar a clsLineaCosteoDto, al final y opcionales: lineaNegocio, claseViaje y claseContenedor (texto legible, minúscula inicial, null si la línea no los tiene). claseContenedor en null salvo cuando la clase de viaje es Contenedor.
2. Opcional en el mismo DTO: origenMunicipioId y destinoMunicipioId (número o null). El front hoy deduce la ciudad leyendo el texto de la ruta y falla con nombres repetidos (68 en el catálogo, p. ej. "Armenia") y con nombres distintos al catálogo ("Bogota" contra "BOGOTA, D.C.").
3. Documentar en COTIFLY-CONTRATO.md y CONTRATO.md.

Impacto: Cotifly lee este DTO por polling; los campos nuevos son opcionales y no lo rompen. Front de Nexo: lo toma solo cuando llegue (ya está preparado).

Checklist
- [ ] Campos nuevos en el DTO
- [ ] ids de municipio (opcional)
- [ ] Pruebas: línea con y sin clase de contenedor
- [ ] Contrato actualizado

CRITERIOS DE ACEPTACION
- [ ] Cada línea de GET /api/solicitudes/{id} trae lineaNegocio, claseViaje y claseContenedor (texto o null; claseContenedor null salvo clase de viaje Contenedor).
- [ ] Los campos nuevos no rompen a Cotifly ni al front actual.
- [ ] Pruebas: línea con y sin clase de contenedor.
- [ ] El contrato queda actualizado.
```

### NEXO-B9 — Ajustes pequeños del API y de los textos
Labels: `nexo`, `sprint-5`, `backend` · Esfuerzo: 8 a 12 horas · Origen: informe del front de Nexo (bloques 1, 3, 8, 11 y 12)
```
1. Textos de las justificaciones (bug, 1 a 2 h). clsReglaDeCosteo.cs formatea con {valor:C0} (líneas 82, 83, 95, 121 y 184) y sale el símbolo "¤" en vez de "$"; lo mismo en el error 400 FLETE_BAJO_PISO_SICETAC. Además clsReglaDeCosteo.cs:175 y clsPropuestaTarifaAdapter.cs:313 empiezan con "clsRuta marcada como SOLO SICE-TAC…" (resto del renombrado de clases con prefijo cls). Hacer: formatear con es-CO o "$" explícito y cambiar "clsRuta" por "La ruta". Revisar las pruebas que comparan esos textos. Cotifly no lee la justificación; el front la muestra tal cual.

2. Paginar el histórico SAP (3 a 5 h). GET /api/biblioteca-sap-historico solo acepta q, lineaNegocio, tipoVehiculo, tipoCarroseria, claseViaje y top (1 a 500); no hay page. Hacer: aceptar page (desde 1), top como tamaño de página, total sin paginar y orden estable. El front ya detecta si el backend pagina y no hay que tocarlo. (Según el informe del front el histórico tiene 25.903 filas; no lo verifiqué.) Desbloquea front#301.

3. 404 propio sin recomendación (2 a 3 h). clsCubicacionRapidaAdapter (línea 115) convierte cualquier error del Cubicador, incluso un 404 "No hay recomendaciones…", en 503 CUBICADOR_ERROR, y el front no distingue "no hay recomendación" de una caída real. Hacer: 404 con código propio (p. ej. RECOMENDACION_NO_ENCONTRADA) y 503 solo para fallas reales. Desbloquea front#286 y front#287.

4. Detalle opcional en tarifas de plaza (1 a 2 h). clsTarifaPlazaValidador rechaza un detalle vacío (400 TARIFA_PLAZA_INVALIDA). Decisión de Sara: al guardar como plaza desde la Mesa los detalles son opcionales; en la pantalla de Tarifas Plaza sí se exigen. Hacer: aceptar detallesTarifa vacío o nulo en POST y PUT /api/tarifas-plaza; el front quita su texto de relleno ("Guardada desde la Mesa de Costeo."). Desbloquea front#313.

Checklist
- [ ] 1 Textos y moneda, con pruebas
- [ ] 2 Paginación del histórico
- [ ] 3 404 propio sin recomendación
- [ ] 4 Detalle opcional en tarifas de plaza

CRITERIOS DE ACEPTACION
- [ ] Ninguna justificación ni mensaje de piso muestra «¤»; sale «$» con formato colombiano, y el texto de Solo SICE-TAC empieza con «La ruta».
- [ ] GET /api/biblioteca-sap-historico con page=2 devuelve filas distintas a page=1, y total sigue siendo el total sin paginar.
- [ ] Una línea sin recomendación responde 404 con código propio; el 503 queda solo para fallas reales del Cubicador.
- [ ] POST y PUT /api/tarifas-plaza aceptan detallesTarifa vacío; la pantalla de Tarifas Plaza lo sigue exigiendo.
- [ ] Las pruebas actualizadas están en verde.
```

### NEXO-B10 — Cambiar el vehículo de una línea, y bandeja de normalización de vehículos
Labels: `nexo`, `sprint-5`, `backend` · Depende de: D9, DATA-2 · Esfuerzo: 2 a 4 días (el informe del front estima 8 a 14 horas solo para cambiar el vehículo)
```
Origen: informe del front (bloques 5 y 6). Desbloquea front#343 (cambiar el vehículo en la Mesa) y front#344 (bandeja de normalización).

Verificado en el código
- No existe forma de cambiar el vehículo de una línea ya creada. ConfigVehiculoId solo se escribe al crearla (captura, lineas:bulk y resolver-catalogos, que solo resuelve texto a ids). PUT /api/cotizaciones/{id}/lineas/{lineaSolicitudId} solo acepta valorFlete y valorRetornoContenedor.
- Ningún controlador menciona EsCatalogoOficial ni cuarentena. NEXO#276 (S5-HU-18.4) dejó la columna y que ResolverVehiculo cree las filas nuevas en cuarentena, y dijo que la pantalla de normalización era "del portal", pero no dejó ninguna ruta.
- GET /api/catalogos/tipos-vehiculo trae esCatalogoOficial, pero lee el catálogo SAP (6 filas), no las configuraciones en cuarentena.

Qué hacer (decisión D9, recomendada)
1. Endpoint para cambiar la configuración de vehículo de una línea (mientras la cotización no esté entregada), con roles AdminTarifas y Admin. Al cambiarlo se recalcula la propuesta y el piso, y sigue valiendo que ningún valor quede bajo SICE-TAC.
2. Rutas de la bandeja de normalización: listar las configuraciones con EsCatalogoOficial = false (id, tipo de vehículo, carrocería), mapear una a un código canónico y descartarla. Lo mapeado o descartado sale de la lista.
3. Mapear una configuración en cuarentena puede crear un alias en el diccionario de NEXO-B4, para que no vuelva a caer en cuarentena.

Impacto: Cotifly manda códigos del Cubicador con una bandera apagada por defecto; con la bandera encendida llegan en cuarentena y esta bandeja los resolvería. Cubicador: sin cambios.

CRITERIOS DE ACEPTACION
- [ ] Se puede cambiar el vehículo de una línea con la cotización sin entregar; entregada responde 409.
- [ ] Solo AdminTarifas y Admin pueden hacerlo.
- [ ] Al cambiarlo se recalculan propuesta y piso, y ningún valor queda por debajo de SICE-TAC.
- [ ] La bandeja lista las configuraciones en cuarentena; mapear o descartar una la saca de la lista.
- [ ] Mapear una crea un alias que evita que vuelva a caer en cuarentena.
```

### NEXO-B11 — Aclaraciones pendientes del contrato de captura
Labels: `nexo`, `sprint-5`, `backend`, `investigación` · Decide Ismael (D10 y D11) · Esfuerzo: 3 a 6 horas · Origen: informe del front de Nexo (bloques 4 y 7)
```
Sin código hasta tener las respuestas; todo queda en CONTRATO.md y se avisa al front.

A. grupoRutaId (front#286). CONTRATO.md §3.3 dice que lo pone siempre Cotifly y que es null si la línea nace en Nexo, pero el backend lo acepta en la captura (CapturaRequests.cs:119) y la respuesta del backend dice que agrupa las líneas de una carga repartida en varios camiones. Si el front de Nexo crea una línea por camión, ¿manda un GUID compartido o va null?

B. Posiciones por camión (front#286). Cada camión de la recomendación trae mercancías como cantidad, código, pesoKg y volumen, sin medidas, y la línea de captura exige largo, ancho y alto. Al partir una carga en varios camiones, ¿cómo arma el front las posiciones de cada línea? Si dos posiciones tienen el mismo tipo de mercancía, la cantidad de un camión no dice a cuál corresponde.

C. Clase de contenedor de Extradimensionado (front#307, decisión D10). El backend la guarda como texto libre (ParseClaseContenedor) y no valida. El front pide CNT20.FT, CNT20.OP, CNT40.FT y CNT40.OP, que no existen en el catálogo real (CNT20.IT, RE, ST y CNT40.HC, RE). Elegir: catálogo propio expuesto por endpoint, usar el existente, u opcional sin validar (recomendado). Riesgo: si un día se valida contra el catálogo, los nombres provisionales se rechazan.

Checklist
- [ ] A respondida y documentada
- [ ] B respondida y documentada
- [ ] C decidida y documentada

CRITERIOS DE ACEPTACION
- [ ] Las preguntas A, B y C tienen respuesta escrita en CONTRATO.md.
- [ ] El front fue avisado (front#286 y front#307).
- [ ] La decisión sobre la clase de contenedor de Extradimensionado quedó aplicada o documentada.
```

---

## 4. Repo administracion — Front de Nexo (Juan Pablo)

Datos tomados del chat de ese repo (rama QA, árbol limpio). Yo no leí este código.

### ADM-F1 — Comercial no marca Ganada ni Perdida desde Nexo
Labels: `nexo`, `frontend` · Esfuerzo: 2 a 3 horas
```
Hoy
- Ambos sitios chequean solo access.puede('marcarGanadaPerdida'), que Comercial tiene: mesa-costeo-page.component.html:35-37 y cotizacion-resumen-page.component.html:75-79.
- NexoAccessService no expone el rol, solo puede().

Hacer
- Método nuevo puedeAccionarResultado() que combine el permiso con rol !== 'Comercial', usado en los dos sitios. No se quita el permiso de la matriz.
- El backend no cambia. Cotifly sigue marcando el resultado por API.

Decisión D7: la columna y el filtro "Resultado" de la Bandeja NO existen en QA (eran un prototipo del sandbox) y NO se van a hacer. Esta issue solo oculta los botones.

Checklist
- [ ] Botones ocultos al Comercial en la Mesa y en el Resumen
- [ ] Admin sigue marcando

CRITERIOS DE ACEPTACION
- [ ] Comercial no ve los botones Ganada ni Perdida en la Mesa ni en el Resumen.
- [ ] Admin sigue viéndolos y marcando.
- [ ] No se quitó el permiso de la matriz ni se tocó el backend.
```

### ADM-F2 — Quitar tres filtros de la matriz SICE-TAC
Labels: `nexo`, `frontend` · Esfuerzo: 1 a 2 horas
```
Hoy: biblioteca-page.component.html:28-49 (3 selectores), las propiedades sicetacTipoCarga, sicetacCondicion y sicetacViaEstandar en el .ts, y el modelo FiltrosMatrizSicetac.
Hacer: borrar los tres bloques y sus referencias. Las columnas de la tabla se quedan. No toca el backend ni otras pantallas.
Por verificar: que el backend responda igual sin esos parámetros (son opcionales).

CRITERIOS DE ACEPTACION
- [ ] Los tres filtros (tipo de carga, condición y vía estándar) no aparecen.
- [ ] La búsqueda sigue funcionando con los demás filtros y las columnas de la tabla se mantienen.
```

### ADM-F3 — Editar el flete de una línea (plaza vigente)
Labels: `nexo`, `frontend` · Depende de: NEXO-B2 · Esfuerzo: 6 a 10 horas
```
Hoy (hallazgo clave)
- NO existe ningún flujo de edición de flete en el front, ni siquiera para fuente Manual. Si la línea ya tiene flete, la Mesa solo muestra una tarjeta de lectura (mesa-costeo-page.component.html:253).
- La fuente viene como 'sicetac' | 'historica' | 'manual' | 'tarifario' ("Plaza" es 'tarifario' en el front).
- Existe TarifasPlazaApiService (listar, crear, editar), pero no hay búsqueda por ruta: habría que filtrar en el cliente.

Hacer
1. Botón "Editar flete" que reabre el formulario si la fuente es manual o tarifario y la línea NO tiene regla Solo-SICE (campo nuevo de NEXO-B2).
2. Mostrar el error 409 FLETE_SOLO_SICETAC con un mensaje claro.
3. Si el valor es manual, la pregunta "guardar como plaza" (HU-25 del documento 2026-10-09_HU-FRONTEND.md): si la tarifa ya existe, ofrecer actualizarla o guardarla como nueva.
4. Capacidad nueva en la matriz para AdminTarifas.

Impacto: Tarifas Plaza. No afecta a Cotifly.

CRITERIOS DE ACEPTACION
- [ ] AdminTarifas ve «Editar flete» en una línea con flete y sin regla Solo-SICE.
- [ ] Con regla Solo-SICE no aparece el botón, o se muestra el 409 con un mensaje claro.
- [ ] El error de piso se entiende.
- [ ] Con un valor manual se ofrece actualizar la tarifa de plaza o guardarla como nueva.
```

### ADM-F4 — Rechazar línea con motivo
Labels: `nexo`, `frontend` · Depende de: NEXO-B1, NEXO-B6 · Esfuerzo: 10 a 14 horas (con el backend listo)
```
Hoy
- CotizacionLineaDto solo trae valores numéricos y fuente; no hay estado por línea.
- El PDF de Nexo lo arma el FRONT (jsPDF, construirPdf, líneas 773-890). El Excel lo arma el backend.
- Comercial solo ve una tarjeta simplificada del flete (fleteActualCard).

Hacer
1. Botón "Rechazar línea" con motivo obligatorio, junto a la tarjeta del flete guardado (mesa-flete-success, líneas 254-262). Solo AdminTarifas y Admin: capacidad nueva en la matriz.
2. "Deshacer" mientras cotizacion.estado !== 'Entregada'.
3. El Comercial ve el motivo en su tarjeta.
4. PDF: la línea rechazada sale "Sin costeo", sin precio y sin motivo.
5. Si todas están rechazadas: BLOQUEAR la generación del PDF con un mensaje claro (decisión D1). El Excel sí sale.
6. Revisar Bandeja, Tablero y Resumen de cotización (no verificado en detalle).

CRITERIOS DE ACEPTACION
- [ ] Solo AdminTarifas y Admin ven «Rechazar línea»; el motivo es obligatorio.
- [ ] El Comercial ve el motivo en su tarjeta.
- [ ] «Deshacer» funciona en la Mesa y no aparece con la cotización entregada.
- [ ] En el PDF la línea sale «Sin costeo», sin precio ni motivo; con todas rechazadas el PDF se bloquea con un mensaje claro y el Excel sale.
```

### ADM-F5 — Lista de empaque y posiciones solo en Carga Suelta y Extradimensionado
Labels: `nexo`, `frontend` · Esfuerzo: 4 a 7 horas
```
Hoy
- mostrarPosiciones() y mostrarEmpaqueYPosiciones() (captura-page.component.ts:544-560) dependen de la clase de viaje. Las líneas Nacional tienen una excepción deliberada que invierte la regla pedida.
- El componente no tiene lógica de rol. verCaptura solo la tienen Comercial y Admin.
- La importación de Excel llena las posiciones sin mirar si la sección está visible (aplicarExcelALineas:690-697). Ocultar la sección no impide que el dato viaje al backend.

Hacer
- Decisión D3: la lista de empaque y las posiciones se muestran SOLO en Carga Suelta y Extradimensionado, en todas las líneas. Se quita la excepción de las líneas Nacional (la que hoy las oculta en Carga Suelta y las muestra en Cupo).
- Supuesto: Admin sigue viéndolas, porque comparte la pantalla de captura.
- Evitar que la importación de Excel llene posiciones en clases donde la sección se oculta.
Impacto: la Mesa lee linea.posiciones para cubicar y seguiría funcionando.

CRITERIOS DE ACEPTACION
- [ ] La lista de empaque y las posiciones solo se ven en Carga Suelta y Extradimensionado, en todas las líneas (incluidas Nacional).
- [ ] La importación de Excel no llena posiciones en otras clases.
- [ ] Admin sigue viéndolas.
```

### ADM-F6 — Desglose de flete, levante y retorno en la Mesa
Labels: `nexo`, `frontend` · Esfuerzo: 3 a 5 horas
```
Hoy
- El backend ya manda valorFlete, valorAprovisionamiento y valorRetornoContenedor por separado (clsCotizacionLineaDto). El front suma el total (totalLineaFlete) y los muestra como texto corrido.
Hacer
- Mostrar 3 valores con etiqueta propia en mesa-costeo-page.component.html:253-269 y, por consistencia, en cotizacion-resumen-page.component.html:38-52.
- El PDF y el Excel NO cambian (decisión 5).
Sin cambios de backend.

CRITERIOS DE ACEPTACION
- [ ] La Mesa y el Resumen muestran flete, levante y retorno, cada uno con su etiqueta.
- [ ] El PDF y el Excel no cambian.
```

### ADM-F7 — Encabezados fijos y barra de desplazamiento arriba y abajo
Labels: `nexo`, `frontend` · Esfuerzo: 14 a 20 horas
```
Hoy
- 9 tablas, sin componente compartido: Bandeja (1), Biblioteca (4), Tarifas Plaza (1), Reglas Solo-SICE (1) y Cubicador (2).
- Ninguna tiene encabezado fijo ni contenedor con altura máxima; el scroll vertical es el de toda la página. Todas tienen overflow-x: auto.

Hacer
1. Contenedor con max-height y overflow-y: auto, y position: sticky con fondo opaco en los encabezados.
2. Barra horizontal arriba y abajo: no existe en CSS puro; requiere una directiva Angular de "scroll espejado" reutilizable.
3. Aplicar a las 9 tablas y probar cada una (también en pantalla pequeña).
Desglose: directiva sticky 2-3 h, directiva de scroll doble 3-4 h, aplicar a cada tabla 1-1,5 h.

CRITERIOS DE ACEPTACION
- [ ] En cada una de las 9 tablas los encabezados quedan fijos al bajar, también en pantalla pequeña.
- [ ] Cada tabla tiene barra de desplazamiento horizontal arriba y abajo, sincronizadas.
- [ ] Los encabezados fijos no se rompen con el desplazamiento horizontal.
```

### ADM-F8 — Peso: nota de tara y quitar la validación
Labels: `nexo`, `frontend` · Esfuerzo: 1 a 2 horas
```
Hoy
- La nota de texto YA EXISTE en QA (captura-page.component.html:175, desde el 28 de septiembre). Verificar que diga que el peso incluye la tara.
- La validación contra la suma de las posiciones está en captura-page.component.ts:1054, dentro de validateLinea(), con tolerancia del 1 %. Bloquea el envío completo del formulario.
Hacer
- Borrar esa condición. Es la única comparación de este tipo en el repo.
- Decisión D2 y D2b: el peso de cada línea lo escribe el Comercial ya con la tara incluida; la regla vale también para Cotifly (ver CTF-B5). Nexo no suma nada.
Por verificar: que el backend no tenga una validación espejo (en el código de Nexo no encontré ninguna).

CRITERIOS DE ACEPTACION
- [ ] La nota del peso dice que debe incluir la tara.
- [ ] Un peso total distinto de la suma de las posiciones ya no bloquea el envío.
```

### ADM-F9 — Roles: AdminBiblioteca en el front
Labels: `nexo`, `frontend` · Depende de: NEXO-B3 · Esfuerzo: 2 a 2,5 horas
```
Hoy
- La matriz (nexo-access.models.ts) da a CoordinadorFletes 9 capacidades: verBandeja, verBiblioteca, ajustarVigencia, verCotizacionIntegral, agregarFlete, entregarComercial, gestionarServicios, aplicarPropuestas y verPropuestaCosteo.
- El rol viene de GET /api/sesion/actual, sin traducción. Si el nombre cambia solo de un lado, el acceso de ese rol se rompe por completo.
- El menú y las rutas ya dependen de capacidades, no del nombre.

Hacer
1. Aceptar los dos nombres durante la transición (sección 2.3).
2. Nueva matriz de AdminBiblioteca: Biblioteca, Tarifas Plaza, Reglas Solo-SICE y ajuste de vigencia. Sin Bandeja ni Mesa. El front NO le muestra el Cubicador (aunque el backend conserva su permiso de cubicación, decisión D5).
3. Quitar el nombre viejo cuando el backend esté desplegado y verificado.
4. Revisar el comentario de cotizaciones-api.service.ts:67 sobre el endpoint de conexos.

CRITERIOS DE ACEPTACION
- [ ] Un usuario AdminBiblioteca ve Biblioteca, Tarifas Plaza y Reglas Solo-SICE, y no ve Bandeja, Mesa ni el Cubicador.
- [ ] Durante la transición funcionan los dos nombres de rol.
- [ ] Tras el despliegue y la verificación se retira el nombre viejo.
```

### ADM-F10 — Mostrar por qué una línea quedó sin piso (SICE-TAC desactualizada)
Labels: `nexo`, `frontend` · Depende de: NEXO-B6 · Esfuerzo: 0 a 3 horas
```
Hoy
- El front pinta justificacion (por línea) y advertencias (por solicitud) de forma genérica.
- motivoPendiente y avisoSicetac NO existen en el modelo del front. Verificado en el backend: avisoSicetac existe en clsSolicitudCosteoDto desde el PR #301.
Hacer
- Agregar avisoSicetac y motivoPendiente al modelo y pintarlos junto al bloque "Piso SICE-TAC" de la Mesa.
- Mensaje claro los primeros días del mes (cuando aún no se carga el período nuevo).

CRITERIOS DE ACEPTACION
- [ ] Cuando el backend manda avisoSicetac o motivoPendiente, la Mesa los muestra junto al bloque «Piso SICE-TAC».
- [ ] Sin aviso, no se muestra nada.
```

### ADM-F11 — Importación de licitación: kg/t y lectura de números
Labels: `nexo`, `frontend` · Esfuerzo: 8 a 12 horas
```
Hoy
- El mapeo de columnas es un objeto plano (mapeoExcel); el peso está en CAMPOS_MAPEO_EXCEL:56.
- Hay TRES lectores de números distintos y aislados: parseNumeroExcel (líneas de captura, :767), parseNumeroEmpaque (:269) y parsearImporte (Biblioteca).
- BUG real: parseNumeroExcel lee "18.000" como 18 y "18.000,5" como vacío (NaN), sin avisar. Los demás campos numéricos del mismo mapeo tienen el mismo problema.
- No existe nada que separe una letra del número en la misma celda ("18t").

Hacer
1. Selector kg/t en el mapeo de la columna de peso (decisión 8).
2. Función que separe el número del sufijo ("18t" → 18; con unidad t → 18.000 kg) y convierta. Siempre se envía kg al backend.
3. Corregir el formato colombiano de los miles en el lector (2-3 h adicionales).
4. Si Nexo agrega el diccionario (NEXO-B4), el campo de vehículo del Excel manda el texto crudo y el backend lo resuelve; el front no hace emparejamiento.
Referencia: el lector de Cotifly (licitacion-excel.util.ts:93-100) ya entiende "18.000" y "18,5".

CRITERIOS DE ACEPTACION
- [ ] El usuario elige kg o toneladas para la columna de peso.
- [ ] «18t» con toneladas se envía como 18.000 kg; el backend siempre recibe kg.
- [ ] «18.000» se lee como 18.000 y «18.000,5» como 18.000,5 en todos los campos numéricos del mapeo.
- [ ] Prueba con un Excel real.
```

---

## 5. Repo Cotifly — Backend (Duban)

Datos del chat de Cotifly (rama QA, #177). Su repo: `InnovacionIA-proyectos/Cotifly`, backend en `backend-dotnet/`, contrato en `backend-dotnet/specs/openapi.yaml`, scripts de base de datos en `db/`. Cotifly ya dejó el detalle en `docs/backlog/2026-10-16_PENDIENTES_DUBAN.md` (punto 8).

### CTF-B1 — Soportar el rechazo de línea que viene de Nexo
Labels: `cotifly`, `backend` · Depende de: NEXO-B1, NEXO-B6, DATA-4 · Esfuerzo: por estimar en su archivo
```
Hoy (verificado por Cotifly)
- Una línea borrada en Nexo pasa a camión "descartada" (sin costo, no cuenta, sin conexos), pero si todos los camiones de una ruta están descartados, la ruta queda sin flete.
- Antes de entregar, el autocosteo exige que TODAS las líneas tengan flete. Una línea rechazada dejaría la cotización en "pendiente de costeo" para siempre.
- Una línea sin flete en una cotización ya entregada deja la ruta sin precio y el PDF falla con 409 COSTEO_NO_LISTO.
- Cotifly no distingue una línea rechazada de una pendiente o borrada.

Hacer
1. Estado nuevo "rechazada" y motivo por camión.
2. Leer estadoLinea y motivoRechazo de GET /api/solicitudes/{id}.
3. El autocosteo ignora las rechazadas.
4. Total y PDF sin las rutas rechazadas: "Sin costeo", sin precio y sin motivo.
5. Excel de licitación con "Rechazada" sin motivo.
6. Margen y conexos bloqueados en una ruta rechazada.
7. Versiones -vN: decidir si se copian.
8. Deshacer solo antes de la entrega: el camión rechazado no es definitivo hasta entonces.
9. Decisión D1: si todas las rutas están rechazadas, el PDF se BLOQUEA con un error propio (por ejemplo SIN_RUTAS_COTIZABLES). Endpoint: POST /web/cotizaciones/{folio}/costeo/pdf.

CRITERIOS DE ACEPTACION
- [ ] Una línea rechazada en Nexo se ve como «rechazada» en Cotifly, no como pendiente.
- [ ] El autocosteo no queda esperando una línea rechazada.
- [ ] El total y el PDF no suman las rutas rechazadas y las muestran «Sin costeo», sin precio ni motivo; el Excel de licitación igual.
- [ ] Con todas las rutas rechazadas el PDF responde un error propio (SIN_RUTAS_COTIZABLES).
- [ ] Deshacer el rechazo solo es posible antes de entregar.
```

### CTF-B2 — Exponer las clases de contenedor en su catálogo agregado
Labels: `cotifly`, `backend` · Esfuerzo: 2 a 4 horas
```
Hoy: el backend de Cotifly consume GET /api/catalogos/clases-contenedor de Nexo (valores reales: CNT40.HC, CNT40.RE, CNT20.ST, CNT20.RE, CNT20.IT) y no lo expone en GET /web/catalogos-flete. El front tiene su propio catálogo fijo.
Hacer: agregar clases_contenedor a GET /web/catalogos-flete. Si la base de SAP no está configurada en Nexo, el endpoint devuelve [] (lista vacía): manejarlo.

CRITERIOS DE ACEPTACION
- [ ] GET /web/catalogos-flete incluye clases_contenedor con los cinco valores reales.
- [ ] Si Nexo devuelve una lista vacía, no se rompe.
```

### CTF-B3 — Corregir dos textos de error desactualizados
Labels: `cotifly`, `backend` · Esfuerzo: 1 hora
```
clsCosteoNexo ("pide… el rol Comercial") y clsServiciosConexosNexo ("rol Comercial, CoordinadorFletes o Admin") mencionan roles que cambian con NEXO-B3. Ajustar el texto cuando se despliegue.

CRITERIOS DE ACEPTACION
- [ ] Los dos textos de error nombran los roles vigentes.
```

### CTF-B5 — Peso con tara en lo que Cotifly manda a Nexo
Labels: `cotifly`, `backend` · Depende de: NEXO-B5, CTF-F3 · Esfuerzo: por estimar
```
Hoy (verificado por Cotifly)
- peso_kg es el peso total de la CARGA de la ruta, sin tara. A Nexo va, por línea, el peso del camión que devolvió el Cubicador (peso_total_kg), o el de la ruta si no hay Cubicador. Al Cubicador va el peso por unidad de cada mercancía.
- Cotifly no conoce la tara.
- Que Nexo quite la validación contra la suma de posiciones no rompe nada: con varios camiones Cotifly no manda posiciones.

Hacer (decisión D2 y D2b: la regla aplica también a Cotifly y la tara la escribe el Comercial)
- Aceptar y guardar, por línea o camión, el peso total CON tara que escribe el Comercial, y mandar ese valor a Nexo como peso de la línea.
- No cambiar lo que se manda al Cubicador (peso por unidad de cada mercancía, sin tara).
- Valor inicial sugerido: el peso de carga del camión; el comercial lo ajusta sumándole la tara.
- Documentarlo en el contrato con Nexo.

CRITERIOS DE ACEPTACION
- [ ] Cotifly manda a Nexo el peso total con tara que escribió el Comercial.
- [ ] Lo que va al Cubicador (peso por mercancía, sin tara) no cambia.
- [ ] Queda documentado en el contrato con Nexo.
```

### CTF-B4 — (Opcional) Mostrar el motivo por el que una línea sigue pendiente
Labels: `cotifly`, `backend` · Depende de: NEXO-B6
```
Hoy Cotifly no lee motivoPendiente. Con "SICE-TAC solo del mes actual", más líneas quedarán en consulta completa, y Cotifly las seguirá consultando hasta 72 horas antes de pasarlas a "costeo vencido". Si Nexo expone el motivo, Cotifly puede mostrarlo. No es urgente.

CRITERIOS DE ACEPTACION
- [ ] Si Nexo expone motivoPendiente, Cotifly lo muestra o lo guarda; si no lo expone, no se rompe.
```

---

## 6. Repo clientepro-frontend — Front de Cotifly (Sara)

Datos del chat de ese repo. Los pendientes ya documentados están en `docs/COTIFLY_CONSTRUCCION.md` (revisar antes de crear las issues para no duplicar) y en `docs/backlog/2026-10-16_PENDIENTES_SARA.md` del repo Cotifly.

### CTF-F1 — Selector kg/t y lectura de pesos con letra
Labels: `cotifly`, `frontend` · Esfuerzo: 3 a 5 horas
```
Hoy
- El mapeo de columnas (cotizador.component.html:185) usa CAMPOS_LICITACION (licitacion-excel.util.ts:61), con un solo campo pesoKg y sin unidad.
- numeroExcel() (licitacion-excel.util.ts:93-100) entiende "18.000" y "18,5", pero "18t" da NaN y la fila queda con peso vacío.
Hacer
- Control de unidad kg/t en el grid de mapeo y extender el lector para separar el número del sufijo y convertir. El backend de Cotifly sigue recibiendo siempre peso_kg.
- Se afecta: licitacion-excel.util.ts, cotizador.component.ts (poblarRutaDesdeFilaExcel, ~línea 997) y cotizador.component.html.

CRITERIOS DE ACEPTACION
- [ ] El usuario elige kg o toneladas para la columna de peso; «18t» da 18.000 kg y el backend sigue recibiendo kg.
- [ ] «18.000» y «18,5» se siguen leyendo bien.
- [ ] Prueba unitaria del lector de números.
```

### CTF-F2 — Mostrar el rechazo de línea en el costeo
Labels: `cotifly`, `frontend` · Depende de: CTF-B1 · Esfuerzo: 6 a 10 horas
```
- La tabla "Información de costeo" de "Ver detalle" (detalle-cotizacion.component.html:387-454) necesita un estado "rechazado" con el motivo visible y el botón de deshacer. También ProgresoLicitacionComponent.
- Mensaje de bloqueo del PDF si todas las rutas están rechazadas (decisión D1).
- El PDF y el Excel los genera el backend de Cotifly, no este front.

CRITERIOS DE ACEPTACION
- [ ] La tabla «Información de costeo» muestra el estado «rechazado» con el motivo.
- [ ] Hay botón de deshacer mientras no se entregue.
- [ ] Con todas las rutas rechazadas, el PDF se bloquea con un mensaje claro.
```

### CTF-F3 — Etiqueta del peso (tara)
Labels: `cotifly`, `frontend` · Depende de: CTF-B5 · Esfuerzo: 3 a 5 horas
```
Hoy el campo "Peso (kg)" (por mercancía, nuevaMercancia()) no aclara si es carga o con tara, y Cotifly manda a Nexo el peso de la mercancía sin tara.
Decisión D2 y D2b: la regla aplica también a la captura de Cotifly y el Comercial escribe el peso total de cada línea ya con la tara.
Hacer
- Campo editable "Peso total con tara" por camión o línea, junto a la nota que explique qué debe incluir.
- Se manda a Nexo como peso de la línea (CTF-B5). El peso por mercancía que va al Cubicador no cambia.

CRITERIOS DE ACEPTACION
- [ ] Hay un campo editable «Peso total con tara» por camión o línea, con su nota.
- [ ] Ese valor viaja a Nexo; el peso por mercancía que va al Cubicador no cambia.
```

### CTF-F4 — Catálogo de clases de contenedor desde el backend
Labels: `cotifly`, `frontend` · Depende de: CTF-B2 · Esfuerzo: 2 a 4 horas
```
Reemplazar clase-contenedor.catalogo.ts (5 valores fijos) por una llamada a GET /web/catalogos-flete, igual que tiposVehiculo, tiposCarroceria y clasesViaje. Los valores reales coinciden hoy con el catálogo fijo.

CRITERIOS DE ACEPTACION
- [ ] El front lee las clases de contenedor de GET /web/catalogos-flete y ya no usa el catálogo fijo.
- [ ] Los cinco valores reales aparecen en el selector.
```

**No requiere cambio:** el desglose de flete, levante y retorno. Su API de costeo no lo expone y el desglose es solo de la Mesa de Nexo (decisión 5). El campo de vehículo en el Excel de licitación tampoco se agrega: Cotifly no usa el vehículo y el diccionario vive en Nexo.

---

## 7. Datos y tablas (Sara)

### DATA-1 — Columnas del rechazo en la línea de solicitud
```
Tabla db_NEXO.tbl_lineas_solicitud (hoy EstadoLinea es nvarchar(20) sin CHECK, así que "Rechazada" cabe).
Agregar: MotivoRechazo nvarchar(500) NULL, RechazadaPor uniqueidentifier NULL, RechazadaAt datetimeoffset NULL.
Actualizar docs/Database/ESQUEMA_NEXO.sql. Este proyecto no usa migraciones: el script se corre a mano en cada base.

CRITERIOS DE ACEPTACION
- [ ] La tabla tiene las tres columnas nuevas y el script quedó en ESQUEMA_NEXO.sql.
- [ ] Se probó en una base de pruebas, con respaldo previo.
```

### DATA-2 — Tabla de alias de vehículos y su carga
```
Crear la tabla (sugerido db_NEXO.tbl_alias_vehiculo: Id, Alias, AliasNormalizado único, ConfiguracionVehiculoId, Activo, CreatedAt).
Cargarla con el archivo cubicador/database/datos_raw/diccionario_configuraciones_vehiculo_RAW.csv.
Decisión D6: el mapeo de cada columna del archivo (Turbo, Sencillo, Minimula, Tractomula, Dobletroque, 40 HC, 20 STD) a un código de Nexo se define dentro de esta issue.
Actualizar ESQUEMA_NEXO.sql y docs/Database/seeder/README.md.

CRITERIOS DE ACEPTACION
- [ ] La tabla de alias existe y está cargada con el diccionario.
- [ ] El mapeo de cada columna del archivo a un código de Nexo está documentado.
- [ ] ESQUEMA_NEXO.sql y el README del seeder están actualizados.
```

### DATA-3 — Script de usuarios para el cambio de rol
```
UPDATE db_NEXO.tbl_usuarios SET Rol = 'AdminBiblioteca' WHERE Rol = 'CoordinadorFletes';
UPDATE db_NEXO.tbl_mapeo_rol_portal SET Rol = 'AdminBiblioteca' WHERE Rol = 'CoordinadorFletes';
El rol del portal (CoordinadorFletesNEXO, id 49) puede conservar su nombre: el mapeo es aparte.
Correr en el orden de la sección 2.3, con respaldo previo y primero en pruebas.

CRITERIOS DE ACEPTACION
- [ ] Los usuarios y el mapeo de rol pasan de CoordinadorFletes a AdminBiblioteca.
- [ ] Se corrió primero en pruebas, con respaldo.
- [ ] Ningún usuario queda con el rol viejo.
```

### DATA-4 — Base de datos de Cotifly para el rechazo
```
Script en db/ del repo Cotifly: estado "rechazada" y motivo por camión (lo describe el archivo de pendientes de Duban).

CRITERIOS DE ACEPTACION
- [ ] El script de Cotifly con el estado «rechazada» y el motivo por camión está en db/ y probado.
```

---

## 8. QA y validaciones (kcano)

### QA-1 — Validar los cálculos 1+1 y 4+4
Labels: `nexo`, `qa` · Repo: NEXO
```
Validar los tiempos del PR #301 contra los casos reales:
- Viaje cargado: 4 h de cargue + 4 h de descarga; si la línea trae sus dos horas, mandan.
- Devolución de contenedor: 1 + 1 por defecto.
- Retorno de contenedor en un viaje cargado: el viaje conserva 4 + 4 y el tramo vacío se calcula aparte con 1 + 1.
- Levante en ciudad diferente: 2 h en total.
- Comparar el piso SICE-TAC contra el valor esperado.
- Solicitud por API con horas explícitas.
Checklist: un caso por cada punto; las diferencias quedan como issue.

CRITERIOS DE ACEPTACION
- [ ] Cada caso (viaje cargado, devolución, retorno, levante y solicitud por API) da las horas y el piso esperados.
- [ ] Las diferencias quedan como issue.
```

### QA-2 — Revisar los valores calculados con SICE-TAC
Labels: `nexo`, `qa` · Repo: NEXO · Depende de: ejemplos de Fredy
```
Tomar un conjunto de rutas reales (spot y licitación) y comparar el valor de Nexo contra lo esperado por el negocio. Revisar la regla (período vigente, configuración del vehículo, horas) y su implementación. Pendiente: Fredy envía ejemplos de licitaciones con el resultado que esperan.

CRITERIOS DE ACEPTACION
- [ ] Fredy envió los ejemplos de licitación con lo que esperan.
- [ ] Hay un informe de rutas comparadas contra lo esperado.
- [ ] Las diferencias quedan como issue.
```

### QA-3 — Pruebas de roles tras el cambio
Labels: `nexo`, `qa` · Depende de: NEXO-B3, ADM-F9
```
Matriz de pruebas por rol y endpoint (Comercial, AdminTarifas, AdminBiblioteca, Admin, LiderProceso, ServiciosLogisticos): cada uno entra a lo suyo y recibe 403 en lo demás. Incluir un usuario real de AdminBiblioteca después del despliegue. Verificar que el Comercial de Cotifly siga pudiendo captura, solicitudes, cotizaciones, conexos y resultado.

CRITERIOS DE ACEPTACION
- [ ] Cada rol entra a lo suyo y recibe 403 en lo demás.
- [ ] Un usuario real de AdminBiblioteca fue probado tras el despliegue.
- [ ] El Comercial de Cotifly sigue funcionando.
```

### QA-4 — Prueba de punta a punta Nexo y Cotifly con rechazo de línea
Labels: `nexo`, `cotifly`, `qa` · Depende de: NEXO-B1, CTF-B1, CTF-F2
```
Casos: rechazar una línea, deshacerla, entregar con una rechazada, entregar con todas rechazadas, PDF y Excel de Nexo y de Cotifly, correo con motivos, contrato GET /api/solicitudes/{id} y GET /api/cotizaciones/{id}.

CRITERIOS DE ACEPTACION
- [ ] Todos los casos pasan o quedan como issue: rechazar, deshacer, entregar con una y con todas rechazadas, PDF y Excel de Nexo y de Cotifly, correo con motivos.
```

### QA-5 — Verificar con datos reales los casos de front#353
Labels: `nexo`, `qa` · Repo: administracion y NEXO · Esfuerzo: 2 a 4 horas
```
Origen: informe del front. El código ya está hecho y mergeado a QA (front#292 y front#339); faltan cuatro casillas que no se vieron con datos reales:
- Una línea con una plaza vencida (vencida = true): debe verse "Vencida · solo referencia" y no ofrecer "Usar este".
- Una línea con tablaOrigen = NEXO en el histórico: debe etiquetarse "Histórico (NEXO)".
- Un histórico por encima del piso: la fuente queda habilitada y "Usar este" la aplica.
- Que el backend mande avisoSicetac con texto: debe verse el aviso sobre las líneas. Se espera desde el 1 de noviembre, cuando el período cargado deje de ser el actual (coincide con la regla de SICE-TAC del mes actual del PR #301).
Es una verificación de datos, no de código. Hay que preparar los casos (o esperar a que existan) y marcar las casillas de front#353.

CRITERIOS DE ACEPTACION
- [ ] Las cuatro situaciones se vieron con datos reales y se marcaron las casillas de front#353.
```

---

## 9. Observaciones fuera de las issues

- **Flete manual sin piso (sin decidir):** antes del PR #301, agregar o editar un flete solo validaba el piso si lo encontraba. El PR deja la línea en consulta completa con motivo cuando SICE-TAC no es del mes actual, pero no verifiqué si guardar un flete manual a mano sigue aceptando cualquier valor sin piso. Conviene una prueba antes de decidir si es issue.
- **Documentos con datos viejos:** `docs/documentacion/Nexo_Comparacion_Backend_vs_Entendimiento.md` dice que RN-03 está implementada y que no hay fletes bajo el piso. Contradice el código revisado; no usarlo como estado.
- **Rutas de los front:** las líneas y archivos citados son del código de cada chat en rama QA. Confirmarlas al crear la issue.
- **Estimaciones:** las del backend de Nexo son mías y orientativas. Las del front de Nexo y de Cotifly vienen de sus chats.

---

## 10. Ya resuelto en QA (no crear issue)

Lo pidió Sara y ya lo implementaron los PR #301 y #303, según sus README de QA (`tests/QA/pr-301-…` y `tests/QA/pr-303-…`). Yo no lo probé; la validación es la issue QA-1.

| Pedido | Qué quedó |
|---|---|
| AdminTarifas puede cubicar una línea | El PR #301 lo agregó a `POST` y `GET /api/cubicacion`. El PR #303 agregó pruebas de los roles de ese endpoint. Lo que falta es el resto del Cubicador: ver NEXO-B7. |
| SICE-TAC solo se usa si es del mes y año actuales | `clsPeriodoSicetac`, hora de Colombia y sin mes de gracia. La línea queda en consulta completa con motivo pendiente y `aplicar-propuestas` no la aplica en bloque. |
| Devolución de contenedor con 1 h + 1 h por defecto | `clsResolucionHorasLinea.Resolver`. |
| «Retorna contenedor» ya no cambia las horas de todo el viaje (RN-03) | El tramo vacío de retorno se calcula aparte, como piso SICE-TAC de viaje vacío con 1 h + 1 h, y reemplaza la antigua tarifa de plaza «línea 6». |
| Levante en ciudad diferente | Se estima con 2 h en total. |

---

## 11. Relación con las issues del front del sprint 6 (informe del 16 de octubre)

El front de Nexo ya tiene abiertas issues propias para lo que quedó bloqueado por el backend. Estas son las equivalencias, para no duplicar:

| Issue del front | Qué espera | Issue de backend de este documento |
|---|---|---|
| front#343 | Cambiar el vehículo de una línea | NEXO-B10 (decisión D9) |
| front#344 | Bandeja de normalización de vehículos | NEXO-B10 y NEXO-B4 |
| front#345 | Chips de línea de negocio, clase de viaje y clase de contenedor | NEXO-B8 |
| front#347 | Prellenar la tarifa de plaza desde la línea | NEXO-B8 |
| front#346 | Evidencias fotográficas | **Descartada** (decisión 11, foto en carga extradimensionada). Cerrar front#346; NEXO#234 sigue cerrada. |
| front#353 | Verificar cuatro casos con datos reales | QA-5 |
| front#286 y front#287 | Cubicación en Captura y en la Mesa | NEXO-B9 (404 propio) y NEXO-B11 (A y B) |
| front#301 | Histórico SAP paginado | NEXO-B9 |
| front#307 | Clase de contenedor de Extradimensionado | NEXO-B11 (decisión D10) |
| front#313 | Guardar como tarifa de plaza | NEXO-B9 (detalle opcional) |

Lo del informe que **no** necesita issue: el permiso de cubicar para AdminTarifas (bloque 9) ya lo resolvió el PR #301, aunque solo para `/api/cubicacion`; el resto del Cubicador está en NEXO-B7.

**Diferencias que conviene saber**
- Las issues del informe (bloques 1 a 12) llevan identificadores `front#…` y vienen de otra numeración: las issues de este documento no sustituyen a las del front. Cada issue de backend aquí desbloquea una o varias issues del front de arriba.
- El informe asume que QA crea las issues de backend a partir de `PENDIENTES-BACKEND-NEXO.md`. Con este documento ya están escritas (NEXO-B8 a NEXO-B11): no hace falta crearlas dos veces.
- Cotifly no depende de ninguna de estas issues, salvo NEXO-B8 y NEXO-B6 (campos nuevos y opcionales en el mismo contrato) y NEXO-B10 (bandeja de cuarentena para los códigos que manda con la bandera encendida).
