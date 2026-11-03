// QA — Bug #3 (issue #133): borrar un plan ya optimizado siempre falla.
// plan_cargue_repo.py::eliminar() no borraba antes las colocaciones (a diferencia
// de eliminar_item()), asi que revienta con ForeignKeyViolationError para
// cualquier plan con colocaciones. Como NEXO borra de forma optimista (responde
// antes de saber si Cubicador lo logro), el resultado observable es: NEXO dice que
// el plan ya no existe, Cubicador lo conserva intacto.
// Uso: node verify-bug-eliminar-plan-optimizado.mjs <vehiculoId-valido-en-cubicador>
const BASE = process.env.NEXO_BASE_URL ?? "http://localhost:8080";
const H = { "X-Portal-Usuario-Id": process.env.NEXO_USUARIO_ID ?? "1005", "Content-Type": "application/json" };
const vehiculoId = process.argv[2];
if (!vehiculoId) { console.error("Uso: node verify-bug-eliminar-plan-optimizado.mjs <vehiculoId>"); process.exit(2); }

const espera = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const plan = await (await fetch(`${BASE}/api/planes-cargue`, {
    method: "POST", headers: H, body: JSON.stringify({ nombre: "QA-Bug-Eliminar-Optimizado", vehiculoId }),
  })).json();
  const planId = plan.planCargueId;
  console.log("plan creado:", plan);

  await fetch(`${BASE}/api/planes-cargue/${planId}/items`, {
    method: "POST", headers: H, body: JSON.stringify({
      sku: "QA-SKU-1", descripcion: "Caja", secuenciaEntrega: 1,
      largoMm: 1000, anchoMm: 800, altoMm: 800, pesoKg: 300, cantidad: 2,
      prioridad: 1, apilable: true, maxNiveles: 2, maxPesoEncimaKg: 500,
      rotable: true, mantenerVertical: false, fragil: false, peligroso: false, refrigerado: false,
    }),
  });
  await espera(2000);

  console.log("optimizando (para que el plan tenga colocaciones reales)...");
  await fetch(`${BASE}/api/planes-cargue/${planId}/optimizar`, { method: "POST", headers: H });
  await espera(3000);

  const antes = await (await fetch(`${BASE}/api/planes-cargue/${planId}/resultado`, { headers: H })).json();
  console.log(`colocaciones antes de borrar: ${antes.colocaciones?.length}`);

  console.log("borrando el plan optimizado...");
  const del = await fetch(`${BASE}/api/planes-cargue/${planId}`, { method: "DELETE", headers: H });
  console.log(`DELETE -> HTTP ${del.status}`);

  await espera(3000);
  const despues = await fetch(`${BASE}/api/planes-cargue/${planId}`, { headers: H });
  console.log(`GET tras el borrado -> HTTP ${despues.status} (NEXO)`);

  console.log([
    "",
    "Con el bug presente (antes del fix del PR #141):",
    "  - NEXO ya respondio 404/borro su fila (borrado optimista) inmediatamente.",
    "  - En Cubicador, el DELETE revienta con ForeignKeyViolationError y el plan",
    "    (con sus colocaciones) sigue existiendo intacto -> ver directamente en",
    "    Postgres: SELECT COUNT(*) FROM colocaciones_cargue WHERE plan_cargue_id=...",
    "  - Es decir: NEXO cree que ya no existe, Cubicador lo conserva - inconsistencia",
    "    permanente sin reconciliacion.",
    "Con el bug corregido: el DELETE en Cubicador tambien succeeds (204) y no quedan",
    "filas huerfanas en colocaciones_cargue ni items_plan_cargue.",
  ].join("\n"));
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
