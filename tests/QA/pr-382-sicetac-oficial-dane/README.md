# QA — PR #382: SICE-TAC oficial de Contenedor/Cupo, la ruta se compara por código DANE

**PR**: https://github.com/InnovacionIA-proyectos/NEXO/pull/382
**Autor**: ivasco-dev
**Rama**: `378-sicetac-oficial-dane-primero` → `QA`
**Cierra**: #378

## Qué cambia

- `clsTarifarioSapAdapter.SicetacOficialAsync` buscaba la ruta por texto (`%YUMBO%-%CALI%`) y tomaba el valor más alto: Yumbo→Cali elegía Yumbo→Calima y Cartagena→Ciénaga elegía Ciénaga de Oro (Córdoba).
- Con los dos municipios de la línea resueltos, compara la ruta exacta por código DANE (`Origen`/`Destino` de `tblRutasSicetac`, sin el cero a la izquierda). El texto queda como respaldo cuando falta alguno de los dos códigos.
- `clsConsultaTarifarioSap` gana `OrigenDane` y `DestinoDane` (opcionales, con valor por defecto: no rompe a quien ya la construye). El criterio del candidato termina en `ruta DANE <origen>-<destino>`.
- Sin cambios de base de datos.

## Cómo se verificó

1. Checkout de `pr382` (rama de la PR con `QA` integrada, sin conflictos).
2. Revisión del diff real de `src/`: el `TrimStart('0')` solo se aplica al código DANE, la rama por DANE exige los dos códigos (`is not null && is not null`), y el filtro por `Origen`/`Destino` se compone sobre la misma consulta base (mismos 5 filtros de vía estándar, periodo, carga, unidad, configuración).
3. `dotnet build Nexo.Clean.sln -c Release` → 0 errores.
4. `clsSicetacOficialPorDaneTests` contra DWSice real → **6/6** ✅
5. Regresión (tarifario / propuesta / SICE-TAC / cubicación de líneas, Api.Tests) → **40/40** ✅
6. `Nexo.Domain.Tests` → **218/218** ✅ · `Nexo.Application.Tests` → **533/533** ✅
7. `cubicador` pytest (la PR toca `nexo_client.py` y `desde_linea.py`) → **275 passed** ✅

## Prueba de mutación (los tests detectan el bug)

Se revirtió temporalmente solo `clsTarifarioSapAdapter.cs` a su versión previa al fix (`f902e72^`) y se corrieron los mismos 6 tests: **4 fallan, 2 pasan**.

- Fallan: Yumbo→Cali, Cartagena→Ciénaga, «códigos DANE sin ruta no dan piso» y la propuesta de línea de Cupo.
- Pasan: los dos de respaldo por texto (sin DANE / un solo DANE), que es comportamiento que no cambió.

Luego se restauró el archivo (`git status` limpio salvo esta carpeta). Es decir, los tests no son vacuos: reproducen el síntoma original.

## Limpieza de datos de prueba

Solo el test `La_propuesta_de_una_linea_de_Cupo_...` escribe en la BD real de pruebas (una solicitud, su línea, un vehículo y una ruta con sufijo único). Borra todo en `finally`, de mejor esfuerzo: si un bloqueo ajeno impidiera el borrado quedaría como máximo ese registro con sufijo `test378-<guid>`. Los otros 5 tests solo leen DWSice.

## Observaciones (no bloqueantes)

- El título de la PR («Merge branch '339-aprovisionamiento-levante-dane' into DEPLOY») no corresponde a su contenido, que es #378. Conviene renombrarla.
- Los tests fijan el reloj en el período más reciente de DWSice, así que no dependen del mes en que se corran.

## Cómo volver a correr esta verificación

```
powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-382-sicetac-oficial-dane/verificar.ps1
```

Requiere `.env.pruebas-reales.local` en la raíz del repo y VPN corporativa conectada.
