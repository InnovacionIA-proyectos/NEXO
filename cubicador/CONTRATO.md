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
`POST /recomendaciones` (selección de flota — sección 3.1).
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
cola — era la única de las 20 sin plan ni Id que persistir. **Ese endpoint se
retiró en S5-HU-17.4**; lo reemplaza `POST /recomendaciones`, que sí persiste
su corrida (sección 3.1). Inventario 1 a 1
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
  rápido, hoy `/recomendaciones`, trabaja en **centímetros** — son dos sistemas
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

### 3.1 `/recomendaciones` — selección de flota (S5-HU-17.4)

**Reemplaza a `POST /api/cubicacion`, que se retiró en el mismo corte.** No hay
endpoint de transición: NEXO y Cotifly apuntan acá desde el release que lo publica.

La especificación completa —request, las tres formas de respuesta, la tabla de
campos obligatorios y los códigos de error— está en la **sección 2 de
[`docs/HOJA-DE-RUTA-CUBICADOR.md`](./docs/HOJA-DE-RUTA-CUBICADOR.md)**. Acá va lo
operativo.

| Método | Ruta | Body | Respuesta |
|---|---|---|---|
| POST | `/recomendaciones` | `CrearRecomendacionDto` | `RecomendacionDto` |
| GET | `/recomendaciones?idLineaSolicitud=&origenSistema=` | — | `RecomendacionDto` |
| GET | `/recomendaciones/{idRecomendacionVehiculo}/mercancias` | — | `MercanciaDeVehiculoDto[]` |
| GET | `/tipos-mercancia` | — | `TipoMercanciaDto[]` |
| GET | `/tipos-carroceria` | — | `TipoCarroceriaDto[]` |
| GET | `/medidas-estandar` | — | `MedidasEstandarDto` |
| PUT | `/medidas-estandar` | `MedidasEstandarDto` | `MedidasEstandarDto` |

`/medidas-estandar` vivía bajo `/api/cubicacion`: se mudó sin cambios de
comportamiento. Sigue siendo la única forma de editar los topes contra los que se
detecta extradimensión, que el contrato devuelve en el bloque `extradimension`.

Límite: **10.000 unidades totales por request**
(`application/recomendaciones/casos_de_uso.py`) — por encima,
`400 CUBICACION_DEMASIADO_GRANDE`.

**Las mercancías viajan con id y código.** En el request va el `idTipoMercancia`
que devolvió `GET /tipos-mercancia` (acordado con Cotifly el 2026-10-08, su
HU-021.5). En la respuesta van **los dos**: el id es lo que Cotifly persiste y el
código estable es lo que usa NEXO. Un uuid se acepta en cualquier caja —
mayúsculas o minúsculas— porque es el mismo uuid.

**Los ids que devuelve este servicio vienen en MAYÚSCULAS.** SQL Server
devuelve así las columnas `uniqueidentifier` y no es configurable; se generan ya
en mayúsculas para que el id del POST y el de la reconsulta sean el mismo texto.

**Lo que antes era error ahora es resultado.** `SIN_SELECCION_AUTOMATICA`,
`condicionada` y `extradimension` vuelven con **200**, dentro de `resultado`. Los
códigos de error quedan solo para fallos reales: `MERCANCIAS_REQUERIDAS`,
`MERCANCIA_DESCONOCIDA`, `MERCANCIA_NO_CUBICABLE`, `DIMENSIONES_INVALIDAS` (400),
`NO_AUTORIZADO` (401) y `CUBICADOR_NO_DISPONIBLE` (503).

Valores de `motivoSinSeleccion`: `EXTRADIMENSIONADO`,
`SIN_CANDIDATOS_DIMENSIONALES`, `SUPERA_K_MAX`, `SIN_CARROCERIA_COMPATIBLE` y
`BUSQUEDA_AGOTADA` — esta última es una condición del algoritmo, no del
negocio: el buscador se quedó sin intentos de empaque antes de encontrar una
flota, que no es lo mismo que no caber en `k_max` vehículos.

**La reconsulta exige `origenSistema` en la query**, no lo toma de la llave: un
sistema puede necesitar leer lo que recomendó el otro, y un mismo código de línea
puede existir en los dos. Devuelve la **última** corrida de esa línea.

**Append-only**: recalcular una ruta crea una corrida nueva. La anterior queda
intacta, y el `idRecomendacionVehiculo` que un consumidor guardó sigue
devolviendo lo mismo.

DTOs reales en `api/dtos/recomendaciones.py` y `api/dtos/catalogo.py`; el esquema
completo, en `docs/openapi.json` (lo regenera `scripts/generar_openapi.py`).

### 3.2 Los seis CRUD de catálogo (S5-HU-17.5)

Viven en el Cubicador, que es donde está el dominio. **El portal nunca llama
acá directo**: no hay CORS, la llave terminaría en el bundle y el rol no se
podría aplicar, porque este Bearer autoriza un backend y no a una persona. Entre
el formulario y este servicio hay un passthrough en NEXO que espeja estas rutas
y aplica el rol (`AdminTarifas,Admin` para escribir, `[Authorize]` a secas para
leer).

| Catálogo | Rutas | Qué administra |
|---|---|---|
| Parámetros del motor | `GET /parametros-motor`<br>`PUT /parametros-motor/{clave}` | `k_max`, `umbral_alerta_llenado`, `usar_alternativa`, `factor_vol_util`, `margen_cg` |
| Categorías de mercancía | `GET` `POST` `PUT/{id}` `DELETE/{id}` en `/categorias-mercancia` | Las familias que agrupan las mercancías y sobre las que se declaran las incompatibilidades |
| Tipos de carrocería | ídem en `/tipos-carroceria` | El catálogo de carrocerías y qué vehículos las admiten |
| Tipos de mercancía | ídem en `/tipos-mercancia` | Qué se transporta, con qué carrocería y bajo qué condiciones |
| Vehículos del catálogo | ídem en `/vehiculos-catalogo` | Las configuraciones con sus medidas, capacidad y carrocerías |
| Reglas de incompatibilidad | `GET` `POST` `DELETE/{id}` en `/reglas-incompatibilidad`, **sin `PUT`** | Qué pares de categorías no pueden viajar juntos |

> `/vehiculos-catalogo` **no es** `/api/planes-cargue/vehiculos`. Son dos tablas
> distintas: la primera es el catálogo que alimenta al motor de selección de
> flota, la segunda es el catálogo propio del motor 3D.

**Concurrencia optimista.** Todo `GET` devuelve `version` y todo `PUT`/`DELETE`
la exige en el cuerpo. Si la fila cambió desde que el formulario la cargó, el
servicio responde **409 `CONFLICTO_DE_VERSION`** en vez de pisar el cambio
ajeno. En un catálogo que alimenta al motor eso no se manifestaría como error
sino como recomendaciones sutilmente equivocadas.

`relacionVehiculosCarroceria` no tiene versión propia: viaja dentro del payload
del vehículo, y es la versión del **vehículo** la que se exige y se sube, aunque
el cambio solo toque las relaciones.

**`usuarioId` es obligatorio en toda escritura.** El Cubicador no tiene
usuarios: NEXO manda el id y acá queda en `CreadoPor` como dato declarado, no
como identidad comprobada.

**Errores.** Mismo formato `{ "codigo": "...", "mensaje": "..." }` de la sección
4:

| HTTP | Código | Cuándo |
|---|---|---|
| 400 | `CARROCERIA_SECUNDARIA_IGUAL`, `CONDICIONADA_SIN_SECUNDARIA`, `SIN_CARROCERIAS`, `CAPACIDAD_ESPECIFICA_INVALIDA`, `DOMINANCIA_ROTA`, `CATEGORIAS_IGUALES`, … | Una validación de negocio |
| 404 | `NO_ENCONTRADO`, `PARAMETRO_NO_ENCONTRADO` | El id o la clave no existen |
| 409 | `CONFLICTO_DE_VERSION` | La fila cambió desde que se leyó |
| 409 | `CATALOGO_REFERENCIADO` | Hay filas que la referencian; el mensaje dice cuántas y de dónde |
| 409 | `REGLA_DUPLICADA` | Ya existe esa regla, en un sentido o en el otro |
| 409 | `CONFLICTO_DE_DATOS` | Un `UNIQUE`, un `CHECK` o una llave foránea de la base |

**Eventos.** Las escrituras de **mercancías, carrocerías y vehículos** publican
su evento para que la proyección de NEXO no quede vieja. Categorías, parámetros
y reglas no los publican: NEXO no los proyecta, y serían mensajes que ninguna
cola consume.

El evento lleva **la fila entera**, con la misma forma que devuelve el `GET`:
NEXO hace upsert por Id, así que un payload parcial le dejaría los demás campos
vacíos.

Para reparar una desincronización hay un endpoint por catálogo proyectado:

| | |
|---|---|
| `POST /tipos-carroceria/republicar-catalogo` | Reemite el evento de creación para **todas** las filas |
| `POST /tipos-mercancia/republicar-catalogo` | ídem |
| `POST /vehiculos-catalogo/republicar-catalogo` | ídem |

Seguro de correr más de una vez —el consumer hace upsert por Id— y no es parte
de ningún flujo automático: se invoca a mano.

### 3.3 `/api/planes-cargue` — motor 3D (con persistencia)

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

### 3.4 Catálogo de `codigo_rechazo` en `ColocacionCargueDto`

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

- **Una llave POR SISTEMA para `/recomendaciones`** (S5-HU-17.4):
  `CUBICADOR_API_KEY_NEXO` y `CUBICADOR_API_KEY_COTIFLY`
  (`api/dependencias.py:verificar_origen_sistema`). De ahí sale el
  `origenSistema` **verificado**, en vez de creerle al cuerpo de la petición:
  quien tiene la llave de Cotifly es Cotifly. Si el cuerpo declara un
  `origenSistema` que no corresponde a la llave, **401**. La de Cotifly la
  genera Cotifly y solo tiene que ser distinta de la de NEXO.
- Consumidores server-to-server → el resto del servicio
  (`/api/planes-cargue`, catálogos, medidas estándar): valida contra
  `CUBICADOR_API_KEY`, o contra cualquiera de las dos de arriba
  (`api/dependencias.py:verificar_api_key`). NEXO guarda este valor como
  `Cubicador3D__OutboundApiKey`. Los otros consumidores son CI/scripts de
  verificación manual; anteriormente también la usaba el navegador vía CORS —
  ya cerrado, y la clave fue rotada porque ese valor estuvo expuesto en el
  bundle público del portal.
- **La llave genérica NO sirve para `/recomendaciones`**: identifica al
  servicio, no a un sistema, y una recomendación tiene que saber de quién
  viene para poder reconsultarla después.
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
