"""Puertos de persistencia (Protocols). Implementados por
`infrastructure/persistence/repositorios/*.py` con SQLAlchemy. Nunca se
extraen a un microservicio - son EF/SQLAlchemy puro, igual que en NEXO."""

from __future__ import annotations

from typing import Protocol

from domain.entidades.catalogo import Catalogo
from domain.entidades.entidades import (
    ItemPlanCargue,
    ParametrosCubicacion,
    PlanCargue,
    VehiculoCubicacion,
)


class IVehiculoCubicacionRepository(Protocol):
    async def listar_con_siembra(self) -> list[VehiculoCubicacion]:
        """Siembra los presets del sistema que aun no existan (por nombre) y devuelve el catalogo completo."""
        ...

    async def republicar_todos(self) -> list[VehiculoCubicacion]:
        """Reparo manual - republica vehiculo.creado para TODO el catalogo
        actual (no solo lo nuevo). Idempotente del lado NEXO (upsert por Id),
        asi que es seguro correrlo mas de una vez."""
        ...

    async def obtener(self, id: str) -> VehiculoCubicacion | None: ...

    async def buscar_por_nombre(self, nombre: str) -> VehiculoCubicacion | None: ...

    async def guardar(self, vehiculo: VehiculoCubicacion) -> VehiculoCubicacion:
        """Upsert por id (id nuevo o inexistente = nuevo)."""
        ...

    async def tiene_planes_asociados(self, id: str) -> bool: ...

    async def eliminar(self, id: str) -> None: ...


class IPlanCargueRepository(Protocol):
    async def vehiculo_existe(self, vehiculo_id: str) -> bool: ...

    async def crear(
        self, nombre: str, vehiculo_id: str, linea_solicitud_id: str | None = None, numero_vehiculo: int = 1,
        id: str | None = None,
    ) -> PlanCargue: ...

    async def obtener(self, id: str) -> PlanCargue | None:
        """Con sus items incluidos."""
        ...

    async def listar_recientes(self, top: int) -> list[PlanCargue]: ...

    async def eliminar(self, id: str) -> None: ...

    async def guardar_item(self, plan_id: str, item_id: str | None, item: ItemPlanCargue) -> ItemPlanCargue: ...

    async def eliminar_item(self, plan_id: str, item_id: str) -> None: ...

    async def obtener_colocaciones(self, plan_id: str): ...

    async def obtener_colocaciones_bloqueadas(self, plan_id: str): ...

    async def obtener_incidencias(self, plan_id: str) -> list: ...

    async def guardar_resultado_optimizacion(
        self, plan_id: str, resultado, metricas, *,
        estrategia: str, algoritmo: str, semilla: int | None, ponderaciones,
    ) -> None: ...

    async def marcar_procesando(self, plan_id: str) -> None: ...

    async def marcar_error(self, plan_id: str | None, mensaje: str) -> None:
        """No-op silencioso si el plan no existe. Acepta None a proposito:
        lo llama el manejador de errores del worker, que ante un mensaje
        malformado puede no tener planId - y ese bloque nunca debe lanzar,
        porque corre antes del `ack`."""
        ...

    async def obtener_atascados(self, limite) -> list[str]:
        """Ids de los planes en `procesando` con `updated_at` anterior a
        `limite`. SOLO consulta, no muta: quien llama publica el rechazo y
        DESPUES marca cada uno con `marcar_error`. Ese orden es deliberado -
        es el mismo criterio que el Outbox de NEXO (publicar antes de
        persistir): si el broker falla, el plan sigue en `procesando` y el
        proximo ciclo lo reintenta, en vez de quedar marcado `error` sin que
        nadie se haya enterado. Ver worker/main.py::_revisar_timeouts."""
        ...

    async def obtener_colocacion(self, plan_id: str, colocacion_id: str): ...

    async def marcar_bloqueada(self, colocacion_id: str, bloqueada: bool) -> None: ...

    async def marcar_retirada(self, colocacion_id: str) -> None: ...

    async def resolver_colocacion_por_clave(self, plan_id: str, clave_item_indice: str) -> str | None: ...

    async def aplicar_movimiento(
        self, colocacion_id: str, x: float, y: float, z: float,
        orientacion_indice: int, largo_mm: float, ancho_mm: float, alto_mm: float,
        porcentaje_soporte: float, soporte_colocacion_id: str | None,
    ) -> None: ...

    async def actualizar_tras_ajuste(self, plan_id: str, incidencias, puntaje: float, metricas, anotaciones: dict) -> None: ...

    async def eliminar_planes_de_linea(self, linea_solicitud_id: str) -> None: ...

    async def obtener_sobrantes(self, plan_id: str) -> list[tuple[str, int]]: ...

    async def cargo_algo(self, plan_id: str) -> bool: ...

    async def flota(self, plan_id: str) -> list: ...

    async def listar_por_linea(self, linea_solicitud_id: str) -> list:
        """Los planes (flota) de una linea de solicitud - vacio si `crear_desde_linea`
        todavia no ha terminado de procesarse (ver api/routers/planes_cargue.py `por_linea`)."""
        ...


class IParametrosCubicacionRepository(Protocol):
    async def obtener(self) -> ParametrosCubicacion: ...

    async def guardar(self, parametros: ParametrosCubicacion) -> ParametrosCubicacion: ...


class ICatalogoRepository(Protocol):
    """Lectura del catalogo que administra el equipo de Datos (S5-HU-17.2).

    **Solo lee.** Las 10 tablas de `dbo_cubicador` las crea y mantiene el equipo
    de Datos con sus propios scripts; el Cubicador las consume. Las escrituras
    que si le corresponden son las de los CRUD (S5-HU-17.5) y las de
    recomendaciones, que son append-only y tienen su propio puerto.

    Un solo metodo a proposito: el motor necesita todo el catalogo para evaluar
    candidatos y lo recorre varias veces en una corrida. Son menos de cien filas
    entre las cinco tablas, asi que traerlo entero sale mas barato que ir y
    volver por partes.
    """

    async def cargar(self) -> Catalogo:
        """Trae vehiculos con sus carrocerias, mercancias, reglas y parametros."""
        ...
