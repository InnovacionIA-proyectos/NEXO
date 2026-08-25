"""Puente Mesa de Costeo -> Cubicador: crea (o reconstruye) el plan de
cargue de una linea a partir de sus posiciones capturadas, con el vehiculo
sugerido por el cubicador rapido, y lo deja optimizado. Si la carga no cabe
en un vehiculo, reparte en cascada hasta 20 (misma linea_solicitud_id,
numero_vehiculo 1..N) y devuelve el primero de la flota. Equivalente a
CrearPlanDesdeLineaCommand de NEXO."""

from __future__ import annotations

from application.excepciones import NotFoundException, ValidationFailedException
from application.ports.externos import ILineaSolicitudPort, PosicionCarga
from application.ports.repositorios import IPlanCargueRepository, IVehiculoCubicacionRepository
from domain.entidades.entidades import ItemPlanCargue, PlanCargue
from domain.motor import cubicacion_rapida as rapida

from .optimizar import ParametrosOptimizacionInput, optimizar_plan

_MAX_VEHICULOS_FLOTA = 20

# Unidad del cubicador rapido (SICCE) -> nombre del preset del plan 3D.
_PRESET_POR_UNIDAD_RAPIDA = {
    "turbo_estacas": "Turbo con estacas",
    "sencillo_estacas": "Camion sencillo (estacas)",
    "dobletroque_estacas": "Camion rigido dobletroque",
    "furgon_semirremolque": "Tractocamion furgon 13.6 m",
    "plataforma_sencilla": "Plataforma (tractomula)",
    "cama_baja": "Cama baja (lowboy)",
    "contenedor_seco_20": "Contenedor 20 pies",
    "contenedor_seco_40": "Contenedor 40 pies",
    "contenedor_refrigerado_40": "Contenedor 40 pies High Cube",
}


async def crear_plan_desde_linea(
    planes: IPlanCargueRepository, vehiculos: IVehiculoCubicacionRepository, lineas: ILineaSolicitudPort,
    linea_solicitud_id: str,
) -> PlanCargue:
    linea = await lineas.obtener(linea_solicitud_id)
    if linea is None:
        raise NotFoundException("LINEA_NO_ENCONTRADA", "La linea de solicitud no existe.")
    if not linea.posiciones:
        raise ValidationFailedException(
            "SIN_POSICIONES", "La linea no tiene posiciones con dimensiones: capturalas antes de generar el plan de cargue 3D.",
        )

    # Vehiculo: el sugerido por el cubicador rapido (o la flota); sin sugerencia, el furgon 13,6 m.
    respuesta = rapida.cubicar(
        rapida.CubicacionRequest(items=[
            rapida.ItemCubicacion(descripcion=p.descripcion, cantidad=p.cantidad,
                                   largo_cm=p.largo_cm, ancho_cm=p.ancho_cm, alto_cm=p.alto_cm, peso_kg=p.peso_kg)
            for p in linea.posiciones
        ]),
        rapida.MedidasEstandar.por_defecto(),
    )
    unidad_rapida = (respuesta.sugerencia.unidad_id if respuesta.sugerencia
                     else respuesta.sugerencia_flota.unidad_id if respuesta.sugerencia_flota else None)
    nombre_preset = _PRESET_POR_UNIDAD_RAPIDA.get(unidad_rapida, "Tractocamion furgon 13.6 m")
    catalogo = await vehiculos.listar_con_siembra()
    vehiculo = next(v for v in catalogo if v.nombre == nombre_preset)

    # Una FLOTA de planes por linea (Vehiculo 1..N): si ya existia se reconstruye.
    await planes.eliminar_planes_de_linea(linea_solicitud_id)

    ruta = "linea" if linea.origen is None and linea.destino is None else f"{linea.origen} -> {linea.destino}"

    # Reparto en CASCADA sobre el vehiculo real (mismo tipo): se llena un
    # vehiculo con el optimizador 3D y lo que NO cabe pasa al siguiente,
    # hasta cargar todo o alcanzar el tope de flota.
    pendientes: list[tuple[PosicionCarga, int]] = [(p, max(1, p.cantidad)) for p in linea.posiciones]
    primero: PlanCargue | None = None
    numero = 0

    while pendientes and numero < _MAX_VEHICULOS_FLOTA:
        numero += 1
        plan = await planes.crear(f"Mesa - {ruta} - Vehiculo {numero}", vehiculo.id, linea_solicitud_id, numero)
        primero = primero or plan

        pos_por_item_id: dict[str, PosicionCarga] = {}
        for idx, (pos, cant) in enumerate(pendientes, start=1):
            item = ItemPlanCargue(
                plan_cargue_id=plan.id, sku=f"POS-{idx}",
                descripcion=pos.descripcion if pos.descripcion and pos.descripcion.strip() else f"Posicion {idx}",
                destino=linea.destino, secuencia_entrega=1,
                largo_mm=pos.largo_cm * 10, ancho_mm=pos.ancho_cm * 10, alto_mm=pos.alto_cm * 10,
                peso_kg=pos.peso_kg or 1.0, cantidad=cant,
            )
            guardado = await planes.guardar_item(plan.id, None, item)
            pos_por_item_id[guardado.id] = pos

        await optimizar_plan(planes, vehiculos, plan.id, ParametrosOptimizacionInput("Combinada", "bfd"))

        # Salvaguarda: si el vehiculo no logro cargar NADA, abrir otro
        # identico no ayuda (una pieza no cabe por si sola). Se detiene y
        # esas unidades quedan "sin cargar".
        if not await planes.cargo_algo(plan.id):
            break

        sobrantes = await planes.obtener_sobrantes(plan.id)
        pendientes = [
            (pos_por_item_id[item_id], cantidad)
            for item_id, cantidad in sobrantes
            if cantidad > 0 and item_id in pos_por_item_id
        ]

    assert primero is not None
    return primero
