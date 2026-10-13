"""Caso de uso de recomendacion de flota (S5-HU-17.3).

Es la costura entre el motor puro (`domain.motor.seleccion_flota`) y la base:
trae el catalogo por su puerto, trae las medidas estandar por el suyo, resuelve
los codigos de mercancia que llegaron contra el catalogo real y llama al motor.

**Aca es donde deja de haber catalogo quemado.** El motor no conoce
SQLAlchemy ni sabe de donde salen los vehiculos; este modulo es el unico que
toca los dos lados.

Lo que NO hace: HTTP, DTOs ni persistir la recomendacion. Eso es S5-HU-17.4.
"""

from __future__ import annotations

from dataclasses import dataclass
from decimal import Decimal

from application.excepciones import ValidationFailedException
from application.ports.repositorios import (
    ICatalogoRepository,
    IParametrosCubicacionRepository,
)
from domain.motor import seleccion_flota as motor

# Mismo tope que el cubicador rapido: proteccion anti-DoS, no una regla de
# negocio. Expandir cantidades a unidades fisicas es lo que cuesta.
_MAX_UNIDADES_TOTALES = 10_000


@dataclass(frozen=True)
class LineaMercancia:
    """Una linea tal como llega del llamante, con el codigo **sin resolver**."""

    codigo_mercancia: str
    cantidad: int
    largo_cm: Decimal
    ancho_cm: Decimal
    alto_cm: Decimal
    peso_kg: Decimal


async def recomendar_flota(
    catalogo_repo: ICatalogoRepository,
    parametros_repo: IParametrosCubicacionRepository,
    lineas: list[LineaMercancia],
    clase_viaje_preferida: str | None = None,
) -> motor.Recomendacion:
    """Resuelve las lineas contra el catalogo y corre el motor.

    Los codigos se resuelven **aca y no en el motor**: un codigo que no existe
    o una mercancia no cubicable son errores del llamante (400 en el contrato),
    no un resultado del algoritmo. Nunca se cae a un default silencioso - ese
    es el fallback que mata S5-HU-17.6.
    """
    if not lineas:
        raise ValidationFailedException(
            "MERCANCIAS_REQUERIDAS", "La solicitud no trae ninguna mercancia.")

    total = sum(linea.cantidad for linea in lineas)
    if total > _MAX_UNIDADES_TOTALES:
        raise ValidationFailedException(
            "CUBICACION_DEMASIADO_GRANDE",
            f"La solicitud suma {total} unidades; el maximo es {_MAX_UNIDADES_TOTALES}.")

    catalogo = await catalogo_repo.cargar()

    mercancias: list[motor.MercanciaSolicitada] = []
    for linea in lineas:
        if linea.cantidad < 1:
            raise ValidationFailedException(
                "DIMENSIONES_INVALIDAS",
                f"La cantidad de '{linea.codigo_mercancia}' debe ser al menos 1.")
        if min(linea.largo_cm, linea.ancho_cm, linea.alto_cm, linea.peso_kg) <= 0:
            raise ValidationFailedException(
                "DIMENSIONES_INVALIDAS",
                f"Las medidas y el peso de '{linea.codigo_mercancia}' deben ser mayores que cero.")

        tipo = catalogo.mercancia_por_codigo(linea.codigo_mercancia)
        if tipo is None:
            raise ValidationFailedException(
                "MERCANCIA_DESCONOCIDA",
                f"'{linea.codigo_mercancia}' no esta en el catalogo de tipos de mercancia.")
        if not tipo.cubicable:
            raise ValidationFailedException(
                "MERCANCIA_NO_CUBICABLE",
                f"'{linea.codigo_mercancia}' esta marcada como no cubicable: no se le puede "
                "recomendar un vehiculo.")

        mercancias.append(motor.MercanciaSolicitada(
            tipo=tipo, cantidad=linea.cantidad, largo_cm=linea.largo_cm,
            ancho_cm=linea.ancho_cm, alto_cm=linea.alto_cm, peso_kg=linea.peso_kg,
        ))

    parametros = await parametros_repo.obtener()
    medidas = motor.MedidasEstandar(
        largo_max_cm=Decimal(str(parametros.largo_max_cm)),
        ancho_max_cm=Decimal(str(parametros.ancho_max_cm)),
        alto_max_cm=Decimal(str(parametros.alto_max_cm)),
        peso_max_kg=Decimal(str(parametros.peso_max_kg)),
    )

    return motor.recomendar(catalogo, motor.SolicitudRecomendacion(
        mercancias=tuple(mercancias), medidas_estandar=medidas,
        clase_viaje_preferida=clase_viaje_preferida,
    ))
