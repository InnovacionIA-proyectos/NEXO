"""Verifica por API los 6 bugs de Cubicador que cierra S4-HU-03 (commit 7902fdf).

Cada bloque replica el escenario del test unitario correspondiente, pero pasando
por la API real -> cola -> worker -> Postgres, que es donde QA los reporto. Los
tests unitarios corren sobre SQLite; esto corre contra el motor de base de
verdad.

    #158  vehiculo con toleranciaParedesMm > 0 rechazaba CUALQUIER item
    #150  metricas.advertencias/violacionesCriticas siempre en 0
    #152  SIN_ESPACIO culpaba a "fragmentacion" aunque el item no cupiera ni vacio
    #153  SECUENCIA_BLOQUEADA duplicada (una por bloqueador, no por colocacion)
    #154  apilamiento de 3+ niveles excedia el maxPesoEncimaKg de la base
    #151  el flag `bloqueada` no sobrevivia a "Reoptimizar (respetar bloqueadas)"

## Como correrlo

Levantar el stack de cubicador (Postgres + api + worker) y:

    export CUBICADOR_API_KEY=<la del .env de cubicador>
    python tests/QA/s4-hu-03-bugs-optimizador/verify-bugs-optimizador.py

Opcional: CUBICADOR_BASE (default http://localhost:8081/api) para apuntarlo a
otro ambiente.

## Como leerlo

Sale con codigo 0 si los 6 se comportan como deben. Para que la corrida PRUEBE
algo hay que verla fallar: revertir los 4 archivos de codigo del commit
(`git checkout 7902fdf~1 -- cubicador/application/plan_cargue/resultado.py
cubicador/domain/motor/optimizadores.py cubicador/domain/motor/validadores.py
cubicador/infrastructure/persistence/repositorios/plan_cargue_repo.py`),
reconstruir las imagenes y volver a correr: los 6 deben fallar. Resultados de esa
contraprueba, en el README de esta carpeta.

Crea datos de prueba (vehiculos y planes con sufijo de timestamp). Pensado para
un stack local efimero, no para un ambiente compartido.
"""

import json
import os
import sys
import time
import urllib.error
import urllib.request

BASE = os.environ.get("CUBICADOR_BASE", "http://localhost:8081/api")
KEY = os.environ.get("CUBICADOR_API_KEY")
if not KEY:
    sys.exit("Falta CUBICADOR_API_KEY (el mismo valor del .env de cubicador).")

SUFIJO = str(int(time.time()))
fallos: list[str] = []


def pedir(metodo, ruta, cuerpo=None):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    req = urllib.request.Request(
        BASE + ruta, data=datos, method=metodo,
        headers={"Authorization": f"Bearer {KEY}", "Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            c = r.read().decode()
            return r.status, (json.loads(c) if c else None)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()[:300]


def afirmar(ok, etiqueta, detalle=""):
    print(f"  [{'OK  ' if ok else 'FALLA'}] {etiqueta}{('  -> ' + detalle) if detalle else ''}")
    if not ok:
        fallos.append(etiqueta)


def vehiculo(nombre, largo, ancho, alto, cap=10000, tolerancia_paredes=0.0):
    estado, v = pedir("POST", "/planes-cargue/vehiculos", {
        "nombre": f"{nombre}-{SUFIJO}", "descripcion": "verificacion S4-HU-03",
        "largoInternoMm": largo, "anchoInternoMm": ancho, "altoInternoMm": alto,
        "capacidadKg": cap, "taraKg": 1000,
        "puertaAnchoMm": ancho, "puertaAltoMm": alto, "puertaPosicion": "trasera",
        "toleranciaParedesMm": tolerancia_paredes, "toleranciaEntreUnidadesMm": 0,
        "ejes": [{"posicionMm": largo * 0.2, "capacidadKg": cap},
                 {"posicionMm": largo * 0.8, "capacidadKg": cap}],
    })
    if estado not in (200, 201):
        sys.exit(f"No se pudo crear el vehiculo {nombre} (HTTP {estado}): {v}")
    return v["id"]


def plan_con(vehiculo_id, nombre, items):
    _, p = pedir("POST", "/planes-cargue", {"nombre": f"{nombre}-{SUFIJO}", "vehiculoId": vehiculo_id})
    for it in items:
        pedir("POST", f"/planes-cargue/{p['id']}/items", it)
    return p["id"]


def optimizar_y_leer(plan_id, parametros=None):
    pedir("POST", f"/planes-cargue/{plan_id}/optimizar", parametros or {})
    resultado = None
    for _ in range(30):
        estado, resultado = pedir("GET", f"/planes-cargue/{plan_id}/resultado")
        if estado == 200 and (resultado.get("plan") or {}).get("estado") in ("optimizado", "ajustado", "error"):
            return resultado
        time.sleep(2)
    return resultado


def item(sku, largo, ancho, alto, peso, cantidad=1, **extra):
    base = {"sku": sku, "descripcion": sku, "secuenciaEntrega": 1,
            "largoMm": largo, "anchoMm": ancho, "altoMm": alto, "pesoKg": peso, "cantidad": cantidad}
    base.update(extra)
    return base


def skus(resultado, cargada: bool):
    por_id = {i["id"]: i["sku"] for i in ((resultado.get("plan") or {}).get("items") or [])}
    return sorted({por_id.get(c.get("itemPlanCargueId"), "?")
                   for c in (resultado.get("colocaciones") or []) if bool(c.get("cargada")) is cargada})


# ------------------------------------------------------------------ #158 + #150
print("=== #158: vehiculo CON toleranciaParedesMm > 0 ===")
v = vehiculo("QA158", 6200, 2200, 2000, cap=6300, tolerancia_paredes=50)
p = plan_con(v, "QA158", [item("H", 1200, 1000, 400, 1500, cantidad=8, rotable=False)])
r = optimizar_y_leer(p)
cargadas = [c for c in (r.get("colocaciones") or []) if c.get("cargada")]
afirmar(len(cargadas) > 0, "#158: el vehiculo con tolerancias SI coloca items",
        f"{len(cargadas)} cargadas (sin el arreglo: 0, rechazaba todo)")

print("\n=== #150: los contadores reflejan las incidencias ===")
incidencias = r.get("incidencias") or []
metricas = r.get("metricas") or {}
adv = sum(1 for i in incidencias if i.get("severidad") == "advertencia")
cri = sum(1 for i in incidencias if i.get("severidad") == "critica")
print(f"  incidencias reales -> advertencia={adv} critica={cri}"
      f"   metricas -> advertencias={metricas.get('advertencias')} violacionesCriticas={metricas.get('violacionesCriticas')}")
afirmar(cri > 0, "el escenario produce al menos una critica real",
        ", ".join(sorted({i.get("codigoRegla") for i in incidencias if i.get("severidad") == "critica"})) or "ninguna")
afirmar(metricas.get("advertencias") == adv, "#150: advertencias coincide con el arreglo de la misma respuesta")
afirmar(metricas.get("violacionesCriticas") == cri, "#150: violacionesCriticas coincide con el arreglo")

# ------------------------------------------------------------------ #152
print("\n=== #152: el item no cabe ni en el vehiculo vacio ===")
v = vehiculo("QA152", 6200, 2200, 2000)
p = plan_con(v, "QA152", [item("GRANDE", 7000, 1000, 1000, 200, rotable=False)])
r = optimizar_y_leer(p)
rechazadas = [c for c in (r.get("colocaciones") or []) if not c.get("cargada")]
razon = (rechazadas[0].get("razonRechazo") or "") if rechazadas else ""
print(f"  razon: {razon!r}")
afirmar(len(rechazadas) == 1, "el item se rechaza")
afirmar("excede las dimensiones internas" in razon, "#152: el mensaje dice que excede el vehiculo")
afirmar("fragmentado" not in razon, "#152: el mensaje NO culpa a fragmentacion")

# ------------------------------------------------------------------ #154
print("\n=== #154: apilamiento en cadena de 3 niveles ===")
v = vehiculo("QA154", 1200, 1000, 3000)
p = plan_con(v, "QA154", [
    item("A", 1200, 1000, 1000, 500, rotable=False, maxPesoEncimaKg=250),
    item("B", 1200, 1000, 1000, 200, rotable=False),
    item("C", 1200, 1000, 1000, 60, rotable=False),
])
r = optimizar_y_leer(p)
print(f"  cargados={skus(r, True)}  rechazados={skus(r, False)}")
afirmar(skus(r, True) == ["A", "B"], "#154: solo A y B se cargan")
afirmar(skus(r, False) == ["C"], "#154: C se rechaza (200+60 = 260 > 250 que soporta A)")

# ------------------------------------------------------------------ #153
print("\n=== #153: SECUENCIA_BLOQUEADA consolidada ===")
v = vehiculo("QA153", 2200, 2450, 1400)
p = plan_con(v, "QA153", [
    item("a", 1000, 2450, 1350, 100, rotable=False, secuenciaEntrega=1),
    item("b", 1000, 1225, 1350, 100, rotable=False, secuenciaEntrega=2),
    item("c", 1000, 1225, 1350, 100, rotable=False, secuenciaEntrega=3),
])
r = optimizar_y_leer(p)
inc = [i for i in (r.get("incidencias") or []) if i.get("codigoRegla") == "SECUENCIA_BLOQUEADA"]
por_colocacion: dict = {}
for i in inc:
    por_colocacion[i.get("colocacionItemId")] = por_colocacion.get(i.get("colocacionItemId"), 0) + 1
print(f"  SECUENCIA_BLOQUEADA={len(inc)} sobre {len(por_colocacion)} colocacion(es) -> {por_colocacion}")
if not inc:
    # Sin incidencias el chequeo pasaria vacio y no distinguiria nada: es una falla.
    afirmar(False, "#153: el optimizador no reprodujo la geometria (chequeo vacio, no prueba nada)")
else:
    afirmar(all(n == 1 for n in por_colocacion.values()),
            "#153: una sola incidencia por colocacion", str(por_colocacion))

# ------------------------------------------------------------------ #151
print("\n=== #151: `bloqueada` sobrevive a reoptimizar ===")
v = vehiculo("QA151", 3000, 1200, 1200)
p = plan_con(v, "QA151", [item("P", 1200, 1000, 1000, 100, cantidad=2, rotable=False)])
r = optimizar_y_leer(p)
cargadas = [c for c in (r.get("colocaciones") or []) if c.get("cargada")]
if len(cargadas) < 2:
    afirmar(False, "#151: hacen falta 2 colocaciones para el escenario", f"hay {len(cargadas)}")
else:
    estado, _ = pedir("PATCH", f"/planes-cargue/{p}/colocaciones/{cargadas[0]['id']}", {"bloqueada": True})
    afirmar(estado in (200, 202), f"bloquear una colocacion (HTTP {estado})")
    r2 = optimizar_y_leer(p, {"estrategia": "Combinada", "algoritmo": "bfd", "respetarBloqueadas": True})
    bloqueadas = [c for c in (r2.get("colocaciones") or []) if c.get("bloqueada")]
    print(f"  tras reoptimizar: colocaciones con bloqueada=true -> {len(bloqueadas)}")
    afirmar(len(bloqueadas) >= 1, "#151: el flag bloqueada sobrevive a la reoptimizacion")

print("\n" + ("TODO OK" if not fallos else f"{len(fallos)} FALLAS: {fallos}"))
sys.exit(1 if fallos else 0)
