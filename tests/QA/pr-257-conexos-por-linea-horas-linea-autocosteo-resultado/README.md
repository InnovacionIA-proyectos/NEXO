# PR #257 — Servicios Conexos por línea, horas por línea, resultado tras auto-costeo, importación diferenciada (S4-HU-15.3/.4/.5/.6, #249-#252) + revert de `cubicador/` a DEPLOY

## Qué se verificó

**Revisión de código línea por línea, antes de correr nada:**
- **#250 — Conexo ligado a línea**: `clsConexoItem.LineaId` obligatorio; `clsAgregarConexosCotizacionCommandHandler` valida pertenencia (`LINEA_INVALIDA`) antes de resolver el servicio.
- **#251 — Horas por línea**: `clsResolucionHorasLinea.Resolver` (función pura, domain) — override solo si vienen AMBAS horas, override parcial cae al default completo, caso especial de retorno de contenedor (2h totales).
- **#252 — Resultado tras auto-costeo**: `clsMarcarResultadoCommandHandler` salta la exigencia de `Entregada` solo cuando `EsAutoCosteo=true` y el estado es `Borrador` — una cotización manual en Borrador sigue bloqueada.
- **#249 — Importación diferenciada**: `clsEfServiciosConexosRepository` pasa de solo-detectar-duplicados a upsert real (tracked, no `AsNoTracking`), más 4 plantillas Excel por tipo de servicio.

**Esquema de BD:** confirmé que las 3 columnas nuevas (`tbl_lineas_solicitud.HorasCargue/HorasDescargue`, `tbl_cotizaciones.EsAutoCosteo`, `tbl_cotizacion_conexos.CotizacionLineaId` + FK sin cascada) ya estaban aplicadas en `dbInformesAutomatizados_Pruebas` antes de correr la suite — no hizo falta aplicar DDL yo mismo.

**Merge real contra `QA` (no solo diff):** esta rama es más vieja que las PRs #248 y #253 ya mergeadas. Probé el merge real (no solo comparar diffs) y confirmé que las correcciones de seeders de #253 (`reglas_solo_sicetac.csv`, `tarifas_plaza.csv`, `DECISIONES-TECNICAS.md`) sobreviven intactas — git las reconoce como solo-tocadas-por-QA y las conserva. Sin conflictos.

**Suite completa contra la base real de pruebas (2026-10-01):**
- `Nexo.Domain.Tests`: 147/147.
- `Nexo.Application.Tests`: 234/234 — incluye `Linea_que_no_pertenece_a_la_cotizacion_lanza_ValidationFailedException`, `Borrador_auto_costeado_SI_puede_marcar_resultado_sin_pasar_por_entregar`, `Borrador_manual_sigue_exigiendo_Entregada_aunque_tenga_lineas`, `clsResolucionHorasLineaTests` (4/4).
- `Nexo.Api.Tests`: 214/216 (2 skips preexistentes de `clsMailSenderRealTests`). Incluye las 4 plantillas Excel confirmadas individualmente (`almacenamiento`, `itr`, `precinto`, `acompanamiento`), `ImportarExcel_fila_existente_actualiza_fila_nueva_crea`, `ExportarExcel_devuelve_el_catalogo_con_las_12_columnas`.
- Coincide exacto con lo que afirma la PR (147/234/214-216).

## Revert de `cubicador/` a DEPLOY

Confirmado con la suite real de Python (no Docker, mismo resultado): **123/123 passed**. Verificado en el código que el catálogo único de vehículos, la recomendación por peso equilibrada y el evento `FlotaDeCargueGenerada` (todo el trabajo de S4-HU-12, PR #248) **ya no existen** — `desde_linea.py` volvió a la implementación de cascada original. Es intencional y está documentado en la PR, no un descuido.

**⚠️ Consecuencia para QA ya cerrado:** las issues #215/#216 (catálogo único, recomendación por peso — no asignadas a mí, pero su código fue lo que verifiqué para la PR #248) quedan sin efecto en `QA`: el código que las resolvía ya no está. Si alguien retoma S4-HU-12 más adelante, tendrá que rehacerse como PR nueva contra el estado actual de `cubicador/`.

## Resultado

Sin hallazgos bloqueantes. Los 4 features nuevos están completos y verificados contra la BD real; el revert de Cubicador es limpio, intencional y no rompe nada del lado de NEXO (que nunca llegó a consumir el evento de flota).
