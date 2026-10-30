"""Lo que propone el modelo, leido y validado contra el catalogo.

Mismo criterio que `ia_sombra/propuesta.py`: el esquema cerrado garantiza la
FORMA, y aca se comprueba el FONDO -que el codigo exista, que las medidas sean
numeros positivos, que la cantidad sea un entero de al menos uno-. **Nunca se
corrige ni se completa un valor que el modelo no dio**: lo que falta se rechaza
con su motivo.

Esa regla no es celo: el peso es lo que cuesta el flete. Estimar "una caja de
gaseosa pesa unos 20 kilos" produce una cotizacion que parece buena y esta mal,
que es peor que no producir ninguna.

La interpretacion es PARCIAL a proposito: si de tres posiciones una no trae
peso, vuelven dos posiciones y un rechazo, no un fracaso entero. Lo que no se
permite es que el rechazo pase inadvertido -ver `Interpretacion.completa`-,
porque esa es exactamente la forma de fallo de #304: una posicion que se pierde
sin que nadie lo note y un peso total que sale corto.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from decimal import Decimal, InvalidOperation

from domain.entidades.catalogo import Catalogo

MOTIVOS = (
    "CODIGO_DESCONOCIDO",
    "CANTIDAD_FALTANTE", "CANTIDAD_INVALIDA", "CANTIDAD_AMBIGUA",
    "DIMENSION_FALTANTE", "DIMENSION_INVALIDA",
    "PESO_FALTANTE", "PESO_INVALIDO",
    "SIN_POSICIONES",
)

# Un texto de captura no trae mil posiciones. El tope existe para que una
# respuesta desbocada no se convierta en mil lineas de solicitud mas abajo.
MAX_POSICIONES = 50
MAX_TEXTO_RECHAZO = 300


class RespuestaInvalida(Exception):
    """La respuesta del modelo no cumple el contrato. `args[0]` dice por que (sin datos del usuario)."""


@dataclass(frozen=True)
class PosicionInterpretada:
    codigo_mercancia: str
    cantidad: int
    largo_cm: Decimal
    ancho_cm: Decimal
    alto_cm: Decimal
    peso_kg: Decimal


@dataclass(frozen=True)
class Rechazo:
    """Un pedazo del texto que no se pudo convertir, y por que."""

    texto_original: str
    motivo: str


@dataclass(frozen=True)
class Interpretacion:
    posiciones: tuple[PosicionInterpretada, ...]
    rechazos: tuple[Rechazo, ...]

    @property
    def completa(self) -> bool:
        """Sin rechazos y con al menos una posicion.

        Existe para que usar el resultado sin mirar los rechazos cueste
        trabajo: quien llame tiene que decidir que hace con un `False`, en vez
        de leer `.posiciones` y seguir de largo.
        """
        return not self.rechazos and bool(self.posiciones)


def leer_interpretacion(texto: str, catalogo: Catalogo) -> Interpretacion:
    try:
        d = json.loads(texto)
    except (TypeError, ValueError) as ex:
        raise RespuestaInvalida("La respuesta no es JSON.") from ex
    if not isinstance(d, dict):
        raise RespuestaInvalida("La respuesta no es un objeto JSON.")

    crudas = d.get("posiciones") or []
    if not isinstance(crudas, list):
        raise RespuestaInvalida("`posiciones` no es una lista.")
    if len(crudas) > MAX_POSICIONES:
        raise RespuestaInvalida(f"La respuesta trae mas de {MAX_POSICIONES} posiciones.")

    posiciones: list[PosicionInterpretada] = []
    rechazos = [r for r in (_leer_rechazo(x, i) for i, x in enumerate(d.get("rechazos") or [])) if r]

    for i, cruda in enumerate(crudas):
        if not isinstance(cruda, dict):
            raise RespuestaInvalida(f"La posicion {i} no es un objeto.")
        try:
            posiciones.append(_leer_posicion(cruda, catalogo))
        except _PosicionRechazada as rechazo:
            rechazos.append(Rechazo(_recortar(cruda.get("textoOriginal")), rechazo.motivo))

    if not posiciones and not rechazos:
        rechazos.append(Rechazo("", "SIN_POSICIONES"))
    return Interpretacion(tuple(posiciones), tuple(rechazos))


class _PosicionRechazada(Exception):
    def __init__(self, motivo: str) -> None:
        super().__init__(motivo)
        self.motivo = motivo


def _leer_posicion(cruda: dict, catalogo: Catalogo) -> PosicionInterpretada:
    codigo = cruda.get("codigo")
    # `mercancia_por_codigo` ya tolera mayusculas y espacios (S5-HU-17.7).
    tipo = catalogo.mercancia_por_codigo(codigo) if isinstance(codigo, str) else None
    # Un tipo no cubicable y uno inexistente son el mismo caso para el motor
    # (ver el docstring de `TipoMercancia`), asi que tambien lo son aca.
    if tipo is None or not tipo.cubicable:
        raise _PosicionRechazada("CODIGO_DESCONOCIDO")

    return PosicionInterpretada(
        codigo_mercancia=tipo.codigo,
        cantidad=_cantidad(cruda.get("cantidad")),
        largo_cm=_medida(cruda.get("largoCm")),
        ancho_cm=_medida(cruda.get("anchoCm")),
        alto_cm=_medida(cruda.get("altoCm")),
        peso_kg=_peso(cruda.get("pesoKg")),
    )


def _cantidad(valor: object) -> int:
    if valor is None:
        raise _PosicionRechazada("CANTIDAD_FALTANTE")
    # bool es int en Python: True no puede pasar por una cantidad.
    if isinstance(valor, bool) or not isinstance(valor, int):
        raise _PosicionRechazada("CANTIDAD_INVALIDA")
    if valor < 1:
        raise _PosicionRechazada("CANTIDAD_INVALIDA")
    return valor


def _medida(valor: object) -> Decimal:
    return _numero_positivo(valor, "DIMENSION_FALTANTE", "DIMENSION_INVALIDA")


def _peso(valor: object) -> Decimal:
    return _numero_positivo(valor, "PESO_FALTANTE", "PESO_INVALIDO")


def _numero_positivo(valor: object, si_falta: str, si_es_invalido: str) -> Decimal:
    if valor is None:
        raise _PosicionRechazada(si_falta)
    if isinstance(valor, bool) or not isinstance(valor, (int, float, str)):
        raise _PosicionRechazada(si_es_invalido)
    try:
        # Por `str` y no por `float` directo: `Decimal(0.1)` arrastra el error
        # binario y las medidas terminan en el motor y en la cotizacion.
        numero = Decimal(str(valor))
    except (InvalidOperation, ValueError):
        raise _PosicionRechazada(si_es_invalido) from None
    if not numero.is_finite() or numero <= 0:
        raise _PosicionRechazada(si_es_invalido)
    return numero


def _leer_rechazo(crudo: object, indice: int) -> Rechazo | None:
    if not isinstance(crudo, dict):
        raise RespuestaInvalida(f"El rechazo {indice} no es un objeto.")
    motivo = crudo.get("motivo")
    if motivo not in MOTIVOS:
        raise RespuestaInvalida(f"El rechazo {indice} trae un motivo que no es de la lista.")
    return Rechazo(_recortar(crudo.get("textoOriginal")), motivo)


def _recortar(texto: object) -> str:
    """El texto vuelve recortado y es del usuario, no del modelo: no se interpreta, se muestra."""
    return texto.strip()[:MAX_TEXTO_RECHAZO] if isinstance(texto, str) else ""
