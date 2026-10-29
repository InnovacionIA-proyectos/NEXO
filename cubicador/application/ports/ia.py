"""Puerto hacia el modelo de lenguaje. Lo implementa `infrastructure/ia/`.

Solo lo usa el modo sombra: la respuesta de la API al usuario nunca depende de
este puerto.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Protocol


class IaNoDisponible(Exception):
    """El proveedor no pudo atender la peticion. `codigo` es estable (IA_*) y es lo que se registra."""

    def __init__(self, codigo: str, mensaje: str) -> None:
        super().__init__(mensaje)
        self.codigo = codigo


@dataclass(frozen=True)
class RespuestaIa:
    """Lo que devolvio el modelo: el JSON de su salida estructurada y lo que costo."""

    json: str
    modelo: str
    tokens_entrada: int
    tokens_salida: int
    latencia_ms: int
    # Tokens de cache: se cobran distinto pero cuentan para el tope diario.
    tokens_cache: int = 0


class IClienteIa(Protocol):
    async def completar(self, instrucciones: str, mensaje: str, esquema_salida: dict) -> RespuestaIa:
        """Una peticion con salida JSON de esquema cerrado. Levanta `IaNoDisponible` si el proveedor falla."""
        ...
