"""Verifica que un motor alternativo se puede evaluar en paralelo sin tocar
motor.py ni el resto del dominio - solo implementar el protocolo
`Optimizador` y llamar a `registrar_optimizador` (ver docstring de
domain/motor/optimizadores.py)."""

from __future__ import annotations

import pytest

from domain.motor import optimizadores as opt
from domain.motor.dominio import Rechazo
from domain.motor.motor import MotorCubicacion3D


class _OptimizadorFalso:
    """Motor alternativo minimo: no ubica nada, rechaza todo con su propio
    codigo - basta para probar que el registro lo resuelve y lo ejecuta."""

    nombre = "falso"

    def optimizar(self, items, contenedor, parametros, fijas=None):
        return list(fijas or []), [Rechazo(i, "MOTOR_FALSO", "Rechazado por el optimizador de prueba.") for i in items]


@pytest.fixture(autouse=True)
def _limpiar_registro_tras_el_test():
    yield
    opt._REGISTRO.pop("falso", None)


def test_registrar_optimizador_lo_deja_disponible():
    assert "falso" not in opt.algoritmos_disponibles()
    opt.registrar_optimizador("falso", _OptimizadorFalso)
    assert "falso" in opt.algoritmos_disponibles()
    assert isinstance(opt.obtener_optimizador("FALSO"), _OptimizadorFalso)  # case-insensitive


def test_motor_usa_el_optimizador_registrado_sin_cambios_en_motor_py():
    from domain.motor.dominio import ParametrosOptimizacion
    from tests.domain.test_motor import contenedor_demo, item

    opt.registrar_optimizador("falso", _OptimizadorFalso)
    parametros = ParametrosOptimizacion(algoritmo="falso")

    resultado = MotorCubicacion3D().optimizar([item("1", 1000, 1000, 1000, 100)], contenedor_demo(), parametros)

    assert resultado.colocaciones == []
    assert len(resultado.rechazos) == 1
    assert resultado.rechazos[0].codigo_razon == "MOTOR_FALSO"
