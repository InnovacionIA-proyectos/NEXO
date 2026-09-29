"""Casos de uso del cubicador rapido (SICCE). Delegacion fina hacia el
dominio puro `domain.motor.cubicacion_rapida` - sin reglas propias que
aislar aqui, igual criterio que NEXO aplico a Biblioteca/ServiciosConexos."""

from __future__ import annotations

from application.excepciones import ValidationFailedException
from application.ports.repositorios import IParametrosCubicacionRepository, IVehiculoCubicacionRepository
from domain.entidades.entidades import VehiculoCubicacion
from domain.motor import cubicacion_rapida as motor

# Total de bultos que la disposicion puede expandir (proteccion anti-DoS).
_MAX_UNIDADES_TOTALES = 10_000


def unidad_desde_vehiculo(v: VehiculoCubicacion) -> motor.UnidadCubicacion | None:
    """Traduce una fila del catalogo unico a la unidad que entiende el motor
    rapido. Devuelve None si la fila no se puede evaluar.

    Aqui se cierra la brecha que S4-HU-12.1 vino a eliminar: el motor pensaba en
    metros y peso bruto, la tabla piensa en milimetros y carga util. El peso
    bruto NO se guarda, se deriva: capacidad + tara.

    Una fila sin `geometria` o sin `control_ambiental` se DESCARTA en vez de
    asumirle un valor. Un vehiculo creado desde la interfaz nace sin ellos
    porque el contrato de POST /api/vehiculos todavia no los expone (queda
    cerrado cuando entren los contratos de back y front). Suponer "sin
    refrigeracion" o suponer que la tiene son dos errores caros con carga
    refrigerada, y el segundo no se nota hasta que la mercancia llega dañada.
    """
    if v.codigo is None or v.geometria is None or v.control_ambiental is None:
        return None
    return motor.UnidadCubicacion(
        id=v.codigo,
        nombre=v.nombre,
        geometria=v.geometria,
        control_ambiental=v.control_ambiental,
        largo_util_m=v.largo_interno_mm / 1000,
        ancho_util_m=v.ancho_interno_mm / 1000,
        alto_util_m=v.alto_interno_mm / 1000,
        peso_bruto_permitido_kg=v.capacidad_kg + v.tara_kg,
        peso_vacio_kg=v.tara_kg,
        capacidad_por_eje_kg=[e.capacidad_kg for e in v.ejes],
        config_vehiculo=v.config_vehiculo,
        carroceria=v.carroceria,
        clase_viaje=v.clase_viaje,
        clase_contenedor=v.clase_contenedor,
    )


async def unidades_evaluables(vehiculos_repo: IVehiculoCubicacionRepository) -> list[motor.UnidadCubicacion]:
    """El catalogo unico, traducido y filtrado a lo que el motor puede evaluar."""
    return [u for u in (unidad_desde_vehiculo(v) for v in await vehiculos_repo.listar_con_siembra()) if u is not None]


async def evaluar_cubicacion(
    parametros_repo: IParametrosCubicacionRepository,
    vehiculos_repo: IVehiculoCubicacionRepository,
    request: motor.CubicacionRequest,
) -> motor.CubicacionResponse:
    total = sum(i.cantidad for i in request.items)
    if total > _MAX_UNIDADES_TOTALES:
        raise ValidationFailedException(
            "CUBICACION_DEMASIADO_GRANDE", f"La cubicacion suma {total} bultos; el maximo es {_MAX_UNIDADES_TOTALES}.",
        )
    medidas = await parametros_repo.obtener()
    return motor.cubicar(request, medidas, await unidades_evaluables(vehiculos_repo))


async def catalogo_unidades(vehiculos_repo: IVehiculoCubicacionRepository) -> list[motor.UnidadCubicacion]:
    return await unidades_evaluables(vehiculos_repo)


async def obtener_medidas_estandar(parametros_repo: IParametrosCubicacionRepository) -> motor.MedidasEstandar:
    return await parametros_repo.obtener()


async def actualizar_medidas_estandar(
    parametros_repo: IParametrosCubicacionRepository, medidas: motor.MedidasEstandar,
) -> motor.MedidasEstandar:
    if medidas.largo_max_cm <= 0 or medidas.ancho_max_cm <= 0 or medidas.alto_max_cm <= 0 or medidas.peso_max_kg <= 0:
        raise ValidationFailedException("MEDIDAS_INVALIDAS", "Todas las medidas estandar deben ser mayores que cero.")
    return await parametros_repo.guardar(medidas)
