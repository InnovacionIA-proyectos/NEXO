# Reporte de inconsistencias — Set final Habilitador B8

Principio aplicado: **ningún registro incompleto o contradictorio se descarta
en silencio** — cada uno queda aquí con su causa. Un faltante también es
hallazgo.

## Resumen

| # | Hallazgo | Tipo | Severidad | Bloquea el uso del set |
|---|---|---|---|---|
| 1 | Bajo volumen de datos reales (13 solicitudes) | Estructural | Alta | No — pero limita el alcance de cualquier conclusión |
| 2 | `Deadline` anterior a `CreatedAt` (1 caso real) | Registro puntual | Baja | No |
| 3 | Ruta malformada, origen "b" destino "j" | Registro puntual | Baja | No — 1 sola línea afectada |
| 4 | Verificación cruzada contra fuentes externas | Validación (positiva) | — | No — 14/15 confirmadas, 1 no verificable por el punto 3 |
| 5 | `consultascubos` con permisos de escritura | Seguridad, heredado de S2-HU-10.2 | Media | No — pero requiere acción de IT |

## 1. Bajo volumen de datos reales (hallazgo estructural, no un registro puntual)

**Qué se encontró:** el lado propio de este set son solo **13 solicitudes, 18
cotizaciones y 48 líneas de cotización** — no es producción real, es data que
generaste probando manualmente la aplicación (filtrado `CreatedAt >= 2026-08-22`
para excluir las ~30 filas del `DataSeeder`, del 2026-08-21).

**Causa:** la aplicación está en construcción, no hay instancia de producción
todavía.

**Por qué se documenta en vez de descartarse:** es real (pasó por el motor de
costeo de verdad, contra los DW reales), a diferencia del `DataSeeder` — pero
quien use este set para el EDA (S2-HU-11) debe saber que **no tiene escala de
producción** y que las conclusiones que saque están limitadas a esta muestra
pequeña.

## 2. `Deadline` anterior a `CreatedAt` — 1 caso real (de 16 que detecta la regla completa)

| Campo | Valor |
|---|---|
| `SolicitudId` | `3EAB0AB6-ED6D-4983-A8EA-F9BF7406AB0A` |
| `Tipo` | Licitación |
| `Deadline` | 2026-08-26 16:44 |
| `CreatedAt` | 2026-08-31 16:44 |

**Causa:** la solicitud se creó 5 días **después** de su propio plazo límite —
o bien fue una captura de prueba con fecha de deadline puesta manualmente sin
cuidado, o el flujo real permite crear solicitudes con esta inconsistencia sin
bloquear. No se investigó el flujo de captura para confirmar cuál de las dos.

**Nota de alcance:** la regla completa (`analitica.inconsistencias`) devuelve
16 filas, pero **15 son del `DataSeeder`** (dato sintético, del 2026-08-21) —
se excluyeron de `bd-propia/inconsistencias.csv` para no mezclar hallazgos
reales con datos de prueba fabricados. Si se necesita ver las 15 restantes,
están en la vista `analitica.inconsistencias` completa, no en este set.

## 3. Ruta malformada: origen "b", destino "j"

| Campo | Valor |
|---|---|
| `LineaCotizacionId` | `CA0FC8A8-6AB8-4206-9794-17DD283F2BD3` |
| `RutaId` | `0571BA81-5C4D-4325-A957-30163A4F6BBB` |
| `Origen` / `Destino` | `b` / `j` |
| `ValorFlete` | $5.783.697 |
| `Fuente` | `Sicetac` |
| Fecha | 2026-08-24 |

**Causa:** `rutas` se crea dinámicamente a partir del texto libre capturado en
la solicitud (confirmado en `diccionario-y-linaje.md`, sección
de la tabla `rutas`) — no hay validación de longitud mínima ni de que el texto
sea un nombre de ciudad real. Esta fila probablemente vino de una prueba
manual rápida con datos de relleno.

**Impacto:** esta línea **no se pudo verificar** contra el piso SICE-TAC (ver
punto 4) — no porque el dato de mercado esté mal, sino porque "b"/"j" no son
ciudades reales contra las que cruzar.

## 4. Verificación cruzada: ¿el valor cotizado existe de verdad en la fuente externa?

Se tomaron las 15 combinaciones únicas de ruta+valor con `Fuente` en
`Sicetac`/`Tarifario` (excluyendo `Manual` e `Historica`, que no vienen de un
DW externo) y se buscó cada valor exacto en el extracto/tabla correspondiente.

| Resultado | Cantidad |
|---|---|
| Coincide exacto contra la fuente externa | **14 / 15** |
| No verificable (ruta malformada, ver punto 3) | 1 / 15 |

**Las 6 líneas con `Fuente=Sicetac` no coincidían en el primer intento contra
la columna `VALOR` de `tblRutasSicetac` (0/6) — coincidieron el 100% (6/6) al
comparar contra `VALORSICECONHORA`.** Esto reveló que el motor real usa
`VALORSICECONHORA` (valor + componente por hora) como el piso efectivo, no
`VALOR` — corrección ya aplicada en
`./datos/fuentes-corporativas/README.md`.

Las 8 líneas con `Fuente=Tarifario` coincidieron 8/8 contra `VALOR` de
`tblSAPTarifas` en el primer intento.

**Conclusión:** el pipeline real (captura → matching contra el DW → valor
cotizado) funciona correctamente en el 100% de los casos verificables. La
única falla es de captura de datos (ruta "b"/"j"), no del motor de costeo.

## 5. Permisos de escritura en `consultascubos` (heredado de S2-HU-10.2)

Ya documentado en `./datos/fuentes-corporativas/README.md`: la cuenta
`consultascubos` tiene `INSERT`/`UPDATE`/`DELETE` en `DWCubosOperacionesEBS`
(catálogo de clientes), no solo `SELECT`. Se repite aquí porque este set
también depende de esa fuente — no es un hallazgo nuevo, es el mismo riesgo
heredado.

## Resumen para quien use este set en el EDA (S2-HU-11)

- Trátalo como una muestra pequeña y real, no como producción a escala.
- `Fuente=Sicetac`/`Tarifario` en `cotizacion_lineas` es confiable — se probó
  contra el DW real y coincide.
- Descarta o trata aparte cualquier ruta con texto de 1-2 caracteres si vuelves
  a generar datos de prueba — no son ciudades reales.
- Si necesitas volumen de producción real, este set no lo tiene todavía; habrá
  que re-ejecutar esta misma extracción cuando exista.
