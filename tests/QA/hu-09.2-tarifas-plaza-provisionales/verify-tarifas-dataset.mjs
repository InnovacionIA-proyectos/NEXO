// Verificación QA independiente del dataset de tarifas de plaza provisionales
// (HU-09.2, issue #32, PR #74). No repite lo que ya prueba
// TarifasPlazaProvisionalesDatasetTests.cs (conteos por categoría, forma del JSON) -
// cruza afirmaciones concretas del README contra el contenido real del archivo.
import fs from "fs";

const RUTA = "docs/tarifas-plaza-provisionales/dataset-tarifas-plaza-provisionales.json";
const HOY = new Date("2026-08-27T00:00:00Z"); // fecha de extracción documentada en el README

const d = JSON.parse(fs.readFileSync(RUTA, "utf8"));
let ok = true;
const fallo = (msg) => { console.error(`FALLÓ: ${msg}`); ok = false; };
const paso = (msg) => console.log(`OK: ${msg}`);

// 1) Totales
if (d.nucleo.length !== 150) fallo(`núcleo tiene ${d.nucleo.length} filas, esperaba 150`);
else paso("núcleo = 150 filas");

const totalCasosBorde = Object.values(d.casosBorde).reduce((acc, arr) => acc + arr.length, 0);
if (totalCasosBorde !== 60) fallo(`casos borde suman ${totalCasosBorde}, esperaba 60`);
else paso("casos borde = 60 filas (suma de las 6 categorías)");

// 2) sourceId único entre núcleo + todas las categorías que lo traen (sinPlaza no
//    tiene sourceId -- es ausencia real de fila, no una fila con id).
const idsConSourceId = [
  ...d.nucleo,
  ...d.casosBorde.vigenciaVencida,
  ...d.casosBorde.vigenciaAbsurda,
  ...d.casosBorde.valorVacio,
  ...d.casosBorde.valorMaximo,
  ...d.casosBorde.valorMinimo,
].map((l) => l.sourceId);
const idsUnicos = new Set(idsConSourceId);
if (idsUnicos.size !== idsConSourceId.length)
  fallo(`hay sourceId duplicados: ${idsConSourceId.length} filas, ${idsUnicos.size} ids únicos`);
else paso(`${idsConSourceId.length} filas con sourceId, todas con Id real único (sin duplicados ni filas inventadas repetidas)`);

// 3) vigenciaVencida: las 30 realmente vencidas a la fecha de extracción, y el
//    subCaso se reparte 15/15 tal como documenta el README.
const vencidas = d.casosBorde.vigenciaVencida;
const noVencidas = vencidas.filter((l) => new Date(l.vigencia + "T00:00:00Z") >= HOY);
if (noVencidas.length > 0)
  fallo(`${noVencidas.length} filas de "vigenciaVencida" NO están vencidas al 2026-08-27: ${noVencidas.map(l => l.sourceId).join(",")}`);
else paso("las 30 filas de vigenciaVencida tienen Vigencia < 2026-08-27 (realmente vencidas)");

const recientes = vencidas.filter((l) => l.subCaso === "recien_vencida").length;
const antiguas = vencidas.filter((l) => l.subCaso === "vencida_hace_mucho").length;
if (recientes !== 15 || antiguas !== 15)
  fallo(`subCaso no reparte 15/15: recien_vencida=${recientes}, vencida_hace_mucho=${antiguas}`);
else paso("subCaso de vigenciaVencida reparte exacto 15 recien_vencida / 15 vencida_hace_mucho");

// 4) vigenciaAbsurda: año 3999 exacto, tal como afirma el README (no solo "> 2030").
const anios = d.casosBorde.vigenciaAbsurda.map((l) => l.vigencia.slice(0, 4));
if (!anios.every((a) => a === "3999"))
  fallo(`vigenciaAbsurda no es todo año 3999: ${anios.join(",")}`);
else paso("las 2 filas de vigenciaAbsurda son año 3999 exacto (coincide con el README)");

// 5) valorVacio: las 3 son de línea "Devolución Contenedor", tal como afirma el README.
const vacias = d.casosBorde.valorVacio;
const noDevolucion = vacias.filter((l) => !/Devoluci.n Contenedor/i.test(l.lineaNegocioDescripcion ?? ""));
if (vacias.length !== 3) fallo(`valorVacio tiene ${vacias.length} filas, esperaba 3`);
else if (noDevolucion.length > 0) fallo(`valorVacio trae filas que no son de Devolución Contenedor: ${noDevolucion.map(l => l.sourceId).join(",")}`);
else paso("las 3 filas de valorVacio son de línea 'Devolución Contenedor' (coincide con el README)");

// 6) Valor máximo/mínimo reales coinciden con lo que afirma el README ($18.000.000 / $100.000).
const maxValor = Math.max(...d.casosBorde.valorMaximo.map((l) => l.valor));
const minValor = Math.min(...d.casosBorde.valorMinimo.map((l) => l.valor));
if (maxValor !== 18_000_000) fallo(`valor máximo real es ${maxValor}, README dice 18.000.000`);
else paso("valor máximo real = $18.000.000 (coincide con el README)");
if (minValor !== 100_000) fallo(`valor mínimo real es ${minValor}, README dice 100.000`);
else paso("valor mínimo real = $100.000 (coincide con el README)");

// 7) sinPlaza: ninguna trae campo "valor" (ausencia real, no un null), todas con
//    frecuencia histórica > 0, y la más frecuente es Cúcuta<->Buenaventura con 59
//    apariciones, tal como afirma el README.
const sinPlaza = d.casosBorde.sinPlaza;
const conValor = sinPlaza.filter((l) => "valor" in l);
if (conValor.length > 0) fallo(`${conValor.length} filas de sinPlaza traen campo "valor" (no deberían)`);
else paso("las 15 filas de sinPlaza no traen campo 'valor' (ausencia real de tarifa, no un null)");

const masFrecuente = sinPlaza.reduce((a, b) => (b.frecuenciaHistorica > a.frecuenciaHistorica ? b : a));
const esCucutaBuenaventura =
  /CUCUTA/i.test(masFrecuente.origen) && /BUENAVENTURA/i.test(masFrecuente.destino) && masFrecuente.frecuenciaHistorica === 59;
if (!esCucutaBuenaventura)
  fallo(`la ruta "sin plaza" más frecuente es ${masFrecuente.origen}->${masFrecuente.destino} (${masFrecuente.frecuenciaHistorica}), README dice Cúcuta->Buenaventura (59)`);
else paso("la ruta 'sin plaza' más frecuente es Cúcuta->Buenaventura con 59 apariciones (coincide con el README)");

console.log(ok ? "\nTODO OK." : "\nHAY FALLAS.");
process.exit(ok ? 0 : 1);
