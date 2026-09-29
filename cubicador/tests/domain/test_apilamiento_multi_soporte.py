"""CUB-01/CUB-02: el apilamiento se valida contra TODOS los soportes (no solo
el principal) y el peso se reparte proporcionalmente al area apoyada sobre
cada uno. Ver docs/testDocumentation/REGLAS_DE_NEGOCIO.md, reglas 27-31. Portado de
cabinaFletes/src/CabinaFletes.Tests/ApilamientoMultiSoporteTests.cs.
"""

from __future__ import annotations

from domain.motor import validadores as v
from domain.motor.dominio import Colocacion, ItemCarga, Orientacion


def item(
    id: str, l: float, w: float, h: float, peso: float,
    apilable: bool = True, fragil: bool = False, max_peso_encima: float = -1, max_niveles: int = 0,
) -> ItemCarga:
    return ItemCarga(
        id, f"SKU-{id}", f"Item {id}", "P", "C", "D", 1,
        l, w, h, peso, apilable, max_niveles, max_peso_encima, True, False, fragil,
    )


def base(it: ItemCarga, x: float, y: float, z: float) -> Colocacion:
    return Colocacion(item=it, x=x, y=y, z=z, orientacion=Orientacion(0, it.largo_mm, it.ancho_mm, it.alto_mm))


def escenario_puente(
    a_apilable: bool = True, b_apilable: bool = True, b_fragil: bool = False,
    a_peso: float = 100, b_peso: float = 100, b_max_peso_encima: float = -1,
) -> tuple[Colocacion, Colocacion, ItemCarga, Orientacion]:
    """Dos bases de 1000x1000x1000 adyacentes; una unidad superior las puentea 50/50."""
    a = base(item("A", 1000, 1000, 1000, a_peso, apilable=a_apilable), 0, 0, 0)
    b = base(
        item("B", 1000, 1000, 1000, b_peso, apilable=b_apilable, fragil=b_fragil, max_peso_encima=b_max_peso_encima),
        1000, 0, 0,
    )
    superior = item("U", 1000, 1000, 500, 200)
    # Colocada en x=500 -> 500 mm sobre A y 500 mm sobre B (fraccion 0.5 y 0.5).
    return a, b, superior, Orientacion(0, 1000, 1000, 500)


def test_calcular_soporte_unidad_que_puentea_devuelve_dos_soportes():
    a, b, _, o = escenario_puente()
    pct, soportes = v.calcular_soporte(500, 0, 1000, o, [a, b])

    assert pct == 1.0  # base totalmente apoyada
    assert len(soportes) == 2  # sobre A y sobre B
    assert all(s.fraccion == 0.5 for s in soportes)


def test_validar_apilamiento_soporte_secundario_no_apilable_es_rechazado():
    # A (principal por igual solape) es apilable, B NO. Antes solo se miraba el
    # principal y pasaba; ahora se detecta el peso sobre B (no apilable).
    a, b, superior, o = escenario_puente(b_apilable=False)
    _, soportes = v.calcular_soporte(500, 0, 1000, o, [a, b])

    error = v.validar_apilamiento(superior, 1000, soportes, [a, b])
    assert error is not None
    assert "no apilable" in error


def test_validar_apilamiento_soporte_secundario_fragil_mas_liviano_es_rechazado():
    a, b, superior, o = escenario_puente(b_fragil=True, b_peso=50)  # U pesa 200 > 50
    _, soportes = v.calcular_soporte(500, 0, 1000, o, [a, b])

    error = v.validar_apilamiento(superior, 1000, soportes, [a, b])
    assert error is not None
    assert "fr" in error and "gil" in error  # "frágil" (evitar dependencia de encoding en el aserto)


def test_validar_apilamiento_peso_sobre_secundario_se_mide_proporcional():
    # U pesa 200 -> 100 kg sobre B (fraccion 0.5). Con limite 60 kg en B: rechazo.
    a, b, superior, o = escenario_puente(b_max_peso_encima=60)
    _, soportes = v.calcular_soporte(500, 0, 1000, o, [a, b])
    assert v.validar_apilamiento(superior, 1000, soportes, [a, b]) is not None

    # Con limite 120 kg en B: los 100 kg proporcionales caben -> valido.
    a2, b2, sup2, o2 = escenario_puente(b_max_peso_encima=120)
    _, soportes2 = v.calcular_soporte(500, 0, 1000, o2, [a2, b2])
    assert v.validar_apilamiento(sup2, 1000, soportes2, [a2, b2]) is None


def test_validar_apilamiento_todos_apilables_sin_limites_es_valido():
    a, b, superior, o = escenario_puente()
    _, soportes = v.calcular_soporte(500, 0, 1000, o, [a, b])
    assert v.validar_apilamiento(superior, 1000, soportes, [a, b]) is None
