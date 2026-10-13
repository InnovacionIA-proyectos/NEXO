"""CRUD de los seis catalogos (S5-HU-17.5).

Los endpoints viven en el Cubicador porque aqui esta el dominio; el portal los
consume por passthrough desde NEXO, que es donde estan los usuarios y los
roles. El Cubicador no tiene usuarios: su Bearer autoriza **un backend**, no a
una persona.

De ahi sale una consecuencia que conviene decir en voz alta: **`CreadoPor` lo
declara el llamante**. NEXO manda el id de usuario como un campo del cuerpo y
el Cubicador no lo puede verificar. Queda guardado como lo que es, un dato
declarado, no una identidad comprobada.

Las validaciones de aqui son las que la base no puede hacer sola. Lo que si
hace la base -y por eso no se repite- son las llaves foraneas que impiden
borrar una categoria o una carroceria referenciada, el `UNIQUE` de las siete
claves de negocio y tres `CHECK`: categoria 1 distinta de categoria 2,
secundaria distinta de principal, y la bandera condicionada en falso cuando no
hay secundaria. Lo que agrega esta capa es traducir esos choques a un 400 o un
409 **con un mensaje que diga que paso**, en vez de dejar salir el error de SQL.
"""

from __future__ import annotations

from dataclasses import replace
from decimal import Decimal

from application.excepciones import (
    ConflictException,
    NotFoundException,
    ValidationFailedException,
)
from application.ports.externos import IEventosPort
from application.ports.repositorios import ICatalogoCrudRepository, ICatalogoRepository
from domain.entidades.catalogo import CarroceriaDeVehiculo, Vehiculo
from domain.motor.seleccion_flota import CatalogoInconsistente, validar_dominancia

# Los catalogos que NEXO proyecta de su lado. Los otros tres -categorias,
# parametros y reglas- no los proyecta nadie: publicarles un evento seria
# mandar mensajes que ninguna cola consume.
CON_EVENTO = {"mercancias", "carrocerias", "vehiculos"}


def _evento_carroceria(fila) -> dict:
    from api.dtos.catalogo import carroceria_desde, como_evento

    return como_evento(carroceria_desde(fila))


async def _evento_mercancia(repo: ICatalogoCrudRepository, fila) -> dict:
    from api.dtos.catalogo import como_evento, mercancia_desde

    categorias = {c.id: c.nombre for c in await repo.listar("categorias")}
    carrocerias = {c.id: c.codigo for c in await repo.listar("carrocerias")}
    return como_evento(mercancia_desde(fila, categorias, carrocerias))


async def _evento_vehiculo(repo: ICatalogoCrudRepository, fila) -> dict:
    from api.dtos.catalogo import como_evento, vehiculo_desde

    carrocerias = {c.id: c.codigo for c in await repo.listar("carrocerias")}
    relaciones = await repo.carrocerias_de(fila.id)
    return como_evento(vehiculo_desde(fila, relaciones, carrocerias))


def _exigir(condicion: bool, codigo: str, mensaje: str) -> None:
    if not condicion:
        raise ValidationFailedException(codigo, mensaje)


async def _publicar(eventos: IEventosPort | None, catalogo: str, accion: str,
                    id: str, datos: dict | None) -> None:
    """Avisa a NEXO para que su proyeccion no quede vieja sin enterarse.

    Si no se publica, `GET /api/mercancias` del lado de NEXO sigue ofreciendo
    una mercancia que aqui ya no existe, y la solicitud que la elija falla
    recien al llegar al Cubicador.

    `datos` es la **fila entera** en forma de DTO, no un resumen: NEXO hace
    upsert por Id, asi que un payload parcial le dejaria los demas campos
    vacios. `None` es solo para los eventos de borrado, que no tienen fila que
    proyectar.
    """
    if eventos is None or catalogo not in CON_EVENTO:
        return
    singular = {"mercancias": "mercancia", "carrocerias": "carroceria",
                "vehiculos": "vehiculo"}[catalogo]
    metodo = getattr(eventos, f"publicar_{singular}_{accion}")
    await (metodo(id) if datos is None else metodo(id, datos))


# --- 1. Parametros del motor -----------------------------------------------


async def listar_parametros(repo: ICatalogoCrudRepository) -> list:
    return await repo.listar("parametros")


async def actualizar_parametro(
    repo: ICatalogoCrudRepository, clave: str, valor: str, version: int, usuario_id: str,
):
    """Solo `GET` y `PUT` por clave: no se crean ni se borran parametros.

    Agregar uno es un cambio de codigo -alguien tiene que leerlo- y borrarlo
    dejaria al motor con su valor por defecto sin que nada avise.
    """
    filas = await repo.listar("parametros")
    fila = next((f for f in filas if f.clave == clave), None)
    if fila is None:
        raise NotFoundException(
            "PARAMETRO_NO_ENCONTRADO", f"No existe el parametro '{clave}'.")
    _exigir(bool(valor and valor.strip()), "VALOR_REQUERIDO",
            f"El parametro '{clave}' no puede quedar vacio.")
    return await repo.actualizar("parametros", fila.id, {"valor": valor}, version, usuario_id)


# --- 2 y 3. Categorias y carrocerias ---------------------------------------


async def crear_categoria(repo: ICatalogoCrudRepository, nombre: str, usuario_id: str):
    # Sin `eventos`: NEXO no proyecta las categorias (ver CON_EVENTO).
    _exigir(bool(nombre and nombre.strip()), "NOMBRE_REQUERIDO",
            "La categoria necesita un nombre.")
    return await repo.crear("categorias", {"nombre": nombre.strip().upper()}, usuario_id)


async def crear_carroceria(
    repo: ICatalogoCrudRepository, codigo: str, nombre: str, usuario_id: str, eventos=None,
):
    _exigir(bool(codigo and codigo.strip()), "CODIGO_REQUERIDO",
            "La carroceria necesita un codigo.")
    fila = await repo.crear(
        "carrocerias",
        {"codigo": codigo.strip().upper(), "nombre": nombre.strip(), "activo": True},
        usuario_id)
    await _publicar(eventos, "carrocerias", "creada", fila.id, _evento_carroceria(fila))
    return fila


async def eliminar_del_catalogo(
    repo: ICatalogoCrudRepository, catalogo: str, id: str, version: int, eventos=None,
) -> None:
    """Borrado fisico, bloqueado si algo la referencia.

    Las llaves foraneas ya lo impedirian, pero el error 547 de SQL Server no
    dice cuantas filas ni de donde: se cuenta antes para poder responderlo.

    Las carrocerias de un vehiculo son la excepcion: **le pertenecen**, no son
    una referencia de afuera. Se borran con el, igual que se reconcilian con el
    al guardarlo. Lo que si bloquea a un vehiculo es una recomendacion que lo
    nombre, porque esa es de otro y es append-only.
    """
    if catalogo == "vehiculos":
        await repo.reconciliar_carrocerias(id, [], "borrado")

    referencias = await repo.contar_referencias(catalogo, id)
    if referencias:
        detalle = ", ".join(f"{cuantas} en {donde}" for donde, cuantas in referencias.items())
        raise ConflictException(
            "CATALOGO_REFERENCIADO",
            f"No se puede borrar: hay {detalle}. Quitar esas referencias primero.")

    if not await repo.eliminar(catalogo, id, version):
        raise NotFoundException("NO_ENCONTRADO", f"No existe el registro '{id}'.")

    singular = {"carrocerias": "eliminada", "mercancias": "eliminada",
                "vehiculos": "eliminado"}.get(catalogo)
    if singular:
        await _publicar(eventos, catalogo, singular, id, None)


# --- 4. Tipos de mercancia -------------------------------------------------


def _validar_mercancia(datos: dict) -> None:
    principal = datos.get("carroceria_principal_id")
    secundaria = datos.get("carroceria_secundaria_id")
    _exigir(bool(principal), "CARROCERIA_PRINCIPAL_REQUERIDA",
            "Una mercancia sin carroceria principal no tiene con que emparejar un vehiculo.")
    _exigir(secundaria is None or secundaria != principal, "CARROCERIA_SECUNDARIA_IGUAL",
            "La carroceria secundaria tiene que ser distinta de la principal: "
            "si son la misma, no hay respaldo.")
    if secundaria is None:
        _exigir(not datos.get("secundaria_condicionada"), "CONDICIONADA_SIN_SECUNDARIA",
                "No se puede marcar la secundaria como condicionada si no hay secundaria.")


async def crear_mercancia(
    repo: ICatalogoCrudRepository, datos: dict, usuario_id: str, eventos=None,
):
    _validar_mercancia(datos)
    fila = await repo.crear("mercancias", datos, usuario_id)
    await _publicar(eventos, "mercancias", "creada", fila.id,
                    await _evento_mercancia(repo, fila))
    return fila


async def actualizar_mercancia(
    repo: ICatalogoCrudRepository, id: str, datos: dict, version: int,
    usuario_id: str, eventos=None,
):
    _validar_mercancia(datos)
    fila = await repo.actualizar("mercancias", id, datos, version, usuario_id)
    if fila is None:
        raise NotFoundException("NO_ENCONTRADO", f"No existe la mercancia '{id}'.")
    await _publicar(eventos, "mercancias", "actualizada", fila.id,
                    await _evento_mercancia(repo, fila))
    return fila


async def eliminar_mercancia(
    repo: ICatalogoCrudRepository, id: str, version: int, usuario_id: str, eventos=None,
):
    """Borrado **logico** si ya participo en alguna recomendacion.

    Esas recomendaciones son append-only y tienen que seguir diciendo lo mismo
    dentro de dos anos: borrar la mercancia dejaria filas apuntando a nada.
    Desactivarla la saca de los desplegables sin tocar la historia.
    """
    referencias = await repo.contar_referencias("mercancias", id)
    if referencias:
        fila = await repo.actualizar("mercancias", id, {"activo": False}, version, usuario_id)
        if fila is None:
            raise NotFoundException("NO_ENCONTRADO", f"No existe la mercancia '{id}'.")
        await _publicar(eventos, "mercancias", "actualizada", fila.id,
                        await _evento_mercancia(repo, fila))
        return fila

    if not await repo.eliminar("mercancias", id, version):
        raise NotFoundException("NO_ENCONTRADO", f"No existe la mercancia '{id}'.")
    await _publicar(eventos, "mercancias", "eliminada", id, None)
    return None


# --- 5. Vehiculos y sus carrocerias ----------------------------------------


def _validar_carrocerias_del_vehiculo(carrocerias: list[dict]) -> None:
    _exigir(bool(carrocerias), "SIN_CARROCERIAS",
            "Un vehiculo sin carrocerias no puede llevar ninguna mercancia.")
    vistas = set()
    for c in carrocerias:
        _exigir(c["carroceria_id"] not in vistas, "CARROCERIA_REPETIDA",
                "La misma carroceria aparece dos veces en el payload.")
        vistas.add(c["carroceria_id"])
        if not c["usa_capacidad_del_vehiculo"]:
            _exigir(c["capacidad_especifica_kg"] > 0, "CAPACIDAD_ESPECIFICA_INVALIDA",
                    "Si la combinacion no usa la capacidad del vehiculo, la suya tiene que "
                    "ser mayor que cero: en cero el motor entiende que no carga nada.")


async def _verificar_dominancia(
    catalogo_repo: ICatalogoRepository, vehiculo_id: str | None, datos: dict,
    carrocerias: list[dict], codigos_carroceria: dict[str, str],
) -> None:
    """Comprueba la dominancia **antes** de escribir, sobre como quedaria.

    Se proyecta el catalogo con el vehiculo ya cambiado y se corre la misma
    validacion que el motor hace al arrancar. Hacerlo despues de guardar
    obligaria a deshacer una escritura ya confirmada; hacerlo antes solo cuesta
    una lectura.
    """
    catalogo = await catalogo_repo.cargar()
    propuesto = Vehiculo(
        id=vehiculo_id or "nuevo",
        codigo=datos["codigo"], nombre=datos["nombre"],
        largo_cm=datos["largo_cm"], ancho_cm=datos["ancho_cm"],
        alto_util_cm=datos["alto_util_cm"], capacidad_kg=datos["capacidad_kg"],
        activo=datos.get("activo", True),
        carrocerias=tuple(
            CarroceriaDeVehiculo(
                carroceria=next(c for c in catalogo.carrocerias
                                if c.codigo == codigos_carroceria[r["carroceria_id"]]),
                capacidad_kg=(datos["capacidad_kg"] if r["usa_capacidad_del_vehiculo"]
                              else r["capacidad_especifica_kg"]),
            )
            for r in carrocerias
        ),
    )
    otros = tuple(v for v in catalogo.vehiculos if v.id != propuesto.id)
    try:
        validar_dominancia(replace(catalogo, vehiculos=otros + (propuesto,)))
    except CatalogoInconsistente as error:
        raise ValidationFailedException("DOMINANCIA_ROTA", str(error)) from error


async def guardar_vehiculo(
    repo: ICatalogoCrudRepository,
    catalogo_repo: ICatalogoRepository,
    datos: dict,
    carrocerias: list[dict],
    usuario_id: str,
    id: str | None = None,
    version: int | None = None,
    eventos=None,
):
    """Un solo payload con el vehiculo y sus carrocerias; el backend reconcilia.

    La version que se exige es la del **vehiculo**, y se sube aunque el cambio
    solo toque las relaciones: si no, dos administradores editando las
    carrocerias del mismo vehiculo no verian conflicto, porque la fila del
    vehiculo no cambiaria.
    """
    _exigir(bool(datos.get("codigo")), "CODIGO_REQUERIDO", "El vehiculo necesita un codigo.")
    _validar_carrocerias_del_vehiculo(carrocerias)

    existentes = {c.id: c.codigo for c in (await catalogo_repo.cargar()).carrocerias}
    faltantes = [c["carroceria_id"] for c in carrocerias if c["carroceria_id"] not in existentes]
    _exigir(not faltantes, "CARROCERIA_DESCONOCIDA",
            f"Estas carrocerias no estan en el catalogo: {', '.join(faltantes)}.")

    await _verificar_dominancia(catalogo_repo, id, datos, carrocerias, existentes)

    if id is None:
        fila = await repo.crear("vehiculos", datos, usuario_id)
        accion = "creado"
    else:
        fila = await repo.actualizar("vehiculos", id, datos, version or 0, usuario_id)
        if fila is None:
            raise NotFoundException("NO_ENCONTRADO", f"No existe el vehiculo '{id}'.")
        accion = "actualizado"

    await repo.reconciliar_carrocerias(fila.id, carrocerias, usuario_id)
    await _publicar(eventos, "vehiculos", accion, fila.id,
                    await _evento_vehiculo(repo, fila))
    return fila


# --- 6. Reglas de incompatibilidad -----------------------------------------


def _par_ordenado(categoria_1: str, categoria_2: str) -> tuple[str, str]:
    """Siempre el id menor primero, comparando como TEXTO.

    El `UNIQUE` de la tabla es sobre el par ordenado, asi que por si solo no
    impide que entre `(B,A)` habiendo `(A,B)`. Normalizando, ese indice pasa a
    garantizarlo. La incompatibilidad es simetrica -`aplica_a` la evalua en las
    dos direcciones- asi que el orden no significa nada.

    Se compara como texto y no con el orden nativo de `uniqueidentifier`:
    SQL Server ordena ese tipo por los ultimos seis bytes primero, y comparar
    distinto en cada lado dejaria filas que un lado cree normalizadas y el otro
    no. El script `S5-HU-17.5_crud_catalogos.sql` normalizo las existentes con
    el mismo criterio.
    """
    a, b = categoria_1.strip().upper(), categoria_2.strip().upper()
    return (a, b) if a <= b else (b, a)


async def crear_regla(
    repo: ICatalogoCrudRepository, categoria_1: str, categoria_2: str, motivo: str,
    usuario_id: str,
):
    """Sin `PUT`: una regla se borra y se crea de nuevo.

    Cambiarle las categorias a una regla existente es crear otra regla; dejarlo
    como `PUT` invitaria a editar el par en vez del motivo, que es lo unico que
    tendria sentido cambiar.
    """
    _exigir(categoria_1 != categoria_2, "CATEGORIAS_IGUALES",
            "Una categoria no puede ser incompatible consigo misma.")
    _exigir(bool(motivo and motivo.strip()), "MOTIVO_REQUERIDO",
            "La regla necesita un motivo: es lo que se le muestra a quien recibe el rechazo.")

    primera, segunda = _par_ordenado(categoria_1, categoria_2)
    existentes = await repo.listar("reglas")
    ya_esta = any(
        _par_ordenado(r.categoria_1_id, r.categoria_2_id) == (primera, segunda)
        for r in existentes
    )
    if ya_esta:
        raise ConflictException(
            "REGLA_DUPLICADA",
            "Ya existe una regla entre esas dos categorias, en un sentido o en el otro.")

    return await repo.crear(
        "reglas",
        {"categoria_1_id": primera, "categoria_2_id": segunda, "motivo": motivo.strip()},
        usuario_id)


# --- republicar el catalogo entero ------------------------------------------


async def republicar_catalogo(
    repo: ICatalogoCrudRepository, catalogo: str, eventos: IEventosPort,
) -> list:
    """Republica el evento de creacion para TODAS las filas de un catalogo.

    Repara una desincronizacion ya existente entre este catalogo y la
    proyeccion de NEXO: la misma herramienta que ya existe para vehiculos del
    motor 3D (`republicar_catalogo` de `plan_cargue/vehiculos.py`), que se
    necesito cuando la proyeccion quedo en 13 filas contra 20 de aca.

    Seguro de correr mas de una vez: el consumer de NEXO hace upsert por Id.
    No es parte de ningun flujo automatico; se invoca a mano.
    """
    if catalogo not in CON_EVENTO:
        raise ValidationFailedException(
            "CATALOGO_SIN_EVENTO",
            f"El catalogo '{catalogo}' no se proyecta en NEXO: no hay nada que republicar.")

    filas = await repo.listar(catalogo)
    accion = "creado" if catalogo == "vehiculos" else "creada"
    for fila in filas:
        if catalogo == "carrocerias":
            payload = _evento_carroceria(fila)
        elif catalogo == "mercancias":
            payload = await _evento_mercancia(repo, fila)
        else:
            payload = await _evento_vehiculo(repo, fila)
        await _publicar(eventos, catalogo, accion, fila.id, payload)
    return filas
