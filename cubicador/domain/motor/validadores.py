"""Validadores del motor (portados tal cual de cabinaFletes). Todas son
funciones puras: reciben el estado y responden; no mutan colocaciones, salvo
`anotar_accesibilidad` que enriquece metadatos (accesibilidad)."""

from __future__ import annotations

from .dominio import (
    ApoyoSoporte,
    Colocacion,
    Contenedor,
    IncidenciaValidacion,
    ItemCarga,
    Orientacion,
    PosicionPuerta,
    orientaciones_de,
)

# ------------------------------------------------------------- colision


def colisiona(
    ax: float, ay: float, az: float, al: float, aw: float, ah: float,
    bx: float, by: float, bz: float, bl: float, bw: float, bh: float,
    tolerancia: float = 0.0,
) -> bool:
    """AABB: dos cajas NO colisionan si estan separadas en algun eje. La
    tolerancia entre unidades se aplica como separacion minima requerida."""
    separados = (
        ax + al + tolerancia <= bx or bx + bl + tolerancia <= ax
        or ay + aw + tolerancia <= by or by + bw + tolerancia <= ay
        or az + ah <= bz or bz + bh <= az  # en Z el contacto cara a cara es apoyo, no colision
    )
    return not separados


def colisiona_con_alguna(
    x: float, y: float, z: float, o: Orientacion,
    colocadas: list[Colocacion], tolerancia: float, ignorar_item_id: str | None = None,
) -> bool:
    return any(
        c.item.id != ignorar_item_id
        and colisiona(
            x, y, z, o.largo_mm, o.ancho_mm, o.alto_mm,
            c.x, c.y, c.z, c.orientacion.largo_mm, c.orientacion.ancho_mm, c.orientacion.alto_mm,
            tolerancia,
        )
        for c in colocadas
    )


# ------------------------------------------------------------- limites y puerta


def dentro_de_limites(x: float, y: float, z: float, o: Orientacion, c: Contenedor) -> bool:
    """Dentro del contenedor, respetando la tolerancia contra paredes."""
    t = c.tolerancia_paredes_mm
    return (
        x >= t and y >= t and z >= 0
        and x + o.largo_mm <= c.largo_mm - t
        and y + o.ancho_mm <= c.ancho_mm - t
        and z + o.alto_mm <= c.alto_mm
    )


def pasa_por_puerta(item: ItemCarga, c: Contenedor) -> bool:
    """La unidad debe pasar por la puerta en ALGUNA de sus orientaciones
    permitidas (seccion transversal ancho x alto contra la puerta)."""
    for o in orientaciones_de(item):
        seccion_ancho = (
            o.ancho_mm if c.puerta in (PosicionPuerta.TRASERA, PosicionPuerta.FRONTAL) else o.largo_mm
        )
        if seccion_ancho <= c.puerta_ancho_mm and o.alto_mm <= c.puerta_alto_mm:
            return True
    return False


# ------------------------------------------------------------- soporte y apilamiento


def calcular_soporte(
    x: float, y: float, z: float, o: Orientacion, colocadas: list[Colocacion],
) -> tuple[float, list[ApoyoSoporte]]:
    """Area de la base (en 0..1) apoyada sobre el piso o sobre otras unidades
    cuyo tope coincide con la Z de esta unidad. Devuelve TODOS los soportes con
    la fraccion de area que descansa sobre cada uno (una unidad puede puentear
    varias cajas)."""
    if z == 0:
        return 1.0, []  # sobre el piso: soporte completo

    area_base = o.largo_mm * o.ancho_mm
    if area_base <= 0:
        return 0.0, []

    apoyada = 0.0
    soportes: list[ApoyoSoporte] = []
    for c in colocadas:
        if c.fin_z != z:
            continue  # solo lo que termina exactamente donde esta empieza
        solape_x = max(0.0, min(x + o.largo_mm, c.fin_x) - max(x, c.x))
        solape_y = max(0.0, min(y + o.ancho_mm, c.fin_y) - max(y, c.y))
        solape = solape_x * solape_y
        if solape <= 0:
            continue
        apoyada += solape
        soportes.append(ApoyoSoporte(c, solape / area_base))
    return min(1.0, apoyada / area_base), soportes


def soporte_principal(soportes: list[ApoyoSoporte]) -> Colocacion | None:
    """El soporte con mayor fraccion de area apoyada (para CG/encadenamiento);
    None si esta sobre el piso."""
    if not soportes:
        return None
    return max(soportes, key=lambda s: s.fraccion).colocacion


def centro_de_masa_soportado(
    x: float, y: float, z: float, o: Orientacion, soporte: Colocacion | None,
) -> bool:
    """Centro de masa dentro del area apoyada (aproximacion: dentro del
    rectangulo del soporte principal o sobre el piso)."""
    if z == 0 or soporte is None:
        return True
    cx = x + o.largo_mm / 2
    cy = y + o.ancho_mm / 2
    return soporte.x <= cx <= soporte.fin_x and soporte.y <= cy <= soporte.fin_y


def validar_apilamiento(
    item: ItemCarga, z: float, soportes: list[ApoyoSoporte], colocadas: list[Colocacion],
) -> str | None:
    """Reglas de apilamiento sobre TODAS las unidades que quedan debajo (no
    solo la principal): cada soporte debe ser apilable, no exceder niveles ni
    el peso maximo que recibe (proporcional a su fraccion de apoyo) y, si es
    fragil, no cargar una unidad mas pesada. Devuelve la primera violacion
    encontrada, o None si es valido."""
    if z == 0 or not soportes:
        return None

    for apoyo in soportes:
        soporte = apoyo.colocacion
        peso_recibido = item.peso_kg * apoyo.fraccion

        if not soporte.item.apilable:
            return f"la unidad inferior '{soporte.item.descripcion}' esta marcada como no apilable"

        niveles = _niveles_pila(soporte, colocadas)
        if soporte.item.max_niveles > 0 and niveles + 1 > soporte.item.max_niveles:
            return (
                f"'{soporte.item.descripcion}' admite maximo {soporte.item.max_niveles} "
                f"niveles y ya hay {niveles}"
            )

        if soporte.item.max_peso_encima_kg >= 0 and soporte.peso_encima_kg + peso_recibido > soporte.item.max_peso_encima_kg:
            return (
                f"'{soporte.item.descripcion}' soporta maximo {soporte.item.max_peso_encima_kg:.0f} kg encima "
                f"(lleva {soporte.peso_encima_kg:.0f} kg y se agregarian {peso_recibido:.0f} kg)"
            )

        if soporte.item.fragil and item.peso_kg > soporte.item.peso_kg:
            return f"'{soporte.item.descripcion}' es fragil y no debe cargar una unidad mas pesada que ella"

        # Issue #154: el peso tambien baja hacia los ancestros INDIRECTOS de la
        # pila (2+ niveles debajo) - `_acumular_peso_encima` ya se lo suma a toda
        # la cadena via `soporte_id`, pero antes solo se comparaba contra
        # `max_peso_encima_kg` del soporte DIRECTO. Con 3 niveles A-B-C, colocar C
        # nunca revalidaba el limite de A aunque el peso de C SI le llegara.
        ancestro = (
            next((c for c in colocadas if c.item.id == soporte.soporte_id), None)
            if soporte.soporte_id is not None else None
        )
        visitados = 0
        while ancestro is not None and visitados < 50:
            if (
                ancestro.item.max_peso_encima_kg >= 0
                and ancestro.peso_encima_kg + peso_recibido > ancestro.item.max_peso_encima_kg
            ):
                return (
                    f"'{ancestro.item.descripcion}' soporta maximo {ancestro.item.max_peso_encima_kg:.0f} kg "
                    f"encima (lleva {ancestro.peso_encima_kg:.0f} kg y la cadena de apilamiento le agregaria "
                    f"{peso_recibido:.0f} kg)"
                )
            ancestro = (
                next((c for c in colocadas if c.item.id == ancestro.soporte_id), None)
                if ancestro.soporte_id is not None else None
            )
            visitados += 1

    return None


def _niveles_pila(soporte: Colocacion, colocadas: list[Colocacion]) -> int:
    """Cuenta los niveles de la pila bajo un soporte (siguiendo su cadena principal)."""
    niveles = 1
    actual: Colocacion | None = soporte
    while actual is not None and actual.soporte_id is not None:
        actual = next((c for c in colocadas if c.item.id == actual.soporte_id), None)
        niveles += 1
        if niveles > 50:  # defensa contra ciclos
            break
    return niveles


# ------------------------------------------------------------- compatibilidad


def validar_compatibilidad(item: ItemCarga, colocadas: list[Colocacion]) -> str | None:
    """Grupos de compatibilidad distintos no comparten vehiculo."""
    if not item.grupo_compatibilidad or not item.grupo_compatibilidad.strip():
        return None
    conflicto = next(
        (
            c for c in colocadas
            if c.item.grupo_compatibilidad and c.item.grupo_compatibilidad.strip()
            and c.item.grupo_compatibilidad.casefold() != item.grupo_compatibilidad.casefold()
        ),
        None,
    )
    if conflicto is None:
        return None
    return (
        f"grupo '{item.grupo_compatibilidad}' incompatible con '{conflicto.item.grupo_compatibilidad}' "
        f"de '{conflicto.item.descripcion}' ya cargada"
    )


# ------------------------------------------------------------- peso y ejes


def excede_peso_total(peso_actual_kg: float, item: ItemCarga, c: Contenedor) -> bool:
    return peso_actual_kg + item.peso_kg > c.capacidad_kg


def estimar_peso_por_eje(colocadas: list[Colocacion], c: Contenedor) -> list[float]:
    """Estimacion de peso por eje (SIMPLIFICADA): modelo de viga estaticamente
    determinada. El peso de cada unidad se reparte entre dos ejes inversamente
    proporcional a la distancia a cada uno. En VOLADIZO (centro de masa por
    fuera del vano de los ejes) se extrapola con los dos ejes del extremo
    correspondiente: el eje cercano recibe MAS que el peso de la unidad y el
    lejano una fraccion NEGATIVA (efecto palanca: ese eje se descarga). La
    suma se conserva. No incluye la tara ni es una certificacion legal."""
    pesos = [0.0] * len(c.ejes)
    if not c.ejes:
        return pesos
    if len(c.ejes) == 1:
        pesos[0] = sum(x.item.peso_kg for x in colocadas)
        return pesos

    ejes = sorted(c.ejes, key=lambda e: e.posicion_mm)
    for col in colocadas:
        cm = col.centro_masa[0]

        if cm <= ejes[0].posicion_mm:
            i_ant, i_sig = 0, 1
        elif cm >= ejes[-1].posicion_mm:
            i_ant, i_sig = len(ejes) - 2, len(ejes) - 1
        else:
            i_ant = max(i for i, e in enumerate(ejes) if e.posicion_mm <= cm)
            i_sig = min(i for i, e in enumerate(ejes) if e.posicion_mm >= cm)
            if i_ant == i_sig:  # CM justo sobre un eje
                pesos[i_ant] += col.item.peso_kg
                continue

        luz = ejes[i_sig].posicion_mm - ejes[i_ant].posicion_mm
        if luz <= 0:
            pesos[i_sig] += col.item.peso_kg
            continue

        # fraccion_siguiente puede ser < 0 o > 1 en voladizo (efecto palanca).
        fraccion_siguiente = (cm - ejes[i_ant].posicion_mm) / luz
        pesos[i_sig] += col.item.peso_kg * fraccion_siguiente
        pesos[i_ant] += col.item.peso_kg * (1 - fraccion_siguiente)
    return pesos


# ------------------------------------------------------------- secuencia / accesibilidad


def distancia_a_puerta(c: Colocacion, cont: Contenedor) -> float:
    """Distancia hacia la puerta segun su posicion: valores MENORES = mas
    cerca de la puerta (se descarga antes con menos movimientos)."""
    if cont.puerta == PosicionPuerta.TRASERA:
        return cont.largo_mm - c.fin_x
    if cont.puerta == PosicionPuerta.FRONTAL:
        return c.x
    if cont.puerta == PosicionPuerta.COSTADO_DERECHO:
        return cont.ancho_mm - c.fin_y
    return c.y


def anotar_accesibilidad(colocadas: list[Colocacion], cont: Contenedor) -> list[IncidenciaValidacion]:
    """Anota la accesibilidad de cada unidad: bloqueada si otra unidad de
    entrega POSTERIOR queda entre ella y la puerta en su mismo corredor
    (proyeccion del area transversal), parcialmente bloqueada si la tapa una
    de entrega igual o anterior. Devuelve las violaciones de secuencia detectadas."""
    incidencias: list[IncidenciaValidacion] = []
    for c in colocadas:
        bloqueadores = [otra for otra in colocadas if otra is not c and _tapa(otra, c, cont)]
        if not bloqueadores:
            c.accesibilidad = "accesible"
            continue

        violaciones = [b for b in bloqueadores if b.item.secuencia_entrega > c.item.secuencia_entrega]
        c.accesibilidad = "bloqueada" if violaciones else "parcialmente_bloqueada"
        if violaciones:
            # Issue #153: UNA sola incidencia consolidada por colocacion bloqueada,
            # aunque la tapen varios bloqueadores a la vez (caso realista: varias
            # cajas apiladas/adyacentes en el corredor) - antes se agregaba una
            # IncidenciaValidacion POR CADA bloqueador, duplicando la misma
            # SECUENCIA_BLOQUEADA para el mismo colocacion_item_id.
            detalles = "; ".join(
                f"'{b.item.descripcion}' (entrega {b.item.secuencia_entrega})"
                for b in sorted(violaciones, key=lambda b: b.item.secuencia_entrega)
            )
            incidencias.append(
                IncidenciaValidacion(
                    "advertencia", "SECUENCIA_BLOQUEADA",
                    "Secuencia de entrega bloqueada",
                    f"'{c.item.descripcion}' (entrega {c.item.secuencia_entrega}) queda detras de "
                    f"{detalles}: habra movimientos adicionales en la descarga.",
                    "Reoptimizar priorizando secuencia de entrega o ajustar manualmente.",
                    c.item.id,
                )
            )
    return incidencias


def _tapa(otra: Colocacion, c: Colocacion, cont: Contenedor) -> bool:
    """¿'otra' esta entre 'c' y la puerta, solapando su seccion transversal?"""
    mas_cerca = distancia_a_puerta(otra, cont) < distancia_a_puerta(c, cont)
    if not mas_cerca:
        return False

    if cont.puerta in (PosicionPuerta.TRASERA, PosicionPuerta.FRONTAL):
        solapa_y = otra.y < c.fin_y and c.y < otra.fin_y
        solapa_z = otra.z < c.fin_z and c.z < otra.fin_z
        return solapa_y and solapa_z
    solapa_x = otra.x < c.fin_x and c.x < otra.fin_x
    solapa_z2 = otra.z < c.fin_z and c.z < otra.fin_z
    return solapa_x and solapa_z2
