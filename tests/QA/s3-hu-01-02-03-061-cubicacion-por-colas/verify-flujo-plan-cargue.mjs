// QA — S3-HU-01/02/03 (PRs #122/#124): flujo real completo del plan de cargue por
// colas contra el stack real (SQL Server + RabbitMQ + Cubicador).
// crear vehiculo -> crear plan -> agregar item -> optimizar -> leer /resultado ->
// ajustar una colocacion.
// Uso: node verify-flujo-plan-cargue.mjs
const BASE = process.env.NEXO_BASE_URL ?? "http://localhost:8080";
const H = { "X-Portal-Usuario-Id": process.env.NEXO_USUARIO_ID ?? "1005", "Content-Type": "application/json" };

async function post(path, body) {
  const r = await fetch(`${BASE}${path}`, { method: "POST", headers: H, body: body ? JSON.stringify(body) : undefined });
  return { status: r.status, body: await r.json().catch(() => null) };
}
async function get(path) {
  const r = await fetch(`${BASE}${path}`, { headers: H });
  return { status: r.status, body: await r.json().catch(() => null) };
}
async function patch(path, body) {
  const r = await fetch(`${BASE}${path}`, { method: "PATCH", headers: H, body: JSON.stringify(body) });
  return { status: r.status, body: await r.json().catch(() => null) };
}
const espera = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  let ok = true;

  console.log("1) crear vehiculo...");
  const veh = await post("/api/vehiculos", {
    nombre: "QA-Vehiculo-flujo", descripcion: "verify-flujo-plan-cargue",
    largoInternoMm: 9000, anchoInternoMm: 2300, altoInternoMm: 2400,
    capacidadKg: 12000, taraKg: 6000, puertaAnchoMm: 2200, puertaAltoMm: 2300, puertaPosicion: "trasera",
    ejes: [{ posicionMm: 1200, capacidadKg: 6000 }, { posicionMm: 7000, capacidadKg: 6000 }],
  });
  console.log(`   -> ${veh.status}`, veh.body);
  if (veh.status !== 202) { console.error("FALLO: esperaba 202 al crear vehiculo."); ok = false; }
  const vehiculoId = veh.body?.correlationId;
  await espera(3000);

  console.log("2) crear plan...");
  const plan = await post("/api/planes-cargue", { nombre: "QA-Plan-flujo", vehiculoId });
  console.log(`   -> ${plan.status}`, plan.body);
  if (plan.status !== 202) { console.error("FALLO: esperaba 202 al crear plan."); ok = false; }
  const planId = plan.body?.planCargueId;

  console.log("3) agregar item (con SKU)...");
  const item = await post(`/api/planes-cargue/${planId}/items`, {
    sku: "QA-SKU-FLUJO", descripcion: "Caja QA", secuenciaEntrega: 1,
    largoMm: 1000, anchoMm: 800, altoMm: 800, pesoKg: 300, cantidad: 2,
    prioridad: 1, apilable: true, maxNiveles: 2, maxPesoEncimaKg: 500,
    rotable: true, mantenerVertical: false, fragil: false, peligroso: false, refrigerado: false,
  });
  console.log(`   -> ${item.status}`, item.body);
  await espera(2000);

  console.log("4) optimizar...");
  const opt = await post(`/api/planes-cargue/${planId}/optimizar`);
  console.log(`   -> ${opt.status}`, opt.body);
  await espera(3000);

  console.log("5) leer /resultado (proyeccion enriquecida)...");
  const resultado = await get(`/api/planes-cargue/${planId}/resultado`);
  console.log(`   -> ${resultado.status} estado=${resultado.body?.plan?.estado} colocaciones=${resultado.body?.colocaciones?.length}`);
  if (resultado.body?.plan?.estado !== "optimizado") { console.error("FALLO: esperaba estado=optimizado."); ok = false; }
  if (!resultado.body?.colocaciones?.length) { console.error("FALLO: esperaba al menos 1 colocacion."); ok = false; }

  console.log("6) ajustar una colocacion (bloquear)...");
  const colocacionId = resultado.body?.colocaciones?.[0]?.id;
  const ajuste = await patch(`/api/planes-cargue/${planId}/colocaciones/${colocacionId}`, { bloqueada: true });
  console.log(`   -> ${ajuste.status}`, ajuste.body);
  if (ajuste.status !== 202) { console.error("FALLO: esperaba 202 al ajustar colocacion."); ok = false; }

  console.log(ok ? "\nTODO OK." : "\nHAY FALLAS.");
  process.exit(ok ? 0 : 1);
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
