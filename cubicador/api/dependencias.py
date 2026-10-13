"""Inyeccion de dependencias de FastAPI - equivalente al composition root
(`Program.cs`) de NEXO."""

from __future__ import annotations

import os
from collections.abc import AsyncIterator

from fastapi import Depends, HTTPException, Security
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from sqlalchemy.ext.asyncio import AsyncSession

from application.ports.externos import ILineaSolicitudPort
from infrastructure.clientes.nexo_client import NexoLineaSolicitudClient
from infrastructure.mensajeria.rabbitmq import RabbitMqEventosPublisher
from infrastructure.persistence.db import obtener_sesion
from infrastructure.persistence.repositorios.catalogo_repo import SqlCatalogoRepository
from infrastructure.persistence.repositorios.parametros_repo import SqlParametrosCubicacionRepository
from infrastructure.persistence.repositorios.plan_cargue_repo import SqlPlanCargueRepository
from infrastructure.persistence.repositorios.recomendacion_repo import SqlRecomendacionRepository
from infrastructure.persistence.repositorios.vehiculo_repo import SqlVehiculoCubicacionRepository

CUBICADOR_API_KEY = os.environ.get("CUBICADOR_API_KEY", "")
# Una llave POR SISTEMA (S5-HU-17.4). De aca sale el `origenSistema`
# **verificado** de las recomendaciones, en vez de creerle al cuerpo de la
# peticion: quien tiene la llave de Cotifly es Cotifly.
#
# Dos variables y no un mapa en una sola cadena: se leen mejor en Dokploy y
# rotar una no obliga a tocar la otra.
CUBICADOR_API_KEY_NEXO = os.environ.get("CUBICADOR_API_KEY_NEXO", "")
CUBICADOR_API_KEY_COTIFLY = os.environ.get("CUBICADOR_API_KEY_COTIFLY", "")
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


_bearer_scheme = HTTPBearer(auto_error=False)


async def verificar_api_key(credenciales: HTTPAuthorizationCredentials | None = Security(_bearer_scheme)) -> None:
    """Bearer estatico de servicio-a-servicio (ver CONTRATO.md seccion 5) -
    no identifica un usuario, solo autoriza al backend llamante.

    Usa HTTPBearer (esquema de seguridad real) en vez de un Header() crudo:
    un parametro de header llamado literalmente "authorization" sin esquema
    de seguridad asociado no siempre queda editable en Swagger UI (le da un
    tratamiento especial a ese nombre) - con HTTPBearer aparece el boton
    "Authorize" real y funciona en todos los endpoints de una sola vez.
    `auto_error=False` para mantener el mismo cuerpo de error 401 propio en
    vez del generico que HTTPBearer devolveria solo."""
    validas = {k for k in (os.environ.get("CUBICADOR_API_KEY", ""), *_llaves_por_sistema()) if k}
    if not validas or credenciales is None or credenciales.credentials not in validas:
        raise HTTPException(status_code=401, detail={"codigo": "NO_AUTORIZADO", "mensaje": "Bearer invalido o ausente."})


def _llaves_por_sistema() -> dict[str, str]:
    """Llave -> sistema, leido del entorno en cada llamada.

    Se lee de `os.environ` y no de las constantes de arriba porque esas quedan
    fijas al importar el modulo: una rotacion de llave exigiria reiniciar el
    proceso, y las pruebas no podrian cambiarlas entre casos.
    """
    return {
        llave: sistema
        for llave, sistema in (
            (os.environ.get("CUBICADOR_API_KEY_NEXO", ""), "NEXO"),
            (os.environ.get("CUBICADOR_API_KEY_COTIFLY", ""), "COTIFLY"),
        )
        if llave
    }


async def verificar_origen_sistema(
    credenciales: HTTPAuthorizationCredentials | None = Security(_bearer_scheme),
) -> str:
    """Devuelve el sistema al que pertenece la llave presentada.

    **La llave generica `CUBICADOR_API_KEY` no sirve aca**: identifica al
    servicio, no a un sistema, y el contrato necesita saber de quien viene la
    recomendacion para poder reconsultarla despues. Las dos pueden convivir -
    una para los endpoints de siempre, las otras dos para el contrato nuevo.
    """
    llaves = _llaves_por_sistema()
    sistema = llaves.get(credenciales.credentials) if credenciales is not None else None
    if sistema is None:
        raise HTTPException(status_code=401, detail={
            "codigo": "NO_AUTORIZADO",
            "mensaje": "Bearer ausente o invalido: este endpoint exige la llave de NEXO o la de Cotifly.",
        })
    return sistema


def exigir_origen_declarado(declarado: str | None, verificado: str) -> None:
    """Si el cuerpo declara un `origenSistema`, tiene que coincidir con la llave.

    Declararlo es opcional; mentir no. Un 401 y no un 400 porque el problema es
    de identidad, no de forma del mensaje.
    """
    if declarado and declarado.strip().upper() != verificado:
        raise HTTPException(status_code=401, detail={
            "codigo": "NO_AUTORIZADO",
            "mensaje": f"origenSistema '{declarado}' no corresponde a la llave presentada.",
        })


async def obtener_db() -> AsyncIterator[AsyncSession]:
    async for sesion in obtener_sesion():
        yield sesion


def obtener_vehiculo_repo(
    sesion: AsyncSession = Depends(obtener_db),
    publisher: RabbitMqEventosPublisher = Depends(obtener_eventos_publisher),
) -> SqlVehiculoCubicacionRepository:
    # El publisher es necesario para que listar_con_siembra() pueda publicar
    # vehiculo.creado por cada preset nuevo que siembre - ver ese metodo en
    # infrastructure/persistence/repositorios/vehiculo_repo.py.
    return SqlVehiculoCubicacionRepository(sesion, publisher)


def obtener_plan_repo(sesion: AsyncSession = Depends(obtener_db)) -> SqlPlanCargueRepository:
    return SqlPlanCargueRepository(sesion)


def obtener_parametros_repo(sesion: AsyncSession = Depends(obtener_db)) -> SqlParametrosCubicacionRepository:
    return SqlParametrosCubicacionRepository(sesion)


def obtener_catalogo_repo(sesion: AsyncSession = Depends(obtener_db)) -> SqlCatalogoRepository:
    # Solo lectura: el catalogo lo administra el equipo de Datos (S5-HU-17.2).
    return SqlCatalogoRepository(sesion)


def obtener_recomendacion_repo(sesion: AsyncSession = Depends(obtener_db)) -> SqlRecomendacionRepository:
    # Append-only: solo inserta y lee (S5-HU-17.4).
    return SqlRecomendacionRepository(sesion)


def obtener_linea_solicitud_port() -> ILineaSolicitudPort:
    return NexoLineaSolicitudClient(NEXO_BASE_URL, NEXO_CALLBACK_API_KEY)
