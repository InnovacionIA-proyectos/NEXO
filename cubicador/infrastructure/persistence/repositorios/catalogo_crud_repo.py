"""Escritura de los seis catalogos (S5-HU-17.5).

Implementa `ICatalogoCrudRepository`. Las lecturas del motor siguen en
`catalogo_repo.py`, que no escribe nada: son dos usos distintos de las mismas
tablas.

**Generico a proposito.** Los seis catalogos tienen la misma forma de
persistencia -id, clave de negocio, auditoria y version-, asi que repetir seis
veces el mismo `select/add/commit` solo multiplicaria los lugares donde
equivocarse. Lo que los distingue son las validaciones, y esas viven en
`application/catalogo/casos_de_uso.py`.

La concurrencia la lleva SQLAlchemy con `version_id_col` (ver
`modelos_catalogo.py`): si la fila cambio desde que el llamante la leyo, el
`flush` levanta `StaleDataError` y el borde HTTP responde 409.
"""

from __future__ import annotations

import uuid
from datetime import datetime, timezone

from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm.exc import StaleDataError

from infrastructure.persistence import modelos_catalogo as m

# Nombre publico del catalogo -> modelo y columna por la que se ordena.
CATALOGOS: dict[str, tuple[type, str]] = {
    "parametros": (m.ParametroMotor, "clave"),
    "categorias": (m.CategoriaMercancia, "nombre"),
    "carrocerias": (m.TipoCarroceria, "codigo"),
    "mercancias": (m.TipoMercancia, "codigo"),
    "vehiculos": (m.Vehiculo, "codigo"),
    "reglas": (m.ReglaIncompatibilidad, "id"),
}

# Quien apunta a quien. Sirve para decir "3 mercancias la referencian" en vez
# de dejar salir el error 547 de SQL Server, que no dice cuantas ni de donde.
REFERENCIAS: dict[str, list[tuple[str, type, str]]] = {
    "categorias": [
        ("mercancias", m.TipoMercancia, "categoria_id"),
        ("reglas de incompatibilidad", m.ReglaIncompatibilidad, "categoria_1_id"),
        ("reglas de incompatibilidad", m.ReglaIncompatibilidad, "categoria_2_id"),
    ],
    "carrocerias": [
        ("mercancias (carroceria principal)", m.TipoMercancia, "carroceria_principal_id"),
        ("mercancias (carroceria secundaria)", m.TipoMercancia, "carroceria_secundaria_id"),
        ("vehiculos", m.RelacionVehiculoCarroceria, "carroceria_id"),
    ],
    "mercancias": [
        ("recomendaciones", m.RecomendacionVehiculoMercancia, "mercancia_id"),
    ],
    "vehiculos": [
        ("recomendaciones", m.RecomendacionVehiculo, "vehiculo_id"),
    ],
}


class CatalogoDesconocido(KeyError):
    """Nombre de catalogo que no esta en `CATALOGOS`. Es un error de
    programacion, no de datos: las rutas lo fijan, no el llamante."""


def _modelo(catalogo: str) -> type:
    if catalogo not in CATALOGOS:
        raise CatalogoDesconocido(catalogo)
    return CATALOGOS[catalogo][0]


def _ahora() -> datetime:
    return datetime.now(timezone.utc)


def _nuevo_id() -> str:
    """Uuid en mayusculas, como los devuelve SQL Server (ver recomendacion_repo)."""
    return str(uuid.uuid4()).upper()


class SqlCatalogoCrudRepository:
    def __init__(self, sesion: AsyncSession) -> None:
        self._s = sesion

    async def listar(self, catalogo: str) -> list[object]:
        modelo, orden = CATALOGOS[catalogo] if catalogo in CATALOGOS else (_modelo(catalogo), "id")
        filas = (await self._s.execute(
            select(modelo).order_by(getattr(modelo, orden))
        )).scalars().all()
        return list(filas)

    async def obtener(self, catalogo: str, id: str) -> object | None:
        modelo = _modelo(catalogo)
        return (await self._s.execute(
            select(modelo).where(modelo.id == id)
        )).scalars().first()

    async def crear(self, catalogo: str, datos: dict, creado_por: str) -> object:
        modelo = _modelo(catalogo)
        ahora = _ahora()
        fila = modelo(
            id=_nuevo_id(), creado_por=creado_por, created_at=ahora, updated_at=ahora,
            **datos,
        )
        self._s.add(fila)
        await self._s.commit()
        await self._s.refresh(fila)
        return fila

    async def actualizar(
        self, catalogo: str, id: str, datos: dict, version: int, creado_por: str,
    ) -> object | None:
        fila = await self.obtener(catalogo, id)
        if fila is None:
            return None

        # La version del llamante se compara contra la que acaba de leerse.
        # NO se le asigna a la instancia: `version_id_col` arma el WHERE del
        # UPDATE con el valor CARGADO, no con el asignado, asi que escribirle
        # encima no compara nada y la concurrencia no protegeria de nada.
        #
        # El `version_id_col` sigue sirviendo: cubre la ventana entre este
        # SELECT y el UPDATE, donde otra transaccion podria colarse. Son dos
        # capas del mismo chequeo, no una repetida.
        if fila.version != version:
            raise StaleDataError(
                f"La fila {id} cambio desde que se leyo (version {fila.version}, "
                f"se envio {version})."
            )

        for campo, valor in datos.items():
            setattr(fila, campo, valor)
        fila.updated_at = _ahora()
        fila.creado_por = creado_por

        await self._s.commit()
        await self._s.refresh(fila)
        return fila

    async def tocar(self, catalogo: str, id: str, version: int, creado_por: str) -> object | None:
        """Sube la version de una fila sin cambiarle ningun dato.

        Es lo que hace que el payload de vehiculo + carrocerias este protegido:
        un cambio que solo toca las relaciones no moveria la fila del vehiculo,
        y dos administradores editando sus carrocerias no verian conflicto.
        """
        return await self.actualizar(catalogo, id, {}, version, creado_por)

    async def eliminar(self, catalogo: str, id: str, version: int) -> bool:
        fila = await self.obtener(catalogo, id)
        if fila is None:
            return False
        if fila.version != version:
            raise StaleDataError(
                f"La fila {id} cambio desde que se leyo (version {fila.version}, "
                f"se envio {version})."
            )
        await self._s.delete(fila)
        await self._s.commit()
        return True

    async def contar_referencias(self, catalogo: str, id: str) -> dict[str, int]:
        conteos: dict[str, int] = {}
        for etiqueta, modelo, columna in REFERENCIAS.get(catalogo, []):
            cuantas = (await self._s.execute(
                select(func.count()).select_from(modelo).where(getattr(modelo, columna) == id)
            )).scalar_one()
            if cuantas:
                conteos[etiqueta] = conteos.get(etiqueta, 0) + cuantas
        return conteos

    # --- vehiculos: el payload trae el vehiculo Y sus carrocerias ----------

    async def carrocerias_de(self, vehiculo_id: str) -> list[m.RelacionVehiculoCarroceria]:
        filas = (await self._s.execute(
            select(m.RelacionVehiculoCarroceria)
            .where(m.RelacionVehiculoCarroceria.vehiculo_id == vehiculo_id)
        )).scalars().all()
        return list(filas)

    async def reconciliar_carrocerias(
        self, vehiculo_id: str, deseadas: list[dict], creado_por: str,
    ) -> None:
        """Deja las relaciones del vehiculo exactamente como las pide el payload.

        Reconcilia en vez de borrar y reinsertar: las filas que no cambian
        conservan su `CreatedAt`, que es lo unico que dice desde cuando existe
        esa combinacion. Un borrado y reinsercion la perderia en cada guardado.
        """
        actuales = {r.carroceria_id: r for r in await self.carrocerias_de(vehiculo_id)}
        ahora = _ahora()

        for deseada in deseadas:
            carroceria_id = deseada["carroceria_id"]
            fila = actuales.pop(carroceria_id, None)
            if fila is None:
                self._s.add(m.RelacionVehiculoCarroceria(
                    id=_nuevo_id(), vehiculo_id=vehiculo_id, carroceria_id=carroceria_id,
                    usa_capacidad_del_vehiculo=deseada["usa_capacidad_del_vehiculo"],
                    capacidad_especifica_kg=deseada["capacidad_especifica_kg"],
                    creado_por=creado_por, created_at=ahora, updated_at=ahora,
                ))
            else:
                fila.usa_capacidad_del_vehiculo = deseada["usa_capacidad_del_vehiculo"]
                fila.capacidad_especifica_kg = deseada["capacidad_especifica_kg"]
                fila.updated_at = ahora
                fila.creado_por = creado_por

        for sobrante in actuales.values():
            await self._s.delete(sobrante)

        await self._s.commit()
