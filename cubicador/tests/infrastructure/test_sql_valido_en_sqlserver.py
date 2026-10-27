"""Guarda: el SQL que generan los repositorios tiene que ser valido en SQL Server (#344).

**Por que hace falta una prueba aparte.** Toda la suite corre contra SQLite en
memoria, y SQLite es mas permisivo que SQL Server. `columna IS 0` es sintaxis
valida en SQLite y en PostgreSQL, y SQL Server la rechaza con el error 102
("Incorrect syntax near '0'"). O sea: hay una familia entera de errores que
**ninguna prueba del repositorio puede detectar**, porque el motor que valida
la sintaxis no es el de produccion.

Eso no es teorico. El plan de cargue 3D desde una linea llevaba meses fallando
en QA con ERROR_INTERNO por ese motivo, con las 275 pruebas en verde.

Estas pruebas no necesitan base de datos: compilan las consultas reales contra
el dialecto de SQL Server y revisan la forma del SQL. Corren en milisegundos.
"""

from __future__ import annotations

import ast
import pathlib
import re

import pytest
from sqlalchemy.dialects import mssql

from infrastructure.persistence.repositorios.plan_cargue_repo import SqlPlanCargueRepository

# `IS <literal>` solo es valido en SQL Server con NULL: `IS NULL` e `IS NOT
# NULL`. Con 0, 1, true o false es el error 102.
_IS_CON_LITERAL = re.compile(r"\bIS\s+(?!NULL\b|NOT\s+NULL\b)\S", re.IGNORECASE)


class _SesionQueSoloCompila:
    """Sesion falsa que compila lo que recibe y no toca ninguna base.

    Sirve para sacar el SQL que de verdad generan los metodos del repositorio,
    en vez de confiar en leer el codigo fuente: si manana alguien arma la misma
    consulta de otra forma, esto la sigue viendo.
    """

    def __init__(self) -> None:
        self.sentencias: list[str] = []

    async def execute(self, sentencia, *_args, **_kwargs):
        self.sentencias.append(
            str(sentencia.compile(dialect=mssql.dialect(),
                                  compile_kwargs={"literal_binds": True})))
        return _ResultadoConUnaFila()

    async def get(self, *_args, **_kwargs):
        # `flota` exige un plan cargado antes de llegar a sus consultas.
        return _PlanDeMentira()

    async def commit(self) -> None:
        pass


class _PlanDeMentira:
    id = "plan-1"
    linea_solicitud_id = "linea-1"
    numero_vehiculo = 1


class _ResultadoConUnaFila:
    """Devuelve UNA fila, no cero.

    Con cero, `flota` y `listar_por_linea` se cortaban en su
    `... if ids else {}` y nunca llegaban a armar la consulta booleana: la
    prueba pasaba aunque el codigo estuviera roto. Se detecto revirtiendo el
    arreglo y mirando CUALES casos fallaban - fallaban 5 de 7, no 7 de 7.
    """

    def scalars(self):
        # `scalars().all()` devuelve ENTIDADES, y los metodos que lo usan las
        # mapean a dominio: una tupla ahi revienta. Vacio es suficiente para
        # ellos, porque su consulta booleana ya se compilo al llamar.
        return _SinFilas()

    def all(self):
        # `.all()` sin `scalars` devuelve filas crudas, y `flota` /
        # `listar_por_linea` las usan para armar la lista de ids del SEGUNDO
        # query, que es el que filtra por booleano. Con cero filas ese query
        # no se arma y la prueba pasaba en vacio.
        return [("plan-1", 1)]

    def first(self):
        return None


class _SinFilas:
    def all(self):
        return []


async def _sql_de(metodo, *args) -> list[str]:
    sesion = _SesionQueSoloCompila()
    repo = SqlPlanCargueRepository(sesion)  # type: ignore[arg-type]
    await getattr(repo, metodo)(*args)
    return sesion.sentencias


# Los metodos que filtran por booleano. `actualizar_tras_ajuste` queda fuera
# porque necesita un plan cargado para avanzar; su consulta usa el mismo
# modelo y la cubre el barrido de abajo.
METODOS_CON_BOOLEANOS = [
    ("obtener_colocaciones_bloqueadas", ("plan-1",)),
    ("obtener_sobrantes", ("plan-1",)),
    ("cargo_algo", ("plan-1",)),
    ("flota", ("plan-1",)),
    ("listar_por_linea", ("linea-1",)),
]


@pytest.mark.parametrize("metodo,args", METODOS_CON_BOOLEANOS)
async def test_no_generan_is_con_literal(metodo, args):
    """`IS 0` / `IS 1` son el error 102 en SQL Server.

    Antes de #344 estos cinco metodos lo generaban: SQLAlchemy compila
    `.is_(True)` a `columna IS 1`. Las formas que SQL Server si acepta son
    `= 1` y `= 0`, que es a lo que compilan `Modelo.campo` y `~Modelo.campo`.
    """
    for sql in await _sql_de(metodo, *args):
        encontrado = _IS_CON_LITERAL.search(sql)
        assert encontrado is None, (
            f"{metodo} genera `IS` con un literal, que SQL Server rechaza con el "
            f"error 102:\n    ...{sql[max(0, encontrado.start() - 60):encontrado.end() + 20]}...\n"
            "Usar `Modelo.campo` / `~Modelo.campo` en vez de `.is_(True)` / `.is_(False)`.")


async def test_los_booleanos_se_comparan_con_igual():
    """La contraparte del anterior: que la comparacion siga ahi.

    Sin esto, quitar el filtro entero tambien pasaria la prueba de arriba.
    """
    sql = (await _sql_de("obtener_sobrantes", "plan-1"))[0]
    assert "cargada = 0" in sql, sql


def _llamadas_is_con_booleano(ruta: pathlib.Path) -> list[int]:
    """Lineas con `algo.is_(<booleano>)`, leyendo el ARBOL y no el texto.

    Buscarlo con una expresion regular tenia dos agujeros, los dos reales:

    - se tropezaba con los comentarios que explican por que no se usa, asi que
      hubo que exigir un prefijo `...Model.`; y con eso dejaba fuera los
      modelos del catalogo, que se llaman `Vehiculo`, `TipoMercancia`,
      `ReglaIncompatibilidad`... y tienen trece columnas booleanas entre ellos
    - no veia `.is_(sa.false())`, que compila al mismo `IS 0`

    El arbol no tiene esos problemas: un comentario no es una llamada, y el
    nombre del modelo da igual.
    """
    arbol = ast.parse(ruta.read_text(encoding="utf-8"), filename=str(ruta))
    lineas = []
    for nodo in ast.walk(arbol):
        if not (isinstance(nodo, ast.Call)
                and isinstance(nodo.func, ast.Attribute)
                and nodo.func.attr == "is_"
                and len(nodo.args) == 1):
            continue
        arg = nodo.args[0]
        es_literal = isinstance(arg, ast.Constant) and isinstance(arg.value, bool)
        # `sa.true()` / `false()` compilan igual que el literal.
        es_helper = (isinstance(arg, ast.Call) and isinstance(arg.func, (ast.Name, ast.Attribute))
                     and (arg.func.id if isinstance(arg.func, ast.Name) else arg.func.attr)
                     in ("true", "false"))
        if es_literal or es_helper:
            lineas.append(nodo.lineno)
    return lineas


def test_ningun_repositorio_usa_is_sobre_booleanos():
    """Barrido por si aparece donde las pruebas de arriba no llegan.

    Las de compilacion solo recorren `SqlPlanCargueRepository`; los otros cinco
    repositorios no pasan por ningun compilador. Y `actualizar_tras_ajuste`
    necesita un plan cargado para avanzar, asi que tampoco esta ahi. Este
    barrido es lo unico que los cubre.
    """
    raiz = pathlib.Path(__file__).resolve().parents[2] / "infrastructure"
    ofensores = [
        f"{ruta.relative_to(raiz.parent)}:{n}"
        for ruta in sorted(raiz.rglob("*.py"))
        for n in _llamadas_is_con_booleano(ruta)
    ]
    assert not ofensores, (
        "`.is_(True)` / `.is_(False)` compilan a `IS 1` / `IS 0`, que SQL Server "
        f"rechaza con el error 102. Usar `Modelo.campo` / `~Modelo.campo`:\n  "
        + "\n  ".join(ofensores))
