"""Casos de uso de vehiculos/contenedores del Cubicador 3D."""

from __future__ import annotations

import re
import unicodedata

from application.excepciones import ConflictException, ValidationFailedException
from application.ports.repositorios import IVehiculoCubicacionRepository
from domain.entidades.entidades import EjeVehiculoRow, VehiculoCubicacion

_PUERTA_POSICIONES_VALIDAS = {"trasera", "frontal", "costado_izquierdo", "costado_derecho"}


def _validar(v: VehiculoCubicacion) -> None:
    if v.largo_interno_mm <= 0 or v.ancho_interno_mm <= 0 or v.alto_interno_mm <= 0:
        raise ValidationFailedException("DIMENSIONES_INVALIDAS", "Las dimensiones internas deben ser positivas.")
    if v.largo_interno_mm > 30000 or v.ancho_interno_mm > 5000 or v.alto_interno_mm > 5000:
        raise ValidationFailedException("DIMENSIONES_INVALIDAS", "Dimensiones fuera de un rango razonable.")
    if v.capacidad_kg <= 0 or v.capacidad_kg > 100_000:
        raise ValidationFailedException("CAPACIDAD_INVALIDA", "La capacidad debe estar entre 1 y 100.000 kg.")

    # Puerta: debe caber por las paredes y ser positiva (una puerta 0x0
    # rechazaria todo sin explicar el porque real).
    #
    # Desde S4-HU-12.1 la puerta puede ser DESCONOCIDA (None) y eso no es un
    # error de validacion: el contenedor refrigerado de 40 solo existia en el
    # catalogo del motor rapido, que no guarda puerta. Un None se deja pasar y
    # quien necesite el dato -el motor 3D- decide que hacer sin el; lo que no se
    # acepta es una puerta presente pero absurda. Ojo: None y 0 NO son lo mismo,
    # 0 sigue siendo invalido.
    if v.puerta_ancho_mm is not None and v.puerta_ancho_mm <= 0:
        raise ValidationFailedException("PUERTA_INVALIDA", "Las dimensiones de la puerta deben ser positivas.")
    if v.puerta_alto_mm is not None and v.puerta_alto_mm <= 0:
        raise ValidationFailedException("PUERTA_INVALIDA", "Las dimensiones de la puerta deben ser positivas.")
    if v.puerta_ancho_mm is not None and v.puerta_ancho_mm > v.ancho_interno_mm:
        raise ValidationFailedException("PUERTA_INVALIDA", "La puerta no puede ser mayor que la seccion interna.")
    if v.puerta_alto_mm is not None and v.puerta_alto_mm > v.alto_interno_mm:
        raise ValidationFailedException("PUERTA_INVALIDA", "La puerta no puede ser mayor que la seccion interna.")
    if v.puerta_posicion not in _PUERTA_POSICIONES_VALIDAS:
        raise ValidationFailedException("PUERTA_POSICION_INVALIDA", f"Posicion de puerta invalida: '{v.puerta_posicion}'.")

    # Tolerancias: no negativas y menores a la mitad de la menor dimension
    # interna (una tolerancia excesiva volveria infactible cualquier colocacion).
    max_tol = min(v.largo_interno_mm, v.ancho_interno_mm, v.alto_interno_mm) / 2
    if v.tolerancia_paredes_mm < 0 or v.tolerancia_entre_unidades_mm < 0:
        raise ValidationFailedException("TOLERANCIA_INVALIDA", "Las tolerancias no pueden ser negativas.")
    if v.tolerancia_paredes_mm >= max_tol or v.tolerancia_entre_unidades_mm >= max_tol:
        raise ValidationFailedException("TOLERANCIA_INVALIDA", "Las tolerancias son demasiado grandes para el vehiculo.")

    # Ejes: capacidad siempre positiva; la posicion, cuando se conoce, dentro del
    # largo. Misma razon que la puerta: el reefer trae capacidades por eje pero
    # no sus posiciones, y eso es un dato faltante, no un dato invalido.
    if any(e.capacidad_kg <= 0 for e in v.ejes):
        raise ValidationFailedException(
            "EJES_INVALIDOS", "Cada eje debe tener una capacidad positiva.",
        )
    if any(e.posicion_mm is not None and not (0 <= e.posicion_mm <= v.largo_interno_mm) for e in v.ejes):
        raise ValidationFailedException(
            "EJES_INVALIDOS", "La posicion de cada eje debe estar dentro del largo del vehiculo.",
        )


def apto_para_plan_3d(v: VehiculoCubicacion) -> bool:
    """El motor 3D necesita la puerta -para decidir si la pieza entra- y la
    POSICION de cada eje -para validar distribucion de peso-. Un vehiculo al
    que le falten se puede seguir evaluando en la cubicacion rapida, pero no se
    le puede armar un plan de cargue.

    Hoy solo lo incumple el contenedor refrigerado de 40, que venia del catalogo
    del motor rapido y nunca tuvo esos datos medidos (S4-HU-12.1). Sin este
    filtro, el motor reventaba con un TypeError al comparar contra None: un
    fallo INESPERADO, que el worker reintenta tres veces y manda a la DLQ, en
    vez de un rechazo de negocio con causa clara.
    """
    return (
        v.puerta_ancho_mm is not None
        and v.puerta_alto_mm is not None
        and all(e.posicion_mm is not None for e in v.ejes)
    )


async def listar_vehiculos(repo: IVehiculoCubicacionRepository) -> list[VehiculoCubicacion]:
    return await repo.listar_con_siembra()


async def republicar_catalogo(repo: IVehiculoCubicacionRepository) -> list[VehiculoCubicacion]:
    """Reparo manual, una sola vez, de la desincronizacion ya existente
    entre el catalogo de Cubicador y la proyeccion de NEXO."""
    return await repo.republicar_todos()


_PALABRAS_VACIAS = {"con", "de", "del", "la", "el", "pies"}


def slug(nombre: str) -> str:
    """Genera un `codigo` a partir del nombre, con la misma forma que los
    sembrados: minusculas, sin tildes, sin lo que va entre parentesis, sin
    palabras vacias y con guion bajo por espacio.

    La regla reproduce 7 de los 10 codigos sembrados; los otros tres son
    historicos y se conservan tal cual. Por eso `codigo` es un identificador
    OPACO y no un slug vivo: se genera UNA vez, al crear el vehiculo, y no se
    vuelve a derivar del nombre. Si se recalculara, renombrar un vehiculo le
    cambiaria la identidad, que es justo el problema que esta columna resuelve.
    """
    sin_parentesis = re.sub(r"\(.*?\)", "", nombre)
    sin_tildes = unicodedata.normalize("NFD", sin_parentesis).encode("ascii", "ignore").decode()
    partes = [p for p in re.findall(r"[a-z0-9]+", sin_tildes.lower()) if p not in _PALABRAS_VACIAS]
    return "_".join(partes)


async def _codigo_disponible(repo: IVehiculoCubicacionRepository, nombre: str) -> str:
    base = slug(nombre) or "vehiculo"
    candidato = base
    sufijo = 2
    while await repo.buscar_por_codigo(candidato) is not None:
        candidato = f"{base}_{sufijo}"
        sufijo += 1
    return candidato


async def guardar_vehiculo(repo: IVehiculoCubicacionRepository, vehiculo: VehiculoCubicacion) -> VehiculoCubicacion:
    _validar(vehiculo)
    # Un vehiculo creado desde la interfaz llega sin codigo: se le genera aqui.
    # Sin esto nacia con `codigo` nulo y el motor rapido lo descartaba para
    # siempre, sin que nada lo dijera.
    if vehiculo.codigo is None:
        # En una EDICION la entidad se reconstruye desde el input, que no trae
        # codigo (es identidad de este servicio, no del que llama). Sin mirar la
        # fila existente se le generaria uno nuevo desde el nombre y se pisaria
        # el que ya tenia: renombrar un vehiculo le cambiaria la identidad, que
        # es exactamente lo que esta columna existe para evitar.
        existente = await repo.obtener(vehiculo.id) if vehiculo.id else None
        vehiculo.codigo = (
            existente.codigo if existente is not None and existente.codigo
            else await _codigo_disponible(repo, vehiculo.nombre)
        )
    return await repo.guardar(vehiculo)


async def eliminar_vehiculo(repo: IVehiculoCubicacionRepository, id: str) -> None:
    if await repo.tiene_planes_asociados(id):
        raise ConflictException("VEHICULO_EN_USO", "El vehiculo tiene planes de cargue asociados.")
    await repo.eliminar(id)


# Siembra inicial del CATALOGO UNICO de vehiculos (S4-HU-12.1).
#
# Antes de 12.1 habia DOS fuentes describiendo los mismos vehiculos con campos
# distintos: esta lista (los fisicos - dimensiones, capacidad, tara, puerta,
# ejes) y la constante CATALOGO de domain/motor/cubicacion_rapida.py (la
# clasificacion SICE/SAP y los campos de evaluacion). Ninguna alcanzaba sola.
# Ahora esta es la unica, y el motor rapido lee de la tabla que esta siembra.
#
# Las filas siguen siendo EDITABLES: esto solo las crea la primera vez.
#
# Las cuatro reglas acordadas con negocio (2026-09-28) para resolver las
# divergencias, aplicadas sobre las dos fuentes del repo mas la extraccion de
# tblSAPTiposVehiculoTarifario:
#
#   1. SAP declara capacidad y las dos fuentes del repo discrepan -> manda SAP.
#   2. SAP no declara capacidad -> el menor de los dos. Subestimar cuesta un
#      camion de mas; sobrestimar es sobrepeso, que es sancion.
#   3. SAP declara pero las dos fuentes del repo coinciden y son consistentes
#      entre si -> se mantiene el repo y la discrepancia queda documentada sin
#      resolver. Cambiarlo exigiria decidir cual de los tres numeros esta mal.
#   4. Fuente unica -> esa, y lo que la otra habria aportado queda en None.
#
# El peso bruto NO se guarda: se deriva de capacidad_kg + tara_kg. Guardar los
# tres permitiria que se contradigan entre si, que es exactamente la divergencia
# que esta issue corrige.
def presets() -> list[VehiculoCubicacion]:
    def preset(
        codigo: str, nombre: str, largo: float, ancho: float, alto: float,
        cap: float, tara: float, puerta_w: float | None, puerta_h: float | None,
        ejes: list[EjeVehiculoRow], config_vehiculo: str | None, carroceria: str | None,
        clase_viaje: str | None, clase_contenedor: str | None,
        geometria: str, control_ambiental: str,
    ) -> VehiculoCubicacion:
        return VehiculoCubicacion(
            nombre=nombre, descripcion="Preset del sistema (editable)", es_preset=True,
            largo_interno_mm=largo, ancho_interno_mm=ancho, alto_interno_mm=alto,
            capacidad_kg=cap, tara_kg=tara,
            puerta_ancho_mm=puerta_w, puerta_alto_mm=puerta_h, puerta_posicion="trasera",
            ejes=ejes, codigo=codigo,
            config_vehiculo=config_vehiculo, carroceria=carroceria,
            clase_viaje=clase_viaje, clase_contenedor=clase_contenedor,
            geometria=geometria, control_ambiental=control_ambiental,
        )

    E = EjeVehiculoRow
    return [
        # Regla 3: ambas fuentes del repo dan 6300 (PBV 10500 - tara 4200) y son
        # consistentes entre si. SAP, en 2L3, dice "Liviano (capacidad 4 ton)" =
        # 4000. Las dos no pueden ser ciertas; resolverlo exigiria decidir cual de
        # los tres numeros esta mal, asi que se mantiene el repo y la discrepancia
        # queda anotada para revisar contra el inventario de flota cuando exista.
        # Nota aparte: 2L3 figura INACTIVO en SAP (Estado=0).
        preset("turbo_estacas", "Turbo con estacas", 6200, 2200, 2000, 6300, 4200, 2200, 1950,
               [E(1000, 3500), E(4600, 7000)],
               "2L3", "ESTACAS", "Carga Suelta", None, "semicerrada", "ninguno"),

        # Regla 1: las dos fuentes del repo discrepaban en la TARA, no en la
        # capacidad (mismo PBV 17500, tara 9000 contra 7500). SAP desempata: el
        # codigo 2 dice "Liviano mas 10.5 ton (capacidad 8.5 ton)", que confirma
        # la capacidad de 8500 y por lo tanto la tara de 9000.
        preset("sencillo_estacas", "Camion sencillo (estacas)", 7200, 2350, 2200, 8500, 9000, 2350, 2150,
               [E(1200, 6000), E(5800, 11000)],
               "2", "ESTACAS", "Carga Suelta", None, "semicerrada", "ninguno"),

        # Regla 2: SAP no declara capacidad. Se toma el menor: 17000 del repo
        # contra 17500 del motor.
        # CODIGO CORREGIDO: el repo usaba "3", que segun la extraccion NO EXISTE
        # en tblSAPTiposVehiculoTarifario. Un TipoVehiculo que no matchea ningun
        # CODIGO hace que IdVehiculoSapAsync devuelva null y el costeo deje de
        # filtrar por vehiculo EN SILENCIO, sin error ni log. SAP tiene C3,
        # activo, con descripcion literal "Dobletroque".
        preset("dobletroque_estacas", "Camion rigido dobletroque", 7800, 2400, 2300, 17000, 10500, 2400, 2250,
               [E(1300, 6000), E(6300, 22000)],
               "C3", "ESTACAS", "Carga Suelta", None, "semicerrada", "ninguno"),

        # Regla 2: SAP no declara capacidad para 3S3 ("Tractomula"). Menor de los
        # dos: 27000 (34000 - 7000, del motor) contra 28000 del repo.
        # Las dos fuentes ademas describian este vehiculo distinto: largo 13600
        # contra 12200, 2 ejes contra 3, PBV 35000 contra 34000. Puede ser el
        # mismo vehiculo modelado de dos maneras -ejes agrupados frente a
        # individuales- o dos vehiculos distintos. Se aplica la regla estructural:
        # LO FISICO LO APORTA ESTA LISTA, que es la unica con posiciones de eje y
        # datos de puerta, y la que el motor 3D usa hoy en produccion. Si
        # resultan ser dos vehiculos se parte despues, pero eso ya seria dato
        # nuevo, no una divergencia resoluble con lo que hay.
        preset("furgon_semirremolque", "Tractocamion furgon 13.6 m", 13600, 2450, 2700, 27000, 7000, 2400, 2650,
               [E(1500, 8000), E(11000, 22000)],
               "3S3", "FURGON", "Carga Suelta", None, "cerrada", "ninguno"),

        # Ambas fuentes coinciden en todo.
        preset("plataforma_sencilla", "Plataforma (tractomula)", 12200, 2500, 2800, 27500, 6500, 2500, 2800,
               [E(1500, 6000), E(8500, 10500), E(10000, 10500)],
               "3S3", "PLATAFORMA", "Carga Suelta", None, "abierta", "ninguno"),

        # Ambas fuentes coinciden en todo.
        preset("cama_baja", "Cama baja (lowboy)", 12500, 2600, 1600, 39000, 9000, 2600, 1600,
               [E(1500, 6000), E(8500, 10500), E(9900, 10500), E(11300, 10500)],
               "3S3", "CAMABAJA", "Extradimensionado", None, "abierta", "ninguno"),

        # Ambas fuentes coinciden en todo.
        preset("contenedor_seco_20", "Contenedor 20 pies", 5900, 2350, 2390, 21800, 2200, 2340, 2280,
               [E(1000, 10900), E(4900, 10900)],
               "2S2", "PORTACONTENEDORES", "Contenedor", "CNT20.ST", "cerrada", "ninguno"),

        # Ambas fuentes coinciden en todo. Ojo: el motor lo tenia con
        # clase_contenedor "CNT40.HC" -codigo de High Cube- pese a llamarse seco.
        # Se conserva tal cual: cambiarlo seria suponer cual de las dos etiquetas
        # esta equivocada.
        preset("contenedor_seco_40", "Contenedor 40 pies", 12030, 2350, 2390, 26680, 3800, 2340, 2280,
               [E(1500, 13340), E(10500, 13340)],
               "3S3", "PORTACONTENEDORES", "Contenedor", "CNT40.HC", "cerrada", "ninguno"),

        # Regla 4, fuente unica: solo existia en esta lista. Hasta 12.1 el mapeo
        # del motor lo emparejaba con el REEFER, que es otro vehiculo (el de
        # abajo). clase_contenedor queda None porque ese campo solo existia en el
        # motor y esta fila no tenia contraparte alli: no se hereda la del seco
        # de 40.
        preset("contenedor_40_high_cube", "Contenedor 40 pies High Cube", 12030, 2350, 2690, 26460, 3900, 2340, 2580,
               [E(1500, 13340), E(10500, 13340)],
               "3S3", "PORTACONTENEDORES", "Contenedor", None, "cerrada", "ninguno"),

        # Regla 4, fuente unica: solo existia en el motor (29500 - 4800 = 24700).
        # El motor no guarda puerta ni POSICIONES de eje, solo capacidades: por
        # eso ambas quedan en None. No se copian las del contenedor de 40 seco,
        # serian dato inventado. Consecuencia a verificar: sin posiciones de eje
        # no se puede validar distribucion de peso, asi que este vehiculo puede no
        # ser utilizable en el plan 3D hasta que alguien lo mida.
        preset("contenedor_refrigerado_40", "Contenedor refrigerado 40", 11560, 2280, 2250, 24700, 4800, None, None,
               [E(None, 12350), E(None, 12350)],
               "3S3", "PORTACONTENEDORES", "Contenedor", "CNT40.RE", "cerrada", "refrigeracion"),
    ]
