// QA — S3-HU-02.3 (PR #127): eliminar_vehiculo por cola + evento vehiculo.eliminado.
// Crea un vehiculo, confirma que aparece en la proyeccion de NEXO, lo borra (rol
// Admin) y confirma que desaparece. Ademas confirma el control de rol (no-Admin
// recibe 403) y que un vehiculo EN USO por un plan no se borra (Cubicador rechaza
// con VEHICULO_EN_USO, sin publicar el evento).
// Uso: node verify-eliminar-vehiculo.mjs
const BASE = process.env.NEXO_BASE_URL ?? "http://localhost:8080";
const ADMIN = { "X-Portal-Usuario-Id": process.env.NEXO_ADMIN_ID ?? "1005", "Content-Type": "application/json" };
const NO_ADMIN = { "X-Portal-Usuario-Id": process.env.NEXO_NO_ADMIN_ID ?? "1001", "Content-Type": "application/json" };
const espera = (ms) => new Promise((r) => setTimeout(r, ms));

async function crearVehiculo(nombre) {
  const r = await fetch(`${BASE}/api/vehiculos`, {
    method: "POST", headers: ADMIN, body: JSON.stringify({
      nombre, descripcion: "verify-eliminar-vehiculo",
      largoInternoMm: 8000, anchoInternoMm: 2300, altoInternoMm: 2300,
      capacidadKg: 10000, taraKg: 5000, puertaAnchoMm: 2200, puertaAltoMm: 2200, puertaPosicion: "trasera",
      ejes: [{ posicionMm: 1000, capacidadKg: 5000 }],
    }),
  });
  const body = await r.json();
  return body.correlationId;
}
async function listado() {
  return (await (await fetch(`${BASE}/api/vehiculos`, { headers: ADMIN })).json());
}

async function main() {
  let ok = true;

  console.log("1) crear vehiculo y confirmar que aparece en la proyeccion...");
  const id1 = await crearVehiculo("QA-Eliminar-1");
  await espera(3000);
  let vive = (await listado()).some((v) => v.id === id1);
  console.log(`   presente antes de borrar: ${vive}`);
  if (!vive) { console.error("FALLO: el vehiculo deberia aparecer tras crearse."); ok = false; }

  console.log("2) borrar (rol Admin)...");
  const del = await fetch(`${BASE}/api/vehiculos/${id1}`, { method: "DELETE", headers: ADMIN });
  console.log(`   -> HTTP ${del.status}`);
  await espera(3000);
  vive = (await listado()).some((v) => v.id === id1);
  console.log(`   presente despues de borrar: ${vive}`);
  if (vive) { console.error("FALLO: el vehiculo deberia desaparecer tras el evento vehiculo.eliminado."); ok = false; }

  console.log("3) control de rol: usuario NO-Admin intenta borrar...");
  const rechazo = await fetch(`${BASE}/api/vehiculos/00000000-0000-0000-0000-000000000001`, { method: "DELETE", headers: NO_ADMIN });
  console.log(`   -> HTTP ${rechazo.status}`);
  if (rechazo.status !== 403) { console.error("FALLO: esperaba 403 para un usuario sin rol Admin."); ok = false; }

  console.log("4) VEHICULO_EN_USO: crear un plan usando un vehiculo y luego intentar borrarlo...");
  const id2 = await crearVehiculo("QA-Eliminar-EnUso");
  await espera(3000);
  await fetch(`${BASE}/api/planes-cargue`, {
    method: "POST", headers: ADMIN, body: JSON.stringify({ nombre: "QA-Plan-UsaVehiculo", vehiculoId: id2 }),
  });
  await espera(3000);
  await fetch(`${BASE}/api/vehiculos/${id2}`, { method: "DELETE", headers: ADMIN });
  await espera(3000);
  vive = (await listado()).some((v) => v.id === id2);
  console.log(`   vehiculo en uso sigue existiendo tras el intento de borrado: ${vive}`);
  if (!vive) { console.error("FALLO: un vehiculo en uso no deberia poder borrarse (VEHICULO_EN_USO)."); ok = false; }

  console.log(ok ? "\nTODO OK." : "\nHAY FALLAS.");
  process.exit(ok ? 0 : 1);
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
