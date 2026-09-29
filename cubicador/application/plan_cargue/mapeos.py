"""Mapeos compartidos entre casos de uso del Plan de Cargue: traduce entre
las entidades de persistencia (`domain.entidades`) y los tipos puros del
motor (`domain.motor.dominio`). Equivalente a `ResultadoPlanReader.cs` de NEXO.

La clave de correlacion que usa el motor para identificar una unidad fisica
expandida es siempre `"{itemPlanCargueId}#{indice}"` - se arma y se deshace
UNICAMENTE aqui, igual que en NEXO solo el repositorio EF lo conocia.
"""

from __future__ import annotations

from typing import TYPE_CHECKING

from domain.entidades.entidades import EjeVehiculoRow, ItemPlanCargue, VehiculoCubicacion
from domain.motor.dominio import Contenedor, EjeVehiculo, ItemCarga, PosicionPuerta

if TYPE_CHECKING:
    # Import solo para chequeo de tipos (evita que `application` dependa en
    # runtime de `api` - la inversion de capas que se ve rara pero es la unica
    # forma de compartir este mapeo Pydantic-a-dominio entre el router
    # sincrono y el worker de colas sin duplicarlo).
    from api.dtos.plan_cargue import GuardarItemPlanCargueInput, GuardarVehiculoInput


def clave(item_plan_cargue_id: str, indice: int) -> str:
    return f"{item_plan_cargue_id}#{indice}"


def descomponer_clave(clave_compuesta: str) -> tuple[str, int]:
    item_id, indice = clave_compuesta.split("#")
    return item_id, int(indice)


def a_contenedor(v: VehiculoCubicacion) -> Contenedor:
    return Contenedor(
        largo_mm=v.largo_interno_mm, ancho_mm=v.ancho_interno_mm, alto_mm=v.alto_interno_mm,
        capacidad_kg=v.capacidad_kg, tara_kg=v.tara_kg,
        puerta_ancho_mm=v.puerta_ancho_mm, puerta_alto_mm=v.puerta_alto_mm,
        puerta=PosicionPuerta(v.puerta_posicion),
        ejes=[EjeVehiculo(e.posicion_mm, e.capacidad_kg) for e in v.ejes],
        tolerancia_paredes_mm=v.tolerancia_paredes_mm,
        tolerancia_entre_unidades_mm=v.tolerancia_entre_unidades_mm,
    )


def a_item_carga(i: ItemPlanCargue, indice: int) -> ItemCarga:
    return ItemCarga(
        id=clave(i.id, indice), sku=i.sku, descripcion=i.descripcion,
        pedido=i.pedido, cliente=i.cliente, destino=i.destino, secuencia_entrega=i.secuencia_entrega,
        largo_mm=i.largo_mm, ancho_mm=i.ancho_mm, alto_mm=i.alto_mm, peso_kg=i.peso_kg,
        apilable=i.apilable, max_niveles=i.max_niveles, max_peso_encima_kg=i.max_peso_encima_kg,
        rotable=i.rotable, mantener_vertical=i.mantener_vertical,
        fragil=i.fragil, peligroso=i.peligroso, refrigerado=i.refrigerado,
        grupo_compatibilidad=i.grupo_compatibilidad, prioridad=i.prioridad,
    )


def expandir_items(items: list[ItemPlanCargue]) -> list[ItemCarga]:
    """Expande cada item por su cantidad: una instancia fisica por unidad."""
    return [a_item_carga(i, n) for i in items for n in range(i.cantidad)]


# ------------------------------------------------------------- Pydantic (API/cola) -> dominio
#
# Movidos ahi desde api/routers/planes_cargue.py: el router sincrono y el
# worker de colas (worker/main.py) necesitan EXACTAMENTE el mismo mapeo
# input-a-entidad para guardar_vehiculo/guardar_item - antes solo lo tenia el router.


def vehiculo_de_input(v: "GuardarVehiculoInput") -> VehiculoCubicacion:
    kwargs = dict(
        nombre=v.nombre, descripcion=v.descripcion,
        largo_interno_mm=v.largo_interno_mm, ancho_interno_mm=v.ancho_interno_mm, alto_interno_mm=v.alto_interno_mm,
        capacidad_kg=v.capacidad_kg, tara_kg=v.tara_kg,
        puerta_ancho_mm=v.puerta_ancho_mm, puerta_alto_mm=v.puerta_alto_mm, puerta_posicion=v.puerta_posicion,
        ejes=[EjeVehiculoRow(e.posicion_mm, e.capacidad_kg) for e in v.ejes],
        tolerancia_paredes_mm=v.tolerancia_paredes_mm, tolerancia_entre_unidades_mm=v.tolerancia_entre_unidades_mm,
        config_vehiculo=v.config_vehiculo, carroceria=v.carroceria,
        clase_viaje=v.clase_viaje, clase_contenedor=v.clase_contenedor,
        geometria=v.geometria, control_ambiental=v.control_ambiental,
    )
    return VehiculoCubicacion(id=v.id, **kwargs) if v.id else VehiculoCubicacion(**kwargs)


def item_de_input(i: "GuardarItemPlanCargueInput", plan_id: str) -> ItemPlanCargue:
    kwargs = dict(
        plan_cargue_id=plan_id, sku=i.sku, descripcion=i.descripcion, pedido=i.pedido, cliente=i.cliente,
        destino=i.destino, secuencia_entrega=i.secuencia_entrega, largo_mm=i.largo_mm, ancho_mm=i.ancho_mm,
        alto_mm=i.alto_mm, peso_kg=i.peso_kg, cantidad=i.cantidad, color=i.color, prioridad=i.prioridad,
        tipo_embalaje=i.tipo_embalaje, apilable=i.apilable, max_niveles=i.max_niveles,
        max_peso_encima_kg=i.max_peso_encima_kg, rotable=i.rotable, mantener_vertical=i.mantener_vertical,
        fragil=i.fragil, peligroso=i.peligroso, refrigerado=i.refrigerado,
        grupo_compatibilidad=i.grupo_compatibilidad, observaciones=i.observaciones,
    )
    return ItemPlanCargue(id=i.id, **kwargs) if i.id else ItemPlanCargue(**kwargs)
