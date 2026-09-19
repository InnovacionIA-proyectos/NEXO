# Verificación QA — S2-HU-05.2 (issue #20, PR #67)

Rama: `20-s2-hu-052-bandeja-push` (se apila sobre HU-01.1 → HU-05.1). Como esta rama
salió del PR #66 antes de que este mergeara a QA, la comparación contra QA quedaba
arrastrando esos dos PRs — según lo advierte el propio autor en la descripción del PR.
Verificación hecha **después** de que PR #64 y PR #66 ya estaban en `QA`: la comparación
queda limpia, 6 commits propios (`b26dcd1`..`92d4925`), 0 conflictos.

## Qué trae el PR

- **Hub aparte** (`/hubs/bandeja`, evento `"bandeja"`, un solo grupo sin filtrar por
  usuario — la bandeja no distingue por comercial).
- **5 disparadores** del aviso: `solicitud-nueva`, `solicitud-rechazada`,
  `trabajo-terminado`, `parametros-recalibrados`, `refresco-periodico` (latido).
- **El hallazgo de diseño que justifica el PR**: un pliego de 300 líneas dispara 300
  avisos de avance (la barra los necesita) pero **1 solo** aviso de bandeja, publicado
  donde el trabajo cierra — no por línea, no por contador ni temporizador.
- El aviso **no lleva la bandeja ni secuencia**: es una señal idempotente, el cliente
  siempre reconsulta `GET /api/solicitudes` con sus propios filtros.
- Corrige un defecto real que el propio recorrido E2E destapó al extenderse: con long
  polling, dejar de sondear el hub es desconectarse — el recorrido ahora escucha en un
  hilo aparte.

## Verificación — reproducida de forma independiente, no solo leída

### Build y regresión

```
docker compose -f docker-compose.ci.yml build --no-cache
docker compose -f docker-compose.ci.yml run --rm build
docker compose -f docker-compose.ci.yml run --rm pruebas
```

| Suite | Resultado | Afirmado por el PR |
|---|---|---|
| Build | 0 errores, 1 advertencia conocida (`ASPDEPR005`) | igual |
| `Domain.Tests` | **69/69** ✅ | 69 |
| `Application.Tests` | **96/96** ✅ | 96 |
| `Api.Tests` (sin filtro) | **72/72** ✅ | 72 |
| **Total** | **237/237** | **237/237** — coincide exacto |

### Integración, contra la app real levantada desde volúmenes limpios

```
docker compose up -d --build --wait
docker compose --profile e2e run --rm e2e
```

**`RECORRIDO COMPLETO SIN FALLOS`**, incluyendo la sección propia de esta HU:

```
=== Bandeja: el pliego entero produce UN aviso, no uno por línea (HU-05.2)
  OK   el alta de la solicitud avisó a la bandeja
  OK   el pliego de 15 líneas produjo 1 aviso(s) de trabajo-terminado (esperado 1, NO 15)
  OK   el aviso es una señal, no la bandeja: solo motivo y fecha
  OK   la bandeja avisó 1 vez del cierre y el avance 17 veces: el canal de bandeja se
       agrupa, el de avance no
...
=== Bandeja: recalibrar avisa por el canal (HU-05.2)
  OK   llegó el aviso de recalibración (motivos nuevos: ['parametros-recalibrados',
       'refresco-periodico'])
```

`negotiate` de `/hubs/bandeja` sin identidad → `401`, verificado.

## Carga y estrés — las dos pruebas que faltaban, escritas y corridas para este documento

Ni el PR ni las HU anteriores (HU-04.5 dejó esto explícitamente pendiente: *"Cubierta
indirectamente... no se repite aquí"*) tenían una prueba de carga real del canal de
bandeja. Se escribieron dos, a los dos niveles que ya se usan en el resto de esta
carpeta QA:

### 1) `BandejaCargaTests.cs` — a nivel de saga, en memoria (mismo patrón que HU-04.3/04.4)

No forma parte del PR. Para reproducirla: copiar dentro de
`tests/CabinaFletes.Api.Tests/Mensajeria/` y correr
`dotnet test --filter FullyQualifiedName~BandejaCarga`. No necesita infraestructura real
— usa el transporte en memoria del arnés de MassTransit.

**Qué prueba:** 20 pliegos concurrentes, de tamaños desiguales a propósito (1 a 30
líneas cada uno, 232 líneas en total, todos entrelazados), disparados y resueltos a la
vez. Si el agrupamiento dependiera de un contador o temporizador compartido en vez del
cierre real de cada saga individual, mezclar tamaños desiguales en la misma corrida lo
delataría.

**Resultado:**
```
Passed CabinaFletes.Api.Tests.Mensajeria.BandejaCargaTests
  .Pliegos_concurrentes_de_tamanos_distintos_producen_exactamente_un_aviso_cada_uno [3 s]
```
Exactamente 20 avisos de bandeja (`trabajo-terminado`) para los 20 pliegos — ni uno
perdido, ni uno duplicado por la concurrencia. Los avisos de avance (232 esperados)
salieron todos, sin cruzarse entre trabajos.

### 2) `verify-bandeja-carga.mjs` — a nivel de transporte real, contra la app levantada

**Qué prueba, y por qué es distinto de la prueba anterior:** `BandejaCargaTests.cs`
prueba la publicación al bus; esta prueba el **fan-out real de SignalR** — que un solo
grupo (`BandejaHub.Grupo`, sin filtrar por usuario) reparte correctamente a **todos** los
conectados bajo carga real de RabbitMQ + WebSocket, no en memoria.

**8 clientes SignalR reales**, conectados y suscritos a la vez a `/hubs/bandeja`,
mientras se disparan **12 pliegos concurrentes** (tamaños de 1 a 10 líneas) contra la
API real.

**Resultado de una corrida:**
```
Conectando 8 clientes concurrentes a /hubs/bandeja...
8 clientes conectados y suscritos al grupo único.
Disparando 12 pliegos concurrentes (cliente Almacenes Éxito S.A.)...
12 pliegos disparados en 634 ms.

Avisos 'trabajo-terminado' recibidos por cada cliente:
  cliente 1: 12   cliente 2: 12   cliente 3: 12   cliente 4: 12
  cliente 5: 12   cliente 6: 12   cliente 7: 12   cliente 8: 12
OK: los 8 clientes recibieron exactamente 12 avisos cada uno (fan-out íntegro bajo carga).
TODO OK.
```

**Estado del sistema después de la ráfaga** (las 10 colas de RabbitMQ, incluida
`AvisarBandeja`):
```
name              messages
RecostearLinea    0
EstadoPriorizacion 0
RepartidorDeLineas 0
RecalcularScores  0
FalloDeCosteo     0
EstadoAutoCosteo  0
AvisarTrabajo     0
RecosteoPorTarifas 0
AvisarBandeja     0
CostearLinea      0
```
0 mensajes en las 10 colas, ninguna cola `_error` creada, 0 líneas `fail:`/`crit:` en el
log de la API — igual que el estado que ya dejó documentado el PR para su propio
recorrido.

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ `Build succeeded --no-cache`, 0 errores, 1 advertencia conocida |
| Regresión | ✅ 237/237 en las 3 suites, sin ningún filtro, coincide exacto con el PR |
| Integración | ✅ Recorrido E2E completo del autor, sin fallos; autorización 401/200 verificada |
| **Carga y estrés** | ✅ **Nuevo.** 20 pliegos concurrentes en memoria (232 líneas, 0 contaminación entre sagas) + 8 clientes SignalR reales × 12 pliegos concurrentes contra la app real (fan-out íntegro, 0 pérdidas, sistema limpio tras la ráfaga) |

## Conclusión

Las 3 verificaciones del propio autor (build, 237/237, recorrido E2E) se reprodujeron
exactas. Se completó la cobertura de carga y estrés que faltaba para esta HU, a los dos
niveles relevantes: la saga (agrupamiento correcto bajo concurrencia) y el transporte
real (fan-out de SignalR íntegro a múltiples clientes bajo carga, sin dejar el sistema
sucio). Sin objeciones para mergear.
