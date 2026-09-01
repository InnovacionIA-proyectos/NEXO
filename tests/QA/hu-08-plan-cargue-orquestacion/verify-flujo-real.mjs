// Verificación QA en vivo de la HU-08.1/08.2 (issues #29/#30, PR #79): flujo real
// completo -- POST /api/planes-cargue/{id}/optimizar (202 inmediato) -> aviso real por
// SignalR (/hubs/plan-cargue, evento "avance") -> consulta de respaldo coincidente.
// Requiere:
//   - La compuerta de paridad ABIERTA (Cubicacion__ParidadVerificada=true + Evidencia +
//     Fecha en .env) -- cerrada por defecto, ver docs/PENDIENTES.md.
//   - NEXO (db+rabbit+api) y Cubicador (db+api+worker, unido a nexo_internal) arriba.
//   - Un plan que YA EXISTE en Cubicador (con su ítem) Y su espejo en NEXO (misma Id,
//     estado "borrador") -- hoy no hay endpoint para crear ese espejo por REST, se
//     inserta por SQL directo (ver README de esta carpeta para el paso a paso).
// Uso: node verify-flujo-real.mjs <planId>
const BASE = "http://localhost:8080";
const WS_BASE = "ws://localhost:8080";
const RS = "\x1e";
const PLAN_ID = process.argv[2];
const H = { "X-Portal-Usuario-Id": "1005" }; // Admin

async function conectarHub() {
  const negotiate = await fetch(`${BASE}/hubs/plan-cargue/negotiate?negotiateVersion=1`, { method: "POST", headers: H });
  if (negotiate.status !== 200) throw new Error(`negotiate: ${negotiate.status}`);
  const { connectionToken } = await negotiate.json();
  const ws = new WebSocket(`${WS_BASE}/hubs/plan-cargue?id=${connectionToken}`, { headers: H });

  const avisos = [];
  const listo = new Promise((resolve, reject) => {
    ws.addEventListener("open", () => ws.send(JSON.stringify({ protocol: "json", version: 1 }) + RS));
    ws.addEventListener("message", (ev) => {
      const texto = typeof ev.data === "string" ? ev.data : ev.data.toString();
      for (const trozo of texto.split(RS).filter(Boolean)) {
        const msg = JSON.parse(trozo);
        if (Object.keys(msg).length === 0) resolve();
        else if (msg.type === 1 && msg.target === "avance") avisos.push(msg.arguments[0]);
      }
    });
    ws.addEventListener("error", reject);
  });
  await listo;
  return { ws, avisos };
}

async function main() {
  console.log(`Conectando a /hubs/plan-cargue y suscribiendo al plan ${PLAN_ID}...`);
  const { ws, avisos } = await conectarHub();
  ws.send(JSON.stringify({ type: 1, target: "Suscribir", arguments: [PLAN_ID] }) + RS);
  await new Promise((r) => setTimeout(r, 500));

  console.log("Pidiendo la optimización real (POST /api/planes-cargue/{id}/optimizar)...");
  const t0 = Date.now();
  const resp = await fetch(`${BASE}/api/planes-cargue/${PLAN_ID}/optimizar`, { method: "POST", headers: H });
  const tResp = Date.now() - t0;
  const body = await resp.json();
  console.log(`-> ${resp.status} en ${tResp} ms:`, body);

  if (resp.status !== 202) { console.error("FALLÓ: esperaba 202."); process.exit(1); }

  console.log("Esperando el aviso 'avance' por SignalR...");
  for (let i = 0; i < 20 && avisos.length === 0; i++) await new Promise((r) => setTimeout(r, 1000));
  ws.close();

  console.log(`Avisos recibidos por SignalR: ${JSON.stringify(avisos)}`);

  const consulta = await (await fetch(`${BASE}/api/planes-cargue/${PLAN_ID}`, { headers: H })).json();
  console.log(`Consulta de respaldo: estado=${consulta.estado} puntaje=${consulta.puntaje}`);

  let ok = true;
  if (avisos.length === 0) { console.error("FALLÓ: no llegó ningún aviso por SignalR."); ok = false; }
  else if (avisos[0].estado !== "optimizado" && avisos[0].estado !== consulta.estado) {
    console.error("FALLÓ: el aviso no coincide con la consulta de respaldo.");
    ok = false;
  }
  if (consulta.estado !== "optimizado") { console.error(`FALLÓ: estado final = ${consulta.estado}, esperaba optimizado.`); ok = false; }

  console.log(ok ? "\nTODO OK." : "\nHAY FALLAS.");
  process.exit(ok ? 0 : 1);
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
