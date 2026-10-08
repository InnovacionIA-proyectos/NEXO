# -*- coding: utf-8 -*-
"""Aplica ESQUEMA_CUBICADOR.sql contra un SQL Server 2019 y carga los 7 CSV
de seeder/ en orden (respetando dependencias de FK).

Uso:
    py ejecutar_schema_y_seeders.py --server <host[,puerto]> [--solo-schema] [--solo-datos]

Requiere sqlcmd en el PATH y autenticacion de Windows (-E) contra el server
indicado -- mismo criterio ya usado en todo el proyecto NEXO. Si el server
usa otra autenticacion, ajustar CMD_BASE abajo.

El esquema (--solo-schema) sigue sin ser idempotente: ESQUEMA_CUBICADOR.sql
solo crea la base si no existe, pero las 9 tablas fallan si ya existen --
correr esa parte una sola vez contra una base vacia, igual que ESQUEMA_NEXO.sql.

La carga de datos (--solo-datos) SI es idempotente: los Id del seeder son
deterministicos (ver seeder/_generar_seeders.py), asi que correr esta parte
de nuevo nunca duplica filas ni pisa una fila que ya exista (por ejemplo,
una corregida a mano por un futuro CRUD) -- solo inserta lo que falta.
"""
import argparse
import csv
import subprocess
import sys
from pathlib import Path

CARPETA = Path(__file__).parent          # cubicador/database/scripts/
DATABASE = CARPETA.parent                # cubicador/database/
ESQUEMA_SQL = DATABASE / "esquema" / "ESQUEMA_CUBICADOR.sql"
SEEDER = DATABASE / "seeder"
TAMANO_LOTE = 100

# Base de datos COMPARTIDA de NEXO (no una base propia del Cubicador) --
# las 9 tablas viven ahi, bajo su propio esquema, para no mezclarse con el
# resto de tablas de esa base.
NOMBRE_BD = "dbTarifario_Nexo_Pruebas"
NOMBRE_ESQUEMA_SQL = "dbo_cubicador"  # el esquema SQL dentro de NOMBRE_BD, no confundir con ESQUEMA_SQL (la ruta al .sql)

# Orden de carga: respeta las FK (categoria/carroceria/vehiculo antes que lo
# que depende de ellas; recomendacionVehiculo* se deja vacio, no trae seeder).
ARCHIVOS_EN_ORDEN = [
    "tiposCarroceria.csv",
    "categoriaMercancia.csv",
    "vehiculos.csv",
    "tiposMercancia.csv",
    "relacionVehiculosCarroceria.csv",
    "reglasIncompatibilidad.csv",
    "parametrosMotor.csv",
]

# Mapa archivo CSV -> tabla real. La mayoria coincide con el nombre del
# archivo (sin .csv); TiposCarroceria lleva mayuscula inicial en la tabla.
TABLA_POR_ARCHIVO = {
    "tiposCarroceria.csv": "TiposCarroceria",
    "categoriaMercancia.csv": "categoriaMercancia",
    "vehiculos.csv": "vehiculos",
    "tiposMercancia.csv": "tiposMercancia",
    "relacionVehiculosCarroceria.csv": "relacionVehiculosCarroceria",
    "reglasIncompatibilidad.csv": "reglasIncompatibilidad",
    "parametrosMotor.csv": "parametrosMotor",
}

def correr(cmd, descripcion):
    print(f"--- {descripcion} ---")
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.stdout.strip():
        print(r.stdout)
    if r.returncode != 0:
        print(r.stderr, file=sys.stderr)
        sys.exit(f"FALLO: {descripcion}")


def aplicar_schema(server):
    cmd = ["sqlcmd", "-S", server, "-d", NOMBRE_BD, "-E", "-C", "-b", "-i", str(ESQUEMA_SQL)]
    correr(cmd, f"Aplicando {ESQUEMA_SQL.name} contra {server}/{NOMBRE_BD}")


# Columnas que son REALMENTE numericas (decimal) en ESQUEMA_CUBICADOR.sql.
# Todo lo que no esta aqui se manda siempre como string (N'...'), aunque el
# texto "parezca" numero -- CodigoVehiculo='2' o parametrosMotor.Valor='4'
# son nvarchar en el esquema real. Adivinar el tipo por el contenido del
# texto (como se hacia antes) rompe el INSERT multi-fila: SQL Server infiere
# un solo tipo por columna para toda la lista VALUES, asi que una fila con
# '2' sin comillas en esa posicion fuerza a convertir TODAS las filas de esa
# columna a int, y revienta con la primera que no es numero (ej. '2L2').
COLUMNAS_NUMERICAS = {
    "vehiculos": {"LargoCm", "AnchoCm", "AltoVehiculoCm", "CapacidadMaxima"},
    "relacionVehiculosCarroceria": {"CapacidadMaximaEspecifica"},
}

# Columnas bit -- se mandan como 1/0 sin comillas (N'true' no es seguro en
# todas las configuraciones), nunca como el numero "adivinado". Apilable,
# Fragil y RotacionLibre son bit NULL -- valor_sql ya manda NULL para
# celdas vacias antes de llegar aqui, no hace falta distinguirlas.
COLUMNAS_BOOLEANAS = {
    "vehiculos": {"Activo"},
    "TiposCarroceria": {"Activo"},
    "tiposMercancia": {"Cubicable", "CondicionadaCubicacion", "CarroceriaSecundariaCondicionada",
                        "Apilable", "Fragil", "RotacionLibre", "Activo"},
    "relacionVehiculosCarroceria": {"UsaCapacidadMaximaDelVehiculo"},
}


def valor_sql(tabla, columna, texto):
    """Formatea un valor de celda CSV como literal T-SQL, segun el tipo
    REAL de la columna en el esquema (ver COLUMNAS_NUMERICAS/BOOLEANAS) --
    no adivinando por el contenido del texto."""
    if texto is None or texto == "":
        return "NULL"
    if columna in COLUMNAS_BOOLEANAS.get(tabla, ()):
        return "1" if texto.strip().lower() == "true" else "0"
    if columna in COLUMNAS_NUMERICAS.get(tabla, ()):
        return texto
    return "N'" + texto.replace("'", "''") + "'"


def ids_existentes(server, tabla):
    """Los Id de _generar_seeders.py son deterministicos (uuid5 sobre la
    clave de negocio, ej. CodigoVehiculo) -- el mismo vehiculo/categoria/etc
    SIEMPRE genera el mismo Id, se corra el generador hoy o en un año. Por
    eso basta con mirar que Id ya existen en la tabla real para saber que
    filas son "nuevas de este seeder" y cuales ya estan (sembradas antes, o
    ya editadas a mano por un CRUD) -- a esas no se les toca nada."""
    query = f"SET NOCOUNT ON; SELECT CAST([Id] AS nvarchar(36)) FROM [{NOMBRE_ESQUEMA_SQL}].[{tabla}];"
    cmd = ["sqlcmd", "-S", server, "-d", NOMBRE_BD, "-E", "-C", "-h", "-1", "-Q", query]
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        print(r.stderr, file=sys.stderr)
        sys.exit(f"FALLO leyendo Id existentes de {tabla}")
    return {linea.strip().lower() for linea in r.stdout.splitlines() if linea.strip()}


def cargar_csv(server, archivo):
    ruta = SEEDER / archivo
    tabla = TABLA_POR_ARCHIVO[archivo]
    with open(ruta, encoding="utf-8-sig") as f:
        filas = list(csv.DictReader(f))
    if not filas:
        print(f"--- {archivo}: sin filas, nada que cargar ---")
        return

    existentes = ids_existentes(server, tabla)
    filas_nuevas = [f for f in filas if f["Id"].strip().lower() not in existentes]
    saltadas = len(filas) - len(filas_nuevas)
    if saltadas:
        print(f"--- {archivo}: {saltadas} fila(s) ya existian en {tabla}, se dejan intactas (no se sobrescriben) ---")
    if not filas_nuevas:
        print(f"--- {archivo}: nada nuevo que insertar en {tabla} ---")
        return

    columnas = list(filas_nuevas[0].keys())
    cols_sql = ", ".join(f"[{c}]" for c in columnas)

    for i in range(0, len(filas_nuevas), TAMANO_LOTE):
        lote = filas_nuevas[i : i + TAMANO_LOTE]
        valores = ",\n".join(
            "(" + ", ".join(valor_sql(tabla, c, fila[c]) for c in columnas) + ")" for fila in lote
        )
        query = f"INSERT INTO [{NOMBRE_ESQUEMA_SQL}].[{tabla}] ({cols_sql}) VALUES\n{valores};"
        cmd = ["sqlcmd", "-S", server, "-d", NOMBRE_BD, "-E", "-C", "-b", "-Q", query]
        correr(cmd, f"{archivo}: filas {i + 1}-{i + len(lote)} de {len(filas_nuevas)} nuevas -> {tabla}")

    print(f"--- {archivo}: {len(filas_nuevas)} fila(s) nueva(s) insertada(s) en {tabla} ---")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--server", required=True, help="host[,puerto] del SQL Server 2019")
    ap.add_argument("--solo-schema", action="store_true", help="solo aplica ESQUEMA_CUBICADOR.sql")
    ap.add_argument("--solo-datos", action="store_true", help="solo carga los CSV (la base ya existe)")
    args = ap.parse_args()

    if not args.solo_datos:
        aplicar_schema(args.server)
    if not args.solo_schema:
        for archivo in ARCHIVOS_EN_ORDEN:
            cargar_csv(args.server, archivo)

    print("Listo.")


if __name__ == "__main__":
    main()
