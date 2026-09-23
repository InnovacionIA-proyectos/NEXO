// Verificación QA — S2-HU-10.3 (issue #37, PR #91): intenta reproducir el hallazgo
// central del reporte de inconsistencias del PR ("14/15 líneas Sicetac/Tarifario
// coinciden exacto contra la fuente externa, usando VALORSICECONHORA en vez de
// VALOR para SICE-TAC") cruzando cotizacion_lineas -> lineas_solicitud ->
// catalogo_rutas contra el extracto piso-sicetac-...csv.
//
// Resultado esperado y por qué: NO se puede reproducir con los archivos
// versionados en el repo, porque piso-sicetac-...csv es una MUESTRA
// ESTRATIFICADA de 151 filas (126 núcleo + 25 casos borde, ver HU-10.2), no la
// tabla completa de ~2 millones de filas — las rutas reales de las 13
// solicitudes de prueba (Medellín-Bogotá, Bogotá-Barranquilla, etc.) no tienen
// por qué estar en esa muestra. El autor pudo verificar esto porque tenía
// acceso a la tabla completa en el momento de escribir el reporte; nosotros no
// (misma limitación de siempre, issue #3). Este script deja la verificación
// lista para correr contra la tabla completa el día que haya acceso.
import { readFileSync } from "node:fs";

const DATA = "analitica/01-set-final/v1.0.0-piloto/datos";

function parseCsv(text) {
  const rows = [];
  let field = "", row = [], inQuotes = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (inQuotes) { if (c === '"') { if (text[i + 1] === '"') { field += '"'; i++; } else inQuotes = false; } else field += c; }
    else { if (c === '"') inQuotes = true; else if (c === ",") { row.push(field); field = ""; } else if (c === "\n") { row.push(field); rows.push(row); row = []; field = ""; } else if (c === "\r") { } else field += c; }
  }
  if (field.length || row.length) { row.push(field); rows.push(row); }
  return rows.filter((r) => !(r.length === 1 && r[0] === ""));
}
function loadCsv(file) {
  const text = readFileSync(file, "utf8").replace(/^﻿/, "");
  const rows = parseCsv(text);
  const header = rows[0];
  const dataRows = rows.slice(1).filter((r) => !r[0].startsWith("--"));
  return dataRows.map((r) => Object.fromEntries(header.map((h, i) => [h, r[i]])));
}

const cotLineas = loadCsv(`${DATA}/bd-propia/cotizacion_lineas.csv`);
const lineasSol = loadCsv(`${DATA}/bd-propia/lineas_solicitud.csv`);
const rutas = loadCsv(`${DATA}/bd-propia/catalogo_rutas.csv`);
const piso = loadCsv(`${DATA}/fuentes-corporativas/piso-sicetac-tblRutasSicetac-muestra-202608.csv`);

const lineaById = Object.fromEntries(lineasSol.map((l) => [l.Id, l]));
const rutaById = Object.fromEntries(rutas.map((r) => [r.Id, r]));
const norm = (s) => (s || "").toUpperCase().normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/\s*\([^)]*\)/g, "").trim();

let ok = 0, bad = 0;
const detalle = [];
for (const cl of cotLineas) {
  if (cl.Fuente !== "Sicetac") continue;
  const linea = lineaById[cl.LineaSolicitudId];
  const ruta = linea && rutaById[linea.RutaId];
  if (!ruta) continue;
  const origen = norm(ruta.Origen), destino = norm(ruta.Destino);
  const valor = parseFloat(cl.ValorFlete);
  const filasRuta = piso.filter((p) => {
    const nr = norm(p.NOMBRERUTA);
    return nr === `${origen} - ${destino}` || nr === `${destino} - ${origen}`;
  });
  const coincide = filasRuta.some((p) => Math.abs(parseFloat(p.VALORSICECONHORA) - valor) < 1);
  detalle.push({ ruta: `${ruta.Origen} -> ${ruta.Destino}`, valor, filasEnMuestra: filasRuta.length, coincide });
  coincide ? ok++ : bad++;
}
console.table(detalle);
console.log(`\nResultado: ${ok} coinciden contra la MUESTRA de 151 filas, ${bad} no.`);
console.log("Interpretación esperada: 0 coincidencias, porque ninguna de las rutas reales cae en la muestra estratificada (esto NO contradice el 14/15 del reporte, que se validó contra la tabla completa de ~2M filas, sin acceso aquí).");
