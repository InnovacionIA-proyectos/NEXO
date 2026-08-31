// Carga/estrés QA para la HU-03 (issues #9/#10/#11, PR #77): la identidad se resuelve
// en CADA petición, sin caché (docs/contrato-integracion, sección 1) - este script
// confirma que, bajo peticiones concurrentes de VARIOS usuarios a la vez, ninguna
// recibe la identidad de otro (el riesgo real de resolver por petición sin caché es
// justo ese: una fuga entre scopes de DI bajo carga).
//
// Requiere la app levantada SIN ConnectionStrings:IdentidadPortal configurada (usa
// ResolverIdentidadPortalLocalAdapter contra la tabla Usuarios local sembrada):
//   docker compose up -d --build --wait
//   node tests/QA/hu-03-sesion-portal-6-roles/verify-identidad-carga.mjs
const BASE = "http://localhost:8080";
const USUARIOS = [
  { id: 1001, nombre: "Carlos Herrera", rol: "CoordinadorFletes" },
  { id: 1002, nombre: "Yined Zapata", rol: "CoordinadorFletes" },
  { id: 1003, nombre: "Luisa Chaverra", rol: "LiderProceso" },
  { id: 1004, nombre: "Sandra Nieto", rol: "ServiciosLogisticos" },
  { id: 1005, nombre: "Paola Restrepo", rol: "Admin" },
  { id: 1006, nombre: "Albert Blanco", rol: "Admin" },
];
const RONDAS = 10; // 6 usuarios x 10 rondas = 60 peticiones concurrentes

async function main() {
  const peticiones = [];
  for (let r = 0; r < RONDAS; r++) {
    for (const u of USUARIOS) {
      peticiones.push(
        fetch(`${BASE}/api/sesion/actual`, { headers: { "X-Portal-Usuario-Id": String(u.id) } })
          .then((r) => r.json())
          .then((cuerpo) => ({ esperado: u, recibido: cuerpo }))
      );
    }
  }

  const resultados = await Promise.all(peticiones);
  let ok = true;
  for (const { esperado, recibido } of resultados) {
    if (recibido.id !== esperado.id || recibido.nombre !== esperado.nombre || recibido.rol !== esperado.rol) {
      console.error(`FALLÓ: pedí id=${esperado.id} (${esperado.nombre}/${esperado.rol}) y recibí ${JSON.stringify(recibido)}`);
      ok = false;
    }
  }

  console.log(`${resultados.length} peticiones concurrentes, ${USUARIOS.length} identidades distintas x ${RONDAS} rondas.`);
  console.log(ok ? "TODO OK: ninguna petición recibió la identidad de otro usuario." : "HAY FALLAS: hubo cruce de identidades.");
  process.exit(ok ? 0 : 1);
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
