"""Servicio de interpretacion: texto libre -> posiciones validadas.

Es **sincrono y a la vista**, al reves del modo sombra: ahi la comparacion corre
en segundo plano porque a nadie le importa esperarla, y aca el resultado ES lo
que se pidio. Por eso tampoco tiene muestreo ni se descarta bajo carga: o
responde o falla, y el que llama se entera.

Lo que NO hace, a proposito:
  - no decide vehiculos ni si la carga cabe -eso es el motor-,
  - no marca extradimensionado: entrega la posicion y el motor dictamina,
  - no completa ningun dato que el texto no diera (ver `lectura`).
"""

from __future__ import annotations

import logging
from dataclasses import dataclass

from application.interpretacion import prompt
from application.interpretacion.lectura import Interpretacion, RespuestaInvalida, leer_interpretacion
from application.ports.ia import IaNoDisponible, IClienteIa
from domain.entidades.catalogo import Catalogo

_log = logging.getLogger("cubicador.ia")


@dataclass(frozen=True)
class Medicion:
    """Lo que costo la llamada. Lo usa el corredor de evaluacion para sus topes."""

    modelo: str
    tokens_entrada: int
    tokens_salida: int
    latencia_ms: int


@dataclass(frozen=True)
class ResultadoInterpretacion:
    interpretacion: Interpretacion
    medicion: Medicion


class TextoVacio(ValueError):
    """No hay nada que interpretar. Se decide aca, sin gastar una llamada."""


class ServicioInterpretacion:
    def __init__(self, cliente: IClienteIa, catalogo: Catalogo) -> None:
        self._cliente = cliente
        self._catalogo = catalogo

    async def interpretar(self, texto: str) -> ResultadoInterpretacion:
        """Levanta `TextoVacio`, `IaNoDisponible` o `RespuestaInvalida`; nunca devuelve a medias.

        Se dejan subir en vez de envolverlas en un resultado vacio porque son
        tres cosas distintas -no escribiste nada, el proveedor fallo, el modelo
        respondio algo que no cumple el contrato- y quien llame decide distinto
        en cada una. Un rechazo de negocio, en cambio, SI viaja dentro de la
        interpretacion: eso no es un fallo, es la respuesta.
        """
        if not texto or not texto.strip():
            raise TextoVacio("No hay descripcion que interpretar.")

        respuesta = await self._cliente.completar(
            prompt.INSTRUCCIONES,
            prompt.construir_mensaje(self._catalogo, texto),
            prompt.esquema_salida(self._catalogo),
        )
        interpretacion = leer_interpretacion(respuesta.json, self._catalogo)

        # Sin el texto ni lo interpretado: el log no es sitio para la carga de
        # un cliente (ADR seccion 3, "el contenido enviado no se guarda").
        _log.info(
            "Interpretacion: %d posiciones, %d rechazos, %d tokens de entrada, %d de salida, %d ms",
            len(interpretacion.posiciones), len(interpretacion.rechazos),
            respuesta.tokens_entrada, respuesta.tokens_salida, respuesta.latencia_ms)

        return ResultadoInterpretacion(
            interpretacion,
            Medicion(respuesta.modelo, respuesta.tokens_entrada, respuesta.tokens_salida, respuesta.latencia_ms),
        )


__all__ = [
    "IaNoDisponible", "Interpretacion", "Medicion", "RespuestaInvalida",
    "ResultadoInterpretacion", "ServicioInterpretacion", "TextoVacio",
]
