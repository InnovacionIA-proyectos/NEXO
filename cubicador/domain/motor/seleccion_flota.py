"""Motor de seleccion de flota (S5-HU-17.3).

Reemplaza la heuristica de `cubicacion_rapida.py`, que comparaba contra nueve
arquetipos quemados y decidia por volumen puro. Resuelve en tres partes, como
pide la issue:

 1. **Filtrar candidatos** - carroceria compatible con todas las mercancias,
    dimensiones que admitan cada pieza en alguna orientacion, y capacidad de
    peso suficiente para la pieza mas pesada.
 2. **Construir el llenado con DBLF** - `DeepestBottomLeftFill` de
    `optimizadores.py` como oraculo de empaquetado. No se escribe un segundo
    empacador: se reusa el del motor 3D, con sus validadores de colision,
    soporte y apilamiento.
 3. **Elegir la flota minima** - primero la menor cantidad de vehiculos, y a
    igual cantidad la combinacion mas ajustada. La flota **puede ser mixta**.

**Se decide por `max(%volumen, %peso)` y se reportan los dos.** Una carga de
bebidas llena el peso mucho antes que el volumen; una de cajas vacias al
reves. Mirar solo el volumen - lo que hacia el motor viejo - pide vehiculos de
menos en el primer caso.

Unidades: el catalogo esta en **centimetros y kilos**; el motor 3D trabaja en
**milimetros**. La conversion ocurre solo en `_a_contenedor` y `_a_items`.

Puro: sin I/O, sin SQLAlchemy. El catalogo entra ya resuelto como
`domain.entidades.catalogo.Catalogo`.
"""

from __future__ import annotations

import itertools
from dataclasses import dataclass
from decimal import Decimal

from domain.entidades.catalogo import Catalogo, TipoMercancia, Vehiculo

from .dominio import Contenedor, ItemCarga, ParametrosOptimizacion, PosicionPuerta
from .optimizadores import obtener_optimizador

# Tope de combinaciones de flota que se mandan a empacar de verdad. El filtro
# por volumen y peso deja las candidatas ordenadas de mas ajustada a menos, asi
# que la que sirve aparece entre las primeras; el tope evita que una carga
# patologica dispare miles de corridas de DBLF.
_MAX_INTENTOS_EMPAQUE = 25

CIEN = Decimal(100)


# ------------------------------------------------------------------ entrada


@dataclass(frozen=True)
class MercanciaSolicitada:
    """Una linea de la solicitud: que mercancia, cuantas unidades y de que medida.

    `peso_kg` es **por unidad**, no el total - igual que en el contrato
    (seccion 2.2 de la hoja de ruta).
    """

    tipo: TipoMercancia
    cantidad: int
    largo_cm: Decimal
    ancho_cm: Decimal
    alto_cm: Decimal
    peso_kg: Decimal

    @property
    def volumen_unitario_m3(self) -> Decimal:
        return (self.largo_cm * self.ancho_cm * self.alto_cm) / Decimal(1_000_000)

    @property
    def volumen_total_m3(self) -> Decimal:
        return self.volumen_unitario_m3 * self.cantidad

    @property
    def peso_total_kg(self) -> Decimal:
        return self.peso_kg * self.cantidad


@dataclass(frozen=True)
class MedidasEstandar:
    """Tope por pieza antes de considerar la carga extradimensionada."""

    largo_max_cm: Decimal
    ancho_max_cm: Decimal
    alto_max_cm: Decimal
    peso_max_kg: Decimal


@dataclass(frozen=True)
class SolicitudRecomendacion:
    mercancias: tuple[MercanciaSolicitada, ...]
    medidas_estandar: MedidasEstandar
    clase_viaje_preferida: str | None = None


# ------------------------------------------------------------------ salida


@dataclass(frozen=True)
class MercanciaEnVehiculo:
    codigo_mercancia: str
    cantidad: int
    peso_kg: Decimal
    volumen_m3: Decimal


@dataclass(frozen=True)
class VehiculoRecomendado:
    numero: int
    codigo_vehiculo: str
    nombre_vehiculo: str
    codigo_carroceria: str
    peso_total_kg: Decimal
    volumen_total_m3: Decimal
    porcentaje_llenado_volumen: Decimal
    porcentaje_llenado_peso: Decimal
    alerta_bajo_llenado: bool
    heterogenea: bool
    mercancias: tuple[MercanciaEnVehiculo, ...]

    @property
    def porcentaje_decisorio(self) -> Decimal:
        """El mayor de los dos: es el que manda al comparar flotas."""
        return max(self.porcentaje_llenado_volumen, self.porcentaje_llenado_peso)


@dataclass(frozen=True)
class PiezaExcedida:
    # Los dos identificadores: el id es lo que Cotifly manda en el request y el
    # codigo lo que usa NEXO. Darlos juntos evita que cada consumidor tenga que
    # resolver el otro por su cuenta.
    id_tipo_mercancia: str
    codigo_mercancia: str
    excesos: tuple[str, ...]


@dataclass(frozen=True)
class DeteccionExtradimension:
    hay_extradimension: bool
    medidas_estandar: MedidasEstandar
    piezas: tuple[PiezaExcedida, ...]


# Valores de `motivo_sin_seleccion` - seccion 2.4 de la hoja de ruta.
MOTIVO_EXTRADIMENSIONADO = "EXTRADIMENSIONADO"
MOTIVO_SIN_CANDIDATOS_DIMENSIONALES = "SIN_CANDIDATOS_DIMENSIONALES"
MOTIVO_SUPERA_K_MAX = "SUPERA_K_MAX"
# No esta en la seccion 2.4 del contrato: es una condicion del algoritmo, no
# del negocio. Se reporta aparte para no decir "no cabe en 10 vehiculos" cuando
# lo que paso es que el buscador se quedo sin intentos de empaque.
MOTIVO_BUSQUEDA_AGOTADA = "BUSQUEDA_AGOTADA"
MOTIVO_SIN_CARROCERIA_COMPATIBLE = "SIN_CARROCERIA_COMPATIBLE"

RESULTADO_ASIGNADO = "ASIGNADO"
RESULTADO_SIN_SELECCION = "SIN_SELECCION_AUTOMATICA"

# Clases de viaje derivadas de la carroceria - no hay columna para esto.
CLASE_CONTENEDOR = "Contenedor"
CLASE_EXTRADIMENSIONADO = "Extradimensionado"
CLASE_CARGA_SUELTA = "Carga Suelta"


@dataclass(frozen=True)
class Recomendacion:
    resultado: str
    motivo_sin_seleccion: str | None
    condicionada: bool
    mercancias_condicionadas: tuple[str, ...]
    vehiculos: tuple[VehiculoRecomendado, ...]
    advertencias: tuple[str, ...]
    extradimension: DeteccionExtradimension | None
    clase_viaje: str


class CatalogoInconsistente(Exception):
    """La dominancia por carroceria no se cumple. Ver `validar_dominancia`."""


class BusquedaAgotada(Exception):
    """Se acabaron los intentos de empaque antes de encontrar una flota.

    Es distinto de "no cabe en k_max vehiculos": ahi se agotaron las
    combinaciones posibles, aca se agoto el presupuesto de corridas de DBLF
    (`_MAX_INTENTOS_EMPAQUE`). Confundirlos le diria al usuario que su carga no
    cabe en diez camiones cuando en realidad el buscador se rindio antes.
    """


# ------------------------------------------------------- parte 0: dominancia


def validar_dominancia(catalogo: Catalogo) -> None:
    """Dentro de una carroceria, mas volumen tiene que implicar mas capacidad.

    La seleccion de flota minima descarta un vehiculo cuando otro lo domina en
    las dos dimensiones. Si el catalogo tuviera un vehiculo con mas volumen y
    MENOS capacidad que otro, esa poda eliminaria la respuesta correcta y
    devolveria un minimo equivocado **en silencio**: el resultado se ve bien,
    solo pide un camion de mas. Por eso falla ruidosamente al validar y no al
    calcular.

    Hoy se cumple en las 6 carrocerias con vehiculo asignado.
    """
    for carroceria in catalogo.carrocerias:
        conjunto = [
            (v, v.capacidad_con(carroceria.codigo))
            for v in catalogo.vehiculos
            if v.activo and v.admite(carroceria.codigo)
        ]
        por_volumen = sorted(conjunto, key=lambda par: par[0].volumen_m3)
        for (menor, cap_menor), (mayor, cap_mayor) in zip(por_volumen, por_volumen[1:]):
            if menor.volumen_m3 < mayor.volumen_m3 and cap_mayor < cap_menor:
                raise CatalogoInconsistente(
                    f"En la carroceria {carroceria.codigo}, {mayor.codigo} tiene mas volumen que "
                    f"{menor.codigo} ({mayor.volumen_m3:.2f} vs {menor.volumen_m3:.2f} m3) pero menos "
                    f"capacidad ({cap_mayor:.0f} vs {cap_menor:.0f} kg). La seleccion de flota minima "
                    "asume que las dos dimensiones ordenan igual; con esta fila devolveria un minimo "
                    "equivocado sin avisar. Revisar el catalogo antes de seguir."
                )


# ------------------------------------------------------- parte 1: candidatos


def _intentos_de_carroceria(
    catalogo: Catalogo, mercancias: tuple[MercanciaSolicitada, ...],
) -> list[tuple[list[str], list[str]]]:
    """Las carrocerias a probar, en orden de preferencia.

    Devuelve una lista de intentos; cada uno es (carrocerias, mercancias que
    quedan condicionadas por haber usado su secundaria).

    `usar_alternativa = solo_respaldo` (parametro del motor): la secundaria no
    compite con la principal. **Respaldo no significa solo "no hay carroceria
    comun"**: tambien cubre el caso de que la principal exista pero no tenga
    ningun vehiculo asignado, que es lo que le pasa hoy a
    `CARGA_CONTENERIZADA` (su principal `CONTENEDOR_SECO` esta sembrada sin
    vehiculo) y a `VIDRIO_PLANO` (`PORTAVIDRIOS`). Por eso son dos intentos y
    no una sola decision: el segundo solo se evalua si el primero no produjo
    ningun candidato.
    """
    intentos: list[tuple[list[str], list[str]]] = []

    principales = [
        {m.tipo.carroceria_principal.codigo} if m.tipo.carroceria_principal else set()
        for m in mercancias
    ]
    comunes = set.intersection(*principales) if principales else set()
    if comunes:
        intentos.append((sorted(comunes), []))

    if catalogo.parametros.valores.get("usar_alternativa", "solo_respaldo") == "nunca":
        return intentos

    con_respaldo = [
        {c.codigo for c in (m.tipo.carroceria_principal, m.tipo.carroceria_secundaria) if c}
        for m in mercancias
    ]
    ampliadas = set.intersection(*con_respaldo) if con_respaldo else set()
    nuevas = ampliadas - comunes
    if nuevas:
        # Una mercancia queda condicionada si la carroceria que se va a usar es
        # su secundaria y esa secundaria esta marcada como condicionada.
        condicionadas = [
            m.tipo.codigo for m in mercancias
            if m.tipo.secundaria_condicionada
            and m.tipo.carroceria_secundaria is not None
            and m.tipo.carroceria_secundaria.codigo in nuevas
        ]
        intentos.append((sorted(nuevas), condicionadas))

    return intentos


def _candidatos(
    catalogo: Catalogo, carroceria: str, mercancias: tuple[MercanciaSolicitada, ...],
) -> list[tuple[Vehiculo, Decimal]]:
    """Vehiculos que admiten la carroceria y en los que cabe cada pieza.

    El filtro dimensional compara ordenado contra ordenado: una pieza larga y
    angosta entra si cabe girada. El filtro de peso usa la capacidad de ESTA
    combinacion vehiculo-carroceria, no la del chasis (`3S3 + FURGON` son
    30.000 kg, no los 34.000 del chasis).
    """
    salida: list[tuple[Vehiculo, Decimal]] = []
    for vehiculo in catalogo.vehiculos:
        if not vehiculo.activo or not vehiculo.admite(carroceria):
            continue
        capacidad = vehiculo.capacidad_con(carroceria)
        if capacidad is None:
            continue
        caja = sorted([vehiculo.largo_cm, vehiculo.ancho_cm, vehiculo.alto_util_cm])
        cabe_todo = all(
            all(d <= c for d, c in zip(sorted([m.largo_cm, m.ancho_cm, m.alto_cm]), caja))
            and m.peso_kg <= capacidad
            for m in mercancias
        )
        if cabe_todo:
            salida.append((vehiculo, capacidad))
    return salida


# -------------------------------------------------- parte 2: DBLF como oraculo


def _a_contenedor(vehiculo: Vehiculo, capacidad_kg: Decimal) -> Contenedor:
    """Vehiculo del catalogo -> contenedor del motor 3D (cm -> mm).

    Sin tolerancias y con la puerta del ancho y alto completos: aca no se esta
    planificando el cargue real sino preguntando si la carga ENTRA. Las
    restricciones de puerta estan fuera del alcance de la issue, y el catalogo
    no tiene medidas de puerta.
    """
    return Contenedor(
        largo_mm=float(vehiculo.largo_cm) * 10,
        ancho_mm=float(vehiculo.ancho_cm) * 10,
        alto_mm=float(vehiculo.alto_util_cm) * 10,
        capacidad_kg=float(capacidad_kg),
        tara_kg=0.0,
        puerta_ancho_mm=float(vehiculo.ancho_cm) * 10,
        puerta_alto_mm=float(vehiculo.alto_util_cm) * 10,
        puerta=PosicionPuerta.TRASERA,
        ejes=[],
    )


def _a_items(mercancias: tuple[MercanciaSolicitada, ...]) -> list[ItemCarga]:
    """Expande cantidades a unidades fisicas para el empacador.

    Las banderas del catalogo mandan: `Apilable` y `Fragil` tal cual, y
    `RotacionLibre` se traduce a `rotable` - en falso el motor 3D deja la pieza
    en su orientacion original. `grupo_compatibilidad` queda vacio a proposito:
    ese validador separa CUALQUIER par de grupos distintos, y nuestras reglas
    son por pares concretos (ver `_particionar_por_incompatibilidad`).
    """
    items: list[ItemCarga] = []
    for m in mercancias:
        for i in range(m.cantidad):
            items.append(ItemCarga(
                id=f"{m.tipo.codigo}#{i}", sku=m.tipo.codigo, descripcion=m.tipo.nombre,
                pedido=None, cliente=None, destino=None, secuencia_entrega=1,
                largo_mm=float(m.largo_cm) * 10, ancho_mm=float(m.ancho_cm) * 10,
                alto_mm=float(m.alto_cm) * 10, peso_kg=float(m.peso_kg),
                apilable=m.tipo.apilable, rotable=m.tipo.rotacion_libre, fragil=m.tipo.fragil,
            ))
    return items


def _empacar(items: list[ItemCarga], contenedor: Contenedor) -> tuple[list[ItemCarga], list[ItemCarga]]:
    """Llena un vehiculo con DBLF. Devuelve lo que entro y lo que no."""
    optimizador = obtener_optimizador("dblf")
    colocadas, rechazos = optimizador.optimizar(items, contenedor, ParametrosOptimizacion())
    return [c.item for c in colocadas], [r.item for r in rechazos]


# ---------------------------------------------- parte 3: la flota minima


def _particionar_por_incompatibilidad(
    catalogo: Catalogo, mercancias: tuple[MercanciaSolicitada, ...],
) -> list[tuple[MercanciaSolicitada, ...]]:
    """Separa las mercancias que no pueden viajar juntas.

    Las reglas son por PARES de categorias concretas, no "cada categoria con la
    suya": bebidas y cajas de carton son categorias distintas y comparten
    camion sin problema. Por eso no sirve el `grupo_compatibilidad` del motor
    3D, que separa cualquier par de grupos distintos.

    Coloreo voraz. Con las 3 reglas del catalogo alcanza con dos grupos; el
    algoritmo no asume eso y agrega los que hagan falta. El motor viejo vetaba
    la carga entera cuando encontraba un par incompatible - aca se reparte en
    vehiculos distintos, que es lo que haria un planillador.
    """
    grupos: list[list[MercanciaSolicitada]] = []
    for m in mercancias:
        destino = next(
            (
                g for g in grupos
                if all(catalogo.son_incompatibles(m.tipo, otra.tipo) is None for otra in g)
            ),
            None,
        )
        if destino is None:
            grupos.append([m])
        else:
            destino.append(m)
    return [tuple(g) for g in grupos]


def _techo(dividendo: Decimal, divisor: Decimal) -> int:
    if divisor <= 0:
        return 1
    entero = int(dividendo / divisor)
    return entero + 1 if Decimal(entero) * divisor < dividendo else max(entero, 1)


def _repartir(
    combinacion: tuple[tuple[Vehiculo, Decimal], ...], mercancias: tuple[MercanciaSolicitada, ...],
) -> list[list[ItemCarga]] | None:
    """Reparte la carga en los vehiculos de la combinacion, con DBLF.

    Llena del mas grande al mas chico; lo que no entra pasa al siguiente. Si al
    final queda algo sin ubicar, la combinacion no sirve.
    """
    orden = sorted(combinacion, key=lambda par: par[0].volumen_m3, reverse=True)
    restantes = _a_items(mercancias)
    reparto: list[list[ItemCarga]] = []
    for vehiculo, capacidad in orden:
        if not restantes:
            reparto.append([])
            continue
        colocados, sobrantes = _empacar(restantes, _a_contenedor(vehiculo, capacidad))
        if not colocados:
            return None  # no progresa: esta combinacion no va a cerrar
        reparto.append(colocados)
        restantes = sobrantes
    return None if restantes else reparto


def _flota_minima(
    candidatos: list[tuple[Vehiculo, Decimal]], mercancias: tuple[MercanciaSolicitada, ...], k_max: int,
) -> tuple[list[tuple[Vehiculo, Decimal]], list[list[ItemCarga]]] | None:
    """Menor cantidad de vehiculos; a igual cantidad, la combinacion mas ajustada.

    Dos filtros en cascada, por costo: primero volumen y peso totales, que es
    aritmetica, y solo lo que pasa ese filtro se manda a empacar con DBLF. Las
    combinaciones salen ordenadas de menos capacidad sobrante a mas, asi que la
    primera que empaca es tambien la mas ajustada - por eso la flota resulta
    MIXTA sin pedirselo: un Sencillo mas una Minimula sobran menos que dos
    Minimulas.
    """
    total_volumen = sum((m.volumen_total_m3 for m in mercancias), Decimal(0))
    total_peso = sum((m.peso_total_kg for m in mercancias), Decimal(0))
    minimo = max(
        _techo(total_volumen, max(v.volumen_m3 for v, _ in candidatos)),
        _techo(total_peso, max(c for _, c in candidatos)),
    )

    intentos = 0
    for cantidad in range(minimo, k_max + 1):
        viables: list[tuple[Decimal, Decimal, tuple[tuple[Vehiculo, Decimal], ...]]] = []
        for combinacion in itertools.combinations_with_replacement(candidatos, cantidad):
            volumen = sum((v.volumen_m3 for v, _ in combinacion), Decimal(0))
            capacidad = sum((c for _, c in combinacion), Decimal(0))
            if volumen >= total_volumen and capacidad >= total_peso:
                viables.append((volumen, capacidad, combinacion))
        viables.sort(key=lambda t: (t[0], t[1]))

        for _, _, combinacion in viables:
            if intentos >= _MAX_INTENTOS_EMPAQUE:
                raise BusquedaAgotada(
                    f"Se probaron {intentos} combinaciones de flota sin encontrar una que "
                    "empaque la carga.")
            intentos += 1
            reparto = _repartir(combinacion, mercancias)
            if reparto is not None:
                orden = sorted(combinacion, key=lambda par: par[0].volumen_m3, reverse=True)
                return orden, reparto
    return None


# ------------------------------------------- armado del resultado y entrada


def _detectar_extradimension(
    mercancias: tuple[MercanciaSolicitada, ...], medidas: MedidasEstandar,
) -> DeteccionExtradimension:
    """Compara cada pieza contra las medidas estandar.

    Largo y ancho se comparan en la orientacion mas favorable: una pieza larga
    y angosta no es extradimensionada si cabe girada. El alto no rota - una
    pieza de 4 m de alto sigue siendo de 4 m.
    """
    excedidas: list[PiezaExcedida] = []
    for m in mercancias:
        planta = sorted([m.largo_cm, m.ancho_cm], reverse=True)
        excesos: list[str] = []
        if planta[0] > medidas.largo_max_cm:
            excesos.append(f"largo {planta[0]:.1f} cm > {medidas.largo_max_cm:.1f} cm")
        if planta[1] > medidas.ancho_max_cm:
            excesos.append(f"ancho {planta[1]:.1f} cm > {medidas.ancho_max_cm:.1f} cm")
        if m.alto_cm > medidas.alto_max_cm:
            excesos.append(f"alto {m.alto_cm:.1f} cm > {medidas.alto_max_cm:.1f} cm")
        if m.peso_kg > medidas.peso_max_kg:
            excesos.append(f"peso {m.peso_kg:.1f} kg > {medidas.peso_max_kg:.1f} kg")
        if excesos:
            excedidas.append(PiezaExcedida(m.tipo.id, m.tipo.codigo, tuple(excesos)))
    return DeteccionExtradimension(bool(excedidas), medidas, tuple(excedidas))


def _clase_de_viaje(carroceria: str, hay_extradimension: bool) -> str:
    """Derivada de la carroceria, sin columna propia.

    `CONTENEDOR_SECO` y `CONTENEDOR_REFRIGERADO` son dos codigos distintos: se
    compara por PREFIJO, no por igualdad, porque en el catalogo no existe un
    `CONTENEDOR` suelto.
    """
    if hay_extradimension:
        return CLASE_EXTRADIMENSIONADO
    if carroceria.startswith("CONTENEDOR"):
        return CLASE_CONTENEDOR
    return CLASE_CARGA_SUELTA


def _porcentaje(parte: Decimal, total: Decimal) -> Decimal:
    if total <= 0:
        return Decimal(0)
    return (parte * CIEN / total).quantize(Decimal("0.1"))


def _armar_vehiculo(
    numero: int, vehiculo: Vehiculo, capacidad_kg: Decimal, carroceria: str,
    items: list[ItemCarga], mercancias: tuple[MercanciaSolicitada, ...], umbral_alerta: Decimal,
) -> VehiculoRecomendado:
    """Agrega lo que DBLF coloco en un vehiculo y calcula sus dos porcentajes.

    Los porcentajes van contra el volumen GEOMETRICO del vehiculo y contra la
    capacidad de ESTA combinacion vehiculo-carroceria, no contra el chasis.
    """
    por_codigo: dict[str, int] = {}
    for item in items:
        por_codigo[item.sku] = por_codigo.get(item.sku, 0) + 1

    unitarios = {m.tipo.codigo: m for m in mercancias}
    detalle = tuple(
        MercanciaEnVehiculo(
            codigo_mercancia=codigo,
            cantidad=cantidad,
            peso_kg=unitarios[codigo].peso_kg * cantidad,
            volumen_m3=(unitarios[codigo].volumen_unitario_m3 * cantidad).quantize(Decimal("0.01")),
        )
        for codigo, cantidad in sorted(por_codigo.items())
    )

    peso = sum((d.peso_kg for d in detalle), Decimal(0))
    volumen = sum(
        (unitarios[d.codigo_mercancia].volumen_unitario_m3 * d.cantidad for d in detalle),
        Decimal(0),
    )
    pct_volumen = _porcentaje(volumen, vehiculo.volumen_m3)
    pct_peso = _porcentaje(peso, capacidad_kg)

    return VehiculoRecomendado(
        numero=numero,
        codigo_vehiculo=vehiculo.codigo,
        nombre_vehiculo=vehiculo.nombre,
        codigo_carroceria=carroceria,
        peso_total_kg=peso.quantize(Decimal("0.01")),
        volumen_total_m3=volumen.quantize(Decimal("0.01")),
        porcentaje_llenado_volumen=pct_volumen,
        porcentaje_llenado_peso=pct_peso,
        alerta_bajo_llenado=max(pct_volumen, pct_peso) < umbral_alerta * CIEN,
        heterogenea=len(detalle) > 1,
        mercancias=detalle,
    )


def _sin_seleccion(
    motivo: str, clase_viaje: str, advertencias: list[str],
    extradimension: DeteccionExtradimension | None = None,
    condicionadas: tuple[str, ...] = (),
) -> Recomendacion:
    return Recomendacion(
        resultado=RESULTADO_SIN_SELECCION, motivo_sin_seleccion=motivo,
        condicionada=bool(condicionadas), mercancias_condicionadas=condicionadas,
        vehiculos=(), advertencias=tuple(advertencias),
        extradimension=extradimension, clase_viaje=clase_viaje,
    )


def recomendar(catalogo: Catalogo, solicitud: SolicitudRecomendacion) -> Recomendacion:
    """Punto de entrada del motor. Ver el encabezado del modulo.

    No valida que las mercancias existan ni que sean cubicables: eso es del
    borde HTTP (S5-HU-17.4), que devuelve `MERCANCIA_DESCONOCIDA` o
    `MERCANCIA_NO_CUBICABLE` con 400. Aca ya llegan resueltas contra el
    catalogo.
    """
    validar_dominancia(catalogo)

    mercancias = solicitud.mercancias
    advertencias: list[str] = []

    extradimension = _detectar_extradimension(mercancias, solicitud.medidas_estandar)
    condicionadas_tipo = tuple(sorted({m.tipo.codigo for m in mercancias if m.tipo.condicionada}))

    if extradimension.hay_extradimension:
        advertencias.append(
            f"{len(extradimension.piezas)} pieza(s) exceden las medidas estandar: "
            "la carga se trata como extradimensionada."
        )
        return _sin_seleccion(
            MOTIVO_EXTRADIMENSIONADO, CLASE_EXTRADIMENSIONADO, advertencias,
            extradimension, condicionadas_tipo,
        )

    intentos = _intentos_de_carroceria(catalogo, mercancias)
    if not intentos:
        advertencias.append(
            "Las mercancias de la solicitud no comparten ninguna carroceria: no hay un vehiculo "
            "que pueda llevarlas juntas."
        )
        return _sin_seleccion(
            MOTIVO_SIN_CARROCERIA_COMPATIBLE, CLASE_CARGA_SUELTA, advertencias,
            None, condicionadas_tipo,
        )

    k_max = catalogo.parametros.k_max
    umbral = catalogo.parametros.decimal("umbral_alerta_llenado", Decimal("0.75"))
    grupos = _particionar_por_incompatibilidad(catalogo, mercancias)
    if len(grupos) > 1:
        advertencias.append(
            f"La carga se reparte en {len(grupos)} juegos de vehiculos: hay mercancias que no "
            "pueden viajar juntas segun las reglas de incompatibilidad."
        )

    mejor: tuple[str, list[VehiculoRecomendado]] | None = None
    condicionadas_respaldo: list[str] = []
    hubo_candidatos = False
    agotados: list[str] = []
    for carrocerias, condicionadas_del_intento in intentos:
        for carroceria in carrocerias:
            if not _candidatos(catalogo, carroceria, mercancias):
                continue
            hubo_candidatos = True

            vehiculos: list[VehiculoRecomendado] = []
            completo = True
            for grupo in grupos:
                try:
                    resultado = _flota_minima(
                        _candidatos(catalogo, carroceria, grupo), grupo, k_max)
                except BusquedaAgotada as agotada:
                    agotados.append(str(agotada))
                    resultado = None
                if resultado is None:
                    completo = False
                    break
                orden, reparto = resultado
                for (vehiculo, capacidad), items in zip(orden, reparto):
                    if not items:
                        continue  # la combinacion dejo un vehiculo vacio: no se reporta
                    vehiculos.append(_armar_vehiculo(
                        len(vehiculos) + 1, vehiculo, capacidad, carroceria, items, grupo, umbral,
                    ))
            if completo and vehiculos and (mejor is None or len(vehiculos) < len(mejor[1])):
                mejor = (carroceria, vehiculos)
                condicionadas_respaldo = condicionadas_del_intento
        if mejor is not None:
            break  # la secundaria es RESPALDO: no compite con la principal

    if mejor is None:
        if agotados:
            motivo = MOTIVO_BUSQUEDA_AGOTADA
            advertencias.extend(agotados)
        elif hubo_candidatos:
            motivo = MOTIVO_SUPERA_K_MAX
            advertencias.append(
                f"Ninguna combinacion de hasta {k_max} vehiculos acomoda la carga.")
        else:
            motivo = MOTIVO_SIN_CANDIDATOS_DIMENSIONALES
            advertencias.append(
                "Ningun vehiculo del catalogo admite las dimensiones o el peso unitario "
                "de la carga.")
        return _sin_seleccion(motivo, CLASE_CARGA_SUELTA, advertencias, None, condicionadas_tipo)

    carroceria, vehiculos = mejor
    clase_viaje = _clase_de_viaje(carroceria, False)

    preferida = solicitud.clase_viaje_preferida
    if preferida and preferida.strip().casefold() != clase_viaje.casefold():
        advertencias.append(
            f"La clase de viaje marcada en la solicitud ({preferida}) no coincide con la que "
            f"corresponde a la carroceria {carroceria} ({clase_viaje}). Es una preferencia, no "
            "un filtro: el motor corrio completo."
        )

    bajos = [str(v.numero) for v in vehiculos if v.alerta_bajo_llenado]
    if bajos:
        advertencias.append(
            "Vehiculo(s) " + ", ".join(bajos) + " por debajo del "
            f"{umbral * CIEN:.0f}% de llenado."
        )

    condicionadas = tuple(sorted(set(condicionadas_tipo) | set(condicionadas_respaldo)))
    if condicionadas_respaldo:
        advertencias.append(
            "Se uso la carroceria de respaldo para: " + ", ".join(sorted(condicionadas_respaldo)) + "."
        )

    return Recomendacion(
        resultado=RESULTADO_ASIGNADO, motivo_sin_seleccion=None,
        condicionada=bool(condicionadas), mercancias_condicionadas=condicionadas,
        vehiculos=tuple(vehiculos), advertencias=tuple(advertencias),
        extradimension=None, clase_viaje=clase_viaje,
    )
