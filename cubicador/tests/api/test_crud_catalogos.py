"""CRUD de los seis catalogos (S5-HU-17.5), de punta a punta.

Corre contra la app real por ASGI, con su **propia** base SQLite en memoria:
`obtener_db` se sobreescribe para que estas pruebas puedan crear, cambiar y
borrar filas del catalogo sin afectar a las de
`test_contrato_recomendaciones.py`, que comparten el motor de `db.py`.

Lo que verifica: las validaciones que la base no puede hacer sola, la
traduccion de las que si hace a un 400/409 con mensaje, y la concurrencia
optimista.
"""

from __future__ import annotations

import os
from datetime import datetime, timezone
from decimal import Decimal

os.environ.setdefault("DATABASE_URL", "sqlite+aiosqlite:///:memory:")
os.environ.setdefault("CUBICADOR_API_KEY", "llave-servicio")

import pytest
from httpx import ASGITransport, AsyncClient
from sqlalchemy.ext.asyncio import async_sessionmaker, create_async_engine
from sqlalchemy.pool import StaticPool

from api.dependencias import obtener_db, obtener_eventos_publisher, verificar_api_key
from api.main import app
from infrastructure.persistence import modelos_catalogo as mc

# La autenticacion se sobreescribe en el fixture: `CUBICADOR_API_KEY` la
# escriben varios modulos de la suite al importarse y el orden no esta
# garantizado. El Bearer ya esta cubierto en test_contrato_recomendaciones.py.
LLAVE: dict[str, str] = {}
USUARIO = "1042"


class _PublisherFalso:
    """Registra lo publicado. Los eventos existen para que la proyeccion de
    NEXO no quede vieja: si no salen, nada falla aqui y todo falla alla."""

    def __init__(self) -> None:
        self.eventos: list[tuple[str, str, dict | None]] = []

    def __getattr__(self, nombre):
        if not nombre.startswith("publicar_"):
            raise AttributeError(nombre)

        async def _registrar(id_, datos=None):
            self.eventos.append((nombre, id_, datos))

        return _registrar


def _auditoria() -> dict:
    ahora = datetime.now(timezone.utc)
    return {"creado_por": "siembra", "created_at": ahora, "updated_at": ahora}


async def _recomendar_mercancia(Sesion, mercancia_id: str) -> None:
    """Deja una recomendacion que referencia la mercancia, para probar que el
    borrado pasa a ser logico."""
    a = _auditoria()
    async with Sesion() as s:
        s.add(mc.Recomendacion(
            id="rec-1", linea_solicitud_id="L-1", origen_sistema="NEXO",
            resultado="ASIGNADO", motivo_sin_seleccion="NINGUNO", condicionada=False,
            mercancias_condicionadas_json="[]", advertencias_json="[]",
            extradimension_json="{}", **a))
        s.add(mc.RecomendacionVehiculo(
            id="recv-1", recomendacion_id="rec-1", linea_solicitud_id="L-1",
            origen_sistema="NEXO", vehiculo_id="v-c2", carroceria_id="c-furgon",
            heterogenea=False, porcentaje_llenado=None,
            porcentaje_llenado_volumen=Decimal(50), porcentaje_llenado_peso=Decimal(50),
            alerta_bajo_llenado=False, peso_total_kg=Decimal(100),
            volumen_total_m3=Decimal(1), numero_vehiculo=1, resultado="ASIGNADO",
            motivo_sin_seleccion=None, **a))
        s.add(mc.RecomendacionVehiculoMercancia(
            id="recm-1", recomendacion_vehiculo_id="recv-1", mercancia_id=mercancia_id,
            cantidad=1, peso_kg=Decimal(100), volumen_m3=Decimal(1), **a))
        await s.commit()


@pytest.fixture
async def entorno():
    """Base propia por test: cada uno arranca con el mismo catalogo limpio."""
    engine = create_async_engine("sqlite+aiosqlite:///:memory:", poolclass=StaticPool,
                                 connect_args={"check_same_thread": False})
    async with engine.begin() as conn:
        await conn.run_sync(mc.BaseCatalogo.metadata.create_all)
    Sesion = async_sessionmaker(engine, expire_on_commit=False)

    a = _auditoria()
    async with Sesion() as s:
        s.add_all([
            mc.TipoCarroceria(id="c-furgon", codigo="FURGON", nombre="Furgon",
                              activo=True, **a),
            mc.TipoCarroceria(id="c-estacas", codigo="ESTACAS", nombre="Estacas",
                              activo=True, **a),
            mc.TipoCarroceria(id="c-libre", codigo="PORTAVIDRIOS",
                              nombre="Portavidrios", activo=True, **a),
            mc.CategoriaMercancia(id="cat-ali", nombre="ALIMENTOS", **a),
            mc.CategoriaMercancia(id="cat-fer", nombre="FERTILIZANTE", **a),
            mc.CategoriaMercancia(id="cat-libre", nombre="SIN_USO", **a),
        ])
        s.add(mc.Vehiculo(id="v-c2", codigo="C2", nombre="Sencillo C2",
                          largo_cm=Decimal(650), ancho_cm=Decimal(240),
                          alto_util_cm=Decimal(240), capacidad_kg=Decimal(8500),
                          activo=True, **a))
        s.add(mc.RelacionVehiculoCarroceria(
            id="r-1", vehiculo_id="v-c2", carroceria_id="c-furgon",
            usa_capacidad_del_vehiculo=True, capacidad_especifica_kg=Decimal(0), **a))
        s.add(mc.TipoMercancia(
            id="m-beb", codigo="BEBIDAS", nombre="Bebidas", categoria_id="cat-ali",
            cubicable=True, condicionada=False, comentario_condicion=None,
            carroceria_principal_id="c-furgon", carroceria_secundaria_id="c-estacas",
            secundaria_condicionada=True, control_ambiental="NINGUNO",
            apilable=True, fragil=False, rotacion_libre=True, activo=True, **a))
        s.add(mc.ParametroMotor(id="p-k", clave="k_max", valor="10",
                                descripcion="Tope de vehiculos", **a))
        await s.commit()

    publisher = _PublisherFalso()

    async def _db():
        async with Sesion() as sesion:
            yield sesion

    app.dependency_overrides[verificar_api_key] = lambda: None
    app.dependency_overrides[obtener_db] = _db
    app.dependency_overrides[obtener_eventos_publisher] = lambda: publisher
    transporte = ASGITransport(app=app)
    async with AsyncClient(transport=transporte, base_url="http://test") as c:
        yield c, publisher, Sesion
    app.dependency_overrides.clear()
    await engine.dispose()


@pytest.fixture
def client(entorno):
    return entorno[0]


@pytest.fixture
def publisher(entorno):
    return entorno[1]


@pytest.fixture
def sesion(entorno):
    return entorno[2]


async def _version(client, ruta: str, clave: str, valor: str) -> dict:
    filas = (await client.get(ruta, headers=LLAVE)).json()
    return next(f for f in filas if f[clave] == valor)


# --- concurrencia optimista -------------------------------------------------


async def test_el_get_devuelve_version_y_el_put_la_exige(client):
    carroceria = await _version(client, "/tipos-carroceria", "codigoCarroceria", "ESTACAS")
    assert carroceria["version"] == 1

    r = await client.put(f"/tipos-carroceria/{carroceria['id']}", headers=LLAVE, json={
        "nombreCarroceria": "Estacas altas", "activo": True,
        "usuarioId": USUARIO, "version": 1})
    assert r.status_code == 200, r.text
    assert r.json()["version"] == 2, "la version sube en cada escritura"


async def test_guardar_con_una_version_vieja_es_409(client):
    """Dos administradores abren la misma fila; el segundo no puede pisar al
    primero en silencio."""
    carroceria = await _version(client, "/tipos-carroceria", "codigoCarroceria", "ESTACAS")

    primero = await client.put(f"/tipos-carroceria/{carroceria['id']}", headers=LLAVE, json={
        "nombreCarroceria": "Cambio del primero", "activo": True,
        "usuarioId": "A", "version": 1})
    assert primero.status_code == 200

    segundo = await client.put(f"/tipos-carroceria/{carroceria['id']}", headers=LLAVE, json={
        "nombreCarroceria": "Cambio del segundo", "activo": True,
        "usuarioId": "B", "version": 1})
    assert segundo.status_code == 409
    assert segundo.json()["codigo"] == "CONFLICTO_DE_VERSION"

    actual = await _version(client, "/tipos-carroceria", "codigoCarroceria", "ESTACAS")
    assert actual["nombreCarroceria"] == "Cambio del primero", "el cambio del primero sobrevive"


async def test_el_usuario_es_obligatorio_en_toda_escritura(client):
    r = await client.post("/categorias-mercancia", headers=LLAVE,
                          json={"nombreCategoria": "NUEVA"})
    assert r.status_code == 422


# --- categorias y carrocerias: borrado bloqueado ----------------------------


async def test_no_se_borra_una_categoria_referenciada(client):
    categoria = await _version(client, "/categorias-mercancia", "nombreCategoria", "ALIMENTOS")
    r = await client.request("DELETE", f"/categorias-mercancia/{categoria['id']}",
                             headers=LLAVE, json={"usuarioId": USUARIO, "version": 1})
    assert r.status_code == 409
    assert r.json()["codigo"] == "CATALOGO_REFERENCIADO"
    assert "mercancias" in r.json()["mensaje"], "el mensaje dice cuantas y de donde"


async def test_una_categoria_sin_referencias_si_se_borra(client):
    categoria = await _version(client, "/categorias-mercancia", "nombreCategoria", "SIN_USO")
    r = await client.request("DELETE", f"/categorias-mercancia/{categoria['id']}",
                             headers=LLAVE, json={"usuarioId": USUARIO, "version": 1})
    assert r.status_code == 204, r.text


async def test_no_se_borra_una_carroceria_que_un_vehiculo_usa(client):
    carroceria = await _version(client, "/tipos-carroceria", "codigoCarroceria", "FURGON")
    r = await client.request("DELETE", f"/tipos-carroceria/{carroceria['id']}",
                             headers=LLAVE, json={"usuarioId": USUARIO, "version": 1})
    assert r.status_code == 409
    assert r.json()["codigo"] == "CATALOGO_REFERENCIADO"


# --- tipos de mercancia -----------------------------------------------------


def _mercancia(**extra) -> dict:
    cuerpo = {
        "codigoMercancia": "NUEVA", "nombreMercancia": "Nueva", "idCategoria": "cat-ali",
        "idCarroceriaPrincipal": "c-furgon", "idCarroceriaSecundaria": "c-estacas",
        "carroceriaSecundariaCondicionada": False, "usuarioId": USUARIO,
    }
    cuerpo.update(extra)
    return cuerpo


async def test_la_secundaria_no_puede_ser_la_principal(client):
    r = await client.post("/tipos-mercancia", headers=LLAVE,
                          json=_mercancia(idCarroceriaSecundaria="c-furgon"))
    assert r.status_code == 400
    assert r.json()["codigo"] == "CARROCERIA_SECUNDARIA_IGUAL"


async def test_sin_secundaria_la_bandera_condicionada_va_en_falso(client):
    r = await client.post("/tipos-mercancia", headers=LLAVE, json=_mercancia(
        idCarroceriaSecundaria=None, carroceriaSecundariaCondicionada=True))
    assert r.status_code == 400
    assert r.json()["codigo"] == "CONDICIONADA_SIN_SECUNDARIA"


async def test_una_mercancia_valida_se_crea_y_publica_su_evento(client, publisher):
    r = await client.post("/tipos-mercancia", headers=LLAVE, json=_mercancia())
    assert r.status_code == 201, r.text
    assert r.json()["codigoMercancia"] == "NUEVA"
    assert r.json()["version"] == 1
    assert any(e[0] == "publicar_mercancia_creada" for e in publisher.eventos)


async def test_una_mercancia_sin_recomendaciones_se_borra_de_verdad(client):
    creada = (await client.post("/tipos-mercancia", headers=LLAVE, json=_mercancia())).json()
    r = await client.request("DELETE", f"/tipos-mercancia/{creada['id']}", headers=LLAVE,
                             json={"usuarioId": USUARIO, "version": 1})
    assert r.status_code == 204, r.text
    codigos = {m["codigoMercancia"] for m in (await client.get("/tipos-mercancia",
                                                               headers=LLAVE)).json()}
    assert "NUEVA" not in codigos


# --- vehiculos y sus carrocerias --------------------------------------------


def _vehiculo(**extra) -> dict:
    cuerpo = {
        "codigoVehiculo": "C3", "nombreVehiculo": "Dobletroque C3",
        "largoCm": 800, "anchoCm": 240, "altoUtilCm": 260, "capacidadMaximaKg": 17000,
        "usuarioId": USUARIO,
        "carrocerias": [{"idCarroceria": "c-furgon", "usaCapacidadMaximaDelVehiculo": True}],
    }
    cuerpo.update(extra)
    return cuerpo


async def test_un_solo_payload_crea_el_vehiculo_y_sus_carrocerias(client, publisher):
    r = await client.post("/vehiculos-catalogo", headers=LLAVE, json=_vehiculo())
    assert r.status_code == 201, r.text
    datos = r.json()
    assert [c["codigoCarroceria"] for c in datos["carrocerias"]] == ["FURGON"]
    assert any(e[0] == "publicar_vehiculo_creado" for e in publisher.eventos)


async def test_el_backend_reconcilia_las_carrocerias(client):
    """Quitar una y agregar otra en el mismo payload: el backend deja el
    conjunto exactamente como se pidio."""
    creado = (await client.post("/vehiculos-catalogo", headers=LLAVE, json=_vehiculo())).json()

    r = await client.put(f"/vehiculos-catalogo/{creado['id']}", headers=LLAVE, json=_vehiculo(
        version=creado["version"],
        carrocerias=[{"idCarroceria": "c-estacas", "usaCapacidadMaximaDelVehiculo": True}]))
    assert r.status_code == 200, r.text
    assert [c["codigoCarroceria"] for c in r.json()["carrocerias"]] == ["ESTACAS"]


async def test_cambiar_solo_las_carrocerias_sube_la_version_del_vehiculo(client):
    """Si no subiera, dos administradores editando las carrocerias del mismo
    vehiculo no verian conflicto: la fila del vehiculo no cambia."""
    creado = (await client.post("/vehiculos-catalogo", headers=LLAVE, json=_vehiculo())).json()

    r = await client.put(f"/vehiculos-catalogo/{creado['id']}", headers=LLAVE, json=_vehiculo(
        version=creado["version"],
        carrocerias=[{"idCarroceria": "c-estacas", "usaCapacidadMaximaDelVehiculo": True}]))
    assert r.json()["version"] == creado["version"] + 1

    choque = await client.put(f"/vehiculos-catalogo/{creado['id']}", headers=LLAVE,
                              json=_vehiculo(version=creado["version"], carrocerias=[
                                  {"idCarroceria": "c-furgon",
                                   "usaCapacidadMaximaDelVehiculo": True}]))
    assert choque.status_code == 409


async def test_capacidad_especifica_en_cero_sin_usar_la_del_vehiculo(client):
    """En cero, el motor entiende que ese vehiculo no carga nada."""
    r = await client.post("/vehiculos-catalogo", headers=LLAVE, json=_vehiculo(carrocerias=[
        {"idCarroceria": "c-furgon", "usaCapacidadMaximaDelVehiculo": False,
         "capacidadMaximaEspecifica": 0}]))
    assert r.status_code == 400
    assert r.json()["codigo"] == "CAPACIDAD_ESPECIFICA_INVALIDA"


async def test_un_vehiculo_que_rompe_la_dominancia_se_rechaza(client):
    """Mas volumen y menos capacidad que el C2 existente: la seleccion de flota
    minima descartaria la respuesta correcta y pediria un camion de mas sin
    que nada avise."""
    r = await client.post("/vehiculos-catalogo", headers=LLAVE, json=_vehiculo(
        codigoVehiculo="XX", nombreVehiculo="Trailer liviano",
        largoCm=1400, anchoCm=250, altoUtilCm=270, capacidadMaximaKg=5000))
    assert r.status_code == 400, r.text
    assert r.json()["codigo"] == "DOMINANCIA_ROTA"


async def test_un_vehiculo_sin_carrocerias_se_rechaza(client):
    r = await client.post("/vehiculos-catalogo", headers=LLAVE, json=_vehiculo(carrocerias=[]))
    assert r.status_code == 400
    assert r.json()["codigo"] == "SIN_CARROCERIAS"


# --- reglas de incompatibilidad ---------------------------------------------


async def test_una_categoria_no_es_incompatible_consigo_misma(client):
    r = await client.post("/reglas-incompatibilidad", headers=LLAVE, json={
        "idCategoria1": "cat-ali", "idCategoria2": "cat-ali",
        "motivo": "da igual", "usuarioId": USUARIO})
    assert r.status_code == 400
    assert r.json()["codigo"] == "CATEGORIAS_IGUALES"


async def test_no_se_admite_el_par_invertido(client):
    """El UNIQUE de la base es sobre el par ORDENADO: por si solo no impide
    `(B,A)` habiendo `(A,B)`. El par se normaliza al guardar."""
    primera = await client.post("/reglas-incompatibilidad", headers=LLAVE, json={
        "idCategoria1": "cat-ali", "idCategoria2": "cat-fer",
        "motivo": "Contaminacion cruzada", "usuarioId": USUARIO})
    assert primera.status_code == 201, primera.text

    invertida = await client.post("/reglas-incompatibilidad", headers=LLAVE, json={
        "idCategoria1": "cat-fer", "idCategoria2": "cat-ali",
        "motivo": "La misma, al reves", "usuarioId": USUARIO})
    assert invertida.status_code == 409
    assert invertida.json()["codigo"] == "REGLA_DUPLICADA"


async def test_la_regla_se_guarda_con_el_par_ordenado(client):
    creada = (await client.post("/reglas-incompatibilidad", headers=LLAVE, json={
        "idCategoria1": "cat-fer", "idCategoria2": "cat-ali",
        "motivo": "Contaminacion cruzada", "usuarioId": USUARIO})).json()
    assert creada["idCategoria1"] <= creada["idCategoria2"]


async def test_las_reglas_no_tienen_put(client):
    r = await client.put("/reglas-incompatibilidad/lo-que-sea", headers=LLAVE, json={})
    assert r.status_code == 405


# --- parametros del motor ---------------------------------------------------


async def test_un_parametro_se_actualiza_por_clave(client):
    r = await client.put("/parametros-motor/k_max", headers=LLAVE, json={
        "valor": "12", "usuarioId": USUARIO, "version": 1})
    assert r.status_code == 200, r.text
    assert r.json()["valor"] == "12"
    assert r.json()["version"] == 2


async def test_un_parametro_que_no_existe_es_404(client):
    r = await client.put("/parametros-motor/no_existe", headers=LLAVE, json={
        "valor": "1", "usuarioId": USUARIO, "version": 1})
    assert r.status_code == 404


async def test_los_parametros_no_se_crean_ni_se_borran(client):
    """Agregar uno es un cambio de codigo -alguien tiene que leerlo- y borrarlo
    dejaria al motor con su valor por defecto sin que nada avise."""
    assert (await client.post("/parametros-motor", headers=LLAVE, json={})).status_code == 405
    assert (await client.request("DELETE", "/parametros-motor/k_max", headers=LLAVE,
                                 json={})).status_code == 405


# --- eventos ----------------------------------------------------------------


async def test_los_catalogos_que_nexo_no_proyecta_no_publican(client, publisher):
    """Categorias, parametros y reglas no los proyecta nadie: publicarles un
    evento seria mandar mensajes que ninguna cola consume."""
    await client.post("/categorias-mercancia", headers=LLAVE,
                      json={"nombreCategoria": "OTRA", "usuarioId": USUARIO})
    await client.put("/parametros-motor/k_max", headers=LLAVE,
                     json={"valor": "9", "usuarioId": USUARIO, "version": 1})
    assert publisher.eventos == []


async def test_una_mercancia_con_recomendaciones_se_desactiva_en_vez_de_borrarse(
    client, sesion,
):
    """Las recomendaciones son append-only y tienen que seguir diciendo lo
    mismo dentro de dos anos: borrar la mercancia dejaria filas apuntando a
    nada. Desactivarla la saca de los desplegables sin tocar la historia."""
    beb = await _version(client, "/tipos-mercancia", "codigoMercancia", "BEBIDAS")
    await _recomendar_mercancia(sesion, beb["id"])

    r = await client.request("DELETE", f"/tipos-mercancia/{beb['id']}", headers=LLAVE,
                             json={"usuarioId": USUARIO, "version": beb["version"]})
    assert r.status_code == 204, r.text

    despues = await _version(client, "/tipos-mercancia", "codigoMercancia", "BEBIDAS")
    assert despues["activo"] is False, "sigue existiendo, desactivada"


async def test_el_usuario_declarado_queda_en_creado_por(client, sesion):
    """El Cubicador no tiene usuarios: NEXO manda el id como campo del cuerpo
    y aqui se guarda como dato declarado, no como identidad comprobada."""
    from sqlalchemy import select

    creada = (await client.post("/categorias-mercancia", headers=LLAVE, json={
        "nombreCategoria": "AUDITADA", "usuarioId": "7788"})).json()

    async with sesion() as s:
        fila = (await s.execute(
            select(mc.CategoriaMercancia)
            .where(mc.CategoriaMercancia.id == creada["id"])
        )).scalars().one()
        assert fila.creado_por == "7788"


async def test_borrar_un_vehiculo_se_lleva_sus_carrocerias(client):
    """Las carrocerias de un vehiculo le pertenecen: no son una referencia de
    afuera que deba bloquear el borrado. Sin esto, la relacion que el propio
    vehiculo creo le impedia borrarse."""
    creado = (await client.post("/vehiculos-catalogo", headers=LLAVE, json=_vehiculo())).json()

    r = await client.request("DELETE", f"/vehiculos-catalogo/{creado['id']}", headers=LLAVE,
                             json={"usuarioId": USUARIO, "version": creado["version"]})
    assert r.status_code == 204, r.text

    codigos = {v["codigoVehiculo"]
               for v in (await client.get("/vehiculos-catalogo", headers=LLAVE)).json()}
    assert "C3" not in codigos


# --- lo que necesita la proyeccion de NEXO ----------------------------------


async def test_el_evento_lleva_la_fila_entera_no_solo_el_codigo(client, publisher):
    """NEXO hace upsert por Id: un payload parcial le dejaria la proyeccion con
    los demas campos vacios, y sus desplegables mostrarian filas a medias."""
    creada = (await client.post("/tipos-mercancia", headers=LLAVE, json=_mercancia())).json()

    nombre, _id, payload = publisher.eventos[-1]
    assert nombre == "publicar_mercancia_creada"
    assert payload["codigoMercancia"] == "NUEVA"
    assert payload["nombreMercancia"] == creada["nombreMercancia"]
    assert payload["categoria"] == creada["categoria"]
    assert payload["carroceriaPrincipal"] == "FURGON"
    assert payload["version"] == creada["version"]


async def test_republicar_el_catalogo_reemite_todo(client, publisher):
    """La herramienta de reparacion que pide S5-HU-18.5, equivalente a la que
    ya existia para los vehiculos del motor 3D."""
    antes = len(publisher.eventos)
    r = await client.post("/tipos-carroceria/republicar-catalogo", headers=LLAVE)
    assert r.status_code == 200, r.text

    publicados = publisher.eventos[antes:]
    assert len(publicados) == len(r.json()) == 3
    assert all(nombre == "publicar_carroceria_creada" for nombre, _, _ in publicados)
    assert all(p["codigoCarroceria"] for _, _, p in publicados)


async def test_republicar_es_seguro_de_correr_dos_veces(client, publisher):
    """El consumer de NEXO hace upsert por Id."""
    primera = (await client.post("/tipos-carroceria/republicar-catalogo", headers=LLAVE)).json()
    segunda = (await client.post("/tipos-carroceria/republicar-catalogo", headers=LLAVE)).json()
    assert [c["id"] for c in primera] == [c["id"] for c in segunda]


async def test_no_se_republica_un_catalogo_que_nexo_no_proyecta(client):
    r = await client.post("/categorias-mercancia/republicar-catalogo", headers=LLAVE)
    assert r.status_code in (404, 405), "no deberia existir la ruta"
