"""El adaptador de la IA del modo sombra (S6-HU-35.5).

No llama al modelo: un cliente falso responde lo que el motor recomendaria, o una
respuesta rota, y se comprueba que el adaptador la traduce bien a las metricas.
Si esta traduccion fallara, el informe de la IA (y el acta que se apoya en el)
mediria otra cosa.
"""

from __future__ import annotations

import asyncio
import json
import sys
from pathlib import Path

HERRAMIENTA = Path(__file__).resolve().parents[1]
CUBICADOR = HERRAMIENTA.parents[1] / "cubicador"
sys.path.insert(0, str(CUBICADOR))
sys.path.insert(0, str(HERRAMIENTA))

import metricas as m  # noqa: E402
from application.ports.ia import IaNoDisponible, RespuestaIa  # noqa: E402
from catalogo_qa import catalogo_qa  # noqa: E402
from evaluar import adaptador_motor, correr, leer_casos  # noqa: E402
from evaluar_ia import adaptador_ia  # noqa: E402

SEMILLA = HERRAMIENTA / "casos" / "semilla_sintetica.json"


class ClienteFijo:
    """Devuelve, para cada llamada, la siguiente respuesta de la lista (o levanta la excepcion)."""

    def __init__(self, respuestas: list) -> None:
        self.respuestas = list(respuestas)
        self.llamadas = 0

    async def completar(self, instrucciones: str, mensaje: str, esquema_salida: dict) -> RespuestaIa:
        self.llamadas += 1
        r = self.respuestas.pop(0)
        if isinstance(r, Exception):
            raise r
        return RespuestaIa(r, "falso", 100, 20, 1500, tokens_cache=10)


def _como_ia(r: m.Respuesta) -> str:
    """La respuesta del motor escrita con el contrato de salida del modo sombra."""
    return json.dumps({
        "resultado": r.resultado, "motivo": r.motivo,
        "vehiculos": [{"codigoVehiculo": v.codigo_vehiculo, "codigoCarroceria": v.codigo_carroceria,
                       "carga": [{"indice": i, "cantidad": c} for i, c in v.carga]}
                      for v in r.vehiculos],
    })


def test_si_la_ia_responde_lo_mismo_que_el_motor_las_metricas_dan_lo_mismo():
    catalogo = catalogo_qa()
    casos, _ = leer_casos(SEMILLA)
    motor = correr(casos, adaptador_motor(catalogo), 1)
    cliente = ClienteFijo([_como_ia(motor.por_caso[c.id][0]) for c in casos])
    errores: list[dict] = []

    ia = correr(casos, adaptador_ia(catalogo, cliente, errores), 1)

    assert errores == []
    assert cliente.llamadas == len(casos)
    r_motor, r_ia = m.evaluar(casos, motor, catalogo), m.evaluar(casos, ia, catalogo)
    for clave in ("acierto_decision", "acierto_vehiculo", "flota_minima", "respeto_de_capacidad"):
        assert r_ia[clave] == r_motor[clave]
    # Los tokens de cache cuentan como entrada: el costo sale por lo alto, nunca por lo bajo.
    assert ia.por_caso[casos[0].id][0].tokens_entrada == 110


def test_una_respuesta_rota_o_un_fallo_del_proveedor_cuentan_como_error_y_quedan_anotados():
    catalogo = catalogo_qa()
    casos, _ = leer_casos(SEMILLA)
    dos = casos[:2]
    cliente = ClienteFijo(["no es json", IaNoDisponible("IA_TIEMPO_AGOTADO", "tardo demasiado")])
    errores: list[dict] = []

    ia = correr(dos, adaptador_ia(catalogo, cliente, errores), 1)

    assert [ia.por_caso[c.id][0].resultado for c in dos] == ["ERROR", "ERROR"]
    assert [e["estado"] for e in errores] == ["IA_RESPUESTA_INVALIDA", "IA_TIEMPO_AGOTADO"]
    assert m.evaluar(dos, ia, catalogo)["acierto_decision"]["resultado"]["aciertos"] == 0


def test_el_mensaje_a_la_ia_es_el_mismo_que_arma_el_modo_sombra():
    from application.ia_sombra import prompt
    from application.recomendaciones.casos_de_uso import LineaMercancia
    from catalogo_qa import MEDIDAS_ESTANDAR

    catalogo = catalogo_qa()
    caso = leer_casos(SEMILLA)[0][0]
    vistos: list[str] = []

    class Espia(ClienteFijo):
        async def completar(self, instrucciones, mensaje, esquema_salida):
            vistos.append(mensaje)
            assert instrucciones == prompt.INSTRUCCIONES
            assert esquema_salida == prompt.esquema_salida(catalogo)
            return await super().completar(instrucciones, mensaje, esquema_salida)

    adaptador_ia(catalogo, Espia(["no es json"]), [])(caso)

    lineas = [LineaMercancia(p.codigo, p.cantidad, p.largo_cm, p.ancho_cm, p.alto_cm, p.peso_kg) for p in caso.mercancias]
    assert vistos == [prompt.construir_mensaje(catalogo, lineas, MEDIDAS_ESTANDAR, caso.clase_viaje)]
