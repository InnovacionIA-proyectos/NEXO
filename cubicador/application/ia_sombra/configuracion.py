"""Configuracion del modo sombra de la IA, leida del entorno en cada uso.

Se lee de `os.environ` y no se congela al importar: cambiar una variable en
Dokploy y redesplegar tiene que bastar para apagar la funcion, y las pruebas
cambian el entorno entre casos.

La funcion nace APAGADA: sin `IA_SOMBRA_HABILITADA=true` y sin llave no se crea
ningun cliente ni se toca la red, y la API responde exactamente igual que antes.

La llave es propia del Cubicador (su propia *workspace* de Anthropic, con su
limite de gasto mensual): no se comparte con NEXO. Nunca se escribe en logs ni en
`repr`.
"""

from __future__ import annotations

import os
from collections.abc import Mapping
from dataclasses import dataclass, field

MODELO_POR_DEFECTO = "claude-sonnet-5-5"
ESFUERZO_POR_DEFECTO = "high"
_ESFUERZOS = ("low", "medium", "high", "xhigh", "max")


def _texto(entorno: Mapping[str, str], clave: str) -> str | None:
    valor = (entorno.get(clave) or "").strip()
    return valor or None


def _booleano(entorno: Mapping[str, str], clave: str, defecto: bool) -> bool:
    valor = _texto(entorno, clave)
    return defecto if valor is None else valor.lower() in ("1", "true", "yes", "si", "on")


def _numero(entorno: Mapping[str, str], clave: str, defecto: float, minimo: float, maximo: float) -> float:
    try:
        valor = float(_texto(entorno, clave) or defecto)
    except ValueError:
        valor = defecto
    return min(max(valor, minimo), maximo)


@dataclass(frozen=True)
class ConfiguracionSombra:
    habilitada: bool = False
    api_key: str | None = field(default=None, repr=False)
    workspace_id: str | None = None
    modelo: str = MODELO_POR_DEFECTO
    esfuerzo: str = ESFUERZO_POR_DEFECTO
    # Fraccion de recomendaciones que se comparan (0 a 1): sirve para acotar el gasto sin apagar la funcion.
    muestreo: float = 1.0
    # Tope de tokens (entrada + salida) por dia, hora de Colombia. 0 lo desactiva.
    tope_tokens_dia: int = 1_000_000
    timeout_segundos: float = 60.0
    max_concurrentes: int = 2
    # Cuantas comparaciones pueden esperar su turno; si hay mas, se descartan (la API nunca se encola detras de la IA).
    max_pendientes: int = 20
    max_tokens_salida: int = 4096

    @property
    def activa(self) -> bool:
        return self.habilitada and self.api_key is not None and self.muestreo > 0

    @classmethod
    def desde_entorno(cls, entorno: Mapping[str, str] | None = None) -> ConfiguracionSombra:
        e = os.environ if entorno is None else entorno
        esfuerzo = (_texto(e, "ANTHROPIC_EFFORT") or ESFUERZO_POR_DEFECTO).lower()
        return cls(
            habilitada=_booleano(e, "IA_SOMBRA_HABILITADA", False),
            api_key=_texto(e, "ANTHROPIC_API_KEY"),
            workspace_id=_texto(e, "ANTHROPIC_WORKSPACE_ID"),
            modelo=_texto(e, "ANTHROPIC_MODEL") or MODELO_POR_DEFECTO,
            esfuerzo=esfuerzo if esfuerzo in _ESFUERZOS else ESFUERZO_POR_DEFECTO,
            muestreo=_numero(e, "IA_SOMBRA_MUESTREO", 1.0, 0.0, 1.0),
            tope_tokens_dia=int(_numero(e, "IA_SOMBRA_TOPE_TOKENS_DIA", 1_000_000, 0, 1_000_000_000)),
            timeout_segundos=_numero(e, "IA_SOMBRA_TIMEOUT_SEGUNDOS", 60.0, 5.0, 300.0),
            max_concurrentes=int(_numero(e, "IA_SOMBRA_MAX_CONCURRENTES", 2, 1, 16)),
            max_pendientes=int(_numero(e, "IA_SOMBRA_MAX_PENDIENTES", 20, 1, 500)),
            max_tokens_salida=int(_numero(e, "IA_SOMBRA_MAX_TOKENS_SALIDA", 4096, 256, 32_000)),
        )
