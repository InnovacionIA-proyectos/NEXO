"""DTOs del contrato unico (S5-HU-17.4).

La especificacion esta en la seccion 2 de `docs/HOJA-DE-RUTA-CUBICADOR.md`;
estos DTOs la implementan. Reemplazan a los de `cubicacion_rapida.py`, que
describian matrices, criterios y bandas - una forma que el contrato nuevo no
tiene.

**Mercancias: entra el codigo, salen los dos.** En el request va el
`codigoMercancia` (S5-HU-17.7, reemplaza al `idTipoMercancia` que se habia
acordado el 2026-10-08). En la respuesta siguen viajando los dos, igual que en
`GET /tipos-mercancia`: dar los dos evita que cada consumidor tenga que
resolver el otro por su cuenta, y el id sigue siendo la llave para unir contra
el catalogo.

El request cambio al codigo porque es la forma que el dato ya tiene en el resto
del recorrido: NEXO lo persiste asi en la captura
(`CapturaRequests.CodigoTipoMercancia`) y `codigoVehiculo`/`codigoCarroceria`
tambien viajan como codigo. Ver `docs/DECISIONES_TECNICAS.md` #17.
"""

from __future__ import annotations

from pydantic import Field

from .comun import ApiModel, DecimalJson


# ------------------------------------------------------------------ request


class MercanciaSolicitadaDto(ApiModel):
    # El `codigoMercancia` de `GET /tipos-mercancia`, no el id (S5-HU-17.7).
    codigo_mercancia: str
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


class ExplicacionDto(ApiModel):
    """El texto es OPCIONAL: `null` cuando la IA no esta configurada, fallo, o
    escribio una cifra que no era del motor. La pantalla se dibuja igual sin el
    (#369, segundo criterio)."""

    explicacion: str | None = None


class MercanciaDeVehiculoDto(ApiModel):
    """Lo que de UNA posicion de la solicitud viaja en este camion.

    **Una fila por posicion, no por codigo** (S5-HU-17.8): dos filas del mismo
    camion pueden traer el mismo `codigoMercancia` si la solicitud declaro dos
    posiciones de ese tipo con medidas distintas. Quien agrupe por codigo tiene
    que sumar.

    `indicePosicion` es el indice (base 0) en el arreglo `mercancias` del
    request, que es como el llamante recupera las medidas: las mando el. Viaja
    en null al reconsultar una corrida anterior a S5-HU-17.8.
    """

    indice_posicion: int | None
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
    # `indicePosicion` por la misma razon que en MercanciaDeVehiculoDto: dos
    # posiciones del mismo tipo pueden tener medidas distintas y que solo una
    # exceda. Null al releer una corrida anterior a S5-HU-17.8.
    indice_posicion: int | None
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
