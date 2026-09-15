// QA — fix traido desde Pre-Despliegue-BACKEND (commit 774de8c, Bran/bquintero,
// 14-sep-2026): "el reporte de un fallo ya no puede atascar el worker".
//
// Causa real: un `guardar_item` contra un plan inexistente revienta con
// ForeignKeyViolationError durante el flush - eso deja la sesion de SQLAlchemy
// "envenenada" (PendingRollbackError en cualquier uso posterior). El manejador
// de errores llamaba a marcar_error() sobre esa misma sesion, explotaba ANTES
// del ack(), y el mensaje quedaba unacknowledged para siempre. Con
// prefetch_count=2, 2 mensajes asi atascaban el worker completo - confirmado en
// el propio ambiente de pre-despliegue el 14-sep (mismo sintoma que el bug #131
// que cerramos en S3-HU-10.1, causa distinta).
//
// Publica 2 mensajes "guardar_item" contra un planId que no existe (via la API
// de gestion de RabbitMQ, sin libreria de AMQP), y confirma que el worker NO
// queda atascado - ambos deben terminar en la DLQ, no unacknowledged.
//
// Uso: node verify-fix-sesion-envenenada.mjs
const RABBIT_MGMT = process.env.RABBIT_MGMT_URL ?? "http://localhost:15672";
const RABBIT_USER = process.env.RABBITMQ_USER ?? "ci_user";
const RABBIT_PASSWORD = process.env.RABBITMQ_PASSWORD ?? "ci_password";
const COLA = "cubicador.optimizar";
const AUTH = "Basic " + Buffer.from(`${RABBIT_USER}:${RABBIT_PASSWORD}`).toString("base64");

const espera = (ms) => new Promise((r) => setTimeout(r, ms));

async function publicarItemContraPlanInexistente(planIdFalso, i) {
  const payload = {
    tipo: "guardar_item",
    planId: planIdFalso,
    correlationId: `qa-envenenada-${i}-${Date.now()}`,
    solicitud: {
      sku: "QA-SKU", descripcion: "Item contra plan inexistente", secuenciaEntrega: 1,
      largoMm: 100, anchoMm: 100, altoMm: 100, pesoKg: 10, cantidad: 1,
      prioridad: 1, apilable: true, maxNiveles: 1, maxPesoEncimaKg: 100,
      rotable: true, mantenerVertical: false, fragil: false, peligroso: false, refrigerado: false,
    },
  };
  const r = await fetch(`${RABBIT_MGMT}/api/exchanges/%2f/amq.default/publish`, {
    method: "POST",
    headers: { Authorization: AUTH, "Content-Type": "application/json" },
    body: JSON.stringify({ properties: {}, routing_key: COLA, payload: JSON.stringify(payload), payload_encoding: "string" }),
  });
  console.log(`publicado ${i}:`, await r.json());
}

async function estadoCola() {
  const r = await fetch(`${RABBIT_MGMT}/api/queues/%2f/${COLA}`, { headers: { Authorization: AUTH } });
  const q = await r.json();
  return { ready: q.messages_ready, unacked: q.messages_unacknowledged, consumers: q.consumers };
}

async function main() {
  console.log("estado inicial:", await estadoCola());

  console.log("\npublicando 2 mensajes guardar_item contra planes inexistentes...");
  await publicarItemContraPlanInexistente("00000000-0000-0000-0000-000000000001", 1);
  await publicarItemContraPlanInexistente("00000000-0000-0000-0000-000000000002", 2);

  console.log("\nesperando a que el worker procese y agote los 3 reintentos de cada uno...");
  await espera(10000);

  const estado = await estadoCola();
  console.log("estado tras los 2 mensajes:", estado);

  if (estado.unacked > 0) {
    console.error("\nFALLO: quedaron mensajes 'unacknowledged' - el worker sigue pudiendo atascarse.");
    process.exit(1);
  }
  console.log("\nOK: 0 mensajes unacknowledged - el reporte del fallo (sesion envenenada) ya no impide el ack().");
  console.log("Confirmar tambien que ambos llegaron a cubicacion.dlq (2 mensajes) y que el worker sigue vivo");
  console.log("creando un plan nuevo despues de esto.");
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
