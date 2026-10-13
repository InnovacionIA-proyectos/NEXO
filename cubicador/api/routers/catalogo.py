"""Catalogos publicos y medidas estandar (S5-HU-17.4).

`GET /tipos-mercancia` y `GET /tipos-carroceria` son la seccion 2.7 del
contrato: solo lectura. NEXO proyecta estos catalogos por eventos y Cotifly los
consulta con cache. Las escrituras son los CRUD de S5-HU-17.5.

`/medidas-estandar` vivia bajo `/api/cubicacion`, que se retira en este corte.
Se muda aca sin cambios de comportamiento: sigue siendo la unica forma de
editar los topes contra los que se detecta extradimension, que el contrato
nuevo sigue devolviendo en la seccion 2.4.

Van sin prefijo comun porque son tres rutas de primer nivel distintas.
"""

from __future__ import annotations

from decimal import Decimal

from fastapi import APIRouter, Depends

from api.dependencias import (
    obtener_catalogo_repo,
    obtener_parametros_repo,
    verificar_api_key,
)
from api.dtos.catalogo import TipoCarroceriaDto, TipoMercanciaDto
from api.dtos.recomendaciones import MedidasEstandarDto
from application.parametros import casos_de_uso as cu_parametros
from domain.entidades.entidades import ParametrosCubicacion

router = APIRouter(tags=["catalogo"], dependencies=[Depends(verificar_api_key)])


@router.get("/tipos-carroceria", response_model=list[TipoCarroceriaDto])
async def tipos_carroceria(catalogo_repo=Depends(obtener_catalogo_repo)):
    catalogo = await catalogo_repo.cargar()
    return [
        TipoCarroceriaDto(
            id=c.id, codigo_carroceria=c.codigo, nombre_carroceria=c.nombre, activo=c.activa)
        for c in catalogo.carrocerias
    ]


@router.get("/tipos-mercancia", response_model=list[TipoMercanciaDto])
async def tipos_mercancia(catalogo_repo=Depends(obtener_catalogo_repo)):
    catalogo = await catalogo_repo.cargar()
    return [
        TipoMercanciaDto(
            id=m.id,
            codigo_mercancia=m.codigo,
            nombre_mercancia=m.nombre,
            categoria=m.categoria.nombre,
            cubicable=m.cubicable,
            condicionada_cubicacion=m.condicionada,
            carroceria_principal=m.carroceria_principal.codigo if m.carroceria_principal else None,
            carroceria_secundaria=m.carroceria_secundaria.codigo if m.carroceria_secundaria else None,
            carroceria_secundaria_condicionada=m.secundaria_condicionada,
            control_ambiental_requerido=m.control_ambiental,
            apilable=m.apilable,
            fragil=m.fragil,
            rotacion_libre=m.rotacion_libre,
            comentario_condicion=m.comentario_condicion,
            activo=m.activo,
        )
        for m in catalogo.mercancias
    ]


def _a_dto(parametros: ParametrosCubicacion) -> MedidasEstandarDto:
    return MedidasEstandarDto(
        largo_max_cm=Decimal(str(parametros.largo_max_cm)),
        ancho_max_cm=Decimal(str(parametros.ancho_max_cm)),
        alto_max_cm=Decimal(str(parametros.alto_max_cm)),
        peso_max_kg=Decimal(str(parametros.peso_max_kg)),
    )


@router.get("/medidas-estandar", response_model=MedidasEstandarDto)
async def medidas_estandar(parametros_repo=Depends(obtener_parametros_repo)):
    return _a_dto(await cu_parametros.obtener_medidas_estandar(parametros_repo))


@router.put("/medidas-estandar", response_model=MedidasEstandarDto)
async def actualizar_medidas(
    peticion: MedidasEstandarDto, parametros_repo=Depends(obtener_parametros_repo),
):
    medidas = ParametrosCubicacion(
        largo_max_cm=float(peticion.largo_max_cm), ancho_max_cm=float(peticion.ancho_max_cm),
        alto_max_cm=float(peticion.alto_max_cm), peso_max_kg=float(peticion.peso_max_kg),
    )
    return _a_dto(await cu_parametros.actualizar_medidas_estandar(parametros_repo, medidas))
