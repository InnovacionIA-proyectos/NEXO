"""Dominio interno del motor de cubicacion 3D (portado tal cual de cabinaFletes).

Convenciones: eje X = largo, eje Y = ancho, eje Z = alto. Unidades: milimetros
y kilogramos. Origen (0,0,0) = esquina inferior interna opuesta a la puerta
trasera. Las coordenadas de una colocacion son la esquina inferior de la unidad.

Nota de traduccion (ver docs/DECISIONES_TECNICAS.md): el original en C# usa `decimal`
para todos los valores; aqui se usa `float`, idiomatico en Python para calculo
geometrico. El algoritmo ya redondea explicitamente (`round(...)`) en los
puntos donde una division podria introducir imprecision, igual que el
original usaba `Math.Round` en esos mismos puntos - no se pierde nada.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum


class PosicionPuerta(str, Enum):
    TRASERA = "trasera"
    FRONTAL = "frontal"
    COSTADO_IZQUIERDO = "costado_izquierdo"
    COSTADO_DERECHO = "costado_derecho"


@dataclass(frozen=True)
class EjeVehiculo:
    """Un eje del vehiculo: posicion longitudinal (mm desde el origen) y capacidad."""

    posicion_mm: float
    capacidad_kg: float


@dataclass(frozen=True)
class Contenedor:
    """Contenedor/vehiculo donde se cubica. Dimensiones INTERNAS en mm."""

    largo_mm: float
    ancho_mm: float
    alto_mm: float
    capacidad_kg: float
    tara_kg: float
    puerta_ancho_mm: float
    puerta_alto_mm: float
    puerta: PosicionPuerta
    ejes: list[EjeVehiculo]
    tolerancia_paredes_mm: float = 0.0
    tolerancia_entre_unidades_mm: float = 0.0

    @property
    def volumen_util_mm3(self) -> float:
        return self.largo_mm * self.ancho_mm * self.alto_mm


@dataclass(frozen=True)
class ItemCarga:
    """Unidad de carga a cubicar (una instancia fisica; las cantidades se
    expanden antes de optimizar). Dimensiones originales en mm, peso en kg."""

    id: str
    sku: str
    descripcion: str
    pedido: str | None
    cliente: str | None
    destino: str | None
    secuencia_entrega: int  # 1 = se entrega primero (debe quedar cerca de la puerta)
    largo_mm: float
    ancho_mm: float
    alto_mm: float
    peso_kg: float
    apilable: bool = True
    max_niveles: int = 0  # 0 = sin limite declarado
    max_peso_encima_kg: float = -1.0  # -1 = sin limite declarado
    rotable: bool = True
    mantener_vertical: bool = False
    fragil: bool = False
    peligroso: bool = False
    refrigerado: bool = False
    grupo_compatibilidad: str | None = None
    prioridad: int = 3  # 1 = critica ... 5 = baja

    @property
    def volumen_mm3(self) -> float:
        return self.largo_mm * self.ancho_mm * self.alto_mm

    @property
    def area_base_mm2(self) -> float:
        return self.largo_mm * self.ancho_mm


@dataclass(frozen=True)
class Orientacion:
    """Dimensiones de una orientacion concreta (mm)."""

    indice: int
    largo_mm: float
    ancho_mm: float
    alto_mm: float

    @property
    def area_base_mm2(self) -> float:
        return self.largo_mm * self.ancho_mm


def orientaciones_de(item: ItemCarga) -> list[Orientacion]:
    """Hasta 6 orientaciones de un prisma, sin duplicadas. Con mantener_vertical
    solo las que conservan el alto original; sin rotable solo la original."""
    if not item.rotable:
        return [Orientacion(0, item.largo_mm, item.ancho_mm, item.alto_mm)]

    todas = [
        (item.largo_mm, item.ancho_mm, item.alto_mm),
        (item.largo_mm, item.alto_mm, item.ancho_mm),
        (item.ancho_mm, item.largo_mm, item.alto_mm),
        (item.ancho_mm, item.alto_mm, item.largo_mm),
        (item.alto_mm, item.largo_mm, item.ancho_mm),
        (item.alto_mm, item.ancho_mm, item.largo_mm),
    ]

    vistas: set[tuple[float, float, float]] = set()
    resultado: list[Orientacion] = []
    for i, (l, w, h) in enumerate(todas):
        if item.mantener_vertical and h != item.alto_mm:
            continue  # rotacion solo horizontal
        if (l, w, h) in vistas:
            continue  # duplicada (dims iguales)
        vistas.add((l, w, h))
        resultado.append(Orientacion(i, l, w, h))
    return resultado


@dataclass
class Colocacion:
    """Una unidad ya colocada en el contenedor."""

    item: ItemCarga
    x: float
    y: float
    z: float
    orientacion: Orientacion
    secuencia_cargue: int = 0
    soporte_id: str | None = None  # None = piso
    porcentaje_soporte: float = 0.0  # 0..1 de su base apoyada
    peso_encima_kg: float = 0.0
    estado_estabilidad: str = "estable"  # estable | advertencia | inestable
    accesibilidad: str = "accesible"  # accesible | parcialmente_bloqueada | bloqueada
    bloqueada: bool = False
    ajuste_manual: bool = False

    @property
    def fin_x(self) -> float:
        return self.x + self.orientacion.largo_mm

    @property
    def fin_y(self) -> float:
        return self.y + self.orientacion.ancho_mm

    @property
    def fin_z(self) -> float:
        return self.z + self.orientacion.alto_mm

    @property
    def volumen_mm3(self) -> float:
        return self.orientacion.largo_mm * self.orientacion.ancho_mm * self.orientacion.alto_mm

    @property
    def centro_masa(self) -> tuple[float, float, float]:
        return (
            self.x + self.orientacion.largo_mm / 2,
            self.y + self.orientacion.ancho_mm / 2,
            self.z + self.orientacion.alto_mm / 2,
        )


@dataclass(frozen=True)
class ApoyoSoporte:
    """Una unidad que soporta (total o parcialmente) a otra apilada encima, con
    la fraccion (0..1) del area de la base superior que descansa sobre ella.
    Una unidad puede apoyarse sobre VARIAS a la vez (puentea dos cajas): cada
    apoyo se valida y recibe su parte proporcional del peso."""

    colocacion: Colocacion
    fraccion: float


@dataclass(frozen=True)
class Rechazo:
    """Un item que no pudo cargarse, con la razon especifica."""

    item: ItemCarga
    codigo_razon: str
    razon: str


@dataclass(frozen=True)
class PuntoExtremo:
    """Punto extremo candidato para colocar la esquina inferior de una unidad
    (colocacion por extreme points)."""

    x: float
    y: float
    z: float


@dataclass
class ResultadoCubicacion:
    """Resultado completo de una corrida del motor."""

    colocaciones: list[Colocacion]
    rechazos: list[Rechazo]
    contenedor: Contenedor
    puntaje: float = 0.0
    puntajes_parciales: dict[str, float] = field(default_factory=dict)
    incidencias: list[IncidenciaValidacion] = field(default_factory=list)


@dataclass(frozen=True)
class IncidenciaValidacion:
    """Incidencia de validacion (concepto ValidationIssue)."""

    severidad: str  # critica | advertencia | informativa
    codigo_regla: str
    titulo: str
    descripcion: str
    accion_sugerida: str | None = None
    colocacion_item_id: str | None = None


class EstrategiaOptimizacion(str, Enum):
    MAXIMIZAR_VOLUMEN = "MaximizarVolumen"
    MAXIMIZAR_UNIDADES = "MaximizarUnidades"
    MAXIMIZAR_PESO = "MaximizarPeso"
    PRIORIZAR_PEDIDOS_CRITICOS = "PriorizarPedidosCriticos"
    PRIORIZAR_SECUENCIA_ENTREGA = "PriorizarSecuenciaEntrega"
    MINIMIZAR_MOVIMIENTOS = "MinimizarMovimientos"
    MEJORAR_ESTABILIDAD = "MejorarEstabilidad"
    BALANCEAR_EJES = "BalancearEjes"
    MINIMIZAR_FRAGMENTACION = "MinimizarFragmentacion"
    COMBINADA = "Combinada"


@dataclass(frozen=True)
class PonderacionesCombinada:
    """Ponderaciones de la estrategia combinada (editables desde la UI)."""

    volumen: float = 0.25
    balance_ejes: float = 0.25
    estabilidad: float = 0.20
    secuencia: float = 0.15
    prioridad: float = 0.10
    unidades: float = 0.05


@dataclass(frozen=True)
class ParametrosOptimizacion:
    estrategia: EstrategiaOptimizacion = EstrategiaOptimizacion.COMBINADA
    ponderaciones: PonderacionesCombinada | None = None
    algoritmo: str = "bfd"  # ffd | bfd
    semilla: int | None = None  # reproducibilidad
    soporte_minimo: float = 0.60
    soporte_recomendado: float = 0.80
