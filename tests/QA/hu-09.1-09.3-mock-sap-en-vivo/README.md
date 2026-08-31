# Verificación QA — S2-HU-09.1/09.3 (issues #31, #33, PR #80)

Rama: `33-s2-hu-093-adaptadores-mock-sustituibles-con-conmutación-por-configuración`
(ivasco-dev). Verificación hecha antes de aprobar/mergear. Sin conflictos de merge.

## Qué trae el PR

- `ISapTarifaFleteEnVivoPort`: contrato de la conexión SAP nueva (plaza en vivo),
  hoy inexistente en la realidad — el equipo de conexiones aún no la construye.
- `SapTarifaFleteEnVivoMockAdapter`: dataset provisional (10 rutas, valor base +
  escalado lineal por peso), activo solo con `SapTarifaFleteEnVivo:Modo=Mock`.
- `SapTarifaFleteEnVivoDeshabilitadoAdapter`: el default — nunca produce candidato,
  `PropuestaTarifaAdapter` sigue exactamente igual que antes de esta HU.
- Conectado a `PropuestaTarifaAdapter` como sustitución **aditiva** del candidato
  "plaza" — `ReglaDeCosteo` sin cambios, las 4 fuentes reales (DwSice, DwCubos,
  DbSolucionesTi) quedan intactas.

## ⚠️ Bug real encontrado — documentado, sin bloquear (decisión de Kevin)

Con `Modo=Mock` activo, una línea que el mock resuelve queda **`AutoResuelta`** y con
el **valor correcto** — pero la columna `Fuente` de `cotizacion_lineas` queda grabada
como **`Manual`**, no como algo que indique que salió de la conexión SAP en vivo.

**Causa raíz**: `SapTarifaFleteEnVivoMockAdapter` etiqueta su candidato con el texto
`"sap_en_vivo_mock"` (`CandidatoTarifaDto.Fuente`). Ese texto viaja intacto por
`ReglaDeCosteo` (que solo repite el `.Fuente` del candidato ganador) hasta el switch
de `CapturarSolicitudCommand.cs`:

```csharp
Fuente = p.FuentePropuesta switch
{
    "sicetac" => FuenteFlete.Sicetac,
    "plaza" => FuenteFlete.Tarifario,
    "historico_sap" => FuenteFlete.Historica,
    _ => FuenteFlete.Manual,   // <- aquí cae "sap_en_vivo_mock"
},
```

El enum `FuenteFlete` (`Sicetac, Historica, Manual, Tarifario`) tampoco tiene ningún
valor para "SAP en vivo" hoy.

**Por qué importa más allá del modo Mock**: el propio PR documenta esto como
"sustitución aditiva del candidato plaza". El día que el equipo de conexiones
construya el adaptador REAL (no el mock), va a heredar el mismo problema —
cualquier línea resuelta por la conexión SAP en vivo real quedaría también mal
etiquetada como `Manual` en producción, y cualquier reporte o regla que distinga
"resuelto automático" de "necesita revisión manual" mentiría para esas líneas.

**Reportado a Kevin, quien decidió**: documentarlo aquí sin tocar código ni bloquear
esta PR — el equipo decide el diseño correcto (¿nuevo valor de enum, o que el
mock/adaptador real se etiquete como `"plaza"` ya que el PR dice que lo sustituye?).

**Verificado, no solo inferido del código**: confirmado por consulta SQL directa
sobre 8 líneas distintas (3 de escalado de peso + 5 de rutas concurrentes) — las 8
mostraron `Fuente = Manual` a pesar de haberse resuelto automáticamente con el valor
exacto del mock.

## Verificación — reproducida de forma independiente, no solo leída

### Regresión completa

| Suite | Resultado |
|---|---|
| `Domain.Tests` | 127/127 |
| `Application.Tests` | **121/121** (coincide exacto) |
| `Api.Tests` | **103/105** (+2 saltadas — coincide exacto) |

Build `--no-cache`: 0 errores.

### Comportamiento por defecto (Deshabilitado) — sin cambios frente a antes de esta HU

Con la configuración por defecto, una línea Bogotá→Medellín (ruta real, sin datos
locales de DW): `fletesCalculados: 0, lineasPorCostear: 1` — idéntico al
comportamiento de siempre en este entorno (sin acceso a DW corporativo, mismo
bloqueo que la issue #3). Confirma la afirmación central del PR: nada cambia si no
se enciende el modo.

### Modo Mock — valor exacto, incluido el escalado por peso

Con `SapTarifaFleteEnVivo:Modo=Mock`, la misma ruta resuelve sola:
`fletesCalculados: 1, lineasPorCostear: 0`, valor **`$2.850.000`** — exacto al
dataset (`Bogota↔Medellin`, peso base 8000kg, factor 1.0).

[`verify-escalado-peso.mjs`](verify-escalado-peso.mjs): 3 pesos, incluidos los dos
límites del `Math.Clamp(peso/8000, 0.6, 1.6)`:

| Peso | Factor esperado | Valor esperado | Valor real (SQL) |
|---|---|---|---|
| 4.000 kg | 0.6 (clamp inferior) | $1.710.000 | **$1.710.000** ✅ |
| 8.000 kg | 1.0 (base) | $2.850.000 | **$2.850.000** ✅ |
| 20.000 kg | 1.6 (clamp superior) | $4.560.000 | **$4.560.000** ✅ |

### Carga y estrés — 5 rutas concurrentes, sin cruces

[`verify-rutas-carga.mjs`](verify-rutas-carga.mjs): 5 solicitudes concurrentes, cada
una con una ruta distinta del dataset mock:

```
5 solicitudes en 142 ms.
```

Confirmado por SQL — cada una con su valor exacto, ninguna cruzada con otra:

| Ruta | Valor esperado | Valor real |
|---|---|---|
| Bogotá→Cali | $3.100.000 | **$3.100.000** ✅ |
| Cali→Bogotá | $3.100.000 | **$3.100.000** ✅ |
| Bogotá→Barranquilla | $4.600.000 | **$4.600.000** ✅ |
| Medellín→Cartagena | $3.900.000 | **$3.900.000** ✅ |
| Bogotá→Bucaramanga | $2.400.000 | **$2.400.000** ✅ |

`/health` sano y 0 líneas `fail:`/`crit:` en el log después de toda la sesión de
pruebas.

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ Build `--no-cache`, 0 errores |
| Regresión | ✅ 127+121+105 = 353 pruebas, sin ningún filtro, cero fallos |
| Integración | ✅ Deshabilitado sin cambios, Mock con valor exacto (incluido el hallazgo de la `Fuente` mal etiquetada) |
| Carga y estrés | ✅ Escalado por peso en los 2 límites del clamp + 5 rutas concurrentes, sin cruces |

## Conclusión

El mecanismo de conmutación funciona exactamente como documenta el PR: apagado, cero
cambios; encendido, resuelve con el valor correcto, incluido el escalado por peso, y
sostiene bajo concurrencia. Se encontró y documentó (sin bloquear) un defecto real en
el etiquetado de la fuente, con implicación directa para el futuro adaptador real —
el equipo decide el diseño correcto. Sin más objeciones para mergear.
