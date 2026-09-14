"""DTOs HTTP del cubicador rapido - ver CONTRATO.md seccion 3.1."""

from __future__ import annotations

from .comun import ApiModel


class ItemCubicacionDto(ApiModel):
    descripcion: str | None = None
    cantidad: int = 1
    largo_cm: float
    ancho_cm: float
    alto_cm: float
    peso_kg: float | None = None
    tipo_mercancia: str | None = None


class CubicacionRequestDto(ApiModel):
    items: list[ItemCubicacionDto]
    unidad_ids: list[str] | None = None
    clase_viaje: str | None = None


class UnidadCubicacionDto(ApiModel):
    id: str
    nombre: str
    geometria: str
    control_ambiental: str
    largo_util_m: float
    ancho_util_m: float
    alto_util_m: float
    peso_bruto_permitido_kg: float
    peso_vacio_kg: float
    capacidad_por_eje_kg: list[float]
    config_vehiculo: str | None
    carroceria: str | None
    clase_viaje: str | None
    clase_contenedor: str | None


class CriterioCubicacionDto(ApiModel):
    criterio: str
    resultado: str
    detalle: str
    criticidad: str


class ColocacionDisposicionDto(ApiModel):
    descripcion: str
    x: float
    y: float
    z: float
    largo_m: float
    ancho_m: float
    alto_m: float
    peso_kg: float


class DisposicionDto(ApiModel):
    peso_total_kg: float
    centro_gravedad_m: list[float]
    margen_estabilidad_ok: bool
    factor_utilizacion_volumen: float
    items_sin_ubicar: list[str]
    colocaciones: list[ColocacionDisposicionDto]


class MatrizCubicacionDto(ApiModel):
    unidad: UnidadCubicacionDto
    criterios: list[CriterioCubicacionDto]
    banda: str
    advertencias: list[str]
    disposicion: DisposicionDto | None


class SugerenciaVehiculoDto(ApiModel):
    unidad_id: str
    unidad_nombre: str
    banda: str
    factor_utilizacion: float
    config_vehiculo: str | None
    carroceria: str | None
    clase_viaje: str | None
    clase_contenedor: str | None


class SugerenciaFlotaDto(ApiModel):
    unidad_id: str
    unidad_nombre: str
    cantidad_vehiculos: int
    utilizacion_promedio: float
    config_vehiculo: str | None
    carroceria: str | None
    clase_viaje: str | None
    clase_contenedor: str | None
    disposiciones: list[DisposicionDto]


class MedidasEstandarDto(ApiModel):
    largo_max_cm: float
    ancho_max_cm: float
    alto_max_cm: float
    peso_max_kg: float


class PiezaExcedidaDto(ApiModel):
    descripcion: str
    excesos: list[str]
    largo_cm: float
    ancho_cm: float
    alto_cm: float
    peso_kg: float | None


class DeteccionExtradimensionDto(ApiModel):
    hay_extradimension: bool
    medidas_estandar: MedidasEstandarDto
    piezas: list[PiezaExcedidaDto]
    sugerencia: str | None


class CubicacionResponseDto(ApiModel):
    matrices: list[MatrizCubicacionDto]
    sugerencia: SugerenciaVehiculoDto | None
    advertencias: list[str]
    sugerencia_flota: SugerenciaFlotaDto | None = None
    extradimension: DeteccionExtradimensionDto | None = None
