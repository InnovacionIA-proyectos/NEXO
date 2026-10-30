# -*- coding: utf-8 -*-
"""Crea las tablas de NEXO y carga sus datos iniciales.

Aplica esquema/ESQUEMA_NEXO.sql, después seeder/datos_base_nexo.sql y por último
los CSV de seeder/ en el orden que exigen las llaves foráneas.

Uso (desde docs/Database/):
    py scripts/ejecutar_schema_y_seeders.py --server <host[,puerto]> [--solo-schema] [--solo-datos]

    --solo-schema  solo crea las tablas
    --solo-datos   solo carga los datos iniciales (las tablas ya existen)

Requisitos: sqlcmd en el PATH y autenticación de Windows contra el servidor, con un
usuario que pueda crear tablas en la base NOMBRE_BD.

El esquema se aplica una sola vez, contra una base vacía: si las tablas ya existen,
falla. La carga de datos sí se puede repetir: datos_base_nexo.sql solo inserta lo que
falta, y cada CSV se carga solo si su tabla está vacía.

Precios de venta de los servicios conexos: si existe
seeder/servicios_conexos_precios.local.csv (archivo local, no versionado), se carga
ese en lugar de seeder/servicios_conexos.csv.
"""
import argparse
import csv
import subprocess
import sys
import tempfile
from pathlib import Path

CARPETA = Path(__file__).parent          # docs/Database/scripts/
DATABASE = CARPETA.parent                # docs/Database/
ESQUEMA_SQL = DATABASE / "esquema" / "ESQUEMA_NEXO.sql"
SEEDER = DATABASE / "seeder"
DATOS_BASE_SQL = SEEDER / "datos_base_nexo.sql"
TAMANO_LOTE = 200

# Base y esquema destino. Para otro ambiente, cambiar NOMBRE_BD.
NOMBRE_BD = "dbTarifario_Nexo_Pruebas"
NOMBRE_ESQUEMA_SQL = "dbo"

# CSV -> tabla, en orden de carga: primero las tablas de las que dependen las demás
# (tarifas_plaza apunta a municipios y pesos; rangos_peso_contenedor, al mapeo de
# vehículos). Las tablas de solicitudes, cotizaciones y demás historial no tienen
# datos iniciales.
CARGAS = [
    ("municipios.csv", "tbl_municipios"),
    ("pesos.csv", "tbl_pesos"),
    ("reglas_solo_sicetac.csv", "tbl_reglas_solo_sicetac"),
    ("tarifas_plaza.csv", "tbl_tarifas_plaza"),
    ("servicios_conexos.csv", "tbl_servicios_conexos"),
    ("alias_elementos.csv", "tbl_alias_elementos"),
    ("mapeo_vehiculos.csv", "tbl_mapeo_vehiculo_tarifario"),
    ("rangos_peso_contenedor.csv", "tbl_rangos_peso_contenedor"),
    ("tipos_carroceria_catalogo.csv", "tbl_tipos_carroceria_catalogo"),
]
PRECIOS_LOCAL = "servicios_conexos_precios.local.csv"

TIPOS_NUMERICOS = {"int", "bigint", "smallint", "tinyint", "decimal", "numeric", "float", "real", "money"}


def sqlcmd(*argumentos):
    return ["sqlcmd", "-S", ARGS.server, "-d", NOMBRE_BD, "-E", "-C", "-b", "-f", "65001", *argumentos]


def correr(cmd, descripcion):
    print(f"--- {descripcion} ---")
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.stdout.strip():
        print(r.stdout.strip())
    if r.returncode != 0:
        print(r.stderr, file=sys.stderr)
        sys.exit(f"FALLO: {descripcion}")
    return r.stdout


def consultar(query):
    r = subprocess.run(sqlcmd("-h", "-1", "-W", "-s", "|", "-Q", "SET NOCOUNT ON; " + query),
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        print(r.stdout, r.stderr, file=sys.stderr)
        sys.exit("FALLO consultando la base")
    return [linea.split("|") for linea in r.stdout.splitlines() if linea.strip()]


def columnas_de(tabla):
    """Tipo de cada columna de la tabla y si el Id es autoincremental o no tiene valor por defecto."""
    filas = consultar(
        f"SELECT c.name, ty.name, c.is_identity, CASE WHEN c.default_object_id = 0 THEN 0 ELSE 1 END "
        f"FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id "
        f"WHERE c.object_id = OBJECT_ID(N'{NOMBRE_ESQUEMA_SQL}.{tabla}')")
    return {nombre: {"tipo": tipo, "identidad": ident == "1", "con_defecto": defecto == "1"}
            for nombre, tipo, ident, defecto in filas}


def literal(valor, tipo):
    """Convierte una celda del CSV en un literal T-SQL según el tipo de su columna."""
    if valor is None or valor.strip() == "":
        return "NULL"
    if tipo == "bit":
        return "1" if valor.strip().lower() in ("true", "1", "si", "sí") else "0"
    if tipo in TIPOS_NUMERICOS:
        return valor.strip()
    return "N'" + valor.replace("'", "''") + "'"


def aplicar(archivo, descripcion):
    correr(sqlcmd("-i", str(archivo)), descripcion)


def cargar_csv(archivo, tabla):
    ruta = SEEDER / archivo
    if archivo == "servicios_conexos.csv" and (SEEDER / PRECIOS_LOCAL).exists():
        ruta = SEEDER / PRECIOS_LOCAL
        print(f"--- {archivo}: se usa {PRECIOS_LOCAL} (con precios de venta) ---")

    existentes = consultar(f"SELECT COUNT(*) FROM {NOMBRE_ESQUEMA_SQL}.{tabla}")[0][0]
    if existentes != "0":
        print(f"--- {tabla}: ya tiene {existentes} filas, se deja igual ---")
        return

    with open(ruta, encoding="utf-8-sig", newline="") as f:
        filas = list(csv.DictReader(f))
    if not filas:
        print(f"--- {ruta.name}: sin filas ---")
        return

    tipos = columnas_de(tabla)
    columnas = list(filas[0].keys())
    faltan = [c for c in columnas if c not in tipos]
    if faltan:
        sys.exit(f"FALLO: {ruta.name} trae columnas que {tabla} no tiene: {faltan}")

    # Un Id uniqueidentifier sin valor por defecto que el CSV no trae se genera aquí.
    generar_id = "Id" not in columnas and "Id" in tipos and tipos["Id"]["tipo"] == "uniqueidentifier" \
        and not tipos["Id"]["con_defecto"]
    identidad = any(tipos[c]["identidad"] for c in columnas)

    nombres = (["[Id]"] if generar_id else []) + [f"[{c}]" for c in columnas]
    for inicio in range(0, len(filas), TAMANO_LOTE):
        lote = filas[inicio:inicio + TAMANO_LOTE]
        valores = ",\n".join(
            "(" + ", ".join((["NEWID()"] if generar_id else []) + [literal(fila[c], tipos[c]["tipo"]) for c in columnas]) + ")"
            for fila in lote)
        sentencia = f"INSERT INTO [{NOMBRE_ESQUEMA_SQL}].[{tabla}] ({', '.join(nombres)}) VALUES\n{valores};"
        if identidad:
            sentencia = (f"SET IDENTITY_INSERT [{NOMBRE_ESQUEMA_SQL}].[{tabla}] ON;\n{sentencia}\n"
                         f"SET IDENTITY_INSERT [{NOMBRE_ESQUEMA_SQL}].[{tabla}] OFF;")
        with tempfile.NamedTemporaryFile("w", suffix=".sql", encoding="utf-8", delete=False) as tmp:
            # Los índices filtrados exigen estas opciones y sqlcmd no las trae activas.
            tmp.write("SET NOCOUNT ON;\nSET ANSI_NULLS ON;\nSET QUOTED_IDENTIFIER ON;\n" + sentencia + "\n")
        try:
            correr(sqlcmd("-i", tmp.name), f"{ruta.name}: filas {inicio + 1}-{inicio + len(lote)} de {len(filas)} -> {tabla}")
        finally:
            Path(tmp.name).unlink(missing_ok=True)
    print(f"--- {tabla}: {len(filas)} filas cargadas ---")


def main():
    if not ARGS.solo_datos:
        aplicar(ESQUEMA_SQL, f"Aplicando {ESQUEMA_SQL.name} contra {ARGS.server}/{NOMBRE_BD}")
    if not ARGS.solo_schema:
        aplicar(DATOS_BASE_SQL, f"Aplicando {DATOS_BASE_SQL.name}")
        for archivo, tabla in CARGAS:
            cargar_csv(archivo, tabla)
    print("Listo.")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--server", required=True, help="host[,puerto] del SQL Server")
    ap.add_argument("--solo-schema", action="store_true", help="solo aplica ESQUEMA_NEXO.sql")
    ap.add_argument("--solo-datos", action="store_true", help="solo carga los datos iniciales (las tablas ya existen)")
    ARGS = ap.parse_args()
    main()
