# Decisión — advertencia explícita cuando la plaza SAP cae al fallback sin match exacto

**Estado:** aceptada · **Fecha:** 2026-09-18 · **Issue:** #169 (S4-HU-04)

## Qué se pidió originalmente

El issue #169 pedía "conectar `ISapTarifaFleteEnVivoPort` a la plaza SAP real ya
disponible", asumiendo que el motor de decisión (RN-01) nunca producía un
candidato de plaza real.

## Lo que se encontró (descarta el diagnóstico original)

`RN-01` (`PropuestaTarifaAdapter`) **ya compara contra plaza real hoy**, vía
`ITarifarioSapPort`/`TarifarioSapAdapter`, registrado sin ningún flag
(`DependencyInjection.cs:197`) — consulta directo `dbo.tblSAPTarifas` /
`dbo.tblSAPTarifasLog` (las mismas 2 tablas que confirmó el equipo, alimentadas
constantemente desde SAP porque hoy no hay conexión directa posible a SAP).
`ISapTarifaFleteEnVivoPort` es un contrato **aparte**, para una integración SAP
en tiempo real que el equipo externo de conexiones no ha construido — conectarlo
a la búsqueda paginada de `/biblioteca-sap-plaza` habría **sobrescrito** el
candidato real ya correcto con uno peor (búsqueda de texto en vez de match
exacto). Se descartó esa vía.

## El problema real (confirmado con datos reales, control A/B)

Se levantó un SQL Server efímero local (no se tocó ninguna base corporativa
real ni el `.env` de producción) con el esquema exacto de `tblSAPTarifas` +
`tblSAPTiposVehiculoTarifario`, se sembraron datos sintéticos, y se ejecutó el
código real de `TarifarioSapAdapter.ConsultarAsync` en 4 escenarios:

| # | Escenario | Resultado (antes del fix) |
|---|---|---|
| 1 | Match exacto (vehículo + peso dentro del límite) | ✅ Encuentra la tarifa correcta vía Regla C |
| 2 | Mismo caso en minúsculas | ✅ Igual — normalización correcta |
| 3 | Peso de la línea EXCEDE el límite de todas las tarifas vigentes para ese vehículo | ⚠️ Devolvía la tarifa igual, vía "fallback · mayor valor vigente", sin avisar que el peso no calzaba |
| 4 | Vehículo que no resuelve en `tblSAPTiposVehiculoTarifario` | ⚠️ Devolvía la tarifa igual, vía el mismo fallback, sin avisar que el vehículo no se pudo verificar |

Causa: `TarifarioSapAdapter.Seleccionar` (Reglas A/B/C) intenta un match exacto
por vehículo/peso/tipo de viaje; cuando NINGUNA regla aplica, cae a un
**fallback conservador** ("la tarifa vigente de mayor valor de la ruta") sin
distinguir ese caso de un match real — la línea se costeaba con una tarifa que
podía no corresponderle.

## Decisión: no descartar la línea, avisar explícitamente

Se decidió (con el equipo, tras confirmar el escenario) que la línea **debe
seguir cotizándose** con la tarifa del fallback — no dejarla sin propuesta —
pero con un mensaje explícito que el frontend pueda mostrarle al cliente/
Administrador de Fletes antes de confirmar.

Implementado:
- `CandidatoTarifaDto` gana un campo `Advertencia: string?` (null en un match
  exacto real). `TarifarioSapAdapter.Seleccionar` lo llena con un mensaje
  específico: menciona el peso exacto que no calzó, o que el vehículo no
  resolvió en el catálogo — nunca un mensaje genérico.
- `PropuestaTarifaAdapter` anexa esa advertencia a la `Justificacion` de la
  línea (el campo que YA se le muestra al usuario) cuando la propuesta final
  es plaza y viene de ese fallback.
- Contrato documentado en `CONTRATO.md` (`CandidatoTarifaDto.advertencia`).

Verificado: los mismos 4 escenarios, repetidos después del fix, contra el
mismo SQL Server efímero — advertencia ausente en 1 y 2, presente y específica
en 3 y 4 (peso exacto / catálogo de vehículo, según corresponda).

## Limitación conocida (no resuelta aquí)

No hay un proyecto de tests para la capa `Nexo.Infrastructure` en este
repo — `TarifarioSapAdapter`/`PropuestaTarifaAdapter` no tienen cobertura
automatizada de regresión (ni la tenían antes de este fix). La verificación de
esta issue se hizo de forma real pero manual (SQL Server efímero + datos
sintéticos, sin tocar ninguna base real), no queda como test que corra en CI.
Vale la pena un issue de seguimiento si se decide invertir en
`Nexo.Infrastructure.Tests`.
