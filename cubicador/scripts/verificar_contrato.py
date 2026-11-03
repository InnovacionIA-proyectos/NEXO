"""Verifica el contrato contra el servicio corriendo (S5-HU-17.4).

Comprueba uno por uno los criterios de aceptacion de la issue y la forma que
declara la seccion 2 de `docs/HOJA-DE-RUTA-CUBICADOR.md`: los campos
obligatorios, que lo que no es un fallo real vuelva con 200, que el origen
salga de la llave, que la reconsulta exija el sistema y que lo guardado se
relea igual.

Las pruebas de pytest cubren lo mismo contra SQLite y por ASGI; esto lo cubre
contra el servicio REAL por HTTP, que es donde se ven las diferencias del motor
de base (la caja de los uuid, la escala de los decimales) y de la
serializacion.

    ./.venv/Scripts/python.exe cubicador/scripts/verificar_contrato.py

Lee el puerto y las llaves de `cubicador/.env`. Sale 0 si todo pasa.
"""

from __future__ import annotations

import io
import json
import os
import urllib.error
import urllib.request

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))

_fallos: list[str] = []


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
LLAVE_NEXO = ENV.get("CUBICADOR_API_KEY_NEXO", "")
LLAVE_COTIFLY = ENV.get("CUBICADOR_API_KEY_COTIFLY", "")
LLAVE_GENERICA = ENV.get("CUBICADOR_API_KEY", "")


def pedir(metodo: str, ruta: str, llave: str | None = None, cuerpo: dict | None = None):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    peticion = urllib.request.Request(BASE + ruta, data=datos, method=metodo)
    peticion.add_header("Content-Type", "application/json")
    if llave:
        peticion.add_header("Authorization", "Bearer " + llave)
    try:
        with urllib.request.urlopen(peticion, timeout=60) as r:
            return r.status, json.loads(r.read() or b"null")
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read() or b"null")


def revisar(titulo: str, condicion: bool, detalle: str = "") -> bool:
    marca = "OK   " if condicion else "FALLA"
    extra = "  [" + detalle + "]" if detalle else ""
    print("  " + marca + "  " + titulo + extra)
    if not condicion:
        _fallos.append(titulo)
    return condicion


def main() -> int:
    if not LLAVE_NEXO or not LLAVE_COTIFLY:
        raise SystemExit("Faltan CUBICADOR_API_KEY_NEXO / _COTIFLY en cubicador/.env")

    print("Servicio: " + BASE + "\n")

    # --- catalogos (seccion 2.7) -------------------------------------------
    print("Catalogos")
    estado, carrocerias = pedir("GET", "/tipos-carroceria", LLAVE_GENERICA)
    revisar("GET /tipos-carroceria responde 200", estado == 200, str(estado))
    revisar("trae id, codigo, nombre y activo",
            bool(carrocerias) and {"id", "codigoCarroceria", "nombreCarroceria", "activo"}
            <= set(carrocerias[0]))

    estado, mercancias = pedir("GET", "/tipos-mercancia", LLAVE_GENERICA)
    revisar("GET /tipos-mercancia responde 200", estado == 200, str(estado))
    esperados = {"id", "codigoMercancia", "nombreMercancia", "categoria", "cubicable",
                 "condicionadaCubicacion", "carroceriaPrincipal", "carroceriaSecundaria",
                 "carroceriaSecundariaCondicionada", "controlAmbientalRequerido",
                 "apilable", "fragil", "rotacionLibre", "comentarioCondicion", "activo"}
    faltantes = esperados - set(mercancias[0]) if mercancias else esperados
    revisar("trae los 15 campos de la seccion 2.7", not faltantes, "faltan " + str(sorted(faltantes)))

    id_mercancia = next(m["id"] for m in mercancias if m["cubicable"])
    codigo_mercancia = next(m["codigoMercancia"] for m in mercancias if m["id"] == id_mercancia)

    # --- POST /recomendaciones (seccion 2.3) -------------------------------
    print("\nPOST /recomendaciones")
    linea = "VERIF-" + str(os.getpid())
    cuerpo = {"idLineaSolicitud": linea, "mercancias": [{
        "codigoMercancia": codigo_mercancia, "cantidad": 1200,
        "largoCm": 50, "anchoCm": 40, "altoCm": 40, "pesoKg": 20}]}
    estado, creada = pedir("POST", "/recomendaciones", LLAVE_COTIFLY, cuerpo)
    revisar("responde 200", estado == 200, str(estado))
    revisar("resultado ASIGNADO", creada.get("resultado") == "ASIGNADO")
    revisar("origenSistema sale de la llave", creada.get("origenSistema") == "COTIFLY")
    revisar("motivoSinSeleccion es null cuando asigna", creada.get("motivoSinSeleccion") is None)

    obligatorios = {"idRecomendacionVehiculo", "codigoVehiculo", "codigoCarroceria",
                    "pesoTotalKg", "volumenTotalM3", "porcentajeLlenadoVolumen",
                    "porcentajeLlenadoPeso"}
    vehiculos = creada.get("vehiculos", [])
    revisar("hay vehiculos", bool(vehiculos))
    nulos = [c for v in vehiculos for c in obligatorios if v.get(c) is None]
    revisar("ningun campo obligatorio viaja en null", not nulos, str(nulos))
    revisar("la flota puede ser mixta", len({v["codigoVehiculo"] for v in vehiculos}) > 1,
            str([v["codigoVehiculo"] for v in vehiculos]))
    numeros = [v["numeroVehiculo"] for v in vehiculos]
    revisar("numeroVehiculo es consecutivo desde 1", numeros == list(range(1, len(numeros) + 1)))

    tipos = [type(v[c]) for v in vehiculos
             for c in ("pesoTotalKg", "volumenTotalM3", "porcentajeLlenadoVolumen")]
    revisar("los decimales viajan como numero", all(t in (int, float) for t in tipos),
            str({t.__name__ for t in tipos}))

    mercancia = vehiculos[0]["mercancias"][0]
    revisar("las mercancias traen id Y codigo",
            mercancia.get("idTipoMercancia") == id_mercancia
            and mercancia.get("codigoMercancia") == codigo_mercancia)
    revisar("los ids vienen en mayusculas",
            creada["idRecomendacion"] == creada["idRecomendacion"].upper())

    # --- autenticacion (seccion 2.1) ---------------------------------------
    print("\nAutenticacion")
    estado, _ = pedir("POST", "/recomendaciones", None, cuerpo)
    revisar("sin llave es 401", estado == 401, str(estado))
    estado, _ = pedir("POST", "/recomendaciones", LLAVE_GENERICA, cuerpo)
    revisar("la llave generica no sirve aca", estado == 401, str(estado))
    estado, _ = pedir("POST", "/recomendaciones", LLAVE_COTIFLY,
                      dict(cuerpo, origenSistema="NEXO"))
    revisar("declarar un origen que no corresponde es 401", estado == 401, str(estado))
    estado, con_nexo = pedir("POST", "/recomendaciones", LLAVE_NEXO,
                             dict(cuerpo, idLineaSolicitud=linea + "-N"))
    revisar("la llave de NEXO da origenSistema NEXO",
            estado == 200 and con_nexo.get("origenSistema") == "NEXO")

    # --- reconsulta y append-only (secciones 2.8 y 2.9) --------------------
    print("\nReconsulta y append-only")
    estado, leida = pedir(
        "GET", "/recomendaciones?idLineaSolicitud=" + linea + "&origenSistema=COTIFLY",
        LLAVE_COTIFLY)
    revisar("la reconsulta responde 200", estado == 200, str(estado))
    revisar("devuelve la misma corrida",
            leida.get("idRecomendacion") == creada.get("idRecomendacion"))
    revisar("los vehiculos son identicos", leida.get("vehiculos") == creada.get("vehiculos"))

    estado, _ = pedir(
        "GET", "/recomendaciones?idLineaSolicitud=" + linea + "&origenSistema=NEXO",
        LLAVE_COTIFLY)
    revisar("filtrar por el otro sistema no la encuentra", estado == 404, str(estado))

    estado, segunda = pedir("POST", "/recomendaciones", LLAVE_COTIFLY, cuerpo)
    revisar("recalcular crea una corrida nueva",
            segunda.get("idRecomendacion") != creada.get("idRecomendacion"))

    id_vehiculo = creada["vehiculos"][0]["idRecomendacionVehiculo"]
    estado, mercs = pedir("GET", "/recomendaciones/" + id_vehiculo + "/mercancias", LLAVE_COTIFLY)
    revisar("el id guardado de la PRIMERA corrida sigue sirviendo", estado == 200, str(estado))
    revisar("y devuelve sus mercancias",
            bool(mercs) and mercs[0]["idTipoMercancia"] == id_mercancia)

    estado, _ = pedir("GET", "/recomendaciones/" + id_vehiculo.lower() + "/mercancias",
                      LLAVE_COTIFLY)
    revisar("el id se acepta en cualquier caja", estado == 200, str(estado))

    # --- lo que antes era error ahora es resultado (seccion 2.6) -----------
    print("\nResultados que no son errores")
    estado, extra = pedir("POST", "/recomendaciones", LLAVE_COTIFLY, {
        "idLineaSolicitud": linea + "-X", "mercancias": [{
            "codigoMercancia": codigo_mercancia, "cantidad": 2,
            "largoCm": 1400, "anchoCm": 40, "altoCm": 40, "pesoKg": 25}]})
    revisar("extradimensionado vuelve con 200", estado == 200, str(estado))
    revisar("con resultado SIN_SELECCION_AUTOMATICA",
            extra.get("resultado") == "SIN_SELECCION_AUTOMATICA")
    revisar("y motivo EXTRADIMENSIONADO",
            extra.get("motivoSinSeleccion") == "EXTRADIMENSIONADO")
    revisar("con el bloque extradimension lleno",
            (extra.get("extradimension") or {}).get("hayExtradimension") is True)

    print("\nErrores reales")
    estado, error = pedir("POST", "/recomendaciones", LLAVE_COTIFLY, {
        "idLineaSolicitud": linea, "mercancias": [{
            "codigoMercancia": "NO_EXISTE_EN_EL_CATALOGO", "cantidad": 1,
            "largoCm": 60, "anchoCm": 40, "altoCm": 40, "pesoKg": 25}]})
    revisar("mercancia desconocida es 400", estado == 400, str(estado))
    revisar("con el formato de error del contrato",
            isinstance(error, dict) and error.get("codigo") == "MERCANCIA_DESCONOCIDA")

    print("\nEndpoint retirado")
    estado, _ = pedir("POST", "/api/cubicacion", LLAVE_GENERICA, {"items": []})
    revisar("POST /api/cubicacion ya no existe", estado == 404, str(estado))

    print()
    if _fallos:
        print(str(len(_fallos)) + " verificacion(es) fallaron:")
        for f in _fallos:
            print("  - " + f)
        return 1
    print("Contrato verificado contra el servicio real.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
