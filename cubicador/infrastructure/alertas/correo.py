"""Alerta por correo cuando un mensaje agota reintentos y cae a
`cubicacion.dlq` (criterio de cierre de la HU: "Reintentos + DLQ + alerta
ante fallos repetidos del motor" - ver worker/main.py::_mover_a_dlq).

SMTP directo, sin depender de que NEXO este arriba: si el motor de
Cubicador falla, tiene que poder avisar solo, sin llamar de vuelta a otro
servicio. Mismo patron que NEXO (`AlertaDeFalloConsumer`, ver
NEXO/docs/mensajeria/TOPOLOGIA.md #5) - un correo por destinatario
configurado, degrada en silencio (log, no excepcion) si falta configuracion:
un correo que no sale no debe tumbar el worker.
"""

from __future__ import annotations

import asyncio
import logging
import os
import smtplib
from email.message import EmailMessage

logger = logging.getLogger("cubicador.alertas")

SMTP_HOST = os.environ.get("ALERTAS_SMTP_HOST", "")
SMTP_PUERTO = int(os.environ.get("ALERTAS_SMTP_PUERTO", "587"))
SMTP_SSL = os.environ.get("ALERTAS_SMTP_SSL", "true").lower() == "true"
SMTP_REMITENTE = os.environ.get("ALERTAS_SMTP_REMITENTE", "cubicador@local")
SMTP_USUARIO = os.environ.get("ALERTAS_SMTP_USUARIO", "")
SMTP_CLAVE = os.environ.get("ALERTAS_SMTP_CLAVE", "")

# Mismo equipo que ya recibe la alerta equivalente del lado de NEXO (#13,
# Alertas:EquipoTecnico en appsettings.json) - no son secretos, van
# committeados como default; ALERTAS_EQUIPO_TECNICO permite sobreescribirlos.
DESTINATARIOS_DEFAULT = [
    "scalderon@boterosoto.com.co", "dguerra@boterosoto.com.co", "jpvelez@boterosoto.com.co",
    "kcano@boterosoto.com.co", "ivasco@boterosoto.com.co", "bquintero@boterosoto.com.co",
]


def _destinatarios() -> list[str]:
    crudo = os.environ.get("ALERTAS_EQUIPO_TECNICO", "")
    configurados = [d.strip() for d in crudo.split(",") if d.strip()]
    return configurados or DESTINATARIOS_DEFAULT


def _enviar_sync(destinatario: str, asunto: str, cuerpo: str) -> None:
    mensaje = EmailMessage()
    mensaje["Subject"] = asunto
    mensaje["From"] = SMTP_REMITENTE
    mensaje["To"] = destinatario
    mensaje.set_content(cuerpo)

    with smtplib.SMTP(SMTP_HOST, SMTP_PUERTO, timeout=10) as smtp:
        if SMTP_SSL:
            smtp.starttls()
        if SMTP_USUARIO:
            smtp.login(SMTP_USUARIO, SMTP_CLAVE)
        smtp.send_message(mensaje)


async def alertar_mensaje_en_dlq(tipo: str, error: str, intentos: int) -> None:
    """Envia un correo por cada destinatario configurado. No lanza: un fallo
    de correo se registra y sigue, nunca tumba el worker (ver docstring del
    modulo)."""
    if not SMTP_HOST:
        logger.warning("ALERTAS_SMTP_HOST no configurado - nadie recibio correo por este fallo.")
        return

    asunto = f"[Cubicador] Mensaje {tipo} cayo a la cola de error tras {intentos} intentos"
    cuerpo = (
        f"Un mensaje de tipo {tipo} agoto sus reintentos ({intentos}) y quedo "
        "en cubicacion.dlq para inspeccion manual.\n\n"
        f"Motivo: {error}\n\n"
        "Revisa la consola de administracion de RabbitMQ (cola cubicacion.dlq) "
        "para ver el mensaje completo y decidir si reintentar manualmente."
    )
    for destinatario in _destinatarios():
        try:
            await asyncio.to_thread(_enviar_sync, destinatario, asunto, cuerpo)
            logger.info("Correo de alerta enviado a %s", destinatario)
        except Exception:  # noqa: BLE001 - un correo que falla no debe tumbar el worker
            logger.exception("No se pudo enviar el correo de alerta a %s", destinatario)
