"""Implementa IIaSombraRepository sobre la tabla `iaSombra`."""

from __future__ import annotations

import json
import uuid
from datetime import datetime

from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from application.ia_sombra.registro import RegistroSombra
from infrastructure.persistence.modelos import IaSombraModel


class SqlIaSombraRepository:
    def __init__(self, sesion: AsyncSession) -> None:
        self._s = sesion

    async def guardar(self, registro: RegistroSombra) -> None:
        self._s.add(IaSombraModel(
            id=str(uuid.uuid4()),
            id_recomendacion=registro.id_recomendacion, id_linea_solicitud=registro.id_linea_solicitud,
            origen_sistema=registro.origen_sistema, estado=registro.estado,
            modelo=registro.modelo, esfuerzo=registro.esfuerzo,
            resultado_motor=registro.resultado_motor, resultado_ia=registro.resultado_ia,
            motivo_motor=registro.motivo_motor, motivo_ia=registro.motivo_ia,
            cantidad_motor=registro.cantidad_motor, cantidad_ia=registro.cantidad_ia,
            flota_motor=json.dumps(registro.flota_motor),
            flota_ia=None if registro.flota_ia is None else json.dumps(registro.flota_ia),
            coincide_resultado=registro.coincide_resultado, coincide_flota=registro.coincide_flota,
            coincide_configuracion=registro.coincide_configuracion,
            ia_respeta_peso=registro.ia_respeta_peso, ia_respeta_volumen=registro.ia_respeta_volumen,
            ia_conserva_unidades=registro.ia_conserva_unidades,
            violaciones=json.dumps(registro.violaciones, ensure_ascii=False) if registro.violaciones else None,
            explicacion=registro.explicacion,
            tokens_entrada=registro.tokens_entrada, tokens_salida=registro.tokens_salida,
            latencia_ia_ms=registro.latencia_ia_ms,
        ))
        await self._s.commit()

    async def tokens_desde(self, desde: datetime) -> int:
        total = await self._s.scalar(
            select(func.coalesce(func.sum(IaSombraModel.tokens_entrada + IaSombraModel.tokens_salida), 0))
            .where(IaSombraModel.created_at >= desde))
        return int(total or 0)
