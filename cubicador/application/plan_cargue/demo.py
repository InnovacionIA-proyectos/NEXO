"""Escenario de demostracion: vehiculo 13,6 m + carga mixta con rechazos
(incluye piezas disenadas para no pasar por la puerta o exceder peso).
Equivalente a CrearPlanDemoCommand de NEXO."""

from __future__ import annotations

from datetime import datetime

from application.excepciones import ValidationFailedException
from application.ports.repositorios import IPlanCargueRepository, IVehiculoCubicacionRepository
from domain.entidades.entidades import ItemPlanCargue, PlanCargue

_CODIGO_VEHICULO_DEMO = "furgon_semirremolque"


async def crear_plan_demo(planes: IPlanCargueRepository, vehiculos: IVehiculoCubicacionRepository) -> PlanCargue:
    # S4-HU-12.1: busqueda por codigo. Antes era `"13.6" in v.nombre`, una
    # subcadena del NOMBRE, que el usuario edita desde la interfaz: renombrar el
    # furgon dejaba esta funcion sin vehiculo. Y el `next()` sin default lanzaba
    # StopIteration, que el worker trata como fallo INESPERADO -tres reintentos
    # identicos y luego DLQ- en vez de un rechazo de negocio.
    catalogo = await vehiculos.listar_con_siembra()
    vehiculo = next((v for v in catalogo if v.codigo == _CODIGO_VEHICULO_DEMO), None)
    if vehiculo is None:
        raise ValidationFailedException(
            "PRESET_VEHICULO_NO_ENCONTRADO",
            f"El vehiculo de la demostracion ('{_CODIGO_VEHICULO_DEMO}') no existe en el catalogo.",
        )
    plan = await planes.crear(f"Demo cubicacion {datetime.now():%Y-%m-%d %H:%M}", vehiculo.id)

    def demo(
        sku: str, desc: str, pedido: str, destino: str, secuencia: int, color: str,
        l: float, w: float, h: float, peso: float, cant: int,
        apilable: bool = True, rotable: bool = True, fragil: bool = False, prioridad: int = 3,
    ) -> ItemPlanCargue:
        return ItemPlanCargue(
            plan_cargue_id=plan.id, sku=sku, descripcion=desc, pedido=pedido, cliente=f"Cliente {destino}",
            destino=destino, secuencia_entrega=secuencia, color=color,
            largo_mm=l, ancho_mm=w, alto_mm=h, peso_kg=peso, cantidad=cant,
            apilable=apilable, rotable=rotable, fragil=fragil, prioridad=prioridad,
            tipo_embalaje="caja" if fragil else "estiba",
        )

    items = [
        demo("PAL-1200", "Pallet industrial 1200x1000", "PED-001", "Medellin", 1, "#2563eb", 1200, 1000, 1400, 800, 10),
        demo("PAL-800", "Pallet europeo 1200x800", "PED-002", "Bogota", 2, "#16a34a", 1200, 800, 1100, 500, 12),
        demo("CAJ-600", "Caja mediana", "PED-002", "Bogota", 2, "#f59e0b", 600, 400, 500, 35, 20),
        demo("EQP-1500", "Equipo no apilable", "PED-003", "Cali", 3, "#dc2626", 1500, 900, 1200, 450, 5,
             apilable=False, rotable=False, prioridad=1),
        demo("FRG-800", "Caja fragil (vertical)", "PED-003", "Cali", 3, "#9333ea", 800, 600, 700, 80, 8,
             fragil=True, rotable=False),
        # Disenadas para el panel de rechazos: no pasa por la puerta / demasiado pesada.
        demo("XL-2600", "Bobina extra-ancha (no rota)", "PED-004", "Cali", 3, "#0d9488", 2600, 2200, 2400, 3000, 1,
             rotable=False),
        demo("HW-9000", "Bloque de prueba sobrepeso", "PED-004", "Cali", 3, "#475569", 1000, 1000, 1000, 9000, 3),
    ]
    for item in items:
        await planes.guardar_item(plan.id, None, item)

    return await planes.obtener(plan.id) or plan
