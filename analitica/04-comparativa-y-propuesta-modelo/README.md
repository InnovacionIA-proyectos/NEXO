# Comparativa y propuesta de modelo(s) — S2-HU-11 parte 3

Tercer entregable de S2-HU-11 (issue #41): contrasta los dos EDA ya
completos —
[`../02-eda-viajes-btura/`](../02-eda-viajes-btura/) (EDA-1, histórico de
viajes Buenaventura) y
[`../03-eda-extraccion-4-fuentes/`](../03-eda-extraccion-4-fuentes/) (EDA-2,
set consolidado de S2-HU-10) — y produce el informe de decisión que pide la
HU. **Ningún modelo se construye este sprint.**

## Qué hay en esta carpeta

| Documento | Contenido | Estado |
|---|---|---|
| [`informe_comparativo.md`](informe_comparativo.md) | Qué aporta cada fuente, métricas de calidad lado a lado, viabilidad de cada objetivo de predicción (con evidencia), grano recomendado, variables candidatas, limitaciones consolidadas | ✅ Primer borrador |
| [`propuesta_modelos.md`](propuesta_modelos.md) | 3 fichas de modelo (SICE-TAC, Flete Producción FO, recomendador de tarifa ganada) con objetivo, grano, features, supuestos y limitaciones — más el diseño de reentrenamiento del modelo de SICE-TAC | ✅ Primer borrador |
| [`recomendacion_captura_datos.md`](recomendacion_captura_datos.md) | Qué ya se consiguió este sprint, qué falta pedir, qué falta mapear/traducir, qué falta confirmar con negocio, y qué fuentes nuevas hay que curar — con prioridad sugerida | ✅ Primer borrador |
| [`declaracion_no_viabilidad.md`](declaracion_no_viabilidad.md) | Los 3 objetivos donde el dato no soporta predicción útil, con evidencia, más el matiz del objetivo de contexto de mercado (no es "no viable por dato", es cobertura de procesamiento pendiente) | ✅ Primer borrador |

## Criterio de cierre de la HU (referencia)

- [x] Informe comparativo publicado (versionado en el repo) — contenido completo, **falta commitear**
- [x] Propuesta de modelo(s) con objetivo, grano, features y supuestos — contenido completo, **falta commitear**
- [x] Recomendación de captura de datos futura — contenido completo, **falta commitear**
- [x] Si el dato no soporta predicción útil: declarado con evidencia — contenido completo, **falta commitear**

Los 4 documentos ya existen con contenido completo. Falta: commit → push → PR a QA (CI verde + 1 revisor) → Kevin valida QA→main.
