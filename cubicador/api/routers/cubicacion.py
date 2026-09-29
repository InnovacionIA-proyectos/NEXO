"""Router `/api/cubicacion` - cubicador rapido + medidas estandar. Ver
CONTRATO.md seccion 3.1. `POST /costear` del original NO se migro - ver
README.md."""

from __future__ import annotations

from fastapi import APIRouter, Depends

from api.dependencias import obtener_parametros_repo, verificar_api_key
from api.dtos.cubicacion_rapida import CubicacionRequestDto, CubicacionResponseDto, MedidasEstandarDto, UnidadCubicacionDto
from application.cubicacion_rapida import casos_de_uso as cu
from domain.motor import cubicacion_rapida as motor

router = APIRouter(prefix="/api/cubicacion", tags=["cubicacion-rapida"], dependencies=[Depends(verificar_api_key)])


def _a_request_dominio(req: CubicacionRequestDto) -> motor.CubicacionRequest:
    return motor.CubicacionRequest(
        items=[
            motor.ItemCubicacion(
                descripcion=i.descripcion, cantidad=i.cantidad, largo_cm=i.largo_cm,
                ancho_cm=i.ancho_cm, alto_cm=i.alto_cm, peso_kg=i.peso_kg, tipo_mercancia=i.tipo_mercancia,
            )
            for i in req.items
        ],
        unidad_ids=req.unidad_ids, clase_viaje=req.clase_viaje,
    )


@router.post("", response_model=CubicacionResponseDto)
async def cubicar(request: CubicacionRequestDto, parametros_repo=Depends(obtener_parametros_repo)):
    respuesta = await cu.evaluar_cubicacion(parametros_repo, _a_request_dominio(request))
    return CubicacionResponseDto.model_validate(respuesta)


@router.get("/unidades", response_model=list[UnidadCubicacionDto])
async def unidades():
    return [UnidadCubicacionDto.model_validate(u) for u in cu.catalogo_unidades()]


@router.get("/medidas-estandar", response_model=MedidasEstandarDto)
async def medidas_estandar(parametros_repo=Depends(obtener_parametros_repo)):
    return MedidasEstandarDto.model_validate(await cu.obtener_medidas_estandar(parametros_repo))


@router.put("/medidas-estandar", response_model=MedidasEstandarDto)
async def actualizar_medidas(req: MedidasEstandarDto, parametros_repo=Depends(obtener_parametros_repo)):
    medidas = motor.MedidasEstandar(req.largo_max_cm, req.ancho_max_cm, req.alto_max_cm, req.peso_max_kg)
    return MedidasEstandarDto.model_validate(await cu.actualizar_medidas_estandar(parametros_repo, medidas))
