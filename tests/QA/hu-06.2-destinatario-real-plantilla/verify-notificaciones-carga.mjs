// Carga QA para la HU-06.2: varios flujos completos (crear solicitud -> cotizar ->
// entregar) concurrentes, cada uno con un CREADOR distinto, confirmando que cada
// notificación llega exactamente a su propio creador y no se cruza con otro bajo
// concurrencia real. Mismos requisitos que verify-destinatario-real.mjs (smtp4dev +
// app levantada sin ConnectionStrings:IdentidadPortal).
const BASE = "http://localhost:8080";

// Solo CoordinadorFletes/Admin pueden crear solicitudes (POST /api/solicitudes) -
// LiderProceso/ServiciosLogisticos no tienen esa capacidad, ver SesionController.cs.
const CREADORES = [
  { id: 1001, email: "cherrera@boterosoto.com.co" },
  { id: 1002, email: "yzapata@boterosoto.com.co" },
  { id: 1005, email: "prestrepo@boterosoto.com.co" },
  { id: 1006, email: "ablanco@boterosoto.com.co" },
];

async function json(resp, contexto) {
  const texto = await resp.text();
  try {
    return JSON.parse(texto);
  } catch {
    throw new Error(`${contexto}: HTTP ${resp.status}, cuerpo no-JSON: ${texto.slice(0, 300)}`);
  }
}

async function flujoCompleto(creador, clienteId) {
  const H = { "X-Portal-Usuario-Id": String(creador.id) };
  const crear = await fetch(`${BASE}/api/solicitudes`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...H },
    body: JSON.stringify({ clienteId, tipo: "spot" }),
  });
  if (crear.status !== 201) throw new Error(`crear solicitud (creador ${creador.id}): HTTP ${crear.status}: ${await crear.text()}`);
  const { id: solicitudId } = await json(crear, `crear solicitud (creador ${creador.id})`);

  await fetch(`${BASE}/api/solicitudes/${solicitudId}/lineas:bulk`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...H },
    body: JSON.stringify({ lineas: [{ tipologia: "CargaSeca", peso: 3000 }] }),
  });

  let linea;
  for (let i = 0; i < 30; i++) {
    const r = await fetch(`${BASE}/api/solicitudes/${solicitudId}`, { headers: H });
    const det = await json(r, `poll solicitud (creador ${creador.id}, intento ${i})`);
    if (det.lineas?.length) { linea = det.lineas[0].lineaId; break; }
    await new Promise((r) => setTimeout(r, 300));
  }
  if (!linea) throw new Error(`creador ${creador.id}: nunca aparecio la linea tras el bulk`);

  const cotResp = await fetch(`${BASE}/api/cotizaciones`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...H },
    body: JSON.stringify({ solicitudId }),
  });
  const { id: cid } = await json(cotResp, `crear cotizacion (creador ${creador.id}, solicitud ${solicitudId}, linea ${linea})`);

  await fetch(`${BASE}/api/cotizaciones/${cid}/lineas`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...H },
    body: JSON.stringify({ lineaSolicitudId: linea, valorFlete: 900000, fuente: "sicetac" }),
  });

  await fetch(`${BASE}/api/cotizaciones/${cid}/entregar`, { method: "POST", headers: H });
  return { creador, cid };
}

async function main() {
  const clienteResp = await fetch(`${BASE}/api/captura-solicitudes/clientes?q=a`, { headers: { "X-Portal-Usuario-Id": "1005" } });
  const clientes = await clienteResp.json();
  const clienteId = clientes[0].clienteId;

  console.log(`Disparando ${CREADORES.length} flujos completos CONCURRENTES (crear->cotizar->entregar), cada uno con un creador distinto...`);
  const t0 = Date.now();
  const resultados = await Promise.all(CREADORES.map((c) => flujoCompleto(c, clienteId)));
  console.log(`${resultados.length} flujos completados en ${Date.now() - t0} ms.`);

  console.log("Esperando las notificaciones...");
  let mensajes = [];
  for (let i = 0; i < 20; i++) {
    const r = await fetch("http://localhost:5000/api/Messages?pageSize=50");
    const j = await r.json();
    if (j.rowCount >= CREADORES.length) { mensajes = j.results; break; }
    await new Promise((r) => setTimeout(r, 2000));
  }

  console.log(`\n${mensajes.length} correos recibidos (esperados ${CREADORES.length}).`);
  let ok = mensajes.length === CREADORES.length;

  const destinatariosRecibidos = mensajes.map((m) => m.to.map((t) => t.address || t).join(",")).sort();
  const destinatariosEsperados = CREADORES.map((c) => c.email).sort();

  console.log("Esperados:", destinatariosEsperados);
  console.log("Recibidos:", destinatariosRecibidos);

  if (JSON.stringify(destinatariosRecibidos) !== JSON.stringify(destinatariosEsperados)) {
    console.error("FALLÓ: los destinatarios no coinciden exactamente (posible cruce entre creadores).");
    ok = false;
  }

  console.log(ok
    ? "\nTODO OK: cada notificación llegó exactamente a su propio creador, sin cruces bajo concurrencia."
    : "\nHAY FALLAS.");
  process.exit(ok ? 0 : 1);
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
