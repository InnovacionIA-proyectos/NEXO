"""Puente Mesa de Costeo -> Cubicador: crea (o reconstruye) el plan de
cargue de una linea a partir de sus posiciones capturadas, con el vehiculo
sugerido por el cubicador rapido, y lo deja optimizado. Si la carga no cabe
en un vehiculo, reparte en cascada hasta 20 (misma linea_solicitud_id,
numero_vehiculo 1..N) y devuelve LA FLOTA COMPLETA. Equivalente a
CrearPlanDesdeLineaCommand de NEXO."""

from __future__ import annotations

from application.excepciones import NotFoundException, ValidationFailedException
from application.ports.externos import ILineaSolicitudPort, PosicionCarga
from application.cubicacion_rapida.casos_de_uso import unidad_desde_vehiculo
from application.ports.repositorios import IPlanCargueRepository, IVehiculoCubicacionRepository
from domain.entidades.entidades import ItemPlanCargue, PlanCargue
from domain.motor import cubicacion_rapida as rapida

from .optimizar import ParametrosOptimizacionInput, optimizar_plan
from .vehiculos import apto_para_plan_3d

_MAX_VEHICULOS_FLOTA = 20

# Vehiculo por defecto cuando el cubicador rapido no sugiere ninguno.
_CODIGO_POR_DEFECTO = "furgon_semirremolque"

# S4-HU-12.1: aqui vivia _PRESET_POR_UNIDAD_RAPIDA, un diccionario que traducia
# el id de unidad del motor rapido al NOMBRE del preset del plan 3D. Ya no hace
# falta: ambos catalogos se unificaron, asi que el id que devuelve el motor ES
# el `codigo` de la fila. La tabla desaparece y con ella sus dos errores:
#
#   - Emparejaba "contenedor_refrigerado_40" (un REEFER) con "Contenedor 40
#     pies High Cube" (un contenedor SECO). Eran dos vehiculos distintos.
#   - Buscaba por NOMBRE, que el usuario puede editar desde la interfaz.
#     Renombrar un preset dejaba esta funcion sin vehiculo y el plan de cargue
#     atascado. Ahora se busca por `codigo`, que es opaco y no se edita.


async def crear_plan_desde_linea(
    planes: IPlanCargueRepository, vehiculos: IVehiculoCubicacionRepository, lineas: ILineaSolicitudPort,
    linea_solicitud_id: str,
) -> list[PlanCargue]:
    linea = await lineas.obtener(linea_solicitud_id)
    if linea is None:
        raise NotFoundException("LINEA_NO_ENCONTRADA", "La linea de solicitud no existe.")
    if not linea.posiciones:
        raise ValidationFailedException(
            "SIN_POSICIONES", "La linea no tiene posiciones con dimensiones: capturalas antes de generar el plan de cargue 3D.",
        )

    # Vehiculo: el sugerido por el cubicador rapido (o la flota); sin sugerencia, el furgon 13,6 m.
    # El catalogo se lee UNA vez y sirve para las dos cosas: alimentar al motor
    # rapido y resolver despues la fila real del vehiculo elegido.
    # Solo se ofrecen al motor los vehiculos a los que SI se les puede armar un
    # plan 3D. La cubicacion rapida por si sola puede sugerir uno sin puerta ni
    # posiciones de eje -alli esos datos no hacen falta-, pero aqui el paso
    # siguiente es optimizar, y ese vehiculo no llegaria. Filtrar antes hace que
    # se elija el mejor de los que SI sirven, en vez de fallar con el primero.
    catalogo = [v for v in await vehiculos.listar_con_siembra() if apto_para_plan_3d(v)]
    unidades = [u for u in (unidad_desde_vehiculo(v) for v in catalogo) if u is not None]
    respuesta = rapida.cubicar(
        rapida.CubicacionRequest(items=[
            rapida.ItemCubicacion(descripcion=p.descripcion, cantidad=p.cantidad,
                                   largo_cm=p.largo_cm, ancho_cm=p.ancho_cm, alto_cm=p.alto_cm, peso_kg=p.peso_kg)
            for p in linea.posiciones
        ]),
        rapida.MedidasEstandar.por_defecto(),
        unidades,
    )
    # El id de unidad del motor ES el codigo de la fila: son el mismo catalogo.
    codigo = (respuesta.sugerencia.unidad_id if respuesta.sugerencia
              else respuesta.sugerencia_flota.unidad_id if respuesta.sugerencia_flota
              else _CODIGO_POR_DEFECTO)
    vehiculo = next((v for v in catalogo if v.codigo == codigo), None)
    if vehiculo is None:
        # Sin este guarda, `next(...)` lanzaba StopIteration, que el worker trata
        # como fallo INESPERADO -tres reintentos identicos y luego DLQ- en vez de
        # un rechazo de negocio inmediato. Ahora es casi inalcanzable, porque el
        # codigo sale del mismo catalogo que se acaba de leer; queda como red por
        # si alguien borra el vehiculo por defecto.
        raise ValidationFailedException(
            "PRESET_VEHICULO_NO_ENCONTRADO",
            f"El vehiculo sugerido '{codigo}' no existe en el catalogo: revisa que no haya sido eliminado.",
        )

    # Una FLOTA de planes por linea (Vehiculo 1..N): si ya existia se reconstruye.
    await planes.eliminar_planes_de_linea(linea_solicitud_id)

    ruta = "linea" if linea.origen is None and linea.destino is None else f"{linea.origen} -> {linea.destino}"

    # S4-HU-12.3: el reparto se decide ANTES de crear los planes, por PESO.
    #
    # Antes esto era una cascada: se llenaba un vehiculo con el optimizador 3D y
    # lo que no cabia pasaba al siguiente. Eso dejaba el primer camion al tope y
    # el ultimo casi vacio -medido: 20.000 y 10.000 kg para una carga de 30.000-
    # y era exactamente el reparto voraz que S4-HU-12.2 reemplazo en el cubicador
    # rapido. Mantenerlo aqui habria sido peor que no hacer 12.2: el peso de cada
    # camion es lo que viaja en el evento de flota y lo que NEXO escribe en cada
    # linea de solicitud, asi que el desbalance se propagaba hasta la cotizacion.
    #
    # `cantidad_vehiculos` sale de la recomendacion del cubicador rapido, que ya
    # verifico que la carga entra en esos N con el reparto equilibrado.
    cantidad = respuesta.sugerencia_flota.cantidad_vehiculos if respuesta.sugerencia_flota else 1
    reparto = _repartir_por_peso(linea.posiciones, cantidad)

    # Se devuelve la FLOTA COMPLETA, no solo el primero: el worker necesita los N
    # camiones para publicar el evento de flota. Hasta ahora se creaban todos
    # pero solo se reportaba uno, asi que NEXO nunca se enteraba de los camiones
    # 2..N y no podia crear su linea de solicitud.
    flota: list[PlanCargue] = []

    for numero, posiciones_del_camion in enumerate(reparto, start=1):
        if not posiciones_del_camion:
            continue
        plan = await planes.crear(f"Mesa - {ruta} - Vehiculo {numero}", vehiculo.id, linea_solicitud_id, numero)

        for idx, (pos, cant) in enumerate(posiciones_del_camion, start=1):
            await planes.guardar_item(plan.id, None, ItemPlanCargue(
                plan_cargue_id=plan.id, sku=f"POS-{idx}",
                descripcion=pos.descripcion if pos.descripcion and pos.descripcion.strip() else f"Posicion {idx}",
                destino=linea.destino, secuencia_entrega=1,
                largo_mm=pos.largo_cm * 10, ancho_mm=pos.ancho_cm * 10, alto_mm=pos.alto_cm * 10,
                peso_kg=pos.peso_kg or 1.0, cantidad=cant,
            ))

        await optimizar_plan(planes, vehiculos, plan.id, ParametrosOptimizacionInput("Combinada", "bfd"))

        # Salvaguarda: si el vehiculo no cargo NADA, ese plan no aporta. Lo que
        # no entro queda como "no cargado" y se ve en el resultado del plan, en
        # vez de desaparecer sin rastro.
        if not await planes.cargo_algo(plan.id):
            await planes.eliminar(plan.id)
            continue

        flota.append(plan)

    if not flota:
        raise ValidationFailedException(
            "CARGA_NO_UBICABLE",
            "Ninguna posicion de la linea se pudo cargar en el vehiculo sugerido.",
        )
    # Se releen para que traigan las metricas que dejo la optimizacion (peso
    # total por vehiculo, que es lo que viaja en el evento de flota).
    return [await planes.obtener(p.id) or p for p in flota]


def _repartir_por_peso(
    posiciones: list[PosicionCarga], cantidad: int,
) -> list[list[tuple[PosicionCarga, int]]]:
    """Reparte las unidades de carga entre `cantidad` camiones buscando que
    todos pesen lo mismo (S4-HU-12.3).

    Heuristica LPT: se ordenan las unidades de mas pesada a mas liviana y cada
    una va al camion que menos lleve hasta ese momento. Es la forma estandar de
    equilibrar y da un resultado muy cercano al optimo sin costo notable.

    Se reparte por UNIDAD, no por posicion: una posicion de 60 pallets se puede
    partir entre camiones, que es lo que permite equilibrar de verdad. Al final
    se reagrupa por posicion para no crear 60 items de cantidad 1.

    Una unidad sin peso declarado cuenta como 0: no se le inventa un valor. El
    efecto es que se reparte por las que si lo tienen, y el aviso de "items sin
    peso" del cubicador rapido ya le dijo al usuario que complete el dato.
    """
    if cantidad <= 1:
        return [[(p, max(1, p.cantidad)) for p in posiciones]]

    unidades: list[tuple[float, int]] = []  # (peso, indice de la posicion)
    for indice, pos in enumerate(posiciones):
        for _ in range(max(1, pos.cantidad)):
            unidades.append((pos.peso_kg or 0.0, indice))
    unidades.sort(key=lambda u: u[0], reverse=True)

    pesos = [0.0] * cantidad
    conteos: list[dict[int, int]] = [{} for _ in range(cantidad)]
    for peso, indice in unidades:
        destino = min(range(cantidad), key=lambda c: pesos[c])
        pesos[destino] += peso
        conteos[destino][indice] = conteos[destino].get(indice, 0) + 1

    return [
        [(posiciones[indice], cant) for indice, cant in sorted(conteo.items())]
        for conteo in conteos
    ]
