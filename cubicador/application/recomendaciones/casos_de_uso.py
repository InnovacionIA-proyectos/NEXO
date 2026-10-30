"""Casos de uso de recomendacion de flota (S5-HU-17.3 y 17.4).

`recomendar_flota` es la costura entre el motor puro
(`domain.motor.seleccion_flota`) y la base: trae el catalogo por su puerto,
trae las medidas estandar por el suyo, resuelve las mercancias que llegaron
contra el catalogo real y llama al motor. **Ahi es donde deja de haber catalogo
quemado.**

`crear_recomendacion` agrega la persistencia append-only y es lo que sirve
`POST /recomendaciones`; `consultar_recomendacion` y
`listar_mercancias_de_vehiculo` son la reconsulta.

Lo que NO hace este modulo: HTTP, DTOs ni autenticacion. El `origen_sistema`
llega ya **verificado contra la llave** desde la capa `api/` - nunca se toma
del cuerpo de la peticion.
"""

from __future__ import annotations

from dataclasses import dataclass
from decimal import Decimal

from application.excepciones import NotFoundException, ValidationFailedException
from application.ports.repositorios import (
    ICatalogoRepository,
    IParametrosCubicacionRepository,
    IRecomendacionRepository,
)
from domain.entidades.recomendacion import MercanciaRecomendada, RecomendacionGuardada
from domain.motor import seleccion_flota as motor

# Mismo tope que tenia el cubicador rapido: proteccion anti-DoS, no una regla
# de negocio. Expandir cantidades a unidades fisicas es lo que cuesta.
_MAX_UNIDADES_TOTALES = 10_000

# Tope de POSICIONES, que es distinto del de unidades (#304).
#
# El de arriba limita cuantas unidades fisicas se expanden; este limita cuantas
# filas trae la solicitud. Son dos costos distintos: 10.000 posiciones de una
# unidad pasan el tope de unidades y aun asi producen 10.000 filas en la
# respuesta y otras tantas en `recomendacionVehiculoMercancia` -desde
# S5-HU-17.8 el detalle es por posicion y ya no se agrupa por codigo-.
#
# El numero sale de NEXO: `clsCapturaLineaRequest.Posiciones` admite maximo 200
# por linea, asi que aceptar mas aca seria aceptar algo que la captura no puede
# haber producido.
_MAX_POSICIONES = 200

SISTEMAS_VALIDOS = ("NEXO", "COTIFLY")


@dataclass(frozen=True)
class LineaMercancia:
    """Una linea tal como llega del llamante.

    Viaja el **codigo** del tipo de mercancia, no el id (S5-HU-17.7). Es el
    `codigoMercancia` que este servicio devuelve en `GET /tipos-mercancia`.

    Se cambio del id al codigo para que el dato tenga una sola forma en todo
    el recorrido: NEXO ya persiste el codigo en la captura
    (`CapturaRequests.CodigoTipoMercancia`) y `codigoVehiculo` y
    `codigoCarroceria` tambien viajan como codigo. Con el id, NEXO tenia que
    traducir de un campo al otro para hablar con este servicio.
    """

    codigo_mercancia: str
    cantidad: int
    largo_cm: Decimal
    ancho_cm: Decimal
    alto_cm: Decimal
    peso_kg: Decimal


@dataclass(frozen=True)
class SolicitudDeRecomendacion:
    id_linea_solicitud: str
    origen_sistema: str
    mercancias: list[LineaMercancia]
    clase_viaje_preferida: str | None = None
    usuario_id: str | None = None


async def recomendar_flota(
    catalogo_repo: ICatalogoRepository,
    parametros_repo: IParametrosCubicacionRepository,
    lineas: list[LineaMercancia],
    clase_viaje_preferida: str | None = None,
) -> motor.Recomendacion:
    """Resuelve las lineas contra el catalogo y corre el motor, sin persistir.

    Las mercancias se resuelven **aca y no en el motor**: un codigo que no
    existe o una mercancia no cubicable son errores del llamante (400 en el
    contrato),
    no un resultado del algoritmo. Nunca se cae a un default silencioso - ese
    es justamente el fallback que mata S5-HU-17.6.
    """
    if not lineas:
        raise ValidationFailedException(
            "MERCANCIAS_REQUERIDAS", "La solicitud no trae ninguna mercancia.")

    if len(lineas) > _MAX_POSICIONES:
        raise ValidationFailedException(
            "CUBICACION_DEMASIADO_GRANDE",
            f"La solicitud trae {len(lineas)} posiciones; el maximo es {_MAX_POSICIONES}.")

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
                f"'{tipo.codigo}' esta marcada como no cubicable: no se le puede recomendar "
                "un vehiculo.")

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


async def crear_recomendacion(
    catalogo_repo: ICatalogoRepository,
    parametros_repo: IParametrosCubicacionRepository,
    recomendacion_repo: IRecomendacionRepository,
    solicitud: SolicitudDeRecomendacion,
) -> RecomendacionGuardada:
    """Corre el motor y guarda la corrida. Es lo que sirve `POST /recomendaciones`.

    **Se guarda tambien cuando el resultado es `SIN_SELECCION_AUTOMATICA`.**
    Esa corrida tambien ocurrio, y sin la fila padre no quedaria rastro de que
    se consulto ni de por que no hubo vehiculo: es la razon de existir de la
    tabla `recomendaciones`.
    """
    if not solicitud.id_linea_solicitud or not solicitud.id_linea_solicitud.strip():
        raise ValidationFailedException(
            "DIMENSIONES_INVALIDAS", "La solicitud no trae idLineaSolicitud.")

    resultado = await recomendar_flota(
        catalogo_repo, parametros_repo, solicitud.mercancias, solicitud.clase_viaje_preferida)

    return await recomendacion_repo.guardar(
        resultado, solicitud.id_linea_solicitud, solicitud.origen_sistema, solicitud.usuario_id)


async def consultar_recomendacion(
    recomendacion_repo: IRecomendacionRepository, id_linea_solicitud: str, origen_sistema: str,
) -> RecomendacionGuardada:
    """La ultima corrida de una linea. `origen_sistema` es obligatorio.

    Un mismo codigo de linea puede existir en NEXO y en Cotifly y ser rutas
    distintas: sin el filtro por origen, una consulta podria devolver la
    recomendacion del otro sistema.
    """
    if origen_sistema not in SISTEMAS_VALIDOS:
        raise ValidationFailedException(
            "ORIGEN_SISTEMA_INVALIDO",
            f"origenSistema debe ser uno de {', '.join(SISTEMAS_VALIDOS)}.")

    guardada = await recomendacion_repo.obtener_por_linea(id_linea_solicitud, origen_sistema)
    if guardada is None:
        raise NotFoundException(
            "RECOMENDACION_NO_ENCONTRADA",
            f"No hay recomendaciones para la linea '{id_linea_solicitud}' en {origen_sistema}.")
    return guardada


async def listar_mercancias_de_vehiculo(
    recomendacion_repo: IRecomendacionRepository, id_recomendacion_vehiculo: str,
) -> list[MercanciaRecomendada]:
    mercancias = await recomendacion_repo.listar_mercancias(id_recomendacion_vehiculo)
    if mercancias is None:
        raise NotFoundException(
            "RECOMENDACION_NO_ENCONTRADA",
            f"No existe el vehiculo recomendado '{id_recomendacion_vehiculo}'.")
    return mercancias
