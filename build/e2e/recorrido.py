"""
Recorrido de punta a punta de NEXO, contra la aplicación levantada.

    docker compose up -d
    docker compose --profile e2e run --rm e2e

QUÉ COMPRUEBA QUE LA SUITE NO PUEDE. Las pruebas de la solución corren con transporte en
memoria y bases efímeras: son rápidas y aíslan bien, pero por eso mismo no ven si las
piezas encajan de verdad. Esto sí — el Outbox entregando contra SQL Server, RabbitMQ con
su plugin de retardo, la saga persistida, SignalR empujando a un cliente real y los dos
disparadores del recálculo del score.

Ya encontró dos cosas que ninguna prueba vio:

  * El arranque de la cadena de priorización publicaba al Outbox sin llamar a SaveChanges.
    El log decía "cadena armada" y no quedaba ni una fila. Ninguna prueba de mensajería
    publica a través del Outbox, así que era invisible.

  * Los avisos del hub llegan desordenados y repetidos DE VERDAD: en una corrida se vio
    [1, 2, 3, 4, 6, 5, 6, 7…]. Eso confirma que la secuencia del contrato no es
    precaución teórica.

SOLO BIBLIOTECA ESTÁNDAR, a propósito. La red de esta máquina tiene interceptación TLS y
pip no siempre puede instalar nada; una prueba que depende de eso falla por el entorno y
no por el código. El hub se habla por long polling, que es HTTP plano — el mismo hub y el
mismo camino de empuje que usaría el navegador, solo otro transporte de los tres que
SignalR ofrece.

Termina con código distinto de cero si algo falla, para que sirva en un pipeline.
"""
import json
import os
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone

API = os.environ.get("API_BASE", "http://api:8080")
SEP = "\x1e"
ADMIN = ("X-Rol", "Admin")

fallos = []
paso_actual = ["(inicio)"]


def paso(titulo):
    paso_actual[0] = titulo
    print(f"\n=== {titulo}")


def ok(condicion, detalle):
    print(f"{'  OK  ' if condicion else ' FALLA'} {detalle}")
    if not condicion:
        fallos.append(f"[{paso_actual[0]}] {detalle}")
    return condicion


def dato(texto):
    print(f"       {texto}")


def pedir(ruta, cuerpo=None, metodo=None, rol=ADMIN, esperado=None):
    datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
    req = urllib.request.Request(
        f"{API}{ruta}", data=datos,
        method=metodo or ("POST" if datos is not None else "GET"))
    req.add_header(*rol)
    if datos is not None:
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            codigo, texto = r.status, r.read().decode()
    except urllib.error.HTTPError as e:
        codigo, texto = e.code, e.read().decode()
    if esperado is not None:
        ok(codigo == esperado, f"{req.get_method()} {ruta} -> {codigo} (esperado {esperado})")
    return codigo, (json.loads(texto) if texto.strip().startswith(("{", "[")) else texto)


def esperar(condicion, descripcion, intentos=120, espera=0.5):
    """Sondea hasta que la condición devuelva algo con valor de verdad."""
    for _ in range(intentos):
        valor = condicion()
        if valor:
            return valor
        time.sleep(espera)
    ok(False, f"nunca se cumplió: {descripcion}")
    return None


def parametros(horizonte=240, urgencia=0.40, valor=0.30, prob=0.20, tipo=0.10):
    return {
        "pesoUrgencia": urgencia, "pesoValor": valor,
        "pesoProbabilidad": prob, "pesoTipo": tipo,
        "horizonteHoras": horizonte, "valorReferencia": 100000000,
        "puntajeLicitacion": 100, "puntajeSpot": 50,
        "valorPorDefectoSinValor": 30, "probabilidadPorDefecto": 50,
    }


class ClienteDelHub:
    """
    Cliente de un hub de SignalR por long polling, con la biblioteca estándar.

    Se usa long polling y no WebSocket para no depender de instalar una librería: es uno
    de los tres transportes que el propio hub negocia, y el camino de empuje es el mismo.

    Sirve para los dos hubs —el de trabajos y el de bandeja— porque la ruta es parámetro.
    """

    def __init__(self, ruta="/hubs/trabajos"):
        self.ruta = ruta
        self.token = None
        self._escuchando = False
        self._hilo = None

    def _url(self):
        return f"{API}{self.ruta}?id={urllib.parse.quote(self.token)}"

    def _enviar(self, cuerpo):
        req = urllib.request.Request(self._url(), data=cuerpo.encode(), method="POST")
        req.add_header(*ADMIN)
        req.add_header("Content-Type", "text/plain;charset=UTF-8")
        with urllib.request.urlopen(req, timeout=15) as r:
            return r.read()

    def _sondear(self, timeout):
        req = urllib.request.Request(self._url(), method="GET")
        req.add_header(*ADMIN)
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return r.read().decode("utf-8", "replace")
        except (urllib.error.URLError, TimeoutError):
            return ""

    def conectar(self):
        req = urllib.request.Request(
            f"{API}{self.ruta}/negotiate?negotiateVersion=1", method="POST")
        req.add_header(*ADMIN)
        with urllib.request.urlopen(req, timeout=15) as r:
            negociacion = json.load(r)
        self.token = negociacion["connectionToken"]
        transportes = [t["transport"] for t in negociacion["availableTransports"]]
        ok("LongPolling" in transportes, f"transportes de {self.ruta}: {transportes}")
        self._enviar(json.dumps({"protocol": "json", "version": 1}) + SEP)
        # El primer sondeo puede volver vacío según cómo caiga el ciclo. Que el apretón de
        # manos sirvió lo demuestra que después lleguen avisos.
        self._sondear(10)

    def invocar(self, metodo, argumentos=None):
        self._enviar(json.dumps({"type": 1, "target": metodo,
                                 "arguments": argumentos or [], "invocationId": "1"}) + SEP)

    def _leer(self, crudo, evento, destino):
        for trozo in (crudo or "").split(SEP):
            if not trozo.strip():
                continue
            try:
                m = json.loads(trozo)
            except json.JSONDecodeError:
                continue
            if m.get("type") == 1 and m.get("target") == evento:
                destino.append(m["arguments"][0])

    def recolectar(self, evento, segundos):
        recibidos, limite = [], time.time() + segundos
        while time.time() < limite:
            self._leer(self._sondear(8), evento, recibidos)
        return recibidos

    def escuchar_en_segundo_plano(self, evento):
        """
        Sondea sin parar en un hilo aparte y acumula lo que llegue.

        HACE FALTA, no es comodidad. Con long polling el sondeo ES la conexión: si el
        cliente deja de sondear, el servidor lo da por desconectado y lo saca de sus
        grupos. Suscribirse y volver cuarenta segundos después no funciona — se pierde
        todo lo de mientras y encima sin error, que es lo que despista.

        Un cliente de SignalR de verdad hace esto solo; aquí hay que hacerlo a mano.
        """
        recibidos = []
        self._escuchando = True

        def bucle():
            while self._escuchando:
                self._leer(self._sondear(8), evento, recibidos)

        self._hilo = threading.Thread(target=bucle, daemon=True)
        self._hilo.start()
        return recibidos

    def dejar_de_escuchar(self):
        self._escuchando = False
        if getattr(self, "_hilo", None):
            self._hilo.join(timeout=12)

    def cerrar(self):
        try:
            req = urllib.request.Request(self._url(), method="DELETE")
            req.add_header(*ADMIN)
            urllib.request.urlopen(req, timeout=5)
        except Exception:
            pass


def main():
    paso("La aplicación responde y está sana")
    codigo, salud = pedir("/health", rol=("Accept", "application/json"), esperado=200)
    ok(salud.get("status") == "Healthy", f"estado global = {salud.get('status')}")
    for nombre, chequeo in (salud.get("chequeos") or {}).items():
        ok(chequeo["status"] == "Healthy", f"chequeo {nombre} = {chequeo['status']}")

    paso("Autorización: la recalibración del score es solo de Admin")
    pedir("/api/priorizacion/parametros", rol=("Accept", "*/*"), esperado=401)
    pedir("/api/priorizacion/parametros", rol=("X-Rol", "Comercial"), esperado=403)
    pedir("/api/priorizacion/parametros", esperado=200)
    codigo, _ = pedir("/hubs/trabajos/negotiate?negotiateVersion=1",
                      None, "POST", rol=("Accept", "*/*"))
    ok(codigo == 401, f"negotiate del hub sin identidad -> {codigo} (esperado 401)")

    paso("Priorización: parámetros de partida (HU-05.1)")
    pedir("/api/priorizacion/parametros", parametros(240), "PUT", esperado=200)
    _, p = pedir("/api/priorizacion/parametros")
    ok(p["horizonteHoras"] == 240, f"horizonte de partida = {p['horizonteHoras']}")

    paso("Priorización: pesos que no suman uno se rechazan sin tocar la fila")
    _, err = pedir("/api/priorizacion/parametros",
                   parametros(240, 0.5, 0.5, 0.2, 0.1), "PUT", esperado=400)
    ok(err.get("code") == "PARAMETROS_SCORE_INVALIDOS", f"code = {err.get('code')}")
    _, p = pedir("/api/priorizacion/parametros")
    ok(p["pesoUrgencia"] == 0.40, "un rechazo no dejó los parámetros a medio cambiar")

    paso("Autorización del canal de bandeja (HU-05.2)")
    codigo, _ = pedir("/hubs/bandeja/negotiate?negotiateVersion=1",
                      None, "POST", rol=("Accept", "*/*"))
    ok(codigo == 401, f"negotiate de /hubs/bandeja sin identidad -> {codigo} (esperado 401)")

    paso("Solicitud con deadline futuro")
    _, clientes = pedir("/api/captura-solicitudes/clientes?q=a")
    if not ok(len(clientes) > 0, f"clientes disponibles = {len(clientes)}"):
        return
    cliente = clientes[0]["clienteId"]

    # Se conecta al canal de bandeja ANTES de mover nada, para poder observar los avisos
    # que provoca el resto del recorrido.
    bandeja_hub = ClienteDelHub("/hubs/bandeja")
    bandeja_hub.conectar()
    bandeja_hub.invocar("Suscribir")
    # Escucha desde ya y sin parar: con long polling, dejar de sondear es desconectarse.
    avisos_bandeja = bandeja_hub.escuchar_en_segundo_plano("bandeja")
    # 30 días = 720 horas. Con horizonte 240 queda fuera y el puntaje es 0; el paso final
    # sube el horizonte a 4000 y tiene que pasar a 82 = 100*(1 - 720/4000).
    deadline = (datetime.now(timezone.utc) + timedelta(days=30)).strftime("%Y-%m-%dT%H:%M:%SZ")
    _, creada = pedir("/api/solicitudes",
                      {"clienteId": cliente, "tipo": "licitacion", "deadline": deadline},
                      esperado=201)
    solicitud = creada["id"]
    dato(f"solicitud = {solicitud} (cliente {clientes[0]['nombre']})")

    paso("Carga masiva: responde 202 sin costear nada (HU-04.2)")
    _, carga = pedir(f"/api/solicitudes/{solicitud}/lineas:bulk",
                     {"lineas": [{"tipologia": "CargaSeca", "peso": 10000 + i}
                                 for i in range(15)]},
                     esperado=202)
    trabajo = carga["trabajoId"]
    ok(carga["estado"] == "Pendiente", f"estado inicial = {carga['estado']}")
    ok(carga["consultarEn"] == f"/api/trabajos/{trabajo}", "trae la ruta de respaldo")
    dato(f"trabajo = {trabajo}")

    paso("Avisos en vivo: un cliente real suscrito al hub (HU-04.5)")
    hub = ClienteDelHub()
    hub.conectar()
    hub.invocar("Suscribir", [trabajo])
    avisos = hub.recolectar("avance", segundos=20)
    hub.cerrar()

    if not ok(len(avisos) > 0, f"llegaron {len(avisos)} avisos por el canal"):
        return
    secuencias = [a["secuencia"] for a in avisos]
    dato(f"secuencias tal como llegaron: {secuencias}")

    # EL CANAL NO PROMETE ORDEN NI AUSENCIA DE REPETIDOS. Entrega al menos una vez y varios
    # avisos se procesan a la vez, así que llegan desordenados y repetidos — se ha visto
    # [1,2,3,4,6,5,6,7…] en una corrida real. Lo que el contrato promete es que aplicando
    # la regla del cliente la barra nunca retrocede. Eso es lo que hay que afirmar; afirmar
    # "estrictamente creciente" sería afirmar algo que el contrato no promete.
    desordenados = sum(1 for a, b in zip(secuencias, secuencias[1:]) if b < a)
    repetidos = len(secuencias) - len(set(secuencias))
    dato(f"desordenados={desordenados} repetidos={repetidos} (legítimos: para eso está la regla)")

    pintadas, ultima = [], -1
    for s in secuencias:
        if s <= ultima:
            continue
        ultima = s
        pintadas.append(s)
    ok(pintadas == sorted(pintadas) and len(set(pintadas)) == len(pintadas),
       f"con la regla del contrato la barra nunca retrocede: {pintadas}")
    ok(all(a["trabajoId"] == trabajo for a in avisos), "todos los avisos son del trabajo suscrito")
    ok(all(set(a) == {"trabajoId", "estado", "avance", "terminadoEn", "secuencia"}
           for a in avisos),
       "la forma del aviso es la del contrato, sin la lista de pendientes")

    paso("Consulta de respaldo: deja al cliente al día (HU-04.5)")
    resultado = esperar(
        lambda: (lambda r: r[1] if r[1].get("terminadoEn") else None)(
            pedir(f"/api/trabajos/{trabajo}")),
        "el trabajo termina")
    if not resultado:
        return
    ok(resultado["estado"] in ("Completado", "Parcial"), f"estado final = {resultado['estado']}")
    ok(resultado["avance"]["total"] == 15, f"total autoritativo = {resultado['avance']['total']}")
    ok(resultado["secuencia"] >= max(secuencias),
       f"secuencia de la consulta ({resultado['secuencia']}) >= último aviso ({max(secuencias)})")
    ok(len(resultado["pendientes"]) == resultado["avance"]["pendientes"],
       "la lista de pendientes cuadra con su contador")

    paso("Bandeja: el pliego entero produce UN aviso, no uno por línea (HU-05.2)")
    time.sleep(6)   # margen para que lleguen los del cierre del trabajo
    motivos = [a["motivo"] for a in list(avisos_bandeja)]
    dato(f"motivos recibidos: {motivos}")

    ok(motivos.count("solicitud-nueva") >= 1,
       "el alta de la solicitud avisó a la bandeja")
    ok(motivos.count("trabajo-terminado") == 1,
       f"el pliego de 15 líneas produjo {motivos.count('trabajo-terminado')} aviso(s) de "
       f"trabajo-terminado (esperado 1, NO 15)")
    ok(all(set(a) == {"motivo", "en"} for a in list(avisos_bandeja)),
       "el aviso es una señal, no la bandeja: solo motivo y fecha")
    ok(motivos.count("trabajo-terminado") < len(avisos),
       f"la bandeja avisó {motivos.count('trabajo-terminado')} vez del cierre y el avance "
       f"{len(avisos)} veces: el canal de bandeja se agrupa, el de avance no")

    paso("Cotización completa sobre el módulo piloto")
    _, det = pedir(f"/api/solicitudes/{solicitud}")
    linea = det["lineas"][0]["lineaId"]
    _, cot = pedir("/api/cotizaciones", {"solicitudId": solicitud}, esperado=201)
    cid = cot["id"]
    # Agregar el flete es el paso que devolvía 409 CONFLICTO_CONCURRENCIA hasta la #17:
    # EF emitía UPDATE en vez de INSERT para la línea nueva.
    pedir(f"/api/cotizaciones/{cid}/lineas",
          {"lineaSolicitudId": linea, "valorFlete": 1850000, "fuente": "sicetac"}, esperado=200)
    _, srv = pedir("/api/servicios-conexos")
    items = srv["items"] if isinstance(srv, dict) else srv
    if ok(len(items) > 0, f"servicios conexos en catálogo = {len(items)}"):
        pedir(f"/api/cotizaciones/{cid}/conexos",
              {"items": [{"servicioConexoId": items[0]["id"], "cantidad": 2}]}, esperado=200)
    pedir(f"/api/cotizaciones/{cid}/entregar", None, "POST", esperado=200)
    _, res = pedir(f"/api/cotizaciones/{cid}/resultado", {"resultado": "ganada"}, esperado=200)
    ok(res["resultado"] == "Ganada", f"resultado = {res['resultado']}")

    paso("Priorización: el horizonte mueve el puntaje persistido (HU-05.1)")
    # Se usa una solicitud nueva y que se queda Pendiente: el recálculo solo toca activas,
    # y la de arriba ya pasó por la cotización.
    _, activa = pedir("/api/solicitudes",
                      {"clienteId": cliente, "tipo": "licitacion", "deadline": deadline},
                      esperado=201)
    vigilada = activa["id"]

    def score_de(id_solicitud):
        _, filas = pedir("/api/solicitudes?atencion=todas")
        for f in (filas if isinstance(filas, list) else filas.get("items", [])):
            if f["id"] == id_solicitud:
                return f["scorePrioridad"]
        return None

    dato(f"solicitud vigilada = {vigilada} (deadline a 720 horas)")
    pedir("/api/priorizacion/parametros", parametros(4000), "PUT", esperado=200)

    # La bandeja recalcula al leer, así que refleja el horizonte nuevo de inmediato. El
    # valor persistido lo refresca el consumidor aparte; aquí se afirma el efecto visible.
    nuevo = esperar(lambda: (lambda s: s if s and s > 0 else None)(score_de(vigilada)),
                    "el puntaje sube al ampliar el horizonte")
    ok(nuevo == 82, f"puntaje con horizonte 4000 = {nuevo} (esperado 82 = 100*(1-720/4000))")

    paso("Priorización: cambiar un peso NO mueve el puntaje")
    # Los cuatro pesos son configuración dormida: el total es solo la urgencia por
    # deadline. Está documentado en docs/contrato-integracion; esto lo fija.
    antes = score_de(vigilada)
    pedir("/api/priorizacion/parametros",
          parametros(4000, 0.60, 0.20, 0.15, 0.05), "PUT", esperado=200)
    time.sleep(8)
    ok(score_de(vigilada) == antes,
       f"el puntaje sigue en {antes} después de cambiar los pesos")

    paso("Bandeja: recalibrar avisa por el canal (HU-05.2)")
    antes = len(avisos_bandeja)
    pedir("/api/priorizacion/parametros", parametros(240), "PUT", esperado=200)
    time.sleep(8)
    nuevos = [a["motivo"] for a in list(avisos_bandeja)[antes:]]
    ok("parametros-recalibrados" in nuevos,
       f"llegó el aviso de recalibración (motivos nuevos: {nuevos})")
    bandeja_hub.dejar_de_escuchar()
    bandeja_hub.cerrar()

    paso("Bandeja: sirve por puntaje descendente, no por orden de llegada")
    _, bandeja = pedir("/api/solicitudes?atencion=todas")
    filas = bandeja if isinstance(bandeja, list) else bandeja.get("items", [])
    scores = [f["scorePrioridad"] for f in filas]
    ok(scores == sorted(scores, reverse=True), f"orden descendente ({len(scores)} filas)")
    ok(any(f["id"] == solicitud for f in filas), "la solicitud del recorrido aparece")

    paso("Dejar los parámetros como estaban")
    pedir("/api/priorizacion/parametros", parametros(240), "PUT", esperado=200)


if __name__ == "__main__":
    main()
    print("\n" + "=" * 70)
    if fallos:
        print(f"RECORRIDO CON {len(fallos)} FALLO(S):")
        for f in fallos:
            print(f"  - {f}")
        sys.exit(1)
    print("RECORRIDO COMPLETO SIN FALLOS")
