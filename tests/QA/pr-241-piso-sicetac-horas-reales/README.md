# PR #241 — Recalcular piso SICE-TAC con horas reales de la solicitud (S4-HU-14.1, #221)

## Qué se verificó

Que la propuesta automática de piso SICE-TAC recalcula con las horas de
cargue/descargue REALES de la solicitud (`clsPisoConHoras`), en vez de asumir
siempre las horas pactadas — y que sin horas capturadas o con horas iguales a
las pactadas, el resultado es idéntico al de antes (sin regresión). También
corrige un bug gemelo en `clsTarifarioSapAdapter.SicetacOficialAsync`.

## Cómo correrlo

```bash
./verify-suite-completa.sh
```

## Resultado real verificado (2026-09-29)

- `Nexo.Domain.Tests`: 143/143 (6 tests nuevos de `clsPisoConHoras`)
- `Nexo.Application.Tests`: 226/226
- `Nexo.Api.Tests`: 1er intento 199/200 (1 fallo, `clsCotizacionesTests.Flujo_completo_cotizacion`,
  500); 2do intento 200/200 limpio. Es la misma intermitencia ya documentada en
  `tests/QA/pr-231-237-sicetac-y-servicios-conexos/diagnostico-test-intermitente.md`
  (condición de carrera de infraestructura de pruebas), no una regresión de esta PR —
  confirmado reproduciendo 2 veces.
