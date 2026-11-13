# Tableros y alertas de NEXO para el Grafana del servidor de monitoreo (10.10.40.252).
#
# Los crea por la API de Grafana, NO por archivos de aprovisionamiento: asi quedan guardados en la base de
# Grafana (volumen grafana-data), se pueden editar desde la interfaz y sobreviven a recrear el contenedor.
#
# OJO: despues de la primera publicacion, la fuente de verdad es la INTERFAZ de Grafana. Por eso este script
# no toca lo que ya existe; con --sobrescribir lo reemplaza y se pierde lo editado a mano en esos tableros y reglas.
#
# Uso (en el servidor, junto a tableros_base.py del stack de observabilidad):
#   sudo cp nexo_grafana.py /data/apps/observabilidad/grafana/ && cd /data/apps/observabilidad/grafana
#   set -a; . ../grafana.env; set +a
#   python3 nexo_grafana.py [--sobrescribir]
import base64
import json
import os
import sys
import urllib.error
import urllib.request

from tableros_base import *

TAG, CARPETA = "nexo", "nexo"
JA, JC, JW = 'job="nexo/nexo-api"', 'job="nexo/cubicador-api"', 'job="nexo/cubicador-worker"'
JR, JRC = 'job="nexo/rabbitmq"', 'job="nexo/rabbitmq-colas"'
# Colas donde es normal que se queden mensajes sin consumidor: las de error.
COLAS_DE_ERROR = 'queue=~".*_error|.*_skipped|.*dlq.*"'
COLAS_DE_TRABAJO = 'queue!~".*_error|.*_skipped|.*dlq.*"'
RABBIT_VIVO = f'min(up{{{JR}}}) or vector(0)'
RABBIT_ALARMAS = f'sum({{__name__=~"rabbitmq_alarms_.+",{JR}}}) or vector(0)'
COLA_MAS_LLENA = f'max(rabbitmq_detailed_queue_messages_ready{{{JRC},{COLAS_DE_TRABAJO}}}) or vector(0)'
COLAS_SIN_CONSUMIDOR = f'count(rabbitmq_detailed_queue_consumers{{{JRC},{COLAS_DE_TRABAJO}}} == 0) or vector(0)'
SONDA = 'probe_success{job="integrations/blackbox/nexo-api"}'
# Los preflight OPTIONS del navegador no son peticiones de un usuario.
SIN_HEALTH = 'http_route!~"/?health.*|/?hubs.*",http_request_method!="OPTIONS"'
PET = f'http_server_request_duration_seconds_count{{{JA},{SIN_HEALTH}}}'
BUCKETS = f'http_server_request_duration_seconds_bucket{{{JA},{SIN_HEALTH}}}'
TASA_5XX = (f'(sum(rate(http_server_request_duration_seconds_count{{{JA},{SIN_HEALTH},http_response_status_code=~"5.."}}[5m])) or vector(0))'
            f' / clamp_min(sum(rate({PET}[5m])), 1e-9)')
P95 = f'histogram_quantile(0.95, sum by (le) (rate({BUCKETS}[5m])))'
P95_CUBICACION = f'histogram_quantile(0.95, sum by (le) (rate(cubicador_mensaje_duracion_seconds_bucket{{{JW}}}[15m])))'
FALLOS_MT = f'{{__name__=~"messaging_masstransit_consume_fault.*_total",{JA}}}'
CONSUMOS_MT = f'{{__name__=~"messaging_masstransit_consume(_ea)?_total",{JA}}}'
# Lineas "resumen de peticion" que escribe nexo-api (clsObservabilidad.UseResumenDePeticion).
ACCESOS = '{service_name="nexo-api"} | scope_name="Nexo.Api.Peticion" | Metodo!="OPTIONS"'
DE_USUARIOS = ACCESOS + ' | Usuario!="anónimo"'

ENLACES = [
    {"title": "Trazas en Jaeger", "type": "link", "icon": "external link", "targetBlank": True,
     "url": "http://10.10.40.252:16686/search?service=nexo-api&lookback=custom&limit=20&start=${__from}000&end=${__to}000"},
    {"title": "Tableros del servidor", "type": "dashboards", "tags": ["servidor-tbsodiseo"],
     "asDropdown": True, "includeVars": False, "keepTime": True},
]
VIVO = [{"type": "value", "options": {"1": {"text": "✅ Funcionando", "color": VERDE}, "0": {"text": "❌ Sin señal", "color": ROJO}}},
        {"type": "special", "options": {"match": "null", "result": {"text": "❌ Sin señal", "color": ROJO}}}]
SIN_TRAFICO = [{"type": "special", "options": {"match": "null+nan", "result": {"text": "Sin tráfico", "color": AZUL}}}]
TH_ERRORES = umbrales((VERDE, None), (AMARILLO, 0.01), (ROJO, 0.05))
TH_LATENCIA = umbrales((VERDE, None), (AMARILLO, 1), (ROJO, 2))
TH_CERO_MALO = umbrales((VERDE, None), (ROJO, 1))
TH_AZUL = umbrales((AZUL, None))

PANEL = {}  # clave -> (uid del tablero, id del panel): destino del boton "ver panel" de cada alerta
_uid_actual = None


def marca(clave, panel):
    PANEL[clave] = (_uid_actual, panel["id"])
    return panel


def con_loki(panel, instantaneo=True):
    """Los ayudantes de tableros_base consultan Prometheus; esto pasa el panel a Loki."""
    panel["datasource"] = LOKI
    for t in panel["targets"]:
        t["datasource"] = LOKI
        t.pop("instant", None)
        t["queryType"] = "instant" if instantaneo else "range"
    panel["fieldConfig"]["defaults"]["noValue"] = "0"
    return panel


def accion(ruta, metodo="POST"):
    """Peticiones exitosas a una ruta de negocio. Los {id:guid} de la ruta van como '[^/]+'."""
    return (f'http_server_request_duration_seconds_count{{{JA},http_request_method="{metodo}",'
            f'http_route=~"/?{ruta}",http_response_status_code=~"2.."}}')


def total(expr):
    return f'round(sum(increase({expr}[$__range])) or vector(0))'


def vivo(job):
    return f'max(cubicador_activo{{{job}}}) or vector(0)'


def tablero(uid, titulo, desc, armar, rango="now-24h"):
    global _uid_actual
    _uid_actual = uid
    reiniciar_ids()
    d = dashboard(uid, titulo, desc, armar(), TAG, "Otros tableros de NEXO", ENLACES, rango)
    d["editable"] = True
    return d


# ── 1. Resumen ───────────────────────────────────────────────────────────────
def resumen():
    P, y = [], 0
    P.append(texto("## NEXO — ¿está funcionando bien?\nEstado de la **API de NEXO**, del **Cubicador** (API y worker) y de la "
                   "mensajería entre ellos. " + LEYENDA, 0, y, 24, 3)); y += 3
    P.append(fila("Estado en este momento", y)); y += 1
    P.append(marca("api", stat("API de NEXO", f'max({SONDA})', 0, y, 4, 4, ayuda(
        "Si la API responde a la sonda que la consulta cada 30 segundos (/health: incluye base de datos y bus de mensajes).",
        "Si dice **Sin señal**, la cabina de fletes no funciona para nadie.",
        "Revisar la app nexo-api en Dokploy y sus logs en el tablero 3."), mapeos=VIVO)))
    P.append(marca("cubicador", stat("API del Cubicador", vivo(JC), 4, y, 4, 4, ayuda(
        "Si el Cubicador (API) sigue enviando su latido de telemetría.",
        "Sin él NEXO no puede recomendar vehículo ni consultar catálogos de cubicación.",
        "Revisar la app cubicador-api en Dokploy."), mapeos=VIVO)))
    P.append(marca("worker", stat("Worker del Cubicador", vivo(JW), 8, y, 4, 4, ayuda(
        "Si el worker que procesa los planes de cargue sigue vivo.",
        "Si se cae, los planes de cargue quedan en 'procesando' y nadie los resuelve.",
        "Revisar la app cubicador-worker en Dokploy y la cola en RabbitMQ."), mapeos=VIVO)))
    P.append(marca("5xx", stat("Errores del servidor (5xx)", TASA_5XX, 12, y, 4, 4, ayuda(
        "Porcentaje de respuestas de la API que terminaron en error interno (últimos 5 min).",
        "Cada 5xx es una acción de un usuario que falló por culpa del sistema.",
        "Abrir el tablero 3 y buscar la ruta y el log del error (trae el id de traza)."),
        unidad="percentunit", th=TH_ERRORES, mapeos=SIN_TRAFICO, decimales=1)))
    P.append(marca("p95", stat("Lentitud (p95)", P95, 16, y, 4, 4, ayuda(
        "El tiempo de respuesta que cumple el 95 % de las peticiones (últimos 5 min).",
        "Sobre 1 s la aplicación se siente lenta; sobre 2 s la gente deja de esperar.",
        "Ver en el tablero 3 qué ruta o qué dependencia (SQL, otro servicio) está lenta."),
        unidad="s", th=TH_LATENCIA, mapeos=SIN_TRAFICO, decimales=2)))
    P.append(stat("Peticiones por minuto", f'sum(rate({PET}[5m])) * 60', 20, y, 4, 4, ayuda(
        "Cuántas peticiones atiende la API por minuto.", "Da la medida de cuánta gente la está usando ahora.",
        "Nada: es informativo."), th=TH_AZUL, decimales=0, color_modo="value", grafico=True)); y += 4
    P.append(lista_alertas("Alertas activas de NEXO", 0, y, 24, 5, "Alertas de NEXO que están disparadas o por dispararse.",
                           'servicio=~"nexo.*"')); y += 5
    P.append(fila("Tendencia", y)); y += 1
    P.append(serie("Peticiones por resultado", [
        (f'sum(rate({PET[:-1]},http_response_status_code=~"2..|3.."}}[5m])) * 60', "Correctas"),
        (f'sum(rate({PET[:-1]},http_response_status_code=~"4.."}}[5m])) * 60', "Rechazadas (4xx)"),
        (f'sum(rate({PET[:-1]},http_response_status_code=~"5.."}}[5m])) * 60', "Errores (5xx)")], 0, y, 12, 8, ayuda(
        "Peticiones por minuto, separadas por resultado.", "Un salto de 4xx o 5xx señala un problema antes de que alguien lo reporte.",
        "Si crecen los 5xx, ir al tablero 3."), apilado=True))
    P.append(serie("Tiempo de respuesta (p95) por servicio", [
        (P95, "API de NEXO"),
        (f'histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket{{{JC}}}[5m])))', "API del Cubicador")],
        12, y, 12, 8, ayuda("El p95 del tiempo de respuesta de cada API.", "Muestra cuál de los dos servicios se está degradando.",
                            "Si sube de forma sostenida, revisar SQL y dependencias en el tablero 3 o 4."), unidad="s")); y += 8
    return P


# ── 2. Negocio ───────────────────────────────────────────────────────────────
def negocio():
    P, y = [], 0
    P.append(texto("## NEXO — actividad del negocio\nLo que la gente está haciendo en la cabina de fletes, contado a partir de las "
                   "acciones exitosas. Los totales son del **rango de tiempo elegido arriba a la derecha**. " + LEYENDA, 0, y, 24, 3)); y += 3
    P.append(fila("Totales del periodo", y)); y += 1
    tarjetas = [
        ("Solicitudes creadas", total(accion("api/(captura-)?solicitudes")), "Solicitudes de cotización registradas (manuales o desde Excel)."),
        ("Excel analizados", total(accion("api/captura-solicitudes/(analizar-excel|leer-estructura)")), "Archivos de clientes leídos para capturar solicitudes."),
        ("Cotizaciones entregadas", total(accion("api/cotizaciones/[^/]+/entregar")), "Cotizaciones que se enviaron al cliente."),
        ("Solicitudes rechazadas", total(accion("api/solicitudes/[^/]+/rechazar")), "Solicitudes descartadas por el equipo."),
        ("Planes de cargue calculados", f'round(sum(increase(cubicador_mensajes_total{{{JW},tipo="optimizar",resultado="ok"}}[$__range])) or vector(0))',
         "Optimizaciones 3D que el Cubicador terminó bien."),
        ("Mensajes al asistente (IA)", total(accion("api/asistente/conversaciones/mensajes")), "Preguntas hechas al asistente de cotización."),
    ]
    for i, (titulo, expr, que) in enumerate(tarjetas):
        P.append(stat(titulo, expr, i * 4, y, 4, 4, ayuda(que, "Mide el uso real de la herramienta.", "Nada: es informativo."),
                      th=TH_AZUL, decimales=0, color_modo="value"))
    y += 4
    P.append(fila("Ritmo de trabajo", y)); y += 1
    P.append(serie("Acciones por hora", [
        (f'sum(increase({accion("api/(captura-)?solicitudes")}[1h]))', "Solicitudes creadas"),
        (f'sum(increase({accion("api/cotizaciones/[^/]+/entregar")}[1h]))', "Cotizaciones entregadas"),
        (f'sum(increase({accion("api/cotizaciones/[^/]+/lineas.*", "P.*")}[1h]))', "Líneas costeadas a mano"),
        (f'sum(increase({accion("api/planes-cargue/[^/]+/optimizar")}[1h]))', "Planes de cargue pedidos"),
        (f'sum(increase({accion("api/cotizaciones")}[1h]))', "Cotizaciones creadas"),
        (f'sum(increase({accion("api/cubicacion")}[1h]))', "Recomendaciones de vehículo (IA)")], 0, y, 12, 8, ayuda(
        "Cuántas acciones de cada tipo se hacen por hora.", "Muestra las horas pico y si el uso crece o cae.",
        "Una caída a cero en horario laboral con la API arriba merece una llamada al equipo comercial."), barras_tiempo=True))
    P.append(serie("Costeo automático: mensajes procesados y fallidos", [
        (f'sum(increase({CONSUMOS_MT}[1h]))', "Procesados"),
        (f'sum(increase({FALLOS_MT}[1h])) or vector(0)', "Fallidos")], 12, y, 12, 8, ayuda(
        "Mensajes de la cola interna de NEXO (costeo automático, recosteo, avisos, catálogos) por hora.",
        "Los fallidos son líneas que no se costearon solas y que alguien tendrá que revisar.",
        "Ver el detalle por tipo de mensaje en el tablero 4."), barras_tiempo=True)); y += 8
    P.append(fila("Servicios externos de los que depende el negocio", y)); y += 1
    P.append(marca("ia", serie("Inteligencia artificial (Anthropic): llamadas y errores", [
        (f'sum(rate(http_client_request_duration_seconds_count{{{JA},server_address=~".*anthropic.*"}}[5m])) * 60', "Llamadas/min"),
        (f'(sum(rate(http_client_request_duration_seconds_count{{{JA},server_address=~".*anthropic.*",http_response_status_code!~"2.."}}[5m])) or vector(0)) * 60', "Con error/min")],
        0, y, 12, 8, ayuda("Llamadas al modelo de lenguaje y cuántas fallan.", "Si falla, la lectura de Excel con IA y el asistente dejan de responder.",
                           "Revisar la llave y el tope diario (docs/despliegue/ROTACION-LLAVE-ANTHROPIC.md)."))))
    P.append(marca("correo", serie("Correo (MailSender): envíos y errores", [
        (f'sum(increase(http_client_request_duration_seconds_count{{{JA},server_address=~".*mailsender.*"}}[1h]))', "Enviados"),
        (f'sum(increase(http_client_request_duration_seconds_count{{{JA},server_address=~".*mailsender.*",http_response_status_code!~"2.."}}[1h])) or vector(0)', "Con error")],
        12, y, 12, 8, ayuda("Correos de cotización que NEXO pidió enviar y cuántos fallaron.", "Un correo fallido es una cotización que el cliente no recibió.",
                            "Revisar el servicio MailSender y reenviar desde NEXO."), barras_tiempo=True))); y += 8
    return P


# ── 3. API ───────────────────────────────────────────────────────────────────
def api():
    P, y = [], 0
    P.append(texto("## NEXO — rendimiento y errores de la API\nDetalle técnico: qué rutas fallan o están lentas, cómo responden la base de "
                   "datos y los servicios de los que depende, y los logs de error (cada uno trae su id de traza para abrirlo en Jaeger). "
                   + LEYENDA, 0, y, 24, 3)); y += 3
    P.append(fila("Rutas", y)); y += 1
    P.append(barras("Rutas más lentas (p95, última hora)", [
        (f'topk(10, histogram_quantile(0.95, sum by (le, http_route) (rate({BUCKETS}[1h]))))', "{{http_route}}")], 0, y, 12, 9, ayuda(
        "Las 10 rutas con peor tiempo de respuesta.", "Dice dónde optimizar primero.", "Abrir una traza de esa ruta en Jaeger para ver en qué se va el tiempo."),
        "s", TH_LATENCIA, sin_datos="Sin tráfico"))
    P.append(barras("Rutas con más errores 5xx (última hora)", [
        (f'topk(10, sum by (http_route) (increase(http_server_request_duration_seconds_count{{{JA},http_response_status_code=~"5.."}}[1h])))', "{{http_route}}")],
        12, y, 12, 9, ayuda("Rutas que devolvieron errores internos.", "Cada una es una función rota para algún usuario.",
                            "Buscar el log del error abajo y abrir su traza."), "short", TH_CERO_MALO, sin_datos="Sin errores")); y += 9
    P.append(fila("Dependencias", y)); y += 1
    P.append(marca("sql", serie("Base de datos: tiempo (p95) y errores", [
        (f'histogram_quantile(0.95, sum by (le, db_namespace) (rate(db_client_operation_duration_seconds_bucket{{{JA}}}[5m])))', "p95 {{db_namespace}}"),
        (f'sum by (db_namespace) (rate(db_client_operation_duration_seconds_count{{{JA},error_type!=""}}[5m])) or vector(0)', "errores/s {{db_namespace}}")],
        0, y, 12, 8, ayuda("Cuánto tardan las consultas a cada base (NEXO, portal, Data Warehouse) y si fallan.",
                           "La base es la causa más frecuente de lentitud y de errores 5xx.", "Si una base falla o se pone lenta, avisar a quien la administra."), unidad="s")))
    P.append(serie("Otros servicios (HTTP saliente): tiempo p95", [
        (f'histogram_quantile(0.95, sum by (le, server_address) (rate(http_client_request_duration_seconds_bucket{{{JA},server_address!~"10.10.40.252|obs-alloy"}}[5m])))', "{{server_address}}")],
        12, y, 12, 8, ayuda("Cuánto tardan los servicios que NEXO llama: Cubicador, MailSender, Anthropic.",
                            "Si uno se pone lento, arrastra a las pantallas que lo usan.", "Revisar ese servicio."), unidad="s")); y += 8
    P.append(fila("Salud del proceso (señales tempranas de degradación)", y)); y += 1
    P.append(marca("hilos", serie("Trabajo en espera (cola de hilos)", [(f'max(dotnet_thread_pool_queue_length{{{JA}}})', "En espera")], 0, y, 8, 7, ayuda(
        "Tareas que esperan un hilo libre.", "Si crece y no baja, la API se va a poner lenta para todos en minutos.",
        "Buscar la ruta lenta que está reteniendo hilos; reiniciar solo como último recurso."))))
    P.append(serie("Memoria del proceso", [(f'max(dotnet_process_memory_working_set_bytes{{{JA}}})', "Memoria en uso")], 8, y, 8, 7, ayuda(
        "Memoria que ocupa la API.", "Un crecimiento continuo sin bajar anuncia un reinicio por falta de memoria.",
        "Si sube día tras día, revisar con desarrollo."), unidad="bytes"))
    P.append(serie("Excepciones por minuto", [(f'sum by (error_type) (rate(dotnet_exceptions_total{{{JA}}}[5m])) * 60', "{{error_type}}")], 16, y, 8, 7, ayuda(
        "Excepciones que lanza el código, por tipo.", "Un tipo nuevo o un salto suele anteceder a los 5xx.",
        "Buscar ese tipo en los logs de abajo."))); y += 7
    P.append(fila("Logs", y)); y += 1
    P.append(logs("Errores y advertencias de la API", '{service_name="nexo-api"} | detected_level=~"error|fatal|critical|warn"', 0, y, 24, 10,
                  "Últimos errores y advertencias. Abrir una línea muestra su trace_id, con enlace directo a la traza en Jaeger.")); y += 10
    return P


# ── 4. Cubicador y mensajería ────────────────────────────────────────────────
def cubicador():
    P, y = [], 0
    P.append(texto("## NEXO — Cubicador y mensajería\nEl recorrido de un plan de cargue: NEXO publica un mensaje, el **worker** del Cubicador "
                   "lo procesa y responde por la cola. Aquí se ve si ese recorrido se atasca. " + LEYENDA, 0, y, 24, 3)); y += 3
    P.append(fila("Worker del Cubicador", y)); y += 1
    P.append(marca("dlq", stat("Mensajes perdidos (última hora)", f'round(sum(increase(cubicador_mensajes_total{{{JW},resultado=~"dlq|ilegible"}}[1h])) or vector(0))',
                               0, y, 6, 4, ayuda("Mensajes que el worker no pudo procesar ni reintentando y mandó a la cola de errores.",
                                                 "Cada uno es un plan de cargue que un usuario pidió y no va a recibir.",
                                                 "Revisar los logs del worker abajo y la cola cubicacion.dlq en RabbitMQ."), th=TH_CERO_MALO, decimales=0)))
    P.append(stat("Rechazados por reglas de negocio (última hora)", f'round(sum(increase(cubicador_mensajes_total{{{JW},resultado="rechazado"}}[1h])) or vector(0))',
                  6, y, 6, 4, ayuda("Solicitudes que el Cubicador rechazó por datos inválidos (medidas, pesos, vehículo en uso).",
                                    "No es una falla del sistema, pero muchos seguidos señalan datos mal capturados.",
                                    "Ver el código de rechazo en los logs."), th=umbrales((VERDE, None), (AMARILLO, 5)), decimales=0))
    P.append(marca("lenta", stat("Tiempo de cálculo (p95)", P95_CUBICACION, 12, y, 6, 4, ayuda(
        "Cuánto tarda el worker en procesar un mensaje.", "A los 5 minutos el plan se da por perdido (timeout).",
        "Si se acerca a 2 min, revisar el tamaño de los planes y la carga del servidor."),
        unidad="s", th=umbrales((VERDE, None), (AMARILLO, 60), (ROJO, 120)), mapeos=SIN_TRAFICO, decimales=1)))
    P.append(stat("Procesados bien (última hora)", f'round(sum(increase(cubicador_mensajes_total{{{JW},resultado="ok"}}[1h])) or vector(0))',
                  18, y, 6, 4, ayuda("Mensajes terminados correctamente.", "Da la medida del trabajo real del Cubicador.", "Nada: es informativo."),
                  th=TH_AZUL, decimales=0, color_modo="value")); y += 4
    P.append(serie("Mensajes por tipo y resultado", [
        (f'sum by (tipo, resultado) (increase(cubicador_mensajes_total{{{JW}}}[15m]))', "{{tipo}} · {{resultado}}")], 0, y, 12, 8, ayuda(
        "Qué operaciones procesa el worker y cómo terminan.", "Separa el trabajo normal de los reintentos y las pérdidas.",
        "Un tipo con muchos 'reintento' o 'dlq' apunta al caso de uso con problemas."), apilado=True, barras_tiempo=True))
    P.append(serie("Tiempo de cálculo por tipo (p95)", [
        (f'histogram_quantile(0.95, sum by (le, tipo) (rate(cubicador_mensaje_duracion_seconds_bucket{{{JW}}}[15m])))', "{{tipo}}")], 12, y, 12, 8, ayuda(
        "El p95 de cada operación del worker.", "La optimización es la más pesada: es la que puede llegar al timeout.", "Ver arriba."), unidad="s")); y += 8
    P.append(fila("Mensajería de NEXO (MassTransit)", y)); y += 1
    P.append(marca("mt", serie("Mensajes fallidos por tipo", [
        (f'sum by (messaging_masstransit_message_type) (increase({FALLOS_MT}[15m]))', "{{messaging_masstransit_message_type}}")], 0, y, 12, 8, ayuda(
        "Mensajes que un consumidor de NEXO no pudo procesar.", "Tras agotar reintentos van a la cola de error y el equipo recibe un correo.",
        "Abrir la traza del mensaje en Jaeger: muestra dónde falló."), barras_tiempo=True)))
    P.append(serie("Mensajes procesados por tipo", [
        (f'sum by (messaging_masstransit_message_type) (increase({CONSUMOS_MT}[15m]))', "{{messaging_masstransit_message_type}}")], 12, y, 12, 8, ayuda(
        "Volumen de cada tipo de mensaje.", "Muestra qué procesos automáticos están trabajando.", "Nada: es informativo."), apilado=True, barras_tiempo=True)); y += 8
    P.append(fila("API del Cubicador y logs", y)); y += 1
    P.append(serie("API del Cubicador: peticiones por resultado", [
        (f'sum by (http_response_status_code) (rate(http_server_request_duration_seconds_count{{{JC}}}[5m])) * 60', "{{http_response_status_code}}")],
        0, y, 8, 9, ayuda("Peticiones por minuto que recibe el Cubicador, por código de respuesta.", "Los 5xx/503 son recomendaciones de vehículo que NEXO no pudo obtener.",
                          "Revisar los logs de la derecha."), apilado=True))
    P.append(logs("Errores y advertencias del Cubicador (API y worker)", '{service_name=~"cubicador-.*"} | detected_level=~"error|fatal|critical|warn"',
                  8, y, 16, 9, "Últimos errores y advertencias del Cubicador, con su id de traza.")); y += 9
    return P


# ── 5. Usuarios ──────────────────────────────────────────────────────────────
def usuarios():
    P, y = [], 0
    P.append(texto("## NEXO — usuarios y actividad reciente\nQuién está usando NEXO y qué está haciendo, a partir del registro de cada petición "
                   "(usuario, rol, acción y resultado). " + LEYENDA, 0, y, 24, 3)); y += 3
    P.append(fila("Ahora mismo", y)); y += 1
    for i, (titulo, ventana) in enumerate([("Usuarios activos (últimos 15 min)", "15m"), ("Usuarios activos (última hora)", "1h"),
                                           ("Usuarios activos (últimas 24 h)", "24h")]):
        P.append(con_loki(stat(titulo, f'count(sum by (Usuario) (count_over_time({DE_USUARIOS} [{ventana}])))', i * 6, y, 6, 4, ayuda(
            "Personas distintas que hicieron al menos una acción en ese tiempo.", "Mide la adopción real de la herramienta.",
            "Nada: es informativo."), th=TH_AZUL, decimales=0, color_modo="value")))
    P.append(con_loki(stat("Accesos rechazados (última hora)", f'sum(count_over_time({ACCESOS} | Codigo=~"401|403" [1h]))', 18, y, 6, 4, ayuda(
        "Peticiones rechazadas por falta de sesión (401) o de permiso (403).", "Muchas seguidas pueden ser un rol mal asignado o un intento de acceso indebido.",
        "Ver abajo quién y a qué ruta."), th=umbrales((VERDE, None), (AMARILLO, 20), (ROJO, 100)), decimales=0))); y += 4
    P.append(con_loki(barras("Quién está activo (acciones en la última hora)", [
        (f'topk(25, sum by (Nombre, Rol) (count_over_time({DE_USUARIOS} [1h])))', "{{Nombre}} ({{Rol}})")], 0, y, 12, 10,
        "Cada persona activa en la última hora, con su rol y cuántas acciones hizo.", "short", TH_AZUL, sin_datos="Nadie activo")))
    P.append(con_loki(barras("Uso por rol (últimas 24 h)", [
        (f'sum by (Rol) (count_over_time({DE_USUARIOS} [24h]))', "{{Rol}}")], 12, y, 12, 10,
        "Cuánto usa NEXO cada rol (comercial, líder de proceso, tarifas...).", "short", TH_AZUL, sin_datos="Sin actividad"))); y += 10
    P.append(fila("Actividad más reciente", y)); y += 1
    P.append(logs("Últimas acciones (quién hizo qué y cuándo)", DE_USUARIOS + ' | Metodo!="GET"', 0, y, 24, 11,
                  "Cada línea es una acción que cambió datos: método y ruta, resultado, duración, usuario, rol y el id de traza para ver el detalle en Jaeger.")); y += 11
    P.append(fila("Tendencia", y)); y += 1
    P.append(con_loki(serie("Usuarios activos por hora", [(f'count(sum by (Usuario) (count_over_time({DE_USUARIOS} [1h])))', "Usuarios activos")],
                            0, y, 12, 8, "Personas distintas que usaron NEXO en cada hora.", barras_tiempo=True), instantaneo=False))
    P.append(logs("Accesos rechazados (401 / 403)", ACCESOS + ' | Codigo=~"401|403"', 12, y, 12, 8,
                  "Quién intentó entrar a qué sin sesión o sin permiso.")); y += 8
    return P


# ── 6. RabbitMQ ──────────────────────────────────────────────────────────────
def rabbitmq():
    P, y = [], 0
    P.append(texto("## NEXO — RabbitMQ (colas y mensajes)\nRabbitMQ es el buzón por donde NEXO y el Cubicador se pasan el trabajo. Si se cae o una cola "
                   "se llena, las cotizaciones dejan de costearse solas y los planes de cargue se quedan en 'procesando'. " + LEYENDA, 0, y, 24, 3)); y += 3
    P.append(fila("Estado", y)); y += 1
    P.append(marca("rabbit", stat("RabbitMQ", RABBIT_VIVO, 0, y, 4, 4, ayuda(
        "Si el servidor de monitoreo logra leer las métricas de RabbitMQ.", "Sin RabbitMQ no hay costeo automático ni planes de cargue.",
        "Revisar el servicio nexo-rabbit en Dokploy."), mapeos=VIVO, th=umbrales((ROJO, None), (VERDE, 1)))))
    P.append(marca("rabbit-alarmas", stat("Alarmas activas", RABBIT_ALARMAS, 4, y, 4, 4, ayuda(
        "Alarmas de memoria, disco o archivos abiertos de RabbitMQ.", "Con una alarma activa RabbitMQ deja de aceptar mensajes nuevos.",
        "Ver la fila 'Recursos' y liberar memoria o disco en el servidor."), th=TH_CERO_MALO, decimales=0)))
    P.append(marca("rabbit-cola", stat("Cola más llena (mensajes en espera)", COLA_MAS_LLENA, 8, y, 4, 4, ayuda(
        "Cuántos mensajes esperan en la cola con más trabajo acumulado.", "Una cola que crece es trabajo que entra más rápido de lo que se atiende.",
        "Ver abajo cuál cola es y si su consumidor está vivo."), th=umbrales((VERDE, None), (AMARILLO, 50), (ROJO, 200)), decimales=0)))
    P.append(marca("rabbit-consumidor", stat("Colas sin quien las atienda", COLAS_SIN_CONSUMIDOR, 12, y, 4, 4, ayuda(
        "Colas de trabajo que no tienen ningún consumidor conectado.", "Lo que llegue a esas colas se queda esperando indefinidamente.",
        "Ver abajo cuáles son y reiniciar el servicio que las atiende (API de NEXO o worker del Cubicador)."), th=TH_CERO_MALO, decimales=0)))
    P.append(stat("Mensajes en colas de error", f'sum(rabbitmq_detailed_queue_messages{{{JRC},{COLAS_DE_ERROR}}}) or vector(0)', 16, y, 4, 4, ayuda(
        "Mensajes que fallaron y quedaron guardados en las colas de error.", "Cada uno es un trabajo que no se hizo y que nadie va a reintentar solo.",
        "Revisar el mensaje en la consola de RabbitMQ y la traza en Jaeger."), th=umbrales((VERDE, None), (AMARILLO, 1)), decimales=0))
    P.append(stat("Conexiones", f'max(rabbitmq_connections{{{JR}}})', 20, y, 4, 4, ayuda(
        "Servicios conectados a RabbitMQ.", "Lo normal es un número estable; una caída indica un servicio desconectado.",
        "Comparar con los servicios vivos en el tablero 1."), th=TH_AZUL, decimales=0, color_modo="value")); y += 4
    P.append(fila("Flujo de mensajes", y)); y += 1
    P.append(serie("Mensajes por minuto", [
        (f'sum(rate(rabbitmq_global_messages_received_total{{{JR}}}[5m])) * 60', "Publicados"),
        (f'sum(rate(rabbitmq_global_messages_delivered_total{{{JR}}}[5m])) * 60', "Entregados"),
        (f'sum(rate(rabbitmq_global_messages_acknowledged_total{{{JR}}}[5m])) * 60', "Confirmados"),
        (f'sum(rate(rabbitmq_global_messages_redelivered_total{{{JR}}}[5m])) * 60', "Reentregados")], 0, y, 12, 8, ayuda(
        "Mensajes que entran a RabbitMQ, los que se entregan a un servicio y los que ese servicio confirma.",
        "Publicados y confirmados deben ir parejos; los reentregados son reintentos.",
        "Si se publica y no se confirma, ver las colas de abajo.")))
    P.append(serie("Mensajes sin destino", [
        (f'sum(increase(rabbitmq_global_messages_unroutable_dropped_total{{{JR}}}[15m]))', "Descartados"),
        (f'sum(increase(rabbitmq_global_messages_unroutable_returned_total{{{JR}}}[15m]))', "Devueltos")], 12, y, 12, 8, ayuda(
        "Mensajes publicados que ninguna cola recibe, así que RabbitMQ los descarta.",
        "Si alguien debía enterarse de ese mensaje, no se enteró. Si nadie lo esperaba, es solo ruido.",
        "Buscar qué tipo de mensaje se publica sin cola asociada."), barras_tiempo=True)); y += 8
    P.append(fila("Colas", y)); y += 1
    P.append(serie("Mensajes en espera por cola", [
        (f'rabbitmq_detailed_queue_messages{{{JRC},{COLAS_DE_TRABAJO}}} > 0', "{{queue}}")], 0, y, 12, 9, ayuda(
        "Las colas de trabajo que tienen mensajes pendientes; las vacías no aparecen.", "Señala exactamente qué proceso se está atrasando.",
        "Una línea que sube sin bajar: revisar el servicio que consume esa cola.")))
    P.append(serie("Mensajes en colas de error", [
        (f'rabbitmq_detailed_queue_messages{{{JRC},{COLAS_DE_ERROR}}} > 0', "{{queue}}")], 12, y, 6, 9, ayuda(
        "Mensajes fallidos guardados, por cola de error.", "Dice qué proceso es el que está fallando.", "Ver el tablero 4 y la traza del mensaje.")))
    P.append(serie("Colas sin quien las atienda", [
        (f'rabbitmq_detailed_queue_consumers{{{JRC},{COLAS_DE_TRABAJO}}} == 0', "{{queue}}")], 18, y, 6, 9, ayuda(
        "Colas de trabajo sin consumidor; si todo está bien este panel sale vacío.", "Dice qué servicio se desconectó.",
        "Reiniciar el servicio que atiende esa cola."))); y += 9
    P.append(fila("Recursos de RabbitMQ", y)); y += 1
    P.append(serie("Memoria usada frente al límite", [
        (f'max(rabbitmq_process_resident_memory_bytes{{{JR}}}) / max(rabbitmq_resident_memory_limit_bytes{{{JR}}})', "Memoria")], 0, y, 12, 7, ayuda(
        "Qué parte de su límite de memoria usa RabbitMQ.", "Al llegar al límite RabbitMQ bloquea a quienes publican.",
        "Si pasa de 80 %, buscar colas acumuladas."), unidad="percentunit", maximo=1))
    P.append(serie("Disco disponible", [(f'max(rabbitmq_disk_space_available_bytes{{{JR}}})', "Disponible")], 12, y, 12, 7, ayuda(
        "Espacio libre en el disco donde RabbitMQ guarda los mensajes.", "Sin disco RabbitMQ deja de aceptar mensajes.",
        "Liberar espacio en el nodo del clúster."), unidad="bytes")); y += 7
    return P


TABLEROS = [
    ("nexo-resumen", "1. Resumen de salud de NEXO", "Estado de NEXO y del Cubicador de un vistazo.", resumen, "now-24h"),
    ("nexo-negocio", "2. Actividad del negocio", "Solicitudes, cotizaciones, planes de cargue y servicios externos.", negocio, "now-24h"),
    ("nexo-api", "3. Rendimiento y errores de la API", "Rutas, dependencias, salud del proceso y logs.", api, "now-6h"),
    ("nexo-cubicador", "4. Cubicador y mensajería", "Worker, colas y API del Cubicador.", cubicador, "now-6h"),
    ("nexo-usuarios", "5. Usuarios y actividad reciente", "Quién usa NEXO y qué hace.", usuarios, "now-24h"),
    ("nexo-rabbitmq", "6. RabbitMQ (colas y mensajes)", "Estado del bus de mensajes, colas y recursos.", rabbitmq, "now-6h"),
]


# ── Alertas ──────────────────────────────────────────────────────────────────
def regla(uid, titulo, expr, comparador, umbral, por, severidad, panel, resumen_, servicio="nexo-api",
          sin_datos="OK", pausada=False, ventana=600):
    tablero_uid, panel_id = PANEL[panel]
    return {
        "uid": uid, "title": titulo, "ruleGroup": "nexo", "folderUID": CARPETA, "orgID": 1, "condition": "C", "for": por,
        "noDataState": sin_datos, "execErrState": "Error", "isPaused": pausada,
        "labels": {"severidad": severidad, "servicio": servicio},
        "annotations": {"summary": resumen_, "__dashboardUid__": tablero_uid, "__panelId__": str(panel_id)},
        "data": [
            {"refId": "A", "datasourceUid": "prometheus", "relativeTimeRange": {"from": ventana, "to": 0},
             "model": {"refId": "A", "expr": expr, "instant": True}},
            {"refId": "C", "datasourceUid": "__expr__", "relativeTimeRange": {"from": 0, "to": 0},
             "model": {"refId": "C", "type": "threshold", "expression": "A", "datasource": {"type": "__expr__", "uid": "__expr__"},
                       "conditions": [{"evaluator": {"type": comparador, "params": [umbral]}}]}},
        ],
    }


def reglas():
    # "Estuvo vivo en las ultimas 24 h y ya no": no dispara para un servicio que nunca ha reportado.
    def dejo_de_reportar(job):
        return f'(max(max_over_time(cubicador_activo{{{job}}}[24h])) unless max(cubicador_activo{{{job}}})) or vector(0)'
    return [
        regla("nexo-api-caida", "API de NEXO caída", f'min({SONDA})', "lt", 1, "2m", "critica", "api",
              "La sonda a /health de NEXO falla hace más de 2 minutos: la cabina de fletes no responde, o perdió la base de datos o el bus de mensajes.",
              sin_datos="Alerting", ventana=300),
        regla("nexo-errores-5xx", "NEXO con más de 5 % de errores 5xx", TASA_5XX, "gt", 0.05, "5m", "alta", "5xx",
              "Más del 5 % de las respuestas de la API son errores internos (5 min). Revisar rutas y logs en el tablero 3."),
        regla("nexo-latencia-p95", "NEXO lento (p95 > 2 s)", P95, "gt", 2, "10m", "media", "p95",
              "El tiempo de respuesta p95 de la API supera 2 s hace 10 minutos. Aún funciona, pero se está degradando.", ventana=900),
        regla("nexo-errores-sql", "NEXO con errores contra SQL Server",
              f'sum(rate(db_client_operation_duration_seconds_count{{{JA},error_type!=""}}[5m])) or vector(0)', "gt", 0, "5m", "alta", "sql",
              "La API registra errores en operaciones contra alguna de sus bases durante 5 minutos."),
        regla("nexo-hilos-en-espera", "NEXO acumulando trabajo en espera (preventiva)",
              f'max(dotnet_thread_pool_queue_length{{{JA}}})', "gt", 50, "5m", "media", "hilos",
              "La cola de hilos de la API lleva 5 minutos por encima de 50: es la antesala de una API lenta para todos."),
        regla("nexo-cubicador-sin-senal", "API del Cubicador dejó de reportar", dejo_de_reportar(JC), "gt", 0, "5m", "alta", "cubicador",
              "La API del Cubicador dejó de enviar su latido hace más de 5 minutos.", servicio="nexo-cubicador", pausada=True),
        regla("nexo-worker-sin-senal", "Worker del Cubicador dejó de reportar", dejo_de_reportar(JW), "gt", 0, "5m", "critica", "worker",
              "El worker del Cubicador dejó de enviar su latido: los planes de cargue se quedan en 'procesando'.", servicio="nexo-cubicador", pausada=True),
        regla("nexo-mensajes-perdidos", "Cubicador perdió mensajes (cola de errores)",
              f'sum(increase(cubicador_mensajes_total{{{JW},resultado=~"dlq|ilegible"}}[15m])) or vector(0)', "gt", 0, "0m", "alta", "dlq",
              "El worker mandó mensajes a la cola de errores en los últimos 15 minutos: hay planes de cargue que no se van a resolver solos.",
              servicio="nexo-cubicador", ventana=1800),
        regla("nexo-cubicacion-lenta", "Cubicación acercándose al timeout (preventiva)", P95_CUBICACION, "gt", 120, "10m", "media", "lenta",
              "El p95 del cálculo del Cubicador supera 2 minutos; a los 5 el plan se da por perdido.", servicio="nexo-cubicador", ventana=1800),
        regla("nexo-mensajeria-fallos", "NEXO con mensajes internos fallando",
              f'sum(increase({FALLOS_MT}[15m])) or vector(0)', "gt", 0, "5m", "alta", "mt",
              "Consumidores de la mensajería de NEXO (costeo automático, avisos, catálogos) están fallando hace 5 minutos.", ventana=1800),
        regla("nexo-ia-fallando", "Inteligencia artificial respondiendo con errores",
              f'(sum(rate(http_client_request_duration_seconds_count{{{JA},server_address=~".*anthropic.*",http_response_status_code!~"2.."}}[10m])) or vector(0))'
              f' / clamp_min(sum(rate(http_client_request_duration_seconds_count{{{JA},server_address=~".*anthropic.*"}}[10m])), 1e-9)',
              "gt", 0.2, "10m", "media", "ia", "Más del 20 % de las llamadas al modelo de lenguaje fallan: lectura de Excel con IA y asistente afectados.", ventana=1200),
        regla("nexo-correo-fallando", "NEXO con envíos de correo fallando",
              f'sum(increase(http_client_request_duration_seconds_count{{{JA},server_address=~".*mailsender.*",http_response_status_code!~"2.."}}[15m])) or vector(0)',
              "gt", 0, "5m", "media", "correo", "Hay cotizaciones cuyo correo falló en los últimos 15 minutos.", ventana=1800),
        regla("nexo-rabbit-caido", "RabbitMQ de NEXO no responde", RABBIT_VIVO, "lt", 1, "3m", "critica", "rabbit",
              "El servidor de monitoreo no logra leer RabbitMQ hace 3 minutos: sin él no hay costeo automático ni planes de cargue.",
              servicio="nexo-rabbitmq", sin_datos="Alerting", ventana=300),
        regla("nexo-rabbit-alarma", "RabbitMQ con alarma de memoria o disco", RABBIT_ALARMAS, "gt", 0, "1m", "critica", "rabbit-alarmas",
              "RabbitMQ activó una alarma de recursos y está bloqueando la publicación de mensajes.", servicio="nexo-rabbitmq", ventana=300),
        regla("nexo-rabbit-cola-acumulada", "RabbitMQ acumulando mensajes en una cola (preventiva)", COLA_MAS_LLENA, "gt", 50, "10m", "media", "rabbit-cola",
              "Una cola de trabajo lleva 10 minutos con más de 50 mensajes en espera: el trabajo entra más rápido de lo que se atiende.",
              servicio="nexo-rabbitmq", ventana=900),
        regla("nexo-rabbit-sin-consumidor", "RabbitMQ con una cola sin quien la atienda (preventiva)", COLAS_SIN_CONSUMIDOR, "gt", 0, "5m", "alta", "rabbit-consumidor",
              "Una cola de trabajo lleva 5 minutos sin consumidor: lo que llegue a ella se queda esperando. Ver cuál en el tablero 6.",
              servicio="nexo-rabbitmq", ventana=600),
    ]


# ── Publicación por la API de Grafana ────────────────────────────────────────
def llamar(metodo, ruta, cuerpo=None):
    credencial = base64.b64encode(f'{os.environ["GF_SECURITY_ADMIN_USER"]}:{os.environ["GF_SECURITY_ADMIN_PASSWORD"]}'.encode()).decode()
    peticion = urllib.request.Request(
        os.environ.get("GRAFANA_URL", "http://localhost:3000") + ruta, method=metodo,
        data=json.dumps(cuerpo).encode() if cuerpo is not None else None,
        # X-Disable-Provenance: sin ella las reglas creadas por API quedan bloqueadas para edicion en la interfaz.
        headers={"Authorization": "Basic " + credencial, "Content-Type": "application/json", "X-Disable-Provenance": "true"})
    try:
        with urllib.request.urlopen(peticion, timeout=30) as respuesta:
            return respuesta.status, respuesta.read().decode()
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode()


def publicar(sobrescribir):
    fallos = 0
    codigo, texto_ = llamar("POST", "/api/folders", {"uid": CARPETA, "title": "NEXO"})
    print("carpeta NEXO:", "creada" if codigo == 200 else "ya existía" if codigo in (409, 412) else f"ERROR {codigo} {texto_}")

    for uid, titulo, desc, armar, rango in TABLEROS:
        codigo, texto_ = llamar("POST", "/api/dashboards/db", {
            "dashboard": tablero(uid, titulo, desc, armar, rango), "folderUid": CARPETA, "overwrite": sobrescribir,
            "message": "Publicado por nexo_grafana.py"})
        if codigo in (409, 412) and not sobrescribir:
            print(f"tablero {uid}: ya existe, no se toca")
        else:
            fallos += codigo != 200
            print(f"tablero {uid}:", "publicado" if codigo == 200 else f"ERROR {codigo} {texto_}")

    for r in reglas():
        existe = llamar("GET", f'/api/v1/provisioning/alert-rules/{r["uid"]}')[0] == 200
        if existe and not sobrescribir:
            print(f'regla {r["uid"]}: ya existe, no se toca')
            continue
        codigo, texto_ = (llamar("PUT", f'/api/v1/provisioning/alert-rules/{r["uid"]}', r) if existe
                          else llamar("POST", "/api/v1/provisioning/alert-rules", r))
        fallos += codigo not in (200, 201)
        print(f'regla {r["uid"]}:', "publicada" + (" (en pausa)" if r["isPaused"] else "") if codigo in (200, 201) else f"ERROR {codigo} {texto_}")
    return fallos


if __name__ == "__main__":
    sys.exit(1 if publicar("--sobrescribir" in sys.argv) else 0)
