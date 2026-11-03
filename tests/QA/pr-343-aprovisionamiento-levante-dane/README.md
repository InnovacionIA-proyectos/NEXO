# QA — PR #343: Estimación del levante — la ruta se compara por código DANE

**PR**: https://github.com/InnovacionIA-proyectos/NEXO/pull/343
**Autor**: ivasco-dev
**Rama**: `339-aprovisionamiento-levante-dane` → `QA`
**Cierra**: #339

## Qué cambia

- `GET /api/captura-solicitudes/aprovisionamiento` comparaba la ruta del tramo vacío (levante → origen) por texto contra el nombre de SICE-TAC, y no encontraba `"BOGOTA, D.C."` (como lo entrega `GET /api/captura-solicitudes/ciudades`) porque SICE-TAC escribe `"BOGOTA D. C."`.
- Ahora `levante` y `origen` se resuelven primero a su municipio (`tbl_municipios`) y la ruta se compara por código DANE, igual que ya hace el costeo de la línea. Si alguna de las dos no resuelve a un único municipio, cae al comportamiento anterior (texto).
- Sin cambios de esquema de BD. Sin cambios incompatibles (solo amplía qué formatos de ciudad encuentran el piso).

## Cómo se verificó

1. `git checkout -B pr343 origin/339-aprovisionamiento-levante-dane` + `git merge origin/QA --no-ff`. Hubo auto-merge en `CONTRATO.md` (lo toca también la PR #341, ya en `QA`) — se verificó manualmente con `grep` que el texto de ambos cambios quedó íntegro, sin corrupción.
2. Revisión de código en `clsPropuestaTarifaAdapter.cs`: usa `ResolverDaneCiudadAsync` (método ya existente, sin tocar, que devuelve `null` ante ambigüedad) para resolver `ciudadLevante` y `origen`; si cualquiera de los dos no resuelve, cae a `(null, null)` y el camino aguas abajo sigue comparando por texto como antes — no se pierde cobertura del caso ambiguo.
3. `dotnet build Nexo.Clean.sln -c Release` → 0 errores.
4. `dotnet test tests/Nexo.Domain.Tests` → **218/218** ✅
5. `dotnet test tests/Nexo.Application.Tests` → **533/533** ✅
6. `dotnet test tests/Nexo.Api.Tests` contra la BD real + DWSice (`.env.pruebas-reales.local`) → **738/738** ✅ (0 con error), **10 omitidos** — los mismos esperados de siempre (6 E2E de Cubicador no tocado por esta PR, 2 de MailSender real, 2 de histórico SAP con tabla vacía en este ambiente).

## Verificación de valores exactos

El test nuevo `La_estimacion_del_levante_encuentra_el_piso_de_Bogota_se_escriba_como_se_escriba` (contra la BD y el DWSice reales) pide la estimación con `"BOGOTA D. C."` como referencia y la compara, cifra por cifra (`Assert.Equal(referencia.Valor, candidato.Valor)`), contra la misma estimación pedida con `"BOGOTA, D.C."` y con `"Bogotá"` — las tres deben dar exactamente el mismo piso, con y sin carrocería. El test `La_estimacion_de_una_ciudad_que_no_existe_no_devuelve_candidato` confirma el caso negativo: una ciudad inexistente en levante u origen devuelve `candidato: null`, no un valor por defecto.

## Resultado

Sin hallazgos bloqueantes. PR aprobada y mergeada a `QA` el 2026-10-23.

## Cómo volver a correr esta verificación

```
powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-343-aprovisionamiento-levante-dane/verificar.ps1
```

Requiere `.env.pruebas-reales.local` en la raíz del repo y VPN corporativa conectada (acceso a `10.10.40.85` y `10.10.40.24`, este último para DWSice).
