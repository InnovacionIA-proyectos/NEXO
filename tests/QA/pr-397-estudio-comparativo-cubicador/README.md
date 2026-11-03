# QA — PR #397: Estudio comparativo del Cubicador (#366)

## Resumen

PR casi enteramente documental/tooling, **no toca el servicio ni el CI**:

- `docs/ia/ESTUDIO-CUBICADOR-IA.md`: diseño del estudio que compara el motor
  determinista del Cubicador contra una IA en modo sombra — las 5 métricas
  (acierto del vehículo, flota mínima, respeto de capacidad/volumen, latencia
  p95, costo por cubicación), el dataset dorado y la línea base medida sobre
  22 casos sintéticos.
- `evaluacion-ia/cubicador/` (fuera de `cubicador/` a propósito, no redespliega
  nada): `evaluar.py` + `metricas.py` corren el motor real sobre
  `casos/semilla_sintetica.json`; `catalogo_qa.py` es un catálogo calcado para
  correr sin BD; `extraer_cargas_reales.sql` es un **SELECT de solo lectura**
  (verificado: no tiene INSERT/UPDATE/DELETE/MERGE/ALTER/DROP/CREATE) para
  cuando haya que extraer cargas reales a validar por un experto;
  `importar_cargas_reales.py` las convierte en casos; `tests/` son pruebas de
  las cuentas y del importador.
- Dos ajustes puntuales de código para quedar consistente con lo que ya se
  integró en `QA` mientras tanto: `clsProgram.cs` restaura el registro
  `AddDiccionarioClienteUseCases()` (se había perdido en un merge previo) y
  `clsAplicarMapeoExcelTests.cs` ajusta un test existente a la regla de tara
  que ya está en `QA` (peso total puede superar la suma de la carga por la
  tara, pero no al revés). Ninguno de los dos es comportamiento nuevo de esta
  PR.

## Qué se verificó

1. Diff aislado de la PR (3 commits propios además de los merges de
   integración con `QA`/#365, ya verificados en PRs anteriores): el estudio y
   el harness de evaluación en Python, la restauración de
   `AddDiccionarioClienteUseCases` y el ajuste del test de tara.
2. `extraer_cargas_reales.sql` revisado línea por línea: confirmado solo
   lectura (`SELECT`), sin cliente/ruta/descripciones ni texto libre.
3. `dotnet build -c Release` → 0 errores (mismos warnings preexistentes).
4. `python -m pytest evaluacion-ia/cubicador --noconftest` → **26/26**.
5. `dotnet test tests/Nexo.Domain.Tests` → **218/218**.
6. `dotnet test tests/Nexo.Application.Tests` → **901/901**.
7. `dotnet test tests/Nexo.Api.Tests` contra la BD real
   (`dbInformesAutomatizados_Pruebas`) → **870/883**, 13 omitidas: las
   esperadas de siempre (incluidas las 3 de `clsMapeoVehiculoSicetacTests` que
   necesitan `CREATE ANY DATABASE`, ya documentadas en la PR #405).

## Datos de prueba

Ninguno: esta PR no toca base de datos ni red (ni siquiera el script SQL se
corrió contra la BD real — es solo lectura y se revisó por inspección). El
harness de Python no se ejecuta contra ningún servicio.

## Resultado

Verificación técnica completa, sin hallazgos. Pendiente de autorización de
Kevin para comentar/aprobar/mergear en GitHub.
