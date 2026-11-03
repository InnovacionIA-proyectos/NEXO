"""Adaptador de lectura del catalogo (S5-HU-17.2).

Implementa `ICatalogoRepository` leyendo las tablas que el equipo de Datos
administra en `dbo_cubicador`. **No escribe nada**: ni siembra, ni corrige, ni
crea filas que falten. Si el catalogo esta vacio o incompleto, eso se resuelve
del lado de ellos, no silenciandolo aca.

Trae todo de una vez. Son cinco consultas que devuelven menos de cien filas
entre todas, y el motor recorre el catalogo varias veces al evaluar candidatos:
ir y volver por partes seria mas lento y mas ruidoso.
"""

from __future__ import annotations

from decimal import Decimal

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from domain.entidades.catalogo import (
    Carroceria,
    CarroceriaDeVehiculo,
    Catalogo,
    CategoriaMercancia,
    ParametrosMotor,
    ReglaIncompatibilidad,
    TipoMercancia,
    Vehiculo,
)
from infrastructure.persistence import modelos_catalogo as m


def _carroceria(fila: m.TipoCarroceria) -> Carroceria:
    return Carroceria(id=fila.id, codigo=fila.codigo, nombre=fila.nombre, activa=fila.activo)


class SqlCatalogoRepository:
    def __init__(self, sesion: AsyncSession) -> None:
        self._s = sesion

    async def cargar(self) -> Catalogo:
        carrocerias = await self._carrocerias()
        por_id = {c.id: c for c in carrocerias}
        return Catalogo(
            vehiculos=await self._vehiculos(por_id),
            carrocerias=carrocerias,
            mercancias=await self._mercancias(por_id),
            reglas=await self._reglas(),
            parametros=await self._parametros(),
        )

    async def _carrocerias(self) -> tuple[Carroceria, ...]:
        filas = (await self._s.execute(
            select(m.TipoCarroceria).order_by(m.TipoCarroceria.codigo)
        )).scalars().all()
        return tuple(_carroceria(f) for f in filas)

    async def _vehiculos(self, carrocerias: dict[str, Carroceria]) -> tuple[Vehiculo, ...]:
        filas = (await self._s.execute(
            select(m.Vehiculo)
            .options(selectinload(m.Vehiculo.carrocerias))
            .order_by(m.Vehiculo.codigo)
        )).scalars().all()

        vehiculos = []
        for f in filas:
            combinaciones = []
            for rel in f.carrocerias:
                carroceria = carrocerias.get(rel.carroceria_id)
                if carroceria is None:
                    # Una relacion que apunta a una carroceria inexistente es un
                    # problema de datos del lado del equipo. Se omite en vez de
                    # reventar la carga entera, pero no se inventa nada.
                    continue
                capacidad = (f.capacidad_kg if rel.usa_capacidad_del_vehiculo
                             else rel.capacidad_especifica_kg)
                combinaciones.append(CarroceriaDeVehiculo(
                    carroceria=carroceria, capacidad_kg=Decimal(capacidad)))
            vehiculos.append(Vehiculo(
                id=f.id, codigo=f.codigo, nombre=f.nombre,
                largo_cm=Decimal(f.largo_cm),
                ancho_cm=Decimal(f.ancho_cm),
                alto_util_cm=Decimal(f.alto_util_cm),
                capacidad_kg=Decimal(f.capacidad_kg),
                activo=f.activo,
                carrocerias=tuple(combinaciones),
            ))
        return tuple(vehiculos)

    async def _mercancias(self, carrocerias: dict[str, Carroceria]) -> tuple[TipoMercancia, ...]:
        filas = (await self._s.execute(
            select(m.TipoMercancia)
            .options(selectinload(m.TipoMercancia.categoria))
            .order_by(m.TipoMercancia.codigo)
        )).scalars().all()

        return tuple(
            TipoMercancia(
                id=f.id, codigo=f.codigo, nombre=f.nombre,
                categoria=CategoriaMercancia(id=f.categoria.id, nombre=f.categoria.nombre),
                cubicable=f.cubicable,
                condicionada=f.condicionada,
                carroceria_principal=carrocerias.get(f.carroceria_principal_id or ""),
                carroceria_secundaria=carrocerias.get(f.carroceria_secundaria_id or ""),
                secundaria_condicionada=f.secundaria_condicionada,
                control_ambiental=f.control_ambiental,
                apilable=f.apilable,
                fragil=f.fragil,
                rotacion_libre=f.rotacion_libre,
                comentario_condicion=f.comentario_condicion,
                activo=f.activo,
            )
            for f in filas
        )

    async def _reglas(self) -> tuple[ReglaIncompatibilidad, ...]:
        filas = (await self._s.execute(select(m.ReglaIncompatibilidad))).scalars().all()
        return tuple(
            ReglaIncompatibilidad(
                id=f.id,
                categoria_1_id=f.categoria_1_id,
                categoria_2_id=f.categoria_2_id,
                motivo=f.motivo,
            )
            for f in filas
        )

    async def _parametros(self) -> ParametrosMotor:
        filas = (await self._s.execute(select(m.ParametroMotor))).scalars().all()
        return ParametrosMotor(valores={f.clave: f.valor for f in filas})
