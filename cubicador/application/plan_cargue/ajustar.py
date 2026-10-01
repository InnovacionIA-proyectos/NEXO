"""Ajuste manual de una colocacion (mover, rotar, bloquear, retirar o
reinsertar), validado en backend antes de aplicarse. Equivalente a
AjustarColocacionCommand de NEXO. Al vivir el motor en el mismo proceso, la
validacion (equivalente a ValidarAjusteAsync) se resuelve aqui mismo, en
memoria - exactamente la misma naturaleza "siempre local" que se decidio
para NEXO por ser una interaccion interactiva en pantalla (ver
docs/DECISIONES_TECNICAS.md #5), solo que aqui ni siquiera hace falta un puerto
separado para lograrlo."""

from __future__ import annotations

from dataclasses import dataclass

from application.excepciones import NotFoundException, ValidationFailedException
from application.ports.repositorios import IPlanCargueRepository, IVehiculoCubicacionRepository
from domain.motor import puntuacion as pt
from domain.motor import validadores as v
from domain.motor.dominio import Colocacion, Orientacion, ParametrosOptimizacion, ResultadoCubicacion, orientaciones_de
from domain.motor.motor import MotorCubicacion3D

from . import mapeos
from .resultado import ResultadoPlan, obtener_resultado

_motor = MotorCubicacion3D()


@dataclass
class AjusteColocacionInput:
    x: float | None = None
    y: float | None = None
    z: float | None = None
    orientacion_indice: int | None = None
    bloqueada: bool | None = None
    retirar: bool = False
    reinsertar: bool = False


async def ajustar_colocacion(
    planes: IPlanCargueRepository, vehiculos: IVehiculoCubicacionRepository,
    plan_id: str, colocacion_id: str, ajuste: AjusteColocacionInput,
) -> ResultadoPlan:
    plan = await planes.obtener(plan_id)
    if plan is None:
        raise NotFoundException("PLAN_NO_ENCONTRADO", "Plan de cargue no encontrado.")
    vehiculo = await vehiculos.obtener(plan.vehiculo_id)
    if vehiculo is None:
        raise NotFoundException("VEHICULO_NO_ENCONTRADO", "El vehiculo del plan ya no existe.")

    contenedor = mapeos.a_contenedor(vehiculo)
    items_por_id = {i.id: i for i in plan.items}

    colocaciones = await planes.obtener_colocaciones(plan_id)
    objetivo = next((c for c in colocaciones if c.id == colocacion_id), None)
    if objetivo is None:
        raise NotFoundException("COLOCACION_NO_ENCONTRADA", "La colocacion no existe en el plan.")

    def item_de(c):
        return mapeos.a_item_carga(items_por_id[c.item_plan_cargue_id], c.indice_unidad)

    if ajuste.bloqueada is not None:
        await planes.marcar_bloqueada(colocacion_id, ajuste.bloqueada)

    if ajuste.retirar:
        await planes.marcar_retirada(colocacion_id)
    elif ajuste.x is not None or ajuste.y is not None or ajuste.z is not None or ajuste.orientacion_indice is not None or ajuste.reinsertar:
        item_objetivo = item_de(objetivo)
        x = ajuste.x if ajuste.x is not None else objetivo.x
        y = ajuste.y if ajuste.y is not None else objetivo.y
        z = ajuste.z if ajuste.z is not None else objetivo.z

        if ajuste.orientacion_indice is not None:
            orientacion = next(
                (o for o in orientaciones_de(item_objetivo) if o.indice == ajuste.orientacion_indice), None,
            )
            if orientacion is None:
                raise ValidationFailedException(
                    "ORIENTACION_NO_PERMITIDA", "La orientacion solicitada no esta permitida para esta unidad.",
                )
        else:
            orientacion = Orientacion(
                objetivo.orientacion_indice, objetivo.largo_colocado_mm, objetivo.ancho_colocado_mm, objetivo.alto_colocado_mm,
            )

        otras_raw = [c for c in colocaciones if c.id != colocacion_id and c.cargada]
        otras = [
            Colocacion(
                item=item_de(c), x=c.x, y=c.y, z=c.z,
                orientacion=Orientacion(c.orientacion_indice, c.largo_colocado_mm, c.ancho_colocado_mm, c.alto_colocado_mm),
            )
            for c in otras_raw
        ]

        if not v.dentro_de_limites(x, y, z, orientacion, contenedor):
            raise ValidationFailedException("FUERA_DE_LIMITES", "La posicion queda fuera del contenedor o viola tolerancias.")
        if v.colisiona_con_alguna(x, y, z, orientacion, otras, contenedor.tolerancia_entre_unidades_mm):
            raise ValidationFailedException("COLISION", "La posicion colisiona con otra unidad.")
        soporte, soportes = v.calcular_soporte(x, y, z, orientacion, otras)
        if soporte < 0.60:
            raise ValidationFailedException(
                "SOPORTE_INSUFICIENTE", f"La unidad quedaria con solo {soporte:.0%} de su base apoyada (minimo 60%).",
            )
        error_apilamiento = v.validar_apilamiento(item_objetivo, z, soportes, otras)
        if error_apilamiento is not None:
            raise ValidationFailedException("APILAMIENTO", f"Apilamiento invalido: {error_apilamiento}.")

        principal = v.soporte_principal(soportes)
        soporte_colocacion_id = None
        if principal is not None:
            item_id, indice = mapeos.descomponer_clave(principal.item.id)
            soporte_colocacion_id = await planes.resolver_colocacion_por_clave(plan_id, mapeos.clave(item_id, indice))

        await planes.aplicar_movimiento(
            colocacion_id, x, y, z, orientacion.indice,
            orientacion.largo_mm, orientacion.ancho_mm, orientacion.alto_mm,
            soporte, soporte_colocacion_id,
        )

    # Revalidacion integral del plan tras el ajuste - SIEMPRE con la estrategia
    # por defecto (Combinada), igual que el original: no reutiliza la
    # estrategia con la que se optimizo originalmente el plan.
    colocaciones_final = await planes.obtener_colocaciones(plan_id)
    cargadas_final = [c for c in colocaciones_final if c.cargada]
    colocaciones_motor = [
        Colocacion(
            item=item_de(c), x=c.x, y=c.y, z=c.z,
            orientacion=Orientacion(c.orientacion_indice, c.largo_colocado_mm, c.ancho_colocado_mm, c.alto_colocado_mm),
        )
        for c in cargadas_final
    ]
    todos_items = mapeos.expandir_items(plan.items)
    parametros_default = ParametrosOptimizacion()

    resultado = ResultadoCubicacion(colocaciones=colocaciones_motor, rechazos=[], contenedor=contenedor)
    incidencias = _motor.validar(resultado, parametros_default)
    resultado.incidencias = incidencias
    puntaje, parciales = pt.evaluar(resultado, todos_items, parametros_default)
    metricas = _motor.metricas(resultado, todos_items)

    anotaciones = {
        c.item.id: (c.estado_estabilidad, c.accesibilidad, c.porcentaje_soporte)
        for c in colocaciones_motor
    }

    await planes.actualizar_tras_ajuste(plan_id, incidencias, puntaje, metricas, anotaciones)
    return await obtener_resultado(planes, vehiculos, plan_id)
