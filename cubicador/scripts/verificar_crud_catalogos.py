"""Verifica los CRUD de catalogo contra el servicio corriendo (S5-HU-17.5).

Recorre los criterios de aceptacion de la issue y la tabla de validaciones
propias de cada catalogo, por HTTP, contra SQL Server real. Las pruebas de
pytest cubren lo mismo contra SQLite; esto cubre lo que solo se ve con el motor
de verdad: que el `UNIQUE` y los `CHECK` de la base salgan traducidos, que la
columna `Version` se comporte, y que las llaves foraneas bloqueen el borrado.

    ./.venv/Scripts/python.exe cubicador/scripts/verificar_crud_catalogos.py

**Escribe en el catalogo**, asi que se niega a correr contra un servicio que no
sea local: apuntarlo a QA tocaria el catalogo que administra el equipo de Datos.
Las filas que crea llevan el prefijo `PRUEBA_` y se borran al terminar; las que
ya existen solo se leen o se intenta borrarlas esperando el 409.
"""

from __future__ import annotations

import io
import json
import os
import urllib.error
import urllib.request
from urllib.parse import urlsplit

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))

_fallos: list[str] = []
_HOSTS_LOCALES = {"localhost", "127.0.0.1", "::1"}
USUARIO = "verificador"


def _env() -> dict[str, str]:
    valores: dict[str, str] = {}
    with io.open(os.path.join(RAIZ, ".env"), encoding="utf-8") as f:
        for linea in f:
            linea = linea.strip()
            if linea and not linea.startswith("#") and "=" in linea:
                clave, _, valor = linea.partition("=")
                valores[clave.strip()] = valor.strip()
    return valores


ENV = _env()
BASE = "http://localhost:" + ENV.get("CUBICADOR_HOST_PORT", "8081")
LLAVE = ENV.get("CUBICADOR_API_KEY", "")


def pedir(metodo: str, ruta: str, cuerpo: dict | None = None):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    peticion = urllib.request.Request(BASE + ruta, data=datos, method=metodo)
    peticion.add_header("Content-Type", "application/json")
    if LLAVE:
        peticion.add_header("Authorization", "Bearer " + LLAVE)
    try:
        with urllib.request.urlopen(peticion, timeout=60) as r:
            crudo = r.read()
            return r.status, (json.loads(crudo) if crudo else None)
    except urllib.error.HTTPError as e:
        crudo = e.read()
        return e.code, (json.loads(crudo) if crudo else None)


def revisar(titulo: str, condicion: bool, detalle: str = "") -> bool:
    marca = "OK   " if condicion else "FALLA"
    print("  " + marca + "  " + titulo + ("  [" + detalle + "]" if detalle else ""))
    if not condicion:
        _fallos.append(titulo)
    return condicion


def _buscar(ruta: str, clave: str, valor: str):
    _, filas = pedir("GET", ruta)
    return next((f for f in (filas or []) if f.get(clave) == valor), None)


def main() -> int:
    host = (urlsplit(BASE).hostname or "").lower()
    if host not in _HOSTS_LOCALES:
        raise SystemExit(
            f"NEGADO: {BASE} no es un servicio local.\n"
            "Este script CREA Y BORRA filas de catalogo. El de QA lo administra\n"
            "el equipo de Datos: no se toca desde aca."
        )

    print("Servicio: " + BASE + "\n")
    creados: list[tuple[str, str, int]] = []

    # --- 1. Parametros del motor ------------------------------------------
    print("1. Parametros del motor - solo GET y PUT por clave")
    estado, parametros = pedir("GET", "/parametros-motor")
    revisar("GET /parametros-motor responde 200", estado == 200, str(estado))
    revisar("cada parametro trae su version",
            bool(parametros) and all("version" in p for p in parametros))
    k_max = next((p for p in parametros if p["clave"] == "k_max"), None)
    revisar("existe k_max", k_max is not None)

    estado, _ = pedir("POST", "/parametros-motor", {})
    revisar("no se pueden crear parametros", estado == 405, str(estado))
    estado, _ = pedir("DELETE", "/parametros-motor/k_max", {})
    revisar("no se pueden borrar parametros", estado == 405, str(estado))

    if k_max:
        estado, act = pedir("PUT", "/parametros-motor/k_max", {
            "valor": k_max["valor"], "usuarioId": USUARIO, "version": k_max["version"]})
        revisar("PUT por clave sube la version",
                estado == 200 and act["version"] == k_max["version"] + 1, str(estado))
        estado, err = pedir("PUT", "/parametros-motor/k_max", {
            "valor": "99", "usuarioId": USUARIO, "version": k_max["version"]})
        revisar("PUT con version vieja es 409",
                estado == 409 and err["codigo"] == "CONFLICTO_DE_VERSION", str(estado))
    estado, _ = pedir("PUT", "/parametros-motor/no_existe", {
        "valor": "1", "usuarioId": USUARIO, "version": 1})
    revisar("PUT de una clave que no existe es 404", estado == 404, str(estado))

    # --- 2. Categorias de mercancia ---------------------------------------
    print("\n2. Categorias - borrado bloqueado si algo la referencia")
    estado, nueva = pedir("POST", "/categorias-mercancia",
                          {"nombreCategoria": "PRUEBA_CAT", "usuarioId": USUARIO})
    revisar("POST crea con version 1", estado == 201 and nueva["version"] == 1, str(estado))
    if estado == 201:
        creados.append(("/categorias-mercancia", nueva["id"], nueva["version"]))

    usada = _buscar("/categorias-mercancia", "nombreCategoria", "ALIMENTOS")
    if usada:
        estado, err = pedir("DELETE", f"/categorias-mercancia/{usada['id']}",
                            {"usuarioId": USUARIO, "version": usada["version"]})
        revisar("no se borra una categoria referenciada",
                estado == 409 and err["codigo"] == "CATALOGO_REFERENCIADO", str(estado))
        revisar("el mensaje dice de donde vienen las referencias",
                bool(err) and ("mercancias" in err["mensaje"] or "reglas" in err["mensaje"]))

    # --- 3. Tipos de carroceria -------------------------------------------
    print("\n3. Carrocerias - borrado bloqueado, codigo unico")
    estado, carroceria = pedir("POST", "/tipos-carroceria", {
        "codigoCarroceria": "PRUEBA_CARR", "nombreCarroceria": "Prueba", "usuarioId": USUARIO})
    revisar("POST crea", estado == 201, str(estado))
    if estado == 201:
        creados.append(("/tipos-carroceria", carroceria["id"], carroceria["version"]))

    estado, _ = pedir("POST", "/tipos-carroceria", {
        "codigoCarroceria": "PRUEBA_CARR", "nombreCarroceria": "Repetida",
        "usuarioId": USUARIO})
    revisar("un codigo repetido choca con el UNIQUE y sale como 409", estado == 409, str(estado))

    usada = _buscar("/tipos-carroceria", "codigoCarroceria", "FURGON")
    if usada:
        estado, err = pedir("DELETE", f"/tipos-carroceria/{usada['id']}",
                            {"usuarioId": USUARIO, "version": usada["version"]})
        revisar("no se borra una carroceria que un vehiculo usa",
                estado == 409 and err["codigo"] == "CATALOGO_REFERENCIADO", str(estado))

    # --- 4. Tipos de mercancia --------------------------------------------
    print("\n4. Mercancias - secundaria distinta de principal, condicionada coherente")
    furgon = _buscar("/tipos-carroceria", "codigoCarroceria", "FURGON")
    estacas = _buscar("/tipos-carroceria", "codigoCarroceria", "ESTACAS")
    categoria = _buscar("/categorias-mercancia", "nombreCategoria", "ALIMENTOS")

    if furgon and estacas and categoria:
        base = {"codigoMercancia": "PRUEBA_MERC", "nombreMercancia": "Prueba",
                "idCategoria": categoria["id"], "idCarroceriaPrincipal": furgon["id"],
                "usuarioId": USUARIO}

        estado, err = pedir("POST", "/tipos-mercancia",
                            dict(base, idCarroceriaSecundaria=furgon["id"]))
        revisar("la secundaria no puede ser la principal",
                estado == 400 and err["codigo"] == "CARROCERIA_SECUNDARIA_IGUAL", str(estado))

        estado, err = pedir("POST", "/tipos-mercancia",
                            dict(base, carroceriaSecundariaCondicionada=True))
        revisar("sin secundaria, la bandera condicionada va en falso",
                estado == 400 and err["codigo"] == "CONDICIONADA_SIN_SECUNDARIA", str(estado))

        estado, mercancia = pedir("POST", "/tipos-mercancia",
                                  dict(base, idCarroceriaSecundaria=estacas["id"]))
        revisar("una mercancia valida se crea", estado == 201, str(estado))
        if estado == 201:
            creados.append(("/tipos-mercancia", mercancia["id"], mercancia["version"]))

    # --- 5. Vehiculos y sus carrocerias ------------------------------------
    print("\n5. Vehiculos - un payload, dominancia y capacidad especifica")
    estado, vehiculos = pedir("GET", "/vehiculos-catalogo")
    revisar("GET trae cada vehiculo con sus carrocerias",
            estado == 200 and bool(vehiculos) and all("carrocerias" in v for v in vehiculos),
            str(estado))

    if furgon and estacas:
        base = {"codigoVehiculo": "PRUEBA_VEH", "nombreVehiculo": "Prueba",
                "largoCm": 500, "anchoCm": 230, "altoUtilCm": 230,
                "capacidadMaximaKg": 4500, "usuarioId": USUARIO,
                "carrocerias": [{"idCarroceria": furgon["id"],
                                 "usaCapacidadMaximaDelVehiculo": True}]}

        estado, err = pedir("POST", "/vehiculos-catalogo", dict(base, carrocerias=[]))
        revisar("un vehiculo sin carrocerias se rechaza",
                estado == 400 and err["codigo"] == "SIN_CARROCERIAS", str(estado))

        estado, err = pedir("POST", "/vehiculos-catalogo", dict(base, carrocerias=[
            {"idCarroceria": furgon["id"], "usaCapacidadMaximaDelVehiculo": False,
             "capacidadMaximaEspecifica": 0}]))
        revisar("capacidad especifica en cero sin usar la del vehiculo se rechaza",
                estado == 400 and err["codigo"] == "CAPACIDAD_ESPECIFICA_INVALIDA", str(estado))

        estado, err = pedir("POST", "/vehiculos-catalogo", dict(
            base, codigoVehiculo="PRUEBA_DOM", largoCm=1400, anchoCm=250,
            altoUtilCm=270, capacidadMaximaKg=1000))
        revisar("un vehiculo que rompe la dominancia se rechaza",
                estado == 400 and err["codigo"] == "DOMINANCIA_ROTA", str(estado))

        estado, vehiculo = pedir("POST", "/vehiculos-catalogo", base)
        revisar("un vehiculo valido se crea con sus carrocerias",
                estado == 201 and len(vehiculo["carrocerias"]) == 1, str(estado))
        if estado == 201:
            estado, act = pedir("PUT", f"/vehiculos-catalogo/{vehiculo['id']}", dict(
                base, version=vehiculo["version"],
                carrocerias=[{"idCarroceria": estacas["id"],
                              "usaCapacidadMaximaDelVehiculo": True}]))
            revisar("el backend reconcilia las carrocerias del payload",
                    estado == 200 and [c["codigoCarroceria"] for c in act["carrocerias"]]
                    == ["ESTACAS"], str(estado))
            revisar("cambiar solo las carrocerias sube la version del vehiculo",
                    estado == 200 and act["version"] == vehiculo["version"] + 1)
            creados.append(("/vehiculos-catalogo", vehiculo["id"], act["version"]
                            if estado == 200 else vehiculo["version"]))

    # --- 6. Reglas de incompatibilidad ------------------------------------
    print("\n6. Reglas - sin PUT, par normalizado")
    estado, _ = pedir("PUT", "/reglas-incompatibilidad/lo-que-sea", {})
    revisar("las reglas no tienen PUT", estado == 405, str(estado))

    _, categorias = pedir("GET", "/categorias-mercancia")
    libres = [c for c in categorias if c["nombreCategoria"].startswith("PRUEBA_")]
    otra = next((c for c in categorias if not c["nombreCategoria"].startswith("PRUEBA_")), None)

    if libres and otra:
        a, b = libres[0]["id"], otra["id"]
        estado, err = pedir("POST", "/reglas-incompatibilidad", {
            "idCategoria1": a, "idCategoria2": a, "motivo": "consigo misma",
            "usuarioId": USUARIO})
        revisar("una categoria no es incompatible consigo misma",
                estado == 400 and err["codigo"] == "CATEGORIAS_IGUALES", str(estado))

        estado, regla = pedir("POST", "/reglas-incompatibilidad", {
            "idCategoria1": a, "idCategoria2": b, "motivo": "prueba", "usuarioId": USUARIO})
        revisar("una regla valida se crea", estado == 201, str(estado))
        if estado == 201:
            revisar("el par queda ordenado",
                    regla["idCategoria1"] <= regla["idCategoria2"])
            estado, err = pedir("POST", "/reglas-incompatibilidad", {
                "idCategoria1": b, "idCategoria2": a, "motivo": "al reves",
                "usuarioId": USUARIO})
            revisar("no se admite el par invertido",
                    estado == 409 and err["codigo"] == "REGLA_DUPLICADA", str(estado))
            pedir("DELETE", f"/reglas-incompatibilidad/{regla['id']}",
                  {"usuarioId": USUARIO, "version": regla["version"]})

    # --- limpieza ----------------------------------------------------------
    print("\nLimpieza de lo creado por esta corrida")
    for ruta, id_, version in reversed(creados):
        estado, _ = pedir("DELETE", f"{ruta}/{id_}", {"usuarioId": USUARIO, "version": version})
        revisar(f"borrada la fila de prueba en {ruta}", estado in (204, 404), str(estado))

    print()
    if _fallos:
        print(str(len(_fallos)) + " verificacion(es) fallaron:")
        for f in _fallos:
            print("  - " + f)
        return 1
    print("Los seis CRUD verificados contra el servicio real.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
