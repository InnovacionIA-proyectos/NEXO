# QA — PR #342: Plan 3D desde línea — el Cubicador lee codigoTipoMercancia

**PR**: https://github.com/InnovacionIA-proyectos/NEXO/pull/342
**Autor**: ivasco-dev
**Rama**: `337-plan-3d-tipo-mercancia` → `QA`
**Cierra**: #337

## Qué cambia

- El cliente Python del Cubicador (`cubicador/infrastructure/clientes/nexo_client.py`) leía `codigoMercancia` del cuerpo que devuelve NEXO en `GET /api/lineas-solicitud/{id}/cubicacion`, pero NEXO emite el campo como `codigoTipoMercancia`. Como consecuencia, **toda** línea se rechazaba al armar el plan 3D con `MERCANCIA_NO_DECLARADA`, aunque la línea sí traía el tipo de mercancía.
- Fix de una línea: se lee `p.get("codigoTipoMercancia")` en vez de `p.get("codigoMercancia")`.
- `CONTRATO.md` y `cubicador/CONTRATO.md` documentan el nombre correcto del campo.
- Sin cambios de esquema de BD. Sin cambios incompatibles.

## Cómo se verificó

1. `git checkout -B pr342 origin/337-plan-3d-tipo-mercancia` + `git merge origin/QA --no-ff`. Auto-merge en `CONTRATO.md` (también tocado por la PR #341) — verificado manualmente que el texto de ambos cambios quedó íntegro.
2. Revisión de código: el fix es literalmente el nombre de un campo (`codigoMercancia` → `codigoTipoMercancia`), coincide exacto con el síntoma reportado en #337.
3. **Tests de contrato en los dos lados** — la forma más directa de blindar este tipo exacto de bug (nombre de campo entre dos servicios que no comparten tipos):
   - `cubicador/tests/infrastructure/test_nexo_client.py` (nuevo, lado Cubicador): usa el cuerpo JSON **tal cual lo emite NEXO**, con `codigoTipoMercancia`, y verifica que el cliente lo lee correctamente (incluyendo el caso `null` cuando la posición no tiene tipo).
   - `clsLineasSolicitudCubicacionControllerTests.El_tipo_de_mercancia_de_cada_posicion_viaja_como_codigoTipoMercancia` (nuevo, lado NEXO): crea una línea real contra la BD y confirma que el campo en la respuesta JSON real se llama exactamente `codigoTipoMercancia`. Limpia la línea y la solicitud que creó al terminar (`finally`).
4. `dotnet build Nexo.Clean.sln -c Release` → 0 errores.
5. `dotnet test tests/Nexo.Domain.Tests` → **218/218** ✅
6. `dotnet test tests/Nexo.Application.Tests` → **533/533** ✅
7. `dotnet test tests/Nexo.Api.Tests` contra la BD real → **733/733** ✅ (0 con error), **10 omitidos** esperados (6 E2E de Cubicador no levantado para esta verificación — ver nota abajo, 2 de MailSender real, 2 de histórico SAP vacío).
8. `python -m pytest` en `cubicador/` (venv local con `aioodbc`/`pyodbc` instalados) → **275/275** ✅, incluye los 6 tests nuevos de `test_nexo_client.py`.

## Por qué no se levantó el Cubicador local para un E2E completo

Es un fix de una sola línea (nombre de campo), ya cubierto por tests de contrato específicos en ambos lados que usan exactamente el cuerpo real que cada servicio emite/consume — es la forma más confiable de detectar justo este tipo de bug, más que un E2E con RabbitMQ que ejercitaría muchos componentes no tocados por el cambio. Criterio documentado para la próxima sesión: si una futura PR vuelve a tocar el contrato entre NEXO y el Cubicador de forma más amplia, ahí sí vale la pena el E2E completo.

## Nota de entorno (no es un hallazgo de la PR)

Al correr `pytest` por primera vez en el venv local de `cubicador/`, 3 tests de `test_esquema_y_migraciones.py` fallaron con `ModuleNotFoundError: No module named 'aioodbc'` — el venv no tenía esa dependencia instalada (`pip install aioodbc pyodbc`). Tras instalarla, los 275 tests pasan. No es un problema del código de la PR.

## Resultado

Sin hallazgos bloqueantes. PR aprobada y mergeada a `QA` el 2026-10-23.

## Cómo volver a correr esta verificación

```
powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-342-cubicador-codigotipomercancia/verificar.ps1
```

Requiere `.env.pruebas-reales.local` en la raíz del repo, VPN corporativa conectada, y un venv en `cubicador/.venv` con `pip install -r requirements.txt`.
