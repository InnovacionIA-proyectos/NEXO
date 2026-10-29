# QA — PR #391: Aplicar el mapeo de un Excel de cliente a todas las filas

## Resumen

Primera verificación encontró un test desactualizado:
`El_peso_total_debe_coincidir_con_la_suma_de_la_carga`
(`tests/Nexo.Application.Tests/MapeoExcel/clsAplicarMapeoExcelTests.cs`)
esperaba `PESO_INCONSISTENTE` en un escenario que, desde la PR #341 (regla de
tara: peso declarado ≥ suma de la carga es válido), ya no debía fallar.

No era un bug de código: `clsAplicarMapeoExcelQuery` reutiliza correctamente
la validación compartida `clsCapturarSolicitudValidador.ValidarLinea` (ya
actualizada por #341), así que el comportamiento de producción era correcto;
solo el test quedó con la expectativa anterior porque la rama se creó antes
de que #341 llegara a `QA`.

Se reportó con el detalle exacto (archivo, test, snippet y dos opciones de
corrección) y no se aprobó hasta que @BS-IVasco corrigió el test en el
commit `04c6e6c` ("Integrar QA y ajustar la prueba del peso total a la regla
con tara").

## Corrección verificada

El test se renombró a
`El_peso_total_puede_superar_la_suma_de_la_carga_por_la_tara_pero_no_al_reves`
y ahora cubre los tres escenarios relevantes en un solo archivo de 3 filas:

- Fila 2: carga (200 kg) supera el peso declarado (100 kg) en más del 1% →
  rechaza con `PESO_INCONSISTENTE`.
- Fila 3: peso declarado (1000 kg) mayor a la carga (200 kg) → tara válida,
  se acepta.
- Fila 4: peso declarado igual a la carga (200 kg) → se acepta.

## Cómo se verificó

1. `git show 04c6e6c` revisado línea por línea contra el hallazgo original.
2. Rama actualizada con el merge de QA que ya trae la PR #390 integrada.
3. `dotnet build -c Release` → 0 errores (un `using` duplicado cosmético,
   heredado del propio merge de QA de la rama, no afecta la compilación ni
   se arrastra a `QA` tras el merge).
4. `dotnet test tests/Nexo.Domain.Tests` → 218/218.
5. `dotnet test tests/Nexo.Application.Tests` → 787/787 (incluye el test
   corregido).
6. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → 840/850, 10 omitidas esperadas
   (servicios externos no disponibles en este entorno).

Ver `verificar.ps1` para el script runnable equivalente.

## Resto de la PR (sin más hallazgos)

- `clsConversoresDeCelda`: conversión toneladas→kg, coma decimal vs. punto de
  miles, fechas día-primero, separador de "ORIGEN - DESTINO".
- `clsAplicarMapeoExcelQuery`: determinista (sin IA), mapeo validado contra
  listas cerradas, límites de fila/columna, reutiliza el validador
  compartido en vez de duplicar la regla de peso.
- Endpoint `POST /api/captura-solicitudes/aplicar-mapeo`, acorde a los
  patrones existentes.

## Resultado

Aprobada y mergeada (`gh pr merge 391 --merge`).
