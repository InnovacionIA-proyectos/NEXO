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
                              # DeepestBottomLeftFill (S5-HU-17.3), que es el
                              # oraculo de empaque de la seleccion de flota.
      puntuacion.py          # scoring 0..100 por estrategia
      motor.py               # MotorCubicacion3D: orquesta optimizar/validar/metricas
      cubicacion_rapida.py   # evaluador SICCE (7 criterios + heuristica de estanterias).
                              # LO REEMPLAZA seleccion_flota.py; se retira junto
                              # con POST /api/cubicacion en S5-HU-17.4.
      seleccion_flota.py      # motor de seleccion de flota (S5-HU-17.3): filtra
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
                               # ReglaIncompatibilidad, ParametrosMotor (S5-HU-17.2):
                               # el catalogo que administra el equipo de Datos, ya
                               # resuelto en memoria. Dataclasses frozen.
  application/
    excepciones.py            # AppException y subtipos (equivalente a NEXO)
    cubicacion_rapida/
      casos_de_uso.py         # evaluar_cubicacion, catalogo_unidades, medidas_estandar
    recomendaciones/
      casos_de_uso.py          # recomendar_flota (S5-HU-17.3): resuelve los codigos
                                # contra el catalogo de base y corre el motor. Es la
                                # unica pieza que toca el puerto y el dominio a la vez.
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
  infrastructure/
    persistence/
      db.py                     # engine/sesion SQLAlchemy async (esquema dbo_cubicador)
      modelos.py                 # las 5 tablas del motor 3D - DDL en docs/Database/
      modelos_catalogo.py         # las 10 tablas del catalogo (S5-HU-17.2). MetaData
                                   # PROPIO: las administra el equipo de Datos, el
                                   # Cubicador no las crea ni las altera.
      repositorios/               # implementan los Ports de persistencia
        catalogo_repo.py           # SqlCatalogoRepository: lee el catalogo entero
    mensajeria/
      rabbitmq.py                 # implementa IEventosPort con aio-pika
    clientes/
      nexo_client.py               # implementa ILineaSolicitudPort (httpx + Bearer)
  api/
    dependencias.py               # inyeccion de dependencias FastAPI (equivalente a Program.cs)
    dtos/
      comun.py                     # ApiModel base (camelCase)
      cubicacion_rapida.py          # DTOs de /api/cubicacion
      plan_cargue.py                 # DTOs de /api/planes-cargue
    routers/
      cubicacion.py                  # FastAPI router: /api/cubicacion
      planes_cargue.py                # FastAPI router: /api/planes-cargue
    main.py                          # app FastAPI, manejo de excepciones, healthcheck
  worker/
    main.py                          # consumidor RabbitMQ (optimizar/crear_desde_linea)
tests/
  domain/                            # 33 pytest - las 31 reglas de testDocumentation/REGLAS_DE_NEGOCIO.md
  api/
    test_smoke.py                    # flujo end-to-end contra SQLite
scripts/
  aplicar_sql.py                      # aplica los .sql de docs/Database/ (corta por GO)
  verificar_mapeo_catalogo.py          # compara modelos_catalogo.py contra sys.columns
                                        # del esquema REAL. Las pruebas corren contra
                                        # SQLite y no pueden ver esa divergencia.
  verificar_integracion_mssql.py        # humo end-to-end contra SQL Server real
  verificar_aislamiento.sql              # confirma que no tocamos dbo ni dbo_cotifly
docs/Database/ESQUEMA_CUBICADOR.sql   # DDL del motor 3D, fuente de verdad, se aplica a mano
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
| `CubicacionService.cs` (evaluador SICCE) | `domain/motor/cubicacion_rapida.py` |
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

- **`api/main.py`** — FastAPI, atiende las rutas síncronas. **Estado actual**: `optimizar`/`reoptimizar`/`desde-línea` también se resuelven aquí mismo, síncronas (ver README.md "Estado actual") — moverlas a publicar en RabbitMQ y responder 202 de inmediato es el siguiente paso, no un cambio de diseño: el camino async ya está probado de punta a punta por el worker (ver abajo), solo falta que la API lo use.
- **`worker/main.py`** — consume `cubicacion.solicitada`, ejecuta el caso de uso pesado, y publica el evento de resultado. **Verificado contra RabbitMQ real** en Docker Compose: mensaje publicado → worker lo consume → optimiza contra la base → publica `plan-de-cargue.generado` → plan queda persistido como `optimizado`.

Ambos se escalan de forma independiente (más réplicas del worker si hay cola acumulada, sin tocar cuántas instancias de la API atienden lecturas).
