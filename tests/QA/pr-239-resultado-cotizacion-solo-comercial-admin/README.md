# PR #239 — Marcar resultado de cotización solo para Comercial/Admin (S4-HU-11.1, #213)

## Qué se verificó

Que `POST /api/cotizaciones/{id}/resultado` ahora exige rol `Comercial` o `Admin`
— se quitaron `AdminTarifas` y `CoordinadorFletes` del `[Authorize]`. Cambio
mínimo y aislado: solo el atributo del endpoint + 3 tests nuevos
(`AdminTarifas`/`CoordinadorFletes` → 403, `Comercial` → 200).

## Cómo correrlo

```bash
./verify-suite-completa.sh
```

## Resultado real verificado (2026-09-29)

- `Nexo.Domain.Tests`: 137/137
- `Nexo.Application.Tests`: 226/226
- `Nexo.Api.Tests`: 199/199 (2 omitidos a propósito) contra la base real corporativa
