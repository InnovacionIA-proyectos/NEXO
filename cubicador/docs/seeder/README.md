# Datos de siembra del catálogo

## ⚠️ Los CSV son referencia, no fuente ejecutable

**No los corras contra `dbo_cubicador`.** El catálogo ya está creado y sembrado
por el equipo de Datos en la base real (`10.10.40.85`,
`dbTarifario_Nexo_Pruebas`, esquema `dbo_cubicador`), con los datos cruzados
fila por fila contra estos mismos CSV.

Los Id de esas filas son **`uuid5` determinísticos**, derivados de la clave de
negocio con un namespace propio del equipo. Un seeder que genere Id por su
cuenta produciría **dos juegos de filas equivalentes con Id distinto** — que es
exactamente el escenario que hay que evitar, y el que la propia nota de
S5-HU-17.1 advertía.

Mientras no se resuelva el punto 4 de los bloqueos de S5-HU-17.2 (o nos pasan
el namespace, o se decide que el Cubicador solo lea), estos archivos sirven
para:

- Entender de dónde salió cada dato del catálogo
- Comparar contra lo sembrado cuando algo no cuadre
- Reconstruir el catálogo desde cero en un ambiente nuevo, si alguna vez hace
  falta

## Qué hay acá

| Archivo | Qué trae |
|---|---|
| `vehiculos.csv` | Las 5 configuraciones del Excel de dimensiones, en cm |
| `vehiculoCarroceria.csv` | Alto útil por combinación. Único override: `3S3 + FURGON = 30.000 kg` |
| `tiposCarroceria.csv` | Los nombres de la matriz normalizados a códigos |
| `categoriaMercancia.csv` | Las categorías de mercancía |
| `tiposMercancia.csv` | Los tipos, con sus banderas y su carrocería |
| `reglasIncompatibilidad.csv` | Las reglas de segregación |
| `parametrosMotor.csv` | Parámetros del motor, con `k_max = 10` |

**Diferencias conocidas contra lo sembrado**, verificadas el 2026-10-09:

- Lo sembrado tiene **17 carrocerías**; estos CSV traen menos. Entre las que
  agregaron están `FURGON_REFRIGERADO` como carrocería propia y `CONTENEDOR`
  partido en seco y refrigerado — las dos contradicen reglas del motor escritas
  en la hoja de ruta, y hay que reconciliarlas antes de S5-HU-17.3.
- Lo sembrado tiene **31 tipos de mercancía, todos con `Cubicable = 1`**.
  Faltan los 10 excluidos (graneles, combustibles, cemento, concreto, asfalto,
  residuos, vehículos como unidad, ingeniería especial) que permiten rechazar
  "cemento a granel" con motivo en vez de inventar una recomendación.

## `corregir_vehiculos_existentes.sql`

Este **sí se ejecuta**, y es otra cosa: corrige las 5 filas de
`vehiculos_cubicacion` (el catálogo del motor 3D, no el del cubicador rápido)
que tenían medidas inventadas. Es idempotente. Va como paso de datos de la
migración, después de crear el esquema y antes del primer arranque.

Hace falta porque `listar_con_siembra` siembra **por nombre** y nunca actualiza
una fila existente: la corrección del código solo alcanza a bases nuevas.
