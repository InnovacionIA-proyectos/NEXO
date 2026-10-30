"""Genera la explicacion y la descarta si alguna cifra no es del motor.

**Es opcional por diseno.** La pantalla ya tiene los numeros cuando pide esto;
el texto es un agregado. Por eso aqui nada se propaga como error al que llama:
si el proveedor falla, si tarda, o si el texto trae una cifra inventada, vuelve
`None` y la pantalla se dibuja igual. Ese es el segundo criterio de la issue, y
asi se cumple por construccion en vez de por manejo de errores aguas arriba.
"""

from __future__ import annotations

import logging
from dataclasses import dataclass

from application.explicacion import prompt
from application.explicacion.verificacion import verificar
from application.ports.ia import IaNoDisponible, IClienteIa
from domain.entidades.recomendacion import RecomendacionGuardada

_log = logging.getLogger("cubicador.ia")

# Lo que el modelo contesta cuando no quiere comprometerse. Como la explicacion
# es opcional, es mejor no mostrar nada que mostrar un relleno.
SIN_EXPLICACION = ""


@dataclass(frozen=True)
class Explicacion:
    texto: str
    modelo: str
    tokens_entrada: int
    tokens_salida: int


class ServicioExplicacion:
    def __init__(self, cliente: IClienteIa) -> None:
        self._cliente = cliente

    async def explicar(self, recomendacion: RecomendacionGuardada) -> Explicacion | None:
        """`None` cuando no hay explicacion que mostrar. Nunca levanta."""
        if not recomendacion.vehiculos:
            # Sin flota no hay nada que explicar: el motivo ya viaja en el
            # contrato y es un dato, no una redaccion.
            return None
        try:
            respuesta = await self._cliente.completar(
                prompt.INSTRUCCIONES,
                prompt.construir_mensaje(recomendacion),
                prompt.esquema_salida(),
            )
            texto = _texto(respuesta.json)
        except IaNoDisponible as ex:
            _log.info("Explicacion no disponible (%s): la respuesta sigue igual sin ella.", ex.codigo)
            return None
        except Exception:  # noqa: BLE001 - un texto opcional no puede tumbar una consulta
            _log.exception("Explicacion: fallo inesperado; se responde sin ella.")
            return None

        if not texto:
            return None

        v = verificar(texto, prompt.cifras_permitidas(recomendacion))
        if not v.fiel:
            # Se descarta ENTERA, no la frase: una cifra inventada vuelve
            # sospechoso todo el texto, y no mostrarlo no rompe nada.
            _log.warning(
                "Explicacion descartada: %d cifra(s) que no son del motor (%s).",
                len(v.cifras_inventadas), ", ".join(v.cifras_inventadas))
            return None

        return Explicacion(texto, respuesta.modelo, respuesta.tokens_entrada, respuesta.tokens_salida)


def _texto(crudo: str) -> str:
    import json
    try:
        d = json.loads(crudo)
    except (TypeError, ValueError):
        return SIN_EXPLICACION
    texto = d.get("explicacion") if isinstance(d, dict) else None
    return texto.strip()[:prompt.MAX_EXPLICACION] if isinstance(texto, str) else SIN_EXPLICACION
