"""Las cinco metricas del estudio comparativo del Cubicador, como funciones puras.

Reciben los casos del dataset dorado y lo que respondio quien se evalua (el motor
actual hoy, la IA en modo sombra despues) y devuelven numeros. No llaman al motor,
no leen archivos ni miden el tiempo: eso lo hace `evaluar.py`. Asi la misma cuenta
sirve para comparar cualquier candidato y se prueba sin correr nada.

Cada tasa se entrega como `{aciertos, total, tasa}` para que el denominador nunca
quede implicito: "acierto 80 %" sin saber sobre cuantos casos no dice nada.

Definiciones (ver `docs/ia/ESTUDIO-CUBICADOR-IA.md`):

  acierto del vehiculo   la flota propuesta es una de las flotas correctas del caso.
  flota minima           la cantidad de vehiculos propuesta es la minima correcta.
  respeto de capacidad   recalculado aca con el catalogo, sin creerle al candidato:
                         peso y volumen de cada vehiculo dentro de su limite, y toda
                         unidad pedida va en exactamente un vehiculo.
  latencia               p50, p95 y maximo de las muestras, en milisegundos.
  costo por cubicacion   tokens por su precio; el motor no gasta tokens.
"""

from __future__ import annotations

import math
from dataclasses import dataclass, field
from decimal import Decimal
from typing import Iterable

from domain.entidades.catalogo import Catalogo

ASIGNADO = "ASIGNADO"
SIN_SELECCION = "SIN_SELECCION_AUTOMATICA"

# Un vehiculo es el par (configuracion, carroceria): un 3S3 furgon y un 3S3 plataforma
# no son la misma respuesta, tienen capacidades y tarifas distintas.
Vehiculo = tuple[str, str]
Flota = tuple[Vehiculo, ...]


@dataclass(frozen=True)
class Posicion:
    codigo: str
    cantidad: int
    largo_cm: Decimal
    ancho_cm: Decimal
    alto_cm: Decimal
    peso_kg: Decimal

    @property
    def volumen_unitario_m3(self) -> Decimal:
        return self.largo_cm * self.ancho_cm * self.alto_cm / Decimal(1_000_000)


@dataclass(frozen=True)
class Caso:
    id: str
    origen: str
    etiqueta_validada: bool
    descripcion: str
    clase_viaje: str | None
    mercancias: tuple[Posicion, ...]
    resultado_esperado: str
    motivo_esperado: str | None
    # Cuantos vehiculos hacen falta como minimo.
    cantidad_minima: int
    # Las flotas que un humano daria por buenas, todas de `cantidad_minima` vehiculos. Puede
    # ir vacia cuando hay demasiadas combinaciones validas para listarlas: el caso entonces
    # solo cuenta para la flota minima, no para el acierto del vehiculo.
    flotas_correctas: tuple[Flota, ...]


@dataclass(frozen=True)
class VehiculoPropuesto:
    codigo_vehiculo: str
    codigo_carroceria: str
    # (indice de la posicion en `Caso.mercancias`, unidades de esa posicion en este vehiculo)
    carga: tuple[tuple[int, int], ...]

    @property
    def vehiculo(self) -> Vehiculo:
        return (self.codigo_vehiculo, self.codigo_carroceria)


@dataclass(frozen=True)
class Respuesta:
    """Lo que respondio quien se evalua, en una forma comun al motor y a la IA."""

    resultado: str
    motivo: str | None
    vehiculos: tuple[VehiculoPropuesto, ...]
    latencia_ms: float
    tokens_entrada: int = 0
    tokens_salida: int = 0


@dataclass
class Evaluacion:
    """Las respuestas de un candidato: una lista por caso (una muestra por repeticion)."""

    por_caso: dict[str, list[Respuesta]] = field(default_factory=dict)


# ----------------------------------------------------------------------- utilidades


def _tasa(aciertos: int, total: int) -> dict:
    return {"aciertos": aciertos, "total": total, "tasa": round(aciertos / total, 4) if total else None}


def _flota(respuesta: Respuesta) -> Flota:
    return tuple(sorted(v.vehiculo for v in respuesta.vehiculos))


def percentil(muestras: Iterable[float], p: float) -> float | None:
    """Percentil por rango mas cercano: el valor bajo el cual queda `p` de las muestras."""
    ordenadas = sorted(muestras)
    if not ordenadas:
        return None
    posicion = max(math.ceil(p * len(ordenadas)) - 1, 0)
    return ordenadas[posicion]


# ----------------------------------------------------------------------- metricas


def acierto_vehiculo(casos: list[Caso], evaluacion: Evaluacion) -> dict:
    """La flota propuesta es una de las correctas. Solo cuenta los casos que se deben asignar y traen flotas."""
    asignables = [c for c in casos if c.resultado_esperado == ASIGNADO and c.flotas_correctas]
    exactos = tipo = 0
    for c in asignables:
        r = evaluacion.por_caso[c.id][0]
        if r.resultado != ASIGNADO:
            continue
        flota = _flota(r)
        if flota in {tuple(sorted(f)) for f in c.flotas_correctas}:
            exactos += 1
        # Igual que arriba pero sin mirar la carroceria: separa "eligio otro camion" de
        # "eligio el camion correcto con otra carroceria".
        if sorted(v for v, _ in flota) in [sorted(v for v, _ in f) for f in c.flotas_correctas]:
            tipo += 1
    return {
        "flota_exacta": _tasa(exactos, len(asignables)),
        "solo_configuracion": _tasa(tipo, len(asignables)),
    }


def acierto_decision(casos: list[Caso], evaluacion: Evaluacion) -> dict:
    """Asignar o no asignar: acuerdo en el resultado y, cuando no se asigna, en el motivo."""
    coincide = sum(1 for c in casos if evaluacion.por_caso[c.id][0].resultado == c.resultado_esperado)
    sin_asignar = [c for c in casos if c.resultado_esperado == SIN_SELECCION and c.motivo_esperado]
    motivo = sum(1 for c in sin_asignar if evaluacion.por_caso[c.id][0].motivo == c.motivo_esperado)
    return {"resultado": _tasa(coincide, len(casos)), "motivo_cuando_no_asigna": _tasa(motivo, len(sin_asignar))}


def flota_minima(casos: list[Caso], evaluacion: Evaluacion) -> dict:
    """Cuantos vehiculos pide, contra la cantidad minima correcta.

    El exceso cuesta plata (un camion de mas); el defecto es peor: pedir menos de los
    que caben es prometer algo que no se puede cargar. Se separan para no promediarlos.
    """
    diferencias = []
    for c in casos:
        r = evaluacion.por_caso[c.id][0]
        if c.resultado_esperado == ASIGNADO and r.resultado == ASIGNADO:
            diferencias.append((c.id, len(r.vehiculos) - c.cantidad_minima))
    exactos = sum(1 for _, d in diferencias if d == 0)
    return {
        "cantidad_exacta": _tasa(exactos, len(diferencias)),
        "con_exceso": [{"caso": i, "de_mas": d} for i, d in diferencias if d > 0],
        "con_defecto": [{"caso": i, "de_menos": -d} for i, d in diferencias if d < 0],
        "exceso_medio": round(sum(d for _, d in diferencias) / len(diferencias), 3) if diferencias else None,
    }


def respeto_de_capacidad(casos: list[Caso], evaluacion: Evaluacion, catalogo: Catalogo) -> dict:
    """Recalcula peso, volumen y unidades de lo propuesto, sin creer en lo que el candidato reporta.

    Mide contra el volumen fisico del vehiculo, no contra el factor de aprovechamiento
    que usa el motor para empacar: una propuesta que pasa de 100 % no se puede cargar
    aunque el motor la considere "ajustada".
    """
    violaciones: list[dict] = []
    evaluados = peso_ok = volumen_ok = unidades_ok = 0
    for c in casos:
        r = evaluacion.por_caso[c.id][0]
        if r.resultado != ASIGNADO:
            continue
        evaluados += 1
        caso_peso = caso_volumen = True

        por_posicion: dict[int, int] = {}
        for v in r.vehiculos:
            ficha = catalogo.vehiculo_por_codigo(v.codigo_vehiculo)
            capacidad = ficha.capacidad_con(v.codigo_carroceria) if ficha else None
            if ficha is None or capacidad is None:
                caso_peso = caso_volumen = False
                violaciones.append({"caso": c.id, "vehiculo": f"{v.codigo_vehiculo}/{v.codigo_carroceria}",
                                    "detalle": "el vehiculo o la combinacion con la carroceria no existe en el catalogo"})
                continue
            peso = sum((c.mercancias[i].peso_kg * n for i, n in v.carga), Decimal(0))
            volumen = sum((c.mercancias[i].volumen_unitario_m3 * n for i, n in v.carga), Decimal(0))
            for i, n in v.carga:
                por_posicion[i] = por_posicion.get(i, 0) + n
            if peso > capacidad:
                caso_peso = False
                violaciones.append({"caso": c.id, "vehiculo": f"{v.codigo_vehiculo}/{v.codigo_carroceria}",
                                    "detalle": f"peso {peso} kg supera la capacidad de {capacidad} kg"})
            if volumen > ficha.volumen_m3:
                caso_volumen = False
                violaciones.append({"caso": c.id, "vehiculo": f"{v.codigo_vehiculo}/{v.codigo_carroceria}",
                                    "detalle": f"volumen {volumen:.2f} m3 supera los {ficha.volumen_m3:.2f} m3 del vehiculo"})

        caso_unidades = all(por_posicion.get(i, 0) == p.cantidad for i, p in enumerate(c.mercancias))
        if not caso_unidades:
            violaciones.append({"caso": c.id, "vehiculo": "-",
                                "detalle": "alguna unidad pedida no quedo en ningun vehiculo, o quedo en mas de uno"})
        peso_ok += caso_peso
        volumen_ok += caso_volumen
        unidades_ok += caso_unidades

    return {
        "peso": _tasa(peso_ok, evaluados),
        "volumen": _tasa(volumen_ok, evaluados),
        "unidades_conservadas": _tasa(unidades_ok, evaluados),
        "violaciones": violaciones,
    }


def latencia(casos: list[Caso], evaluacion: Evaluacion) -> dict:
    muestras = [r.latencia_ms for c in casos for r in evaluacion.por_caso[c.id]]
    return {
        "muestras": len(muestras),
        "p50_ms": round(percentil(muestras, 0.50), 3) if muestras else None,
        "p95_ms": round(percentil(muestras, 0.95), 3) if muestras else None,
        "max_ms": round(max(muestras), 3) if muestras else None,
    }


def costo_por_cubicacion(
    casos: list[Caso], evaluacion: Evaluacion,
    precio_entrada_usd_por_millon: float | None, precio_salida_usd_por_millon: float | None,
) -> dict:
    """Tokens por su precio. Sin precios entrega solo los tokens (el motor no gasta ninguno)."""
    respuestas = [r for c in casos for r in evaluacion.por_caso[c.id]]
    if not respuestas:
        return {"muestras": 0}
    entrada = sum(r.tokens_entrada for r in respuestas)
    salida = sum(r.tokens_salida for r in respuestas)
    salida_costo: dict = {
        "muestras": len(respuestas),
        "tokens_entrada_medio": round(entrada / len(respuestas), 1),
        "tokens_salida_medio": round(salida / len(respuestas), 1),
    }
    if precio_entrada_usd_por_millon is not None and precio_salida_usd_por_millon is not None:
        total = (entrada * precio_entrada_usd_por_millon + salida * precio_salida_usd_por_millon) / 1_000_000
        salida_costo["usd_por_cubicacion"] = round(total / len(respuestas), 6)
    return salida_costo


def evaluar(
    casos: list[Caso], evaluacion: Evaluacion, catalogo: Catalogo,
    precio_entrada_usd_por_millon: float | None = None, precio_salida_usd_por_millon: float | None = None,
) -> dict:
    return {
        "casos": len(casos),
        "acierto_decision": acierto_decision(casos, evaluacion),
        "acierto_vehiculo": acierto_vehiculo(casos, evaluacion),
        "flota_minima": flota_minima(casos, evaluacion),
        "respeto_de_capacidad": respeto_de_capacidad(casos, evaluacion, catalogo),
        "latencia": latencia(casos, evaluacion),
        "costo": costo_por_cubicacion(casos, evaluacion, precio_entrada_usd_por_millon, precio_salida_usd_por_millon),
    }
