# PR #240 — Corte de 3 meses obligatorio en sugerencia de tarifas históricas (S4-HU-13.1, #219)

## Qué se verificó

Que `SugerirTarifaHistoricaQuery` (sugerencia automática por ruta+vehículo) ya no
muestra tarifas de más de 3 meses de antigüedad (corte mes-a-mes, ignora el día,
mismo criterio que ya usa `BuscarAsync`). Confirmado que `BuscarBibliotecaTarifasQuery`
(Biblioteca, umbrales 6/12) no se tocó — son ejes independientes.

## Cómo correrlo

```bash
./verify-suite-completa.sh
```

## Resultado real verificado (2026-09-29)

- `Nexo.Domain.Tests`: 137/137
- `Nexo.Application.Tests`: 226/226
- `Nexo.Api.Tests`: 200/200 (2 omitidos a propósito) contra la base real corporativa
