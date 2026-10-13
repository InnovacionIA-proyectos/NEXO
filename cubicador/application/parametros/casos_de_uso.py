"""Medidas estandar contra las que se detecta extradimension (S5-HU-17.4).

Vivian en `application/cubicacion_rapida/casos_de_uso.py`, que se retira con
`POST /api/cubicacion`. El comportamiento no cambia: es la fila unica
(singleton, id=1) de `parametros_cubicacion`, que el contrato sigue devolviendo
en el bloque `extradimension` de la seccion 2.4.
"""

from __future__ import annotations

from application.excepciones import ValidationFailedException
from application.ports.repositorios import IParametrosCubicacionRepository
from domain.entidades.entidades import ParametrosCubicacion


async def obtener_medidas_estandar(
    parametros_repo: IParametrosCubicacionRepository,
) -> ParametrosCubicacion:
    return await parametros_repo.obtener()


async def actualizar_medidas_estandar(
    parametros_repo: IParametrosCubicacionRepository, medidas: ParametrosCubicacion,
) -> ParametrosCubicacion:
    if min(medidas.largo_max_cm, medidas.ancho_max_cm,
           medidas.alto_max_cm, medidas.peso_max_kg) <= 0:
        raise ValidationFailedException(
            "MEDIDAS_INVALIDAS", "Todas las medidas estandar deben ser mayores que cero.")
    return await parametros_repo.guardar(medidas)
