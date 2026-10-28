# Pendientes de Sara — a partir de `resultado_analisis.md`

_Fecha: 2026-10-26_

No son HU de código — son de datos/coordinación, quedan aparte del plan (`2026-10-23_PLAN-IA-CLAUDE-NEXO.md`).

## 1. Ciudades de exportación faltantes en el catálogo de municipios

Verificado contra `tbl_municipios` (export del 2026-10-21):

- **Tulcán** sí existe en la tabla (Ecuador, Carchi, DANE `040150`), pero **sin zona de tarificación asignada** (`NombreTarifasPlaza`/`ZonaTarificacion` vacíos) — por eso probablemente no aparece en el selector de ciudades de la app, aunque el registro exista.
- **Guarero** no aparece en absoluto en el catálogo — falta crearlo.
- **San Antonio** (el tercer destino de exportación que menciona el reporte) no se revisó todavía.

**Qué falta:** revisar esto con Ismael/el equipo de datos — decidir si se les asigna zona de tarificación a Tulcán y se crea Guarero (y verificar San Antonio).

## 2. Dato de plaza que parece mal cargado: Funza → Cota, Mula

El Excel del cliente da 1.113.281; NEXO propuso 6.000.000 (casi 5.5 veces más). No parece un error de lógica de NEXO — parece una tarifa de plaza cargada mal en la base de datos.

**Qué falta:** revisar esa fila en la tabla de tarifas de plaza (origen Funza, destino Cota, vehículo Mula) y corregirla si en efecto está mal, con quien administra esas tarifas.
