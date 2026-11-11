# -*- coding: utf-8 -*-
"""Valida de punta a punta el esquema + seeders de la PR #454 contra una SQL Server
desechable (Docker), replicando la MISMA logica de ejecutar_schema_y_seeders.py
(generar_id/IDENTITY_INSERT) pero via pyodbc con auth SQL en vez de sqlcmd -E
(auth de Windows), que no aplica contra un contenedor Docker con auth SQL.
"""
import csv
import sys
from pathlib import Path

import pyodbc

SERVER = "localhost,15454"
DATABASE = "dbTarifario_Nexo_Pruebas"
USER = "sa"
PASSWORD = "Pr454Pass!2026"

ROOT = Path(__file__).parent
ESQUEMA_SQL = ROOT / "database" / "esquema" / "ESQUEMA_NEXO.sql"
SEEDER = ROOT / "database" / "seeder"
DATOS_BASE_SQL = SEEDER / "datos_base_nexo.sql"

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
    ("configuraciones_vehiculo_oficiales.csv", "tbl_configuraciones_vehiculo"),
    ("diccionario_cliente.csv", "tbl_diccionario_cliente"),
]
TIPOS_NUMERICOS = {"int", "bigint", "smallint", "tinyint", "decimal", "numeric", "float", "real", "money"}


def conectar():
    cs = (f"DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={SERVER};DATABASE={DATABASE};"
          f"UID={USER};PWD={PASSWORD};TrustServerCertificate=yes")
    cn = pyodbc.connect(cs, autocommit=True)
    return cn


def correr_script(cn, texto, descripcion):
    print(f"--- {descripcion} ---")
    import re
    cur = cn.cursor()
    lotes = re.split(r"(?im)^\s*GO\s*$", texto)
    for lote in lotes:
        lote = lote.strip()
        if not lote:
            continue
        cur.execute(lote)
        while cur.nextset():
            pass


def columnas_de(cn, tabla):
    cur = cn.cursor()
    cur.execute(
        "SELECT c.name, ty.name, c.is_identity, CASE WHEN c.default_object_id = 0 THEN 0 ELSE 1 END "
        "FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id "
        f"WHERE c.object_id = OBJECT_ID(N'dbo.{tabla}')")
    filas = cur.fetchall()
    return {r[0]: {"tipo": r[1], "identidad": bool(r[2]), "con_defecto": bool(r[3])} for r in filas}


def literal(valor, tipo):
    if valor is None or valor.strip() == "":
        return "NULL"
    if tipo == "bit":
        return "1" if valor.strip().lower() in ("true", "1", "si", "sí") else "0"
    if tipo in TIPOS_NUMERICOS:
        return valor.strip()
    return "N'" + valor.replace("'", "''") + "'"


def cargar_csv(cn, archivo, tabla):
    ruta = SEEDER / archivo
    cur = cn.cursor()
    cur.execute(f"SELECT COUNT(*) FROM dbo.{tabla}")
    existentes = cur.fetchone()[0]
    if existentes != 0:
        print(f"--- {tabla}: ya tiene {existentes} filas, se deja igual ---")
        return

    with open(ruta, encoding="utf-8-sig", newline="") as f:
        filas = list(csv.DictReader(f))
    if not filas:
        print(f"--- {ruta.name}: sin filas ---")
        return

    tipos = columnas_de(cn, tabla)
    columnas = list(filas[0].keys())
    faltan = [c for c in columnas if c not in tipos]
    if faltan:
        sys.exit(f"FALLO: {ruta.name} trae columnas que {tabla} no tiene: {faltan}")

    generar_id = "Id" not in columnas and "Id" in tipos and tipos["Id"]["tipo"] == "uniqueidentifier" \
        and not tipos["Id"]["con_defecto"]
    identidad = any(tipos[c]["identidad"] for c in columnas)

    nombres = (["[Id]"] if generar_id else []) + [f"[{c}]" for c in columnas]
    TAMANO_LOTE = 200
    for inicio in range(0, len(filas), TAMANO_LOTE):
        lote = filas[inicio:inicio + TAMANO_LOTE]
        valores = ",\n".join(
            "(" + ", ".join((["NEWID()"] if generar_id else []) + [literal(fila[c], tipos[c]["tipo"]) for c in columnas]) + ")"
            for fila in lote)
        sentencia = f"INSERT INTO [dbo].[{tabla}] ({', '.join(nombres)}) VALUES\n{valores};"
        if identidad:
            sentencia = f"SET IDENTITY_INSERT [dbo].[{tabla}] ON;\n{sentencia}\nSET IDENTITY_INSERT [dbo].[{tabla}] OFF;"
        cur.execute("SET ANSI_NULLS ON; " + sentencia)
    print(f"--- {tabla}: {len(filas)} filas cargadas ---")


def main():
    cn = conectar()
    esquema = ESQUEMA_SQL.read_text(encoding="utf-8-sig")
    correr_script(cn, esquema, f"Aplicando {ESQUEMA_SQL.name}")

    cur = cn.cursor()
    cur.execute("SELECT COUNT(*) FROM sys.tables")
    print(f"tablas creadas: {cur.fetchone()[0]}")

    datos_base = DATOS_BASE_SQL.read_text(encoding="utf-8-sig")
    correr_script(cn, datos_base, f"Aplicando {DATOS_BASE_SQL.name}")

    for archivo, tabla in CARGAS:
        cargar_csv(cn, archivo, tabla)

    cur.execute("SELECT COUNT(*) FROM dbo.tbl_configuraciones_vehiculo")
    print(f"tbl_configuraciones_vehiculo: {cur.fetchone()[0]} filas")
    cur.execute("SELECT COUNT(*) FROM dbo.tbl_alias_elementos")
    print(f"tbl_alias_elementos: {cur.fetchone()[0]} filas")
    cur.execute("SELECT COUNT(DISTINCT AliasNormalizado) FROM dbo.tbl_alias_elementos")
    print(f"tbl_alias_elementos AliasNormalizado distintos: {cur.fetchone()[0]} (debe ser igual al total, sin duplicados)")
    print("RESULTADO: TODO OK")


if __name__ == "__main__":
    main()
