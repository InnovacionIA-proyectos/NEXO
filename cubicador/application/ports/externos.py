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
    PlanDeCargueGenerado, CubicaciónRechazada, y desde S3-HU-03 tambien
    VehiculoCreado/VehiculoActualizado). Ver CONTRATO.md seccion 6 para las
    rutas/colas."""

    async def publicar_cubicacion_solicitada(
        self, plan_id: str, tipo: str, solicitud: dict[str, Any],
    ) -> None: ...

    async def publicar_plan_generado(self, plan_id: str, resultado: dict[str, Any]) -> None: ...

    async def publicar_cubicacion_rechazada(self, plan_id: str, codigo: str, mensaje: str) -> None: ...

    async def publicar_vehiculo_creado(self, vehiculo_id: str, vehiculo: dict[str, Any]) -> None: ...

    async def publicar_vehiculo_actualizado(self, vehiculo_id: str, vehiculo: dict[str, Any]) -> None: ...
