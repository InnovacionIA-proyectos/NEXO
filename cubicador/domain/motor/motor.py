"""Orquestador del motor: ejecuta el algoritmo elegido, anota accesibilidad,
evalua el plan y produce metricas e incidencias. No conoce persistencia ni
HTTP - esa frontera la pone la capa de aplicacion/infraestructura."""

from __future__ import annotations

from dataclasses import dataclass, field

from . import puntuacion as pt
from . import validadores as v
from .dominio import (
    Colocacion,
    Contenedor,
    IncidenciaValidacion,
    ItemCarga,
    ParametrosOptimizacion,
    ResultadoCubicacion,
)
from .optimizadores import obtener_optimizador


@dataclass(frozen=True)
class MetricasCubicacion:
    """Indicadores del plan."""

    volumen_interno_m3: float
    volumen_utilizado_m3: float
    porcentaje_volumen: float
    area_piso_m2: float
    area_piso_utilizada_m2: float
    porcentaje_piso: float
    peso_maximo_kg: float
    peso_cargado_kg: float
    porcentaje_peso: float
    unidades_totales: int
    unidades_cargadas: int
    unidades_no_cargadas: int
    pedidos_completos: int
    pedidos_parciales: int
    destinos: int
    altura_maxima_mm: float
    centro_gravedad_mm: list[float]  # x, y, z
    peso_por_eje_kg: list[float]
    diferencia_lados_kg: float  # |izquierda - derecha| respecto al eje longitudinal
    advertencias: int
    violaciones_criticas: int
    puntaje: float
    puntajes_parciales: dict[str, float] = field(default_factory=dict)
    espacio_vacio_util_m3: float = 0.0
    espacio_fragmentado_m3: float = 0.0


class MotorCubicacion3D:
    def optimizar(
        self, items: list[ItemCarga], contenedor: Contenedor, parametros: ParametrosOptimizacion,
        fijas: list[Colocacion] | None = None,
    ) -> ResultadoCubicacion:
        colocadas, rechazos = obtener_optimizador(parametros.algoritmo).optimizar(items, contenedor, parametros, fijas)
        resultado = ResultadoCubicacion(colocaciones=colocadas, rechazos=rechazos, contenedor=contenedor)
        resultado.incidencias = self.validar(resultado, parametros)
        total, parciales = pt.evaluar(resultado, items, parametros)
        resultado.puntaje = total
        resultado.puntajes_parciales = parciales
        return resultado

    def validar(self, r: ResultadoCubicacion, p: ParametrosOptimizacion) -> list[IncidenciaValidacion]:
        """Validacion integral del plan (tambien usable tras ajustes manuales)."""
        incidencias: list[IncidenciaValidacion] = []
        cont = r.contenedor
        cols = r.colocaciones

        # Geometria dura: limites y colisiones (deben ser cero tras el motor;
        # el ajuste manual puede introducirlas y aqui se detectan).
        for c in cols:
            if not v.dentro_de_limites(c.x, c.y, c.z, c.orientacion, cont):
                incidencias.append(IncidenciaValidacion(
                    "critica", "FUERA_DE_LIMITES", "Unidad fuera de los limites",
                    f"'{c.item.descripcion}' sobresale del espacio interno o invade la tolerancia contra paredes.",
                    "Reubicar la unidad dentro del contenedor.", c.item.id,
                ))

            otras = [o for o in cols if o is not c]
            if v.colisiona_con_alguna(c.x, c.y, c.z, c.orientacion, otras, cont.tolerancia_entre_unidades_mm, c.item.id):
                incidencias.append(IncidenciaValidacion(
                    "critica", "COLISION", "Colision entre unidades",
                    f"'{c.item.descripcion}' se superpone con otra unidad o viola la tolerancia minima.",
                    "Separar las unidades en conflicto.", c.item.id,
                ))

            soporte, soportes = v.calcular_soporte(c.x, c.y, c.z, c.orientacion, otras)
            principal = v.soporte_principal(soportes)
            c.porcentaje_soporte = soporte
            if soporte < p.soporte_minimo:
                c.estado_estabilidad = "inestable"
                incidencias.append(IncidenciaValidacion(
                    "critica", "SOPORTE_INSUFICIENTE", "Soporte insuficiente",
                    f"'{c.item.descripcion}' solo tiene {soporte:.0%} de su base apoyada "
                    f"(minimo absoluto {p.soporte_minimo:.0%}).",
                    "Bajar la unidad o apoyarla sobre una superficie completa.", c.item.id,
                ))
            elif soporte < p.soporte_recomendado or not v.centro_de_masa_soportado(c.x, c.y, c.z, c.orientacion, principal):
                c.estado_estabilidad = "advertencia"
                incidencias.append(IncidenciaValidacion(
                    "advertencia", "ESTABILIDAD", "Estabilidad aceptable con advertencia",
                    f"'{c.item.descripcion}': soporte {soporte:.0%} (recomendado {p.soporte_recomendado:.0%}) "
                    "o centro de masa cerca del borde del apoyo.",
                    "Verificar amarre o mejorar el apoyo.", c.item.id,
                ))

            # Apilamiento: un ajuste manual puede dejar una unidad sobre otra no
            # apilable/fragil o excediendo niveles; se revalida contra todos los soportes.
            error_apilamiento = v.validar_apilamiento(c.item, c.z, soportes, otras)
            if error_apilamiento is not None:
                incidencias.append(IncidenciaValidacion(
                    "critica", "APILAMIENTO_INVALIDO", "Apilamiento invalido",
                    f"'{c.item.descripcion}': {error_apilamiento}.",
                    "Reubicar la unidad o cambiar el orden de apilado.", c.item.id,
                ))

        # Peso total y por eje.
        peso_total = sum(c.item.peso_kg for c in cols)
        if peso_total > cont.capacidad_kg:
            incidencias.append(IncidenciaValidacion(
                "critica", "PESO_TOTAL", "Peso total excedido",
                f"Cargados {peso_total:.0f} kg de {cont.capacidad_kg:.0f} kg permitidos.",
                "Retirar unidades o usar un vehiculo de mayor capacidad.",
            ))

        por_eje = v.estimar_peso_por_eje(cols, cont)
        ejes = sorted(cont.ejes, key=lambda e: e.posicion_mm)
        for i, eje in enumerate(ejes):
            if por_eje[i] > eje.capacidad_kg:
                incidencias.append(IncidenciaValidacion(
                    "critica", "EJE_EXCEDIDO", f"Capacidad del eje {i + 1} excedida (estimacion)",
                    f"Estimados {por_eje[i]:.0f} kg sobre {eje.capacidad_kg:.0f} kg permitidos. "
                    "La distribucion por ejes es una ESTIMACION OPERATIVA, no una certificacion legal.",
                    "Redistribuir la carga a lo largo del vehiculo.",
                ))

        # Secuencia de entrega / accesibilidad.
        incidencias.extend(v.anotar_accesibilidad(cols, cont))

        return incidencias

    def metricas(self, r: ResultadoCubicacion, todas: list[ItemCarga]) -> MetricasCubicacion:
        cont = r.contenedor
        cols = r.colocaciones
        m3 = 1_000_000_000.0

        vol_interno = cont.volumen_util_mm3 / m3
        vol_usado = sum(c.volumen_mm3 for c in cols) / m3

        area_piso = cont.largo_mm * cont.ancho_mm / 1_000_000.0
        area_usada = sum(c.orientacion.area_base_mm2 for c in cols if c.z == 0) / 1_000_000.0

        peso_cargado = sum(c.item.peso_kg for c in cols)
        altura_max = max((c.fin_z for c in cols), default=0.0)

        cg_x = cg_y = cg_z = 0.0
        if peso_cargado > 0:
            cg_x = sum(c.centro_masa[0] * c.item.peso_kg for c in cols) / peso_cargado
            cg_y = sum(c.centro_masa[1] * c.item.peso_kg for c in cols) / peso_cargado
            cg_z = sum(c.centro_masa[2] * c.item.peso_kg for c in cols) / peso_cargado

        mitad = cont.ancho_mm / 2
        izquierda = sum(c.item.peso_kg for c in cols if c.centro_masa[1] < mitad)
        derecha = peso_cargado - izquierda

        # Pedidos completos vs parciales (por numero de pedido).
        cargados_por_pedido: dict[str, int] = {}
        for c in cols:
            clave = c.item.pedido or ""
            cargados_por_pedido[clave] = cargados_por_pedido.get(clave, 0) + 1
        total_por_pedido: dict[str, int] = {}
        for i in todas:
            clave = i.pedido or ""
            total_por_pedido[clave] = total_por_pedido.get(clave, 0) + 1
        completos = sum(1 for k, tot in total_por_pedido.items() if cargados_por_pedido.get(k, 0) == tot)
        parciales_ped = sum(
            1 for k, tot in total_por_pedido.items()
            if 0 < cargados_por_pedido.get(k, 0) < tot
        )

        # Espacio vacio: utilizable si sobre el "horizonte" de la carga cabe al
        # menos la unidad no cargada mas pequena; el resto se considera fragmentado.
        vacio = vol_interno - vol_usado
        min_rechazo = min((x.item.volumen_mm3 for x in r.rechazos), default=0.0) / m3
        fragmentado = (
            round(vacio * 0.5, 2)
            if any(x.codigo_razon == "SIN_ESPACIO" for x in r.rechazos) and vacio >= min_rechazo
            else 0.0
        )

        return MetricasCubicacion(
            round(vol_interno, 2), round(vol_usado, 2),
            round(vol_usado / vol_interno * 100, 1) if vol_interno > 0 else 0.0,
            round(area_piso, 2), round(area_usada, 2),
            round(area_usada / area_piso * 100, 1) if area_piso > 0 else 0.0,
            cont.capacidad_kg, peso_cargado,
            round(peso_cargado / cont.capacidad_kg * 100, 1) if cont.capacidad_kg > 0 else 0.0,
            len(todas), len(cols), len(r.rechazos),
            completos, parciales_ped,
            len({i.destino for i in todas if i.destino}),
            altura_max,
            [round(cg_x), round(cg_y), round(cg_z)],
            [round(x) for x in v.estimar_peso_por_eje(cols, cont)],
            abs(izquierda - derecha),
            sum(1 for i in r.incidencias if i.severidad == "advertencia"),
            sum(1 for i in r.incidencias if i.severidad == "critica"),
            r.puntaje, r.puntajes_parciales,
            round(vacio - fragmentado, 2), fragmentado,
        )
