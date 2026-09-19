# Comparativa y propuesta de modelo(s)

Tercer entregable de este proyecto de análisis: contrasta los dos EDA ya
completos —
[`../02-eda-viajes-btura/`](../02-eda-viajes-btura/) (EDA-1, histórico de
viajes Buenaventura) y
[`../03-eda-extraccion-4-fuentes/`](../03-eda-extraccion-4-fuentes/) (EDA-2,
set consolidado) — y produce el informe de decisión correspondiente.
**Ningún modelo se construye en esta etapa.**

## Qué hay en esta carpeta

| Documento | Contenido | Estado |
|---|---|---|
| [`informe_comparativo.md`](informe_comparativo.md) | Qué aporta cada fuente, métricas de calidad lado a lado, viabilidad de cada objetivo de predicción (con evidencia), grano recomendado, variables candidatas, limitaciones consolidadas | ✅ Primer borrador |
| [`propuesta_modelos.md`](propuesta_modelos.md) | Modelo 1 (pronóstico SICE-TAC), Modelo 2 (predicción de `Flete Producción FO`/tarifa de techo con `FactFWOFUFO` nacional) y Modelo 3 (recomendador de tarifa ganada) — objetivo, grano, features, supuestos y limitaciones de cada uno, más el diseño de reentrenamiento de los modelos 1 y 2 | ✅ Primer borrador |
| [`recomendacion_captura_datos.md`](recomendacion_captura_datos.md) | Qué ya se consiguió hasta ahora, qué falta pedir, qué falta mapear/traducir, qué falta confirmar con negocio, qué fuentes nuevas hay que curar, y la recomendación de un trabajo de validación de la regla de precio (§7) — con prioridad sugerida | ✅ Primer borrador |
| [`declaracion_no_viabilidad.md`](declaracion_no_viabilidad.md) | Los 3 objetivos donde el dato no soporta predicción útil, con evidencia, más el matiz del objetivo de contexto de mercado (no es "no viable por dato", es cobertura de procesamiento pendiente) | ✅ Primer borrador |

## Criterio de cierre (referencia)

- [x] Informe comparativo publicado (versionado en el repo) — contenido completo, **falta commitear**
- [x] Propuesta de modelo(s) con objetivo, grano, features y supuestos — contenido completo, **falta commitear**
- [x] Recomendación de captura de datos futura — contenido completo, **falta commitear**
- [x] Si el dato no soporta predicción útil: declarado con evidencia — contenido completo, **falta commitear**

Los 4 documentos ya existen con contenido completo. Falta: commit → push → PR a QA (CI verde + 1 revisor) → Kevin valida QA→main.
