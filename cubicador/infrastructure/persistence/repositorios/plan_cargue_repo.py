"""Implementa IPlanCargueRepository con SQLAlchemy. La clave de dominio
"itemPlanCargueId#indice" que usa el motor para correlacionar unidades
fisicas se arma y se deshace UNICAMENTE aqui - ni application ni el motor
conocen el formato del id real de una fila."""

from __future__ import annotations

import json
import uuid
from dataclasses import asdict, is_dataclass

from sqlalchemy import delete, func, select
from sqlalchemy.ext.asyncio import AsyncSession

from application.plan_cargue import mapeos
from domain.entidades.entidades import ColocacionCargue, ItemPlanCargue, PlanCargue
from domain.motor.dominio import IncidenciaValidacion, ResultadoCubicacion
from domain.motor.motor import MetricasCubicacion
from infrastructure.persistence.modelos import ColocacionCargueModel, ItemPlanCargueModel, PlanCargueModel

_JSON = json  # alias legible


class SqlPlanCargueRepository:
    def __init__(self, sesion: AsyncSession) -> None:
        self._s = sesion

    async def vehiculo_existe(self, vehiculo_id: str) -> bool:
        from infrastructure.persistence.modelos import VehiculoCubicacionModel
        fila = await self._s.get(VehiculoCubicacionModel, vehiculo_id)
        return fila is not None

    async def crear(
        self, nombre: str, vehiculo_id: str, linea_solicitud_id: str | None = None, numero_vehiculo: int = 1,
    ) -> PlanCargue:
        fila = PlanCargueModel(
            id=str(uuid.uuid4()), nombre=nombre, vehiculo_id=vehiculo_id,
            linea_solicitud_id=linea_solicitud_id, numero_vehiculo=numero_vehiculo,
        )
        self._s.add(fila)
        await self._s.commit()
        await self._s.refresh(fila, attribute_names=["items"])
        return _plan_a_entidad(fila)

    async def obtener(self, id: str) -> PlanCargue | None:
        fila = await self._s.get(PlanCargueModel, id)
        return _plan_a_entidad(fila) if fila else None

    async def listar_recientes(self, top: int) -> list[PlanCargue]:
        filas = (await self._s.execute(
            select(PlanCargueModel).order_by(PlanCargueModel.created_at.desc()).limit(top)
        )).scalars().all()
        return [_plan_a_entidad(f) for f in filas]

    async def eliminar(self, id: str) -> None:
        fila = await self._s.get(PlanCargueModel, id)
        if fila is not None:
            await self._s.delete(fila)
            await self._s.commit()

    async def guardar_item(self, plan_id: str, item_id: str | None, item: ItemPlanCargue) -> ItemPlanCargue:
        existente = await self._s.get(ItemPlanCargueModel, item_id) if item_id else None
        if existente is None:
            fila = _item_a_modelo(item, plan_id)
            self._s.add(fila)
        else:
            fila = existente
            for campo, valor in _item_a_modelo(item, plan_id).__dict__.items():
                if campo.startswith("_") or campo in ("id", "created_at"):
                    continue
                setattr(fila, campo, valor)
        await self._s.commit()
        await self._s.refresh(fila)
        return _item_a_entidad(fila)

    async def eliminar_item(self, plan_id: str, item_id: str) -> None:
        await self._s.execute(
            delete(ColocacionCargueModel).where(
                ColocacionCargueModel.plan_cargue_id == plan_id, ColocacionCargueModel.item_plan_cargue_id == item_id,
            )
        )
        await self._s.execute(
            delete(ItemPlanCargueModel).where(ItemPlanCargueModel.plan_cargue_id == plan_id, ItemPlanCargueModel.id == item_id)
        )
        await self._s.commit()

    async def obtener_colocaciones(self, plan_id: str) -> list[ColocacionCargue]:
        filas = (await self._s.execute(
            select(ColocacionCargueModel)
            .where(ColocacionCargueModel.plan_cargue_id == plan_id)
            .order_by(ColocacionCargueModel.secuencia_cargue)
        )).scalars().all()
        return [_colocacion_a_entidad(f) for f in filas]

    async def obtener_colocaciones_bloqueadas(self, plan_id: str) -> list[ColocacionCargue]:
        filas = (await self._s.execute(
            select(ColocacionCargueModel).where(
                ColocacionCargueModel.plan_cargue_id == plan_id,
                ColocacionCargueModel.bloqueada.is_(True),
                ColocacionCargueModel.cargada.is_(True),
            )
        )).scalars().all()
        return [_colocacion_a_entidad(f) for f in filas]

    async def obtener_incidencias(self, plan_id: str) -> list[IncidenciaValidacion]:
        fila = await self._s.get(PlanCargueModel, plan_id)
        if fila is None:
            return []
        return _deserializar_incidencias(fila.incidencias_json)

    async def guardar_resultado_optimizacion(
        self, plan_id: str, resultado: ResultadoCubicacion, metricas: MetricasCubicacion, *,
        estrategia: str, algoritmo: str, semilla: int | None, ponderaciones,
    ) -> None:
        await self._s.execute(delete(ColocacionCargueModel).where(ColocacionCargueModel.plan_cargue_id == plan_id))

        def fila_de(clave_compuesta: str) -> ColocacionCargueModel:
            item_id, indice = mapeos.descomponer_clave(clave_compuesta)
            return ColocacionCargueModel(
                id=str(uuid.uuid4()), plan_cargue_id=plan_id, item_plan_cargue_id=item_id, indice_unidad=indice,
            )

        filas_por_clave: dict[str, ColocacionCargueModel] = {}
        for col in resultado.colocaciones:
            fila = fila_de(col.item.id)
            fila.cargada = True
            fila.x, fila.y, fila.z = col.x, col.y, col.z
            fila.largo_colocado_mm = col.orientacion.largo_mm
            fila.ancho_colocado_mm = col.orientacion.ancho_mm
            fila.alto_colocado_mm = col.orientacion.alto_mm
            fila.orientacion_indice = col.orientacion.indice
            fila.secuencia_cargue = col.secuencia_cargue
            fila.porcentaje_soporte = col.porcentaje_soporte
            fila.peso_encima_kg = col.peso_encima_kg
            fila.estado_estabilidad = col.estado_estabilidad
            fila.accesibilidad = col.accesibilidad
            filas_por_clave[col.item.id] = fila
            self._s.add(fila)

        # Soportes: segunda pasada con las filas ya creadas.
        for col in resultado.colocaciones:
            if col.soporte_id is not None and col.item.id in filas_por_clave and col.soporte_id in filas_por_clave:
                filas_por_clave[col.item.id].soporte_colocacion_id = filas_por_clave[col.soporte_id].id

        for rechazo in resultado.rechazos:
            fila = fila_de(rechazo.item.id)
            fila.cargada = False
            fila.codigo_rechazo = rechazo.codigo_razon
            fila.razon_rechazo = rechazo.razon
            self._s.add(fila)

        plan = await self._s.get(PlanCargueModel, plan_id)
        assert plan is not None
        plan.estado = "optimizado"
        plan.estrategia = estrategia
        plan.algoritmo = algoritmo
        plan.semilla = semilla
        plan.ponderaciones_json = json.dumps(asdict(ponderaciones)) if is_dataclass(ponderaciones) else None
        plan.puntaje = resultado.puntaje
        plan.incidencias_json = _serializar_incidencias(resultado.incidencias)
        _aplicar_metricas(plan, metricas)
        await self._s.commit()

    async def marcar_procesando(self, plan_id: str) -> None:
        plan = await self._s.get(PlanCargueModel, plan_id)
        if plan is not None:
            plan.estado = "procesando"
            await self._s.commit()

    async def marcar_error(self, plan_id: str, mensaje: str) -> None:
        plan = await self._s.get(PlanCargueModel, plan_id)
        if plan is not None:
            plan.estado = "error"
            plan.error_mensaje = mensaje
            await self._s.commit()

    async def marcar_error_por_timeout(self, limite) -> int:
        filas = (await self._s.execute(
            select(PlanCargueModel).where(
                PlanCargueModel.estado == "procesando", PlanCargueModel.updated_at < limite,
            )
        )).scalars().all()
        for plan in filas:
            plan.estado = "error"
            plan.error_mensaje = f"Timeout: el plan quedo mas de lo esperado en 'procesando' (worker probablemente se cayo)."
        if filas:
            await self._s.commit()
        return len(filas)

    async def obtener_colocacion(self, plan_id: str, colocacion_id: str) -> ColocacionCargue | None:
        fila = await self._s.get(ColocacionCargueModel, colocacion_id)
        if fila is None or fila.plan_cargue_id != plan_id:
            return None
        return _colocacion_a_entidad(fila)

    async def marcar_bloqueada(self, colocacion_id: str, bloqueada: bool) -> None:
        fila = await self._s.get(ColocacionCargueModel, colocacion_id)
        if fila is not None:
            fila.bloqueada = bloqueada
            await self._s.commit()

    async def marcar_retirada(self, colocacion_id: str) -> None:
        fila = await self._s.get(ColocacionCargueModel, colocacion_id)
        if fila is not None:
            fila.cargada = False
            fila.codigo_rechazo = "RETIRADA_MANUAL"
            fila.razon_rechazo = "Retirada manualmente por el usuario."
            await self._s.commit()

    async def resolver_colocacion_por_clave(self, plan_id: str, clave_item_indice: str) -> str | None:
        item_id, indice = mapeos.descomponer_clave(clave_item_indice)
        fila = (await self._s.execute(
            select(ColocacionCargueModel.id).where(
                ColocacionCargueModel.plan_cargue_id == plan_id,
                ColocacionCargueModel.item_plan_cargue_id == item_id,
                ColocacionCargueModel.indice_unidad == indice,
            )
        )).scalar_one_or_none()
        return fila

    async def aplicar_movimiento(
        self, colocacion_id: str, x: float, y: float, z: float,
        orientacion_indice: int, largo_mm: float, ancho_mm: float, alto_mm: float,
        porcentaje_soporte: float, soporte_colocacion_id: str | None,
    ) -> None:
        fila = await self._s.get(ColocacionCargueModel, colocacion_id)
        assert fila is not None
        fila.x, fila.y, fila.z = x, y, z
        fila.orientacion_indice = orientacion_indice
        fila.largo_colocado_mm, fila.ancho_colocado_mm, fila.alto_colocado_mm = largo_mm, ancho_mm, alto_mm
        fila.cargada = True
        fila.codigo_rechazo = None
        fila.razon_rechazo = None
        fila.ajuste_manual = True
        fila.porcentaje_soporte = porcentaje_soporte
        fila.soporte_colocacion_id = soporte_colocacion_id
        await self._s.commit()

    async def actualizar_tras_ajuste(
        self, plan_id: str, incidencias: list[IncidenciaValidacion], puntaje: float, metricas: MetricasCubicacion,
        anotaciones: dict[str, tuple[str, str, float]],
    ) -> None:
        plan = await self._s.get(PlanCargueModel, plan_id)
        assert plan is not None
        cargadas = (await self._s.execute(
            select(ColocacionCargueModel).where(ColocacionCargueModel.plan_cargue_id == plan_id, ColocacionCargueModel.cargada.is_(True))
        )).scalars().all()
        for c in cargadas:
            clave = mapeos.clave(c.item_plan_cargue_id, c.indice_unidad)
            if clave in anotaciones:
                c.estado_estabilidad, c.accesibilidad, c.porcentaje_soporte = anotaciones[clave]

        plan.estado = "ajustado"
        plan.puntaje = puntaje
        plan.incidencias_json = _serializar_incidencias(incidencias)
        _aplicar_metricas(plan, metricas)
        await self._s.commit()

    async def eliminar_planes_de_linea(self, linea_solicitud_id: str) -> None:
        await self._s.execute(delete(PlanCargueModel).where(PlanCargueModel.linea_solicitud_id == linea_solicitud_id))
        await self._s.commit()

    async def obtener_sobrantes(self, plan_id: str) -> list[tuple[str, int]]:
        filas = (await self._s.execute(
            select(ColocacionCargueModel.item_plan_cargue_id, func.count())
            .where(ColocacionCargueModel.plan_cargue_id == plan_id, ColocacionCargueModel.cargada.is_(False))
            .group_by(ColocacionCargueModel.item_plan_cargue_id)
        )).all()
        return [(item_id, cantidad) for item_id, cantidad in filas]

    async def cargo_algo(self, plan_id: str) -> bool:
        fila = (await self._s.execute(
            select(ColocacionCargueModel.id).where(
                ColocacionCargueModel.plan_cargue_id == plan_id, ColocacionCargueModel.cargada.is_(True),
            ).limit(1)
        )).first()
        return fila is not None

    async def flota(self, plan_id: str) -> list[dict]:
        plan = await self._s.get(PlanCargueModel, plan_id)
        assert plan is not None

        if plan.linea_solicitud_id is not None:
            hermanos = (await self._s.execute(
                select(PlanCargueModel.id, PlanCargueModel.numero_vehiculo)
                .where(PlanCargueModel.linea_solicitud_id == plan.linea_solicitud_id)
                .order_by(PlanCargueModel.numero_vehiculo)
            )).all()
        else:
            hermanos = [(plan.id, plan.numero_vehiculo)]

        ids = [h[0] for h in hermanos]
        conteos = dict(
            (await self._s.execute(
                select(
                    ColocacionCargueModel.plan_cargue_id,
                    func.count(),
                ).where(ColocacionCargueModel.plan_cargue_id.in_(ids)).group_by(ColocacionCargueModel.plan_cargue_id)
            )).all()
        ) if ids else {}
        cargadas_por_plan = dict(
            (await self._s.execute(
                select(
                    ColocacionCargueModel.plan_cargue_id, func.count(),
                ).where(
                    ColocacionCargueModel.plan_cargue_id.in_(ids), ColocacionCargueModel.cargada.is_(True),
                ).group_by(ColocacionCargueModel.plan_cargue_id)
            )).all()
        ) if ids else {}

        return [
            {
                "id": h_id, "numeroVehiculo": numero, "totalVehiculos": len(hermanos),
                "unidadesCargadas": cargadas_por_plan.get(h_id, 0), "unidadesTotales": conteos.get(h_id, 0),
            }
            for h_id, numero in hermanos
        ]

    async def listar_por_linea(self, linea_solicitud_id: str) -> list[dict]:
        hermanos = (await self._s.execute(
            select(PlanCargueModel.id, PlanCargueModel.numero_vehiculo)
            .where(PlanCargueModel.linea_solicitud_id == linea_solicitud_id)
            .order_by(PlanCargueModel.numero_vehiculo)
        )).all()
        if not hermanos:
            return []  # "crear_desde_linea" todavia no ha terminado (o la linea no existe)

        ids = [h[0] for h in hermanos]
        conteos = dict((await self._s.execute(
            select(ColocacionCargueModel.plan_cargue_id, func.count())
            .where(ColocacionCargueModel.plan_cargue_id.in_(ids))
            .group_by(ColocacionCargueModel.plan_cargue_id)
        )).all())
        cargadas_por_plan = dict((await self._s.execute(
            select(ColocacionCargueModel.plan_cargue_id, func.count())
            .where(ColocacionCargueModel.plan_cargue_id.in_(ids), ColocacionCargueModel.cargada.is_(True))
            .group_by(ColocacionCargueModel.plan_cargue_id)
        )).all())

        return [
            {
                "id": h_id, "numeroVehiculo": numero, "totalVehiculos": len(hermanos),
                "unidadesCargadas": cargadas_por_plan.get(h_id, 0), "unidadesTotales": conteos.get(h_id, 0),
            }
            for h_id, numero in hermanos
        ]


# ------------------------------------------------------------- mapeo entidad <-> modelo


def _aplicar_metricas(plan: PlanCargueModel, m: MetricasCubicacion) -> None:
    plan.peso_total_kg = m.peso_cargado_kg
    plan.volumen_utilizado_m3 = m.volumen_utilizado_m3
    plan.utilizacion_volumen = m.porcentaje_volumen
    plan.utilizacion_piso = m.porcentaje_piso


def _serializar_incidencias(incidencias: list[IncidenciaValidacion]) -> str:
    return json.dumps([
        {
            "severidad": i.severidad, "codigoRegla": i.codigo_regla, "titulo": i.titulo,
            "descripcion": i.descripcion, "accionSugerida": i.accion_sugerida, "colocacionItemId": i.colocacion_item_id,
        }
        for i in incidencias
    ])


def _deserializar_incidencias(incidencias_json: str) -> list[IncidenciaValidacion]:
    try:
        crudas = json.loads(incidencias_json or "[]")
    except json.JSONDecodeError:
        return []
    return [
        IncidenciaValidacion(
            i.get("severidad", "informativa"), i.get("codigoRegla", ""), i.get("titulo", ""), i.get("descripcion", ""),
            i.get("accionSugerida"), i.get("colocacionItemId"),
        )
        for i in crudas
    ]


def _plan_a_entidad(f: PlanCargueModel) -> PlanCargue:
    return PlanCargue(
        id=f.id, nombre=f.nombre, vehiculo_id=f.vehiculo_id, linea_solicitud_id=f.linea_solicitud_id,
        numero_vehiculo=f.numero_vehiculo, estado=f.estado, estrategia=f.estrategia,
        ponderaciones_json=f.ponderaciones_json, algoritmo=f.algoritmo, semilla=f.semilla,
        peso_total_kg=f.peso_total_kg, volumen_utilizado_m3=f.volumen_utilizado_m3,
        utilizacion_volumen=f.utilizacion_volumen, utilizacion_piso=f.utilizacion_piso, puntaje=f.puntaje,
        incidencias_json=f.incidencias_json, error_mensaje=f.error_mensaje,
        created_at=f.created_at, updated_at=f.updated_at,
        items=[_item_a_entidad(i) for i in f.items],
    )


def _item_a_modelo(i: ItemPlanCargue, plan_id: str) -> ItemPlanCargueModel:
    return ItemPlanCargueModel(
        id=i.id, plan_cargue_id=plan_id, sku=i.sku, descripcion=i.descripcion, pedido=i.pedido,
        cliente=i.cliente, destino=i.destino, secuencia_entrega=i.secuencia_entrega,
        largo_mm=i.largo_mm, ancho_mm=i.ancho_mm, alto_mm=i.alto_mm, peso_kg=i.peso_kg, cantidad=i.cantidad,
        color=i.color, prioridad=i.prioridad, tipo_embalaje=i.tipo_embalaje,
        apilable=i.apilable, max_niveles=i.max_niveles, max_peso_encima_kg=i.max_peso_encima_kg,
        rotable=i.rotable, mantener_vertical=i.mantener_vertical, fragil=i.fragil, peligroso=i.peligroso,
        refrigerado=i.refrigerado, grupo_compatibilidad=i.grupo_compatibilidad, observaciones=i.observaciones,
        created_at=i.created_at, updated_at=i.updated_at,
    )


def _item_a_entidad(f: ItemPlanCargueModel) -> ItemPlanCargue:
    return ItemPlanCargue(
        id=f.id, plan_cargue_id=f.plan_cargue_id, sku=f.sku, descripcion=f.descripcion, pedido=f.pedido,
        cliente=f.cliente, destino=f.destino, secuencia_entrega=f.secuencia_entrega,
        largo_mm=f.largo_mm, ancho_mm=f.ancho_mm, alto_mm=f.alto_mm, peso_kg=f.peso_kg, cantidad=f.cantidad,
        color=f.color, prioridad=f.prioridad, tipo_embalaje=f.tipo_embalaje,
        apilable=f.apilable, max_niveles=f.max_niveles, max_peso_encima_kg=f.max_peso_encima_kg,
        rotable=f.rotable, mantener_vertical=f.mantener_vertical, fragil=f.fragil, peligroso=f.peligroso,
        refrigerado=f.refrigerado, grupo_compatibilidad=f.grupo_compatibilidad, observaciones=f.observaciones,
        created_at=f.created_at, updated_at=f.updated_at,
    )


def _colocacion_a_entidad(f: ColocacionCargueModel) -> ColocacionCargue:
    return ColocacionCargue(
        id=f.id, plan_cargue_id=f.plan_cargue_id, item_plan_cargue_id=f.item_plan_cargue_id,
        indice_unidad=f.indice_unidad, cargada=f.cargada, x=f.x, y=f.y, z=f.z,
        largo_colocado_mm=f.largo_colocado_mm, ancho_colocado_mm=f.ancho_colocado_mm, alto_colocado_mm=f.alto_colocado_mm,
        orientacion_indice=f.orientacion_indice, secuencia_cargue=f.secuencia_cargue,
        soporte_colocacion_id=f.soporte_colocacion_id, porcentaje_soporte=f.porcentaje_soporte,
        peso_encima_kg=f.peso_encima_kg, estado_estabilidad=f.estado_estabilidad, accesibilidad=f.accesibilidad,
        bloqueada=f.bloqueada, ajuste_manual=f.ajuste_manual, codigo_rechazo=f.codigo_rechazo, razon_rechazo=f.razon_rechazo,
        created_at=f.created_at, updated_at=f.updated_at,
    )
