"""Puntuacion del plan: componentes en 0..1 y estrategia combinada con
ponderaciones configurables desde la UI."""

from __future__ import annotations

from . import validadores as v
from .dominio import (
    Colocacion,
    Contenedor,
    EstrategiaOptimizacion,
    ItemCarga,
    ParametrosOptimizacion,
    PonderacionesCombinada,
    ResultadoCubicacion,
)


def volumen(colocadas: list[Colocacion], c: Contenedor) -> float:
    return sum(x.volumen_mm3 for x in colocadas) / c.volumen_util_mm3 if c.volumen_util_mm3 > 0 else 0.0


def unidades(colocadas: list[Colocacion], total_unidades: int) -> float:
    return len(colocadas) / total_unidades if total_unidades > 0 else 0.0


def peso(colocadas: list[Colocacion], c: Contenedor) -> float:
    return sum(x.item.peso_kg for x in colocadas) / c.capacidad_kg if c.capacidad_kg > 0 else 0.0


def estabilidad(colocadas: list[Colocacion]) -> float:
    """Proporcion de unidades estables (sin advertencias de soporte/centro de masa)."""
    if not colocadas:
        return 1.0
    return sum(1 for x in colocadas if x.estado_estabilidad == "estable") / len(colocadas)


def balance_ejes(colocadas: list[Colocacion], c: Contenedor) -> float:
    """1 = ningun eje sobre su capacidad y reparto parejo; penaliza la peor razon de uso."""
    if not c.ejes:
        return 1.0
    por_eje = v.estimar_peso_por_eje(colocadas, c)
    peor = 0.0
    ejes = sorted(c.ejes, key=lambda e: e.posicion_mm)
    for i, eje in enumerate(ejes):
        if eje.capacidad_kg > 0:
            peor = max(peor, por_eje[i] / eje.capacidad_kg)
    return 1 - peor * 0.2 if peor <= 1 else max(0.0, 1 - peor)  # exceso castiga fuerte


def accesibilidad(colocadas: list[Colocacion]) -> float:
    """Proporcion de unidades sin bloqueo de secuencia (accesibles al descargar)."""
    if not colocadas:
        return 1.0
    return sum(1 for x in colocadas if x.accesibilidad != "bloqueada") / len(colocadas)


def prioridad(colocadas: list[Colocacion], todas: list[ItemCarga]) -> float:
    """Proporcion de unidades de prioridad critica (1-2) que quedaron cargadas."""
    criticas = sum(1 for i in todas if i.prioridad <= 2)
    if criticas == 0:
        return 1.0
    return sum(1 for c in colocadas if c.item.prioridad <= 2) / criticas


_PESOS_POR_ESTRATEGIA: dict[EstrategiaOptimizacion, PonderacionesCombinada] = {
    EstrategiaOptimizacion.MAXIMIZAR_VOLUMEN: PonderacionesCombinada(0.70, 0.05, 0.10, 0.05, 0.05, 0.05),
    EstrategiaOptimizacion.MAXIMIZAR_UNIDADES: PonderacionesCombinada(0.10, 0.05, 0.10, 0.05, 0.05, 0.65),
    EstrategiaOptimizacion.MAXIMIZAR_PESO: PonderacionesCombinada(0.10, 0.15, 0.10, 0.05, 0.05, 0.05),
    EstrategiaOptimizacion.PRIORIZAR_PEDIDOS_CRITICOS: PonderacionesCombinada(0.15, 0.05, 0.10, 0.05, 0.60, 0.05),
    EstrategiaOptimizacion.PRIORIZAR_SECUENCIA_ENTREGA: PonderacionesCombinada(0.15, 0.05, 0.10, 0.60, 0.05, 0.05),
    EstrategiaOptimizacion.MINIMIZAR_MOVIMIENTOS: PonderacionesCombinada(0.15, 0.05, 0.10, 0.60, 0.05, 0.05),
    EstrategiaOptimizacion.MEJORAR_ESTABILIDAD: PonderacionesCombinada(0.15, 0.05, 0.60, 0.05, 0.05, 0.10),
    EstrategiaOptimizacion.BALANCEAR_EJES: PonderacionesCombinada(0.15, 0.60, 0.10, 0.05, 0.05, 0.05),
}


def evaluar(
    r: ResultadoCubicacion, todas: list[ItemCarga], p: ParametrosOptimizacion,
) -> tuple[float, dict[str, float]]:
    """Puntaje global 0..100 segun la estrategia; con Combinada usa las ponderaciones."""
    parciales = {
        "volumen": volumen(r.colocaciones, r.contenedor),
        "unidades": unidades(r.colocaciones, len(todas)),
        "peso": peso(r.colocaciones, r.contenedor),
        "estabilidad": estabilidad(r.colocaciones),
        "balanceEjes": balance_ejes(r.colocaciones, r.contenedor),
        "accesibilidad": accesibilidad(r.colocaciones),
        "prioridad": prioridad(r.colocaciones, todas),
    }

    pesos = _PESOS_POR_ESTRATEGIA.get(p.estrategia) or p.ponderaciones or PonderacionesCombinada()

    # MaximizarPeso pondera el componente de peso aparte (no esta en el record).
    peso_componente = 0.50 if p.estrategia == EstrategiaOptimizacion.MAXIMIZAR_PESO else 0.0
    total = (
        parciales["volumen"] * pesos.volumen
        + parciales["balanceEjes"] * pesos.balance_ejes
        + parciales["estabilidad"] * pesos.estabilidad
        + parciales["accesibilidad"] * pesos.secuencia
        + parciales["prioridad"] * pesos.prioridad
        + parciales["unidades"] * pesos.unidades
        + parciales["peso"] * peso_componente
    )
    suma_pesos = (
        pesos.volumen + pesos.balance_ejes + pesos.estabilidad
        + pesos.secuencia + pesos.prioridad + pesos.unidades + peso_componente
    )

    return round(total / suma_pesos * 100, 1) if suma_pesos > 0 else 0.0, parciales
