# QA — PR #418: Cotizar los conexos con el precio de venta (#408)

## Resumen

Hasta ahora un servicio conexo (cargue, descargue, escolta…) se cotizaba a
su **costo**. Esta PR separa costo y precio de venta:

- **`tbl_servicios_conexos.PrecioVenta`** (catálogo, nullable): precio al
  cliente con margen ya incluido. Vacío = el servicio aún no tiene precio
  de venta propio.
- **`tbl_cotizacion_conexos.PrecioVentaUnitario`** (por conexo agregado a
  una cotización): el precio de venta del servicio **en el momento de
  agregarlo**, fijo para siempre aunque la biblioteca cambie después.
  Nunca vacío: si el servicio no tenía precio de venta, guarda su costo.
- **`ValorVenta`** (`clsCotizacionConexo`, propiedad calculada, no
  persistida): `PrecioVentaUnitario × Cantidad`, redondeado a 2 decimales
  (`MidpointRounding.AwayFromZero`, igual que el costo). Es lo que de
  verdad se cotiza.
- **El total de la cotización cambia de sumar `ValorAplicado` (costo) a
  sumar `ValorVenta`** (`clsCotizacionCalculos.Recalcular`). `ValorAplicado`
  sigue siendo el costo y no cambia de significado — sigue pudiendo venir
  en el request para ajustarlo manualmente, sin que eso toque lo que se
  cotiza.
- Al **editar** cantidad o costo de un conexo ya agregado,
  `PrecioVentaUnitario` **nunca se toca** — queda fijo al valor que tenía
  al agregarlo, incluso si el precio de venta del servicio en la
  biblioteca cambia después. Documentado explícitamente en el código y
  cubierto por un test dedicado.
- El catálogo (crear/editar/importar por Excel) gana el campo opcional
  `precioVenta`, con `limpiarPrecioVenta` para borrarlo explícitamente
  (PATCH: `null` sin la bandera significa "no tocar"; las dos cosas juntas
  son 400 `PRECIO_VENTA_AMBIGUO`). La importación por Excel reconoce una
  columna "…VENTA" (buscada antes que "CODIGO" para no confundirla con una
  columna de costo que también contenga "VALOR").
- La herramienta de lectura del asistente (`consultar_servicios_conexos`,
  PR #401) ahora informa `precioVenta` y aclara que el costo es de uso
  interno y lo que se cotiza es el precio de venta.

## Verificación manual exacta de los valores (regla de costeo)

Calculado a mano desde el código, no solo "respondió bien":

| Caso | Costo | PrecioVenta | Cantidad | `PrecioVentaUnitario` esperado | `ValorVenta` esperado | `ValorAplicado` esperado |
|---|---|---|---|---|---|---|
| Con precio de venta | 50.000 | 80.000 | 3 | 80.000 | 80.000 × 3 = **240.000** | 50.000 × 3 = **150.000** |
| Sin precio de venta | 50.000 | — | 2 | 50.000 (cae al costo) | 50.000 × 2 = **100.000** | 50.000 × 2 = **100.000** |
| Redondeo | 100 | 100,01 | 0,33 | 100,01 | 100,01 × 0,33 = 33,0033 → **33,00** | — |
| Editar tras subir el precio en la biblioteca (80.000→200.000, costo 50.000→150.000) a cantidad 4 | — | — | 4 | **80.000 (fijo, no cambia)** | 80.000 × 4 = **320.000** | 150.000 × 4 = **600.000** (el costo sí se recalcula) |

Los 4 casos coinciden cifra por cifra con lo que afirman los tests
(`clsPrecioVentaConexosTests.cs`), verificado independientemente antes de
leer las aserciones.

## Qué se verificó

1. Diff aislado de la PR (2 commits: el esquema de BD y la lógica).
2. `docs/Database/407_precio_venta_conexos.sql` revisado línea por línea:
   **aditivo** (2 columnas nuevas, ninguna existente cambia), **idempotente**,
   en **dos partes** — la Parte A (antes de desplegar) agrega las columnas
   nullable y llena `PrecioVentaUnitario` de las filas existentes con su
   costo actual (`ValorAplicado / Cantidad`, o el costo del catálogo si
   `Cantidad = 0`); la Parte B (comentada, a correr después de desplegar el
   backend) vuelve a llenar lo que haya quedado en `NULL` y pone la columna
   `NOT NULL`. Se aplicó la **Parte A** contra la BD real de pruebas
   (autorizado por Kevin); el propio script trae su verificación de solo
   lectura, y dio exactamente lo esperado: `columnas_nuevas=2, checks=2,
   servicios_con_precio_venta=0, conexos_en_cotizaciones=1700,
   conexos_sin_precio_venta=0, conexos_cuyo_total_cambia=0` (ningún total
   de cotización existente cambió).
3. Confirmado que `PrecioVentaUnitario` se congela al agregar un conexo y
   nunca se toca al editar cantidad/costo — ver tabla de verificación
   manual arriba.
4. `dotnet build -c Release` → 0 errores (mismos warnings preexistentes).
5. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
6. `dotnet test tests/Nexo.Application.Tests` → **1182/1182** (14 nuevos:
   `clsPrecioVentaConexosTests.cs` + `clsPrecioVentaServicioConexoTests.cs`).
7. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → **1104/1127, 1 error** (22 omitidas
   esperadas de siempre). **El error no es de la lógica de esta PR**: ver
   hallazgo abajo.

## Hallazgo (no bloqueante, con issue de seguimiento)

`clsServiciosConexosTests.Crear_con_precio_de_venta_lo_devuelve_y_sin_el_queda_vacio`
falla de forma **reproducible** (no transitoria: se repitió aislado) con
`System.InvalidOperationException: Sequence contains no matching element`.

**Causa raíz**: toda la clase `clsServiciosConexosTests` usa `"Precinto"`
como tipo de servicio genérico para sus pruebas desde hace tiempo, y
**ninguno de sus tests limpia lo que crea** (sin `Dispose`/cleanup). La BD
compartida de pruebas acumuló **1.594 filas** con `TipoServicio = 'Precinto'`
(confirmado por consulta directa). El test nuevo de esta PR pide
`GET /api/servicios-conexos?pageSize=200&q=Precinto` asumiendo que sus 2
filas nuevas caben en la primera página; con 1.594 filas ya existentes del
mismo tipo (orden `TipoServicio, Origen`), el empate no garantiza que
queden dentro de las primeras 200.

**No es un bug de producción**: la lógica de precio de venta se verificó
correcta a mano (ver tabla arriba) y el resto de los tests de precio de
venta (creación, PATCH, validación de negativos) pasan limpio. Es una
fragilidad del test nuevo frente a una deuda de higiene de datos
preexistente y ajena a esta PR.

Reportado y documentado en **issue #421**, creada con autorización
explícita de Kevin. No bloquea esta PR.

## Datos de prueba

Se aplicó `407_precio_venta_conexos.sql` Parte A (solo DDL + un `UPDATE`
que llena una columna nueva con un valor calculado desde datos ya
existentes, sin alterar ningún total) contra la BD real de pruebas. El
resto de datos de prueba los crean y limpian los propios tests en su
`finally`/`Dispose` (salvo el caso documentado en el hallazgo, ajeno a la
lógica verificada aquí).

## Resultado

Verificación técnica completa. Lógica de producción correcta y verificada
cifra por cifra. Un hallazgo no bloqueante de higiene de tests, reportado
en la issue #421.
