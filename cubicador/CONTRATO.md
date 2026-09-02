# CONTRATO.md — guía rápida de integración

> Punto de entrada corto para que backend (.NET) y frontend integren contra
> este servicio sin tener que leer toda la documentación primero. Para el
> detalle exhaustivo de cada DTO/evento/error, ver
> [`docs/CONTRATOS.md`](./docs/CONTRATOS.md) — este archivo no lo repite,
> lo resume y apunta a él.

## 1. URLs base

| Entorno | URL | Notas |
|---|---|---|
| Docker Compose (recomendado) | `http://localhost:8081` | Puerto publicado al host (`CUBICADOR_HOST_PORT` en `.env`, default `8081`). El contenedor sigue escuchando en `8000` puertas adentro — no cambia nada de la red interna, solo lo que ve el host. Elegido para no chocar con el `8080` donde corre el backend .NET de NEXO. Si `8081` tampoco sirve, `CUBICADOR_HOST_PORT=8082`. |
| Local sin Docker (`uvicorn --reload`) | `http://localhost:8000` | Sin cambios — el `--port` de uvicorn no se tocó, solo el mapeo de Docker. |

Swagger interactivo: `{URL_BASE}/docs`. Contrato OpenAPI ya exportado (para
Postman/Insomnia/generación de cliente, sin necesitar el servicio corriendo):
[`docs/openapi.json`](./docs/openapi.json) — regenerarlo tras cambiar
endpoints/DTOs con:

```bash
.venv/Scripts/python.exe -c "
import json, sys; sys.path.insert(0, '.')
from api.main import app
json.dump(app.openapi(), open('docs/openapi.json', 'w', encoding='utf-8'), indent=2, ensure_ascii=False)
"
```

## 2. Cómo se conecta cada quién

### 2.1 Backend .NET (NEXO) — **no habla REST con este servicio**

⚠️ Punto importante que no es obvio desde afuera: NEXO **no llama** a la API
REST de este servicio. El contrato real backend↔motor es **solo por colas**
(RabbitMQ + MassTransit, mismo broker que el resto de NEXO — issue #28). Ver
la sección B de [`docs/CONTRATOS.md`](./docs/CONTRATOS.md#b-eventos-de-rabbitmq)
para el comando de entrada (`CubicaciónSolicitada`) y los eventos de salida
(`PlanDeCargueGenerado`/`CubicaciónRechazada`), con el "sobre" de MassTransit
que NEXO espera.

Consecuencia práctica: si estás integrando el lado .NET, **no necesitás el
puerto 8081 para nada** — necesitás que ambos apunten al mismo RabbitMQ
(`RABBITMQ_USER`/`RABBITMQ_PASSWORD` idénticos en los dos `.env`) y que la
compuerta de paridad de NEXO (`Cubicacion__ParidadVerificada` + evidencia +
fecha) esté encendida, o `/optimizar` responde `503` sin publicar nada.

La única llamada REST que sí existe **en sentido inverso** (este servicio →
NEXO) es para el flujo "crear plan desde línea de solicitud":

```
GET {NEXO_BASE_URL}/api/lineas-solicitud/{id}/cubicacion
Authorization: Bearer <NEXO_CALLBACK_API_KEY>
```

⚠️ **Discrepancia detectada al escribir este documento**: `docs/CONTRATOS.md`
(sección E) afirma que este endpoint está "implementado en ambos lados" y
"verificado de punta a punta". En el estado actual de la rama `QA` de este
repo eso **no es así del lado NEXO** — no existe ningún controlador ni query
para esa ruta (`grep -rn "lineas-solicitud" src/` no devuelve resultados), y
es consistente con lo que dice `PENDIENTES.md` (sección "Lo que SÍ falta"):
el endpoint no está implementado todavía. Puede que esa nota de
`CONTRATOS.md` describa una verificación hecha contra otro checkout/sesión.
No asumas que `POST /desde-linea/{id}` funciona de punta a punta hasta
confirmar que ese endpoint ya existe en la rama con la que estés integrando.

### 2.2 Frontend — sí puede hablar REST directo

Para un frontend que llame esta API directamente (no a través de NEXO):

1. **Auth**: header `Authorization: Bearer <CUBICADOR_API_KEY>` en cada
   request (excepto `GET /health`). No hay identidad de usuario ni roles —
   es un secreto de servicio compartido, no un login (ver
   `docs/CONTRATOS.md` sección D). El frontend necesita ese valor
   configurado en su propio entorno.
2. **CORS**: antes de este cambio, el servicio **no tenía CORS configurado**
   (solo pensado para consumidores server-to-server). Ahora es configurable
   por variable de entorno `CUBICADOR_CORS_ORIGENES` (lista separada por
   comas, mismo criterio que `Cors__OrigenesPermitidos` en NEXO). Sin esa
   variable, el middleware de CORS **no se agrega** (mismo comportamiento
   que antes). Para desarrollo local, `.env.example` ya trae
   `CUBICADOR_CORS_ORIGENES=http://localhost:5173`.
3. **Endpoints**: ver tabla resumen abajo, o el `openapi.json`/Swagger para
   el detalle completo de cada request/response.

## 3. Endpoints (resumen — detalle completo en `docs/CONTRATOS.md` sección A)

**Cubicador rápido** (`/api/cubicacion`, sin persistencia salvo medidas
estándar):

| Método | Ruta | Uso |
|---|---|---|
| POST | `/api/cubicacion` | Evalúa ítems contra el catálogo y sugiere vehículo/flota |
| GET | `/api/cubicacion/unidades` | Catálogo de unidades |
| GET/PUT | `/api/cubicacion/medidas-estandar` | Medidas máximas para detección de extradimensión |

**Motor 3D — Plan de Cargue** (`/api/planes-cargue`, con persistencia):

| Método | Ruta | Uso |
|---|---|---|
| GET/POST | `/vehiculos` | Catálogo de vehículos (siembra los 9 presets al primer GET) |
| DELETE | `/vehiculos/{id}` | Elimina (409 si tiene planes asociados) |
| GET/POST | `` | Listar / crear plan vacío |
| GET | `/{id}` | Plan con sus ítems |
| POST | `/{id}/items`, `/{id}/items/importar`, DELETE `/{id}/items/{itemId}` | Alta/borrado de ítems |
| POST | `/{id}/optimizar`, `/{id}/reoptimizar` | **Asíncrono**: encola en RabbitMQ, responde `202` con el plan en `procesando` |
| POST | `/desde-linea/{lineaSolicitudId}` | **Asíncrono**: responde `202` con `SolicitudEncoladaDto` — ver ⚠️ en 2.1 |
| GET | `/{id}/resultado`, `/colocaciones`, `/metricas`, `/no-cargadas`, `/exportar` | Resultado (hacer polling tras un `202`) |
| PATCH | `/{id}/colocaciones/{colocacionId}` | Ajuste manual — **siempre síncrono**, no pasa por cola |

## 4. Errores

Todos los errores de la API tienen esta forma (status HTTP según el tipo de
excepción, ver `docs/CONTRATOS.md` sección C):

```json
{ "codigo": "PLAN_NO_ENCONTRADO", "mensaje": "Plan de cargue no encontrado." }
```

## 5. Checklist antes de integrar

- [ ] `docker compose up -d --wait rabbit` en la raíz de NEXO, ANTES (o en
      paralelo con) levantar `cubicador/` — no hay `depends_on` entre
      proyectos Compose distintos.
- [ ] `RABBITMQ_USER`/`RABBITMQ_PASSWORD` idénticos en `NEXO/.env` y
      `cubicador/.env`.
- [ ] Backend .NET: `Cubicacion__ParidadVerificada`/`ParidadEvidencia`/
      `ParidadFecha` configuradas, o `/optimizar` responde `503` siempre.
- [ ] Frontend: `CUBICADOR_CORS_ORIGENES` incluye el origen exacto desde el
      que se va a llamar (protocolo+host+puerto, sin barra final).
- [ ] Frontend: tiene el `CUBICADOR_API_KEY` correcto para el header
      `Authorization: Bearer`.
- [ ] Confirmar contra el checkout real si `GET /api/lineas-solicitud/{id}
      /cubicacion` ya existe del lado NEXO antes de depender de
      `POST /desde-linea/{id}` — ver ⚠️ en 2.1.
