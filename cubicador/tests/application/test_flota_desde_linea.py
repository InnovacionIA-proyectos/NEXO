"""S4-HU-12.3 - una linea de solicitud por camion.

De este lado de la frontera le toca al Cubicador una sola cosa: crear la flota y
CONTARLA. Crear las lineas de solicitud es del backend, porque `lineas_solicitud`
vive en el SQL Server de NEXO y el puerto de este servicio es de solo lectura
por diseño (ver CONTRATO.md 9.3).

El agujero que esta issue cierra: `crear_plan_desde_linea` creaba hasta 20 planes
y devolvia UNO. NEXO nunca se enteraba de los camiones 2..N, asi que no podia
crear su linea ni su cotizacion, y la Mesa de Costeo sondeaba `por-linea`
viendo siempre un solo vehiculo.

Y una cosa que se descubrio al armar el evento: el reparto de esta funcion
seguia siendo VORAZ aunque S4-HU-12.2 ya lo habia equilibrado en el cubicador
rapido. Como el peso de cada camion es lo que viaja en el evento y lo que NEXO
escribe en cada linea, el desbalance se habria propagado hasta la cotizacion.
"""

from __future__ import annotations

import pytest

from application.excepciones import ValidationFailedException
from application.plan_cargue.desde_linea import crear_plan_desde_linea
from application.plan_cargue.optimizar import ParametrosOptimizacionInput, optimizar_plan
from application.ports.externos import LineaParaCubicacion, PosicionCarga
from infrastructure.persistence.db import SessionLocal
from infrastructure.persistence.repositorios.plan_cargue_repo import SqlPlanCargueRepository
from infrastructure.persistence.repositorios.vehiculo_repo import SqlVehiculoCubicacionRepository

LINEA_ID = "aaaaaaaa-1111-2222-3333-444444444444"


class _LineasFalso:
    """Doble del puerto de solo lectura hacia NEXO."""

    def __init__(self, posiciones: list[PosicionCarga]) -> None:
        self._posiciones = posiciones

    async def obtener(self, linea_solicitud_id: str) -> LineaParaCubicacion:
        return LineaParaCubicacion(
            linea_id=linea_solicitud_id, posiciones=self._posiciones,
            origen="Medellin", destino="Bogota",
        )


def _pallets(cantidad: int, peso_kg: float) -> list[PosicionCarga]:
    return [PosicionCarga(descripcion="Pallet", cantidad=cantidad,
                          largo_cm=120, ancho_cm=100, alto_cm=100, peso_kg=peso_kg)]


async def _crear(posiciones: list[PosicionCarga]):
    async with SessionLocal() as sesion:
        planes = SqlPlanCargueRepository(sesion)
        vehiculos = SqlVehiculoCubicacionRepository(sesion)
        flota = await crear_plan_desde_linea(planes, vehiculos, _LineasFalso(posiciones), LINEA_ID)
        return flota, planes, vehiculos


# ------------------------------------------------ la flota completa, no uno


async def test_devuelve_todos_los_camiones_no_solo_el_primero():
    """El agujero central de la issue. Sin esto NEXO no puede crear una linea de
    solicitud por camion porque solo conoce uno."""
    flota, _, _ = await _crear(_pallets(cantidad=60, peso_kg=500))

    assert len(flota) > 1, "30.000 kg no caben en un solo vehiculo del catalogo"
    assert [p.numero_vehiculo for p in flota] == list(range(1, len(flota) + 1))
    assert all(p.linea_solicitud_id == LINEA_ID for p in flota)


async def test_cada_camion_reporta_su_peso_cargado():
    """`pesoAsignadoKg` del evento sale de aqui: es lo que NEXO escribe en cada
    linea nueva y lo que despues determina la banda de tarifa."""
    flota, _, _ = await _crear(_pallets(cantidad=60, peso_kg=500))

    pesos = [p.peso_total_kg for p in flota]
    assert all(p > 0 for p in pesos), "un camion sin peso no sirve para cotizar"
    assert sum(pesos) == 30_000, "no se pierde ni se inventa carga en el reparto"


# ------------------------------------------------------ reparto equilibrado


async def test_el_reparto_entre_camiones_es_equilibrado():
    """Lo que esta issue tuvo que corregir. La cascada anterior llenaba el primer
    camion al tope y dejaba el resto al ultimo: medido, 20.000 y 10.000 kg para
    una carga de 30.000. Ahora los dos llevan 15.000."""
    flota, _, _ = await _crear(_pallets(cantidad=60, peso_kg=500))

    pesos = [p.peso_total_kg for p in flota]
    assert max(pesos) - min(pesos) <= 500, "el desvio no puede pasar de un pallet"


async def test_el_reparto_equilibrado_tambien_con_pesos_desiguales():
    """Con unidades de distinto peso el reparto exacto no existe, pero la
    heuristica tiene que quedar cerca."""
    posiciones = [
        PosicionCarga(descripcion="Pesado", cantidad=10, largo_cm=120, ancho_cm=100, alto_cm=100, peso_kg=1_800),
        PosicionCarga(descripcion="Liviano", cantidad=20, largo_cm=100, ancho_cm=80, alto_cm=80, peso_kg=300),
    ]
    flota, _, _ = await _crear(posiciones)

    pesos = [p.peso_total_kg for p in flota]
    assert sum(pesos) == 10 * 1_800 + 20 * 300
    assert max(pesos) - min(pesos) <= 1_800, "el desvio no supera la unidad mas pesada"


async def test_si_cabe_en_un_camion_la_flota_es_de_uno():
    flota, _, _ = await _crear(_pallets(cantidad=4, peso_kg=500))

    assert len(flota) == 1
    assert flota[0].numero_vehiculo == 1


# -------------------------------------- vehiculos sin datos para el plan 3D


async def test_no_se_elige_un_vehiculo_al_que_no_se_le_puede_armar_el_plan():
    """El contenedor refrigerado de 40 no tiene medidas de puerta ni posiciones
    de eje: solo existia en el catalogo del motor rapido, que no las guarda
    (S4-HU-12.1). El cubicador rapido puede sugerirlo -alli no hacen falta- pero
    el motor 3D reventaba con un TypeError al comparar contra None. Un fallo
    INESPERADO, que el worker reintenta tres veces y manda a la DLQ."""
    flota, _, vehiculos = await _crear(_pallets(cantidad=60, peso_kg=500))

    elegido = await vehiculos.obtener(flota[0].vehiculo_id)
    assert elegido.puerta_ancho_mm is not None
    assert all(e.posicion_mm is not None for e in elegido.ejes)


async def test_optimizar_un_plan_con_vehiculo_incompleto_se_rechaza_con_causa():
    """Por si el vehiculo incompleto llega por otra via -crear un plan desde el
    API con ese id-: tiene que ser un rechazo de negocio, no un TypeError."""
    async with SessionLocal() as sesion:
        planes = SqlPlanCargueRepository(sesion)
        vehiculos = SqlVehiculoCubicacionRepository(sesion)
        catalogo = await vehiculos.listar_con_siembra()
        reefer = next(v for v in catalogo if v.codigo == "contenedor_refrigerado_40")
        plan = await planes.crear("Plan con reefer", reefer.id)

        with pytest.raises(ValidationFailedException) as error:
            await optimizar_plan(planes, vehiculos, plan.id, ParametrosOptimizacionInput("Combinada", "bfd"))

    assert error.value.codigo == "VEHICULO_SIN_DATOS_PARA_PLAN"


# ------------------------------------------------------------ reconstruccion


async def test_volver_a_ejecutar_reconstruye_la_flota_sin_acumular():
    """La Mesa de Costeo puede pedir el plan otra vez -por ejemplo tras corregir
    el peso-. Los planes anteriores de esa linea se borran antes de rehacerla."""
    posiciones = _pallets(cantidad=60, peso_kg=500)
    primera, _, _ = await _crear(posiciones)
    segunda, _, _ = await _crear(posiciones)

    assert len(segunda) == len(primera)
    # Sesion propia: la del helper ya se cerro al salir de su `async with`.
    # `listar_por_linea` devuelve dicts -es la proyeccion que sondea la Mesa
    # de Costeo-, no entidades.
    async with SessionLocal() as sesion:
        vivos = await SqlPlanCargueRepository(sesion).listar_por_linea(LINEA_ID)

    assert len(vivos) == len(segunda), "no quedaron planes huerfanos de la corrida anterior"
    assert {p.id for p in primera}.isdisjoint({v["id"] for v in vivos})
