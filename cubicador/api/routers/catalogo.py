"""Catalogos: lectura publica (S5-HU-17.4) y CRUD (S5-HU-17.5).

`GET /tipos-mercancia` y `GET /tipos-carroceria` son la seccion 2.7 del
contrato: NEXO proyecta estos catalogos por eventos y Cotifly los consulta con
cache.

Las escrituras son los CRUD. **El portal nunca llama aqui directo**: no hay
CORS -se retiro a proposito-, la llave terminaria en el bundle (ya paso una vez
y obligo a rotarla) y, sobre todo, el rol no se podria aplicar, porque el
Bearer del Cubicador autoriza un backend y no a una persona. Entre el
formulario y este servicio hay un passthrough en NEXO que espeja estas rutas y
aplica el rol (S5-HU-18.6).

`/medidas-estandar` vivia bajo `/api/cubicacion`, que se retiro: se mudo aca
sin cambios de comportamiento.
"""

from __future__ import annotations

from decimal import Decimal

from fastapi import APIRouter, Depends

from api.dependencias import (
    obtener_catalogo_crud_repo,
    obtener_catalogo_repo,
    obtener_eventos_publisher,
    obtener_parametros_repo,
    verificar_api_key,
)
from api.dtos.catalogo import (
    carroceria_desde as _carroceria,
    mercancia_desde as _mercancia,
    vehiculo_desde as _vehiculo,
    ActualizarCarroceriaDto,
    ActualizarCategoriaDto,
    ActualizarMercanciaDto,
    ActualizarParametroDto,
    ActualizarVehiculoDto,
    BorrarDto,
    CarroceriaDeVehiculoDto,
    CategoriaMercanciaDto,
    CrearCarroceriaDto,
    CrearCategoriaDto,
    CrearMercanciaDto,
    CrearReglaDto,
    CrearVehiculoDto,
    ParametroMotorDto,
    ReglaIncompatibilidadDto,
    TipoCarroceriaDto,
    TipoMercanciaDto,
    VehiculoCatalogoDto,
)
from api.dtos.recomendaciones import MedidasEstandarDto
from application.catalogo import casos_de_uso as cu
from application.parametros import casos_de_uso as cu_parametros
from domain.entidades.entidades import ParametrosCubicacion

router = APIRouter(tags=["catalogo"], dependencies=[Depends(verificar_api_key)])


# --- traductores ------------------------------------------------------------
#
# Carroceria, mercancia y vehiculo se construyen con las funciones de
# `api/dtos/catalogo.py`, porque las comparten el router (para la respuesta) y
# los casos de uso (para el payload del evento de catalogo). Los tres de abajo
# solo los usa el router: categorias, parametros y reglas no se proyectan en
# NEXO, asi que no viajan en ningun evento.


def _categoria(fila) -> CategoriaMercanciaDto:
    return CategoriaMercanciaDto(id=fila.id, nombre_categoria=fila.nombre,
                                 version=fila.version)


def _parametro(fila) -> ParametroMotorDto:
    return ParametroMotorDto(id=fila.id, clave=fila.clave, valor=fila.valor,
                             descripcion=fila.descripcion, version=fila.version)


def _regla(fila) -> ReglaIncompatibilidadDto:
    return ReglaIncompatibilidadDto(
        id=fila.id, id_categoria_1=fila.categoria_1_id, id_categoria_2=fila.categoria_2_id,
        motivo=fila.motivo, version=fila.version)


async def _codigos_de_carroceria(repo) -> dict[str, str]:
    return {c.id: c.codigo for c in await repo.listar("carrocerias")}


# --- tipos de carroceria ----------------------------------------------------


@router.get("/tipos-carroceria", response_model=list[TipoCarroceriaDto])
async def listar_carrocerias(repo=Depends(obtener_catalogo_crud_repo)):
    return [_carroceria(f) for f in await repo.listar("carrocerias")]


@router.post("/tipos-carroceria", response_model=TipoCarroceriaDto, status_code=201)
async def crear_carroceria(peticion: CrearCarroceriaDto,
                           repo=Depends(obtener_catalogo_crud_repo),
                           eventos=Depends(obtener_eventos_publisher)):
    fila = await cu.crear_carroceria(repo, peticion.codigo_carroceria,
                                     peticion.nombre_carroceria, peticion.usuario_id, eventos)
    return _carroceria(fila)


@router.put("/tipos-carroceria/{id}", response_model=TipoCarroceriaDto)
async def actualizar_carroceria(id: str, peticion: ActualizarCarroceriaDto,
                                repo=Depends(obtener_catalogo_crud_repo),
                                eventos=Depends(obtener_eventos_publisher)):
    fila = await repo.actualizar(
        "carrocerias", id,
        {"nombre": peticion.nombre_carroceria, "activo": peticion.activo},
        peticion.version, peticion.usuario_id)
    if fila is None:
        from application.excepciones import NotFoundException
        raise NotFoundException("NO_ENCONTRADO", f"No existe la carroceria '{id}'.")
    await eventos.publicar_carroceria_actualizada(
        fila.id, {"codigo": fila.codigo, "nombre": fila.nombre, "activo": fila.activo})
    return _carroceria(fila)


@router.delete("/tipos-carroceria/{id}", status_code=204)
async def borrar_carroceria(id: str, peticion: BorrarDto,
                            repo=Depends(obtener_catalogo_crud_repo),
                            eventos=Depends(obtener_eventos_publisher)):
    await cu.eliminar_del_catalogo(repo, "carrocerias", id, peticion.version, eventos)


@router.post("/tipos-carroceria/republicar-catalogo", response_model=list[TipoCarroceriaDto])
async def republicar_carrocerias(repo=Depends(obtener_catalogo_crud_repo),
                                 eventos=Depends(obtener_eventos_publisher)):
    """Reparo manual de una desincronizacion con la proyeccion de NEXO.

    Republica el evento de creacion para TODO el catalogo, no solo lo nuevo.
    Seguro de correr mas de una vez: el consumer de NEXO hace upsert por Id. No
    es parte de ningun flujo automatico. Es la misma herramienta que ya existia
    para los vehiculos del motor 3D, que hizo falta cuando esa proyeccion quedo
    en 13 filas contra 20 de aca.
    """
    filas = await cu.republicar_catalogo(repo, "carrocerias", eventos)
    return [_carroceria(f) for f in filas]


# --- categorias de mercancia ------------------------------------------------


@router.get("/categorias-mercancia", response_model=list[CategoriaMercanciaDto])
async def listar_categorias(repo=Depends(obtener_catalogo_crud_repo)):
    return [_categoria(f) for f in await repo.listar("categorias")]


@router.post("/categorias-mercancia", response_model=CategoriaMercanciaDto, status_code=201)
async def crear_categoria(peticion: CrearCategoriaDto,
                          repo=Depends(obtener_catalogo_crud_repo)):
    return _categoria(await cu.crear_categoria(repo, peticion.nombre_categoria,
                                               peticion.usuario_id))


@router.put("/categorias-mercancia/{id}", response_model=CategoriaMercanciaDto)
async def actualizar_categoria(id: str, peticion: ActualizarCategoriaDto,
                               repo=Depends(obtener_catalogo_crud_repo)):
    fila = await repo.actualizar(
        "categorias", id, {"nombre": peticion.nombre_categoria.strip().upper()},
        peticion.version, peticion.usuario_id)
    if fila is None:
        from application.excepciones import NotFoundException
        raise NotFoundException("NO_ENCONTRADO", f"No existe la categoria '{id}'.")
    return _categoria(fila)


@router.delete("/categorias-mercancia/{id}", status_code=204)
async def borrar_categoria(id: str, peticion: BorrarDto,
                           repo=Depends(obtener_catalogo_crud_repo)):
    await cu.eliminar_del_catalogo(repo, "categorias", id, peticion.version)


# --- tipos de mercancia -----------------------------------------------------


def _datos_de_mercancia(peticion) -> dict:
    return {
        "codigo": peticion.codigo_mercancia.strip().upper(),
        "nombre": peticion.nombre_mercancia.strip(),
        "categoria_id": peticion.id_categoria,
        "cubicable": peticion.cubicable,
        "condicionada": peticion.condicionada_cubicacion,
        "comentario_condicion": peticion.comentario_condicion,
        "carroceria_principal_id": peticion.id_carroceria_principal,
        "carroceria_secundaria_id": peticion.id_carroceria_secundaria,
        "secundaria_condicionada": peticion.carroceria_secundaria_condicionada,
        "control_ambiental": peticion.control_ambiental_requerido,
        "apilable": peticion.apilable,
        "fragil": peticion.fragil,
        "rotacion_libre": peticion.rotacion_libre,
        "activo": peticion.activo,
    }


@router.get("/tipos-mercancia", response_model=list[TipoMercanciaDto])
async def listar_mercancias(repo=Depends(obtener_catalogo_crud_repo)):
    categorias = {c.id: c.nombre for c in await repo.listar("categorias")}
    carrocerias = await _codigos_de_carroceria(repo)
    return [_mercancia(f, categorias, carrocerias) for f in await repo.listar("mercancias")]


@router.post("/tipos-mercancia", response_model=TipoMercanciaDto, status_code=201)
async def crear_mercancia(peticion: CrearMercanciaDto,
                          repo=Depends(obtener_catalogo_crud_repo),
                          eventos=Depends(obtener_eventos_publisher)):
    fila = await cu.crear_mercancia(repo, _datos_de_mercancia(peticion),
                                    peticion.usuario_id, eventos)
    categorias = {c.id: c.nombre for c in await repo.listar("categorias")}
    return _mercancia(fila, categorias, await _codigos_de_carroceria(repo))


@router.put("/tipos-mercancia/{id}", response_model=TipoMercanciaDto)
async def actualizar_mercancia(id: str, peticion: ActualizarMercanciaDto,
                               repo=Depends(obtener_catalogo_crud_repo),
                               eventos=Depends(obtener_eventos_publisher)):
    fila = await cu.actualizar_mercancia(repo, id, _datos_de_mercancia(peticion),
                                         peticion.version, peticion.usuario_id, eventos)
    categorias = {c.id: c.nombre for c in await repo.listar("categorias")}
    return _mercancia(fila, categorias, await _codigos_de_carroceria(repo))


@router.delete("/tipos-mercancia/{id}", status_code=204)
async def borrar_mercancia(id: str, peticion: BorrarDto,
                           repo=Depends(obtener_catalogo_crud_repo),
                           eventos=Depends(obtener_eventos_publisher)):
    await cu.eliminar_mercancia(repo, id, peticion.version, peticion.usuario_id, eventos)


@router.post("/tipos-mercancia/republicar-catalogo", response_model=list[TipoMercanciaDto])
async def republicar_mercancias(repo=Depends(obtener_catalogo_crud_repo),
                                eventos=Depends(obtener_eventos_publisher)):
    """Ver `republicar_carrocerias`."""
    filas = await cu.republicar_catalogo(repo, "mercancias", eventos)
    categorias = {c.id: c.nombre for c in await repo.listar("categorias")}
    carrocerias = await _codigos_de_carroceria(repo)
    return [_mercancia(f, categorias, carrocerias) for f in filas]


# --- vehiculos del catalogo -------------------------------------------------


def _datos_de_vehiculo(peticion) -> dict:
    return {
        "codigo": peticion.codigo_vehiculo.strip().upper(),
        "nombre": peticion.nombre_vehiculo.strip(),
        "largo_cm": peticion.largo_cm,
        "ancho_cm": peticion.ancho_cm,
        "alto_util_cm": peticion.alto_util_cm,
        "capacidad_kg": peticion.capacidad_maxima_kg,
        "activo": peticion.activo,
    }


def _carrocerias_de(peticion) -> list[dict]:
    return [
        {"carroceria_id": c.id_carroceria,
         "usa_capacidad_del_vehiculo": c.usa_capacidad_maxima_del_vehiculo,
         "capacidad_especifica_kg": c.capacidad_maxima_especifica}
        for c in peticion.carrocerias
    ]


@router.get("/vehiculos-catalogo", response_model=list[VehiculoCatalogoDto])
async def listar_vehiculos(repo=Depends(obtener_catalogo_crud_repo)):
    carrocerias = await _codigos_de_carroceria(repo)
    salida = []
    for fila in await repo.listar("vehiculos"):
        salida.append(_vehiculo(fila, await repo.carrocerias_de(fila.id), carrocerias))
    return salida


@router.post("/vehiculos-catalogo", response_model=VehiculoCatalogoDto, status_code=201)
async def crear_vehiculo(peticion: CrearVehiculoDto,
                         repo=Depends(obtener_catalogo_crud_repo),
                         catalogo_repo=Depends(obtener_catalogo_repo),
                         eventos=Depends(obtener_eventos_publisher)):
    fila = await cu.guardar_vehiculo(
        repo, catalogo_repo, _datos_de_vehiculo(peticion), _carrocerias_de(peticion),
        peticion.usuario_id, eventos=eventos)
    return _vehiculo(fila, await repo.carrocerias_de(fila.id),
                     await _codigos_de_carroceria(repo))


@router.put("/vehiculos-catalogo/{id}", response_model=VehiculoCatalogoDto)
async def actualizar_vehiculo(id: str, peticion: ActualizarVehiculoDto,
                              repo=Depends(obtener_catalogo_crud_repo),
                              catalogo_repo=Depends(obtener_catalogo_repo),
                              eventos=Depends(obtener_eventos_publisher)):
    fila = await cu.guardar_vehiculo(
        repo, catalogo_repo, _datos_de_vehiculo(peticion), _carrocerias_de(peticion),
        peticion.usuario_id, id=id, version=peticion.version, eventos=eventos)
    return _vehiculo(fila, await repo.carrocerias_de(fila.id),
                     await _codigos_de_carroceria(repo))


@router.delete("/vehiculos-catalogo/{id}", status_code=204)
async def borrar_vehiculo(id: str, peticion: BorrarDto,
                          repo=Depends(obtener_catalogo_crud_repo),
                          eventos=Depends(obtener_eventos_publisher)):
    await cu.eliminar_del_catalogo(repo, "vehiculos", id, peticion.version, eventos)


@router.post("/vehiculos-catalogo/republicar-catalogo",
             response_model=list[VehiculoCatalogoDto])
async def republicar_vehiculos(repo=Depends(obtener_catalogo_crud_repo),
                               eventos=Depends(obtener_eventos_publisher)):
    """Ver `republicar_carrocerias`."""
    filas = await cu.republicar_catalogo(repo, "vehiculos", eventos)
    carrocerias = await _codigos_de_carroceria(repo)
    return [_vehiculo(f, await repo.carrocerias_de(f.id), carrocerias) for f in filas]


# --- reglas de incompatibilidad ---------------------------------------------


@router.get("/reglas-incompatibilidad", response_model=list[ReglaIncompatibilidadDto])
async def listar_reglas(repo=Depends(obtener_catalogo_crud_repo)):
    return [_regla(f) for f in await repo.listar("reglas")]


@router.post("/reglas-incompatibilidad", response_model=ReglaIncompatibilidadDto,
             status_code=201)
async def crear_regla(peticion: CrearReglaDto, repo=Depends(obtener_catalogo_crud_repo)):
    return _regla(await cu.crear_regla(
        repo, peticion.id_categoria_1, peticion.id_categoria_2, peticion.motivo,
        peticion.usuario_id))


@router.delete("/reglas-incompatibilidad/{id}", status_code=204)
async def borrar_regla(id: str, peticion: BorrarDto,
                       repo=Depends(obtener_catalogo_crud_repo)):
    await cu.eliminar_del_catalogo(repo, "reglas", id, peticion.version)


# --- parametros del motor ---------------------------------------------------


@router.get("/parametros-motor", response_model=list[ParametroMotorDto])
async def listar_parametros(repo=Depends(obtener_catalogo_crud_repo)):
    return [_parametro(f) for f in await cu.listar_parametros(repo)]


@router.put("/parametros-motor/{clave}", response_model=ParametroMotorDto)
async def actualizar_parametro(clave: str, peticion: ActualizarParametroDto,
                               repo=Depends(obtener_catalogo_crud_repo)):
    return _parametro(await cu.actualizar_parametro(
        repo, clave, peticion.valor, peticion.version, peticion.usuario_id))


# --- medidas estandar -------------------------------------------------------


def _medidas(parametros: ParametrosCubicacion) -> MedidasEstandarDto:
    return MedidasEstandarDto(
        largo_max_cm=Decimal(str(parametros.largo_max_cm)),
        ancho_max_cm=Decimal(str(parametros.ancho_max_cm)),
        alto_max_cm=Decimal(str(parametros.alto_max_cm)),
        peso_max_kg=Decimal(str(parametros.peso_max_kg)),
    )


@router.get("/medidas-estandar", response_model=MedidasEstandarDto)
async def medidas_estandar(parametros_repo=Depends(obtener_parametros_repo)):
    return _medidas(await cu_parametros.obtener_medidas_estandar(parametros_repo))


@router.put("/medidas-estandar", response_model=MedidasEstandarDto)
async def actualizar_medidas(peticion: MedidasEstandarDto,
                             parametros_repo=Depends(obtener_parametros_repo)):
    medidas = ParametrosCubicacion(
        largo_max_cm=float(peticion.largo_max_cm), ancho_max_cm=float(peticion.ancho_max_cm),
        alto_max_cm=float(peticion.alto_max_cm), peso_max_kg=float(peticion.peso_max_kg))
    return _medidas(await cu_parametros.actualizar_medidas_estandar(parametros_repo, medidas))
