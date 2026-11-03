"""Catalogo en memoria y dobles para las pruebas del modo sombra."""

from __future__ import annotations

import json
from decimal import Decimal as D

from application.ia_sombra.configuracion import ConfiguracionSombra
from application.ports.ia import IaNoDisponible, RespuestaIa
from application.recomendaciones.casos_de_uso import LineaMercancia
from domain.entidades.catalogo import (
    Carroceria, CarroceriaDeVehiculo, Catalogo, CategoriaMercancia, ParametrosMotor, ReglaIncompatibilidad,
    TipoMercancia, Vehiculo,
)
from domain.entidades.entidades import ParametrosCubicacion
from domain.entidades.recomendacion import RecomendacionGuardada, VehiculoDeRecomendacion
from datetime import datetime, timezone

FURGON = Carroceria("c-furgon", "FURGON", "Furgon")
ESTACAS = Carroceria("c-estacas", "ESTACAS", "Estacas")
ALIMENTOS = CategoriaMercancia("cat-ali", "ALIMENTOS")
FERTILIZANTE = CategoriaMercancia("cat-fer", "FERTILIZANTE")


def _vehiculo(codigo, largo, ancho, alto, capacidad):
    return Vehiculo(
        f"v-{codigo}", codigo, codigo, D(largo), D(ancho), D(alto), D(capacidad), True,
        (CarroceriaDeVehiculo(FURGON, D(capacidad)), CarroceriaDeVehiculo(ESTACAS, D(capacidad))))


TURBO = _vehiculo("2L2", 450, 220, 230, 4000)
SENCILLO = _vehiculo("C2", 650, 240, 240, 8500)


def catalogo() -> Catalogo:
    bebidas = TipoMercancia(
        "m-beb", "BEBIDAS", "Bebidas", ALIMENTOS, True, False, FURGON, ESTACAS, True, "NINGUNO", True, False, True)
    abono = TipoMercancia(
        "m-abo", "FERTILIZANTES", "Fertilizantes", FERTILIZANTE, True, False, FURGON, None, False, "NINGUNO", True, False, True)
    return Catalogo(
        vehiculos=(TURBO, SENCILLO), carrocerias=(FURGON, ESTACAS), mercancias=(bebidas, abono),
        reglas=(ReglaIncompatibilidad("r1", FERTILIZANTE.id, ALIMENTOS.id, "contaminacion"),),
        parametros=ParametrosMotor({"k_max": "10"}))


MEDIDAS = ParametrosCubicacion(D(1220), D(240), D(260), D(34000))


def linea(codigo="BEBIDAS", cantidad=50, largo=50, ancho=40, alto=40, peso=20) -> LineaMercancia:
    return LineaMercancia(codigo, cantidad, D(largo), D(ancho), D(alto), D(peso))


def recomendacion(*vehiculos: tuple[str, str], resultado="ASIGNADO", motivo=None, id="rec-1") -> RecomendacionGuardada:
    return RecomendacionGuardada(
        id=id, id_linea_solicitud="LINEA-1", origen_sistema="NEXO", resultado=resultado, motivo_sin_seleccion=motivo,
        condicionada=False, mercancias_condicionadas=(),
        vehiculos=tuple(
            VehiculoDeRecomendacion(
                f"rv-{i}", i, codigo, codigo, carroceria, D(0), D(0), D(0), D(0), False, False, ())
            for i, (codigo, carroceria) in enumerate(vehiculos, start=1)),
        advertencias=(), extradimension=None, created_at=datetime.now(timezone.utc))


def json_ia(resultado="ASIGNADO", motivo=None, vehiculos=None, explicacion="porque si") -> str:
    return json.dumps({"resultado": resultado, "motivo": motivo, "vehiculos": vehiculos or [], "explicacion": explicacion})


def vehiculo_ia(codigo="2L2", carroceria="FURGON", carga=((0, 50),)) -> dict:
    return {"codigoVehiculo": codigo, "codigoCarroceria": carroceria, "carga": [{"indice": i, "cantidad": n} for i, n in carga]}


def config(**cambios) -> ConfiguracionSombra:
    base = dict(habilitada=True, api_key="llave-de-prueba", muestreo=1.0, tope_tokens_dia=0, timeout_segundos=5.0)
    base.update(cambios)
    return ConfiguracionSombra(**base)


class ClienteIaFalso:
    """Responde lo que se le diga y cuenta las llamadas; guarda lo que recibio para poder revisarlo."""

    def __init__(self, respuesta: str | Exception | None = None, demora: float = 0.0, tokens=(100, 50)) -> None:
        self.respuesta = respuesta
        self.demora = demora
        self.tokens = tokens
        self.llamadas: list[tuple[str, str, dict]] = []

    async def completar(self, instrucciones, mensaje, esquema_salida):
        import asyncio
        self.llamadas.append((instrucciones, mensaje, esquema_salida))
        if self.demora:
            await asyncio.sleep(self.demora)
        if isinstance(self.respuesta, Exception):
            raise self.respuesta
        return RespuestaIa(self.respuesta or json_ia(), "modelo-falso", self.tokens[0], self.tokens[1], 12)


__all__ = ["ClienteIaFalso", "IaNoDisponible"]
