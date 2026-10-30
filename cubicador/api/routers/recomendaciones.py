"""Router `/recomendaciones` - el contrato unico (S5-HU-17.4).

Especificacion completa en la seccion 2 de `docs/HOJA-DE-RUTA-CUBICADOR.md`.

**Reemplaza a `POST /api/cubicacion`**, que se retira en este mismo corte: no
queda endpoint de transicion. NEXO y Cotifly apuntan aca en el release que lo
publica.

El `origenSistema` sale de la **llave** (`verificar_origen_sistema`), no del
cuerpo. Si el cuerpo lo declara y no coincide, 401.
"""

from __future__ import annotations

from decimal import Decimal

from fastapi import APIRouter, Depends, Query

from api.dependencias import (
    obtener_servicio_explicacion,
    exigir_origen_declarado,
    obtener_catalogo_repo,
    obtener_parametros_repo,
    obtener_recomendacion_repo,
    obtener_servicio_sombra,
    verificar_origen_sistema,
)
from api.dtos.recomendaciones import (
    ExplicacionDto,
    CrearRecomendacionDto,
    ExtradimensionDto,
    MedidasEstandarDto,
    MercanciaDeVehiculoDto,
    PiezaFueraDeMedidaDto,
    RecomendacionDto,
    VehiculoRecomendadoDto,
)
from application.recomendaciones import casos_de_uso as cu
from domain.entidades.recomendacion import MercanciaRecomendada, RecomendacionGuardada

router = APIRouter(prefix="/recomendaciones", tags=["recomendaciones"])


# Escala fija del contrato. Hace falta porque los dos caminos que llegan a este
# DTO traen la misma cifra con distinta precision: en el POST sale del motor
# (quantize propio) y en la reconsulta sale de la columna, que tiene su propia
# escala - `VolumenTotalM3` es DECIMAL(18,3) y `PorcentajeLlenadoVolumen`
# DECIMAL(5,2). Sin normalizar, un consumidor que guarda la respuesta del POST
# y despues reconsulta recibe "9.60" y "9.600" para el mismo volumen.
_DOS_DECIMALES = Decimal("0.01")
_UN_DECIMAL = Decimal("0.1")


def _mercancia(m: MercanciaRecomendada) -> MercanciaDeVehiculoDto:
    return MercanciaDeVehiculoDto(
        indice_posicion=m.indice,
        id_tipo_mercancia=m.id_tipo_mercancia, codigo_mercancia=m.codigo_mercancia,
        cantidad=m.cantidad,
        peso_kg=m.peso_kg.quantize(_DOS_DECIMALES),
        volumen_m3=m.volumen_m3.quantize(_DOS_DECIMALES),
    )


def _a_dto(guardada: RecomendacionGuardada) -> RecomendacionDto:
    extradimension = None
    if guardada.extradimension is not None:
        e = guardada.extradimension
        extradimension = ExtradimensionDto(
            hay_extradimension=e.hay_extradimension,
            medidas_estandar=MedidasEstandarDto(
                largo_max_cm=e.largo_max_cm, ancho_max_cm=e.ancho_max_cm,
                alto_max_cm=e.alto_max_cm, peso_max_kg=e.peso_max_kg),
            piezas=[
                PiezaFueraDeMedidaDto(
                    indice_posicion=p.indice,
                    id_tipo_mercancia=p.id_tipo_mercancia,
                    codigo_mercancia=p.codigo_mercancia, excesos=list(p.excesos))
                for p in e.piezas
            ],
        )

    return RecomendacionDto(
        id_recomendacion=guardada.id,
        id_linea_solicitud=guardada.id_linea_solicitud,
        origen_sistema=guardada.origen_sistema,
        resultado=guardada.resultado,
        motivo_sin_seleccion=guardada.motivo_sin_seleccion,
        condicionada=guardada.condicionada,
        mercancias_condicionadas=list(guardada.mercancias_condicionadas),
        vehiculos=[
            VehiculoRecomendadoDto(
                id_recomendacion_vehiculo=v.id, numero_vehiculo=v.numero,
                codigo_vehiculo=v.codigo_vehiculo, nombre_vehiculo=v.nombre_vehiculo,
                codigo_carroceria=v.codigo_carroceria,
                peso_total_kg=v.peso_total_kg.quantize(_DOS_DECIMALES),
                volumen_total_m3=v.volumen_total_m3.quantize(_DOS_DECIMALES),
                porcentaje_llenado_volumen=v.porcentaje_llenado_volumen.quantize(_UN_DECIMAL),
                porcentaje_llenado_peso=v.porcentaje_llenado_peso.quantize(_UN_DECIMAL),
                alerta_bajo_llenado=v.alerta_bajo_llenado, heterogenea=v.heterogenea,
                mercancias=[_mercancia(m) for m in v.mercancias],
            )
            for v in guardada.vehiculos
        ],
        advertencias=list(guardada.advertencias),
        extradimension=extradimension,
    )


@router.post("", response_model=RecomendacionDto)
async def crear(
    peticion: CrearRecomendacionDto,
    origen_sistema: str = Depends(verificar_origen_sistema),
    catalogo_repo=Depends(obtener_catalogo_repo),
    parametros_repo=Depends(obtener_parametros_repo),
    recomendacion_repo=Depends(obtener_recomendacion_repo),
    servicio_sombra=Depends(obtener_servicio_sombra),
):
    exigir_origen_declarado(peticion.origen_sistema, origen_sistema)

    solicitud = cu.SolicitudDeRecomendacion(
        id_linea_solicitud=peticion.id_linea_solicitud,
        origen_sistema=origen_sistema,
        mercancias=[
            cu.LineaMercancia(
                codigo_mercancia=m.codigo_mercancia, cantidad=m.cantidad,
                largo_cm=m.largo_cm, ancho_cm=m.ancho_cm, alto_cm=m.alto_cm,
                peso_kg=m.peso_kg,
            )
            for m in peticion.mercancias
        ],
        clase_viaje_preferida=peticion.clase_viaje_seleccionada,
        usuario_id=peticion.usuario_id,
    )
    guardada = await cu.crear_recomendacion(
        catalogo_repo, parametros_repo, recomendacion_repo, solicitud)
    # Modo sombra: agenda la comparacion con el modelo y regresa en el acto; la respuesta de abajo no depende de ella.
    servicio_sombra.programar(guardada, solicitud.mercancias, solicitud.clase_viaje_preferida)
    return _a_dto(guardada)


@router.get("", response_model=RecomendacionDto)
async def consultar(
    id_linea_solicitud: str = Query(alias="idLineaSolicitud"),
    origen_sistema_consultado: str = Query(alias="origenSistema"),
    _: str = Depends(verificar_origen_sistema),
    recomendacion_repo=Depends(obtener_recomendacion_repo),
):
    """La ultima corrida de una linea.

    `origenSistema` es **obligatorio en la query**, no se toma de la llave: un
    sistema puede necesitar leer lo que recomendo el otro (NEXO consulta lo de
    Cotifly al recibir la solicitud), y un mismo codigo de linea puede existir
    en los dos.
    """
    guardada = await cu.consultar_recomendacion(
        recomendacion_repo, id_linea_solicitud, origen_sistema_consultado.strip().upper())
    return _a_dto(guardada)


@router.get("/explicacion", response_model=ExplicacionDto)
async def explicacion(
    id_linea_solicitud: str = Query(alias="idLineaSolicitud"),
    origen_sistema_consultado: str = Query(alias="origenSistema"),
    _: str = Depends(verificar_origen_sistema),
    recomendacion_repo=Depends(obtener_recomendacion_repo),
    servicio=Depends(obtener_servicio_explicacion),
):
    """Por que se eligio esa flota, en dos o tres frases (#369).

    **Aparte del POST a proposito.** El motor responde en decimas y el modelo
    tarda segundos: meterla en `POST /recomendaciones` volveria lenta una
    respuesta rapida por un texto que es un agregado. Asi la pantalla pinta los
    numeros al instante y pide esto despues.

    Toma la linea y el origen igual que `GET /recomendaciones`, porque es la
    misma corrida que la pantalla acaba de mostrar y el repositorio -append-only
    por diseno- lee por esa pareja, no por id.

    **Siempre responde 200.** `explicacion: null` cuando no hay llave, cuando el
    proveedor fallo o cuando el texto traia una cifra que no era del motor. Que
    la pantalla funcione sin ella es el segundo criterio de la issue, y un 503
    la obligaria a tratar un caso normal como un error.
    """
    guardada = await cu.consultar_recomendacion(
        recomendacion_repo, id_linea_solicitud, origen_sistema_consultado.strip().upper())
    if servicio is None:
        return ExplicacionDto()
    resultado = await servicio.explicar(guardada)
    return ExplicacionDto(explicacion=resultado.texto if resultado else None)


@router.get("/{id_recomendacion_vehiculo}/mercancias", response_model=list[MercanciaDeVehiculoDto])
async def mercancias_de_vehiculo(
    id_recomendacion_vehiculo: str,
    _: str = Depends(verificar_origen_sistema),
    recomendacion_repo=Depends(obtener_recomendacion_repo),
):
    mercancias = await cu.listar_mercancias_de_vehiculo(
        recomendacion_repo, id_recomendacion_vehiculo)
    return [_mercancia(m) for m in mercancias]
