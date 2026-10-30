# Precio de venta de los servicios conexos — issues por repositorio

_Fecha: 2026-10-28 · Documento de planeación: no modifica el proyecto_

Hoy cada servicio conexo de la biblioteca de NEXO tiene un solo valor, `CostoUnitario`, y es el que se cobra en la cotización. Se agrega `PrecioVenta`: el valor que paga el cliente por ese servicio, con su propio margen ya incluido (los márgenes de los conexos varían por servicio y los define quien administra la biblioteca). **Desde este cambio, tanto NEXO como Cotifly cotizan los conexos con el precio de venta.**

## 1. Cómo queda el valor de un conexo

| Dónde | Campo | Puede ser vacío | Qué es |
|---|---|---|---|
| Biblioteca (`tbl_servicios_conexos`) | `CostoUnitario` | No | Lo que le cuesta a la empresa (como hoy). |
| Biblioteca (`tbl_servicios_conexos`) | `PrecioVenta` **(nuevo)** | **Sí** | Precio al cliente. Vacío = el servicio todavía no tiene precio de venta. |
| Cotización (`tbl_cotizacion_conexos`) | `ValorAplicado` | No | **Costo** × cantidad. No cambia de significado: lo usa `costo_final` de Cotifly (HU-039.1). |
| Cotización (`tbl_cotizacion_conexos`) | `PrecioVentaUnitario` **(nuevo)** | **No** | Precio de venta del servicio al agregarlo a la cotización; **si el servicio no tiene precio de venta, se guarda el costo**. |
| Respuesta de la cotización (API) | `valorVenta` **(nuevo)** | No | `PrecioVentaUnitario × Cantidad`. **Es el valor del conexo que se cotiza**, en NEXO y en Cotifly. |

El valor de venta queda **fijo** en la cotización al agregar el conexo: si después cambia la biblioteca, una cotización existente no cambia.

## 2. Decisiones tomadas (Sara, 2026-10-28)

| # | Decisión |
|---|---|
| 1 | NEXO y Cotifly cotizan los conexos con el **precio de venta**: `PrecioVentaUnitario × Cantidad`. |
| 2 | `PrecioVenta` de la biblioteca puede quedar vacío. En la cotización **nunca**: si el servicio no tiene precio de venta, se usa el costo. |
| 3 | El precio de venta se guarda en `tbl_cotizacion_conexos` al agregar el conexo (queda fijo). |
| 4 | El Comercial **no puede** escribir a mano el valor de un conexo: sale siempre de la biblioteca. Se bloquea en el front; la API de NEXO se deja como está y sigue recibiendo `valorAplicado` opcional (Cotifly nunca lo manda). |
| 5 | El comercial de Cotifly **no puede** cambiar el precio de venta para un cliente puntual. |
| 6 | Las cotizaciones de Cotifly ya finalizadas no se recalculan. |
| 7 | El costo lo ve solo el personal interno (Comercial, AdminTarifas…), por ejemplo en el asistente; **nunca** el cliente ni el PDF. |
| 8 | El margen de Cotifly se aplica **solo al flete**, nunca a un servicio conexo. |
| 9 | NEXO sigue mandando el costo (`valorAplicado`) aparte del valor de venta (`valorVenta`): `costo_final` de Cotifly depende de que `valorAplicado` sea costo real. |
| 10 | Responsables: front de NEXO `jpvelez`; backend de Cotifly Duban; front de clientepro y datos `scalderon-bs`. |

**Datos actuales** (base de pruebas, 2026-10-28, solo lectura): 1.545 conexos en cotizaciones; ninguno con cantidad 0, todos con su servicio y ninguno con valor puesto a mano (`ValorAplicado = CostoUnitario × Cantidad` en todos). Por eso las filas existentes se llenan con `PrecioVentaUnitario = ValorAplicado / Cantidad` (su costo), y el total de esas cotizaciones no cambia.

**Cómo usa Cotifly hoy los conexos** (análisis de la sesión de Cotifly, solo lectura, 2026-10-28):
- Lee la cotización con `GET /api/cotizaciones/{id}`; de cada conexo solo lee `valorAplicado` y lo trata como costo; ignora el objeto `servicio` anidado; si un campo que exige llega `null`, descarta el conexo sin avisar.
- Guarda los conexos en `dbo_cotifly.CotizacionServiciosConexos` con **un solo valor**, `precio` (hoy el costo unitario, `valorAplicado ÷ cantidad`); mapeo en `clsCotizacionConfiguracion.cs:341-365`.
- El margen se aplica a un **total combinado**: flete + retorno + levante + conexos (`clsLineaSolicitud.cs:940-941`, `CostoFinal` → `PrecioFinalLinea`). Con la decisión 8 esa fórmula se parte en dos.
- Al agregar un conexo en NEXO, Cotifly manda solo `{ lineaId, servicioConexoId, cantidad }`, **nunca** `valorAplicado` (`clsServiciosConexosNexo.cs:209-211`), y no tiene edición de conexos. En Cotifly el comercial nunca ha podido escribir el valor a mano. Que NEXO deje de aceptar `valorAplicado` no rompe nada en Cotifly; el valor a mano solo existe del lado de NEXO (Mesa de Costeo).

## 3. Pendientes

| # | Pendiente | Quién |
|---|---|---|
| H | Importar Excel de la biblioteca: una celda «PRECIO VENTA» **vacía**, ¿borra el precio de venta o lo deja como estaba? Propuesta: **lo deja** (igual que la columna ACTIVO desde #261); para borrarlo se usa la pantalla. Y si el archivo no trae la columna (un export viejo), no se toca nada. | Sara |
| I | ¿Se permite un precio de venta **menor que el costo**? Propuesta: se permite (puede haber servicios que se venden a pérdida a propósito), pero la pantalla muestra un aviso. | Sara |

---

## 4. Issues — NEXO (`InnovacionIA-proyectos/NEXO`)

**[S6-HU-38.1] Precio de venta en los servicios conexos (base de datos)** · `datos` · `scalderon-bs` · 2 a 3 h
- Resumen:
  - `tbl_servicios_conexos.PrecioVenta decimal(18,2) NULL`, después de `CostoUnitario`, con `CHECK (PrecioVenta IS NULL OR PrecioVenta >= 0)`.
  - `tbl_cotizacion_conexos.PrecioVentaUnitario decimal(18,2) NOT NULL`, con `CHECK (PrecioVentaUnitario >= 0)`. Para las filas existentes: agregar la columna, llenarla con `ValorAplicado / Cantidad` y luego dejarla `NOT NULL`.
  - Script idempotente en `docs/Database/<n.º issue>_precio_venta_conexos.sql`, con su consulta de verificación.
  - Actualizar `ESQUEMA_NEXO.sql`, el diccionario de datos y `seeder/servicios_conexos.csv` (columna nueva, vacía donde no haya dato).
- Criterios: las dos columnas existen en la base de pruebas; `PrecioVenta` queda en `NULL` en el catálogo; ninguna fila de `tbl_cotizacion_conexos` queda sin `PrecioVentaUnitario` y el total de las cotizaciones existentes no cambia; el script corre dos veces sin error; `ESQUEMA_NEXO.sql` crea las columnas en una base nueva.
- Dependencias: ninguna.

**[S6-HU-38.2] Backend: precio de venta en la biblioteca y en la cotización** · `backend` · `ivasco-dev` · 8 a 10 h
- Resumen: mapear las columnas nuevas en las entidades y en EF, y cambiar estos endpoints:

  | Endpoint | Cambio |
  |---|---|
  | `GET /api/servicios-conexos` | `clsServicioConexoDto` trae `precioVenta` (puede ser `null`) |
  | `POST /api/servicios-conexos` | el request acepta `precioVenta` (opcional, ≥ 0) |
  | `PATCH /api/servicios-conexos/{id}` | el request acepta `precioVenta`; hay que poder **borrarlo** (volverlo `null`), no solo cambiarlo |
  | `POST /api/servicios-conexos/importar-excel` | lee la columna «PRECIO VENTA»; vacía = `null` |
  | `GET /api/servicios-conexos/exportar-excel` | escribe la columna «PRECIO VENTA» |
  | `GET /api/servicios-conexos/plantilla-excel/{tipo}` | las plantillas traen la columna «PRECIO VENTA» |
  | `POST /api/cotizaciones/{id}/conexos` | guarda `PrecioVentaUnitario = PrecioVenta ?? CostoUnitario` del servicio. El request no cambia: sigue recibiendo `valorAplicado` opcional, que solo afecta el costo |
  | `PATCH /api/cotizaciones/{id}/conexos/{conexoId}` | si cambia la cantidad, `valorVenta` se recalcula con el `PrecioVentaUnitario` guardado. El request no cambia |
  | `DELETE /api/cotizaciones/{id}/conexos/{conexoId}` | el aviso de «conexos cambiados tras la entrega» (`clsConexoCambiado.Valor`) usa `valorVenta`; el total se recalcula con la venta |
  | `GET /api/cotizaciones/{id}` y `.../por-solicitud/{id}` | cada conexo trae `precioVentaUnitario` y `valorVenta` (nunca `null`) además de `valorAplicado` (costo); `total` suma la venta |
  | `POST /api/cotizaciones/{id}/entregar` | el correo de entrega (`clsPlantillaDeEntrega`) muestra el subtotal de conexos con la venta |
  | `GET /api/cotizaciones/{id}/exportar-excel` | el Excel (`clsExportadorCotizacionExcelClosedXml`) muestra el valor y el subtotal de conexos con la venta |
  | `POST /api/asistente/herramientas/consultar_servicios_conexos` (y la misma herramienta dentro de la conversación) | devuelve costo y precio de venta. Está en la rama de #371 (PR #402–#404): se hace cuando esté integrada |

  **Cambian solos, sin tocar su código:** los otros endpoints de `/api/cotizaciones` devuelven la cotización completa (`clsCotizacionDto`), así que traen `valorVenta` y el total nuevo con solo cambiar el DTO y el cálculo del total. Son:
  - `POST /api/cotizaciones`
  - `POST {id}/aplicar-propuestas`
  - `POST {id}/lineas`
  - `PUT {id}/lineas/{lineaSolicitudId}`
  - `POST` y `DELETE {id}/lineas/{lineaSolicitudId}/rechazo`
  - `PUT {id}/lineas/{lineaSolicitudId}/vehiculo`
  - `POST {id}/resultado`

  En total, 22 endpoints: los 6 de la biblioteca (`/api/servicios-conexos`), los 15 de `/api/cotizaciones` (7 con cambios propios y 8 que cambian solos) y la herramienta del asistente. Hay que actualizar sus pruebas de contrato, porque el total cambia de costo a venta.

- Además:
  - **Total de la cotización** (`clsCotizacionCalculos`): suma `valorVenta` de los conexos, no `ValorAplicado`.
  - El correo de entrega y el Excel de la cotización no muestran el costo.
  - **Importación:** hoy toma como costo cualquier encabezado que contenga «COSTO» **o «VALOR»**. Una columna «VALOR VENTA» se leería como costo. Hay que buscar primero la columna de venta y excluirla de la del costo. Al actualizar un servicio existente (upsert), un archivo **sin** la columna o con la celda vacía no borra el precio de venta (pendiente H).
  - **Los dos sitios que crean conexos de cotización** deben llenar `PrecioVentaUnitario`: `clsAgregarConexosCotizacionCommand` y `clsDataSeeder` (datos de desarrollo, línea ~207). En EF un `decimal` no asignado vale 0: si un camino se queda sin llenarlo, el conexo se cotiza en $0 sin ningún error. Agregar una prueba que lo detecte.
  - **Auditoría de la biblioteca:** el cambio de precio de venta queda en el `Diff` de crear y editar.
  - **Redondeo:** `valorVenta` = `PrecioVentaUnitario × Cantidad` redondeado a 2 decimales, igual que `ValorAplicado`.
  - **PATCH de la biblioteca:** hoy ignora los campos `null` («solo se actualizan los campos presentes»). Para poder borrar el precio de venta hace falta una forma explícita (por ejemplo `"limpiarPrecioVenta": true`, o distinguir campo ausente de campo `null`).
  - Actualizar `CONTRATO.md` (servicios conexos y cotización): `valorAplicado` = costo, `valorVenta` = lo que se cotiza.
- Criterios:
  - crear, editar (incluido borrar), listar, importar, exportar y plantillas manejan `precioVenta`;
  - un conexo de un servicio con precio de venta se cotiza con ese precio; uno sin precio de venta, con el costo;
  - un conexo agregado no cambia si después se edita la biblioteca;
  - un request con `valorAplicado` sigue funcionando y no cambia el `valorVenta`;
  - el total, el correo y el Excel de la cotización usan el valor de venta;
  - en las cotizaciones existentes, el total no cambia.
- Dependencias: S6-HU-38.1.

**[S6-HU-38.3] QA: precio de venta de conexos de punta a punta** · `qa` · `kcano-ux` · 3 a 4 h
- Resumen: probar en QA el ciclo completo NEXO → Cotifly → clientepro con todas las partes integradas.
- Criterios:
  - un servicio con precio de venta: NEXO y Cotifly cotizan `precioVenta × cantidad`, y Cotifly no le suma margen;
  - un servicio sin precio de venta: los dos cotizan con el costo, sin margen en Cotifly, y el conexo no desaparece;
  - el margen de Cotifly sigue aplicándose al flete, también en el margen masivo (HU-041.1);
  - `costo_final` / «Total costeo» de Cotifly sigue mostrando el costo real;
  - el cliente nunca ve el costo de un conexo (PDF, correo);
  - en la Mesa de Costeo ya no hay dónde escribir el valor de un conexo a mano;
  - cambiar el precio en la biblioteca no cambia una cotización existente; una cotización finalizada en Cotifly conserva sus valores;
  - importar y exportar el Excel de la biblioteca conservan el precio de venta, y reimportar un export viejo (sin la columna) no lo borra;
  - las ganancias y el margen promedio del tablero de Cotifly cuadran con conexos que tienen precio de venta distinto del costo.
- Dependencias: S6-HU-38.2, S6-HU-41.1 (administracion), HU-043.1 y HU-043.2 (Cotifly), HU-043-FE (clientepro).

## 5. Issues — Front de NEXO (`InnovacionIA-proyectos/administracion`)

**[S6-HU-41.1] Servicios conexos: precio de venta en la biblioteca y en la Mesa de Costeo** · `frontend` · `jpvelez` · 5 a 7 h
- Resumen:
  - **Biblioteca › Servicios:** «Precio venta» en la tabla y en el formulario de crear y editar (opcional; se puede dejar vacío o borrar). Si es menor que el costo, mostrar un aviso (pendiente I). Los permisos no cambian: lo editan ServiciosLogisticos, AdminTarifas y Admin.
  - **Mesa de Costeo:** el valor de cada conexo y el total salen de `valorVenta`. Quitar el campo para escribir el valor del conexo a mano (al agregar y al editar solo se elige el servicio y la cantidad). El costo (`valorAplicado`) se puede mostrar como dato interno de referencia.
- Criterios: se crea, edita y borra el precio de venta de un servicio; la tabla lo muestra (vacío como «—»); en la Mesa un conexo muestra el precio de venta, o el costo si el servicio no tiene; ya no se puede escribir el valor a mano; importar y exportar Excel siguen funcionando.
- Dependencias: S6-HU-38.2 (NEXO).

## 6. Issues — Cotifly (`InnovacionIA-proyectos/Cotifly`)

**[HU-043.1] [Datos] Precio de venta en los conexos de la cotización** · `datos` · `scalderon-bs` · 2 h
- Resumen:
  - `dbo_cotifly.CotizacionServiciosConexos.precioVenta decimal(18,2) NOT NULL`, junto a `precio` (que sigue siendo el costo unitario).
  - Para las filas existentes: agregar la columna, llenarla con `precio` y luego dejarla `NOT NULL`. No cambia ninguna cotización existente (decisión 6).
  - Script `16_...sql`, idempotente y con su consulta de verificación. El 15 es el del rol Director y el 17 ya lo toma `17_sap_margen_sincronizacion.sql` (HU-042.1, #242, en disco sin commit).
- Criterios: la columna existe en la base de pruebas de Cotifly; ninguna fila queda sin `precioVenta`; el script corre dos veces sin error.
- Dependencias: ninguna (se puede aplicar antes del backend).

**[HU-043.2] [Backend] Servicios conexos con precio de venta y sin margen** · `backend`, `contrato` · Duban · 10 a 14 h
- Resumen:
  - Leer `valorVenta` y `precioVentaUnitario` de cada conexo en `clsCosteoNexo.LeerCotizacion` y agregarlos a `clsConexoNexo`. NEXO los manda siempre (nunca `null`).
  - Guardar el costo en `precio` (como hoy) y la venta en `precioVenta` (HU-043.1); mapearla en `clsCotizacionConfiguracion.cs`. `clsServiciosConexosCosteoService.ImportarConexos` (línea ~458) hoy deriva el precio solo de `valorAplicado`.
  - **Partir la fórmula del precio de la ruta** (`clsLineaSolicitud.cs:940-941`): hoy el margen se aplica a flete + retorno + levante + conexos juntos. Debe quedar: (flete + retorno + levante) con margen **+** conexos a su precio de venta, sin margen.
  - **HU-041.1 (#231):** la Parte A (margen a todas las líneas en una operación) usa la misma fórmula: el margen solo a flete, retorno y levante. Si #231 se hace antes, dejarlo así desde el principio.
  - **Captura de rutas** (`clsCapturaRutasService.cs`): hoy lee `costoUnitario` del catálogo de NEXO para el valor inicial de los conexos. Debe usar `precioVenta ?? costoUnitario`, sin margen.
  - **`ConexosConPrecio`** (`clsCosteoCotizacionService.cs:1132`): hoy devuelve los conexos con el margen de la ruta; debe devolver el valor de venta. Alimenta el detalle del comercial, gerente y director (`clsDetalleCotizacionService.cs:155`) y el Excel de licitación (`clsLicitacionService.cs:171,623`).
  - **Dominio:** `clsServicioConexoRuta` recibe `PrecioVenta` en `Crear(...)` y `Actualizar(...)`.
  - **`GET /web/cotizaciones/{folio}`:** `totales.precio_transporte` = (flete + retorno + levante) con margen; `totales.precio_conexos` = suma de las ventas. El front los usa en vez de restar.
  - **El costo de los conexos** (`CostoServiciosConexos` / `CostoFinal`) sigue siendo el costo real: de eso dependen las ganancias (#201), el margen ponderado y la validación del total (HU-036.4), que así siguen correctos sin cambios.
  - **Notas de significado:** `margen_promedio` (#154) pasa a ser el margen sobre el flete; la deducción `costo = precio × (1 − margen)` de la #236 deja de dar el costo real.
  - Revisado sin impacto: políticas de margen; PDF y correo al cliente (no muestran el precio de cada conexo, solo cambia el total).
  - **Orden de despliegue:** si esta versión de Cotifly sale antes que S6-HU-38.2 en NEXO, `valorVenta` no llega y hoy el conexo se descartaría sin avisar. Mientras el campo no exista, usar `valorAplicado`.
  - `costo_final` (HU-039.1, #218) sigue usando el costo (`valorAplicado` / `precio`).
  - Las cotizaciones finalizadas no se recalculan.
  - El costo del conexo nunca sale en el PDF ni en el correo al cliente.
- Criterios: precio final de una línea = (flete + retorno + levante) con margen + conexos con su valor de venta; la captura de rutas usa el precio de venta; las ganancias y el margen promedio del tablero cuadran con conexos que tienen precio de venta distinto del costo; sin `valorVenta` en la respuesta de NEXO el conexo no desaparece; `costo_final` no cambia; el PDF y el correo al cliente muestran el valor de venta y nunca el costo; una cotización finalizada antes del cambio conserva sus valores.
- Dependencias: S6-HU-38.2 (NEXO), HU-043.1. Relacionada: HU-041.1 (#231).

## 7. Issues — Front de Cotifly (`InnovacionIA-proyectos/clientepro-frontend`)

**[HU-043-FE] Servicios conexos con precio de venta y sin margen (bloqueado por backend HU-043.2)** · `frontend` · `scalderon-bs` · 3 a 4 h
- Resumen:
  - Detalle de cotización (`detalle-cotizacion.component.html` 450-476, 522-544, 687-703): los conexos muestran lo que trae el backend (valor de venta).
  - Quitar la resta de `totalFleteConMargen()` (`.ts:389-394`) y usar `totales.precioTransporte` / `totales.precioConexos` (ya en el contrato, hoy descartados en `.ts:245-254`).
  - Actualizar los comentarios que dicen que `precio` «ya trae el margen» (`cotifly.model.ts:848-849`, `detalle-cotizacion.component.ts:346-347`).
  - Etiqueta «El margen aplica solo al flete» en la caja de margen masivo (~380) y en el formulario por línea (~743).
  - Progreso de licitación (`progreso-licitacion.component.html`): confirmar que muestra el valor de venta.
  - Si el backend agrega el costo al objeto del conexo, no pintarlo. «Total costeo» (#121) sigue con el costo real.
  - Sin cambios: captura, revisión de licitación, vista previa del correo y PDF, pantallas de gerente y director, mis cotizaciones.
- Criterios: el precio final de la ruta en pantalla coincide con el del backend; flete + conexos = precio final usando los totales del backend; cambiar el margen solo mueve el flete; ninguna pantalla muestra el costo de un conexo.
- Dependencias: HU-043.2 (Cotifly).

---

**Orden:** S6-HU-38.1 y HU-043.1 (datos, en paralelo) → S6-HU-38.2 → (S6-HU-41.1 · HU-043.2 → HU-043-FE) → S6-HU-38.3.
**Totales:** 3 issues en NEXO, 1 en administracion, 2 en Cotifly, 1 en clientepro-frontend.
