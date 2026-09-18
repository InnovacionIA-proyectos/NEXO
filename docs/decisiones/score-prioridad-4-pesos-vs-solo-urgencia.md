# Decisión — el score de prioridad usa los 4 pesos documentados, no solo urgencia

**Estado:** aceptada · **Fecha:** 2026-09-18 · **Decide:** Ismael Vasco (decisión técnica, no de negocio — ver issue) · **Issue:** #172 (S4-HU-06.1)

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

1. **`ParametrosScore.cs`** (`src/CabinaFletes.Domain/Priorizacion/Entidades/`)
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
4. Medido contra las 387 solicitudes de prueba (issue #174, antes del fix):
   337 de 387 solicitudes empatadas en score exactamente 50, y otras 45 en
   exactamente 100 — la firma inequívoca de que el "score" es en realidad solo
   un bucket discreto de urgencia (deadline lejano=0, medio=50, vencido=100),
   no un score real diferenciado. Oportunidades de $9M y $3.9M con **el mismo
   puntaje** porque ambas caían en el mismo bucket de urgencia — exactamente
   el problema que la HU pide resolver.

## Decisión

**La versión correcta es la de 4 pesos** (urgencia + valor + probabilidad +
tipo, ponderados por `ParametrosScore`). La prueba unitaria actual
(`Total_es_exclusivamente_la_urgencia_por_deadline` y
`El_total_no_depende_de_valor_probabilidad_ni_tipo`) documenta un defecto, no
una regla de negocio real — se reemplaza (issue #173) por pruebas que
verifiquen la suma ponderada real.

Fórmula a implementar:

```
total = PesoUrgencia × urgencia + PesoValor × valor + PesoProbabilidad × probabilidad + PesoTipo × tipo
```

Con los defaults actuales de `ParametrosScore` (0.40 / 0.30 / 0.20 / 0.10, que
ya suman 1.0), el total queda en la misma escala 0-100 que tenía antes.

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
