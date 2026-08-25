"""Implementa IParametrosCubicacionRepository con SQLAlchemy (fila unica, id=1)."""

from __future__ import annotations

from sqlalchemy.ext.asyncio import AsyncSession

from domain.motor.cubicacion_rapida import MedidasEstandar
from infrastructure.persistence.modelos import ParametrosCubicacionModel


class SqlParametrosCubicacionRepository:
    def __init__(self, sesion: AsyncSession) -> None:
        self._s = sesion

    async def obtener(self) -> MedidasEstandar:
        fila = await self._s.get(ParametrosCubicacionModel, 1)
        if fila is None:
            return MedidasEstandar.por_defecto()
        return MedidasEstandar(fila.largo_max_cm, fila.ancho_max_cm, fila.alto_max_cm, fila.peso_max_kg)

    async def guardar(self, medidas: MedidasEstandar) -> MedidasEstandar:
        fila = await self._s.get(ParametrosCubicacionModel, 1)
        if fila is None:
            fila = ParametrosCubicacionModel(id=1)
            self._s.add(fila)
        fila.largo_max_cm = medidas.largo_max_cm
        fila.ancho_max_cm = medidas.ancho_max_cm
        fila.alto_max_cm = medidas.alto_max_cm
        fila.peso_max_kg = medidas.peso_max_kg
        await self._s.commit()
        return MedidasEstandar(fila.largo_max_cm, fila.ancho_max_cm, fila.alto_max_cm, fila.peso_max_kg)
