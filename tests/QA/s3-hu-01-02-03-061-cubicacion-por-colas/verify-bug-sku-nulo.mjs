// QA — Bug #2 (issue #132): un item sin SKU marca TODO el plan en "error".
// GuardarItemRequest.Sku es string? en NEXO (manda null explicito); el DTO de
// Cubicador (GuardarItemPlanCargueInput.sku: str = "") rechaza ese null porque el
// default solo aplica si la clave FALTA, no si llega en null.
// Uso: node verify-bug-sku-nulo.mjs <vehiculoId-valido-en-cubicador>
const BASE = process.env.NEXO_BASE_URL ?? "http://localhost:8080";
const H = { "X-Portal-Usuario-Id": process.env.NEXO_USUARIO_ID ?? "1005", "Content-Type": "application/json" };
const vehiculoId = process.argv[2];
if (!vehiculoId) { console.error("Uso: node verify-bug-sku-nulo.mjs <vehiculoId>"); process.exit(2); }

const espera = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const plan = await (await fetch(`${BASE}/api/planes-cargue`, {
    method: "POST", headers: H, body: JSON.stringify({ nombre: "QA-Bug-SKU-nulo", vehiculoId }),
  })).json();
  const planId = plan.planCargueId;
  console.log("plan creado:", plan);

  console.log("agregando item SIN sku (null explicito)...");
  const item = await (await fetch(`${BASE}/api/planes-cargue/${planId}/items`, {
    method: "POST", headers: H, body: JSON.stringify({
      sku: null, descripcion: "Caja sin SKU", secuenciaEntrega: 1,
      largoMm: 1000, anchoMm: 800, altoMm: 800, pesoKg: 300, cantidad: 1,
      prioridad: 1, apilable: true, maxNiveles: 2, maxPesoEncimaKg: 500,
      rotable: true, mantenerVertical: false, fragil: false, peligroso: false, refrigerado: false,
    }),
  })).json();
  console.log("respuesta:", item);

  await espera(4000);
  const consulta = await (await fetch(`${BASE}/api/planes-cargue/${planId}`, { headers: H })).json();
  console.log("estado del plan tras el item:", consulta.estado);

  // Con el bug presente (antes del fix del PR #141): estado = "error".
  // Con el bug corregido: estado se queda en "borrador" (el item se guarda con sku="").
  console.log(consulta.estado === "error"
    ? "\nBUG PRESENTE: el plan quedo en error por un item sin SKU (caso de negocio valido)."
    : "\nOK: el item sin SKU no rompio el plan (bug corregido).");
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
