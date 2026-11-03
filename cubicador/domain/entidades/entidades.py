"""Entidades de persistencia del Cubicador 3D - equivalentes a
`VehiculoCubicacion`/`PlanCargue`/`ItemPlanCargue`/`ColocacionCargue`/
`ParametrosCubicacion` en NEXO/cabinaFletes. Dataclasses puras (sin ORM):
`infrastructure/persistence/modelos.py` las traduce a/desde SQLAlchemy.
"""

from __future__ import annotations

import uuid
from dataclasses import dataclass, field
from datetime import datetime, timezone


def _uuid() -> str:
    return str(uuid.uuid4())


def _ahora() -> datetime:
    return datetime.now(timezone.utc)


@dataclass
class EjeVehiculoRow:
    """Un eje persistido dentro de VehiculoCubicacion (se serializa junto al
    vehiculo, no es tabla propia - igual que en el original)."""

    posicion_mm: float
    capacidad_kg: float


@dataclass
class VehiculoCubicacion:
    """Tipo de vehiculo/contenedor (VehicleType). Los presets son filas
    editables, no constantes."""

    nombre: str
    largo_interno_mm: float
    ancho_interno_mm: float
    alto_interno_mm: float
    capacidad_kg: float
    tara_kg: float
    puerta_ancho_mm: float
    puerta_alto_mm: float
    puerta_posicion: str = "trasera"  # trasera | frontal | costado_izquierdo | costado_derecho
    ejes: list[EjeVehiculoRow] = field(default_factory=list)
    tolerancia_paredes_mm: float = 0.0
    tolerancia_entre_unidades_mm: float = 0.0
    es_preset: bool = False
    descripcion: str | None = None
    id: str = field(default_factory=_uuid)
    created_at: datetime = field(default_factory=_ahora)
    updated_at: datetime = field(default_factory=_ahora)


@dataclass
class ItemPlanCargue:
    """Unidad de carga registrada en el plan (CargoItem). La cantidad se
    expande al optimizar."""

    plan_cargue_id: str
    descripcion: str
    sku: str = ""
    pedido: str | None = None
    cliente: str | None = None
    destino: str | None = None
    secuencia_entrega: int = 1  # 1 = se entrega primero
    largo_mm: float = 0.0
    ancho_mm: float = 0.0
    alto_mm: float = 0.0
    peso_kg: float = 0.0
    cantidad: int = 1
    color: str | None = None
    prioridad: int = 3  # 1 = critica ... 5 = baja
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
    id: str = field(default_factory=_uuid)
    created_at: datetime = field(default_factory=_ahora)
    updated_at: datetime = field(default_factory=_ahora)


@dataclass
class ColocacionCargue:
    """Resultado por unidad fisica (CargoPlacement): una fila por instancia
    expandida de cada item. Las NO cargadas se persisten con cargada=False y
    su razon."""

    plan_cargue_id: str
    item_plan_cargue_id: str
    indice_unidad: int = 0  # 0-based dentro de la cantidad del item
    cargada: bool = False
    x: float = 0.0
    y: float = 0.0
    z: float = 0.0
    largo_colocado_mm: float = 0.0
    ancho_colocado_mm: float = 0.0
    alto_colocado_mm: float = 0.0
    orientacion_indice: int = 0
    secuencia_cargue: int = 0
    soporte_colocacion_id: str | None = None
    porcentaje_soporte: float = 0.0
    peso_encima_kg: float = 0.0
    estado_estabilidad: str = "estable"  # estable | advertencia | inestable
    accesibilidad: str = "accesible"  # accesible | parcialmente_bloqueada | bloqueada
    bloqueada: bool = False
    ajuste_manual: bool = False
    codigo_rechazo: str | None = None
    razon_rechazo: str | None = None
    id: str = field(default_factory=_uuid)
    created_at: datetime = field(default_factory=_ahora)
    updated_at: datetime = field(default_factory=_ahora)


@dataclass
class PlanCargue:
    """Plan de cargue (LoadPlan)."""

    nombre: str
    vehiculo_id: str
    linea_solicitud_id: str | None = None
    numero_vehiculo: int = 1  # posicion (1-based) dentro de la flota de una linea
    estado: str = "borrador"  # borrador | procesando | optimizado | ajustado | error
    estrategia: str = "Combinada"
    ponderaciones_json: str | None = None
    algoritmo: str = "bfd"
    semilla: int | None = None
    peso_total_kg: float = 0.0
    volumen_utilizado_m3: float = 0.0
    utilizacion_volumen: float = 0.0
    utilizacion_piso: float = 0.0
    puntaje: float = 0.0
    incidencias_json: str = "[]"
    error_mensaje: str | None = None  # solo con estado="error"
    id: str = field(default_factory=_uuid)
    created_at: datetime = field(default_factory=_ahora)
    updated_at: datetime = field(default_factory=_ahora)
    items: list[ItemPlanCargue] = field(default_factory=list)


@dataclass
class ParametrosCubicacion:
    """Fila unica (singleton, id=1) con las medidas estandar contra las que
    se detecta extradimension.

    Los maximos son los del vehiculo mas grande que NO es extradimensionado
    (#304). Antes eran 260 de ancho y 300 de alto, por encima de los 240x260 de
    la flota: una pieza de 245x245x255 pasaba el umbral pero no cabia en ningun
    vehiculo en ninguna orientacion, y se reportaba `SIN_CANDIDATOS_DIMENSIONALES`
    en vez de `EXTRADIMENSIONADO`. El operario leia "no hay candidatos" cuando lo
    que tenia era una pieza sobreancha que necesita permiso especial.

    Son editables por `PUT /medidas-estandar`: esto es el arranque, no la verdad.
    Si entra un vehiculo mas grande al catalogo, hay que subirlos.
    """

    largo_max_cm: float = 1220.0
    ancho_max_cm: float = 240.0
    alto_max_cm: float = 260.0
    peso_max_kg: float = 34000.0
    id: int = 1
    updated_at: datetime = field(default_factory=_ahora)
