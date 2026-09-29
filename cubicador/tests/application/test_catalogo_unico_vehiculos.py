"""S4-HU-12.1 - el catalogo de vehiculos pasa a ser UNO SOLO.

Antes de esta issue el mismo vehiculo estaba descrito en dos sitios con campos
distintos: `presets()` (dimensiones, capacidad, tara, puerta, ejes) y la
constante CATALOGO de `domain/motor/cubicacion_rapida.py` (clasificacion
SICE/SAP y campos de evaluacion). Ninguna alcanzaba sola, y donde se solapaban
discrepaban.

Lo que estos tests fijan no son numeros bonitos: son las tres cosas que la
duplicacion estaba causando y que no se pueden volver a romper en silencio.

  1. El reefer de 40 y el High Cube seco eran DOS vehiculos emparejados como
     uno. El plan de cargue de una linea con reefer se armaba sobre un
     contenedor seco.
  2. La busqueda del vehiculo era por NOMBRE, que el usuario edita desde la
     interfaz. Renombrar un preset dejaba el plan de cargue atascado.
  3. El dobletroque viajaba al costeo con el codigo "3", que no existe en
     tblSAPTiposVehiculoTarifario: el filtro por vehiculo se caia sin error.
"""

from __future__ import annotations

import pytest
from sqlalchemy import text

from api.dtos.plan_cargue import EjeVehiculoDto, GuardarVehiculoInput
from application.cubicacion_rapida.casos_de_uso import unidad_desde_vehiculo, unidades_evaluables
from application.plan_cargue.mapeos import vehiculo_de_input
from application.excepciones import ValidationFailedException
from application.plan_cargue.vehiculos import guardar_vehiculo, presets
from domain.entidades.entidades import EjeVehiculoRow, VehiculoCubicacion
from infrastructure.persistence.db import SessionLocal
from infrastructure.persistence.repositorios.vehiculo_repo import SqlVehiculoCubicacionRepository


def _vehiculo_de_prueba(**overrides) -> VehiculoCubicacion:
    base = dict(
        nombre="Camion del usuario", largo_interno_mm=6000, ancho_interno_mm=2200,
        alto_interno_mm=2000, capacidad_kg=5000, tara_kg=3000,
        puerta_ancho_mm=2100, puerta_alto_mm=1900, ejes=[EjeVehiculoRow(1000, 5000)],
    )
    base.update(overrides)
    return VehiculoCubicacion(**base)


# --------------------------------------------------------------- la siembra


async def test_la_siembra_trae_diez_vehiculos_con_codigo_unico():
    async with SessionLocal() as sesion:
        catalogo = await SqlVehiculoCubicacionRepository(sesion).listar_con_siembra()

    assert len(catalogo) == 10
    codigos = [v.codigo for v in catalogo]
    assert all(codigos), "todo preset sembrado debe traer codigo"
    assert len(set(codigos)) == len(codigos), "los codigos son la identidad estable: no pueden repetirse"


async def test_el_reefer_y_el_high_cube_son_dos_vehiculos_distintos():
    """El bug que la unificacion cierra: el mapeo viejo emparejaba
    `contenedor_refrigerado_40` con el preset "Contenedor 40 pies High Cube",
    que es SECO. Una linea de carga refrigerada terminaba planificada sobre un
    contenedor sin refrigeracion."""
    async with SessionLocal() as sesion:
        catalogo = await SqlVehiculoCubicacionRepository(sesion).listar_con_siembra()
    por_codigo = {v.codigo: v for v in catalogo}

    reefer = por_codigo["contenedor_refrigerado_40"]
    high_cube = por_codigo["contenedor_40_high_cube"]

    assert reefer.id != high_cube.id
    assert reefer.control_ambiental == "refrigeracion"
    assert high_cube.control_ambiental == "ninguno"
    # Y no son el mismo vehiculo medido distinto: difieren en todo lo fisico.
    assert (reefer.largo_interno_mm, reefer.capacidad_kg) != (high_cube.largo_interno_mm, high_cube.capacidad_kg)


async def test_el_reefer_no_inventa_los_datos_que_no_tiene():
    """Solo existia en el catalogo del motor, que no guarda puerta ni POSICIONES
    de eje. Quedan en None a proposito: copiarlos del contenedor de 40 seco
    seria inventar un dato, y la puerta la usa el motor 3D para decidir si la
    carga entra."""
    reefer = next(v for v in presets() if v.codigo == "contenedor_refrigerado_40")

    assert reefer.puerta_ancho_mm is None
    assert reefer.puerta_alto_mm is None
    assert [e.posicion_mm for e in reefer.ejes] == [None, None]
    # Las capacidades por eje si se conocen: lo que falta es donde esta cada eje.
    assert [e.capacidad_kg for e in reefer.ejes] == [12350, 12350]


async def test_el_dobletroque_usa_un_codigo_sap_que_existe():
    """Viajaba con "3", que no existe en tblSAPTiposVehiculoTarifario. Cuando el
    TipoVehiculo no matchea ningun CODIGO, `IdVehiculoSapAsync` devuelve null y
    el costeo deja de filtrar por vehiculo SIN error ni log: la linea se cotiza
    sin discriminar. SAP tiene C3, activo, descrito literalmente "Dobletroque"."""
    dobletroque = next(v for v in presets() if v.codigo == "dobletroque_estacas")

    assert dobletroque.config_vehiculo == "C3"
    assert dobletroque.config_vehiculo != "3"


# ------------------------------------------------- traduccion al motor rapido


async def test_la_unidad_del_motor_se_deriva_de_la_fila_sin_guardar_el_peso_bruto():
    """La tabla guarda capacidad y tara; el motor piensa en peso bruto. Se deriva
    sumando, no se guarda: si se guardaran los tres podrian contradecirse, que es
    justo la divergencia que esta issue vino a eliminar."""
    sencillo = next(v for v in presets() if v.codigo == "sencillo_estacas")

    unidad = unidad_desde_vehiculo(sencillo)

    assert unidad is not None
    assert unidad.id == "sencillo_estacas", "el id del motor ES el codigo de la fila"
    assert unidad.largo_util_m == 7.2, "milimetros a metros"
    assert unidad.peso_bruto_permitido_kg == 17500  # 8500 de carga + 9000 de tara
    assert unidad.peso_vacio_kg == 9000
    assert unidad.capacidad_carga_kg == sencillo.capacidad_kg


async def test_un_vehiculo_sin_geometria_no_se_ofrece_para_cubicar():
    """Un vehiculo creado desde la interfaz nace sin `geometria` ni
    `control_ambiental`, porque el contrato de POST /api/vehiculos todavia no los
    expone. Se descarta de la evaluacion en vez de asumirle un valor: con carga
    refrigerada, suponer que la tiene y suponer que no son dos errores caros, y
    el primero no se nota hasta que la mercancia llega dañada."""
    async with SessionLocal() as sesion:
        repo = SqlVehiculoCubicacionRepository(sesion)
        await repo.listar_con_siembra()
        await guardar_vehiculo(repo, _vehiculo_de_prueba())

        catalogo = await repo.listar_con_siembra()
        evaluables = await unidades_evaluables(repo)

    assert len(catalogo) == 11, "esta en el catalogo"
    assert len(evaluables) == 10, "pero no se ofrece para cubicar"
    assert "Camion del usuario" not in [u.nombre for u in evaluables]


# ------------------------------------------------------ generacion del codigo


async def test_un_vehiculo_nuevo_recibe_codigo_generado_desde_el_nombre():
    """Un vehiculo creado desde la interfaz llega sin codigo. Si se quedara sin
    el, el motor rapido lo descartaria para siempre y nada lo diria. Se genera
    con la misma regla que produjo los sembrados, para que el catalogo sea
    coherente mire donde se mire."""
    async with SessionLocal() as sesion:
        repo = SqlVehiculoCubicacionRepository(sesion)
        await repo.listar_con_siembra()

        guardado = await guardar_vehiculo(repo, _vehiculo_de_prueba(
            nombre="Camion con estacas", geometria="semicerrada", control_ambiental="ninguno"))

    assert guardado.codigo == "camion_estacas"


async def test_dos_vehiculos_con_el_mismo_nombre_no_chocan_de_codigo():
    """`codigo` es unico en la base: sin desambiguar, el segundo guardado
    reventaria contra el indice."""
    async with SessionLocal() as sesion:
        repo = SqlVehiculoCubicacionRepository(sesion)
        await repo.listar_con_siembra()

        primero = await guardar_vehiculo(repo, _vehiculo_de_prueba(nombre="Camion prestado"))
        segundo = await guardar_vehiculo(repo, _vehiculo_de_prueba(nombre="Camion prestado"))

    assert primero.codigo == "camion_prestado"
    assert segundo.codigo == "camion_prestado_2"


async def test_el_codigo_no_se_recalcula_al_renombrar():
    """Es la propiedad que hace util a `codigo`: se genera una vez y no se
    vuelve a derivar del nombre. Si se recalculara, renombrar le cambiaria la
    identidad al vehiculo y estariamos donde empezamos."""
    async with SessionLocal() as sesion:
        repo = SqlVehiculoCubicacionRepository(sesion)
        await repo.listar_con_siembra()
        guardado = await guardar_vehiculo(repo, _vehiculo_de_prueba(nombre="Camion viejo"))

        guardado.nombre = "Camion con nombre nuevo"
        rebautizado = await guardar_vehiculo(repo, guardado)

    assert rebautizado.codigo == "camion_viejo", "el codigo sobrevive al renombrado"


async def test_editar_por_el_camino_real_no_le_cambia_el_codigo_al_vehiculo():
    """El test anterior reusa el mismo objeto, que ya trae codigo. El camino
    REAL -la cola y el API- reconstruye la entidad desde `GuardarVehiculoInput`,
    que no lo trae: es identidad de este servicio, no de quien llama. Sin mirar
    la fila existente se generaria uno nuevo desde el nombre y se pisaria el que
    ya tenia."""
    entrada = GuardarVehiculoInput(
        nombre="Camion original", largoInternoMm=6000, anchoInternoMm=2200, altoInternoMm=2000,
        capacidadKg=5000, taraKg=3000, puertaAnchoMm=2100, puertaAltoMm=1900,
        puertaPosicion="trasera", ejes=[EjeVehiculoDto(posicionMm=1000, capacidadKg=5000)],
    )
    async with SessionLocal() as sesion:
        repo = SqlVehiculoCubicacionRepository(sesion)
        await repo.listar_con_siembra()
        creado = await guardar_vehiculo(repo, vehiculo_de_input(entrada))

        # Misma fila, nombre nuevo: exactamente lo que llega al renombrar desde
        # la interfaz.
        renombrado = entrada.model_copy(update={"id": creado.id, "nombre": "Camion rebautizado"})
        editado = await guardar_vehiculo(repo, vehiculo_de_input(renombrado))

    assert creado.codigo == "camion_original"
    assert editado.codigo == "camion_original", "el codigo sobrevive a la edicion"
    assert editado.nombre == "Camion rebautizado"


async def test_la_clasificacion_que_manda_nexo_llega_hasta_la_entidad():
    """Sin esto el vehiculo se guardaba sin geometria ni codigo de tarifa aunque
    NEXO los enviara, y quedaba fuera de la cubicacion sin que nada lo dijera."""
    entrada = GuardarVehiculoInput(
        nombre="Furgon nuevo", largoInternoMm=12000, anchoInternoMm=2400, altoInternoMm=2600,
        capacidadKg=25000, taraKg=7000, puertaAnchoMm=2300, puertaAltoMm=2500,
        puertaPosicion="trasera", ejes=[EjeVehiculoDto(posicionMm=1500, capacidadKg=8000)],
        configVehiculo="3S3", carroceria="FURGON", claseViaje="Carga Suelta",
        geometria="cerrada", controlAmbiental="ninguno",
    )
    async with SessionLocal() as sesion:
        repo = SqlVehiculoCubicacionRepository(sesion)
        await repo.listar_con_siembra()

        guardado = await guardar_vehiculo(repo, vehiculo_de_input(entrada))
        evaluables = await unidades_evaluables(repo)

    assert guardado.config_vehiculo == "3S3"
    assert guardado.geometria == "cerrada"
    assert len(evaluables) == 11, "nace completo, asi que si entra a la cubicacion"


# ------------------------------------------- identidad estable frente al nombre


async def test_renombrar_un_vehiculo_no_rompe_la_busqueda():
    """La razon de ser de `codigo`. Antes se buscaba por nombre y el catalogo es
    editable desde la interfaz: renombrar un preset dejaba a
    `crear_plan_desde_linea` sin vehiculo, y el plan de cargue atascado."""
    async with SessionLocal() as sesion:
        repo = SqlVehiculoCubicacionRepository(sesion)
        catalogo = await repo.listar_con_siembra()
        turbo = next(v for v in catalogo if v.codigo == "turbo_estacas")

        turbo.nombre = "Turbo de la flota nueva"
        await repo.guardar(turbo)

        por_codigo = await repo.buscar_por_codigo("turbo_estacas")
        catalogo = await repo.listar_con_siembra()

    assert por_codigo is not None, "el codigo sobrevive al renombrado"
    assert por_codigo.nombre == "Turbo de la flota nueva"
    assert "Turbo con estacas" not in [v.nombre for v in catalogo], (
        "el nombre viejo ya no existe: por eso no sirve como identidad")


async def test_renombrar_un_preset_no_lo_vuelve_a_sembrar():
    """Corolario del anterior. La siembra comparaba por nombre, asi que un preset
    renombrado se consideraba "faltante" y se insertaba de nuevo, duplicado."""
    async with SessionLocal() as sesion:
        repo = SqlVehiculoCubicacionRepository(sesion)
        catalogo = await repo.listar_con_siembra()
        cama_baja = next(v for v in catalogo if v.codigo == "cama_baja")
        cama_baja.nombre = "Lowboy 4 ejes"
        await repo.guardar(cama_baja)

        despues = await repo.listar_con_siembra()

    assert len(despues) == 10, "sigue habiendo 10, no 11"
    assert [v.codigo for v in despues].count("cama_baja") == 1


async def test_la_siembra_adopta_una_fila_anterior_a_la_issue_en_vez_de_duplicarla():
    """Camino de actualizacion real: las filas creadas ANTES de 12.1 no tienen
    codigo, porque la columna nacio nullable para poder agregarse a una tabla ya
    poblada. La siembra las adopta por nombre, una sola vez."""
    async with SessionLocal() as sesion:
        await sesion.execute(text(
            "INSERT INTO vehiculos_cubicacion "
            "(id, nombre, largo_interno_mm, ancho_interno_mm, alto_interno_mm, capacidad_kg, tara_kg, "
            " puerta_posicion, ejes_json, tolerancia_paredes_mm, tolerancia_entre_unidades_mm, es_preset, "
            " created_at, updated_at) "
            "VALUES ('vieja-1', 'Cama baja (lowboy)', 12500, 2600, 1600, 39000, 9000, "
            "'trasera', '[]', 0, 0, 1, '2026-01-01 00:00:00', '2026-01-01 00:00:00')"
        ))
        await sesion.commit()

        catalogo = await SqlVehiculoCubicacionRepository(sesion).listar_con_siembra()

    assert len(catalogo) == 10, "la fila vieja se adopto: no se sembro un duplicado"
    adoptada = next(v for v in catalogo if v.id == "vieja-1")
    assert adoptada.codigo == "cama_baja"


async def test_una_fila_vieja_ya_renombrada_no_se_adivina():
    """Si el usuario habia renombrado la fila antes de 12.1, no hay forma de
    saber a que preset corresponde. Se deja sin codigo y el preset se siembra
    aparte - exactamente lo que pasaba antes de este cambio. No se adivina."""
    async with SessionLocal() as sesion:
        await sesion.execute(text(
            "INSERT INTO vehiculos_cubicacion "
            "(id, nombre, largo_interno_mm, ancho_interno_mm, alto_interno_mm, capacidad_kg, tara_kg, "
            " puerta_posicion, ejes_json, tolerancia_paredes_mm, tolerancia_entre_unidades_mm, es_preset, "
            " created_at, updated_at) "
            "VALUES ('vieja-2', 'Mi turbo personalizado', 6200, 2200, 2000, 6300, 4200, "
            "'trasera', '[]', 0, 0, 1, '2026-01-01 00:00:00', '2026-01-01 00:00:00')"
        ))
        await sesion.commit()

        catalogo = await SqlVehiculoCubicacionRepository(sesion).listar_con_siembra()

    assert len(catalogo) == 11, "los 10 presets mas la fila renombrada"
    renombrada = next(v for v in catalogo if v.id == "vieja-2")
    assert renombrada.codigo is None


# ------------------------------------------------------------- validacion


async def test_una_puerta_desconocida_es_valida_pero_una_puerta_cero_no():
    """None y 0 no son lo mismo: uno es "no sabemos", el otro es una puerta por
    la que no entra nada. La validacion tiene que distinguirlos."""
    async with SessionLocal() as sesion:
        repo = SqlVehiculoCubicacionRepository(sesion)

        sin_puerta = await guardar_vehiculo(repo, _vehiculo_de_prueba(
            nombre="Sin puerta medida", puerta_ancho_mm=None, puerta_alto_mm=None))
        assert sin_puerta.puerta_ancho_mm is None

        with pytest.raises(ValidationFailedException) as error:
            await guardar_vehiculo(repo, _vehiculo_de_prueba(
                nombre="Puerta cero", puerta_ancho_mm=0, puerta_alto_mm=0))
        assert error.value.codigo == "PUERTA_INVALIDA"


async def test_un_eje_sin_posicion_es_valido_pero_uno_fuera_del_vehiculo_no():
    """Mismo criterio que la puerta: al reefer le falta la posicion de sus ejes,
    y eso es un dato ausente, no un dato invalido."""
    async with SessionLocal() as sesion:
        repo = SqlVehiculoCubicacionRepository(sesion)

        sin_posicion = await guardar_vehiculo(repo, _vehiculo_de_prueba(
            nombre="Ejes sin medir", ejes=[EjeVehiculoRow(None, 5000)]))
        assert sin_posicion.ejes[0].posicion_mm is None

        with pytest.raises(ValidationFailedException) as error:
            await guardar_vehiculo(repo, _vehiculo_de_prueba(
                nombre="Eje fuera del vehiculo", ejes=[EjeVehiculoRow(99999, 5000)]))
        assert error.value.codigo == "EJES_INVALIDOS"
