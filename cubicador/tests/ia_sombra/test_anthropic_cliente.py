"""El adaptador de Anthropic: lo que envia, como traduce los errores y que nunca filtra la llave. No sale a la red."""

from __future__ import annotations

import logging
from types import SimpleNamespace

import anthropic
import httpx
import pytest

from application.ia_sombra.configuracion import ConfiguracionSombra
from application.ports.ia import IaNoDisponible
from infrastructure.ia.anthropic_cliente import AnthropicClienteIa

LLAVE = "sk-ant-api03-llave-secreta-de-prueba"
ESQUEMA = {"type": "object", "properties": {"x": {"type": "string"}}, "required": ["x"], "additionalProperties": False}


def _peticion() -> httpx.Request:
    return httpx.Request("POST", "https://api.anthropic.com/v1/messages")


def _respuesta_http(estado: int) -> httpx.Response:
    return httpx.Response(estado, request=_peticion())


class _Mensajes:
    def __init__(self, resultado) -> None:
        self.resultado = resultado
        self.llamadas: list[dict] = []

    async def create(self, **kwargs):
        self.llamadas.append(kwargs)
        if isinstance(self.resultado, Exception):
            raise self.resultado
        return self.resultado


def _cliente_falso(resultado):
    mensajes = _Mensajes(resultado)
    return SimpleNamespace(messages=mensajes), mensajes


def _ok(texto='{"x":"a"}', stop="end_turn", entrada=120, salida=45, cache_lectura=0, cache_escritura=0):
    return SimpleNamespace(
        content=[SimpleNamespace(type="thinking", thinking=""), SimpleNamespace(type="text", text=texto)],
        usage=SimpleNamespace(input_tokens=entrada, output_tokens=salida, cache_read_input_tokens=cache_lectura,
                              cache_creation_input_tokens=cache_escritura),
        model="claude-sonnet-5-5", stop_reason=stop)


def _adaptador(resultado, **config):
    cfg = ConfiguracionSombra(habilitada=True, api_key=LLAVE, **config)
    falso, mensajes = _cliente_falso(resultado)
    return AnthropicClienteIa(cfg, cliente=falso), mensajes


async def test_envia_el_modelo_el_esfuerzo_y_el_esquema_cerrado_sin_parametros_de_muestreo():
    adaptador, mensajes = _adaptador(_ok(), modelo="claude-sonnet-5-5", esfuerzo="high", max_tokens_salida=2048)

    await adaptador.completar("instrucciones", "mensaje", ESQUEMA)

    (envio,) = mensajes.llamadas
    assert envio["model"] == "claude-sonnet-5-5" and envio["max_tokens"] == 2048
    assert envio["system"] == "instrucciones"
    assert envio["messages"] == [{"role": "user", "content": "mensaje"}]
    assert envio["thinking"] == {"type": "adaptive"}
    assert envio["output_config"] == {"effort": "high", "format": {"type": "json_schema", "schema": ESQUEMA}}
    for prohibido in ("temperature", "top_p", "top_k", "tools", "tool_choice"):
        assert prohibido not in envio, "la API los rechaza o no aplican aqui"


async def test_devuelve_el_json_los_tokens_y_la_latencia():
    adaptador, _ = _adaptador(_ok(entrada=120, salida=45, cache_lectura=30, cache_escritura=10))

    r = await adaptador.completar("i", "m", ESQUEMA)

    assert r.json == '{"x":"a"}' and r.modelo == "claude-sonnet-5-5"
    assert (r.tokens_entrada, r.tokens_salida, r.tokens_cache) == (120, 45, 40)
    assert r.latencia_ms >= 0


@pytest.mark.parametrize("stop,codigo", [("refusal", "IA_RECHAZADA"), ("max_tokens", "IA_RESPUESTA_TRUNCADA")])
async def test_un_rechazo_o_una_respuesta_truncada_no_se_usan(stop, codigo):
    adaptador, _ = _adaptador(_ok(stop=stop))

    with pytest.raises(IaNoDisponible) as ex:
        await adaptador.completar("i", "m", ESQUEMA)

    assert ex.value.codigo == codigo


@pytest.mark.parametrize("error,codigo", [
    (anthropic.RateLimitError("x", response=_respuesta_http(429), body=None), "IA_LIMITE_PROVEEDOR"),
    (anthropic.AuthenticationError("x", response=_respuesta_http(401), body=None), "IA_CREDENCIAL_INVALIDA"),
    (anthropic.PermissionDeniedError("x", response=_respuesta_http(403), body=None), "IA_CREDENCIAL_INVALIDA"),
    (anthropic.BadRequestError("x", response=_respuesta_http(400), body=None), "IA_PETICION_RECHAZADA"),
    (anthropic.NotFoundError("x", response=_respuesta_http(404), body=None), "IA_PETICION_RECHAZADA"),
    (anthropic.InternalServerError("x", response=_respuesta_http(500), body=None), "IA_NO_DISPONIBLE"),
    (anthropic.APITimeoutError(request=_peticion()), "IA_TIEMPO_AGOTADO"),
    (anthropic.APIConnectionError(request=_peticion()), "IA_NO_DISPONIBLE"),
    (RuntimeError("cualquier otra cosa"), "IA_NO_DISPONIBLE"),
])
async def test_los_errores_del_proveedor_se_traducen_a_un_codigo_estable(error, codigo):
    adaptador, _ = _adaptador(error)

    with pytest.raises(IaNoDisponible) as ex:
        await adaptador.completar("i", "m", ESQUEMA)

    assert ex.value.codigo == codigo


async def test_la_llave_y_el_contenido_enviado_no_aparecen_en_el_error_ni_en_los_logs(caplog):
    error = anthropic.BadRequestError(f"invalid x-api-key {LLAVE} en el mensaje secreto", response=_respuesta_http(400), body=None)
    adaptador, _ = _adaptador(error)

    with caplog.at_level(logging.DEBUG, logger="cubicador.ia"), pytest.raises(IaNoDisponible) as ex:
        await adaptador.completar("i", "contenido-que-no-debe-salir", ESQUEMA)

    assert LLAVE not in str(ex.value) and LLAVE not in caplog.text
    assert "contenido-que-no-debe-salir" not in caplog.text
    assert "<llave>" in caplog.text


def test_sin_llave_no_se_puede_crear_el_adaptador():
    with pytest.raises(ValueError):
        AnthropicClienteIa(ConfiguracionSombra(habilitada=True, api_key=None))


def test_el_cliente_real_lleva_la_workspace_solo_cuando_se_configura_y_reintenta_una_vez():
    con = AnthropicClienteIa(ConfiguracionSombra(habilitada=True, api_key=LLAVE, workspace_id="wrkspc_123"))._cliente
    sin = AnthropicClienteIa(ConfiguracionSombra(habilitada=True, api_key=LLAVE))._cliente

    assert con.default_headers.get("anthropic-workspace-id") == "wrkspc_123"
    assert "anthropic-workspace-id" not in sin.default_headers
    assert con.max_retries == 1
