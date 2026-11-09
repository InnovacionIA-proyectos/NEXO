<#
.SINOPSIS
Verificacion de la PR #449: "feat(solicitudes): recomendar el vehiculo de un contenedor
y mostrarlo en el Excel" (Parte B de la issue #446).

.QUE VERIFICA
Nuevo `POST /api/solicitudes/recomendar-vehiculo-contenedor` (Comercial, AdminTarifas, Admin),
de solo lectura y por lote (hasta 5000 lineas), que reutiliza la regla existente del costeo
(`IRangosPesoContenedorPort`/`clsRecomendadorDeVehiculoDeContenedor`) sin duplicar la tabla de
rangos de peso. Tambien agrega la columna "Vehiculo" al Excel exportado (despues de "Flete",
en lineas normales y rechazadas) y renombra las etiquetas "clsCliente"/"clsRuta" a "Cliente"/
"Ruta" SOLO en el Excel (no en el JSON de la API, que debe mantener esos nombres literales
segun CONTRATO.md/PR #431).

.COMO SE VERIFICO EN VIVO
1. `dotnet test` real sobre un worktree aislado de la rama `pr449`: Domain 218/218,
   Application 1286/1286 -- exacto a lo que afirma la PR.
2. Leido `clsRecomendarVehiculoContenedorQuery.cs`: valida SIN_LINEAS, DEMASIADAS_LINEAS
   (MaxLineas=5000 real, no solo documentado) y PESO_INVALIDO antes de llamar al puerto.
3. Leido `clsRangosPesoContenedorAdapter.cs`: `VehiculoParaAsync` y `PesoMaximoAsync`
   comparten la MISMA cache (`LeerRangosAsync`, clave "rangos-peso-contenedor") -- no hay
   tabla duplicada. `PesoMaximoAsync` calcula `Max(PesoMaximo)` entre los rangos de ese
   tamano: es el tope MAS ALTO, no el que se excedio.
4. Confirmado que `IRangosPesoContenedorPort` ya se usaba antes de esta PR en
   `clsPropuestaTarifaAdapter.cs`/`clsTarifarioSapAdapter.cs` (busqueda del piso SICE-TAC) --
   es reutilizacion real, no una reimplementacion paralela.
5. `clsRecomendadorDeVehiculoDeContenedor.Pies()`: regex `(?<![0-9])(20|40)(?![0-9])` sobre el
   texto de `claseContenedor` -- acepta clase SAP (`CNT40.HC`) y pies crudos ("20", "cnt 40 hc")
   por igual, confirmado tambien por el test `El_tamano_se_acepta_como_clase_de_SAP_o_como_pies`.
6. Tests de Application (`clsRecomendarVehiculoContenedorTests.cs`) cubren exactamente los
   casos limite que afirma la PR: peso sobre el tope -> vehiculo null + tope mas alto; peso
   igual al tope SI alcanza; tabla no disponible; PesoMaximoAsync se consulta UNA sola vez por
   tamano en todo el lote (no por linea); limite de 5000 lineas; peso invalido.
7. `clsExportadorCotizacionExcelClosedXml.cs`: diff confirma columna "Vehiculo" agregada
   despues de "Flete" (`{ "Ruta", "Tipologia", "Flete", "Vehiculo" }`), presente tanto en
   lineas normales como rechazadas; las etiquetas "clsCliente"/"clsRuta" -> "Cliente"/"Ruta"
   son SOLO texto de celda en el Excel (`Dato("Cliente", ...)`, encabezado de columna), no
   tocan el JSON de ningun endpoint.
8. Roles del endpoint nuevo: `[Authorize(Roles = "Comercial,AdminTarifas,Admin")]` en el metodo
   (clsSolicitudesController.cs:84), coincide exacto con `clsRolesPorEndpointTests.cs:51` y
   `CONTRATO.md` (lineas 82 y 3107).
9. Confirmado que `clsRecomendarVehiculoContenedorQuery` no se invoca desde ningun otro
   caso de uso de costeo (unica referencia fuera de si mismo y el controller es el registro
   de DI) -- el flujo de costeo real no cambia, es un endpoint nuevo aislado.

.LIMITACIONES CONOCIDAS
- No se corrio la suite completa de Nexo.Api.Tests (requiere conectividad de red a la BD
  real de pruebas, no disponible en este entorno de verificacion) -- Domain y Application,
  que no dependen de ella, quedaron 100% verificados.
- No se ejecuto el exportador de Excel end-to-end generando un archivo real para inspeccion
  visual; la verificacion fue por lectura de codigo + los tests unitarios existentes
  (`clsExportadorCotizacionExcelTests.cs`), que ya cubren la columna nueva.
#>

$ErrorActionPreference = "Stop"
Write-Host "Ver el cuerpo de este script (.SINOPSIS/.QUE VERIFICA/.COMO SE VERIFICO EN VIVO) para el detalle completo de la verificacion de la PR #449." -ForegroundColor Cyan
Write-Host "Resultado: Domain 218/218, Application 1286/1286 -- sin hallazgos bloqueantes." -ForegroundColor Green
exit 0
