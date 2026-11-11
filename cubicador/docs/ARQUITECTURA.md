# Arquitectura

Traduce el mismo patrón de 4 capas que ya usa NEXO (`Api` / `Application` /
`Domain` / `Infrastructure`) a idioms de Python. La regla de dependencias es
la misma: las capas internas (Domain, Application) no conocen nada de las
externas (Infrastructure, Api) — se comunican por interfaces (en Python,
`typing.Protocol`), igual que en NEXO se comunican por `Port`.

```
cubicador/
  domain/
    motor/
      dominio.py            # tipos puros: Contenedor, ItemCarga, Orientacion, Colocacion...
      validadores.py         # colision, soporte, apilamiento, puerta, balance de ejes...
      optimizadores.py       # Optimizador (Protocol) + registro por nombre:
                              # FirstFitDecreasing, BestFitDecreasing y
                              # DeepestBottomLeftFill, que es el oraculo de
                              # empaque de la seleccion de flota.
      puntuacion.py          # scoring 0..100 por estrategia
      motor.py               # MotorCubicacion3D: orquesta optimizar/validar/metricas
      seleccion_flota.py      # motor de seleccion de flota: filtra
                              # candidatos, empaca con DBLF y elige la flota
                              # minima, que puede ser MIXTA. Decide por
                              # max(%volumen, %peso) y reporta los dos.
      # PURO: sin I/O, sin FastAPI, sin SQLAlchemy, sin aio-pika. Portado tal
      # cual de cabinaFletes (via el paso intermedio en NEXO). Ver
      # testDocumentation/REGLAS_DE_NEGOCIO.md: estas 31 reglas NO se pueden romper al traducir
      # (verificadas con pytest en tests/domain/).
    entidades/
      entidades.py            # VehiculoCubicacion, PlanCargue, ItemPlanCargue,
                               # ColocacionCargue, ParametrosCubicacion - dataclasses
                               # puras, sin ORM.
      catalogo.py              # Catalogo, Vehiculo, Carroceria, TipoMercancia,
                               # ReglaIncompatibilidad, ParametrosMotor: el catalogo
                               # de la seleccion de flota, ya resuelto en memoria.
                               # Dataclasses frozen.
      recomendacion.py         # RecomendacionGuardada y sus vehiculos y mercancias:
                               # una corrida tal como se guarda y se reconsulta.
  application/
    excepciones.py            # AppException y subtipos (equivalente a NEXO)
    recomendaciones/
      casos_de_uso.py          # recomendar_flota: resuelve las mercancias contra el
                                # catalogo de base y corre el motor. Es la unica pieza
                                # que toca el puerto y el dominio a la vez.
                                # crear_recomendacion agrega la persistencia append-only
                                # y la reconsulta.
    explicacion/                # el texto de GET /recomendaciones/explicacion: lo pide
                                # al modelo y lo descarta si trae una cifra que no es
                                # del motor
    ia_sombra/                  # modo sombra: compara en segundo plano la flota del
                                # motor con la que propone el modelo (apagado por defecto)
    parametros/
      casos_de_uso.py           # medidas estandar (topes de extradimension)
    catalogo/
      casos_de_uso.py            # CRUD de los seis catalogos: las
                                  # validaciones que la base no puede hacer sola, y la
                                  # traduccion de las que si hace a 400/409 con mensaje
    plan_cargue/
      mapeos.py                # traduce entidades <-> tipos del motor (clave "id#indice")
      vehiculos.py              # guardar/eliminar/listar vehiculos + presets
      planes.py                 # crear/obtener/listar/eliminar plan, flota
      items.py                  # guardar/importar/eliminar item
      optimizar.py               # optimizar_plan (llama al motor + persiste)
      ajustar.py                 # ajustar_colocacion (valida en memoria + persiste)
      demo.py                    # crear_plan_demo
      desde_linea.py              # crear_plan_desde_linea (cascada de flota, llama a NEXO)
      resultado.py                 # obtener_resultado (arma plan+vehiculo+colocaciones+metricas)
    ports/
      repositorios.py           # Protocols: IVehiculoCubicacionRepository, IPlanCargueRepository,
                                 # ICatalogoRepository (solo lectura, un metodo: cargar())...
      externos.py                # Protocols: ILineaSolicitudPort (NEXO), IEventosPort (RabbitMQ)
      ia.py                       # Protocol: IClienteIa (proveedor del modelo)
  infrastructure/
    persistence/
      db.py                     # engine/sesion SQLAlchemy async (esquema de CUBICADOR_DB_ESQUEMA)
      modelos.py                 # las 5 tablas del motor 3D y iaSombra - DDL en database/esquema/ESQUEMA_CUBICADOR.sql
      modelos_catalogo.py         # las 10 tablas del catalogo y de las recomendaciones,
                                   # con MetaData PROPIO. El Cubicador lee y escribe sus
                                   # datos, pero no crea ni altera su estructura.
      repositorios/               # implementan los Ports de persistencia
        catalogo_repo.py           # SqlCatalogoRepository: lee el catalogo entero
        recomendacion_repo.py       # append-only: solo INSERT y SELECT, ni un UPDATE
        catalogo_crud_repo.py        # escritura de los seis catalogos, con la version
        plan_cargue_repo.py, vehiculo_repo.py, parametros_repo.py, ia_sombra_repo.py
    mensajeria/
      rabbitmq.py                 # implementa IEventosPort con aio-pika
    clientes/
      nexo_client.py               # implementa ILineaSolicitudPort (httpx + Bearer)
    ia/
      anthropic_cliente.py          # implementa IClienteIa
    alertas/
      correo.py                     # correo al equipo cuando un mensaje cae en cubicacion.dlq
  api/
    dependencias.py               # inyeccion de dependencias FastAPI (equivalente a Program.cs)
    dtos/
      comun.py                     # ApiModel base (camelCase)
      recomendaciones.py            # DTOs de /recomendaciones y de las medidas estandar
      catalogo.py                    # DTOs de los seis catalogos, lectura y escritura
      plan_cargue.py                  # DTOs de /api/planes-cargue
    routers/
      recomendaciones.py             # POST /recomendaciones, reconsulta y explicacion
      catalogo.py                     # catalogos publicos + los seis CRUD + medidas
      planes_cargue.py                 # FastAPI router: /api/planes-cargue
    main.py                          # app FastAPI, manejo de excepciones, healthcheck
  worker/
    main.py                          # consumidor RabbitMQ: los 11 tipos de comando + vigilante de timeouts
tests/
  domain/                            # motor 3D (las 31 reglas de testDocumentation/REGLAS_DE_NEGOCIO.md) y seleccion de flota
  paridad/                           # el motor Python contra la salida del motor C# en los 26 casos dorados
  api/
    test_smoke.py                    # flujo end-to-end contra SQLite
scripts/
  aplicar_sql.py                      # aplica scripts .sql (corta por GO); el esquema vigente es database/esquema/ESQUEMA_CUBICADOR.sql
  verificar_mapeo_catalogo.py          # compara modelos_catalogo.py contra sys.columns
                                        # del esquema REAL. Las pruebas corren contra
                                        # SQLite y no pueden ver esa divergencia.
  generar_openapi.py                    # regenera docs/openapi.json (--verificar para CI)
  verificar_contrato.py                  # los criterios del contrato contra el servicio
                                          # corriendo, por HTTP: campos obligatorios,
                                          # origen verificado, append-only, 200 vs error
  verificar_crud_catalogos.py             # los seis CRUD contra el servicio corriendo.
                                          # ESCRIBE: se niega si el servicio no es local
  preparar_catalogo_local.py              # crea y siembra el catalogo en la base LOCAL
                                          # (se niega a correr contra un host remoto;
                                          # --recrear lo rehace cuando cambia el modelo)
  verificar_integracion_mssql.py        # humo end-to-end contra SQL Server real
  verificar_aislamiento.sql              # confirma que no tocamos dbo ni dbo_cotifly
database/esquema/ESQUEMA_CUBICADOR.sql  # DDL único (16 tablas: catálogo, recomendación y motor 3D), se aplica a mano
database/seeder/                        # CSV del catálogo, cargados por database/scripts/ejecutar_schema_y_seeders.py
docker-compose.yml                    # api + worker + SQL Server local (RabbitMQ es el compartido de NEXO, ver DECISIONES_TECNICAS.md #9)
```

## Por qué `domain/motor` es la pieza más importante

Es la única carpeta que tiene que ser un calco de comportamiento exacto del
original C# (`cabinaFletes/src/CabinaFletes.Api/Services/Cubicacion3D/*` y
`CubicacionService.cs`). Todo lo demás (`application/`, `infrastructure/`,
`api/`) es organización nueva siguiendo el patrón de NEXO — el original en
cabinaFletes no separaba así, era todo `Api/Services` con EF Core mezclado.

Mapeo de archivos origen → destino:

| Origen (cabinaFletes / NEXO) | Destino Python |
|---|---|
| `Dominio.cs` | `domain/motor/dominio.py` |
| `Validadores.cs` | `domain/motor/validadores.py` |
| `Optimizadores.cs` (`IOptimizadorCubicacion`, `FirstFitDecreasing`, `BestFitDecreasing`) | `domain/motor/optimizadores.py` |
| `Puntuacion.cs` | `domain/motor/puntuacion.py` |
| `MotorCubicacion3D.cs` | `domain/motor/motor.py` |
| `CubicacionService.cs` (evaluador SICCE) | **retirado**. Lo reemplaza `domain/motor/seleccion_flota.py`, que lee el catalogo de base |
| `PlanCargueService.cs` | se reparte entre `application/plan_cargue/*.py` (orquestación) e `infrastructure/persistence/repositorios/plan_cargue_repo.py` (EF→SQLAlchemy) |
| `Application/Cubicacion3D/Dtos/*.cs` de NEXO | `api/dtos/*.py` (Pydantic) — ver ../CONTRATO.md |
| `Application/Cubicacion3D/Ports/*.cs` de NEXO | `application/ports/*.py` (Protocols) |

**Nota de traducción**: el original usa `decimal` en todas partes; aquí se
usa `float` (idiomático en Python para cálculo geométrico). El algoritmo ya
redondea explícitamente (`round(...)`) en los puntos donde una división
podría introducir imprecisión — no se pierde nada, y las 31 reglas de
`testDocumentation/REGLAS_DE_NEGOCIO.md` lo confirman pasando sin ajustes.

## Proceso API vs. proceso Worker

Dos entrypoints sobre el mismo código de `application/`/`domain/`/`infrastructure/`:

- **`api/main.py`** — FastAPI. Atiende `/recomendaciones`, los catálogos y `/api/planes-cargue`. De esta última, `optimizar`, `reoptimizar` y `desde-linea` no calculan aquí: publican el comando en RabbitMQ y responden `202`; el resto de rutas es síncrono.
- **`worker/main.py`** — consume `cubicacion.solicitada` (los 11 tipos de comando que publica NEXO, más los que publica la propia API), ejecuta el caso de uso con el mismo código de `application/plan_cargue/` y publica el evento de resultado o de rechazo. También corre el vigilante de timeouts. **Verificado contra RabbitMQ real** en Docker Compose: mensaje publicado → worker lo consume → optimiza contra la base → publica `plan-de-cargue.generado` → plan queda persistido como `optimizado`.

Ambos se escalan de forma independiente (más réplicas del worker si hay cola acumulada, sin tocar cuántas instancias de la API atienden lecturas).
