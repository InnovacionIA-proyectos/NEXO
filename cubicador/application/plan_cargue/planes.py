"""Casos de uso de alta/consulta de planes de cargue (CRUD simple, sin
optimizacion - ver optimizar.py y ajustar.py para lo pesado)."""

from __future__ import annotations

from application.excepciones import NotFoundException, ValidationFailedException
from application.ports.repositorios import IPlanCargueRepository
from domain.entidades.entidades import PlanCargue


async def crear_plan(
    repo: IPlanCargueRepository, nombre: str, vehiculo_id: str, *, id: str | None = None,
) -> PlanCargue:
    # `id`: S3-HU-02 (issue #105) - cuando el comando "crear_plan" viene de NEXO (que
    # genera el id, ver S3-HU-01.2), la fila se crea con ESE id, nunca uno propio.
    # Sigue siendo None para el POST sincrono directo contra Cubicador.
    if not await repo.vehiculo_existe(vehiculo_id):
        raise ValidationFailedException("VEHICULO_INVALIDO", "El vehiculo indicado no existe.")
    return await repo.crear(nombre.strip(), vehiculo_id, id=id)


async def obtener_plan(repo: IPlanCargueRepository, id: str) -> PlanCargue:
    plan = await repo.obtener(id)
    if plan is None:
        raise NotFoundException("PLAN_NO_ENCONTRADO", "Plan de cargue no encontrado.")
    return plan


async def listar_planes(repo: IPlanCargueRepository) -> list[PlanCargue]:
    return await repo.listar_recientes(100)


async def eliminar_plan(repo: IPlanCargueRepository, id: str) -> None:
    await repo.eliminar(id)


async def obtener_flota(repo: IPlanCargueRepository, plan_id: str) -> list:
    return await repo.flota(plan_id)
