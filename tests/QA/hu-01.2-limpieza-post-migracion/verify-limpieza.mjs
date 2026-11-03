// Verificación QA de la issue #2 / PR #58 (S2-HU-01.2 — eliminar
// src/_to_delete/ y sus marcadores de migración).
//
// Esto es un cambio ESTRUCTURAL (borra 4 archivos que solo tenían un
// comentario, sin código), no un cambio de comportamiento en runtime — así
// que ni pytest ni Playwright ni xUnit son la herramienta indicada para el
// caso de prueba principal (que la carpeta se fue y nada la referencia). Un
// script simple que audite el árbol de archivos es más directo. La
// regresión (que el resto siga compilando y pasando) sí se corre en xUnit,
// vía el arnés de Docker de la HU-01.1.
//
// Uso: node tests/QA/hu-01.2-limpieza-post-migracion/verify-limpieza.mjs
// (desde la raíz del repo)

import { existsSync, readFileSync } from "node:fs";
import { execSync } from "node:child_process";

let ok = true;
const fail = (msg) => {
  console.error(`FALLÓ: ${msg}`);
  ok = false;
};
const pass = (msg) => console.log(`OK: ${msg}`);

// ---------------------------------------------------------------- estático
// 1) La carpeta _to_delete/ ya no existe.
if (existsSync("src/_to_delete")) {
  fail("src/_to_delete todavía existe.");
} else {
  pass("src/_to_delete ya no existe.");
}

// 2) Ningún archivo fuente (ni el propio repo) referencia "_to_delete".
const grep = execSync(
  'git grep -n "_to_delete" -- "*.cs" "*.csproj" "*.sln" || true',
  { encoding: "utf8" }
).trim();
if (grep.length > 0) {
  fail(`quedan referencias a _to_delete:\n${grep}`);
} else {
  pass("ninguna referencia a _to_delete en código fuente.");
}

// 3) Los 4 tipos que vivían ahí siguen existiendo en su ubicación definitiva
//    (según PENDIENTES.md) — no se perdió nada, solo se movieron antes.
const reubicados = [
  "src/Nexo.Application/Common/Ports/IAuditoriaPort.cs",
  "src/Nexo.Application/Common/Ports/IPropuestaTarifaPort.cs",
  "src/Nexo.Application/Common/Dtos/PropuestaTarifaDtos.cs",
  "src/Nexo.Infrastructure/Adapters/PropuestaTarifaAdapter.cs",
  "src/Nexo.Infrastructure/Adapters/TarifarioSapAdapter.cs",
];
for (const ruta of reubicados) {
  if (!existsSync(ruta)) fail(`no existe la ubicación definitiva: ${ruta}`);
}
if (reubicados.every(existsSync)) {
  pass("las 5 ubicaciones definitivas mencionadas en PENDIENTES.md existen.");
}

// 4) PENDIENTES.md ya no promete un borrado manual pendiente (contradiría
//    la propia HU si el texto viejo sigue ahí).
const pendientes = readFileSync("PENDIENTES.md", "utf8");
if (/borrado físico definitivo|manualmente desde el Explorador/i.test(pendientes)) {
  fail("PENDIENTES.md todavía promete un borrado manual pendiente.");
} else {
  pass("PENDIENTES.md ya no promete un borrado manual pendiente.");
}

// ---------------------------------------------------------------- resumen
console.log("");
console.log(ok ? "TODO OK." : "HAY FALLAS — ver arriba.");
console.log("");
console.log("Nota de cobertura de esta verificación:");
console.log(" - Estático/estructural: cubierto arriba (esta ejecución).");
console.log(" - Smoke + regresión (build + Domain/Application.Tests): cubiertos");
console.log("   por separado vía el arnés de la HU-01.1 (docker-compose.ci.yml).");
console.log(" - Integración (Api.Tests): no aplica — este PR no toca ningún");
console.log("   endpoint, DbContext ni comportamiento en runtime.");
console.log(" - Carga/estrés: no aplica — no hay código ejecutable nuevo que");
console.log("   pueda tener un problema de rendimiento; son 4 archivos borrados");
console.log("   y 2 documentos actualizados.");

process.exit(ok ? 0 : 1);
