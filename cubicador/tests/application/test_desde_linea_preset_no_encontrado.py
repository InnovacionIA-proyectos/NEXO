"""Guarda de regresion: si el preset de vehiculo sugerido (o el default de
respaldo) no existe en el catalogo, `crear_plan_desde_linea` debe rechazar
con una excepcion de negocio explicita, NO con un StopIteration crudo.

Antes: `next(v for v in catalogo if v.nombre == nombre_preset)` sin default
lanzaba StopIteration. El worker la trataba como fallo INESPERADO
(worker/main.py::_procesar, rama `except Exception`): 3 reintentos identicos
y destinados a fallar igual, y recien despues DLQ - en vez de un rechazo de
negocio inmediato. Ver tambien clsCubicacionRechazadaConsumer.cs: para este
flujo (`crear_desde_linea`) el rechazo nunca tiene un PlanCargue que marcar
(el id publicado es el LineaSolicitudId, el plan nunca llega a crearse), asi
que un mensaje de rechazo claro es lo unico que queda para diagnosticar.
"""

from __future__ import annotations

import pytest

from application.excepciones import ValidationFailedException
from application.plan_cargue.desde_linea import crear_plan_desde_linea
from application.ports.externos import LineaParaCubicacion, PosicionCarga


class _VehiculosCatalogoVacio:
    """Catalogo que nunca sugiere ningun preset (ni el default de respaldo):
    fuerza el camino de "preset no encontrado" sin depender de la lista real
    de presets sembrados, que podria cambiar con el tiempo."""

    async def listar_con_siembra(self):
        return []


class _LineaConUnaPosicion:
    async def obtener(self, linea_solicitud_id: str) -> LineaParaCubicacion:
        return LineaParaCubicacion(
            linea_id=linea_solicitud_id,
            posiciones=[PosicionCarga(descripcion="Pallet", cantidad=1, largo_cm=100, ancho_cm=100, alto_cm=100, peso_kg=200)],
            origen=None, destino=None,
        )


async def test_preset_no_encontrado_es_rechazo_de_negocio_no_stopiteration():
    with pytest.raises(ValidationFailedException) as excinfo:
        await crear_plan_desde_linea(
            planes=None, vehiculos=_VehiculosCatalogoVacio(), lineas=_LineaConUnaPosicion(),
            linea_solicitud_id="11111111-1111-1111-1111-111111111111",
        )

    assert excinfo.value.codigo == "PRESET_VEHICULO_NO_ENCONTRADO"
