"""Inyeccion de dependencias de FastAPI - equivalente al composition root
(`Program.cs`) de NEXO."""

from __future__ import annotations

import os
from collections.abc import AsyncIterator

from fastapi import Depends, Header, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession

from application.ports.externos import ILineaSolicitudPort
from infrastructure.clientes.nexo_client import NexoLineaSolicitudClient
from infrastructure.mensajeria.rabbitmq import RabbitMqEventosPublisher
from infrastructure.persistence.db import obtener_sesion
from infrastructure.persistence.repositorios.parametros_repo import SqlParametrosCubicacionRepository
from infrastructure.persistence.repositorios.plan_cargue_repo import SqlPlanCargueRepository
from infrastructure.persistence.repositorios.vehiculo_repo import SqlVehiculoCubicacionRepository

CUBICADOR_API_KEY = os.environ.get("CUBICADOR_API_KEY", "")
NEXO_BASE_URL = os.environ.get("NEXO_BASE_URL", "")
NEXO_CALLBACK_API_KEY = os.environ.get("NEXO_CALLBACK_API_KEY", "")
CUBICADOR_RABBITMQ_URL = os.environ.get("CUBICADOR_RABBITMQ_URL", "amqp://guest:guest@localhost:5672/")

# Publisher unico del proceso API: se conecta en el lifespan de api/main.py y
# se comparte entre todas las peticiones (una sola conexion AMQP, no una por
# request). Sigue el mismo patron que el resto de dependencias.py: modulo =
# composition root, igual que Program.cs en NEXO.
_publisher: RabbitMqEventosPublisher | None = None


async def iniciar_publisher() -> None:
    global _publisher
    _publisher = RabbitMqEventosPublisher(CUBICADOR_RABBITMQ_URL)
    await _publisher.conectar()


async def cerrar_publisher() -> None:
    if _publisher is not None:
        await _publisher.cerrar()


def obtener_eventos_publisher() -> RabbitMqEventosPublisher:
    if _publisher is None:
        raise RuntimeError("El publisher de RabbitMQ no se ha inicializado (ver lifespan de api/main.py).")
    return _publisher


async def verificar_api_key(authorization: str = Header(default="")) -> None:
    """Bearer estatico de servicio-a-servicio (ver docs/CONTRATOS.md seccion D) -
    no identifica un usuario, solo autoriza al backend llamante."""
    esperado = f"Bearer {CUBICADOR_API_KEY}"
    if not CUBICADOR_API_KEY or authorization != esperado:
        raise HTTPException(status_code=401, detail={"codigo": "NO_AUTORIZADO", "mensaje": "Bearer invalido o ausente."})


async def obtener_db() -> AsyncIterator[AsyncSession]:
    async for sesion in obtener_sesion():
        yield sesion


def obtener_vehiculo_repo(sesion: AsyncSession = Depends(obtener_db)) -> SqlVehiculoCubicacionRepository:
    return SqlVehiculoCubicacionRepository(sesion)


def obtener_plan_repo(sesion: AsyncSession = Depends(obtener_db)) -> SqlPlanCargueRepository:
    return SqlPlanCargueRepository(sesion)


def obtener_parametros_repo(sesion: AsyncSession = Depends(obtener_db)) -> SqlParametrosCubicacionRepository:
    return SqlParametrosCubicacionRepository(sesion)


def obtener_linea_solicitud_port() -> ILineaSolicitudPort:
    return NexoLineaSolicitudClient(NEXO_BASE_URL, NEXO_CALLBACK_API_KEY)
