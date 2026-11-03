# -*- coding: utf-8 -*-
"""Crea las tablas del Cubicador y carga su catálogo inicial.

Aplica esquema/ESQUEMA_CUBICADOR.sql (tablas en el esquema dbo) y después carga los 7 CSV de seeder/ en el
orden que exigen las llaves foráneas.

Uso (desde cubicador/database/):
    py scripts/ejecutar_schema_y_seeders.py --server <host[,puerto]> [--solo-schema] [--solo-datos]

    --solo-schema  solo crea las tablas
    --solo-datos   solo carga los CSV (las tablas ya existen)

Requisitos: sqlcmd en el PATH y autenticación de Windows contra el servidor, con un
usuario que pueda crear tablas en la base NOMBRE_BD.

El esquema se aplica una sola vez, contra una base vacía: si las tablas ya existen,
falla. La carga de datos sí se puede repetir: los Id del seeder son fijos, así que
solo inserta las filas que faltan y nunca modifica las que ya existen.
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

# Base y esquema destino. Para otro ambiente, cambiar NOMBRE_BD.
NOMBRE_BD = "dbCubicador_Pruebas"
NOMBRE_ESQUEMA_SQL = "dbo"  # esquema SQL dentro de la base (ESQUEMA_SQL es la ruta del archivo .sql)

# Orden de carga: primero las tablas de las que dependen las demás. Las tablas de
# recomendación y del motor 3D no tienen datos iniciales.
ARCHIVOS_EN_ORDEN = [
    "tiposCarroceria.csv",
    "categoriaMercancia.csv",
    "vehiculos.csv",
    "tiposMercancia.csv",
    "relacionVehiculosCarroceria.csv",
    "reglasIncompatibilidad.csv",
    "parametrosMotor.csv",
]

# Tabla destino de cada CSV (TiposCarroceria es la única con mayúscula inicial).
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


# Columnas numéricas (decimal) del esquema; se envían sin comillas. Todo lo demás
# se envía como texto (N'...') aunque parezca un número: CodigoVehiculo o
# parametrosMotor.Valor son nvarchar. El tipo se declara aquí y no se deduce del
# contenido porque, en un INSERT de varias filas, SQL Server toma un solo tipo por
# columna y un valor como '2' sin comillas haría fallar a '2L2'.
COLUMNAS_NUMERICAS = {
    "vehiculos": {"LargoCm", "AnchoCm", "AlturaMaximaReglamentariaCm", "CapacidadMaxima"},
    "relacionVehiculosCarroceria": {"CapacidadMaximaEspecifica"},
}

# Columnas bit: el CSV trae true/false y se envían como 1/0. Una celda vacía se
# envía como NULL.
COLUMNAS_BOOLEANAS = {
    "vehiculos": {"Activo"},
    "TiposCarroceria": {"Activo"},
    "tiposMercancia": {"Cubicable", "CondicionadaCubicacion", "CarroceriaSecundariaCondicionada",
                        "Apilable", "Fragil", "RotacionLibre", "Activo"},
    "relacionVehiculosCarroceria": {"UsaCapacidadMaximaDelVehiculo"},
}


def valor_sql(tabla, columna, texto):
    """Convierte una celda del CSV en un literal T-SQL según el tipo de su columna."""
    if texto is None or texto == "":
        return "NULL"
    if columna in COLUMNAS_BOOLEANAS.get(tabla, ()):
        return "1" if texto.strip().lower() == "true" else "0"
    if columna in COLUMNAS_NUMERICAS.get(tabla, ()):
        return texto
    return "N'" + texto.replace("'", "''") + "'"


def ids_existentes(server, tabla):
    """Id que ya existen en la tabla. Como los Id del seeder son fijos, una fila
    cuyo Id ya está se considera cargada y no se toca (aunque se haya editado
    después desde la aplicación)."""
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
