"""Casos de uso del cubicador rapido (SICCE). Delegacion fina hacia el
dominio puro `domain.motor.cubicacion_rapida` - sin reglas propias que
aislar aqui, igual criterio que NEXO aplico a Biblioteca/ServiciosConexos."""

from __future__ import annotations

from application.excepciones import ValidationFailedException
from application.ports.repositorios import IParametrosCubicacionRepository
from domain.motor import cubicacion_rapida as motor

# Total de bultos que la disposicion puede expandir (proteccion anti-DoS).
_MAX_UNIDADES_TOTALES = 10_000


async def evaluar_cubicacion(
    parametros_repo: IParametrosCubicacionRepository, request: motor.CubicacionRequest,
) -> motor.CubicacionResponse:
    total = sum(i.cantidad for i in request.items)
    if total > _MAX_UNIDADES_TOTALES:
        raise ValidationFailedException(
            "CUBICACION_DEMASIADO_GRANDE", f"La cubicacion suma {total} bultos; el maximo es {_MAX_UNIDADES_TOTALES}.",
        )
    medidas = await parametros_repo.obtener()
    return motor.cubicar(request, medidas)


def catalogo_unidades() -> list[motor.UnidadCubicacion]:
    return motor.catalogo()


async def obtener_medidas_estandar(parametros_repo: IParametrosCubicacionRepository) -> motor.MedidasEstandar:
    return await parametros_repo.obtener()


async def actualizar_medidas_estandar(
    parametros_repo: IParametrosCubicacionRepository, medidas: motor.MedidasEstandar,
) -> motor.MedidasEstandar:
    if medidas.largo_max_cm <= 0 or medidas.ancho_max_cm <= 0 or medidas.alto_max_cm <= 0 or medidas.peso_max_kg <= 0:
        raise ValidationFailedException("MEDIDAS_INVALIDAS", "Todas las medidas estandar deben ser mayores que cero.")
    return await parametros_repo.guardar(medidas)
