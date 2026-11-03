// Verificación QA en vivo del CORS multi-origen para la HU-02.2 (issue #8, PR #75).
// Cubre exactamente lo que CorsTests.cs (del propio PR) NO cubre: el mecanismo real
// de lista separada por coma leído de una variable de entorno (ORIGENES_PERMITIDOS_CORS
// / Cors__OrigenesPermitidos), incluidos espacios alrededor de la coma, y un preflight
// OPTIONS real como el que manda un navegador antes de un POST.
//
// Requiere la app levantada con al menos 3 orígenes en la lista, uno de ellos con un
// espacio adelante a propósito (para probar el TrimEntries de Program.cs), p. ej.:
//   ORIGENES_PERMITIDOS_CORS=http://localhost:5173, https://portal-qa.example.com,https://portal.example.com
//   docker compose up -d --build --wait
//   node tests/QA/hu-02.2-cors-multiorigen-portal/verify-cors-multiorigen.mjs
const BASE = "http://localhost:8080";

const ORIGENES_ESPERADOS_PERMITIDOS = [
  "http://localhost:5173",
  "https://portal-qa.boterosoto.com.co", // con espacio adelante en la variable de entorno
  "https://portal.boterosoto.com.co",    // sin espacio
];
const ORIGEN_NO_AUTORIZADO = "https://origen-malicioso.example.com";

async function main() {
  let ok = true;

  for (const origen of ORIGENES_ESPERADOS_PERMITIDOS) {
    const r = await fetch(`${BASE}/health`, { headers: { Origin: origen } });
    const permitido = r.headers.get("access-control-allow-origin");
    if (permitido === origen) {
      console.log(`OK: ${origen} -> Access-Control-Allow-Origin: ${permitido}`);
    } else {
      console.error(`FALLÓ: ${origen} -> esperaba el encabezado, llegó: ${permitido}`);
      ok = false;
    }
  }

  const rNo = await fetch(`${BASE}/health`, { headers: { Origin: ORIGEN_NO_AUTORIZADO } });
  const encabezadoNo = rNo.headers.get("access-control-allow-origin");
  if (encabezadoNo === null) {
    console.log(`OK: ${ORIGEN_NO_AUTORIZADO} -> sin Access-Control-Allow-Origin (correcto, el navegador bloquearía la lectura)`);
  } else {
    console.error(`FALLÓ: ${ORIGEN_NO_AUTORIZADO} -> recibió el encabezado igual: ${encabezadoNo}`);
    ok = false;
  }

  // Preflight real: lo que manda un navegador antes de un POST con Content-Type/X-Rol.
  const rPreflight = await fetch(`${BASE}/api/solicitudes`, {
    method: "OPTIONS",
    headers: {
      Origin: "https://portal.boterosoto.com.co",
      "Access-Control-Request-Method": "POST",
      "Access-Control-Request-Headers": "content-type,x-rol",
    },
  });
  const permiteOrigen = rPreflight.headers.get("access-control-allow-origin");
  const permiteMetodo = rPreflight.headers.get("access-control-allow-methods");
  const permiteHeaders = rPreflight.headers.get("access-control-allow-headers");
  if (rPreflight.status === 204 && permiteOrigen && permiteMetodo?.includes("POST") && permiteHeaders) {
    console.log(`OK: preflight OPTIONS real -> ${rPreflight.status}, Origin=${permiteOrigen}, Methods=${permiteMetodo}, Headers=${permiteHeaders}`);
  } else {
    console.error(`FALLÓ: preflight OPTIONS -> status=${rPreflight.status}, Origin=${permiteOrigen}, Methods=${permiteMetodo}, Headers=${permiteHeaders}`);
    ok = false;
  }

  console.log(ok ? "\nTODO OK." : "\nHAY FALLAS.");
  process.exit(ok ? 0 : 1);
}

main().catch((e) => { console.error("ERROR:", e); process.exit(1); });
