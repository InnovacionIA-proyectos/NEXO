"""Modelos SQLAlchemy - tablas identicas en nombre a NEXO/cabinaFletes:
vehiculos_cubicacion, planes_cargue, items_plan_cargue, colocaciones_cargue,
parametros_cubicacion. `EjesVehiculo` se guarda como JSON en la columna
`ejes_json` (igual que el `EjesJson` string original) - el repositorio lo
traduce a la lista tipada `EjeVehiculoRow` antes de exponerlo a Application."""

from __future__ import annotations

from datetime import datetime, timezone

from sqlalchemy import Boolean, DateTime, Float, ForeignKey, Integer, String, Text
from sqlalchemy.orm import Mapped, mapped_column, relationship

from .db import Base


def _ahora() -> datetime:
    return datetime.now(timezone.utc)


class VehiculoCubicacionModel(Base):
    __tablename__ = "vehiculos_cubicacion"

    id: Mapped[str] = mapped_column(String(36), primary_key=True)
    nombre: Mapped[str] = mapped_column(String(100))
    descripcion: Mapped[str | None] = mapped_column(String(300), nullable=True)
    largo_interno_mm: Mapped[float] = mapped_column(Float)
    ancho_interno_mm: Mapped[float] = mapped_column(Float)
    alto_interno_mm: Mapped[float] = mapped_column(Float)
    capacidad_kg: Mapped[float] = mapped_column(Float)
    tara_kg: Mapped[float] = mapped_column(Float)
    puerta_ancho_mm: Mapped[float] = mapped_column(Float)
    puerta_alto_mm: Mapped[float] = mapped_column(Float)
    puerta_posicion: Mapped[str] = mapped_column(String(20), default="trasera")
    ejes_json: Mapped[str] = mapped_column(Text, default="[]")
    tolerancia_paredes_mm: Mapped[float] = mapped_column(Float, default=0.0)
    tolerancia_entre_unidades_mm: Mapped[float] = mapped_column(Float, default=0.0)
    es_preset: Mapped[bool] = mapped_column(Boolean, default=False)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_ahora)
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_ahora, onupdate=_ahora)


class PlanCargueModel(Base):
    __tablename__ = "planes_cargue"

    id: Mapped[str] = mapped_column(String(36), primary_key=True)
    nombre: Mapped[str] = mapped_column(String(150))
    vehiculo_id: Mapped[str] = mapped_column(String(36), ForeignKey("vehiculos_cubicacion.id"))
    linea_solicitud_id: Mapped[str | None] = mapped_column(String(36), nullable=True, index=True)
    numero_vehiculo: Mapped[int] = mapped_column(Integer, default=1)
    estado: Mapped[str] = mapped_column(String(20), default="borrador")
    estrategia: Mapped[str] = mapped_column(String(40), default="Combinada")
    ponderaciones_json: Mapped[str | None] = mapped_column(Text, nullable=True)
    algoritmo: Mapped[str] = mapped_column(String(10), default="bfd")
    semilla: Mapped[int | None] = mapped_column(Integer, nullable=True)
    peso_total_kg: Mapped[float] = mapped_column(Float, default=0.0)
    volumen_utilizado_m3: Mapped[float] = mapped_column(Float, default=0.0)
    utilizacion_volumen: Mapped[float] = mapped_column(Float, default=0.0)
    utilizacion_piso: Mapped[float] = mapped_column(Float, default=0.0)
    puntaje: Mapped[float] = mapped_column(Float, default=0.0)
    incidencias_json: Mapped[str] = mapped_column(Text, default="[]")
    error_mensaje: Mapped[str | None] = mapped_column(String(500), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_ahora)
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_ahora, onupdate=_ahora)

    items: Mapped[list["ItemPlanCargueModel"]] = relationship(
        back_populates="plan", cascade="all, delete-orphan", lazy="selectin",
    )


class ItemPlanCargueModel(Base):
    __tablename__ = "items_plan_cargue"

    id: Mapped[str] = mapped_column(String(36), primary_key=True)
    plan_cargue_id: Mapped[str] = mapped_column(String(36), ForeignKey("planes_cargue.id"), index=True)
    sku: Mapped[str] = mapped_column(String(60), default="")
    descripcion: Mapped[str] = mapped_column(String(200))
    pedido: Mapped[str | None] = mapped_column(String(60), nullable=True)
    cliente: Mapped[str | None] = mapped_column(String(120), nullable=True)
    destino: Mapped[str | None] = mapped_column(String(120), nullable=True)
    secuencia_entrega: Mapped[int] = mapped_column(Integer, default=1)
    largo_mm: Mapped[float] = mapped_column(Float)
    ancho_mm: Mapped[float] = mapped_column(Float)
    alto_mm: Mapped[float] = mapped_column(Float)
    peso_kg: Mapped[float] = mapped_column(Float)
    cantidad: Mapped[int] = mapped_column(Integer, default=1)
    color: Mapped[str | None] = mapped_column(String(9), nullable=True)
    prioridad: Mapped[int] = mapped_column(Integer, default=3)
    tipo_embalaje: Mapped[str | None] = mapped_column(String(40), nullable=True)
    apilable: Mapped[bool] = mapped_column(Boolean, default=True)
    max_niveles: Mapped[int] = mapped_column(Integer, default=0)
    max_peso_encima_kg: Mapped[float] = mapped_column(Float, default=-1.0)
    rotable: Mapped[bool] = mapped_column(Boolean, default=True)
    mantener_vertical: Mapped[bool] = mapped_column(Boolean, default=False)
    fragil: Mapped[bool] = mapped_column(Boolean, default=False)
    peligroso: Mapped[bool] = mapped_column(Boolean, default=False)
    refrigerado: Mapped[bool] = mapped_column(Boolean, default=False)
    grupo_compatibilidad: Mapped[str | None] = mapped_column(String(40), nullable=True)
    observaciones: Mapped[str | None] = mapped_column(String(300), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_ahora)
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_ahora, onupdate=_ahora)

    plan: Mapped["PlanCargueModel"] = relationship(back_populates="items")


class ColocacionCargueModel(Base):
    __tablename__ = "colocaciones_cargue"

    id: Mapped[str] = mapped_column(String(36), primary_key=True)
    plan_cargue_id: Mapped[str] = mapped_column(String(36), ForeignKey("planes_cargue.id"), index=True)
    item_plan_cargue_id: Mapped[str] = mapped_column(String(36), ForeignKey("items_plan_cargue.id"), index=True)
    indice_unidad: Mapped[int] = mapped_column(Integer, default=0)
    cargada: Mapped[bool] = mapped_column(Boolean, default=False)
    x: Mapped[float] = mapped_column(Float, default=0.0)
    y: Mapped[float] = mapped_column(Float, default=0.0)
    z: Mapped[float] = mapped_column(Float, default=0.0)
    largo_colocado_mm: Mapped[float] = mapped_column(Float, default=0.0)
    ancho_colocado_mm: Mapped[float] = mapped_column(Float, default=0.0)
    alto_colocado_mm: Mapped[float] = mapped_column(Float, default=0.0)
    orientacion_indice: Mapped[int] = mapped_column(Integer, default=0)
    secuencia_cargue: Mapped[int] = mapped_column(Integer, default=0)
    soporte_colocacion_id: Mapped[str | None] = mapped_column(String(36), nullable=True)
    porcentaje_soporte: Mapped[float] = mapped_column(Float, default=0.0)
    peso_encima_kg: Mapped[float] = mapped_column(Float, default=0.0)
    estado_estabilidad: Mapped[str] = mapped_column(String(20), default="estable")
    accesibilidad: Mapped[str] = mapped_column(String(30), default="accesible")
    bloqueada: Mapped[bool] = mapped_column(Boolean, default=False)
    ajuste_manual: Mapped[bool] = mapped_column(Boolean, default=False)
    codigo_rechazo: Mapped[str | None] = mapped_column(String(30), nullable=True)
    razon_rechazo: Mapped[str | None] = mapped_column(String(500), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_ahora)
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_ahora, onupdate=_ahora)


class ParametrosCubicacionModel(Base):
    __tablename__ = "parametros_cubicacion"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, default=1)
    # Los del vehiculo mas grande que no es extradimensionado (#304) - ver
    # `ParametrosCubicacion` en domain/entidades/entidades.py.
    largo_max_cm: Mapped[float] = mapped_column(Float, default=1220.0)
    ancho_max_cm: Mapped[float] = mapped_column(Float, default=240.0)
    alto_max_cm: Mapped[float] = mapped_column(Float, default=260.0)
    peso_max_kg: Mapped[float] = mapped_column(Float, default=34000.0)
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), default=_ahora, onupdate=_ahora)


class IaSombraModel(Base):
    """Diferencias entre el motor y el modelo de lenguaje en modo sombra.

    Una fila por recomendacion comparada (o por intento que no se pudo comparar:
    `Estado` trae el codigo del problema). Solo se agrega, nunca se actualiza.
    No guarda el contenido que se envia al modelo; `IdRecomendacion` une contra
    la recomendacion del motor.
    """

    __tablename__ = "iaSombra"

    id: Mapped[str] = mapped_column("Id", String(36), primary_key=True)
    id_recomendacion: Mapped[str] = mapped_column("IdRecomendacion", String(36), index=True)
    id_linea_solicitud: Mapped[str] = mapped_column("IdLineaSolicitud", String(100))
    origen_sistema: Mapped[str] = mapped_column("OrigenSistema", String(20))
    estado: Mapped[str] = mapped_column("Estado", String(40))
    modelo: Mapped[str | None] = mapped_column("Modelo", String(100), nullable=True)
    esfuerzo: Mapped[str | None] = mapped_column("Esfuerzo", String(10), nullable=True)
    resultado_motor: Mapped[str] = mapped_column("ResultadoMotor", String(40))
    resultado_ia: Mapped[str | None] = mapped_column("ResultadoIa", String(40), nullable=True)
    motivo_motor: Mapped[str | None] = mapped_column("MotivoMotor", String(40), nullable=True)
    motivo_ia: Mapped[str | None] = mapped_column("MotivoIa", String(40), nullable=True)
    cantidad_motor: Mapped[int] = mapped_column("CantidadMotor", Integer)
    cantidad_ia: Mapped[int | None] = mapped_column("CantidadIa", Integer, nullable=True)
    flota_motor: Mapped[str] = mapped_column("FlotaMotor", Text)
    flota_ia: Mapped[str | None] = mapped_column("FlotaIa", Text, nullable=True)
    coincide_resultado: Mapped[bool | None] = mapped_column("CoincideResultado", Boolean, nullable=True)
    coincide_flota: Mapped[bool | None] = mapped_column("CoincideFlota", Boolean, nullable=True)
    coincide_configuracion: Mapped[bool | None] = mapped_column("CoincideConfiguracion", Boolean, nullable=True)
    ia_respeta_peso: Mapped[bool | None] = mapped_column("IaRespetaPeso", Boolean, nullable=True)
    ia_respeta_volumen: Mapped[bool | None] = mapped_column("IaRespetaVolumen", Boolean, nullable=True)
    ia_conserva_unidades: Mapped[bool | None] = mapped_column("IaConservaUnidades", Boolean, nullable=True)
    violaciones: Mapped[str | None] = mapped_column("Violaciones", Text, nullable=True)
    explicacion: Mapped[str | None] = mapped_column("Explicacion", String(500), nullable=True)
    tokens_entrada: Mapped[int] = mapped_column("TokensEntrada", Integer, default=0)
    tokens_salida: Mapped[int] = mapped_column("TokensSalida", Integer, default=0)
    latencia_ia_ms: Mapped[int | None] = mapped_column("LatenciaIaMs", Integer, nullable=True)
    created_at: Mapped[datetime] = mapped_column("CreatedAt", DateTime(timezone=True), default=_ahora, index=True)
