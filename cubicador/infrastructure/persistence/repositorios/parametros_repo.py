"""Implementa IParametrosCubicacionRepository con SQLAlchemy (fila unica, id=1).

Devuelve `ParametrosCubicacion`, la entidad de dominio que declara el puerto.
Antes devolvia `MedidasEstandar` de `domain/motor/cubicacion_rapida.py`:
funcionaba por casualidad -los dos tipos tienen los mismos cuatro campos- pero
ataba la infraestructura a un modulo del motor viejo, que se retiro.
"""

from __future__ import annotations

from sqlalchemy.ext.asyncio import AsyncSession

from domain.entidades.entidades import ParametrosCubicacion
from infrastructure.persistence.modelos import ParametrosCubicacionModel


class SqlParametrosCubicacionRepository:
    def __init__(self, sesion: AsyncSession) -> None:
        self._s = sesion

    async def obtener(self) -> ParametrosCubicacion:
        fila = await self._s.get(ParametrosCubicacionModel, 1)
        if fila is None:
            return ParametrosCubicacion()
        return ParametrosCubicacion(fila.largo_max_cm, fila.ancho_max_cm, fila.alto_max_cm, fila.peso_max_kg)

    async def guardar(self, medidas: ParametrosCubicacion) -> ParametrosCubicacion:
        fila = await self._s.get(ParametrosCubicacionModel, 1)
        if fila is None:
            fila = ParametrosCubicacionModel(id=1)
            self._s.add(fila)
        fila.largo_max_cm = medidas.largo_max_cm
        fila.ancho_max_cm = medidas.ancho_max_cm
        fila.alto_max_cm = medidas.alto_max_cm
        fila.peso_max_kg = medidas.peso_max_kg
        await self._s.commit()
        return ParametrosCubicacion(fila.largo_max_cm, fila.ancho_max_cm, fila.alto_max_cm, fila.peso_max_kg)
