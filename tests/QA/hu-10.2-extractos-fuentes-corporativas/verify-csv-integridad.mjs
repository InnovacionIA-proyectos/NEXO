// Verificación QA — S2-HU-10.2 (issue #36, PR #87): integridad estructural de los 4
// extractos CSV, con un parser CSV real (respeta comillas y saltos de línea
// embebidos en campos entrecomillados) — a diferencia de un split ingenuo por comas
// (`awk -F','`), que da falsos positivos en cualquier CSV bien formado que traiga
// texto libre con comas.
//
// Uso: node verify-csv-integridad.mjs   (correr desde esta carpeta o cualquier otra).
//
// Nota (HU-10.3, PR #91): estos 4 archivos se reorganizaron de
// docs/extractos-fuentes-corporativas/ a analitica/00-fuentes/fuentes-corporativas/
// — la ruta de abajo se actualizó para seguir esa reubicación.
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const DIR = join(dirname(fileURLToPath(import.meta.url)), "../../../analitica/00-fuentes/fuentes-corporativas");

function parseCsv(text) {
  const rows = [];
  let field = "", row = [], inQuotes = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (inQuotes) {
      if (c === '"') { if (text[i + 1] === '"') { field += '"'; i++; } else inQuotes = false; }
      else field += c;
    } else {
      if (c === '"') inQuotes = true;
      else if (c === ",") { row.push(field); field = ""; }
      else if (c === "\n") { row.push(field); rows.push(row); row = []; field = ""; }
      else if (c === "\r") { /* ignorar */ }
      else field += c;
    }
  }
  if (field.length || row.length) { row.push(field); rows.push(row); }
  return rows.filter((r) => !(r.length === 1 && r[0] === ""));
}

const archivos = [
  ["piso-sicetac-tblRutasSicetac-muestra-202608.csv", 16],
  ["tarifa-plaza-tblSAPTarifas-2022-09-01_a_hoy.csv", 24],
  ["historico-sap-tblSAPTarifasLog-2022-09-01_a_hoy.csv", 20],
  ["catalogo-clientes-BUT000-organizaciones.csv", 10],
];

let todoOk = true;
for (const [archivo, esperado] of archivos) {
  const texto = readFileSync(join(DIR, archivo), "utf8").replace(/^﻿/, "");
  const filas = parseCsv(texto);
  const conteos = {};
  for (const f of filas) conteos[f.length] = (conteos[f.length] || 0) + 1;
  const malas = filas.filter((f) => f.length !== esperado).length;
  const estado = malas === 0 ? "OK" : "MAL FORMADO";
  if (malas > 0) todoOk = false;
  console.log(`${archivo}: ${filas.length} filas totales (incl. encabezado), distribución de columnas ${JSON.stringify(conteos)} -> ${estado}`);
}
console.log(todoOk ? "\nTODO OK: los 4 archivos son CSV válido, ninguna fila con columnas incorrectas." : "\nHay archivos mal formados.");
