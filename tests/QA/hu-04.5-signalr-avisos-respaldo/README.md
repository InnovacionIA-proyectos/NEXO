# Verificación QA — S2-HU-04.5 (issue #17, PR #64)

Rama: `17-s2-hu-045-signalr-avisos-y-respaldo` (se apila sobre HU-01.1 → HU-04.3/04.4).
Verificación hecha situándose en esa rama, antes de aprobar/mergear el PR #64.
Sin conflictos de merge esta vez (`mergeable: true` desde el inicio).

> ## ⚠️ Corrección (agregada durante la verificación de la HU-05.1, PR #66)
> Este documento afirma más abajo que los avisos llegaron con **"secuencia
> estrictamente creciente"**. Es impreciso y hay que corregirlo: el
> `build/e2e/recorrido.py` del PR #66 reprodujo, contra la misma app real,
> avisos **desordenados y repetidos de verdad** (`[1, 1, 8, 8, 8, 8, 9, 8, 9,
> 9, 10, ...]`). Mi corrida de este documento solo tuvo 10 avisos y le tocó
> un orden de llegada favorable — no probaba que el canal entregue
> ordenado, solo que en esa corrida particular no se desordenó.
>
> **Lo que el contrato promete de verdad** (y que sigue cumpliéndose,
> verificado de nuevo en la HU-05.1) no es que la entrega venga ordenada,
> sino que el cliente descarta cualquier aviso con secuencia menor o igual
> a la última que pintó — con esa regla aplicada, la barra de progreso
> nunca retrocede aunque la entrega sí se desordene. Ver
> `tests/QA/hu-05.1-recalculo-score-programado/README.md` para la
> verificación correcta y la evidencia.

## Qué trae el PR
- **Hub SignalR** (`/hubs/trabajos`): un grupo por `trabajoId`, evento `"avance"`, autenticado igual que el resto del API.
- **`GET /api/trabajos/{id}`** como respaldo: reconstruye el estado completo sin haber recibido ningún aviso.
- **Número de secuencia** que solo crece, en el aviso y en la consulta — cierra un hueco de diseño que el propio autor identificó (avisos procesados a la vez pueden llegar al navegador desordenados).
- **Hallazgo mayor, ajeno al canal de avisos**: `Api.Tests` se corría siempre con `--filter Categoria=Integracion` (12 de 54 tests), y los 42 restantes llevaban varias sesiones sin ejecutarse. Al quitar el filtro aparecieron 5 fallas reales, la más grave: **`POST /api/cotizaciones/{id}/lineas` (agregar flete, el camino principal del piloto) devolvía 409 siempre**, por una convención de EF Core que interpretaba mal las claves Guid asignadas por el dominio. Bug preexistente desde antes del sprint 2, no introducido por ningún PR de esta sesión.

## Por qué esta PR importa más de lo que dice su título
Resuelve, con causa raíz y evidencia, **los 3 hallazgos preexistentes que dejé documentados como "fuera de alcance" en mis verificaciones de los PR #61 y #62**: la condición de carrera de `ApiFixture`, el formato de `/health`, y las 3 fallas de `CotizacionesTests`. No los dejo aquí sin cerrar — los verifiqué de nuevo, resueltos.

## Verificación — reproducida de forma independiente, no solo leída

### Build
```
docker compose -f docker-compose.ci.yml build && docker compose -f docker-compose.ci.yml run --rm build
```
`Build succeeded`, 0 errores, 1 advertencia conocida (`ASPDEPR005`) — igual que afirma el PR.

### Las 3 suites, sin ningún filtro, contra SQL Server + RabbitMQ reales

| Suite | Resultado | Afirmado por el PR |
|---|---|---|
| `Domain.Tests` | **61/61** ✅ | 61 |
| `Application.Tests` | **75/75** ✅ | 75 |
| `Api.Tests` (sin `--filter`) | **54/54** ✅ | 54 (antes 12) |
| **Total** | **190/190** | **190/190** |

Coincide exacto. Las 6 fallas que yo mismo había documentado como "preexistentes, fuera de alcance" en los PR #61/#62 ya no existen.

### La app real, levantada (`db` + `rabbit` + `api`, sin nada nativo)

```
docker compose up -d --build --wait
```
(con `ASPNETCORE_ENVIRONMENT=Development` en `.env` — el esquema Dev/X-Rol exige explícitamente no estar en Production, fail-closed a propósito, ver `docs/contrato-integracion`)

- **`GET /health`** → `200`, con detalle por chequeo:
  ```json
  {"status":"Healthy","chequeos":{"masstransit-bus":{"status":"Healthy"},"database":{"status":"Healthy"}}}
  ```
- **`negotiate` del hub**: sin identidad → `401`. Con `X-Rol: Admin` → `200`.
- **Canal SignalR en vivo, verificado con script propio** (`verify-signalr-live.mjs`, sin paquete cliente — habla el protocolo JSON de SignalR directo por WebSocket con el `WebSocket` nativo de Node):
  1. Crea una solicitud de licitación real.
  2. Conecta al hub, hace el handshake.
  3. Carga 8 líneas en bulk (`202` con `trabajoId`).
  4. Se suscribe al grupo del trabajo.
  5. Recoge los avisos que lleguen en 20 s.
  6. Consulta `GET /api/trabajos/{id}` como respaldo.
  7. Verifica: secuencia estrictamente creciente, la consulta nunca queda por detrás del último aviso, y el aviso no lleva la lista de pendientes (solo contadores).

  **Resultado real de una corrida:**
  ```
  Avisos recibidos: 10 (secuencia 0 → 9, estrictamente creciente)
  Consulta de respaldo: secuencia=10, estado=Parcial
  TODO OK.
  ```
  El décimo aviso (secuencia 10) nunca llegó por WebSocket — se cerró la escucha antes de que llegara — y la consulta de respaldo lo trajo al día de todas formas: exactamente la garantía que promete el contrato, reproducida con datos reales, no solo leída.

  (Estado final `Parcial` con 0/8 resueltas: esperado — el entorno de prueba no tiene tarifas semilla suficientes para costear rutas nuevas, así que las 8 líneas quedan pendientes de tarifa manual. No es un defecto del canal de avisos.)

## Cobertura por tipo de prueba

| Tipo | Resultado |
|---|---|
| Smoke | ✅ Build succeeded |
| Regresión | ✅ 190/190 en las 3 suites, sin ningún filtro |
| Integración | ✅ `/health` con detalle, `negotiate` 401/200, canal SignalR en vivo con secuencia verificada, consulta de respaldo verificada |
| Carga y estrés | Cubierta indirectamente por la HU-04.3/04.4 (20 sagas concurrentes, ya verificado ahí). No se repite aquí: el canal de avisos en sí no tiene un mecanismo propio que cargar más allá del broadcast por grupo de SignalR, que es infraestructura de Microsoft, no código de este PR |

## Conclusión
El canal de avisos cumple las 3 garantías del contrato (secuencia creciente, respaldo consistente, avisos livianos), verificado en vivo con datos reales. El hallazgo del filtro oculto en `Api.Tests` es el más importante de toda la sesión: sin él, ninguna de las verificaciones "en verde" de los PR anteriores había corrido la mitad de la suite. Sin objeciones para mergear.
