# Interpretacion de descripciones libres — informe

- **fecha**: 2026-10-29
- **modelo**: claude-sonnet-5-5
- **casos**: 30
- **llamadas**: 30
- **tokens**: 64607 entrada / 6765 salida
- **costo estimado**: US$0.20
- **dataset**: interpretacion_sintetica.json

## Resultado

- Casos exactos: **23/23 (100 %)**
- Rechazos correctos: **7/7 (100 %)**
- Casos con error: 0

## Acierto por campo

| Campo | Acierto |
|---|---|
| codigo | 26/26 (100 %) |
| cantidad | 26/26 (100 %) |
| largoCm | 26/26 (100 %) |
| anchoCm | 26/26 (100 %) |
| altoCm | 26/26 (100 %) |
| pesoKg | 26/26 (100 %) |

## Caso por caso

| Caso | Esperado | Resultado |
|---|---|---|
| I-01 | 1 posicion(es) | exacto |
| I-02 | 1 posicion(es) | exacto |
| I-03 | 1 posicion(es) | exacto |
| I-04 | 1 posicion(es) | exacto |
| I-05 | 1 posicion(es) | exacto |
| I-06 | 1 posicion(es) | exacto |
| I-07 | 1 posicion(es) | exacto |
| I-08 | 1 posicion(es) | exacto |
| I-09 | 1 posicion(es) | exacto |
| I-10 | 2 posicion(es) | exacto |
| I-11 | 1 posicion(es) | exacto |
| I-12 | 1 posicion(es) | exacto |
| I-13 | 2 posicion(es) | exacto |
| I-14 | 1 posicion(es) | exacto |
| I-15 | 1 posicion(es) | exacto |
| I-16 | 1 posicion(es) | exacto |
| I-17 | 1 posicion(es) | exacto |
| I-18 | 2 posicion(es) | exacto |
| I-19 | 1 posicion(es) | exacto |
| I-20 | 1 posicion(es) | exacto |
| I-21 | 1 posicion(es) | exacto |
| I-22 | 1 posicion(es) | exacto |
| R-01 | rechazo PESO_FALTANTE | ok |
| R-02 | rechazo DIMENSION_FALTANTE | ok |
| R-03 | rechazo CANTIDAD_FALTANTE | ok |
| R-04 | rechazo CODIGO_DESCONOCIDO | ok |
| R-05 | 1 posicion(es) | exacto |
| R-06 | rechazo CANTIDAD_AMBIGUA | ok |
| R-07 | rechazo SIN_POSICIONES | ok |
| R-08 | rechazo CANTIDAD_INVALIDA | ok |
