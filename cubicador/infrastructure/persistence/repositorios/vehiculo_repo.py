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
        # Siembra por CODIGO desde S4-HU-12.1. Antes era por NOMBRE, que el
        # usuario puede editar desde la interfaz: renombrar un preset lo volvia
        # "faltante" y lo sembraba de nuevo, duplicado. El codigo es opaco y no
        # se edita.
        #
        # Backfill primero: las filas creadas ANTES de 12.1 no tienen codigo
        # (la columna nacio nullable, ver db.py). Se les asigna por nombre, una
        # sola vez. Una fila cuyo nombre el usuario ya habia editado no matchea
        # y queda sin codigo - se comporta igual que antes de este cambio, el
        # preset se vuelve a sembrar. No se adivina a que preset corresponde.
        await self._backfill_codigos()

        codigos = set((await self._s.execute(select(VehiculoCubicacionModel.codigo))).scalars().all())
        faltantes = [p for p in presets() if p.codigo not in codigos]
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

    # Campos que la siembra aporta y que una fila anterior a S4-HU-12.1 no puede
    # tener: las columnas nacieron vacias. NO incluye los fisicos (dimensiones,
    # capacidad, tara, puerta, ejes), que el usuario pudo haber ajustado a su
    # flota real y no se deben tocar nunca.
    _CAMPOS_DE_CLASIFICACION = (
        "config_vehiculo", "carroceria", "clase_viaje", "clase_contenedor",
        "geometria", "control_ambiental",
    )

    async def _backfill_codigos(self) -> None:
        """Completa lo que a una fila anterior a S4-HU-12.1 le falta.

        Dos pasos, y el segundo se descubrio probando contra un Postgres real
        con datos: rellenar solo el `codigo` dejaba a los presets que YA existian
        sin `geometria` ni `control_ambiental`, y el motor rapido descarta a
        quien no los tenga. Una instalacion que actualizara se quedaba con el
        catalogo entero fuera de la cubicacion - todo menos los presets nuevos.
        No se veia en las pruebas porque todas arrancan de una base vacia, donde
        la siembra inserta filas completas.

        Solo se rellena lo que esta en NULL. Un valor que el usuario haya puesto
        -o corregido- se respeta: esto completa huecos, no reimpone la siembra.
        """
        filas = (await self._s.execute(
            select(VehiculoCubicacionModel).where(
                VehiculoCubicacionModel.codigo.is_(None)
                | VehiculoCubicacionModel.geometria.is_(None)
            )
        )).scalars().all()
        if not filas:
            return

        por_nombre = {p.nombre: p for p in presets()}
        por_codigo = {p.codigo: p for p in presets()}
        tocadas = 0

        for fila in filas:
            # Por codigo si ya lo tiene; si no, por nombre. Una fila que el
            # usuario renombro antes de 12.1 no matchea por ninguno de los dos y
            # se deja como esta: no se adivina a que preset corresponde.
            preset = por_codigo.get(fila.codigo) if fila.codigo else por_nombre.get(fila.nombre)
            if preset is None:
                continue
            if fila.codigo is None:
                fila.codigo = preset.codigo
                tocadas += 1
            for campo in self._CAMPOS_DE_CLASIFICACION:
                if getattr(fila, campo) is None and getattr(preset, campo) is not None:
                    setattr(fila, campo, getattr(preset, campo))
                    tocadas += 1

        if tocadas:
            await self._s.commit()

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

    async def buscar_por_codigo(self, codigo: str) -> VehiculoCubicacion | None:
        """La UNICA forma de buscar un vehiculo. Hasta S4-HU-12.1 existia tambien
        `buscar_por_nombre`, y el nombre es editable desde la interfaz:
        renombrar un preset rompia a quien lo buscara asi. Se elimino en vez de
        dejarla como alternativa - tres sitios distintos habian caido en esa
        trampa (desde_linea, demo y la propia siembra)."""
        fila = (await self._s.execute(
            select(VehiculoCubicacionModel).where(VehiculoCubicacionModel.codigo == codigo)
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
        es_preset=v.es_preset,
        codigo=v.codigo, config_vehiculo=v.config_vehiculo, carroceria=v.carroceria,
        clase_viaje=v.clase_viaje, clase_contenedor=v.clase_contenedor,
        geometria=v.geometria, control_ambiental=v.control_ambiental,
        created_at=v.created_at, updated_at=v.updated_at,
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
        # .get("posicionMm"): desde S4-HU-12.1 la posicion puede faltar (el
        # reefer no la tiene). Un JSON viejo siempre la trae, asi que el .get
        # tambien cubre el caso legado sin ramas extra.
        ejes = [EjeVehiculoRow(e.get("posicionMm"), e["capacidadKg"]) for e in json.loads(f.ejes_json)]
    except (json.JSONDecodeError, KeyError, TypeError):
        ejes = []  # tolerante a datos legados/corruptos, igual que el original
    return VehiculoCubicacion(
        id=f.id, nombre=f.nombre, descripcion=f.descripcion,
        largo_interno_mm=f.largo_interno_mm, ancho_interno_mm=f.ancho_interno_mm, alto_interno_mm=f.alto_interno_mm,
        capacidad_kg=f.capacidad_kg, tara_kg=f.tara_kg,
        puerta_ancho_mm=f.puerta_ancho_mm, puerta_alto_mm=f.puerta_alto_mm, puerta_posicion=f.puerta_posicion,
        ejes=ejes, tolerancia_paredes_mm=f.tolerancia_paredes_mm, tolerancia_entre_unidades_mm=f.tolerancia_entre_unidades_mm,
        es_preset=f.es_preset,
        codigo=f.codigo, config_vehiculo=f.config_vehiculo, carroceria=f.carroceria,
        clase_viaje=f.clase_viaje, clase_contenedor=f.clase_contenedor,
        geometria=f.geometria, control_ambiental=f.control_ambiental,
        created_at=f.created_at, updated_at=f.updated_at,
    )
