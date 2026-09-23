# Decisión — el score de prioridad usa los 4 pesos documentados, no solo urgencia

**Estado:** aceptada · **Fecha:** 2026-09-18 · **Decide:** Ismael Vasco (decisión técnica, no de negocio)

## La contradicción encontrada

`ScorePrioridadCalculator.cs` calcula los 4 componentes (urgencia, valor,
probabilidad, tipo) pero el total descarta 3 de ellos:

```csharp
var urgencia = ComponenteUrgencia(...);
var valor = ComponenteValor(...);
var probabilidad = ComponenteProbabilidad(...);
var tipo = ComponenteTipo(...);

// El total es EXCLUSIVAMENTE la urgencia por deadline. Valor/probabilidad/tipo
// quedan en el desglose como referencia, sin ponderarse en el total.
var total = urgencia;
```

Y el propio repo se contradice a sí mismo sobre cuál es la versión correcta:

- **`README.md:243-245`** (describiendo el flujo de negocio): *"Se calcula su
  `ScorePrioridad` (...función pura: urgencia por deadline, valor estimado,
  probabilidad de cierre y tipo, **con pesos configurables en
  `ParametrosScore`**)."* → versión de 4 pesos.
- **`README.md:370-373`** (describiendo qué cubre `Domain.Tests`): *"confirma
  que el score total es **exclusivamente el componente de urgencia por
  deadline, no una suma ponderada**"* → versión de solo-urgencia.
- **`ScorePrioridadCalculatorTests.cs`**: dos tests (`Total_es_exclusivamente_
  la_urgencia_por_deadline`, `El_total_no_depende_de_valor_probabilidad_ni_
  tipo`) afirman activamente la versión de solo-urgencia.

## Evidencia a favor de los 4 pesos (la que se adopta)

1. **`ParametrosScore.cs`** (`src/Nexo.Domain/Priorizacion/Entidades/`)
   ya tiene los 4 campos de peso, editables por el Admin, con defaults que
   suman 1.0:
   ```csharp
   [Range(0, 1)] public double PesoUrgencia { get; set; } = 0.40;
   [Range(0, 1)] public double PesoValor { get; set; } = 0.30;
   [Range(0, 1)] public double PesoProbabilidad { get; set; } = 0.20;
   [Range(0, 1)] public double PesoTipo { get; set; } = 0.10;
   ```
   Estos 4 campos existen, se validan, se editan vía `PATCH /api/priorizacion/
   parametros` — pero HOY no los usa ningún cálculo. Son configuración muerta.
2. El propio `ScorePrioridadCalculator` YA calcula los 4 componentes
   individuales (`ComponenteUrgencia`, `ComponenteValor`,
   `ComponenteProbabilidad`, `ComponenteTipo`) — nadie los descartaría del
   cálculo si la intención original fuera "solo urgencia"; se calculan
   exactamente para ponderarse.
3. El propio `README.md:243-245` (la sección que describe el flujo real de
   negocio, no la sección que describe qué prueba el test) documenta
   explícitamente los 4 factores con pesos configurables.
4. Medido contra las 387 solicitudes de prueba (antes del fix):
   337 de 387 solicitudes empatadas en score exactamente 50, y otras 45 en
   exactamente 100 — la firma inequívoca de que el "score" es en realidad solo
   un bucket discreto de urgencia (deadline lejano=0, medio=50, vencido=100),
   no un score real diferenciado. Oportunidades de $9M y $3.9M con **el mismo
   puntaje** porque ambas caían en el mismo bucket de urgencia — exactamente
   el problema que se busca resolver.

## Decisión

**La versión correcta es la de 4 pesos** (urgencia + valor + probabilidad +
tipo, ponderados por `ParametrosScore`). La prueba unitaria actual
(`Total_es_exclusivamente_la_urgencia_por_deadline` y
`El_total_no_depende_de_valor_probabilidad_ni_tipo`) documenta un defecto, no
una regla de negocio real — se reemplaza por pruebas que
verifiquen la suma ponderada real.

Fórmula a implementar:

```
total = PesoUrgencia × urgencia + PesoValor × valor + PesoProbabilidad × probabilidad + PesoTipo × tipo
```

Con los defaults actuales de `ParametrosScore` (0.40 / 0.30 / 0.20 / 0.10, que
ya suman 1.0), el total queda en la misma escala 0-100 que tenía antes.

## Verificación — contra las 409 solicitudes reales de `dbInformesAutomatizados_Pruebas`

Corrido `tests/QA/s4-hu-06.3-score-4-pesos-387-solicitudes/verify-score-4-pesos.sql`
(2026-09-18, solo lectura) contra el dataset real (409 solicitudes al momento de correr,
más que las 387 de la medición original porque el dataset crece con el tiempo).

**La prueba puntual queda confirmada:** las oportunidades de $9.000.000 y
$3.900.000, que antes compartían score 100.00, ahora quedan en **86.08** y **80.72**
respectivamente — distintas, como pide el criterio de aceptación.

**El empate exacto en 50/100 desaparece** (0 solicitudes en esos valores con la fórmula
nueva, contra 352 en 50.00 y 47 en 100.00 con la vieja).

**Pero aparece un empate igual de grande en otros dos valores, por una razón distinta:**

| | Antes (solo urgencia) | Después (4 pesos) |
|---|---|---|
| Score más repetido | 50.00 → 352/409 (86%) | 44.0 → 293/409 (72%) |
| Segundo más repetido | 100.00 → 47/409 (11%) | 49.0 → 61/409 (15%) |

44.0 = 0.40·**50** + 0.30·**30** + 0.20·**50** + 0.10·50 (Spot), y 49.0 es la misma
cuenta con Tipo=Licitación (peso 100 en vez de 50). Esos números (50/30/50) son
exactamente `ComponenteUrgencia` sin deadline, `ValorPorDefectoSinValor` y
`ProbabilidadPorDefecto` — es decir, esas 354 solicitudes (87% del dataset) tienen
**Deadline, ValorEstimado y ProbabilidadCierre en NULL**. Se diferencian solo por Tipo
porque es el único de los 4 componentes que sí tienen capturado.

**Conclusión:** el fix funciona como se documentó — los 4 pesos ya ponderan el
total, y por eso el ejemplo puntual ($9M vs $3.9M) y el empate exacto en
50/100 quedan resueltos. Pero la mayoría de las solicitudes en este dataset nunca
tuvieron deadline/valor/probabilidad capturados, así que ninguna fórmula (ni esta ni
ninguna otra) puede diferenciarlas mientras esos 3 campos falten en el origen. Ese es un
problema de captura de datos aguas arriba (en `CrearSolicitudCommand` o en el flujo que
alimenta esos campos), no un defecto de `ScorePrioridadCalculator` — recomendado decidir aparte si esos campos deben ser obligatorios o tener otro
fallback.

## Por qué no la otra versión

La versión "solo urgencia" no tiene ningún respaldo textual propio — ni un
comentario que explique POR QUÉ se decidió ignorar valor/probabilidad/tipo, ni
un ticket/commit citado. Es indistinguible de una regresión: alguien calculó
los 4 componentes (con toda la lógica de normalización que eso implica) y
luego, en algún punto, la línea `var total = ...` quedó apuntando solo a
`urgencia` — probablemente durante un refactor que nunca se terminó de cablear
contra `ParametrosScore`. Mantenerla habría dejado 4 campos de configuración
completos (`PesoUrgencia`/`PesoValor`/`PesoProbabilidad`/`PesoTipo`,
editables por el Admin vía la API) sin ningún efecto real — una superficie de
configuración que miente sobre lo que hace.
