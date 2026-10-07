# -*- coding: utf-8 -*-
"""Aplica ESQUEMA_CUBICADOR.sql contra un SQL Server 2019 y carga los 7 CSV
de seeder/ en orden (respetando dependencias de FK).

Uso:
    py ejecutar_schema_y_seeders.py --server <host[,puerto]> [--solo-schema] [--solo-datos]

Requiere sqlcmd en el PATH y autenticacion de Windows (-E) contra el server
indicado -- mismo criterio ya usado en todo el proyecto NEXO. Si el server
usa otra autenticacion, ajustar CMD_BASE abajo.

Idempotente a medias: ESQUEMA_CUBICADOR.sql solo crea la base si no existe
(IF DB_ID(...) IS NULL), pero las 9 tablas fallan si ya existen -- correr
este script una sola vez contra una base vacia, igual que ESQUEMA_NEXO.sql.
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

# Columnas "de referencia" que cada CSV trae solo para que un humano lo lea
# (nombre resuelto de una FK) -- no existen como columna real en esa tabla,
# no se insertan. Por archivo, porque "CodigoVehiculo" SI es columna real en
# vehiculos.csv pero solo de referencia en relacionVehiculosCarroceria.csv.
COLUMNAS_DE_REFERENCIA_POR_ARCHIVO = {
    "tiposMercancia.csv": {"Categoria_ref", "CarroceriaPrincipal_ref", "CarroceriaSecundaria_ref"},
    "relacionVehiculosCarroceria.csv": {"CodigoVehiculo", "CodigoCarroceria"},
    "reglasIncompatibilidad.csv": {"Categoria1", "Categoria2"},
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
    cmd = ["sqlcmd", "-S", server, "-d", "master", "-E", "-C", "-b", "-i", str(ESQUEMA_SQL)]
    correr(cmd, f"Aplicando {ESQUEMA_SQL.name} contra {server}")


def valor_sql(texto):
    """Formatea un valor de celda CSV como literal T-SQL: NULL si viene
    vacio, 1/0 sin comillas si es booleano (para columnas bit -- mandarlo
    como N'true' no es seguro en todas las configuraciones), el numero tal
    cual sin comillas si parece numero (para columnas decimal/int), y
    N'...' como string en cualquier otro caso."""
    if texto is None or texto == "":
        return "NULL"
    minuscula = texto.strip().lower()
    if minuscula in ("true", "false"):
        return "1" if minuscula == "true" else "0"
    try:
        float(texto)
        return texto
    except ValueError:
        return "N'" + texto.replace("'", "''") + "'"


def cargar_csv(server, archivo):
    ruta = SEEDER / archivo
    tabla = TABLA_POR_ARCHIVO[archivo]
    with open(ruta, encoding="utf-8-sig") as f:
        filas = list(csv.DictReader(f))
    if not filas:
        print(f"--- {archivo}: sin filas, nada que cargar ---")
        return

    excluir = COLUMNAS_DE_REFERENCIA_POR_ARCHIVO.get(archivo, set())
    columnas = [c for c in filas[0].keys() if c not in excluir]
    cols_sql = ", ".join(f"[{c}]" for c in columnas)

    for i in range(0, len(filas), TAMANO_LOTE):
        lote = filas[i : i + TAMANO_LOTE]
        valores = ",\n".join(
            "(" + ", ".join(valor_sql(fila[c]) for c in columnas) + ")" for fila in lote
        )
        query = f"INSERT INTO [dbo].[{tabla}] ({cols_sql}) VALUES\n{valores};"
        cmd = ["sqlcmd", "-S", server, "-d", "cubicador", "-E", "-C", "-b", "-Q", query]
        correr(cmd, f"{archivo}: filas {i + 1}-{i + len(lote)} de {len(filas)} -> {tabla}")

    print(f"--- {archivo}: {len(filas)} filas cargadas en {tabla} ---")


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
