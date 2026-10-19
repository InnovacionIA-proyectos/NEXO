"""Crea y siembra el catalogo en la base LOCAL de desarrollo (S5-HU-17.4).

Las 10 tablas del catalogo las administra el equipo de Datos y solo existen en
QA. El `docker-compose.yml` de esta carpeta levanta un SQL Server propio que no
las tiene, asi que `GET /tipos-carroceria` y `POST /recomendaciones` responden
503 recien levantado el stack. Esto las crea ahi y las siembra con un recorte
del catalogo real, suficiente para desarrollar y para el humo de CI.

**No reemplaza al catalogo de QA ni pretende igualarlo.** Son los 5 vehiculos
reales con sus medidas y las carrocerias que tienen vehiculo asignado, mas unas
pocas mercancias representativas: una de furgon, una refrigerada, una de
plataforma y un par incompatible entre si.

Guarda: **se niega a correr contra cualquier host que no sea local.** El
catalogo de QA es de otro equipo y comparte base con Cotifly; este script crea
tablas, asi que apuntarlo por error a QA es exactamente lo que no puede pasar.

Correr (dentro del contenedor, que es el que tiene el driver ODBC):

    docker compose exec api python scripts/preparar_catalogo_local.py

Con `--recrear` borra las tablas del catalogo y las vuelve a hacer. Hace falta
cuando el modelo gana una columna -las tablas locales las crea `create_all`, que
nunca altera una existente- y son datos de desarrollo: se rehacen en segundos.
"""

from __future__ import annotations

import argparse
import asyncio
import os
import sys
from datetime import datetime, timezone
from decimal import Decimal
from urllib.parse import urlsplit

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
sys.path.insert(0, RAIZ)

from sqlalchemy import func, select  # noqa: E402
from sqlalchemy.ext.asyncio import async_sessionmaker, create_async_engine  # noqa: E402

from infrastructure.persistence import modelos_catalogo as m  # noqa: E402

# El servicio `db` del compose, y las formas de nombrar la maquina propia.
_HOSTS_LOCALES = {"db", "localhost", "127.0.0.1", "::1", "host.docker.internal"}


def _verificar_que_es_local(url: str) -> str:
    partes = urlsplit(url)
    host = (partes.hostname or "").lower()
    if host not in _HOSTS_LOCALES:
        raise SystemExit(
            f"NEGADO: DATABASE_URL apunta a '{host}', que no es una base local.\n"
            "Este script CREA TABLAS. El catalogo de QA lo administra el equipo de Datos\n"
            "y su base la comparte Cotifly: no se toca desde aca.\n"
            f"Hosts permitidos: {', '.join(sorted(_HOSTS_LOCALES))}."
        )
    return host


def _auditoria() -> dict:
    ahora = datetime.now(timezone.utc)
    return {"creado_por": "catalogo-local", "created_at": ahora, "updated_at": ahora}


# (codigo, nombre, largo_cm, ancho_cm, alto_util_cm, capacidad_kg) - los 5 reales.
VEHICULOS = [
    ("2L2", "Turbo 2L2", 450, 220, 230, 4000),
    ("C2", "Sencillo C2", 650, 240, 240, 8500),
    ("C3", "Dobletroque C3", 800, 240, 260, 17000),
    ("2S2", "Minimula 2S2", 1220, 240, 260, 18000),
    ("3S3", "Tractomula 3S3", 1220, 240, 260, 34000),
]

CARROCERIAS = [
    ("FURGON", "Furgon"),
    ("FURGON_REFRIGERADO", "Furgon refrigerado"),
    ("ESTACAS", "Estacas"),
    ("PLATAFORMA", "Plataforma"),
]

# Como en el catalogo real: el C3 NO admite furgon, solo estacas y plataforma.
CARROCERIAS_POR_VEHICULO = {
    "2L2": ["FURGON", "FURGON_REFRIGERADO", "ESTACAS", "PLATAFORMA"],
    "C2": ["FURGON", "FURGON_REFRIGERADO", "ESTACAS", "PLATAFORMA"],
    "C3": ["ESTACAS", "PLATAFORMA"],
    "2S2": ["FURGON", "FURGON_REFRIGERADO", "ESTACAS", "PLATAFORMA"],
    "3S3": ["FURGON", "FURGON_REFRIGERADO", "ESTACAS", "PLATAFORMA"],
}

# El unico override real del catalogo: el furgon pesa.
OVERRIDES = {("3S3", "FURGON"): 30000, ("3S3", "FURGON_REFRIGERADO"): 30000}

CATEGORIAS = ["ALIMENTOS", "GENERAL", "FERTILIZANTE", "METALICO"]

# (codigo, categoria, principal, secundaria, secundaria_condicionada, control, condicionada)
MERCANCIAS = [
    ("BEBIDAS", "ALIMENTOS", "FURGON", "ESTACAS", True, "NINGUNO", False),
    ("CAJAS_CARTON", "GENERAL", "FURGON", "ESTACAS", True, "NINGUNO", False),
    ("ALIMENTOS_SECOS", "ALIMENTOS", "FURGON", "ESTACAS", True, "NINGUNO", False),
    ("CARNICOS", "ALIMENTOS", "FURGON_REFRIGERADO", None, False, "REFRIGERADO", False),
    ("FERTILIZANTES_ENSACADOS", "FERTILIZANTE", "FURGON", "ESTACAS", True, "NINGUNO", False),
    ("BOBINAS_ACERO", "METALICO", "PLATAFORMA", None, False, "NINGUNO", True),
]

PARAMETROS = [
    ("k_max", "25", "Tope de vehiculos por grupo antes de sin seleccion automatica"),
    ("umbral_alerta_llenado", "0.75", "Por debajo de este porcentaje se marca alerta"),
    ("usar_alternativa", "solo_respaldo", "Si la carroceria alternativa entra siempre o solo como respaldo"),
    ("factor_vol_util", "0.90", "Fraccion del volumen util aprovechable en la practica"),
    ("margen_cg", "0.30", "Centro de gravedad dentro de +-30% del centro geometrico"),
]


async def main() -> int:
    parser = argparse.ArgumentParser(description="Catalogo de la base LOCAL.")
    parser.add_argument("--recrear", action="store_true",
                        help="Borra las tablas del catalogo y las vuelve a hacer.")
    args = parser.parse_args()

    url = os.environ.get("DATABASE_URL", "")
    if not url:
        raise SystemExit("Falta DATABASE_URL.")
    host = _verificar_que_es_local(url)
    print(f"Base local: {host}")

    engine = create_async_engine(url, echo=False)
    if args.recrear:
        async with engine.begin() as conn:
            await conn.run_sync(m.BaseCatalogo.metadata.drop_all)
        print("Tablas del catalogo borradas.")
    async with engine.begin() as conn:
        await conn.run_sync(m.BaseCatalogo.metadata.create_all)
    print("Tablas del catalogo creadas (las que faltaran).")

    async with async_sessionmaker(engine, expire_on_commit=False)() as s:
        ya = (await s.execute(select(func.count()).select_from(m.Vehiculo))).scalar_one()
        if ya:
            print(f"El catalogo local ya tiene {ya} vehiculos: no se siembra nada.")
            await engine.dispose()
            return 0

        a = _auditoria()
        for codigo, nombre in CARROCERIAS:
            s.add(m.TipoCarroceria(id=f"c-{codigo}", codigo=codigo, nombre=nombre,
                                   activo=True, **a))
        for codigo, nombre, largo, ancho, alto, capacidad in VEHICULOS:
            s.add(m.Vehiculo(
                id=f"v-{codigo}", codigo=codigo, nombre=nombre, largo_cm=Decimal(largo),
                ancho_cm=Decimal(ancho), alto_util_cm=Decimal(alto),
                capacidad_kg=Decimal(capacidad), activo=True, **a))
            for carroceria in CARROCERIAS_POR_VEHICULO[codigo]:
                propia = OVERRIDES.get((codigo, carroceria))
                s.add(m.RelacionVehiculoCarroceria(
                    id=f"r-{codigo}-{carroceria}", vehiculo_id=f"v-{codigo}",
                    carroceria_id=f"c-{carroceria}",
                    usa_capacidad_del_vehiculo=propia is None,
                    capacidad_especifica_kg=Decimal(propia or 0), **a))

        for categoria in CATEGORIAS:
            s.add(m.CategoriaMercancia(id=f"cat-{categoria}", nombre=categoria, **a))

        for codigo, categoria, principal, secundaria, sec_cond, control, condicionada in MERCANCIAS:
            s.add(m.TipoMercancia(
                id=f"m-{codigo}", codigo=codigo, nombre=codigo.replace("_", " ").title(),
                categoria_id=f"cat-{categoria}", cubicable=True, condicionada=condicionada,
                comentario_condicion=None, carroceria_principal_id=f"c-{principal}",
                carroceria_secundaria_id=f"c-{secundaria}" if secundaria else None,
                secundaria_condicionada=sec_cond, control_ambiental=control,
                apilable=True, fragil=False, rotacion_libre=True, activo=True, **a))

        s.add(m.ReglaIncompatibilidad(
            id="rg-fertilizante-alimentos", categoria_1_id="cat-FERTILIZANTE",
            categoria_2_id="cat-ALIMENTOS",
            motivo="Riesgo de contaminacion de alimentos por fertilizante ensacado.", **a))

        for clave, valor, descripcion in PARAMETROS:
            s.add(m.ParametroMotor(id=f"p-{clave}", clave=clave, valor=valor,
                                   descripcion=descripcion, **a))

        await s.commit()

    print(f"Sembrado: {len(VEHICULOS)} vehiculos, {len(CARROCERIAS)} carrocerias, "
          f"{len(MERCANCIAS)} mercancias, 1 regla y {len(PARAMETROS)} parametros.")
    await engine.dispose()
    return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
