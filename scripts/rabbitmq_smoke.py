"""Smoke test POST-DEPLOY de RabbitMQ (S3-HU-11.4, issue #137).

RabbitMQ no tiene código fuente propio en este repo (su imagen es
`build/Dockerfile.rabbit`, RabbitMQ oficial + el plugin de retardo que
MassTransit necesita para los timeouts de saga) — por eso "test" acá NO es
una suite unitaria: es una verificación posterior al despliegue de que el
broker levantó bien Y de que la topología esperada (exchange, colas,
bindings) está declarada.

Importante — orden de las piezas: las colas y bindings de este exchange las
declaran NEXO (MassTransit, al arrancar `CabinaFletes.Api`) y Cubicador
(`worker/main.py`, al arrancar el worker) — NO RabbitMQ por sí solo. Un
RabbitMQ recién desplegado CON el volumen de datos anterior (`rabbit-data`,
ver docker-compose.yml raíz) conserva las colas durables de antes; un
RabbitMQ con volumen VACÍO (primera vez, o volumen borrado a propósito) no
tendrá ninguna cola hasta que NEXO y el worker de Cubicador se conecten al
menos una vez. Este script asume que esos dos servicios ya corrieron contra
este broker en algún momento — no es una verificación de "todo el stack
nació hoy", es la verificación de "el broker que se acaba de desplegar sigue
sirviendo la topología que el resto del sistema espera".

Usa la API de administración HTTP de RabbitMQ (igual criterio que
`cubicador/scripts/ci_docker_smoke.py::_mensajes_en_dlq`) — no una conexión
AMQP — porque preguntar "¿existe este binding?" es una consulta declarativa,
no un intercambio de mensajes.
"""

from __future__ import annotations

import os
import sys

import httpx

RABBITMQ_MGMT_URL = os.environ.get("RABBITMQ_MGMT_URL", "http://localhost:15672")
RABBITMQ_USER = os.environ["RABBITMQ_USER"]
RABBITMQ_PASSWORD = os.environ["RABBITMQ_PASSWORD"]
RABBITMQ_VHOST = os.environ.get("RABBITMQ_VHOST", "/")
EXCHANGE = "cubicador.eventos"

# (nombre de cola, routing key esperada) — ver MensajeriaDependencyInjection.cs
# (lado NEXO) y worker/main.py (lado Cubicador) para el origen de cada una.
# "cubicador.optimizar" es la ÚNICA cola que atienden los 9 comandos que
# valida CubicacionSolicitadaValidador (optimizar + los 8 de S3-HU-02) más
# crear_desde_linea — los 10 se despachan por el campo "tipo" dentro del
# mismo mensaje, así que un solo binding basta.
COLAS_Y_ROUTING_KEYS_ESPERADAS: list[tuple[str, str]] = [
    ("cubicador.optimizar", "cubicacion.solicitada"),
    ("cubicacion.dlq", "cubicacion.dlq"),
    ("Cubicacion.PlanDeCargueGenerado", "plan-de-cargue.generado"),
    ("Cubicacion.CubicacionRechazada", "cubicacion.rechazada"),
    ("Cubicacion.VehiculoCreado", "vehiculo.creado"),
    ("Cubicacion.VehiculoActualizado", "vehiculo.actualizado"),
    ("Cubicacion.VehiculoEliminado", "vehiculo.eliminado"),
]


def log(mensaje: str) -> None:
    print(f"[rabbitmq-smoke] {mensaje}", flush=True)


def _vhost_codificado() -> str:
    # RabbitMQ usa "%2F" para el vhost por defecto "/" en la URL de su API.
    return "%2F" if RABBITMQ_VHOST == "/" else RABBITMQ_VHOST


def main() -> None:
    auth = (RABBITMQ_USER, RABBITMQ_PASSWORD)
    vhost = _vhost_codificado()

    with httpx.Client(base_url=RABBITMQ_MGMT_URL, auth=auth, timeout=10) as client:
        log(f"conectando a {RABBITMQ_MGMT_URL}...")
        r = client.get("/api/overview")
        r.raise_for_status()
        log(f"conectado. Version RabbitMQ: {r.json().get('rabbitmq_version', '?')}")

        log(f"verificando que el exchange '{EXCHANGE}' existe (topic, durable)...")
        r = client.get(f"/api/exchanges/{vhost}/{EXCHANGE}")
        if r.status_code == 404:
            sys.exit(
                f"[rabbitmq-smoke] FALLO: el exchange '{EXCHANGE}' no existe. "
                "¿Ningún servicio (NEXO o Cubicador) se ha conectado a este broker todavía?"
            )
        r.raise_for_status()
        exchange = r.json()
        assert exchange["type"] == "topic", f"Exchange '{EXCHANGE}' existe pero no es 'topic': {exchange['type']}"
        assert exchange["durable"] is True, f"Exchange '{EXCHANGE}' existe pero no es durable."
        log("  ok.")

        r = client.get(f"/api/exchanges/{vhost}/{EXCHANGE}/bindings/source")
        r.raise_for_status()
        bindings_reales = {(b["destination"], b["routing_key"]) for b in r.json() if b["destination_type"] == "queue"}

        faltantes = [
            (cola, rk) for cola, rk in COLAS_Y_ROUTING_KEYS_ESPERADAS if (cola, rk) not in bindings_reales
        ]

        if faltantes:
            detalle = "\n".join(f"  - cola='{c}', routing_key='{rk}'" for c, rk in faltantes)
            sys.exit(
                f"[rabbitmq-smoke] FALLO: faltan {len(faltantes)} binding(s) esperado(s) en "
                f"'{EXCHANGE}':\n{detalle}\n"
                "Si esto es un broker recién desplegado con el volumen vacío, hace falta que "
                "NEXO y el worker de Cubicador se conecten al menos una vez para declararlos "
                "(ver el docstring de este script)."
            )

        log(f"los {len(COLAS_Y_ROUTING_KEYS_ESPERADAS)} bindings esperados están declarados:")
        for cola, rk in COLAS_Y_ROUTING_KEYS_ESPERADAS:
            log(f"  - {cola} <- routing_key='{rk}' [ok]")

    log("todo verificado correctamente.")


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception as exc:  # noqa: BLE001 - se quiere el traceback completo en el log de CI
        import traceback

        traceback.print_exc()
        print(f"[rabbitmq-smoke] FALLO: {exc}", file=sys.stderr)
        sys.exit(1)
