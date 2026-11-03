# EDA — Viajes Buenaventura 2025-2026 (S2-HU-11)

Análisis exploratorio sobre el set de viajes de Buenaventura ya entregado al equipo de datos (Habilitador B8). El plan completo, con todas las fases, hallazgos previos y anexos, vive en [`PLAN_EDA_Viajes_Btura.md`](PLAN_EDA_Viajes_Btura.md) — este README es solo la guía para correrlo.

## Insumo y trazabilidad

| Campo | Valor |
|---|---|
| Archivo crudo | `datos/Viajes-Btura-2025-2026.xlsx` (hoja `Hoja1`) |
| Hash MD5 del crudo | `9102c305227a8c37ae6939f643c4861a` (verificado en `00_setup_y_carga.ipynb`) |
| Volumen | 41.237 filas × 17 columnas |
| Ventana temporal | 2025-04-01 → 2026-04-07 (abril 2026 es mes parcial, solo 7 días) |
| Insumo secundario | `datos/inconsistencias_viajes.xlsx` (5 hojas de excepciones) |

Si al releer el crudo el hash no coincide con el de arriba, el archivo cambió — no seguir sin explicar la diferencia (regla de carga defensiva del plan, §2.4).

## Cómo correrlo

```bash
cd analitica/02-eda-viajes-btura
python -m venv .venv
.venv\Scripts\activate          # Windows
pip install -r requirements.txt
jupyter lab notebooks/
```

Correr los notebooks **en orden**, de principio a fin (`Restart & Run All` en cada uno) — cada uno depende de que el anterior haya generado sus artefactos intermedios:

| # | Notebook | Fase del plan | Genera |
|---|---|---|---|
| 00 | `00_setup_y_carga.ipynb` | Fase 1 — Montaje reproducible | `datos/interim/viajes_btura_tipado.parquet` (usado por todos los siguientes) |
| 01 | `01_perfilado_estructural.ipynb` | Fase 2 — Inventario y perfilado | — |
| 02 | `02_calidad_del_dato.ipynb` | Fases 3-4 — Unicidad, granularidad, calidad | — |
| 03 | `03_univariado.ipynb` | Fase 5 — Análisis univariado | `reportes/figures/03_*.png` |
| 04 | `04_bivariado_multivariado.ipynb` | Fase 6 — Bivariado y multivariado | `reportes/figures/04_*.png` |
| 05 | `05_temporal_y_geografico.ipynb` | Fases 7-8 — Temporal y geográfico | `reportes/figures/05_*.png` |
| 06 | `06_outliers_y_sesgos.ipynb` | Fases 9-10 — Outliers y sesgos | — |
| 07 | `07_sintesis_y_limitaciones.ipynb` | Fases 11-12 — Síntesis y cierre | Verifica los números citados en `reportes/` |

Todos los notebooks leen desde `datos/interim/viajes_btura_tipado.parquet` (generado por el `00`), no vuelven a tocar el Excel crudo — así corren rápido y de forma determinista.

**Verificado (2026-09-10):** los 8 notebooks corren limpios con `Restart & Run All`, sin errores, desde cero.

## Qué hay en `reportes/`

- **[`hallazgos.md`](reportes/hallazgos.md)** — cada hallazgo relevante en ficha estándar (evidencia, severidad, recomendación), más el log de supuestos y las recomendaciones al negocio. Los hallazgos marcados 🔎 se verificaron contra la base de datos operativa de la empresa (no reproducibles desde estos notebooks, que solo leen el extracto).
- **[`limitaciones.md`](reportes/limitaciones.md)** — qué preguntas no puede responder este set, por qué, y qué haría falta para responderlas.
- **[`diccionario_reconciliado.md`](reportes/diccionario_reconciliado.md)** — las 17 columnas del extracto, definición oficial + confiabilidad real confirmada en el análisis.
- **`figures/`** — gráficos generados por los notebooks 03-05.

## Alcance y limitaciones clave (resumen — ver `limitaciones.md` para el detalle)

- Este set cubre **únicamente viajes con origen o destino en Buenaventura** — no representa la operación nacional completa (esa se evaluará con otro dataset en un trabajo posterior).
- El set solo contiene FOs en estado `Concluido`/`Ejecutado` (sesgo de selección y supervivencia confirmado) — no sirve para analizar cancelaciones ni viajes en curso. Ya se identificaron las columnas de Estado en `FactFWOFUFO` que resolverían esto en una extracción futura (ver `limitaciones.md`, L-03/L-04).
- `Tarifa Venta al Cliente` **no es confiable** en este extracto (72,2% de las filas son idénticas a `Flete Producción FO`) — cualquier análisis de margen/rentabilidad queda descartado; el foco del EDA y del futuro modelo predictivo es `Flete Producción FO`.
- `FO` no es clave única — una fila no equivale a un viaje; ver `hallazgos.md` (H-001) para la clave compuesta práctica.
- Este corredor representa **≈54,1%** de todo lo `Concluido`/`Ejecutado` de la compañía en la misma ventana (ver `limitaciones.md`, L-11) — es una porción grande de la operación total, no un caso marginal.
