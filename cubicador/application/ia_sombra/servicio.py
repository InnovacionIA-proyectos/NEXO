"""Modo sombra: el modelo propone una flota en paralelo y se registra la diferencia con el motor.

REGLA QUE MANDA: la respuesta al usuario no cambia ni se retrasa. Por eso
`programar` no espera nada: decide si corresponde, agenda la comparacion como
una tarea independiente y regresa en el acto. La tarea tiene su propia sesion de
base de datos, no comparte nada con la peticion y no puede levantar una
excepcion hacia ella (todo se registra como un estado, nunca se propaga).

Cuando NO corre (y la API responde exactamente igual que sin esta funcion):
  - `IA_SOMBRA_HABILITADA` no es true (interruptor de apagado),
  - no hay llave propia del Cubicador (`ANTHROPIC_API_KEY`),
  - el muestreo la salta,
  - ya hay demasiadas comparaciones esperando turno (se descarta, no se encola la API detras del modelo),
  - se agoto el tope de tokens del dia.

El modelo no recibe la respuesta del motor: la comparacion es a ciegas.
"""

from __future__ import annotations

import asyncio
import logging
import random
from collections.abc import Callable
from dataclasses import replace
from datetime import datetime, timedelta, timezone
from typing import Any

from sqlalchemy.ext.asyncio import AsyncSession

from application.ia_sombra import prompt
from application.ia_sombra.comparador import comparar
from application.ia_sombra.configuracion import ConfiguracionSombra
from application.ia_sombra.propuesta import RespuestaInvalida, leer_propuesta
from application.ia_sombra.registro import ESTADO_OK, RegistroSombra
from application.ports.ia import IaNoDisponible, IClienteIa
from application.recomendaciones.casos_de_uso import LineaMercancia
from domain.entidades.recomendacion import RecomendacionGuardada

_log = logging.getLogger("cubicador.ia")

# El dia del tope se cuenta en hora de Colombia (UTC-5, sin horario de verano).
_COLOMBIA = timezone(timedelta(hours=-5))

FabricaDeSesion = Callable[[], Any]  # devuelve un gestor de contexto asincrono que entrega una AsyncSession
FabricaDeCliente = Callable[[ConfiguracionSombra], IClienteIa]
FabricaDeRepos = Callable[[AsyncSession], tuple[Any, Any, Any]]  # (catalogo, parametros, sombra)


class ServicioSombra:
    def __init__(
        self,
        fabrica_sesion: FabricaDeSesion,
        fabrica_repos: FabricaDeRepos,
        fabrica_cliente: FabricaDeCliente,
        config: Callable[[], ConfiguracionSombra] = ConfiguracionSombra.desde_entorno,
        azar: Callable[[], float] = random.random,
        ahora: Callable[[], datetime] = lambda: datetime.now(timezone.utc),
        margen_timeout: float = 5.0,
    ) -> None:
        self._fabrica_sesion = fabrica_sesion
        self._fabrica_repos = fabrica_repos
        self._fabrica_cliente = fabrica_cliente
        self._config = config
        self._azar = azar
        self._ahora = ahora
        # El cliente ya tiene su propio timeout; este es el de seguridad por encima del suyo.
        self._margen_timeout = margen_timeout
        self._pendientes: set[asyncio.Task] = set()
        self._semaforo: asyncio.Semaphore | None = None
        self._cliente: IClienteIa | None = None
        self._dia_del_aviso_de_tope: object = None

    # ------------------------------------------------------------- entrada

    def programar(
        self, guardada: RecomendacionGuardada, lineas: list[LineaMercancia], clase_viaje: str | None,
    ) -> bool:
        """Agenda la comparacion y regresa de inmediato. Devuelve si la agendo. Nunca levanta."""
        try:
            config = self._config()
            if not config.activa:
                return False
            if self._azar() >= config.muestreo:
                return False
            if len(self._pendientes) >= config.max_pendientes:
                _log.warning("IA sombra: %d comparaciones esperando, se descarta esta.", len(self._pendientes))
                return False
            tarea = asyncio.get_running_loop().create_task(self._ejecutar(config, guardada, list(lineas), clase_viaje))
            self._pendientes.add(tarea)
            tarea.add_done_callback(self._pendientes.discard)
            return True
        except Exception:  # noqa: BLE001 - la API no puede enterarse de que esta funcion fallo
            _log.exception("IA sombra: no se pudo agendar la comparacion.")
            return False

    async def esperar_pendientes(self, timeout: float | None = None) -> None:
        """Para el cierre ordenado (y las pruebas): espera las comparaciones en curso."""
        if self._pendientes:
            await asyncio.wait(set(self._pendientes), timeout=timeout)

    async def cerrar(self, timeout: float = 10.0) -> None:
        await self.esperar_pendientes(timeout)
        for t in list(self._pendientes):
            t.cancel()

    @property
    def pendientes(self) -> int:
        return len(self._pendientes)

    # ------------------------------------------------------------- la comparacion

    async def _ejecutar(
        self, config: ConfiguracionSombra, guardada: RecomendacionGuardada, lineas: list[LineaMercancia], clase_viaje: str | None,
    ) -> None:
        try:
            if self._semaforo is None:
                self._semaforo = asyncio.Semaphore(config.max_concurrentes)
            async with self._semaforo:
                await self._comparar(config, guardada, lineas, clase_viaje)
        except asyncio.CancelledError:
            raise
        except Exception:  # noqa: BLE001
            _log.exception("IA sombra: la comparacion fallo.")

    async def _comparar(
        self, config: ConfiguracionSombra, guardada: RecomendacionGuardada, lineas: list[LineaMercancia], clase_viaje: str | None,
    ) -> None:
        base = _base(guardada, config)
        async with self._fabrica_sesion() as sesion:
            repo_catalogo, repo_parametros, repo_sombra = self._fabrica_repos(sesion)

            if await self._tope_agotado(config, repo_sombra):
                return

            catalogo = await repo_catalogo.cargar()
            medidas = await repo_parametros.obtener()
            mensaje = prompt.construir_mensaje(catalogo, lineas, medidas, clase_viaje)
            esquema = prompt.esquema_salida(catalogo)

            try:
                respuesta = await asyncio.wait_for(
                    self._cliente_ia(config).completar(prompt.INSTRUCCIONES, mensaje, esquema),
                    timeout=config.timeout_segundos + self._margen_timeout)
            except IaNoDisponible as ex:
                await repo_sombra.guardar(_con(base, estado=ex.codigo))
                return
            except asyncio.TimeoutError:
                await repo_sombra.guardar(_con(base, estado="IA_TIEMPO_AGOTADO"))
                return

            tokens = {"tokens_entrada": respuesta.tokens_entrada + respuesta.tokens_cache, "tokens_salida": respuesta.tokens_salida,
                      "latencia_ia_ms": respuesta.latencia_ms, "modelo": respuesta.modelo}
            try:
                propuesta = leer_propuesta(respuesta.json, catalogo, len(lineas))
            except RespuestaInvalida as ex:
                _log.warning("IA sombra: respuesta invalida: %s", ex)
                await repo_sombra.guardar(_con(base, estado="IA_RESPUESTA_INVALIDA", **tokens))
                return

            c = comparar(catalogo, lineas, guardada, propuesta)
            await repo_sombra.guardar(_con(
                base, estado=ESTADO_OK, **tokens,
                resultado_ia=c.resultado_ia, motivo_ia=c.motivo_ia, cantidad_ia=c.cantidad_ia,
                flota_ia=tuple(f"{v}/{k}" for v, k in c.flota_ia),
                coincide_resultado=c.coincide_resultado, coincide_flota=c.coincide_flota,
                coincide_configuracion=c.coincide_configuracion, ia_respeta_peso=c.ia_respeta_peso,
                ia_respeta_volumen=c.ia_respeta_volumen, ia_conserva_unidades=c.ia_conserva_unidades,
                violaciones=c.violaciones, explicacion=propuesta.explicacion or None))

    def _cliente_ia(self, config: ConfiguracionSombra) -> IClienteIa:
        if self._cliente is None:
            self._cliente = self._fabrica_cliente(config)
        return self._cliente

    async def _tope_agotado(self, config: ConfiguracionSombra, repo_sombra: Any) -> bool:
        if config.tope_tokens_dia <= 0:
            return False
        ahora = self._ahora().astimezone(_COLOMBIA)
        inicio_del_dia = ahora.replace(hour=0, minute=0, second=0, microsecond=0).astimezone(timezone.utc)
        usados = await repo_sombra.tokens_desde(inicio_del_dia)
        if usados < config.tope_tokens_dia:
            return False
        if self._dia_del_aviso_de_tope != inicio_del_dia.date():
            self._dia_del_aviso_de_tope = inicio_del_dia.date()
            _log.warning("IA sombra: se alcanzo el tope de %d tokens del dia; no se compara hasta manana.", config.tope_tokens_dia)
        return True


def _base(guardada: RecomendacionGuardada, config: ConfiguracionSombra) -> RegistroSombra:
    flota = tuple(sorted(f"{v.codigo_vehiculo}/{v.codigo_carroceria}" for v in guardada.vehiculos))
    motivo = guardada.motivo_sin_seleccion
    return RegistroSombra(
        id_recomendacion=guardada.id, id_linea_solicitud=guardada.id_linea_solicitud, origen_sistema=guardada.origen_sistema,
        estado=ESTADO_OK, modelo=config.modelo, esfuerzo=config.esfuerzo, resultado_motor=guardada.resultado,
        motivo_motor=None if motivo in (None, "", "NINGUNO") else motivo,
        cantidad_motor=len(flota), flota_motor=flota)


def _con(base: RegistroSombra, **cambios: Any) -> RegistroSombra:
    return replace(base, **cambios)
