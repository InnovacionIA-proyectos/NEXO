"""Adaptador de persistencia de recomendaciones (S5-HU-17.4).

Implementa `IRecomendacionRepository` sobre las tres tablas de recomendacion de
`dbo_cubicador`. **Append-only**: solo inserta y lee. No hay un solo `UPDATE`
ni `DELETE` en este archivo, y es a proposito - ver el docstring del puerto.

Los ids se generan aca con `uuid4` y no se dejan al `newid()` de la base: el
contrato tiene que devolver `idRecomendacion` e `idRecomendacionVehiculo` en la
misma respuesta del POST, y recuperarlos despues del INSERT obligaria a un
`OUTPUT` o a releer. Generarlos antes es mas simple y funciona igual contra
SQLite en las pruebas.

**Se generan en MAYUSCULAS a proposito.** SQL Server devuelve las columnas
`uniqueidentifier` en mayusculas siempre, sin forma de configurarlo. Al
escribir `b9c435cf-...` la reconsulta traia `B9C435CF-...`, asi que el
`idRecomendacionVehiculo` del POST y el de la reconsulta no eran el mismo
texto - un consumidor que los compare como cadenas (y guardar un id es
exactamente para eso) veia dos ids distintos para la misma fila. SQLite no lo
muestra: devuelve lo que se escribio. Generarlos ya en mayusculas hace que los
dos caminos coincidan en los dos motores.
"""

from __future__ import annotations

import json
import uuid
from datetime import datetime, timezone
from decimal import Decimal

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from domain.entidades.recomendacion import (
    ExtradimensionGuardada,
    MercanciaRecomendada,
    PiezaFueraDeMedida,
    RecomendacionGuardada,
    VehiculoDeRecomendacion,
)
from infrastructure.persistence import modelos_catalogo as m

# `MotivoSinSeleccion` es NOT NULL con este default en la base: el equipo de
# Datos pidio no manejar un tercer estado NULL. En el contrato el campo si
# viaja como null cuando el resultado es ASIGNADO, asi que la traduccion ocurre
# en los dos sentidos aca y no se filtra al resto del codigo.
_SIN_MOTIVO = "NINGUNO"


def _ahora() -> datetime:
    return datetime.now(timezone.utc)


def _nuevo_id() -> str:
    """Uuid en mayusculas, que es como los devuelve SQL Server (ver cabecera)."""
    return str(uuid.uuid4()).upper()


def _normalizar(id_: str) -> str:
    """Un uuid es el mismo uuid venga como venga. Un consumidor puede guardarlo
    en minusculas de su lado y devolverlo asi; comparar exacto lo dejaria sin
    encontrar su propia fila."""
    return (id_ or "").strip().upper()


class SqlRecomendacionRepository:
    def __init__(self, sesion: AsyncSession) -> None:
        self._s = sesion

    # ------------------------------------------------------------- escritura

    async def guardar(
        self, resultado, id_linea_solicitud: str, origen_sistema: str, usuario_id: str | None,
    ) -> RecomendacionGuardada:
        """Inserta la corrida completa: la fila padre, sus vehiculos y sus mercancias."""
        creado_por = usuario_id or origen_sistema
        ahora = _ahora()
        recomendacion_id = _nuevo_id()

        extradimension = _extradimension_a_dict(resultado.extradimension)

        self._s.add(m.Recomendacion(
            id=recomendacion_id,
            linea_solicitud_id=id_linea_solicitud,
            origen_sistema=origen_sistema,
            resultado=resultado.resultado,
            motivo_sin_seleccion=resultado.motivo_sin_seleccion or _SIN_MOTIVO,
            condicionada=resultado.condicionada,
            mercancias_condicionadas_json=json.dumps(list(resultado.mercancias_condicionadas)),
            advertencias_json=json.dumps(list(resultado.advertencias)),
            extradimension_json=json.dumps(extradimension or {}),
            creado_por=creado_por, created_at=ahora, updated_at=ahora,
        ))

        # Para resolver codigo -> id de vehiculo, carroceria y mercancia: el
        # motor trabaja con codigos y las tablas guardan ids.
        ids_vehiculo = await self._ids_por_codigo(m.Vehiculo)
        ids_carroceria = await self._ids_por_codigo(m.TipoCarroceria)
        ids_mercancia = await self._ids_por_codigo(m.TipoMercancia)

        vehiculos: list[VehiculoDeRecomendacion] = []
        for vehiculo in resultado.vehiculos:
            vehiculo_id = _nuevo_id()
            self._s.add(m.RecomendacionVehiculo(
                id=vehiculo_id,
                recomendacion_id=recomendacion_id,
                linea_solicitud_id=id_linea_solicitud,
                origen_sistema=origen_sistema,
                vehiculo_id=ids_vehiculo.get(vehiculo.codigo_vehiculo),
                carroceria_id=ids_carroceria.get(vehiculo.codigo_carroceria),
                heterogenea=vehiculo.heterogenea,
                porcentaje_llenado=None,
                porcentaje_llenado_volumen=vehiculo.porcentaje_llenado_volumen,
                porcentaje_llenado_peso=vehiculo.porcentaje_llenado_peso,
                alerta_bajo_llenado=vehiculo.alerta_bajo_llenado,
                peso_total_kg=vehiculo.peso_total_kg,
                volumen_total_m3=vehiculo.volumen_total_m3,
                numero_vehiculo=vehiculo.numero,
                resultado=resultado.resultado,
                motivo_sin_seleccion=None,
                creado_por=creado_por, created_at=ahora, updated_at=ahora,
            ))

            mercancias: list[MercanciaRecomendada] = []
            for mercancia in vehiculo.mercancias:
                id_tipo = ids_mercancia.get(mercancia.codigo_mercancia, "")
                self._s.add(m.RecomendacionVehiculoMercancia(
                    id=_nuevo_id(),
                    recomendacion_vehiculo_id=vehiculo_id,
                    mercancia_id=id_tipo,
                    indice_posicion=mercancia.indice,
                    cantidad=mercancia.cantidad,
                    peso_kg=mercancia.peso_kg,
                    volumen_m3=mercancia.volumen_m3,
                    creado_por=creado_por, created_at=ahora, updated_at=ahora,
                ))
                mercancias.append(MercanciaRecomendada(
                    indice=mercancia.indice,
                    id_tipo_mercancia=id_tipo, codigo_mercancia=mercancia.codigo_mercancia,
                    cantidad=mercancia.cantidad, peso_kg=mercancia.peso_kg,
                    volumen_m3=mercancia.volumen_m3,
                ))

            vehiculos.append(VehiculoDeRecomendacion(
                id=vehiculo_id, numero=vehiculo.numero,
                codigo_vehiculo=vehiculo.codigo_vehiculo,
                nombre_vehiculo=vehiculo.nombre_vehiculo,
                codigo_carroceria=vehiculo.codigo_carroceria,
                peso_total_kg=vehiculo.peso_total_kg,
                volumen_total_m3=vehiculo.volumen_total_m3,
                porcentaje_llenado_volumen=vehiculo.porcentaje_llenado_volumen,
                porcentaje_llenado_peso=vehiculo.porcentaje_llenado_peso,
                alerta_bajo_llenado=vehiculo.alerta_bajo_llenado,
                heterogenea=vehiculo.heterogenea, mercancias=tuple(mercancias),
            ))

        await self._s.commit()

        return RecomendacionGuardada(
            id=recomendacion_id, id_linea_solicitud=id_linea_solicitud,
            origen_sistema=origen_sistema, resultado=resultado.resultado,
            motivo_sin_seleccion=resultado.motivo_sin_seleccion,
            condicionada=resultado.condicionada,
            mercancias_condicionadas=tuple(resultado.mercancias_condicionadas),
            vehiculos=tuple(vehiculos), advertencias=tuple(resultado.advertencias),
            extradimension=_a_extradimension(extradimension), created_at=ahora,
        )

    # --------------------------------------------------------------- lectura

    async def obtener_por_linea(
        self, id_linea_solicitud: str, origen_sistema: str,
    ) -> RecomendacionGuardada | None:
        """La ULTIMA corrida: append-only significa que puede haber varias."""
        fila = (await self._s.execute(
            select(m.Recomendacion)
            .where(
                m.Recomendacion.linea_solicitud_id == id_linea_solicitud,
                m.Recomendacion.origen_sistema == origen_sistema,
            )
            .order_by(m.Recomendacion.created_at.desc())
            .limit(1)
            .options(selectinload(m.Recomendacion.vehiculos)
                     .selectinload(m.RecomendacionVehiculo.mercancias))
        )).scalars().first()
        if fila is None:
            return None

        codigos_vehiculo = await self._codigos_por_id(m.Vehiculo)
        codigos_carroceria = await self._codigos_por_id(m.TipoCarroceria)
        nombres_vehiculo = await self._nombres_por_id(m.Vehiculo)
        codigos_mercancia = await self._codigos_por_id(m.TipoMercancia)

        vehiculos = tuple(
            VehiculoDeRecomendacion(
                id=v.id, numero=v.numero_vehiculo,
                codigo_vehiculo=codigos_vehiculo.get(v.vehiculo_id or "", ""),
                nombre_vehiculo=nombres_vehiculo.get(v.vehiculo_id or "", ""),
                codigo_carroceria=codigos_carroceria.get(v.carroceria_id or "", ""),
                peso_total_kg=v.peso_total_kg, volumen_total_m3=v.volumen_total_m3,
                porcentaje_llenado_volumen=v.porcentaje_llenado_volumen,
                porcentaje_llenado_peso=v.porcentaje_llenado_peso,
                alerta_bajo_llenado=v.alerta_bajo_llenado, heterogenea=v.heterogenea,
                mercancias=tuple(
                    MercanciaRecomendada(
                        indice=x.indice_posicion,
                        id_tipo_mercancia=x.mercancia_id,
                        codigo_mercancia=codigos_mercancia.get(x.mercancia_id, ""),
                        cantidad=x.cantidad, peso_kg=x.peso_kg, volumen_m3=x.volumen_m3,
                    )
                    # Por posicion cuando la hay; las corridas viejas no la
                    # tienen y se ordenan por mercancia como siempre.
                    for x in sorted(
                        v.mercancias,
                        key=lambda x: (x.indice_posicion is None,
                                       x.indice_posicion or 0, x.mercancia_id))
                ),
            )
            for v in sorted(fila.vehiculos, key=lambda v: v.numero_vehiculo)
        )

        return RecomendacionGuardada(
            id=fila.id, id_linea_solicitud=fila.linea_solicitud_id,
            origen_sistema=fila.origen_sistema, resultado=fila.resultado,
            motivo_sin_seleccion=(
                None if fila.motivo_sin_seleccion == _SIN_MOTIVO else fila.motivo_sin_seleccion),
            condicionada=fila.condicionada,
            mercancias_condicionadas=tuple(json.loads(fila.mercancias_condicionadas_json or "[]")),
            vehiculos=vehiculos,
            advertencias=tuple(json.loads(fila.advertencias_json or "[]")),
            extradimension=_a_extradimension(json.loads(fila.extradimension_json or "{}")),
            created_at=fila.created_at,
        )

    async def listar_mercancias(
        self, id_recomendacion_vehiculo: str,
    ) -> list[MercanciaRecomendada] | None:
        id_recomendacion_vehiculo = _normalizar(id_recomendacion_vehiculo)
        existe = (await self._s.execute(
            select(m.RecomendacionVehiculo.id)
            .where(m.RecomendacionVehiculo.id == id_recomendacion_vehiculo)
        )).scalars().first()
        if existe is None:
            return None

        filas = (await self._s.execute(
            select(m.RecomendacionVehiculoMercancia)
            .where(m.RecomendacionVehiculoMercancia.recomendacion_vehiculo_id
                   == id_recomendacion_vehiculo)
        )).scalars().all()
        codigos = await self._codigos_por_id(m.TipoMercancia)
        return [
            MercanciaRecomendada(
                indice=f.indice_posicion,
                id_tipo_mercancia=f.mercancia_id,
                codigo_mercancia=codigos.get(f.mercancia_id, ""),
                cantidad=f.cantidad, peso_kg=f.peso_kg, volumen_m3=f.volumen_m3,
            )
            for f in sorted(
                filas,
                key=lambda f: (f.indice_posicion is None,
                               f.indice_posicion or 0, f.mercancia_id))
        ]

    # ------------------------------------------------------------- auxiliares

    async def _ids_por_codigo(self, modelo) -> dict[str, str]:
        filas = (await self._s.execute(select(modelo.codigo, modelo.id))).all()
        return {codigo: id_ for codigo, id_ in filas}

    async def _codigos_por_id(self, modelo) -> dict[str, str]:
        filas = (await self._s.execute(select(modelo.id, modelo.codigo))).all()
        return {id_: codigo for id_, codigo in filas}

    async def _nombres_por_id(self, modelo) -> dict[str, str]:
        filas = (await self._s.execute(select(modelo.id, modelo.nombre))).all()
        return {id_: nombre for id_, nombre in filas}


# ------------------------------------------------- extradimension <-> JSON


def _extradimension_a_dict(extradimension) -> dict | None:
    """La extradimension se guarda como JSON y no en columnas propias.

    Es un bloque opcional de forma irregular (una lista de piezas, cada una con
    una lista de excesos en texto) que ningun consumidor filtra ni agrega:
    solo lo lee completo. Partirlo en tablas seria estructura sin uso.
    """
    if extradimension is None:
        return None
    return {
        "hayExtradimension": extradimension.hay_extradimension,
        "medidasEstandar": {
            "largoMaxCm": str(extradimension.medidas_estandar.largo_max_cm),
            "anchoMaxCm": str(extradimension.medidas_estandar.ancho_max_cm),
            "altoMaxCm": str(extradimension.medidas_estandar.alto_max_cm),
            "pesoMaxKg": str(extradimension.medidas_estandar.peso_max_kg),
        },
        "piezas": [
            {
                "indicePosicion": p.indice,
                "idTipoMercancia": p.id_tipo_mercancia,
                "codigoMercancia": p.codigo_mercancia,
                "excesos": list(p.excesos),
            }
            for p in extradimension.piezas
        ],
    }


def _a_extradimension(datos: dict | None) -> ExtradimensionGuardada | None:
    if not datos or not datos.get("hayExtradimension"):
        return None
    medidas = datos.get("medidasEstandar", {})
    return ExtradimensionGuardada(
        hay_extradimension=True,
        largo_max_cm=Decimal(str(medidas.get("largoMaxCm", "0"))),
        ancho_max_cm=Decimal(str(medidas.get("anchoMaxCm", "0"))),
        alto_max_cm=Decimal(str(medidas.get("altoMaxCm", "0"))),
        peso_max_kg=Decimal(str(medidas.get("pesoMaxKg", "0"))),
        piezas=tuple(
            PiezaFueraDeMedida(
                # Las corridas anteriores a S5-HU-17.8 no la traen.
                indice=p.get("indicePosicion"),
                id_tipo_mercancia=p.get("idTipoMercancia", ""),
                codigo_mercancia=p.get("codigoMercancia", ""),
                excesos=tuple(p.get("excesos", [])),
            )
            for p in datos.get("piezas", [])
        ),
    )
