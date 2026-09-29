"""Nucleo de cubicacion rapida: port en Python del prototipo SICCE (Sistema
Inteligente de Cubicacion, Cargue y Estiba), a su vez portado tal cual de
cabinaFletes (`CubicacionService.cs`). Correspondencia con la fuente:

 - Evaluador de compatibilidad (SICCE-005): 7 criterios con umbrales
   gobernados y logica de bandas POR VETO (nunca se promedia: un bloqueante
   de seguridad rechazado declara Incompatible).
 - Constructor de disposicion (SICCE-001): heuristica de estanterias
   (pesados primero, base y centro), centro de gravedad, margen de
   estabilidad +-30% y factor de utilizacion.
 - Catalogo de unidades (SICCE-004), extendido con el vinculo al costeo:
   config SICE-TAC + carroceria + clase de viaje que representa cada
   arquetipo.

Sin estado, sin persistencia: trabaja en las unidades de la captura (cm/kg);
internamente convierte a metros como el prototipo.
"""

from __future__ import annotations

from dataclasses import dataclass
from math import ceil

# ---- UMBRALES GOBERNADOS (SICCE-005) - cualquier cambio debe documentarse como nueva version de la regla.
_UMBRAL_ADVERTENCIA_PESO_EJE = 0.90  # 90-100% de un eje: advertencia
_UMBRAL_ADVERTENCIA_UTILIZACION = 0.95  # >95% del volumen util: advertencia
_MARGEN_ESTABILIDAD_CG = 0.30  # CG a +-30% del centro geometrico

# Tope de camiones que se propone en una flota. Mismo valor que usa
# application/plan_cargue/desde_linea.py al repartir en cascada.
_MAX_VEHICULOS_FLOTA = 20

# Incompatibilidades declaradas entre categorias (segregacion SICCE-002).
_INCOMPATIBILIDADES: list[tuple[str, str, str]] = [
    ("quimico_oxidante", "combustible", "Riesgo de reaccion / incendio sin separacion certificada."),
    ("quimico_corrosivo", "alimentos", "Riesgo de contaminacion de alimentos."),
]


# ------------------------------------------------------------- DTOs (planos, sin dependencia de FastAPI)


@dataclass
class ItemCubicacion:
    descripcion: str | None = None
    cantidad: int = 1
    largo_cm: float = 0.0
    ancho_cm: float = 0.0
    alto_cm: float = 0.0
    peso_kg: float | None = None
    tipo_mercancia: str | None = None


@dataclass
class CubicacionRequest:
    items: list[ItemCubicacion]
    unidad_ids: list[str] | None = None
    clase_viaje: str | None = None


@dataclass(frozen=True)
class UnidadCubicacion:
    id: str
    nombre: str
    geometria: str  # cerrada | semicerrada | abierta
    control_ambiental: str  # ninguno | refrigeracion
    largo_util_m: float
    ancho_util_m: float
    alto_util_m: float
    peso_bruto_permitido_kg: float
    peso_vacio_kg: float
    capacidad_por_eje_kg: list[float]
    config_vehiculo: str | None
    carroceria: str | None
    clase_viaje: str | None
    clase_contenedor: str | None

    @property
    def capacidad_carga_kg(self) -> float:
        return self.peso_bruto_permitido_kg - self.peso_vacio_kg

    @property
    def volumen_util_m3(self) -> float:
        return self.largo_util_m * self.ancho_util_m * self.alto_util_m


@dataclass(frozen=True)
class CriterioCubicacion:
    criterio: str
    resultado: str  # aprobado | rechazado | no_evaluable
    detalle: str
    criticidad: str  # bloqueante_seguridad | bloqueante | maxima


@dataclass(frozen=True)
class ColocacionDisposicion:
    descripcion: str
    x: float
    y: float
    z: float
    largo_m: float
    ancho_m: float
    alto_m: float
    peso_kg: float


@dataclass(frozen=True)
class Disposicion:
    peso_total_kg: float
    centro_gravedad_m: list[float]
    margen_estabilidad_ok: bool
    factor_utilizacion_volumen: float
    items_sin_ubicar: list[str]
    colocaciones: list[ColocacionDisposicion]


@dataclass(frozen=True)
class MatrizCubicacion:
    unidad: UnidadCubicacion
    criterios: list[CriterioCubicacion]
    banda: str  # compatible | compatible_con_advertencias | incompatible | no_evaluable
    advertencias: list[str]
    disposicion: Disposicion | None


@dataclass(frozen=True)
class SugerenciaVehiculo:
    unidad_id: str
    unidad_nombre: str
    banda: str
    factor_utilizacion: float
    config_vehiculo: str | None
    carroceria: str | None
    clase_viaje: str | None
    clase_contenedor: str | None


@dataclass(frozen=True)
class SugerenciaFlota:
    unidad_id: str
    unidad_nombre: str
    cantidad_vehiculos: int
    utilizacion_promedio: float
    config_vehiculo: str | None
    carroceria: str | None
    clase_viaje: str | None
    clase_contenedor: str | None
    disposiciones: list[Disposicion]
    # S4-HU-12.2: el reparto por peso, que es LA respuesta de la HU.
    # `peso_por_vehiculo_kg` es igual para todos los camiones: esa es la
    # "distribucion equilibrada" que pide el criterio de aceptacion.
    peso_total_kg: float = 0.0
    peso_por_vehiculo_kg: float = 0.0


@dataclass(frozen=True)
class MedidasEstandar:
    largo_max_cm: float
    ancho_max_cm: float
    alto_max_cm: float
    peso_max_kg: float

    @staticmethod
    def por_defecto() -> "MedidasEstandar":
        return MedidasEstandar(1220, 260, 300, 34000)


@dataclass(frozen=True)
class PiezaExcedida:
    descripcion: str
    excesos: list[str]
    largo_cm: float
    ancho_cm: float
    alto_cm: float
    peso_kg: float | None


@dataclass(frozen=True)
class DeteccionExtradimension:
    hay_extradimension: bool
    medidas_estandar: MedidasEstandar
    piezas: list[PiezaExcedida]
    sugerencia: str | None


@dataclass(frozen=True)
class CubicacionResponse:
    matrices: list[MatrizCubicacion]
    sugerencia: SugerenciaVehiculo | None
    advertencias: list[str]
    sugerencia_flota: SugerenciaFlota | None = None
    extradimension: DeteccionExtradimension | None = None


@dataclass(frozen=True)
class _Bulto:
    descripcion: str
    largo: float
    ancho: float
    alto: float
    peso: float


# ------------------------------------------------------------- catalogo de arquetipos (SICCE-004)
#
# S4-HU-12.1: aqui vivia la constante CATALOGO con 9 UnidadCubicacion escritas
# a mano. Ya no existe. El catalogo es UNO SOLO y vive en la tabla
# `vehiculos_cubicacion`; quien llama a `cubicar` trae las unidades y este
# modulo sigue siendo dominio puro, sin saber de donde salieron.
#
# La traduccion de fila de catalogo a UnidadCubicacion la hace la capa de
# aplicacion (application/cubicacion_rapida/casos_de_uso.py::unidad_desde_vehiculo).


def cubicar(
    request: CubicacionRequest, medidas: MedidasEstandar, unidades: list[UnidadCubicacion],
) -> CubicacionResponse:
    """Evalua compatibilidad + disposicion, sugiere vehiculo y detecta extradimension.

    `unidades` entra como parametro desde S4-HU-12.1 (antes era la constante
    CATALOGO de este mismo modulo). El dominio sigue siendo puro: no sabe que
    detras hay una tabla.
    """
    candidatas = (
        [u for u in unidades if u.id in request.unidad_ids] if request.unidad_ids else unidades
    )

    matrices: list[MatrizCubicacion] = []
    for unidad in candidatas:
        criterios = _evaluar(request.items, unidad)
        banda = _calcular_banda(criterios)
        advertencias = [c.detalle for c in criterios if c.resultado == "aprobado" and _es_advertencia(c.detalle)]

        # La disposicion solo se construye para unidades no vetadas: en las
        # incompatibles no aporta y puede confundir.
        disposicion = (
            _construir_disposicion(request.items, unidad)
            if banda in ("compatible", "compatible_con_advertencias") else None
        )
        matrices.append(MatrizCubicacion(unidad, criterios, banda, advertencias, disposicion))

    sugerencia = _sugerir(matrices, request.clase_viaje)

    # La carga no cupo en NINGUN vehiculo individual: si el problema es de
    # capacidad (no de seguridad), se propone una FLOTA del mismo tipo.
    sugerencia_flota: SugerenciaFlota | None = None
    if sugerencia is None and _evaluar_entre_mercancias(request.items).resultado == "aprobado":
        sugerencia_flota = _sugerir_flota(request.items, candidatas, request.clase_viaje)

    globales: list[str] = []

    # S4-HU-12.2: sin peso no hay recomendacion de cuantos camiones. El DTO de
    # la API ya lo exige, pero el puente `crear_desde_linea` arma los items
    # desde `PosicionCarga`, cuyo `peso_kg` SI puede venir nulo desde NEXO.
    # `_expandir` convierte ese None en 0.0, asi que sin este aviso la flota
    # devolveria "ninguna" sin decir que el problema es un dato que falta, no
    # que la carga quepa en un vehiculo.
    sin_peso = [i.descripcion or "sin descripcion" for i in request.items if i.peso_kg is None]
    if sin_peso:
        globales.append(
            f"{len(sin_peso)} item(s) sin peso: no se puede calcular cuantos vehiculos hacen falta. "
            f"Captura el peso de {', '.join(sin_peso[:3])}"
            + (f" y {len(sin_peso) - 3} mas." if len(sin_peso) > 3 else ".")
        )

    if sugerencia_flota is not None:
        globales.append(
            f"La carga no cabe en un solo vehiculo: se requieren "
            f"{sugerencia_flota.cantidad_vehiculos} x {sugerencia_flota.unidad_nombre} "
            f"(utilizacion promedio {sugerencia_flota.utilizacion_promedio:.0%})."
        )
    elif sugerencia is None and matrices and all(m.banda == "incompatible" for m in matrices):
        # ¿Extradimensionado? Si el criterio dimensional rechazo en TODAS las
        # unidades, ninguna estandar sirve: sugerir el flujo extradimensionado.
        dimensional_en_todas = all(
            any(c.criterio == "Compatibilidad Dimensional" and c.resultado == "rechazado" for c in m.criterios)
            for m in matrices
        )
        globales.append(
            "Ninguna unidad estandar admite las dimensiones de la mercancia: tratar como viaje EXTRADIMENSIONADO."
            if dimensional_en_todas else
            "Ninguna unidad del catalogo es compatible con la mercancia evaluada: revisar los criterios rechazados."
        )

    # Deteccion de extradimension por PIEZA contra las medidas estandar parametrizadas.
    extradimension = detectar_extradimension(request.items, medidas)
    if extradimension.hay_extradimension and not any("EXTRADIMENSIONADO" in g for g in globales):
        globales.append(extradimension.sugerencia)  # type: ignore[arg-type]

    return CubicacionResponse(matrices, sugerencia, globales, sugerencia_flota, extradimension)


def detectar_extradimension(items: list[ItemCubicacion], m: MedidasEstandar) -> DeteccionExtradimension:
    """Compara cada pieza contra las medidas estandar y marca las que exceden
    largo, ancho, alto o peso, sugiriendo el tratamiento. Las dimensiones se
    comparan en la orientacion mas favorable: una pieza larga y angosta no es
    extradimensionada si cabe girada."""
    excedidas: list[PiezaExcedida] = []

    for it in items:
        planta = sorted([it.largo_cm, it.ancho_cm], reverse=True)
        largo, ancho = planta[0], planta[1]

        excesos: list[str] = []
        if largo > m.largo_max_cm:
            excesos.append(f"largo {largo:.1f} cm > {m.largo_max_cm:.1f} cm")
        if ancho > m.ancho_max_cm:
            excesos.append(f"ancho {ancho:.1f} cm > {m.ancho_max_cm:.1f} cm")
        if it.alto_cm > m.alto_max_cm:
            excesos.append(f"alto {it.alto_cm:.1f} cm > {m.alto_max_cm:.1f} cm")
        if it.peso_kg is not None and it.peso_kg > m.peso_max_kg:
            excesos.append(f"peso {it.peso_kg:.1f} kg > {m.peso_max_kg:.1f} kg")

        if excesos:
            excedidas.append(PiezaExcedida(
                it.descripcion if it.descripcion and it.descripcion.strip() else f"Pieza {len(excedidas) + 1}",
                excesos, it.largo_cm, it.ancho_cm, it.alto_cm, it.peso_kg,
            ))

    hay = len(excedidas) > 0
    sugerencia = (
        f"{len(excedidas)} pieza(s) exceden las medidas estandar: tratar la solicitud como carga "
        "EXTRADIMENSIONADA (cama baja / permisos de transito) y registrar las medidas y evidencia fotografica."
        if hay else None
    )
    return DeteccionExtradimension(hay, m, excedidas, sugerencia)


def _sugerir(matrices: list[MatrizCubicacion], clase_viaje: str | None) -> SugerenciaVehiculo | None:
    """Vehiculo sugerido: entre las unidades compatibles (o con advertencias si
    no hay plenas), primero las de la clase de viaje de la linea y luego la de
    MAYOR factor de utilizacion = la mas ajustada a la carga."""
    candidatas = [m for m in matrices if m.disposicion is not None and not m.disposicion.items_sin_ubicar]
    if not candidatas:
        return None
    elegida = min(
        candidatas,
        key=lambda m: (
            0 if m.banda == "compatible" else 1,
            0 if clase_viaje is not None and m.unidad.clase_viaje == clase_viaje else 1,
            -m.disposicion.factor_utilizacion_volumen,  # type: ignore[union-attr]
        ),
    )
    d = elegida.disposicion
    assert d is not None
    return SugerenciaVehiculo(
        elegida.unidad.id, elegida.unidad.nombre, elegida.banda, d.factor_utilizacion_volumen,
        elegida.unidad.config_vehiculo, elegida.unidad.carroceria,
        elegida.unidad.clase_viaje, elegida.unidad.clase_contenedor,
    )


def _sugerir_flota(
    items: list[ItemCubicacion], unidades: list[UnidadCubicacion], clase_viaje: str | None,
) -> SugerenciaFlota | None:
    """Cuantos camiones hacen falta y cuanto lleva cada uno, POR PESO (S4-HU-12.2).

    Antes esto era un reparto VORAZ: se llenaba un vehiculo con la heuristica de
    estanterias hasta donde cupiera -por volumen o por peso, lo que se agotara
    primero- y el sobrante pasaba al siguiente. El ultimo camion quedaba casi
    vacio. La HU pide lo contrario: "una distribucion equilibrada", "no solo la
    primera combinacion que quepa".

    Ahora manda el peso:

      cantidad = techo(peso_total / capacidad_de_carga)
      por cami0n = peso_total / cantidad        <- iguales, no el tope y el resto

    El empaque 3D se sigue haciendo, pero con el reparto equilibrado como TECHO
    de cada camion (`limite_peso_kg`), para que las disposiciones digan lo mismo
    que la recomendacion. Si se empacara con la capacidad plena, el primer
    vehiculo se llenaria al maximo y los numeros reportados y los reales se
    contradirian.

    Que veh0culo se elige NO cambia en esta issue: se mantiene el criterio que
    ya existia -clase de viaje, menos camiones, mayor utilizacion- por decision
    explicita de no inventar una regla de negocio que negocio no dio. El texto
    de la issue dice "solo peso (no dimensiones)"; se interpreta que eso aplica
    a CUANTOS camiones, no a cual. Esta diferencia esta documentada a proposito.
    """
    peso_total = sum(b.peso for b in _expandir(items))
    if peso_total <= 0:
        return None  # sin peso no hay nada que repartir: lo resuelve `_sugerir`

    opciones: list[tuple[UnidadCubicacion, list[Disposicion], float]] = []

    for unidad in unidades:
        if _evaluar_ambiental(items, unidad).resultado != "aprobado":
            continue
        if unidad.capacidad_carga_kg <= 0:
            continue

        bultos = _expandir(items)
        if any(not _cabe_en_alguna_orientacion(b, unidad) or b.peso > unidad.capacidad_carga_kg for b in bultos):
            continue

        # El peso da el MINIMO de camiones, no el numero final: una carga
        # voluminosa y liviana puede caber de sobra en peso y no caber en
        # volumen. Se arranca por el minimo y se sube hasta que la carga entre
        # de verdad, repartiendo equilibrado en cada intento.
        cantidad = ceil(peso_total / unidad.capacidad_carga_kg)
        resultado: list[Disposicion] | None = None
        while cantidad <= _MAX_VEHICULOS_FLOTA:
            por_vehiculo = peso_total / cantidad
            disposiciones: list[Disposicion] = []
            restantes = bultos
            while restantes and len(disposiciones) < cantidad:
                # Ultimo camion sin techo: se lleva lo que quede. El reparto por
                # peso es exacto, pero los bultos son indivisibles y el redondeo
                # deja unos kilos fuera del objetivo.
                es_ultimo = len(disposiciones) == cantidad - 1
                colocaciones, sobrantes = _empacar(restantes, unidad, None if es_ultimo else por_vehiculo)
                if not colocaciones:
                    break  # no progresa: este tipo no sirve con ningun reparto
                disposiciones.append(_medir(colocaciones, [], unidad))
                restantes = sobrantes
            if not restantes and disposiciones:
                resultado = disposiciones
                break
            if not disposiciones:
                break
            cantidad += 1  # la geometria pide mas de lo que pedia el peso

        if resultado is None or len(resultado) < 2:
            continue  # un solo camion es trabajo de `_sugerir`

        opciones.append((unidad, resultado, peso_total / len(resultado)))

    if not opciones:
        return None

    mejor_unidad, mejor_disp, mejor_por_vehiculo = min(
        opciones,
        key=lambda o: (
            0 if clase_viaje is not None and o[0].clase_viaje == clase_viaje else 1,
            len(o[1]),
            -(sum(d.factor_utilizacion_volumen for d in o[1]) / len(o[1])),
        ),
    )
    utilizacion_prom = sum(d.factor_utilizacion_volumen for d in mejor_disp) / len(mejor_disp)
    return SugerenciaFlota(
        mejor_unidad.id, mejor_unidad.nombre, len(mejor_disp), round(utilizacion_prom, 3),
        mejor_unidad.config_vehiculo, mejor_unidad.carroceria,
        mejor_unidad.clase_viaje, mejor_unidad.clase_contenedor, mejor_disp,
        peso_total_kg=round(peso_total, 2),
        peso_por_vehiculo_kg=round(mejor_por_vehiculo, 2),
    )


def _cabe_en_alguna_orientacion(b: _Bulto, unidad: UnidadCubicacion) -> bool:
    dims = sorted([b.largo, b.ancho, b.alto])
    dims_unidad = sorted([unidad.largo_util_m, unidad.ancho_util_m, unidad.alto_util_m])
    return not any(d > du for d, du in zip(dims, dims_unidad))


# ------------------------------------------------------------- evaluador (SICCE-005)


def _evaluar(items: list[ItemCubicacion], unidad: UnidadCubicacion) -> list[CriterioCubicacion]:
    criterios = [
        _evaluar_entre_mercancias(items),
        _evaluar_volumetrica(items, unidad),
        _evaluar_dimensional(items, unidad),
        _evaluar_peso(items, unidad),
        _evaluar_distribucion_peso(items, unidad),
        _evaluar_ambiental(items, unidad),
    ]
    criterios.append(_evaluar_seguridad(criterios))
    return criterios


def _clasificar_categoria(item: ItemCubicacion) -> str | None:
    texto = f"{item.tipo_mercancia or ''} {item.descripcion or ''}".lower()
    if any(p in texto for p in ("oxidante", "hipoclorito", "peroxido", "peróxido")):
        return "quimico_oxidante"
    if any(p in texto for p in ("corrosivo", "acido", "ácido")):
        return "quimico_corrosivo"
    if any(p in texto for p in ("textil", "papel", "carton", "cartón", "madera")):
        return "combustible"
    if any(p in texto for p in ("alimento", "comestible", "food")):
        return "alimentos"
    return None


def _evaluar_entre_mercancias(items: list[ItemCubicacion]) -> CriterioCubicacion:
    categorias = [(i, _clasificar_categoria(i)) for i in items]
    for idx, (a, cat_a) in enumerate(categorias):
        for b, cat_b in categorias[idx + 1:]:
            if cat_a is None or cat_b is None or cat_a == cat_b:
                continue
            razon = next(
                (r for x, y, r in _INCOMPATIBILIDADES if (x, y) in ((cat_a, cat_b), (cat_b, cat_a))),
                None,
            )
            if razon is not None:
                return CriterioCubicacion(
                    "Compatibilidad entre Mercancias", "rechazado",
                    f"'{a.descripcion}' y '{b.descripcion}' son incompatibles: {razon}",
                    "bloqueante_seguridad",
                )
    return CriterioCubicacion(
        "Compatibilidad entre Mercancias", "aprobado",
        "No se detectaron incompatibilidades declaradas entre los items evaluados.",
        "bloqueante_seguridad",
    )


def _volumen_m3(i: ItemCubicacion) -> float:
    return i.largo_cm / 100 * (i.ancho_cm / 100) * (i.alto_cm / 100)


def _evaluar_volumetrica(items: list[ItemCubicacion], unidad: UnidadCubicacion) -> CriterioCubicacion:
    total = sum(_volumen_m3(i) * i.cantidad for i in items)
    disponible = unidad.volumen_util_m3
    if total > disponible:
        return CriterioCubicacion(
            "Compatibilidad Volumetrica", "rechazado",
            f"Volumen requerido {total:.2f} m3 excede el disponible {disponible:.2f} m3.",
            "bloqueante",
        )
    utilizacion = total / disponible if disponible > 0 else 0.0
    detalle = f"Volumen requerido {total:.2f} m3 de {disponible:.2f} m3 disponibles ({utilizacion:.0%})."
    if utilizacion > _UMBRAL_ADVERTENCIA_UTILIZACION:
        detalle += " Utilizacion superior al umbral de advertencia: verificar margen de maniobra."
    return CriterioCubicacion("Compatibilidad Volumetrica", "aprobado", detalle, "bloqueante")


def _evaluar_dimensional(items: list[ItemCubicacion], unidad: UnidadCubicacion) -> CriterioCubicacion:
    dims_unidad = sorted([unidad.largo_util_m, unidad.ancho_util_m, unidad.alto_util_m])
    for item in items:
        dims = sorted([item.largo_cm / 100, item.ancho_cm / 100, item.alto_cm / 100])
        if any(d > du for d, du in zip(dims, dims_unidad)):
            return CriterioCubicacion(
                "Compatibilidad Dimensional", "rechazado",
                f"'{item.descripcion}' ({item.largo_cm}x{item.ancho_cm}x{item.alto_cm} cm) "
                "no cabe en ninguna orientacion dentro de la unidad.",
                "bloqueante",
            )
    return CriterioCubicacion(
        "Compatibilidad Dimensional", "aprobado",
        "Todos los items caben individualmente en alguna orientacion.", "bloqueante",
    )


def _evaluar_peso(items: list[ItemCubicacion], unidad: UnidadCubicacion) -> CriterioCubicacion:
    if any(i.peso_kg is None for i in items):
        return CriterioCubicacion(
            "Compatibilidad por Peso", "no_evaluable",
            "Uno o mas items no tienen peso declarado.", "bloqueante_seguridad",
        )
    total = sum(i.peso_kg * i.cantidad for i in items)  # type: ignore[operator]
    capacidad = unidad.capacidad_carga_kg
    if total > capacidad:
        return CriterioCubicacion(
            "Compatibilidad por Peso", "rechazado",
            f"Peso total {total:.0f} kg excede la capacidad de carga {capacidad:.0f} kg.",
            "bloqueante_seguridad",
        )
    return CriterioCubicacion(
        "Compatibilidad por Peso", "aprobado",
        f"Peso total {total:.0f} kg dentro de la capacidad {capacidad:.0f} kg.", "bloqueante_seguridad",
    )


def _evaluar_distribucion_peso(items: list[ItemCubicacion], unidad: UnidadCubicacion) -> CriterioCubicacion:
    ejes = [c for c in unidad.capacidad_por_eje_kg if c > 0]
    if any(i.peso_kg is None for i in items) or not ejes:
        return CriterioCubicacion(
            "Compatibilidad por Distribucion de Peso", "no_evaluable",
            "Informacion insuficiente para estimar el reparto por eje.", "bloqueante_seguridad",
        )

    # La cubicacion rapida no tiene posiciones, asi que se asume un REPARTO
    # UNIFORME (el mejor caso de balance): cada eje soporta total/N.
    total = sum(i.peso_kg * i.cantidad for i in items)  # type: ignore[operator]
    peso_por_eje = total / len(ejes)
    capacidad_minima = min(ejes)
    peor_razon = peso_por_eje / capacidad_minima

    if peor_razon > 1.0:
        return CriterioCubicacion(
            "Compatibilidad por Distribucion de Peso", "rechazado",
            f"Con reparto uniforme cada eje llevaria {peso_por_eje:.0f} kg y el eje mas debil admite "
            f"{capacidad_minima:.0f} kg ({peor_razon:.0%} de su limite). ESTIMACION, no certificacion legal.",
            "bloqueante_seguridad",
        )

    detalle = (
        f"Reparto uniforme estimado: {peso_por_eje:.0f} kg/eje sobre un minimo de {capacidad_minima:.0f} kg "
        f"({peor_razon:.0%} del eje mas debil). ESTIMACION, no certificacion legal."
    )
    if peor_razon > _UMBRAL_ADVERTENCIA_PESO_EJE:
        detalle += f" Umbral de advertencia superado ({_UMBRAL_ADVERTENCIA_PESO_EJE:.0%})."
    return CriterioCubicacion("Compatibilidad por Distribucion de Peso", "aprobado", detalle, "bloqueante_seguridad")


def _evaluar_ambiental(items: list[ItemCubicacion], unidad: UnidadCubicacion) -> CriterioCubicacion:
    requiere_frio = next(
        (i for i in items if "refrigerad" in f"{i.tipo_mercancia or ''} {i.descripcion or ''}".lower()),
        None,
    )
    if requiere_frio is not None and unidad.control_ambiental == "ninguno":
        return CriterioCubicacion(
            "Compatibilidad Ambiental", "rechazado",
            f"'{requiere_frio.descripcion}' requiere control ambiental y la unidad no lo ofrece.",
            "bloqueante",
        )
    return CriterioCubicacion(
        "Compatibilidad Ambiental", "aprobado",
        "Sin requisitos ambientales declarados sin satisfacer.", "bloqueante",
    )


def _evaluar_seguridad(previos: list[CriterioCubicacion]) -> CriterioCubicacion:
    """Veto transversal (criterio 18): cualquier bloqueante de seguridad
    rechazado/no evaluable veta la unidad."""
    veto = next(
        (c for c in previos if c.criticidad == "bloqueante_seguridad" and c.resultado in ("rechazado", "no_evaluable")),
        None,
    )
    if veto is not None:
        return CriterioCubicacion(
            "Compatibilidad de Seguridad", "rechazado",
            f"Vetado por el criterio '{veto.criterio}': {veto.detalle}", "maxima",
        )
    return CriterioCubicacion(
        "Compatibilidad de Seguridad", "aprobado",
        "Ningun criterio de seguridad subyacente fue rechazado.", "maxima",
    )


def _calcular_banda(criterios: list[CriterioCubicacion]) -> str:
    """Bandas por veto: nunca se promedia."""
    if any(c.resultado == "rechazado" for c in criterios):
        return "incompatible"
    if any(c.resultado == "no_evaluable" for c in criterios):
        return "no_evaluable"
    if any(_es_advertencia(c.detalle) for c in criterios):
        return "compatible_con_advertencias"
    return "compatible"


def _es_advertencia(detalle: str) -> bool:
    d = detalle.lower()
    return "advertencia" in d or "umbral" in d


# ------------------------------------------------------------- disposicion (SICCE-001)


def _construir_disposicion(items: list[ItemCubicacion], unidad: UnidadCubicacion) -> Disposicion:
    """Heuristica de estanterias del prototipo: expande cantidades a bultos,
    ordena por peso descendente, coloca en filas sobre el piso (avanza en Y,
    salta fila en X, sube de nivel en Z) y centra el bloque resultante para no
    dejar el centro de gravedad excentrico."""
    colocaciones, sobrantes = _empacar(_expandir(items), unidad)
    return _medir(colocaciones, sobrantes, unidad)


def _expandir(items: list[ItemCubicacion]) -> list[_Bulto]:
    bultos = [
        _Bulto(i.descripcion or "bulto", i.largo_cm / 100, i.ancho_cm / 100, i.alto_cm / 100, i.peso_kg or 0.0)
        for i in items
        for _ in range(i.cantidad)
    ]
    return sorted(bultos, key=lambda b: b.peso, reverse=True)


def _empacar(
    bultos: list[_Bulto], unidad: UnidadCubicacion, limite_peso_kg: float | None = None,
) -> tuple[list[ColocacionDisposicion], list[_Bulto]]:
    """Coloca los bultos en la unidad respetando geometria Y capacidad de
    peso; lo que no quepa vuelve como sobrante (en la flota pasa al siguiente
    vehiculo).

    `limite_peso_kg` (S4-HU-12.2) baja el techo de peso por debajo de la
    capacidad del vehiculo. Lo usa el reparto de flota para que cada camion se
    cargue hasta su porcion EQUILIBRADA y no hasta el tope: sin esto el empaque
    llena el primero al maximo y el ultimo queda casi vacio, que es justo el
    reparto voraz que esta issue vino a reemplazar. Nunca sube por encima de la
    capacidad real.
    """
    tope = unidad.capacidad_carga_kg if limite_peso_kg is None else min(limite_peso_kg, unidad.capacidad_carga_kg)
    colocaciones: list[ColocacionDisposicion] = []
    sobrantes: list[_Bulto] = []
    x = y = z = alto_fila = alto_nivel = peso_acumulado = 0.0

    for b in bultos:
        if peso_acumulado + b.peso > tope:
            sobrantes.append(b)
            continue
        if y + b.ancho > unidad.ancho_util_m:
            y = 0.0
            largos_en_fila = [c.largo_m for c in colocaciones if c.z == z and c.x == x]
            x += max(largos_en_fila) if largos_en_fila else b.largo
            alto_nivel = max(alto_nivel, alto_fila)
            alto_fila = 0.0
        if x + b.largo > unidad.largo_util_m:
            x = y = 0.0
            alto_nivel = max(alto_nivel, alto_fila)
            z += alto_nivel
            alto_nivel = alto_fila = 0.0
        if z + b.alto > unidad.alto_util_m:
            sobrantes.append(b)
            continue
        colocaciones.append(ColocacionDisposicion(b.descripcion, x, y, z, b.largo, b.ancho, b.alto, b.peso))
        y += b.ancho
        alto_fila = max(alto_fila, b.alto)
        peso_acumulado += b.peso
    return colocaciones, sobrantes


def _medir(colocaciones: list[ColocacionDisposicion], sobrantes: list[_Bulto], unidad: UnidadCubicacion) -> Disposicion:
    """Centra el bloque colocado y calcula las propiedades fisicas de la disposicion."""
    sin_ubicar = [b.descripcion for b in sobrantes]
    colocaciones = _centrar(colocaciones, unidad)

    peso_total = sum(c.peso_kg for c in colocaciones)
    cg_x = cg_y = cg_z = 0.0
    if peso_total > 0:
        cg_x = sum((c.x + c.largo_m / 2) * c.peso_kg for c in colocaciones) / peso_total
        cg_y = sum((c.y + c.ancho_m / 2) * c.peso_kg for c in colocaciones) / peso_total
        cg_z = sum((c.z + c.alto_m / 2) * c.peso_kg for c in colocaciones) / peso_total

    margen_ok = (
        abs(cg_x - unidad.largo_util_m / 2) <= unidad.largo_util_m * _MARGEN_ESTABILIDAD_CG
        and abs(cg_y - unidad.ancho_util_m / 2) <= unidad.ancho_util_m * _MARGEN_ESTABILIDAD_CG
    )

    volumen_ocupado = sum(c.largo_m * c.ancho_m * c.alto_m for c in colocaciones)
    utilizacion = volumen_ocupado / unidad.volumen_util_m3 if unidad.volumen_util_m3 > 0 else 0.0

    return Disposicion(
        peso_total,
        [round(cg_x, 2), round(cg_y, 2), round(cg_z, 2)],
        margen_ok,
        round(utilizacion, 3),
        sin_ubicar,
        colocaciones,
    )


def _centrar(colocaciones: list[ColocacionDisposicion], unidad: UnidadCubicacion) -> list[ColocacionDisposicion]:
    """Desplaza el bloque completo para centrar su caja envolvente en el piso de la unidad."""
    if not colocaciones:
        return colocaciones
    min_x = min(c.x for c in colocaciones)
    max_x = max(c.x + c.largo_m for c in colocaciones)
    min_y = min(c.y for c in colocaciones)
    max_y = max(c.y + c.ancho_m for c in colocaciones)
    dx = max((unidad.largo_util_m - (max_x - min_x)) / 2 - min_x, -min_x)
    dy = max((unidad.ancho_util_m - (max_y - min_y)) / 2 - min_y, -min_y)
    return [
        ColocacionDisposicion(
            c.descripcion, round(c.x + dx, 3), round(c.y + dy, 3), c.z, c.largo_m, c.ancho_m, c.alto_m, c.peso_kg,
        )
        for c in colocaciones
    ]
