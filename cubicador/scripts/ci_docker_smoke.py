"""Verificacion de humo contra el stack real de Docker Compose (Postgres +
RabbitMQ + api + worker) - usado por el job `docker-smoke` de
.github/workflows/cubicador-ci.yml. Automatiza lo que hasta ahora era el
procedimiento manual documentado en docs/testDocumentation/
PRUEBAS_DE_INTEGRACION.md secciones 2.2/2.3/2.4/2.6, INCLUIDO el criterio de
cierre "trabajo publicado -> resultado recibido por cola" (2.3, ahora también
automatizado en flujo_resultado_por_cola - antes solo se había probado a
mano): falla (exit != 0) si el flujo real no llega al resultado esperado, en
vez de solo revisar el codigo por lectura.

Requiere el stack de cubicador ya levantado y sano (`docker compose up -d
--build --wait`) con el puerto de la api publicado al host (8081 por
defecto, CUBICADOR_HOST_PORT en .env - el contenedor sigue escuchando en
8000 puertas adentro), Y el
`rabbit` compartido de NEXO (docker-compose.yml raiz, ver
DECISIONES_TECNICAS.md #9) levantado con SUS puertos publicados al host: AMQP
en 5672 y su consola de administracion en 15672.

No cubre el vigilante de timeout de punta a punta (docs/DECISIONES_TECNICAS.md
#10): ese escenario requiere detener el contenedor `worker` a mitad de un
mensaje y llamar a `_revisar_timeouts` directo contra la base, lo que acopla
este script a la topologia interna de docker-compose de una forma que no
vale la pena en CI - sigue siendo verificacion manual (ver PRUEBAS_DE_
INTEGRACION.md seccion 2.4 punto (c)). Lo que SI esta automatizado es la
logica del vigilante con dobles de prueba, incluido el orden
publicar-antes-de-marcar: tests/worker/test_vigilante_timeouts.py.
"""

from __future__ import annotations

import asyncio
import json
import os
import sys
import time

import aio_pika
import httpx

API_BASE_URL = os.environ.get("API_BASE_URL", "http://localhost:8081")
API_KEY = os.environ["CUBICADOR_API_KEY"]
RABBITMQ_USER = os.environ["RABBITMQ_USER"]
RABBITMQ_PASS = os.environ["RABBITMQ_PASSWORD"]
RABBITMQ_URL = f"amqp://{RABBITMQ_USER}:{RABBITMQ_PASS}@localhost:5672/"
RABBITMQ_MGMT_URL = "http://localhost:15672"
HEADERS = {"Authorization": f"Bearer {API_KEY}"}
EXCHANGE = "cubicador.eventos"


def log(mensaje: str) -> None:
    print(f"[ci-smoke] {mensaje}", flush=True)


async def esperar_health(client: httpx.AsyncClient, timeout_s: float = 60) -> None:
    log("esperando GET /health...")
    limite = time.monotonic() + timeout_s
    ultimo_error: Exception | None = None
    while time.monotonic() < limite:
        try:
            r = await client.get(f"{API_BASE_URL}/health", timeout=3)
            if r.status_code == 200:
                log("api saludable.")
                return
        except httpx.HTTPError as exc:
            ultimo_error = exc
        await asyncio.sleep(1)
    raise TimeoutError(f"la api no respondio saludable en {timeout_s}s (ultimo error: {ultimo_error})")


async def flujo_optimizar_async(client: httpx.AsyncClient) -> None:
    log("flujo REST + async: vehiculos -> plan -> item -> optimizar -> resultado")

    r = await client.get(f"{API_BASE_URL}/api/planes-cargue/vehiculos", headers=HEADERS)
    r.raise_for_status()
    vehiculos = r.json()
    # El catalogo de presets crece (hoy son 10): se siembra por nombre y los faltantes se agregan. Un `== 9` fijo rompia el CI cada vez
    # que se sumaba uno; lo que importa aca es que la siembra corrio y que existe el que usa el flujo de abajo.
    assert len(vehiculos) >= 9, f"se esperaban al menos 9 presets sembrados, llegaron {len(vehiculos)}"
    furgon = next(v for v in vehiculos if "13.6" in v["nombre"])

    r = await client.post(
        f"{API_BASE_URL}/api/planes-cargue", headers=HEADERS,
        json={"nombre": "CI smoke", "vehiculoId": furgon["id"]},
    )
    r.raise_for_status()
    plan = r.json()
    assert plan["estado"] == "borrador", plan

    r = await client.post(
        f"{API_BASE_URL}/api/planes-cargue/{plan['id']}/items", headers=HEADERS,
        json={"descripcion": "Pallet CI", "largoMm": 1200, "anchoMm": 1000, "altoMm": 1000, "pesoKg": 500, "cantidad": 3},
    )
    r.raise_for_status()

    r = await client.post(f"{API_BASE_URL}/api/planes-cargue/{plan['id']}/optimizar", headers=HEADERS, json={})
    assert r.status_code == 202, r.text
    assert r.json()["estado"] == "procesando", r.text

    resultado = await _poll_resultado(client, plan["id"])
    assert resultado["plan"]["estado"] == "optimizado", resultado["plan"]
    assert len(resultado["colocaciones"]) == 3, resultado["colocaciones"]
    assert all(c["cargada"] for c in resultado["colocaciones"])
    # Regresion del bug real de metricas.puntaje == 0 (ver README.md "Estado actual"
    # y docs/testDocumentation/PRUEBAS_DE_INTEGRACION.md seccion 2.2).
    assert resultado["metricas"]["puntaje"] == resultado["plan"]["puntaje"] > 0, resultado["metricas"]
    log(f"optimizado correctamente, puntaje={resultado['plan']['puntaje']}")


async def _poll_resultado(client: httpx.AsyncClient, plan_id: str, timeout_s: float = 30) -> dict:
    limite = time.monotonic() + timeout_s
    while time.monotonic() < limite:
        r = await client.get(f"{API_BASE_URL}/api/planes-cargue/{plan_id}/resultado", headers=HEADERS)
        r.raise_for_status()
        resultado = r.json()
        if resultado["plan"]["estado"] != "procesando":
            return resultado
        await asyncio.sleep(1)
    raise TimeoutError(f"el plan {plan_id} siguio en 'procesando' tras {timeout_s}s (worker no proceso el mensaje)")


async def flujo_resultado_por_cola(client: httpx.AsyncClient) -> None:
    """Criterio de cierre 'trabajo publicado -> resultado recibido por cola':
    a diferencia de flujo_optimizar_async (que confirma el resultado vía
    HTTP/DB), esta prueba se suscribe DIRECTO a la routing key
    `plan-de-cargue.generado` con una cola de prueba propia (exclusiva, no
    la del worker) ANTES de disparar `optimizar`, y verifica que el evento
    real llega por esa cola - no solo que el efecto quedó persistido. Mismo
    camino que se probó a mano en PRUEBAS_DE_INTEGRACION.md sección 2.3,
    ahora automatizado y repetible en cada push/PR."""
    log("flujo E2E por cola: trabajo publicado -> resultado recibido en 'plan-de-cargue.generado'")

    r = await client.get(f"{API_BASE_URL}/api/planes-cargue/vehiculos", headers=HEADERS)
    r.raise_for_status()
    furgon = next(v for v in r.json() if "13.6" in v["nombre"])

    r = await client.post(
        f"{API_BASE_URL}/api/planes-cargue", headers=HEADERS,
        json={"nombre": "CI smoke cola", "vehiculoId": furgon["id"]},
    )
    r.raise_for_status()
    plan = r.json()

    r = await client.post(
        f"{API_BASE_URL}/api/planes-cargue/{plan['id']}/items", headers=HEADERS,
        json={"descripcion": "Pallet CI cola", "largoMm": 1200, "anchoMm": 1000, "altoMm": 1000, "pesoKg": 500, "cantidad": 2},
    )
    r.raise_for_status()

    conexion = await aio_pika.connect_robust(RABBITMQ_URL)
    async with conexion:
        canal = await conexion.channel()
        exchange = await canal.declare_exchange(EXCHANGE, aio_pika.ExchangeType.TOPIC, durable=True)
        # Cola exclusiva/propia de esta verificación: se suma como OTRO suscriptor
        # de la misma routing key, no interfiere con la cola del worker (cada cola
        # ligada recibe su propia copia del evento).
        cola = await canal.declare_queue(exclusive=True)
        await cola.bind(exchange, routing_key="plan-de-cargue.generado")

        r = await client.post(f"{API_BASE_URL}/api/planes-cargue/{plan['id']}/optimizar", headers=HEADERS, json={})
        assert r.status_code == 202, r.text

        evento = await asyncio.wait_for(_esperar_evento_del_plan(cola, plan["id"]), timeout=30)
        assert evento["planId"] == plan["id"], evento
        assert evento["correlationId"] == plan["id"], evento
        # "resultado" ya no es {estado, puntaje, colocaciones: int} - es el
        # ResultadoPlanDto completo (mismo shape que ya usa GET .../resultado,
        # ver flujo_optimizar_async arriba).
        assert evento["resultado"]["plan"]["estado"] == "optimizado", evento
        assert len(evento["resultado"]["colocaciones"]) == 2, evento
        assert all(c["cargada"] for c in evento["resultado"]["colocaciones"]), evento
        log(f"evento recibido por cola: plan={evento['resultado']['plan']['estado']}, "
            f"colocaciones={len(evento['resultado']['colocaciones'])}")


async def _esperar_evento_del_plan(cola: aio_pika.abc.AbstractQueue, plan_id: str) -> dict:
    async with cola.iterator() as mensajes:
        async for mensaje in mensajes:
            async with mensaje.process():
                cuerpo = json.loads(mensaje.body)
                # NEXO consume este mismo evento con un IConsumer<T> de
                # MassTransit, así que ahora se publica envuelto (ver
                # infrastructure/mensajeria/rabbitmq.py::_sobre_masstransit) - se
                # desenvuelve aquí para que las aserciones de abajo no cambien.
                if isinstance(cuerpo, dict) and "message" in cuerpo and "messageType" in cuerpo:
                    cuerpo = cuerpo["message"]
                if cuerpo.get("planId") == plan_id:
                    return cuerpo
    raise RuntimeError("la cola se cerró sin entregar el evento esperado")  # pragma: no cover - iterator() no termina solo


async def flujo_reintentos_y_dlq(client: httpx.AsyncClient) -> None:
    log("flujo de reintentos + Dead-Letter Queue (mensaje con tipo invalido)")
    await _purgar_dlq(client)

    conexion = await aio_pika.connect_robust(RABBITMQ_URL)
    async with conexion:
        canal = await conexion.channel()
        exchange = await canal.declare_exchange(EXCHANGE, aio_pika.ExchangeType.TOPIC, durable=True)
        cuerpo = json.dumps({"tipo": "tipo_invalido_ci", "correlationId": "ci-dlq-test"}).encode()
        await exchange.publish(
            aio_pika.Message(body=cuerpo, content_type="application/json"),
            routing_key="cubicacion.solicitada",
        )

    await _esperar_mensaje_en_dlq(client, timeout_s=30)
    await _purgar_dlq(client)
    log("mensaje invalido termino en cubicacion.dlq tras agotar reintentos, como se esperaba.")


async def _mensajes_en_dlq(client: httpx.AsyncClient) -> int:
    r = await client.get(
        f"{RABBITMQ_MGMT_URL}/api/queues/%2F/cubicacion.dlq",
        auth=(RABBITMQ_USER, RABBITMQ_PASS),
    )
    r.raise_for_status()
    return r.json().get("messages", 0)


async def _esperar_mensaje_en_dlq(client: httpx.AsyncClient, timeout_s: float) -> None:
    limite = time.monotonic() + timeout_s
    while time.monotonic() < limite:
        if await _mensajes_en_dlq(client) >= 1:
            return
        await asyncio.sleep(1)
    raise TimeoutError(f"cubicacion.dlq siguio vacia tras {timeout_s}s (se esperaban 3 reintentos y caida a DLQ)")


async def _purgar_dlq(client: httpx.AsyncClient) -> None:
    await client.delete(
        f"{RABBITMQ_MGMT_URL}/api/queues/%2F/cubicacion.dlq/contents",
        auth=(RABBITMQ_USER, RABBITMQ_PASS),
    )


async def main() -> None:
    async with httpx.AsyncClient() as client:
        await esperar_health(client)
        await flujo_optimizar_async(client)
        await flujo_resultado_por_cola(client)
        await flujo_reintentos_y_dlq(client)
    log("todo verificado correctamente.")


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except Exception as exc:  # noqa: BLE001 - se quiere el traceback completo en el log de CI
        import traceback

        traceback.print_exc()
        print(f"[ci-smoke] FALLO: {exc}", file=sys.stderr)
        sys.exit(1)
