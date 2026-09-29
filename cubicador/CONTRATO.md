# Contrato de integración — Cubicador

> Documento único (reemplaza a `docs/CONTRATOS.md`, que quedaba enterrado y
> duplicaba contenido). Todo lo que este servicio expone, todo lo que
> consume de NEXO, y las formas exactas de datos — verificado contra el
> código real al escribir esto, no copiado de memoria. Si algo cambia acá
> (endpoint, DTO, código de error), el sitio a actualizar es este archivo.

## 1. Quickstart — URLs, Swagger, cómo conectarse

| Entorno | URL base | Notas |
|---|---|---|
| Docker Compose (recomendado) | `http://localhost:8081` | Puerto publicado al host (`CUBICADOR_HOST_PORT` en `.env`, default `8081`). El contenedor sigue escuchando en `8000` puertas adentro — no cambia nada de la red interna, solo lo que ve el host. Se eligió 8081 para no chocar con el 8080 donde corre el backend .NET de NEXO; si tampoco sirve, `CUBICADOR_HOST_PORT=8082`. |
| Local sin Docker (`uvicorn --reload`) | `http://localhost:8000` | Sin cambios — el `--port` de uvicorn no se tocó, solo el mapeo de Docker. |

- **Swagger interactivo**: `{URL_BASE}/docs` (botón "Authorize" arriba a la
  derecha — pegar el `CUBICADOR_API_KEY` sin la palabra "Bearer", Swagger la
  agrega sola).
- **OpenAPI ya exportado**: [`docs/openapi.json`](./docs/openapi.json) —
  para importar en Postman/Insomnia o generar un cliente sin tener el
  servicio corriendo. Regenerarlo tras cambiar endpoints/DTOs:
  ```bash
  .venv/Scripts/python.exe -c "
  import json, sys; sys.path.insert(0, '.')
  from api.main import app
  json.dump(app.openapi(), open('docs/openapi.json', 'w', encoding='utf-8'), indent=2, ensure_ascii=False)
  "
  ```

### 1.1 Backend .NET (NEXO) — no habla REST con este servicio (con una excepción declarada)

⚠️ Punto que no es obvio desde afuera: NEXO **no llama** a la API REST de
este servicio para nada de Plan de Cargue. El contrato real backend↔motor es
**por colas** (RabbitMQ + MassTransit, mismo broker que el resto de NEXO).
Ver sección 3 más abajo para el mensaje de entrada y los eventos
de salida.

**Única excepción, acotada a un solo endpoint**:
`POST /api/cubicacion` (cubicación rápida, sin persistencia — sección 3.1).
Al no haber plan ni Id que guardar ni proyectar, no hay cola que lo procese
ni proyección que lo sirva después — NEXO lo llama directo con el mismo
Bearer de esta sección (`CUBICADOR_API_KEY`), guardado en su configuración
como `Cubicador3D:OutboundApiKey` (nunca en el navegador). Ver
`NEXO/src/Nexo.Application/CasosDeUso/Cubicacion/Interfaces/ICubicacionRapidaPort.cs`
para el razonamiento completo de por qué esto no reabre la puerta a más
llamadas HTTP.

Consecuencia práctica: si estás integrando el lado .NET, **el puerto 8081
no importa para nada** — lo que hace falta es:
- que ambos `.env` (raíz de NEXO y `cubicador/`) tengan el **mismo**
  `RABBITMQ_USER`/`RABBITMQ_PASSWORD` (mismo broker compartido);
- que el `rabbit` de la raíz de NEXO esté corriendo antes (o en paralelo)
  de levantar `cubicador/` — no hay `depends_on` entre proyectos Compose
  distintos: `docker compose up -d --wait rabbit` desde la raíz primero;
- que la compuerta de paridad de NEXO
  (`Cubicacion__ParidadVerificada`/`ParidadEvidencia`/`ParidadFecha`) esté
  encendida — si no, `POST /api/planes-cargue/{id}/optimizar` en NEXO
  responde `503` sin publicar nada a la cola.

### 1.2 Frontend — YA NO habla REST directo (ya cerrado)

**Anteriormente** un frontend podía hablarle REST directo a este
servicio (con `CUBICADOR_CORS_ORIGENES` + `CUBICADOR_API_KEY` en el
navegador). Eso se cerró a propósito: toda escritura y toda lectura del plan
de cargue pasan ahora por NEXO. **No queda
middleware CORS en el código** (`api/main.py`) ni la variable
`CUBICADOR_CORS_ORIGENES` en `.env`/`.env.example`/`docker-compose.yml` — no
es que esté vacía por defecto, es que la capacidad se retiró del todo. Si
algún día vuelve a hacer falta un frontend hablándole directo a este
servicio, es una decisión nueva a tomar a propósito (reintroducir el
middleware), no reabrir una variable vieja.

⚠️ **Estado real del frontend, a la fecha de este documento.** La versión
anterior de este párrafo afirmaba que el visor 3D *"ya consume solo esos
endpoints de NEXO, confirmado y verificado en QA"*. **No es así todavía**:
`administracion_embarqa_nexo` sigue con 12 puntos de llamada directa a este
servicio — nadie tocó esos call-sites todavía. El CORS se retiró igual, y está
bien que así sea —la capacidad no debe existir— pero conviene no leer esta
sección como si la migración del portal ya estuviera hecha.

Del lado NEXO sí hubo avance: de los 4 endpoints que
faltaban para completar esa migración, ya se agregaron 3 (`desde-linea/{id}`,
`por-linea/{id}`, `{id}/colocaciones`) sobre el `crear_desde_linea` que este
servicio ya implementaba de punta a punta — solo faltaba que NEXO lo publicara
en vez de que lo disparara el navegador. Los 20 de 20 ya tienen equivalente del lado NEXO: `POST /api/cubicacion` se
resolvió como excepción HTTP declarada (sección 1.1), no como comando de
cola — es la única de las 20 sin plan ni Id que persistir. Inventario 1 a 1
en `NEXO/docs/mensajeria/CUBICACION.md` §8.

`CUBICADOR_API_KEY` (sección 4/5) sigue existiendo — protege la API para
consumidores server-to-server legítimos (hoy: scripts de CI/verificación
manual, `tests/api/test_smoke.py`, `scripts/ci_docker_smoke.py`). Rotada
porque el valor anterior estuvo expuesto en el bundle
público del portal mientras el navegador todavía llamaba directo.

### 1.3 Checklist antes de integrar

- [ ] `docker compose up -d --wait rabbit` en la raíz de NEXO, ANTES (o en
      paralelo con) levantar `cubicador/`.
- [ ] `RABBITMQ_USER`/`RABBITMQ_PASSWORD` idénticos en `NEXO/.env` y
      `cubicador/.env`.
- [ ] Backend .NET: compuerta de paridad configurada, o `/optimizar`
      responde `503` siempre (sección 1.1) — checklist de encendido en
      `docs/CHECKLIST_DESPLIEGUE.md`.
- [ ] `POST /desde-linea/{id}`: `NEXO_CALLBACK_API_KEY` (`cubicador/.env`) debe
      ser IDÉNTICA a `Cubicador3D__InboundApiKey` (`NEXO/.env`) — es el mismo
      secreto compartido, visto desde cada lado (sección 5).

---

## 2. Convenciones generales

- **JSON en camelCase** en todo el cuerpo de request/response
  (`api/dtos/comun.py` — `ApiModel` con `alias_generator=to_camel`;
  atributos Python en snake_case, JSON de la red en camelCase).
- **Unidades**: milímetros y kilogramos en todo lo que toca el motor 3D
  (`/api/planes-cargue`: vehículos, ítems, colocaciones). El cubicador
  rápido (`/api/cubicacion`) trabaja en **centímetros** — son dos sistemas
  heredados de dos prototipos distintos, no se unifican (cambiaría
  comportamiento, no solo forma). ⚠️ Si tu integración toca ambos endpoints,
  no asumas la misma unidad en los dos.
- **Ejes**: X = largo, Y = ancho, Z = alto. Origen (0,0,0) = esquina inferior
  interna opuesta a la puerta trasera.
- **Decimales**: todo `float` de punta a punta (no hay `Decimal` en este
  servicio — ver el motor en el brief de extracción del repo si necesitás
  el detalle de por qué).

---

## 3. API REST

### 3.1 `/api/cubicacion` — cubicador rápido (sin persistencia salvo medidas estándar)

| Método | Ruta | Body | Respuesta |
|---|---|---|---|
| POST | `/api/cubicacion` | `CubicacionRequestDto` | `CubicacionResponseDto` |
| GET | `/api/cubicacion/unidades` | — | `UnidadCubicacionDto[]` |
| GET | `/api/cubicacion/medidas-estandar` | — | `MedidasEstandarDto` |
| PUT | `/api/cubicacion/medidas-estandar` | `MedidasEstandarDto` | `MedidasEstandarDto` |

Límite: **10.000 bultos totales por request** (`application/cubicacion_rapida/casos_de_uso.py:12`) — por encima, `400 CUBICACION_DEMASIADO_GRANDE`.

DTOs reales (`api/dtos/cubicacion_rapida.py`):

```python
class ItemCubicacionDto(ApiModel):
    descripcion: str | None = None
    cantidad: int = 1
    largo_cm: float
    ancho_cm: float
    alto_cm: float
    peso_kg: float | None = None
    tipo_mercancia: str | None = None

class CubicacionRequestDto(ApiModel):
    items: list[ItemCubicacionDto]
    unidad_ids: list[str] | None = None
    clase_viaje: str | None = None

class UnidadCubicacionDto(ApiModel):
    id: str
    nombre: str
    geometria: str
    control_ambiental: str
    largo_util_m: float
    ancho_util_m: float
    alto_util_m: float
    peso_bruto_permitido_kg: float
    peso_vacio_kg: float
    capacidad_por_eje_kg: list[float]
    config_vehiculo: str | None
    carroceria: str | None
    clase_viaje: str | None
    clase_contenedor: str | None

class MedidasEstandarDto(ApiModel):
    largo_max_cm: float
    ancho_max_cm: float
    alto_max_cm: float
    peso_max_kg: float

class CubicacionResponseDto(ApiModel):
    matrices: list[MatrizCubicacionDto]        # una fila por unidad evaluada
    sugerencia: SugerenciaVehiculoDto | None    # mejor unidad para UN vehículo
    advertencias: list[str]
    sugerencia_flota: SugerenciaFlotaDto | None = None   # si necesita varios vehículos
    extradimension: DeteccionExtradimensionDto | None = None

class MatrizCubicacionDto(ApiModel):
    unidad: UnidadCubicacionDto
    criterios: list[CriterioCubicacionDto]      # {criterio, resultado, detalle, criticidad}
    banda: str
    advertencias: list[str]
    disposicion: DisposicionDto | None          # colocaciones + peso + centro de gravedad

class SugerenciaVehiculoDto(ApiModel):
    unidad_id: str
    unidad_nombre: str
    banda: str
    factor_utilizacion: float
    config_vehiculo: str | None
    carroceria: str | None
    clase_viaje: str | None
    clase_contenedor: str | None

class DeteccionExtradimensionDto(ApiModel):
    hay_extradimension: bool
    medidas_estandar: MedidasEstandarDto
    piezas: list[PiezaExcedidaDto]              # {descripcion, excesos, largo_cm, ancho_cm, alto_cm, peso_kg}
    sugerencia: str | None
```

### 3.2 `/api/planes-cargue` — motor 3D (con persistencia)

| Método | Ruta | Notas |
|---|---|---|
| GET/POST | `/vehiculos` | catálogo de tipos de vehículo (siembra los 9 presets en el primer GET; cada preset sembrado publica `vehiculo.creado`, igual que un alta normal, para que la proyección de NEXO no vuelva a quedarse atrás) |
| DELETE | `/vehiculos/{id}` | `409 VEHICULO_EN_USO` si tiene planes asociados |
| POST | `/vehiculos/republicar-catalogo` | Reparo manual — republica `vehiculo.creado` para TODO el catálogo actual (no solo lo nuevo). Idempotente del lado NEXO (upsert por Id); no forma parte de ningún flujo automático, se invoca a mano si la proyección de NEXO vuelve a desincronizarse |
| GET/POST | `` (raíz) | listar / crear plan vacío |
| GET | `/{id}` | plan con sus ítems |
| GET | `/{id}/flota` | vehículos (planes) de la misma flota |
| DELETE | `/{id}` | elimina el plan |
| POST | `/demo` | escenario de demostración |
| POST | `/{id}/items`, `/{id}/items/importar`, DELETE `/{id}/items/{itemId}` | alta/borrado de ítems (importar: 1-2000) |
| GET | `/{id}/resultado`, `/colocaciones`, `/metricas`, `/no-cargadas`, `/exportar` | resultado completo o subconjuntos |
| PATCH | `/{id}/colocaciones/{colocacionId}` | ajuste manual — **siempre síncrono**, nunca pasa por cola |
| POST | `/{id}/optimizar`, `/{id}/reoptimizar` | **asíncrono**: encola en RabbitMQ, responde `202` con el plan en `procesando` |
| POST | `/desde-linea/{lineaSolicitudId}` | puente Mesa de Costeo → 3D — **asíncrono**, responde `202 SolicitudEncoladaDto` (ver ⚠️ sección 5) |
| GET | `/por-linea/{lineaSolicitudId}` | flota resultante del `POST` de arriba — lista vacía mientras sigue `procesando` |

En NEXO, los endpoints de esta sección que hoy el
navegador podía llamar DIRECTO (alta/borrado de vehículos, planes, ítems, y el
PATCH de colocaciones de la fila de arriba) ahora tienen un camino alterno
**recomendado** desde NEXO (`POST /api/planes-cargue`,
`POST /api/planes-cargue/demo`, `POST /api/vehiculos`,
`POST /api/planes-cargue/{id}/items[/importar]`,
`DELETE /api/planes-cargue/{id}[/items/{itemId}]`,
`DELETE /api/vehiculos/{id}`,
`PATCH /api/planes-cargue/{id}/colocaciones/{colocacionId}`) que publica por
la MISMA cola que `optimizar`/`desde-linea` — ver
`NEXO/docs/mensajeria/CUBICACION.md` sección 6 para el contrato completo.
`worker/main.py` aplica esos 9 comandos con los MISMOS casos de uso que esta
API síncrona (`application/plan_cargue/*.py`) — nada de lógica nueva, solo un
segundo punto de entrada. Esta API directa NO se retira en esta HU (la siguen
usando esta app y el smoke test de Docker); es el navegador de NEXO el que
deja de llamarla.

`optimizar`/`reoptimizar` y `desde-linea` encolan y responden de inmediato
con `202`. El caller sigue el resultado con **polling** sobre
`GET /{id}/resultado` (o `GET /por-linea/{lineaSolicitudId}` para el
puente) — no hay webhook ni notificación push desde este servicio.

DTOs reales (`api/dtos/plan_cargue.py`):

```python
class EjeVehiculoDto(ApiModel):
    posicion_mm: float
    capacidad_kg: float

class VehiculoCubicacionDto(ApiModel):
    id: str
    nombre: str
    descripcion: str | None
    largo_interno_mm: float
    ancho_interno_mm: float
    alto_interno_mm: float
    capacidad_kg: float
    tara_kg: float
    puerta_ancho_mm: float
    puerta_alto_mm: float
    puerta_posicion: str          # trasera | frontal | costado_izquierdo | costado_derecho
    ejes: list[EjeVehiculoDto]
    tolerancia_paredes_mm: float
    tolerancia_entre_unidades_mm: float
    es_preset: bool

class GuardarVehiculoInput(ApiModel):
    id: str | None = None         # None = crear; con id = actualizar (upsert)
    nombre: str
    descripcion: str | None = None
    largo_interno_mm: float
    ancho_interno_mm: float
    alto_interno_mm: float
    capacidad_kg: float
    tara_kg: float
    puerta_ancho_mm: float
    puerta_alto_mm: float
    puerta_posicion: str
    ejes: list[EjeVehiculoDto]
    tolerancia_paredes_mm: float = 0.0
    tolerancia_entre_unidades_mm: float = 0.0

class ItemPlanCargueDto(ApiModel):
    id: str
    plan_cargue_id: str
    sku: str
    descripcion: str
    pedido: str | None
    cliente: str | None
    destino: str | None
    secuencia_entrega: int
    largo_mm: float
    ancho_mm: float
    alto_mm: float
    peso_kg: float
    cantidad: int
    color: str | None
    prioridad: int
    tipo_embalaje: str | None
    apilable: bool
    max_niveles: int
    max_peso_encima_kg: float     # -1.0 = sin restriccion (ver GuardarItemPlanCargueInput)
    rotable: bool
    mantener_vertical: bool
    fragil: bool
    peligroso: bool               # declarado, sin efecto en la optimizacion hoy
    refrigerado: bool             # declarado, sin efecto en la optimizacion hoy
    grupo_compatibilidad: str | None
    observaciones: str | None

class GuardarItemPlanCargueInput(ApiModel):
    id: str | None = None
    sku: str = ""          # acepta null explicito -> se normaliza a "" (ver nota abajo)
    descripcion: str
    pedido: str | None = None
    cliente: str | None = None
    destino: str | None = None
    secuencia_entrega: int = 1
    largo_mm: float
    ancho_mm: float
    alto_mm: float
    peso_kg: float
    cantidad: int = 1
    color: str | None = None
    prioridad: int = 3
    tipo_embalaje: str | None = None
    apilable: bool = True
    max_niveles: int = 0
    max_peso_encima_kg: float = -1.0
    rotable: bool = True
    mantener_vertical: bool = False
    fragil: bool = False
    peligroso: bool = False
    refrigerado: bool = False
    grupo_compatibilidad: str | None = None
    observaciones: str | None = None

class PlanCargueDto(ApiModel):
    id: str
    nombre: str
    vehiculo_id: str
    linea_solicitud_id: str | None
    numero_vehiculo: int
    estado: str                   # borrador | procesando | optimizado | ajustado | error
    estrategia: str                # default "Combinada"
    algoritmo: str                  # default "bfd" (o "ffd")
    semilla: int | None
    peso_total_kg: float
    volumen_utilizado_m3: float
    utilizacion_volumen: float
    utilizacion_piso: float
    puntaje: float
    items: list[ItemPlanCargueDto]
    created_at: datetime
    updated_at: datetime

class ColocacionCargueDto(ApiModel):
    id: str
    plan_cargue_id: str
    item_plan_cargue_id: str
    indice_unidad: int
    cargada: bool                  # false = rechazada; ver codigo_rechazo/razon_rechazo
    x: float
    y: float
    z: float
    largo_colocado_mm: float
    ancho_colocado_mm: float
    alto_colocado_mm: float
    orientacion_indice: int
    secuencia_cargue: int
    soporte_colocacion_id: str | None
    porcentaje_soporte: float
    peso_encima_kg: float
    estado_estabilidad: str        # estable | advertencia
    accesibilidad: str
    bloqueada: bool
    ajuste_manual: bool
    codigo_rechazo: str | None     # ver catalogo en 3.3 mas abajo
    razon_rechazo: str | None

class IncidenciaValidacionDto(ApiModel):
    severidad: str
    codigo_regla: str
    titulo: str
    descripcion: str
    accion_sugerida: str | None
    colocacion_item_id: str | None

class MetricasCubicacionDto(ApiModel):
    volumen_interno_m3: float
    volumen_utilizado_m3: float
    porcentaje_volumen: float
    area_piso_m2: float
    area_piso_utilizada_m2: float
    porcentaje_piso: float
    peso_maximo_kg: float
    peso_cargado_kg: float
    porcentaje_peso: float
    unidades_totales: int
    unidades_cargadas: int
    unidades_no_cargadas: int
    pedidos_completos: int
    pedidos_parciales: int
    destinos: int
    altura_maxima_mm: float
    centro_gravedad_mm: list[float]
    peso_por_eje_kg: list[float]
    diferencia_lados_kg: float
    advertencias: int
    violaciones_criticas: int
    puntaje: float
    puntajes_parciales: dict[str, float]
    espacio_vacio_util_m3: float
    espacio_fragmentado_m3: float

class ResultadoPlanDto(ApiModel):
    plan: PlanCargueDto
    vehiculo: VehiculoCubicacionDto
    colocaciones: list[ColocacionCargueDto]
    metricas: MetricasCubicacionDto | None
    incidencias: list[IncidenciaValidacionDto]

class PlanFlotaItemDto(ApiModel):
    id: str
    numero_vehiculo: int
    total_vehiculos: int
    unidades_cargadas: int
    unidades_totales: int

class OptimizarRequestDto(ApiModel):
    estrategia: str = "Combinada"
    algoritmo: str = "bfd"
    semilla: int | None = None
    ponderaciones: PonderacionesCombinadaDto | None = None
    respetar_bloqueadas: bool = False

class PonderacionesCombinadaDto(ApiModel):
    volumen: float = 0.25
    balance_ejes: float = 0.25
    estabilidad: float = 0.20
    secuencia: float = 0.15
    prioridad: float = 0.10
    unidades: float = 0.05

class AjusteColocacionInput(ApiModel):
    x: float | None = None
    y: float | None = None
    z: float | None = None
    orientacion_indice: int | None = None
    bloqueada: bool | None = None
    retirar: bool | None = None
    reinsertar: bool | None = None

class CrearPlanRequest(ApiModel):
    nombre: str
    vehiculo_id: str

class SolicitudEncoladaDto(ApiModel):
    """Respuesta 202 de una operacion que se resuelve async."""
    correlation_id: str
    estado: str = "procesando"
```

> **`sku` acepta `null` explícito**. `GuardarItemRequest.Sku`
> en NEXO es `string?` —un ítem sin SKU es un caso de negocio legítimo— y cuando no lo
> tiene, NEXO manda la clave con `null`, no la omite. En Pydantic el default solo aplica
> si la clave **falta**, así que ese `null` se rechazaba contra un campo `str`: el ítem
> fallaba 3 veces, caía en la DLQ y marcaba **todo el plan** en `error`.
>
> Un `field_validator(mode="before")` lo normaliza a `""` antes de la validación de
> tipo. Se normaliza en vez de declarar `str | None` porque la columna `sku` **no es
> nullable** (`modelos.py`: `Mapped[str]`, default `""`) y la entidad también usa `""`
> como ausencia — aceptar el null solo en el borde habría movido el fallo a la base, no
> lo habría quitado. Para quien consume el DTO, el tipo sigue siendo `str`.

### 3.3 Catálogo de `codigo_rechazo` en `ColocacionCargueDto`

Distinto del catálogo de errores HTTP de la sección 4 — estos son razones de
negocio dentro de una respuesta `200` exitosa, cuando una unidad no se pudo
cargar:

| Código | Cuándo (`domain/motor/optimizadores.py`, `infrastructure/persistence/repositorios/plan_cargue_repo.py`) |
|---|---|
| `PUERTA` | Ninguna orientación permitida pasa por la puerta del vehículo |
| `PESO_TOTAL` | Cargarla superaría la capacidad del vehículo |
| `COMPATIBILIDAD` | Choca con el grupo de compatibilidad de otra unidad ya cargada |
| `SIN_ESPACIO` | No se encontró posición válida (colisión/soporte/apilamiento/límites) para ninguna orientación |
| `RETIRADA_MANUAL` | El usuario la retiró con `PATCH .../colocaciones/{id}` (`{"retirar": true}`) |

---

## 4. Formato de error (API REST)

```json
{ "codigo": "PLAN_NO_ENCONTRADO", "mensaje": "Plan de cargue no encontrado." }
```

HTTP status por tipo de excepción (`api/main.py`, `application/excepciones.py`):

| Excepción | Status |
|---|---|
| `ValidationFailedException` | 400 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `ServiceUnavailableException` | 503 |
| Bearer inválido/ausente (`api/dependencias.py`) | 401 `NO_AUTORIZADO` |

Catálogo de `codigo` verificado por grep contra el código real (los 28 que
efectivamente existen — no una lista heredada sin revisar):

`ALGORITMO_INVALIDO`, `APILAMIENTO`, `CANTIDAD_INVALIDA`,
`CAPACIDAD_INVALIDA`, `COLISION`, `COLOCACION_NO_ENCONTRADA`,
`CUBICACION_DEMASIADO_GRANDE`, `DIMENSIONES_INVALIDAS`, `EJES_INVALIDOS`,
`ESTRATEGIA_INVALIDA`, `FUERA_DE_LIMITES`, `IMPORTACION_INVALIDA`,
`LINEA_NO_ENCONTRADA`, `MEDIDAS_INVALIDAS`, `NO_AUTORIZADO`,
`ORIENTACION_NO_PERMITIDA`, `PESO_INVALIDO`, `PLAN_DEMASIADO_GRANDE`,
`PLAN_NO_ENCONTRADO`, `PUERTA_INVALIDA`, `PUERTA_POSICION_INVALIDA`,
`SOPORTE_INSUFICIENTE`, `TOLERANCIA_INVALIDA`, `VEHICULO_EN_USO`,
`VEHICULO_INVALIDO`, `VEHICULO_NO_ENCONTRADO`.

(`SIN_ESPACIO` y `RETIRADA_MANUAL` NO son códigos de error HTTP — son
valores de `codigo_rechazo`, ver sección 3.3.)

---

## 5. Autenticación

Header en **todas** las llamadas, en ambas direcciones:

```
Authorization: Bearer <secreto correspondiente>
```

- Consumidores server-to-server → este servicio: valida contra
  `CUBICADOR_API_KEY` (`api/dependencias.py:verificar_api_key`). **NEXO SÍ es
  uno de estos consumidores, pero solo para un endpoint** (`POST
  /api/cubicacion`, sección 1.1) — todo lo demás sigue siendo solo por
  colas. NEXO guarda este mismo valor como `Cubicador3D__OutboundApiKey`.
  Los otros consumidores son CI/scripts de verificación manual;
  anteriormente también la usaba el navegador vía CORS — ya cerrado, y
  la clave fue rotada porque ese valor estuvo
  expuesto en el bundle público del portal.
- Este servicio → NEXO (llamada de `crear_desde_linea`, ver sección 7): NEXO
  valida contra `Cubicador3D__InboundApiKey`
  (`CubicadorApiKeyAuthenticationHandler`, esquema `CubicadorApiKey` aparte
  de la sesión del portal) — debe ser IDÉNTICA a `NEXO_CALLBACK_API_KEY` acá.
  También rotada, como higiene general aunque este
  par no fue el que se filtró.

Sin identidad de usuario ni roles en ninguno de los dos sentidos — es un
secreto de servicio, no un login.

---

## 6. Mensajería (RabbitMQ / MassTransit) — el contrato real con NEXO

### 6.1 Comando entrante: `CubicaciónSolicitada`

Publicado por la API al recibir `POST /{id}/optimizar` o
`POST /desde-linea/{id}` (payload plano, sin sobre) — **o por NEXO**,
que lo publica con `IPublishEndpoint` de MassTransit
real, envuelto en su "sobre" estándar (`messageType`/`message`). El worker
acepta ambos formatos: si el JSON trae `message`+`messageType`, se
desenvuelve primero; si no, se usa tal cual (`worker/main.py::_procesar`).

```json
{
  "tipo": "optimizar" | "crear_desde_linea" | "crear_plan" | "crear_plan_demo" | "guardar_vehiculo" | "guardar_item" | "importar_items" | "eliminar_item" | "eliminar_plan" | "ajustar_colocacion",
  "planId": "uuid",
  "correlationId": "uuid",
  "solicitud": { "...": "shape distinto por tipo, ver NEXO/docs/mensajeria/CUBICACION.md seccion 6.3 para el detalle de los 8 nuevos" }
}
```

Los 8 tipos nuevos son operaciones de datos, no de
cálculo — comparten exchange/routing-key/reintentos con `optimizar`, pero
NO pasan por ninguna compuerta de paridad. `ajustar_colocacion` reemplaza el
PATCH síncrono que existía antes en `/{id}/colocaciones/{colocacionId}` (ver
`docs/decisiones/ADR-ajuste-manual-async.md` del lado de NEXO) — sigue
resolviéndose con la MISMA lógica de negocio de siempre
(`application/plan_cargue/ajustar.py`), solo que ahora llega por cola en vez
de HTTP directo.

### 6.2 Eventos de salida (el worker publica alguno de estos al terminar)

- **`PlanDeCargueGenerado`**: `{ correlationId, planId, resultado: {...} }` —
  se publica para CUALQUIER tipo que termine con un plan vivo que reportar
  (`optimizar`, `crear_desde_linea`, `crear_plan`, `crear_plan_demo`,
  `guardar_item`, `importar_items`, `eliminar_item`, `ajustar_colocacion` —
  no para `guardar_vehiculo`/`eliminar_plan`, que no tienen plan que
  reportar). **Enriquecido después**: `resultado` ya no es
  `{estado, puntaje, colocaciones: int}` — es el `ResultadoPlanDto` completo
  (items, colocaciones con geometría, métricas, incidencias). No existe un
  evento separado para el ajuste manual: reutiliza este mismo, es la misma
  forma de reportar cualquier desenlace de un plan.
- **`CubicaciónRechazada`**: `{ correlationId, planId, codigo: string, mensaje: string }`
  — para CUALQUIERA de los 9 tipos que falle (ver `worker/main.py::_procesar`,
  `marcar_error` es un no-op seguro si el id no corresponde a un plan real).
- **`VehiculoCreado`/`VehiculoActualizado`**: `{ correlationId, vehiculo: {...VehiculoCubicacionDto...} }`
  — se publican cuando `guardar_vehiculo` crea o actualiza una fila,
  respectivamente (según si el `id` ya existía antes de guardar).

Todos menos `CubicaciónSolicitada` van envueltos en el "sobre" de MassTransit
— NEXO los consume con un `IConsumer<T>` real
(`PlanDeCargueGeneradoConsumer`/`CubicacionRechazadaConsumer`/
`VehiculoCatalogoConsumer`, ver `NEXO/docs/mensajeria/CUBICACION.md`), que
espera ese formato para deserializar:

```json
{
  "messageId": "uuid",
  "messageType": ["urn:message:Nexo.Mensajes.Cubicacion:PlanDeCargueGenerado"],
  "message": { "correlationId": "uuid", "planId": "uuid", "resultado": {...} },
  "sentTime": "2026-08-27T21:00:00.000Z"
}
```

`CubicaciónSolicitada` (el que Cubicador consume) nunca se publica envuelto
desde este lado — solo lo consume Cubicador mismo. `_sobre_masstransit()`
en `infrastructure/mensajeria/rabbitmq.py` arma el sobre; el `messageType`
debe coincidir EXACTO con el namespace/nombre de la clase C#
correspondiente — si NEXO renombra esa clase, este valor queda
desincronizado sin ningún error de compilación que lo avise, solo se nota
si el mensaje deja de entregarse.

### 6.3 Colas/exchanges (verificado contra `infrastructure/mensajeria/rabbitmq.py`)

- Exchange: `cubicador.eventos` (topic).
- Routing keys: `cubicacion.solicitada`, `plan-de-cargue.generado`,
  `flota-de-cargue.generada` (S4-HU-12.3),
  `cubicacion.rechazada` y también `vehiculo.creado`/
  `vehiculo.actualizado`/`vehiculo.eliminado` (los 3 del catálogo: el último
  lo publica `eliminar_vehiculo`, y solo si el borrado se aplicó de verdad —
  ver `NEXO/docs/mensajeria/CUBICACION.md` §7.1).
- Dead-letter: `cubicacion.dlq`. Al caer un mensaje ahí (agotados los
  **3** reintentos, `worker/main.py:49` `MAX_INTENTOS = 3`), el worker
  además alerta por correo al equipo técnico
  (`infrastructure/alertas/correo.py::alertar_mensaje_en_dlq`, configurable
  vía `ALERTAS_SMTP_*`/`ALERTAS_EQUIPO_TECNICO`); sin SMTP configurado,
  degrada a un log sin romper el worker.

### 6.4 Vigilante de timeouts

Cubre el caso que el reintento por `x-intentos` **no** cubre: que el worker se
caiga a mitad de un mensaje, sin llegar ni a fallar ni a completar — no hay
excepción que atrapar ni ack/nack que emitir, así que el plan quedaría en
`procesando` para siempre.

Cada `CUBICADOR_INTERVALO_VIGILANTE_SEGUNDOS` (60 por defecto), el worker busca
los planes en `procesando` cuyo `updated_at` supere
`CUBICADOR_TIMEOUT_PROCESANDO_MINUTOS` (5 por defecto) y, por cada uno:

1. Publica `CubicaciónRechazada` con `codigo: "TIMEOUT_PROCESAMIENTO"`.
2. **Después** marca el plan `error`.

Ese orden es deliberado y es el punto de la HU. Marcar primero y publicar
después significaría que, con el broker caído, el plan sale de `procesando` sin
que nadie se entere y el ciclo siguiente ya no lo encuentra: NEXO se quedaría en
`procesando` indefinidamente. Publicando primero, un fallo del broker deja el
plan intacto y el próximo ciclo reintenta. Es el mismo criterio que el Outbox
transaccional de NEXO (publicar antes de persistir). El precio es un posible
rechazo duplicado si el proceso muere entre ambos pasos — inofensivo, porque el
consumer de NEXO se limita a volver a poner `Estado = "error"`.

`TIMEOUT_PROCESAMIENTO` es un `codigo` de mensajería, no de la API REST: no
aparece en el catálogo de la sección 4, igual que `ERROR_INTERNO`.

---

## 7. Lo que este servicio consume DE NEXO

```
GET {NEXO_BASE_URL}/api/lineas-solicitud/{id}/cubicacion
Authorization: Bearer <NEXO_CALLBACK_API_KEY>

200 → { "lineaId": "guid", "posiciones": [{ "descripcion"?, "cantidad", "largoCm", "anchoCm", "altoCm", "pesoKg"? }], "origen"?, "destino"? }
404 → este cliente solo mira el status code (ver infrastructure/clientes/nexo_client.py), no parsea el cuerpo.
```

Implementado del lado Cubicador en
`infrastructure/clientes/nexo_client.py` (timeout fijo de 10s, sin reintentos).

**Implementado del lado NEXO** (`LineasSolicitudCubicacionController` +
`ObtenerCubicacionLineaQuery` + `LineaSolicitudCubicacionAdapter`,
autenticación `CubicadorApiKeyAuthenticationHandler` — ver sección 5): lee
`LineaSolicitud` con su `Ruta`, deserializa `Dimensiones` (JSON ya
capturado por `CapturarSolicitudCommand` desde antes de este puente — no
hacía falta ningún cambio de captura) y devuelve `{ posiciones, origen,
destino }`. Cubierto por 6 tests de integración HTTP contra SQL Server real
(200 con posiciones/ruta, lista vacía, 404, 401 sin Bearer, 401 con Bearer
incorrecto, 401 con la sesión del portal en vez del Bearer de servicio).

**Verificado de punta a punta**, no solo por tests: `POST /desde-linea/{id}`
real contra ambos stacks en Docker (NEXO + Cubicador + RabbitMQ compartido) —
el worker llamó este endpoint (`GET .../cubicacion` → `200`), armó el plan
con el vehículo sugerido por el cubicador rápido, y lo optimizó (3/3
unidades cargadas, estables, sin incidencias). Único detalle de
infraestructura a tener en cuenta si armás un compose combinado: NEXO y
Cubicador tienen cada uno un servicio llamado `api` en su propio
`docker-compose.yml` — si el worker de Cubicador se conecta a las dos redes
a la vez, `NEXO_BASE_URL=http://api:8080` puede resolver al `api` EQUIVOCADO
(el propio del cubicador). Usar el nombre de contenedor explícito
(`http://nexo-api-1:8080`, o el que corresponda según `container_name`/
nombre de proyecto Compose) evita la ambigüedad.

---

## 8. Límites y comportamiento a tener en cuenta al integrar

- Dimensión máxima de un ítem: `30000mm` por lado (`application/plan_cargue/items.py:13`).
- Peso máximo de un ítem: `100000kg` (`items.py:15`).
- Cantidad por ítem: `1..2000` (`items.py:17-18`).
- Ítems por importación masiva: `1..2000` (`items.py:31-32`).
- No hay timeout sobre la ejecución del algoritmo de optimización en sí —
  para planes muy grandes, el polling de `GET /{id}/resultado` puede tardar
  sin que haya un límite duro que lo corte.
- `PATCH .../colocaciones/{id}` es la única operación de escritura que
  **nunca** pasa por cola — todo el resto de mutaciones sobre un plan
  (crear, optimizar, reoptimizar, crear-desde-línea) son asíncronas.

---

## 9. S4-HU-12 — qué tiene que hacer el backend (NEXO)

> **Destinatario: vasco-dev.** Este equipo solo construye dentro de
> `cubicador/`; todo lo que cambie en NEXO se documenta aquí y lo ejecuta el
> backend. Esta sección se escribe a medida que cada issue de la HU termina,
> así que crece: lo que está marcado **LISTO** ya está implementado de este
> lado y espera su contraparte; lo marcado **PENDIENTE** todavía no.

### 9.1 (12.1 · LISTO de este lado) El catálogo de vehículos cambió de forma

Hasta ahora había **dos** fuentes describiendo los mismos vehículos con campos
distintos: la tabla `vehiculos_cubicacion` (dimensiones, capacidad, tara,
puerta, ejes) y una constante en el código del motor rápido (clasificación
SICE/SAP). Ninguna alcanzaba sola. Se unificaron en la tabla, que ahora es la
única fuente.

**Tres consecuencias para NEXO, en orden de urgencia.**

#### a) El evento `VehiculoCreado`/`VehiculoActualizado` ya no cabe en el DTO actual

`VehiculoCubicacionDto` gana siete campos y relaja tres. El payload que viaja
en `{ correlationId, vehiculo: {...} }` queda así:

```jsonc
{
  "id": "uuid", "nombre": "Contenedor refrigerado 40", "descripcion": "...",
  "largoInternoMm": 11560, "anchoInternoMm": 2280, "altoInternoMm": 2250,
  "capacidadKg": 24700, "taraKg": 4800,

  "puertaAnchoMm": null,          // ← AHORA PUEDE SER NULL
  "puertaAltoMm": null,           // ← AHORA PUEDE SER NULL
  "puertaPosicion": "trasera",
  "ejes": [ { "posicionMm": null, "capacidadKg": 12350 } ],   // ← posicionMm PUEDE SER NULL

  "toleranciaParedesMm": 0, "toleranciaEntreUnidadesMm": 0, "esPreset": true,

  "codigo": "contenedor_refrigerado_40",   // ← NUEVO
  "configVehiculo": "3S3",                 // ← NUEVO
  "carroceria": "PORTACONTENEDORES",       // ← NUEVO
  "claseViaje": "Contenedor",              // ← NUEVO
  "claseContenedor": "CNT40.RE",           // ← NUEVO
  "geometria": "cerrada",                  // ← NUEVO
  "controlAmbiental": "refrigeracion"      // ← NUEVO
}
```

**Esto rompe hoy**, y está verificado contra el código de NEXO, no supuesto.
Los tres campos que pasan a admitir `null` están declarados como `decimal` no
anulable, que en C# es un tipo por valor: un `null` en el JSON revienta al
deserializar, antes de llegar a la base.

| Dónde | Qué dice hoy |
|---|---|
| `src/Nexo.Mensajes/Cubicacion/MensajesDeCubicacion.cs:74` | `record clsEjeVehiculoInput(decimal PosicionMm, decimal CapacidadKg)` |
| `src/Nexo.Mensajes/Cubicacion/MensajesDeCubicacion.cs:87` y `:259` | `decimal PuertaAnchoMm, decimal PuertaAltoMm` |
| `src/Nexo.Domain/PlanDeCargue/Entidades/clsPlanCargue.cs:29-30` | `public decimal PuertaAnchoMm { get; set; }` |

Hay que pasarlos a `decimal?` en los tres sitios, hacer anulables las columnas
correspondientes de `tbl_vehiculos_cubicacion` y agregar las siete nuevas. Los
ejes viajan serializados en `EjesJson` (`clsPlanCargue.cs:37`), así que ahí no
hay columna que alterar, pero sí el record que los parsea.

**Por qué hay nulos y no valores por defecto.** El reefer solo existía en la
constante del motor, que nunca guardó puerta ni posiciones de eje. Se decidió
dejarlos en `null` en vez de copiarlos del contenedor de 40 seco: sería un dato
inventado, y la puerta la usa el motor 3D para decidir si la carga entra.
`null` significa *desconocido*, no *cero*.

#### b) `configVehiculo` es el puente hacia `ConfiguracionVehiculo`

Es el código de `tblSAPTiposVehiculoTarifario`, y es el campo que permite
resolver o crear la `clsConfiguracionVehiculo` de una línea sin adivinarla.
Hasta ahora el Cubicador no lo exponía y el back no tenía de dónde sacarlo.

Dos correcciones que viajan en él y que **arreglan un fallo silencioso que ya
existe en producción**:

| Vehículo | Antes | Ahora | Por qué |
|---|---|---|---|
| Camión rígido dobletroque | `3` | `C3` | `3` **no existe** en `tblSAPTiposVehiculoTarifario`. Cuando `TipoVehiculo` no matchea ningún `CODIGO`, `IdVehiculoSapAsync` devuelve null y el costeo **deja de filtrar por vehículo, sin error ni log**. `C3` está activo y su descripción es literalmente "Dobletroque" |
| Contenedor 40 High Cube | mapeado al reefer | fila propia | Eran dos vehículos distintos emparejados por error |

#### c) El catálogo pasa de 9 a 10 vehículos

Al separar el High Cube seco del refrigerado. Cualquier prueba o pantalla que
asuma nueve deja de valer.

#### d) `POST /api/vehiculos` tiene que **mandar** la clasificación, no solo recibirla

Esto es lo inverso de (a): ahí el evento viaja de Cubicador hacia NEXO; aquí el
comando viaja de NEXO hacia Cubicador, y **también le faltan campos**.

El `GuardarVehiculoInput` de este servicio ya los acepta (todos opcionales, para
no romper a NEXO mientras se actualiza):

```jsonc
{
  "nombre": "...", "largoInternoMm": 12000, /* ...lo de siempre... */

  "puertaAnchoMm": null,      // ← ahora admite null
  "puertaAltoMm": null,       // ← ahora admite null
  "ejes": [ { "posicionMm": null, "capacidadKg": 8000 } ],

  "configVehiculo": "3S3",       // ← NEXO debe enviarlos
  "carroceria": "FURGON",
  "claseViaje": "Carga Suelta",
  "claseContenedor": null,
  "geometria": "cerrada",
  "controlAmbiental": "ninguno"
}
```

**Qué pasa si NEXO no los manda** — y pasa hoy: el vehículo se crea, aparece en
`GET /api/vehiculos`, pero **queda fuera de la cubicación rápida**. Sin
`geometria` ni `controlAmbiental` no se puede evaluar, y no se les supone un
valor por defecto a propósito: con carga refrigerada, suponer que el vehículo la
tiene y suponer que no son dos errores caros, y el primero no se nota hasta que
la mercancía llega dañada.

Para el usuario eso se ve como *"creé un camión y nunca me lo sugiere"*, sin
ninguna explicación. Por eso el mismo campo va también al contrato del frontend:
el formulario tiene que pedirlos. Mientras las dos partes no estén, conviene que
la interfaz avise que el vehículo quedará limitado.

**`codigo` NO se envía.** Es identidad de este servicio y se genera aquí — ver
el punto siguiente.

#### e) `codigo` viene siempre, también en los vehículos que crea el usuario

Es la identidad **estable** del vehículo: se genera una sola vez, al crearlo, a
partir del nombre (minúsculas, sin tildes, sin paréntesis, sin palabras vacías)
y **no se recalcula al renombrar**. Esa es justamente su razón de ser — el
nombre es editable desde la interfaz y no sirve como identidad.

Del lado de NEXO conviene usarlo como clave al proyectar el espejo y al
referirse a un vehículo desde cualquier otro sitio, en vez del nombre. Si dos
vehículos comparten nombre, el segundo recibe un sufijo (`camion_prestado_2`).

---

### 9.2 (12.2 · LISTO de este lado) La recomendación pasa a calcularse por peso

#### a) `pesoKg` deja de ser opcional — **es ruptura**

En `POST /api/cubicacion`, cada ítem tenía `pesoKg` opcional. Ahora es
obligatorio, porque de él sale cuántos camiones hacen falta.

Del lado de NEXO hay que cambiarlo en
`src/Nexo.Application/CasosDeUso/Cubicacion/DTOs/CubicacionRapidaDtos.cs:11`:

```csharp
public sealed record clsItemCubicacionRapidaDto(
    string? Descripcion, int Cantidad, decimal LargoCm, decimal AnchoCm, decimal AltoCm,
    decimal? PesoKg, string? TipoMercancia);   // ← PesoKg deja de ser decimal?
```

Conviene validarlo en NEXO y devolver un `400` propio en vez de dejar que
Cubicador responda un error de validación menos claro.

**Ojo con un caso que no pasa por ahí.** El puente `crear_desde_linea` arma los
ítems desde `PosicionCarga`, cuyo `pesoKg` **sí puede venir nulo** desde NEXO
(`GET /api/lineas-solicitud/{id}/cubicacion`). Ese camino no lo cubre la
validación del API. El motor ya lo rechazaba —veta por seguridad cuando falta
el peso— pero desde 12.2 además lo dice explícitamente:

> `3 item(s) sin peso: no se puede calcular cuantos vehiculos hacen falta.
> Captura el peso de ...`

Ese texto viaja en `advertencias`. Vale la pena que NEXO lo propague a la Mesa
de Costeo, porque hoy el usuario solo ve que no hay sugerencia.

#### b) La respuesta gana el reparto por peso

`clsSugerenciaFlotaDto` (`CubicacionRapidaDtos.cs:43`) necesita dos campos:

```csharp
public sealed record clsSugerenciaFlotaDto(
    string UnidadId, string UnidadNombre, int CantidadVehiculos, decimal UtilizacionPromedio,
    string? ConfigVehiculo, string? Carroceria, string? ClaseViaje, string? ClaseContenedor,
    IReadOnlyList<clsDisposicionDto> Disposiciones,
    decimal PesoTotalKg,          // ← NUEVO
    decimal PesoPorVehiculoKg);   // ← NUEVO: igual para todos los camiones
```

`PesoPorVehiculoKg` es **la respuesta de la HU**: la carga que le toca a cada
camión, repartida en partes iguales. Es el número que hay que mostrar y el que
alimenta la creación de líneas de 12.3.

#### c) Qué cambia en el comportamiento, para que no sorprenda

La cantidad de camiones ya no sale del empaque 3D. Sale del peso
—`techo(peso_total / capacidad)`— y **sube si la geometría exige más**: una
carga voluminosa y liviana puede caber de sobra en peso y necesitar tres
camiones por volumen. El peso fija el mínimo, no el número final.

Y el reparto dejó de ser voraz. Antes se llenaba el primer camión al tope y el
último quedaba con lo que sobrara; ahora todos llevan lo mismo:

| 18.000 kg en camiones de 10.000 | Antes | Ahora |
|---|---|---|
| Reparto | 10.000 + 8.000 | 9.000 + 9.000 |

Eso **cambia el precio**: el costeo elige tarifa por banda de peso
(`peso >= RangoPesoInf && peso <= RangoPesoSup`), y dos camiones de 9.000 pueden
caer en una banda distinta que uno de 10.000 y otro de 8.000.

### 9.3 (12.3 · LISTO de este lado) Evento de flota, y las N líneas de solicitud

Esta es la issue donde **la mayor parte del trabajo es tuyo**. De este lado solo
se podía crear la flota y contarla: `lineas_solicitud` vive en el SQL Server de
NEXO y el puerto de este servicio es de solo lectura por diseño (§7).

#### a) Evento nuevo: `flota-de-cargue.generada`

Routing key `flota-de-cargue.generada`, exchange `cubicador.eventos`, con el
mismo sobre de MassTransit que los demás (`messageType`
`...Cubicacion:FlotaDeCargueGenerada`). Se publica al terminar
`crear_desde_linea`:

```jsonc
{
  "correlationId": "<lineaSolicitudId>",
  "lineaSolicitudId": "aaaaaaaa-1111-2222-3333-444444444444",
  "cantidadVehiculos": 2,
  "pesoTotalKg": 30000.0,

  // Va UNA vez, no por camión: la recomendación son N camiones del MISMO tipo.
  // Repetirlo N veces solo invita a que diverjan.
  "vehiculo": {
    "codigo": "contenedor_seco_40",
    "nombre": "Contenedor 40 pies",
    "capacidadKg": 26680.0,
    "configVehiculo": "3S3",          // ← con esto resuelves la ConfiguracionVehiculo
    "carroceria": "PORTACONTENEDORES",
    "claseViaje": "Contenedor",
    "claseContenedor": "CNT40.HC"
  },

  "camiones": [
    { "numeroVehiculo": 1, "planCargueId": "e1bf...", "pesoAsignadoKg": 15000.0 },
    { "numeroVehiculo": 2, "planCargueId": "b6f9...", "pesoAsignadoKg": 15000.0 }
  ]
}
```

**Se publica ADEMÁS de `plan-de-cargue.generado`**, que se sigue emitiendo para
el primer plan. Quitarlo dejaría sin datos a la proyección que la Mesa de Costeo
ya consulta con `GET /api/planes-cargue/por-linea/{id}`, y eso sí sería una
regresión visible hoy.

#### b) Lo que hay que construir en NEXO

1. **Consumer de `FlotaDeCargueGenerada`.** Mismo patrón que
   `clsPlanDeCargueGeneradoConsumer`: `IConsumer<T>` real, hereda reintentos,
   DLQ y alerta.
2. **Una línea de solicitud por camión.** Según lo acordado con negocio el
   28-sep: la **línea original se reusa como camión 1** —conserva su Id, se le
   ajusta el peso a `pesoAsignadoKg`— y se crean las N-1 restantes. No se borra
   ni se deja en cero: conservar el Id es lo que evita romper la línea de
   cotización que ya la referencia y la auditoría.
3. **`ConfigVehiculoId` de cada línea**, resolviendo o creando la
   `clsConfiguracionVehiculo` con `vehiculo.configVehiculo` y
   `vehiculo.carroceria`. **Antes de esto hay que corregir `ClaveVehiculo`**
   (§9.1), que compara sin quitar tildes: si no, cada alta ensucia la tabla con
   duplicados.
4. **Una línea de cotización por línea de solicitud**, poblando
   `clsCotizacionLinea.LineaSolicitudId`. El campo ya existe y es anulable: no
   hace falta cambio de esquema.

#### c) Tres efectos que conviene medir antes de soltarlo

Partir una línea en N toca a tres consumidores del conteo de líneas, y nadie lo
ha medido: el **puntaje de la Bandeja Priorizada**, los **contadores de la saga
de auto-costeo** y lo que llega a **Tarifas Ganadas**. Una solicitud de 10
líneas puede pasar a tener 25.

#### d) Por qué el peso de cada camión importa tanto

`pesoAsignadoKg` es lo que se escribe en cada línea, y el costeo elige tarifa
por banda de peso (`peso >= RangoPesoInf && peso <= RangoPesoSup`). El reparto
ahora es **equilibrado**: dos camiones de 15.000 en vez de uno de 20.000 y otro
de 10.000. Eso puede cambiar la banda, y por lo tanto el precio.

Se corrigió en esta issue: la cascada de `crear_desde_linea` seguía siendo voraz
aunque 12.2 ya había equilibrado el cubicador rápido, así que el desbalance se
habría propagado hasta la cotización.

---

## 10. S4-HU-12 — qué tiene que hacer el frontend

> **Destinatario: juanpablo.** El frontend **no habla con este servicio** — eso
> se cerró (§1.2). Todo lo de abajo le llega a través de NEXO, así que la
> contraparte de vasco-dev (§9) tiene que existir primero o estos campos no
> viajan.
>
> Cuando se implemente, el cambio debe reflejarse además en
> `NEXO/docs/contrato-integracion/`, que es el contrato versionado del portal.
> Su propia regla dice que *"un cambio que rompa a quien ya consume sube la
> versión mayor y se anuncia antes de implementarse"* — y varios de estos
> rompen.
>
> Igual que §9, esta sección crece a medida que cada issue termina.

### 10.1 (12.1) El catálogo de vehículos

#### a) La interfaz `VehiculoCubicacion` gana campos y relaja tres

En `src/app/cabina-fletes/models/cubicador.models.ts`:

```ts
export interface VehiculoCubicacion {
  // ...lo de siempre...
  puertaAnchoMm: number | null;   // ← antes number
  puertaAltoMm:  number | null;   // ← antes number

  codigo: string | null;           // ← NUEVO, ver (c)
  configVehiculo: string | null;   // ← NUEVO
  carroceria: string | null;       // ← NUEVO
  claseViaje: string | null;       // ← NUEVO
  claseContenedor: string | null;  // ← NUEVO
  geometria: string | null;        // ← NUEVO: cerrada | semicerrada | abierta
  controlAmbiental: string | null; // ← NUEVO: ninguno | refrigeracion
}

export interface EjeVehiculoDto {
  posicionMm: number | null;       // ← antes number
  capacidadKg: number;
}
```

**Donde se muestren, `null` no es `0`.** Un vehículo sin puerta medida no es un
vehículo con una puerta de cero milímetros. Conviene pintar "sin dato" o dejar
la celda vacía; mostrar `0` haría creer que no entra nada por ahí.

Hoy hay un vehículo así: el **contenedor refrigerado de 40**, que solo existía
en el catálogo interno del motor y del que nunca se midieron la puerta ni las
posiciones de eje. No se copiaron las del contenedor seco a propósito.

#### b) El editor de vehículos debe pedir la clasificación

`cubicador-page.component.ts` (`vehiculoEdit`, `guardarVehiculo()`) crea y edita
vehículos con los campos físicos nada más. Hay que agregar al menos
`geometria` y `controlAmbiental`, y preferiblemente también `configVehiculo`,
`carroceria`, `claseViaje` y `claseContenedor`.

**Qué pasa si no se agregan** — y pasa hoy: el vehículo se crea y aparece en la
lista, **pero nunca se ofrece en la cubicación**. Sin saber si lleva
refrigeración no se puede evaluar, y no se le supone un valor por defecto:
suponer que la tiene y suponer que no son dos errores caros, y uno de ellos no
se nota hasta que la mercancía llega dañada.

Para el usuario eso se ve como *"creé un camión y el sistema nunca me lo
sugiere"*, sin ninguna explicación. Mientras el formulario no pida esos campos,
vale la pena que la pantalla avise que el vehículo quedará limitado.

#### c) `codigo` es de solo lectura

Es la identidad estable del vehículo: se genera al crearlo, a partir del nombre,
y **no cambia al renombrarlo**. Se puede mostrar, no se debe editar ni enviar en
`POST /api/vehiculos`.

El detalle que importa para la interfaz: **renombrar un vehículo ya no rompe
nada**. Antes el sistema lo buscaba por nombre, así que renombrar un preset
dejaba el plan de cargue de una línea atascado sin explicación. Si hay algún
aviso o restricción en la UI puesta por ese motivo, ya no hace falta.

#### d) El catálogo pasa de 9 a 10 vehículos

Se separó el **Contenedor 40 pies High Cube** (seco) del **Contenedor
refrigerado 40**, que hasta ahora eran el mismo registro por error. Cualquier
selector, conteo o prueba que asuma nueve deja de valer — por ejemplo el
`vehiculosDisponibles` de `cubicador-page.component.ts:57`.

### 10.2 (12.2) La recomendación por peso

#### a) El peso pasa a ser obligatorio en el formulario — **es ruptura**

`ItemCubicacionRequest` (`captura-api.models.ts`) tiene `pesoKg` opcional. Pasa
a obligatorio, y la interfaz debería exigirlo antes de dejar cubicar: sin peso
no hay forma de decir cuántos camiones hacen falta.

Es el cambio que obliga a subir versión mayor en
`docs/contrato-integracion/` y a anunciarlo antes de implementarlo, según la
regla del propio contrato.

#### b) Mostrar el reparto, que es la respuesta que el usuario espera

`SugerenciaFlotaDto` (`captura-api.models.ts:164`) gana dos campos:

```ts
export interface SugerenciaFlotaDto {
  // ...lo de siempre...
  pesoTotalKg: number;        // ← NUEVO
  pesoPorVehiculoKg: number;  // ← NUEVO: el mismo para todos los camiones
}
```

Con `cantidadVehiculos` —que la interfaz ya recibe— alcanza para pintar la
frase completa: *"2 camiones, 9.000 kg cada uno"*. Hoy solo se muestra el
vehículo sugerido, sin decir cuánto lleva cada uno.

#### c) Dos comportamientos que van a cambiar en pantalla

**La cantidad de camiones puede subir.** Antes salía del empaque 3D; ahora sale
del peso, y sube si la geometría exige más. Una carga liviana y voluminosa que
antes mostraba un vehículo ahora puede mostrar tres.

**El reparto deja de estar desbalanceado.** Antes el primer camión iba al tope y
el último con lo que sobrara (10.000 + 8.000); ahora todos llevan lo mismo
(9.000 + 9.000). Si hay alguna pantalla que muestre las disposiciones por
vehículo, los números van a verse distintos aunque la carga sea la misma.

#### d) Un aviso nuevo que conviene mostrar

Cuando algún ítem llega sin peso, la respuesta trae en `advertencias`:

> `3 item(s) sin peso: no se puede calcular cuantos vehiculos hacen falta.
> Captura el peso de ...`

Pasa sobre todo en la Mesa de Costeo, donde los ítems salen de las posiciones
capturadas y el peso puede faltar. Hoy el usuario solo ve que no hay sugerencia,
sin saber que el problema es un dato que falta y que puede completar él mismo.

### 10.3 (12.3) Una línea por camión, y lo que eso cambia en pantalla

Depende por completo de que exista la parte de vasco-dev (§9.3): sin el consumer
que crea las N líneas, la Mesa de Costeo sigue viendo lo de hoy.

#### a) La Mesa de Costeo verá N camiones donde hoy ve uno

`porLinea()` devolvía siempre un solo vehículo — no porque el front lo limitara,
sino porque el Cubicador reportaba uno solo. Eso ya está corregido. El comentario
de `cubicador-api.service.ts` que dice *"Solo reporta el primer vehículo de una
flota multi-vehículo: limitación del motor Cubicador"* **deja de ser cierto** y
conviene quitarlo para que nadie programe alrededor de él.

#### b) Una línea de la Bandeja puede volverse varias

Es el cambio más visible y no es cosmético: una solicitud de 10 líneas puede
pasar a tener 25. Vale revisar los sitios donde se cuentan o se listan líneas, y
cómo se muestra que varias son el mismo despacho repartido —`numeroVehiculo`
viaja en la proyección para eso.

#### c) El sondeo sin límite de `porLinea` se vuelve más peligroso

`mesa-costeo-page.component.ts` sondea cada 3 segundos **sin límite de
intentos**: solo para cuando aparece una flota. Si el plan se rechaza, gira para
siempre y el usuario ve un spinner eterno sin saber que falló.

Con N camiones eso empeora, porque hay más formas de que el proceso se
interrumpa a mitad. Dos mitigaciones que conviene meter juntas: un tope de
intentos con mensaje de error, y consumir el aviso de rechazo que vasco-dev
tiene que exponer por línea (§9.1 del contrato del back lo menciona como
pendiente de esa parte).

#### d) Nada de esto exige cambios de modelo en el front

`PlanFlotaItem` ya trae `numeroVehiculo` y `totalVehiculos`: el front **ya modela
flotas**, solo que nunca recibía más de un elemento. Lo que cambia es la
cantidad de datos que llegan, no su forma.
