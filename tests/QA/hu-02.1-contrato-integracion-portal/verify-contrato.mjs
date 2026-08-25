// Verificación QA de la issue #7 / PR #59 (S2-HU-02.1 — contrato de
// integración con el portal). El PR es documentación pura (docs/contrato-
// integracion/: README, openapi.yaml, escenarios.feature, ADR-001) — no
// agrega código ejecutable. La sección 3 del README dice explícitamente
// que "no es propuesta: es lo que el backend exige HOY, leído de los
// atributos [Authorize] de cada controlador" — esa frase convierte la
// tabla en una afirmación verificable, así que este script la verifica
// contra el código real en vez de solo leerla.
//
// No aplica pytest (no hay Python) ni Playwright (no hay UI): es una
// comprobación de "el documento dice la verdad sobre el código", así
// que un script que lea ambos lados y compare es la herramienta correcta.
//
// Uso: node tests/QA/hu-02.1-contrato-integracion-portal/verify-contrato.mjs

import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";

const CONTROLLERS_DIR = "src/CabinaFletes.Api/Controllers";

// Controladores que la sección 3 del README SÍ describe explícitamente
// (por nombre de operación, no por nombre de clase — mapeados a mano
// leyendo la tabla una vez). Cualquier otro controlador con un
// [Authorize(Roles=...)] --es decir, restringido a roles específicos,
// no solo "cualquier autenticado"-- que no esté en esta lista es un
// hueco de documentación.
const CUBIERTOS_POR_LA_TABLA = new Set([
  "CapturaSolicitudesController",
  "SolicitudesController",
  "CotizacionesController",
  "ServiciosConexosController",
  "ReglasSoloSicetacController",
  "MetricasController",
]);

let ok = true;
const archivos = readdirSync(CONTROLLERS_DIR).filter((f) => f.endsWith(".cs"));
const conRolRestringido = [];

for (const archivo of archivos) {
  const contenido = readFileSync(join(CONTROLLERS_DIR, archivo), "utf8");
  const clase = archivo.replace(".cs", "");
  const matches = [...contenido.matchAll(/\[Authorize\(Roles\s*=\s*"([^"]+)"\)\]/g)];
  if (matches.length > 0) {
    const roles = [...new Set(matches.map((m) => m[1]))];
    conRolRestringido.push({ clase, roles });
  }
}

console.log("Controladores con [Authorize(Roles=...)] en el código real:");
for (const { clase, roles } of conRolRestringido) {
  const cubierto = CUBIERTOS_POR_LA_TABLA.has(clase);
  console.log(`  ${cubierto ? "✓" : "✗"} ${clase} — ${roles.join(" | ")}`);
}

const noDocumentados = conRolRestringido.filter((c) => !CUBIERTOS_POR_LA_TABLA.has(c.clase));

console.log("");
if (noDocumentados.length > 0) {
  ok = false;
  console.error(
    `HALLAZGO: ${noDocumentados.length} controlador(es) con restricción de rol NO aparecen en la sección 3 del contrato (docs/contrato-integracion/README.md):`
  );
  for (const { clase, roles } of noDocumentados) {
    console.error(`  - ${clase} (${roles.join(" | ")})`);
  }
} else {
  console.log("OK: todos los controladores con restricción de rol están cubiertos por la tabla.");
}

process.exit(ok ? 0 : 1);
