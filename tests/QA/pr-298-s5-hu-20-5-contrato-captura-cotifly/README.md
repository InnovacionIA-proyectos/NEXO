# PR #298 (S5-HU-20.5) — Contrato de captura con Cotifly (cierra #297)

## Qué cambia

- `numeroLinea` en cada línea del GET de costeo: orden estable (número, luego fecha de
  creación, luego Id); líneas viejas sin número al final.
- Línea de Cupo (o sin clase de viaje) **sin vehículo** ya no se autocostea: queda
  `PorCostear` con motivo `"Línea sin vehículo: requiere consulta completa"`, candidatos
  como referencia. Contenedor/Carga suelta/Extradimensionado no cambian.
- Normalización de nombres de catálogo (sin tildes, `_`/`-` equivalentes a espacio).
- Sin cambios de esquema.

## Hallazgos de esta misma sesión, ya corregidos en esta PR

Esta PR referencia explícitamente, en sus propios comentarios de código, dos problemas
que aparecieron durante la verificación de PRs anteriores de esta sesión (2026-10-13/14):

1. **`clsPropuestaPlazaPropiaTests`** (el test frágil que reporté en la verificación de
   la PR #292, hardcodeaba un valor SAP con fecha de vigencia fija): ahora consulta la
   tarifa SAP vigente en tiempo real contra la misma base que usa el motor, en vez de
   fijar `3.000.000` a mano. Verificado: el test ya no depende de que una fila externa
   siga vigente por calendario.
2. **Evento de `actualizar_carroceria` en Cubicador** (causaba que el consumer de NEXO
   cayera a la cola de error — exactamente el flujo que probé manualmente en la
   verificación de la PR #291, passthrough de carrocerías): el router ahora llama un
   caso de uso que publica la fila completa (`id`, `version` incluidos), igual que
   crear/republicar.

Y en el propio workflow de CI: las 6 pruebas E2E contra el Cubicador real ahora se
saltan explícitamente (`clsHechoConCubicadorRealAttribute`) cuando el runner no tiene
el stack levantado, en vez de fallar con `ServiceUnavailable` como pasaba antes (lo vi
yo mismo en cada PR de esta sesión).

## Qué se verificó

**Rama ya al día con `QA`** (sin necesidad de merge).

**Código revisado línea por línea:** `clsReglaDeCosteo.LineaSinVehiculoQueExigeConsultaCompleta`
(Cupo o sin clase de viaje, nunca Contenedor/Carga suelta/Extradimensionado) y
`DecidirFlujo` (salta Solo-SICETAC y PlazaVigenteMayor, va directo a consulta completa
con los candidatos como referencia, nunca bajo el piso).

**Suite completa contra la base real de pruebas, con el Cubicador real armado
localmente desde el arranque (para no depender de skips):**
- `Nexo.Domain.Tests`: **173/173**.
- `Nexo.Application.Tests`: **390/390**.
- `Nexo.Api.Tests`: **368/370** (solo los 2 skips preexistentes de `clsMailSenderRealTests`)
  — **0 errores, ningún test se saltó por falta de Cubicador** (las 6 pruebas E2E
  corrieron de verdad, no se omitieron). Coincide/mejora lo declarado por la PR
  (363 OK/8 omitidas en su runner sin Cubicador).

**Verificación en vivo (API real contra BD real):**
- Capturé un pliego con 2 líneas de Cupo sin vehículo, mismo `grupoRutaId` → ambas
  quedaron `PorCostear`, `fletesCalculados: 0`, con el motivo exacto
  `"Línea sin vehículo: requiere consulta completa"`.
- `numeroLinea` salió `1, 2` en el orden enviado.

## Resultado

Sin hallazgos. Esta PR además resuelve, de forma directa y verificada, dos problemas
reales que surgieron durante esta misma sesión de QA.
