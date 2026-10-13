"""Entidades del catalogo que administra el equipo de Datos (S5-HU-17.2).

Dataclasses puras, sin ORM: `infrastructure/persistence/modelos_catalogo.py`
las traduce desde SQLAlchemy. El motor de seleccion de flota (S5-HU-17.3)
trabaja contra estas, nunca contra el ORM — asi se puede escribir y probar el
algoritmo con objetos en memoria, sin base de datos de por medio.

Todas las medidas en **centimetros** y los pesos en **kilos**, como el catalogo.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from decimal import Decimal


@dataclass(frozen=True)
class Carroceria:
    """Una de las 17 carrocerias del catalogo."""

    id: str
    codigo: str
    nombre: str
    activa: bool = True


@dataclass(frozen=True)
class CarroceriaDeVehiculo:
    """Una combinacion vehiculo-carroceria, con la capacidad que le aplica.

    La capacidad puede ser la del chasis o una propia: el unico caso sembrado
    con capacidad propia es `3S3 + FURGON = 30.000 kg`, menor que los 34.000 del
    chasis porque el furgon pesa.
    """

    carroceria: Carroceria
    capacidad_kg: Decimal


@dataclass(frozen=True)
class Vehiculo:
    """Una de las 5 configuraciones: 2L2, C2, C3, 2S2, 3S3.

    `alto_util_cm` es el alto aprovechable del vehiculo. **No hay alto por
    combinacion**: las 6 carrocerias del 3S3 comparten 260 cm. Para un furgon
    eso es correcto; para una plataforma o estacas subestima lo que cabe, porque
    ahi la carga puede subir hasta el limite legal menos la altura del piso. Es
    una limitacion del modelo de datos, no de este codigo.
    """

    id: str
    codigo: str
    nombre: str
    largo_cm: Decimal
    ancho_cm: Decimal
    alto_util_cm: Decimal
    capacidad_kg: Decimal
    activo: bool = True
    carrocerias: tuple[CarroceriaDeVehiculo, ...] = ()

    @property
    def volumen_m3(self) -> Decimal:
        """Volumen interior aprovechable, en metros cubicos."""
        return (self.largo_cm * self.ancho_cm * self.alto_util_cm) / Decimal(1_000_000)

    def admite(self, codigo_carroceria: str) -> bool:
        return any(c.carroceria.codigo == codigo_carroceria for c in self.carrocerias)

    def capacidad_con(self, codigo_carroceria: str) -> Decimal | None:
        """La capacidad real de esta combinacion, o None si no la admite."""
        for c in self.carrocerias:
            if c.carroceria.codigo == codigo_carroceria:
                return c.capacidad_kg
        return None


@dataclass(frozen=True)
class CategoriaMercancia:
    """Una de las 13 categorias. La clave de negocio es el nombre.

    `CARBURANTE` y `MATERIAL_INFLAMABLE` son dos categorias distintas: papel,
    carton y madera van bajo la segunda.
    """

    id: str
    nombre: str


@dataclass(frozen=True)
class TipoMercancia:
    """Uno de los 31 tipos de la matriz de Operaciones.

    `cubicable` esta en True en los 31: los tipos excluidos (graneles, cemento,
    combustibles) no se sembraron, por decision del equipo de Datos. Para el
    motor eso significa que un codigo desconocido y uno no cubicable son el
    mismo caso.

    `condicionada` es una **marca**, no un flujo de aprobacion: la recomendacion
    sale normal y queda señalada para que la Mesa de Costeo la revise.
    """

    id: str
    codigo: str
    nombre: str
    categoria: CategoriaMercancia
    cubicable: bool
    condicionada: bool
    carroceria_principal: Carroceria | None
    carroceria_secundaria: Carroceria | None
    secundaria_condicionada: bool
    # NINGUNO | REFRIGERADO | VENTILADO_CONDICIONAL | CLIMATIZADO_CONDICIONAL |
    # REFRIGERADO_O_CLIMATIZADO | VENTILADO_O_REFRIGERADO
    control_ambiental: str
    apilable: bool
    fragil: bool
    rotacion_libre: bool
    comentario_condicion: str | None = None
    activo: bool = True

    @property
    def requiere_control_ambiental(self) -> bool:
        return self.control_ambiental != "NINGUNO"


@dataclass(frozen=True)
class ReglaIncompatibilidad:
    """Dos categorias que no pueden viajar juntas.

    El par se guarda ordenado como lo haya cargado el CRUD, asi que al
    comparar hay que mirar las dos direcciones: `aplica_a` lo hace.
    """

    id: str
    categoria_1_id: str
    categoria_2_id: str
    motivo: str

    def aplica_a(self, categoria_a_id: str, categoria_b_id: str) -> bool:
        par = {self.categoria_1_id, self.categoria_2_id}
        return par == {categoria_a_id, categoria_b_id}


@dataclass
class ParametrosMotor:
    """Los parametros del motor, por clave.

    Se exponen como diccionario y no como campos fijos porque la tabla es un
    catalogo editable por CRUD: agregar un parametro no deberia obligar a tocar
    esta clase.
    """

    valores: dict[str, str] = field(default_factory=dict)

    def entero(self, clave: str, default: int) -> int:
        try:
            return int(self.valores[clave])
        except (KeyError, ValueError):
            return default

    def decimal(self, clave: str, default: Decimal) -> Decimal:
        try:
            return Decimal(self.valores[clave])
        except (KeyError, ArithmeticError, ValueError):
            return default

    @property
    def k_max(self) -> int:
        """Tope de vehiculos que el motor evalua para una carga."""
        return self.entero("k_max", 10)


@dataclass(frozen=True)
class Catalogo:
    """Todo lo que el motor necesita para una corrida, leido de una vez.

    Se pasa completo en vez de consultar tabla por tabla: son menos de cien
    filas en total y el motor las recorre varias veces al evaluar candidatos.
    """

    vehiculos: tuple[Vehiculo, ...]
    carrocerias: tuple[Carroceria, ...]
    mercancias: tuple[TipoMercancia, ...]
    reglas: tuple[ReglaIncompatibilidad, ...]
    parametros: ParametrosMotor

    def vehiculo_por_codigo(self, codigo: str) -> Vehiculo | None:
        return next((v for v in self.vehiculos if v.codigo == codigo), None)

    def mercancia_por_codigo(self, codigo: str) -> TipoMercancia | None:
        return next((m for m in self.mercancias if m.codigo == codigo), None)

    def mercancia_por_id(self, id_mercancia: str) -> TipoMercancia | None:
        return next((m for m in self.mercancias if m.id == id_mercancia), None)

    def son_incompatibles(self, a: TipoMercancia, b: TipoMercancia) -> str | None:
        """El motivo si no pueden viajar juntas, o None si pueden."""
        for regla in self.reglas:
            if regla.aplica_a(a.categoria.id, b.categoria.id):
                return regla.motivo
        return None
