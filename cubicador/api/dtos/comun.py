"""Base Pydantic con camelCase (igual que JsonSerializerDefaults.Web de
NEXO) - ver CONTRATO.md seccion 2 (convenciones generales)."""

from __future__ import annotations

from decimal import Decimal
from typing import Annotated

from pydantic import BaseModel, ConfigDict, PlainSerializer
from pydantic.alias_generators import to_camel

# Decimal que viaja en JSON como NUMERO, no como cadena.
#
# Pydantic serializa `Decimal` como string por defecto, para no perder
# precision. Pero la seccion 2.3 del contrato muestra numeros (`8420.0`,
# `33.68`, `90.0`) y hay dos consumidores: .NET lo tolera porque
# `JsonSerializerDefaults.Web` trae `AllowReadingFromString`, pero cualquier
# front en JavaScript recibiria la cadena "8420.00" donde espera un numero, y
# un `toFixed` o una suma fallarian en silencio.
#
# `when_used="json"` solo afecta la salida serializada: adentro sigue siendo
# `Decimal`, asi que el redondeo del contrato se decide en el router y no en el
# float. Los valores del contrato tienen 4 decimales como mucho - muy dentro de
# lo que un double representa exacto.
DecimalJson = Annotated[
    Decimal, PlainSerializer(float, return_type=float, when_used="json")
]


class ApiModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, from_attributes=True)


class ErrorResponse(ApiModel):
    codigo: str
    mensaje: str
