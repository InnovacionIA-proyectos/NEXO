# QA — PR #341: Captura — aceptar peso total con tara mayor que la suma de las posiciones

**PR**: https://github.com/InnovacionIA-proyectos/NEXO/pull/341
**Autor**: ivasco-dev
**Rama**: `336-peso-con-tara-captura` → `QA`
**Cierra**: #336

## Qué cambia

- `PESO_INCONSISTENTE` solo se dispara cuando las posiciones de una línea suman **más** que el peso total, con tolerancia del 1 % por redondeos. Antes se comparaba con `Math.Abs(diferencia) / peso > 1%` en cualquier dirección; ahora un peso total mayor o igual a la suma de las posiciones (es decir, que incluye la tara: embalaje, estibas, contenedor vacío) se acepta sin límite.
- Mensaje de error más claro: indica explícitamente que el peso total incluye la tara.
- `CONTRATO.md` y `COTIFLY-CONTRATO.md` quedan actualizados — resuelven un hallazgo que ya estaba registrado ahí desde el cierre de #308 ("peso con tara y posiciones se rechazaba incorrectamente").
- Sin cambios de esquema de BD. Sin cambios incompatibles: solo se aceptan casos que antes se rechazaban.

## Cómo se verificó

1. `git checkout -B pr341 origin/336-peso-con-tara-captura` + `git merge origin/QA --no-ff` → limpio, sin conflictos (la rama venía detrás del merge de la PR #338, recién integrado).
2. Revisión de código del diff real (`git diff <merge-base> origin/336-peso-con-tara-captura`, 5 archivos, ~210 líneas) en `clsCapturarSolicitudValidador.cs`:
   - El guard `peso is null or <= 0` (ya existente, sin tocar) garantiza `peso.Value > 0` antes de la nueva comparación `sumaPosiciones > peso.Value * 1.01m`, sin riesgo de división por cero ni de aceptar pesos negativos.
   - Cuando no viene `lr.Peso` explícito, `PesoDeclarado` cae a la suma de las posiciones — en ese caso `peso.Value == sumaPosiciones` exactamente, así que la condición nunca se cumple (consistente con "una línea sin posiciones no cambia" y con que Cotifly pueda seguir mandando peso = suma sin tara).
3. `dotnet build Nexo.Clean.sln -c Release` → 0 errores.
4. `dotnet test tests/Nexo.Domain.Tests` → **218/218** ✅
5. `dotnet test tests/Nexo.Application.Tests` → **533/533** ✅ (igual a lo reportado por el autor)
6. `dotnet test tests/Nexo.Api.Tests` contra la BD real (`.env.pruebas-reales.local`) → **732/732** ✅ (0 con error), **10 omitidos** — los mismos de siempre y esperados: 6 E2E de Cubicador (esta PR no lo toca), 2 de `clsMailSenderRealTests`, 2 de `clsHistoricoSapPaginacionTests` (tabla vacía en este ambiente, documentado desde la PR #335).

## Verificación de valores exactos (boundary del 1 %)

La condición nueva es `sumaPosiciones > peso.Value * 1.01m`. Los tests nuevos verifican el límite exacto, no un caso general:

- `clsCapturarSolicitudValidadorPesoTests.Un_exceso_de_hasta_el_1_por_ciento_de_las_posiciones_sobre_el_peso_total_se_tolera`: 1010 kg de posiciones contra 1000 kg de peso total (exactamente 1000×1.01) → **se acepta**.
- `clsCapturarSolicitudValidadorPesoTests.Un_exceso_de_mas_del_1_por_ciento_de_las_posiciones_sobre_el_peso_total_se_rechaza`: 1011 kg contra 1000 kg → **se rechaza** con `PESO_INCONSISTENTE`.
- `clsCapturaPesoConTaraTests` (contra la API y la BD reales): el caso exitoso (`201`) verifica que el peso guardado en `LineasSolicitud` es exactamente el valor con tara que se envió (`Assert.Equal(1000m, peso)`), no solo que la respuesta fue `201`.

## Limpieza de datos de prueba

Los tests de `clsCapturaPesoConTaraTests` crean su propia solicitud contra la BD real y la borran en `finally` cuando la captura fue exitosa (caso `201`); el caso rechazado (`400`) no llega a crear nada, y el test lo confirma explícitamente (`Assert.False(... db.Solicitudes.Any(...))`).

## Resultado

Sin hallazgos bloqueantes. PR aprobada y mergeada a `QA` el 2026-10-23.

## Cómo volver a correr esta verificación

```
powershell -NoProfile -ExecutionPolicy Bypass -File tests/QA/pr-341-peso-con-tara-captura/verificar.ps1
```

Requiere `.env.pruebas-reales.local` en la raíz del repo y VPN corporativa conectada (acceso a `10.10.40.85`).
