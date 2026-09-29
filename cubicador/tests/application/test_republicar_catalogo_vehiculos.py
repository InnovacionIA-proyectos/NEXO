"""Issue #162: la siembra de presets (`listar_con_siembra`) insertaba las
filas en la base de Cubicador pero nunca publicaba `vehiculo.creado` - era la
UNICA escritura del catalogo que no pasaba por `guardar_vehiculo`
(worker/main.py, que si publica). Medido: Cubicador con 20 vehiculos (9
presets + 11 creados por cola) contra 13 en la proyeccion de NEXO.
"""

from __future__ import annotations

from infrastructure.persistence.db import SessionLocal
from infrastructure.persistence.repositorios.vehiculo_repo import SqlVehiculoCubicacionRepository


class _PublisherFalso:
    def __init__(self) -> None:
        self.publicados: list[str] = []

    async def publicar_vehiculo_creado(self, vehiculo_id: str, vehiculo: dict) -> None:
        self.publicados.append(vehiculo_id)


async def test_listar_con_siembra_publica_vehiculo_creado_por_cada_preset_nuevo():
    publisher = _PublisherFalso()
    async with SessionLocal() as sesion:
        repo = SqlVehiculoCubicacionRepository(sesion, publisher)

        catalogo = await repo.listar_con_siembra()

        # 10 y no 9 desde S4-HU-12.1: el catalogo unificado separo el contenedor
        # refrigerado de 40 del High Cube seco, que estaban mal emparejados
        # entre las dos fuentes (eran vehiculos distintos).
        assert len(catalogo) == 10  # los 10 presets, base limpia (conftest.py)
        assert set(publisher.publicados) == {v.id for v in catalogo}


async def test_listar_con_siembra_no_vuelve_a_publicar_en_la_segunda_llamada():
    publisher = _PublisherFalso()
    async with SessionLocal() as sesion:
        repo = SqlVehiculoCubicacionRepository(sesion, publisher)
        await repo.listar_con_siembra()
        publisher.publicados.clear()

        await repo.listar_con_siembra()  # ya sembrado - nada nuevo que publicar

        assert publisher.publicados == []


async def test_sin_publisher_la_siembra_sigue_funcionando_igual_que_antes():
    # No-regresion: sitios que instancian el repo sin publisher (tests,
    # scripts one-off) no deben romperse por este fix.
    async with SessionLocal() as sesion:
        repo = SqlVehiculoCubicacionRepository(sesion)

        catalogo = await repo.listar_con_siembra()

        assert len(catalogo) == 10


async def test_republicar_todos_republica_el_catalogo_completo():
    publisher = _PublisherFalso()
    async with SessionLocal() as sesion:
        repo = SqlVehiculoCubicacionRepository(sesion, publisher)
        await repo.listar_con_siembra()  # siembra inicial, ya publico los 9
        publisher.publicados.clear()

        catalogo = await repo.republicar_todos()

        # TODOS los vehiculos, no solo los "nuevos" (aqui no hay ninguno nuevo
        # que sembrar) - es el reparo manual de una desincronizacion existente.
        assert len(catalogo) == 10
        assert set(publisher.publicados) == {v.id for v in catalogo}
