"""Lee las credenciales del .env de NEXO y abre conexiones con pymssql.

Nunca imprime usuario ni clave: solo servidor, base y esquema.
"""
from __future__ import annotations

import io
import os
import re

import pymssql

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
ENV_NEXO = os.path.join(RAIZ, ".env")


def _leer_env(ruta: str) -> dict[str, str]:
    valores: dict[str, str] = {}
    with io.open(ruta, encoding="utf-8") as f:
        for linea in f:
            linea = linea.strip()
            if not linea or linea.startswith("#") or "=" not in linea:
                continue
            clave, _, valor = linea.partition("=")
            valores[clave.strip()] = valor.strip()
    return valores


def _partes(cadena: str) -> dict[str, str]:
    partes: dict[str, str] = {}
    for trozo in cadena.split(";"):
        if "=" in trozo:
            k, _, v = trozo.partition("=")
            partes[k.strip().lower()] = v.strip()
    return partes


def origen() -> dict[str, object]:
    """Datos del SQL Server CORPORATIVO, desde COPIA_ORIGEN_CONNECTION.

    NO se lee de `ConnectionStrings__Nexo`: esa variable se apunta a la copia
    local para que la app trabaje contra ella, y reusarla aqui haria que los
    scripts comparen la copia contra si misma y den todo por bueno.
    """
    env = _leer_env(os.path.join(RAIZ, "cubicador", ".env"))
    cadena = env.get("COPIA_ORIGEN_CONNECTION")
    if not cadena:
        raise SystemExit(
            "Falta COPIA_ORIGEN_CONNECTION en cubicador/.env (ver .env.example)."
        )
    p = _partes(cadena)
    servidor = p.get("server", "")
    host, _, puerto = servidor.partition(",")
    return {
        "server": host,
        "port": int(puerto or 1433),
        "user": p.get("user id") or p.get("uid") or p.get("user"),
        "password": p.get("password") or p.get("pwd"),
        "database": p.get("database") or p.get("initial catalog"),
        "esquema": _leer_env(ENV_NEXO).get("Database__Esquema", "db_NEXO"),
    }


def destino() -> dict[str, object]:
    """Datos del SQL Server LOCAL del docker-compose del cubicador."""
    env = _leer_env(os.path.join(RAIZ, "cubicador", ".env"))
    return {
        "server": "127.0.0.1",
        "port": 1434,  # el compose publica 1434 -> 1433
        "user": "sa",
        "password": env.get("MSSQL_SA_PASSWORD", ""),
        "database": "dbInformesAutomatizados_Pruebas",
        "esquema": "db_NEXO",
    }


def conectar(cfg: dict[str, object], base: str | None = None):
    return pymssql.connect(
        server=str(cfg["server"]),
        port=int(cfg["port"]),
        user=str(cfg["user"]),
        password=str(cfg["password"]),
        database=base if base is not None else str(cfg["database"]),
        timeout=60,
        login_timeout=30,
    )


def describir(cfg: dict[str, object]) -> str:
    return f"{cfg['server']}:{cfg['port']}/{cfg['database']} esquema {cfg['esquema']}"
