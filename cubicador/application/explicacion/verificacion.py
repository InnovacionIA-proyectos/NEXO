"""Que ninguna cifra del texto sea inventada (#369, primer criterio).

El criterio de la issue -"los valores del texto coinciden con los del motor"-
suena a promesa. Aca se convierte en algo que una prueba demuestra: se extrae
**toda** cifra del texto generado y se exige que cada una salga de las que le
pasamos al modelo.

Por que hace falta. Una explicacion que dice "la Minimula va al 87 % de su
volumen" se lee perfecta aunque el motor hubiera calculado 78. Nadie lo nota
mirando, y el numero equivocado es justo el que sustenta la recomendacion. El
modelo no tiene forma de verificarse a si mismo; el codigo si.

Que se tolera, porque no es trampa sino escritura normal:
  - el formato: `1.234,5` y `1234.5` son el mismo numero;
  - el redondeo DECLARADO: si el motor dice 87,47 y el texto dice "87 %" o
    "87,5 %", las dos son fieles. Se acepta cuando redondear el valor del motor
    a los decimales que el texto muestra da exactamente lo escrito.

Lo que no se tolera es una cifra que no se parezca a ninguna. Ahi se descarta
la explicacion entera -no la frase- porque una sola cifra inventada vuelve
sospechoso todo el texto, y es opcional: no mostrarla no rompe nada.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from decimal import ROUND_HALF_EVEN, ROUND_HALF_UP, Decimal, InvalidOperation

# Una cifra con separadores de miles opcionales y decimales con coma o punto.
# Se exige que no venga pegada a una letra o a un digito para no partir
# "2S2" (un codigo de vehiculo) en un 2 y un 2.
_CIFRA = re.compile(r"(?<![\w.,])\d{1,3}(?:[.,]\d{3})*(?:[.,]\d+)?(?![\w])")


@dataclass(frozen=True)
class Verificacion:
    fiel: bool
    cifras_inventadas: tuple[str, ...]


def verificar(texto: str, permitidos: list[Decimal]) -> Verificacion:
    inventadas = tuple(
        crudo for crudo, valor in _cifras(texto)
        if not any(_coincide(valor, p) for p in permitidos)
    )
    return Verificacion(not inventadas, inventadas)


def _cifras(texto: str) -> list[tuple[str, Decimal]]:
    halladas = []
    for m in _CIFRA.finditer(texto):
        try:
            halladas.append((m.group(), _a_decimal(m.group())))
        except (InvalidOperation, ValueError):
            continue
    return halladas


def _a_decimal(crudo: str) -> Decimal:
    """`1.234,5` -> 1234.5 y `1,234.5` -> 1234.5.

    Manda el ULTIMO separador: el que queda mas a la derecha es el decimal, y
    los demas son de miles. Con un solo separador se mira cuantos digitos lo
    siguen: exactamente tres significa miles (`1.234`), cualquier otra cosa es
    decimal (`87,5`). `1.234` como "mil doscientos treinta y cuatro" es lo
    normal en espanol, y como 1,234 no aparece en este dominio.
    """
    puntos, comas = crudo.rfind("."), crudo.rfind(",")
    corte = max(puntos, comas)
    if corte == -1:
        return Decimal(crudo)
    decimales = len(crudo) - corte - 1
    if decimales == 3 and crudo.count(".") + crudo.count(",") == 1:
        return Decimal(crudo.replace(".", "").replace(",", ""))
    entero = crudo[:corte].replace(".", "").replace(",", "")
    return Decimal(f"{entero}.{crudo[corte + 1:]}")


def _coincide(delTexto: Decimal, delMotor: Decimal) -> bool:
    if delTexto == delMotor:
        return True
    # Redondeo declarado: el texto muestra N decimales, asi que se compara el
    # valor del motor redondeado a esos mismos N.
    #
    # Se aceptan las DOS convenciones de la mitad exacta, y no por laxitud:
    # Python redondea 62,5 a 62 (al par, que es lo que hace `round`), y la
    # gente lo redondea a 63. Con solo la de Python se rechazaban
    # explicaciones correctas -medido contra el modelo: 1 de cada 8, y el
    # unico fallo de la tanda era exactamente este-. Una cifra inventada no se
    # cuela por aqui: sigue teniendo que estar a medio punto de un valor real.
    decimales = -delTexto.as_tuple().exponent
    if decimales < 0:
        return False
    paso = Decimal(1).scaleb(-decimales)
    try:
        return any(delMotor.quantize(paso, rounding=modo) == delTexto
                   for modo in (ROUND_HALF_UP, ROUND_HALF_EVEN))
    except (InvalidOperation, ValueError):
        return False
