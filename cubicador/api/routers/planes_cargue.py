"""Router `/api/planes-cargue` - vehiculos, planes, items, optimizar,
ajustar, resultado, flota, demo, puente desde linea. Ver docs/CONTRATOS.md
seccion A. `optimizar`/`reoptimizar` y `desde-linea` publican a RabbitMQ y
responden 202 de inmediato (ver docs/DECISIONES_TECNICAS.md #5) - el worker
(`worker/main.py`) es quien corre el caso de uso pesado de verdad. El ajuste
manual (`PATCH .../colocaciones/{id}`) sigue siempre sincrono a proposito."""

from __future__ import annotations

from fastapi import APIRouter, Depends

from api.dependencias import (
    obtener_eventos_publisher,
    obtener_plan_repo,
    obtener_vehiculo_repo,
    verificar_api_key,
)
from api.dtos.plan_cargue import (
    AjusteColocacionInput as AjusteColocacionInputDto,
    CrearPlanRequest,
    GuardarItemPlanCargueInput as GuardarItemPlanCargueInputDto,
    GuardarVehiculoInput as GuardarVehiculoInputDto,
    OptimizarRequestDto,
    PlanCargueDto,
    PlanFlotaItemDto,
    ResultadoPlanDto,
    SolicitudEncoladaDto,
    VehiculoCubicacionDto,
    ItemPlanCargueDto,
)
from application.plan_cargue import ajustar as ajustar_uc
from application.plan_cargue import demo as demo_uc
from application.plan_cargue import items as items_uc
from application.plan_cargue import planes as planes_uc
from application.plan_cargue import vehiculos as vehiculos_uc
from application.plan_cargue.resultado import obtener_resultado
from domain.entidades.entidades import EjeVehiculoRow, ItemPlanCargue, VehiculoCubicacion

router = APIRouter(prefix="/api/planes-cargue", tags=["plan-cargue"], dependencies=[Depends(verificar_api_key)])


# ------------------------------------------------------------- mapeo Pydantic <-> dominio


def _vehiculo_de_input(v: GuardarVehiculoInputDto) -> VehiculoCubicacion:
    kwargs = dict(
        nombre=v.nombre, descripcion=v.descripcion,
        largo_interno_mm=v.largo_interno_mm, ancho_interno_mm=v.ancho_interno_mm, alto_interno_mm=v.alto_interno_mm,
        capacidad_kg=v.capacidad_kg, tara_kg=v.tara_kg,
        puerta_ancho_mm=v.puerta_ancho_mm, puerta_alto_mm=v.puerta_alto_mm, puerta_posicion=v.puerta_posicion,
        ejes=[EjeVehiculoRow(e.posicion_mm, e.capacidad_kg) for e in v.ejes],
        tolerancia_paredes_mm=v.tolerancia_paredes_mm, tolerancia_entre_unidades_mm=v.tolerancia_entre_unidades_mm,
    )
    return VehiculoCubicacion(id=v.id, **kwargs) if v.id else VehiculoCubicacion(**kwargs)


def _item_de_input(i: GuardarItemPlanCargueInputDto, plan_id: str) -> ItemPlanCargue:
    kwargs = dict(
        plan_cargue_id=plan_id, sku=i.sku, descripcion=i.descripcion, pedido=i.pedido, cliente=i.cliente,
        destino=i.destino, secuencia_entrega=i.secuencia_entrega, largo_mm=i.largo_mm, ancho_mm=i.ancho_mm,
        alto_mm=i.alto_mm, peso_kg=i.peso_kg, cantidad=i.cantidad, color=i.color, prioridad=i.prioridad,
        tipo_embalaje=i.tipo_embalaje, apilable=i.apilable, max_niveles=i.max_niveles,
        max_peso_encima_kg=i.max_peso_encima_kg, rotable=i.rotable, mantener_vertical=i.mantener_vertical,
        fragil=i.fragil, peligroso=i.peligroso, refrigerado=i.refrigerado,
        grupo_compatibilidad=i.grupo_compatibilidad, observaciones=i.observaciones,
    )
    return ItemPlanCargue(id=i.id, **kwargs) if i.id else ItemPlanCargue(**kwargs)


def _resultado_a_dto(r) -> ResultadoPlanDto:
    return ResultadoPlanDto.model_validate(r)


# ------------------------------------------------------------- vehiculos


@router.get("/vehiculos", response_model=list[VehiculoCubicacionDto])
async def vehiculos(repo=Depends(obtener_vehiculo_repo)):
    return [VehiculoCubicacionDto.model_validate(v) for v in await vehiculos_uc.listar_vehiculos(repo)]


@router.post("/vehiculos", response_model=VehiculoCubicacionDto)
async def guardar_vehiculo(v: GuardarVehiculoInputDto, repo=Depends(obtener_vehiculo_repo)):
    guardado = await vehiculos_uc.guardar_vehiculo(repo, _vehiculo_de_input(v))
    return VehiculoCubicacionDto.model_validate(guardado)


@router.delete("/vehiculos/{id}", status_code=204)
async def eliminar_vehiculo(id: str, repo=Depends(obtener_vehiculo_repo)):
    await vehiculos_uc.eliminar_vehiculo(repo, id)


# ------------------------------------------------------------- planes


@router.get("", response_model=list[PlanCargueDto])
async def planes(repo=Depends(obtener_plan_repo)):
    return [PlanCargueDto.model_validate(p) for p in await planes_uc.listar_planes(repo)]


@router.post("", response_model=PlanCargueDto)
async def crear_plan(req: CrearPlanRequest, repo=Depends(obtener_plan_repo)):
    return PlanCargueDto.model_validate(await planes_uc.crear_plan(repo, req.nombre, req.vehiculo_id))


@router.get("/{id}", response_model=PlanCargueDto)
async def obtener_plan(id: str, repo=Depends(obtener_plan_repo)):
    return PlanCargueDto.model_validate(await planes_uc.obtener_plan(repo, id))


@router.get("/{id}/flota", response_model=list[PlanFlotaItemDto])
async def flota(id: str, repo=Depends(obtener_plan_repo)):
    return [PlanFlotaItemDto.model_validate(f) for f in await planes_uc.obtener_flota(repo, id)]


@router.delete("/{id}", status_code=204)
async def eliminar_plan(id: str, repo=Depends(obtener_plan_repo)):
    await planes_uc.eliminar_plan(repo, id)


@router.post("/demo", response_model=PlanCargueDto)
async def crear_demo(planes_repo=Depends(obtener_plan_repo), vehiculos_repo=Depends(obtener_vehiculo_repo)):
    return PlanCargueDto.model_validate(await demo_uc.crear_plan_demo(planes_repo, vehiculos_repo))


@router.post("/desde-linea/{linea_solicitud_id}", response_model=SolicitudEncoladaDto, status_code=202)
async def crear_desde_linea(linea_solicitud_id: str, publisher=Depends(obtener_eventos_publisher)):
    # No hay un plan que marcar "procesando" todavia (esta operacion CREA el
    # plan/flota) - se usa linea_solicitud_id como correlationId; el caller
    # consulta el resultado con GET .../por-linea/{linea_solicitud_id}.
    await publisher.publicar_cubicacion_solicitada(
        linea_solicitud_id, "crear_desde_linea", {"lineaSolicitudId": linea_solicitud_id},
    )
    return SolicitudEncoladaDto(correlation_id=linea_solicitud_id)


@router.get("/por-linea/{linea_solicitud_id}", response_model=list[PlanFlotaItemDto])
async def por_linea(linea_solicitud_id: str, repo=Depends(obtener_plan_repo)):
    """Lista vacia mientras `crear_desde_linea` sigue en proceso (ver POST de arriba)."""
    return [PlanFlotaItemDto.model_validate(f) for f in await repo.listar_por_linea(linea_solicitud_id)]


# ------------------------------------------------------------- items


@router.post("/{id}/items", response_model=ItemPlanCargueDto)
async def guardar_item(id: str, item: GuardarItemPlanCargueInputDto, repo=Depends(obtener_plan_repo)):
    guardado = await items_uc.guardar_item(repo, id, item.id, _item_de_input(item, id))
    return ItemPlanCargueDto.model_validate(guardado)


@router.post("/{id}/items/importar", response_model=list[ItemPlanCargueDto])
async def importar_items(id: str, items: list[GuardarItemPlanCargueInputDto], repo=Depends(obtener_plan_repo)):
    entidades = [_item_de_input(i, id) for i in items]
    guardados = await items_uc.importar_items(repo, id, entidades)
    return [ItemPlanCargueDto.model_validate(g) for g in guardados]


@router.delete("/{id}/items/{item_id}", status_code=204)
async def eliminar_item(id: str, item_id: str, repo=Depends(obtener_plan_repo)):
    await items_uc.eliminar_item(repo, id, item_id)


# ------------------------------------------------------------- optimizacion y resultado


@router.post("/{id}/optimizar", response_model=PlanCargueDto, status_code=202)
async def optimizar(
    id: str, req: OptimizarRequestDto,
    planes_repo=Depends(obtener_plan_repo), publisher=Depends(obtener_eventos_publisher),
):
    await planes_uc.obtener_plan(planes_repo, id)  # 404 rapido si el plan no existe, antes de encolar
    solicitud = {
        "estrategia": req.estrategia, "algoritmo": req.algoritmo, "semilla": req.semilla,
        "ponderaciones": req.ponderaciones.model_dump() if req.ponderaciones else None,
        "respetarBloqueadas": req.respetar_bloqueadas,
    }
    await planes_repo.marcar_procesando(id)
    await publisher.publicar_cubicacion_solicitada(id, "optimizar", solicitud)
    return PlanCargueDto.model_validate(await planes_uc.obtener_plan(planes_repo, id))


@router.post("/{id}/reoptimizar", response_model=PlanCargueDto, status_code=202)
async def reoptimizar(
    id: str, req: OptimizarRequestDto,
    planes_repo=Depends(obtener_plan_repo), publisher=Depends(obtener_eventos_publisher),
):
    return await optimizar(id, req.model_copy(update={"respetar_bloqueadas": True}), planes_repo, publisher)


@router.get("/{id}/resultado", response_model=ResultadoPlanDto)
async def resultado(id: str, planes_repo=Depends(obtener_plan_repo), vehiculos_repo=Depends(obtener_vehiculo_repo)):
    return _resultado_a_dto(await obtener_resultado(planes_repo, vehiculos_repo, id))


@router.get("/{id}/colocaciones")
async def colocaciones(id: str, planes_repo=Depends(obtener_plan_repo), vehiculos_repo=Depends(obtener_vehiculo_repo)):
    return _resultado_a_dto(await obtener_resultado(planes_repo, vehiculos_repo, id)).colocaciones


@router.get("/{id}/metricas")
async def metricas(id: str, planes_repo=Depends(obtener_plan_repo), vehiculos_repo=Depends(obtener_vehiculo_repo)):
    return _resultado_a_dto(await obtener_resultado(planes_repo, vehiculos_repo, id)).metricas


@router.get("/{id}/no-cargadas")
async def no_cargadas(id: str, planes_repo=Depends(obtener_plan_repo), vehiculos_repo=Depends(obtener_vehiculo_repo)):
    r = _resultado_a_dto(await obtener_resultado(planes_repo, vehiculos_repo, id))
    return [c for c in r.colocaciones if not c.cargada]


@router.patch("/{id}/colocaciones/{colocacion_id}", response_model=ResultadoPlanDto)
async def ajustar(
    id: str, colocacion_id: str, ajuste: AjusteColocacionInputDto,
    planes_repo=Depends(obtener_plan_repo), vehiculos_repo=Depends(obtener_vehiculo_repo),
):
    ajuste_dominio = ajustar_uc.AjusteColocacionInput(
        x=ajuste.x, y=ajuste.y, z=ajuste.z, orientacion_indice=ajuste.orientacion_indice,
        bloqueada=ajuste.bloqueada, retirar=bool(ajuste.retirar), reinsertar=bool(ajuste.reinsertar),
    )
    resultado = await ajustar_uc.ajustar_colocacion(planes_repo, vehiculos_repo, id, colocacion_id, ajuste_dominio)
    return _resultado_a_dto(resultado)


@router.get("/{id}/exportar", response_model=ResultadoPlanDto)
async def exportar(id: str, planes_repo=Depends(obtener_plan_repo), vehiculos_repo=Depends(obtener_vehiculo_repo)):
    return _resultado_a_dto(await obtener_resultado(planes_repo, vehiculos_repo, id))
