// Verificación QA de la issue #50 / PR #60 (S2-HU-01.7 — reorganizar las 4
// capas por responsabilidad de negocio). El PR afirma: "Domain no importa
// ninguna capa, Application no importa Infrastructure ni Api. Verificado
// por búsqueda, cero archivos en ambos casos." Este script automatiza esa
// misma búsqueda para que se pueda repetir en cualquier momento futuro,
// no solo confiar en la afirmación del PR.
//
// No aplica pytest ni Playwright (no es código Python ni hay UI) — es una
// regla arquitectónica de un backend .NET, así que un script que grep-ea
// los usings reales es la herramienta correcta. La regresión funcional
// (que nada se rompió al mover 222 archivos) se corre por separado con
// xUnit vía el arnés de Docker de la HU-01.1.
//
// Uso: node tests/QA/hu-01.7-reorganizacion-4-capas/verify-fronteras.mjs

import { execSync } from "node:child_process";

let ok = true;

function archivosQueImportan(carpeta, patrones) {
  const patronGrep = patrones.join("\\|");
  try {
    // grep sale con código 1 (sin error) cuando no hay coincidencias — ese
    // caso cae al catch de abajo y se interpreta correctamente como "ninguno".
    const salida = execSync(`grep -rl "${patronGrep}" "${carpeta}"`, {
      encoding: "utf8",
      stdio: ["pipe", "pipe", "pipe"],
    }).trim();
    return salida.length > 0 ? salida.split("\n") : [];
  } catch (err) {
    if (err.status === 1) return []; // sin coincidencias
    throw err; // error real (código 2, carpeta inexistente, etc.)
  }
}

const domainViolando = archivosQueImportan("src/CabinaFletes.Domain/", [
  "using CabinaFletes.Application",
  "using CabinaFletes.Infrastructure",
  "using CabinaFletes.Api",
]);
if (domainViolando.length > 0) {
  ok = false;
  console.error("FALLÓ: Domain importa otra capa en:");
  domainViolando.forEach((f) => console.error(`  - ${f}`));
} else {
  console.log("OK: Domain no importa ninguna otra capa (0 archivos).");
}

const applicationViolando = archivosQueImportan("src/CabinaFletes.Application/", [
  "using CabinaFletes.Infrastructure",
  "using CabinaFletes.Api",
]);
if (applicationViolando.length > 0) {
  ok = false;
  console.error("FALLÓ: Application importa Infrastructure o Api en:");
  applicationViolando.forEach((f) => console.error(`  - ${f}`));
} else {
  console.log("OK: Application no importa Infrastructure ni Api (0 archivos).");
}

process.exit(ok ? 0 : 1);
