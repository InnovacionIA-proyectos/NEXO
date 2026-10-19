"""Puente Mesa de Costeo -> Cubicador (S5-HU-17.6).

Crea -o reconstruye- el plan de cargue 3D de una linea a partir de sus
posiciones capturadas, usando **la flota que recomienda el motor de seleccion**
(`domain/motor/seleccion_flota.py`) y dejando cada plan optimizado.

**Que cambio respecto de la version anterior.** Antes el puente adivinaba
cuantos vehiculos hacian falta: llenaba uno con el optimizador 3D, miraba que
sobraba, abria otro igual, y asi hasta veinte veces. Ahora el motor responde
**cuantos y de que tipo** -y la flota puede ser mixta-, asi que el bucle a
ciegas se reemplaza por recorrer los vehiculos que el motor indico. Si despues
del ultimo queda carga sin ubicar, eso es una discrepancia entre los dos
motores y se rechaza con motivo, en vez de perderse.

**Y se murio el fallback silencioso.** Antes, cuando no reconocia el vehiculo
sugerido, asumia un furgon de 13,6 metros. Eso ponia la carga en una caja que
podia no existir y el plan salia creible pero equivocado. Ahora cada traduccion
que falla es un rechazo con motivo.

El motor 3D no se toca: entre el codigo canonico del catalogo y el preset del
3D hay un **diccionario de compatibilidad**, que es deuda declarada y tiene su
propio documento (`docs/DEUDA_DIFERIDA_3D.md`).
"""

from __future__ import annotations

from decimal import Decimal

from application.excepciones import NotFoundException, ValidationFailedException
from application.ports.externos import ILineaSolicitudPort, PosicionCarga
from application.ports.repositorios import (
    ICatalogoRepository,
    IParametrosCubicacionRepository,
    IPlanCargueRepository,
    IVehiculoCubicacionRepository,
)
from application.recomendaciones.casos_de_uso import LineaMercancia, recomendar_flota
from domain.entidades.entidades import ItemPlanCargue, PlanCargue
from domain.motor.seleccion_flota import RESULTADO_ASIGNADO

from .optimizar import ParametrosOptimizacionInput, optimizar_plan

# Codigo canonico del catalogo -> nombre del preset del motor 3D.
#
# **Es deuda, no diseno.** Existe porque hay DOS catalogos de vehiculos: el de
# `dbo_cubicador.vehiculos`, que alimenta al motor de seleccion, y el de
# `vehiculos_cubicacion`, que es el del motor 3D y se siembra POR NOMBRE.
# Unificarlos es el primer punto de `docs/DEUDA_DIFERIDA_3D.md`; mientras tanto,
# este diccionario es el unico lugar donde se cruzan.
#
# Las medidas coinciden en los cinco: el Turbo 450x220x230 del catalogo es el
# preset de 4500x2200x2300 mm, y asi con el resto - se corrigieron contra el
# mismo Excel de Operaciones.
_PRESET_POR_CODIGO: dict[str, str] = {
    "2L2": "Turbo con estacas",
    "C2": "Camion sencillo (estacas)",
    "C3": "Camion rigido dobletroque",
    "2S2": "Minimula 2S2",
    # El 3S3 es el unico que depende de la carroceria: ver `_PRESET_POR_COMBINACION`.
    "3S3": "Plataforma (tractomula)",
}

# El 3S3 tiene dos presets segun la carroceria, y no es un detalle cosmetico:
# las capacidades difieren igual que en el catalogo. Con furgon carga 30.000 kg
# -el furgon pesa- y el preset "Tractocamion furgon 13.6 m" dice exactamente
# eso; con plataforma o estacas carga los 34.000 del chasis, que es lo que dice
# "Plataforma (tractomula)". Mandarlo siempre al mismo preset regalaria o
# quitaria cuatro toneladas.
_PRESET_POR_COMBINACION: dict[tuple[str, str], str] = {
    ("3S3", "FURGON"): "Tractocamion furgon 13.6 m",
    ("3S3", "FURGON_REFRIGERADO"): "Tractocamion furgon 13.6 m",
    ("3S3", "FURGON_CLIMATIZADO"): "Tractocamion furgon 13.6 m",
    ("3S3", "FURGON_VENTILADO"): "Tractocamion furgon 13.6 m",
}


def _preset_de(codigo_vehiculo: str, codigo_carroceria: str) -> str | None:
    return _PRESET_POR_COMBINACION.get(
        (codigo_vehiculo, codigo_carroceria), _PRESET_POR_CODIGO.get(codigo_vehiculo))


def _a_lineas_de_mercancia(posiciones: list[PosicionCarga]) -> list[LineaMercancia]:
    """Traduce las posiciones capturadas a lo que el motor necesita.

    Cada posicion tiene que declarar su codigo de mercancia. Las lineas
    capturadas antes de S5-HU-17.6 no lo traen: se rechazan con motivo en vez
    de asumir un tipo, porque el tipo arrastra la carroceria y asumirlo
    elegiria el vehiculo equivocado sin que nada avise.

    El codigo se pasa tal cual: desde S5-HU-17.7 el contrato entra por codigo,
    asi que no hay que traducirlo a id para despues volverlo a resolver. Quien
    valida que exista es `recomendar_flota`, con el mismo codigo de rechazo
    (`MERCANCIA_DESCONOCIDA`) y el mismo mensaje.

    **El peso tampoco se asume** (#304). Hasta aca el puente ponia 1 kg cuando
    la posicion no lo traia -`posicion.peso_kg or 1`-, y el peso es lo que
    decide si la carga cabe por capacidad: un kilo inventado puede meter en un
    Turbo lo que necesitaba una Minimula. Es el mismo fallback silencioso que
    S5-HU-17.6 elimino para el tipo de mercancia, y se habia quedado vivo. Se
    puede alcanzar porque `PesoKg` es opcional en la captura de NEXO.
    """
    sin_codigo = [p.descripcion or "(sin descripcion)" for p in posiciones if not p.codigo_mercancia]
    if sin_codigo:
        raise ValidationFailedException(
            "MERCANCIA_NO_DECLARADA",
            "Estas posiciones no declaran su tipo de mercancia: "
            f"{', '.join(sin_codigo)}. Sin el tipo no se puede saber que carroceria "
            "necesita la carga; recapturar la linea con el tipo de cada posicion.")

    sin_peso = [
        p.descripcion or "(sin descripcion)"
        for p in posiciones
        if p.peso_kg is None or Decimal(str(p.peso_kg)) <= 0
    ]
    if sin_peso:
        raise ValidationFailedException(
            "PESO_NO_DECLARADO",
            "Estas posiciones no declaran su peso: "
            f"{', '.join(sin_peso)}. El peso decide la capacidad del vehiculo; "
            "recapturar la linea con el peso de cada posicion.")

    return [
        LineaMercancia(
            codigo_mercancia=posicion.codigo_mercancia,
            cantidad=max(1, posicion.cantidad),
            largo_cm=Decimal(str(posicion.largo_cm)),
            ancho_cm=Decimal(str(posicion.ancho_cm)),
            alto_cm=Decimal(str(posicion.alto_cm)),
            peso_kg=Decimal(str(posicion.peso_kg)),
        )
        for posicion in posiciones
    ]


async def crear_plan_desde_linea(
    planes: IPlanCargueRepository,
    vehiculos: IVehiculoCubicacionRepository,
    lineas: ILineaSolicitudPort,
    catalogo_repo: ICatalogoRepository,
    parametros_repo: IParametrosCubicacionRepository,
    linea_solicitud_id: str,
) -> PlanCargue:
    linea = await lineas.obtener(linea_solicitud_id)
    if linea is None:
        raise NotFoundException("LINEA_NO_ENCONTRADA", "La linea de solicitud no existe.")
    if not linea.posiciones:
        raise ValidationFailedException(
            "SIN_POSICIONES",
            "La linea no tiene posiciones con dimensiones: capturalas antes de generar "
            "el plan de cargue 3D.")

    recomendacion = await recomendar_flota(
        catalogo_repo, parametros_repo, _a_lineas_de_mercancia(linea.posiciones))

    if recomendacion.resultado != RESULTADO_ASIGNADO or not recomendacion.vehiculos:
        raise ValidationFailedException(
            "SIN_FLOTA_RECOMENDADA",
            f"El motor no asigno vehiculos para esta linea ({recomendacion.motivo_sin_seleccion}). "
            + (" ".join(recomendacion.advertencias) or "Revisar las medidas y el tipo de cada posicion."))

    # Se traducen TODOS los presets antes de crear nada: fallar a mitad de la
    # flota dejaria planes huerfanos de una recomendacion que no se cumplio.
    catalogo_3d = await vehiculos.listar_con_siembra()
    por_nombre = {v.nombre: v for v in catalogo_3d}
    presets = []
    for recomendado in recomendacion.vehiculos:
        nombre = _preset_de(recomendado.codigo_vehiculo, recomendado.codigo_carroceria)
        if nombre is None:
            raise ValidationFailedException(
                "VEHICULO_SIN_PRESET",
                f"El vehiculo '{recomendado.codigo_vehiculo}' con carroceria "
                f"'{recomendado.codigo_carroceria}' no tiene preset equivalente en el motor 3D. "
                "Agregarlo al diccionario de compatibilidad de desde_linea.py.")
        preset = por_nombre.get(nombre)
        if preset is None:
            raise ValidationFailedException(
                "PRESET_VEHICULO_NO_ENCONTRADO",
                f"El preset '{nombre}' no existe en el catalogo del motor 3D: revisa que no "
                "haya sido renombrado o eliminado.")
        presets.append(preset)

    # Una FLOTA de planes por linea (Vehiculo 1..N): si ya existia se reconstruye.
    await planes.eliminar_planes_de_linea(linea_solicitud_id)

    ruta = ("linea" if linea.origen is None and linea.destino is None
            else f"{linea.origen} -> {linea.destino}")

    # Se recorren los vehiculos QUE EL MOTOR INDICO, en orden. Lo que no entra
    # en uno pasa al siguiente - que puede ser de otro tipo, porque la flota
    # puede ser mixta. Ya no se adivina cuantos hacen falta ni se abren hasta
    # veinte iguales.
    pendientes: list[tuple[PosicionCarga, int]] = [
        (p, max(1, p.cantidad)) for p in linea.posiciones]
    primero: PlanCargue | None = None

    for numero, preset in enumerate(presets, start=1):
        if not pendientes:
            break

        plan = await planes.crear(
            f"Mesa - {ruta} - Vehiculo {numero}", preset.id, linea_solicitud_id, numero)
        primero = primero or plan

        pos_por_item_id: dict[str, PosicionCarga] = {}
        for idx, (posicion, cantidad) in enumerate(pendientes, start=1):
            item = ItemPlanCargue(
                plan_cargue_id=plan.id, sku=f"POS-{idx}",
                descripcion=(posicion.descripcion if posicion.descripcion
                             and posicion.descripcion.strip() else f"Posicion {idx}"),
                destino=linea.destino, secuencia_entrega=1,
                largo_mm=posicion.largo_cm * 10, ancho_mm=posicion.ancho_cm * 10,
                # Sin `or 1`: `_a_lineas_de_mercancia` ya rechazo la linea si
                # alguna posicion no declara peso (#304), asi que aca siempre
                # hay uno. Dejar el default seria repetir el fallback que se
                # acaba de matar, esperando a que alguien reordene el flujo.
                alto_mm=posicion.alto_cm * 10, peso_kg=posicion.peso_kg,
                cantidad=cantidad,
            )
            guardado = await planes.guardar_item(plan.id, None, item)
            pos_por_item_id[guardado.id] = posicion

        await optimizar_plan(
            planes, vehiculos, plan.id, ParametrosOptimizacionInput("Combinada", "bfd"))

        sobrantes = await planes.obtener_sobrantes(plan.id)
        pendientes = [
            (pos_por_item_id[item_id], cantidad)
            for item_id, cantidad in sobrantes
            if cantidad > 0 and item_id in pos_por_item_id
        ]

    if pendientes:
        # Los dos motores no coincidieron: el de seleccion dijo que la carga
        # entraba en esta flota y el 3D no la acomodo. Pasa por diferencias de
        # empaque entre uno y otro - el 3D valida soporte y apilamiento por
        # unidad, el de seleccion empaca con DBLF. Se avisa en vez de devolver
        # un plan incompleto que nadie sabria leer.
        raise ValidationFailedException(
            "FLOTA_INSUFICIENTE",
            f"El motor recomendo {len(presets)} vehiculo(s) pero el plan 3D dejo "
            f"{len(pendientes)} posicion(es) sin ubicar. Revisar apilamiento y soporte "
            "de esas piezas.")

    assert primero is not None
    return primero
