# Decisión — con qué código se busca cada vehículo en SICE-TAC y en Plaza/SAP

**Estado:** aplicada · **Tabla:** `tbl_mapeo_vehiculo_tarifario` (`docs/Database/377_mapeo_vehiculo_tarifario.sql`, carga en `docs/Database/seeder/mapeo_vehiculos.csv`)

## El problema

SICE-TAC y Plaza/SAP nombran el mismo vehículo con códigos distintos a los de NEXO. Buscarlo con el código de NEXO no encontraba filas y el
motor, sin fallar ni avisar, tomaba el vehículo más barato de la ruta (la camioneta `CA_`). El caso más claro es el Sencillo: en NEXO es `C2` y
en las dos fuentes es `2`; por eso salía más barato en el análisis del Excel de un cliente.

## Qué se hace

Todo vehículo de NEXO se traduce con la tabla antes de consultar cada fuente (`IMapeoVehiculoTarifarioPort`):

| Consulta | Código que se usa |
|---|---|
| Piso SICE-TAC (propuesta, aprovisionamiento, retorno de contenedor, camino oficial) | `CodigoSicetac` → `CONFIGURACION` de `tblRutasSicetac` |
| Plaza/SAP (`tblSAPTarifas` y su log) | `CodigoPlazaSap`, que se pasa a ID con el catálogo de vehículos de SAP, como antes |
| Plaza propia (`tbl_tarifas_plaza` y su histórico) | `CodigoPlazaSap` **o** el código del propio vehículo (ver abajo) |

Con la tabla, ya no hace falta quitar la `C` inicial en el camino oficial de SICE-TAC (`TrimStart('C')`), que además dejaba `CA_` en `A_`.

- **Vehículo sin fila de mapeo** (o tabla no disponible): se busca con su propio código, como antes de que existiera el mapeo. Para el camino oficial
  de SICE-TAC se conserva la regla anterior de quitar la `C` inicial. Si el script de la tabla aún no se aplicó o la base no responde, el mapeo
  responde "sin mapeo" por un minuto, deja un aviso en el log y el costeo sigue como antes (no falla).
- **`CodigoPlazaSap` vacío = el vehículo no tiene tarifa de plaza** (solo existe en SICE-TAC: `2_5`, `2_7`, `3S2`, `CA_`, `V2`, `V3`, `V4`). En una
  línea de **cupo**, que se define por el vehículo, no se ofrece la tarifa de plaza de otro vehículo. En contenedor la tarifa de plaza no depende del
  vehículo (depende de la clase de contenedor y del peso), así que ahí no cambia nada.
- **Turbo:** en plaza `2L1`, `2L2` y `2L3` se tarifan como `2L2`; en SICE-TAC cada uno conserva su código.
- El mapeo se guarda en memoria cinco minutos. Es un servicio único con su propio alcance de base de datos porque lo consultan costeos en paralelo.

## Plaza propia (`tbl_tarifas_plaza`): se aceptan los dos códigos

La carga inicial de `tbl_tarifas_plaza` sacó el vehículo con códigos de SICE-TAC (`3S2`: 21 filas, `2L3`: 1 fila), y el mapeo dice que `3S2` no tiene
código de plaza y que `2L3` se tarifa como `2L2`. Mientras no se decida si se corrige la tabla o se separan las columnas del mapeo (pendiente
documentado en `docs/backlog/2026-10-27_PENDIENTES-MAPEO-VEHICULO.md`), el emparejamiento acepta **el código de plaza del mapeo y también el
propio del vehículo**. Es una ampliación: nada de lo que antes coincidía deja de coincidir, y esas filas no quedan sin usar.

## Cómo se verificó

- Contra la tabla real y su carga inicial: los 15 vehículos resuelven exactamente a los códigos del CSV.
- Contra el motor real con un DWSice de mentira (una base temporal con `tblRutasSicetac`): `C2` encuentra la fila `2` (antes caía en `CA_`), `C3` la `3`,
  cada Turbo su código, `CA_` se pide por su código, y el aprovisionamiento del contenedor igual. Quitar el mapeo hace fallar esas pruebas.
- Falta confirmarlo en producción con el DWSice y SAP reales (Turbo y Sencillo contra datos reales).
