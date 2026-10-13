"""DTOs del contrato unico (S5-HU-17.4).

La especificacion esta en la seccion 2 de `docs/HOJA-DE-RUTA-CUBICADOR.md`;
estos DTOs la implementan. Reemplazan a los de `cubicacion_rapida.py`, que
describian matrices, criterios y bandas - una forma que el contrato nuevo no
tiene.

**Mercancias: viajan id Y codigo.** En el request va el `idTipoMercancia`
(acordado con Cotifly el 2026-10-08, su HU-021.5). En la respuesta van los dos,
igual que en `GET /tipos-mercancia`: el id es lo que Cotifly persiste y el
codigo estable es lo que usa NEXO (S5-HU-18.2). Dar los dos evita que cada
consumidor tenga que resolver el otro por su cuenta.
"""

from __future__ import annotations

from pydantic import Field

from .comun import ApiModel, DecimalJson


# ------------------------------------------------------------------ request


class MercanciaSolicitadaDto(ApiModel):
    id_tipo_mercancia: str
    cantidad: int = Field(ge=1)
    largo_cm: DecimalJson = Field(gt=0)
    ancho_cm: DecimalJson = Field(gt=0)
    alto_cm: DecimalJson = Field(gt=0)
    # Por unidad, no el total de la linea.
    peso_kg: DecimalJson = Field(gt=0)


class CrearRecomendacionDto(ApiModel):
    id_linea_solicitud: str
    # Opcional: el origen real sale de la llave. Si viene y no coincide, 401.
    origen_sistema: str | None = None
    # Preferencia, NO filtro: el motor corre completo y si no coincide lo dice
    # en `advertencias` (seccion 2.2 del contrato).
    clase_viaje_seleccionada: str | None = None
    # Declarado por el llamante, para auditoria. No se verifica.
    usuario_id: str | None = None
    mercancias: list[MercanciaSolicitadaDto]


# ----------------------------------------------------------------- respuesta


class MercanciaDeVehiculoDto(ApiModel):
    id_tipo_mercancia: str
    codigo_mercancia: str
    cantidad: int
    peso_kg: DecimalJson
    volumen_m3: DecimalJson


class VehiculoRecomendadoDto(ApiModel):
    """Cada campo es obligatorio y nunca viaja en null cuando el resultado es
    ASIGNADO - ver la tabla de la seccion 2.3. `pesoTotalKg` especialmente: es
    el peso que Cotifly le manda a NEXO al crear la solicitud, y el porcentaje
    no sirve para costear."""

    id_recomendacion_vehiculo: str
    numero_vehiculo: int
    codigo_vehiculo: str
    nombre_vehiculo: str
    codigo_carroceria: str
    peso_total_kg: DecimalJson
    volumen_total_m3: DecimalJson
    porcentaje_llenado_volumen: DecimalJson
    porcentaje_llenado_peso: DecimalJson
    alerta_bajo_llenado: bool
    heterogenea: bool
    mercancias: list[MercanciaDeVehiculoDto]


class MedidasEstandarDto(ApiModel):
    largo_max_cm: DecimalJson
    ancho_max_cm: DecimalJson
    alto_max_cm: DecimalJson
    peso_max_kg: DecimalJson


class PiezaFueraDeMedidaDto(ApiModel):
    id_tipo_mercancia: str
    codigo_mercancia: str
    excesos: list[str]


class ExtradimensionDto(ApiModel):
    hay_extradimension: bool
    medidas_estandar: MedidasEstandarDto
    piezas: list[PiezaFueraDeMedidaDto]


class RecomendacionDto(ApiModel):
    """`SIN_SELECCION_AUTOMATICA`, `condicionada` y `extradimension` vuelven con
    **200**, dentro de `resultado`: lo que antes era error ahora es resultado
    (seccion 2.6). Los codigos de error quedan solo para fallos reales."""

    id_recomendacion: str
    id_linea_solicitud: str
    origen_sistema: str
    # ASIGNADO | SIN_SELECCION_AUTOMATICA
    resultado: str
    # EXTRADIMENSIONADO | SIN_CANDIDATOS_DIMENSIONALES | SUPERA_K_MAX |
    # SIN_CARROCERIA_COMPATIBLE. Null cuando el resultado es ASIGNADO.
    motivo_sin_seleccion: str | None
    condicionada: bool
    mercancias_condicionadas: list[str]
    vehiculos: list[VehiculoRecomendadoDto]
    advertencias: list[str]
    extradimension: ExtradimensionDto | None
