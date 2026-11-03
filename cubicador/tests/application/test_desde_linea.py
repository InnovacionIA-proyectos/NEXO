"""El puente Mesa de Costeo -> Cubicador (S5-HU-17.6).

Reemplaza a `test_desde_linea_preset_no_encontrado.py`, que cubria el puente
viejo: el que adivinaba cuantos vehiculos hacian falta abriendo hasta veinte
iguales y, si no reconocia el vehiculo sugerido, asumia un furgon de 13,6 m.

Lo que se verifica ahora:

 - que **no haya ningun default silencioso**: cada traduccion que falla es un
   rechazo con motivo;
 - que el puente use la flota que indico el motor de seleccion, con su
   cantidad y sus tipos, en vez de adivinar;
 - que el diccionario de compatibilidad cubra las cinco configuraciones
   canonicas, y que el 3S3 caiga en el preset correcto segun la carroceria.
"""

from __future__ import annotations

from decimal import Decimal

import pytest

from application.excepciones import NotFoundException, ValidationFailedException
from application.plan_cargue import desde_linea as puente
from application.plan_cargue.desde_linea import crear_plan_desde_linea
from application.ports.externos import LineaParaCubicacion, PosicionCarga
from domain.entidades.catalogo import (
    Carroceria,
    CarroceriaDeVehiculo,
    Catalogo,
    CategoriaMercancia,
    ParametrosMotor,
    TipoMercancia,
    Vehiculo,
)
from domain.entidades.entidades import ParametrosCubicacion

FURGON = Carroceria("c-furgon", "FURGON", "Furgon")
PLATAFORMA = Carroceria("c-plat", "PLATAFORMA", "Plataforma")
GENERAL = CategoriaMercancia("cat-gen", "GENERAL")

BEBIDAS = TipoMercancia(
    id="m-beb", codigo="BEBIDAS", nombre="Bebidas", categoria=GENERAL, cubicable=True,
    condicionada=False, carroceria_principal=FURGON, carroceria_secundaria=None,
    secundaria_condicionada=False, control_ambiental="NINGUNO", apilable=True,
    fragil=False, rotacion_libre=True)

BOBINAS = TipoMercancia(
    id="m-bob", codigo="BOBINAS_ACERO", nombre="Bobinas", categoria=GENERAL, cubicable=True,
    condicionada=False, carroceria_principal=PLATAFORMA, carroceria_secundaria=None,
    secundaria_condicionada=False, control_ambiental="NINGUNO", apilable=True,
    fragil=False, rotacion_libre=True)


def _vehiculo(codigo, nombre, largo, ancho, alto, capacidad, carrocerias):
    return Vehiculo(
        id=f"v-{codigo}", codigo=codigo, nombre=nombre, largo_cm=Decimal(largo),
        ancho_cm=Decimal(ancho), alto_util_cm=Decimal(alto), capacidad_kg=Decimal(capacidad),
        activo=True,
        carrocerias=tuple(CarroceriaDeVehiculo(c, Decimal(cap)) for c, cap in carrocerias))


CATALOGO = Catalogo(
    vehiculos=(
        _vehiculo("C2", "Sencillo C2", 650, 240, 240, 8500,
                  [(FURGON, 8500), (PLATAFORMA, 8500)]),
        _vehiculo("3S3", "Tractomula 3S3", 1220, 240, 260, 34000,
                  [(FURGON, 30000), (PLATAFORMA, 34000)]),
    ),
    carrocerias=(FURGON, PLATAFORMA),
    mercancias=(BEBIDAS, BOBINAS),
    reglas=(),
    parametros=ParametrosMotor({"k_max": "10"}),
)


class _CatalogoRepo:
    async def cargar(self) -> Catalogo:
        return CATALOGO


class _ParametrosRepo:
    async def obtener(self) -> ParametrosCubicacion:
        return ParametrosCubicacion()

    async def guardar(self, p):
        return p


class _VehiculosDelMotor3D:
    """Catalogo del motor 3D. Por defecto trae los presets reales."""

    def __init__(self, nombres: list[str] | None = None) -> None:
        from application.plan_cargue.vehiculos import presets

        todos = presets()
        self._lista = ([v for v in todos if v.nombre in nombres] if nombres is not None
                       else todos)
        for i, v in enumerate(self._lista):
            if not getattr(v, "id", None):
                object.__setattr__(v, "id", f"preset-{i}") if hasattr(v, "__dataclass_fields__") \
                    else setattr(v, "id", f"preset-{i}")

    async def listar_con_siembra(self):
        return self._lista


def _linea(*posiciones) -> object:
    class _Puerto:
        async def obtener(self, linea_solicitud_id: str):
            return LineaParaCubicacion(
                linea_id=linea_solicitud_id, posiciones=list(posiciones),
                origen="Origen", destino="Destino")

    return _Puerto()


def _posicion(codigo, cantidad=1, largo=60, ancho=40, alto=40, peso=25) -> PosicionCarga:
    return PosicionCarga(descripcion="Pallet", cantidad=cantidad, largo_cm=largo,
                         ancho_cm=ancho, alto_cm=alto, peso_kg=peso,
                         codigo_mercancia=codigo)


async def _correr(lineas, vehiculos=None):
    return await crear_plan_desde_linea(
        planes=None, vehiculos=vehiculos or _VehiculosDelMotor3D(), lineas=lineas,
        catalogo_repo=_CatalogoRepo(), parametros_repo=_ParametrosRepo(),
        linea_solicitud_id="11111111-1111-1111-1111-111111111111")


# --- el fallback silencioso, muerto ----------------------------------------


async def test_una_posicion_sin_tipo_de_mercancia_es_rechazo_explicito():
    """Antes se asumia un furgon de 13,6 m. El tipo arrastra la carroceria:
    asumirlo elige el vehiculo equivocado sin que nada avise."""
    sin_codigo = PosicionCarga(descripcion="Pallet", cantidad=1, largo_cm=60, ancho_cm=40,
                               alto_cm=40, peso_kg=25, codigo_mercancia=None)
    with pytest.raises(ValidationFailedException) as error:
        await _correr(_linea(sin_codigo))
    assert error.value.codigo == "MERCANCIA_NO_DECLARADA"


async def test_una_posicion_sin_peso_es_rechazo_explicito():
    """Antes el puente le ponia 1 kg (#304).

    El peso decide la capacidad del vehiculo: un kilo inventado mete en un
    Turbo lo que necesitaba una Minimula, y el plan sale creible pero
    equivocado. `PesoKg` es opcional en la captura de NEXO, asi que se alcanza.
    """
    sin_peso = PosicionCarga(descripcion="Pallet", cantidad=10, largo_cm=60, ancho_cm=40,
                             alto_cm=40, peso_kg=None, codigo_mercancia="BEBIDAS")
    with pytest.raises(ValidationFailedException) as error:
        await _correr(_linea(sin_peso))
    assert error.value.codigo == "PESO_NO_DECLARADO"


async def test_una_posicion_con_peso_cero_tambien_se_rechaza():
    """Cero no es un peso declarado: es el mismo agujero por otra puerta."""
    peso_cero = PosicionCarga(descripcion="Pallet", cantidad=10, largo_cm=60, ancho_cm=40,
                              alto_cm=40, peso_kg=0, codigo_mercancia="BEBIDAS")
    with pytest.raises(ValidationFailedException) as error:
        await _correr(_linea(peso_cero))
    assert error.value.codigo == "PESO_NO_DECLARADO"


async def test_un_tipo_que_no_esta_en_el_catalogo_es_rechazo_explicito():
    with pytest.raises(ValidationFailedException) as error:
        await _correr(_linea(_posicion("NO_EXISTE")))
    assert error.value.codigo == "MERCANCIA_DESCONOCIDA"


async def test_un_preset_borrado_del_catalogo_3d_es_rechazo_de_negocio():
    """Nunca un `StopIteration`: el worker lo trataria como fallo inesperado y
    gastaria tres reintentos identicos antes de la DLQ."""
    with pytest.raises(ValidationFailedException) as error:
        await _correr(_linea(_posicion("BEBIDAS")), vehiculos=_VehiculosDelMotor3D([]))
    assert error.value.codigo == "PRESET_VEHICULO_NO_ENCONTRADO"


async def test_una_carga_que_el_motor_no_puede_asignar_es_rechazo_con_motivo():
    enorme = _posicion("BEBIDAS", cantidad=1, largo=1400, ancho=40, alto=40, peso=25)
    with pytest.raises(ValidationFailedException) as error:
        await _correr(_linea(enorme))
    assert error.value.codigo == "SIN_FLOTA_RECOMENDADA"
    assert "EXTRADIMENSIONADO" in error.value.mensaje


async def test_una_linea_sin_posiciones_se_rechaza():
    with pytest.raises(ValidationFailedException) as error:
        await _correr(_linea())
    assert error.value.codigo == "SIN_POSICIONES"


async def test_una_linea_que_no_existe_es_404():
    class _Vacio:
        async def obtener(self, linea_solicitud_id: str):
            return None

    with pytest.raises(NotFoundException):
        await _correr(_Vacio())


# --- el diccionario de compatibilidad ---------------------------------------


def test_cubre_las_cinco_configuraciones_canonicas():
    assert set(puente._PRESET_POR_CODIGO) == {"2L2", "C2", "C3", "2S2", "3S3"}


def test_los_presets_del_diccionario_existen_en_el_catalogo_3d():
    from application.plan_cargue.vehiculos import presets

    nombres = {v.nombre for v in presets()}
    declarados = set(puente._PRESET_POR_CODIGO.values()) | set(
        puente._PRESET_POR_COMBINACION.values())
    assert declarados <= nombres, f"presets que no existen: {declarados - nombres}"


def test_el_3s3_cae_en_un_preset_distinto_segun_la_carroceria():
    """No es cosmetico: con furgon carga 30.000 kg porque el furgon pesa, y con
    plataforma los 34.000 del chasis. Mandarlo siempre al mismo preset
    regalaria o quitaria cuatro toneladas."""
    assert puente._preset_de("3S3", "FURGON") == "Tractocamion furgon 13.6 m"
    assert puente._preset_de("3S3", "PLATAFORMA") == "Plataforma (tractomula)"
    assert puente._preset_de("3S3", "ESTACAS") == "Plataforma (tractomula)"


def test_las_capacidades_del_diccionario_coinciden_con_el_catalogo_3d():
    """Si un preset dijera otra capacidad que el catalogo del motor de
    seleccion, el plan 3D aceptaria o rechazaria carga que el otro motor ya
    decidio."""
    from application.plan_cargue.vehiculos import presets

    por_nombre = {v.nombre: v for v in presets()}
    esperado = {
        ("2L2", "ESTACAS"): 4000, ("C2", "FURGON"): 8500,
        ("C3", "ESTACAS"): 17000, ("2S2", "FURGON"): 18000,
        ("3S3", "FURGON"): 30000, ("3S3", "PLATAFORMA"): 34000,
    }
    for (codigo, carroceria), capacidad in esperado.items():
        preset = por_nombre[puente._preset_de(codigo, carroceria)]
        assert preset.capacidad_kg == capacidad, f"{codigo}+{carroceria}"


def test_un_vehiculo_sin_preset_equivalente_es_rechazo():
    assert puente._preset_de("XX", "FURGON") is None
