# Reglas de captura por Línea de negocio y Clase de viaje

Construida únicamente con el texto literal de
`AJUSTES VISUAL . COMERCIAL (CONSULTA INTERNA) NEXO.docx`
(`Proyectos Botero Soto/Nexo/`). Donde el documento no dice nada explícito
sobre un campo, se marca **N/E** (no especificado) en vez de inferir o
adivinar.

| Línea de negocio | Clase de viaje | Origen | Destino | Peso | Valor mercancía | Config. vehículo | Carrocería | Clase contenedor | Horas cargue | Horas descargue | Levante | Retorno | Consolidado | Exclusivo | Importar lista empaque |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **Devolución Contenedor** | Contenedor (única opción) | ✓ | ✓ | ✓¹ | ✓ | ✓ | ✗ | ✓¹ | N/E | N/E | ✗ | ✗ | ✓ | ✓ | ✗ |
| **Interior a Puerto** | Carga Suelta | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ | N/E | N/E | ✓ | ✗ | ✗ | ✗ | ✓ |
| **Interior a Puerto** | Contenedor | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓² | N/E | N/E | ✓ | ✗ | ✗ | ✗ | ✗ |
| **Interior a Puerto** | Cupo | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ | N/E | N/E | ✓ | ✗ | ✗ | ✗ | ✗ |
| **Interior a Puerto** | Extradimensionado | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✗ | N/E | N/E | ✓ | ✗ | ✗ | ✗ | ✓ |
| **Nacional** | Carga Suelta | ✓ | ✓ | ✓ | ✓ | ✓ | ✓³ | ✗ | N/E | N/E | ✗ | N/E⁴ | ✓ | ✗ | ✓ |
| **Nacional** | Extradimensionado | ✓ | ✓ | ✓ | ✓ | ✓ | ✓³ | ✓⁵ | N/E | N/E | ✗ | N/E⁴ | ✓ | ✗ | ✓ |
| **Nacional** | Cupo | ✓ | ✓ | ✓ | ✓ | ✓ | ✓³ | ✗ | N/E | N/E | ✗ | N/E⁴ | ✓ | ✗ | ✗ |

## Notas

1. **Devolución Contenedor:** el peso trae default por tipo de contenedor
   (40' = 4.000 kg / 20' = 2.300 kg) — por eso el campo de tipo/clase de
   contenedor sí aplica, aunque el Word no lo llama con ese nombre exacto.
2. **Interior a Puerto + Contenedor:** el Word no define explícitamente este
   campo para esta combinación; se marca ✓ porque Clase de viaje = Contenedor
   implica lógicamente elegir el tipo de contenedor, pero no es un requisito
   literal del documento.
3. **Carrocería en Nacional:** el campo sí aparece, pero sin la opción
   **Portacontenedores**.
4. **Retorno en Nacional:** el Word no dice nada sobre excluirlo, pero
   conceptualmente es un campo de Contenedor — y Nacional ya excluye
   Contenedor de Clase de viaje, así que en la práctica no debería aplicar.
5. **Clase contenedor en Nacional + Extradimensionado:** solo con el catálogo
   especial FT y OP de 20' y 40', no el catálogo normal de contenedores.

**Horas de cargue/descargue:** el Word no menciona estos dos campos en
ningún párrafo — no hay regla de "debe/no debe" que sacar de él para
ninguna línea ni clase de viaje.

**Nota aparte:** "Puerto a interior" existe como línea de negocio en el
código (`LINEAS_NEGOCIO`, `captura-page.component.ts`), pero el Word nunca la
menciona — no se incluye en la tabla porque no hay ninguna regla literal del
documento que aplicarle.
