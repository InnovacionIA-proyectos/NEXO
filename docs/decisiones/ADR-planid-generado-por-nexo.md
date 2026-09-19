# ADR — NEXO genera el planId al crear un plan de cargue

**Estado:** aceptada e implementada · **Fecha:** 2026-09-09 · **Issue:** #102 (S3-HU-01.2) · **Implementado en:** #104/#105 (S3-HU-02), referenciado desde S3-HU-02.2

## Contexto

Antes de esta HU, el único lugar que creaba un `PlanCargue` con un id nuevo era
Cubicador (`infrastructure/persistence/repositorios/plan_cargue_repo.py::crear`,
`id=str(uuid.uuid4())` fijo). NEXO no participaba de esa generación: no tenía
forma de conocer el id de un plan antes de que Cubicador terminara de
crearlo, lo que rompe el patrón `202 + identificador` que ya usa el resto del
contrato de trabajos pesados (`docs/contrato-integracion/README.md` §4) — ese
patrón exige devolver el id **en el mismo request** que acepta el trabajo, no
después.

Cubo 2 (S3-HU-02) necesitaba resolver esto ANTES de escribir
`POST /api/planes-cargue`: sin un id conocido de antemano, NEXO no puede
responder `202` con un `consultarEn` que apunte a algo real, ni dejar una fila
propia en `borrador` para ese plan.

## Decisión

**NEXO genera el `planId`** (`Guid.NewGuid()`) en el momento de
`POST /api/planes-cargue` — no Cubicador. Ese id viaja como `PlanId` en el
sobre del comando `crear_plan` (`CubicacionSolicitada`, routing key
`cubicacion.solicitada`):

```json
{ "tipo": "crear_plan", "planId": "<guid generado por NEXO>",
  "correlationId": "<igual a planId>",
  "solicitud": { "nombre": "...", "vehiculoId": "..." } }
```

**Confirmado con el lado de Cubicador (implementado, no solo acordado)**: el
worker crea la fila REAL usando ESE id, nunca uno propio.
`application/plan_cargue/planes.py::crear_plan` y
`infrastructure/persistence/repositorios/plan_cargue_repo.py::crear` ahora
aceptan un `id: str | None = None` — si viene (comando `crear_plan` desde
NEXO), se usa tal cual; si no (POST síncrono directo, `crear_plan_demo`,
`crear_desde_linea`), se sigue generando ahí mismo como antes. Cubierto por
`tests/worker/test_ejecutar_solicitud.py::test_crear_plan_usa_el_planid_que_manda_nexo_no_uno_propio`.

Del lado de NEXO, `CrearPlanCommandHandler` deja además una fila espejo en
`borrador` en la base propia de NEXO en el MISMO request (antes de publicar,
mismo criterio de Outbox que ya usa `SolicitarOptimizacionCommand`) — así
`GET /api/planes-cargue/{id}` ya responde algo coherente sin esperar a que
Cubicador confirme.

## Por qué se acepta

- Es el único mecanismo que permite responder `202` con un id útil en el
  mismo request, consistente con el resto del contrato de trabajos pesados.
- Evita una doble ronda (crear en Cubicador, esperar el id, recién ahí crear
  la fila espejo en NEXO) que habría significado inventar un evento
  "plan-creado-con-id-X" solo para este propósito.
- No rompe nada existente: `crear_plan_demo` y `crear_desde_linea` siguen
  dejando que Cubicador genere su propio id (no tienen el mismo requisito de
  responder un id inmediato consultable — ver limitaciones más abajo).

### Efecto colateral encontrado y resuelto: la FK de `VehiculoId` en NEXO

`PlanCargue.VehiculoId` tiene una foreign key real contra la tabla
`VehiculosCubicacion` DE LA BASE DE NEXO (`CabinaFletesDbContext.cs`). Pero el
`vehiculoId` que llega en `POST /api/planes-cargue` es del catálogo de
**Cubicador** (fuente de verdad de vehículos desde el split a microservicio) —
nunca existió en la tabla de NEXO, que hoy no la usa ningún otro código
(`git grep` confirma cero lecturas de `VehiculosCubicacion` en NEXO fuera del
registro del DbContext).

Se descartó migrar el esquema para quitar la FK (cambio de base más invasivo
del que esta ADR necesita) a favor de un stub mínimo: `CrearPlanCommand`
asegura una fila con solo `Id` + `Nombre` (el mismo id, sin dimensiones) antes
de insertar el plan — ver `IPlanCargueRepository.PrepararCreacionAsync`. Nadie
más lee esa fila; existe únicamente para no romper la FK. Documentado también
en `docs/mensajeria/CUBICACION.md` §6.5.

## Cuándo hay que volver a mirar esto

- Si en algún momento NEXO necesita leer datos reales de un vehículo (no solo
  el id), el stub deja de alcanzar y hay que decidir entre replicar el
  catálogo completo o proxyar la lectura contra Cubicador.
- Si `crear_plan_demo` o `crear_desde_linea` alguna vez necesitan el mismo
  patrón de "id conocido de antemano" (hoy no lo necesitan: ver
  `docs/mensajeria/CUBICACION.md` §6.5 sobre `crear_plan_demo` sin GET de
  seguimiento).

## Alternativas descartadas

**Cubicador sigue generando el id; NEXO lo recibe después por evento.** Obliga
a inventar un evento nuevo solo para "acá está el id que te faltaba" y rompe
el patrón `202 + id inmediato` del resto del contrato — el frontend tendría
que esperar un aviso antes incluso de poder construir la URL de seguimiento.

**Quitar la FK de `PlanCargue.VehiculoId` en NEXO (migración de esquema).**
Es la solución "más limpia" a largo plazo, pero es un cambio de base de datos
que esta ADR no necesita para resolver el problema — el stub mínimo cuesta una
fila sin uso, no una migración. Queda anotado como primer punto de "cuándo
revisar esto" si el costo del stub deja de ser aceptable.
