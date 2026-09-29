# PRs #231/#233/#235/#237/#238 — SICE-TAC unificado, filtro de bandeja por comercial,
# reestructuración de ServiciosConexos (issues #179/#199/#206/#208/S4-HU-09)

## Qué se verificó

1. **Build + las 3 suites** de toda la cadena, contra la base real corporativa
   (`dbInformesAutomatizados_Pruebas`), antes y después de que Ismael quitara las
   migraciones de EF que la cadena había reintroducido por error (ver hallazgo en
   `HALLAZGO-MIGRACIONES-PR231.md`, ya resuelto).
2. **Desfase de esquema real vs. código**: la #231 sola fallaba 13 tests
   (`clsCotizacionesTests`, `clsServiciosConexosTests`) porque la tabla real
   `tbl_servicios_conexos` ya había sido reestructurada por Sara (issue #223) con
   columnas nuevas (`CodigoServicio`, `TipoMaquina`, etc.), mientras la #231 en
   aislamiento todavía esperaba el esquema viejo (`Nombre`, `ValorBase`). El fix
   real vive en la #237, que sí actualiza el código al esquema nuevo.
3. **Test intermitente identificado, no bloqueante**: `clsSolicitudesTests` —
   pruebas de aislamiento por comercial (`Un_comercial_no_ve_en_la_bandeja...`,
   `El_dueno_si_puede_pedir_el_costeo...`) fallan de forma NO determinística (1 de
   cada 2-3 corridas completas), siempre con `500 Internal Server Error`, pero
   **cada llamada HTTP individual responde bien cuando se aísla** — ver
   `diagnostico-test-intermitente.md` para el detalle de cómo se aisló.

## Cómo correrlo

```bash
./verify-esquema-servicios-conexos.sh   # columnas reales de tbl_servicios_conexos
./verify-suite-completa.sh              # build + 3 suites contra la base real
./verify-flakiness-bandeja.sh 5         # corre Api.Tests 5 veces seguidas, cuenta fallos
```

## Resultado real verificado (2026-09-29)

- `Nexo.Domain.Tests`: 137/137
- `Nexo.Application.Tests`: 226/226
- `Nexo.Api.Tests`: entre 195/198 y 198/198 según la corrida — el único fallo
  intermitente cae siempre dentro del grupo de aislamiento por comercial en
  `clsSolicitudesTests`, nunca en el mismo test dos veces seguidas.
