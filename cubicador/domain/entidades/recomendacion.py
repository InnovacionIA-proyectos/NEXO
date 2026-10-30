"""La recomendacion ya guardada (S5-HU-17.4).

El motor (`domain.motor.seleccion_flota`) devuelve el resultado del calculo sin
identidad: no sabe de ids ni de persistencia. Estas entidades son ese mismo
resultado **despues de guardarse**, con los ids que el consumidor persiste de
su lado y que usa para reconsultar.

Son la forma que devuelven tanto `POST /recomendaciones` como la reconsulta:
una sola representacion, para que una recomendacion recien calculada y una
leida dos anos despues se vean igual.

**Append-only** (seccion 2.9 del contrato): estas filas nunca se actualizan ni
se borran. Recalcular una ruta crea una corrida nueva. Cotifly guarda
`idRecomendacionVehiculo` dentro de una cotizacion y esa fila tiene que seguir
diciendo lo mismo dentro de dos anos - por eso aca se guardan valores
calculados, como `alerta_bajo_llenado`, en vez de recalcularlos al leer con
parametros que pudieron cambiar.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from decimal import Decimal


@dataclass(frozen=True)
class MercanciaRecomendada:
    """Lo que de UNA posicion de la solicitud va en un camion.

    `indice` es la posicion en el arreglo `mercancias` del request. La fila es
    por posicion, no por codigo: un camion puede traer dos filas con el mismo
    `codigo_mercancia` si la solicitud declaro dos posiciones de ese tipo con
    medidas distintas. Quien agrupe por codigo tiene que sumar.

    Es **nullable** al releer corridas viejas: las guardadas antes de
    S5-HU-17.8 no tienen posicion y no se les inventa una.

    Del tipo viajan **id y codigo**: el id para unir contra el catalogo y el
    codigo para leerlo, igual que en `GET /tipos-mercancia`.
    """

    indice: int | None
    id_tipo_mercancia: str
    codigo_mercancia: str
    cantidad: int
    peso_kg: Decimal
    volumen_m3: Decimal


@dataclass(frozen=True)
class VehiculoDeRecomendacion:
    """Un camion de una corrida.

    `id` es el `idRecomendacionVehiculo` del contrato: la referencia que el
    consumidor guarda en su cotizacion.
    """

    id: str
    numero: int
    codigo_vehiculo: str
    nombre_vehiculo: str
    codigo_carroceria: str
    peso_total_kg: Decimal
    volumen_total_m3: Decimal
    porcentaje_llenado_volumen: Decimal
    porcentaje_llenado_peso: Decimal
    alerta_bajo_llenado: bool
    heterogenea: bool
    mercancias: tuple[MercanciaRecomendada, ...]


@dataclass(frozen=True)
class PiezaFueraDeMedida:
    indice: int | None
    id_tipo_mercancia: str
    codigo_mercancia: str
    excesos: tuple[str, ...]


@dataclass(frozen=True)
class ExtradimensionGuardada:
    hay_extradimension: bool
    largo_max_cm: Decimal
    ancho_max_cm: Decimal
    alto_max_cm: Decimal
    peso_max_kg: Decimal
    piezas: tuple[PiezaFueraDeMedida, ...]


@dataclass(frozen=True)
class RecomendacionGuardada:
    """Una corrida del motor, con su identidad.

    `origen_sistema` sale de la llave que autentico la llamada, no del cuerpo:
    es un dato **verificado**, no autodeclarado (seccion 2.1 del contrato).
    """

    id: str
    id_linea_solicitud: str
    origen_sistema: str
    resultado: str
    motivo_sin_seleccion: str | None
    condicionada: bool
    mercancias_condicionadas: tuple[str, ...]
    vehiculos: tuple[VehiculoDeRecomendacion, ...]
    advertencias: tuple[str, ...]
    extradimension: ExtradimensionGuardada | None
    created_at: datetime
