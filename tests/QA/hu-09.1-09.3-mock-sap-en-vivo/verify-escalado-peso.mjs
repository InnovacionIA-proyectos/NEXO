// Verificación QA en vivo de la HU-09.1/09.3 (issues #31/#33, PR #80): confirma el
// escalado por peso de SapTarifaFleteEnVivoMockAdapter (factor = peso/8000, clamp
// 0.6-1.6) contra la app real, en los 3 casos límite. Requiere la app levantada con
// SAP_TARIFA_FLETE_EN_VIVO_MODO=Mock. El resultado se confirma por SQL directo (ver
// README) porque la respuesta de POST /api/captura-solicitudes no trae el valor del
// flete -- solo confirma cuántas líneas quedaron auto-resueltas.
const BASE = "http://localhost:8080";
const H = { "X-Portal-Usuario-Id": "1005", "Content-Type": "application/json" };

async function costear(peso) {
  const cliente = await fetch(`${BASE}/api/captura-solicitudes/clientes?q=a`, { headers: H }).then((r) => r.json());
  const clienteId = cliente[0].clienteId;
  const r = await fetch(`${BASE}/api/captura-solicitudes`, {
    method: "POST", headers: H,
    body: JSON.stringify({ clienteId, tipo: "spot", lineas: [{ origen: "Bogota", destino: "Medellin", tipoVehiculo: "Turbo", peso }] }),
  }).then((r) => r.json());
  return r.id;
}

async function main() {
  const casos = [
    { peso: 4000, factorEsperado: 0.6, valorEsperado: 1_710_000 },   // clamp inferior
    { peso: 8000, factorEsperado: 1.0, valorEsperado: 2_850_000 },   // base
    { peso: 20000, factorEsperado: 1.6, valorEsperado: 4_560_000 },  // clamp superior
  ];
  for (const c of casos) {
    const id = await costear(c.peso);
    console.log(`peso=${c.peso} -> solicitudId=${id} (esperado: factor ${c.factorEsperado}, valor ${c.valorEsperado})`);
  }
}
main().catch((e) => { console.error(e); process.exit(1); });
