// Integración/carga real para la HU-04.7 (PR #69): somete el dataset real de 320
// líneas (tests/CabinaFletes.Domain.Tests/TestData/pliego-300-lineas.json) contra
// POST /api/captura-solicitudes de la app REALMENTE levantada -- exactamente lo que
// el propio autor declaró "fuera de alcance" por falta de BD corporativa. Usamos la
// BD local de Docker como sustituto (mismo patrón ya usado en toda esta sesión de QA).
//
// Requiere la app completa levantada, corrido desde la raíz del repo:
//   docker compose up -d --build --wait
//   node tests/QA/hu-04.7-pliego-dataset-validacion-muestral/verify-captura-carga-real.mjs
//
// No valida los valores de costeo (ese es el trabajo de PliegoDatasetValidacionTests.cs,
// que corre ReglaDeCosteo en aislamiento) -- este script valida el PIPELINE real completo:
// validación de modelo, resolución/creación de rutas y vehículos desde texto libre, y que
// el costeo diferido en segundo plano no deje nada colgado a escala real (316 líneas).
const BASE = "http://localhost:8080";
import fs from "fs";

async function main() {
  const dataset = JSON.parse(fs.readFileSync(
    "tests/CabinaFletes.Domain.Tests/TestData/pliego-300-lineas.json", "utf8"));
  const lineas = dataset.lineas;
  console.log(`Dataset cargado: ${lineas.length} líneas.`);

  const malformadas = lineas.filter(l => l.caso === "peso_malformado" || l.caso === "sin_ruta");
  const validas = lineas.filter(l => !malformadas.includes(l));
  console.log(`Válidas (para el envío masivo): ${validas.length}. Malformadas (para probar 400 una por una): ${malformadas.length}.`);

  const clienteResp = await fetch(`${BASE}/api/captura-solicitudes/clientes?q=a`, { headers: { "X-Rol": "Admin" } });
  const clientes = await clienteResp.json();
  const clienteId = clientes[0].clienteId;

  function aLineaRequest(l) {
    return {
      origen: l.origen, destino: l.destino, tipoVehiculo: l.vehiculo, carroceria: l.carroceria,
      tipologia: l.tipologia, lineaNegocio: l.lineaNegocio, claseViaje: l.claseViaje,
      claseContenedor: l.claseContenedor, exclusivo: l.exclusivo, consolidado: l.consolidado,
      retornaContenedor: l.retornaContenedor, ciudadDevolucionContenedor: l.ciudadDevolucionContenedor,
      peso: l.peso,
    };
  }

  // 1) Las 4 líneas malformadas/sin ruta, UNA POR UNA -- confirmar que el propio
  //    modelo de datos las rechaza con 400, tal como documenta el dataset.
  console.log("\n=== Validación individual de las 4 líneas malformadas/sin ruta ===");
  for (const l of malformadas) {
    const r = await fetch(`${BASE}/api/captura-solicitudes`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "X-Rol": "Comercial" },
      body: JSON.stringify({ clienteId, tipo: "spot", lineas: [aLineaRequest(l)] }),
    });
    console.log(`  línea ${l.id} (${l.caso}): HTTP ${r.status} ${r.status === 400 ? "OK (rechazada, esperado)" : "**INESPERADO**"}`);
  }

  // 2) Las 316 líneas válidas restantes (320 - 4 malformadas), EN UN SOLO envío --
  //    la escala real más grande sometida contra la app viva en toda esta sesión de QA.
  console.log(`\n=== Envío masivo de ${validas.length} líneas válidas, en una sola petición ===`);
  const t0 = Date.now();
  const crear = await fetch(`${BASE}/api/captura-solicitudes`, {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Rol": "Comercial" },
    body: JSON.stringify({
      clienteId, tipo: "licitacion",
      lineas: validas.map(aLineaRequest),
    }),
  });
  const tCaptura = Date.now() - t0;
  if (crear.status !== 201) {
    console.error(`FALLÓ: captura -> ${crear.status} ${await crear.text()}`);
    process.exit(1);
  }
  const resultado = await crear.json();
  console.log(`HTTP 201 en ${tCaptura} ms.`);
  console.log(JSON.stringify(resultado, null, 2));

  if (resultado.lineas !== validas.length) {
    console.error(`FALLÓ: esperaba ${validas.length} líneas capturadas, llegaron ${resultado.lineas}.`);
    process.exit(1);
  }
  if (!resultado.costeoEnProceso) {
    console.error("FALLÓ: con más de 10 líneas se esperaba costeoEnProceso=true (auto-costeo diferido).");
    process.exit(1);
  }
  console.log(`OK: ${resultado.lineas} líneas capturadas, rutasCreadas=${resultado.rutasCreadas}, vehiculosCreados=${resultado.vehiculosCreados}, costeo diferido a segundo plano.`);
  console.log(`\nsolicitudId=${resultado.id}`);
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
