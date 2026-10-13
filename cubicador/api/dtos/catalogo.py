"""DTOs de los catalogos publicos (S5-HU-17.4, seccion 2.7 del contrato).

`GET /tipos-mercancia` y `GET /tipos-carroceria` son de solo lectura: NEXO los
proyecta por eventos y Cotifly los consulta con cache. Las escrituras son los
CRUD de S5-HU-17.5.

Devuelven **id y codigo**. Cotifly decide cual persiste de su lado, pero en el
cable del request de recomendacion viaja el id.
"""

from __future__ import annotations

from .comun import ApiModel


class TipoCarroceriaDto(ApiModel):
    id: str
    codigo_carroceria: str
    nombre_carroceria: str
    activo: bool


class TipoMercanciaDto(ApiModel):
    id: str
    codigo_mercancia: str
    nombre_mercancia: str
    categoria: str
    cubicable: bool
    condicionada_cubicacion: bool
    # Codigo de carroceria, no id: es lo legible en un log y lo que el
    # consumidor cruza contra `GET /tipos-carroceria`.
    carroceria_principal: str | None
    carroceria_secundaria: str | None
    carroceria_secundaria_condicionada: bool
    # NINGUNO | REFRIGERADO | VENTILADO_CONDICIONAL | CLIMATIZADO_CONDICIONAL |
    # REFRIGERADO_O_CLIMATIZADO | VENTILADO_O_REFRIGERADO
    control_ambiental_requerido: str
    apilable: bool
    fragil: bool
    rotacion_libre: bool
    comentario_condicion: str | None
    activo: bool
