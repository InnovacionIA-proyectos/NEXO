# QA — PR #405: Consultar SICE-TAC y Plaza/SAP con el mapeo de vehículo (#380)

## Resumen

Hace que el motor de costeo traduzca el vehículo de NEXO al código de cada
fuente externa antes de consultarla, usando `tbl_mapeo_vehiculo_tarifario`
(#377/PR #398) vía el nuevo `IMapeoVehiculoTarifarioPort`:

- **Piso SICE-TAC** (propuesta, aprovisionamiento, retorno de contenedor,
  camino oficial): usa `CodigoSicetac`. Corrige el bug real reportado en
  #398 — el Sencillo (`C2` en NEXO) se buscaba literalmente como `C2` en
  SICE-TAC, no encontraba filas y el motor tomaba sin avisar el vehículo más
  barato de la ruta (`CA_`, la camioneta). Ya no hace falta la regla vieja
  de quitar la `C` inicial (`TrimStart('C')`), que además dejaba `CA_` en
  `A_` — se conserva solo como fallback para vehículos sin fila de mapeo.
- **Plaza/SAP** (`tblSAPTarifas` y su log): usa `CodigoPlazaSap` pasado a ID
  por el catálogo de vehículos de SAP, como antes. Un vehículo mapeado sin
  código de plaza (`CodigoPlazaSap` NULL) no tiene tarifa ahí: en una línea
  de **cupo** (definida por el vehículo) ya no se ofrece la tarifa de otro
  vehículo en su lugar (`exigeVehiculoDePlaza`).
- **Plaza propia** (`tbl_tarifas_plaza` y su histórico): acepta **tanto** el
  código de plaza del mapeo como el código propio del vehículo, porque la
  carga inicial de esa tabla usó códigos de SICE-TAC para `3S2`/`2L3`
  (discrepancia ya documentada en `docs/backlog/2026-10-27_PENDIENTES-MAPEO-VEHICULO.md`,
  pendiente de decidir). Es una ampliación aditiva: nada de lo que antes
  coincidía deja de coincidir.
- El mapeo se cachea 5 minutos (1 minuto si la tabla no está disponible) en
  un servicio único con su propio alcance de BD, porque lo consultan
  costeos en paralelo; sin la tabla, responde "sin mapeo" y el costeo se
  comporta exactamente como antes de que existiera (no falla).

## Qué se verificó

1. Diff aislado de la PR (commit `0bcceb9`, el único propio de esta rama más
   allá de lo ya compartido con #398): el puerto/helper
   (`IMapeoVehiculoTarifarioPort`, `clsCodigosDeVehiculo`), su adaptador
   (`clsMapeoVehiculoTarifarioAdapter`, caché + degradación agraciada), y
   los 3 consumidores (`clsTarifarioSapAdapter`, `clsPropuestaTarifaAdapter`,
   `clsTarifaPlazaInternaAdapter`).
2. **Investigado en detalle un posible doble-mapeo**: en
   `clsTarifarioSapAdapter.SicetacOficialAsync`, la rama de contenedor
   (`claseViajeId == 1`) deriva el vehículo de
   `ConfigPorRangoPesoAsync` (de `tblRangoPeso`/`tblContenedorRangoPeso`,
   SAP) y ese resultado se pasa igual por `_mapeo.SicetacAsync(...)`, como
   si fuera un código de NEXO. **Se confirmó con una consulta en vivo contra
   la BD real** (`SELECT DISTINCT TIPOVEHICULO FROM dbo.tblRangoPeso`) que
   esa columna solo trae `2, 2S2, 2S3, 3S3` — exactamente lo que dice el
   comentario del código (`clsSapRangoPeso.TipoVehiculo`). Para esos cuatro
   valores el "doble mapeo" es inofensivo: `2` no tiene fila en
   `tbl_mapeo_vehiculo_tarifario` (pasa sin cambios) y `2S2`/`2S3`/`3S3`
   tienen `CodigoSicetac` idéntico a su propio `TipoVehiculo` (mapean a sí
   mismos). **No es un hallazgo real con los datos actuales**, pero queda
   anotado aquí por si en el futuro esa tabla llega a tener un valor como
   `C2`/`C3` (Sencillo/Dobletroque), que sí cambiaría de valor.
3. `dotnet build -c Release` → 0 errores (mismos warnings preexistentes).
4. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
5. `dotnet test tests/Nexo.Application.Tests` → **901/901** (35 tests
   nuevos: `clsCodigosDeVehiculoTests.cs` + ampliación de
   `clsSugerirMapeoExcelTests.cs`/`clsEnmascaradorDeCeldasTests.cs` que
   venían de la cadena de mapeo de Excel, ya revisados en PRs anteriores).
6. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → **870/883**, 13 omitidas: las 10
   esperadas de siempre más 3 de `clsMapeoVehiculoSicetacTests` (necesitan
   `CREATE ANY DATABASE` para construir un DWSice de mentira — el usuario de
   la BD compartida de pruebas no tiene ese permiso; el propio test se salta
   solo con el motivo exacto, no es un fallo).

## Resultado

Verificación técnica completa, sin hallazgos bloqueantes. El posible
doble-mapeo en la rama de contenedor quedó investigado y confirmado inofensivo
con los datos reales actuales (ver punto 2). Pendiente de autorización de
Kevin para comentar/aprobar/mergear en GitHub.
