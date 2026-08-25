# Estado de las issues asignadas a kcano-ux

Última revisión: 2026-08-25, tras verificar los PR #48-#60. Se actualiza cada vez que
el trabajo de esta sesión (u otra) cambie el estado de alguna.

## Cerradas (2 de 8)

| Issue | Título | Cerrada por |
|---|---|---|
| [#6](https://github.com/InnovacionIA-proyectos/NEXO/issues/6) | Verificar el gate con un PR saboteado hacia QA | PR #54 (cerrado sin merge) + evidencia de CI real |
| [#24](https://github.com/InnovacionIA-proyectos/NEXO/issues/24) | Rescatar y hacer ejecutables los 26 casos dorados | PR #49, mergeado a `QA` |

## Abiertas (6 de 8)

| Issue | Título | Estado | Bloqueo |
|---|---|---|---|
| [#3](https://github.com/InnovacionIA-proyectos/NEXO/issues/3) | Poner en verde las 3 suites contra la BD de pruebas corporativa | 🟡 2/3 suites verdes (`Domain.Tests` 40/40, `Application.Tests` 43/43). `TEST_DB_CONNECTION` documentada — checkbox marcado | `Api.Tests` necesita acceso real a la BD SQL Server corporativa (host + credenciales SQL + confirmar `CabinaFletes_Test`) |
| [#12](https://github.com/InnovacionIA-proyectos/NEXO/issues/12) | Suite de seguridad automatizada rol×endpoint | 🔴 0/3 criterios — trabajo nuevo, no iniciado | Ninguno ya — su dependencia (contrato S2-HU-02.1) se resolvió con el PR #59. Ojo: agregar `TarifasPlazaVigenciaController` y `TarifasSicetacController.Importar` a la matriz (no están en el contrato, hallazgo de la HU-02.1) |
| [#23](https://github.com/InnovacionIA-proyectos/NEXO/issues/23) | QA del flujo de notificación interna | 🔴 Sin avance posible | Depende de #21 y #22 (ambas abiertas, sin PR) |
| [#34](https://github.com/InnovacionIA-proyectos/NEXO/issues/34) | QA de reglas con fuente provisional | 🔴 Sin avance posible | Depende de #32 y #33 (ambas abiertas, sin PR) |
| [#38](https://github.com/InnovacionIA-proyectos/NEXO/issues/38) | QA de la entrega del set | 🔴 Sin avance posible | Depende de #35, #36 y #37 (las 3 abiertas, sin PR) |
| [#43](https://github.com/InnovacionIA-proyectos/NEXO/issues/43) | Verificación de reglas de negocio con datos reales | 🔴 Sin avance posible | Depende de #42 (abierta) y en cascada del pipeline de datos #39-#41 |

## Qué se hizo esta sesión sobre estas 6

- **#3**: se marcó el checkbox de `TEST_DB_CONNECTION` (ya cumplido) y se dejó comentario con el estado real de las 3 suites. Sigue abierta — falta el acceso corporativo.
- **#12**: se dejó comentario notando que su dependencia ya se resolvió y el hallazgo de los 2 endpoints sin cubrir en el contrato, para que quien la tome no arranque desde cero.
- **#23, #34, #38, #43**: se verificó el estado real de cada dependencia (issue por issue, todas siguen abiertas y sin PR) y se dejó comentario confirmándolo — sin cambio de estado, solo trazabilidad de que se revisaron.

## Cómo seguir
- **#3** se cierra el día que haya acceso a la BD corporativa — no depende de más trabajo de código.
- **#12** ya se puede empezar (su bloqueo se resolvió); es trabajo de construcción, no de verificación.
- **#23, #34, #38, #43** no dependen de ti — dependen de que ivasco-dev y scalderon-bs avancen sus HUs (notificaciones, adaptadores mock, extracción del set, despliegue al ambiente). Vale la pena dar seguimiento a esas en vez de a las tuyas directamente.
