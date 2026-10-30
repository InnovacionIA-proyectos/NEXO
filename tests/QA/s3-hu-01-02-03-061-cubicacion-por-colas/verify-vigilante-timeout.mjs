// QA — S3-HU-06.1 (PR #123): el vigilante de timeouts de Cubicador debe detectar un
// plan atascado en "procesando" y publicar cubicacion.rechazada, marcandolo "error"
// tanto en Cubicador como en NEXO.
//
// Requiere que el stack tenga CUBICADOR_TIMEOUT_PROCESANDO_MINUTOS y
// CUBICADOR_INTERVALO_VIGILANTE_SEGUNDOS bajados (p.ej. 1 minuto / 10 segundos) para
// no tener que esperar el timeout real de produccion (5 min por defecto).
//
// Este script solo crea el plan y hace el polling final. El paso intermedio -forzar
// el plan a 'procesando' con un timestamp viejo- se hace con el UPDATE de
// force-plan-procesando.sql contra la base de Cubicador (no se automatiza aqui
// porque requiere una conexion directa a Postgres, fuera del alcance HTTP de NEXO).
//
// Uso: node verify-vigilante-timeout.mjs <vehiculoId-valido-en-cubicador>
//   1) correr este script, que imprime el planId y se queda esperando
//   2) en otra terminal, correr force-plan-procesando.sql con ese planId
//   3) el script detecta el cambio a "error" y termina
const BASE = process.env.NEXO_BASE_URL ?? "http://localhost:8080";
const H = { "X-Portal-Usuario-Id": process.env.NEXO_USUARIO_ID ?? "1005", "Content-Type": "application/json" };
const vehiculoId = process.argv[2];
if (!vehiculoId) { console.error("Uso: node verify-vigilante-timeout.mjs <vehiculoId>"); process.exit(2); }

const espera = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const plan = await (await fetch(`${BASE}/api/planes-cargue`, {
    method: "POST", headers: H, body: JSON.stringify({ nombre: "QA-Vigilante-Timeout", vehiculoId }),
  })).json();
  const planId = plan.planCargueId;
  console.log(`plan creado: ${planId}`);
  console.log(`\n>>> Ahora corre force-plan-procesando.sql contra Postgres con este planId <<<\n`);
  console.log("esperando hasta 90s a que el vigilante lo marque 'error'...");

  for (let i = 0; i < 18; i++) {
    await espera(5000);
    const consulta = await (await fetch(`${BASE}/api/planes-cargue/${planId}`, { headers: H })).json();
    console.log(`  [${(i + 1) * 5}s] estado = ${consulta.estado}`);
    if (consulta.estado === "error") {
      console.log("\nOK: el vigilante detecto el plan atascado y lo marco 'error' (en Cubicador y en NEXO via cubicacion.rechazada).");
      return;
    }
  }
  console.error("\nFALLO: el plan nunca paso a 'error' - el vigilante no reacciono a tiempo.");
  process.exit(1);
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
