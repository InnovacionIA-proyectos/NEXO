"""Bateria de integracion contra SQL Server real (S5-HU-17.1).

Repite contra el motor nuevo el recorrido que `docs/testDocumentation/
PRUEBAS_DE_INTEGRACION.md` seccion 2 verifico en su dia contra Postgres:
flujo REST completo y flujo asincrono de punta a punta por RabbitMQ.

Asume el stack de `docker compose up` corriendo en esta carpeta, mas el
`rabbit` compartido del compose raiz de NEXO.

    cubicador/.venv/Scripts/python.exe scripts/verificar_integracion_mssql.py
"""
from __future__ import annotations

import asyncio
import json
import os
import time
import urllib.error
import urllib.request
import uuid

BASE = os.environ.get("CUBICADOR_URL", "http://localhost:8081")
RABBIT = os.environ.get("RABBITMQ_URL", "amqp://nexo:1234@localhost:5672/")
LLAVE = os.environ.get("CUBICADOR_API_KEY", "")

ok = 0
fallo = 0


def chequeo(titulo: str, condicion: bool, detalle: str = "") -> bool:
    global ok, fallo
    if condicion:
        ok += 1
        print(f"  OK     {titulo}" + (f"  ({detalle})" if detalle else ""))
    else:
        fallo += 1
        print(f"  FALLA  {titulo}" + (f"  ({detalle})" if detalle else ""))
    return condicion


def pedir(metodo: str, ruta: str, cuerpo: dict | None = None, espera: int = 30):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    req = urllib.request.Request(f"{BASE}{ruta}", data=datos, method=metodo)
    req.add_header("Content-Type", "application/json")
    if LLAVE:
        req.add_header("Authorization", f"Bearer {LLAVE}")
    try:
        with urllib.request.urlopen(req, timeout=espera) as r:
            texto = r.read().decode()
            return r.status, (json.loads(texto) if texto else None)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()[:300]


def publicar_optimizar(plan_id: str) -> bool:
    """Publica el comando que el worker espera, igual que lo haria NEXO.

    Optimizar NO es un endpoint HTTP del Cubicador: es un comando por cola.
    NEXO publica `cubicacion.solicitada` al exchange `cubicador.eventos` y el
    worker lo consume de la cola `cubicador.optimizar` (ver worker/main.py).
    """
    import aio_pika

    async def _publicar() -> None:
        conexion = await aio_pika.connect_robust(RABBIT, timeout=20)
        async with conexion:
            canal = await conexion.channel()
            exchange = await canal.declare_exchange(
                "cubicador.eventos", aio_pika.ExchangeType.TOPIC, durable=True)
            await exchange.publish(
                aio_pika.Message(
                    json.dumps({"tipo": "optimizar", "planId": plan_id}).encode(),
                    content_type="application/json",
                    delivery_mode=aio_pika.DeliveryMode.PERSISTENT),
                routing_key="cubicacion.solicitada")

    try:
        asyncio.run(_publicar())
        return True
    except Exception as e:  # noqa: BLE001
        print(f"         no pude publicar: {str(e)[:140]}")
        return False


def main() -> int:
    print(f"Contra {BASE}\n")

    print("2.1 Salud del servicio")
    estado, cuerpo = pedir("GET", "/health")
    chequeo("/health responde 200", estado == 200, str(cuerpo))

    print("\n2.2 Flujo REST completo contra SQL Server")
    estado, vehiculos = pedir("GET", "/api/planes-cargue/vehiculos")
    chequeo("el catalogo se lee de la base", estado == 200 and isinstance(vehiculos, list),
            f"{len(vehiculos) if isinstance(vehiculos, list) else '?'} vehiculos")
    if not isinstance(vehiculos, list) or not vehiculos:
        print("\nSin catalogo no se puede seguir.")
        return 1

    # Los presets corregidos en esta misma HU tienen que haber sobrevivido.
    por_nombre = {v["nombre"]: v for v in vehiculos}
    turbo = por_nombre.get("Turbo con estacas", {})
    chequeo("el Turbo tiene las medidas reales del Excel",
            turbo.get("largoInternoMm") == 4500 and turbo.get("capacidadKg") == 4000,
            f"{turbo.get('largoInternoMm')} mm / {turbo.get('capacidadKg')} kg")
    chequeo("la Minimula 2S2 existe", "Minimula 2S2" in por_nombre)

    vehiculo = por_nombre.get("Camion rigido dobletroque") or vehiculos[0]
    # El endpoint NO acepta un id: lo genera el y lo devuelve. Pasarle uno
    # propio lo ignora en silencio, y los llamados siguientes fallan con una
    # violacion de llave foranea que parece un problema de la base y no lo es.
    estado, plan = pedir("POST", "/api/planes-cargue", {
        "nombre": f"integracion mssql {uuid.uuid4().hex[:8]}",
        "vehiculoId": vehiculo["id"],
    })
    plan_id = (plan or {}).get("id") if isinstance(plan, dict) else None
    chequeo("crear un plan persiste en SQL Server",
            estado in (200, 201, 202) and bool(plan_id), f"HTTP {estado}")
    if not plan_id:
        print("\nSin plan no se puede seguir.")
        return 1

    estado, _ = pedir("POST", f"/api/planes-cargue/{plan_id}/items", {
        "sku": "INTEG-1", "descripcion": "caja de prueba",
        "largoMm": 1000, "anchoMm": 800, "altoMm": 600, "pesoKg": 25, "cantidad": 4,
    })
    chequeo("agregar un item persiste", estado in (200, 201, 202), f"HTTP {estado}")

    estado, leido = pedir("GET", f"/api/planes-cargue/{plan_id}")
    chequeo("releer el plan devuelve lo guardado",
            estado == 200 and isinstance(leido, dict), f"HTTP {estado}")

    print("\n2.3 Flujo asincrono por RabbitMQ")
    encolado = publicar_optimizar(plan_id)
    chequeo("comando optimizar publicado a RabbitMQ", encolado)

    if encolado:
        estado_final = None
        for _ in range(30):
            time.sleep(2)
            _, leido = pedir("GET", f"/api/planes-cargue/{plan_id}")
            estado_final = leido.get("estado") if isinstance(leido, dict) else None
            if estado_final in ("optimizado", "error"):
                break
        chequeo("el worker consumio y dejo el plan optimizado",
                estado_final == "optimizado", f"estado={estado_final}")

    print(f"\n{'-' * 60}\n{ok} verificaciones OK, {fallo} fallas")
    return 1 if fallo else 0


if __name__ == "__main__":
    raise SystemExit(main())
