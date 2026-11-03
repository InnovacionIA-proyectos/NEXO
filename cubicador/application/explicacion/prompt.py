"""Instrucciones, mensaje y esquema de la explicacion de una recomendacion.

Al modelo se le pasan **los numeros ya calculados** y se le pide redactarlos, no
recalcularlos: la aritmetica es del motor y esto solo la cuenta en espanol.

Lo que sale es de nivel C del ADR
(`docs/decisiones/ia-claude-seguridad-y-datos.md`, seccion 2): codigos de
vehiculo y de carroceria del catalogo, y las medidas y pesos de la carga.

**Ninguna tarifa, ni el valor de la mercancia, ni nada comercial**, que son
nivel A y no salen ni enmascarados. Importa decirlo porque la tentacion esta a
un paso: pedirle "explica que este camion sale mas barato" seria exactamente
sacar el dato prohibido. La explicacion es sobre por que ESE vehiculo cabe,
no sobre cuanto cuesta.
"""

from __future__ import annotations

import json
from decimal import Decimal

from domain.entidades.recomendacion import RecomendacionGuardada

VERSION = "1"

MAX_EXPLICACION = 600

INSTRUCCIONES = """Explicas, en espanol y en dos o tres frases, por que se eligio esa flota para una carga. Escribes para quien \
cotiza un flete, no para un ingeniero.

Lo que recibes YA ESTA CALCULADO por el motor de cubicacion. Tu trabajo es contarlo, no recalcularlo.

Reglas:
- **No escribas ninguna cifra que no este en los datos que recibes.** Ni sumes, ni promedies, ni estimes. Si quieres decir algo \
que exigiria una cuenta, dilo con palabras ("queda holgado", "va casi lleno") en vez de con un numero.
- Puedes redondear una cifra que si recibiste, y decir 87 % donde dice 87,4.
- Habla de lo que sustenta la decision: cuanto ocupa la carga, cuanto pesa, que tan lleno queda cada vehiculo y por que hicieron \
falta varios si fue el caso.
- No menciones precios, tarifas ni costos: no los recibes y no son parte de esto.
- Si la carga quedo marcada con alguna advertencia, nombrala en una frase.
- Nada de encabezados ni vinetas: texto corrido."""


def _num(valor: Decimal | float | int) -> float | int:
    f = float(valor)
    return int(f) if f.is_integer() else round(f, 2)


def construir_mensaje(recomendacion: RecomendacionGuardada) -> str:
    """Los numeros del motor, y nada de quien pidio el flete."""
    vehiculos = [
        {
            "numero": v.numero,
            "vehiculo": v.nombre_vehiculo,
            "codigoVehiculo": v.codigo_vehiculo,
            "carroceria": v.codigo_carroceria,
            "pesoTotalKg": _num(v.peso_total_kg),
            "volumenTotalM3": _num(v.volumen_total_m3),
            "llenadoVolumenPorcentaje": _num(v.porcentaje_llenado_volumen),
            "llenadoPesoPorcentaje": _num(v.porcentaje_llenado_peso),
            "alertaBajoLlenado": v.alerta_bajo_llenado,
            "mercancias": [
                {"codigo": m.codigo_mercancia, "unidades": m.cantidad}
                for m in v.mercancias
            ],
        }
        for v in recomendacion.vehiculos
    ]
    return json.dumps(
        {
            "resultado": recomendacion.resultado,
            "motivoSinSeleccion": recomendacion.motivo_sin_seleccion,
            "totalVehiculos": len(recomendacion.vehiculos),
            "vehiculos": vehiculos,
            "advertencias": list(recomendacion.advertencias),
            "condicionada": recomendacion.condicionada,
        },
        ensure_ascii=False,
    )


def cifras_permitidas(recomendacion: RecomendacionGuardada) -> list[Decimal]:
    """Todo numero que el texto puede nombrar legitimamente.

    Es la contraparte de `construir_mensaje`: lo que entra al mensaje es lo que
    puede salir en el texto. Se agrega el conteo de vehiculos y los ordinales
    (1, 2, 3...) porque "el segundo camion" y "los 2 vehiculos" son cifras
    normales de la redaccion y no salen de ningun campo.
    """
    permitidas = [Decimal(len(recomendacion.vehiculos))]
    permitidas += [Decimal(i) for i in range(len(recomendacion.vehiculos) + 1)]
    for v in recomendacion.vehiculos:
        permitidas += [
            Decimal(v.numero), v.peso_total_kg, v.volumen_total_m3,
            v.porcentaje_llenado_volumen, v.porcentaje_llenado_peso,
        ]
        permitidas += [Decimal(m.cantidad) for m in v.mercancias]
    return permitidas


def esquema_salida() -> dict:
    return {
        "type": "object",
        "properties": {"explicacion": {"type": "string"}},
        "required": ["explicacion"],
        "additionalProperties": False,
    }
