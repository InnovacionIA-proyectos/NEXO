// Carga QA para la HU-09.1/09.3: 5 rutas distintas del dataset mock, consultadas
// CONCURRENTEMENTE, confirmando (por SQL, ver README) que cada una resuelve su propio
// valor exacto sin cruzarse con las demás. Mismos requisitos que verify-escalado-peso.mjs.
const BASE = "http://localhost:8080";
const H = { "X-Portal-Usuario-Id": "1005", "Content-Type": "application/json" };

const CASOS = [
  { origen: "Bogota", destino: "Cali", peso: 8000, valorEsperado: 3_100_000 },
  { origen: "Cali", destino: "Bogota", peso: 8000, valorEsperado: 3_100_000 },
  { origen: "Bogota", destino: "Barranquilla", peso: 8000, valorEsperado: 4_600_000 },
  { origen: "Medellin", destino: "Cartagena", peso: 8000, valorEsperado: 3_900_000 },
  { origen: "Bogota", destino: "Bucaramanga", peso: 8000, valorEsperado: 2_400_000 },
];

async function main() {
  const cliente = await fetch(`${BASE}/api/captura-solicitudes/clientes?q=a`, { headers: H }).then((r) => r.json());
  const clienteId = cliente[0].clienteId;

  console.log(`Disparando ${CASOS.length} solicitudes CONCURRENTES, cada una con una ruta distinta del dataset mock...`);
  const t0 = Date.now();
  const resultados = await Promise.all(
    CASOS.map((c) =>
      fetch(`${BASE}/api/captura-solicitudes`, {
        method: "POST", headers: H,
        body: JSON.stringify({ clienteId, tipo: "spot", lineas: [{ origen: c.origen, destino: c.destino, tipoVehiculo: "Turbo", peso: c.peso }] }),
      }).then((r) => r.json())
    )
  );
  console.log(`${resultados.length} solicitudes en ${Date.now() - t0} ms.`);

  const ids = resultados.map((r) => r.id);
  console.log("ids:", ids);
  console.log(ids.join(","));
}
main().catch((e) => { console.error(e); process.exit(1); });
