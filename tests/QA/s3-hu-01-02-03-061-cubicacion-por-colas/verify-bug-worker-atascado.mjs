// QA — Bug #1 CRITICO (issue #131): un mensaje sin "planId" deja el worker de
// Cubicador atascado para siempre. worker/main.py usaba cuerpo["planId"] (indexacion
// directa) en el manejador de errores; si el mensaje no trae esa clave, esa linea
// lanza KeyError ANTES del ack(), y el mensaje queda unacknowledged para siempre.
// Con prefetch_count=2 (DECISIONES_TECNICAS.md #10), 2 mensajes asi bastan para que
// el worker deje de procesar CUALQUIER mensaje nuevo.
//
// Publica 2 mensajes crudos sin "planId" directo a la cola vía la API HTTP de
// gestion de RabbitMQ (no requiere ninguna libreria de AMQP), y confirma que el
// worker queda con mensajes "unacknowledged" para siempre.
//
// Uso: node verify-bug-worker-atascado.mjs
const RABBIT_MGMT = process.env.RABBIT_MGMT_URL ?? "http://localhost:15672";
const RABBIT_USER = process.env.RABBITMQ_USER ?? "ci_user";
const RABBIT_PASS = process.env.RABBITMQ_PASSWORD ?? "ci_password";
const COLA = "cubicador.optimizar";
const AUTH = "Basic " + Buffer.from(`${RABBIT_USER}:${RABBIT_PASS}`).toString("base64");

const espera = (ms) => new Promise((r) => setTimeout(r, ms));

async function publicarMensajeSinPlanId(i) {
  // Mensaje "plano" (sin el sobre de MassTransit) con un tipo desconocido y SIN la
  // clave "planId" - exactamente lo que hace cubicador/scripts/ci_docker_smoke.py
  // para probar la ruta de reintentos/DLQ, y lo que revela el bug.
  const payload = { tipo: `tipo_invalido_qa_${i}`, correlationId: `qa-${Date.now()}-${i}`, solicitud: {} };
  const r = await fetch(`${RABBIT_MGMT}/api/exchanges/%2f/amq.default/publish`, {
    method: "POST",
    headers: { Authorization: AUTH, "Content-Type": "application/json" },
    body: JSON.stringify({
      properties: {}, routing_key: COLA, payload: JSON.stringify(payload), payload_encoding: "string",
    }),
  });
  const body = await r.json();
  console.log(`publicado mensaje ${i}:`, body);
}

async function estadoCola() {
  const r = await fetch(`${RABBIT_MGMT}/api/queues/%2f/${COLA}`, { headers: { Authorization: AUTH } });
  const q = await r.json();
  return { messages: q.messages, ready: q.messages_ready, unacked: q.messages_unacknowledged, consumers: q.consumers };
}

async function main() {
  console.log("estado inicial de la cola:", await estadoCola());

  console.log("\npublicando 2 mensajes sin 'planId' (el minimo para agotar prefetch_count=2)...");
  await publicarMensajeSinPlanId(1);
  await publicarMensajeSinPlanId(2);

  console.log("\nesperando a que el worker los procese y agote sus 3 reintentos cada uno...");
  await espera(8000);

  const estado = await estadoCola();
  console.log("estado de la cola tras los 2 mensajes:", estado);

  if (estado.unacked >= 2) {
    console.log([
      "",
      "BUG PRESENTE: los 2 mensajes quedaron 'unacknowledged' para siempre.",
      "Con prefetch_count=2, esto satura la capacidad del canal - ningun mensaje",
      "nuevo (de ningun tipo) se entregara al worker desde este punto en adelante,",
      "y ni reiniciar el contenedor del worker lo arregla (RabbitMQ los reentrega,",
      "vuelven a fallar de la misma forma). Confirmarlo publicando un mensaje valido",
      "(p.ej. correr scripts/ci_docker_smoke.py) y viendo que nunca se procesa.",
    ].join("\n"));
    process.exitCode = 1;
  } else {
    console.log("\nOK: la cola no quedo atascada (bug corregido) - unacked =", estado.unacked);
  }
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
