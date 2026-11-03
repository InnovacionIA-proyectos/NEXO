# QA — PR #390: Sugerir el mapeo de columnas de un Excel de cliente con IA

## Resumen

Primera verificación encontró un hallazgo **bloqueante**: el enmascarado de PII
(`clsEnmascaradorDeCeldas`) no cubría nombres de personas en texto libre cuando
el encabezado de la columna no coincidía exactamente con la lista cerrada de
palabras clave (p. ej. encabezados concatenados como `NombreContacto`, o
sinónimos como `"Quien recibe"`). Esto contradice el nivel A de
`docs/decisiones/ia-claude-seguridad-y-datos.md` ("nunca sale, ni siquiera
enmascarado").

Se reportó como bloqueante y no se aprobó hasta que @BS-IVasco corrigió el
problema en el commit `597ab1d` ("Enmascarar nombres de personas, teléfonos
con espacios y columnas por su contenido").

## Corrección verificada

- Separación de encabezados concatenados (`CortePorMayuscula` + detección de
  raíces pegadas) antes de clasificar por palabra clave.
- Lista de frases de encabezado para sinónimos no cubiertos por palabras
  sueltas (`"Quien recibe"`, `"A cargo de"`, `"Razón social"`, etc.).
- Nueva clase `clsNombresDePersona`: detecta nombres de persona en texto libre
  por lista de nombres/apellidos comunes en Colombia + heurísticas (excluye
  ciudades con prefijo San/Santa/Puerto/Villa, exige ≥2 palabras significativas
  o un tratamiento tipo "Sr./Ing.").
- Defensa en profundidad por contenido de columna (`MarcadorPorContenido`):
  si el encabezado no delata nada pero el contenido sí (correo, NIT, teléfono,
  o nombres en ≥10% de las celdas con texto), se oculta la columna **entera**.
- `CONTRATO.md` actualizado, documentando la limitación conocida (un nombre
  poco común en una columna no reconocida como "de personas" puede no
  ocultarse individualmente; la defensa de respaldo es la columna completa).

## Cómo se verificó

1. `git fetch` + diff del commit de corrección (`git show 597ab1d`) revisado
   línea por línea contra el hallazgo original.
2. Merge de `QA` sobre la rama de la PR, resolviendo el conflicto aditivo
   habitual en `src/Nexo.Api/clsProgram.cs` (tres `using`/`AddXxxUseCases()`
   en vez de dos, ver histórico de PRs #389/#391 para el mismo patrón).
3. `dotnet build -c Release` → 0 errores.
4. `dotnet test tests/Nexo.Domain.Tests` → 218/218.
5. `dotnet test tests/Nexo.Application.Tests` → 683/683 (incluye los tests
   nuevos end-to-end que confirman que el mensaje real enviado al modelo ya
   no contiene apellidos ni teléfonos).
6. `dotnet test tests/Nexo.Api.Tests` contra la BD real (`dbInformesAutomatizados_Pruebas`)
   → 832/842, 10 omitidas esperadas (servicios externos como Cubicador/mail
   relay no disponibles en este entorno).

Ver `verificar.ps1` para el script runnable equivalente.

## Resultado

Aprobada y mergeada (`gh pr merge 390 --merge`).
