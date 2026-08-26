# Verificación QA — S2-HU-02.1 (issue #7, PR #59)

Rama: `7-s2-hu-021-contrato-integracion-portal` (se apila sobre HU-01.1/01.2).
Verificación hecha situándose en esa rama, antes de aprobar/mergear el PR #59.

## Qué trae el PR
Documentación pura, `docs/contrato-integracion/`: `README.md` (el contrato en sí), `openapi.yaml` (3 endpoints nuevos: sesión, carga masiva async, consulta de trabajo), `escenarios.feature` (criterios Gherkin para las issues #9/#10/#11/#04, **todavía no implementadas**), `ADR-001-identidad-sin-token.md`. El resto del diff (10 `using`, arnés de CI, borrado de `_to_delete`) ya está en `QA` vía #51/#57/#58.

## Por qué esta herramienta y no otra
Es documentación, no comportamiento en runtime nuevo — pero con una particularidad importante: **la sección 3 del propio README dice "no es propuesta: es lo que el backend exige HOY, leído de los atributos `[Authorize]` de cada controlador"**. Esa frase convierte la tabla en una afirmación verificable contra el código real, así que sí hay algo concreto que probar: que el documento no le mienta al frontend sobre lo que el backend hoy exige.

## Cobertura por tipo de prueba

| Tipo | ¿Aplica? | Resultado |
|---|---|---|
| **Verificación de contrato** (¿la tabla dice la verdad sobre el código?) | Sí — es el caso de prueba principal | ⚠️ **Hallazgo** — ver abajo |
| **Smoke** (¿sigue compilando?) | Sí | ✅ Build succeeded, 0 errores |
| **Regresión** | Sí | `Domain.Tests` 40/40 ✅. `Application.Tests` 41/43 — mismas 2 fallas preexistentes de la rama desactualizada (PR #53, no tocado por este PR) |
| **Integración** (`GET /api/sesion/actual`, escenarios de `escenarios.feature`) | **No, todavía** | El endpoint y la lógica de roles que describen los escenarios **no existen en el código hoy** — son criterio de aceptación de las issues #9/#10/#11, que siguen abiertas. No hay nada que ejecutar contra un sistema que aún no se construyó. Cuando esas issues avancen, `escenarios.feature` es exactamente lo que hay que convertir en tests de integración (el propio archivo lo dice: "cada escenario debería terminar como una prueba") |
| **Carga y estrés** | No aplica | Mismo motivo — no hay endpoint nuevo desplegado, solo su especificación |

## Hallazgo — el contrato no es exhaustivo como afirma ser

```
node tests/QA/hu-02.1-contrato-integracion-portal/verify-contrato.mjs
```

Extrae todos los `[Authorize(Roles=...)]` reales de `src/CabinaFletes.Api/Controllers/*.cs` y los compara contra los controladores que la sección 3 del contrato cubre. **2 controladores con restricción de rol real no aparecen en la tabla:**

- **`TarifasPlazaVigenciaController`** (`PATCH /api/biblioteca-sap-plaza/{id}/vigencia` y ajuste masivo/quitar vigencia) — requiere `AdminTarifas, CoordinadorFletes, Admin`.
- **`TarifasSicetacController.Importar`** (`POST /api/tarifas-sicetac/importar`) — requiere `CoordinadorFletes, Admin`.

Confirmado que `openapi.yaml` tampoco los cubre — pero ahí no es un problema, porque ese archivo está acotado a propósito a los 3 endpoints nuevos del patrón async, no a toda la API. El problema es solo con la tabla del README, que se presenta como la referencia completa para que "el frontend decida qué mostrar" por rol. Si el frontend se guía únicamente por esa tabla, no tendría cómo saber que estas 2 operaciones existen ni qué rol necesitan.

**Impacto:** bajo, y no bloqueante — son 2 filas faltantes en una tabla, no un problema de seguridad ni de código. Recomendación: agregarlas a la sección 3 antes o después de mergear (no es indispensable bloquear el PR por esto, pero sí dejarlo señalado para que no se pierda).

## Nota sobre el riesgo de "suplantación" del ADR-001
El escenario `@riesgo-aceptado` describe la futura identidad sin firma (issue #9, `X-Portal-Usuario-Id`). Ese mecanismo concreto no existe todavía. Lo que sí existe HOY es un riesgo equivalente por otra vía: el esquema `Dev`/`X-Rol` (`DevAuthenticationHandler.cs`) acepta cualquier valor de `X-Rol` que sea un rol válido, sin verificar identidad alguna — quien controle el header controla el rol. Es consistente con lo que el ADR ya reconoce como riesgo aceptado para este sprint (se retira en la issue #11), así que no es una contradicción del documento, solo una aclaración de que el riesgo real hoy tiene una forma distinta a la que describe el escenario Gherkin (que ya anticipa la forma que tendrá después de la issue #9).

## Conclusión
El contrato es sólido y bien razonado (D1-D9, ADR-001 explícito sobre el riesgo). El único hallazgo real es la tabla de autorización incompleta — no bloqueante. Sin objeciones para mergear.
