// QA — S3-HU-02.3 (PR #127): corrige la carrera real entre crear_plan y optimizar.
// La fila espejo de NEXO ahora nace en "procesando" (antes "borrador"), asi que el
// guard existente en SolicitarOptimizacionCommand (409 si ya esta "procesando")
// bloquea un POST /optimizar inmediato tras crear el plan.
//
// La ventana real de la carrera es de pocos milisegundos en un entorno local (todo
// en Docker en localhost) - reproducirla con llamadas HTTP secuenciales normales no
// es confiable. Este script crea el plan y confirma que nace en "procesando"; para
// verificar el guard mismo de forma determinista, forzar el estado con
// force-plan-procesando-nexo.sql entre el paso 1 y el paso 2 (ver mensaje del
// script).
//
// Uso: node verify-guard-carrera-crear-plan.mjs <vehiculoId-valido-en-cubicador>
const BASE = process.env.NEXO_BASE_URL ?? "http://localhost:8080";
const H = { "X-Portal-Usuario-Id": process.env.NEXO_USUARIO_ID ?? "1005", "Content-Type": "application/json" };
const vehiculoId = process.argv[2];
if (!vehiculoId) { console.error("Uso: node verify-guard-carrera-crear-plan.mjs <vehiculoId>"); process.exit(2); }

async function main() {
  const plan = await (await fetch(`${BASE}/api/planes-cargue`, {
    method: "POST", headers: H, body: JSON.stringify({ nombre: "QA-Guard-Carrera", vehiculoId }),
  })).json();
  console.log("plan creado:", plan);
  if (plan.estado !== "procesando") {
    console.error(`FALLO: la fila espejo deberia nacer en 'procesando', nacio en '${plan.estado}'.`);
    process.exit(1);
  }
  console.log("OK: la fila espejo nacio en 'procesando' (antes del fix nacia en 'borrador').");

  console.log(`\n>>> Para probar el guard de forma determinista: forzar el estado con`);
  console.log(`    force-plan-procesando-nexo.sql (planId=${plan.planCargueId}) y luego`);
  console.log(`    llamar POST ${BASE}/api/planes-cargue/${plan.planCargueId}/optimizar`);
  console.log(`    -> debe responder 409 PLAN_CARGUE_EN_PROCESO. <<<`);
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
