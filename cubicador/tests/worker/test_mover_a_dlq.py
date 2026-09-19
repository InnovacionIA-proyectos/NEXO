"""Cubre el criterio de cierre "Reintentos + DLQ + alerta ante fallos
repetidos del motor" (ver docs/DECISIONES_TECNICAS.md #10): antes de esto la
cola `cubicacion.dlq` existia pero nadie alertaba al equipo cuando un mensaje
caia ahi. Verifica dos cosas por separado:

1. Que `worker/main.py::_mover_a_dlq` SIEMPRE dispara la alerta (sin broker
   real - publisher y mensaje son dobles de prueba).
2. Que `infrastructure/alertas/correo.py::alertar_mensaje_en_dlq` degrada en
   silencio (no lanza) cuando falta configuracion de SMTP - un correo que no
   sale no debe tumbar el worker.
"""

from __future__ import annotations

import os

os.environ.setdefault("DATABASE_URL", "sqlite+aiosqlite:///:memory:")

import worker.main as worker_main
from infrastructure.alertas import correo as alertas_correo


class _MensajeFalso:
    body = b'{"tipo": "optimizar", "planId": "p1", "correlationId": "p1"}'


class _PublisherFalso:
    def __init__(self) -> None:
        self.publicado: tuple[str, bytes, dict] | None = None

    async def publicar_crudo(self, routing_key: str, body: bytes, headers: dict | None = None) -> None:
        self.publicado = (routing_key, body, headers or {})


async def test_mover_a_dlq_dispara_la_alerta(monkeypatch):
    llamadas: list[tuple[str, str, int]] = []

    async def _alerta_falsa(tipo: str, error: str, intentos: int) -> None:
        llamadas.append((tipo, error, intentos))

    monkeypatch.setattr(worker_main, "alertar_mensaje_en_dlq", _alerta_falsa)
    publisher = _PublisherFalso()

    await worker_main._mover_a_dlq(_MensajeFalso(), publisher, "optimizar", "boom", 3)

    assert llamadas == [("optimizar", "boom", 3)]
    assert publisher.publicado is not None
    routing_key, _, headers = publisher.publicado
    assert routing_key == worker_main.COLA_DLQ
    assert headers == {"x-intentos": 3, "x-error": "boom"}


async def test_alertar_mensaje_en_dlq_degrada_sin_smtp_configurado(monkeypatch, caplog):
    monkeypatch.setattr(alertas_correo, "SMTP_HOST", "")

    # No debe lanzar - un correo que no sale no debe tumbar el worker.
    await alertas_correo.alertar_mensaje_en_dlq("optimizar", "boom", 3)

    assert "ALERTAS_SMTP_HOST no configurado" in caplog.text
