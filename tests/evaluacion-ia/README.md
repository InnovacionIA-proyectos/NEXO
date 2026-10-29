# Evaluación de la IA del mapeo de Excel (S6-HU-32.5, #353)

Un examen para la IA que propone a qué campo de NEXO va cada columna del Excel de un cliente (`sugerir-mapeo`).

| Carpeta | Qué es |
|---|---|
| [`dataset/`](dataset) | Las preguntas y las respuestas: 7 Excels anonimizados y la respuesta correcta de cada uno. Ver su README |
| [`arnes/`](arnes) | El que califica: le da cada Excel a la IA, compara con la respuesta y escribe el informe |
| `informes/` | Los informes de cada corrida (markdown) y los resultados crudos (JSON) |

## Qué mide el arnés

- Usa el **mismo código de producción**: el lector de `analizar-excel` y el handler de `sugerir-mapeo` con el adaptador real de Anthropic
  (mismo prompt, mismo enmascarado, mismas validaciones). Solo cambian el modelo y el esfuerzo, que en producción son configuración.
- Mide a la **IA sola**: sin cliente, sin plantilla y con el diccionario vacío. En producción el diccionario resuelve antes algunas
  columnas, así que el resultado real es igual o mejor.
- Por cada combinación de modelo y esfuerzo: acierto en las columnas con campo, falsos positivos, acierto total, por caso y por formato,
  columnas que requieren despivotar, errores más frecuentes, costo por Excel, latencia p50 y p95, y fallos.
- Deja una **sugerencia automática** con su regla a la vista. La **recomendación** la escribe una persona en el informe.

## Cómo correrlo

Necesita en el `.env` de la raíz (o como variables de entorno) `Anthropic__ApiKey` y, si la llave es de toda la organización,
`Anthropic__WorkspaceId`. La llave no se escribe nunca en el informe ni en la consola.

```bash
# Desde la raíz del repo. Primero en seco: verifica los casos sin llamar a la IA.
dotnet run --project tests/evaluacion-ia/arnes -- --seco

# Corrida completa por defecto: Sonnet 4.6 y 5.5 × esfuerzo low, medium y high × 3 repeticiones (126 llamadas).
dotnet run --project tests/evaluacion-ia/arnes

# Opciones
#   --modelos claude-sonnet-5-5,claude-haiku-5-5   --esfuerzos low,high   --repeticiones 1
#   --casos caso_01,caso_05   --paralelo 3   --timeout 60
```

Cada llamada cuesta centavos de dólar; la corrida completa, unos pocos dólares (el informe trae el total estimado).

## Qué no hace

- **No corre en CI**: el proyecto no está en `Nexo.Clean.sln`, que es lo único que compila y prueba el pipeline.
- **No le manda a la IA ningún Excel real**: solo las réplicas anonimizadas del dataset.
- **No mide la línea de negocio que viene en el nombre de la hoja**: `sugerir-mapeo` no recibe el nombre de la hoja. Se trata en NEXO#412 y
  administracion#469.
