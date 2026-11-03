"""Lo que propone el modelo, leido y validado contra el catalogo.

El modelo responde con una salida estructurada de esquema cerrado, pero el
esquema solo garantiza la FORMA: aca se comprueba que los codigos existan, que
los indices apunten a posiciones reales y que las cantidades sean enteras
positivas. Una respuesta que no pasa se rechaza con motivo; nunca se corrige ni
se completa un valor que el modelo no dio.
"""

from __future__ import annotations

import json
from dataclasses import dataclass

from domain.entidades.catalogo import Catalogo

RESULTADO_ASIGNADO = "ASIGNADO"
RESULTADO_SIN_SELECCION = "SIN_SELECCION_AUTOMATICA"
MOTIVOS = (
    "EXTRADIMENSIONADO", "SIN_CANDIDATOS_DIMENSIONALES", "SUPERA_K_MAX", "SIN_CARROCERIA_COMPATIBLE", "OTRO",
)
MAX_EXPLICACION = 500


class RespuestaInvalida(Exception):
    """La respuesta del modelo no cumple el contrato. `args[0]` dice por que (sin datos del usuario)."""


@dataclass(frozen=True)
class CargaIa:
    indice: int
    cantidad: int


@dataclass(frozen=True)
class VehiculoIa:
    codigo_vehiculo: str
    codigo_carroceria: str
    carga: tuple[CargaIa, ...]

    @property
    def par(self) -> tuple[str, str]:
        return (self.codigo_vehiculo, self.codigo_carroceria)


@dataclass(frozen=True)
class PropuestaIa:
    resultado: str
    motivo: str | None
    vehiculos: tuple[VehiculoIa, ...]
    explicacion: str


def leer_propuesta(texto: str, catalogo: Catalogo, posiciones: int) -> PropuestaIa:
    """`posiciones` es cuantas mercancias traia la solicitud: los indices validos son 0..posiciones-1."""
    try:
        d = json.loads(texto)
    except (TypeError, ValueError) as ex:
        raise RespuestaInvalida("La respuesta no es JSON.") from ex
    if not isinstance(d, dict):
        raise RespuestaInvalida("La respuesta no es un objeto JSON.")

    resultado = d.get("resultado")
    if resultado not in (RESULTADO_ASIGNADO, RESULTADO_SIN_SELECCION):
        raise RespuestaInvalida("El resultado no es ASIGNADO ni SIN_SELECCION_AUTOMATICA.")

    motivo = d.get("motivo")
    if motivo is not None and motivo not in MOTIVOS:
        raise RespuestaInvalida("El motivo no es uno de los conocidos.")

    codigos_vehiculo = {v.codigo for v in catalogo.vehiculos}
    codigos_carroceria = {c.codigo for c in catalogo.carrocerias}
    vehiculos: list[VehiculoIa] = []
    for i, v in enumerate(d.get("vehiculos") or []):
        if not isinstance(v, dict):
            raise RespuestaInvalida(f"El vehiculo {i} no es un objeto.")
        codigo, carroceria = v.get("codigoVehiculo"), v.get("codigoCarroceria")
        if codigo not in codigos_vehiculo:
            raise RespuestaInvalida(f"El vehiculo {i} usa un codigo que no esta en el catalogo.")
        if carroceria not in codigos_carroceria:
            raise RespuestaInvalida(f"El vehiculo {i} usa una carroceria que no esta en el catalogo.")
        carga: list[CargaIa] = []
        for c in v.get("carga") or []:
            indice, cantidad = (c or {}).get("indice"), (c or {}).get("cantidad")
            # bool es int en Python: True no puede pasar por un indice ni una cantidad.
            if isinstance(indice, bool) or isinstance(cantidad, bool) or not isinstance(indice, int) or not isinstance(cantidad, int):
                raise RespuestaInvalida(f"La carga del vehiculo {i} trae un indice o una cantidad que no son enteros.")
            if not 0 <= indice < posiciones:
                raise RespuestaInvalida(f"La carga del vehiculo {i} apunta a una posicion que no existe.")
            if cantidad < 1:
                raise RespuestaInvalida(f"La carga del vehiculo {i} trae una cantidad menor que uno.")
            carga.append(CargaIa(indice, cantidad))
        vehiculos.append(VehiculoIa(codigo, carroceria, tuple(carga)))

    if resultado == RESULTADO_ASIGNADO and not vehiculos:
        raise RespuestaInvalida("Dice ASIGNADO pero no trae ningun vehiculo.")
    if resultado == RESULTADO_SIN_SELECCION and vehiculos:
        raise RespuestaInvalida("Dice SIN_SELECCION_AUTOMATICA pero trae vehiculos.")

    explicacion = d.get("explicacion")
    explicacion = explicacion.strip()[:MAX_EXPLICACION] if isinstance(explicacion, str) else ""
    return PropuestaIa(resultado, motivo, tuple(vehiculos), explicacion)
