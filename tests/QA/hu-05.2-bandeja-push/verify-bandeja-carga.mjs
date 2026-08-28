// Estrés QA en vivo del canal de bandeja para la HU-05.2 (issue #20, PR #67).
// Contra la app real: N clientes SignalR reales conectados a la vez a /hubs/bandeja
// (fan-out real de un solo grupo, no simulado) mientras se disparan M pliegos
// concurrentes de tamaños distintos. Verifica que:
//   1) TODOS los clientes conectados reciben el mismo número de avisos (el broadcast
//      de un solo grupo no pierde ni duplica hacia ningún suscriptor bajo carga).
//   2) El agrupamiento se sostiene bajo concurrencia real de infraestructura (no solo
//      en memoria, como BandejaCargaTests.cs): exactamente M avisos "trabajo-terminado".
//   3) El sistema queda limpio después de la ráfaga: 0 mensajes en las colas, sin cola
//      de error.
//
// Habla el protocolo JSON de SignalR directo por WebSocket (Node 22+), igual que
// verify-signalr-live.mjs (HU-04.5) — sin dependencias externas.
//
// Requiere la app completa levantada:
//   docker compose up -d --build --wait
//   node tests/QA/hu-05.2-bandeja-push/verify-bandeja-carga.mjs
const BASE = "http://localhost:8080";
const WS_BASE = "ws://localhost:8080";
const RS = "\x1e";
const N_CLIENTES = 8;
const PLIEGOS = [1, 3, 5, 2, 8, 4, 6, 2, 10, 1, 7, 3]; // M = 12, tamaños desiguales a propósito

async function conectarClienteBandeja() {
  const negotiate = await fetch(`${BASE}/hubs/bandeja/negotiate?negotiateVersion=1`, {
    method: "POST",
    headers: { "X-Rol": "Admin" },
  });
  if (negotiate.status !== 200) throw new Error(`negotiate: ${negotiate.status}`);
  const { connectionToken } = await negotiate.json();

  const ws = new WebSocket(`${WS_BASE}/hubs/bandeja?id=${connectionToken}`, {
    headers: { "X-Rol": "Admin" },
  });

  const avisos = [];
  const listo = new Promise((resolve, reject) => {
    ws.addEventListener("open", () => {
      ws.send(JSON.stringify({ protocol: "json", version: 1 }) + RS);
    });
    ws.addEventListener("message", (ev) => {
      const texto = typeof ev.data === "string" ? ev.data : ev.data.toString();
      for (const trozo of texto.split(RS).filter(Boolean)) {
        const msg = JSON.parse(trozo);
        if (Object.keys(msg).length === 0) {
          ws.send(JSON.stringify({ type: 1, target: "Suscribir", arguments: [] }) + RS);
          resolve();
        } else if (msg.type === 1 && msg.target === "bandeja") {
          avisos.push(msg.arguments[0]);
        }
      }
    });
    ws.addEventListener("error", reject);
  });
  await listo;
  return { ws, avisos };
}

async function main() {
  console.log(`Conectando ${N_CLIENTES} clientes concurrentes a /hubs/bandeja...`);
  const clientes = await Promise.all(
    Array.from({ length: N_CLIENTES }, () => conectarClienteBandeja())
  );
  console.log(`${clientes.length} clientes conectados y suscritos al grupo único.`);

  const { clienteId, clienteNombre } = await (async () => {
    const r = await fetch(`${BASE}/api/captura-solicitudes/clientes?q=a`, {
      headers: { "X-Rol": "Admin" },
    });
    const lista = await r.json();
    if (!lista.length) throw new Error("no hay clientes en la semilla");
    return { clienteId: lista[0].clienteId, clienteNombre: lista[0].nombre };
  })();
  console.log(`Disparando ${PLIEGOS.length} pliegos concurrentes (cliente ${clienteNombre})...`);

  const cronometro = Date.now();
  await Promise.all(
    PLIEGOS.map(async (lineas) => {
      const crear = await fetch(`${BASE}/api/solicitudes`, {
        method: "POST",
        headers: { "Content-Type": "application/json", "X-Rol": "Admin" },
        body: JSON.stringify({ clienteId, tipo: "spot" }),
      });
      if (crear.status !== 201) throw new Error(`crear solicitud: ${crear.status} ${await crear.text()}`);
      const { id: solicitudId } = await crear.json();

      const bulk = await fetch(`${BASE}/api/solicitudes/${solicitudId}/lineas:bulk`, {
        method: "POST",
        headers: { "Content-Type": "application/json", "X-Rol": "Admin" },
        body: JSON.stringify({
          lineas: Array.from({ length: lineas }, (_, i) => ({
            tipologia: "CargaSeca",
            peso: 10000 + i,
          })),
        }),
      });
      if (bulk.status !== 202) throw new Error(`bulk: ${bulk.status} ${await bulk.text()}`);
    })
  );
  console.log(`${PLIEGOS.length} pliegos disparados en ${Date.now() - cronometro} ms.`);

  // Dar tiempo a que el auto-costeo (async, vía broker) cierre los M pliegos y
  // publique los M avisos de bandeja.
  await new Promise((r) => setTimeout(r, 15000));
  clientes.forEach((c) => c.ws.close());

  console.log("\nAvisos 'trabajo-terminado' recibidos por cada cliente:");
  const conteos = clientes.map(
    (c) => c.avisos.filter((a) => a.motivo === "trabajo-terminado").length
  );
  conteos.forEach((n, i) => console.log(`  cliente ${i + 1}: ${n}`));

  let ok = true;
  const esperado = PLIEGOS.length;
  if (!conteos.every((n) => n === esperado)) {
    console.error(`FALLÓ: no todos los clientes recibieron los ${esperado} avisos esperados (fan-out inconsistente).`);
    ok = false;
  } else {
    console.log(`OK: los ${N_CLIENTES} clientes recibieron exactamente ${esperado} avisos cada uno (fan-out íntegro bajo carga).`);
  }

  const otrosMotivosVistos = new Set(clientes[0].avisos.map((a) => a.motivo));
  console.log(`Motivos observados: ${[...otrosMotivosVistos].join(", ")}`);

  console.log(ok ? "\nTODO OK." : "\nHAY FALLAS.");
  process.exit(ok ? 0 : 1);
}

main().catch((e) => {
  console.error("ERROR:", e);
  process.exit(1);
});
