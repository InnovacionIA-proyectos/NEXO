# Issues — Cotifly reutiliza la importación inteligente de Excel de NEXO (licitaciones)

**Fecha:** 2026-10-29 · **Repos:** NEXO, Cotifly (backend), clientepro-frontend (módulo Cotifly)
**Origen:** plan de IA de NEXO (`2026-10-23_PLAN-IA-CLAUDE-NEXO.md`, milestones S6-HU-33 y S6-HU-34). Ese plan se pensó solo para el portal de NEXO; este documento agrega lo que falta para que **Cotifly use lo mismo** cuando un comercial sube el Excel de una licitación.

**Fuentes revisadas:**
- Código de NEXO (rama `QA` del 2026-10-29) y `CONTRATO.md` §1.1, §3.3 y §3.19.1.
- Código del backend de Cotifly (`backend-dotnet`).
- La sesión de clientepro-frontend, sobre la rama `conexiones-finales-y-ajustes-multirol`.

Las rutas de Cotifly son relativas a `backend-dotnet/src`; las de clientepro, a `src/app/components/cotifly/`.

---

## 0. Resumen

**Idea:** Cotifly **no replica** la lógica. El reconocimiento de columnas, el diccionario por cliente, las plantillas, la IA y la normalización de valores ya existen en NEXO, y Cotifly los consume a través de su backend.

**Cómo encaja:**
- **El front de clientepro nunca llama a NEXO.** Esa regla de arquitectura se mantiene: todo pasa por el backend de Cotifly, igual que hoy pasan `analizar-excel`, `clientes` y `alias`.
- **Cotifly ya hace la mitad del camino.**
  - `POST /web/licitaciones/analizar-excel` reenvía el archivo a NEXO como el comercial que inició sesión.
  - El mapeo de columnas lo hace hoy el front, a mano, con un auto-mapeo por sinónimos que se recalcula en cada carga.
  - No hay plantillas, ni diccionario, ni IA.
- **Lo nuevo en Cotifly es delgado.**
  - Un adaptador hacia los endpoints de NEXO.
  - Cinco grupos de endpoints `/web/*` que los exponen al front con el formato de Cotifly.
  - La pantalla de mapeo y la de revisión del front, alimentadas por esos endpoints.
  - **No se crean tablas en Cotifly:** las plantillas y el diccionario viven en NEXO.

**Bloqueante en NEXO:** con el DW de clientes configurado, que es como corren los ambientes reales, NEXO **nunca entrega el `clienteId`** de un cliente (§3.1). Sin él no hay plantillas ni diccionario por cliente, ni para Cotifly ni para el portal de NEXO. Se resuelve con una issue de NEXO (S6-HU-34.9): que acepte el **NIT** del cliente, que Cotifly siempre tiene.

**Totales:**

| Repo | Issues | Responsable | Esfuerzo |
|---|---|---|---|
| NEXO | 2 | `ivasco-dev` | 9 a 13 h |
| Cotifly backend | 5 + 1 de QA | `dguerraBSSL` (QA `kcano-ux`) | 40 a 52 h + 4 a 6 h |
| clientepro-frontend | 7 + 1 de QA | `scalderon-bs` (QA `kcano-ux`) | 45 a 58 h + 4 a 6 h |

---

## 1. Cómo funciona hoy

### 1.1 Cotifly backend

| Qué | Dónde |
|---|---|
| Analizar el Excel (reenvía a NEXO `analizar-excel`) | `POST /web/licitaciones/analizar-excel` → `clsLicitacionService.AnalizarAsync` (`Cotifly.Aplicacion/Licitaciones/Services/clsLicitacionService.cs:289-315`) → `clsAnalizadorExcelNexo.cs:29` |
| Crear la licitación con las líneas ya mapeadas | `POST /web/licitaciones` (multipart `licitacion` + `archivo`) → `CrearAsync` (L395-576); valida con `clsValidadorLicitacion` (máx. 1000 líneas) y `clsValidadorFilasLicitacion` |
| Ciudad → municipio de NEXO (determinista) | `clsMunicipiosLicitacion.cs` (`MUNICIPIO_AMBIGUO` / `MUNICIPIO_NO_ENCONTRADO`) |
| Vehículo y clase de contenedor → código | descarga todo `GET /api/catalogos/alias` y lo cachea (`clsAliasNexo.cs`, `clsAliasNexoEnCache`); proxy `GET /web/alias/resolver` |
| Columna y código de mercancía | heurística propia (`Cubicacion/Services/clsDetectorMercanciaExcel.cs:117,144`) + `clsSugeridorMercancia` contra el catálogo del cubicador |

**Cómo se conecta con NEXO:**
- **Identidad:** el header `X-Portal-clsUsuario-Id` lleva el `idUsuarioExterno` del comercial (`Cotifly.Infraestructura/Configuracion/clsIdentidadNexo.cs`). NEXO ve siempre a la persona real y aplica su rol.
- **Clientes HTTP:** hay dos, `"Nexo"` y `"NexoCosteo"`, con reintentos, circuit breaker y `clsCupoNexoHandler`. Este último, ante un `429`, respeta `Retry-After` con un bloqueo para todo el proceso.
- **Errores:** se mapean en `clsCosteoNexo.EnviarAsync` (`Cotifly.Infraestructura/Costeo/Services/clsCosteoNexo.cs:869-980`).
- **Cliente de NEXO:** Cotifly solo guarda `nitCliente`, `clienteNombre` y `origenCliente`. El `clienteId` de NEXO lo vuelve a buscar en cada envío, por NIT (`clsEnvioCosteoService.cs:904-949`), y no se lo muestra al front (`[JsonIgnore]`).
- **Convenciones:**
  - Clean Architecture, con prefijo `cls`/`ICls` y DTOs web en snake_case.
  - Resultados con `clsResultado<T>` y códigos de error en `clsCodigosError.cs`.
  - Pruebas con xUnit v3, NSubstitute, Reqnroll y Testcontainers, con el falso de NEXO en `tests/Comun/clsNexoFalso`.
  - Contrato en `specs/openapi.yaml`, y ADRs en `specs/decisiones`; el último es el ADR-026.

### 1.2 clientepro-frontend (módulo Cotifly)

**Carga del archivo:**
- **Pantalla:** «Nueva cotización» → Licitación (`pages/cotizador/cotizador.component.ts`). Al elegir el archivo corre `onArchivoLicitacionSeleccionado()` (L1398).
- **El front no lee el Excel:** lo manda crudo a `analizar-excel`.

**Mapeo de columnas:**
- **Auto-mapeo:** por sinónimos de encabezado, en `services/licitacion-excel.util.ts`: `autoMapearColumnasExcel()`, con 20 campos `CampoMapeableLicitacion` (L9-29).
- **Formato matriz:** detecta el caso de rutas en filas y vehículos en columnas (`pareceMatrizLicitacion()`).
- **Ajuste manual:** el comercial corrige la columna de cada campo en `elegirHojaLicitacion()` (L1468-1480).

**Revisión y normalización:**
- **Revisión fila por fila:** `components/dialogo-revision-licitacion/`.
- **Vehículo y clase de contenedor:** pasan por `GET /web/alias/resolver` (L1512).
- **Carrocería y ciudades:** se resuelven localmente contra los catálogos.
- **Errores por fila:** el `400 LICITACION_INVALIDA` trae `errores[]`, que se muestran en una tabla bajo el resumen.

**Datos y acceso:**
- **Cliente:** se elige con `GET /web/clientes?q=` y la licitación queda identificada por **NIT**. El front no conoce el `clienteId` de NEXO.
- **Stack:** Angular 17.3 con NgModule, RxJS y Material 16.
  - Repositorio abstracto `services/cotifly.repositorio.ts`, con una implementación HTTP y una falsa.
  - Resultados `ok | fallo_negocio | fallo`.
  - Mensajes en `cotifly-mensajes.ts` y pruebas Jasmine/Karma.
- **Roles:** solo el comercial crea licitaciones; gerente y director solo consultan.

---

## 2. Qué ofrece NEXO (ya construido, en `QA`)

| Endpoint NEXO | Qué hace | Roles |
|---|---|---|
| `POST /api/captura-solicitudes/analizar-excel` | Hojas, encabezados y filas (máx. 1000) | Comercial, Admin |
| `POST /api/captura-solicitudes/sugerir-mapeo` | Columna → campo + transformación + confianza + dudas. Orden: **plantilla guardada → diccionario → IA**. Oculta NIT, nombres, correos y teléfonos antes de la IA | Comercial, Admin |
| `POST /api/captura-solicitudes/aplicar-mapeo` | Aplica el mapeo confirmado a todas las filas (determinista, sin IA) y devuelve líneas listas + errores por fila | Comercial, Admin |
| `POST /api/captura-solicitudes/normalizar-valores` | Textos distintos de vehículo, carrocería y ciudad → código oficial / DANE (diccionario → alias → catálogo → IA con lista cerrada) | Comercial, Admin |
| `PUT/GET/DELETE /api/captura-solicitudes/plantillas-mapeo` | Plantilla por cliente, reconocida por la firma de los encabezados | Comercial, Admin |
| `GET /api/diccionario-cliente` y `GET .../opciones` | Términos por cliente (columna, unidad, valor) y listas válidas | lectura: cualquier rol de NEXO |
| `POST/PUT/DELETE /api/diccionario-cliente` | Mantener el diccionario | AdminBiblioteca, AdminTarifas, Admin (**no** Comercial) |

**Cómo se comportan:**
- **Si la IA está apagada o falla,** `sugerir-mapeo` y `normalizar-valores` responden `200` con lo que resolvieron la plantilla, el diccionario y las reglas, más `iaUsada = false` y `motivoSinIa`.
- **El tope diario de IA no se esconde:** responde `429 IA_TOPE_DIARIO`, sin `Retry-After`. Es **por persona**: el comercial gasta el mismo tope en Cotifly y en el portal de NEXO.
- **El límite de peticiones** responde `429 DEMASIADAS_SOLICITUDES` con `Retry-After: 30`.

El detalle completo de cada endpoint está en `CONTRATO.md` §3.3 y §3.19.1.

---

## 3. Hallazgos que condicionan el diseño

### 3.1 Bloqueante: NEXO no entrega el `clienteId` de clientes reales
`GET /api/captura-solicitudes/clientes` con el DW configurado devuelve `clienteId = null` para **todos** los resultados (`src/Nexo.Application/CasosDeUso/Solicitudes/clsBuscarClientesQuery.cs:38` clientes y `:51` prospectos). Solo sin DW, es decir en desarrollo, devuelve el `Id` local. Pero `sugerir-mapeo`, `aplicar-mapeo` y `normalizar-valores` usan `clienteId` para el diccionario del cliente, y las plantillas lo **exigen**.

**Consecuencias:**
- **En los ambientes reales,** ni Cotifly ni el portal de NEXO pueden usar plantillas ni diccionario por cliente; solo el diccionario general.
- **Cotifly no tiene de dónde sacar el id:** su `clsClienteEncontrado.ClienteId` también llega `null`.

**Solución propuesta (issue S6-HU-34.9):**
- Los endpoints de importación aceptan `clienteNit` como alternativa a `clienteId`.
- **Por qué el NIT y no el BP:** la tabla local de clientes de NEXO tiene el NIT como llave única (`IX_clientes_Nit`), e incluso la resolución por BP de la captura termina buscando por NIT (`clsResolucionDeCliente.cs:40`). Cotifly identifica cada licitación por NIT y no siempre tiene el BP. Además, el NIT también sirve para los prospectos.
- **Lecturas** (sugerir, aplicar, normalizar, ver plantillas): NEXO busca el cliente local por NIT. Si no existe, se comporta como sin cliente: solo diccionario general.
- **Guardar una plantilla:** si el cliente local no existe, NEXO lo crea con los datos del DW, como hace la captura; si es un **prospecto**, con los datos de la tabla de prospectos del DW (decisión P5). Cuando el prospecto se vuelva cliente real, sigue siendo el mismo cliente local (el NIT es único) y su plantilla no se pierde.

### 3.2 Los dos `429` de NEXO son distintos
`clsCupoNexoHandler` bloquea **todo el proceso** de Cotifly cuando NEXO responde `429`. Eso está bien para `DEMASIADAS_SOLICITUDES`, pero sería un error con `IA_TOPE_DIARIO`: el tope es de un solo comercial, y bloquear todo el proceso dejaría sin NEXO a todos los demás. El adaptador nuevo debe:
- distinguir el `429` por su `codigo`;
- devolver `IA_TOPE_DIARIO` al front como error de negocio propio, con el mensaje de NEXO;
- **no** activar el bloqueo global.

### 3.3 Límites que no coinciden
| Límite | Cotifly | NEXO | Qué hacer |
|---|---|---|---|
| Columnas | lector de 200 | `sugerir-mapeo` y plantillas: 1 a 100 | Con más de 100 columnas, Cotifly no llama a `sugerir-mapeo` y el front sigue con el mapeo manual; aviso claro |
| Filas | 1000 líneas | `analizar-excel` 1000, `aplicar-mapeo` 5000 | Sin cambio |
| Archivo | 9,9 MB / cuerpo 12 MB | 10 MB | Sin cambio |
| Timeout | `NexoCosteo` 30 s | la IA puede tardar más (`Anthropic:TimeoutSegundos`) | Cliente HTTP propio para la importación, con timeout configurable mayor que el de la IA en NEXO |

### 3.4 Lo que NEXO no cubre y Cotifly conserva
- **Formato matriz** (rutas en filas, vehículos en columnas): `aplicar-mapeo` es una línea por fila, así que NEXO no entiende la matriz. En vez de pedirle a NEXO que la soporte, el front de Cotifly **convierte la matriz en tabla** (una fila por ruta y vehículo marcado) y la manda por el mismo camino (decisión P3, issue HU-044-FE.7).
- **Clase de contenedor:** `normalizar-valores` resuelve vehículo, carrocería y ciudad, **no** clase de contenedor. Se sigue usando `GET /web/alias/resolver` (decisión P4).
- **Mercancía del cubicador:** el código de mercancía de Cotifly sale del catálogo del cubicador (`clsSugeridorMercancia`). Se conserva y corre sobre la `descripcion` que devuelve `aplicar-mapeo` cuando la fila no trae código.
- **Servicios conexos por línea:** NEXO no los mapea desde el Excel. Siguen como hoy.
- **Validaciones de `POST /web/licitaciones`:** se mantienen todas. `aplicar-mapeo` y `normalizar-valores` ayudan a llegar con datos limpios, pero Cotifly vuelve a validar al crear, y NEXO al capturar.

### 3.5 Otros
- **Diccionario:** hoy los comerciales solo lo pueden **leer** en NEXO. Por la decisión P2, NEXO les da permiso de escritura (S6-HU-34.10) y Cotifly permite mantenerlo desde la importación (HU-044.5 y HU-044-FE.6).
- **Filas para `sugerir-mapeo`:** conviene mandar hasta **200 filas**, aunque la IA use 20. NEXO revisa hasta 200 filas para decidir qué columnas oculta por datos personales.
- **Número de fila en los errores:** a `aplicar-mapeo` hay que pasarle `primeraFila = filaEncabezado + 1` de la hoja, para que los errores digan la fila real del Excel.
- **Ambiente:** los endpoints de NEXO están en `QA`. Cotifly solo puede usarlos contra un NEXO donde ya estén desplegados, con la IA configurada para que haya sugerencias (sin IA funcionan con plantilla y diccionario).

---

## 4. Flujo propuesto

1. **El comercial elige el cliente y sube el Excel** (igual que hoy). `analizar-excel` devuelve las hojas.
2. **Elige la hoja.** Si parece matriz, el front la convierte en tabla (una fila por ruta y vehículo) y salta al paso 5 con un mapeo fijo.
3. **El front pide la sugerencia:** `POST /web/licitaciones/sugerir-mapeo`, con `nit_cliente`, los encabezados y hasta 200 filas.
   - **Cotifly llama a `sugerir-mapeo`** con el NIT del cliente (§3.1).
   - **Responde con el mapeo en el formato de Cotifly:** campo, transformación, confianza, origen (`plantilla`/`diccionario`/`ia`/`ninguno`) y dudas.
4. **La pantalla de mapeo se prellena.**
   - **Plantilla reconocida:** muestra «Mapeo guardado para este cliente».
   - **Confianza baja:** resalta la columna.
   - **IA no disponible:** cae al auto-mapeo por sinónimos de hoy.
   - **El comercial corrige y confirma.**
5. **Se aplica el mapeo:** `POST /web/licitaciones/aplicar-mapeo`, con el mapeo confirmado, todas las filas y `primera_fila`.
   - **Cotifly llama a `aplicar-mapeo`** y traduce cada `LineaImportadaDto` a una línea de licitación de Cotifly.
   - **Devuelve las líneas y los errores por fila,** que el front pinta en la tabla de errores.
6. **Se normalizan los textos:** `POST /web/licitaciones/normalizar-valores`, con los textos **distintos** de vehículo, carrocería y ciudades.
   - **La revisión muestra el estado de cada texto:** resuelto (con su origen), ambiguo (con candidatos para elegir) o sin resolver.
   - **Lo que propone la IA va marcado y pide confirmación.**
   - **La clase de contenedor** sigue con `GET /web/alias/resolver`.
7. **Si el comercial marcó «Guardar este mapeo para el cliente»,** el front llama a `PUT /web/licitaciones/plantillas-mapeo`. La próxima vez, ese formato no llama a la IA.
8. **Se crea la licitación:** `POST /web/licitaciones`, igual que hoy, con todas sus validaciones.

**Apagado:** con el interruptor de Cotifly (`ImportacionInteligente:Habilitada = false`), o si NEXO no tiene los endpoints, el front usa el flujo actual completo.

---

## 5. Decisiones

**Tomadas (Sara, 2026-10-29):**
- Cotifly reutiliza los endpoints de NEXO; no replica ni el mapeo, ni la IA, ni el diccionario.
- El front no llama a NEXO; todo pasa por el backend de Cotifly.
- El cliente se identifica ante NEXO por **NIT** (§3.1).
- Issues: 5 de backend para Cotifly; las del front y QA, sin agrupar; NEXO, 2.
- **P1:** la plantilla se guarda **solo si el comercial marca** «Guardar este mapeo para el cliente».
- **P2:** los comerciales **pueden mantener el diccionario** del cliente (crear, editar y borrar términos), en NEXO y desde Cotifly.
- **P3:** NEXO no soporta matrices; el front de Cotifly **convierte la matriz en tabla** antes de usar la importación inteligente.
- **P5:** un prospecto **sí** guarda plantilla: NEXO crea su cliente local desde la tabla de prospectos del DW (va en la misma issue del NIT).
- **P4:** no se agrega la clase de contenedor a `normalizar-valores` de NEXO por ahora; Cotifly la sigue resolviendo con `GET /web/alias/resolver`.

**Propuestas en este documento (confirmar):**
- No se crean tablas en Cotifly: las plantillas y el diccionario viven en NEXO.
- La clase de contenedor se queda como está hoy (§3.4).
- La sesión de NEXO se hace con el comercial real (como hoy), así que el tope diario de IA es por comercial.

**Pendientes:**
- Milestone de las issues de NEXO: propuesta, el existente **S6-HU-34** (son issues nuevas; no se toca ninguna existente).
- Campos por confirmar con Duban (tabla de HU-044.2): `peso`/`pesoKg`, `tipologia`, `deadline` y `referencia`.

---

## 6. Issues

**Todas son issues nuevas: no se modifica ninguna issue existente** (decisión de Sara, 2026-10-29). Las issues ya cerradas que aquí se citan, como las de S6-HU-34 en NEXO o la #108 en clientepro, son solo referencia. Cualquier cambio a algo ya construido va en una issue nueva.

Convenciones de cada repo:

| Repo | Milestone | Título de la issue |
|---|---|---|
| NEXO | `S6-HU-NN` | `[S6-HU-NN.k] …` |
| Cotifly | `HU-0NN` | `[HU-0NN.k] [Backend\|QA] …` |
| clientepro | (sin milestone) | `[HU-0NN-FE.k] …`, con el mismo número de HU que el backend |

**Números libres al 2026-10-29:**
- **NEXO:** `S6-HU-34.9`. Las issues de S6-HU-34 llegan hasta la 34.8; la 34.5 del plan es de `administracion`.
- **Cotifly:** el milestone **HU-044**; el último es HU-043.
- **clientepro:** **HU-044-FE**.

### 6.1 NEXO

**[S6-HU-34.9] Importación inteligente con clientes reales: identificar al cliente por NIT** · `backend` · `nexo` · Responsable: `ivasco-dev` · Esfuerzo: 7 a 10 h

**RESUMEN**
Con el DW configurado, `GET /api/captura-solicitudes/clientes` devuelve `clienteId = null`. Por eso, en los ambientes reales no se pueden usar las plantillas ni el diccionario por cliente, ni desde el portal ni desde Cotifly. Los endpoints de importación deben aceptar también el NIT del cliente.

**CRITERIOS DE ACEPTACION**
- [ ] `sugerir-mapeo`, `aplicar-mapeo`, `normalizar-valores`, `PUT plantillas-mapeo` y `GET plantillas-mapeo` aceptan `clienteNit` (opcional) como alternativa a `clienteId`; con los dos, manda `clienteId`
- [ ] El NIT se normaliza igual que en la tabla local (sin puntos, espacios ni dígito de verificación, o según la regla vigente de `tbl_clientes.Nit`), para que `900.123.456-7` y `900123456` encuentren al mismo cliente
- [ ] Las lecturas (`sugerir`, `aplicar`, `normalizar`, `GET`) buscan el cliente local por NIT y **no crean** clientes: si no existe, se comportan como sin cliente (solo diccionario general, sin plantilla)
- [ ] `PUT plantillas-mapeo` con un NIT sin cliente local lo **crea** con los datos del DW (como la captura, sin duplicar por NIT); si el NIT no existe en el DW → `400 CLIENTE_NIT_INVALIDO`
- [ ] Prospecto (NIT solo en la tabla de prospectos del DW): `PUT plantillas-mapeo` crea el cliente local con su nombre y NIT desde `IProspectosDwPort` (decisión P5); si el NIT no está ni en clientes ni en prospectos → `400 CLIENTE_NIT_INVALIDO`
- [ ] Pruebas: NIT con cliente local, NIT sin cliente local (lectura y guardado), prospecto sin cliente local (guardado), prospecto que luego es cliente real (mismo cliente local), NIT inexistente, NIT con formato distinto, `clienteId` y NIT a la vez
- [ ] `CONTRATO.md` §3.3 actualizado (campo nuevo en las cuatro tablas de body y en `GET plantillas-mapeo`)

**DEPENDENCIAS**
- ninguna. Bloquea: HU-044.5 y HU-044-FE.5 (plantillas); y el uso real de plantillas en el portal de NEXO (S6-HU-34.5, repo `administracion`).

**DETALLE**
- `clsBuscarClientesQuery.cs:38` y `:51` construyen el DTO con `clienteId = null`; `:56` (sin DW) sí trae el `Id` local.
- `tbl_clientes` tiene índice único por NIT (`IX_clientes_Nit`), y `clsResolucionDeCliente` (`:40`) ya busca el espejo local por NIT: es la llave natural.
- Alternativas descartadas:
  - Aceptar el BP: no cubre a los prospectos, y Cotifly no siempre lo tiene.
  - Devolver el `Id` local en la búsqueda: no cubre a los clientes que nunca han cotizado en NEXO, que son justo los de una licitación nueva.

---

**[S6-HU-34.10] El Comercial puede mantener el diccionario de términos por cliente** · `backend` · `nexo` · `ivasco-dev` · 2 a 3 h

**RESUMEN**
Hoy solo AdminBiblioteca, AdminTarifas y Admin pueden crear, editar y borrar términos del diccionario por cliente. Por la decisión P2, el Comercial también puede, para que lo mantenga desde Cotifly o desde el portal de NEXO mientras importa los Excel de sus clientes.

**CRITERIOS DE ACEPTACION**
- [ ] `POST`, `PUT /{id}` y `DELETE /{id}` de `/api/diccionario-cliente` aceptan el rol `Comercial` (además de AdminBiblioteca, AdminTarifas y Admin); la lectura no cambia
- [ ] Cada cambio sigue quedando en `tbl_audit_log` con el usuario que lo hizo (ya existe)
- [ ] Pruebas de roles actualizadas (`clsRolesPorEndpointTests` y las del controlador)
- [ ] `CONTRATO.md` §3.19.1 (roles) actualizado

**DEPENDENCIAS**
- ninguna. La usa HU-044.5 (escritura del diccionario desde Cotifly).

**DETALLE**
- Los roles están en una constante: `RolesEscritura = "AdminBiblioteca,AdminTarifas,Admin"` (`src/Nexo.Api/Catalogos/clsDiccionarioClienteController.cs:74`).

---

### 6.2 Cotifly backend — milestone **HU-044 — Importación inteligente del Excel de licitación con NEXO**

En cada issue, el endpoint nuevo queda en `specs/openapi.yaml` (validado por `Cotifly.Api.Tests/Contrato`) y en `docs/docs_front/CONTRATOS_API_COTIFLY_FRONTEND.md`, para que el front pueda empezar.

**[HU-044.1] [Backend] Adaptador de importación inteligente hacia NEXO** · Responsable: `dguerraBSSL` · Esfuerzo: 10 a 14 h

**RESUMEN**
Base de toda la HU. Comprende:
- **Puerto y adaptador:** `IClsImportacionExcelNexo` (Aplicación) y `clsImportacionExcelNexo` (Infraestructura), para los endpoints de NEXO `sugerir-mapeo`, `aplicar-mapeo`, `normalizar-valores`, `plantillas-mapeo` (`GET`/`PUT`/`DELETE`), `GET diccionario-cliente` y `GET diccionario-cliente/opciones`.
- **Identificación del cliente por NIT.**
- **El ADR de la decisión.**

**CRITERIOS DE ACEPTACION**
- [ ] Cliente HTTP con nombre propio (`"NexoImportacion"`), con timeout configurable (`ImportacionInteligente:TimeoutSegundos`, por defecto 90) mayor que el de la IA en NEXO
- [ ] `sugerir-mapeo` y `normalizar-valores` **no se reintentan** (cada llamada puede gastar IA y cuenta para el tope); `aplicar-mapeo` y las lecturas se reintentan como los GET actuales
- [ ] Identidad con `clsIdentidadNexo`, igual que los demás adaptadores: header `X-Portal-clsUsuario-Id` con el `idUsuarioExterno` del comercial; sin `idUsuarioExterno` → no disponible, sin llamar a NEXO
- [ ] Cliente: se manda `clienteNit` con el `nit_cliente` de la licitación, normalizado como lo espera NEXO; Cotifly **no** busca ni guarda el `clienteId` de NEXO
- [ ] `429` con código `IA_TOPE_DIARIO` → error de negocio `IA_TOPE_DIARIO` con el mensaje de NEXO, **sin** activar el bloqueo global de `clsCupoNexoHandler`; `429 DEMASIADAS_SOLICITUDES` sigue como hoy
- [ ] `400` de NEXO → error de negocio con el mismo código (`DEMASIADAS_COLUMNAS`, `CAMPO_INVALIDO`, `CAMPO_DUPLICADO`, `CAMPO_REQUERIDO`…); `401/403` → `NEXO_SIN_PERMISO` (como `clsCosteoNexo`); red, timeout o circuito abierto → no disponible
- [ ] Interruptor `ImportacionInteligente:Habilitada` (opciones validadas al arrancar); apagado o sin `Nexo:UrlBase` → implementación `*NoDisponible`, que responde «no disponible» sin llamar a NEXO
- [ ] DTOs de NEXO en camelCase, privados del adaptador; códigos nuevos en `clsCodigosError.cs`
- [ ] `clsNexoFalso` (tests/Comun) simula los endpoints nuevos (respuesta normal, IA no disponible, `IA_TOPE_DIARIO`, `400`)
- [ ] Pruebas de infraestructura: header de identidad, NIT enviado, sin reintento en `sugerir`, `429` de tope sin bloqueo global, mapeo de errores
- [ ] ADR-027 «Cotifly reutiliza la importación inteligente de NEXO»: por qué no se replica, sin tablas propias, cliente por NIT, tope de IA por comercial, la matriz se convierte en tabla en el front y la clase de contenedor sigue por alias
- [ ] `docs/conexion_nexo/HU-011-formato-excel-licitacion.md` y `backend-dotnet/docs/COTIFLY-CONTRATO.md` con los endpoints de NEXO que Cotifly consume ahora

**DEPENDENCIAS**
- ninguna. Para que el NIT tenga efecto en plantillas y diccionario: NEXO S6-HU-34.9.

**DETALLE**
- Patrones a seguir:
  - `clsAnalizadorExcelNexo`: multipart, códigos que pasan tal cual.
  - `clsCosteoNexo.EnviarAsync`: mapeo de errores.
- Hoy el envío de costeo resuelve el cliente buscando por NIT (`clsEnvioCosteoService.cs:904-949`). La importación no lo necesita: manda el NIT directo.
- Contrato de NEXO: `CONTRATO.md` §3.3 y §3.19.1 (en el repo NEXO).

---

**[HU-044.2] [Backend] `POST /web/licitaciones/sugerir-mapeo`** · `dguerraBSSL` · 6 a 7 h

**RESUMEN**
Endpoint que propone el mapeo de columnas de una hoja usando NEXO (plantilla del cliente, luego diccionario, luego IA) y lo devuelve en el formato de Cotifly.

**CRITERIOS DE ACEPTACION**
- [ ] Rol Comercial (`clsControladorWebBase`)
- [ ] Body snake_case: `nit_cliente`, `encabezados` (string[]), `filas` (string[][], el front manda hasta 200)
- [ ] Más de 100 encabezados → no llama a NEXO y responde `200` con `ia_usada = false`, `motivo_sin_ia = "DEMASIADAS_COLUMNAS"` y columnas sin propuesta (el front sigue en manual)
- [ ] Respuesta: `ia_usada`, `motivo_sin_ia`, `plantilla_id`, `dudas[]` y `columnas[]` (`indice`, `columna`, `campo`, `transformacion`, `confianza`, `origen`, `nota`)
- [ ] `campo` se devuelve con el nombre de NEXO (es el que vuelve en `aplicar-mapeo` y `plantillas`); la tabla de equivalencias con los campos del front queda en el contrato (ver DETALLE)
- [ ] IA no disponible, interruptor apagado o NEXO caído → `200` con lo que haya y `ia_usada = false`; `IA_TOPE_DIARIO` → error de negocio (no se esconde)
- [ ] Escenario en `specs/features/licitaciones_excel.feature` (sugerencia con IA y con plantilla reconocida)
- [ ] Pruebas de aplicación y API

**DEPENDENCIAS**
- HU-044.1.

**DETALLE: campos de NEXO y del front de Cotifly**

| NEXO (`campo`) | Front (`CampoMapeableLicitacion`) | Nota |
|---|---|---|
| `origen`, `destino` | `origen`, `destino` | |
| `tipoVehiculo`, `carroceria`, `claseViaje`, `claseContenedor`, `lineaNegocio` | igual | |
| `exclusivo`, `consolidado`, `retornaContenedor` | igual | |
| `ciudadDevolucionContenedor`, `ciudadLevanteContenedor` | `ciudadDevolucion…`, `ciudadLevante…` | |
| `horasCargue`, `horasDescargue`, `valorMercancia` | igual | |
| `cantidad`, `largoCm`, `anchoCm`, `altoCm`, `pesoKg` | `cantidad`, `largo`, `ancho`, `alto`, `pesoKg` | En NEXO `pesoKg` es **por unidad** de la posición; confirmar el significado en Cotifly |
| `peso` | — | Peso total de la línea en kg; confirmar si va a `peso_kg` de la línea |
| `descripcion`, `codigoTipoMercancia` | (columna de mercancía detectada por el backend) | Alimentan `mercancias[]` |
| `tipologia`, `levanteContenedorDiferente`, `referencia`, `deadline` | — | Confirmar uso en Cotifly; `deadline` y `referencia` son de la licitación, no de la línea |

---

**[HU-044.3] [Backend] `POST /web/licitaciones/aplicar-mapeo` y traducción a líneas de Cotifly** · `dguerraBSSL` · 9 a 11 h

**RESUMEN**
Aplica en NEXO el mapeo confirmado a todas las filas y traduce cada línea resultante al formato de línea de licitación de Cotifly (`clsLineaLicitacionWebRequest`), para que el front llene la revisión sin armar las líneas a mano.

**CRITERIOS DE ACEPTACION**
- [ ] Rol Comercial; body: `nit_cliente`, `columnas[]` (`indice`, `campo`, `transformacion`), `filas` (todas, máx. 1000) y `primera_fila` (= fila de encabezado + 1)
- [ ] Llama a NEXO `aplicar-mapeo` con el NIT (el diccionario del cliente traduce los valores)
- [ ] Traducción de `LineaImportadaDto` a la línea de Cotifly, documentada campo por campo; `posiciones` → `mercancias[]`; `fila` se conserva como fila del Excel
- [ ] Mercancía sin código → se corre `clsSugeridorMercancia` sobre la `descripcion` (como hoy)
- [ ] Respuesta: `total_filas`, `filas_vacias`, `filas_con_errores`, `lineas[]` (formato Cotifly), `errores[]` (`fila`, `campo`, `codigo`, `mensaje`) y `total_errores`, con los mismos nombres de campo que el `400 LICITACION_INVALIDA` actual, para reutilizar la tabla del front
- [ ] Errores del mapeo mismo (`400` de NEXO: `CAMPO_DUPLICADO`, `CAMPO_REQUERIDO`…) → error de negocio con el código de NEXO
- [ ] No reemplaza las validaciones de `POST /web/licitaciones`: siguen todas
- [ ] Pruebas: transformaciones (toneladas, «ORIGEN - DESTINO», coma decimal), errores por fila con número de fila correcto, traducción de posiciones

**DEPENDENCIAS**
- HU-044.1.

---

**[HU-044.4] [Backend] `POST /web/licitaciones/normalizar-valores`** · `dguerraBSSL` · 5 a 6 h

**RESUMEN**
Lleva los textos distintos de vehículo, carrocería y ciudad a los valores oficiales con NEXO (diccionario, alias, catálogo y, al final, IA con lista cerrada).

**CRITERIOS DE ACEPTACION**
- [ ] Rol Comercial; body: `nit_cliente`, `valores[]` (`campo`: `tipo_vehiculo` | `carroceria` | `ciudad`; `valor`), máx. 1000; el front manda solo los distintos
- [ ] Respuesta: `ia_usada`, `motivo_sin_ia` y, por valor, `estado` (`resuelto`/`ambiguo`/`sin_resolver`), `codigo`, `descripcion`, `municipio_id` (ciudad, el mismo id de municipio de NEXO que ya usa Cotifly), `origen`, `confianza`, `nota` y `candidatos[]` (ambiguo)
- [ ] `IA_TOPE_DIARIO` → error de negocio; IA no disponible → `200` con lo que resolvieron las reglas
- [ ] La clase de contenedor sigue por `GET /web/alias/resolver` (decisión P4)
- [ ] `clsMunicipiosLicitacion` sigue validando al crear (no se quita)
- [ ] Pruebas: resuelto por alias, por diccionario, ciudad ambigua con candidatos, sin resolver, IA no disponible

**DEPENDENCIAS**
- HU-044.1.

---

**[HU-044.5] [Backend] Plantillas de mapeo y diccionario del cliente** · `dguerraBSSL` · 10 a 14 h

**RESUMEN**
Comprende dos cosas:
- **Plantillas:** guardar el mapeo confirmado como plantilla del cliente en NEXO, listarlas y borrarlas. Con plantilla, la próxima carga del mismo formato no llama a la IA.
- **Diccionario:** exponer el diccionario del cliente para consultarlo y mantenerlo (crear, editar y borrar términos) desde Cotifly (decisión P2).

**CRITERIOS DE ACEPTACION**

*Plantillas — `GET/PUT/DELETE /web/licitaciones/plantillas-mapeo`*
- [ ] `PUT`: `nit_cliente`, `nombre` (opcional, máx. 100), `encabezados[]` y `columnas[]`; `201` la primera vez, `200` si reemplaza
- [ ] `GET ?nit_cliente=`: las plantillas del cliente; `DELETE /{id}`: `204`
- [ ] NIT que NEXO no puede usar para guardar (`CLIENTE_NIT_INVALIDO`) → error de negocio con mensaje claro; `GET` de un cliente sin plantillas → lista vacía
- [ ] Los `400` de validación de NEXO (`CAMPO_DUPLICADO`, `ENCABEZADO_DEMASIADO_LARGO`…) pasan con su código
- [ ] NEXO registra quién la guardó (`tbl_audit_log`); Cotifly no guarda copia

*Diccionario — `GET/POST/PUT/DELETE /web/diccionario-cliente`*
- [ ] `GET ?nit_cliente=&tipo=&q=` → términos (`id`, `termino`, `tipo`, `concepto`, `contexto`, `ejemplo`, `activo`, `general`); `GET /web/diccionario-cliente/opciones`
- [ ] `POST` (con `nit_cliente`, o sin él para un término general si NEXO lo permite al rol), `PUT /{id}` y `DELETE /{id}`, que llaman a NEXO con el NIT del cliente
- [ ] Los errores de NEXO pasan con su código (`TERMINO_DUPLICADO`, `CAMPO_DESTINO_INVALIDO`, `CONTEXTO_INVALIDO`, `TERMINO_NO_ENCONTRADO`…); `403` → `NEXO_SIN_PERMISO`
- [ ] Cotifly no guarda copia; la auditoría la lleva NEXO

*Ambos*
- [ ] Escenario en `licitaciones_excel.feature`: guardar plantilla y que la siguiente carga la reconozca
- [ ] Pruebas: guardar, reemplazar, listar, borrar, NIT no válido para guardar; diccionario: listar con cliente y sin cliente (solo generales), crear, editar, borrar, término duplicado, NEXO caído

**DEPENDENCIAS**
- HU-044.1, NEXO S6-HU-34.9 (sin ella, en los ambientes reales no hay cliente y no se pueden guardar plantillas) y NEXO S6-HU-34.10 (escritura del diccionario).

---

**[HU-044.6] [QA] Importación inteligente de licitaciones de punta a punta (backend)** · Responsable: `kcano-ux` (propuesta) · 4 a 6 h

**RESUMEN**
Probar contra un NEXO con los endpoints desplegados: subir, sugerir, confirmar, aplicar, normalizar, guardar plantilla, volver a subir el mismo formato y crear la licitación.

**CRITERIOS DE ACEPTACION**
- [ ] Excels anonimizados con formatos distintos (celdas combinadas, «ORIGEN - DESTINO» en una celda, peso en toneladas, coma decimal)
- [ ] La segunda carga del mismo formato sale con `origen = plantilla` y sin llamar a la IA (`ia_usada = false`, `motivo_sin_ia = PLANTILLA_RECONOCIDA`)
- [ ] IA apagada en NEXO → el flujo sigue con diccionario o manual
- [ ] `IA_TOPE_DIARIO` de un comercial no bloquea a otro comercial
- [ ] Más de 100 columnas → manual con aviso
- [ ] Evidencia en el formato de QA del repo

**DEPENDENCIAS**
- HU-044.2 a HU-044.5.

---

### 6.3 clientepro-frontend — **HU-044-FE**

**[HU-044-FE.1] Repositorio y modelos de la importación inteligente** · Responsable: `scalderon-bs` · Esfuerzo: 4 a 6 h

**RESUMEN**
Métodos nuevos en el repositorio de Cotifly (abstracto, HTTP y falso) para `sugerir-mapeo`, `aplicar-mapeo`, `normalizar-valores`, `plantillas-mapeo` y `diccionario-cliente`, con sus modelos.

**CRITERIOS DE ACEPTACION**
- [ ] `cotifly.repositorio.ts`, `cotifly.repositorio-http.ts` y `cotifly.repositorio-falso.ts` con los métodos nuevos; interfaces `*Http` snake_case y modelos camelCase en `models/cotifly.model.ts`
- [ ] Resultados `ok | fallo_negocio | fallo`; `IA_TOPE_DIARIO`, `NEXO_SIN_PERMISO`, `CLIENTE_SIN_PLANTILLA` y `DEMASIADAS_COLUMNAS` con mensaje en `cotifly-mensajes.ts`
- [ ] El repositorio falso simula: plantilla reconocida, diccionario, IA, IA no disponible y tope diario
- [ ] Pruebas con `HttpTestingController`

**DEPENDENCIAS**
- El contrato de cada endpoint sale con su issue de backend (HU-044.2 a 044.5). Se puede empezar con el falso.

---

**[HU-044-FE.2] Mapeo sugerido prellenado en la pantalla de mapeo** · `scalderon-bs` · 10 a 12 h

**RESUMEN**
Al elegir la hoja, pedir la sugerencia al backend y prellenar el mapeo de columnas con campo, transformación, confianza y dudas, en lugar del auto-mapeo por sinónimos.

**CRITERIOS DE ACEPTACION**
- [ ] `elegirHojaLicitacion()` llama a `sugerirMapeo` (encabezados y hasta 200 filas); mientras responde, indicador de carga; la pantalla se puede usar en manual si tarda
- [ ] Origen visible por columna: «Mapeo guardado del cliente», «Diccionario», «Sugerido por IA» o «Sin propuesta»; las de confianza baja (menor que 0,7) y las `dudas` resaltadas
- [ ] Se puede cambiar el campo **y la transformación** (`ninguna`, toneladas a kg, dividir origen-destino, coma decimal, fecha); solo se ofrecen las transformaciones válidas para cada campo
- [ ] `ia_usada = false` sin propuestas, interruptor apagado o error → cae a `autoMapearColumnasExcel()` como hoy, con un aviso discreto
- [ ] `IA_TOPE_DIARIO` → mensaje claro y mapeo manual
- [ ] Formato matriz → no llama a `sugerirMapeo`: se convierte en tabla (HU-044-FE.7)
- [ ] No se avanza sin que el comercial confirme el mapeo
- [ ] Pruebas del componente (plantilla, IA, sin IA, tope, matriz)

**DEPENDENCIAS**
- HU-044-FE.1, HU-044.2.

---

**[HU-044-FE.3] Aplicar el mapeo en el backend y mostrar errores por fila** · `scalderon-bs` · 8 a 10 h

**RESUMEN**
Al confirmar el mapeo, el front manda todas las filas a `aplicar-mapeo` y usa las líneas que devuelve. Deja de armar las líneas a mano a partir de las columnas.

**CRITERIOS DE ACEPTACION**
- [ ] `aplicarMapeo` con columnas, todas las filas y `primera_fila`; las `lineas` llenan la revisión (`dialogo-revision-licitacion`)
- [ ] `errores[]` en la tabla de errores existente (fila del Excel, campo, mensaje), con `filas_vacias` y `filas_con_errores` en el resumen
- [ ] Errores del mapeo (`CAMPO_DUPLICADO`, `CAMPO_REQUERIDO`…) se muestran en la pantalla de mapeo, no en la de errores
- [ ] Interruptor apagado o backend no disponible → el armado local de hoy
- [ ] `POST /web/licitaciones` y su `400 LICITACION_INVALIDA` siguen funcionando igual
- [ ] Pruebas

**DEPENDENCIAS**
- HU-044-FE.2, HU-044.3.

---

**[HU-044-FE.4] Normalizar vehículo, carrocería y ciudades en la revisión** · `scalderon-bs` · 6 a 8 h

**RESUMEN**
En la revisión, mandar los textos distintos de vehículo, carrocería y ciudad a `normalizar-valores` y mostrar el resultado por valor.

**CRITERIOS DE ACEPTACION**
- [ ] Una sola llamada con los textos **distintos** (no por fila)
- [ ] `resuelto` → se aplica a todas las filas con ese texto; muestra el origen (diccionario, alias, catálogo, DANE, IA)
- [ ] `ambiguo` (ciudades) → el comercial elige uno de los `candidatos`
- [ ] Resultado con origen IA → marcado «Sugerido por IA» y requiere confirmación antes de aplicarse
- [ ] `sin_resolver` → queda como texto y se avisa (en NEXO, un vehículo nuevo cae en cuarentena)
- [ ] Clase de contenedor sigue con `GET /web/alias/resolver` (decisión P4); la resolución local actual de carrocería y ciudad queda como respaldo si el backend no responde
- [ ] Pruebas

**DEPENDENCIAS**
- HU-044-FE.3, HU-044.4.

---

**[HU-044-FE.5] Guardar el mapeo como plantilla del cliente** · `scalderon-bs` · 5 a 6 h

**RESUMEN**
Al confirmar el mapeo, opción «Guardar este mapeo para el cliente», más una vista sencilla para ver y borrar las plantillas del cliente.

**CRITERIOS DE ACEPTACION**
- [ ] Casilla «Guardar este mapeo para el cliente» al confirmar (decisión P1: solo se guarda si el comercial la marca); guarda con `PUT` y muestra confirmación
- [ ] NIT que NEXO no puede usar → mensaje claro con la razón (el error de HU-044.5); los prospectos sí pueden guardar (decisión P5)
- [ ] Lista de plantillas del cliente (nombre, fecha, encabezados) con borrar y confirmación
- [ ] La siguiente carga del mismo formato muestra «Mapeo guardado del cliente»
- [ ] Pruebas

**DEPENDENCIAS**
- HU-044-FE.2, HU-044.5.

---

**[HU-044-FE.6] Administrar el diccionario del cliente** · `scalderon-bs` · 7 a 10 h

**RESUMEN**
Desde la pantalla de mapeo, un panel o diálogo con los términos del cliente y los generales: consultar por qué una columna o un valor se resolvió así, y crear, editar o borrar términos (decisión P2).

**CRITERIOS DE ACEPTACION**
- [ ] Lista con búsqueda por término y filtro por tipo; marca los términos generales
- [ ] Crear y editar con los tipos, campos destino, unidades y contextos de `opciones` (el `contexto` solo para tipo `valor`); borrar con confirmación
- [ ] Atajo desde el mapeo: en una columna o un valor que no se reconoció, «Agregar al diccionario del cliente» con el término prellenado
- [ ] Errores de NEXO con mensaje claro (`TERMINO_DUPLICADO`, `NEXO_SIN_PERMISO`…)
- [ ] Solo para el rol comercial (gerente y director, si lo ven, en solo lectura)
- [ ] Pruebas

**DEPENDENCIAS**
- HU-044-FE.1, HU-044.5.

---

**[HU-044-FE.7] Convertir la licitación en formato matriz a tabla** · `scalderon-bs` · 5 a 6 h

**RESUMEN**
Cuando el Excel viene en formato matriz (rutas en filas, vehículos en columnas), el front lo convierte en tabla (una fila por ruta y vehículo marcado) y lo manda por el mismo camino que una tabla: `aplicar-mapeo` con errores por fila y `normalizar-valores`. NEXO no tiene que entender matrices (decisión P3).

**CRITERIOS DE ACEPTACION**
- [ ] Se reutiliza la detección actual (`pareceMatrizLicitacion()` y `autoMapearMatrizLicitacion()` en `services/licitacion-excel.util.ts`)
- [ ] La conversión genera filas con encabezados fijos (por ejemplo `Origen`, `Destino`, `Tipo de vehículo`, más las demás columnas de la ruta) y un mapeo fijo para `aplicar-mapeo`; no llama a `sugerirMapeo`
- [ ] Una celda vacía en la matriz no genera fila; cada fila generada conserva la fila del Excel de su ruta, para que los errores digan la fila real
- [ ] Los nombres de vehículo que vienen como encabezado de columna («Turbo», «Tractomula») pasan por `normalizar-valores` (HU-044-FE.4)
- [ ] Mismo resultado que hoy para las matrices que ya funcionan (pruebas con los casos actuales del util)
- [ ] Pruebas de la conversión (matriz simple, con celdas vacías, con columnas extra de la ruta)

**DEPENDENCIAS**
- HU-044-FE.3 (aplicar el mapeo en el backend), HU-044-FE.4.

---

**[HU-044-FE.8] QA de punta a punta de la importación inteligente (front)** · `kcano-ux` (propuesta) · 4 a 6 h

**RESUMEN**
Recorrer el flujo completo en el portal con Excels anonimizados.

**CRITERIOS DE ACEPTACION**
- [ ] Casos de HU-044.6 vistos desde la pantalla: plantilla reconocida, IA, sin IA, tope diario, más de 100 columnas, ciudad ambigua
- [ ] Matriz convertida en tabla: mismas líneas que antes y errores con la fila real
- [ ] Diccionario: un término agregado por el comercial se usa en la siguiente carga
- [ ] Mensajes claros en cada error; el flujo manual de respaldo funciona
- [ ] Evidencia

**DEPENDENCIAS**
- HU-044-FE.2 a HU-044-FE.7.

---

## 7. Orden sugerido

1. **NEXO S6-HU-34.9** (cliente por NIT) y **Cotifly HU-044.1** (adaptador), en paralelo. Desde ahí, el front puede avanzar **FE.1** con el repositorio falso.
2. **Cotifly HU-044.2, 044.3 y 044.4,** y en el front **FE.2 → FE.3 → FE.4 → FE.7** (matriz), a medida que salgan los endpoints.
3. **NEXO S6-HU-34.10** (Comercial edita el diccionario), en cualquier momento antes de HU-044.5.
4. **Cotifly HU-044.5 → FE.5 y FE.6** (plantillas y diccionario). Necesita S6-HU-34.9 y S6-HU-34.10 desplegadas.
5. **QA:** HU-044.6 y FE.8.

| Repo | Issues | Responsable | Esfuerzo |
|---|---|---|---|
| NEXO | S6-HU-34.9 y 34.10 | `ivasco-dev` | 9 a 13 h |
| Cotifly | HU-044.1 a 044.5 | `dguerraBSSL` | 40 a 52 h |
| Cotifly | HU-044.6 (QA) | `kcano-ux` | 4 a 6 h |
| clientepro-frontend | HU-044-FE.1 a FE.7 | `scalderon-bs` | 45 a 58 h |
| clientepro-frontend | HU-044-FE.8 (QA) | `kcano-ux` | 4 a 6 h |
