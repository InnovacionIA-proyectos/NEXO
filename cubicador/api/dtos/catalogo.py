"""DTOs de los catalogos (S5-HU-17.4 lectura, S5-HU-17.5 escritura).

`GET /tipos-mercancia` y `GET /tipos-carroceria` son la seccion 2.7 del
contrato: los consumen NEXO, que los proyecta por eventos, y Cotifly, que los
cachea. Los `POST`/`PUT`/`DELETE` son los CRUD, que el portal usa por
passthrough desde NEXO.

**Todos los `GET` devuelven `version`** y **todos los `PUT`/`DELETE` la
exigen**: es la concurrencia optimista. Si la fila cambio desde que el
formulario la cargo, el servicio responde 409 en vez de pisar el cambio ajeno.

**`usuarioId` lo declara el llamante.** El Cubicador no tiene usuarios -su
Bearer autoriza un backend, no a una persona- asi que no lo puede verificar.
Se guarda como lo que es, un dato declarado.
"""

from __future__ import annotations

from pydantic import Field

from .comun import ApiModel, DecimalJson


# ------------------------------------------------------------------ lectura


class TipoCarroceriaDto(ApiModel):
    id: str
    codigo_carroceria: str
    nombre_carroceria: str
    activo: bool
    version: int


class TipoMercanciaDto(ApiModel):
    id: str
    codigo_mercancia: str
    nombre_mercancia: str
    categoria: str
    cubicable: bool
    condicionada_cubicacion: bool
    # Codigo de carroceria, no id: es lo legible en un log y lo que el
    # consumidor cruza contra `GET /tipos-carroceria`.
    carroceria_principal: str | None
    carroceria_secundaria: str | None
    carroceria_secundaria_condicionada: bool
    # NINGUNO | REFRIGERADO | VENTILADO_CONDICIONAL | CLIMATIZADO_CONDICIONAL |
    # REFRIGERADO_O_CLIMATIZADO | VENTILADO_O_REFRIGERADO
    control_ambiental_requerido: str
    apilable: bool
    fragil: bool
    rotacion_libre: bool
    comentario_condicion: str | None
    activo: bool
    version: int


class CategoriaMercanciaDto(ApiModel):
    id: str
    nombre_categoria: str
    version: int


class ParametroMotorDto(ApiModel):
    id: str
    clave: str
    valor: str
    descripcion: str | None
    version: int


class CarroceriaDeVehiculoDto(ApiModel):
    id_carroceria: str
    codigo_carroceria: str
    usa_capacidad_maxima_del_vehiculo: bool
    capacidad_maxima_especifica: DecimalJson


class VehiculoCatalogoDto(ApiModel):
    """El vehiculo del catalogo que alimenta al motor.

    **No confundir con `/api/planes-cargue/vehiculos`**, que es el catalogo
    propio del motor 3D (`vehiculos_cubicacion`): otra tabla, otro proposito.
    """

    id: str
    codigo_vehiculo: str
    nombre_vehiculo: str
    largo_cm: DecimalJson
    ancho_cm: DecimalJson
    alto_util_cm: DecimalJson
    capacidad_maxima_kg: DecimalJson
    activo: bool
    version: int
    carrocerias: list[CarroceriaDeVehiculoDto]


class ReglaIncompatibilidadDto(ApiModel):
    id: str
    id_categoria_1: str
    id_categoria_2: str
    motivo: str
    version: int


# ----------------------------------------------------------------- escritura


class _ConUsuario(ApiModel):
    """`usuarioId` es obligatorio en toda escritura.

    No identifica a nadie ante el Cubicador: lo manda NEXO, que si sabe quien
    es, y aqui queda guardado en `CreadoPor` como dato declarado. Exigirlo
    evita que una escritura quede sin rastro de quien la pidio.
    """

    usuario_id: str = Field(min_length=1)


class _ConVersion(_ConUsuario):
    """La version que el formulario leyo. Si ya no es la actual, 409."""

    version: int = Field(ge=1)


class CrearCategoriaDto(_ConUsuario):
    nombre_categoria: str = Field(min_length=1)


class ActualizarCategoriaDto(_ConVersion):
    nombre_categoria: str = Field(min_length=1)


class CrearCarroceriaDto(_ConUsuario):
    codigo_carroceria: str = Field(min_length=1)
    nombre_carroceria: str = Field(min_length=1)


class ActualizarCarroceriaDto(_ConVersion):
    nombre_carroceria: str = Field(min_length=1)
    activo: bool = True


class ActualizarParametroDto(_ConVersion):
    valor: str = Field(min_length=1)


class _MercanciaBase(ApiModel):
    codigo_mercancia: str = Field(min_length=1)
    nombre_mercancia: str = Field(min_length=1)
    id_categoria: str
    cubicable: bool = True
    condicionada_cubicacion: bool = False
    comentario_condicion: str | None = None
    id_carroceria_principal: str
    id_carroceria_secundaria: str | None = None
    carroceria_secundaria_condicionada: bool = False
    control_ambiental_requerido: str = "NINGUNO"
    apilable: bool = True
    fragil: bool = False
    rotacion_libre: bool = True
    activo: bool = True


class CrearMercanciaDto(_MercanciaBase, _ConUsuario):
    pass


class ActualizarMercanciaDto(_MercanciaBase, _ConVersion):
    pass


class CarroceriaDeVehiculoEntradaDto(ApiModel):
    id_carroceria: str
    usa_capacidad_maxima_del_vehiculo: bool = True
    # Solo manda cuando `usaCapacidadMaximaDelVehiculo` es falso; entonces
    # tiene que ser mayor que cero (hay un CHECK en base ademas de aqui).
    capacidad_maxima_especifica: DecimalJson = Field(default=0, ge=0)


class _VehiculoBase(ApiModel):
    codigo_vehiculo: str = Field(min_length=1)
    nombre_vehiculo: str = Field(min_length=1)
    largo_cm: DecimalJson = Field(gt=0)
    ancho_cm: DecimalJson = Field(gt=0)
    alto_util_cm: DecimalJson = Field(gt=0)
    capacidad_maxima_kg: DecimalJson = Field(gt=0)
    activo: bool = True
    # Un solo payload: el vehiculo y sus carrocerias. El backend reconcilia.
    carrocerias: list[CarroceriaDeVehiculoEntradaDto]


class CrearVehiculoDto(_VehiculoBase, _ConUsuario):
    pass


class ActualizarVehiculoDto(_VehiculoBase, _ConVersion):
    pass


class CrearReglaDto(_ConUsuario):
    """Sin `PUT`: una regla se borra y se crea. Cambiarle el par a una regla
    existente es, en los hechos, otra regla."""

    id_categoria_1: str
    id_categoria_2: str
    motivo: str = Field(min_length=1)


class BorrarDto(_ConVersion):
    """Cuerpo de un `DELETE`. Lleva version por lo mismo que el `PUT`: borrar
    una fila que cambio desde que se leyo es tan destructivo como pisarla."""
