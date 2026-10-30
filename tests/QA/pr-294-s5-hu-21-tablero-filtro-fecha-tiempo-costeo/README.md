# PR #294 (S5-HU-21.1) — Tablero con filtro de fecha y tiempo de costeo híbrido por tamaño (cierra #287)

## Qué cambia

- `GET /api/metricas/cumplimiento` acepta `desde`/`hasta`: las 5 tarjetas filtran por
  `Solicitud.CreatedAt`, el cumplimiento SLA por `Cotizacion.EntregadaAt`. Licitaciones
  sin filtro.
- `GET /api/metricas/kpis-piloto`: el tiempo de costeo mide el ciclo completo — saga
  `Completado` = `TerminadoEn - SolicitadoEn`; saga `Parcial` = hasta `EntregadaAt` de
  la cotización (si no se entregó, no entra). Corte opcional por `tamano` (`1-10` |
  `11-100` | `+100`, con variantes `100+`/`mas100`/`>100`/el "+" sin codificar).
- Sin cambios de esquema.

## Qué se verificó

**Merge real contra `QA`**: sin conflictos.

**Código revisado línea por línea:** `clsTamanoPliego.Parsear` (todas las variantes de
"+100", `TAMANO_INVALIDO` para cualquier otro valor) y `clsMetricasAdapter` (filtro de
`cumplimiento` por `CreatedAt`/`EntregadaAt` según corresponda; medición híbrida de
`kpis-piloto` por `CurrentState` de la saga, Parcial sin entregar excluido).

**Suite completa contra la base real de pruebas (2026-10-13):**
- `Nexo.Domain.Tests`: 157/157.
- `Nexo.Application.Tests`: 346/346.
- `Nexo.Api.Tests`: primera corrida sin Cubicador → 7 fallos (6 por falta del
  Cubicador local, mismo patrón que en PRs anteriores de esta sesión + 1 ya
  diagnosticado en #292, dato SAP vencido por calendario, ajeno a esta PR). Armé el
  Cubicador real localmente de nuevo → **73/73** en los tests de Cubicación, y
  **44/44** en los tests específicos de Métricas/TamañoPliego (`clsMetricasTableroTests`,
  `clsTamanoPliegoTests`).

**Verificación en vivo (API real contra BD real):**
- `GET /api/metricas/cumplimiento` sin parámetros y con `desde=2026-01-01&hasta=2026-12-31`
  — ambas devuelven datos consistentes (`desde`/`hasta` null vs. con valor, reflejados en
  la respuesta).
- `GET /api/metricas/kpis-piloto?tamano=abc` → `400`.
- `GET /api/metricas/kpis-piloto?tamano=%2B100` (urlencoded) → `200`, `tamano: "+100"`
  en la respuesta.
- `GET /api/metricas/kpis-piloto?tamano=100%2B` (variante `100+`) → `200`.

## Resultado

Sin hallazgos bloqueantes. El único fallo de la suite es el ya documentado en la
verificación de la PR #292 (dato SAP vencido por calendario), no relacionado con esta PR.
