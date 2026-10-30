# QA — PR #417: Dataset dorado y arnés de evaluación de la IA del mapeo de Excel (#353)

## Resumen

Herramienta de evaluación para medir qué tan bien propone Claude el mapeo
de columnas de un Excel de cliente (`sugerir-mapeo`). **No toca ningún
archivo de `src/`** y el proyecto (`tests/evaluacion-ia/arnes/Nexo.EvaluacionIa.csproj`)
**no está en `Nexo.Clean.sln`** — no compila ni corre en CI, es una
herramienta bajo demanda.

- `tests/evaluacion-ia/dataset/`: 7 casos (réplicas anonimizadas de Excels
  reales de 4 clientes, en 3 formatos: fila por línea, vehículo en el
  encabezado, matriz) + la respuesta correcta de cada uno (`campo` y
  `transformacion` por columna, con alternativas aceptadas).
- `tests/evaluacion-ia/arnes/`: usa el **mismo código de producción**
  (lector de `analizar-excel` + handler de `sugerir-mapeo` + adaptador
  real de Anthropic) para correr cada caso contra el modelo real y
  calificar la propuesta contra la respuesta correcta.
- Anonimización documentada y verificable: ciudades reemplazadas por
  ciudades reales del catálogo DANE (consistentes dentro de cada caso),
  números inventados, notas y títulos genéricos; se conserva el
  vocabulario que el modelo debe reconocer (vehículos, SICE-TAC,
  contenedores). Ningún Excel real ni sus datos entran al repositorio.

## Qué se verificó

1. Confirmado que el proyecto no está referenciado en `Nexo.Clean.sln` ni
   en ningún archivo de `src/`: cambio aislado, sin riesgo para
   producción.
2. **Modo seco** (`dotnet run --project tests/evaluacion-ia/arnes -- --seco`,
   sin llamar a la IA): confirma que los encabezados que produce el lector
   real de `analizar-excel` sobre las 7 réplicas coinciden exactamente con
   los índices de las respuestas doradas → **"los encabezados de las
   réplicas coinciden con las respuestas"**, sin fallos.
3. **Autoprueba** (`--autoprueba --repeticiones 1`, con una IA falsa que
   responde exactamente lo correcto): **100 % de acierto en columnas con
   campo, 0 falsos positivos, 0 fallos**, para las 6 combinaciones
   modelo×esfuerzo — confirma que el arnés mide bien lo que dice medir.
   Los archivos de informe generados por esta autoprueba se borraron
   (eran mi propia corrida de verificación, no parte de la PR).
4. `dotnet build -c Release` (solución principal) → 0 errores (mismos
   warnings preexistentes, ninguno nuevo).
5. `dotnet test tests/Nexo.Domain.Tests` → **218/218** (sin cambios,
   como se esperaba).
6. `dotnet test tests/Nexo.Application.Tests` → **1168/1168** (sin
   cambios, como se esperaba).
7. `dotnet test tests/Nexo.Api.Tests` contra la BD real → **1094/1116**,
   22 omitidas (las mismas ya documentadas en PRs anteriores) — sin
   cambios respecto al estado previo, confirmando que esta PR no afecta
   el servicio.

## Datos de prueba

Ninguno: el arnés no toca la BD ni la red salvo cuando se corre una
evaluación real (que cuesta dinero y requiere la llave de Anthropic,
fuera del alcance de esta verificación). El modo `--seco` y la
`--autoprueba` no llaman a ningún servicio externo.

## Resultado

Verificación técnica completa, sin hallazgos bloqueantes.
