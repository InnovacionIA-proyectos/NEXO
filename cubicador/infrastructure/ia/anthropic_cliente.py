"""Salida hacia Claude con el SDK oficial de Anthropic para Python.

Solo se crea cuando el modo sombra esta encendido y hay llave. Los errores del
proveedor se traducen a `IaNoDisponible` con un codigo estable (los mismos que
usa NEXO); ni los mensajes ni los logs llevan la llave ni el contenido enviado:
se registra modelo, tokens, latencia y resultado.

No se envian `temperature`, `top_p` ni `top_k` (la API los rechaza) ni se fuerza
el uso de una herramienta: la salida es JSON de esquema cerrado
(`output_config.format`) con razonamiento adaptativo segun el esfuerzo.
"""

from __future__ import annotations

import logging
import time

import anthropic

from application.ia_sombra.configuracion import ConfiguracionSombra
from application.ports.ia import IaNoDisponible, RespuestaIa

_log = logging.getLogger("cubicador.ia")


class AnthropicClienteIa:
    def __init__(self, config: ConfiguracionSombra, cliente: anthropic.AsyncAnthropic | None = None) -> None:
        if config.api_key is None:
            raise ValueError("Falta ANTHROPIC_API_KEY.")
        self._config = config
        # La cabecera de workspace solo hace falta con una llave de organizacion; una llave atada a su workspace no la necesita.
        cabeceras = {"anthropic-workspace-id": config.workspace_id} if config.workspace_id else None
        self._cliente = cliente or anthropic.AsyncAnthropic(
            api_key=config.api_key, timeout=config.timeout_segundos, max_retries=1, default_headers=cabeceras)

    async def completar(self, instrucciones: str, mensaje: str, esquema_salida: dict) -> RespuestaIa:
        inicio = time.perf_counter()
        try:
            respuesta = await self._cliente.messages.create(
                model=self._config.modelo,
                max_tokens=self._config.max_tokens_salida,
                system=instrucciones,
                messages=[{"role": "user", "content": mensaje}],
                thinking={"type": "adaptive"},
                output_config={
                    "effort": self._config.esfuerzo,
                    "format": {"type": "json_schema", "schema": esquema_salida},
                },
            )
        except Exception as ex:  # noqa: BLE001 - se traduce a un codigo estable; el detalle se registra sin la llave
            codigo, texto = _traducir(ex)
            _log.warning(
                "IA sombra fallo: %s (%s, %s) tras %d ms. Proveedor: %s", codigo, type(ex).__name__,
                getattr(ex, "status_code", None), _ms(inicio), self._detalle(ex))
            raise IaNoDisponible(codigo, texto) from None
        latencia = _ms(inicio)

        uso = respuesta.usage
        tokens_cache = (getattr(uso, "cache_read_input_tokens", 0) or 0) + (getattr(uso, "cache_creation_input_tokens", 0) or 0)
        if respuesta.stop_reason == "refusal":
            raise IaNoDisponible("IA_RECHAZADA", "El modelo rechazo la peticion.")
        if respuesta.stop_reason == "max_tokens":
            raise IaNoDisponible("IA_RESPUESTA_TRUNCADA", "La respuesta del modelo quedo incompleta.")

        texto = "".join(b.text for b in respuesta.content if getattr(b, "type", None) == "text")
        _log.info(
            "IA sombra modelo %s: %d tokens de entrada, %d de salida, %d ms",
            respuesta.model, uso.input_tokens, uso.output_tokens, latencia)
        return RespuestaIa(texto, str(respuesta.model), uso.input_tokens, uso.output_tokens, latencia, tokens_cache)

    def _detalle(self, ex: Exception) -> str:
        """Lo que respondio el proveedor, sin la llave y recortado."""
        texto = str(ex).replace("\n", " ").replace(self._config.api_key or "\0", "<llave>")
        return texto[:500]


def _ms(inicio: float) -> int:
    return int((time.perf_counter() - inicio) * 1000)


def _traducir(ex: Exception) -> tuple[str, str]:
    if isinstance(ex, anthropic.APITimeoutError):
        return "IA_TIEMPO_AGOTADO", "El proveedor de IA no respondio a tiempo."
    if isinstance(ex, anthropic.RateLimitError):
        return "IA_LIMITE_PROVEEDOR", "El proveedor de IA alcanzo su limite de uso."
    if isinstance(ex, (anthropic.AuthenticationError, anthropic.PermissionDeniedError)):
        return "IA_CREDENCIAL_INVALIDA", "La credencial del proveedor de IA no es valida o no tiene permiso."
    if isinstance(ex, (anthropic.BadRequestError, anthropic.UnprocessableEntityError, anthropic.NotFoundError)):
        return "IA_PETICION_RECHAZADA", "El proveedor de IA rechazo la peticion."
    return "IA_NO_DISPONIBLE", "El proveedor de IA no pudo atender la peticion."
