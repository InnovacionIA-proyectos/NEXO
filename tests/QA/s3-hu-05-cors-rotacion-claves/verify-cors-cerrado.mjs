// QA — S3-HU-05 (PR #125): confirma que Cubicador ya no responde encabezados CORS
// bajo ningun origen (CUBICADOR_CORS_ORIGENES fue retirada por completo), y que la
// autenticacion sigue fail-closed tras la rotacion de CUBICADOR_API_KEY.
// Uso: node verify-cors-cerrado.mjs
const BASE = process.env.CUBICADOR_BASE_URL ?? "http://localhost:8081";
const API_KEY = process.env.CUBICADOR_API_KEY ?? "ci-api-key";

async function main() {
  let ok = true;

  console.log("1) request con Origin -> no debe traer ningun Access-Control-*...");
  const conOrigin = await fetch(`${BASE}/api/planes-cargue/vehiculos`, {
    headers: { Authorization: `Bearer ${API_KEY}`, Origin: "http://localhost:5173" },
  });
  const headersCors = [...conOrigin.headers.keys()].filter((h) => h.toLowerCase().startsWith("access-control"));
  console.log(`   HTTP ${conOrigin.status}, encabezados CORS presentes: [${headersCors.join(", ")}]`);
  if (headersCors.length > 0) { console.error("FALLO: no deberia traer ningun encabezado Access-Control-*."); ok = false; }

  console.log("2) preflight OPTIONS -> no debe responder como CORS valido...");
  const preflight = await fetch(`${BASE}/api/planes-cargue/vehiculos`, {
    method: "OPTIONS",
    headers: { Origin: "http://localhost:5173", "Access-Control-Request-Method": "GET" },
  });
  console.log(`   HTTP ${preflight.status}`);
  if (preflight.status === 200 || preflight.status === 204) {
    console.error("FALLO: un preflight exitoso indicaria que CORS sigue habilitado.");
    ok = false;
  }

  console.log("3) autenticacion sigue fail-closed...");
  const sinAuth = await fetch(`${BASE}/api/planes-cargue/vehiculos`);
  const authInvalida = await fetch(`${BASE}/api/planes-cargue/vehiculos`, { headers: { Authorization: "Bearer clave-vieja-invalida" } });
  console.log(`   sin Authorization -> HTTP ${sinAuth.status}; con Bearer invalido -> HTTP ${authInvalida.status}`);
  if (sinAuth.status !== 401 || authInvalida.status !== 401) {
    console.error("FALLO: ambos casos deberian responder 401.");
    ok = false;
  }

  console.log(ok ? "\nTODO OK." : "\nHAY FALLAS.");
  process.exit(ok ? 0 : 1);
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
