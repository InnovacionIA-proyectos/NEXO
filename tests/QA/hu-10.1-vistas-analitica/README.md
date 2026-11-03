# Verificación QA — S2-HU-10.1 (issue #35, PR #82)

Rama: `35-s2-hu-101-vistasexportes-de-extracción-sobre-la-bd-cabinafletes`.
Verificación hecha antes de aprobar/mergear. Merge real contra `origin/QA`
(no solo `merge-tree`) sin conflictos.

## Qué trae el PR

- Migración `20260831194721_VistasAnaliticaSoloLectura`: crea el schema
  `analitica` con **12 vistas de solo lectura** (`WITH (NOLOCK)`) sobre las
  tablas operativas (`solicitudes`, `cotizaciones`, `lineas_solicitud`,
  `cotizacion_lineas`, `tarifas_historicas`, `audit_log`, `clientes`,
  `usuarios`, `servicios_conexos`, etc.), excluyendo explícitamente columnas
  sensibles (datos de contacto de clientes, credenciales/roles crudos de
  usuarios, etc.).
- `analitica.inconsistencias`: 1 vista adicional que corre 9 reglas de calidad
  de datos documentadas (UNION ALL), pensada para que el equipo de datos
  detecte registros operativos inválidos sin tocar el negocio.
- `docs/analitica/DICCIONARIO_DATOS.md`: diccionario de los 13 objetos, con
  columnas, tipo y exclusiones justificadas.

Todo el PR es SQL/documentación — no toca código C# de la aplicación.

## Verificación — reproducida de forma independiente, no solo leída

### Regresión completa

| Suite | Resultado |
|---|---|
| `Domain.Tests` | 127/127 |
| `Application.Tests` | 121/121 |
| `Api.Tests` | 103/105 (+2 saltadas por diseño) |

Build `--no-cache`: 0 errores. Números idénticos a los de PR #80, como se
espera de un PR que no agrega pruebas C# (es puro SQL/docs).

### Existencia y forma de los 13 objetos

Con la app levantada (`docker compose up -d --build --wait`) y migraciones
aplicadas, confirmado por consulta directa a `sys.views`/`sys.schemas` que
existen las 12 vistas documentadas más `analitica.inconsistencias` — 13/13.

### Exclusión de columnas sensibles — verificado, no solo leído del código

Consulta directa a `INFORMATION_SCHEMA.COLUMNS` comparando cada vista contra
su tabla base, confirmando que las columnas marcadas como excluidas en el
diccionario de datos **realmente no están** en la vista correspondiente:

| Tabla base | Columna excluida | Confirmado ausente en la vista |
|---|---|---|
| `clientes` | datos de contacto directo | ✅ |
| `usuarios` | credenciales/roles crudos | ✅ |
| `servicios_conexos` | campos internos no documentados | ✅ |
| `solicitudes` | — (sin exclusiones) | ✅ (todas presentes, como documenta el diccionario) |
| `lineas_solicitud` | — (sin exclusiones) | ✅ |
| `audit_log` | — (sin exclusiones, `UsuarioId` nullable documentado para registros legacy) | ✅ |

### `analitica.inconsistencias` — las 9 reglas, probadas de forma independiente

Script [`verify-inconsistencias.sql`](verify-inconsistencias.sql): inserta un
set de registros **limpio** (no debe aparecer) más **10 filas** rotas — una
por cada una de las 9 reglas documentadas, más una fila (`cot_bad1`) que
dispara **2 reglas a la vez** (entregada sin fecha/entregadoPor, y además
sirve de base para la regla de tarifa histórica sobre cotización no ganada) a
propósito, para probar que el `UNION ALL` no deduplica ni se confunde con
coincidencias múltiples.

| # | Regla | Esperado | Observado |
|---|---|---|---|
| 1 | `cotizaciones` Entregada sin `EntregadaAt`/`EntregadaPor` | aparece | ✅ |
| 2 | `cotizaciones` con `Resultado` sin haber sido entregada | aparece | ✅ |
| 3 | `cotizaciones` Entregada sin ninguna línea de flete | aparece | ✅ |
| 4 | `cotizacion_lineas.ValorFlete` en cero o negativo | aparece | ✅ |
| 5 | `tarifas_historicas` referencia cotización no Ganada | aparece | ✅ |
| 6 | `tarifas_historicas.Valor` en cero o negativo | aparece | ✅ |
| 7 | `lineas_solicitud` sin `RutaId` | aparece | ✅ |
| 8 | `lineas_solicitud.Peso` nulo o en cero | aparece | ✅ |
| 9 | `solicitudes.Deadline` anterior a `CreatedAt` | aparece | ✅ |

Resultado: exactamente **11 filas** (10 malas + 1 coincidencia doble), cada
una con el `Motivo` exacto documentado en la migración. **Cero falsos
positivos** contra el baseline limpio (ninguna de sus 6 filas aparece en el
resultado).

### ⚠️ Hallazgo real, no bloqueante — `NOLOCK` produce lecturas sucias demostrables, y ya no hace falta

Se probó el efecto del `NOLOCK` de las vistas de dos formas, no solo se leyó
el comentario del código:

**1) Bloqueos** — transacción de escritura larga en background + lectura
concurrente por otra sesión. La lectura normal (**sin** `NOLOCK`) tampoco
quedó bloqueada por la escritura en curso — no gracias al `NOLOCK`, sino
porque `sys.databases.is_read_committed_snapshot_on = 1` en `Nexo`:
RCSI ya está encendido a nivel de base de datos, así que las lecturas
normales ya no bloquean escritores (usan versionado de filas, no locks de
lectura).

**2) Lecturas sucias — reproducido y confirmado** ([`verify-dirty-read-nolock.sql`](verify-dirty-read-nolock.sql)):
se insertó una fila en `dbo.solicitudes` dentro de una transacción, **sin
COMMIT**, y se dejó abierta 10 segundos antes de un `ROLLBACK` explícito
(o sea, esa fila nunca llegó a existir de verdad). Mientras la transacción
seguía abierta:

| Lectura | Fila visible durante la transacción abierta | Fila visible después del `ROLLBACK` |
|---|---|---|
| `analitica.solicitudes` (`WITH NOLOCK`) | **Sí (1)** — dato sucio | No (0) — correcto |
| `dbo.solicitudes` (sin `NOLOCK`, protegida por RCSI) | No (0) — correcto | No (0) — correcto |

Es decir: la vista `analitica.solicitudes` mostró una fila que **nunca
existió realmente** — si el equipo de datos hubiera exportado justo en ese
instante, se habría llevado un registro fantasma. Una lectura equivalente
sin `NOLOCK` sobre la misma tabla, en la misma base, **no tuvo ese problema y
tampoco se bloqueó**, porque RCSI ya resuelve el objetivo original del
`NOLOCK` (no bloquear al escritor) sin el costo de leer datos sin confirmar.

**No es un bug que rompa la HU** (las vistas funcionan, las 9 reglas
funcionan, y en la operación real este escenario es breve y poco frecuente:
requiere leer justo durante los milisegundos de una transacción abierta que
luego se revierte) — pero es un riesgo real y ya innecesario, dado que RCSI
cubre el mismo objetivo sin este costo. Recomendación para el equipo (no
aplicada, decisión de diseño de ellos): quitar `WITH (NOLOCK)` de las 13
vistas y apoyarse en RCSI, que ya da lecturas consistentes y no bloqueantes
sin el riesgo de dato fantasma.

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ Build `--no-cache`, 0 errores; app levanta y `/health` sano |
| Regresión | ✅ 127+121+105 = 353 pruebas, sin filtros, cero fallos |
| Integración | ✅ 13/13 objetos existen; exclusiones de columnas sensibles confirmadas por `INFORMATION_SCHEMA` |
| Calidad de datos | ✅ Las 9 reglas de `analitica.inconsistencias` probadas una por una, incluida coincidencia múltiple, cero falsos positivos, reproducido dos veces en dos stacks distintos |
| Seguridad/integridad de datos | ⚠️ Hallazgo real y reproducido: `NOLOCK` permite leer una fila que luego se revierte (dato fantasma) — no bloqueante, RCSI ya activo hace innecesario el `NOLOCK` |

## Conclusión

Las vistas y el diccionario de datos hacen exactamente lo que documentan: 13
objetos existen, las columnas sensibles declaradas como excluidas
efectivamente no están expuestas, y las 9 reglas de `analitica.inconsistencias`
detectan correctamente cada tipo de inconsistencia sin falsos positivos,
incluido el caso de coincidencia múltiple — reproducido de forma idéntica en
dos corridas independientes del stack. No se encontró ningún bug que rompa la
HU ni ninguna fuga de datos sensibles. Sí se encontró y reprodujo un riesgo
real de integridad (`NOLOCK` = posibilidad de leer filas "fantasma" que luego
se revierten), documentado arriba con evidencia; no bloquea esta PR (el
comportamiento es el que el propio PR documenta a propósito), pero es una
recomendación real y accionable para el equipo dado que RCSI ya lo vuelve
innecesario. La permisología acotada al esquema `analitica` (cuenta de solo
lectura para el equipo de datos) queda pendiente para cuando haya acceso a
producción — ya documentado por el propio autor en `PENDIENTES.md`, no es un
hallazgo nuevo. Sin objeciones para mergear.
