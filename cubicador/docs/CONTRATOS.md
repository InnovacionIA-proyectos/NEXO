# Contratos

Todo lo que este servicio expone, todo lo que consume de NEXO, y las formas
exactas de datos. Fuente original de los DTOs: `CONTRATOS_CUBICADOR.md` y
`Application/Cubicacion3D/Dtos/*.cs` en el repo NEXO — aquí se reescribe desde
la perspectiva de este servicio como quien AHORA define el contrato.

## Convenciones generales

- **JSON en camelCase** en todo el cuerpo de request/response (`api/dtos/comun.py`).
- **Unidades**: milímetros y kilogramos en todo lo que toca el motor 3D
  (`Contenedor`, `ItemCarga`, colocaciones). El cubicador rápido (SICCE)
  trabaja en centímetros/metros — son dos sistemas heredados de dos
  prototipos distintos, no se unifican (cambiaría comportamiento, no solo forma).
- **Ejes**: X = largo, Y = ancho, Z = alto. Origen (0,0,0) = esquina inferior
  interna opuesta a la puerta trasera.
- **Clave de correlación** de una unidad física expandida:
  `"{itemPlanCargueId}#{índice}"` (0-based) — string opaco, solo para
  correlacionar dentro de una misma llamada/mensaje. Se arma y se deshace
  únicamente en `application/plan_cargue/mapeos.py`.

## A. API REST

### `/api/cubicacion` (cubicador rápido — sin persistencia salvo medidas estándar)

| Método | Ruta | Body | Respuesta |
|---|---|---|---|
| POST | `/api/cubicacion` | `CubicacionRequestDto` | `CubicacionResponseDto` |
| GET | `/api/cubicacion/unidades` | — | `UnidadCubicacionDto[]` |
| GET | `/api/cubicacion/medidas-estandar` | — | `MedidasEstandarDto` |
| PUT | `/api/cubicacion/medidas-estandar` | `MedidasEstandarDto` | `MedidasEstandarDto` |

Límite: 10.000 bultos totales por request (`total > 10000` → 400 `CUBICACION_DEMASIADO_GRANDE`).

### `/api/planes-cargue` (motor 3D — con persistencia)

| Método | Ruta | Notas |
|---|---|---|
| GET/POST | `/vehiculos` | catálogo de tipos de vehículo (siembra los 9 presets en el primer GET) |
| DELETE | `/vehiculos/{id}` | 409 `VEHICULO_EN_USO` si tiene planes asociados |
| GET/POST | `` (raíz) | listar / crear plan vacío |
| GET | `/{id}` | plan con sus ítems |
| GET | `/{id}/flota` | vehículos (planes) de la misma flota |
| DELETE | `/{id}` | elimina el plan |
| POST | `/demo` | escenario de demostración |
| POST | `/{id}/items`, `/{id}/items/importar`, `DELETE /{id}/items/{itemId}` | alta/borrado de ítems (importar: 1-2000) |
| GET | `/{id}/resultado`, `/colocaciones`, `/metricas`, `/no-cargadas`, `/exportar` | resultado completo o subconjuntos |
| PATCH | `/{id}/colocaciones/{colocacionId}` | ajuste manual — SIEMPRE síncrono |
| POST | `/{id}/optimizar`, `/{id}/reoptimizar` | optimizar el plan |
| POST | `/desde-linea/{lineaSolicitudId}` | puente Mesa de Costeo → 3D — encola, responde 202 `SolicitudEncoladaDto` |
| GET | `/por-linea/{lineaSolicitudId}` | flota resultante del `POST` de arriba — lista vacía mientras sigue `procesando` |

`optimizar`/`reoptimizar` y `desde-línea` encolan en RabbitMQ y responden de
inmediato (202) con el plan en estado `procesando` (o `SolicitudEncoladaDto`
en el caso de `desde-línea`, que todavía no tiene un plan que devolver) — ver
DECISIONES_TECNICAS.md #5. El caller sigue el resultado con polling sobre
`GET /{id}/resultado` (o `GET /por-linea/{lineaSolicitudId}` para el puente).

## B. Eventos de RabbitMQ (implementado y verificado de punta a punta — routers HTTP conectados, ver README.md "Estado actual")

### Comando entrante: `CubicaciónSolicitada`

Publicado por la API al recibir `POST /{id}/optimizar` o `POST /desde-linea/{id}`.

```json
{
  "tipo": "optimizar" | "crear_desde_linea",
  "planId": "uuid",
  "correlationId": "uuid",
  "solicitud": { "estrategia": "...", "algoritmo": "...", "semilla": null, "ponderaciones": null, "respetarBloqueadas": false }
}
```

### Eventos de salida (el worker publica UNO de estos al terminar)

- **`PlanDeCargueGenerado`**: `{ correlationId, planId, resultado: {...} }`
- **`CubicaciónRechazada`**: `{ correlationId, planId, codigo: string, mensaje: string }`
- **`PlanDeCargueAjustado`**: mismo payload que `PlanDeCargueGenerado`, para si en el futuro el ajuste manual también se decide mover a async (hoy es síncrono, ver DECISIONES_TECNICAS.md #5).

### Colas/exchanges

- Exchange `cubicador.eventos` (topic).
- Routing keys: `cubicacion.solicitada`, `plan-de-cargue.generado`,
  `cubicacion.rechazada`, `plan-de-cargue.ajustado`.
- Dead-letter: `cubicacion.dlq` (ver DECISIONES_TECNICAS.md #10).

## C. Formato de error (API REST)

```json
{ "codigo": "PLAN_NO_ENCONTRADO", "mensaje": "Plan de cargue no encontrado." }
```

HTTP status por tipo (`api/main.py`, manejador de `AppException`):

| Excepción (`application/excepciones.py`) | Status |
|---|---|
| `ValidationFailedException` | 400 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `ServiceUnavailableException` | 503 |

Catálogo de códigos ya implementado (heredado 1:1 de los Commands de NEXO):

`DIMENSIONES_INVALIDAS`, `CAPACIDAD_INVALIDA`, `PUERTA_INVALIDA`,
`PUERTA_POSICION_INVALIDA`, `TOLERANCIA_INVALIDA`, `EJES_INVALIDOS`,
`VEHICULO_INVALIDO`, `VEHICULO_EN_USO`, `VEHICULO_NO_ENCONTRADO`,
`PLAN_NO_ENCONTRADO`, `PESO_INVALIDO`, `CANTIDAD_INVALIDA`,
`IMPORTACION_INVALIDA`, `PLAN_DEMASIADO_GRANDE`, `ESTRATEGIA_INVALIDA`,
`ALGORITMO_INVALIDO`, `COLOCACION_NO_ENCONTRADA`, `ORIENTACION_NO_PERMITIDA`,
`FUERA_DE_LIMITES`, `COLISION`, `SOPORTE_INSUFICIENTE`, `APILAMIENTO`,
`AJUSTE_INVALIDO`, `LINEA_NO_ENCONTRADA`, `SIN_POSICIONES`,
`CUBICACION_DEMASIADO_GRANDE`, `MEDIDAS_INVALIDAS`.

## D. Autenticación

Header en **todas** las llamadas, en ambas direcciones:

```
Authorization: Bearer <secreto correspondiente>
```

- NEXO → este servicio: valida contra `CUBICADOR_API_KEY` (`api/dependencias.py:verificar_api_key`).
- Este servicio → NEXO: NEXO valida contra `NEXO_CALLBACK_API_KEY`.

Sin identidad de usuario ni roles en ninguno de los dos sentidos — ver
DECISIONES_TECNICAS.md #7.

## E. Lo que este servicio consume DE NEXO (implementado en ambos lados)

```
GET {NEXO_BASE_URL}/api/lineas-solicitud/{id}/cubicacion
Authorization: Bearer <NEXO_CALLBACK_API_KEY>

200 → { "lineaId": "guid", "posiciones": [{ "descripcion"?, "cantidad", "largoCm", "anchoCm", "altoCm", "pesoKg"? }], "origen"?, "destino"? }
404 → cualquier cuerpo — este cliente solo mira el status code (ver nexo_client.py). El cuerpo real de NEXO es
      { "code": "LINEA_NO_ENCONTRADA", "message": "...", "timestamp": "...", "details": null } (la forma estándar
      de error de NEXO — NO {codigo, mensaje} como el resto de este documento; NEXO no reescribió su formato
      global de errores solo para este endpoint, y no hacía falta: el cliente Python no lo parsea).
```

Implementado en `infrastructure/clientes/nexo_client.py` (lado Cubicador) y
en `LineasSolicitudController`/`ObtenerCubicacionLineaQuery` (lado NEXO —
ver `MIGRACION.md`, décima pasada, y `CONTRATOS_CUBICADOR.md` en ese repo).
**Verificado de punta a punta**: `docker compose up -d` de NEXO contra SQL
Server real + datos sembrados, y `NexoLineaSolicitudClient.obtener(...)`
ejecutado de verdad contra esa API — confirmó el parseo correcto de
`posiciones`/`origen`/`destino` (incluida una línea sin `Dimensiones`,
lista vacía) y que una línea inexistente devuelve `None` (404).
