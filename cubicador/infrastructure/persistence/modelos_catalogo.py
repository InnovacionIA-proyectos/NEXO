"""Mapeo de las 10 tablas de `dbo_cubicador` (S5-HU-17.2).

Estas tablas **las crea y las siembra el equipo de Datos** con sus propios
scripts. Mapearlas aca no las convierte en tablas que el Cubicador cree o
altere: solo le da una forma de leerlas.

Van en un modulo aparte de `modelos.py` a proposito. Ese tiene las 5 tablas del
motor 3D, cuyo DDL esta en `docs/Database/ESQUEMA_CUBICADOR.sql`; mezclarlas
haria facil perder de vista cual es cual.

**Convencion de nombres.** El esquema usa camelCase para las tablas (con
`TiposCarroceria` en PascalCase, la unica) y PascalCase para las columnas. El
codigo Python se mantiene idiomatico: cada atributo declara el nombre real de
su columna como primer argumento de `mapped_column`. No se renombra nada en la
base.

**Sobre `AlturaMaximaReglamentariaCm`.** El nombre dice "maxima reglamentaria"
y su default es 440 (el tope legal en Colombia), pero los datos sembrados
guardan el **alto util del vehiculo**: 230 para el Turbo, 260 para la
tractomula. Es lo que el motor necesita para calcular volumen. Si alguien
alguna vez la rellena creyendo el nombre, los volumenes saldrian ~70% mas
grandes. Por eso el atributo de Python se llama `alto_util_cm`.
"""

from __future__ import annotations

import os
from datetime import datetime
from decimal import Decimal

from sqlalchemy import (
    Boolean,
    DateTime,
    ForeignKey,
    Integer,
    MetaData,
    Numeric,
    String,
    Text,
)
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column, relationship

# Mismo criterio que db.py: el esquema solo se declara contra SQL Server, para
# que las pruebas contra SQLite (que no soporta esquemas) sigan funcionando.
_ES_MSSQL = os.environ.get("DATABASE_URL", "").startswith("mssql")
ESQUEMA = os.environ.get("CUBICADOR_DB_ESQUEMA", "dbo_cubicador") if _ES_MSSQL else None


class BaseCatalogo(DeclarativeBase):
    """Base propia, separada de la de `modelos.py`.

    Son dos juegos de tablas con dueños distintos: estas las administra el
    equipo de Datos, las del motor 3D las administra este repo. Compartir el
    `MetaData` haria que un `create_all` las tratara igual, que es justo lo que
    no queremos: el Cubicador nunca debe crear ni alterar las del catalogo.
    """

    metadata = MetaData(schema=ESQUEMA)


# --- Catalogo --------------------------------------------------------------


class Vehiculo(BaseCatalogo):
    """Las 5 configuraciones del Excel de Operaciones: 2L2, C2, C3, 2S2, 3S3."""

    __tablename__ = "vehiculos"

    id: Mapped[str] = mapped_column("Id", String(36), primary_key=True)
    codigo: Mapped[str] = mapped_column("CodigoVehiculo", String(30), unique=True)
    nombre: Mapped[str] = mapped_column("NombreVehiculo", String(120))
    largo_cm: Mapped[Decimal] = mapped_column("LargoCm", Numeric(9, 2))
    ancho_cm: Mapped[Decimal] = mapped_column("AnchoCm", Numeric(9, 2))
    # Ver la nota del encabezado: guarda el alto UTIL, no el tope legal.
    alto_util_cm: Mapped[Decimal] = mapped_column("AlturaMaximaReglamentariaCm", Numeric(9, 2))
    capacidad_kg: Mapped[Decimal] = mapped_column("CapacidadMaxima", Numeric(18, 2))
    activo: Mapped[bool] = mapped_column("Activo", Boolean)
    creado_por: Mapped[str] = mapped_column("CreadoPor", String(100))
    created_at: Mapped[datetime] = mapped_column("CreatedAt", DateTime(timezone=True))
    updated_at: Mapped[datetime] = mapped_column("UpdatedAt", DateTime(timezone=True))

    carrocerias: Mapped[list[RelacionVehiculoCarroceria]] = relationship(
        back_populates="vehiculo", lazy="selectin")


class TipoCarroceria(BaseCatalogo):
    """17 carrocerias, cruzadas contra el catalogo real de SAP.

    `CAMA_BAJA` y `PORTACONTENEDORES` estan sembradas sin ningun vehiculo
    asignado a proposito: con cual emparejan lo decide el Administrador de
    Fletes desde el CRUD.
    """

    # La unica tabla del esquema en PascalCase.
    __tablename__ = "TiposCarroceria"

    id: Mapped[str] = mapped_column("Id", String(36), primary_key=True)
    codigo: Mapped[str] = mapped_column("CodigoCarroceria", String(50), unique=True)
    nombre: Mapped[str] = mapped_column("NombreCarroceria", String(150))
    activo: Mapped[bool] = mapped_column("Activo", Boolean)
    creado_por: Mapped[str] = mapped_column("CreadoPor", String(100))
    created_at: Mapped[datetime] = mapped_column("CreatedAt", DateTime(timezone=True))
    updated_at: Mapped[datetime] = mapped_column("UpdatedAt", DateTime(timezone=True))


class RelacionVehiculoCarroceria(BaseCatalogo):
    """Que carrocerias admite cada vehiculo, y con que capacidad.

    Las relaciones se limitan estrictamente a lo que sale del Excel. Cuando
    `usa_capacidad_del_vehiculo` es falso, manda `capacidad_especifica_kg`: el
    unico caso sembrado es `3S3 + FURGON = 30.000 kg`, menor que los 34.000 del
    chasis porque el furgon pesa.
    """

    __tablename__ = "relacionVehiculosCarroceria"

    id: Mapped[str] = mapped_column("Id", String(36), primary_key=True)
    vehiculo_id: Mapped[str] = mapped_column(
        "IdTipoVehiculo", String(36), ForeignKey("vehiculos.Id"))
    carroceria_id: Mapped[str] = mapped_column(
        "IdCarroceria", String(36), ForeignKey("TiposCarroceria.Id"), index=True)
    usa_capacidad_del_vehiculo: Mapped[bool] = mapped_column(
        "UsaCapacidadMaximaDelVehiculo", Boolean)
    capacidad_especifica_kg: Mapped[Decimal] = mapped_column(
        "CapacidadMaximaEspecifica", Numeric(18, 2))
    creado_por: Mapped[str] = mapped_column("CreadoPor", String(100))
    created_at: Mapped[datetime] = mapped_column("CreatedAt", DateTime(timezone=True))
    updated_at: Mapped[datetime] = mapped_column("UpdatedAt", DateTime(timezone=True))

    vehiculo: Mapped[Vehiculo] = relationship(back_populates="carrocerias", lazy="selectin")
    carroceria: Mapped[TipoCarroceria] = relationship(lazy="selectin")

    @property
    def capacidad_kg(self) -> Decimal:
        """La capacidad que de verdad aplica a esta combinacion."""
        if self.usa_capacidad_del_vehiculo:
            return self.vehiculo.capacidad_kg
        return self.capacidad_especifica_kg


class CategoriaMercancia(BaseCatalogo):
    """13 categorias. La clave de negocio es el nombre, no un codigo aparte.

    `CARBURANTE` y `MATERIAL_INFLAMABLE` son dos categorias separadas: papel,
    carton y madera van bajo la segunda. Es la decision del equipo de Datos, que
    reemplaza lo que pedia la issue original.
    """

    __tablename__ = "categoriaMercancia"

    id: Mapped[str] = mapped_column("Id", String(36), primary_key=True)
    nombre: Mapped[str] = mapped_column("NombreCategoria", String(100), unique=True)
    creado_por: Mapped[str] = mapped_column("CreadoPor", String(100))
    created_at: Mapped[datetime] = mapped_column("CreatedAt", DateTime(timezone=True))
    updated_at: Mapped[datetime] = mapped_column("UpdatedAt", DateTime(timezone=True))


class TipoMercancia(BaseCatalogo):
    """31 tipos de la matriz de Operaciones.

    `cubicable` esta en True en las 31: los 10 tipos excluidos (graneles,
    cemento, combustibles) NO se sembraron, por decision del equipo. Para el
    motor eso significa que un codigo desconocido y uno no cubicable son el
    mismo caso: se rechaza por desconocido.
    """

    __tablename__ = "tiposMercancia"

    id: Mapped[str] = mapped_column("Id", String(36), primary_key=True)
    codigo: Mapped[str] = mapped_column("CodigoMercancia", String(50), unique=True)
    nombre: Mapped[str] = mapped_column("NombreMercancia", String(200))
    categoria_id: Mapped[str] = mapped_column(
        "IdCategoria", String(36), ForeignKey("categoriaMercancia.Id"))
    cubicable: Mapped[bool] = mapped_column("Cubicable", Boolean)
    condicionada: Mapped[bool] = mapped_column("CondicionadaCubicacion", Boolean)
    comentario_condicion: Mapped[str | None] = mapped_column(
        "ComentarioCondicion", String(500), nullable=True)
    carroceria_principal_id: Mapped[str | None] = mapped_column(
        "IdCarroceriaPrincipal", String(36), ForeignKey("TiposCarroceria.Id"), nullable=True)
    carroceria_secundaria_id: Mapped[str | None] = mapped_column(
        "IdCarroceriaSecundaria", String(36), ForeignKey("TiposCarroceria.Id"), nullable=True)
    secundaria_condicionada: Mapped[bool] = mapped_column(
        "CarroceriaSecundariaCondicionada", Boolean)
    # NINGUNO | REFRIGERADO | VENTILADO_CONDICIONAL | CLIMATIZADO_CONDICIONAL |
    # REFRIGERADO_O_CLIMATIZADO | VENTILADO_O_REFRIGERADO
    control_ambiental: Mapped[str] = mapped_column("ControlAmbientalRequerido", String(40))
    apilable: Mapped[bool] = mapped_column("Apilable", Boolean)
    fragil: Mapped[bool] = mapped_column("Fragil", Boolean)
    rotacion_libre: Mapped[bool] = mapped_column("RotacionLibre", Boolean)
    activo: Mapped[bool] = mapped_column("Activo", Boolean)
    creado_por: Mapped[str] = mapped_column("CreadoPor", String(100))
    created_at: Mapped[datetime] = mapped_column("CreatedAt", DateTime(timezone=True))
    updated_at: Mapped[datetime] = mapped_column("UpdatedAt", DateTime(timezone=True))

    categoria: Mapped[CategoriaMercancia] = relationship(lazy="selectin")


class ReglaIncompatibilidad(BaseCatalogo):
    """Que categorias no pueden viajar juntas.

    El par es unico y la base exige que las dos categorias sean distintas
    (`CK_reglasIncompatibilidad_CategoriasDistintas`). El orden del par importa
    para la unicidad: `(A,B)` y `(B,A)` son filas distintas para la base, asi
    que el CRUD tiene que evitar la inversa a mano.
    """

    __tablename__ = "reglasIncompatibilidad"

    id: Mapped[str] = mapped_column("Id", String(36), primary_key=True)
    categoria_1_id: Mapped[str] = mapped_column(
        "IdCategoria1", String(36), ForeignKey("categoriaMercancia.Id"))
    categoria_2_id: Mapped[str] = mapped_column(
        "IdCategoria2", String(36), ForeignKey("categoriaMercancia.Id"))
    motivo: Mapped[str] = mapped_column("Motivo", String(300))
    creado_por: Mapped[str] = mapped_column("CreadoPor", String(100))
    created_at: Mapped[datetime] = mapped_column("CreatedAt", DateTime(timezone=True))
    updated_at: Mapped[datetime] = mapped_column("UpdatedAt", DateTime(timezone=True))


class ParametroMotor(BaseCatalogo):
    """Parametros del motor por clave, incluido `k_max = 10`."""

    __tablename__ = "parametrosMotor"

    id: Mapped[str] = mapped_column("Id", String(36), primary_key=True)
    clave: Mapped[str] = mapped_column("Clave", String(50), unique=True)
    valor: Mapped[str] = mapped_column("Valor", String(100))
    descripcion: Mapped[str | None] = mapped_column("Descripcion", String(300), nullable=True)
    creado_por: Mapped[str] = mapped_column("CreadoPor", String(100))
    created_at: Mapped[datetime] = mapped_column("CreatedAt", DateTime(timezone=True))
    updated_at: Mapped[datetime] = mapped_column("UpdatedAt", DateTime(timezone=True))


# --- Recomendaciones (append-only) -----------------------------------------
#
# Recalcular una ruta crea una corrida NUEVA, nunca actualiza la anterior: una
# cotizacion de Cotifly guarda el id de su camion y esa fila tiene que seguir
# diciendo lo mismo dentro de dos años. Las tres tablas tienen `UpdatedAt`
# aunque por diseño nunca deberia cambiar; esta pendiente de conciliar con el
# equipo de Datos.


class Recomendacion(BaseCatalogo):
    """Una fila por corrida del motor.

    Existe porque `SIN_SELECCION_AUTOMATICA` no produce ningun vehiculo: sin
    esta tabla, esa corrida no tendria donde guardarse. Ademas, los datos de la
    llamada (linea, origen, resultado) viven aca una sola vez en vez de
    repetirse por camion.
    """

    __tablename__ = "recomendaciones"

    id: Mapped[str] = mapped_column("Id", String(36), primary_key=True)
    linea_solicitud_id: Mapped[str] = mapped_column("IdLineaSolicitud", String(100))
    # NEXO | COTIFLY. Verificado contra la llave, no autodeclarado.
    origen_sistema: Mapped[str] = mapped_column("OrigenSistema", String(20))
    # ASIGNADO | SIN_SELECCION_AUTOMATICA
    resultado: Mapped[str] = mapped_column("Resultado", String(30))
    # 'NINGUNO' cuando resultado es ASIGNADO: se evita NULL para no manejar un
    # tercer estado, mismo criterio que `ControlAmbientalRequerido`.
    motivo_sin_seleccion: Mapped[str] = mapped_column("MotivoSinSeleccion", String(500))
    condicionada: Mapped[bool] = mapped_column("Condicionada", Boolean)
    # Que mercancias quedaron marcadas. No se puede derivar al releer: una
    # mercancia queda condicionada por su propia marca O por haberse usado su
    # carroceria de respaldo, y esa segunda razon se pierde si no se guarda.
    mercancias_condicionadas_json: Mapped[str] = mapped_column("MercanciasCondicionadasJson", Text)
    advertencias_json: Mapped[str] = mapped_column("AdvertenciasJson", Text)
    extradimension_json: Mapped[str] = mapped_column("ExtradimensionJson", Text)
    creado_por: Mapped[str] = mapped_column("CreadoPor", String(100))
    created_at: Mapped[datetime] = mapped_column("CreatedAt", DateTime(timezone=True))
    updated_at: Mapped[datetime] = mapped_column("UpdatedAt", DateTime(timezone=True))

    vehiculos: Mapped[list[RecomendacionVehiculo]] = relationship(
        back_populates="recomendacion", lazy="selectin")


class RecomendacionVehiculo(BaseCatalogo):
    """Un camion de una corrida, con su llenado y su peso.

    `peso_total_kg` es el que **Cotifly le manda a NEXO** al crear la solicitud:
    el porcentaje de llenado no sirve para costear.

    Los dos porcentajes van juntos a proposito: el motor filtra por peso pero
    DECIDE por el mayor de los dos, asi que reportar uno solo ocultaria el
    criterio que se uso.
    """

    __tablename__ = "recomendacionVehiculo"

    id: Mapped[str] = mapped_column("Id", String(36), primary_key=True)
    recomendacion_id: Mapped[str] = mapped_column(
        "IdRecomendacion", String(36), ForeignKey("recomendaciones.Id"), index=True)
    linea_solicitud_id: Mapped[str] = mapped_column("IdLineaSolicitud", String(100))
    origen_sistema: Mapped[str] = mapped_column("OrigenSistema", String(20))
    vehiculo_id: Mapped[str | None] = mapped_column("IdTipoVehiculo", String(36), nullable=True)
    carroceria_id: Mapped[str | None] = mapped_column("IdCarroceria", String(36), nullable=True)
    heterogenea: Mapped[bool] = mapped_column("Heterogenea", Boolean)
    # Columna original del equipo. La reemplazan los dos porcentajes de abajo;
    # se mapea para no perder lo que haya escrito en ella.
    porcentaje_llenado: Mapped[Decimal | None] = mapped_column(
        "PorcentajeLlenado", Numeric(5, 2), nullable=True)
    porcentaje_llenado_volumen: Mapped[Decimal] = mapped_column(
        "PorcentajeLlenadoVolumen", Numeric(5, 2))
    porcentaje_llenado_peso: Mapped[Decimal] = mapped_column(
        "PorcentajeLlenadoPeso", Numeric(5, 2))
    # Se guarda, no se recalcula: `umbral_alerta_llenado` es editable por CRUD y
    # esta tabla es append-only. Una fila consultada dentro de dos anos tiene que
    # decir lo que dijo el dia que se calculo.
    alerta_bajo_llenado: Mapped[bool] = mapped_column("AlertaBajoLlenado", Boolean)
    peso_total_kg: Mapped[Decimal] = mapped_column("PesoTotalKg", Numeric(18, 2))
    volumen_total_m3: Mapped[Decimal] = mapped_column("VolumenTotalM3", Numeric(18, 3))
    numero_vehiculo: Mapped[int] = mapped_column("NumeroVehiculo", Integer)
    resultado: Mapped[str] = mapped_column("Resultado", String(30))
    motivo_sin_seleccion: Mapped[str | None] = mapped_column(
        "MotivoSinSeleccion", String(500), nullable=True)
    creado_por: Mapped[str] = mapped_column("CreadoPor", String(100))
    created_at: Mapped[datetime] = mapped_column("CreatedAt", DateTime(timezone=True))
    updated_at: Mapped[datetime] = mapped_column("UpdatedAt", DateTime(timezone=True))

    recomendacion: Mapped[Recomendacion] = relationship(
        back_populates="vehiculos", lazy="selectin")
    mercancias: Mapped[list[RecomendacionVehiculoMercancia]] = relationship(
        back_populates="vehiculo_recomendado", lazy="selectin")


class RecomendacionVehiculoMercancia(BaseCatalogo):
    """Que mercancia va en que camion, con cuanto peso y volumen."""

    __tablename__ = "recomendacionVehiculoMercancia"

    id: Mapped[str] = mapped_column("Id", String(36), primary_key=True)
    recomendacion_vehiculo_id: Mapped[str] = mapped_column(
        "IdRecomendacionVehiculo", String(36),
        ForeignKey("recomendacionVehiculo.Id"), index=True)
    mercancia_id: Mapped[str] = mapped_column("IdMercancia", String(36))
    cantidad: Mapped[int] = mapped_column("CantidadUnidadesMercancia", Integer)
    peso_kg: Mapped[Decimal] = mapped_column("PesoMercanciaKg", Numeric(18, 2))
    # (18,4), no (18,3) como el resto de los volumenes: asi la creo el equipo
    # de Datos. Verificado contra sys.columns (scripts/verificar_mapeo_catalogo.py).
    volumen_m3: Mapped[Decimal] = mapped_column("VolumenMercanciaM3", Numeric(18, 4))
    creado_por: Mapped[str] = mapped_column("CreadoPor", String(100))
    created_at: Mapped[datetime] = mapped_column("CreatedAt", DateTime(timezone=True))
    updated_at: Mapped[datetime] = mapped_column("UpdatedAt", DateTime(timezone=True))

    vehiculo_recomendado: Mapped[RecomendacionVehiculo] = relationship(
        back_populates="mercancias", lazy="selectin")
