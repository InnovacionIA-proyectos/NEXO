"""Una fila del registro de diferencias entre el motor y el modelo.

Guarda LO QUE SE COMPARO y lo que costo, no lo que se envio: el contenido del
mensaje (medidas y pesos de la carga) no se persiste, igual que en el resto del
uso de IA. Con `id_recomendacion` se une contra las tablas de la recomendacion
del motor cuando haga falta mirar la carga.
"""

from __future__ import annotations

from dataclasses import dataclass

ESTADO_OK = "OK"


@dataclass(frozen=True)
class RegistroSombra:
    id_recomendacion: str
    id_linea_solicitud: str
    origen_sistema: str
    # OK, o el codigo del problema (IA_TIEMPO_AGOTADO, IA_RESPUESTA_INVALIDA, ...): una comparacion que no se pudo hacer tambien se cuenta.
    estado: str
    modelo: str | None
    esfuerzo: str | None
    resultado_motor: str
    cantidad_motor: int
    flota_motor: tuple[str, ...]
    tokens_entrada: int = 0
    tokens_salida: int = 0
    latencia_ia_ms: int | None = None
    resultado_ia: str | None = None
    motivo_motor: str | None = None
    motivo_ia: str | None = None
    cantidad_ia: int | None = None
    flota_ia: tuple[str, ...] | None = None
    coincide_resultado: bool | None = None
    coincide_flota: bool | None = None
    coincide_configuracion: bool | None = None
    ia_respeta_peso: bool | None = None
    ia_respeta_volumen: bool | None = None
    ia_conserva_unidades: bool | None = None
    violaciones: tuple[str, ...] = ()
    explicacion: str | None = None
