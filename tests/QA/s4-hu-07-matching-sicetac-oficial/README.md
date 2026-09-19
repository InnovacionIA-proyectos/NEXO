# S4-HU-07 — Matching SICE-TAC oficial vs "Solo SICE-TAC" (issues #175/#176/#177)

## Diagnóstico (issue #175)

Ver `docs/decisiones/matching-sicetac-oficial-vs-solo-sicetac.md` para el diagnóstico
completo de causa raíz. Resumen: `PropuestaTarifaAdapter.ProponerLineaAsync` tiene DOS
caminos distintos para buscar el piso SICE-TAC de una línea:

- **Camino normal** (cuando hay `ClaseViaje` y conexión SAP): usa
  `TarifarioSapAdapter.SicetacOficialAsync`, una réplica 1:1 de la lógica SAP legada
  que exige match EXACTO de Configuración + Tipo de Carga + Unidad de Transporte + Vía
  Estándar a la vez (sin fallback), y que además solo se ejecuta para
  `ClaseViaje ∈ {Contenedor, Cupo}` — cualquier línea `CargaSuelta` o
  `Extradimensionado` queda excluida de entrada, sin importar la ruta.
- **Camino "Solo SICE-TAC"** (marca manual del Administrador): usa
  `PropuestaTarifaAdapter.CandidatoSicetacAsync`/`SeleccionarSice`, un selector
  tolerante que trata esos mismos criterios como preferencia de orden, con fallback
  progresivo, y no excluye ningún `ClaseViaje`.

Por eso una ruta como Medellín-Guarne (corredor corto, probablemente sin fila
`VIAESTANDAR='SI'` o con `NOMBRETIPOCARGA` distinto al parámetro SAP global) nunca
aparece por el camino normal, pero aparece al instante en cuanto alguien la marca
"Solo SICE-TAC" — porque esa marca usa el selector tolerante, que la ruta siempre pudo
satisfacer.

## Qué correr para confirmar con datos reales

No se pudo confirmar contra las bases reales desde el entorno de desarrollo (mismo
límite de siempre en esta sesión: sin conectividad a las bases corporativas desde ahí).

1. `verify-medellin-guarne.sql` (contra DWSice) — trae todas las filas de
   Medellín-Guarne en `tblRutasSicetac` y marca cuál de los 5 criterios estrictos
   incumple cada una. Confirma cuál filtro específico es el que la descarta.
2. `verify-otros-2-casos-cargasuelta.sql` (contra `dbInformesAutomatizados_Pruebas`)
   — trae hasta 10 líneas reales con `ClaseViaje ∈ {CargaSuelta, Extradimensionado}`:
   estas SIEMPRE fallan el camino normal por el gate `aplica`, sin importar el texto
   de la ruta. Sirven como los "≥2 casos adicionales" que pide el issue #175, con una
   causa distinta (y más amplia) a la de Medellín-Guarne.

## Fix (issue #176)

`src/CabinaFletes.Infrastructure/Biblioteca/Adaptadores/PropuestaTarifaAdapter.cs`,
método `ProponerLineaAsync`: cuando el camino oficial no encuentra piso SICE-TAC
(`oficial.SicetacOficial is null`), ahora se reintenta con el MISMO selector tolerante
que ya usa "Solo SICE-TAC" (`CandidatoSicetacAsync`), antes de rendirse. El camino
oficial se prueba primero y no se modifica — sigue gobernando sin cambios cuando sí
encuentra algo, para no arriesgar los casos ya verificados con datos reales en
`tests/QA/hu-13.4-verificacion-reglas-negocio-datos-reales/README.md` (8/12 líneas de
un pliego real, confirmadas 1:1 contra un PDF de cotización real).

No hay proyecto de tests para `CabinaFletes.Infrastructure` (adaptadores EF contra
SQL Server real, sin mocks) — no se pudo agregar una prueba automatizada para este
fix. Verificación manual pendiente: correr el pliego de prueba de la HU-04.7/S4-HU-07.3
contra la app real con el fix desplegado y confirmar que Medellín-Guarne (y los casos
CargaSuelta de arriba) ya reciben flete propuesto sin marcar "Solo SICE-TAC".

## Remedición (issue #177)

Correr el mismo pliego de prueba usado en el documento de comparación contra
`GET /api/metricas/kpis-piloto` con el fix desplegado, y comparar contra el dato
"antes" (58/451 líneas, 12,9%). Ver la plantilla de reporte en
`docs/decisiones/matching-sicetac-oficial-vs-solo-sicetac.md`, sección "Remedición".
