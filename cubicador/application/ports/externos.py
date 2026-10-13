"""Puertos hacia sistemas externos: NEXO (puente Solicitudes) y el broker de
mensajeria (RabbitMQ). Implementados en `infrastructure/clientes/` e
`infrastructure/mensajeria/` respectivamente."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Protocol


@dataclass(frozen=True)
class PosicionCarga:
    descripcion: str | None
    cantidad: int
    largo_cm: float
    ancho_cm: float
    alto_cm: float
    peso_kg: float | None


@dataclass(frozen=True)
class LineaParaCubicacion:
    linea_id: str
    posiciones: list[PosicionCarga]
    origen: str | None
    destino: str | None


class ILineaSolicitudPort(Protocol):
    """Puerto de solo lectura hacia NEXO - GET /api/lineas-solicitud/{id}/cubicacion (ver CONTRATO.md seccion 7)."""

    async def obtener(self, linea_solicitud_id: str) -> LineaParaCubicacion | None: ...


class IEventosPort(Protocol):
    """Publica los eventos de dominio a RabbitMQ (CubicaciónSolicitada,
    PlanDeCargueGenerado, CubicaciónRechazada, VehiculoCreado/
    VehiculoActualizado). Ver CONTRATO.md seccion 6 para las rutas/colas."""

    async def publicar_cubicacion_solicitada(
        self, plan_id: str, tipo: str, solicitud: dict[str, Any],
    ) -> None: ...

    async def publicar_plan_generado(self, plan_id: str, resultado: dict[str, Any]) -> None: ...

    async def publicar_cubicacion_rechazada(self, plan_id: str, codigo: str, mensaje: str) -> None: ...

    async def publicar_vehiculo_creado(self, vehiculo_id: str, vehiculo: dict[str, Any]) -> None: ...

    async def publicar_vehiculo_actualizado(self, vehiculo_id: str, vehiculo: dict[str, Any]) -> None: ...

    # --- Catalogo del contrato unico (S5-HU-17.4) --------------------------
    # NEXO proyecta mercancias y carrocerias igual que ya proyecta vehiculos:
    # sin estos eventos su copia se queda vieja y nadie se entera hasta que un
    # usuario elige una mercancia que ya no existe. Los publican las
    # escrituras de los CRUD (S5-HU-17.5); aca solo queda declarado el puerto.

    async def publicar_mercancia_creada(self, mercancia_id: str, mercancia: dict[str, Any]) -> None: ...

    async def publicar_mercancia_actualizada(self, mercancia_id: str, mercancia: dict[str, Any]) -> None: ...

    async def publicar_mercancia_eliminada(self, mercancia_id: str) -> None: ...

    async def publicar_carroceria_creada(self, carroceria_id: str, carroceria: dict[str, Any]) -> None: ...

    async def publicar_carroceria_actualizada(self, carroceria_id: str, carroceria: dict[str, Any]) -> None: ...

    async def publicar_carroceria_eliminada(self, carroceria_id: str) -> None: ...
