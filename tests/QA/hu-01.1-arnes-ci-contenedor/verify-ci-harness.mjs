// Verificación QA de la issue #1 / PR #57 (S2-HU-01.1 — arnés de compilación
// en contenedor). No usa Playwright ni pytest porque no aplican aquí: no hay
// UI que recorrer ni código Python — lo que se agrega es un arnés de build
// en Docker, así que se verifica ejecutándolo de verdad y confirmando el
// código de salida, igual que lo hará el futuro workflow de CI (issue #4).
//
// Uso: node tests/QA/hu-01.1-arnes-ci-contenedor/verify-ci-harness.mjs
// (desde la raíz del repo, con Docker Desktop corriendo)

import { execSync } from "node:child_process";

function run(cmd) {
  console.log(`\n$ ${cmd}`);
  execSync(cmd, { stdio: "inherit" });
}

function runCapture(cmd) {
  return execSync(cmd, { encoding: "utf8" });
}

let ok = true;

try {
  run("docker compose -f docker-compose.ci.yml build");
} catch {
  console.error("\nFALLÓ: la imagen del arnés de CI no construyó.");
  ok = false;
}

if (ok) {
  try {
    run("docker compose -f docker-compose.ci.yml run --rm build");
    console.log(
      "\nOK: el arnés compiló las 4 capas + los 3 proyectos de test con código de salida 0."
    );
  } catch {
    console.error(
      "\nFALLÓ: dotnet build dentro del contenedor no terminó en 0 (ver salida arriba)."
    );
    ok = false;
  }
}

// Verificación adicional (no exigida por la issue #1, pero útil): confirma
// que Domain.Tests y Application.Tests también corren dentro del contenedor
// Linux, sin depender del SDK del host ni de Windows Smart App Control.
if (ok) {
  const proyectos = [
    "tests/Nexo.Domain.Tests",
    "tests/Nexo.Application.Tests",
  ];
  for (const proyecto of proyectos) {
    try {
      const salida = runCapture(
        `docker compose -f docker-compose.ci.yml run --rm build dotnet test ${proyecto} --logger "console;verbosity=minimal"`
      );
      const resumen = salida
        .split("\n")
        .find((l) => l.includes("Passed!") || l.includes("Failed!"));
      console.log(`\n${proyecto}: ${resumen ?? "(sin resumen reconocible)"}`);
    } catch (err) {
      const salida = err.stdout?.toString() ?? "";
      const resumen = salida
        .split("\n")
        .find((l) => l.includes("Passed!") || l.includes("Failed!"));
      console.log(`\n${proyecto}: ${resumen ?? "(ver salida completa abajo)"}`);
      if (!resumen) console.log(salida);
    }
  }
}

try {
  execSync("docker compose -f docker-compose.ci.yml down --rmi local -v", {
    stdio: "ignore",
  });
} catch {
  /* limpieza best-effort */
}

process.exit(ok ? 0 : 1);
