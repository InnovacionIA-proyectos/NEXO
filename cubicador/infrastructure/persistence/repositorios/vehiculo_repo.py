"""Implementa IVehiculoCubicacionRepository con SQLAlchemy."""

from __future__ import annotations

import json
from typing import TYPE_CHECKING

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from application.plan_cargue.vehiculos import presets
from domain.entidades.entidades import EjeVehiculoRow, VehiculoCubicacion
from infrastructure.persistence.modelos import PlanCargueModel, VehiculoCubicacionModel

if TYPE_CHECKING:
    from infrastructure.mensajeria.rabbitmq import RabbitMqEventosPublisher


class SqlVehiculoCubicacionRepository:
    def __init__(self, sesion: AsyncSession, publisher: "RabbitMqEventosPublisher | None" = None) -> None:
        self._s = sesion
        # Opcional a proposito - sin publisher (scripts/tests que no lo
        # necesitan) la siembra sigue funcionando exactamente igual que
        # siempre, solo que no publica nada.
        self._publisher = publisher

    async def listar_con_siembra(self) -> list[VehiculoCubicacion]:
        # Siembra por NOMBRE: si el catalogo de presets crece, los faltantes
        # se agregan sin duplicar los existentes (que el usuario pudo haber editado).
        nombres = (await self._s.execute(select(VehiculoCubicacionModel.nombre))).scalars().all()
        faltantes = [p for p in presets() if p.nombre not in nombres]
        if faltantes:
            self._s.add_all([_a_modelo(p) for p in faltantes])
            await self._s.commit()
            # Sin esto, un preset sembrado aca nace invisible para NEXO
            # para siempre - era la UNICA escritura del catalogo que no
            # pasaba por "guardar_vehiculo" (worker/main.py), que si publica
            # vehiculo.creado. El consumer de NEXO (VehiculoCatalogoConsumer)
            # hace upsert por Id, asi que publicar de mas nunca duplica ni
            # corrompe la proyeccion - agregar un preset nuevo al codigo y
            # levantar Cubicador ahora lo hace aparecer solo en NEXO.
            if self._publisher is not None:
                for nuevo in faltantes:
                    await self._publisher.publicar_vehiculo_creado(nuevo.id, _a_evento_payload(nuevo))

        filas = (await self._s.execute(select(VehiculoCubicacionModel).order_by(VehiculoCubicacionModel.nombre))).scalars().all()
        return [_a_entidad(f) for f in filas]

    async def republicar_todos(self) -> list[VehiculoCubicacion]:
        """Repara una desincronizacion ya existente entre el catalogo de
        Cubicador y la proyeccion de NEXO: republica vehiculo.creado para
        TODO el catalogo actual, no solo los nuevos. Seguro de correr mas de
        una vez (el consumer de NEXO hace upsert por Id) - pensado como
        reparacion manual, no se llama desde ningun flujo automatico."""
        vehiculos = await self.listar_con_siembra()
        if self._publisher is not None:
            for v in vehiculos:
                await self._publisher.publicar_vehiculo_creado(v.id, _a_evento_payload(v))
        return vehiculos

    async def obtener(self, id: str) -> VehiculoCubicacion | None:
        fila = await self._s.get(VehiculoCubicacionModel, id)
        return _a_entidad(fila) if fila else None

    async def buscar_por_nombre(self, nombre: str) -> VehiculoCubicacion | None:
        fila = (await self._s.execute(
            select(VehiculoCubicacionModel).where(VehiculoCubicacionModel.nombre == nombre)
        )).scalar_one_or_none()
        return _a_entidad(fila) if fila else None

    async def guardar(self, vehiculo: VehiculoCubicacion) -> VehiculoCubicacion:
        existente = await self._s.get(VehiculoCubicacionModel, vehiculo.id)
        if existente is None:
            fila = _a_modelo(vehiculo)
            self._s.add(fila)
        else:
            fila = existente
            for campo, valor in _a_modelo(vehiculo).__dict__.items():
                if campo.startswith("_") or campo in ("id", "created_at"):
                    continue
                setattr(fila, campo, valor)
        await self._s.commit()
        await self._s.refresh(fila)
        return _a_entidad(fila)

    async def tiene_planes_asociados(self, id: str) -> bool:
        fila = (await self._s.execute(
            select(PlanCargueModel.id).where(PlanCargueModel.vehiculo_id == id).limit(1)
        )).first()
        return fila is not None

    async def eliminar(self, id: str) -> None:
        fila = await self._s.get(VehiculoCubicacionModel, id)
        if fila is not None:
            await self._s.delete(fila)
            await self._s.commit()


def _a_modelo(v: VehiculoCubicacion) -> VehiculoCubicacionModel:
    return VehiculoCubicacionModel(
        id=v.id, nombre=v.nombre, descripcion=v.descripcion,
        largo_interno_mm=v.largo_interno_mm, ancho_interno_mm=v.ancho_interno_mm, alto_interno_mm=v.alto_interno_mm,
        capacidad_kg=v.capacidad_kg, tara_kg=v.tara_kg,
        puerta_ancho_mm=v.puerta_ancho_mm, puerta_alto_mm=v.puerta_alto_mm, puerta_posicion=v.puerta_posicion,
        ejes_json=json.dumps([{"posicionMm": e.posicion_mm, "capacidadKg": e.capacidad_kg} for e in v.ejes]),
        tolerancia_paredes_mm=v.tolerancia_paredes_mm, tolerancia_entre_unidades_mm=v.tolerancia_entre_unidades_mm,
        es_preset=v.es_preset, created_at=v.created_at, updated_at=v.updated_at,
    )


def _a_evento_payload(v: VehiculoCubicacion) -> dict:
    """Mismo shape que arma worker/main.py para vehiculo.creado/actualizado
    (VehiculoCubicacionDto.model_validate(...).model_dump(mode="json",
    by_alias=True)) - se reusa el DTO a proposito, mismo criterio ya
    documentado para que el worker importe de api/dtos: evitar una segunda
    definicion del contrato del evento que pueda divergir con el tiempo."""
    from api.dtos.plan_cargue import VehiculoCubicacionDto

    return VehiculoCubicacionDto.model_validate(v).model_dump(mode="json", by_alias=True)


def _a_entidad(f: VehiculoCubicacionModel) -> VehiculoCubicacion:
    try:
        ejes = [EjeVehiculoRow(e["posicionMm"], e["capacidadKg"]) for e in json.loads(f.ejes_json)]
    except (json.JSONDecodeError, KeyError, TypeError):
        ejes = []  # tolerante a datos legados/corruptos, igual que el original
    return VehiculoCubicacion(
        id=f.id, nombre=f.nombre, descripcion=f.descripcion,
        largo_interno_mm=f.largo_interno_mm, ancho_interno_mm=f.ancho_interno_mm, alto_interno_mm=f.alto_interno_mm,
        capacidad_kg=f.capacidad_kg, tara_kg=f.tara_kg,
        puerta_ancho_mm=f.puerta_ancho_mm, puerta_alto_mm=f.puerta_alto_mm, puerta_posicion=f.puerta_posicion,
        ejes=ejes, tolerancia_paredes_mm=f.tolerancia_paredes_mm, tolerancia_entre_unidades_mm=f.tolerancia_entre_unidades_mm,
        es_preset=f.es_preset, created_at=f.created_at, updated_at=f.updated_at,
    )
