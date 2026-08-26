// Verificación QA en vivo del canal SignalR para la HU-04.5 (issue #17, PR #64).
// Habla el protocolo JSON de SignalR directo por WebSocket (Node 22+ trae
// WebSocket nativo) - no necesita el paquete cliente de SignalR ni ninguna
// dependencia externa.
//
// Requiere la app completa levantada:
//   cp .env.example .env   # completar MSSQL_SA_PASSWORD, RABBITMQ_PASSWORD
//   echo "ASPNETCORE_ENVIRONMENT=Development" >> .env   # el esquema Dev/X-Rol
//                                                        # exige no-Production
//   docker compose up -d --build --wait
//   node tests/QA/hu-04.5-signalr-avisos-respaldo/verify-signalr-live.mjs
//
// Ajusta CLIENTE_ID abajo por un Id real de la tabla `clientes` de tu semilla
// si el que trae este archivo no existe en tu base.
const BASE = "http://localhost:8080";
const WS_BASE = "ws://localhost:8080";
const CLIENTE_ID = "AAB1144E-A779-437F-A0CA-53449188E564";
const RS = "\x1e"; // record separator del protocolo de SignalR

async function main() {
  // 1) Crear una solicitud de licitación (rol CoordinadorFletes/Admin para bulk).
  const crear = await fetch(`${BASE}/api/solicitudes`, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Rol": "Admin" },
    body: JSON.stringify({ clienteId: CLIENTE_ID, tipo: "licitacion" }),
  });
  if (crear.status !== 201) throw new Error(`crear solicitud: ${crear.status} ${await crear.text()}`);
  const { id: solicitudId } = await crear.json();
  console.log("solicitud creada:", solicitudId);

  // 2) Negotiate + conectar WebSocket al hub, ANTES de cargar líneas, para no
  //    perderse ningún aviso.
  const negotiate = await fetch(`${BASE}/hubs/trabajos/negotiate?negotiateVersion=1`, {
    method: "POST",
    headers: { "X-Rol": "Admin" },
  });
  if (negotiate.status !== 200) throw new Error(`negotiate: ${negotiate.status}`);
  const { connectionToken } = await negotiate.json();

  const ws = new WebSocket(`${WS_BASE}/hubs/trabajos?id=${connectionToken}`, {
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
          // Handshake OK (mensaje vacío {}). Ahora sí, suscribirse.
          resolve();
        } else if (msg.type === 1 && msg.target === "avance") {
          avisos.push(msg.arguments[0]);
        }
      }
    });
    ws.addEventListener("error", reject);
  });
  await listo;
  console.log("WebSocket conectado y handshake OK.");

  // 3) Cargar líneas en bulk -> dispara el auto-costeo async.
  const lineas = Array.from({ length: 8 }, (_, i) => ({
    tipologia: i % 2 === 0 ? "Contenedor" : "GranelSolido",
    peso: 10000 + i * 1000,
  }));
  const bulk = await fetch(`${BASE}/api/solicitudes/${solicitudId}/lineas:bulk`, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Rol": "Admin" },
    body: JSON.stringify({ lineas }),
  });
  if (bulk.status !== 202) throw new Error(`bulk: ${bulk.status} ${await bulk.text()}`);
  const { trabajoId, consultarEn } = await bulk.json();
  console.log("trabajo aceptado:", trabajoId, "| consultarEn:", consultarEn);

  // 4) Suscribirse al grupo del trabajo (con lo anterior ya puede haber avisos
  //    perdidos por diseño - el respaldo es justo lo que se prueba en el paso 6).
  ws.send(JSON.stringify({ type: 1, target: "Suscribir", arguments: [trabajoId] }) + RS);

  // 5) Esperar hasta 20s recogiendo avisos.
  await new Promise((r) => setTimeout(r, 20000));
  ws.close();

  console.log(`\nAvisos recibidos: ${avisos.length}`);
  avisos.forEach((a) =>
    console.log(`  secuencia=${a.secuencia} estado=${a.estado} resueltas=${a.avance.resueltas}/${a.avance.total ?? "?"}`)
  );

  // 6) Consulta de respaldo: reconstruye el estado sin haber recibido ningún aviso.
  const consulta = await fetch(`${BASE}${consultarEn}`, { headers: { "X-Rol": "Admin" } });
  const estado = await consulta.json();
  console.log(`\nConsulta de respaldo: secuencia=${estado.secuencia} estado=${estado.estado} resueltas=${estado.avance.resueltas}/${estado.avance.total}`);

  // 7) Verificaciones.
  let ok = true;
  const secuencias = avisos.map((a) => a.secuencia);
  for (let i = 1; i < secuencias.length; i++) {
    if (secuencias[i] <= secuencias[i - 1]) {
      console.error(`FALLÓ: secuencia no creciente entre avisos: ${secuencias[i - 1]} -> ${secuencias[i]}`);
      ok = false;
    }
  }
  if (secuencias.length > 0 && estado.secuencia < secuencias[secuencias.length - 1]) {
    console.error("FALLÓ: la consulta de respaldo trae una secuencia menor que el último aviso recibido.");
    ok = false;
  }
  if (avisos.some((a) => "pendientes" in a || "Pendientes" in a)) {
    console.error("FALLÓ: el aviso lleva la lista de pendientes (debería llevar solo contadores).");
    ok = false;
  }
  if (estado.estado === "Completado" || estado.estado === "Parcial") {
    console.log("OK: el trabajo llegó a un desenlace final.");
  }

  console.log(ok ? "\nTODO OK." : "\nHAY FALLAS.");
  process.exit(ok ? 0 : 1);
}

main().catch((e) => {
  console.error("ERROR:", e);
  process.exit(1);
});
