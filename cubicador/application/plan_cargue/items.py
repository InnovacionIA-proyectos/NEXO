"""Casos de uso de items de un plan de cargue (alta/importacion/borrado)."""

from __future__ import annotations

from application.excepciones import ValidationFailedException
from application.ports.repositorios import IPlanCargueRepository
from domain.entidades.entidades import ItemPlanCargue


def _validar(i: ItemPlanCargue) -> None:
    if i.largo_mm <= 0 or i.ancho_mm <= 0 or i.alto_mm <= 0:
        raise ValidationFailedException("DIMENSIONES_INVALIDAS", f"'{i.descripcion}': dimensiones deben ser positivas.")
    if i.largo_mm > 30000 or i.ancho_mm > 30000 or i.alto_mm > 30000:
        raise ValidationFailedException("DIMENSIONES_INVALIDAS", f"'{i.descripcion}': dimensiones fuera de rango.")
    if i.peso_kg <= 0 or i.peso_kg > 100_000:
        raise ValidationFailedException("PESO_INVALIDO", f"'{i.descripcion}': peso invalido.")
    if not (1 <= i.cantidad <= 2000):
        raise ValidationFailedException("CANTIDAD_INVALIDA", f"'{i.descripcion}': cantidad entre 1 y 2.000.")


async def guardar_item(
    repo: IPlanCargueRepository, plan_id: str, item_id: str | None, item: ItemPlanCargue,
) -> ItemPlanCargue:
    _validar(item)
    return await repo.guardar_item(plan_id, item_id, item)


async def importar_items(
    repo: IPlanCargueRepository, plan_id: str, items: list[ItemPlanCargue],
) -> list[ItemPlanCargue]:
    if not (1 <= len(items) <= 2000):
        raise ValidationFailedException("IMPORTACION_INVALIDA", "Entre 1 y 2.000 unidades por importacion.")
    guardados = []
    for item in items:
        _validar(item)
        guardados.append(await repo.guardar_item(plan_id, None, item))
    return guardados


async def eliminar_item(repo: IPlanCargueRepository, plan_id: str, item_id: str) -> None:
    await repo.eliminar_item(plan_id, item_id)
