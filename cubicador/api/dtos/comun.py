"""Base Pydantic con camelCase (igual que JsonSerializerDefaults.Web de
NEXO) - ver CONTRATO.md seccion 2 (convenciones generales)."""

from __future__ import annotations

from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel


class ApiModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, from_attributes=True)


class ErrorResponse(ApiModel):
    codigo: str
    mensaje: str
