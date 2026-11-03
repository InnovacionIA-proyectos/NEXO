# Verificación QA — S2-HU-08.1/08.2 (issues #29, #30, PR #79)

Rama: `30-s2-hu-082-compuerta-de-liberación-por-paridad-sin-fallback-al-poc`
(ivasco-dev). Verificación hecha antes de aprobar/mergear.

## ⚠️ Falsa alarma de conflicto — verificado y descartado

Mi chequeo preliminar con `git merge-tree` marcó 9 líneas de conflicto en
`Program.cs` y en los 2 consumers de Cubicación (`CubicacionRechazadaConsumer.cs`,
`PlanDeCargueGeneradoConsumer.cs`) — esta rama incluye su propio merge del puente de
la #28 (HU-075), ya mergeado a `QA` por separado (PR #73). Al correr el `git merge`
real contra QA, **fusionó limpio, sin ningún conflicto** — la discrepancia fue que mi
chequeo manual con `merge-tree` usó una base de fusión distinta a la que calcula
`git merge` (estrategia `ort`, que sí maneja bien el historial cruzado de dos ramas
que comparten un ancestro por caminos distintos). Verifiqué el resultado igual, por
tratarse de archivos ya probados con infraestructura real en la HU-075: los 2
consumers quedaron con la versión de **esta** PR, que es una extensión aditiva de la
ya verificada (agrega el aviso por SignalR después de guardar el resultado, sin
tocar la lógica de guardado) — confirmado línea por línea contra lo que hay en `QA`.
También confirmé, por grep, que ningún registro de DI quedó duplicado ni perdido
tras la fusión (hubs, consumers, CORS, identidad del portal).

## Qué trae el PR

- `POST /api/planes-cargue/{id}/optimizar`: 202 + id inmediato, nunca bloquea.
- `GET /api/planes-cargue/{id}`: consulta de respaldo.
- Avance real por SignalR (`/hubs/plan-cargue`, evento `"avance"`).
- **Compuerta de paridad** (`CompuertaDeParidad`, función pura): exige
  `ParidadVerificada` + `ParidadEvidencia` + `ParidadFecha` juntos — sin las tres,
  `503 CUBICACION_EN_VERIFICACION` antes de tocar nada. Cerrada por defecto. Según
  `PENDIENTES.md`, soy yo (Kevin) quien la enciende en un despliegue real, tras
  confirmar 26/26 en los golden tests de paridad — no antes.
- Limitación documentada honestamente en `PENDIENTES.md`: solo re-optimiza un plan
  que YA EXISTE en Cubicador; "crear plan desde línea de solicitud" queda pendiente
  (depende de un endpoint nuevo y datos que `LineaSolicitud` no captura hoy).

## Verificación — reproducida de forma independiente, no solo leída

### Regresión completa

| Suite | Resultado |
|---|---|
| `Domain.Tests` | **127/127** |
| `Application.Tests` | **121/121** (coincide exacto con lo que afirma el PR) |
| `Api.Tests` | **97/99** (+2 saltadas por diseño de otra HU — coincide exacto) |

Build `--no-cache`: 0 errores.

### Compuerta de paridad — fail-closed confirmado en vivo

```
Compuerta cerrada (Cubicacion__ParidadVerificada=false, por defecto):
  POST /api/planes-cargue/<cualquier-id>/optimizar -> 503 CUBICACION_EN_VERIFICACION
  (ni siquiera valida si el plan existe -- rechaza ANTES de tocar nada)

Compuerta abierta (las 3 claves configuradas juntas):
  POST /api/planes-cargue/<id-inexistente>/optimizar -> 404 PLAN_CARGUE_NO_ENCONTRADO
```

### Flujo real de punta a punta — con Cubicador real, no simulado

[`verify-flujo-real.mjs`](verify-flujo-real.mjs): plan + ítem creados en Cubicador
por su API REST, espejo insertado en NEXO por SQL directo (mismo patrón ya usado
para verificar la HU-075), suscripción real a `/hubs/plan-cargue` ANTES de pedir la
optimización, y la petición real al endpoint nuevo (no un publicador desechable esta
vez — el endpoint ya existe):

```
Pidiendo la optimización real (POST /api/planes-cargue/{id}/optimizar)...
-> 202 en 234 ms: { estado: 'procesando', consultarEn: '/api/planes-cargue/...' }
Avisos recibidos por SignalR: [{"estado":"optimizado","puntaje":75,"motivo":null}]
Consulta de respaldo: estado=optimizado puntaje=75
TODO OK.
```

El aviso por SignalR y la consulta de respaldo coinciden exactos — la garantía
"la verdad vive en `GET /api/planes-cargue/{id}`, el aviso es la mejora" se sostiene.

### 409 — verificado con concurrencia real, no secuencial

Un primer intento con dos `curl` seguidos no lo reprodujo (el motor optimiza tan
rápido que la segunda llamada ya encontraba el plan `optimizado`, no `procesando` —
dato interesante en sí mismo sobre la velocidad real del motor con planes chicos).
Con dos peticiones genuinamente concurrentes (`Promise.all`) sobre el mismo plan:
**`[202, 409]`** — confirmado.

### Carga y estrés — 5 planes concurrentes, sin cruces

[`verify-carga-concurrente.mjs`](verify-carga-concurrente.mjs): 5 planes distintos
(cada uno con su espejo Cubicador+NEXO), optimizados a la vez:

```
5 peticiones en 219 ms. Estados: [202, 202, 202, 202, 202]
  plan e8f829dd-...: estado=optimizado puntaje=75
  plan f46b7114-...: estado=optimizado puntaje=75
  plan d132f699-...: estado=optimizado puntaje=75
  plan 113905d8-...: estado=optimizado puntaje=75
  plan 7ea2cb6a-...: estado=optimizado puntaje=75
TODO OK.
```

Tras la ráfaga: `/health` sano, la cola `Cubicacion.PlanDeCargueGenerado` en 0
mensajes, 0 líneas `fail:`/`crit:` en el log.

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ Build `--no-cache`, 0 errores |
| Regresión | ✅ 127+121+99 = 347 pruebas, sin ningún filtro, cero fallos |
| Integración | ✅ Compuerta fail-closed/abierta, flujo real con Cubicador+SignalR, 404/409 verificados en vivo |
| Carga y estrés | ✅ 5 planes concurrentes, sin cruces; 409 confirmado bajo concurrencia real |

## Conclusión

La falsa alarma de conflicto se investigó a fondo y se descartó con evidencia (diff
línea por línea + grep de duplicados). El flujo completo —compuerta, endpoint,
puente con Cubicador real, SignalR, consulta de respaldo, y concurrencia— quedó
verificado con datos y ejecuciones propias, no solo leído. Sin objeciones para
mergear.
