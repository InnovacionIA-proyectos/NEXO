"""DTOs HTTP del Cubicador 3D / Plan de Cargue - ver docs/CONTRATOS.md seccion A."""

from __future__ import annotations

from datetime import datetime

from .comun import ApiModel


class EjeVehiculoDto(ApiModel):
    posicion_mm: float
    capacidad_kg: float


class VehiculoCubicacionDto(ApiModel):
    id: str
    nombre: str
    descripcion: str | None
    largo_interno_mm: float
    ancho_interno_mm: float
    alto_interno_mm: float
    capacidad_kg: float
    tara_kg: float
    puerta_ancho_mm: float
    puerta_alto_mm: float
    puerta_posicion: str
    ejes: list[EjeVehiculoDto]
    tolerancia_paredes_mm: float
    tolerancia_entre_unidades_mm: float
    es_preset: bool


class GuardarVehiculoInput(ApiModel):
    id: str | None = None
    nombre: str
    descripcion: str | None = None
    largo_interno_mm: float
    ancho_interno_mm: float
    alto_interno_mm: float
    capacidad_kg: float
    tara_kg: float
    puerta_ancho_mm: float
    puerta_alto_mm: float
    puerta_posicion: str
    ejes: list[EjeVehiculoDto]
    tolerancia_paredes_mm: float = 0.0
    tolerancia_entre_unidades_mm: float = 0.0


class ItemPlanCargueDto(ApiModel):
    id: str
    plan_cargue_id: str
    sku: str
    descripcion: str
    pedido: str | None
    cliente: str | None
    destino: str | None
    secuencia_entrega: int
    largo_mm: float
    ancho_mm: float
    alto_mm: float
    peso_kg: float
    cantidad: int
    color: str | None
    prioridad: int
    tipo_embalaje: str | None
    apilable: bool
    max_niveles: int
    max_peso_encima_kg: float
    rotable: bool
    mantener_vertical: bool
    fragil: bool
    peligroso: bool
    refrigerado: bool
    grupo_compatibilidad: str | None
    observaciones: str | None


class GuardarItemPlanCargueInput(ApiModel):
    id: str | None = None
    sku: str = ""
    descripcion: str
    pedido: str | None = None
    cliente: str | None = None
    destino: str | None = None
    secuencia_entrega: int = 1
    largo_mm: float
    ancho_mm: float
    alto_mm: float
    peso_kg: float
    cantidad: int = 1
    color: str | None = None
    prioridad: int = 3
    tipo_embalaje: str | None = None
    apilable: bool = True
    max_niveles: int = 0
    max_peso_encima_kg: float = -1.0
    rotable: bool = True
    mantener_vertical: bool = False
    fragil: bool = False
    peligroso: bool = False
    refrigerado: bool = False
    grupo_compatibilidad: str | None = None
    observaciones: str | None = None


class PlanCargueDto(ApiModel):
    id: str
    nombre: str
    vehiculo_id: str
    linea_solicitud_id: str | None
    numero_vehiculo: int
    estado: str
    estrategia: str
    algoritmo: str
    semilla: int | None
    peso_total_kg: float
    volumen_utilizado_m3: float
    utilizacion_volumen: float
    utilizacion_piso: float
    puntaje: float
    items: list[ItemPlanCargueDto]
    created_at: datetime
    updated_at: datetime


class ColocacionCargueDto(ApiModel):
    id: str
    plan_cargue_id: str
    item_plan_cargue_id: str
    indice_unidad: int
    cargada: bool
    x: float
    y: float
    z: float
    largo_colocado_mm: float
    ancho_colocado_mm: float
    alto_colocado_mm: float
    orientacion_indice: int
    secuencia_cargue: int
    soporte_colocacion_id: str | None
    porcentaje_soporte: float
    peso_encima_kg: float
    estado_estabilidad: str
    accesibilidad: str
    bloqueada: bool
    ajuste_manual: bool
    codigo_rechazo: str | None
    razon_rechazo: str | None


class IncidenciaValidacionDto(ApiModel):
    severidad: str
    codigo_regla: str
    titulo: str
    descripcion: str
    accion_sugerida: str | None
    colocacion_item_id: str | None


class PonderacionesCombinadaDto(ApiModel):
    volumen: float = 0.25
    balance_ejes: float = 0.25
    estabilidad: float = 0.20
    secuencia: float = 0.15
    prioridad: float = 0.10
    unidades: float = 0.05


class MetricasCubicacionDto(ApiModel):
    volumen_interno_m3: float
    volumen_utilizado_m3: float
    porcentaje_volumen: float
    area_piso_m2: float
    area_piso_utilizada_m2: float
    porcentaje_piso: float
    peso_maximo_kg: float
    peso_cargado_kg: float
    porcentaje_peso: float
    unidades_totales: int
    unidades_cargadas: int
    unidades_no_cargadas: int
    pedidos_completos: int
    pedidos_parciales: int
    destinos: int
    altura_maxima_mm: float
    centro_gravedad_mm: list[float]
    peso_por_eje_kg: list[float]
    diferencia_lados_kg: float
    advertencias: int
    violaciones_criticas: int
    puntaje: float
    puntajes_parciales: dict[str, float]
    espacio_vacio_util_m3: float
    espacio_fragmentado_m3: float


class ResultadoPlanDto(ApiModel):
    plan: PlanCargueDto
    vehiculo: VehiculoCubicacionDto
    colocaciones: list[ColocacionCargueDto]
    metricas: MetricasCubicacionDto | None
    incidencias: list[IncidenciaValidacionDto]


class PlanFlotaItemDto(ApiModel):
    id: str
    numero_vehiculo: int
    total_vehiculos: int
    unidades_cargadas: int
    unidades_totales: int


class OptimizarRequestDto(ApiModel):
    estrategia: str = "Combinada"
    algoritmo: str = "bfd"
    semilla: int | None = None
    ponderaciones: PonderacionesCombinadaDto | None = None
    respetar_bloqueadas: bool = False


class AjusteColocacionInput(ApiModel):
    x: float | None = None
    y: float | None = None
    z: float | None = None
    orientacion_indice: int | None = None
    bloqueada: bool | None = None
    retirar: bool | None = None
    reinsertar: bool | None = None


class CrearPlanRequest(ApiModel):
    nombre: str
    vehiculo_id: str


class SolicitudEncoladaDto(ApiModel):
    """Respuesta 202 de una operacion que se resuelve async (ver docs/CONTRATOS.md seccion B)."""

    correlation_id: str
    estado: str = "procesando"
