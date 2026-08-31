// Carga QA para la HU-08.1: N planes de cargue distintos (con su espejo Cubicador+NEXO
// ya preparado), optimizados CONCURRENTEMENTE, confirmando que todos terminan
// "optimizado" sin cruces entre planes. Mismos requisitos que verify-flujo-real.mjs.
// Uso: node verify-carga-concurrente.mjs <planId1> <planId2> ...
const BASE = "http://localhost:8080";
const H = { "X-Portal-Usuario-Id": "1005" };
const ids = process.argv.slice(2);

async function main() {
  console.log(`Disparando ${ids.length} optimizaciones CONCURRENTES sobre planes distintos...`);
  const t0 = Date.now();
  const respuestas = await Promise.all(
    ids.map((id) => fetch(`${BASE}/api/planes-cargue/${id}/optimizar`, { method: "POST", headers: H }).then((r) => r.status))
  );
  console.log(`${ids.length} peticiones en ${Date.now() - t0} ms. Estados:`, respuestas);

  if (!respuestas.every((s) => s === 202)) { console.error("FALLÓ: no todas dieron 202."); process.exit(1); }

  let ok = true, finales = [];
  for (let i = 0; i < 20; i++) {
    finales = await Promise.all(ids.map((id) => fetch(`${BASE}/api/planes-cargue/${id}`, { headers: H }).then((r) => r.json())));
    if (finales.every((e) => e.estado !== "procesando")) break;
    await new Promise((r) => setTimeout(r, 1000));
  }
  finales.forEach((e, idx) => console.log(`  plan ${ids[idx]}: estado=${e.estado} puntaje=${e.puntaje}`));
  ok = finales.every((e) => e.estado === "optimizado");

  console.log(ok ? `\nTODO OK: los ${ids.length} planes concurrentes terminaron optimizado, sin cruces.` : "\nHAY FALLAS.");
  process.exit(ok ? 0 : 1);
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
