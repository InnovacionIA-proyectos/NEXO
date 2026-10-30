"""El contrato unico, de punta a punta (S5-HU-17.4).

Corre contra la app real por ASGI y contra SQLite en memoria, con un recorte
del catalogo sembrado en la misma base: HTTP -> caso de uso -> motor ->
persistencia -> HTTP otra vez.

Lo que verifica y las otras pruebas no pueden: que los nombres de los campos
salgan en camelCase como los espera el contrato, que el `origenSistema` venga
de la llave y no del cuerpo, y que lo que se guarda se pueda volver a leer
igual - que es de lo que depende el append-only.
"""

from __future__ import annotations

import os
from datetime import datetime, timezone
from decimal import Decimal

os.environ["DATABASE_URL"] = "sqlite+aiosqlite:///:memory:"
os.environ["CUBICADOR_API_KEY"] = "llave-servicio"
os.environ["CUBICADOR_API_KEY_NEXO"] = "llave-de-nexo"
os.environ["CUBICADOR_API_KEY_COTIFLY"] = "llave-de-cotifly"

import pytest
from httpx import ASGITransport, AsyncClient
from sqlalchemy import delete, select

from api.dependencias import obtener_eventos_publisher
from api.main import app
from infrastructure.persistence import modelos_catalogo as mc
from infrastructure.persistence.db import SessionLocal, crear_tablas, engine

COTIFLY = {"Authorization": "Bearer llave-de-cotifly"}
NEXO = {"Authorization": "Bearer llave-de-nexo"}
SERVICIO = {"Authorization": "Bearer llave-servicio"}

ID_BEBIDAS = "m-bebidas"
ID_CARNICOS = "m-carnicos"


class _PublisherFalso:
    async def publicar_vehiculo_creado(self, vehiculo_id: str, vehiculo: dict) -> None:
        pass


def _auditoria() -> dict:
    ahora = datetime.now(timezone.utc)
    return {"creado_por": "prueba", "created_at": ahora, "updated_at": ahora}


async def _limpiar_recomendaciones() -> None:
    """SQLite en memoria con StaticPool es UNA sola conexion para toda la
    suite: lo que escribe un test lo ve el siguiente. Las tres tablas de
    recomendacion se vacian entre casos para que cada uno parta de cero; el
    catalogo no, porque es de solo lectura y sembrarlo una vez alcanza."""
    async with SessionLocal() as s:
        for modelo in (mc.RecomendacionVehiculoMercancia, mc.RecomendacionVehiculo,
                       mc.Recomendacion):
            await s.execute(delete(modelo))
        await s.commit()


async def _sembrar_catalogo() -> None:
    a = _auditoria()
    async with SessionLocal() as s:
        ya_sembrado = (await s.execute(select(mc.TipoMercancia.id).limit(1))).scalars().first()
        if ya_sembrado is not None:
            return
        furgon = mc.TipoCarroceria(id="c-furgon", codigo="FURGON", nombre="Furgon",
                                   activo=True, **a)
        refrigerado = mc.TipoCarroceria(id="c-refri", codigo="FURGON_REFRIGERADO",
                                        nombre="Furgon refrigerado", activo=True, **a)
        s.add_all([furgon, refrigerado])

        for codigo, nombre, largo, ancho, alto, capacidad in (
            ("C2", "Sencillo C2", 650, 240, 240, 8500),
            ("2S2", "Minimula 2S2", 1220, 240, 260, 18000),
        ):
            s.add(mc.Vehiculo(
                id=f"v-{codigo}", codigo=codigo, nombre=nombre, largo_cm=Decimal(largo),
                ancho_cm=Decimal(ancho), alto_util_cm=Decimal(alto),
                capacidad_kg=Decimal(capacidad), activo=True, **a))
            for carroceria in ("c-furgon", "c-refri"):
                s.add(mc.RelacionVehiculoCarroceria(
                    id=f"r-{codigo}-{carroceria}", vehiculo_id=f"v-{codigo}",
                    carroceria_id=carroceria, usa_capacidad_del_vehiculo=True,
                    capacidad_especifica_kg=Decimal(0), **a))

        s.add(mc.CategoriaMercancia(id="cat-ali", nombre="ALIMENTOS", **a))
        s.add_all([
            mc.TipoMercancia(
                id=ID_BEBIDAS, codigo="BEBIDAS", nombre="Bebidas", categoria_id="cat-ali",
                cubicable=True, condicionada=False, comentario_condicion=None,
                carroceria_principal_id="c-furgon", carroceria_secundaria_id=None,
                secundaria_condicionada=False, control_ambiental="NINGUNO",
                apilable=True, fragil=False, rotacion_libre=True, activo=True, **a),
            mc.TipoMercancia(
                id=ID_CARNICOS, codigo="CARNICOS", nombre="Carnicos", categoria_id="cat-ali",
                cubicable=True, condicionada=False, comentario_condicion=None,
                carroceria_principal_id="c-refri", carroceria_secundaria_id=None,
                secundaria_condicionada=False, control_ambiental="REFRIGERADO",
                apilable=True, fragil=False, rotacion_libre=True, activo=True, **a),
        ])
        s.add(mc.ParametroMotor(id="p-k", clave="k_max", valor="10",
                                descripcion="Tope de vehiculos", **a))
        await s.commit()


@pytest.fixture
async def client():
    await crear_tablas()
    async with engine.begin() as conn:
        await conn.run_sync(mc.BaseCatalogo.metadata.create_all)
    await _sembrar_catalogo()
    await _limpiar_recomendaciones()

    app.dependency_overrides[obtener_eventos_publisher] = lambda: _PublisherFalso()
    transporte = ASGITransport(app=app)
    async with AsyncClient(transport=transporte, base_url="http://test") as c:
        yield c
    app.dependency_overrides.clear()


def _cuerpo(**extra) -> dict:
    cuerpo = {
        "idLineaSolicitud": "LINEA-001",
        "mercancias": [{
            "codigoMercancia": "BEBIDAS", "cantidad": 100,
            "largoCm": 60, "anchoCm": 40, "altoCm": 40, "pesoKg": 25,
        }],
    }
    cuerpo.update(extra)
    return cuerpo


# --- POST /recomendaciones --------------------------------------------------


async def test_una_recomendacion_asignada_trae_todos_los_campos_del_contrato(client):
    r = await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo())
    assert r.status_code == 200, r.text
    datos = r.json()

    assert datos["resultado"] == "ASIGNADO"
    assert datos["motivoSinSeleccion"] is None
    assert datos["origenSistema"] == "COTIFLY"
    assert datos["idRecomendacion"]
    assert datos["extradimension"] is None

    vehiculo = datos["vehiculos"][0]
    # Los seis que la seccion 2.3 declara obligatorios y sin null.
    for campo in ("idRecomendacionVehiculo", "codigoVehiculo", "codigoCarroceria",
                  "pesoTotalKg", "volumenTotalM3", "porcentajeLlenadoVolumen",
                  "porcentajeLlenadoPeso"):
        assert vehiculo[campo] is not None, campo

    mercancia = vehiculo["mercancias"][0]
    assert mercancia["codigoMercancia"] == "BEBIDAS"
    assert mercancia["idTipoMercancia"] == ID_BEBIDAS, "el id viaja junto al codigo"


async def test_sin_seleccion_automatica_vuelve_con_200(client):
    """Lo que antes era error ahora es resultado (seccion 2.6)."""
    r = await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo(
        mercancias=[{"codigoMercancia": "BEBIDAS", "cantidad": 2,
                     "largoCm": 1400, "anchoCm": 40, "altoCm": 40, "pesoKg": 25}]))
    assert r.status_code == 200, r.text
    datos = r.json()
    assert datos["resultado"] == "SIN_SELECCION_AUTOMATICA"
    assert datos["motivoSinSeleccion"] == "EXTRADIMENSIONADO"
    assert datos["vehiculos"] == []
    assert datos["extradimension"]["hayExtradimension"] is True
    assert datos["extradimension"]["piezas"][0]["codigoMercancia"] == "BEBIDAS"


async def test_una_mercancia_desconocida_es_400(client):
    r = await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo(
        mercancias=[{"codigoMercancia": "NO_EXISTE", "cantidad": 1,
                     "largoCm": 60, "anchoCm": 40, "altoCm": 40, "pesoKg": 25}]))
    assert r.status_code == 400
    assert r.json()["codigo"] == "MERCANCIA_DESCONOCIDA"


# --- autenticacion y origen -------------------------------------------------


async def test_el_origen_sale_de_la_llave_no_del_cuerpo(client):
    r = await client.post("/recomendaciones", headers=NEXO, json=_cuerpo())
    assert r.status_code == 200, r.text
    assert r.json()["origenSistema"] == "NEXO"


async def test_declarar_un_origen_que_no_corresponde_a_la_llave_es_401(client):
    r = await client.post("/recomendaciones", headers=COTIFLY,
                          json=_cuerpo(origenSistema="NEXO"))
    assert r.status_code == 401
    assert r.json()["detail"]["codigo"] == "NO_AUTORIZADO"


async def test_la_llave_generica_no_sirve_para_recomendaciones(client, monkeypatch):
    """`CUBICADOR_API_KEY` identifica al servicio, no a un sistema: no alcanza
    para una recomendacion, que tiene que saber de quien viene."""
    monkeypatch.setenv("CUBICADOR_API_KEY", "llave-servicio")
    r = await client.post("/recomendaciones", headers=SERVICIO, json=_cuerpo())
    assert r.status_code == 401


async def test_sin_llave_es_401(client):
    r = await client.post("/recomendaciones", json=_cuerpo())
    assert r.status_code == 401


# --- reconsulta -------------------------------------------------------------


async def test_lo_guardado_se_vuelve_a_leer_igual(client):
    creada = (await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo())).json()

    r = await client.get("/recomendaciones", headers=COTIFLY, params={
        "idLineaSolicitud": "LINEA-001", "origenSistema": "COTIFLY"})
    assert r.status_code == 200, r.text
    leida = r.json()

    assert leida["idRecomendacion"] == creada["idRecomendacion"]
    assert leida["vehiculos"] == creada["vehiculos"]
    assert leida["advertencias"] == creada["advertencias"]


async def test_la_reconsulta_separa_los_dos_sistemas(client):
    """Un mismo codigo de linea puede existir en NEXO y en Cotifly y ser rutas
    distintas: sin el filtro por origen, una consulta devolveria la del otro."""
    await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo())

    r = await client.get("/recomendaciones", headers=NEXO, params={
        "idLineaSolicitud": "LINEA-001", "origenSistema": "NEXO"})
    assert r.status_code == 404
    assert r.json()["codigo"] == "RECOMENDACION_NO_ENCONTRADA"


async def test_recalcular_crea_una_corrida_nueva_sin_tocar_la_anterior(client):
    """Append-only (seccion 2.9): la segunda corrida no pisa la primera."""
    primera = (await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo())).json()
    segunda = (await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo(
        mercancias=[{"codigoMercancia": "CARNICOS", "cantidad": 50,
                     "largoCm": 60, "anchoCm": 40, "altoCm": 40, "pesoKg": 25}]))).json()

    assert primera["idRecomendacion"] != segunda["idRecomendacion"]

    # El id que Cotifly guardo de la PRIMERA corrida sigue devolviendo lo suyo.
    id_vehiculo = primera["vehiculos"][0]["idRecomendacionVehiculo"]
    r = await client.get(f"/recomendaciones/{id_vehiculo}/mercancias", headers=COTIFLY)
    assert r.status_code == 200, r.text
    assert r.json()[0]["codigoMercancia"] == "BEBIDAS"

    # Y la reconsulta por linea devuelve la ULTIMA.
    ultima = (await client.get("/recomendaciones", headers=COTIFLY, params={
        "idLineaSolicitud": "LINEA-001", "origenSistema": "COTIFLY"})).json()
    assert ultima["idRecomendacion"] == segunda["idRecomendacion"]


async def test_dos_posiciones_del_mismo_codigo_viajan_separadas(client):
    """Una fila por POSICION, no por codigo (S5-HU-17.8).

    Importa que sobreviva el viaje por la base: el POST arma la respuesta desde
    el motor, pero la reconsulta y `GET /{id}/mercancias` la releen de
    `recomendacionVehiculoMercancia`. Si la posicion no se guardara, esos dos
    caminos devolverian algo distinto al POST.
    """
    cuerpo = {"idLineaSolicitud": "LINEA-POS", "mercancias": [
        {"codigoMercancia": "BEBIDAS", "cantidad": 10,
         "largoCm": 100, "anchoCm": 50, "altoCm": 50, "pesoKg": 20},
        {"codigoMercancia": "BEBIDAS", "cantidad": 5,
         "largoCm": 30, "anchoCm": 30, "altoCm": 30, "pesoKg": 2},
    ]}
    creada = (await client.post("/recomendaciones", headers=COTIFLY, json=cuerpo)).json()
    assert creada["resultado"] == "ASIGNADO"

    filas = [m for v in creada["vehiculos"] for m in v["mercancias"]]
    assert [(m["indicePosicion"], m["cantidad"], m["pesoKg"]) for m in filas] == [
        (0, 10, 200.0), (1, 5, 10.0)]
    assert {m["codigoMercancia"] for m in filas} == {"BEBIDAS"}

    # Dos posiciones del mismo producto NO vuelven heterogeneo el camion.
    assert all(v["heterogenea"] is False for v in creada["vehiculos"])

    # La reconsulta por linea lo relee de la base y dice lo mismo.
    releida = (await client.get("/recomendaciones", headers=COTIFLY, params={
        "idLineaSolicitud": "LINEA-POS", "origenSistema": "COTIFLY"})).json()
    assert [m["indicePosicion"] for v in releida["vehiculos"] for m in v["mercancias"]] == [0, 1]

    # Y el detalle por vehiculo tambien.
    id_vehiculo = creada["vehiculos"][0]["idRecomendacionVehiculo"]
    detalle = (await client.get(
        f"/recomendaciones/{id_vehiculo}/mercancias", headers=COTIFLY)).json()
    assert [(m["indicePosicion"], m["cantidad"]) for m in detalle] == [(0, 10), (1, 5)]


async def test_mercancias_de_un_vehiculo_que_no_existe_es_404(client):
    r = await client.get("/recomendaciones/no-existe/mercancias", headers=COTIFLY)
    assert r.status_code == 404


# --- catalogos y medidas estandar -------------------------------------------


async def test_tipos_mercancia_devuelve_id_y_codigo(client):
    r = await client.get("/tipos-mercancia", headers=COTIFLY)
    assert r.status_code == 200, r.text
    bebidas = next(m for m in r.json() if m["codigoMercancia"] == "BEBIDAS")
    assert bebidas["id"] == ID_BEBIDAS
    assert bebidas["carroceriaPrincipal"] == "FURGON"
    assert bebidas["controlAmbientalRequerido"] == "NINGUNO"


async def test_tipos_carroceria_devuelve_el_catalogo(client):
    r = await client.get("/tipos-carroceria", headers=COTIFLY)
    assert r.status_code == 200, r.text
    assert {c["codigoCarroceria"] for c in r.json()} == {"FURGON", "FURGON_REFRIGERADO"}


async def test_las_medidas_estandar_siguen_siendo_editables(client):
    """Vivian bajo `/api/cubicacion`, que se retiro. Se mudaron, no se
    perdieron: son el tope contra el que se detecta extradimension."""
    r = await client.get("/medidas-estandar", headers=COTIFLY)
    assert r.status_code == 200, r.text

    r = await client.put("/medidas-estandar", headers=COTIFLY, json={
        "largoMaxCm": 1000, "anchoMaxCm": 250, "altoMaxCm": 290, "pesoMaxKg": 30000})
    assert r.status_code == 200, r.text
    assert Decimal(r.json()["largoMaxCm"]) == Decimal(1000)


async def test_el_endpoint_viejo_ya_no_existe(client):
    """`POST /api/cubicacion` se retira en el mismo corte: no queda endpoint
    de transicion (seccion 2.2)."""
    r = await client.post("/api/cubicacion", headers=COTIFLY, json={"items": []})
    assert r.status_code == 404


# --- la caja de los uuid ----------------------------------------------------


async def test_los_ids_salen_en_mayusculas(client):
    """SQL Server devuelve los `uniqueidentifier` en mayusculas, sin forma de
    configurarlo. Si se escribieran en minusculas, el `idRecomendacionVehiculo`
    del POST y el de la reconsulta serian textos distintos para la misma fila -
    y guardar ese id es exactamente para lo que existe. SQLite no lo muestra:
    devuelve lo que se escribio. Por eso se generan ya en mayusculas.
    """
    datos = (await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo())).json()
    assert datos["idRecomendacion"] == datos["idRecomendacion"].upper()
    assert datos["vehiculos"][0]["idRecomendacionVehiculo"] == \
        datos["vehiculos"][0]["idRecomendacionVehiculo"].upper()


async def test_un_codigo_de_mercancia_en_otra_caja_se_resuelve_igual(client):
    """Un consumidor puede guardar el codigo en minusculas de su lado y
    devolverlo asi. Es el mismo codigo: rechazarlo seria un
    `MERCANCIA_DESCONOCIDA` falso."""
    r = await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo(
        mercancias=[{"codigoMercancia": " bebidas ", "cantidad": 10,
                     "largoCm": 60, "anchoCm": 40, "altoCm": 40, "pesoKg": 25}]))
    assert r.status_code == 200, r.text
    assert r.json()["resultado"] == "ASIGNADO"


async def test_el_id_de_vehiculo_se_consulta_en_cualquier_caja(client):
    datos = (await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo())).json()
    id_vehiculo = datos["vehiculos"][0]["idRecomendacionVehiculo"]

    r = await client.get(f"/recomendaciones/{id_vehiculo.lower()}/mercancias", headers=COTIFLY)
    assert r.status_code == 200, r.text
    assert r.json()[0]["codigoMercancia"] == "BEBIDAS"


async def test_los_decimales_viajan_como_numero_no_como_texto(client):
    """La seccion 2.3 muestra `"pesoTotalKg": 8420.0`, un numero. Pydantic
    serializa `Decimal` como cadena por defecto: .NET lo tolera por
    `AllowReadingFromString`, pero un front en JavaScript recibiria "8420.00"
    donde espera un numero y un `toFixed` fallaria en silencio."""
    datos = (await client.post("/recomendaciones", headers=COTIFLY, json=_cuerpo())).json()
    vehiculo = datos["vehiculos"][0]
    for campo in ("pesoTotalKg", "volumenTotalM3",
                  "porcentajeLlenadoVolumen", "porcentajeLlenadoPeso"):
        assert isinstance(vehiculo[campo], (int, float)), f"{campo} llego como {type(vehiculo[campo])}"
    assert isinstance(vehiculo["mercancias"][0]["pesoKg"], (int, float))
