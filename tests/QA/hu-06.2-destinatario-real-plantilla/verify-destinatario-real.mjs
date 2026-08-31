// Verificación QA en vivo de la HU-06.2 (issue #22, PR #78): que la notificación de
// entrega llegue al CREADOR real de la solicitud (resuelto de la sesión, S2-HU-03), no a
// un "comercial responsable" sacado de un DW por NIT del cliente (mecanismo retirado).
//
// Requiere la app levantada con el fallback SMTP apuntando a un capturador desechable
// (nunca el corporativo) y sin ConnectionStrings:IdentidadPortal (usa el resolver local):
//   docker run -d --name smtp4dev-qa --network nexo_internal -p 5000:80 -p 2525:25 rnwood/smtp4dev
//   # .env: Smtp__Host=host.docker.internal, Smtp__Puerto=2525, Smtp__Ssl=false
//   docker compose up -d --build --wait
//   node tests/QA/hu-06.2-destinatario-real-plantilla/verify-destinatario-real.mjs
const BASE = "http://localhost:8080";
const ID_PORTAL_CREADOR = 1001; // Carlos Herrera, CoordinadorFletes
const EMAIL_CREADOR_ESPERADO = "cherrera@boterosoto.com.co";
const H_CREADOR = { "X-Portal-Usuario-Id": String(ID_PORTAL_CREADOR) };

async function main() {
  const clienteResp = await fetch(`${BASE}/api/captura-solicitudes/clientes?q=a`, { headers: { "X-Portal-Usuario-Id": "1005" } });
  const clientes = await clienteResp.json();
  const clienteId = clientes[0].clienteId;
  console.log("cliente:", clientes[0].nombre);

  const crear = await fetch(`${BASE}/api/solicitudes`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...H_CREADOR },
    body: JSON.stringify({ clienteId, tipo: "spot" }),
  });
  const { id: solicitudId } = await crear.json();
  console.log("solicitud creada por Carlos Herrera (1001):", solicitudId);

  await fetch(`${BASE}/api/solicitudes/${solicitudId}/lineas:bulk`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...H_CREADOR },
    body: JSON.stringify({ lineas: [{ tipologia: "CargaSeca", peso: 5000 }] }),
  });

  let linea;
  for (let i = 0; i < 20; i++) {
    const det = await (await fetch(`${BASE}/api/solicitudes/${solicitudId}`, { headers: H_CREADOR })).json();
    if (det.lineas?.length) { linea = det.lineas[0].lineaId; break; }
    await new Promise((r) => setTimeout(r, 300));
  }

  const cotResp = await fetch(`${BASE}/api/cotizaciones`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...H_CREADOR },
    body: JSON.stringify({ solicitudId }),
  });
  const { id: cid } = await cotResp.json();

  await fetch(`${BASE}/api/cotizaciones/${cid}/lineas`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...H_CREADOR },
    body: JSON.stringify({ lineaSolicitudId: linea, valorFlete: 1500000, fuente: "sicetac" }),
  });

  const entregarResp = await fetch(`${BASE}/api/cotizaciones/${cid}/entregar`, { method: "POST", headers: H_CREADOR });
  const entregar = await entregarResp.json();
  console.log(`entregar -> ${entregarResp.status}:`, entregar);

  console.log("\nEsperando la notificación...");
  let mensajes = [];
  for (let i = 0; i < 20; i++) {
    const r = await fetch("http://localhost:5000/api/Messages");
    const j = await r.json();
    if (j.rowCount > 0) { mensajes = j.results; break; }
    await new Promise((r) => setTimeout(r, 2000));
  }

  if (mensajes.length === 0) {
    console.error("FALLÓ: no llegó ningún correo.");
    process.exit(1);
  }

  const msg = mensajes[0];
  const destinatario = msg.to.map((t) => t.address || t).join(",");
  console.log(`\nCorreo recibido -> To: ${destinatario} | Subject: ${msg.subject}`);

  const ok = destinatario === EMAIL_CREADOR_ESPERADO;
  console.log(ok
    ? `TODO OK: el correo llegó al creador real de la solicitud (${EMAIL_CREADOR_ESPERADO}), no a un "comercial responsable" del DW.`
    : `FALLÓ: esperaba ${EMAIL_CREADOR_ESPERADO}, llegó a ${destinatario}.`);
  process.exit(ok ? 0 : 1);
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
