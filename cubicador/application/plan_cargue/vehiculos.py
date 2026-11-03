"""Casos de uso de vehiculos/contenedores del Cubicador 3D."""

from __future__ import annotations

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
    if v.puerta_ancho_mm <= 0 or v.puerta_alto_mm <= 0:
        raise ValidationFailedException("PUERTA_INVALIDA", "Las dimensiones de la puerta deben ser positivas.")
    if v.puerta_ancho_mm > v.ancho_interno_mm or v.puerta_alto_mm > v.alto_interno_mm:
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

    # Ejes: posiciones dentro del largo y capacidades positivas.
    if any(e.posicion_mm < 0 or e.posicion_mm > v.largo_interno_mm or e.capacidad_kg <= 0 for e in v.ejes):
        raise ValidationFailedException(
            "EJES_INVALIDOS", "Cada eje debe tener una posicion dentro del largo del vehiculo y una capacidad positiva.",
        )


async def listar_vehiculos(repo: IVehiculoCubicacionRepository) -> list[VehiculoCubicacion]:
    return await repo.listar_con_siembra()


async def republicar_catalogo(repo: IVehiculoCubicacionRepository) -> list[VehiculoCubicacion]:
    """Reparo manual, una sola vez, de la desincronizacion ya existente
    entre el catalogo de Cubicador y la proyeccion de NEXO."""
    return await repo.republicar_todos()


async def guardar_vehiculo(repo: IVehiculoCubicacionRepository, vehiculo: VehiculoCubicacion) -> VehiculoCubicacion:
    _validar(vehiculo)
    return await repo.guardar(vehiculo)


async def eliminar_vehiculo(repo: IVehiculoCubicacionRepository, id: str) -> None:
    if await repo.tiene_planes_asociados(id):
        raise ConflictException("VEHICULO_EN_USO", "El vehiculo tiene planes de cargue asociados.")
    await repo.eliminar(id)


# Presets iniciales (filas editables, no constantes del codigo en la base de
# datos - esta lista solo se usa para SEMBRARLOS la primera vez).
def presets() -> list[VehiculoCubicacion]:
    def preset(
        nombre: str, l: float, w: float, h: float, cap: float, tara: float,
        puerta_w: float, puerta_h: float, ejes: list[EjeVehiculoRow],
    ) -> VehiculoCubicacion:
        return VehiculoCubicacion(
            nombre=nombre, descripcion="Preset del sistema (editable)", es_preset=True,
            largo_interno_mm=l, ancho_interno_mm=w, alto_interno_mm=h,
            capacidad_kg=cap, tara_kg=tara,
            puerta_ancho_mm=puerta_w, puerta_alto_mm=puerta_h, puerta_posicion="trasera",
            ejes=ejes,
        )

    # Medidas y capacidades CORREGIDAS contra el Excel "Dimenciones y Peso por
    # configuracion para Cubicaje" (fuente: Operaciones). Antes eran valores
    # inventados: el Turbo prometia 6,2 m y 6.300 kg cuando el real es 4,5 m y
    # 4.000 kg. La capacidad del Excel es PAYLOAD, no peso bruto, asi que entra
    # directo en capacidad_kg (ya no se resta la tara).
    #
    # Tres campos NO tienen fuente en el Excel y se dejan como estaban: tara (no
    # se usa en ningun calculo del motor), puerta y ejes. La puerta solo se
    # ajusto donde la vieja excedia la seccion interna nueva, porque `_validar`
    # la rechazaria.
    #
    # Los nombres se conservan a proposito: `listar_con_siembra` siembra POR
    # NOMBRE, asi que renombrar crearia filas duplicadas en las bases que ya
    # existen. El de "Tractocamion furgon 13.6 m" quedo desfasado (son 12,2 m);
    # el renombre va por el CRUD, no por aca.
    return [
        preset("Turbo con estacas", 4500, 2200, 2300, 4000, 4200, 2200, 1950,
               # ejes reubicados: el eje trasero estaba en 4600 mm, fuera del largo nuevo
               [EjeVehiculoRow(750, 3500), EjeVehiculoRow(3350, 7000)]),
        preset("Plataforma (tractomula)", 12200, 2400, 2600, 34000, 6500, 2400, 2600,
               [EjeVehiculoRow(1500, 6000), EjeVehiculoRow(8500, 10500), EjeVehiculoRow(10000, 10500)]),
        preset("Cama baja (lowboy)", 12500, 2600, 1600, 39000, 9000, 2600, 1600,
               # Sin contraparte en el Excel: queda como estaba, pendiente de negocio.
               [EjeVehiculoRow(1500, 6000), EjeVehiculoRow(8500, 10500), EjeVehiculoRow(9900, 10500), EjeVehiculoRow(11300, 10500)]),
        preset("Camion sencillo (estacas)", 6500, 2400, 2400, 8500, 9000, 2350, 2150,
               [EjeVehiculoRow(1200, 6000), EjeVehiculoRow(5800, 11000)]),
        preset("Camion rigido dobletroque", 8000, 2400, 2600, 17000, 10500, 2400, 2250,
               [EjeVehiculoRow(1300, 6000), EjeVehiculoRow(6300, 22000)]),
        preset("Minimula 2S2", 12200, 2400, 2600, 18000, 0, 2400, 2600,
               # Configuracion que faltaba por completo en el catalogo. El Excel no
               # trae tara ni ejes: la tara va en 0 (no se usa) y la lista de ejes
               # vacia, que el motor soporta (no estima peso por eje).
               []),
        preset("Tractocamion furgon 13.6 m", 12200, 2400, 2600, 30000, 7000, 2400, 2600,
               [EjeVehiculoRow(1500, 8000), EjeVehiculoRow(11000, 22000)]),
        preset("Contenedor 20 pies", 5900, 2350, 2390, 21800, 2200, 2340, 2280,
               [EjeVehiculoRow(1000, 10900), EjeVehiculoRow(4900, 10900)]),
        preset("Contenedor 40 pies", 12030, 2350, 2390, 26680, 3800, 2340, 2280,
               [EjeVehiculoRow(1500, 13340), EjeVehiculoRow(10500, 13340)]),
        preset("Contenedor 40 pies High Cube", 12030, 2350, 2690, 26460, 3900, 2340, 2580,
               [EjeVehiculoRow(1500, 13340), EjeVehiculoRow(10500, 13340)]),
    ]
