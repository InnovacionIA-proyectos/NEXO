"""Algoritmos de ubicacion (intercambiables): reciben los items expandidos y
devuelven colocaciones + rechazos con razon especifica. Un motor alternativo
(GA/SA/tabu/OR-Tools, o cualquier otra familia) se evalua en paralelo
implementando el protocolo `Optimizador` (no hace falta heredar de
`OptimizadorExtremePoints`, esa es solo la base compartida de FFD/BFD) y
llamando a `registrar_optimizador(...)` - sin tocar `motor.py` ni el resto
del dominio."""

from __future__ import annotations

from abc import ABC, abstractmethod
from collections.abc import Iterable, Iterator
from typing import Protocol

from . import validadores as v
from .dominio import (
    Colocacion,
    Contenedor,
    EstrategiaOptimizacion,
    ItemCarga,
    ParametrosOptimizacion,
    PuntoExtremo,
    Rechazo,
    orientaciones_de,
)


class Optimizador(Protocol):
    nombre: str

    def optimizar(
        self, items: list[ItemCarga], contenedor: Contenedor, parametros: ParametrosOptimizacion,
        fijas: list[Colocacion] | None = None,
    ) -> tuple[list[Colocacion], list[Rechazo]]: ...


class OptimizadorExtremePoints(ABC):
    """Base comun: colocacion por PUNTOS EXTREMOS. Mantiene la lista de
    esquinas candidatas (inicialmente el origen; cada colocacion aporta 3
    nuevas) y para cada item evalua (punto x orientacion permitida) validando
    limites, colision, tolerancias, soporte minimo, apilamiento, peso y
    compatibilidad. FFD toma el primer punto valido (orden bajo-cerca de la
    puerta segun la secuencia); BFD puntua todos los candidatos y toma el mejor."""

    nombre: str

    def _ordenar(self, items: list[ItemCarga], p: ParametrosOptimizacion) -> Iterable[ItemCarga]:
        """Orden de los items antes de colocar (First/Best FIT DECREASING)."""
        if p.estrategia == EstrategiaOptimizacion.MAXIMIZAR_PESO:
            return sorted(items, key=lambda i: (i.peso_kg, i.volumen_mm3), reverse=True)
        if p.estrategia == EstrategiaOptimizacion.PRIORIZAR_PEDIDOS_CRITICOS:
            return sorted(items, key=lambda i: (i.prioridad, -i.volumen_mm3))
        if p.estrategia in (
            EstrategiaOptimizacion.PRIORIZAR_SECUENCIA_ENTREGA,
            EstrategiaOptimizacion.MINIMIZAR_MOVIMIENTOS,
        ):
            # Se coloca PRIMERO lo que se entrega DE ULTIMO (queda al fondo).
            return sorted(items, key=lambda i: (i.secuencia_entrega, i.volumen_mm3), reverse=True)
        return sorted(items, key=lambda i: (i.volumen_mm3, i.area_base_mm2, i.peso_kg), reverse=True)

    def optimizar(
        self, items: list[ItemCarga], cont: Contenedor, p: ParametrosOptimizacion,
        fijas: list[Colocacion] | None = None,
    ) -> tuple[list[Colocacion], list[Rechazo]]:
        colocadas: list[Colocacion] = list(fijas or [])
        rechazos: list[Rechazo] = []
        # El punto extremo inicial es la primera esquina candidata
        # que `dentro_de_limites` puede aceptar. Esa funcion exige x >= t / y >= t
        # (tolerancia_paredes_mm) - sembrar el origen en (0,0,0) cuando t > 0 deja
        # ese punto SIEMPRE invalido, y como ningun item llega a colocarse nunca se
        # generan mas puntos (`_agregar_puntos` solo parte de colocaciones reales):
        # el vehiculo queda inutilizable por completo, vacio o no. Con t = 0
        # (todos los presets/demo) el punto es identico a antes.
        t = cont.tolerancia_paredes_mm
        puntos: list[PuntoExtremo] = [PuntoExtremo(t, t, 0)]
        for f in colocadas:
            _agregar_puntos(puntos, f)

        peso_actual = sum(c.item.peso_kg for c in colocadas)
        secuencia = len(colocadas)

        for item in self._ordenar(items, p):
            # Vetos previos (razon especifica).
            if not v.pasa_por_puerta(item, cont):
                rechazos.append(Rechazo(
                    item, "PUERTA",
                    f"Ninguna orientacion permitida pasa por la puerta de "
                    f"{cont.puerta_ancho_mm:.0f}x{cont.puerta_alto_mm:.0f} mm.",
                ))
                continue
            if v.excede_peso_total(peso_actual, item, cont):
                rechazos.append(Rechazo(
                    item, "PESO_TOTAL",
                    f"Cargar {item.peso_kg:.0f} kg superaria la capacidad de "
                    f"{cont.capacidad_kg:.0f} kg (cargados {peso_actual:.0f} kg).",
                ))
                continue
            incompatibilidad = v.validar_compatibilidad(item, colocadas)
            if incompatibilidad is not None:
                rechazos.append(Rechazo(item, "COMPATIBILIDAD", f"No cargada: {incompatibilidad}."))
                continue

            candidato = self._elegir_posicion(item, cont, p, colocadas, puntos)
            if candidato is None:
                rechazos.append(Rechazo(item, "SIN_ESPACIO", _razon_sin_espacio(item, cont, colocadas)))
                continue

            secuencia += 1
            col = candidato
            col.secuencia_cargue = secuencia
            soporte_pct, soportes = v.calcular_soporte(col.x, col.y, col.z, col.orientacion, colocadas)
            principal = v.soporte_principal(soportes)
            col.porcentaje_soporte = soporte_pct
            col.soporte_id = principal.item.id if principal else None
            if soportes:
                _acumular_peso_encima(soportes, item.peso_kg, colocadas)
            col.estado_estabilidad = (
                "estable"
                if soporte_pct >= p.soporte_recomendado
                and v.centro_de_masa_soportado(col.x, col.y, col.z, col.orientacion, principal)
                else "advertencia"
            )

            colocadas.append(col)
            _agregar_puntos(puntos, col)
            peso_actual += item.peso_kg

        return colocadas, rechazos

    @abstractmethod
    def _elegir_posicion(
        self, item: ItemCarga, cont: Contenedor, p: ParametrosOptimizacion,
        colocadas: list[Colocacion], puntos: list[PuntoExtremo],
    ) -> Colocacion | None:
        """Elige (punto, orientacion) para el item; None si no hay posicion valida."""

    def _candidatos(
        self, item: ItemCarga, cont: Contenedor, p: ParametrosOptimizacion,
        colocadas: list[Colocacion], puntos: list[PuntoExtremo],
    ) -> Iterator[tuple[Colocacion, float]]:
        """Candidatos validos (punto x orientacion) con su puntuacion heuristica."""
        for punto in puntos:
            for o in orientaciones_de(item):
                if not v.dentro_de_limites(punto.x, punto.y, punto.z, o, cont):
                    continue
                if v.colisiona_con_alguna(punto.x, punto.y, punto.z, o, colocadas, cont.tolerancia_entre_unidades_mm):
                    continue

                soporte_pct, soportes = v.calcular_soporte(punto.x, punto.y, punto.z, o, colocadas)
                if soporte_pct < p.soporte_minimo:
                    continue  # sin unidades flotando
                if v.validar_apilamiento(item, punto.z, soportes, colocadas) is not None:
                    continue

                yield (
                    Colocacion(item=item, x=punto.x, y=punto.y, z=punto.z, orientacion=o),
                    _puntuar_posicion(punto, o, item, cont, soporte_pct),
                )


def _puntuar_posicion(pt: PuntoExtremo, o, item: ItemCarga, cont: Contenedor, soporte: float) -> float:
    """Heuristica de posicion (mayor = mejor): preferir posiciones BAJAS,
    LEJOS de la puerta cuando la entrega es tardia (y cerca cuando es
    temprana), pegadas a las paredes, con soporte completo - y penalizar
    huecos pequenos."""
    puntaje = 0.0
    puntaje -= pt.z / cont.alto_mm * 40  # bajo es mejor
    puntaje += soporte * 20  # apoyo completo

    col = Colocacion(item=item, x=pt.x, y=pt.y, z=pt.z, orientacion=o)
    dist_puerta = v.distancia_a_puerta(col, cont) / max(1.0, cont.largo_mm)
    puntaje += (1 - dist_puerta) * 15 if item.secuencia_entrega <= 1 else dist_puerta * 15

    pegado_y = 5 if (pt.y == 0 or pt.y + o.ancho_mm == cont.ancho_mm) else 0
    puntaje += pegado_y
    return puntaje


def _agregar_puntos(puntos: list[PuntoExtremo], c: Colocacion) -> None:
    """Poda: elimina los puntos que quedan DENTRO del volumen ocupado por la
    nueva colocacion (intervalos semiabiertos: los puntos sobre las caras
    +X/+Y/+Z sobreviven porque si son apoyables). Un punto interior nunca
    produce una colocacion valida (colisiona), asi que quitarlo no pierde
    posiciones y evita que la lista crezca sin control."""
    puntos[:] = [
        p for p in puntos
        if not (c.x <= p.x < c.fin_x and c.y <= p.y < c.fin_y and c.z <= p.z < c.fin_z)
    ]

    for nuevo in (
        PuntoExtremo(c.fin_x, c.y, c.z),
        PuntoExtremo(c.x, c.fin_y, c.z),
        PuntoExtremo(c.x, c.y, c.fin_z),
    ):
        if nuevo not in puntos:
            puntos.append(nuevo)


def _acumular_peso_encima(soportes, peso_kg: float, colocadas: list[Colocacion]) -> None:
    """Cada soporte recibe su parte proporcional del peso (por fraccion de
    apoyo) y ese peso baja por su propia pila (aproximacion conservadora por
    la cadena principal)."""
    for apoyo in soportes:
        peso_recibido = peso_kg * apoyo.fraccion
        actual: Colocacion | None = apoyo.colocacion
        visitados = 0
        while actual is not None and visitados < 50:
            actual.peso_encima_kg += peso_recibido
            actual = (
                next((c for c in colocadas if c.item.id == actual.soporte_id), None)
                if actual.soporte_id is not None else None
            )
            visitados += 1


def _cabe_en_alguna_orientacion(item: ItemCarga, cont: Contenedor) -> bool:
    """¿Alguna orientacion del item entra en las dimensiones internas del
    vehiculo, sin importar colocadas/tolerancias? Si NINGUNA entra, el
    rechazo no tiene nada que ver con fragmentacion - el item no cabria ni en
    un vehiculo completamente vacio."""
    return any(
        o.largo_mm <= cont.largo_mm and o.ancho_mm <= cont.ancho_mm and o.alto_mm <= cont.alto_mm
        for o in orientaciones_de(item)
    )


def _razon_sin_espacio(item: ItemCarga, cont: Contenedor, colocadas: list[Colocacion]) -> str:
    """Razon util cuando no hubo posicion: distingue volumen agotado, la unidad
    excediendo el vehiculo completo, o fragmentacion real."""
    if not _cabe_en_alguna_orientacion(item, cont):
        o = orientaciones_de(item)[0]
        return (
            f"La unidad de {o.largo_mm:.0f}x{o.ancho_mm:.0f}x{o.alto_mm:.0f} mm excede las dimensiones "
            f"internas del vehiculo ({cont.largo_mm:.0f}x{cont.ancho_mm:.0f}x{cont.alto_mm:.0f} mm) en todas "
            "sus orientaciones posibles: no cabria aunque el vehiculo estuviera completamente vacio."
        )
    volumen_libre = cont.volumen_util_mm3 - sum(c.volumen_mm3 for c in colocadas)
    if volumen_libre < item.volumen_mm3:
        return (
            f"Volumen libre insuficiente: quedan {volumen_libre / 1_000_000_000:.2f} m3 "
            f"y la unidad requiere {item.volumen_mm3 / 1_000_000_000:.2f} m3."
        )
    o = orientaciones_de(item)[0]
    return (
        f"Aunque el volumen total libre alcanza ({volumen_libre / 1_000_000_000:.2f} m3), "
        f"no queda un espacio continuo de al menos {o.largo_mm:.0f}x{o.ancho_mm:.0f}x{o.alto_mm:.0f} mm "
        "con soporte y sin colisiones (espacio fragmentado o restricciones de apilamiento)."
    )


class FirstFitDecreasing(OptimizadorExtremePoints):
    """First Fit Decreasing: primer candidato valido en orden heuristico de puntos."""

    nombre = "ffd"

    def _elegir_posicion(self, item, cont, p, colocadas, puntos):
        # Orden estable de puntos: bajos primero, luego lejos de la puerta.
        puntos.sort(key=lambda pt: (pt.z, pt.x, pt.y))
        for col, _ in self._candidatos(item, cont, p, colocadas, puntos):
            return col
        return None


class BestFitDecreasing(OptimizadorExtremePoints):
    """Best Fit Decreasing: evalua todos los candidatos y toma el de mayor puntaje."""

    nombre = "bfd"

    def _elegir_posicion(self, item, cont, p, colocadas, puntos):
        mejor: tuple[Colocacion, float] | None = None
        for candidato in self._candidatos(item, cont, p, colocadas, puntos):
            if mejor is None or candidato[1] > mejor[1]:
                mejor = candidato
        return mejor[0] if mejor else None


# Registro de algoritmos disponibles, por nombre - abierto a extension (ver
# docstring del modulo). `motor.py` y la capa de aplicacion resuelven el
# algoritmo pedido contra este registro, nunca por un if/else propio.
_REGISTRO: dict[str, type[Optimizador]] = {}


def registrar_optimizador(nombre: str, clase: type[Optimizador]) -> None:
    """Agrega (o reemplaza) un algoritmo de ubicacion. `clase` debe ser
    instanciable sin argumentos e implementar el protocolo `Optimizador`."""
    _REGISTRO[nombre.lower()] = clase


def algoritmos_disponibles() -> frozenset[str]:
    return frozenset(_REGISTRO)


def obtener_optimizador(nombre: str) -> Optimizador:
    """Devuelve una instancia nueva del algoritmo registrado bajo `nombre`
    (case-insensitive); BFD si `nombre` no esta registrado (mismo default
    que tenia el `if/else` original)."""
    clase = _REGISTRO.get(nombre.lower(), BestFitDecreasing)
    return clase()


registrar_optimizador("ffd", FirstFitDecreasing)
registrar_optimizador("bfd", BestFitDecreasing)
